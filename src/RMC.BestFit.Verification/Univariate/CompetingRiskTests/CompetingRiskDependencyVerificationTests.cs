using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.CompetingRiskTests;

/// <summary>
/// Computational verification of competing-risk simulation dependence and composite CDF behavior.
/// </summary>
/// <remarks>
/// Each method verifies one supported dependency mode through the production
/// <see cref="CompetingRisksModel.GenerateRandomValues(int, int)"/> path. Rank dependence
/// is compared with the Gaussian-copula population value, while the empirical probability
/// of a two-Normal maximum not exceeding zero is compared with its analytical value.
/// </remarks>
[TestClass]
public class CompetingRiskDependencyVerificationTests
{
    private const int SampleSize = 40000;
    private const int Seed = 24681357;
    private const double Correlation = 0.6d;

    /// <summary>
    /// Verifies independent competing-risk simulation against zero rank dependence and
    /// the independent maximum CDF.
    /// </summary>
    [TestMethod]
    public void Test_IndependentSimulation_MatchesRankDependenceAndCompositeCdf()
    {
        VerifyDependencyMode(Probability.DependencyType.Independent, 0d, 0.25d);
    }

    /// <summary>
    /// Verifies perfectly positive competing-risk simulation against unit rank dependence
    /// and the comonotonic maximum CDF.
    /// </summary>
    [TestMethod]
    public void Test_PerfectlyPositiveSimulation_MatchesRankDependenceAndCompositeCdf()
    {
        VerifyDependencyMode(Probability.DependencyType.PerfectlyPositive, 1d, 0.5d);
    }

    /// <summary>
    /// Verifies perfectly negative competing-risk simulation against its two-dimensional
    /// Gaussian-copula rank dependence and maximum CDF.
    /// </summary>
    [TestMethod]
    public void Test_PerfectlyNegativeSimulation_MatchesRankDependenceAndCompositeCdf()
    {
        double rho = -1d + Math.Sqrt(Numerics.Tools.DoubleMachineEpsilon);
        VerifyDependencyMode(
            Probability.DependencyType.PerfectlyNegative,
            GaussianCopulaSpearmanRho(rho),
            GaussianCopulaMidpointJointProbability(rho));
    }

    /// <summary>
    /// Verifies correlation-matrix competing-risk simulation against the configured
    /// Gaussian-copula rank dependence and maximum CDF.
    /// </summary>
    [TestMethod]
    public void Test_CorrelationMatrixSimulation_MatchesRankDependenceAndCompositeCdf()
    {
        VerifyDependencyMode(
            Probability.DependencyType.CorrelationMatrix,
            GaussianCopulaSpearmanRho(Correlation),
            GaussianCopulaMidpointJointProbability(Correlation));
    }

    /// <summary>
    /// Verifies one dependency mode using separated marginal-location probes and a
    /// same-location composite-CDF probe.
    /// </summary>
    /// <param name="dependency">The dependency mode to verify.</param>
    /// <param name="expectedRankCorrelation">The population Spearman rank correlation.</param>
    /// <param name="expectedCompositeProbability">The population probability that the maximum is at most zero.</param>
    private static void VerifyDependencyMode(
        Probability.DependencyType dependency,
        double expectedRankCorrelation,
        double expectedCompositeProbability)
    {
        double[,]? correlationMatrix = dependency == Probability.DependencyType.CorrelationMatrix
            ? new[,] { { 1d, Correlation }, { Correlation, 1d } }
            : null;

        // A 1,000-standard-deviation location separation makes each maximum identify
        // one latent marginal without exposing or duplicating Numerics sampling code.
        double[] firstLatentProbe = CreateMaximumModel(
            dependency,
            correlationMatrix,
            new Normal(1000d, 1d),
            new Normal(0d, 1d)).GenerateRandomValues(SampleSize, Seed);
        double[] secondLatentProbe = CreateMaximumModel(
            dependency,
            correlationMatrix,
            new Normal(0d, 1d),
            new Normal(1000d, 1d)).GenerateRandomValues(SampleSize, Seed);
        double actualRankCorrelation = SpearmanCorrelation(firstLatentProbe, secondLatentProbe);

        double[] compositeSample = CreateMaximumModel(
            dependency,
            correlationMatrix,
            new Normal(0d, 1d),
            new Normal(0d, 1d)).GenerateRandomValues(SampleSize, Seed);
        double actualCompositeProbability = compositeSample.Count(value => value <= 0d) / (double)SampleSize;

        double rankTolerance = 6d / Math.Sqrt(SampleSize - 1d);
        double probabilityStandardError = Math.Sqrt(
            expectedCompositeProbability * (1d - expectedCompositeProbability) / SampleSize);
        double probabilityTolerance = 6d * probabilityStandardError + 1d / SampleSize;

        Assert.AreEqual(
            expectedRankCorrelation,
            actualRankCorrelation,
            rankTolerance,
            $"{dependency} simulation did not reproduce its Gaussian-copula rank dependence.");
        Assert.AreEqual(
            expectedCompositeProbability,
            actualCompositeProbability,
            probabilityTolerance,
            $"{dependency} simulation did not reproduce the analytical maximum CDF at zero.");
    }

    /// <summary>
    /// Creates a production competing-risk model configured to take a maximum.
    /// </summary>
    /// <param name="dependency">The dependency mode.</param>
    /// <param name="correlationMatrix">The optional correlation matrix.</param>
    /// <param name="first">The first marginal.</param>
    /// <param name="second">The second marginal.</param>
    /// <returns>The configured competing-risk model.</returns>
    private static CompetingRisksModel CreateMaximumModel(
        Probability.DependencyType dependency,
        double[,]? correlationMatrix,
        UnivariateDistributionBase first,
        UnivariateDistributionBase second)
    {
        var distribution = new CompetingRisks(new[] { first, second })
        {
            MinimumOfRandomVariables = false,
            Dependency = dependency
        };
        if (correlationMatrix != null)
            distribution.CorrelationMatrix = (double[,])correlationMatrix.Clone();

        return new CompetingRisksModel
        {
            CompetingRisks = distribution
        };
    }

    /// <summary>
    /// Computes the population Spearman correlation for a Gaussian copula.
    /// </summary>
    /// <param name="pearsonCorrelation">The latent Normal Pearson correlation.</param>
    /// <returns>The corresponding Spearman correlation.</returns>
    private static double GaussianCopulaSpearmanRho(double pearsonCorrelation)
    {
        return 6d / Math.PI * Math.Asin(pearsonCorrelation / 2d);
    }

    /// <summary>
    /// Computes a bivariate standard-Normal joint probability at the marginal medians.
    /// </summary>
    /// <param name="correlation">The latent Normal correlation.</param>
    /// <returns>The probability that both variables are at most zero.</returns>
    private static double GaussianCopulaMidpointJointProbability(double correlation)
    {
        return 0.25d + Math.Asin(correlation) / (2d * Math.PI);
    }

    /// <summary>
    /// Computes Spearman rank correlation for two continuous samples.
    /// </summary>
    /// <param name="first">The first sample.</param>
    /// <param name="second">The second sample.</param>
    /// <returns>The Pearson correlation between sample ranks.</returns>
    private static double SpearmanCorrelation(double[] first, double[] second)
    {
        if (first.Length != second.Length || first.Length < 2)
            throw new ArgumentException("Rank-correlation samples must have equal lengths of at least two.");

        double[] firstRanks = GetRanks(first);
        double[] secondRanks = GetRanks(second);
        double meanRank = (first.Length - 1d) / 2d;
        double covariance = 0d;
        double firstVariance = 0d;
        double secondVariance = 0d;
        for (int index = 0; index < first.Length; index++)
        {
            double firstCentered = firstRanks[index] - meanRank;
            double secondCentered = secondRanks[index] - meanRank;
            covariance += firstCentered * secondCentered;
            firstVariance += firstCentered * firstCentered;
            secondVariance += secondCentered * secondCentered;
        }

        return covariance / Math.Sqrt(firstVariance * secondVariance);
    }

    /// <summary>
    /// Assigns zero-based ranks to a continuous sample.
    /// </summary>
    /// <param name="values">The sample values.</param>
    /// <returns>The rank at each original sample position.</returns>
    private static double[] GetRanks(double[] values)
    {
        int[] order = Enumerable.Range(0, values.Length)
            .OrderBy(index => values[index])
            .ToArray();
        var ranks = new double[values.Length];
        for (int rank = 0; rank < order.Length; rank++)
            ranks[order[rank]] = rank;
        return ranks;
    }
}
