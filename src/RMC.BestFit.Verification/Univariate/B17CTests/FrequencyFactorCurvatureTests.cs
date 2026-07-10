using Numerics.Distributions;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Computes the frequency factor K_gamma(p) and its derivatives with respect to gamma for the
/// Pearson Type III distribution. Uses the Numerics GammaDistribution InverseCDF internally
/// to evaluate K numerically.
/// </summary>
/// <remarks>
/// <para>
/// For Pearson Type III: Q(p) = mu + sigma * K_gamma(p), so the frequency factor is
/// K_gamma(p) = PT3(0, 1, gamma).InverseCDF(p). For Log-Pearson Type III, the quantile
/// mapping is Q = 10^(mu + sigma * K_gamma(p)), adding exponential nonlinearity.
/// </para>
/// <para>
/// The curvature ratio kappa = |d²K/dgamma²| / |dK/dgamma| measures how much the quantile
/// function curves with respect to gamma — this is the theoretical basis for the SES 'a'
/// parameter in the Linked MVN uncertainty method.
/// </para>
/// <para>
/// Motivated by Cohn's EMA CI paper which decomposes Var(Q) using dQ/dgamma = sigma * dK/dgamma.
/// At extreme AEPs, the K_gamma nonlinearity creates quantile-level asymmetry beyond what
/// parameter-level calibration captures.
/// </para>
/// </remarks>
[TestClass]
public class FrequencyFactorCurvatureTests
{
    /// <summary>
    /// Gamma grid matching the BCa calibration experiments.
    /// </summary>
    private static readonly double[] Gammas = { -4.0, -3.0, -2.0, -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0 };

    /// <summary>
    /// AEPs to evaluate. Focus on 0.0001 (10,000-year flood) where nonlinearity is strongest,
    /// but include others for context.
    /// </summary>
    private static readonly double[] AEPs = { 0.10, 0.04, 0.02, 0.01, 0.005, 0.002, 0.001, 0.0005, 0.0002, 0.0001 };

    /// <summary>
    /// Finite difference step size for computing dK/dgamma.
    /// </summary>
    private const double DGamma = 0.01;

    #region Frequency Factor Table

    /// <summary>
    /// Outputs the frequency factor K_gamma(p) for all (gamma, AEP) combinations.
    /// K is computed as PT3(0, 1, gamma).InverseCDF(1-AEP) since Q = mu + sigma*K and mu=0, sigma=1.
    /// </summary>
    [TestMethod]
    public void FrequencyFactorTable()
    {
        Debug.WriteLine("========== FREQUENCY FACTOR K_gamma(p) TABLE ==========");
        Debug.WriteLine("");

        // Header
        string header = $"{"gamma",8}";
        foreach (double aep in AEPs)
            header += $" {"AEP=" + aep.ToString("G4"),14}";
        Debug.WriteLine(header);
        Debug.WriteLine(new string('-', header.Length));

        foreach (double gamma in Gammas)
        {
            string row = $"{gamma,8:F1}";
            foreach (double aep in AEPs)
            {
                double K = ComputeK(gamma, aep);
                row += $" {K,14:F6}";
            }
            Debug.WriteLine(row);
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== END FREQUENCY FACTOR TABLE ==========");
    }

    #endregion

    #region Curvature Analysis at AEP = 0.0001

    /// <summary>
    /// Computes dK/dgamma, d²K/dgamma², and the curvature ratio kappa at AEP = 0.0001.
    /// Compares kappa(gamma) against the current heuristic a(gamma) formula from
    /// Bulletin17CAnalysis.cs lines 694-700.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Expected result: kappa(gamma) should be asymmetric — larger for positive gamma
    /// (convex K amplifies upper tail) vs. negative gamma. This should match the qualitative
    /// shape of the current a(gamma) = 1.0 + 3.0*tanh(0.8*gamma) + 2.0*g²/(2.0+g²).
    /// </para>
    /// </remarks>
    [TestMethod]
    public void CurvatureRatio_AEP0001()
    {
        const double targetAEP = 0.0001;

        Debug.WriteLine("========== CURVATURE ANALYSIS AT AEP = 0.0001 ==========");
        Debug.WriteLine("");
        Debug.WriteLine($"{"gamma",8} {"K",12} {"dK/dg",12} {"d2K/dg2",12} {"kappa",10} {"a_current",12} {"ratio",10}");
        Debug.WriteLine(new string('-', 80));

        foreach (double gamma in Gammas)
        {
            double K = ComputeK(gamma, targetAEP);
            double dKdg = ComputeDKDGamma(gamma, targetAEP);
            double d2Kdg2 = ComputeD2KDGamma2(gamma, targetAEP);

            // Curvature ratio: how much the K function curves relative to its slope
            double kappa = Math.Abs(dKdg) > 1e-12
                ? Math.Abs(d2Kdg2) / Math.Abs(dKdg)
                : double.NaN;

            // Current heuristic a(gamma) from Bulletin17CAnalysis.cs:694-700
            double g2 = gamma * gamma;
            double aCurrent = 1.0 + 3.0 * Math.Tanh(0.8 * gamma) + 2.0 * g2 / (2.0 + g2);
            aCurrent = Math.Max(0.5, Math.Min(6.0, aCurrent));

            // Ratio of kappa to a_current — reveals whether a tracks the curvature
            double ratio = !double.IsNaN(kappa) && aCurrent > 0
                ? kappa / aCurrent
                : double.NaN;

            Debug.WriteLine($"{gamma,8:F1} {K,12:F6} {dKdg,12:F6} {d2Kdg2,12:F6} {kappa,10:F6} {aCurrent,12:F4} {ratio,10:F4}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== END CURVATURE ANALYSIS ==========");
    }

    #endregion

    #region Multi-AEP Curvature Comparison

    /// <summary>
    /// Computes kappa(gamma) across multiple AEPs to show how curvature changes with return period.
    /// Focuses on the upper tail (AEP from 0.01 to 0.0001).
    /// </summary>
    [TestMethod]
    public void CurvatureRatio_MultipleAEPs()
    {
        var targetAEPs = new[] { 0.01, 0.005, 0.002, 0.001, 0.0005, 0.0002, 0.0001 };

        Debug.WriteLine("========== CURVATURE RATIO kappa(gamma, AEP) ==========");
        Debug.WriteLine("");

        string header = $"{"gamma",8}";
        foreach (double aep in targetAEPs)
            header += $" {"AEP=" + aep.ToString("G4"),12}";
        Debug.WriteLine(header);
        Debug.WriteLine(new string('-', header.Length));

        foreach (double gamma in Gammas)
        {
            string row = $"{gamma,8:F1}";
            foreach (double aep in targetAEPs)
            {
                double dKdg = ComputeDKDGamma(gamma, aep);
                double d2Kdg2 = ComputeD2KDGamma2(gamma, aep);
                double kappa = Math.Abs(dKdg) > 1e-12
                    ? Math.Abs(d2Kdg2) / Math.Abs(dKdg)
                    : double.NaN;
                row += $" {kappa,12:F6}";
            }
            Debug.WriteLine(row);
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== END MULTI-AEP CURVATURE ==========");
    }

    #endregion

    #region LP3 Quantile Convexity

    /// <summary>
    /// Computes the second derivative d²Q/dgamma² for the LP3 quantile function
    /// Q = 10^(mu + sigma * K_gamma(p)) at AEP = 0.0001.
    /// This captures the full nonlinear chain: K_gamma curvature amplified by the 10^(...) transform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For LP3: d²Q/dgamma² = Q * [sigma * d²K/dgamma² * ln(10) + (sigma * dK/dgamma * ln(10))²]
    /// The squared first-derivative term means even distributions where d²K/dgamma² is small
    /// can have substantial quantile convexity via the exponential amplification.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LP3QuantileConvexity_AEP0001()
    {
        const double targetAEP = 0.0001;
        const double sigma = 0.5; // Typical LP3 sigma in log10 space
        const double mu = 3.0;    // log10(1000 cfs)

        Debug.WriteLine("========== LP3 QUANTILE CONVEXITY AT AEP = 0.0001 ==========");
        Debug.WriteLine($"mu = {mu}, sigma = {sigma}");
        Debug.WriteLine("");
        Debug.WriteLine($"{"gamma",8} {"Q",14} {"dQ/dg",14} {"d2Q/dg2",14} {"convexity",12} {"sign",6}");
        Debug.WriteLine(new string('-', 72));

        foreach (double gamma in Gammas)
        {
            double K = ComputeK(gamma, targetAEP);
            double dKdg = ComputeDKDGamma(gamma, targetAEP);
            double d2Kdg2 = ComputeD2KDGamma2(gamma, targetAEP);

            // LP3 quantile: Q = 10^(mu + sigma * K)
            double logQ = mu + sigma * K;
            double Q = Math.Pow(10.0, logQ);

            // First derivative: dQ/dgamma = Q * sigma * dK/dgamma * ln(10)
            double ln10 = Math.Log(10.0);
            double dQdg = Q * sigma * dKdg * ln10;

            // Second derivative: d²Q/dgamma²
            // = Q * [sigma * d²K/dgamma² * ln(10) + (sigma * dK/dgamma * ln(10))²]
            double d2Qdg2 = Q * (sigma * d2Kdg2 * ln10 + Math.Pow(sigma * dKdg * ln10, 2));

            // Convexity ratio: |d²Q/dgamma²| / |dQ/dgamma| — rate of curvature per unit slope
            double convexity = Math.Abs(dQdg) > 1e-12
                ? d2Qdg2 / Math.Abs(dQdg)
                : double.NaN;

            string sign = d2Qdg2 > 0 ? "+" : "-";

            Debug.WriteLine($"{gamma,8:F1} {Q,14:F2} {dQdg,14:F2} {d2Qdg2,14:F2} {convexity,12:F4} {sign,6}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("Key: positive d²Q/dg² means quantile is convex in gamma (upper CI wider than lower)");
        Debug.WriteLine("========== END LP3 QUANTILE CONVEXITY ==========");
    }

    #endregion

    #region Theoretical a(gamma) Derivation

    /// <summary>
    /// Proposes a theoretical a(gamma) curve based on the LP3 quantile convexity at AEP = 0.0001.
    /// The idea: a(gamma) should scale with the quantile convexity so that the SES link
    /// produces CIs with the correct asymmetry.
    /// </summary>
    [TestMethod]
    public void TheoreticalA_FromConvexity()
    {
        const double targetAEP = 0.0001;
        const double sigma = 0.5;
        const double mu = 3.0;

        Debug.WriteLine("========== THEORETICAL a(gamma) vs. CURRENT ==========");
        Debug.WriteLine("");

        // First compute convexity at each gamma
        var gammaVals = new List<double>();
        var kappaVals = new List<double>();

        foreach (double gamma in Gammas)
        {
            double K = ComputeK(gamma, targetAEP);
            double dKdg = ComputeDKDGamma(gamma, targetAEP);
            double d2Kdg2 = ComputeD2KDGamma2(gamma, targetAEP);

            double ln10 = Math.Log(10.0);
            double logQ = mu + sigma * K;
            double Q = Math.Pow(10.0, logQ);
            double dQdg = Q * sigma * dKdg * ln10;
            double d2Qdg2 = Q * (sigma * d2Kdg2 * ln10 + Math.Pow(sigma * dKdg * ln10, 2));

            double kappa = Math.Abs(dQdg) > 1e-12
                ? Math.Abs(d2Qdg2 / dQdg)
                : 0;

            gammaVals.Add(gamma);
            kappaVals.Add(kappa);
        }

        // Normalize kappa to range [0.5, 6.0] matching a(gamma) bounds
        double kappaMin = kappaVals.Min();
        double kappaMax = kappaVals.Max();

        Debug.WriteLine($"{"gamma",8} {"kappa_raw",12} {"a_scaled",12} {"a_current",12} {"ratio",10}");
        Debug.WriteLine(new string('-', 58));

        for (int i = 0; i < gammaVals.Count; i++)
        {
            double gamma = gammaVals[i];
            double kappa = kappaVals[i];

            // Scale kappa linearly to [0.5, 6.0]
            double aScaled = kappaMax > kappaMin
                ? 0.5 + 5.5 * (kappa - kappaMin) / (kappaMax - kappaMin)
                : 1.0;

            // Current heuristic
            double g2 = gamma * gamma;
            double aCurrent = 1.0 + 3.0 * Math.Tanh(0.8 * gamma) + 2.0 * g2 / (2.0 + g2);
            aCurrent = Math.Max(0.5, Math.Min(6.0, aCurrent));

            double ratio = aCurrent > 0 ? aScaled / aCurrent : double.NaN;

            Debug.WriteLine($"{gamma,8:F1} {kappa,12:F4} {aScaled,12:F4} {aCurrent,12:F4} {ratio,10:F4}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== END THEORETICAL a(gamma) ==========");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Computes the frequency factor K_gamma(p) = PT3(0, 1, gamma).InverseCDF(1-AEP).
    /// </summary>
    /// <param name="gamma">The skewness parameter.</param>
    /// <param name="aep">The annual exceedance probability.</param>
    /// <returns>The frequency factor K.</returns>
    /// <remarks>
    /// Uses PearsonTypeIII(mu=0, sigma=1, gamma) so that InverseCDF(p) directly gives K.
    /// Handles gamma=0 as Normal(0,1) and gamma near zero with small perturbation.
    /// </remarks>
    private static double ComputeK(double gamma, double aep)
    {
        double p = 1.0 - aep; // non-exceedance probability
        if (Math.Abs(gamma) < 1e-10)
        {
            // gamma=0 → Normal(0,1)
            return Normal.StandardZ(p);
        }

        var dist = new PearsonTypeIII(0.0, 1.0, gamma);
        return dist.InverseCDF(p);
    }

    /// <summary>
    /// Computes dK/dgamma via central finite differences.
    /// </summary>
    /// <param name="gamma">The skewness parameter.</param>
    /// <param name="aep">The annual exceedance probability.</param>
    /// <returns>The first derivative of K with respect to gamma.</returns>
    private static double ComputeDKDGamma(double gamma, double aep)
    {
        double h = DGamma;

        // Use central differences with adaptive step near boundaries
        double gammaPlus = gamma + h;
        double gammaMinus = gamma - h;

        // Clamp to valid range if needed (P3 supports gamma in [-6, 6])
        if (gammaPlus > 5.99) gammaPlus = 5.99;
        if (gammaMinus < -5.99) gammaMinus = -5.99;

        double KPlus = ComputeK(gammaPlus, aep);
        double KMinus = ComputeK(gammaMinus, aep);

        return (KPlus - KMinus) / (gammaPlus - gammaMinus);
    }

    /// <summary>
    /// Computes d²K/dgamma² via central finite differences.
    /// </summary>
    /// <param name="gamma">The skewness parameter.</param>
    /// <param name="aep">The annual exceedance probability.</param>
    /// <returns>The second derivative of K with respect to gamma.</returns>
    private static double ComputeD2KDGamma2(double gamma, double aep)
    {
        double h = DGamma;

        // Use central differences: f''(x) ≈ [f(x+h) - 2f(x) + f(x-h)] / h²
        double gammaPlus = gamma + h;
        double gammaMinus = gamma - h;

        // Clamp to valid range
        if (gammaPlus > 5.99) gammaPlus = 5.99;
        if (gammaMinus < -5.99) gammaMinus = -5.99;

        double KPlus = ComputeK(gammaPlus, aep);
        double KCenter = ComputeK(gamma, aep);
        double KMinus = ComputeK(gammaMinus, aep);

        // Adjust h for asymmetric step (at boundaries)
        double hPlus = gammaPlus - gamma;
        double hMinus = gamma - gammaMinus;

        if (Math.Abs(hPlus - hMinus) < 1e-12)
        {
            // Symmetric case
            return (KPlus - 2.0 * KCenter + KMinus) / (h * h);
        }
        else
        {
            // Asymmetric finite difference
            return 2.0 * (KPlus / (hPlus * (hPlus + hMinus))
                        - KCenter / (hPlus * hMinus)
                        + KMinus / (hMinus * (hPlus + hMinus)));
        }
    }

    #endregion
}
