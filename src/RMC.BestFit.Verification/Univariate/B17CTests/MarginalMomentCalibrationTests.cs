using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Sampling;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Diagnostics;
using System.Text;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Comprehensive calibration experiment for LinkedMVN link function parameters.
/// Captures raw bootstrap marginal moments, BCa-adjusted quantile shapes, PIT shape diagnostics,
/// Cohn analytical CIs, and quantile-level bootstrap/BCa CIs — all in a single 145-column CSV.
/// </summary>
/// <remarks>
/// <para>
/// This is the definitive calibration dataset for publication-quality LinkedMVN link parameter
/// derivation. It combines three independent information sources:
/// </para>
/// <list type="number">
///     <item><description>Raw bootstrap marginal moments (skewness, kurtosis) — direct targets for
///     SES moment-matching of (a, λ) parameters.</description></item>
///     <item><description>BCa-adjusted quantile shapes — for comparison; BCa is correct for σ and
///     positive γ but over-corrects for negative γ.</description></item>
///     <item><description>Cohn analytical CIs — gold standard quantile-level coverage; provides
///     β₁ decomposition for z0 calibration and validation targets.</description></item>
/// </list>
/// <para>
/// Design: 13γ × 3σ × 4n = 156 cells, M=50 datasets/cell, B=5000 bootstrap/dataset.
/// Expected runtime: ~2 hours.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class MarginalMomentCalibrationTests
{
    #region Configuration

    /// <summary>
    /// Gamma grid covering practical LP3 range. ±1.5 is the safe limit for MOM bootstrap.
    /// </summary>
    private static readonly double[] Gammas =
    {
        -1.5, -1.0, -0.75, -0.5, -0.25, -0.1, 0.0, 0.1, 0.25, 0.5, 0.75, 1.0, 1.5
    };

    /// <summary>
    /// Sigma grid. Three values verify sigma-independence of shape diagnostics.
    /// </summary>
    private static readonly double[] Sigmas = { 0.3, 0.5, 0.7 };

    /// <summary>
    /// Sample sizes spanning small-sample (n=25) to moderate (n=200).
    /// </summary>
    private static readonly int[] SampleSizes = { 25, 50, 100, 200 };

    /// <summary>
    /// AEPs for Cohn CI and quantile-level bootstrap/BCa CI extraction.
    /// </summary>
    private static readonly double[] TargetAEPs = { 0.01, 0.001, 0.0001 };

    /// <summary>
    /// AEP suffixes for CSV column naming.
    /// </summary>
    private static readonly string[] AEPLabels = { "p01", "p001", "p0001" };

    private const double Mu = 3.0;
    private const int DatasetsPerCell = 50;
    private const int BootstrapReplications = 5000;
    private const int MasterSeed = 88888;
    private const double NominalCI = 0.90;
    private const double Alpha = 0.10; // 1 - NominalCI

    #endregion

    #region Main Experiment

    /// <summary>
    /// Full calibration sweep producing 145-column CSV with all diagnostics needed for
    /// publication-quality LinkedMVN link parameter calibration.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task MarginalMomentCalibration_FullSweep()
    {
        var masterRng = new MersenneTwister(MasterSeed);
        var csv = new StringBuilder();
        csv.AppendLine(BuildHeaderLine());

        int totalCells = SampleSizes.Length * Sigmas.Length * Gammas.Length;
        int cellsDone = 0;

        foreach (int n in SampleSizes)
        {
            foreach (double sigma in Sigmas)
            {
                foreach (double gamma in Gammas)
                {
                    cellsDone++;
                    Debug.WriteLine($"Cell {cellsDone}/{totalCells}: n={n}, σ={sigma:F2}, γ={gamma:+0.00;-0.00}");

                    var cellResult = await RunCell(n, sigma, gamma, masterRng);
                    csv.AppendLine(FormatCellRow(n, sigma, gamma, cellResult));
                }
            }
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== MARGINAL MOMENT CALIBRATION RESULTS ==========");
        Debug.WriteLine($"Completed {cellsDone}/{totalCells} cells.");
        Debug.WriteLine("");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END RESULTS ==========");
    }

    #endregion

    #region Cell Processing

    /// <summary>
    /// Runs M=50 independent datasets for a single (n, σ, γ) cell and averages all diagnostics.
    /// </summary>
    private async Task<CellResult> RunCell(int n, double sigma, double gamma, MersenneTwister masterRng)
    {
        var acc = new CellAccumulator();

        for (int m = 0; m < DatasetsPerCell; m++)
        {
            int seed = masterRng.Next();
            try
            {
                var ds = await RunSingleDataset(n, sigma, gamma, seed);
                if (ds != null)
                    acc.Add(ds);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Dataset m={m} failed: {ex.Message}");
            }
        }

        return acc.Finalize();
    }

    /// <summary>
    /// Processes a single dataset: generates LP3 sample, fits GMM, runs bootstrap with BCa,
    /// computes Cohn CIs, and extracts all 145 diagnostic quantities.
    /// </summary>
    private async Task<DatasetResult?> RunSingleDataset(int n, double sigma, double gamma, int seed)
    {
        // Use a small epsilon for gamma=0 to avoid degenerate P3
        double effectiveGamma = Math.Abs(gamma) < 1e-10 ? 0.001 : gamma;

        // --- Generate LP3 sample ---
        var trueLogDist = new PearsonTypeIII(Mu, sigma, effectiveGamma);
        var logValues = trueLogDist.GenerateRandomValues(n, seed);
        var realValues = new double[n];
        for (int k = 0; k < n; k++)
            realValues[k] = Math.Pow(10, logValues[k]);

        // --- Fit LP3 via MOM on log-space ---
        var sampleList = logValues.ToList();
        var fitLogDist = new PearsonTypeIII();
        ((IEstimation)fitLogDist).Estimate(sampleList, ParameterEstimationMethod.MethodOfMoments);
        double muHat = fitLogDist.Mu;
        double sigmaHat = fitLogDist.Sigma;
        double gammaHat = fitLogDist.Gamma;

        if (sigmaHat <= 0 || double.IsNaN(gammaHat)) return null;

        // --- Parametric bootstrap ---
        var bootstrap = new BootstrapAnalysis(
            fitLogDist,
            ParameterEstimationMethod.MethodOfMoments,
            n,
            replications: BootstrapReplications,
            seed: seed + 1);

        double[,] bootParams = bootstrap.Parameters(); // B x 3

        // Extract valid bootstrap columns
        var muStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 0, v => !double.IsNaN(v));
        var sigmaStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 1, v => !double.IsNaN(v) && v > 0);
        var gammaStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 2, v => !double.IsNaN(v));

        if (muStars.Length < 100 || sigmaStars.Length < 100 || gammaStars.Length < 100)
            return null;

        var logSigmaStars = sigmaStars.Select(s => Math.Log(s)).ToArray();

        // --- Common valid rows for cross-moments ---
        int B = bootParams.GetLength(0);
        var validMu = new List<double>(B);
        var validSigma = new List<double>(B);
        var validLogSigma = new List<double>(B);
        var validGamma = new List<double>(B);
        for (int i = 0; i < B; i++)
        {
            double bm = bootParams[i, 0], bs = bootParams[i, 1], bg = bootParams[i, 2];
            if (!double.IsNaN(bm) && !double.IsNaN(bs) && bs > 0 && !double.IsNaN(bg))
            {
                validMu.Add(bm);
                validSigma.Add(bs);
                validLogSigma.Add(Math.Log(bs));
                validGamma.Add(bg);
            }
        }

        if (validMu.Count < 100) return null;

        var ds = new DatasetResult();

        // --- Block D: Point estimates ---
        ds.MuHat = muHat;
        ds.SigmaHat = sigmaHat;
        ds.GammaHat = gammaHat;

        // --- Block A: Raw moment-based statistics ---
        ds.RawSdMu = ComputeSD(muStars);
        ds.RawSkewMu = ComputeSkewness(muStars);
        ds.RawKurtMu = ComputeExcessKurtosis(muStars);
        ds.RawSdSigma = ComputeSD(sigmaStars);
        ds.RawSdLogSigma = ComputeSD(logSigmaStars);
        ds.RawSkewLogSigma = ComputeSkewness(logSigmaStars);
        ds.RawKurtLogSigma = ComputeExcessKurtosis(logSigmaStars);
        ds.RawSdGamma = ComputeSD(gammaStars);
        ds.RawSkewGamma = ComputeSkewness(gammaStars);
        ds.RawKurtGamma = ComputeExcessKurtosis(gammaStars);
        ds.RawCorrMuSigma = PearsonCorrelation(validMu, validSigma);
        ds.RawCorrMuGamma = PearsonCorrelation(validMu, validGamma);
        ds.RawCorrSigmaGamma = PearsonCorrelation(validSigma, validGamma);

        // --- Block B: Raw quantile-based shape ---
        ComputeQuantileShape(muStars, out ds.RawP05Mu, out ds.RawP25Mu, out ds.RawP50Mu,
            out ds.RawP75Mu, out ds.RawP95Mu, out ds.RawBowleyMu);
        ComputeQuantileShape(logSigmaStars, out ds.RawP05LogSigma, out ds.RawP25LogSigma,
            out ds.RawP50LogSigma, out ds.RawP75LogSigma, out ds.RawP95LogSigma, out ds.RawBowleyLogSigma);
        ComputeQuantileShape(gammaStars, out ds.RawP05Gamma, out ds.RawP25Gamma, out ds.RawP50Gamma,
            out ds.RawP75Gamma, out ds.RawP95Gamma, out ds.RawBowleyGamma);

        // --- Block E + C: BCa diagnostics and BCa-adjusted quantile shapes ---
        var jackMu = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 0, JackEstimatorP3);
        var jackSigma = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 1, JackEstimatorP3);
        var jackGamma = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 2, JackEstimatorP3);
        var jackLogSigma = jackSigma.Select(s => double.IsNaN(s) || s <= 0 ? double.NaN : Math.Log(s)).ToArray();

        var bcaMu = BCaCalibrationHelpers.BCaParameterCI(muStars, muHat, jackMu);
        var bcaLogSigma = BCaCalibrationHelpers.BCaParameterCI(logSigmaStars, Math.Log(sigmaHat), jackLogSigma);
        var bcaGamma = BCaCalibrationHelpers.BCaParameterCI(gammaStars, gammaHat, jackGamma);

        ds.BcaZ0Mu = bcaMu.Z0;
        ds.BcaZ0LogSigma = bcaLogSigma.Z0;
        ds.BcaZ0Gamma = bcaGamma.Z0;
        ds.BcaAccelMu = bcaMu.Accel;
        ds.BcaAccelLogSigma = bcaLogSigma.Accel;
        ds.BcaAccelGamma = bcaGamma.Accel;
        ds.BcaRMu = BCaCalibrationHelpers.ComputeAsymmetryRatio(bcaMu.Lower, bcaMu.Upper, muHat);
        ds.BcaRLogSigma = BCaCalibrationHelpers.ComputeAsymmetryRatio(
            bcaLogSigma.Lower, bcaLogSigma.Upper, Math.Log(sigmaHat));
        ds.BcaRGamma = BCaCalibrationHelpers.ComputeAsymmetryRatio(bcaGamma.Lower, bcaGamma.Upper, gammaHat);

        // BCa-adjusted percentiles at 5 levels
        ComputeBCaQuantileShape(muStars, muHat, jackMu,
            out ds.BcaP05Mu, out ds.BcaP25Mu, out ds.BcaP50Mu, out ds.BcaP75Mu, out ds.BcaP95Mu, out ds.BcaBowleyMu);
        ComputeBCaQuantileShape(logSigmaStars, Math.Log(sigmaHat), jackLogSigma,
            out ds.BcaP05LogSigma, out ds.BcaP25LogSigma, out ds.BcaP50LogSigma,
            out ds.BcaP75LogSigma, out ds.BcaP95LogSigma, out ds.BcaBowleyLogSigma);
        ComputeBCaQuantileShape(gammaStars, gammaHat, jackGamma,
            out ds.BcaP05Gamma, out ds.BcaP25Gamma, out ds.BcaP50Gamma,
            out ds.BcaP75Gamma, out ds.BcaP95Gamma, out ds.BcaBowleyGamma);

        // --- Block F: Tail ratios ---
        ds.RawTailratioMu = TailRatio(ds.RawP05Mu, ds.RawP25Mu, ds.RawP75Mu, ds.RawP95Mu);
        ds.BcaTailratioMu = TailRatio(ds.BcaP05Mu, ds.BcaP25Mu, ds.BcaP75Mu, ds.BcaP95Mu);
        ds.RawTailratioLogSigma = TailRatio(ds.RawP05LogSigma, ds.RawP25LogSigma, ds.RawP75LogSigma, ds.RawP95LogSigma);
        ds.BcaTailratioLogSigma = TailRatio(ds.BcaP05LogSigma, ds.BcaP25LogSigma, ds.BcaP75LogSigma, ds.BcaP95LogSigma);
        ds.RawTailratioGamma = TailRatio(ds.RawP05Gamma, ds.RawP25Gamma, ds.RawP75Gamma, ds.RawP95Gamma);
        ds.BcaTailratioGamma = TailRatio(ds.BcaP05Gamma, ds.BcaP25Gamma, ds.BcaP75Gamma, ds.BcaP95Gamma);

        // --- Block G: Co-skewness ---
        ds.RawCoskewMuGamma = ComputeCoSkewness(validMu, validGamma);
        ds.RawCoskewSigmaGamma = ComputeCoSkewness(validSigma, validGamma);

        // --- Block H: PIT shape diagnostics ---
        ComputePITDiagnostics(sigmaStars, sigmaHat, gammaStars, gammaHat, ds);

        // --- Block J: GMM covariance ---
        // Need to run B17C analysis to get Fisher information covariance and Cohn CIs
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(realValues);
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var analysis = new Bulletin17CAnalysis(model);
        analysis.BayesianAnalysis.CredibleIntervalWidth = NominalCI;
        analysis.BayesianAnalysis.OutputLength = 10;
        analysis.ProbabilityOrdinates.Clear();
        foreach (double aep in TargetAEPs)
            analysis.ProbabilityOrdinates.Add(aep);
        analysis.UncertaintyMethod = UncertaintyMethod.MultivariateNormal;
        await analysis.RunAsync();

        if (!analysis.IsEstimated || analysis.GMM == null || !analysis.GMM.IsEstimated)
            return ds; // Return what we have from bootstrap; Cohn/GMM blocks will be NaN

        var thetaHat = analysis.GMM.BestParameterSet.Values;
        Matrix sigmaHatMat;
        try
        {
            sigmaHatMat = analysis.GMM.GetCovariance(thetaHat);
            sigmaHatMat = MatrixRegularization.MakeSymmetricPositiveDefinite(sigmaHatMat);
        }
        catch { return ds; }

        ds.GmmVarMu = sigmaHatMat[0, 0];
        ds.GmmVarSigma = sigmaHatMat[1, 1];
        ds.GmmVarGamma = thetaHat.Length >= 3 ? sigmaHatMat[2, 2] : 0;
        ds.GmmCovMuSigma = sigmaHatMat[0, 1];
        ds.GmmCovMuGamma = thetaHat.Length >= 3 ? sigmaHatMat[0, 2] : 0;
        ds.GmmCovSigmaGamma = thetaHat.Length >= 3 ? sigmaHatMat[1, 2] : 0;

        // --- Block I: Cohn CI diagnostics at 3 AEPs ---
        var cohnResult = analysis.ComputeCohnStyleConfidenceIntervals();
        if (cohnResult != null)
        {
            for (int k = 0; k < TargetAEPs.Length; k++)
            {
                try
                {
                    ComputeCohnDiagnostics(k, TargetAEPs[k], cohnResult, thetaHat, sigmaHatMat, model, ds);
                }
                catch { /* Skip this AEP */ }
            }
        }

        // --- Block K: Quantile-level bootstrap and BCa CIs at 3 AEPs ---
        ComputeQuantileLevelCIs(bootParams, fitLogDist, sampleList, n, ds);

        return ds;
    }

    #endregion

    #region Block Computations

    /// <summary>
    /// Computes raw percentiles and Bowley skewness for a bootstrap parameter array.
    /// </summary>
    private static void ComputeQuantileShape(double[] values,
        out double p05, out double p25, out double p50, out double p75, out double p95, out double bowley)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        p05 = Statistics.Percentile(sorted, 0.05, dataIsSorted: true);
        p25 = Statistics.Percentile(sorted, 0.25, dataIsSorted: true);
        p50 = Statistics.Percentile(sorted, 0.50, dataIsSorted: true);
        p75 = Statistics.Percentile(sorted, 0.75, dataIsSorted: true);
        p95 = Statistics.Percentile(sorted, 0.95, dataIsSorted: true);
        double range = p95 - p05;
        bowley = Math.Abs(range) > 1e-30 ? (p95 + p05 - 2.0 * p50) / range : 0.0;
    }

    /// <summary>
    /// Computes BCa-adjusted percentiles at 5 levels (0.05, 0.25, 0.50, 0.75, 0.95)
    /// and the Bowley skewness from the adjusted percentiles.
    /// </summary>
    private static void ComputeBCaQuantileShape(double[] bootValues, double pointEstimate, double[] jackEstimates,
        out double p05, out double p25, out double p50, out double p75, out double p95, out double bowley)
    {
        var sorted = (double[])bootValues.Clone();
        Array.Sort(sorted);

        // Compute z0 and acceleration
        double p0 = (sorted.Count(v => v <= pointEstimate) + 1.0) / (sorted.Length + 1.0);
        double z0 = Normal.StandardZ(p0);
        double accel = BCaCalibrationHelpers.JackknifeAcceleration(jackEstimates);

        p05 = BCaPercentile(sorted, 0.05, z0, accel);
        p25 = BCaPercentile(sorted, 0.25, z0, accel);
        p50 = BCaPercentile(sorted, 0.50, z0, accel);
        p75 = BCaPercentile(sorted, 0.75, z0, accel);
        p95 = BCaPercentile(sorted, 0.95, z0, accel);

        double range = p95 - p05;
        bowley = Math.Abs(range) > 1e-30 ? (p95 + p05 - 2.0 * p50) / range : 0.0;
    }

    /// <summary>
    /// Computes a single BCa-adjusted percentile from sorted bootstrap values.
    /// </summary>
    private static double BCaPercentile(double[] sorted, double nominalLevel, double z0, double accel)
    {
        double zAlpha = Normal.StandardZ(nominalLevel);
        double num = z0 + zAlpha;
        double den = 1.0 - accel * num;
        double adjusted = Math.Abs(den) > 1e-10 ? Normal.StandardCDF(z0 + num / den) : nominalLevel;
        adjusted = Math.Max(0.001, Math.Min(0.999, adjusted));
        return Statistics.Percentile(sorted, adjusted, dataIsSorted: true);
    }

    /// <summary>
    /// Computes PIT shape diagnostics: effective ν from variance and kurtosis matching,
    /// plus KS goodness-of-fit statistics.
    /// </summary>
    private static void ComputePITDiagnostics(double[] sigmaStars, double sigmaHat,
        double[] gammaStars, double gammaHat, DatasetResult ds)
    {
        // Sigma: r = (σ̂/σ̂*)² follows scaled inverse-chi-squared
        // If σ̂* ~ scaled-chi with ν d.f., then r has E[r]=1, Var[r]=2/ν
        var r = sigmaStars.Select(s => (sigmaHat / s) * (sigmaHat / s)).ToArray();
        double varR = ComputeVariance(r);
        double kurtR = ComputeExcessKurtosis(r);
        ds.PitNuSigmaVar = varR > 1e-10 ? 2.0 / varR : double.NaN;
        ds.PitNuSigmaKurt = kurtR > 1e-10 ? 12.0 / kurtR : double.NaN;

        // KS for sigma: test if ν·r ~ Gamma(ν/2, 2)
        double nuSig = ds.PitNuSigmaVar;
        if (!double.IsNaN(nuSig) && nuSig > 0)
        {
            var scaledR = r.Select(v => nuSig * v).ToArray();
            Array.Sort(scaledR);
            var gammaDist = new GammaDistribution(scale: 2.0, shape: Math.Max(nuSig / 2.0, 1.0));
            ds.PitKsSigma = ComputeKS(scaledR, x => gammaDist.CDF(x));
        }
        else
        {
            ds.PitKsSigma = double.NaN;
        }

        // Gamma: effective ν from kurtosis of standardized deviations
        double sdGamma = ComputeSD(gammaStars);
        if (sdGamma > 1e-10)
        {
            var zGamma = gammaStars.Select(g => (g - gammaHat) / sdGamma).ToArray();
            var zSq = zGamma.Select(z => z * z).ToArray();
            double varZSq = ComputeVariance(zSq);
            double kurtZGamma = ComputeExcessKurtosis(gammaStars);
            ds.PitNuGammaVar = varZSq > 1e-10 ? 2.0 / varZSq : double.NaN;
            ds.PitNuGammaKurt = kurtZGamma > 1e-10 ? 12.0 / kurtZGamma : double.NaN;
        }
        else
        {
            ds.PitNuGammaVar = double.NaN;
            ds.PitNuGammaKurt = double.NaN;
        }

        // KS for gamma: test if γ̂* ~ PT3(gammaHat, sd, skew)
        double skewGamma = ComputeSkewness(gammaStars);
        if (sdGamma > 1e-10 && Math.Abs(skewGamma) > 0.01)
        {
            var sortedGamma = (double[])gammaStars.Clone();
            Array.Sort(sortedGamma);
            var pt3 = new PearsonTypeIII(gammaHat, sdGamma, skewGamma);
            ds.PitKsGamma = ComputeKS(sortedGamma, x => pt3.CDF(x));
        }
        else
        {
            ds.PitKsGamma = double.NaN;
        }
    }

    /// <summary>
    /// Extracts Cohn CI diagnostics at a single AEP from the B17C analysis results.
    /// </summary>
    private static void ComputeCohnDiagnostics(int aepIndex, double aep,
        dynamic cohnResult, double[] thetaHat, Matrix sigmaHat,
        Bulletin17CDistribution model, DatasetResult ds)
    {
        int p = thetaHat.Length;
        double qHat = cohnResult.PointEstimates[aepIndex];
        double ciLow = Math.Log10(Math.Max(cohnResult.LowerCI[aepIndex], 1e-30));
        double ciHigh = Math.Log10(Math.Max(cohnResult.UpperCI[aepIndex], 1e-30));
        double beta1 = cohnResult.Beta1[aepIndex];
        double nu = cohnResult.Nu[aepIndex];
        double varQ = cohnResult.QuantileVariance[aepIndex];

        double upperWidth = ciHigh - qHat;
        double lowerWidth = qHat - ciLow;
        double rCohn = Math.Abs(lowerWidth) > 1e-10 ? upperWidth / lowerWidth : 1.0;

        double nonExceedProb = 1.0 - aep;
        double[] gradient;
        try { gradient = model.QuantileGradient(nonExceedProb, thetaHat); }
        catch { return; }

        double[] contributions = new double[p];
        double totalContrib = 0;
        for (int i = 0; i < p; i++)
        {
            contributions[i] = gradient[i] * gradient[i] * sigmaHat[i, i];
            totalContrib += contributions[i];
        }

        double sigmaHatVal = thetaHat[1];
        double gammaHatVal = p >= 3 ? thetaHat[2] : 0;
        double deltaSigma = sigmaHatVal > 1e-10
            ? 1.645 * Math.Sqrt(sigmaHat[1, 1]) / sigmaHatVal : 1.0;
        double deltaGamma = 0;
        if (p >= 3)
        {
            double sesJac = 1.0 / Math.Sqrt(1.0 + gammaHatVal * gammaHatVal);
            deltaGamma = 1.645 * sesJac * Math.Sqrt(sigmaHat[2, 2]);
        }

        double covQSE = beta1 * varQ;
        double varSE = covQSE * covQSE / varQ + 0.5 * varQ / nu;

        // Store into the appropriate AEP slot
        var aepResult = new AEPDiagnostics
        {
            Beta1 = beta1,
            Nu = nu,
            RCohn = rCohn,
            QHat = qHat,
            CILow = ciLow,
            CIHigh = ciHigh,
            GradMu = gradient[0],
            GradSigma = gradient[1],
            GradGamma = p >= 3 ? gradient[2] : 0,
            ContribMu = totalContrib > 1e-30 ? contributions[0] / totalContrib : 0,
            ContribSigma = totalContrib > 1e-30 ? contributions[1] / totalContrib : 0,
            ContribGamma = p >= 3 && totalContrib > 1e-30 ? contributions[2] / totalContrib : 0,
            VarQ = varQ,
            DeltaGamma = deltaGamma
        };

        switch (aepIndex)
        {
            case 0: ds.Cohn_p01 = aepResult; break;
            case 1: ds.Cohn_p001 = aepResult; break;
            case 2: ds.Cohn_p0001 = aepResult; break;
        }
    }

    /// <summary>
    /// Computes quantile-level bootstrap and BCa CIs at the 3 target AEPs.
    /// For each AEP, evaluates the quantile Q(AEP) for each bootstrap parameter set,
    /// then extracts raw percentile CI and BCa-adjusted CI.
    /// </summary>
    private static void ComputeQuantileLevelCIs(double[,] bootParams, PearsonTypeIII fitLogDist,
        List<double> sampleList, int n, DatasetResult ds)
    {
        int Brows = bootParams.GetLength(0);
        double muHat = fitLogDist.Mu;
        double sigmaHat = fitLogDist.Sigma;
        double gammaHat = fitLogDist.Gamma;

        // Jackknife for quantile-level BCa
        for (int aepIdx = 0; aepIdx < TargetAEPs.Length; aepIdx++)
        {
            double aep = TargetAEPs[aepIdx];
            double nonExceedProb = 1.0 - aep;

            // Point estimate quantile (log10-space)
            double qHatLog10 = fitLogDist.InverseCDF(nonExceedProb);

            // Bootstrap quantile distribution
            var bootQ = new List<double>(Brows);
            for (int i = 0; i < Brows; i++)
            {
                double bm = bootParams[i, 0], bs = bootParams[i, 1], bg = bootParams[i, 2];
                if (double.IsNaN(bm) || double.IsNaN(bs) || bs <= 0 || double.IsNaN(bg)) continue;
                try
                {
                    var bDist = new PearsonTypeIII(bm, bs, bg);
                    double q = bDist.InverseCDF(nonExceedProb);
                    if (!double.IsNaN(q) && !double.IsInfinity(q))
                        bootQ.Add(q);
                }
                catch { /* skip invalid bootstrap quantile */ }
            }

            if (bootQ.Count < 100) continue;

            var bootQArr = bootQ.ToArray();
            var sortedQ = (double[])bootQArr.Clone();
            Array.Sort(sortedQ);

            // Raw bootstrap percentile CI
            double rawLo = Statistics.Percentile(sortedQ, 0.05, dataIsSorted: true);
            double rawHi = Statistics.Percentile(sortedQ, 0.95, dataIsSorted: true);

            // Jackknife for quantile-level acceleration
            var jackQ = new double[n];
            for (int j = 0; j < n; j++)
            {
                var jackSample = new List<double>(sampleList);
                jackSample.RemoveAt(j);
                try
                {
                    var jDist = new PearsonTypeIII();
                    ((IEstimation)jDist).Estimate(jackSample, ParameterEstimationMethod.MethodOfMoments);
                    jackQ[j] = jDist.InverseCDF(nonExceedProb);
                    if (double.IsNaN(jackQ[j]) || double.IsInfinity(jackQ[j]))
                        jackQ[j] = double.NaN;
                }
                catch { jackQ[j] = double.NaN; }
            }

            var bcaQ = BCaCalibrationHelpers.BCaParameterCI(bootQArr, qHatLog10, jackQ);

            var qci = new QuantileCIDiagnostics
            {
                RawBootCILo = rawLo,
                RawBootCIHi = rawHi,
                BcaCILo = bcaQ.Lower,
                BcaCIHi = bcaQ.Upper,
                BcaZ0Q = bcaQ.Z0,
                BcaAccelQ = bcaQ.Accel
            };

            switch (aepIdx)
            {
                case 0: ds.QCI_p01 = qci; break;
                case 1: ds.QCI_p001 = qci; break;
                case 2: ds.QCI_p0001 = qci; break;
            }
        }
    }

    #endregion

    #region Statistical Helpers

    /// <summary>
    /// Computes sD.
    /// </summary>
    /// <param name="values">The input values.</param>
    /// <returns>The computed value.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double ComputeSD(double[] values)
    {
        int n = values.Length;
        if (n < 2) return double.NaN;
        double mean = 0;
        for (int i = 0; i < n; i++) mean += values[i];
        mean /= n;
        double sumSq = 0;
        for (int i = 0; i < n; i++) { double d = values[i] - mean; sumSq += d * d; }
        return Math.Sqrt(sumSq / (n - 1));
    }

    /// <summary>
    /// Computes variance.
    /// </summary>
    /// <param name="values">The input values.</param>
    /// <returns>The computed value.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double ComputeVariance(double[] values)
    {
        int n = values.Length;
        if (n < 2) return double.NaN;
        double mean = 0;
        for (int i = 0; i < n; i++) mean += values[i];
        mean /= n;
        double sumSq = 0;
        for (int i = 0; i < n; i++) { double d = values[i] - mean; sumSq += d * d; }
        return sumSq / (n - 1);
    }

    /// <summary>
    /// Computes skewness.
    /// </summary>
    /// <param name="values">The input values.</param>
    /// <returns>The computed value.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double ComputeSkewness(double[] values)
    {
        int n = values.Length;
        if (n < 3) return double.NaN;
        double mean = 0;
        for (int i = 0; i < n; i++) mean += values[i];
        mean /= n;
        double m2 = 0, m3 = 0;
        for (int i = 0; i < n; i++)
        {
            double d = values[i] - mean;
            m2 += d * d;
            m3 += d * d * d;
        }
        m2 /= n;
        m3 /= n;
        double sd3 = Math.Pow(m2, 1.5);
        return sd3 > 1e-30 ? m3 / sd3 : 0.0;
    }

    /// <summary>
    /// Computes excess Kurtosis.
    /// </summary>
    /// <param name="values">The input values.</param>
    /// <returns>The computed value.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double ComputeExcessKurtosis(double[] values)
    {
        int n = values.Length;
        if (n < 4) return double.NaN;
        double mean = 0;
        for (int i = 0; i < n; i++) mean += values[i];
        mean /= n;
        double m2 = 0, m4 = 0;
        for (int i = 0; i < n; i++)
        {
            double d = values[i] - mean;
            double d2 = d * d;
            m2 += d2;
            m4 += d2 * d2;
        }
        m2 /= n;
        m4 /= n;
        double m2sq = m2 * m2;
        return m2sq > 1e-30 ? m4 / m2sq - 3.0 : 0.0;
    }

    /// <summary>
    /// Supports the <c>PearsonCorrelation</c> helper.
    /// </summary>
    /// <param name="x">The numeric values.</param>
    /// <param name="y">The numeric values.</param>
    /// <returns>The numeric data.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double PearsonCorrelation(List<double> x, List<double> y)
    {
        int n = Math.Min(x.Count, y.Count);
        if (n < 3) return double.NaN;
        double mx = 0, my = 0;
        for (int i = 0; i < n; i++) { mx += x[i]; my += y[i]; }
        mx /= n; my /= n;
        double sxy = 0, sx2 = 0, sy2 = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - mx, dy = y[i] - my;
            sxy += dx * dy; sx2 += dx * dx; sy2 += dy * dy;
        }
        double denom = Math.Sqrt(sx2 * sy2);
        return denom > 1e-30 ? sxy / denom : 0.0;
    }

    /// <summary>
    /// Co-skewness: E[(X-μX)²(Y-μY)] / (σX² · σY). Measures how Y variation skews X.
    /// </summary>
    private static double ComputeCoSkewness(List<double> x, List<double> y)
    {
        int n = Math.Min(x.Count, y.Count);
        if (n < 3) return double.NaN;
        double mx = 0, my = 0;
        for (int i = 0; i < n; i++) { mx += x[i]; my += y[i]; }
        mx /= n; my /= n;
        double sx2 = 0, sy2 = 0, sxxy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - mx, dy = y[i] - my;
            sx2 += dx * dx;
            sy2 += dy * dy;
            sxxy += dx * dx * dy;
        }
        sx2 /= n; sy2 /= n; sxxy /= n;
        double denom = sx2 * Math.Sqrt(sy2);
        return denom > 1e-30 ? sxxy / denom : 0.0;
    }

    /// <summary>
    /// Computes a tail-spread ratio from selected quantiles.
    /// </summary>
    /// <param name="p05">The selected probability or quantile value.</param>
    /// <param name="p25">The selected probability or quantile value.</param>
    /// <param name="p75">The selected probability or quantile value.</param>
    /// <param name="p95">The selected probability or quantile value.</param>
    /// <returns>The numeric data.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double TailRatio(double p05, double p25, double p75, double p95)
    {
        double iqr = p75 - p25;
        double range = p95 - p05;
        return Math.Abs(iqr) > 1e-30 ? range / iqr : double.NaN;
    }

    /// <summary>
    /// Kolmogorov-Smirnov statistic: max |F_empirical - F_theoretical|.
    /// </summary>
    private static double ComputeKS(double[] sortedValues, Func<double, double> cdf)
    {
        int n = sortedValues.Length;
        double maxD = 0;
        for (int i = 0; i < n; i++)
        {
            double empirical = (i + 1.0) / n;
            double theoretical = cdf(sortedValues[i]);
            maxD = Math.Max(maxD, Math.Abs(empirical - theoretical));
            maxD = Math.Max(maxD, Math.Abs((double)i / n - theoretical));
        }
        return maxD;
    }

    /// <summary>
    /// Jackknife estimator factory for PearsonTypeIII (log-space MOM).
    /// </summary>
    private static readonly Func<List<double>, double[]?> JackEstimatorP3 = sample =>
    {
        var d = new PearsonTypeIII();
        ((IEstimation)d).Estimate(sample, ParameterEstimationMethod.MethodOfMoments);
        return d.GetParameters;
    };

    #endregion

    #region CSV Formatting

    /// <summary>
    /// Builds header Line.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static string BuildHeaderLine()
    {
        var cols = new List<string>();

        // Block D: Identifiers (7)
        cols.AddRange(new[] { "n", "sigma_true", "gamma_true", "valid_count",
            "mean_mu_hat", "mean_sigma_hat", "mean_gamma_hat" });

        // Block A: Raw moments (13)
        cols.AddRange(new[] { "raw_sd_mu", "raw_skew_mu", "raw_kurt_mu",
            "raw_sd_sigma", "raw_sd_log_sigma", "raw_skew_log_sigma", "raw_kurt_log_sigma",
            "raw_sd_gamma", "raw_skew_gamma", "raw_kurt_gamma",
            "raw_corr_mu_sigma", "raw_corr_mu_gamma", "raw_corr_sigma_gamma" });

        // Block B: Raw quantiles (18)
        foreach (string param in new[] { "mu", "log_sigma", "gamma" })
            foreach (string pct in new[] { "p05", "p25", "p50", "p75", "p95", "bowley" })
                cols.Add($"raw_{pct}_{param}");

        // Block C: BCa quantiles (18)
        foreach (string param in new[] { "mu", "log_sigma", "gamma" })
            foreach (string pct in new[] { "p05", "p25", "p50", "p75", "p95", "bowley" })
                cols.Add($"bca_{pct}_{param}");

        // Block E: BCa scalars (9)
        cols.AddRange(new[] { "bca_z0_mu", "bca_z0_log_sigma", "bca_z0_gamma",
            "bca_accel_mu", "bca_accel_log_sigma", "bca_accel_gamma",
            "bca_R_mu", "bca_R_log_sigma", "bca_R_gamma" });

        // Block F: Tail ratios (6)
        cols.AddRange(new[] { "raw_tailratio_mu", "bca_tailratio_mu",
            "raw_tailratio_log_sigma", "bca_tailratio_log_sigma",
            "raw_tailratio_gamma", "bca_tailratio_gamma" });

        // Block G: Co-skewness (2)
        cols.AddRange(new[] { "raw_coskew_mu_gamma", "raw_coskew_sigma_gamma" });

        // Block H: PIT (6)
        cols.AddRange(new[] { "pit_nu_sigma_var", "pit_nu_sigma_kurt",
            "pit_nu_gamma_var", "pit_nu_gamma_kurt", "pit_ks_sigma", "pit_ks_gamma" });

        // Block I: Cohn at 3 AEPs (42)
        foreach (string aep in AEPLabels)
            cols.AddRange(new[] {
                $"cohn_beta1_{aep}", $"cohn_nu_{aep}", $"cohn_R_{aep}",
                $"cohn_qhat_{aep}", $"cohn_ci_lo_{aep}", $"cohn_ci_hi_{aep}",
                $"cohn_grad_mu_{aep}", $"cohn_grad_sigma_{aep}", $"cohn_grad_gamma_{aep}",
                $"cohn_contrib_mu_{aep}", $"cohn_contrib_sigma_{aep}", $"cohn_contrib_gamma_{aep}",
                $"cohn_varQ_{aep}", $"cohn_delta_gamma_{aep}" });

        // Block J: GMM covariance (6)
        cols.AddRange(new[] { "gmm_var_mu", "gmm_var_sigma", "gmm_var_gamma",
            "gmm_cov_mu_sigma", "gmm_cov_mu_gamma", "gmm_cov_sigma_gamma" });

        // Block K: Quantile CIs at 3 AEPs (18)
        foreach (string aep in AEPLabels)
            cols.AddRange(new[] {
                $"raw_boot_ci_lo_{aep}", $"raw_boot_ci_hi_{aep}",
                $"bca_ci_lo_{aep}", $"bca_ci_hi_{aep}",
                $"bca_z0_Q_{aep}", $"bca_accel_Q_{aep}" });

        return string.Join(",", cols);
    }

    /// <summary>
    /// Supports the <c>FormatCellRow</c> helper.
    /// </summary>
    /// <param name="n">The n value.</param>
    /// <param name="sigma">The standard deviation used by the scenario.</param>
    /// <param name="gamma">The skew coefficient used by the scenario.</param>
    /// <param name="r">The r value.</param>
    /// <returns>The formatted text.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static string FormatCellRow(int n, double sigma, double gamma, CellResult r)
    {
        string F(double v) => double.IsNaN(v) ? "" : v.ToString("G6");

        var vals = new List<string>();

        // Block D
        vals.AddRange(new[] { n.ToString(), sigma.ToString("F2"), gamma.ToString("F2"),
            r.ValidCount.ToString(), F(r.MuHat), F(r.SigmaHat), F(r.GammaHat) });

        // Block A
        vals.AddRange(new[] { F(r.RawSdMu), F(r.RawSkewMu), F(r.RawKurtMu),
            F(r.RawSdSigma), F(r.RawSdLogSigma), F(r.RawSkewLogSigma), F(r.RawKurtLogSigma),
            F(r.RawSdGamma), F(r.RawSkewGamma), F(r.RawKurtGamma),
            F(r.RawCorrMuSigma), F(r.RawCorrMuGamma), F(r.RawCorrSigmaGamma) });

        // Block B
        vals.AddRange(new[] { F(r.RawP05Mu), F(r.RawP25Mu), F(r.RawP50Mu), F(r.RawP75Mu), F(r.RawP95Mu), F(r.RawBowleyMu) });
        vals.AddRange(new[] { F(r.RawP05LogSigma), F(r.RawP25LogSigma), F(r.RawP50LogSigma), F(r.RawP75LogSigma), F(r.RawP95LogSigma), F(r.RawBowleyLogSigma) });
        vals.AddRange(new[] { F(r.RawP05Gamma), F(r.RawP25Gamma), F(r.RawP50Gamma), F(r.RawP75Gamma), F(r.RawP95Gamma), F(r.RawBowleyGamma) });

        // Block C
        vals.AddRange(new[] { F(r.BcaP05Mu), F(r.BcaP25Mu), F(r.BcaP50Mu), F(r.BcaP75Mu), F(r.BcaP95Mu), F(r.BcaBowleyMu) });
        vals.AddRange(new[] { F(r.BcaP05LogSigma), F(r.BcaP25LogSigma), F(r.BcaP50LogSigma), F(r.BcaP75LogSigma), F(r.BcaP95LogSigma), F(r.BcaBowleyLogSigma) });
        vals.AddRange(new[] { F(r.BcaP05Gamma), F(r.BcaP25Gamma), F(r.BcaP50Gamma), F(r.BcaP75Gamma), F(r.BcaP95Gamma), F(r.BcaBowleyGamma) });

        // Block E
        vals.AddRange(new[] { F(r.BcaZ0Mu), F(r.BcaZ0LogSigma), F(r.BcaZ0Gamma),
            F(r.BcaAccelMu), F(r.BcaAccelLogSigma), F(r.BcaAccelGamma),
            F(r.BcaRMu), F(r.BcaRLogSigma), F(r.BcaRGamma) });

        // Block F
        vals.AddRange(new[] { F(r.RawTailratioMu), F(r.BcaTailratioMu),
            F(r.RawTailratioLogSigma), F(r.BcaTailratioLogSigma),
            F(r.RawTailratioGamma), F(r.BcaTailratioGamma) });

        // Block G
        vals.AddRange(new[] { F(r.RawCoskewMuGamma), F(r.RawCoskewSigmaGamma) });

        // Block H
        vals.AddRange(new[] { F(r.PitNuSigmaVar), F(r.PitNuSigmaKurt),
            F(r.PitNuGammaVar), F(r.PitNuGammaKurt), F(r.PitKsSigma), F(r.PitKsGamma) });

        // Block I: Cohn at 3 AEPs
        foreach (var c in new[] { r.Cohn_p01, r.Cohn_p001, r.Cohn_p0001 })
        {
            vals.AddRange(new[] { F(c.Beta1), F(c.Nu), F(c.RCohn),
                F(c.QHat), F(c.CILow), F(c.CIHigh),
                F(c.GradMu), F(c.GradSigma), F(c.GradGamma),
                F(c.ContribMu), F(c.ContribSigma), F(c.ContribGamma),
                F(c.VarQ), F(c.DeltaGamma) });
        }

        // Block J
        vals.AddRange(new[] { F(r.GmmVarMu), F(r.GmmVarSigma), F(r.GmmVarGamma),
            F(r.GmmCovMuSigma), F(r.GmmCovMuGamma), F(r.GmmCovSigmaGamma) });

        // Block K: Quantile CIs at 3 AEPs
        foreach (var q in new[] { r.QCI_p01, r.QCI_p001, r.QCI_p0001 })
        {
            vals.AddRange(new[] { F(q.RawBootCILo), F(q.RawBootCIHi),
                F(q.BcaCILo), F(q.BcaCIHi), F(q.BcaZ0Q), F(q.BcaAccelQ) });
        }

        return string.Join(",", vals);
    }

    #endregion

    #region Data Types

    /// <summary>
    /// Provides a helper struct used by the containing fixture or implementation.
    /// </summary>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private struct AEPDiagnostics
    {
        public double Beta1, Nu, RCohn;
        public double QHat, CILow, CIHigh;
        public double GradMu, GradSigma, GradGamma;
        public double ContribMu, ContribSigma, ContribGamma;
        public double VarQ, DeltaGamma;
    }

    /// <summary>
    /// Provides a helper struct used by the containing fixture or implementation.
    /// </summary>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private struct QuantileCIDiagnostics
    {
        public double RawBootCILo, RawBootCIHi;
        public double BcaCILo, BcaCIHi;
        public double BcaZ0Q, BcaAccelQ;
    }

    /// <summary>
    /// Provides a helper class used by the containing fixture or implementation.
    /// </summary>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private class DatasetResult
    {
        // Block D
        public double MuHat, SigmaHat, GammaHat;

        // Block A
        public double RawSdMu, RawSkewMu, RawKurtMu;
        public double RawSdSigma, RawSdLogSigma, RawSkewLogSigma, RawKurtLogSigma;
        public double RawSdGamma, RawSkewGamma, RawKurtGamma;
        public double RawCorrMuSigma, RawCorrMuGamma, RawCorrSigmaGamma;

        // Block B
        public double RawP05Mu, RawP25Mu, RawP50Mu, RawP75Mu, RawP95Mu, RawBowleyMu;
        public double RawP05LogSigma, RawP25LogSigma, RawP50LogSigma, RawP75LogSigma, RawP95LogSigma, RawBowleyLogSigma;
        public double RawP05Gamma, RawP25Gamma, RawP50Gamma, RawP75Gamma, RawP95Gamma, RawBowleyGamma;

        // Block C
        public double BcaP05Mu, BcaP25Mu, BcaP50Mu, BcaP75Mu, BcaP95Mu, BcaBowleyMu;
        public double BcaP05LogSigma, BcaP25LogSigma, BcaP50LogSigma, BcaP75LogSigma, BcaP95LogSigma, BcaBowleyLogSigma;
        public double BcaP05Gamma, BcaP25Gamma, BcaP50Gamma, BcaP75Gamma, BcaP95Gamma, BcaBowleyGamma;

        // Block E
        public double BcaZ0Mu, BcaZ0LogSigma, BcaZ0Gamma;
        public double BcaAccelMu, BcaAccelLogSigma, BcaAccelGamma;
        public double BcaRMu, BcaRLogSigma, BcaRGamma;

        // Block F
        public double RawTailratioMu, BcaTailratioMu;
        public double RawTailratioLogSigma, BcaTailratioLogSigma;
        public double RawTailratioGamma, BcaTailratioGamma;

        // Block G
        public double RawCoskewMuGamma, RawCoskewSigmaGamma;

        // Block H
        public double PitNuSigmaVar, PitNuSigmaKurt;
        public double PitNuGammaVar, PitNuGammaKurt;
        public double PitKsSigma, PitKsGamma;

        // Block I
        public AEPDiagnostics Cohn_p01, Cohn_p001, Cohn_p0001;

        // Block J
        public double GmmVarMu, GmmVarSigma, GmmVarGamma;
        public double GmmCovMuSigma, GmmCovMuGamma, GmmCovSigmaGamma;

        // Block K
        public QuantileCIDiagnostics QCI_p01, QCI_p001, QCI_p0001;
    }

    /// <summary>
    /// Cell-level result: averages of DatasetResult fields over M valid datasets.
    /// Uses the same field layout as DatasetResult for simplicity.
    /// </summary>
    private class CellResult : DatasetResult
    {
        public int ValidCount;
    }

    /// <summary>
    /// Accumulates DatasetResult values for cell-level averaging.
    /// </summary>
    private class CellAccumulator
    {
        private readonly List<DatasetResult> _results = new();

        /// <summary>
        /// Supports the <c>Add</c> helper.
        /// </summary>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public void Add(DatasetResult ds) => _results.Add(ds);

        /// <summary>
        /// Supports the <c>Finalize</c> helper.
        /// </summary>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public CellResult Finalize()
        {
            int count = _results.Count;
            var r = new CellResult { ValidCount = count };
            if (count == 0) return r;

            // Average all double fields using reflection would be clean but slow.
            // Instead, sum manually for each field.
            foreach (var ds in _results)
            {
                // Block D
                r.MuHat += ds.MuHat; r.SigmaHat += ds.SigmaHat; r.GammaHat += ds.GammaHat;

                // Block A
                r.RawSdMu += ds.RawSdMu; r.RawSkewMu += ds.RawSkewMu; r.RawKurtMu += ds.RawKurtMu;
                r.RawSdSigma += ds.RawSdSigma; r.RawSdLogSigma += ds.RawSdLogSigma;
                r.RawSkewLogSigma += ds.RawSkewLogSigma; r.RawKurtLogSigma += ds.RawKurtLogSigma;
                r.RawSdGamma += ds.RawSdGamma; r.RawSkewGamma += ds.RawSkewGamma; r.RawKurtGamma += ds.RawKurtGamma;
                r.RawCorrMuSigma += ds.RawCorrMuSigma; r.RawCorrMuGamma += ds.RawCorrMuGamma;
                r.RawCorrSigmaGamma += ds.RawCorrSigmaGamma;

                // Block B
                r.RawP05Mu += ds.RawP05Mu; r.RawP25Mu += ds.RawP25Mu; r.RawP50Mu += ds.RawP50Mu;
                r.RawP75Mu += ds.RawP75Mu; r.RawP95Mu += ds.RawP95Mu; r.RawBowleyMu += ds.RawBowleyMu;
                r.RawP05LogSigma += ds.RawP05LogSigma; r.RawP25LogSigma += ds.RawP25LogSigma;
                r.RawP50LogSigma += ds.RawP50LogSigma; r.RawP75LogSigma += ds.RawP75LogSigma;
                r.RawP95LogSigma += ds.RawP95LogSigma; r.RawBowleyLogSigma += ds.RawBowleyLogSigma;
                r.RawP05Gamma += ds.RawP05Gamma; r.RawP25Gamma += ds.RawP25Gamma;
                r.RawP50Gamma += ds.RawP50Gamma; r.RawP75Gamma += ds.RawP75Gamma;
                r.RawP95Gamma += ds.RawP95Gamma; r.RawBowleyGamma += ds.RawBowleyGamma;

                // Block C
                r.BcaP05Mu += ds.BcaP05Mu; r.BcaP25Mu += ds.BcaP25Mu; r.BcaP50Mu += ds.BcaP50Mu;
                r.BcaP75Mu += ds.BcaP75Mu; r.BcaP95Mu += ds.BcaP95Mu; r.BcaBowleyMu += ds.BcaBowleyMu;
                r.BcaP05LogSigma += ds.BcaP05LogSigma; r.BcaP25LogSigma += ds.BcaP25LogSigma;
                r.BcaP50LogSigma += ds.BcaP50LogSigma; r.BcaP75LogSigma += ds.BcaP75LogSigma;
                r.BcaP95LogSigma += ds.BcaP95LogSigma; r.BcaBowleyLogSigma += ds.BcaBowleyLogSigma;
                r.BcaP05Gamma += ds.BcaP05Gamma; r.BcaP25Gamma += ds.BcaP25Gamma;
                r.BcaP50Gamma += ds.BcaP50Gamma; r.BcaP75Gamma += ds.BcaP75Gamma;
                r.BcaP95Gamma += ds.BcaP95Gamma; r.BcaBowleyGamma += ds.BcaBowleyGamma;

                // Block E
                r.BcaZ0Mu += ds.BcaZ0Mu; r.BcaZ0LogSigma += ds.BcaZ0LogSigma; r.BcaZ0Gamma += ds.BcaZ0Gamma;
                r.BcaAccelMu += ds.BcaAccelMu; r.BcaAccelLogSigma += ds.BcaAccelLogSigma; r.BcaAccelGamma += ds.BcaAccelGamma;
                r.BcaRMu += ds.BcaRMu; r.BcaRLogSigma += ds.BcaRLogSigma; r.BcaRGamma += ds.BcaRGamma;

                // Block F
                r.RawTailratioMu += ds.RawTailratioMu; r.BcaTailratioMu += ds.BcaTailratioMu;
                r.RawTailratioLogSigma += ds.RawTailratioLogSigma; r.BcaTailratioLogSigma += ds.BcaTailratioLogSigma;
                r.RawTailratioGamma += ds.RawTailratioGamma; r.BcaTailratioGamma += ds.BcaTailratioGamma;

                // Block G
                r.RawCoskewMuGamma += ds.RawCoskewMuGamma; r.RawCoskewSigmaGamma += ds.RawCoskewSigmaGamma;

                // Block H
                r.PitNuSigmaVar += ds.PitNuSigmaVar; r.PitNuSigmaKurt += ds.PitNuSigmaKurt;
                r.PitNuGammaVar += ds.PitNuGammaVar; r.PitNuGammaKurt += ds.PitNuGammaKurt;
                r.PitKsSigma += ds.PitKsSigma; r.PitKsGamma += ds.PitKsGamma;

                // Block I (3 AEPs)
                r.Cohn_p01 = AddAEP(r.Cohn_p01, ds.Cohn_p01);
                r.Cohn_p001 = AddAEP(r.Cohn_p001, ds.Cohn_p001);
                r.Cohn_p0001 = AddAEP(r.Cohn_p0001, ds.Cohn_p0001);

                // Block J
                r.GmmVarMu += ds.GmmVarMu; r.GmmVarSigma += ds.GmmVarSigma; r.GmmVarGamma += ds.GmmVarGamma;
                r.GmmCovMuSigma += ds.GmmCovMuSigma; r.GmmCovMuGamma += ds.GmmCovMuGamma;
                r.GmmCovSigmaGamma += ds.GmmCovSigmaGamma;

                // Block K (3 AEPs)
                r.QCI_p01 = AddQCI(r.QCI_p01, ds.QCI_p01);
                r.QCI_p001 = AddQCI(r.QCI_p001, ds.QCI_p001);
                r.QCI_p0001 = AddQCI(r.QCI_p0001, ds.QCI_p0001);
            }

            // Divide by count
            double c = count;
            r.MuHat /= c; r.SigmaHat /= c; r.GammaHat /= c;

            r.RawSdMu /= c; r.RawSkewMu /= c; r.RawKurtMu /= c;
            r.RawSdSigma /= c; r.RawSdLogSigma /= c; r.RawSkewLogSigma /= c; r.RawKurtLogSigma /= c;
            r.RawSdGamma /= c; r.RawSkewGamma /= c; r.RawKurtGamma /= c;
            r.RawCorrMuSigma /= c; r.RawCorrMuGamma /= c; r.RawCorrSigmaGamma /= c;

            r.RawP05Mu /= c; r.RawP25Mu /= c; r.RawP50Mu /= c; r.RawP75Mu /= c; r.RawP95Mu /= c; r.RawBowleyMu /= c;
            r.RawP05LogSigma /= c; r.RawP25LogSigma /= c; r.RawP50LogSigma /= c; r.RawP75LogSigma /= c; r.RawP95LogSigma /= c; r.RawBowleyLogSigma /= c;
            r.RawP05Gamma /= c; r.RawP25Gamma /= c; r.RawP50Gamma /= c; r.RawP75Gamma /= c; r.RawP95Gamma /= c; r.RawBowleyGamma /= c;

            r.BcaP05Mu /= c; r.BcaP25Mu /= c; r.BcaP50Mu /= c; r.BcaP75Mu /= c; r.BcaP95Mu /= c; r.BcaBowleyMu /= c;
            r.BcaP05LogSigma /= c; r.BcaP25LogSigma /= c; r.BcaP50LogSigma /= c; r.BcaP75LogSigma /= c; r.BcaP95LogSigma /= c; r.BcaBowleyLogSigma /= c;
            r.BcaP05Gamma /= c; r.BcaP25Gamma /= c; r.BcaP50Gamma /= c; r.BcaP75Gamma /= c; r.BcaP95Gamma /= c; r.BcaBowleyGamma /= c;

            r.BcaZ0Mu /= c; r.BcaZ0LogSigma /= c; r.BcaZ0Gamma /= c;
            r.BcaAccelMu /= c; r.BcaAccelLogSigma /= c; r.BcaAccelGamma /= c;
            r.BcaRMu /= c; r.BcaRLogSigma /= c; r.BcaRGamma /= c;

            r.RawTailratioMu /= c; r.BcaTailratioMu /= c;
            r.RawTailratioLogSigma /= c; r.BcaTailratioLogSigma /= c;
            r.RawTailratioGamma /= c; r.BcaTailratioGamma /= c;

            r.RawCoskewMuGamma /= c; r.RawCoskewSigmaGamma /= c;

            r.PitNuSigmaVar /= c; r.PitNuSigmaKurt /= c;
            r.PitNuGammaVar /= c; r.PitNuGammaKurt /= c;
            r.PitKsSigma /= c; r.PitKsGamma /= c;

            r.Cohn_p01 = DivAEP(r.Cohn_p01, c);
            r.Cohn_p001 = DivAEP(r.Cohn_p001, c);
            r.Cohn_p0001 = DivAEP(r.Cohn_p0001, c);

            r.GmmVarMu /= c; r.GmmVarSigma /= c; r.GmmVarGamma /= c;
            r.GmmCovMuSigma /= c; r.GmmCovMuGamma /= c; r.GmmCovSigmaGamma /= c;

            r.QCI_p01 = DivQCI(r.QCI_p01, c);
            r.QCI_p001 = DivQCI(r.QCI_p001, c);
            r.QCI_p0001 = DivQCI(r.QCI_p0001, c);

            return r;
        }

        /// <summary>
        /// Adds aEP.
        /// </summary>
        /// <param name="a">The a value.</param>
        /// <param name="b">The diagnostic values to add.</param>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        private static AEPDiagnostics AddAEP(AEPDiagnostics a, AEPDiagnostics b) => new()
        {
            Beta1 = a.Beta1 + b.Beta1, Nu = a.Nu + b.Nu, RCohn = a.RCohn + b.RCohn,
            QHat = a.QHat + b.QHat, CILow = a.CILow + b.CILow, CIHigh = a.CIHigh + b.CIHigh,
            GradMu = a.GradMu + b.GradMu, GradSigma = a.GradSigma + b.GradSigma, GradGamma = a.GradGamma + b.GradGamma,
            ContribMu = a.ContribMu + b.ContribMu, ContribSigma = a.ContribSigma + b.ContribSigma,
            ContribGamma = a.ContribGamma + b.ContribGamma,
            VarQ = a.VarQ + b.VarQ, DeltaGamma = a.DeltaGamma + b.DeltaGamma
        };

        /// <summary>
        /// Divides aEP.
        /// </summary>
        /// <param name="a">The a value.</param>
        /// <param name="c">The divisor used to average the diagnostic values.</param>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        private static AEPDiagnostics DivAEP(AEPDiagnostics a, double c) => new()
        {
            Beta1 = a.Beta1 / c, Nu = a.Nu / c, RCohn = a.RCohn / c,
            QHat = a.QHat / c, CILow = a.CILow / c, CIHigh = a.CIHigh / c,
            GradMu = a.GradMu / c, GradSigma = a.GradSigma / c, GradGamma = a.GradGamma / c,
            ContribMu = a.ContribMu / c, ContribSigma = a.ContribSigma / c, ContribGamma = a.ContribGamma / c,
            VarQ = a.VarQ / c, DeltaGamma = a.DeltaGamma / c
        };

        /// <summary>
        /// Adds qCI.
        /// </summary>
        /// <param name="a">The a value.</param>
        /// <param name="b">The diagnostic values to add.</param>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        private static QuantileCIDiagnostics AddQCI(QuantileCIDiagnostics a, QuantileCIDiagnostics b) => new()
        {
            RawBootCILo = a.RawBootCILo + b.RawBootCILo, RawBootCIHi = a.RawBootCIHi + b.RawBootCIHi,
            BcaCILo = a.BcaCILo + b.BcaCILo, BcaCIHi = a.BcaCIHi + b.BcaCIHi,
            BcaZ0Q = a.BcaZ0Q + b.BcaZ0Q, BcaAccelQ = a.BcaAccelQ + b.BcaAccelQ
        };

        /// <summary>
        /// Divides qCI.
        /// </summary>
        /// <param name="a">The a value.</param>
        /// <param name="c">The divisor used to average the diagnostic values.</param>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        private static QuantileCIDiagnostics DivQCI(QuantileCIDiagnostics a, double c) => new()
        {
            RawBootCILo = a.RawBootCILo / c, RawBootCIHi = a.RawBootCIHi / c,
            BcaCILo = a.BcaCILo / c, BcaCIHi = a.BcaCIHi / c,
            BcaZ0Q = a.BcaZ0Q / c, BcaAccelQ = a.BcaAccelQ / c
        };
    }

    #endregion
}
