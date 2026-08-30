using Numerics.Data.Statistics;
using Numerics.Distributions;

namespace RMC.BestFit.Verification.Univariate.CompetingRiskTests;

/// <summary>
/// Defines the predeclared, clearly identified competing-risk recovery fixtures.
/// </summary>
public partial class CompetingRiskRecoveryTests
{
    /// <summary>
    /// Creates the independent minimum dog leg formed by Weibull(50, 1) and Weibull(80, 3).
    /// </summary>
    /// <returns>The identified recovery fixture.</returns>
    private static RecoveryFixture CreateMinimumTwoWeibullDogLeg()
    {
        return CreateFixture(
            "minimum Weibull(50,1) + Weibull(80,3) dog leg",
            true,
            [new Weibull(50d, 1d), new Weibull(80d, 3d)]);
    }

    /// <summary>
    /// Creates the independent maximum dog leg formed by Weibull(100, 3) and Gumbel(80, 20).
    /// </summary>
    /// <returns>The identified recovery fixture.</returns>
    private static RecoveryFixture CreateMaximumWeibullGumbelDogLeg()
    {
        return CreateFixture(
            "maximum Weibull(100,3) + Gumbel(80,20) dog leg",
            false,
            [new Weibull(100d, 3d), new Gumbel(80d, 20d)]);
    }

    /// <summary>
    /// Creates the fixed-correlation minimum dog leg formed by Weibull(50, 1) and
    /// Weibull(80, 3) at latent Gaussian correlation 0.6.
    /// </summary>
    /// <returns>The identified MLE-only recovery fixture.</returns>
    private static RecoveryFixture CreateMinimumCorrelatedTwoWeibullDogLeg()
    {
        return CreateFixture(
            "correlated minimum Weibull(50,1) + Weibull(80,3) dog leg, rho=0.6",
            true,
            [new Weibull(50d, 1d), new Weibull(80d, 3d)],
            Probability.DependencyType.CorrelationMatrix,
            new[,] { { 1d, 0.6d }, { 0.6d, 1d } });
    }
}
