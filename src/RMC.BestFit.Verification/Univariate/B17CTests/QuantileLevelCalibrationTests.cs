using Numerics.Distributions;
using Numerics.Sampling;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Diagnostics;
using System.Text;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// BCa bootstrap calibration at the quantile level for AEP = 0.0001 (10,000-year flood).
/// Measures the asymmetry ratio R_Q of bootstrap quantile CIs and compares against
/// the R_Q produced by the current Linked MVN method.
/// </summary>
/// <remarks>
/// <para>
/// Motivation (from Cohn EMA CI analysis): parameter-level BCa calibration (R_log_sigma, R_gamma)
/// does not fully capture quantile-level CI asymmetry because the mapping Q = mu + sigma * K_gamma(p)
/// introduces additional nonlinearity. At AEP = 0.0001 this nonlinearity is strongest, making it
/// the ideal test point.
/// </para>
/// <para>
/// Expected result: for positive gamma, K_gamma is convex → Q CI is right-skewed → R_Q > 1.
/// For negative gamma, K_gamma is concave → Q CI is left-skewed → R_Q &lt; 1.
/// The asymmetry should flip sign with gamma.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class QuantileLevelCalibrationTests
{
    /// <summary>
    /// Gamma grid matching the parameter-level calibration experiments.
    /// </summary>
    private static readonly double[] Gammas = { -4.0, -3.0, -2.0, -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0 };

    /// <summary>
    /// Single sigma value. Sigma-independence was confirmed in Phase 1 parameter-level experiments.
    /// </summary>
    private const double Sigma = 0.5;

    /// <summary>
    /// Fixed mu for LP3. Results are sigma/mu-independent for normalized diagnostics.
    /// </summary>
    private const double Mu = 3.0;

    /// <summary>
    /// Target AEP. Extreme enough to expose full nonlinear chain.
    /// </summary>
    private const double TargetAEP = 0.0001;

    /// <summary>
    /// Sample size per dataset.
    /// </summary>
    private const int SampleSize = 50;

    /// <summary>
    /// Number of independent datasets per gamma cell for Monte Carlo averaging.
    /// </summary>
    private const int DatasetsPerCell = 30;

    /// <summary>
    /// Number of parametric bootstrap replications per dataset.
    /// </summary>
    private const int BootstrapReplications = 10000;

    #region Main Experiment

    /// <summary>
    /// Quantile-level BCa bootstrap experiment across all gamma values at AEP = 0.0001.
    /// Outputs a CSV table of R_Q (BCa truth), R_Q (from Linked MVN), z0, and acceleration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For each gamma cell, generates M=30 LP3 datasets, computes BCa CIs on the Q(0.0001)
    /// quantile directly, and measures the CI asymmetry ratio. Also runs the Linked MVN method
    /// and measures its quantile asymmetry for comparison.
    /// </para>
    /// <para>
    /// Runtime: ~30-60 minutes (13 gamma cells x 30 datasets x 10K bootstrap + GMM fitting).
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task QuantileBCa_AEP0001_AllGammas()
    {
        var masterRng = new MersenneTwister(54321);
        var csv = new StringBuilder();

        csv.AppendLine("gamma_true,valid_count," +
            "mean_Q_hat,R_Q_BCa,z0_Q,accel_Q," +
            "R_Q_LinkedMVN,R_Q_MVN," +
            "R_logQ_BCa,z0_logQ,accel_logQ," +
            "R_logQ_LinkedMVN,R_logQ_MVN");

        Debug.WriteLine("========== QUANTILE-LEVEL BCa CALIBRATION AT AEP = 0.0001 ==========");
        Debug.WriteLine("");
        Debug.WriteLine("--- Real Q space ---");

        foreach (double gamma in Gammas)
        {
            var cellResult = await RunQuantileCellExperiment(gamma, masterRng);

            csv.AppendLine(
                $"{gamma:F2},{cellResult.ValidCount}," +
                $"{cellResult.MeanQHat:F4},{cellResult.RQBCa:F4}," +
                $"{cellResult.Z0Q:F4},{cellResult.AccelQ:F6}," +
                $"{cellResult.RQLinkedMVN:F4},{cellResult.RQMVN:F4}," +
                $"{cellResult.RLogQBCa:F4},{cellResult.Z0LogQ:F4},{cellResult.AccelLogQ:F6}," +
                $"{cellResult.RLogQLinkedMVN:F4},{cellResult.RLogQMVN:F4}");

            Debug.WriteLine($"gamma={gamma,5:F1}: R_Q_BCa={cellResult.RQBCa,12:G6}  R_Q_Linked={cellResult.RQLinkedMVN,10:G6}  R_Q_MVN={cellResult.RQMVN,10:G6}  | log10: R_BCa={cellResult.RLogQBCa,8:F4}  R_Linked={cellResult.RLogQLinkedMVN,8:F4}  R_MVN={cellResult.RLogQMVN,8:F4}  z0={cellResult.Z0LogQ,7:F4}  (n={cellResult.ValidCount})");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== FULL CSV ==========");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END ==========");

        // Verify asymmetry flip in log10 space: positive gamma should have R_logQ > 1,
        // negative gamma R_logQ < 1, with bounded values (~0.5 to ~3.0)
    }

    #endregion

    #region Cell Experiment

    /// <summary>
    /// Runs the quantile-level BCa experiment for a single gamma cell,
    /// averaging diagnostics over M independent datasets.
    /// </summary>
    /// <param name="gamma">True LP3 skewness parameter.</param>
    /// <param name="masterRng">Master RNG for generating per-dataset seeds.</param>
    /// <returns>Averaged quantile-level diagnostics for the cell.</returns>
    private static async Task<QuantileCellDiagnostics> RunQuantileCellExperiment(double gamma, MersenneTwister masterRng)
    {
        double sumQHat = 0;
        double sumRQBCa = 0, sumZ0Q = 0, sumAccelQ = 0;
        double sumRQLinkedMVN = 0, sumRQMVN = 0;
        double sumRLogQBCa = 0, sumZ0LogQ = 0, sumAccelLogQ = 0;
        double sumRLogQLinkedMVN = 0, sumRLogQMVN = 0;
        int validCount = 0;
        int logValidCount = 0, logLinkedValid = 0, logMVNValid = 0;

        for (int m = 0; m < DatasetsPerCell; m++)
        {
            int seed = masterRng.Next();

            try
            {
                var result = await RunSingleDatasetQuantile(gamma, seed);
                if (result == null) continue;

                sumQHat += result.Value.QHat;
                sumRQBCa += result.Value.RQBCa;
                sumZ0Q += result.Value.Z0Q;
                sumAccelQ += result.Value.AccelQ;
                sumRQLinkedMVN += result.Value.RQLinkedMVN;
                sumRQMVN += result.Value.RQMVN;
                validCount++;

                // Log10-space accumulators (NaN-safe: only accumulate finite values)
                if (!double.IsNaN(result.Value.RLogQBCa) && !double.IsInfinity(result.Value.RLogQBCa))
                {
                    sumRLogQBCa += result.Value.RLogQBCa;
                    sumZ0LogQ += result.Value.Z0LogQ;
                    sumAccelLogQ += result.Value.AccelLogQ;
                    logValidCount++;
                }
                if (!double.IsNaN(result.Value.RLogQLinkedMVN) && !double.IsInfinity(result.Value.RLogQLinkedMVN))
                {
                    sumRLogQLinkedMVN += result.Value.RLogQLinkedMVN;
                    logLinkedValid++;
                }
                if (!double.IsNaN(result.Value.RLogQMVN) && !double.IsInfinity(result.Value.RLogQMVN))
                {
                    sumRLogQMVN += result.Value.RLogQMVN;
                    logMVNValid++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Dataset {m} failed for gamma={gamma:F1}: {ex.Message}");
            }
        }

        if (validCount == 0)
        {
            return new QuantileCellDiagnostics
            {
                ValidCount = 0,
                MeanQHat = double.NaN,
                RQBCa = double.NaN,
                Z0Q = double.NaN,
                AccelQ = double.NaN,
                RQLinkedMVN = double.NaN,
                RQMVN = double.NaN,
                RLogQBCa = double.NaN,
                Z0LogQ = double.NaN,
                AccelLogQ = double.NaN,
                RLogQLinkedMVN = double.NaN,
                RLogQMVN = double.NaN
            };
        }

        return new QuantileCellDiagnostics
        {
            ValidCount = validCount,
            MeanQHat = sumQHat / validCount,
            RQBCa = sumRQBCa / validCount,
            Z0Q = sumZ0Q / validCount,
            AccelQ = sumAccelQ / validCount,
            RQLinkedMVN = sumRQLinkedMVN / validCount,
            RQMVN = sumRQMVN / validCount,
            RLogQBCa = logValidCount > 0 ? sumRLogQBCa / logValidCount : double.NaN,
            Z0LogQ = logValidCount > 0 ? sumZ0LogQ / logValidCount : double.NaN,
            AccelLogQ = logValidCount > 0 ? sumAccelLogQ / logValidCount : double.NaN,
            RLogQLinkedMVN = logLinkedValid > 0 ? sumRLogQLinkedMVN / logLinkedValid : double.NaN,
            RLogQMVN = logMVNValid > 0 ? sumRLogQMVN / logMVNValid : double.NaN
        };
    }

    /// <summary>
    /// Runs the quantile-level BCa analysis for a single LP3 dataset.
    /// </summary>
    /// <param name="gamma">True LP3 skewness parameter.</param>
    /// <param name="seed">RNG seed for this dataset.</param>
    /// <returns>Quantile-level diagnostics, or null if estimation failed.</returns>
    private static async Task<QuantileDatasetDiagnostics?> RunSingleDatasetQuantile(double gamma, int seed)
    {
        double p = 1.0 - TargetAEP; // non-exceedance probability

        // Generate random sample from true LP3
        var trueDist = new LogPearsonTypeIII(Mu, Sigma, gamma);
        double trueQ = trueDist.InverseCDF(p);
        var sample = trueDist.GenerateRandomValues(SampleSize, seed);
        var sampleList = sample.ToList();

        // Fit LP3 via MOM
        var fitDist = new LogPearsonTypeIII();
        ((IEstimation)fitDist).Estimate(sampleList, ParameterEstimationMethod.MethodOfMoments);
        double qHat = fitDist.InverseCDF(p);

        // === Part 1: BCa on quantile Q(0.0001) ===

        // Parametric bootstrap: resample from fitted distribution, re-estimate, compute quantile
        var bootstrap = new BootstrapAnalysis(
            fitDist,
            ParameterEstimationMethod.MethodOfMoments,
            SampleSize,
            replications: BootstrapReplications,
            seed: seed + 1);

        double[,] bootParams = bootstrap.Parameters(); // B x 3
        int B = bootParams.GetLength(0);

        // Compute Q(0.0001) for each bootstrap replication
        var bootQ = new List<double>(B);
        for (int i = 0; i < B; i++)
        {
            double bMu = bootParams[i, 0];
            double bSigma = bootParams[i, 1];
            double bGamma = bootParams[i, 2];

            // Skip invalid parameter sets
            if (double.IsNaN(bMu) || double.IsNaN(bSigma) || double.IsNaN(bGamma))
                continue;
            if (bSigma <= 0 || Math.Abs(bGamma) > 5.99)
                continue;

            try
            {
                var bDist = new LogPearsonTypeIII(bMu, bSigma, bGamma);
                double bQ = bDist.InverseCDF(p);
                if (!double.IsNaN(bQ) && !double.IsInfinity(bQ) && bQ > 0)
                    bootQ.Add(bQ);
            }
            catch
            {
                // Skip invalid distributions
            }
        }

        if (bootQ.Count < 100)
            return null;

        // Jackknife estimates for Q(0.0001)
        var jackQ = new double[SampleSize];
        for (int j = 0; j < SampleSize; j++)
        {
            var jackSample = new List<double>(sampleList);
            jackSample.RemoveAt(j);

            try
            {
                var jDist = new LogPearsonTypeIII();
                ((IEstimation)jDist).Estimate(jackSample, ParameterEstimationMethod.MethodOfMoments);
                jackQ[j] = jDist.InverseCDF(p);
                if (double.IsNaN(jackQ[j]) || double.IsInfinity(jackQ[j]))
                    jackQ[j] = double.NaN;
            }
            catch
            {
                jackQ[j] = double.NaN;
            }
        }

        // BCa CI on Q (real space)
        var bootQArr = bootQ.ToArray();
        var bcaQ = BCaCalibrationHelpers.BCaParameterCI(bootQArr, qHat, jackQ);
        double rQBCa = BCaCalibrationHelpers.ComputeAsymmetryRatio(bcaQ.Lower, bcaQ.Upper, qHat);

        // BCa CI on log10(Q) — the natural working space for LP3
        // log10(Q) = mu + sigma * K_gamma(p), removing the 10^(...) exponential amplification
        var bootLogQ = bootQArr.Where(q => q > 0).Select(q => Math.Log10(q)).ToArray();
        double logQHat = qHat > 0 ? Math.Log10(qHat) : double.NaN;
        var jackLogQ = jackQ.Select(q => double.IsNaN(q) || q <= 0 ? double.NaN : Math.Log10(q)).ToArray();

        double rLogQBCa = double.NaN;
        double z0LogQ = double.NaN;
        double accelLogQ = double.NaN;

        if (bootLogQ.Length >= 100 && !double.IsNaN(logQHat))
        {
            var bcaLogQ = BCaCalibrationHelpers.BCaParameterCI(bootLogQ, logQHat, jackLogQ);
            rLogQBCa = BCaCalibrationHelpers.ComputeAsymmetryRatio(bcaLogQ.Lower, bcaLogQ.Upper, logQHat);
            z0LogQ = bcaLogQ.Z0;
            accelLogQ = bcaLogQ.Accel;
        }

        // === Part 2: Linked MVN R_Q ===
        // Run Bulletin17CAnalysis with LinkedMVN and measure quantile CI asymmetry
        double rQLinkedMVN = double.NaN;
        double rQMVN = double.NaN;
        double rLogQLinkedMVN = double.NaN;
        double rLogQMVN = double.NaN;

        try
        {
            // Create B17C model from the sample data
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(sample);

            // Run LinkedMVN
            var modelLinked = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
            var analysisLinked = new Bulletin17CAnalysis(modelLinked)
            {
                UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
            };
            analysisLinked.BayesianAnalysis.CredibleIntervalWidth = 0.90;
            analysisLinked.BayesianAnalysis.OutputLength = 1000;

            await analysisLinked.RunAsync();

            if (analysisLinked.AnalysisResults?.ConfidenceIntervals != null)
            {
                // Find the AEP closest to 0.0001 in the probability ordinates
                var probs = analysisLinked.ProbabilityOrdinates;
                int targetIdx = -1;
                double minDist = double.MaxValue;
                for (int i = 0; i < probs.Count; i++)
                {
                    double dist = Math.Abs(probs[i] - TargetAEP);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        targetIdx = i;
                    }
                }

                if (targetIdx >= 0)
                {
                    double ciLo = analysisLinked.AnalysisResults.ConfidenceIntervals[targetIdx, 0];
                    double ciHi = analysisLinked.AnalysisResults.ConfidenceIntervals[targetIdx, 1];
                    double modeCurve = analysisLinked.AnalysisResults.ModeCurve![targetIdx];
                    rQLinkedMVN = BCaCalibrationHelpers.ComputeAsymmetryRatio(ciLo, ciHi, modeCurve);

                    // Log10-space ratio for LinkedMVN
                    if (ciLo > 0 && ciHi > 0 && modeCurve > 0)
                    {
                        rLogQLinkedMVN = BCaCalibrationHelpers.ComputeAsymmetryRatio(
                            Math.Log10(ciLo), Math.Log10(ciHi), Math.Log10(modeCurve));
                    }
                }
            }

            // Run plain MVN for comparison
            var modelMVN = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
            var analysisMVN = new Bulletin17CAnalysis(modelMVN)
            {
                UncertaintyMethod = UncertaintyMethod.MultivariateNormal
            };
            analysisMVN.BayesianAnalysis.CredibleIntervalWidth = 0.90;
            analysisMVN.BayesianAnalysis.OutputLength = 1000;

            await analysisMVN.RunAsync();

            if (analysisMVN.AnalysisResults?.ConfidenceIntervals != null)
            {
                var probs = analysisMVN.ProbabilityOrdinates;
                int targetIdx = -1;
                double minDist = double.MaxValue;
                for (int i = 0; i < probs.Count; i++)
                {
                    double dist = Math.Abs(probs[i] - TargetAEP);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        targetIdx = i;
                    }
                }

                if (targetIdx >= 0)
                {
                    double ciLo = analysisMVN.AnalysisResults.ConfidenceIntervals[targetIdx, 0];
                    double ciHi = analysisMVN.AnalysisResults.ConfidenceIntervals[targetIdx, 1];
                    double modeCurve = analysisMVN.AnalysisResults.ModeCurve![targetIdx];
                    rQMVN = BCaCalibrationHelpers.ComputeAsymmetryRatio(ciLo, ciHi, modeCurve);

                    // Log10-space ratio for MVN
                    if (ciLo > 0 && ciHi > 0 && modeCurve > 0)
                    {
                        rLogQMVN = BCaCalibrationHelpers.ComputeAsymmetryRatio(
                            Math.Log10(ciLo), Math.Log10(ciHi), Math.Log10(modeCurve));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"  LinkedMVN/MVN failed: {ex.Message}");
        }

        return new QuantileDatasetDiagnostics
        {
            QHat = qHat,
            RQBCa = rQBCa,
            Z0Q = bcaQ.Z0,
            AccelQ = bcaQ.Accel,
            RQLinkedMVN = rQLinkedMVN,
            RQMVN = rQMVN,
            RLogQBCa = rLogQBCa,
            Z0LogQ = z0LogQ,
            AccelLogQ = accelLogQ,
            RLogQLinkedMVN = rLogQLinkedMVN,
            RLogQMVN = rLogQMVN
        };
    }

    #endregion

    #region Data Types

    /// <summary>
    /// Diagnostics from a single dataset's quantile-level BCa bootstrap.
    /// Includes both real-space and log10-space asymmetry ratios.
    /// </summary>
    private struct QuantileDatasetDiagnostics
    {
        // Real Q space
        public double QHat;
        public double RQBCa;
        public double Z0Q;
        public double AccelQ;
        public double RQLinkedMVN;
        public double RQMVN;

        // Log10(Q) space — the natural working space for LP3
        public double RLogQBCa;
        public double Z0LogQ;
        public double AccelLogQ;
        public double RLogQLinkedMVN;
        public double RLogQMVN;
    }

    /// <summary>
    /// Averaged quantile-level diagnostics for a gamma cell.
    /// Includes both real-space and log10-space asymmetry ratios.
    /// </summary>
    private struct QuantileCellDiagnostics
    {
        public int ValidCount;
        public double MeanQHat;

        // Real Q space
        public double RQBCa;
        public double Z0Q;
        public double AccelQ;
        public double RQLinkedMVN;
        public double RQMVN;

        // Log10(Q) space
        public double RLogQBCa;
        public double Z0LogQ;
        public double AccelLogQ;
        public double RLogQLinkedMVN;
        public double RLogQMVN;
    }

    #endregion
}
