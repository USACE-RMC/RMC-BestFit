using Numerics.Distributions;
using Numerics.Sampling;
using System.Diagnostics;
using System.Text;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// BCa bootstrap calibration experiments for LP3/P3 link function parameters.
/// Characterizes the sampling distribution shape of sigma-hat and gamma-hat across a
/// grid of true gamma values. Extended to gamma in [-4, 4] for safe extrapolation.
/// </summary>
/// <remarks>
/// <para>
/// P3 and LP3 share identical MOM structure (product moments on their respective data space),
/// so BCa diagnostics are the same. Normal (gamma=0 of P3) and LogNormal (gamma=0 of LP3)
/// are automatically covered.
/// </para>
/// <para>
/// The output CSV table drives link parameter calibration for the P3/LP3 branch of
/// <c>GetDistributionsFromLinkedMultivariateNormal()</c>.
/// </para>
/// <para>
/// Reference: Efron, B. (1987). Better Bootstrap Confidence Intervals. JASA, 82(397), 171-185.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class LinkCalibrationTests
{
    /// <summary>
    /// Extended gamma grid covering the full practical range for P3/LP3.
    /// Cap at +/-1.5 because MOM bootstrap becomes unreliable at higher |gamma| with n=50.
    /// Fitted formulas must extrapolate safely to +/-6 (LP3 bound) and +/-6 (P3 bound).
    /// </summary>
    private static readonly double[] Gammas = { -1.5, -1.0, -0.5, -0.25, -0.1, -0.05, 0.0, 0.05, 0.1, 0.25, 0.5, 1.0, 1.5 };

    /// <summary>
    /// Reduced sigma set. Previous experiments confirmed sigma-independence of BCa diagnostics.
    /// Three values are sufficient for verification.
    /// </summary>
    private static readonly double[] Sigmas = { 0.3, 0.5, 0.7 };

    /// <summary>
    /// Sample sizes for characterizing variance-dependence of BCa diagnostics.
    /// Three values provide distinct bootstrap variance levels for fitting
    /// R(gamma, delta) and z0(gamma, delta) relationships.
    /// </summary>
    private static readonly int[] SampleSizes = { 25, 50, 100, 200 };

    /// <summary>
    /// Number of independent datasets per cell for Monte Carlo averaging.
    /// </summary>
    private const int DatasetsPerCell = 50;

    /// <summary>
    /// Number of parametric bootstrap replications per dataset.
    /// </summary>
    private const int BootstrapReplications = 10000;

    #region Main Experiment

    /// <summary>
    /// BCa-corrected bootstrap parameter distribution shape for LP3.
    /// Outputs a CSV table of sigma-hat and gamma-hat diagnostics across all (sigma, gamma, n) cells.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Design matrix: 3 sigma x 13 gamma x 3 n = 117 cells x 50 datasets x 10K bootstrap.
    /// Expected runtime: ~1-2 hours.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void BootstrapParameterShape_AllCells()
    {
        var masterRng = new MersenneTwister(12345);
        var csv = new StringBuilder();

        csv.AppendLine("sigma_true,gamma_true,n,valid_count," +
            "mean_mu_hat,mean_sigma_hat,mean_gamma_hat," +
            "R_mu,z0_mu,accel_mu," +
            "R_sigma,R_log_sigma,z0_sigma,accel_sigma," +
            "R_gamma,z0_gamma,accel_gamma," +
            "BCa_lo_mu,BCa_hi_mu,BCa_lo_sigma,BCa_hi_sigma,BCa_lo_gamma,BCa_hi_gamma," +
            "corr_mu_sigma,corr_mu_gamma,corr_sigma_gamma," +
            "boot_sd_mu,boot_sd_log_sigma,boot_sd_gamma");

        int totalCells = 0;
        foreach (double sigma in Sigmas)
        {
            foreach (double gamma in Gammas)
            {
                foreach (int n in SampleSizes)
                {
                    totalCells++;
                    var cellResult = RunCellExperiment(sigma, gamma, n, masterRng);

                    csv.AppendLine(
                        $"{sigma:F2},{gamma:F2},{n},{cellResult.ValidCount}," +
                        $"{cellResult.MeanMuHat:F6},{cellResult.MeanSigmaHat:F6},{cellResult.MeanGammaHat:F6}," +
                        $"{cellResult.RMu:F4},{cellResult.Z0Mu:F4},{cellResult.AccelMu:F6}," +
                        $"{cellResult.RSigma:F4},{cellResult.RLogSigma:F4}," +
                        $"{cellResult.Z0Sigma:F4},{cellResult.AccelSigma:F6}," +
                        $"{cellResult.RGamma:F4},{cellResult.Z0Gamma:F4},{cellResult.AccelGamma:F6}," +
                        $"{cellResult.BCaLoMu:F6},{cellResult.BCaHiMu:F6}," +
                        $"{cellResult.BCaLoSigma:F6},{cellResult.BCaHiSigma:F6}," +
                        $"{cellResult.BCaLoGamma:F6},{cellResult.BCaHiGamma:F6}," +
                        $"{cellResult.CorrMuSigma:F4},{cellResult.CorrMuGamma:F4},{cellResult.CorrSigmaGamma:F4}," +
                        $"{cellResult.BootSdMu:F6},{cellResult.BootSdLogSigma:F6},{cellResult.BootSdGamma:F6}");
                }
            }
        }

        // Dump clean CSV block at the end, separated from any exception noise
        Debug.WriteLine("");
        Debug.WriteLine("========== LP3 BCa CALIBRATION RESULTS ==========");
        Debug.WriteLine($"Completed {totalCells} cells.");
        Debug.WriteLine("");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END LP3 BCa RESULTS ==========");
    }

    #endregion

    #region Cell Experiment

    /// <summary>
    /// Runs the BCa bootstrap experiment for a single (sigma, gamma, n) cell,
    /// averaging diagnostics over M independent datasets.
    /// </summary>
    /// <param name="sigma">True LP3 scale parameter.</param>
    /// <param name="gamma">True LP3 skewness parameter.</param>
    /// <param name="n">Sample size.</param>
    /// <param name="masterRng">Master RNG for generating per-dataset seeds.</param>
    /// <returns>Averaged BCa diagnostics for the cell.</returns>
    private static CellDiagnostics RunCellExperiment(double sigma, double gamma, int n, MersenneTwister masterRng)
    {
        // Accumulators
        double sumMuHat = 0, sumSigmaHat = 0, sumGammaHat = 0;
        double sumRMu = 0, sumZ0Mu = 0, sumAccelMu = 0;
        double sumRSigma = 0, sumRLogSigma = 0, sumZ0Sigma = 0, sumAccelSigma = 0;
        double sumRGamma = 0, sumZ0Gamma = 0, sumAccelGamma = 0;
        double sumBCaLoMu = 0, sumBCaHiMu = 0;
        double sumBCaLoSigma = 0, sumBCaHiSigma = 0;
        double sumBCaLoGamma = 0, sumBCaHiGamma = 0;
        double sumCorrMS = 0, sumCorrMG = 0, sumCorrSG = 0;
        double sumBootSdMu = 0, sumBootSdLogSigma = 0, sumBootSdGamma = 0;
        int validCount = 0;

        for (int m = 0; m < DatasetsPerCell; m++)
        {
            int seed = masterRng.Next();

            try
            {
                var result = RunSingleDataset(sigma, gamma, n, seed);
                if (result == null) continue;

                sumMuHat += result.Value.MuHat;
                sumSigmaHat += result.Value.SigmaHat;
                sumGammaHat += result.Value.GammaHat;
                sumRMu += result.Value.RMu;
                sumZ0Mu += result.Value.Z0Mu;
                sumAccelMu += result.Value.AccelMu;
                sumRSigma += result.Value.RSigma;
                sumRLogSigma += result.Value.RLogSigma;
                sumZ0Sigma += result.Value.Z0Sigma;
                sumAccelSigma += result.Value.AccelSigma;
                sumRGamma += result.Value.RGamma;
                sumZ0Gamma += result.Value.Z0Gamma;
                sumAccelGamma += result.Value.AccelGamma;
                sumBCaLoMu += result.Value.BCaLoMu;
                sumBCaHiMu += result.Value.BCaHiMu;
                sumBCaLoSigma += result.Value.BCaLoSigma;
                sumBCaHiSigma += result.Value.BCaHiSigma;
                sumBCaLoGamma += result.Value.BCaLoGamma;
                sumBCaHiGamma += result.Value.BCaHiGamma;
                sumCorrMS += result.Value.CorrMuSigma;
                sumCorrMG += result.Value.CorrMuGamma;
                sumCorrSG += result.Value.CorrSigmaGamma;
                sumBootSdMu += result.Value.BootSdMu;
                sumBootSdLogSigma += result.Value.BootSdLogSigma;
                sumBootSdGamma += result.Value.BootSdGamma;
                validCount++;
            }
            catch
            {
                // Silently skip failed datasets; parameter blow-ups are expected
                // at extreme gamma values. validCount tracks success rate.
            }
        }

        if (validCount == 0)
        {
            return new CellDiagnostics
            {
                ValidCount = 0,
                MeanMuHat = double.NaN, MeanSigmaHat = double.NaN, MeanGammaHat = double.NaN,
                RMu = double.NaN, Z0Mu = double.NaN, AccelMu = double.NaN,
                RSigma = double.NaN, RLogSigma = double.NaN,
                Z0Sigma = double.NaN, AccelSigma = double.NaN,
                RGamma = double.NaN, Z0Gamma = double.NaN, AccelGamma = double.NaN,
                BCaLoMu = double.NaN, BCaHiMu = double.NaN,
                BCaLoSigma = double.NaN, BCaHiSigma = double.NaN,
                BCaLoGamma = double.NaN, BCaHiGamma = double.NaN,
                CorrMuSigma = double.NaN, CorrMuGamma = double.NaN, CorrSigmaGamma = double.NaN,
                BootSdMu = double.NaN, BootSdLogSigma = double.NaN, BootSdGamma = double.NaN
            };
        }

        return new CellDiagnostics
        {
            ValidCount = validCount,
            MeanMuHat = sumMuHat / validCount,
            MeanSigmaHat = sumSigmaHat / validCount,
            MeanGammaHat = sumGammaHat / validCount,
            RMu = sumRMu / validCount,
            Z0Mu = sumZ0Mu / validCount,
            AccelMu = sumAccelMu / validCount,
            RSigma = sumRSigma / validCount,
            RLogSigma = sumRLogSigma / validCount,
            Z0Sigma = sumZ0Sigma / validCount,
            AccelSigma = sumAccelSigma / validCount,
            RGamma = sumRGamma / validCount,
            Z0Gamma = sumZ0Gamma / validCount,
            AccelGamma = sumAccelGamma / validCount,
            BCaLoMu = sumBCaLoMu / validCount,
            BCaHiMu = sumBCaHiMu / validCount,
            BCaLoSigma = sumBCaLoSigma / validCount,
            BCaHiSigma = sumBCaHiSigma / validCount,
            BCaLoGamma = sumBCaLoGamma / validCount,
            BCaHiGamma = sumBCaHiGamma / validCount,
            CorrMuSigma = sumCorrMS / validCount,
            CorrMuGamma = sumCorrMG / validCount,
            CorrSigmaGamma = sumCorrSG / validCount,
            BootSdMu = sumBootSdMu / validCount,
            BootSdLogSigma = sumBootSdLogSigma / validCount,
            BootSdGamma = sumBootSdGamma / validCount
        };
    }

    /// <summary>
    /// Runs BCa bootstrap for a single LP3 dataset and returns parameter diagnostics.
    /// </summary>
    /// <param name="sigma">True LP3 scale parameter.</param>
    /// <param name="gamma">True LP3 skewness parameter.</param>
    /// <param name="n">Sample size.</param>
    /// <param name="seed">RNG seed for this dataset.</param>
    /// <returns>Diagnostics for this dataset, or null if estimation failed.</returns>
    private static DatasetDiagnostics? RunSingleDataset(double sigma, double gamma, int n, int seed)
    {
        // Generate random sample from true LP3(mu=3, sigma, gamma)
        var trueDist = new LogPearsonTypeIII(3.0, sigma, gamma);
        var sample = trueDist.GenerateRandomValues(n, seed);
        var sampleList = sample.ToList();

        // Fit LP3 via Method of Moments
        var fitDist = new LogPearsonTypeIII();
        ((IEstimation)fitDist).Estimate(sampleList, ParameterEstimationMethod.MethodOfMoments);
        double muHat = fitDist.Mu;
        double sigmaHat = fitDist.Sigma;
        double gammaHat = fitDist.Gamma;

        // Parametric bootstrap: resample from fitted distribution, re-estimate
        var bootstrap = new BootstrapAnalysis(
            fitDist,
            ParameterEstimationMethod.MethodOfMoments,
            n,
            replications: BootstrapReplications,
            seed: seed + 1);

        double[,] bootParams = bootstrap.Parameters(); // B x 3

        // Extract mu-hat*, sigma-hat*, and gamma-hat* columns, filter NaN and invalid
        var muStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 0, v => !double.IsNaN(v));
        var sigmaStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 1, v => !double.IsNaN(v) && v > 0);
        var gammaStars = BCaCalibrationHelpers.ExtractColumn(bootParams, 2, v => !double.IsNaN(v));

        if (muStars.Length < 100 || sigmaStars.Length < 100 || gammaStars.Length < 100)
            return null;

        // Jackknife leave-one-out re-estimation (shared across all three parameters)
        Func<List<double>, double[]?> jackEstimator = sample =>
        {
            var d = new LogPearsonTypeIII();
            ((IEstimation)d).Estimate(sample, ParameterEstimationMethod.MethodOfMoments);
            return d.GetParameters;
        };

        // Jackknife estimates for mu, sigma, and gamma
        var jackMu = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 0, jackEstimator);
        var jackSigma = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 1, jackEstimator);
        var jackGamma = BCaCalibrationHelpers.ComputeJackknifeEstimates(sampleList, 2, jackEstimator);

        // BCa for mu-hat
        var bcaMu = BCaCalibrationHelpers.BCaParameterCI(muStars, muHat, jackMu);

        // BCa for sigma-hat
        var bcaSigma = BCaCalibrationHelpers.BCaParameterCI(sigmaStars, sigmaHat, jackSigma);

        // BCa for gamma-hat
        var bcaGamma = BCaCalibrationHelpers.BCaParameterCI(gammaStars, gammaHat, jackGamma);

        // CI asymmetry ratios
        double rMu = BCaCalibrationHelpers.ComputeAsymmetryRatio(bcaMu.Lower, bcaMu.Upper, muHat);
        double rSigma = BCaCalibrationHelpers.ComputeAsymmetryRatio(bcaSigma.Lower, bcaSigma.Upper, sigmaHat);
        double rLogSigma = BCaCalibrationHelpers.ComputeAsymmetryRatio(
            Math.Log(bcaSigma.Lower), Math.Log(bcaSigma.Upper), Math.Log(sigmaHat));
        double rGamma = BCaCalibrationHelpers.ComputeAsymmetryRatio(bcaGamma.Lower, bcaGamma.Upper, gammaHat);

        // Pairwise bootstrap parameter correlations (using common valid rows)
        var (corrMS, corrMG, corrSG) = ComputeBootstrapCorrelations(bootParams);

        // Bootstrap standard deviations (empirical analogs of GMM sqrt(Sigma) diagonal elements).
        // These will be used to compute delta values for variance-based link calibration:
        //   delta_sigma = 1.645 * bootSdLogSigma
        //   delta_gamma = 1.645 * bootSdGamma / sqrt(1 + gammaHat^2)
        double bootSdMu = ComputeSD(muStars);
        double bootSdLogSigma = ComputeSD(sigmaStars.Select(s => Math.Log(s)).ToArray());
        double bootSdGamma = ComputeSD(gammaStars);

        return new DatasetDiagnostics
        {
            MuHat = muHat,
            SigmaHat = sigmaHat,
            GammaHat = gammaHat,
            RMu = rMu,
            Z0Mu = bcaMu.Z0,
            AccelMu = bcaMu.Accel,
            RSigma = rSigma,
            RLogSigma = rLogSigma,
            Z0Sigma = bcaSigma.Z0,
            AccelSigma = bcaSigma.Accel,
            RGamma = rGamma,
            Z0Gamma = bcaGamma.Z0,
            AccelGamma = bcaGamma.Accel,
            BCaLoMu = bcaMu.Lower,
            BCaHiMu = bcaMu.Upper,
            BCaLoSigma = bcaSigma.Lower,
            BCaHiSigma = bcaSigma.Upper,
            BCaLoGamma = bcaGamma.Lower,
            BCaHiGamma = bcaGamma.Upper,
            CorrMuSigma = corrMS,
            CorrMuGamma = corrMG,
            CorrSigmaGamma = corrSG,
            BootSdMu = bootSdMu,
            BootSdLogSigma = bootSdLogSigma,
            BootSdGamma = bootSdGamma
        };
    }

    /// <summary>
    /// Computes pairwise Pearson correlations between bootstrap parameter columns,
    /// using only rows where all three parameters are valid (non-NaN, sigma > 0).
    /// </summary>
    /// <param name="bootParams">Bootstrap parameter matrix [B x 3].</param>
    /// <returns>Tuple of (corr_mu_sigma, corr_mu_gamma, corr_sigma_gamma).</returns>
    private static (double CorrMuSigma, double CorrMuGamma, double CorrSigmaGamma) ComputeBootstrapCorrelations(double[,] bootParams)
    {
        int B = bootParams.GetLength(0);

        // Extract common valid rows (all 3 parameters valid)
        var muVals = new List<double>(B);
        var sigmaVals = new List<double>(B);
        var gammaVals = new List<double>(B);

        for (int i = 0; i < B; i++)
        {
            double m = bootParams[i, 0], s = bootParams[i, 1], g = bootParams[i, 2];
            if (!double.IsNaN(m) && !double.IsNaN(s) && s > 0 && !double.IsNaN(g))
            {
                muVals.Add(m);
                sigmaVals.Add(s);
                gammaVals.Add(g);
            }
        }

        int N = muVals.Count;
        if (N < 3)
            return (double.NaN, double.NaN, double.NaN);

        double corrMS = PearsonCorrelation(muVals, sigmaVals, N);
        double corrMG = PearsonCorrelation(muVals, gammaVals, N);
        double corrSG = PearsonCorrelation(sigmaVals, gammaVals, N);
        return (corrMS, corrMG, corrSG);
    }

    /// <summary>
    /// Computes the Pearson correlation coefficient between two arrays of equal length.
    /// </summary>
    private static double PearsonCorrelation(List<double> x, List<double> y, int n)
    {
        double meanX = 0, meanY = 0;
        for (int i = 0; i < n; i++) { meanX += x[i]; meanY += y[i]; }
        meanX /= n; meanY /= n;

        double sumXY = 0, sumX2 = 0, sumY2 = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - meanX, dy = y[i] - meanY;
            sumXY += dx * dy;
            sumX2 += dx * dx;
            sumY2 += dy * dy;
        }

        double denom = Math.Sqrt(sumX2 * sumY2);
        return denom > 1e-30 ? sumXY / denom : 0.0;
    }

    /// <summary>
    /// Computes the sample standard deviation of an array.
    /// </summary>
    /// <param name="values">Array of values.</param>
    /// <returns>Sample standard deviation, or NaN if fewer than 2 values.</returns>
    private static double ComputeSD(double[] values)
    {
        int n = values.Length;
        if (n < 2) return double.NaN;
        double mean = 0;
        for (int i = 0; i < n; i++) mean += values[i];
        mean /= n;
        double sumSq = 0;
        for (int i = 0; i < n; i++)
        {
            double d = values[i] - mean;
            sumSq += d * d;
        }
        return Math.Sqrt(sumSq / (n - 1));
    }

    #endregion

    #region Data Types

    /// <summary>
    /// Diagnostics from a single dataset's BCa bootstrap.
    /// </summary>
    private struct DatasetDiagnostics
    {
        public double MuHat;
        public double SigmaHat;
        public double GammaHat;
        public double RMu;
        public double Z0Mu;
        public double AccelMu;
        public double RSigma;
        public double RLogSigma;
        public double Z0Sigma;
        public double AccelSigma;
        public double RGamma;
        public double Z0Gamma;
        public double AccelGamma;
        public double BCaLoMu;
        public double BCaHiMu;
        public double BCaLoSigma;
        public double BCaHiSigma;
        public double BCaLoGamma;
        public double BCaHiGamma;
        public double CorrMuSigma;
        public double CorrMuGamma;
        public double CorrSigmaGamma;
        public double BootSdMu;
        public double BootSdLogSigma;
        public double BootSdGamma;
    }

    /// <summary>
    /// Averaged diagnostics for a (sigma, gamma, n) cell.
    /// </summary>
    private struct CellDiagnostics
    {
        public int ValidCount;
        public double MeanMuHat;
        public double MeanSigmaHat;
        public double MeanGammaHat;
        public double RMu;
        public double Z0Mu;
        public double AccelMu;
        public double RSigma;
        public double RLogSigma;
        public double Z0Sigma;
        public double AccelSigma;
        public double RGamma;
        public double Z0Gamma;
        public double AccelGamma;
        public double BCaLoMu;
        public double BCaHiMu;
        public double BCaLoSigma;
        public double BCaHiSigma;
        public double BCaLoGamma;
        public double BCaHiGamma;
        public double CorrMuSigma;
        public double CorrMuGamma;
        public double CorrSigmaGamma;
        public double BootSdMu;
        public double BootSdLogSigma;
        public double BootSdGamma;
    }

    #endregion
}
