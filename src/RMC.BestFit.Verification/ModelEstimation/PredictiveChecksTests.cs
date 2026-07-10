using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Unit tests for the <see cref="PriorPredictiveCheck"/> and
/// <see cref="PosteriorPredictiveCheck"/> classes.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
/// These tests verify the predictive checking functionality for Bayesian models.
/// The PriorPredictiveCheck validates that priors produce reasonable predictions,
/// while PosteriorPredictiveCheck assesses model fit using posterior predictive p-values.
/// </para>
/// </remarks>
[TestClass]
public class PredictiveChecksTests
{
    #region Test Data

    /// <summary>
    /// Creates a DataFrame with sample flood data.
    /// </summary>
    private static DataFrame CreateTestDataFrame()
    {
        var values = new double[]
        {
            12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600,
            19200, 13800, 25600, 10500, 16900, 21300, 14700, 8200, 23800, 15900,
            12100, 27400, 19800, 11200, 16400, 20600, 13200, 9400, 24900, 17800
        };

        var df = new DataFrame();
        for (int i = 0; i < values.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        }
        return df;
    }

    /// <summary>
    /// Creates a test UnivariateDistribution model for predictive checking.
    /// </summary>
    private static UnivariateDistribution CreateTestModel()
    {
        var df = CreateTestDataFrame();
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    /// <summary>
    /// Creates a GEV distribution model for testing.
    /// </summary>
    private static UnivariateDistribution CreateGEVModel()
    {
        var df = CreateTestDataFrame();
        return new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);
    }

    /// <summary>
    /// Generates mock posterior samples for testing.
    /// </summary>
    /// <param name="model">The model to generate samples for.</param>
    /// <param name="nSamples">Number of samples to generate.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A list of ParameterSet objects representing posterior samples.</returns>
    private static IList<ParameterSet> GenerateMockPosteriorSamples(IModel model, int nSamples, int seed)
    {
        var rng = new Random(seed);
        var result = new List<ParameterSet>();
        int numParams = model.NumberOfParameters;

        for (int i = 0; i < nSamples; i++)
        {
            var values = new double[numParams];
            for (int j = 0; j < numParams; j++)
            {
                var param = model.Parameters[j];
                // Generate random values within parameter bounds
                double lower = Math.Max(param.LowerBound, -1e6);
                double upper = Math.Min(param.UpperBound, 1e6);
                values[j] = lower + rng.NextDouble() * (upper - lower);
            }
            result.Add(new ParameterSet(values, 0.0));
        }
        return result;
    }

    /// <summary>
    /// Gets observed data from a DataFrame.
    /// </summary>
    private static double[] GetObservedData(DataFrame df)
    {
        return df.ExactSeries.Select(e => e.Value).ToArray();
    }

    #endregion

    #region PriorPredictiveCheck Constructor Tests

    /// <summary>
    /// Tests that the PriorPredictiveCheck constructor creates an instance.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_Constructor_CreatesInstance()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var ppc = new PriorPredictiveCheck(model);

        // Assert
        Assert.IsNotNull(ppc);
        Assert.IsNotNull(ppc.Model);
    }

    /// <summary>
    /// Tests that the constructor throws when model is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_PriorPredictive_Constructor_NullModel_Throws()
    {
        var ppc = new PriorPredictiveCheck(null!);
    }

    /// <summary>
    /// Tests default property values after construction.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_DefaultProperties()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var ppc = new PriorPredictiveCheck(model);

        // Assert
        Assert.AreEqual(12345, ppc.Seed);
        Assert.AreEqual(1000, ppc.NumberOfDraws);
    }

    #endregion

    #region PriorPredictiveCheck Property Tests

    /// <summary>
    /// Tests that the Seed property can be set and retrieved.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_Seed_SetAndGet()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model);

        // Act
        ppc.Seed = 54321;

        // Assert
        Assert.AreEqual(54321, ppc.Seed);
    }

    /// <summary>
    /// Tests that the NumberOfDraws property can be set and retrieved.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_NumberOfDraws_SetAndGet()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model);

        // Act
        ppc.NumberOfDraws = 500;

        // Assert
        Assert.AreEqual(500, ppc.NumberOfDraws);
    }

    /// <summary>
    /// Tests that NumberOfDraws throws for zero value.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_PriorPredictive_NumberOfDraws_ZeroThrows()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model);

        // Act
        ppc.NumberOfDraws = 0;
    }

    /// <summary>
    /// Tests that NumberOfDraws throws for negative value.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_PriorPredictive_NumberOfDraws_NegativeThrows()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model);

        // Act
        ppc.NumberOfDraws = -1;
    }

    #endregion

    #region PriorPredictiveCheck SampleFromPriors Tests

    /// <summary>
    /// Tests that SampleFromPriors returns the correct number of samples.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_SampleFromPriors_ReturnsCorrectCount()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model);
        ppc.NumberOfDraws = 100;

        // Act
        var samples = ppc.SampleFromPriors();

        // Assert
        Assert.AreEqual(100, samples.Count);
    }

    /// <summary>
    /// Tests that SampleFromPriors returns samples with correct parameter count.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_SampleFromPriors_CorrectDimensions()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model);
        ppc.NumberOfDraws = 50;

        // Act
        var samples = ppc.SampleFromPriors();

        // Assert
        Assert.IsTrue(samples.Count > 0);
        Assert.AreEqual(model.NumberOfParameters, samples[0].Values.Length);
    }

    /// <summary>
    /// Tests that SampleFromPriors is reproducible with the same seed.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_SampleFromPriors_Reproducible()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc1 = new PriorPredictiveCheck(model) { Seed = 12345, NumberOfDraws = 10 };
        var ppc2 = new PriorPredictiveCheck(model) { Seed = 12345, NumberOfDraws = 10 };

        // Act
        var samples1 = ppc1.SampleFromPriors();
        var samples2 = ppc2.SampleFromPriors();

        // Assert
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < samples1[i].Values.Length; j++)
            {
                Assert.AreEqual(samples1[i].Values[j], samples2[i].Values[j], 1e-10);
            }
        }
    }

    #endregion

    #region PriorPredictiveCheck GeneratePriorPredictive Tests

    /// <summary>
    /// Tests that GeneratePriorPredictive returns datasets.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_GeneratePriorPredictive_ReturnsData()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model) { NumberOfDraws = 50 };

        // Act
        var datasets = ppc.GeneratePriorPredictive(30);

        // Assert
        Assert.IsTrue(datasets.Count > 0, "Should generate at least one dataset.");
    }

    /// <summary>
    /// Tests that GeneratePriorPredictive returns datasets with correct sample size.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_GeneratePriorPredictive_CorrectSampleSize()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model) { NumberOfDraws = 50 };

        // Act
        var datasets = ppc.GeneratePriorPredictive(25);

        // Assert
        foreach (var data in datasets)
        {
            Assert.AreEqual(25, data.Length);
        }
    }

    /// <summary>
    /// Tests that GeneratePriorPredictive throws for zero sample size.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_PriorPredictive_GeneratePriorPredictive_ZeroSampleSize_Throws()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model);

        // Act
        ppc.GeneratePriorPredictive(0);
    }

    #endregion

    #region PriorPredictiveCheck ComputeSummary Tests

    /// <summary>
    /// Tests that ComputeSummary returns a valid summary.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_ComputeSummary_ReturnsValidSummary()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model) { NumberOfDraws = 100 };

        // Act
        var summary = ppc.ComputeSummary(30);

        // Assert
        Assert.IsNotNull(summary);
        Assert.IsTrue(summary.NumberOfValidDraws > 0);
    }

    /// <summary>
    /// Tests that summary quantiles are properly ordered.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_ComputeSummary_QuantilesAreOrdered()
    {
        // Arrange
        var model = CreateTestModel();
        var ppc = new PriorPredictiveCheck(model) { NumberOfDraws = 200 };

        // Act
        var summary = ppc.ComputeSummary(30);

        // Assert - Quantiles should be ordered: 2.5% < 25% < 50% < 75% < 97.5%
        if (summary.MeanQuantiles.Length == 5)
        {
            Assert.IsTrue(summary.MeanQuantiles[0] <= summary.MeanQuantiles[1]);
            Assert.IsTrue(summary.MeanQuantiles[1] <= summary.MeanQuantiles[2]);
            Assert.IsTrue(summary.MeanQuantiles[2] <= summary.MeanQuantiles[3]);
            Assert.IsTrue(summary.MeanQuantiles[3] <= summary.MeanQuantiles[4]);
        }
    }

    #endregion

    #region PosteriorPredictiveCheck Constructor Tests

    /// <summary>
    /// Tests that the PosteriorPredictiveCheck constructor creates an instance.
    /// </summary>
    [TestMethod]
    public void Test_PosteriorPredictive_Constructor_CreatesInstance()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);
        var observedData = GetObservedData(model.DataFrame);

        // Act
        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Assert
        Assert.IsNotNull(ppc);
        Assert.IsNotNull(ppc.Model);
    }

    /// <summary>
    /// Tests that the constructor throws when model is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_PosteriorPredictive_Constructor_NullModel_Throws()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);
        var observedData = GetObservedData(model.DataFrame);

        // Act
        var ppc = new PosteriorPredictiveCheck(null!, posteriorSamples, observedData);
    }

    /// <summary>
    /// Tests that the constructor throws when posterior samples is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_PosteriorPredictive_Constructor_NullPosterior_Throws()
    {
        // Arrange
        var model = CreateTestModel();
        var observedData = GetObservedData(model.DataFrame);

        // Act
        var ppc = new PosteriorPredictiveCheck(model, (IList<ParameterSet>)null!, observedData);
    }

    /// <summary>
    /// Tests that the constructor throws when observed data is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_PosteriorPredictive_Constructor_NullData_Throws()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);

        // Act
        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, (double[])null!);
    }

    /// <summary>
    /// Tests that the constructor throws when posterior samples is empty.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_PosteriorPredictive_Constructor_EmptyPosterior_Throws()
    {
        // Arrange
        var model = CreateTestModel();
        var observedData = GetObservedData(model.DataFrame);

        // Act
        var ppc = new PosteriorPredictiveCheck(model, new List<ParameterSet>(), observedData);
    }

    /// <summary>
    /// Tests that the constructor throws when observed data is empty.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_PosteriorPredictive_Constructor_EmptyData_Throws()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);

        // Act
        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, Array.Empty<double>());
    }

    #endregion

    #region PosteriorPredictiveCheck Property Tests

    /// <summary>
    /// Tests that NumberOfPosteriorSamples returns correct count.
    /// </summary>
    [TestMethod]
    public void Test_PosteriorPredictive_NumberOfPosteriorSamples()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 250, 12345);
        var observedData = GetObservedData(model.DataFrame);

        // Act
        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Assert
        Assert.AreEqual(250, ppc.NumberOfPosteriorSamples);
    }

    /// <summary>
    /// Tests that SampleSize returns correct value.
    /// </summary>
    [TestMethod]
    public void Test_PosteriorPredictive_SampleSize()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);
        var observedData = GetObservedData(model.DataFrame);

        // Act
        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Assert
        Assert.AreEqual(observedData.Length, ppc.SampleSize);
    }

    /// <summary>
    /// Tests that Seed property can be set and retrieved.
    /// </summary>
    [TestMethod]
    public void Test_PosteriorPredictive_Seed_SetAndGet()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);
        var observedData = GetObservedData(model.DataFrame);

        // Act
        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);
        ppc.Seed = 99999;

        // Assert
        Assert.AreEqual(99999, ppc.Seed);
    }

    #endregion

    #region PosteriorPredictiveCheck GenerateReplicates Tests

    /// <summary>
    /// Tests that GenerateReplicates returns data.
    /// </summary>
    [TestMethod]
    public void Test_PosteriorPredictive_GenerateReplicates_ReturnsData()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);
        var observedData = GetObservedData(model.DataFrame);

        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Act
        var replicates = ppc.GenerateReplicates(50);

        // Assert
        Assert.IsTrue(replicates.Count > 0);
    }

    /// <summary>
    /// Tests that replicates have correct sample size.
    /// </summary>
    [TestMethod]
    public void Test_PosteriorPredictive_GenerateReplicates_CorrectSampleSize()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);
        var observedData = GetObservedData(model.DataFrame);

        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Act
        var replicates = ppc.GenerateReplicates(50);

        // Assert - Each replicate should have same size as observed data
        foreach (var rep in replicates)
        {
            Assert.AreEqual(observedData.Length, rep.Length);
        }
    }

    /// <summary>
    /// Tests that GenerateReplicates throws for zero.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_PosteriorPredictive_GenerateReplicates_ZeroThrows()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);
        var observedData = GetObservedData(model.DataFrame);

        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Act
        ppc.GenerateReplicates(0);
    }

    #endregion

    #region PosteriorPredictiveCheck ComputePValue Tests

    /// <summary>
    /// Tests that ComputePValue returns a valid value between 0 and 1.
    /// </summary>
    [TestMethod]
    public void Test_PosteriorPredictive_ComputePValue_ReturnsValidValue()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 200, 12345);
        var observedData = GetObservedData(model.DataFrame);

        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Act
        double pValue = ppc.ComputePValue(data => Statistics.Mean(data), 100);

        // Assert
        Assert.IsTrue(pValue >= 0.0 && pValue <= 1.0);
    }

    /// <summary>
    /// Tests that ComputePValue throws for null statistic.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_PosteriorPredictive_ComputePValue_NullStatistic_Throws()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 100, 12345);
        var observedData = GetObservedData(model.DataFrame);

        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Act
        ppc.ComputePValue(null!, 100);
    }

    #endregion

    #region PosteriorPredictiveCheck ComputeCommonPValues Tests

    /// <summary>
    /// Tests that ComputeCommonPValues returns all statistics.
    /// </summary>
    [TestMethod]
    public void Test_PosteriorPredictive_ComputeCommonPValues_ReturnsAllStatistics()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 200, 12345);
        var observedData = GetObservedData(model.DataFrame);

        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Act
        var results = ppc.ComputeCommonPValues(100);

        // Assert
        Assert.IsTrue(results.MeanPValue >= 0 && results.MeanPValue <= 1);
        Assert.IsTrue(results.SDPValue >= 0 && results.SDPValue <= 1);
        Assert.IsTrue(results.SkewnessPValue >= 0 && results.SkewnessPValue <= 1);
        Assert.IsTrue(results.MinPValue >= 0 && results.MinPValue <= 1);
        Assert.IsTrue(results.MaxPValue >= 0 && results.MaxPValue <= 1);
    }

    /// <summary>
    /// Tests that NumberOfReplicates is set correctly.
    /// </summary>
    [TestMethod]
    public void Test_PosteriorPredictive_ComputeCommonPValues_NumberOfReplicates()
    {
        // Arrange
        var model = CreateTestModel();
        var posteriorSamples = GenerateMockPosteriorSamples(model, 200, 12345);
        var observedData = GetObservedData(model.DataFrame);

        var ppc = new PosteriorPredictiveCheck(model, posteriorSamples, observedData);

        // Act
        var results = ppc.ComputeCommonPValues(150);

        // Assert
        Assert.AreEqual(150, results.NumberOfReplicates);
    }

    #endregion

    #region PredictiveCheckResults Tests

    /// <summary>
    /// Tests that HasPotentialMisfit returns false for good fit.
    /// </summary>
    [TestMethod]
    public void Test_PredictiveCheckResults_HasPotentialMisfit_GoodFit_ReturnsFalse()
    {
        // Arrange
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.5,
            SDPValue = 0.45,
            SkewnessPValue = 0.6,
            MinPValue = 0.3,
            MaxPValue = 0.7
        };

        // Assert
        Assert.IsFalse(results.HasPotentialMisfit());
    }

    /// <summary>
    /// Tests that HasPotentialMisfit returns true for extreme mean.
    /// </summary>
    [TestMethod]
    public void Test_PredictiveCheckResults_HasPotentialMisfit_ExtremeMean_ReturnsTrue()
    {
        // Arrange
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.01,  // Extreme
            SDPValue = 0.5,
            SkewnessPValue = 0.5,
            MinPValue = 0.5,
            MaxPValue = 0.5
        };

        // Assert
        Assert.IsTrue(results.HasPotentialMisfit());
    }

    /// <summary>
    /// Tests that HasPotentialMisfit returns true for extreme max.
    /// </summary>
    [TestMethod]
    public void Test_PredictiveCheckResults_HasPotentialMisfit_ExtremeMax_ReturnsTrue()
    {
        // Arrange
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.5,
            SDPValue = 0.5,
            SkewnessPValue = 0.5,
            MinPValue = 0.5,
            MaxPValue = 0.99  // Extreme (upper)
        };

        // Assert
        Assert.IsTrue(results.HasPotentialMisfit());
    }

    /// <summary>
    /// Tests custom threshold for HasPotentialMisfit.
    /// </summary>
    [TestMethod]
    public void Test_PredictiveCheckResults_HasPotentialMisfit_CustomThreshold()
    {
        // Arrange
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.08,  // Would fail at 0.1 threshold
            SDPValue = 0.5,
            SkewnessPValue = 0.5,
            MinPValue = 0.5,
            MaxPValue = 0.5
        };

        // Assert
        Assert.IsFalse(results.HasPotentialMisfit(0.05)); // Pass with 0.05 threshold
        Assert.IsTrue(results.HasPotentialMisfit(0.10));  // Fail with 0.10 threshold
    }

    #endregion

    #region PredictiveSummary Tests

    /// <summary>
    /// Tests that PredictiveSummary has default values.
    /// </summary>
    [TestMethod]
    public void Test_PredictiveSummary_DefaultValues()
    {
        // Act
        var summary = new PredictiveSummary();

        // Assert
        Assert.AreEqual(0, summary.NumberOfValidDraws);
        Assert.IsNotNull(summary.MeanQuantiles);
        Assert.IsNotNull(summary.SDQuantiles);
        Assert.IsNotNull(summary.MinQuantiles);
        Assert.IsNotNull(summary.MaxQuantiles);
    }

    /// <summary>
    /// Tests that PredictiveSummary values can be set.
    /// </summary>
    [TestMethod]
    public void Test_PredictiveSummary_SetValues()
    {
        // Arrange & Act
        var summary = new PredictiveSummary
        {
            NumberOfValidDraws = 100,
            MeanQuantiles = new double[] { 90, 95, 100, 105, 110 },
            SDQuantiles = new double[] { 10, 12, 15, 18, 20 }
        };

        // Assert
        Assert.AreEqual(100, summary.NumberOfValidDraws);
        Assert.AreEqual(5, summary.MeanQuantiles.Length);
        Assert.AreEqual(100, summary.MeanQuantiles[2]); // Median
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests PriorPredictiveCheck with GEV distribution.
    /// </summary>
    [TestMethod]
    public void Test_PriorPredictive_GEVDistribution()
    {
        // Arrange
        var model = CreateGEVModel();
        var ppc = new PriorPredictiveCheck(model) { NumberOfDraws = 50 };

        // Act
        var summary = ppc.ComputeSummary(30);

        // Assert
        Assert.IsTrue(summary.NumberOfValidDraws > 0);
    }

    #endregion
}
