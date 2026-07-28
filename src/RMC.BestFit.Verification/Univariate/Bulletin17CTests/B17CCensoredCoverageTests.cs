using Numerics;
using Numerics.Distributions;
using Numerics.Sampling;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>
/// Tests confidence interval coverage for censored data scenarios.
/// Validates that the Linked MVN link functions produce adequate CIs when the data
/// includes low outliers, historical thresholds, or both.
/// </summary>
/// <remarks>
/// <para>
/// Motivated by Cohn's EMA CI paper which shows that censoring modifies the Fisher
/// information matrix, potentially changing the parameter covariance structure in ways
/// not captured by the uncensored BCa calibration experiments.
/// </para>
/// <para>
/// Uses existing infrastructure:
/// <list type="bullet">
///     <item><description><c>DataFrame.BootstrapDataFrame()</c> properly resamples censored data
///     (binomial resampling for threshold exceedance counts).</description></item>
///     <item><description><c>Bulletin17CAnalysis.AccelerationConstants()</c> uses
///     <c>DataFrame.JackKnife()</c> which handles all data types.</description></item>
/// </list>
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CCensoredCoverageTests
{
    #region LP3 Low Outlier Tests

    /// <summary>
    /// LP3 with low outliers: 50 systematic observations, bottom ~10% flagged as low outliers.
    /// The low outlier threshold is set at the true Q_0.10.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Expectation: CensoringAsymmetryScore should detect the left-censoring and trigger
    /// CenteredLink for the location parameter. Coverage should remain adequate.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_LowOutliers_N50_LinkedMVN()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.2);
        double lowOutlierThreshold = trueDist.InverseCDF(0.10);

        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCensoredCoverageSimulation(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 50,
            lowOutlierThreshold: lowOutlierThreshold,
            uncertaintyMethod: UncertaintyMethod.LinkedMultivariateNormal);

        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LP3 LowOutliers LinkedMVN N=50");
    }

    /// <summary>
    /// LP3 with low outliers using the parametric bootstrap method for comparison.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_LowOutliers_N50_Bootstrap()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.2);
        double lowOutlierThreshold = trueDist.InverseCDF(0.10);

        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCensoredCoverageSimulation(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 50,
            lowOutlierThreshold: lowOutlierThreshold,
            uncertaintyMethod: UncertaintyMethod.Bootstrap);

        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LP3 LowOutliers Bootstrap N=50");
    }

    #endregion

    #region LP3 Historical Threshold Tests

    /// <summary>
    /// LP3 with historical threshold data: 50 systematic observations + 150-year historical
    /// period with a perception threshold at the true Q_0.98 (approximately 3 expected exceedances).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Historical data adds upper-tail information, which should improve the upper-tail CIs.
    /// The threshold data uses Duration=150, with the true number of exceedances drawn from
    /// the true distribution. This tests the interaction of ThresholdSeries with linked MVN.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_HistoricalThreshold_N50_LinkedMVN()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.5);
        double historicalThreshold = trueDist.InverseCDF(0.9);

        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCensoredCoverageSimulation(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 100,
            historicalThreshold: historicalThreshold,
            historicalDuration: 50,
            uncertaintyMethod: UncertaintyMethod.LinkedMultivariateNormal);

        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LP3 HistThreshold LinkedMVN N=50");
    }

    /// <summary>
    /// LP3 with historical threshold using parametric bootstrap for comparison.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_HistoricalThreshold_N50_Bootstrap()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.2);
        double historicalThreshold = trueDist.InverseCDF(0.98);

        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCensoredCoverageSimulation(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 50,
            historicalThreshold: historicalThreshold,
            historicalDuration: 150,
            uncertaintyMethod: UncertaintyMethod.Bootstrap);

        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LP3 HistThreshold Bootstrap N=50");
    }

    #endregion

    #region LP3 Combined Censoring Tests

    /// <summary>
    /// LP3 with both low outliers and historical threshold — the most realistic censoring scenario.
    /// 50 systematic observations with low outlier threshold at Q_0.10 and
    /// historical perception threshold at Q_0.98 over 150 years.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_CombinedCensoring_N50_LinkedMVN()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.2);
        double lowOutlierThreshold = trueDist.InverseCDF(0.10);
        double historicalThreshold = trueDist.InverseCDF(0.98);

        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCensoredCoverageSimulation(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 50,
            lowOutlierThreshold: lowOutlierThreshold,
            historicalThreshold: historicalThreshold,
            historicalDuration: 150,
            uncertaintyMethod: UncertaintyMethod.LinkedMultivariateNormal);

        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LP3 Combined LinkedMVN N=50");
    }

    /// <summary>
    /// LP3 combined censoring with parametric bootstrap for comparison.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_CombinedCensoring_N50_Bootstrap()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.2);
        double lowOutlierThreshold = trueDist.InverseCDF(0.10);
        double historicalThreshold = trueDist.InverseCDF(0.98);

        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCensoredCoverageSimulation(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 50,
            lowOutlierThreshold: lowOutlierThreshold,
            historicalThreshold: historicalThreshold,
            historicalDuration: 150,
            uncertaintyMethod: UncertaintyMethod.Bootstrap);

        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LP3 Combined Bootstrap N=50");
    }

    #endregion

    #region Normal Censoring Tests

    /// <summary>
    /// Normal distribution with low outlier censoring to isolate the censoring effect
    /// from skewness effects.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task Normal_LowOutliers_N50_LinkedMVN()
    {
        var trueDist = new Normal(100.0, 15.0);
        double lowOutlierThreshold = trueDist.InverseCDF(0.20); // 20% censoring

        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCensoredCoverageSimulation(
            UnivariateDistributionType.Normal, trueDist, n: 50,
            lowOutlierThreshold: lowOutlierThreshold,
            uncertaintyMethod: UncertaintyMethod.LinkedMultivariateNormal);

        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "Normal LowOutliers LinkedMVN N=50");
    }

    #endregion

    #region LP3 Censored Diagnostic Grid

    /// <summary>
    /// Comprehensive diagnostic grid: LP3 with gamma × sigma × censoring type.
    /// Outputs CSV with coverage, CI asymmetry, CensoringAsymmetryScore, parameter bias, and RMSE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs 84 cells: 7 gamma × 3 sigma × 4 censoring types, with B=200 MC replicates each.
    /// Primary uncertainty method: LinkedMultivariateNormal.
    /// </para>
    /// <para>
    /// This diagnostic test produces a CSV table via Debug.WriteLine for offline analysis
    /// of link function behavior under censoring. It does not assert pass/fail — instead it
    /// provides the empirical data needed to evaluate whether link parameters need
    /// censoring-specific adjustment.
    /// </para>
    /// <para>
    /// Metrics per cell: coverage at 5 AEPs, miss pattern at AEP≈0.002,
    /// log10-space CI asymmetry ratio, CensoringAsymmetryScore per parameter,
    /// CenteredLink trigger rate, parameter bias and RMSE.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_CensoredGrid_Diagnostics()
    {
        // Grid parameters
        double mu = 3.0;
        double[] gammaValues = { -1.0, -0.5, 0.0, 0.5, 1.0 };
        double[] sigmaValues = { 0.2, 0.5, 0.8 };
        string[] censoringLabels = { "None", "LowOutlier", "Historical", "Combined" };
        int B = 200;
        double nominalCI = 0.90;
        int nSystematic = 50;
        int outputLength = 1000; // Number of LinkedMVN parameter draws per replicate

        // Target AEPs for detailed coverage reporting
        double[] targetAEPs = { 0.50, 0.10, 0.01, 0.002, 0.001 };

        // Get default ProbabilityOrdinates from a template analysis
        var templateDf = new DataFrame();
        templateDf.ExactSeries = new ExactSeries(new double[] { 1000.0 });
        var templateDist = new Bulletin17CDistribution(templateDf, UnivariateDistributionType.LogPearsonTypeIII);
        var templateAnalysis = new Bulletin17CAnalysis(templateDist);
        var probOrdinates = templateAnalysis.ProbabilityOrdinates.ToArray();
        int nOrdinates = probOrdinates.Length;

        // Find closest ordinate indices for target AEPs
        int[] targetIndices = FindClosestIndices(probOrdinates, targetAEPs);
        int rLogQIndex = targetIndices[3]; // AEP ≈ 0.002

        // Accumulate all CSV rows; output at the very end to avoid console clutter from exceptions
        string csvHeader =
            "gamma,sigma,censoring,success_rate," +
            "cov_0.50,cov_0.10,cov_0.01,cov_0.002,cov_0.001,mean_cov," +
            "miss_below_0.002,miss_above_0.002,R_logQ_linked," +
            "CAS_mu_mean,CAS_mu_sd,CAS_sig_mean,CAS_sig_sd,CAS_gam_mean,CAS_gam_sd," +
            "centered_link_rate,bias_mu,bias_sig,bias_gam,rmse_mu,rmse_sig,rmse_gam";
        var csvRows = new List<string>();

        for (int gi = 0; gi < gammaValues.Length; gi++)
        {
            for (int si = 0; si < sigmaValues.Length; si++)
            {
                double gamma = gammaValues[gi];
                double sigma = sigmaValues[si];
                var trueDist = new LogPearsonTypeIII(mu, sigma, gamma);

                // Compute true quantiles at all ordinates (non-exceedance prob = 1 - AEP)
                var trueQ = new double[nOrdinates];
                for (int j = 0; j < nOrdinates; j++)
                    trueQ[j] = trueDist.InverseCDF(1.0 - probOrdinates[j]);

                // Censoring thresholds from true distribution
                double lowOutlierThreshold = trueDist.InverseCDF(0.10);
                double historicalThreshold = trueDist.InverseCDF(0.98);

                for (int ci = 0; ci < censoringLabels.Length; ci++)
                {
                    string cLabel = censoringLabels[ci];

                    // Reproducible seeds per cell
                    int masterSeed = 10000 * gi + 100 * si + ci;
                    var masterPRNG = new MersenneTwister(masterSeed);
                    var seeds = masterPRNG.NextIntegers(B);

                    // Accumulators
                    int successCount = 0;
                    var coverage = new double[nOrdinates];
                    var missAbove = new double[nOrdinates];
                    var missBelow = new double[nOrdinates];
                    var rLogQList = new List<double>(B);
                    var casMuList = new List<double>(B);
                    var casSigList = new List<double>(B);
                    var casGamList = new List<double>(B);
                    int centeredLinkCount = 0;
                    var biasMuList = new List<double>(B);
                    var biasSigList = new List<double>(B);
                    var biasGamList = new List<double>(B);

                    for (int rep = 0; rep < B; rep++)
                    {
                        try
                        {
                            // Determine censoring configuration:
                            // ci=0: None, ci=1: LowOutlier, ci=2: Historical, ci=3: Combined
                            double? loThresh = (ci == 1 || ci == 3) ? lowOutlierThreshold : (double?)null;
                            double? hiThresh = (ci == 2 || ci == 3) ? historicalThreshold : (double?)null;

                            var df = CreateCensoredDataFrame(trueDist, nSystematic, seeds[rep],
                                loThresh, hiThresh, 150);

                            var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
                            var analysis = new Bulletin17CAnalysis(model)
                            {
                                UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
                            };
                            analysis.BayesianAnalysis.CredibleIntervalWidth = nominalCI;
                            analysis.BayesianAnalysis.OutputLength = outputLength;

                            await analysis.RunAsync();

                            var CIs = analysis.AnalysisResults?.ConfidenceIntervals;
                            var modeCurve = analysis.AnalysisResults?.ModeCurve;
                            if (CIs == null || modeCurve == null || analysis.GMM == null)
                                continue;

                            successCount++;

                            // Coverage at all ordinates
                            for (int j = 0; j < nOrdinates; j++)
                            {
                                if (trueQ[j] >= CIs[j, 0] && trueQ[j] <= CIs[j, 1])
                                    coverage[j]++;
                                else if (trueQ[j] > CIs[j, 1])
                                    missAbove[j]++;
                                else
                                    missBelow[j]++;
                            }

                            // R_logQ at AEP ≈ 0.002
                            double ciLo = CIs[rLogQIndex, 0];
                            double ciHi = CIs[rLogQIndex, 1];
                            double mode = modeCurve[rLogQIndex];
                            if (ciLo > 0 && ciHi > 0 && mode > 0)
                            {
                                double logMode = Math.Log10(mode);
                                double logLo = Math.Log10(ciLo);
                                double logHi = Math.Log10(ciHi);
                                double denom = logMode - logLo;
                                if (Math.Abs(denom) > 1e-12)
                                    rLogQList.Add((logHi - logMode) / denom);
                            }

                            // CensoringAsymmetryScore
                            var parms = analysis.GMM.BestParameterSet.Values;
                            var cas = model.CensoringAsymmetryScore(parms);
                            casMuList.Add(cas[0]);
                            casSigList.Add(cas[1]);
                            casGamList.Add(cas[2]);
                            if (Math.Abs(cas[0]) > 0.025)
                                centeredLinkCount++;

                            // Parameter bias (LP3 params: [mu, sigma, gamma])
                            biasMuList.Add(parms[0] - mu);
                            biasSigList.Add(parms[1] - sigma);
                            biasGamList.Add(parms[2] - gamma);
                        }
                        catch
                        {
                            // Silently skip failed replicates; failure rate captured in success_rate column
                        }
                    }

                    // Compute cell averages and accumulate CSV row
                    csvRows.Add(FormatGridCellCSV(gamma, sigma, cLabel, successCount, B,
                        coverage, missAbove, missBelow, nOrdinates, targetIndices,
                        rLogQList, casMuList, casSigList, casGamList, centeredLinkCount,
                        biasMuList, biasSigList, biasGamList));
                }
            }
        }

        // Output all results at the end for clean console output
        Debug.WriteLine(csvHeader);
        foreach (var row in csvRows)
            Debug.WriteLine(row);
    }

    /// <summary>
    /// Computes cell averages from accumulated replicates and returns one CSV row string.
    /// </summary>
    /// <param name="gamma">True gamma value for this cell.</param>
    /// <param name="sigma">True sigma value for this cell.</param>
    /// <param name="cLabel">Censoring type label.</param>
    /// <param name="successCount">Number of successful replicates.</param>
    /// <param name="totalReps">Total replicates attempted.</param>
    /// <param name="coverage">Raw coverage counts per ordinate.</param>
    /// <param name="missAbove">Raw miss-above counts per ordinate.</param>
    /// <param name="missBelow">Raw miss-below counts per ordinate.</param>
    /// <param name="nOrdinates">Number of probability ordinates.</param>
    /// <param name="targetIndices">Indices of the 5 target AEPs in the ordinate array.</param>
    /// <param name="rLogQList">Accumulated R_logQ values from successful replicates.</param>
    /// <param name="casMuList">Accumulated CAS[mu] values.</param>
    /// <param name="casSigList">Accumulated CAS[sigma] values.</param>
    /// <param name="casGamList">Accumulated CAS[gamma] values.</param>
    /// <param name="centeredLinkCount">Number of replicates where CenteredLink was triggered.</param>
    /// <param name="biasMuList">Accumulated mu bias values.</param>
    /// <param name="biasSigList">Accumulated sigma bias values.</param>
    /// <param name="biasGamList">Accumulated gamma bias values.</param>
    /// <returns>A CSV-formatted row string for this cell.</returns>
    private static string FormatGridCellCSV(
        double gamma, double sigma, string cLabel,
        int successCount, int totalReps,
        double[] coverage, double[] missAbove, double[] missBelow,
        int nOrdinates, int[] targetIndices,
        List<double> rLogQList,
        List<double> casMuList, List<double> casSigList, List<double> casGamList,
        int centeredLinkCount,
        List<double> biasMuList, List<double> biasSigList, List<double> biasGamList)
    {
        if (successCount == 0)
            return $"{gamma:F2},{sigma:F2},{cLabel},0.000,,,,,,,,,,,,,,,,,,,,,,,";

        double successRate = (double)successCount / totalReps;

        // Normalize coverage and miss rates
        for (int j = 0; j < nOrdinates; j++)
        {
            coverage[j] /= successCount;
            missAbove[j] /= successCount;
            missBelow[j] /= successCount;
        }

        double meanCov = coverage.Average();
        double rLogQ = rLogQList.Count > 0 ? rLogQList.Average() : double.NaN;

        double casMuMean = casMuList.Average();
        double casMuSd = casMuList.Count > 1 ? SampleStdDev(casMuList) : 0;
        double casSigMean = casSigList.Average();
        double casSigSd = casSigList.Count > 1 ? SampleStdDev(casSigList) : 0;
        double casGamMean = casGamList.Average();
        double casGamSd = casGamList.Count > 1 ? SampleStdDev(casGamList) : 0;
        double centeredRate = (double)centeredLinkCount / successCount;

        double biasMu = biasMuList.Average();
        double biasSig = biasSigList.Average();
        double biasGam = biasGamList.Average();
        double rmseMu = Math.Sqrt(biasMuList.Average(x => x * x));
        double rmseSig = Math.Sqrt(biasSigList.Average(x => x * x));
        double rmseGam = Math.Sqrt(biasGamList.Average(x => x * x));

        return
            $"{gamma:F2},{sigma:F2},{cLabel},{successRate:F3}," +
            $"{coverage[targetIndices[0]]:F3},{coverage[targetIndices[1]]:F3},{coverage[targetIndices[2]]:F3}," +
            $"{coverage[targetIndices[3]]:F3},{coverage[targetIndices[4]]:F3},{meanCov:F3}," +
            $"{missBelow[targetIndices[3]]:F3},{missAbove[targetIndices[3]]:F3},{rLogQ:F4}," +
            $"{casMuMean:F4},{casMuSd:F4},{casSigMean:F4},{casSigSd:F4},{casGamMean:F4},{casGamSd:F4}," +
            $"{centeredRate:F3},{biasMu:F4},{biasSig:F4},{biasGam:F4}," +
            $"{rmseMu:F4},{rmseSig:F4},{rmseGam:F4}";
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Runs a Monte Carlo coverage simulation with censored data.
    /// </summary>
    /// <param name="distributionType">The B17C-supported distribution type.</param>
    /// <param name="trueDist">The Numerics distribution with true parameters set.</param>
    /// <param name="n">The systematic sample size for each replicate.</param>
    /// <param name="lowOutlierThreshold">If non-null, observations below this value are flagged as low outliers.</param>
    /// <param name="historicalThreshold">If non-null, adds a historical threshold data record.</param>
    /// <param name="historicalDuration">Duration of the historical period (years).</param>
    /// <param name="uncertaintyMethod">The uncertainty method to test.</param>
    /// <param name="B">Number of Monte Carlo replicates.</param>
    /// <param name="nominalCI">Nominal credible interval width.</param>
    /// <param name="masterSeed">Master PRNG seed.</param>
    /// <returns>Coverage rates, miss rates, success count, and probability ordinates.</returns>
    private static async Task<(double[] Coverage, double[] MissAbove, double[] MissBelow, int SuccessCount, double[] Probabilities)>
        RunCensoredCoverageSimulation(
            UnivariateDistributionType distributionType,
            UnivariateDistributionBase trueDist,
            int n,
            double? lowOutlierThreshold = null,
            double? historicalThreshold = null,
            int historicalDuration = 150,
            UncertaintyMethod uncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal,
            int B = 1000,
            double nominalCI = 0.90,
            int masterSeed = 12345)
    {
        // Create a template analysis to get the default probability ordinates
        var templateDf = new DataFrame();
        templateDf.ExactSeries = new ExactSeries(trueDist.InverseCDF(new double[] { 0.5 }));
        var templateDist = new Bulletin17CDistribution(templateDf, distributionType);
        var templateAnalysis = new Bulletin17CAnalysis(templateDist);

        var probabilities = templateAnalysis.ProbabilityOrdinates.Select(p => 1.0 - p).ToArray();
        var coverage = new double[probabilities.Length];
        var missAbove = new double[probabilities.Length];
        var missBelow = new double[probabilities.Length];
        var trueQuantiles = trueDist.InverseCDF(probabilities);

        var masterPRNG = new MersenneTwister(masterSeed);
        var seeds = masterPRNG.NextIntegers(B);

        int successCount = 0;

        for (int i = 0; i < B; i++)
        {
            try
            {
                var df = CreateCensoredDataFrame(trueDist, n, seeds[i],
                    lowOutlierThreshold, historicalThreshold, historicalDuration);

                var model = new Bulletin17CDistribution(df, distributionType);
                var analysis = new Bulletin17CAnalysis(model)
                {
                    UncertaintyMethod = uncertaintyMethod
                };
                analysis.BayesianAnalysis.CredibleIntervalWidth = nominalCI;
                analysis.BayesianAnalysis.OutputLength = B;

                await analysis.RunAsync();

                var CIs = analysis.AnalysisResults?.ConfidenceIntervals;
                if (CIs == null) continue;

                successCount++;
                for (int j = 0; j < probabilities.Length; j++)
                {
                    if (trueQuantiles[j] >= CIs[j, 0] && trueQuantiles[j] <= CIs[j, 1])
                    {
                        coverage[j]++;
                    }
                    else if (trueQuantiles[j] > CIs[j, 1])
                    {
                        missAbove[j]++;
                    }
                    else
                    {
                        missBelow[j]++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Replicate {i} failed: {ex.Message}");
            }
        }

        // Normalize by success count
        if (successCount > 0)
        {
            for (int j = 0; j < probabilities.Length; j++)
            {
                coverage[j] /= successCount;
                missAbove[j] /= successCount;
                missBelow[j] /= successCount;
            }
        }

        return (coverage, missAbove, missBelow, successCount, probabilities);
    }

    /// <summary>
    /// Creates a DataFrame with censored observations from a known true distribution.
    /// </summary>
    /// <param name="trueDist">The true distribution to sample from.</param>
    /// <param name="n">Systematic sample size.</param>
    /// <param name="seed">RNG seed for this replicate.</param>
    /// <param name="lowOutlierThreshold">If non-null, values below this are marked as low outliers.</param>
    /// <param name="historicalThreshold">If non-null, adds a historical threshold record.</param>
    /// <param name="historicalDuration">Duration of the historical period.</param>
    /// <returns>A DataFrame with exact and optionally censored data.</returns>
    private static DataFrame CreateCensoredDataFrame(
        UnivariateDistributionBase trueDist,
        int n,
        int seed,
        double? lowOutlierThreshold,
        double? historicalThreshold,
        int historicalDuration)
    {
        var prng = new MersenneTwister(seed);
        var df = new DataFrame();

        // Suppress events during construction for speed
        df.ExactSeries.SuppressCollectionChanged = true;
        df.ThresholdSeries.SuppressCollectionChanged = true;

        // Generate systematic exact observations
        var values = trueDist.GenerateRandomValues(n, prng.Next());
        for (int i = 0; i < n; i++)
        {
            df.ExactSeries.Add(new ExactData(i + 1, values[i]));
        }

        // Apply low outlier threshold if specified
        if (lowOutlierThreshold.HasValue)
        {
            df.LowOutlierThreshold = lowOutlierThreshold.Value;
            df.SetLowOutliersFromThreshold();
        }

        // Add historical threshold data if specified
        if (historicalThreshold.HasValue)
        {
            // Compute the true exceedance probability at the threshold
            double pExceed = 1.0 - trueDist.CDF(historicalThreshold.Value);

            // Draw the true number of exceedances from Binomial(duration, pExceed)
            var binomial = new Binomial(pExceed, historicalDuration);
            int nAbove = (int)Math.Floor(binomial.InverseCDF(prng.NextDouble()));
            nAbove = Math.Max(0, nAbove); // Safety clamp

            // Historical period starts before the systematic record
            // Systematic record: indices 1..n, historical: indices (1-historicalDuration)..(0)
            int startIdx = 1 - historicalDuration;
            int endIdx = 0;

            var thresholdData = new ThresholdData(startIdx, endIdx, historicalThreshold.Value)
            {
                NumberAbove = nAbove
            };
            df.ThresholdSeries.Add(thresholdData);
        }

        // Un-suppress and process
        df.ExactSeries.SuppressCollectionChanged = false;
        df.ThresholdSeries.SuppressCollectionChanged = false;

        if (df.ThresholdSeries.Count > 0)
            df.ProcessThresholdSeries();

        return df;
    }

    /// <summary>
    /// Finds the indices in <paramref name="ordinates"/> closest to each value in <paramref name="targets"/>.
    /// </summary>
    /// <param name="ordinates">Array of probability ordinates (AEPs).</param>
    /// <param name="targets">Target AEP values to match.</param>
    /// <returns>Array of indices, one per target.</returns>
    private static int[] FindClosestIndices(double[] ordinates, double[] targets)
    {
        int[] indices = new int[targets.Length];
        for (int t = 0; t < targets.Length; t++)
        {
            double minDiff = double.MaxValue;
            for (int j = 0; j < ordinates.Length; j++)
            {
                double diff = Math.Abs(ordinates[j] - targets[t]);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    indices[t] = j;
                }
            }
        }
        return indices;
    }

    /// <summary>
    /// Computes sample standard deviation (Bessel-corrected, n-1 denominator).
    /// </summary>
    /// <param name="values">The values to compute standard deviation for.</param>
    /// <returns>Sample standard deviation, or 0 if fewer than 2 values.</returns>
    private static double SampleStdDev(List<double> values)
    {
        if (values.Count < 2) return 0.0;
        double mean = values.Average();
        double sumSq = values.Sum(x => (x - mean) * (x - mean));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }

    /// <summary>
    /// Asserts that coverage rates are within acceptable bounds and writes a diagnostic summary table.
    /// Same criteria as uncensored B17CCoverageTests.
    /// </summary>
    private static void AssertCoverage(double[] coverage, double[] missAbove, double[] missBelow,
        int successCount, double[] probabilities, string testName)
    {
        Debug.WriteLine($"\n=== Coverage Summary: {testName} (successes={successCount}/1000) ===");
        Debug.WriteLine($"{"AEP",10} {"NonExcProb",12} {"Coverage",10} {"%Below",8} {"%Above",8}");
        Debug.WriteLine(new string('-', 52));
        for (int j = 0; j < probabilities.Length; j++)
        {
            double aep = 1.0 - probabilities[j];
            Debug.WriteLine($"{aep,10:F4} {probabilities[j],12:F4} {coverage[j],10:F3} {missBelow[j],8:F3} {missAbove[j],8:F3}");
        }
        double meanCoverage = coverage.Average();
        double meanBelow = missBelow.Average();
        double meanAbove = missAbove.Average();
        Debug.WriteLine($"{"Mean",10} {"",12} {meanCoverage,10:F3} {meanBelow,8:F3} {meanAbove,8:F3}");

        // Assertions — same bounds as uncensored tests
        Assert.IsTrue(successCount >= 900,
            $"{testName}: Too many failures — {1000 - successCount}/1000 replicates failed.");

        Assert.IsTrue(meanCoverage >= 0.82 && meanCoverage <= 0.97,
            $"{testName}: Mean coverage {meanCoverage:F3} outside [0.82, 0.97].");

        for (int j = 0; j < probabilities.Length; j++)
        {
            Assert.IsTrue(coverage[j] >= 0.70,
                $"{testName}: Coverage at p={probabilities[j]:F4} is {coverage[j]:F3} < 0.70.");
        }
    }

    #endregion
}
