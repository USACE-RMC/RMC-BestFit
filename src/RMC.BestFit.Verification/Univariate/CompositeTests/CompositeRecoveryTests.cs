using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.Verification.Univariate.CompositeTests;

/// <summary>
/// Verifies deterministic and posterior composite analysis against analytical,
/// published, and complete Cartesian-product oracles.
/// </summary>
/// <remarks>
/// Composite analysis estimates no likelihood. These fixtures therefore supply already
/// estimated child analyses with explicit retained <c>MCMCResults</c> and verify only the
/// composite probability and uncertainty-propagation contracts.
/// </remarks>
[TestClass]
public partial class CompositeRecoveryTests
{
    /// <summary>
    /// Verifies the three-Normal mixture CDF against the exact weighted sum of child CDFs.
    /// </summary>
    [TestMethod]
    public void MixtureCdf_MatchesExactWeightedNormalSum()
    {
        CompositeAnalysis composite = CreateReportComposite(CompositeType.Mixture);
        UnivariateDistributionBase distribution = GetPointEstimate(composite);

        for (double x = 0d; x <= 55d; x += 1d)
        {
            Assert.AreEqual(
                ReportMixtureCdf(x),
                distribution.CDF(x),
                1E-12,
                $"Three-Normal mixture CDF mismatch at x={x:G4}.");
        }
    }

    /// <summary>
    /// Verifies the three-Normal mixture quantiles against the 25 published R
    /// <c>mistr</c> values in RMC-TotalRisk report Table 45.
    /// </summary>
    [TestMethod]
    public void MixtureQuantiles_MatchPublishedRMistrTable45()
    {
        UnivariateDistributionBase distribution = GetPointEstimate(
            CreateReportComposite(CompositeType.Mixture));

        for (int index = 0; index < ReportAeps.Length; index++)
        {
            double actual = distribution.InverseCDF(1d - ReportAeps[index]);
            Assert.AreEqual(
                MistrTable45[index],
                actual,
                0.01d * MistrTable45[index],
                $"Mixture quantile at AEP {ReportAeps[index]:E1} exceeded the report's 1% band.");
        }
    }

    /// <summary>
    /// Verifies mixture quantile inversion against the analytical weighted-Normal CDF
    /// using the probability-dependent RMC-TotalRisk bound.
    /// </summary>
    [TestMethod]
    public void MixtureQuantiles_InvertAnalyticWeightedNormalCdf()
    {
        UnivariateDistributionBase distribution = GetPointEstimate(
            CreateReportComposite(CompositeType.Mixture));

        for (int index = 0; index < ReportAeps.Length; index++)
        {
            double nonexceedance = 1d - ReportAeps[index];
            double quantile = distribution.InverseCDF(nonexceedance);
            double bound = Math.Max(
                1E-8,
                5E-3 * Math.Min(ReportAeps[index], 1d - ReportAeps[index]));
            Assert.AreEqual(
                nonexceedance,
                ReportMixtureCdf(quantile),
                bound,
                $"Analytical mixture inversion failed at AEP {ReportAeps[index]:E1}.");
        }
    }

    /// <summary>
    /// Verifies independent and perfectly-positive maximum composites against their
    /// exact three-Normal closed forms.
    /// </summary>
    [TestMethod]
    public void MaximumComposite_MatchesIndependentAndComonotonicClosedForms()
    {
        UnivariateDistributionBase independent = GetPointEstimate(CreateReportComposite(
            CompositeType.CompetingRisks,
            isMaximum: true,
            Probability.DependencyType.Independent));
        UnivariateDistributionBase comonotonic = GetPointEstimate(CreateReportComposite(
            CompositeType.CompetingRisks,
            isMaximum: true,
            Probability.DependencyType.PerfectlyPositive));

        for (double x = 0d; x <= 55d; x += 1d)
        {
            double[] cdfs = ReportChildCdfs(x);
            Assert.AreEqual(cdfs.Aggregate(1d, (product, cdf) => product * cdf),
                independent.CDF(x), 1E-10, $"Independent maximum CDF mismatch at x={x:G4}.");
            Assert.AreEqual(cdfs.Min(), comonotonic.CDF(x), 1E-10,
                $"Comonotonic maximum CDF mismatch at x={x:G4}.");
        }
    }

    /// <summary>
    /// Verifies independent and perfectly-positive minimum composites against their
    /// exact three-Normal weakest-link closed forms.
    /// </summary>
    [TestMethod]
    public void MinimumComposite_MatchesIndependentAndComonotonicClosedForms()
    {
        UnivariateDistributionBase independent = GetPointEstimate(CreateReportComposite(
            CompositeType.CompetingRisks,
            isMaximum: false,
            Probability.DependencyType.Independent));
        UnivariateDistributionBase comonotonic = GetPointEstimate(CreateReportComposite(
            CompositeType.CompetingRisks,
            isMaximum: false,
            Probability.DependencyType.PerfectlyPositive));

        for (double x = 0d; x <= 55d; x += 1d)
        {
            double[] cdfs = ReportChildCdfs(x);
            double independentUnion = 1d - cdfs.Aggregate(1d, (product, cdf) => product * (1d - cdf));
            Assert.AreEqual(independentUnion, independent.CDF(x), 1E-10,
                $"Independent minimum CDF mismatch at x={x:G4}.");
            Assert.AreEqual(cdfs.Max(), comonotonic.CDF(x), 1E-10,
                $"Comonotonic minimum CDF mismatch at x={x:G4}.");
        }
    }

    /// <summary>
    /// Verifies theoretical bracketing for mixture, maximum, and minimum rules across
    /// the report grid and requires the rules to differ materially.
    /// </summary>
    [TestMethod]
    public void CombinationRules_SatisfyTheoreticalBracketingAndRemainDistinct()
    {
        UnivariateDistributionBase mixture = GetPointEstimate(CreateReportComposite(CompositeType.Mixture));
        UnivariateDistributionBase maximum = GetPointEstimate(CreateReportComposite(
            CompositeType.CompetingRisks,
            isMaximum: true,
            Probability.DependencyType.Independent));
        UnivariateDistributionBase minimum = GetPointEstimate(CreateReportComposite(
            CompositeType.CompetingRisks,
            isMaximum: false,
            Probability.DependencyType.Independent));
        double maximumRuleDifference = 0d;

        for (double x = 0d; x <= 55d; x += 0.5d)
        {
            double[] cdfs = ReportChildCdfs(x);
            double mixtureCdf = mixture.CDF(x);
            double maximumCdf = maximum.CDF(x);
            double minimumCdf = minimum.CDF(x);
            Assert.IsTrue(mixtureCdf >= cdfs.Min() - 1E-12 && mixtureCdf <= cdfs.Max() + 1E-12,
                $"Mixture left the child-CDF envelope at x={x:G4}.");
            Assert.IsTrue(maximumCdf <= cdfs.Min() + 1E-12,
                $"Maximum rule exceeded the smallest child CDF at x={x:G4}.");
            Assert.IsTrue(minimumCdf >= cdfs.Max() - 1E-12,
                $"Minimum rule fell below the largest child CDF at x={x:G4}.");
            maximumRuleDifference = Math.Max(
                maximumRuleDifference,
                Math.Max(Math.Abs(mixtureCdf - maximumCdf), Math.Abs(mixtureCdf - minimumCdf)));
        }

        Assert.IsTrue(maximumRuleDifference >= 0.10d,
            $"Composite rules differed by only {maximumRuleDifference:G6}; expected a material distinction.");
    }

    /// <summary>
    /// Verifies three-child mixture posterior means and 90% limits against the complete
    /// Cartesian product of the deterministic child posterior supports.
    /// </summary>
    /// <returns>A task that completes after the posterior oracle comparison.</returns>
    [TestMethod]
    public Task MixturePosterior_MatchesCompleteCartesianOracle()
    {
        return VerifyPosteriorCartesianOracleAsync(PosteriorRule.Mixture);
    }

    /// <summary>
    /// Verifies three-child maximum posterior means and 90% limits against the complete
    /// Cartesian product of the deterministic child posterior supports.
    /// </summary>
    /// <returns>A task that completes after the posterior oracle comparison.</returns>
    [TestMethod]
    public Task MaximumPosterior_MatchesCompleteCartesianOracle()
    {
        return VerifyPosteriorCartesianOracleAsync(PosteriorRule.Maximum);
    }

    /// <summary>
    /// Verifies three-child minimum posterior means and 90% limits against the complete
    /// Cartesian product of the deterministic child posterior supports.
    /// </summary>
    /// <returns>A task that completes after the posterior oracle comparison.</returns>
    [TestMethod]
    public Task MinimumPosterior_MatchesCompleteCartesianOracle()
    {
        return VerifyPosteriorCartesianOracleAsync(PosteriorRule.Minimum);
    }

    /// <summary>
    /// Verifies correlation-matrix maximum and minimum probabilities for two equal Normal
    /// marginals at their shared median against analytical bivariate-Normal orthant formulas.
    /// </summary>
    [TestMethod]
    public void CorrelationMatrix_MinimumAndMaximumMatchBivariateNormalOrthants()
    {
        const double correlation = 0.6d;
        double[,] matrix = { { 1d, correlation }, { correlation, 1d } };
        var children = new[]
        {
            CreateEstimatedNormalChild([10d], 10d, 1d),
            CreateEstimatedNormalChild([10d], 10d, 1d)
        };
        CompositeAnalysis maximum = CreateComposite(
            children,
            [0.5d, 0.5d],
            CompositeType.CompetingRisks,
            isMaximum: true,
            Probability.DependencyType.CorrelationMatrix,
            matrix);
        CompositeAnalysis minimum = CreateComposite(
            children,
            [0.5d, 0.5d],
            CompositeType.CompetingRisks,
            isMaximum: false,
            Probability.DependencyType.CorrelationMatrix,
            matrix);

        double lowerOrthant = 0.25d + Math.Asin(correlation) / (2d * Math.PI);
        Assert.AreEqual(lowerOrthant, GetPointEstimate(maximum).CDF(10d), 1E-8,
            "Correlation-matrix maximum did not match the lower orthant probability.");
        Assert.AreEqual(1d - lowerOrthant, GetPointEstimate(minimum).CDF(10d), 1E-8,
            "Correlation-matrix minimum did not match the complementary upper orthant probability.");
    }
}
