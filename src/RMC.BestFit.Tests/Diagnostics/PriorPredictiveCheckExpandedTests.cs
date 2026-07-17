using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Expanded unit tests for the <c>PriorPredictiveCheck</c> class that
/// exercise <c>PriorPredictiveCheck.SampleFromPriors</c>,
/// <c>PriorPredictiveCheck.GeneratePriorPredictive</c>, and
/// <c>PriorPredictiveCheck.ComputeSummary</c> code paths.
/// </summary>
/// <remarks>
/// These tests are programmatic, not computational verification. They use a
/// <c>UnivariateDistribution</c> with fixed inline data and never invoke
/// any estimator (MLE, MAP, GMM, MCMC). The methods under test sample from priors
/// and generate replicates, which are pure simulation operations supported by
/// the <c>ISimulatable{T}</c> interface — not estimation.
/// Computational verification tests (e.g., comparing replicate distributions to
/// theoretical ones) belong in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class PriorPredictiveCheckExpandedTests
{
    /// <summary>
    /// Creates a small, well-defined Normal model for use across tests.
    /// </summary>
    /// <returns>A configured <c>UnivariateDistribution</c> with a Normal distribution.</returns>
    private static UnivariateDistribution MakeNormalModel()
    {
        var df = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(
                new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 })
        };
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    #region SampleFromPriors Tests

    /// <summary>
    /// SampleFromPriors returns the requested number of parameter sets.
    /// </summary>
    [TestMethod]
    public void SampleFromPriors_ReturnsRequestedNumberOfDraws()
    {
        // Arrange
        var model = MakeNormalModel();
        var check = new PriorPredictiveCheck(model) { NumberOfDraws = 50, Seed = 42 };

        // Act
        var samples = check.SampleFromPriors();

        // Assert
        Assert.AreEqual(50, samples.Count);
    }

    /// <summary>
    /// SampleFromPriors with a fixed seed produces reproducible results.
    /// </summary>
    [TestMethod]
    public void SampleFromPriors_FixedSeed_IsReproducible()
    {
        // Arrange
        var checkA = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 30, Seed = 12345 };
        var checkB = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 30, Seed = 12345 };

        // Act
        var samplesA = checkA.SampleFromPriors();
        var samplesB = checkB.SampleFromPriors();

        // Assert
        Assert.AreEqual(samplesA.Count, samplesB.Count);
        for (int i = 0; i < samplesA.Count; i++)
        {
            for (int j = 0; j < samplesA[i].Values.Length; j++)
            {
                Assert.AreEqual(samplesA[i].Values[j], samplesB[i].Values[j], 1e-12,
                    $"Sample {i} parameter {j} should match across reproducible runs.");
            }
        }
    }

    /// <summary>
    /// SampleFromPriors returns parameter sets with the model's parameter count.
    /// </summary>
    [TestMethod]
    public void SampleFromPriors_ReturnsValuesMatchingParameterCount()
    {
        // Arrange
        var model = MakeNormalModel();
        var check = new PriorPredictiveCheck(model) { NumberOfDraws = 5, Seed = 1 };

        // Act
        var samples = check.SampleFromPriors();

        // Assert
        int expectedCount = model.Parameters.Count;
        foreach (var sample in samples)
        {
            Assert.AreEqual(expectedCount, sample.Values.Length);
        }
    }

    /// <summary>
    /// SampleFromPriors with different seeds produces different draws.
    /// </summary>
    [TestMethod]
    public void SampleFromPriors_DifferentSeeds_ProduceDifferentDraws()
    {
        // Arrange
        var checkA = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 20, Seed = 1 };
        var checkB = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 20, Seed = 2 };

        // Act
        var samplesA = checkA.SampleFromPriors();
        var samplesB = checkB.SampleFromPriors();

        // Assert: at least one parameter must differ
        bool anyDifferent = false;
        for (int i = 0; i < samplesA.Count && !anyDifferent; i++)
        {
            for (int j = 0; j < samplesA[i].Values.Length && !anyDifferent; j++)
            {
                if (Math.Abs(samplesA[i].Values[j] - samplesB[i].Values[j]) > 1e-10)
                    anyDifferent = true;
            }
        }
        Assert.IsTrue(anyDifferent, "Different seeds should yield at least one different parameter value.");
    }

    /// <summary>
    /// SampleFromPriors clamps sampled values to parameter bounds.
    /// </summary>
    [TestMethod]
    public void SampleFromPriors_ClampsToParameterBounds()
    {
        // Arrange
        var model = MakeNormalModel();
        var check = new PriorPredictiveCheck(model) { NumberOfDraws = 100, Seed = 7 };

        // Act
        var samples = check.SampleFromPriors();

        // Assert: every sampled value should respect lower/upper bounds
        for (int i = 0; i < samples.Count; i++)
        {
            for (int j = 0; j < model.Parameters.Count; j++)
            {
                var p = model.Parameters[j];
                Assert.IsTrue(samples[i].Values[j] >= p.LowerBound,
                    $"Sample {i} parameter {j} ({samples[i].Values[j]}) below lower bound ({p.LowerBound}).");
                Assert.IsTrue(samples[i].Values[j] <= p.UpperBound,
                    $"Sample {i} parameter {j} ({samples[i].Values[j]}) above upper bound ({p.UpperBound}).");
            }
        }
    }

    #endregion

    #region GeneratePriorPredictive Tests

    /// <summary>
    /// GeneratePriorPredictive produces datasets of the requested sample size.
    /// </summary>
    [TestMethod]
    public void GeneratePriorPredictive_ProducesDatasetsOfRequestedSize()
    {
        // Arrange
        var model = MakeNormalModel();
        var check = new PriorPredictiveCheck(model) { NumberOfDraws = 20, Seed = 100 };

        // Act
        var datasets = check.GeneratePriorPredictive(sampleSize: 25);

        // Assert: every successfully generated dataset has the requested length
        foreach (var d in datasets)
        {
            Assert.AreEqual(25, d.Length);
        }
    }

    /// <summary>
    /// GeneratePriorPredictive returns at most NumberOfDraws datasets.
    /// </summary>
    [TestMethod]
    public void GeneratePriorPredictive_DatasetCount_DoesNotExceedNumberOfDraws()
    {
        // Arrange
        var model = MakeNormalModel();
        var check = new PriorPredictiveCheck(model) { NumberOfDraws = 15, Seed = 5 };

        // Act
        var datasets = check.GeneratePriorPredictive(sampleSize: 10);

        // Assert
        Assert.IsTrue(datasets.Count <= 15);
    }

    /// <summary>
    /// GeneratePriorPredictive throws ArgumentOutOfRangeException when sampleSize is zero.
    /// </summary>
    [TestMethod]
    public void GeneratePriorPredictive_SampleSizeZero_Throws()
    {
        // Arrange
        var check = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 5, Seed = 1 };

        // Act & Assert
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => check.GeneratePriorPredictive(0));
    }

    /// <summary>
    /// GeneratePriorPredictive throws ArgumentOutOfRangeException when sampleSize is negative.
    /// </summary>
    [TestMethod]
    public void GeneratePriorPredictive_NegativeSampleSize_Throws()
    {
        // Arrange
        var check = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 5, Seed = 1 };

        // Act & Assert
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => check.GeneratePriorPredictive(-1));
    }

    /// <summary>
    /// GeneratePriorPredictive with the same seed produces reproducible result counts.
    /// </summary>
    [TestMethod]
    public void GeneratePriorPredictive_SameSeed_ProducesReproducibleResults()
    {
        // Arrange
        var checkA = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 20, Seed = 999 };
        var checkB = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 20, Seed = 999 };

        // Act
        var datasetsA = checkA.GeneratePriorPredictive(sampleSize: 5);
        var datasetsB = checkB.GeneratePriorPredictive(sampleSize: 5);

        // Assert
        Assert.AreEqual(datasetsA.Count, datasetsB.Count,
            "Same seed should produce same number of valid replicates.");
    }

    #endregion

    #region ComputeSummary Tests

    /// <summary>
    /// ComputeSummary returns a non-null PredictiveSummary instance.
    /// </summary>
    [TestMethod]
    public void ComputeSummary_ReturnsNonNullPredictiveSummary()
    {
        // Arrange
        var check = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 20, Seed = 50 };

        // Act
        var summary = check.ComputeSummary(sampleSize: 10);

        // Assert
        Assert.IsNotNull(summary);
    }

    /// <summary>
    /// ComputeSummary returns NumberOfValidDraws &lt;= NumberOfDraws.
    /// </summary>
    [TestMethod]
    public void ComputeSummary_NumberOfValidDraws_AtMostNumberOfDraws()
    {
        // Arrange
        var check = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 30, Seed = 50 };

        // Act
        var summary = check.ComputeSummary(sampleSize: 8);

        // Assert
        Assert.IsTrue(summary.NumberOfValidDraws <= 30,
            $"NumberOfValidDraws ({summary.NumberOfValidDraws}) should not exceed NumberOfDraws (30).");
    }

    /// <summary>
    /// ComputeSummary returns five-quantile arrays (2.5%, 25%, 50%, 75%, 97.5%) when there are valid draws.
    /// </summary>
    [TestMethod]
    public void ComputeSummary_QuantileArraysHaveFiveEntries()
    {
        // Arrange
        var check = new PriorPredictiveCheck(MakeNormalModel()) { NumberOfDraws = 50, Seed = 22 };

        // Act
        var summary = check.ComputeSummary(sampleSize: 10);

        // Assert: when there are valid draws the quantile arrays must be length 5
        if (summary.NumberOfValidDraws > 0)
        {
            Assert.AreEqual(5, summary.MeanQuantiles.Length);
            Assert.AreEqual(5, summary.SDQuantiles.Length);
            Assert.AreEqual(5, summary.MinQuantiles.Length);
            Assert.AreEqual(5, summary.MaxQuantiles.Length);
        }
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Model property returns the same instance passed to the constructor.
    /// </summary>
    [TestMethod]
    public void Model_ReturnsSameInstancePassedToConstructor()
    {
        // Arrange
        var model = MakeNormalModel();

        // Act
        var check = new PriorPredictiveCheck(model);

        // Assert
        Assert.AreSame(model, check.Model);
    }

    /// <summary>
    /// Setting NumberOfDraws to one is allowed (boundary).
    /// </summary>
    [TestMethod]
    public void NumberOfDraws_SetToOne_DoesNotThrow()
    {
        // Arrange
        var check = new PriorPredictiveCheck(MakeNormalModel());

        // Act
        check.NumberOfDraws = 1;

        // Assert
        Assert.AreEqual(1, check.NumberOfDraws);
    }

    /// <summary>
    /// Constructor throws ArgumentException when the model does not implement <c>ISimulatable{T}</c>.
    /// </summary>
    [TestMethod]
    public void Constructor_NonSimulatableModel_Throws()
    {
        // Arrange
        var nonSimulatableModel = new NonSimulatableModelStub();

        // Act & Assert
        Assert.ThrowsException<ArgumentException>(
            () => new PriorPredictiveCheck(nonSimulatableModel));
    }

    #endregion

    /// <summary>
    /// Lightweight stub of <c>IModel</c> that does NOT implement
    /// <c>ISimulatable{T}</c>; used only to test the constructor's
    /// type guard.
    /// </summary>
    private sealed class NonSimulatableModelStub : IModel
    {
        /// <summary>Backing field for the parameter list.</summary>
        private readonly List<ModelParameter> _parameters = new();

        /// <inheritdoc/>
        public List<ModelParameter> Parameters => _parameters;

        /// <inheritdoc/>
        public int NumberOfParameters => 0;

        /// <inheritdoc/>
        public bool UseDefaultFlatPriors { get; set; }

        /// <inheritdoc/>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc/>
        public double LogLikelihood(double[] parameters) => 0;

        /// <inheritdoc/>
        public double DataLogLikelihood(double[] parameters) => 0;

        /// <inheritdoc/>
        public double[] PointwiseDataLogLikelihood(double[] parameters) => Array.Empty<double>();

        /// <inheritdoc/>
        public List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters) => new();

        /// <inheritdoc/>
        public double PriorLogLikelihood(double[] parameters) => 0;

        /// <inheritdoc/>
        public List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters) => new();

        /// <inheritdoc/>
        public void SetParameterValues(IList<double> parameters) { }

        /// <inheritdoc/>
        public void SetDefaultParameters() { }

        /// <inheritdoc/>
        public IModel Clone() => new NonSimulatableModelStub();

        /// <inheritdoc/>
        public System.Xml.Linq.XElement ToXElement() => new("Stub");

        /// <inheritdoc/>
        public (bool IsValid, List<string> ValidationMessages) Validate() => (true, new List<string>());

        /// <summary>
        /// Suppresses the unused-event warning by a no-op invoker; the test class
        /// never raises this event but the interface requires it to be present.
        /// </summary>
        private void RaisePropertyChanged() => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(""));
    }
}
