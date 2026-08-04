namespace RMC.BestFit.Verification.Univariate.CompetingRiskTests;

/// <summary>
/// Recovers the combined distribution of synthetic competing-risk populations by
/// maximum likelihood and Bayesian analysis.
/// </summary>
/// <remarks>
/// Each public method is intentionally independent so it can be authorized, run, and
/// reported through the guarded exact-method runner. The synthetic populations reproduce
/// the pinned Numerics recovery fixtures and add two fixed Gaussian-copula correlation cases.
/// Bayesian methods leave every <see cref="RMC.BestFit.Estimation.BayesianAnalysis"/> option
/// at the production DEMCzs default and assert that resolved default contract before sampling.
/// </remarks>
[TestClass]
public partial class CompetingRiskRecoveryTests
{
    /// <summary>
    /// Verifies MLE recovery for the minimum of Weibull(50, 1) and Weibull(80, 3).
    /// </summary>
    [TestMethod]
    public void MLE_Minimum_TwoWeibullConstantIncreasing_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMinimumTwoWeibullConstantIncreasing());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for the minimum of Weibull(50, 1)
    /// and Weibull(80, 3).
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Minimum_TwoWeibullConstantIncreasing_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMinimumTwoWeibullConstantIncreasing());
    }

    /// <summary>
    /// Verifies MLE recovery for the minimum of Weibull(30, 0.8) and Weibull(100, 3).
    /// </summary>
    [TestMethod]
    public void MLE_Minimum_TwoWeibullContrastingShapes_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMinimumTwoWeibullContrastingShapes());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for the minimum of Weibull(30, 0.8)
    /// and Weibull(100, 3).
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Minimum_TwoWeibullContrastingShapes_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMinimumTwoWeibullContrastingShapes());
    }

    /// <summary>
    /// Verifies MLE recovery for a three-Weibull minimum representing a bathtub hazard.
    /// </summary>
    [TestMethod]
    public void MLE_Minimum_ThreeWeibullBathtub_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMinimumThreeWeibullBathtub());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for a three-Weibull minimum representing
    /// a bathtub hazard.
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Minimum_ThreeWeibullBathtub_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMinimumThreeWeibullBathtub());
    }

    /// <summary>
    /// Verifies MLE recovery for the minimum of three Weibulls with strongly separated shapes.
    /// </summary>
    [TestMethod]
    public void MLE_Minimum_ThreeWeibullSeparatedShapes_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMinimumThreeWeibullSeparatedShapes());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for the minimum of three Weibulls with
    /// strongly separated shapes.
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Minimum_ThreeWeibullSeparatedShapes_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMinimumThreeWeibullSeparatedShapes());
    }

    /// <summary>
    /// Verifies MLE recovery for the maximum of Normal(50, 8) and Normal(85, 12).
    /// </summary>
    [TestMethod]
    public void MLE_Maximum_TwoSeparatedNormals_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMaximumTwoSeparatedNormals());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for the maximum of Normal(50, 8)
    /// and Normal(85, 12).
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Maximum_TwoSeparatedNormals_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMaximumTwoSeparatedNormals());
    }

    /// <summary>
    /// Verifies MLE recovery for the maximum of Weibull(50, 2) and Gumbel(70, 15).
    /// </summary>
    [TestMethod]
    public void MLE_Maximum_WeibullAndGumbel_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMaximumWeibullAndGumbel());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for the maximum of Weibull(50, 2)
    /// and Gumbel(70, 15).
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Maximum_WeibullAndGumbel_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMaximumWeibullAndGumbel());
    }

    /// <summary>
    /// Verifies MLE recovery for the maximum of three separated Normal distributions.
    /// </summary>
    [TestMethod]
    public void MLE_Maximum_ThreeSeparatedNormals_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMaximumThreeSeparatedNormals());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for the maximum of three separated
    /// Normal distributions.
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Maximum_ThreeSeparatedNormals_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMaximumThreeSeparatedNormals());
    }

    /// <summary>
    /// Verifies MLE recovery for the maximum of Exponential, Gamma, and natural-base
    /// LogNormal components.
    /// </summary>
    [TestMethod]
    public void MLE_Maximum_ThreeDifferentFamilies_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMaximumThreeDifferentFamilies());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for the maximum of Exponential, Gamma,
    /// and natural-base LogNormal components.
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Maximum_ThreeDifferentFamilies_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMaximumThreeDifferentFamilies());
    }

    /// <summary>
    /// Verifies MLE recovery for the correlated minimum of Weibull(50, 1) and
    /// Weibull(80, 3) with latent correlation 0.6.
    /// </summary>
    [TestMethod]
    public void MLE_Minimum_CorrelatedTwoWeibulls_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMinimumCorrelatedTwoWeibulls());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for the correlated minimum of
    /// Weibull(50, 1) and Weibull(80, 3) with latent correlation 0.6.
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Minimum_CorrelatedTwoWeibulls_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMinimumCorrelatedTwoWeibulls());
    }

    /// <summary>
    /// Verifies MLE recovery for the correlated maximum of Normal(50, 10) and
    /// Normal(65, 12) with latent correlation 0.6.
    /// </summary>
    [TestMethod]
    public void MLE_Maximum_CorrelatedTwoNormals_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMaximumCorrelatedTwoNormals());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian recovery for the correlated maximum of
    /// Normal(50, 10) and Normal(65, 12) with latent correlation 0.6.
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Maximum_CorrelatedTwoNormals_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMaximumCorrelatedTwoNormals());
    }
}
