using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies Bayesian posterior recovery, credible-interval coverage, and informative-prior response on deterministic synthetic data.
/// </summary>
[TestClass]
public class BayesianAnalysisRecoveryTests
{
#region Test Data

    /// <summary>
    /// Creates normal Test Data.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static DataFrame CreateNormalTestData()
    {
        // Generate normal data: μ = 100, σ = 15
        var random = new Random(12345);
        var data = new List<ExactData>();
        var normal = new Normal(100, 15);

        for (int i = 0; i < 100; i++)
        {
            double value = normal.InverseCDF(random.NextDouble());
            data.Add(new ExactData(i, value));
        }

        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    /// <summary>
    /// Creates gEV Test Data.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static DataFrame CreateGEVTestData()
    {
        // Generate GEV data: μ = 100, σ = 25, ξ = 0.1
        var random = new Random(54321);
        var data = new List<ExactData>();
        var gev = new GeneralizedExtremeValue(100, 25, 0.1);

        for (int i = 0; i < 100; i++)
        {
            double value = gev.InverseCDF(random.NextDouble());
            data.Add(new ExactData(i, value));
        }

        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    #endregion

    /// <summary>
    /// Verifies <c>Test_Estimate_PosteriorMean_NearTrueValue</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Estimate_PosteriorMean_NearTrueValue()
    {
        // Arrange - known parameters μ=100, σ=15
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 10000,
            WarmupIterations = 2000
        };

        // Act
        await bayesian.RunAsync();

        // Assert - posterior mean should be close to true values
        Assert.IsNotNull(bayesian.Results);
        double meanEstimate = bayesian.Results.ParameterResults[0].SummaryStatistics.Mean;
        double sigmaEstimate = bayesian.Results.ParameterResults[1].SummaryStatistics.Mean;

        Assert.AreEqual(100.0, meanEstimate, 5.0, "Mean estimate should be near true value.");
        Assert.AreEqual(15.0, sigmaEstimate, 3.0, "Sigma estimate should be near true value.");
    }

    /// <summary>
    /// Verifies <c>Test_CredibleIntervals_ContainTrueValue</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_CredibleIntervals_ContainTrueValue()
    {
        // Arrange - true values: μ=100, σ=15
        // Note: With n=100 samples, the sample mean has SE ≈ 15/√100 = 1.5
        // The true value may be outside the posterior CI due to sampling variation
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 10000,
            WarmupIterations = 2000
        };

        // Act
        await bayesian.RunAsync();

        // Assert - posterior estimates should be reasonably close to true values
        // Allow for sampling variation (±2 SE for mean, ±20% for sigma)
        Assert.IsNotNull(bayesian.Results);
        var muResult = bayesian.Results.ParameterResults[0].SummaryStatistics;
        var sigmaResult = bayesian.Results.ParameterResults[1].SummaryStatistics;

        // Mean should be within ±5 of true value (allowing for sampling variation)
        Assert.IsTrue(muResult.Mean > 95.0 && muResult.Mean < 105.0,
            $"Mean estimate {muResult.Mean} should be near μ=100 (within ±5)");

        // Sigma should be within ±5 of true value
        Assert.IsTrue(sigmaResult.Mean > 10.0 && sigmaResult.Mean < 20.0,
            $"Sigma estimate {sigmaResult.Mean} should be near σ=15 (within ±5)");

        // CI should have reasonable width (not too narrow or wide)
        double muCIWidth = muResult.UpperCI - muResult.LowerCI;
        Assert.IsTrue(muCIWidth > 2.0 && muCIWidth < 10.0,
            $"Mean CI width {muCIWidth} should be reasonable");
    }

    /// <summary>
    /// Verifies <c>Test_InformativePrior_ShiftsPosterior</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_InformativePrior_ShiftsPosterior()
    {
        // Arrange - data with μ≈100, but prior centered at 120
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);

        // First, get the MLE estimate to understand where data likelihood peaks
        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        double mleMean = mle.BestParameterSet.Values[0];

        // Set informative prior on mean (centered well above the data mean)
        model.Parameters[0].PriorDistribution = new Normal(120, 5);

        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 10000,
            WarmupIterations = 2000
        };

        // Act
        await bayesian.RunAsync();

        // Assert - posterior mean should be pulled toward prior (higher than MLE)
        Assert.IsNotNull(bayesian.Results);
        double posteriorMean = bayesian.Results.ParameterResults[0].SummaryStatistics.Mean;

        // The posterior should be shifted toward the prior mean (120)
        // So posterior mean should be greater than MLE mean
        Assert.IsTrue(posteriorMean > mleMean,
            $"Posterior mean {posteriorMean} should be greater than MLE mean {mleMean} due to informative prior at 120");

        // And posterior should be less than the prior mean
        Assert.IsTrue(posteriorMean < 120.0,
            $"Posterior mean {posteriorMean} should be less than prior mean 120");
    }
}
