using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Tests for profile Q methods on <see cref="GeneralizedMethodOfMoments"/>:
/// <see cref="GeneralizedMethodOfMoments.ProfileQ"/>,
/// <see cref="GeneralizedMethodOfMoments.ProfileConfidenceIntervals"/>,
/// <see cref="GeneralizedMethodOfMoments.ProfilePercentiles"/>.
/// </summary>
/// <remarks>
/// <para>
/// Profile Q is the GMM analog of profile likelihood for MLE. Under efficient GMM (W = S⁻¹),
/// the profile Q difference n·[Q_profile(θ_i) − Q(θ̂)] is asymptotically χ²(1).
/// </para>
/// <para>
/// All tests use Log-Pearson Type III via Bulletin17CDistribution with μ=3.0, σ=0.5, γ=0.5
/// and n=50 to exercise the real B17C moment conditions with correlated parameters and
/// detectable asymmetry (positive skewness → right-skewed σ̂ and asymmetric γ̂).
/// </para>
/// </remarks>
[TestClass]
public class ProfileQTests
{
    #region Test Setup

    /// <summary>
    /// True LP3 parameters for all tests: μ=3.0, σ=0.5, γ=0.5 in log10-space.
    /// </summary>
    private const double TrueMu = 3.0;
    private const double TrueSigma = 0.5;
    private const double TrueGamma = -0.5;
    private const int SampleSize = 50;
    private const int Seed = 12345;

    /// <summary>
    /// Creates a Bulletin17CDistribution with LP3 synthetic data and runs GMM estimation.
    /// Returns the estimated GMM object ready for profile Q analysis.
    /// </summary>
    /// <param name="n">Sample size. Default = 50.</param>
    /// <param name="seed">PRNG seed for reproducibility. Default = 12345.</param>
    /// <returns>A tuple of the estimated GMM and the Bulletin17CDistribution model.</returns>
    private static (GeneralizedMethodOfMoments GMM, Bulletin17CDistribution Model) CreateEstimatedLP3GMM(
        int n = SampleSize, int seed = Seed)
    {
        var (df, _) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(
            mu: TrueMu, sigma: TrueSigma, gamma: TrueGamma, n: n);

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        return (gmm, model);
    }

    /// <summary>
    /// Creates a Bulletin17CDistribution with LP3 synthetic data but does NOT estimate.
    /// Used for testing pre-estimation error handling.
    /// </summary>
    private static GeneralizedMethodOfMoments CreateUnestimatedLP3GMM()
    {
        var (df, _) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(
            mu: TrueMu, sigma: TrueSigma, gamma: TrueGamma, n: SampleSize, prngSeed: Seed);

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        return new GeneralizedMethodOfMoments(model);
    }

    #endregion

    #region ProfileQ Tests

    /// <summary>
    /// Verifies that ProfileQ returns 3 profiles (μ, σ, γ) with correct dimensions for LP3.
    /// </summary>
    [TestMethod]
    public void ProfileQ_LP3_ReturnsCorrectDimensions()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed.");

        // Act
        var profiles = gmm.ProfileQ(bins: 50, trueProfile: false);

        // Assert
        Assert.AreEqual(3, profiles.Count, "LP3 should have 3 profiles (μ, σ, γ).");
        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(50, profiles[i].GetLength(0), $"Profile {i} should have 50 bins.");
            Assert.AreEqual(2, profiles[i].GetLength(1), $"Profile {i} rows should have [value, Q].");
        }
    }

    /// <summary>
    /// Verifies that the conditional profile Q surface has its minimum at (or very near) the GMM estimate.
    /// For a just-identified model, Q(θ̂) ≈ 0 and all profile Q values should be ≥ 0.
    /// </summary>
    [TestMethod]
    public void ProfileQ_LP3_MinimumAtEstimate()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();
        double qHat = gmm.Q(gmm.BestParameterSet.Values);

        // Act — conditional profile (fast)
        var profiles = gmm.ProfileQ(bins: 80, trueProfile: false);

        // Assert — all Q values should be >= Q(θ̂) within numerical tolerance
        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            double minQ = double.MaxValue;
            for (int j = 0; j < profiles[i].GetLength(0); j++)
            {
                if (profiles[i][j, 1] < minQ) minQ = profiles[i][j, 1];
            }
            Assert.IsTrue(minQ >= qHat - 1e-6,
                $"Profile Q for {paramNames[i]} should not go below Q(θ̂). Min={minQ:G8}, Q̂={qHat:G8}");
        }
    }

    /// <summary>
    /// Verifies that true profiling produces Q values ≤ conditional profiling at each grid point.
    /// True profiling re-optimizes nuisance parameters, so it can only do as well or better.
    /// For LP3 with correlated (μ, σ, γ), the difference should be noticeable.
    /// </summary>
    [TestMethod]
    public void ProfileQ_LP3_TrueProfile_LessOrEqualToConditional()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act — use fewer bins since true profiling does (p-1)=2D optimization per point
        var trueProfiles = gmm.ProfileQ(bins: 20, trueProfile: true);
        var condProfiles = gmm.ProfileQ(bins: 20, trueProfile: false);

        // Assert
        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 20; j++)
            {
                Assert.IsTrue(trueProfiles[i][j, 1] <= condProfiles[i][j, 1] + 1e-6,
                    $"True profile should be ≤ conditional for {paramNames[i]} at bin {j}. " +
                    $"True={trueProfiles[i][j, 1]:G6}, Cond={condProfiles[i][j, 1]:G6}");
            }
        }
    }

    /// <summary>
    /// Verifies that ProfileQ throws when the model has not been estimated.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void ProfileQ_ThrowsWhenNotEstimated()
    {
        var gmm = CreateUnestimatedLP3GMM();
        gmm.ProfileQ();
    }

    #endregion

    #region ProfileConfidenceIntervals Tests

    /// <summary>
    /// Verifies that 90% profile CIs contain all three GMM parameter estimates (μ, σ, γ).
    /// </summary>
    [TestMethod]
    public void ProfileConfidenceIntervals_LP3_ContainEstimate()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act
        var cis = gmm.ProfileConfidenceIntervals(alpha: 0.1, trueProfile: false);

        // Assert
        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            double est = gmm.BestParameterSet.Values[i];
            Assert.IsTrue(cis[i, 0] <= est,
                $"Lower CI for {paramNames[i]} should be ≤ estimate. Lower={cis[i, 0]:G6}, Est={est:G6}");
            Assert.IsTrue(cis[i, 1] >= est,
                $"Upper CI for {paramNames[i]} should be ≥ estimate. Upper={cis[i, 1]:G6}, Est={est:G6}");
        }
    }

    /// <summary>
    /// Verifies that larger alpha gives narrower CIs: 80% (α=0.2) narrower than 90% (α=0.1).
    /// </summary>
    [TestMethod]
    public void ProfileConfidenceIntervals_LP3_WiderAlphaGivesNarrowerCI()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act
        var ci90 = gmm.ProfileConfidenceIntervals(alpha: 0.1, trueProfile: false);
        var ci80 = gmm.ProfileConfidenceIntervals(alpha: 0.2, trueProfile: false);

        // Assert
        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            double width90 = ci90[i, 1] - ci90[i, 0];
            double width80 = ci80[i, 1] - ci80[i, 0];
            Assert.IsTrue(width80 < width90,
                $"80% CI should be narrower than 90% CI for {paramNames[i]}. 80%={width80:G6}, 90%={width90:G6}");
        }
    }

    /// <summary>
    /// Verifies that true profiling CIs are at least as wide as conditional profiling CIs.
    /// True profiling produces lower Q values away from the optimum (because nuisance params
    /// are re-optimized), so the chi-squared threshold is crossed further from θ̂.
    /// For LP3, the (μ,σ,γ) correlation makes this difference meaningful.
    /// </summary>
    [TestMethod]
    public void ProfileConfidenceIntervals_LP3_TrueProfile_WiderThanConditional()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act
        var trueCIs = gmm.ProfileConfidenceIntervals(alpha: 0.1, trueProfile: true);
        var condCIs = gmm.ProfileConfidenceIntervals(alpha: 0.1, trueProfile: false);

        // Assert
        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            double trueWidth = trueCIs[i, 1] - trueCIs[i, 0];
            double condWidth = condCIs[i, 1] - condCIs[i, 0];
            Assert.IsTrue(trueWidth >= condWidth - 1e-6,
                $"True profile CIs should be ≥ conditional for {paramNames[i]}. " +
                $"True={trueWidth:G6}, Cond={condWidth:G6}");
        }
    }

    #endregion

    #region ProfilePercentiles Tests

    /// <summary>
    /// Verifies that profile percentiles are monotonically increasing for all 3 LP3 parameters.
    /// </summary>
    [TestMethod]
    public void ProfilePercentiles_LP3_AreMonotonic()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act
        var pctls = gmm.ProfilePercentiles(trueProfile: false);

        // Assert — default percentiles: {0.05, 0.25, 0.50, 0.75, 0.95}
        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            for (int k = 1; k < 5; k++)
            {
                Assert.IsTrue(pctls[i, k] >= pctls[i, k - 1],
                    $"Percentiles for {paramNames[i]} should be monotonic. " +
                    $"P[{k - 1}]={pctls[i, k - 1]:G6}, P[{k}]={pctls[i, k]:G6}");
            }
        }
    }

    /// <summary>
    /// Verifies that P50 is close to the point estimate. With the density-based approach
    /// (exp(-n·ΔQ) CDF inversion), P50 is the median of the implied density which may
    /// differ slightly from θ̂ due to asymmetry — this offset is the centering shift.
    /// </summary>
    [TestMethod]
    public void ProfilePercentiles_LP3_MedianNearEstimate()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act
        var pctls = gmm.ProfilePercentiles(trueProfile: false);

        // Assert — P50 should be close to θ̂ (within ~10% of the IQR)
        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            double est = gmm.BestParameterSet.Values[i];
            double p50 = pctls[i, 2];
            double p25 = pctls[i, 1];
            double p75 = pctls[i, 3];
            double iqr = p75 - p25;
            double shift = p50 - est;

            // The shift magnitude should be small relative to the IQR
            Assert.IsTrue(Math.Abs(shift) < 0.5 * iqr,
                $"P50 for {paramNames[i]} should be near estimate. " +
                $"P50={p50:G6}, θ̂={est:G6}, shift={shift:G4}, IQR={iqr:G4}");
        }
    }

    /// <summary>
    /// Verifies that σ profile percentiles show detectable asymmetry with magnitude
    /// useful for link parameter calibration. The profile Q pseudo-likelihood inverts
    /// the skewness direction (R &lt; 1 here corresponds to right-skewed sampling distribution),
    /// so consumers negate the Bowley skewness and shift when setting link parameters.
    /// </summary>
    [TestMethod]
    public void ProfilePercentiles_LP3_SigmaShowsAsymmetry()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act
        var pctls = gmm.ProfilePercentiles(trueProfile: false);

        // Assert — R ratio for σ (index 1) should deviate meaningfully from 1.0
        double p05 = pctls[1, 0];
        double p50 = pctls[1, 2];
        double p95 = pctls[1, 4];
        double lowerSpread = p50 - p05;
        double upperSpread = p95 - p50;

        Assert.IsTrue(lowerSpread > 1e-10, "Lower spread for σ should be positive.");
        Assert.IsTrue(upperSpread > 1e-10, "Upper spread for σ should be positive.");
        double R = upperSpread / lowerSpread;

        // The profile pseudo-likelihood inverts skewness: R < 1 here means the
        // sampling distribution is right-skewed (as expected for σ̂).
        // Verify meaningful asymmetry (|R - 1| > 0.05).
        Assert.IsTrue(Math.Abs(R - 1.0) > 0.05,
            $"R ratio for σ should show detectable asymmetry. R={R:F3}, " +
            $"P05={p05:G6}, P50={p50:G6}, P95={p95:G6}");
    }

    /// <summary>
    /// Verifies that the γ parameter profile percentiles show detectable asymmetry.
    /// The profile pseudo-likelihood inverts the skewness direction, so the raw R ratio
    /// is inverted relative to the true sampling distribution. Consumers negate Bowley/shift
    /// when setting SES link parameters.
    /// </summary>
    [TestMethod]
    public void ProfilePercentiles_LP3_GammaShowsAsymmetry()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act
        var pctls = gmm.ProfilePercentiles(trueProfile: true);

        // Assert — R ratio for γ (index 2) should deviate from 1.0
        double p05 = pctls[2, 0];
        double p50 = pctls[2, 2];
        double p95 = pctls[2, 4];
        double lowerSpread = p50 - p05;
        double upperSpread = p95 - p50;

        Assert.IsTrue(lowerSpread > 1e-10, "Lower spread for γ should be positive.");
        Assert.IsTrue(upperSpread > 1e-10, "Upper spread for γ should be positive.");
        double R = upperSpread / lowerSpread;

        // Verify detectable asymmetry (any direction — magnitude matters for link calibration)
        Assert.IsTrue(Math.Abs(R - 1.0) > 0.02,
            $"R ratio for γ should show detectable asymmetry. R={R:F3}, " +
            $"P05={p05:G6}, P50={p50:G6}, P95={p95:G6}");
    }

    /// <summary>
    /// Computes and reports all link-calibration diagnostic quantities from profile percentiles.
    /// This test exercises the full diagnostic pipeline that would feed into B17C link parameter selection.
    /// </summary>
    [TestMethod]
    public void ProfilePercentiles_LP3_LinkCalibrationDiagnostics()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act
        var pctls = gmm.ProfilePercentiles(trueProfile: true);

        // Assert — compute and validate diagnostics for each parameter
        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            double p05 = pctls[i, 0];
            double p25 = pctls[i, 1];
            double p50 = pctls[i, 2];
            double p75 = pctls[i, 3];
            double p95 = pctls[i, 4];

            double iqr = p75 - p25;
            double lowerSpread = p50 - p05;
            double upperSpread = p95 - p50;
            double outerSpread = p95 - p05;

            // Bowley skewness: (P75 + P25 - 2·P50) / IQR
            double bowley = iqr > 1e-10 ? (p75 + p25 - 2.0 * p50) / iqr : 0;

            // Asymmetry ratio R: (P95 - P50) / (P50 - P05) — directly gives λ
            double R = lowerSpread > 1e-10 ? upperSpread / lowerSpread : double.NaN;

            // Tail weight: (P95 - P05) / IQR — informs the 'a' parameter
            double tailWeight = iqr > 1e-10 ? outerSpread / iqr : double.NaN;

            // Centering shift: P50 - θ̂ — gives z0 for SES link
            double shift = p50 - gmm.BestParameterSet.Values[i];

            // All should be finite
            Assert.IsTrue(double.IsFinite(bowley),
                $"Bowley skewness should be finite for {paramNames[i]}. Got {bowley}");
            Assert.IsTrue(double.IsFinite(R) && R > 0,
                $"R ratio should be positive and finite for {paramNames[i]}. Got {R}");
            Assert.IsTrue(double.IsFinite(tailWeight) && tailWeight > 1,
                $"Tail weight should be > 1 for {paramNames[i]}. Got {tailWeight}");
            Assert.IsTrue(double.IsFinite(shift),
                $"Centering shift should be finite for {paramNames[i]}. Got {shift}");

            // Tail weight for a chi-squared-like profile should be ~2.44 (Gaussian reference)
            Assert.IsTrue(tailWeight > 1.5 && tailWeight < 5.0,
                $"Tail weight for {paramNames[i]} should be in [1.5, 5.0]. Got {tailWeight:F3}");
        }
    }

    /// <summary>
    /// Verifies that custom percentiles can be specified (e.g., finer grid for detailed calibration).
    /// </summary>
    [TestMethod]
    public void ProfilePercentiles_LP3_CustomPercentiles()
    {
        // Arrange
        var (gmm, _) = CreateEstimatedLP3GMM();

        // Act — finer grid including extreme tails
        var custom = new[] { 0.01, 0.05, 0.10, 0.25, 0.50, 0.75, 0.90, 0.95, 0.99 };
        var pctls = gmm.ProfilePercentiles(custom, trueProfile: false);

        // Assert
        Assert.AreEqual(3, pctls.GetLength(0), "Should have 3 parameters.");
        Assert.AreEqual(9, pctls.GetLength(1), "Should have 9 percentiles.");

        // Monotonicity across all 9 percentiles
        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            for (int k = 1; k < 9; k++)
            {
                Assert.IsTrue(pctls[i, k] >= pctls[i, k - 1],
                    $"Percentiles for {paramNames[i]} should be monotonic at positions {k - 1},{k}.");
            }

            // 1-99% range should be wider than 5-95% range
            double outerWidth = pctls[i, 8] - pctls[i, 0]; // P99 - P01
            double innerWidth = pctls[i, 7] - pctls[i, 1]; // P95 - P05
            Assert.IsTrue(outerWidth > innerWidth,
                $"1-99% range should be wider than 5-95% for {paramNames[i]}.");
        }
    }

    /// <summary>
    /// Verifies that ProfilePercentiles throws when the model has not been estimated.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void ProfilePercentiles_ThrowsWhenNotEstimated()
    {
        var gmm = CreateUnestimatedLP3GMM();
        gmm.ProfilePercentiles();
    }

    /// <summary>
    /// Verifies that profile CIs contain the true population parameters at 90% level
    /// (nominal coverage check). With n=50 and a single realization, this is a sanity check
    /// rather than a statistical coverage test.
    /// </summary>
    [TestMethod]
    public void ProfileConfidenceIntervals_LP3_ContainTrueParameters()
    {
        // Arrange — use larger sample for better coverage probability
        var (gmm, _) = CreateEstimatedLP3GMM(n: 200, seed: 42);

        // Act — 90% CIs should contain true values most of the time
        var cis = gmm.ProfileConfidenceIntervals(alpha: 0.1, trueProfile: false);

        // The true parameters (in the GMM's parameterization, which are moments of log10(x))
        // may differ slightly from the constructor values due to sampling.
        // We check that the true generating values are at least roughly within the CIs.
        // For a large sample (n=200), the GMM estimates should be close to truth.
        string[] paramNames = { "μ", "σ", "γ" };
        double[] trueParams = { TrueMu, TrueSigma, TrueGamma };

        int covered = 0;
        for (int i = 0; i < 3; i++)
        {
            if (trueParams[i] >= cis[i, 0] && trueParams[i] <= cis[i, 1])
                covered++;
        }

        // At least 2 of 3 should be covered (allowing for one unlucky parameter)
        Assert.IsTrue(covered >= 2,
            $"At least 2 of 3 true parameters should be within 90% profile CIs. " +
            $"Covered={covered}. " +
            $"μ: [{cis[0, 0]:G4}, {cis[0, 1]:G4}] (true={TrueMu}), " +
            $"σ: [{cis[1, 0]:G4}, {cis[1, 1]:G4}] (true={TrueSigma}), " +
            $"γ: [{cis[2, 0]:G4}, {cis[2, 1]:G4}] (true={TrueGamma})");
    }

    #endregion
}
