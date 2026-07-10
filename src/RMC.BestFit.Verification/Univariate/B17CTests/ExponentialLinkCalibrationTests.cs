using Numerics.Distributions;
using Numerics.Sampling;
using System.Diagnostics;
using System.Text;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// BCa bootstrap calibration experiment for Exponential distribution link function parameters.
/// Characterizes the sampling distribution shape of xi-hat (location) and alpha-hat (scale).
/// </summary>
/// <remarks>
/// <para>
/// Exponential MOM: xi_hat = mean - std, alpha_hat = std.
/// </para>
/// <para>
/// The location parameter xi is bounded above by min(data), making the sampling distribution
/// of xi-hat negatively skewed (left tail heavier, R_xi &lt; 1). This requires a CenteredSES
/// link with negative lambda.
/// </para>
/// <para>
/// The scale parameter alpha_hat = std follows a chi-squared-like distribution.
/// Its BCa asymmetry should match Gamma at kappa=1 (since Exponential is Gamma with kappa=1).
/// </para>
/// <para>
/// Exponential has no free shape parameter, so BCa diagnostics depend only on n.
/// A single cell experiment suffices, but we use higher M=100 for precision.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ExponentialLinkCalibrationTests
{
    /// <summary>
    /// Fixed location parameter. Diagnostics are xi-independent (affine invariance).
    /// </summary>
    private const double XiTrue = 0.0;

    /// <summary>
    /// Fixed scale parameter. Diagnostics are alpha-independent (affine invariance).
    /// </summary>
    private const double AlphaTrue = 1.0;

    /// <summary>
    /// Sample size. Using n=50 consistent with other calibration experiments.
    /// </summary>
    private const int SampleSize = 50;

    /// <summary>
    /// Higher number of datasets for single-cell precision.
    /// </summary>
    private const int DatasetsPerCell = 100;

    /// <summary>
    /// Number of parametric bootstrap replications per dataset.
    /// </summary>
    private const int BootstrapReplications = 10000;

    #region Main Experiment

    /// <summary>
    /// BCa-corrected bootstrap parameter distribution shape for Exponential.
    /// Outputs diagnostics for both location (xi) and scale (alpha) parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Design: 1 cell x 100 datasets x 10K bootstrap. Expected runtime: ~10 minutes.
    /// </para>
    /// <para>
    /// Key outputs:
    /// <list type="bullet">
    ///     <item><description>R_xi: expected &lt; 1 (negatively skewed location)</description></item>
    ///     <item><description>R_log_alpha: should match Gamma kappa=1 (cross-validation)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void BootstrapParameterShape_Exponential()
    {
        var masterRng = new MersenneTwister(99999);

        // Accumulators
        double sumXiHat = 0, sumAlphaHat = 0;
        double sumRXi = 0, sumZ0Xi = 0, sumAccelXi = 0;
        double sumRLogAlpha = 0, sumZ0Alpha = 0, sumAccelAlpha = 0;
        double sumBCaLoXi = 0, sumBCaHiXi = 0;
        double sumBCaLoAlpha = 0, sumBCaHiAlpha = 0;
        int validCount = 0;

        for (int m = 0; m < DatasetsPerCell; m++)
        {
            int seed = masterRng.Next();

            try
            {
                var result = RunSingleDataset(seed);
                if (result == null) continue;

                sumXiHat += result.Value.XiHat;
                sumAlphaHat += result.Value.AlphaHat;
                sumRXi += result.Value.RXi;
                sumZ0Xi += result.Value.Z0Xi;
                sumAccelXi += result.Value.AccelXi;
                sumRLogAlpha += result.Value.RLogAlpha;
                sumZ0Alpha += result.Value.Z0Alpha;
                sumAccelAlpha += result.Value.AccelAlpha;
                sumBCaLoXi += result.Value.BCaLoXi;
                sumBCaHiXi += result.Value.BCaHiXi;
                sumBCaLoAlpha += result.Value.BCaLoAlpha;
                sumBCaHiAlpha += result.Value.BCaHiAlpha;
                validCount++;
            }
            catch
            {
                // Silently skip failed datasets
            }
        }

        // Dump clean results at the end, separated from any exception noise
        var csv = new StringBuilder();
        csv.AppendLine("xi_true,alpha_true,n,valid_count," +
            "mean_xi_hat,mean_alpha_hat," +
            "R_xi,z0_xi,accel_xi," +
            "R_log_alpha,z0_alpha,accel_alpha," +
            "BCa_lo_xi,BCa_hi_xi,BCa_lo_alpha,BCa_hi_alpha");

        if (validCount > 0)
        {
            double meanXiHat = sumXiHat / validCount;
            double meanAlphaHat = sumAlphaHat / validCount;
            double rXi = sumRXi / validCount;
            double z0Xi = sumZ0Xi / validCount;
            double accelXi = sumAccelXi / validCount;
            double rLogAlpha = sumRLogAlpha / validCount;
            double z0Alpha = sumZ0Alpha / validCount;
            double accelAlpha = sumAccelAlpha / validCount;

            csv.AppendLine(
                $"{XiTrue:F1},{AlphaTrue:F1},{SampleSize},{validCount}," +
                $"{meanXiHat:F6},{meanAlphaHat:F6}," +
                $"{rXi:F4},{z0Xi:F4},{accelXi:F6}," +
                $"{rLogAlpha:F4},{z0Alpha:F4},{accelAlpha:F6}," +
                $"{sumBCaLoXi / validCount:F6},{sumBCaHiXi / validCount:F6}," +
                $"{sumBCaLoAlpha / validCount:F6},{sumBCaHiAlpha / validCount:F6}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== EXPONENTIAL BCa CALIBRATION RESULTS ==========");
        Debug.WriteLine($"Valid datasets: {validCount} / {DatasetsPerCell}");
        Debug.WriteLine("");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END EXPONENTIAL BCa RESULTS ==========");
    }

    #endregion

    #region Single Dataset

    /// <summary>
    /// Runs BCa bootstrap for a single Exponential dataset and returns parameter diagnostics.
    /// </summary>
    /// <param name="seed">RNG seed for this dataset.</param>
    /// <returns>Diagnostics for this dataset, or null if estimation failed.</returns>
    private static ExpDatasetDiagnostics? RunSingleDataset(int seed)
    {
        // Generate random sample from true Exponential(xi=0, alpha=1)
        var trueDist = new Exponential(XiTrue, AlphaTrue);
        var sample = trueDist.GenerateRandomValues(SampleSize, seed);
        var sampleList = sample.ToList();

        // Fit Exponential via Method of Moments
        var fitDist = new Exponential();
        ((IEstimation)fitDist).Estimate(sampleList, ParameterEstimationMethod.MethodOfMoments);
        double xiHat = fitDist.Xi;
        double alphaHat = fitDist.Alpha;

        // Guard: scale must be positive
        if (alphaHat <= 0) return null;

        // Parametric bootstrap: resample from fitted distribution, re-estimate
        var bootstrap = new BootstrapAnalysis(
            fitDist,
            ParameterEstimationMethod.MethodOfMoments,
            SampleSize,
            replications: BootstrapReplications,
            seed: seed + 1);

        double[,] bootParams = bootstrap.Parameters(); // B x 2

        // Extract xi-hat* and alpha-hat* columns
        // xi can be any real value; alpha must be positive
        var xiStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 0, v => !double.IsNaN(v));
        var alphaStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 1, v => !double.IsNaN(v) && v > 0);

        if (xiStars.Length < 100 || alphaStars.Length < 100)
            return null;

        // Jackknife factory for Exponential MOM
        double[]? ExponentialMomEstimate(List<double> s)
        {
            var d = new Exponential();
            ((IEstimation)d).Estimate(s, ParameterEstimationMethod.MethodOfMoments);
            return d.GetParameters;
        }

        // Jackknife estimates for xi and alpha
        var jackXi = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 0, ExponentialMomEstimate);
        var jackAlpha = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 1, ExponentialMomEstimate);

        // BCa CIs
        var bcaXi = BCaCalibrationHelpers.BCaParameterCI(xiStars, xiHat, jackXi);
        var bcaAlpha = BCaCalibrationHelpers.BCaParameterCI(alphaStars, alphaHat, jackAlpha);

        // R_xi in real space (xi can be negative, so no log)
        double rXi = BCaCalibrationHelpers.ComputeAsymmetryRatio(bcaXi.Lower, bcaXi.Upper, xiHat);

        // R_log_alpha on log scale (alpha is positive)
        double rLogAlpha = BCaCalibrationHelpers.ComputeAsymmetryRatio(
            Math.Log(bcaAlpha.Lower), Math.Log(bcaAlpha.Upper), Math.Log(alphaHat));

        return new ExpDatasetDiagnostics
        {
            XiHat = xiHat,
            AlphaHat = alphaHat,
            RXi = rXi,
            Z0Xi = bcaXi.Z0,
            AccelXi = bcaXi.Accel,
            RLogAlpha = rLogAlpha,
            Z0Alpha = bcaAlpha.Z0,
            AccelAlpha = bcaAlpha.Accel,
            BCaLoXi = bcaXi.Lower,
            BCaHiXi = bcaXi.Upper,
            BCaLoAlpha = bcaAlpha.Lower,
            BCaHiAlpha = bcaAlpha.Upper
        };
    }

    #endregion

    #region Data Types

    /// <summary>
    /// Diagnostics from a single Exponential dataset's BCa bootstrap.
    /// </summary>
    private struct ExpDatasetDiagnostics
    {
        public double XiHat;
        public double AlphaHat;
        public double RXi;
        public double Z0Xi;
        public double AccelXi;
        public double RLogAlpha;
        public double Z0Alpha;
        public double AccelAlpha;
        public double BCaLoXi;
        public double BCaHiXi;
        public double BCaLoAlpha;
        public double BCaHiAlpha;
    }

    #endregion
}
