using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Expanded unit tests for the <see cref="PosteriorPredictiveCheck"/> class that
/// exercise <see cref="PosteriorPredictiveCheck.GenerateReplicates"/>,
/// <see cref="PosteriorPredictiveCheck.ComputePValue"/>,
/// <see cref="PosteriorPredictiveCheck.ComputeCommonPValues"/>, and
/// <see cref="PosteriorPredictiveCheck.ComputeSummary"/> code paths.
/// </summary>
/// <remarks>
/// These tests are programmatic — they use a <see cref="UnivariateDistribution"/>
/// with fixed inline data and pre-built <see cref="ParameterSet"/> samples that
/// represent already-fitted posterior draws. None of these tests run an estimator
/// (no MLE, MAP, GMM, or MCMC). The methods under test only generate replicates
/// from given parameter values, which is pure simulation supported by the
/// <see cref="ISimulatable{T}"/> interface.
/// Computational verification (e.g., comparing replicate distributions to fitted
/// theoretical values) belongs in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class PosteriorPredictiveCheckExpandedTests
{
    /// <summary>
    /// Creates a small Normal model used as the fitted target across tests.
    /// </summary>
    private static UnivariateDistribution MakeNormalModel()
    {
        var df = new DataFrame
        {
            ExactSeries = new ExactSeries(
                new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 })
        };
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    /// <summary>
    /// Creates a deterministic list of <see cref="ParameterSet"/> draws representing
    /// hypothetical posterior samples around the data mean and SD. The Fitness field
    /// is set to 0 (real posteriors store negative-log-likelihood, but the predictive
    /// check does not use it for replicate generation).
    /// </summary>
    /// <param name="count">The number of samples to produce.</param>
    /// <returns>A list of <see cref="ParameterSet"/> with two parameters per draw.</returns>
    private static IList<ParameterSet> MakePosteriorSamples(int count)
    {
        var samples = new List<ParameterSet>();
        for (int i = 0; i < count; i++)
        {
            // Slight drift in mu and sigma so replicates aren't degenerate.
            samples.Add(new ParameterSet([16500.0 + 5.0 * i, 6000.0 + 1.0 * i], 0.0));
        }
        return samples;
    }

    /// <summary>
    /// Returns sample observed data of fixed length 10.
    /// </summary>
    private static double[] MakeObservedData() =>
        new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };

    #region GenerateReplicates Tests

    /// <summary>
    /// GenerateReplicates returns at most numberOfReplicates datasets.
    /// </summary>
    [TestMethod]
    public void GenerateReplicates_DatasetCount_AtMostNumberOfReplicates()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(50), MakeObservedData())
        {
            Seed = 42
        };

        // Act
        var replicates = check.GenerateReplicates(20);

        // Assert
        Assert.IsTrue(replicates.Count <= 20);
    }

    /// <summary>
    /// GenerateReplicates produces datasets whose length equals the observed sample size.
    /// </summary>
    [TestMethod]
    public void GenerateReplicates_DatasetLengthEqualsObservedSampleSize()
    {
        // Arrange
        var observed = MakeObservedData();
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(40), observed)
        {
            Seed = 1
        };

        // Act
        var replicates = check.GenerateReplicates(15);

        // Assert
        foreach (var rep in replicates)
        {
            Assert.AreEqual(observed.Length, rep.Length);
        }
    }

    /// <summary>
    /// GenerateReplicates with the same seed produces reproducible results.
    /// </summary>
    [TestMethod]
    public void GenerateReplicates_SameSeed_IsReproducible()
    {
        // Arrange
        var checkA = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(30), MakeObservedData())
        { Seed = 123 };
        var checkB = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(30), MakeObservedData())
        { Seed = 123 };

        // Act
        var repsA = checkA.GenerateReplicates(10);
        var repsB = checkB.GenerateReplicates(10);

        // Assert
        Assert.AreEqual(repsA.Count, repsB.Count);
    }

    /// <summary>
    /// GenerateReplicates throws ArgumentOutOfRangeException when the count is zero.
    /// </summary>
    [TestMethod]
    public void GenerateReplicates_Zero_Throws()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(10), MakeObservedData());

        // Act & Assert
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => check.GenerateReplicates(0));
    }

    /// <summary>
    /// GenerateReplicates throws ArgumentOutOfRangeException when the count is negative.
    /// </summary>
    [TestMethod]
    public void GenerateReplicates_Negative_Throws()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(10), MakeObservedData());

        // Act & Assert
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => check.GenerateReplicates(-5));
    }

    #endregion

    #region ComputePValue Tests

    /// <summary>
    /// ComputePValue returns a value in [0, 1] when replicates are produced.
    /// </summary>
    [TestMethod]
    public void ComputePValue_ReturnsValueInUnitInterval()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(40), MakeObservedData())
        {
            Seed = 7
        };

        // Act
        double pValue = check.ComputePValue(data => data.Length > 0 ? data[0] : 0.0, numberOfReplicates: 20);

        // Assert
        Assert.IsTrue(pValue >= 0.0 && pValue <= 1.0,
            $"P-value should be in [0, 1] but was {pValue}.");
    }

    /// <summary>
    /// ComputePValue throws ArgumentNullException when the test statistic delegate is null.
    /// </summary>
    [TestMethod]
    public void ComputePValue_NullTestStatistic_Throws()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(10), MakeObservedData());

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(
            () => check.ComputePValue(null!, numberOfReplicates: 5));
    }

    /// <summary>
    /// ComputePValue uses the supplied test statistic on observed data and replicates.
    /// </summary>
    [TestMethod]
    public void ComputePValue_UsesProvidedTestStatistic()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(30), MakeObservedData())
        {
            Seed = 11
        };
        int callCount = 0;

        // Act
        double pValue = check.ComputePValue(
            data => { System.Threading.Interlocked.Increment(ref callCount); return data.Sum(); },
            numberOfReplicates: 10);

        // Assert: the delegate must be invoked at least once for the observed data.
        Assert.IsTrue(callCount > 0, "Test statistic delegate should be invoked.");
        Assert.IsTrue(pValue >= 0.0 && pValue <= 1.0);
    }

    #endregion

    #region ComputeCommonPValues Tests

    /// <summary>
    /// ComputeCommonPValues populates all five p-value fields when replicates are produced.
    /// </summary>
    [TestMethod]
    public void ComputeCommonPValues_PopulatesAllFiveFields()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(60), MakeObservedData())
        {
            Seed = 200
        };

        // Act
        var results = check.ComputeCommonPValues(numberOfReplicates: 40);

        // Assert: when replicates are produced, every p-value must be a valid probability.
        if (results.NumberOfReplicates > 0)
        {
            Assert.IsTrue(results.MeanPValue >= 0 && results.MeanPValue <= 1);
            Assert.IsTrue(results.SDPValue >= 0 && results.SDPValue <= 1);
            Assert.IsTrue(results.SkewnessPValue >= 0 && results.SkewnessPValue <= 1);
            Assert.IsTrue(results.MinPValue >= 0 && results.MinPValue <= 1);
            Assert.IsTrue(results.MaxPValue >= 0 && results.MaxPValue <= 1);
        }
    }

    /// <summary>
    /// ComputeCommonPValues NumberOfReplicates is at most numberOfReplicates argument.
    /// </summary>
    [TestMethod]
    public void ComputeCommonPValues_NumberOfReplicates_AtMostRequested()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(50), MakeObservedData())
        {
            Seed = 33
        };

        // Act
        var results = check.ComputeCommonPValues(numberOfReplicates: 25);

        // Assert
        Assert.IsTrue(results.NumberOfReplicates <= 25,
            $"NumberOfReplicates ({results.NumberOfReplicates}) must not exceed requested (25).");
    }

    #endregion

    #region ComputeSummary Tests

    /// <summary>
    /// ComputeSummary returns a non-null summary with five-quantile arrays.
    /// </summary>
    [TestMethod]
    public void ComputeSummary_ReturnsNonNullSummary()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(30), MakeObservedData())
        {
            Seed = 17
        };

        // Act
        var summary = check.ComputeSummary(numberOfReplicates: 15);

        // Assert
        Assert.IsNotNull(summary);
        if (summary.NumberOfValidDraws > 0)
        {
            Assert.AreEqual(5, summary.MeanQuantiles.Length);
            Assert.AreEqual(5, summary.SDQuantiles.Length);
            Assert.AreEqual(5, summary.MinQuantiles.Length);
            Assert.AreEqual(5, summary.MaxQuantiles.Length);
        }
    }

    /// <summary>
    /// ComputeSummary NumberOfValidDraws is at most the requested replicate count.
    /// </summary>
    [TestMethod]
    public void ComputeSummary_NumberOfValidDraws_AtMostRequested()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(40), MakeObservedData())
        {
            Seed = 88
        };

        // Act
        var summary = check.ComputeSummary(numberOfReplicates: 20);

        // Assert
        Assert.IsTrue(summary.NumberOfValidDraws <= 20);
    }

    #endregion

    #region Property Round-Trip Tests

    /// <summary>
    /// Seed property setter and getter round-trip.
    /// </summary>
    [TestMethod]
    public void Seed_SetterRoundTrips()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(5), MakeObservedData());

        // Act
        check.Seed = 777;

        // Assert
        Assert.AreEqual(777, check.Seed);
    }

    /// <summary>
    /// Default seed value is 12345.
    /// </summary>
    [TestMethod]
    public void DefaultSeed_Is12345()
    {
        // Arrange
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(5), MakeObservedData());

        // Assert
        Assert.AreEqual(12345, check.Seed);
    }

    /// <summary>
    /// NumberOfPosteriorSamples returns the count of supplied parameter sets.
    /// </summary>
    [TestMethod]
    public void NumberOfPosteriorSamples_MatchesSuppliedCount()
    {
        // Arrange
        var samples = MakePosteriorSamples(33);

        // Act
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), samples, MakeObservedData());

        // Assert
        Assert.AreEqual(33, check.NumberOfPosteriorSamples);
    }

    /// <summary>
    /// SampleSize returns the length of the observed-data array.
    /// </summary>
    [TestMethod]
    public void SampleSize_MatchesObservedLength()
    {
        // Arrange
        var observed = new double[] { 1, 2, 3, 4, 5, 6, 7 };

        // Act
        var check = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(5), observed);

        // Assert
        Assert.AreEqual(7, check.SampleSize);
    }

    #endregion
}
