using Numerics.Distributions;
using Numerics.Sampling;
using System.Diagnostics;
using System.Text;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// BCa bootstrap calibration experiments for Gamma distribution link function parameters.
/// Characterizes the sampling distribution shape of theta-hat (scale) and kappa-hat (shape)
/// across a grid of true kappa values.
/// </summary>
/// <remarks>
/// <para>
/// Gamma MOM: theta_hat = std^2/mean, kappa_hat = mean^2/std^2. Both parameters are positive
/// and use LogSES links. The BCa diagnostics are theta-independent (affine invariance of MOM),
/// so we fix theta=1 and vary only kappa.
/// </para>
/// <para>
/// Gamma skewness = 2/sqrt(kappa), so small kappa gives high skew (like Exponential)
/// and large kappa gives near-Normal behavior.
/// </para>
/// <para>
/// The output CSV table drives link parameter calibration for the Gamma branch of
/// <c>GetDistributionsFromLinkedMultivariateNormal()</c>.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class GammaLinkCalibrationTests
{
    /// <summary>
    /// Shape parameter grid spanning from extreme skew (kappa=0.5, skew=2.83) to
    /// near-symmetric (kappa=64, skew=0.25).
    /// </summary>
    private static readonly double[] Kappas = { 0.5, 1.0, 2.0, 4.0, 8.0, 16.0, 32.0, 64.0 };

    /// <summary>
    /// Fixed scale parameter. BCa diagnostics are theta-independent for MOM.
    /// </summary>
    private const double ThetaTrue = 1.0;

    /// <summary>
    /// Sample size. Using n=50 consistent with LP3 experiments.
    /// </summary>
    private const int SampleSize = 50;

    /// <summary>
    /// Number of independent datasets per kappa cell for Monte Carlo averaging.
    /// </summary>
    private const int DatasetsPerCell = 30;

    /// <summary>
    /// Number of parametric bootstrap replications per dataset.
    /// </summary>
    private const int BootstrapReplications = 10000;

    #region Main Experiment

    /// <summary>
    /// BCa-corrected bootstrap parameter distribution shape for Gamma.
    /// Outputs a CSV table of theta-hat and kappa-hat diagnostics across all kappa values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Design matrix: 8 kappa x 1 theta x 1 n = 8 cells x 30 datasets x 10K bootstrap.
    /// Expected runtime: ~15 minutes.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void BootstrapParameterShape_GammaAllCells()
    {
        var masterRng = new MersenneTwister(54321);
        var csv = new StringBuilder();

        csv.AppendLine("kappa_true,n,valid_count,mean_theta_hat,mean_kappa_hat," +
            "R_log_theta,z0_theta,accel_theta," +
            "R_log_kappa,z0_kappa,accel_kappa," +
            "BCa_lo_theta,BCa_hi_theta,BCa_lo_kappa,BCa_hi_kappa");

        foreach (double kappa in Kappas)
        {
            var cellResult = RunCellExperiment(kappa, masterRng);

            csv.AppendLine(
                $"{kappa:F1},{SampleSize},{cellResult.ValidCount}," +
                $"{cellResult.MeanThetaHat:F6},{cellResult.MeanKappaHat:F6}," +
                $"{cellResult.RLogTheta:F4},{cellResult.Z0Theta:F4},{cellResult.AccelTheta:F6}," +
                $"{cellResult.RLogKappa:F4},{cellResult.Z0Kappa:F4},{cellResult.AccelKappa:F6}," +
                $"{cellResult.BCaLoTheta:F6},{cellResult.BCaHiTheta:F6}," +
                $"{cellResult.BCaLoKappa:F6},{cellResult.BCaHiKappa:F6}");
        }

        // Dump clean CSV block at the end
        Debug.WriteLine("");
        Debug.WriteLine("========== GAMMA BCa CALIBRATION RESULTS ==========");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END GAMMA BCa RESULTS ==========");
    }

    #endregion

    #region Cell Experiment

    /// <summary>
    /// Runs the BCa bootstrap experiment for a single kappa cell,
    /// averaging diagnostics over M independent datasets.
    /// </summary>
    /// <param name="kappa">True Gamma shape parameter.</param>
    /// <param name="masterRng">Master RNG for generating per-dataset seeds.</param>
    /// <returns>Averaged BCa diagnostics for the cell.</returns>
    private static GammaCellDiagnostics RunCellExperiment(double kappa, MersenneTwister masterRng)
    {
        double sumThetaHat = 0, sumKappaHat = 0;
        double sumRLogTheta = 0, sumZ0Theta = 0, sumAccelTheta = 0;
        double sumRLogKappa = 0, sumZ0Kappa = 0, sumAccelKappa = 0;
        double sumBCaLoTheta = 0, sumBCaHiTheta = 0;
        double sumBCaLoKappa = 0, sumBCaHiKappa = 0;
        int validCount = 0;

        for (int m = 0; m < DatasetsPerCell; m++)
        {
            int seed = masterRng.Next();

            try
            {
                var result = RunSingleDataset(kappa, seed);
                if (result == null) continue;

                sumThetaHat += result.Value.ThetaHat;
                sumKappaHat += result.Value.KappaHat;
                sumRLogTheta += result.Value.RLogTheta;
                sumZ0Theta += result.Value.Z0Theta;
                sumAccelTheta += result.Value.AccelTheta;
                sumRLogKappa += result.Value.RLogKappa;
                sumZ0Kappa += result.Value.Z0Kappa;
                sumAccelKappa += result.Value.AccelKappa;
                sumBCaLoTheta += result.Value.BCaLoTheta;
                sumBCaHiTheta += result.Value.BCaHiTheta;
                sumBCaLoKappa += result.Value.BCaLoKappa;
                sumBCaHiKappa += result.Value.BCaHiKappa;
                validCount++;
            }
            catch
            {
                // Silently skip failed datasets; parameter blow-ups are expected
                // at small kappa. validCount tracks success rate.
            }
        }

        if (validCount == 0)
        {
            return new GammaCellDiagnostics
            {
                ValidCount = 0,
                MeanThetaHat = double.NaN, MeanKappaHat = double.NaN,
                RLogTheta = double.NaN, Z0Theta = double.NaN, AccelTheta = double.NaN,
                RLogKappa = double.NaN, Z0Kappa = double.NaN, AccelKappa = double.NaN,
                BCaLoTheta = double.NaN, BCaHiTheta = double.NaN,
                BCaLoKappa = double.NaN, BCaHiKappa = double.NaN
            };
        }

        return new GammaCellDiagnostics
        {
            ValidCount = validCount,
            MeanThetaHat = sumThetaHat / validCount,
            MeanKappaHat = sumKappaHat / validCount,
            RLogTheta = sumRLogTheta / validCount,
            Z0Theta = sumZ0Theta / validCount,
            AccelTheta = sumAccelTheta / validCount,
            RLogKappa = sumRLogKappa / validCount,
            Z0Kappa = sumZ0Kappa / validCount,
            AccelKappa = sumAccelKappa / validCount,
            BCaLoTheta = sumBCaLoTheta / validCount,
            BCaHiTheta = sumBCaHiTheta / validCount,
            BCaLoKappa = sumBCaLoKappa / validCount,
            BCaHiKappa = sumBCaHiKappa / validCount
        };
    }

    /// <summary>
    /// Runs BCa bootstrap for a single Gamma dataset and returns parameter diagnostics.
    /// </summary>
    /// <param name="kappa">True Gamma shape parameter.</param>
    /// <param name="seed">RNG seed for this dataset.</param>
    /// <returns>Diagnostics for this dataset, or null if estimation failed.</returns>
    private static GammaDatasetDiagnostics? RunSingleDataset(double kappa, int seed)
    {
        // Generate random sample from true Gamma(theta=1, kappa)
        var trueDist = new GammaDistribution(ThetaTrue, kappa);
        var sample = trueDist.GenerateRandomValues(SampleSize, seed);
        var sampleList = sample.ToList();

        // Fit Gamma via Method of Moments
        var fitDist = new GammaDistribution();
        ((IEstimation)fitDist).Estimate(sampleList, ParameterEstimationMethod.MethodOfMoments);
        double thetaHat = fitDist.Theta;
        double kappaHat = fitDist.Kappa;

        // Guard: both parameters must be positive
        if (thetaHat <= 0 || kappaHat <= 0) return null;

        // Parametric bootstrap: resample from fitted distribution, re-estimate
        var bootstrap = new BootstrapAnalysis(
            fitDist,
            ParameterEstimationMethod.MethodOfMoments,
            SampleSize,
            replications: BootstrapReplications,
            seed: seed + 1);

        double[,] bootParams = bootstrap.Parameters(); // B x 2

        // Extract theta-hat* and kappa-hat* columns, filter NaN and non-positive
        var thetaStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 0, v => !double.IsNaN(v) && v > 0);
        var kappaStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 1, v => !double.IsNaN(v) && v > 0);

        if (thetaStars.Length < 100 || kappaStars.Length < 100)
            return null;

        // Jackknife factory for Gamma MOM
        double[]? GammaMomEstimate(List<double> s)
        {
            var d = new GammaDistribution();
            ((IEstimation)d).Estimate(s, ParameterEstimationMethod.MethodOfMoments);
            return d.GetParameters;
        }

        // Jackknife estimates for theta and kappa
        var jackTheta = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 0, GammaMomEstimate);
        var jackKappa = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 1, GammaMomEstimate);

        // BCa CIs
        var bcaTheta = BCaCalibrationHelpers.BCaParameterCI(thetaStars, thetaHat, jackTheta);
        var bcaKappa = BCaCalibrationHelpers.BCaParameterCI(kappaStars, kappaHat, jackKappa);

        // CI asymmetry ratios on log scale (both parameters are positive)
        double rLogTheta = BCaCalibrationHelpers.ComputeAsymmetryRatio(
            Math.Log(bcaTheta.Lower), Math.Log(bcaTheta.Upper), Math.Log(thetaHat));
        double rLogKappa = BCaCalibrationHelpers.ComputeAsymmetryRatio(
            Math.Log(bcaKappa.Lower), Math.Log(bcaKappa.Upper), Math.Log(kappaHat));

        return new GammaDatasetDiagnostics
        {
            ThetaHat = thetaHat,
            KappaHat = kappaHat,
            RLogTheta = rLogTheta,
            Z0Theta = bcaTheta.Z0,
            AccelTheta = bcaTheta.Accel,
            RLogKappa = rLogKappa,
            Z0Kappa = bcaKappa.Z0,
            AccelKappa = bcaKappa.Accel,
            BCaLoTheta = bcaTheta.Lower,
            BCaHiTheta = bcaTheta.Upper,
            BCaLoKappa = bcaKappa.Lower,
            BCaHiKappa = bcaKappa.Upper
        };
    }

    #endregion

    #region Data Types

    /// <summary>
    /// Diagnostics from a single Gamma dataset's BCa bootstrap.
    /// </summary>
    private struct GammaDatasetDiagnostics
    {
        public double ThetaHat;
        public double KappaHat;
        public double RLogTheta;
        public double Z0Theta;
        public double AccelTheta;
        public double RLogKappa;
        public double Z0Kappa;
        public double AccelKappa;
        public double BCaLoTheta;
        public double BCaHiTheta;
        public double BCaLoKappa;
        public double BCaHiKappa;
    }

    /// <summary>
    /// Averaged diagnostics for a kappa cell.
    /// </summary>
    private struct GammaCellDiagnostics
    {
        public int ValidCount;
        public double MeanThetaHat;
        public double MeanKappaHat;
        public double RLogTheta;
        public double Z0Theta;
        public double AccelTheta;
        public double RLogKappa;
        public double Z0Kappa;
        public double AccelKappa;
        public double BCaLoTheta;
        public double BCaHiTheta;
        public double BCaLoKappa;
        public double BCaHiKappa;
    }

    #endregion
}
