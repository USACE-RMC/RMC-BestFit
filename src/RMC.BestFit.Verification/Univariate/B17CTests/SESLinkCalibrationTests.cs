using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Functions;
using Numerics.Sampling;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.LinkFunctions;
using System.Diagnostics;
using System.Text;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// SES link calibration experiments for the symmetric-a hypothesis.
/// </summary>
/// <remarks>
/// <para>
///     Authors:
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
/// Tests whether a symmetric SES 'a' parameter (same for +γ and −γ) that matches
/// the parameter-level bootstrap kurtosis of γ̂ can produce correct quantile-level
/// coverage when combined with the MVN correlation structure in V_η = G·Σ̂·G'.
/// </para>
/// <para>
/// The core hypothesis: the quantile-level asymmetry (positive γ → unbounded upper tail,
/// negative γ → constrained) is driven by parameter correlations (ρ_σγ &gt; 0 for positive γ,
/// &lt; 0 for negative γ), which are already present in the off-diagonal terms of V_η.
/// A symmetric 'a' matching parameter-level kurtosis should therefore suffice.
/// </para>
/// <para>
/// Reference data: PIT bootstrap experiment (52 cells, 13γ × 4n, M=50 datasets, B=10K bootstrap).
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class SESLinkCalibrationTests
{
    #region Constants and Reference Data

    /// <summary>
    /// Gamma grid matching PIT experiment.
    /// </summary>
    private static readonly double[] Gammas = { -1.5, -1.0, -0.5, -0.25, -0.1, -0.05, 0.0, 0.05, 0.1, 0.25, 0.5, 1.0, 1.5 };

    /// <summary>
    /// Sample sizes matching PIT experiment.
    /// </summary>
    private static readonly int[] SampleSizes = { 25, 50, 100, 200 };

    /// <summary>
    /// Fixed LP3 parameters (sigma-independence confirmed by prior experiments).
    /// </summary>
    private const double Mu = 3.0;
    private const double Sigma = 0.5;

    /// <summary>
    /// Monte Carlo sample count for SES kurtosis computation.
    /// </summary>
    private const int KurtosisMCSamples = 100000;

    /// <summary>
    /// PIT bootstrap reference data: boot_kurt_gamma for each (gamma, n) cell.
    /// Rows ordered by Gammas × SampleSizes (13 × 4 = 52 cells).
    /// </summary>
    private static readonly double[,] PitReference = new double[52, 7]
    {
        // gamma_true, n, mean_gamma_hat, boot_sd_gamma, boot_skew_gamma, boot_kurt_gamma, corr_sigma_gamma
        { -1.50,  25, -1.168431, 0.564582, -0.6671, 3.9241, -0.4540 },
        { -1.50,  50, -1.330074, 0.501284, -0.9567, 4.9672, -0.4896 },
        { -1.50, 100, -1.342158, 0.416294, -1.1269, 5.9886, -0.5109 },
        { -1.50, 200, -1.419990, 0.344357, -1.1657, 6.3247, -0.5134 },
        { -1.00,  25, -0.911236, 0.539204, -0.5495, 3.8810, -0.3759 },
        { -1.00,  50, -0.854474, 0.424677, -0.7841, 4.6386, -0.4283 },
        { -1.00, 100, -0.950751, 0.343800, -0.9065, 5.2195, -0.4676 },
        { -1.00, 200, -0.969319, 0.262668, -0.8913, 5.2699, -0.4783 },
        { -0.50,  25, -0.448166, 0.496258, -0.3468, 3.8232, -0.2400 },
        { -0.50,  50, -0.435761, 0.370680, -0.4515, 4.0439, -0.2712 },
        { -0.50, 100, -0.485062, 0.277860, -0.5239, 4.1805, -0.3148 },
        { -0.50, 200, -0.455847, 0.197278, -0.4376, 3.8211, -0.3150 },
        { -0.25,  25, -0.206070, 0.487941, -0.1533, 3.7721, -0.1042 },
        { -0.25,  50, -0.279959, 0.360044, -0.3071, 3.9017, -0.1814 },
        { -0.25, 100, -0.267210, 0.257911, -0.2903, 3.6538, -0.1870 },
        { -0.25, 200, -0.214768, 0.179554, -0.2083, 3.4712, -0.1674 },
        { -0.10,  25,  0.006615, 0.486047,  0.0226, 3.7613,  0.0130 },
        { -0.10,  50, -0.064914, 0.347398, -0.0832, 3.7046, -0.0475 },
        { -0.10, 100, -0.116676, 0.250073, -0.1306, 3.5137, -0.0883 },
        { -0.10, 200, -0.098622, 0.177305, -0.0868, 3.2792, -0.0710 },
        { -0.05,  25,  0.001459, 0.480180,  0.0073, 3.7174,  0.0077 },
        { -0.05,  50, -0.013710, 0.353437, -0.0199, 3.7977, -0.0088 },
        { -0.05, 100, -0.020410, 0.247493, -0.0199, 3.4540, -0.0186 },
        { -0.05, 200, -0.079774, 0.176433, -0.0691, 3.2690, -0.0634 },
        {  0.00,  25, -0.057282, 0.481479, -0.0491, 3.7386, -0.0338 },
        {  0.00,  50,  0.021583, 0.349628,  0.0174, 3.6781,  0.0101 },
        {  0.00, 100,  0.008692, 0.247736,  0.0148, 3.4589,  0.0061 },
        {  0.00, 200, -0.027938, 0.174538, -0.0277, 3.2260, -0.0224 },
        {  0.05,  25,  0.048044, 0.486598,  0.0267, 3.7548,  0.0186 },
        {  0.05,  50,  0.096992, 0.352056,  0.1061, 3.7963,  0.0644 },
        {  0.05, 100,  0.070662, 0.248923,  0.0789, 3.4625,  0.0560 },
        {  0.05, 200,  0.048435, 0.174644,  0.0388, 3.2338,  0.0393 },
        {  0.10,  25,  0.077817, 0.484346,  0.0415, 3.7122,  0.0266 },
        {  0.10,  50,  0.105455, 0.349598,  0.1257, 3.7469,  0.0773 },
        {  0.10, 100,  0.101379, 0.250016,  0.1087, 3.5036,  0.0731 },
        {  0.10, 200,  0.051576, 0.175168,  0.0494, 3.2028,  0.0425 },
        {  0.25,  25,  0.256844, 0.485613,  0.2052, 3.7707,  0.1402 },
        {  0.25,  50,  0.257135, 0.356812,  0.2801, 3.8672,  0.1719 },
        {  0.25, 100,  0.242763, 0.254889,  0.2732, 3.6103,  0.1767 },
        {  0.25, 200,  0.227181, 0.179663,  0.2117, 3.3591,  0.1766 },
        {  0.50,  25,  0.294564, 0.483240,  0.2404, 3.7526,  0.1707 },
        {  0.50,  50,  0.409321, 0.369703,  0.4184, 4.0119,  0.2479 },
        {  0.50, 100,  0.439245, 0.271847,  0.4876, 4.1337,  0.2924 },
        {  0.50, 200,  0.432322, 0.195225,  0.4003, 3.7587,  0.3008 },
        {  1.00,  25,  0.890295, 0.538147,  0.5760, 3.9583,  0.3863 },
        {  1.00,  50,  0.919686, 0.432613,  0.8124, 4.7214,  0.4438 },
        {  1.00, 100,  1.014901, 0.354956,  0.9655, 5.3654,  0.4791 },
        {  1.00, 200,  0.925836, 0.255247,  0.8642, 5.1711,  0.4718 },
        {  1.50,  25,  1.304624, 0.581794,  0.6952, 3.9403,  0.4658 },
        {  1.50,  50,  1.361726, 0.507050,  0.9744, 4.9594,  0.5007 },
        {  1.50, 100,  1.402249, 0.427488,  1.1235, 5.8191,  0.5118 },
        {  1.50, 200,  1.493141, 0.356957,  1.2174, 6.5675,  0.5177 },
    };

    /// <summary>Column indices for PitReference.</summary>
    private const int ColGamma = 0, ColN = 1, ColMeanGammaHat = 2, ColBootSdGamma = 3,
                       ColBootSkewGamma = 4, ColBootKurtGamma = 5, ColCorrSigmaGamma = 6;

    #endregion

    #region Test 1: SES Kurtosis Function Tabulation

    /// <summary>
    /// Tabulates the deterministic kurtosis of the SES inverse link applied to standard normal draws.
    /// At a=0 the SES degenerates to identity → kurtosis = 3 (Gaussian). Larger a → heavier tails.
    /// Verifies analytical formula against Monte Carlo.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void SESKurtosisFunction_Tabulation()
    {
        var csv = new StringBuilder();
        csv.AppendLine("a,kurt_analytical,kurt_mc,abs_diff");

        double[] aGrid = { 0.0, 0.05, 0.1, 0.15, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9,
                           1.0, 1.2, 1.4, 1.6, 1.8, 2.0, 2.5, 3.0, 3.5, 4.0, 5.0, 6.0, 8.0, 10.0 };

        foreach (double a in aGrid)
        {
            double kurtAnalytical = ComputeSESKurtosisAnalytical(a);
            double kurtMC = ComputeSESKurtosisMC(a, lambda: 0.0, sigmaEta: 1.0, etaHat: 0.0,
                                                  nSamples: 500000, seed: 12345);
            double diff = Math.Abs(kurtAnalytical - kurtMC);

            csv.AppendLine($"{a:F4},{kurtAnalytical:F6},{kurtMC:F6},{diff:F6}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== SES KURTOSIS FUNCTION TABULATION ==========");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END SES KURTOSIS FUNCTION TABULATION ==========");
    }

    #endregion

    #region Test 2: Kurtosis Matching All Cells

    /// <summary>
    /// For each (n, γ) cell, finds the symmetric SES 'a' that reproduces the observed bootstrap kurtosis.
    /// Uses full non-centered Monte Carlo at each cell's actual (η̂, σ_η) values.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void KurtosisMatching_AllCells()
    {
        var csv = new StringBuilder();
        csv.AppendLine("gamma_true,n,boot_kurt_gamma,boot_sd_gamma,a_matched,a_current,a_ratio,ses_kurt_achieved");

        for (int row = 0; row < 52; row++)
        {
            double gamma = PitReference[row, ColGamma];
            int n = (int)PitReference[row, ColN];
            double gammaHat = PitReference[row, ColMeanGammaHat];
            double bootSdGamma = PitReference[row, ColBootSdGamma];
            double bootKurtTarget = PitReference[row, ColBootKurtGamma];

            // Current production a(γ) for comparison
            double g2 = gammaHat * gammaHat;
            double aCurrent = 1.0 + 3.0 * Math.Tanh(0.8 * gammaHat) + 2.0 * g2 / (2.0 + g2);
            aCurrent = Math.Max(0.5, Math.Min(4.0, aCurrent));

            // Compute λ from the current R_gamma formula
            double lnRGamma = gammaHat >= 0
                ? 0.13 + 1.22 * Math.Tanh(1.01 * gammaHat)
                : 0.13 + 1.52 * Math.Tanh(0.81 * gammaHat);

            // Binary search for a_matched
            double aMatched = FindAMatchingKurtosis(bootKurtTarget, gammaHat, bootSdGamma, lnRGamma, seed: row * 1000 + 42);

            // Verify: compute kurtosis at a_matched
            double kurtAchieved = ComputeSESKurtosisAtCell(aMatched, gammaHat, bootSdGamma, lnRGamma, seed: row * 1000 + 99);

            double aRatio = aCurrent > 1e-10 ? aMatched / aCurrent : double.NaN;

            csv.AppendLine($"{gamma:F2},{n},{bootKurtTarget:F4},{bootSdGamma:F6}," +
                           $"{aMatched:F4},{aCurrent:F4},{aRatio:F4},{kurtAchieved:F4}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== SES KURTOSIS MATCHING — ALL CELLS ==========");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END SES KURTOSIS MATCHING ==========");
    }

    #endregion

    #region Test 2b: Kurtosis Matching Lambda=0

    /// <summary>
    /// Repeats kurtosis matching with λ=0 to isolate the pure SES 'a' → kurtosis relationship.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Test 2 confounded 'a' and 'λ' because the SES link at a→0 with λ≠0 reduces to
    /// γ(η) = η·exp(λη), which is NOT identity — the exponential tilt creates leptokurtic
    /// back-transformed draws even at a=0. This test removes the confound by forcing λ=0.
    /// </para>
    /// <para>
    /// With λ=0, SES at a=0 is true identity → kurtosis = 3.0 exactly. Any bootstrap
    /// kurtosis above 3.0 should be matchable by some a &gt; 0, and the a_matched values
    /// should reflect pure sinh-curvature kurtosis without exponential tilt contamination.
    /// </para>
    /// <para>
    /// Key hypothesis: a_matched(λ=0) should be symmetric in |γ| since the parameter-level
    /// bootstrap kurtosis is symmetric, and the sinh function is an odd function.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void KurtosisMatching_LambdaZero()
    {
        var csv = new StringBuilder();
        csv.AppendLine("gamma_true,n,boot_kurt_gamma,a_matched_lam0,a_matched_with_lam,kurt_at_a0_lam0,kurt_at_a12_lam0");

        for (int row = 0; row < 52; row++)
        {
            double gamma = PitReference[row, ColGamma];
            int n = (int)PitReference[row, ColN];
            double gammaHat = PitReference[row, ColMeanGammaHat];
            double bootSdGamma = PitReference[row, ColBootSdGamma];
            double bootKurtTarget = PitReference[row, ColBootKurtGamma];

            // λ=0: pure a → kurtosis matching
            double aMatchedLam0 = FindAMatchingKurtosis(bootKurtTarget, gammaHat, bootSdGamma,
                lnRGamma: 0.0, seed: row * 1000 + 42);

            // Also get the original Test 2 result (with λ) for comparison
            double lnRGamma = gammaHat >= 0
                ? 0.13 + 1.22 * Math.Tanh(1.01 * gammaHat)
                : 0.13 + 1.52 * Math.Tanh(0.81 * gammaHat);
            double aMatchedWithLam = FindAMatchingKurtosis(bootKurtTarget, gammaHat, bootSdGamma,
                lnRGamma, seed: row * 1000 + 42);

            // Kurtosis at boundary a values with λ=0
            double kurtAtA0 = ComputeSESKurtosisAtCell(0.0, gammaHat, bootSdGamma,
                lnRGamma: 0.0, seed: row * 1000 + 88);
            double kurtAtA12 = ComputeSESKurtosisAtCell(12.0, gammaHat, bootSdGamma,
                lnRGamma: 0.0, seed: row * 1000 + 99);

            csv.AppendLine($"{gamma:F2},{n},{bootKurtTarget:F4},{aMatchedLam0:F4}," +
                           $"{aMatchedWithLam:F4},{kurtAtA0:F4},{kurtAtA12:F4}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== SES KURTOSIS MATCHING — LAMBDA=0 ==========");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END SES KURTOSIS MATCHING LAMBDA=0 ==========");
    }

    #endregion

    #region Test 2c: Kurtosis Decomposition

    /// <summary>
    /// Decomposes total SES kurtosis into a-driven and λ-driven components at each cell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For each cell, computes three kurtosis values:
    /// </para>
    /// <list type="bullet">
    ///     <item><description>kurt(a=0, λ=cell_λ) — kurtosis from λ alone (exponential tilt, no sinh curvature)</description></item>
    ///     <item><description>kurt(a=cell_a, λ=0) — kurtosis from a alone (sinh curvature, no exponential tilt)</description></item>
    ///     <item><description>kurt(a=cell_a, λ=cell_λ) — combined effect</description></item>
    /// </list>
    /// <para>
    /// This reveals whether a and λ kurtosis contributions are approximately additive,
    /// multiplicative, or strongly interactive, which determines whether they can be
    /// calibrated independently.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void KurtosisDecomposition()
    {
        var csv = new StringBuilder();
        csv.AppendLine("gamma_true,n,boot_kurt,kurt_lam_only,kurt_a_only,kurt_combined,lambda,a_current,excess_lam,excess_a,excess_combined");

        for (int row = 0; row < 52; row++)
        {
            double gamma = PitReference[row, ColGamma];
            int n = (int)PitReference[row, ColN];
            double gammaHat = PitReference[row, ColMeanGammaHat];
            double bootSdGamma = PitReference[row, ColBootSdGamma];
            double bootKurt = PitReference[row, ColBootKurtGamma];

            // Compute production a(γ) for this cell
            double g2 = gammaHat * gammaHat;
            double aCurrent = 1.0 + 3.0 * Math.Tanh(0.8 * gammaHat) + 2.0 * g2 / (2.0 + g2);
            aCurrent = Math.Max(0.5, Math.Min(4.0, aCurrent));

            // Compute λ from R_gamma formula
            double lnRGamma = gammaHat >= 0
                ? 0.13 + 1.22 * Math.Tanh(1.01 * gammaHat)
                : 0.13 + 1.52 * Math.Tanh(0.81 * gammaHat);

            // Compute the actual λ value for output
            // First need a link to compute delta
            var tempLink = new SESLink(a: Math.Max(aCurrent, 1e-12)) { UseAdaptiveLambda = false };
            double dLink = tempLink.DLink(gammaHat);
            double sigmaEta = bootSdGamma * Math.Abs(dLink);
            double deltaGamma = 1.645 * sigmaEta;
            double lambdaVal = deltaGamma > 1e-10 ? lnRGamma / (2.0 * deltaGamma) : 0.0;
            lambdaVal = Math.Max(-0.95, Math.Min(0.95, lambdaVal));

            // Apply monotonicity constraint from production
            aCurrent = Math.Max(aCurrent, Math.Abs(lambdaVal) + 0.10);

            int seed = row * 1000 + 55;

            // 1. λ-only: a=0, λ=cell_λ (exponential tilt kurtosis)
            double kurtLamOnly = ComputeSESKurtosisAtCell(0.0, gammaHat, bootSdGamma, lnRGamma, seed);

            // 2. a-only: a=cell_a, λ=0 (sinh curvature kurtosis)
            double kurtAOnly = ComputeSESKurtosisAtCell(aCurrent, gammaHat, bootSdGamma, lnRGamma: 0.0, seed);

            // 3. Combined: a=cell_a, λ=cell_λ (full SES kurtosis)
            double kurtCombined = ComputeSESKurtosisAtCell(aCurrent, gammaHat, bootSdGamma, lnRGamma, seed);

            // Excess kurtosis above Gaussian (3.0) for decomposition analysis
            double excessLam = kurtLamOnly - 3.0;
            double excessA = kurtAOnly - 3.0;
            double excessCombined = kurtCombined - 3.0;

            csv.AppendLine($"{gamma:F2},{n},{bootKurt:F4},{kurtLamOnly:F4},{kurtAOnly:F4},{kurtCombined:F4}," +
                           $"{lambdaVal:F4},{aCurrent:F4},{excessLam:F4},{excessA:F4},{excessCombined:F4}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== SES KURTOSIS DECOMPOSITION ==========");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END SES KURTOSIS DECOMPOSITION ==========");
    }

    #endregion

    #region Test 3: Influence Function Kurtosis

    /// <summary>
    /// For each (n, γ) cell, computes the GMM influence function kurtosis κ_ψ(γ)
    /// and checks if it predicts a_matched from Test 2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates M=50 independent LP3 datasets per cell, fits GMM to each,
    /// extracts the [n × 3] influence matrix, and computes kurtosis of the γ-column.
    /// </para>
    /// <para>
    /// If the influence function kurtosis predicts a_matched from bootstrap,
    /// then 'a' can be derived at runtime from the GMM influence diagnostics
    /// without any hardcoded formula.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void InfluenceKurtosis_AllCells()
    {
        const int M = 50;
        var csv = new StringBuilder();
        csv.AppendLine("gamma_true,n,kappa_IF_gamma_mean,kappa_IF_gamma_std,a_predicted_IF,a_matched_bootstrap,se_gamma_mean,corr_sigma_gamma_mean");

        for (int row = 0; row < 52; row++)
        {
            double gamma = PitReference[row, ColGamma];
            int n = (int)PitReference[row, ColN];
            double bootKurtTarget = PitReference[row, ColBootKurtGamma];
            double bootSdGamma = PitReference[row, ColBootSdGamma];
            double gammaHat = PitReference[row, ColMeanGammaHat];

            // Compute a_matched from bootstrap kurtosis (same as Test 2)
            double lnRGamma = gammaHat >= 0
                ? 0.13 + 1.22 * Math.Tanh(1.01 * gammaHat)
                : 0.13 + 1.52 * Math.Tanh(0.81 * gammaHat);
            double aMatchedBoot = FindAMatchingKurtosis(bootKurtTarget, gammaHat, bootSdGamma, lnRGamma, seed: row * 1000 + 42);

            // Generate M datasets and compute influence kurtosis
            var kappaValues = new List<double>();
            var seGammaValues = new List<double>();
            var corrSigmaGammaValues = new List<double>();
            var trueDist = new LogPearsonTypeIII(Mu, Sigma, gamma);
            var rng = new MersenneTwister(row * 10000 + 7);

            for (int m = 0; m < M; m++)
            {
                try
                {
                    var data = trueDist.GenerateRandomValues(n, rng.Next());
                    var df = new DataFrame();
                    df.ExactSeries = new ExactSeries(data);

                    var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
                    model.LinkController = LinkController.ForLocationScaleShape();
                    model.SetPenaltyFunction();

                    var gmm = new GeneralizedMethodOfMoments(model);
                    gmm.Estimate();

                    if (!gmm.IsEstimated) continue;

                    // Get influence matrix [n × 3]
                    var influence = gmm.GetObservationInfluence();
                    int nObs = influence.GetLength(0);

                    // Extract γ column (index 2) and compute kurtosis
                    var gammaInfluence = new double[nObs];
                    for (int i = 0; i < nObs; i++)
                        gammaInfluence[i] = influence[i, 2];

                    double kappa = Kurtosis(gammaInfluence, nObs);
                    if (!double.IsNaN(kappa) && Tools.IsFinite(kappa))
                        kappaValues.Add(kappa);

                    // Get covariance matrix for SE and correlations
                    var cov = gmm.GetCovarianceMatrix();
                    double seGamma = Math.Sqrt(Math.Max(0, cov[2, 2]));
                    seGammaValues.Add(seGamma);

                    double seSigma = Math.Sqrt(Math.Max(0, cov[1, 1]));
                    if (seSigma > 0 && seGamma > 0)
                        corrSigmaGammaValues.Add(cov[1, 2] / (seSigma * seGamma));
                }
                catch
                {
                    // Skip failed datasets
                }
            }

            double kappaMean = kappaValues.Count > 0 ? kappaValues.Average() : double.NaN;
            double kappaStd = kappaValues.Count > 1
                ? Math.Sqrt(kappaValues.Select(k => (k - kappaMean) * (k - kappaMean)).Sum() / (kappaValues.Count - 1))
                : double.NaN;
            double seGammaMean = seGammaValues.Count > 0 ? seGammaValues.Average() : double.NaN;
            double corrMean = corrSigmaGammaValues.Count > 0 ? corrSigmaGammaValues.Average() : double.NaN;

            // Map influence kurtosis → a_predicted via SES kurtosis matching
            double aPredictedIF = double.NaN;
            if (!double.IsNaN(kappaMean) && kappaMean > 3.0)
            {
                aPredictedIF = FindAMatchingKurtosis(kappaMean, gammaHat, bootSdGamma, lnRGamma, seed: row * 1000 + 77);
            }
            else if (!double.IsNaN(kappaMean))
            {
                aPredictedIF = 0.0; // Gaussian or sub-Gaussian
            }

            csv.AppendLine($"{gamma:F2},{n},{kappaMean:F4},{kappaStd:F4},{aPredictedIF:F4},{aMatchedBoot:F4},{seGammaMean:F6},{corrMean:F4}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== INFLUENCE KURTOSIS — ALL CELLS ==========");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END INFLUENCE KURTOSIS ==========");
    }

    #endregion

    #region Test 4: Model-Based Kurtosis PT3

    /// <summary>
    /// Computes the theoretical kurtosis of the g₃ moment condition from the PT3 distribution
    /// via Monte Carlo, and compares to the influence function kurtosis from Test 3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For PT3 with (μ, σ, γ): g₃ = (Y-μ)³ - μ₃ where μ₃ = γσ³.
    /// The kurtosis of g₃ is a population quantity (n-independent), computed by generating
    /// N=1M draws from the PT3 and evaluating the fourth moment ratio.
    /// </para>
    /// <para>
    /// If this population kurtosis matches the influence function kurtosis (which has n-dependence
    /// from the sandwich structure), it means the sandwich covariance doesn't distort the tail
    /// structure significantly, and a model-based analytical formula for 'a' is feasible.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void ModelBasedKurtosis_PT3()
    {
        const int N = 1000000;
        var csv = new StringBuilder();
        csv.AppendLine("gamma_true,kappa_g3_model,kappa_g2_model,eg3_sq,eg3_fourth");

        foreach (double gamma in Gammas)
        {
            // Generate large sample from PT3(Mu, Sigma, gamma) in log-space
            // LP3 is PT3 applied to log10(Y), so we work directly in log-space
            var pt3 = new PearsonTypeIII(Mu, Sigma, gamma);
            var rng = new MersenneTwister((int)((gamma + 2.0) * 1000) + 12345);
            var values = pt3.GenerateRandomValues(N, rng.Next());

            // Compute g₃ = (Y - μ)³ - μ₃ for each observation
            double mu3 = gamma * Sigma * Sigma * Sigma;
            var g3 = new double[N];
            var g2 = new double[N];
            for (int i = 0; i < N; i++)
            {
                double d = values[i] - Mu;
                g3[i] = d * d * d - mu3;
                g2[i] = d * d - Sigma * Sigma;
            }

            double kappaG3 = Kurtosis(g3, N);
            double kappaG2 = Kurtosis(g2, N);

            // Also compute Var(g₃) analytically for verification
            // E[g₃²] = μ₆ - μ₃²
            double s2 = Sigma * Sigma;
            double s4 = s2 * s2;
            double s6 = s4 * s2;
            double g2Val = gamma * gamma;
            double mu6 = s6 * (15.0 + 32.5 * g2Val + 7.5 * g2Val * g2Val);
            double eg3Sq = mu6 - mu3 * mu3;

            // E[g₃⁴] from MC
            double m2 = 0, m4 = 0;
            double mean = 0;
            for (int i = 0; i < N; i++) mean += g3[i];
            mean /= N;
            for (int i = 0; i < N; i++)
            {
                double d = g3[i] - mean;
                double d2 = d * d;
                m2 += d2;
                m4 += d2 * d2;
            }
            m2 /= N;
            m4 /= N;
            double eg3Fourth = m4; // This is E[(g₃-E[g₃])⁴] ≈ E[g₃⁴] since E[g₃]≈0

            csv.AppendLine($"{gamma:F2},{kappaG3:F4},{kappaG2:F4},{eg3Sq:F6},{eg3Fourth:F6}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== MODEL-BASED KURTOSIS PT3 ==========");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END MODEL-BASED KURTOSIS PT3 ==========");
    }

    #endregion

    #region Test 5: Symmetric A Quantile Coverage

    /// <summary>
    /// Definitive validation: does symmetric a_matched + MVN correlations give correct
    /// quantile-level coverage across the (γ, n, AEP) grid?
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses the <see cref="Bulletin17CAnalysis.AFormulaOverride"/> hook to inject the symmetric
    /// formula, and compares coverage against the current asymmetric formula (control).
    /// </para>
    /// <para>
    /// For each (γ, n) cell, generates B=500 independent LP3 datasets, fits B17C with both
    /// current and symmetric formulas, and checks whether the true quantiles fall within
    /// the 90% confidence intervals. Reports coverage, miss-above, and miss-below for
    /// both approaches side by side.
    /// </para>
    /// <para>
    /// The symmetric formula is a placeholder that should be updated based on Test 2/6 results.
    /// Update <see cref="SymmetricAFormula"/> with the calibrated formula before running.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task SymmetricA_QuantileCoverage()
    {
        // Coverage test subset: 7 γ values × 4 n values = 28 cells
        double[] coverageGammas = { -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5 };
        int[] coverageNs = { 25, 50, 100, 200 };
        int B = 500;
        double nominalCI = 0.90;

        // AEPs to evaluate (standard B17C ordinates)
        double[] aeps = { 0.10, 0.04, 0.02, 0.01, 0.005, 0.002, 0.001 };

        var csv = new StringBuilder();
        csv.AppendLine("gamma_true,n,aep,coverage_current,miss_above_current,miss_below_current," +
                       "coverage_symmetric,miss_above_sym,miss_below_sym,n_valid");

        foreach (double gamma in coverageGammas)
        {
            foreach (int n in coverageNs)
            {
                var trueDist = new LogPearsonTypeIII(Mu, Sigma, gamma);

                // True quantiles at each AEP
                var trueQ = new double[aeps.Length];
                for (int j = 0; j < aeps.Length; j++)
                    trueQ[j] = trueDist.InverseCDF(1.0 - aeps[j]);

                // Coverage accumulators
                var covCurrent = new double[aeps.Length];
                var missAboveCurrent = new double[aeps.Length];
                var missBelowCurrent = new double[aeps.Length];
                var covSymmetric = new double[aeps.Length];
                var missAboveSymmetric = new double[aeps.Length];
                var missBelowSymmetric = new double[aeps.Length];
                int validCount = 0;

                var masterRng = new MersenneTwister((int)((gamma + 2.0) * 1000) + n * 100 + 42);
                var seeds = masterRng.NextIntegers(B);

                for (int rep = 0; rep < B; rep++)
                {
                    var data = trueDist.GenerateRandomValues(n, seeds[rep]);

                    // Run with CURRENT asymmetric formula
                    double[,]? cisCurrent = await RunSingleAnalysis(data, null, nominalCI, seeds[rep], aeps);

                    // Run with SYMMETRIC formula
                    double[,]? cisSymmetric = await RunSingleAnalysis(data, SymmetricAFormula, nominalCI, seeds[rep] + 1, aeps);

                    if (cisCurrent == null || cisSymmetric == null) continue;
                    validCount++;

                    for (int j = 0; j < aeps.Length; j++)
                    {
                        // Current formula coverage
                        if (trueQ[j] >= cisCurrent[j, 0] && trueQ[j] <= cisCurrent[j, 1])
                            covCurrent[j]++;
                        else if (trueQ[j] > cisCurrent[j, 1])
                            missAboveCurrent[j]++;
                        else
                            missBelowCurrent[j]++;

                        // Symmetric formula coverage
                        if (trueQ[j] >= cisSymmetric[j, 0] && trueQ[j] <= cisSymmetric[j, 1])
                            covSymmetric[j]++;
                        else if (trueQ[j] > cisSymmetric[j, 1])
                            missAboveSymmetric[j]++;
                        else
                            missBelowSymmetric[j]++;
                    }
                }

                // Normalize
                if (validCount > 0)
                {
                    for (int j = 0; j < aeps.Length; j++)
                    {
                        covCurrent[j] /= validCount;
                        missAboveCurrent[j] /= validCount;
                        missBelowCurrent[j] /= validCount;
                        covSymmetric[j] /= validCount;
                        missAboveSymmetric[j] /= validCount;
                        missBelowSymmetric[j] /= validCount;

                        csv.AppendLine($"{gamma:F2},{n},{aeps[j]:F4}," +
                            $"{covCurrent[j]:F3},{missAboveCurrent[j]:F3},{missBelowCurrent[j]:F3}," +
                            $"{covSymmetric[j]:F3},{missAboveSymmetric[j]:F3},{missBelowSymmetric[j]:F3},{validCount}");
                    }
                }
            }
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== SYMMETRIC A QUANTILE COVERAGE ==========");
        Debug.Write(csv.ToString());
        Debug.WriteLine("========== END SYMMETRIC A QUANTILE COVERAGE ==========");
    }

    /// <summary>
    /// Calibrated symmetric 'a' formula from Test 6 (Formula 2, λ=0 kurtosis matching).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Formula: a = c₀ + c₁·γ²/(c₂ + γ²) + c₃·γ²·ln(n)/ln(100)
    /// </para>
    /// <para>
    /// Coefficients fitted by least-squares on 45 non-boundary cells (of 52 total)
    /// from the λ=0 kurtosis matching experiment (Test 2b). Grid search over c₂ ∈ [0.1, 5.0]
    /// with linear regression for c₀, c₁, c₃ at each c₂.
    /// </para>
    /// <para>
    /// R² ≈ 0.38, MaxErr ≈ 1.72 on fit cells. The modest R² reflects the delta-method ceiling:
    /// at large |γ| and large n, the SES link cannot achieve the target kurtosis because
    /// DLink(γ̂) → 0 as a grows, compressing σ_η and canceling the sinh curvature. These
    /// ceiling cells (7 of 52) are excluded from fitting.
    /// </para>
    /// <para>
    /// The formula is symmetric in |γ| by construction (uses γ²). The quantile-level
    /// asymmetry comes from ρ_σγ correlations already in V_η = G·Σ̂·G'.
    /// </para>
    /// <para>
    /// The production monotonicity constraint a ≥ |λ| + 0.10 still applies after this formula.
    /// Result is clamped to [0.5, 4.0] to match the production range.
    /// </para>
    /// </remarks>
    private static double SymmetricAFormula(double gammaHat, int sampleSize)
    {
        double g2 = gammaHat * gammaHat;
        double ln100 = Math.Log(100.0);

        // Formula 2 coefficients from Test 6 grid search
        // c2 = 5.0 hit the grid ceiling, meaning the saturating term g²/(c2+g²)
        // is nearly linear in g² at this range: g²/5 ≈ g²/(5+g²) for g² ≤ 2.25.
        // The large negative c1 offsets the large positive c3 n-term,
        // producing the correct net n-dependence at each |γ|.
        const double c0 = 1.2211;
        const double c1 = -20.03;
        const double c2 = 5.00;
        const double c3 = 3.778;

        double a = c0 + c1 * g2 / (c2 + g2) + c3 * g2 * Math.Log(sampleSize) / ln100;
        return Math.Max(0.5, Math.Min(4.0, a));
    }

    /// <summary>
    /// Runs a single B17C analysis on the given data, returning CI bounds at the specified AEPs.
    /// </summary>
    /// <param name="data">The sample data (real-space values).</param>
    /// <param name="aFormula">Optional AFormulaOverride. Null uses the production formula.</param>
    /// <param name="nominalCI">Nominal CI width (e.g. 0.90).</param>
    /// <param name="seed">PRNG seed for the MVN sampling.</param>
    /// <param name="aeps">AEP values to evaluate.</param>
    /// <returns>A [nAEP × 2] array of CI bounds, or null if analysis failed.</returns>
    private static async Task<double[,]?> RunSingleAnalysis(
        double[] data,
        Func<double, int, double>? aFormula,
        double nominalCI,
        int seed,
        double[] aeps)
    {
        try
        {
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(data);

            var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
            var analysis = new Bulletin17CAnalysis(model)
            {
                UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal,
                AFormulaOverride = aFormula
            };
            analysis.BayesianAnalysis.CredibleIntervalWidth = nominalCI;
            analysis.BayesianAnalysis.OutputLength = 5000;
            analysis.BayesianAnalysis.PRNGSeed = seed;

            // Set probability ordinates to match our AEPs
            analysis.ProbabilityOrdinates.Clear();
            foreach (double aep in aeps)
                analysis.ProbabilityOrdinates.Add(aep);

            await analysis.RunAsync();

            var CIs = analysis.AnalysisResults?.ConfidenceIntervals;
            if (CIs == null) return null;

            // Extract CI bounds for each AEP
            var result = new double[aeps.Length, 2];
            for (int j = 0; j < aeps.Length; j++)
            {
                result[j, 0] = CIs[j, 0];
                result[j, 1] = CIs[j, 1];
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Test 6: Formula Fitting

    /// <summary>
    /// Fits closed-form symmetric formulas a(|γ|, n) from the λ=0 kurtosis matching data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses λ=0 kurtosis matching (Test 2b) to isolate the pure a → kurtosis relationship,
    /// removing the λ confound discovered in Test 2. Cells where a_matched hits the ceiling
    /// (12.0) or floor (0.0) are excluded from fitting but included in the residual table
    /// for extrapolation assessment.
    /// </para>
    /// <para>
    /// Three candidate formulas are fit to the well-behaved cells:
    /// </para>
    /// <list type="number">
    /// <item><description>a = c₀ + c₁·γ²/(c₂ + γ²) — saturating, n-independent (3 params)</description></item>
    /// <item><description>a = c₀ + c₁·γ²/(c₂ + γ²) + c₃·γ²·log(n)/log(100) — with log(n) scaling (4 params)</description></item>
    /// <item><description>a = c₀ + c₁·|γ|^c₂ · (1 + c₃·log(n/25)) — power-log product form (4 params)</description></item>
    /// </list>
    /// <para>
    /// Formulas use |γ| (symmetric by construction). The fitted formula will be used in Test 5
    /// via <see cref="SymmetricAFormula"/> to evaluate coverage. Note that the production
    /// monotonicity constraint a ≥ |λ| + 0.10 still applies on top of this formula.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public void FormulaFitting_SymmetricA()
    {
        // Step 1: Compute λ=0 kurtosis matching for all 52 cells (reproduces Test 2b)
        var allCellData = new List<(double gamma, int n, double aMatched, double bootKurt, bool atBoundary)>();

        var matchCsv = new StringBuilder();
        matchCsv.AppendLine("gamma_true,n,a_matched_lam0,boot_kurt,at_boundary");

        for (int row = 0; row < 52; row++)
        {
            double gamma = PitReference[row, ColGamma];
            int n = (int)PitReference[row, ColN];
            double gammaHat = PitReference[row, ColMeanGammaHat];
            double bootSdGamma = PitReference[row, ColBootSdGamma];
            double bootKurtTarget = PitReference[row, ColBootKurtGamma];

            // λ=0: isolate pure a → kurtosis relationship
            double aMatched = FindAMatchingKurtosis(bootKurtTarget, gammaHat, bootSdGamma,
                lnRGamma: 0.0, seed: row * 1000 + 42);

            // Flag cells at boundaries (ceiling or floor)
            bool atBoundary = aMatched <= 0.001 || aMatched >= 11.99;

            allCellData.Add((gamma, n, aMatched, bootKurtTarget, atBoundary));
            matchCsv.AppendLine($"{gamma:F2},{n},{aMatched:F4},{bootKurtTarget:F4},{atBoundary}");
        }

        Debug.WriteLine("");
        Debug.WriteLine("========== A_MATCHED TABLE (LAMBDA=0) ==========");
        Debug.Write(matchCsv.ToString());

        // Step 2: Filter to well-behaved cells for fitting
        var fitData = allCellData
            .Where(c => !c.atBoundary)
            .Select(c => (c.gamma, c.n, c.aMatched, c.bootKurt))
            .ToList();

        Debug.WriteLine($"\nFitting on {fitData.Count} of {allCellData.Count} cells (excluded {allCellData.Count - fitData.Count} boundary cells)");

        // Step 3: Fit candidate formulas using grid search
        // All formulas use |γ| (symmetric by construction)

        // Formula 1: a = c0 + c1 * g2 / (c2 + g2)
        var (best1, r2_1, maxErr1) = FitFormula1(fitData);
        Debug.WriteLine($"\n--- Formula 1: a = {best1.c0:F4} + {best1.c1:F4} * g^2 / ({best1.c2:F4} + g^2) ---");
        Debug.WriteLine($"R² = {r2_1:F6}, MaxErr = {maxErr1:F4}");

        // Formula 2: a = c0 + c1 * g2 / (c2 + g2) + c3 * g2 * ln(n) / ln(100)
        var (best2, r2_2, maxErr2) = FitFormula2(fitData);
        Debug.WriteLine($"\n--- Formula 2: a = {best2.c0:F4} + {best2.c1:F4} * g^2 / ({best2.c2:F4} + g^2) + {best2.c3:F4} * g^2 * ln(n)/ln(100) ---");
        Debug.WriteLine($"R² = {r2_2:F6}, MaxErr = {maxErr2:F4}");

        // Formula 3: a = c0 + c1 * |g|^c2 * (1 + c3 * ln(n/25))
        var (best3, r2_3, maxErr3) = FitFormula3(fitData);
        Debug.WriteLine($"\n--- Formula 3: a = {best3.c0:F4} + {best3.c1:F4} * |g|^{best3.c2:F4} * (1 + {best3.c3:F4} * ln(n/25)) ---");
        Debug.WriteLine($"R² = {r2_3:F6}, MaxErr = {maxErr3:F4}");

        // Step 4: Residual table for ALL cells (including boundary cells for extrapolation check)
        var residualCsv = new StringBuilder();
        residualCsv.AppendLine("gamma_true,n,a_matched,at_boundary,a_f1,a_f2,a_f3,resid_f1,resid_f2,resid_f3");
        double ln100 = Math.Log(100);

        foreach (var (gamma, n, aMatched, bootKurt, atBoundary) in allCellData)
        {
            double g2 = gamma * gamma;
            double aF1 = best1.c0 + best1.c1 * g2 / (best1.c2 + g2);
            double aF2 = best2.c0 + best2.c1 * g2 / (best2.c2 + g2) + best2.c3 * g2 * Math.Log(n) / ln100;
            double absG = Math.Abs(gamma);
            double aF3 = absG > 1e-10
                ? best3.c0 + best3.c1 * Math.Pow(absG, best3.c2) * (1.0 + best3.c3 * Math.Log((double)n / 25.0))
                : best3.c0;
            residualCsv.AppendLine($"{gamma:F2},{n},{aMatched:F4},{atBoundary}," +
                $"{aF1:F4},{aF2:F4},{aF3:F4}," +
                $"{aMatched - aF1:F4},{aMatched - aF2:F4},{aMatched - aF3:F4}");
        }

        Debug.WriteLine("\n--- Residual Table (all cells, boundary flagged) ---");
        Debug.Write(residualCsv.ToString());
        Debug.WriteLine("========== END FORMULA FITTING ==========");
    }

    /// <summary>
    /// Fits Formula 1: a = c₀ + c₁·γ²/(c₂ + γ²) via grid search over c₂.
    /// For each c₂, c₀ and c₁ are fit by linear regression.
    /// </summary>
    private static ((double c0, double c1, double c2) best, double r2, double maxErr) FitFormula1(
        List<(double gamma, int n, double aMatched, double bootKurt)> data)
    {
        double bestSSR = double.MaxValue;
        (double c0, double c1, double c2) best = (0, 0, 1);

        // Grid search over c2
        for (double c2 = 0.1; c2 <= 5.0; c2 += 0.05)
        {
            // For this c2, compute x_i = g^2 / (c2 + g^2) for each cell
            // Then fit a = c0 + c1 * x via linear regression
            double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
            int count = data.Count;
            foreach (var (gamma, n, aMatched, _) in data)
            {
                double g2 = gamma * gamma;
                double x = g2 / (c2 + g2);
                sumX += x;
                sumY += aMatched;
                sumXX += x * x;
                sumXY += x * aMatched;
            }

            double meanX = sumX / count;
            double meanY = sumY / count;
            double c1Val = (sumXY / count - meanX * meanY) / (sumXX / count - meanX * meanX + 1e-15);
            double c0Val = meanY - c1Val * meanX;

            // Compute SSR
            double ssr = 0;
            foreach (var (gamma, n, aMatched, _) in data)
            {
                double g2 = gamma * gamma;
                double x = g2 / (c2 + g2);
                double pred = c0Val + c1Val * x;
                double resid = aMatched - pred;
                ssr += resid * resid;
            }

            if (ssr < bestSSR)
            {
                bestSSR = ssr;
                best = (c0Val, c1Val, c2);
            }
        }

        // Compute R² and max error
        double meanA = data.Average(d => d.aMatched);
        double ssTot = data.Sum(d => (d.aMatched - meanA) * (d.aMatched - meanA));
        double r2 = 1.0 - bestSSR / (ssTot + 1e-15);
        double maxErr = 0;
        foreach (var (gamma, n, aMatched, _) in data)
        {
            double g2 = gamma * gamma;
            double pred = best.c0 + best.c1 * g2 / (best.c2 + g2);
            maxErr = Math.Max(maxErr, Math.Abs(aMatched - pred));
        }

        return (best, r2, maxErr);
    }

    /// <summary>
    /// Fits Formula 2: a = c₀ + c₁·γ²/(c₂ + γ²) + c₃·γ²·ln(n)/ln(100) via grid search over c₂.
    /// For each c₂, the remaining parameters are fit by 2D linear regression.
    /// </summary>
    private static ((double c0, double c1, double c2, double c3) best, double r2, double maxErr) FitFormula2(
        List<(double gamma, int n, double aMatched, double bootKurt)> data)
    {
        double bestSSR = double.MaxValue;
        (double c0, double c1, double c2, double c3) best = (0, 0, 1, 0);
        double ln100 = Math.Log(100);

        for (double c2 = 0.1; c2 <= 5.0; c2 += 0.05)
        {
            // For this c2: a = c0 + c1*x1 + c3*x2 where x1 = g²/(c2+g²), x2 = g²*ln(n)/ln(100)
            // 3×3 linear regression: [1, x1, x2] → a
            int count = data.Count;
            var X = new double[count, 3];
            var Y = new double[count];
            for (int i = 0; i < count; i++)
            {
                double g2 = data[i].gamma * data[i].gamma;
                X[i, 0] = 1.0;
                X[i, 1] = g2 / (c2 + g2);
                X[i, 2] = g2 * Math.Log(data[i].n) / ln100;
                Y[i] = data[i].aMatched;
            }

            // Normal equations: (X'X)β = X'Y
            var xtx = new double[3, 3];
            var xty = new double[3];
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    xty[j] += X[i, j] * Y[i];
                    for (int k = 0; k < 3; k++)
                        xtx[j, k] += X[i, j] * X[i, k];
                }
            }

            // Solve 3×3 via Cramer's rule (small system)
            double[] beta = SolveLinearSystem3x3(xtx, xty);
            if (beta == null!) continue;

            double ssr = 0;
            for (int i = 0; i < count; i++)
            {
                double pred = beta[0] + beta[1] * X[i, 1] + beta[2] * X[i, 2];
                double resid = Y[i] - pred;
                ssr += resid * resid;
            }

            if (ssr < bestSSR)
            {
                bestSSR = ssr;
                best = (beta[0], beta[1], c2, beta[2]);
            }
        }

        double meanA = data.Average(d => d.aMatched);
        double ssTot = data.Sum(d => (d.aMatched - meanA) * (d.aMatched - meanA));
        double r2 = 1.0 - bestSSR / (ssTot + 1e-15);
        double maxErr = 0;
        foreach (var (gamma, n, aMatched, _) in data)
        {
            double g2 = gamma * gamma;
            double pred = best.c0 + best.c1 * g2 / (best.c2 + g2) + best.c3 * g2 * Math.Log(n) / ln100;
            maxErr = Math.Max(maxErr, Math.Abs(aMatched - pred));
        }

        return (best, r2, maxErr);
    }

    /// <summary>
    /// Fits Formula 3: a = c₀ + c₁·|γ|^c₂ · (1 + c₃·ln(n/25)) via 2D grid search over (c₂, c₃).
    /// For each (c₂, c₃), c₀ and c₁ are fit by linear regression.
    /// </summary>
    private static ((double c0, double c1, double c2, double c3) best, double r2, double maxErr) FitFormula3(
        List<(double gamma, int n, double aMatched, double bootKurt)> data)
    {
        double bestSSR = double.MaxValue;
        (double c0, double c1, double c2, double c3) best = (0, 0, 1, 0);

        for (double c2 = 0.5; c2 <= 3.0; c2 += 0.1)
        {
            for (double c3 = -0.2; c3 <= 0.5; c3 += 0.02)
            {
                // For this (c2, c3): a = c0 + c1 * x where x = |g|^c2 * (1 + c3*ln(n/25))
                // Linear regression on x
                double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
                int count = 0;
                foreach (var (gamma, n, aMatched, _) in data)
                {
                    double absG = Math.Abs(gamma);
                    double x = absG > 1e-10
                        ? Math.Pow(absG, c2) * (1.0 + c3 * Math.Log((double)n / 25.0))
                        : 0.0;
                    sumX += x;
                    sumY += aMatched;
                    sumXX += x * x;
                    sumXY += x * aMatched;
                    count++;
                }

                double meanX = sumX / count;
                double meanY = sumY / count;
                double denom = sumXX / count - meanX * meanX;
                if (Math.Abs(denom) < 1e-15) continue;

                double c1Val = (sumXY / count - meanX * meanY) / denom;
                double c0Val = meanY - c1Val * meanX;

                double ssr = 0;
                foreach (var (gamma, n, aMatched, _) in data)
                {
                    double absG = Math.Abs(gamma);
                    double x = absG > 1e-10
                        ? Math.Pow(absG, c2) * (1.0 + c3 * Math.Log((double)n / 25.0))
                        : 0.0;
                    double pred = c0Val + c1Val * x;
                    double resid = aMatched - pred;
                    ssr += resid * resid;
                }

                if (ssr < bestSSR)
                {
                    bestSSR = ssr;
                    best = (c0Val, c1Val, c2, c3);
                }
            }
        }

        double meanA = data.Average(d => d.aMatched);
        double ssTot = data.Sum(d => (d.aMatched - meanA) * (d.aMatched - meanA));
        double r2 = 1.0 - bestSSR / (ssTot + 1e-15);
        double maxErr = 0;
        foreach (var (gamma, n, aMatched, _) in data)
        {
            double absG = Math.Abs(gamma);
            double pred = absG > 1e-10
                ? best.c0 + best.c1 * Math.Pow(absG, best.c2) * (1.0 + best.c3 * Math.Log((double)n / 25.0))
                : best.c0;
            maxErr = Math.Max(maxErr, Math.Abs(aMatched - pred));
        }

        return (best, r2, maxErr);
    }

    /// <summary>
    /// Solves a 3×3 linear system Ax = b using Gaussian elimination.
    /// </summary>
    private static double[] SolveLinearSystem3x3(double[,] A, double[] b)
    {
        // Augmented matrix
        var aug = new double[3, 4];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
                aug[i, j] = A[i, j];
            aug[i, 3] = b[i];
        }

        // Forward elimination with partial pivoting
        for (int col = 0; col < 3; col++)
        {
            // Find pivot
            int maxRow = col;
            for (int row = col + 1; row < 3; row++)
            {
                if (Math.Abs(aug[row, col]) > Math.Abs(aug[maxRow, col]))
                    maxRow = row;
            }

            // Swap rows
            if (maxRow != col)
            {
                for (int j = 0; j < 4; j++)
                    (aug[col, j], aug[maxRow, j]) = (aug[maxRow, j], aug[col, j]);
            }

            if (Math.Abs(aug[col, col]) < 1e-15) return null!;

            // Eliminate
            for (int row = col + 1; row < 3; row++)
            {
                double factor = aug[row, col] / aug[col, col];
                for (int j = col; j < 4; j++)
                    aug[row, j] -= factor * aug[col, j];
            }
        }

        // Back substitution
        var x = new double[3];
        for (int i = 2; i >= 0; i--)
        {
            x[i] = aug[i, 3];
            for (int j = i + 1; j < 3; j++)
                x[i] -= aug[i, j] * x[j];
            x[i] /= aug[i, i];
        }

        return x;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Computes the analytical kurtosis of X = (1/a)·sinh(a·η) where η ~ N(0,1).
    /// </summary>
    /// <param name="a">SES curvature parameter. a=0 gives kurtosis=3 (Gaussian identity).</param>
    /// <returns>The kurtosis (not excess kurtosis) of the SES-transformed standard normal.</returns>
    /// <remarks>
    /// <para>
    /// Uses the moment generating function identity E[exp(cη)] = exp(c²/2) for η ~ N(0,1).
    /// </para>
    /// <para>
    /// sinh(aη) = (exp(aη) - exp(-aη))/2, so:
    ///   sinh²(aη) = (exp(2aη) - 2 + exp(-2aη))/4 = (cosh(2aη) - 1)/2
    ///   sinh⁴(aη) = (3 - 4·cosh(2aη) + cosh(4aη))/8
    /// </para>
    /// <para>
    /// Taking expectations:
    ///   E[sinh²(aη)] = (exp(2a²) - 1)/2
    ///   E[sinh⁴(aη)] = (3 - 4·exp(2a²) + exp(8a²))/8
    /// </para>
    /// <para>
    /// Therefore:
    ///   E[X²] = E[sinh²(aη)]/a² = (exp(2a²) - 1)/(2a²)
    ///   E[X⁴] = E[sinh⁴(aη)]/a⁴ = (3 - 4·exp(2a²) + exp(8a²))/(8a⁴)
    ///   Kurt = E[X⁴]/(E[X²])²
    /// </para>
    /// </remarks>
    private static double ComputeSESKurtosisAnalytical(double a)
    {
        if (Math.Abs(a) < 1e-10)
            return 3.0; // Identity link → Gaussian

        double a2 = a * a;
        double exp2a2 = Math.Exp(2.0 * a2);
        double exp8a2 = Math.Exp(8.0 * a2);

        // Guard against overflow for very large a
        if (double.IsInfinity(exp8a2))
            return double.PositiveInfinity;

        double EX2 = (exp2a2 - 1.0) / (2.0 * a2);
        double EX4 = (3.0 - 4.0 * exp2a2 + exp8a2) / (8.0 * a2 * a2);

        return EX4 / (EX2 * EX2);
    }

    /// <summary>
    /// Computes the kurtosis of γ* = SES⁻¹(η) where η ~ N(η̂, σ²_η) via Monte Carlo.
    /// </summary>
    /// <param name="a">SES curvature parameter.</param>
    /// <param name="lambda">SES asymmetry parameter.</param>
    /// <param name="sigmaEta">Standard deviation in link space.</param>
    /// <param name="etaHat">Mean in link space.</param>
    /// <param name="nSamples">Number of Monte Carlo draws.</param>
    /// <param name="seed">Random seed.</param>
    /// <returns>The kurtosis of the back-transformed draws.</returns>
    private static double ComputeSESKurtosisMC(double a, double lambda, double sigmaEta,
                                                double etaHat, int nSamples, int seed)
    {
        var link = new SESLink(a: Math.Max(a, 1e-12)) { Lambda = lambda, UseAdaptiveLambda = false };
        var rng = new MersenneTwister(seed);
        var normal = new Normal(etaHat, sigmaEta);

        var gammaValues = new double[nSamples];
        int validCount = 0;

        for (int i = 0; i < nSamples; i++)
        {
            double eta = normal.InverseCDF(rng.NextDouble());
            double gamma = link.InverseLink(eta);
            if (Tools.IsFinite(gamma))
            {
                gammaValues[validCount++] = gamma;
            }
        }

        if (validCount < 100)
            return double.NaN;

        return Kurtosis(gammaValues, validCount);
    }

    /// <summary>
    /// Computes kurtosis of the SES link output for a specific cell configuration.
    /// Sets up the SES link with the given 'a', derives λ and σ_η from the cell's data,
    /// and runs Monte Carlo.
    /// </summary>
    /// <param name="a">SES curvature parameter to test.</param>
    /// <param name="gammaHat">Point estimate of γ.</param>
    /// <param name="bootSdGamma">Bootstrap standard deviation of γ̂.</param>
    /// <param name="lnRGamma">Target ln(R_gamma) for λ derivation.</param>
    /// <param name="seed">Random seed.</param>
    /// <returns>The kurtosis of back-transformed draws at this cell's configuration.</returns>
    private static double ComputeSESKurtosisAtCell(double a, double gammaHat, double bootSdGamma,
                                                    double lnRGamma, int seed)
    {
        // Create SES link with candidate a
        var link = new SESLink(a: Math.Max(a, 1e-12)) { UseAdaptiveLambda = false };

        // Compute σ_η from bootstrap SD and link Jacobian
        double dLinkAtGammaHat = link.DLink(gammaHat);
        double sigmaEta = bootSdGamma * Math.Abs(dLinkAtGammaHat);
        if (sigmaEta < 1e-15) sigmaEta = 1e-15;

        // Compute η̂
        double etaHat = link.Link(gammaHat);

        // Compute λ from R_gamma formula
        double deltaGamma = 1.645 * sigmaEta;
        double lambda = deltaGamma > 1e-10
            ? lnRGamma / (2.0 * deltaGamma)
            : 0.0;
        lambda = Math.Max(-0.95, Math.Min(0.95, lambda));

        // Set λ on the link
        link.Lambda = lambda;

        // Recompute η̂ and σ_η with the final λ (DLink depends on λ through EffectiveLambda,
        // but since UseAdaptiveLambda=false, setting Lambda directly works)
        dLinkAtGammaHat = link.DLink(gammaHat);
        sigmaEta = bootSdGamma * Math.Abs(dLinkAtGammaHat);
        etaHat = link.Link(gammaHat);

        return ComputeSESKurtosisMC(a, lambda, sigmaEta, etaHat, KurtosisMCSamples, seed);
    }

    /// <summary>
    /// Binary search for the SES 'a' parameter that matches a target kurtosis
    /// at a specific cell's (γ̂, σ_η) configuration.
    /// </summary>
    /// <param name="targetKurtosis">Target kurtosis to match.</param>
    /// <param name="gammaHat">Point estimate of γ.</param>
    /// <param name="bootSdGamma">Bootstrap standard deviation of γ̂.</param>
    /// <param name="lnRGamma">Target ln(R_gamma) for λ derivation.</param>
    /// <param name="seed">Random seed.</param>
    /// <returns>The SES 'a' value that produces the target kurtosis, or NaN if not found.</returns>
    private static double FindAMatchingKurtosis(double targetKurtosis, double gammaHat,
                                                 double bootSdGamma, double lnRGamma, int seed)
    {
        double aLo = 0.0;
        double aHi = 12.0;
        double tolerance = 0.02;
        int maxIter = 40;

        // Evaluate at endpoints
        double kurtLo = ComputeSESKurtosisAtCell(aLo, gammaHat, bootSdGamma, lnRGamma, seed);
        double kurtHi = ComputeSESKurtosisAtCell(aHi, gammaHat, bootSdGamma, lnRGamma, seed);

        // Target below our minimum — return 0 (Gaussian)
        if (targetKurtosis <= kurtLo)
            return aLo;

        // Target above our maximum — return max
        if (targetKurtosis >= kurtHi)
            return aHi;

        for (int iter = 0; iter < maxIter; iter++)
        {
            double aMid = (aLo + aHi) / 2.0;
            double kurtMid = ComputeSESKurtosisAtCell(aMid, gammaHat, bootSdGamma, lnRGamma, seed);

            if (Math.Abs(kurtMid - targetKurtosis) < tolerance)
                return aMid;

            if (kurtMid < targetKurtosis)
                aLo = aMid;
            else
                aHi = aMid;

            if (aHi - aLo < 0.001)
                return (aLo + aHi) / 2.0;
        }

        return (aLo + aHi) / 2.0;
    }

    /// <summary>
    /// Computes the kurtosis (not excess kurtosis) of the first <paramref name="count"/> elements.
    /// </summary>
    /// <param name="values">Data array.</param>
    /// <param name="count">Number of elements to use.</param>
    /// <returns>Kurtosis = E[(x-μ)⁴] / (E[(x-μ)²])². Gaussian = 3.0.</returns>
    private static double Kurtosis(double[] values, int count)
    {
        if (count < 4) return double.NaN;

        double mean = 0;
        for (int i = 0; i < count; i++)
            mean += values[i];
        mean /= count;

        double m2 = 0, m4 = 0;
        for (int i = 0; i < count; i++)
        {
            double d = values[i] - mean;
            double d2 = d * d;
            m2 += d2;
            m4 += d2 * d2;
        }
        m2 /= count;
        m4 /= count;

        if (m2 < 1e-30) return double.NaN;
        return m4 / (m2 * m2);
    }

    #endregion
}
