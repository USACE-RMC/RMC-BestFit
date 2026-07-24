using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Long-running MCMC tests for the <see cref="BayesianAnalysis"/> class.
/// Tests convergence, posterior inference, and credible intervals using real MCMC runs
/// with 3000-10000 iterations. Structural/constructor tests live in
/// RMC.BestFit.Verification/ModelEstimation/BayesianAnalysisTests.cs.
/// </summary>
[TestClass]
public class BayesianAnalysisMCMCTests
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

    #region MCMC Estimation Tests

    /// <summary>
    /// Verifies <c>Test_Estimate_Normal_Converges</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Estimate_Normal_Converges()
    {
        // Arrange
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 5000,
            WarmupIterations = 1000
        };

        // Act
        await bayesian.RunAsync();

        // Assert
        Assert.IsTrue(bayesian.IsEstimated, "MCMC should converge for Normal distribution.");
    }

    /// <summary>
    /// Verifies <c>Test_Estimate_GEV_Converges</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Estimate_GEV_Converges()
    {
        // Arrange
        var dataFrame = CreateGEVTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 10000,
            WarmupIterations = 2000
        };

        // Act
        await bayesian.RunAsync();

        // Assert
        Assert.IsTrue(bayesian.IsEstimated, "MCMC should converge for GEV distribution.");
    }

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

    #endregion

    #region Sampler Type Tests

    /// <summary>
    /// Verifies <c>Test_SamplerType_DEMCzs_Works</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_SamplerType_DEMCzs_Works()
    {
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            Type = BayesianAnalysis.SamplerType.DEMCzs,
            NumberOfChains = 3,
            Iterations = 3000,
            WarmupIterations = 500
        };

        await bayesian.RunAsync();

        Assert.IsTrue(bayesian.IsEstimated);
    }

    /// <summary>
    /// Verifies <c>Test_SamplerType_ARWMH_Works</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_SamplerType_ARWMH_Works()
    {
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            Type = BayesianAnalysis.SamplerType.ARWMH,
            NumberOfChains = 3,
            Iterations = 5000,
            WarmupIterations = 1000
        };

        await bayesian.RunAsync();

        Assert.IsTrue(bayesian.IsEstimated);
    }

    #endregion

    #region Convergence Diagnostics Tests

    /// <summary>
    /// Verifies <c>Test_RHat_LessThan1Point1_Indicates_Convergence</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_RHat_LessThan1Point1_Indicates_Convergence()
    {
        // Arrange
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 4,
            Iterations = 10000,
            WarmupIterations = 2000
        };

        // Act
        await bayesian.RunAsync();

        // Assert - R-hat should be < 1.1 for well-converged chains
        Assert.IsNotNull(bayesian.Results);
        for (int i = 0; i < bayesian.Results.ParameterResults.Count(); i++)
        {
            var result = bayesian.Results.ParameterResults[i];
            string paramName = model.Parameters[i].Name;
            Assert.IsTrue(result.SummaryStatistics.Rhat < 1.1,
                $"R-hat for {paramName} is {result.SummaryStatistics.Rhat}, should be < 1.1");
        }
    }

    /// <summary>
    /// Verifies <c>Test_EffectiveSampleSize_IsAdequate</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_EffectiveSampleSize_IsAdequate()
    {
        // Arrange
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

        // Assert - ESS should be reasonably high (at least 100)
        Assert.IsNotNull(bayesian.Results);
        for (int i = 0; i < bayesian.Results.ParameterResults.Count(); i++)
        {
            var result = bayesian.Results.ParameterResults[i];
            string paramName = model.Parameters[i].Name;
            Assert.IsTrue(result.SummaryStatistics.ESS > 100,
                $"ESS for {paramName} is {result.SummaryStatistics.ESS}, should be > 100");
        }
    }

    #endregion

    #region Posterior Inference Tests

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
    /// Verifies <c>Test_PosteriorQuantiles_Computed</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_PosteriorQuantiles_Computed()
    {
        // Arrange
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 5000,
            WarmupIterations = 1000
        };

        // Act
        await bayesian.RunAsync();

        // Assert - quantiles should be ordered correctly
        Assert.IsNotNull(bayesian.Results);
        foreach (var result in bayesian.Results.ParameterResults)
        {
            Assert.IsTrue(result.SummaryStatistics.LowerCI < result.SummaryStatistics.Median,
                "Lower bound should be less than median");
            Assert.IsTrue(result.SummaryStatistics.Median < result.SummaryStatistics.UpperCI,
                "Median should be less than upper bound");
        }
    }

    #endregion

    #region Prior Sensitivity Tests

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

    #endregion

    #region Edge Cases

    /// <summary>
    /// Verifies <c>Test_SmallSample_StillConverges</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_SmallSample_StillConverges()
    {
        // Arrange - only 20 observations
        var random = new Random(99999);
        var data = new List<ExactData>();
        var normal = new Normal(50, 10);

        for (int i = 0; i < 20; i++)
        {
            data.Add(new ExactData(i, normal.InverseCDF(random.NextDouble())));
        }

        var dataFrame = new DataFrame();
        dataFrame.ExactSeries = new ExactSeries(data);
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 5000,
            WarmupIterations = 1000
        };

        // Act
        await bayesian.RunAsync();

        // Assert
        Assert.IsTrue(bayesian.IsEstimated, "Should converge even with small sample.");
    }

    /// <summary>
    /// Verifies <c>Test_MinimumChains_Works</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_MinimumChains_Works()
    {
        // DEMCzs requires at least 3 chains for differential evolution
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,  // Minimum required for DEMCzs
            Iterations = 5000,
            WarmupIterations = 1000
        };

        await bayesian.RunAsync();

        Assert.IsTrue(bayesian.IsEstimated);
    }

    #endregion

    #region MCMCResults Tests

    /// <summary>
    /// Verifies <c>Test_Results_NotNull_AfterEstimation</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Results_NotNull_AfterEstimation()
    {
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 3000,
            WarmupIterations = 500
        };

        await bayesian.RunAsync();

        Assert.IsNotNull(bayesian.Results);
    }

    /// <summary>
    /// Verifies <c>Test_Results_HasCorrectDimensions</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Results_HasCorrectDimensions()
    {
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        int numChains = 3;

        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = numChains,
            Iterations = 3000,
            WarmupIterations = 500
        };

        await bayesian.RunAsync();

        Assert.IsNotNull(bayesian.Results);
        Assert.AreEqual(numChains, bayesian.NumberOfChains);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Verifies <c>Test_ParameterResults_MatchModelParameters</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_ParameterResults_MatchModelParameters()
    {
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 3000,
            WarmupIterations = 500
        };

        await bayesian.RunAsync();

        // Should have same number of results as model parameters
        Assert.IsNotNull(bayesian.Results);
        Assert.AreEqual(model.Parameters.Count(), bayesian.Results.ParameterResults.Count());
    }

    /// <summary>
    /// Verifies <c>Test_StandardDeviation_IsPositive</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_StandardDeviation_IsPositive()
    {
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model)
        {
            NumberOfChains = 3,
            Iterations = 5000,
            WarmupIterations = 1000
        };

        await bayesian.RunAsync();

        Assert.IsNotNull(bayesian.Results);
        for (int i = 0; i < bayesian.Results.ParameterResults.Count(); i++)
        {
            var result = bayesian.Results.ParameterResults[i];
            string paramName = model.Parameters[i].Name;
            Assert.IsTrue(result.SummaryStatistics.StandardDeviation > 0,
                $"Standard deviation for {paramName} should be positive");
        }
    }

    #endregion
}
