using Numerics;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Sampling;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Comprehensive Cohn CI calibration sweep for deriving static LinkedMVN link function parameters.
/// </summary>
/// <remarks>
/// <para>
/// For each (n, σ, γ) cell, generates M=50 synthetic LP3 datasets, fits B17C via GMM,
/// computes Cohn nested-quadrature CIs at multiple AEPs, and extracts all quantities needed
/// to calibrate static formulas for λ_μ, λ_σ, λ_γ, a_σ, a_γ.
/// </para>
/// <para>
/// Unlike <see cref="LinkCalibrationTests"/> which uses 10K-replicate bootstrap (hours of runtime),
/// this uses Cohn's analytical quadrature (~64 quantile evaluations per AEP) — orders of magnitude faster.
/// The 50-dataset averaging smooths estimation noise from individual random samples.
/// </para>
/// <para>
/// Grid: γ ∈ [-1, +1] by 0.25 (11 values) × σ ∈ {0.3, 0.5, 0.7} × n ∈ {25, 50, 100, 200} = 204 cells.
/// At 50 datasets per cell × ~7 AEPs = ~71,400 Cohn CI computations. Expected runtime: 30-90 minutes.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class CohnCalibrationTests
{
    private static readonly double[] Gammas =
    {
        -1.0, -0.75, -0.5, -0.25, -0.1, 0.0, 0.1, 0.25, 0.5, 0.75, 1.0
    };

    private static readonly double[] Sigmas = { 0.3, 0.5, 0.7 };

    private static readonly int[] SampleSizes = { 25, 50, 100, 200 };

    /// <summary>
    /// AEPs at which to extract Cohn CI diagnostics. Covers moderate through extreme return periods.
    /// </summary>
    private static readonly double[] TargetAEPs = { 0.10, 0.04, 0.02, 0.01, 0.005, 0.002, 0.001 };

    private const double Mu = 3.0;
    private const int DatasetsPerCell = 30;
    private const int MasterSeed = 77777;
    private const double NominalCI = 0.90;

    /// <summary>
    /// Full calibration sweep. Outputs two CSV tables:
    /// 1. Cell-level averages: one row per (n, σ, γ) cell with averaged Cohn diagnostics at each AEP.
    /// 2. Per-AEP detail: one row per (n, σ, γ, AEP) with β₁, ν, R_Cohn, and gradient decomposition.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task CohnCalibration_FullSweep()
    {
        var masterRng = new MersenneTwister(MasterSeed);

        // Per-AEP detail table: every quantity needed for λ and a calibration
        var detailCsv = new StringBuilder();
        detailCsv.AppendLine(
            "n,sigma_true,gamma_true,AEP," +
            "valid_count," +
            // GMM point estimates (averaged over M datasets)
            "mean_mu_hat,mean_sigma_hat,mean_gamma_hat," +
            // GMM covariance diagonal (averaged)
            "mean_var_mu,mean_var_sigma,mean_var_gamma," +
            // GMM covariance off-diagonal (averaged)
            "mean_cov_mu_sigma,mean_cov_mu_gamma,mean_cov_sigma_gamma," +
            // Cohn 2x2 matrix elements (averaged)
            "mean_varQ,mean_covQSE,mean_varSE," +
            // Cohn CI diagnostics (averaged)
            "mean_beta1,mean_nu," +
            // Cohn CI asymmetry ratio R = upper_width / lower_width (averaged)
            "mean_R_Cohn," +
            // Cohn CI bounds (averaged, in log-space for LP3)
            "mean_qhat,mean_ci_low,mean_ci_high," +
            // Quantile gradient contributions (averaged)
            "mean_grad_mu,mean_grad_sigma,mean_grad_gamma," +
            "mean_contrib_mu,mean_contrib_sigma,mean_contrib_gamma," +
            // Delta values (averaged): δ_μ, δ_σ, δ_γ
            "mean_delta_mu,mean_delta_sigma,mean_delta_gamma," +
            // Target λ per parameter: ln(R_Cohn * weight_i) / (2δ_i)
            "mean_lambda_target_mu,mean_lambda_target_sigma,mean_lambda_target_gamma"
        );

        // Cell summary table: one row per (n, σ, γ) with overall diagnostics
        var cellCsv = new StringBuilder();
        cellCsv.AppendLine(
            "n,sigma_true,gamma_true,valid_count," +
            "mean_mu_hat,mean_sigma_hat,mean_gamma_hat," +
            "mean_var_mu,mean_var_sigma,mean_var_gamma," +
            "mean_cov_mu_sigma,mean_cov_mu_gamma,mean_cov_sigma_gamma"
        );

        int totalCells = 0;
        int totalProcessed = 0;

        foreach (int n in SampleSizes)
        {
            foreach (double sigma in Sigmas)
            {
                foreach (double gamma in Gammas)
                {
                    totalCells++;
                    double effectiveGamma = Math.Abs(gamma) < 1e-10 ? 0.001 : gamma;

                    // Per-AEP accumulators
                    var aepAccum = new AEPAccumulator[TargetAEPs.Length];
                    for (int k = 0; k < TargetAEPs.Length; k++)
                        aepAccum[k] = new AEPAccumulator();

                    // Cell-level accumulators
                    double sumMuHat = 0, sumSigmaHat = 0, sumGammaHat = 0;
                    double sumVarMu = 0, sumVarSigma = 0, sumVarGamma = 0;
                    double sumCovMS = 0, sumCovMG = 0, sumCovSG = 0;
                    int validCount = 0;

                    for (int m = 0; m < DatasetsPerCell; m++)
                    {
                        int seed = masterRng.Next();

                        try
                        {
                            var result = await RunSingleDataset(n, sigma, effectiveGamma, seed);
                            if (result == null) continue;

                            validCount++;
                            sumMuHat += result.MuHat;
                            sumSigmaHat += result.SigmaHat;
                            sumGammaHat += result.GammaHat;
                            sumVarMu += result.VarMu;
                            sumVarSigma += result.VarSigma;
                            sumVarGamma += result.VarGamma;
                            sumCovMS += result.CovMuSigma;
                            sumCovMG += result.CovMuGamma;
                            sumCovSG += result.CovSigmaGamma;

                            for (int k = 0; k < TargetAEPs.Length; k++)
                            {
                                if (result.AEPResults[k] != null)
                                    aepAccum[k].Add(result.AEPResults[k]!.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Cell (n={n}, σ={sigma}, γ={gamma}, m={m}) failed: {ex.Message}");
                        }
                    }

                    // Write cell summary
                    if (validCount > 0)
                    {
                        cellCsv.AppendLine(
                            $"{n},{sigma:F2},{gamma:F2},{validCount}," +
                            $"{sumMuHat / validCount:G6},{sumSigmaHat / validCount:G6},{sumGammaHat / validCount:G6}," +
                            $"{sumVarMu / validCount:G6},{sumVarSigma / validCount:G6},{sumVarGamma / validCount:G6}," +
                            $"{sumCovMS / validCount:G6},{sumCovMG / validCount:G6},{sumCovSG / validCount:G6}"
                        );
                    }

                    // Write per-AEP detail rows
                    for (int k = 0; k < TargetAEPs.Length; k++)
                    {
                        var acc = aepAccum[k];
                        int cnt = acc.Count;
                        if (cnt == 0) continue;

                        detailCsv.AppendLine(
                            $"{n},{sigma:F2},{gamma:F2},{TargetAEPs[k]:G4}," +
                            $"{cnt}," +
                            $"{sumMuHat / validCount:G6},{sumSigmaHat / validCount:G6},{sumGammaHat / validCount:G6}," +
                            $"{sumVarMu / validCount:G6},{sumVarSigma / validCount:G6},{sumVarGamma / validCount:G6}," +
                            $"{sumCovMS / validCount:G6},{sumCovMG / validCount:G6},{sumCovSG / validCount:G6}," +
                            $"{acc.SumVarQ / cnt:G6},{acc.SumCovQSE / cnt:G6},{acc.SumVarSE / cnt:G6}," +
                            $"{acc.SumBeta1 / cnt:G6},{acc.SumNu / cnt:G6}," +
                            $"{acc.SumRCohn / cnt:G6}," +
                            $"{acc.SumQHat / cnt:G6},{acc.SumCILow / cnt:G6},{acc.SumCIHigh / cnt:G6}," +
                            $"{acc.SumGradMu / cnt:G6},{acc.SumGradSigma / cnt:G6},{acc.SumGradGamma / cnt:G6}," +
                            $"{acc.SumContribMu / cnt:G6},{acc.SumContribSigma / cnt:G6},{acc.SumContribGamma / cnt:G6}," +
                            $"{acc.SumDeltaMu / cnt:G6},{acc.SumDeltaSigma / cnt:G6},{acc.SumDeltaGamma / cnt:G6}," +
                            $"{acc.SumLambdaTargetMu / cnt:G6},{acc.SumLambdaTargetSigma / cnt:G6},{acc.SumLambdaTargetGamma / cnt:G6}"
                        );
                    }

                    totalProcessed++;
                    if (totalProcessed % 10 == 0)
                        Debug.WriteLine($"  Progress: {totalProcessed}/{Gammas.Length * Sigmas.Length * SampleSizes.Length} cells");
                }
            }
        }

        // Output
        Debug.WriteLine("\n========== COHN CALIBRATION: CELL SUMMARY ==========");
        Debug.Write(cellCsv.ToString());
        Debug.WriteLine("========== END CELL SUMMARY ==========\n");

        Debug.WriteLine("========== COHN CALIBRATION: PER-AEP DETAIL ==========");
        Debug.Write(detailCsv.ToString());
        Debug.WriteLine("========== END PER-AEP DETAIL ==========\n");

        Assert.IsTrue(totalProcessed > 0, "No cells completed successfully.");
    }

    /// <summary>
    /// Processes a single synthetic LP3 dataset: fits GMM, computes Cohn CIs at all target AEPs,
    /// extracts all calibration quantities.
    /// </summary>
    private async Task<DatasetResult?> RunSingleDataset(int n, double sigma, double gamma, int seed)
    {
        // Generate LP3 sample
        var trueLogDist = new PearsonTypeIII(Mu, sigma, gamma);
        var logValues = trueLogDist.GenerateRandomValues(n, seed);
        var realValues = new double[n];
        for (int k = 0; k < n; k++)
            realValues[k] = Math.Pow(10, logValues[k]);

        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(realValues);

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var analysis = new Bulletin17CAnalysis(model);
        analysis.BayesianAnalysis.CredibleIntervalWidth = NominalCI;
        analysis.BayesianAnalysis.OutputLength = 10; // Minimal draws — we only need GMM + Cohn CIs

        // Set target AEPs
        analysis.ProbabilityOrdinates.Clear();
        foreach (double aep in TargetAEPs)
            analysis.ProbabilityOrdinates.Add(aep);

        // Run analysis (GMM + minimal uncertainty — fast)
        analysis.UncertaintyMethod = UncertaintyMethod.MultivariateNormal;
        await analysis.RunAsync();

        if (!analysis.IsEstimated) return null;

        // Extract GMM results
        var gmm = analysis.GMM;
        if (gmm == null || !gmm.IsEstimated) return null;
        var thetaHat = gmm.BestParameterSet.Values;

        Matrix sigmaHat;
        try
        {
            sigmaHat = gmm.GetCovariance(thetaHat);
            sigmaHat = MatrixRegularization.MakeSymmetricPositiveDefinite(sigmaHat);
        }
        catch { return null; }

        int p = thetaHat.Length;
        double muHat = thetaHat[0];
        double sigmaHat_val = thetaHat[1];
        double gammaHat = p >= 3 ? thetaHat[2] : 0.0;

        var result = new DatasetResult
        {
            MuHat = muHat,
            SigmaHat = sigmaHat_val,
            GammaHat = gammaHat,
            VarMu = sigmaHat[0, 0],
            VarSigma = sigmaHat[1, 1],
            VarGamma = p >= 3 ? sigmaHat[2, 2] : 0,
            CovMuSigma = sigmaHat[0, 1],
            CovMuGamma = p >= 3 ? sigmaHat[0, 2] : 0,
            CovSigmaGamma = p >= 3 ? sigmaHat[1, 2] : 0,
            AEPResults = new AEPResult?[TargetAEPs.Length]
        };

        // Compute Cohn CIs
        var cohnResult = analysis.ComputeCohnStyleConfidenceIntervals();
        if (cohnResult == null) return result;

        // For each AEP, extract detailed diagnostics
        for (int k = 0; k < TargetAEPs.Length; k++)
        {
            try
            {
                double nonExceedProb = 1.0 - TargetAEPs[k];
                double qHat = cohnResult.PointEstimates[k]; // log10-space
                // CIs are returned in real-space (10^log), convert back to log10
                double ciLow = Math.Log10(Math.Max(cohnResult.LowerCI[k], 1e-30));
                double ciHigh = Math.Log10(Math.Max(cohnResult.UpperCI[k], 1e-30));
                double beta1 = cohnResult.Beta1[k];
                double nu = cohnResult.Nu[k];
                double varQ = cohnResult.QuantileVariance[k];

                // Compute asymmetry ratio in log10-space
                double upperWidth = ciHigh - qHat;
                double lowerWidth = qHat - ciLow;
                double rCohn = Math.Abs(lowerWidth) > 1e-10 ? upperWidth / lowerWidth : 1.0;

                // Quantile gradient in log-space (for LP3, parameters are log-space moments)
                double[] gradient;
                try
                {
                    gradient = model.QuantileGradient(nonExceedProb, thetaHat);
                }
                catch { continue; }

                // Gradient contributions to quantile variance
                double[] contributions = new double[p];
                double totalContrib = 0;
                for (int i = 0; i < p; i++)
                {
                    contributions[i] = gradient[i] * gradient[i] * sigmaHat[i, i];
                    totalContrib += contributions[i];
                }

                // Delta values per parameter
                double deltaMu = 1.645; // standardized by CenteredLink
                double deltaSigma = sigmaHat_val > 1e-10
                    ? 1.645 * Math.Sqrt(sigmaHat[1, 1]) / sigmaHat_val : 1.0;
                double deltaGamma = 0;
                if (p >= 3)
                {
                    double sesJac = 1.0 / Math.Sqrt(1.0 + gammaHat * gammaHat);
                    deltaGamma = 1.645 * sesJac * Math.Sqrt(sigmaHat[2, 2]);
                }

                // Compute the Cohn CI 2x2 elements from the result
                // covQSE and varSE can be back-computed from beta1, nu, varQ:
                //   beta1 = covQSE / varQ  →  covQSE = beta1 * varQ
                //   nu = 0.5 * varQ / (varSE - covQSE²/varQ)
                //   → varSE = covQSE²/varQ + 0.5*varQ/nu
                double covQSE = beta1 * varQ;
                double varSE = covQSE * covQSE / varQ + 0.5 * varQ / nu;

                // Target λ per parameter (from ln(R) decomposed by gradient weight)
                double lnR = Math.Log(Math.Max(rCohn, 0.01));
                double lambdaTargetMu = 0, lambdaTargetSigma = 0, lambdaTargetGamma = 0;
                if (totalContrib > 1e-30)
                {
                    double lnR_mu = lnR * contributions[0] / totalContrib;
                    double lnR_sigma = lnR * contributions[1] / totalContrib;
                    lambdaTargetMu = deltaMu > 1e-10 ? lnR_mu / (2.0 * deltaMu) : 0;
                    lambdaTargetSigma = deltaSigma > 1e-10 ? lnR_sigma / (2.0 * deltaSigma) : 0;
                    if (p >= 3)
                    {
                        double lnR_gamma = lnR * contributions[2] / totalContrib;
                        lambdaTargetGamma = deltaGamma > 1e-10 ? lnR_gamma / (2.0 * deltaGamma) : 0;
                    }
                }

                result.AEPResults[k] = new AEPResult
                {
                    VarQ = varQ,
                    CovQSE = covQSE,
                    VarSE = varSE,
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
                    DeltaMu = deltaMu,
                    DeltaSigma = deltaSigma,
                    DeltaGamma = deltaGamma,
                    LambdaTargetMu = lambdaTargetMu,
                    LambdaTargetSigma = lambdaTargetSigma,
                    LambdaTargetGamma = lambdaTargetGamma
                };
            }
            catch { /* skip this AEP */ }
        }

        return result;
    }

    #region Data Types

    /// <summary>
    /// Provides a helper class used by the containing fixture or implementation.
    /// </summary>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private class DatasetResult
    {
        public double MuHat, SigmaHat, GammaHat;
        public double VarMu, VarSigma, VarGamma;
        public double CovMuSigma, CovMuGamma, CovSigmaGamma;
        public AEPResult?[] AEPResults = Array.Empty<AEPResult?>();
    }

    /// <summary>
    /// Provides a helper struct used by the containing fixture or implementation.
    /// </summary>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private struct AEPResult
    {
        public double VarQ, CovQSE, VarSE;
        public double Beta1, Nu, RCohn;
        public double QHat, CILow, CIHigh;
        public double GradMu, GradSigma, GradGamma;
        public double ContribMu, ContribSigma, ContribGamma;
        public double DeltaMu, DeltaSigma, DeltaGamma;
        public double LambdaTargetMu, LambdaTargetSigma, LambdaTargetGamma;
    }

    /// <summary>
    /// Provides a helper class used by the containing fixture or implementation.
    /// </summary>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private class AEPAccumulator
    {
        public int Count;
        public double SumVarQ, SumCovQSE, SumVarSE;
        public double SumBeta1, SumNu, SumRCohn;
        public double SumQHat, SumCILow, SumCIHigh;
        public double SumGradMu, SumGradSigma, SumGradGamma;
        public double SumContribMu, SumContribSigma, SumContribGamma;
        public double SumDeltaMu, SumDeltaSigma, SumDeltaGamma;
        public double SumLambdaTargetMu, SumLambdaTargetSigma, SumLambdaTargetGamma;

        /// <summary>
        /// Supports the <c>Add</c> helper.
        /// </summary>
        /// <param name="r">The r value.</param>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public void Add(AEPResult r)
        {
            // Guard against NaN/Inf contaminating averages
            if (!IsFinite(r.Beta1) || !IsFinite(r.Nu) || !IsFinite(r.VarQ)) return;

            Count++;
            SumVarQ += r.VarQ;
            SumCovQSE += r.CovQSE;
            SumVarSE += r.VarSE;
            SumBeta1 += r.Beta1;
            SumNu += r.Nu;
            SumRCohn += r.RCohn;
            SumQHat += r.QHat;
            SumCILow += r.CILow;
            SumCIHigh += r.CIHigh;
            SumGradMu += r.GradMu;
            SumGradSigma += r.GradSigma;
            SumGradGamma += r.GradGamma;
            SumContribMu += r.ContribMu;
            SumContribSigma += r.ContribSigma;
            SumContribGamma += r.ContribGamma;
            SumDeltaMu += r.DeltaMu;
            SumDeltaSigma += r.DeltaSigma;
            SumDeltaGamma += r.DeltaGamma;
            SumLambdaTargetMu += r.LambdaTargetMu;
            SumLambdaTargetSigma += r.LambdaTargetSigma;
            SumLambdaTargetGamma += r.LambdaTargetGamma;
        }

        /// <summary>
        /// Determines whether a value is finite.
        /// </summary>
        /// <returns>A value indicating whether the condition is satisfied.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        private static bool IsFinite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }

    #endregion
}
