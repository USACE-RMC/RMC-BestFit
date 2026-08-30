namespace RMC.BestFit.Verification.Univariate.CompetingRiskTests;

/// <summary>
/// Recovers the identified component distributions of synthetic competing-risk populations by
/// maximum likelihood and Bayesian analysis.
/// </summary>
/// <remarks>
/// Each public method is intentionally independent so it can be authorized, run, and
/// reported through the guarded exact-method runner. Every fixture predeclares and verifies
/// balanced cause shares and visible dog-leg behavior before fitting. Bayesian recovery keeps
/// the production DEMCzs configuration. The independent maximum disables only the optional
/// Jeffreys scale multiplier because a component can disappear at its scale boundary while the
/// maximum likelihood remains finite; its bounded parameter priors remain active. The
/// fixed-correlation likelihood is exercised by MLE only.
/// </remarks>
[TestClass]
public partial class CompetingRiskRecoveryTests
{
    /// <summary>
    /// Verifies MLE component recovery for the independent minimum dog leg formed by
    /// Weibull(50, 1) and Weibull(80, 3).
    /// </summary>
    [TestMethod]
    public void MLE_Minimum_TwoWeibullDogLeg_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMinimumTwoWeibullDogLeg());
    }

    /// <summary>
    /// Verifies default-DEMCzs Bayesian component recovery for the independent minimum dog leg
    /// formed by Weibull(50, 1) and Weibull(80, 3).
    /// </summary>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Minimum_TwoWeibullDogLeg_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(CreateMinimumTwoWeibullDogLeg());
    }

    /// <summary>
    /// Verifies MLE component recovery for the independent maximum dog leg formed by
    /// Weibull(100, 3) and Gumbel(80, 20).
    /// </summary>
    [TestMethod]
    public void MLE_Maximum_WeibullGumbelDogLeg_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMaximumWeibullGumbelDogLeg());
    }

    /// <summary>
    /// Verifies Bayesian component recovery for the independent maximum dog leg formed by
    /// Weibull(100, 3) and Gumbel(80, 20), with the optional Jeffreys scale prior disabled.
    /// </summary>
    /// <remarks>
    /// As the Weibull scale approaches its numerical lower boundary, that component disappears
    /// from a maximum while the surviving Gumbel likelihood remains finite. Multiplying the
    /// bounded parameter prior by 1/scale therefore creates a collapsed boundary mode unrelated
    /// to the cause-balanced data design. This recovery cell retains the bounded parameter
    /// priors, generating parent, seed, sample size, production DEMCzs settings, and common
    /// Bayesian acceptance rule; it changes only <c>UseJeffreysRuleForScale</c> to false.
    /// </remarks>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    [TestMethod]
    public Task Bayesian_Maximum_WeibullGumbelDogLeg_RecoversParent()
    {
        return VerifyBayesianRecoveryAsync(
            CreateMaximumWeibullGumbelDogLeg(),
            useJeffreysRuleForScale: false);
    }

    /// <summary>
    /// Verifies MLE component recovery for the fixed-correlation minimum dog leg formed by
    /// Weibull(50, 1) and Weibull(80, 3) at latent correlation 0.6.
    /// </summary>
    [TestMethod]
    public void MLE_Minimum_CorrelatedTwoWeibullDogLeg_RecoversParent()
    {
        VerifyMaximumLikelihoodRecovery(CreateMinimumCorrelatedTwoWeibullDogLeg());
    }
}
