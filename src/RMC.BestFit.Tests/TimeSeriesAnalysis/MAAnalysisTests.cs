using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.TimeSeriesAnalysis;

/// <summary>
/// Programmatic unit tests for the <see cref="MAAnalysis"/> wrapper.
/// </summary>
/// <remarks>
/// <para>
/// Covers constructors, configuration, validation, serialization round-trip, and
/// property-change semantics. Estimation-driven tests (Bayesian MCMC parameter
/// recovery, R parity) live in <c>RMC.BestFit.Verification</c>.
/// </para>
/// </remarks>
[TestClass]
public class MAAnalysisTests
{
    #region Inline test fixtures

    /// <summary>
    /// Deterministic 60-observation annual streamflow fixture with MA(1) structure.
    /// Mean ≈ 5000, theta ≈ 0.5, sigma ≈ 600.
    /// </summary>
    private static TimeSeries CreateAnnualStreamflowTimeSeries()
    {
        var ts = new TimeSeries(TimeInterval.OneYear, new DateTime(1960, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(12345);

        double mean = 5000;
        double theta = 0.5;
        double sigma = 600;

        var epsilon = new double[ts.Count + 1];
        for (int i = 0; i <= ts.Count; i++)
        {
            epsilon[i] = (rng.NextDouble() * 2 - 1) * sigma;
        }

        for (int i = 0; i < ts.Count; i++)
        {
            double value = mean + epsilon[i + 1];
            if (i > 0)
            {
                value += theta * epsilon[i];
            }
            ts[i].Value = Math.Max(100, value);
        }

        return ts;
    }

    /// <summary>
    /// Deterministic short fixture (15 observations) for edge-case validation.
    /// </summary>
    private static TimeSeries CreateShortTimeSeries()
    {
        var ts = new TimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), new DateTime(2014, 1, 1));
        var rng = new Random(54321);

        double mean = 3000;
        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = mean + (rng.NextDouble() * 2 - 1) * 500;
        }
        return ts;
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the MAAnalysis constructor properly initializes with a valid MA model.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_ValidModel_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts, order: 1);

        var analysis = new MAAnalysis(model);

        Assert.IsNotNull(analysis);
        Assert.IsNotNull(analysis.MovingAverage);
        Assert.IsNotNull(analysis.BayesianAnalysis);
        Assert.AreSame(model, analysis.MovingAverage);
    }

    /// <summary>
    /// Tests that the constructor throws ArgumentNullException when model is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullModel_ThrowsArgumentNullException()
    {
        var analysis = new MAAnalysis(null!);
    }

    /// <summary>
    /// Tests that BayesianAnalysis has the correct model reference.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_BayesianAnalysis_HasCorrectModel()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts, order: 1);

        var analysis = new MAAnalysis(model);

        Assert.AreSame(model, analysis.BayesianAnalysis.Model);
    }

    /// <summary>
    /// Tests that ForecastingTimeSteps defaults to 0.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_ForecastingTimeSteps_DefaultsToZero()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);

        var analysis = new MAAnalysis(model);

        Assert.AreEqual(0, analysis.ForecastingTimeSteps);
    }

    /// <summary>
    /// Tests that IsEstimated is false initially.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_IsEstimated_IsFalseInitially()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);

        var analysis = new MAAnalysis(model);

        Assert.IsFalse(analysis.IsEstimated);
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>
    /// Tests that XML round-trip preserves configuration.
    /// </summary>
    [TestMethod]
    public void Test_XmlSerialization_RoundTrip_PreservesConfiguration()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts, order: 2);
        var original = new MAAnalysis(model);
        original.ForecastingTimeSteps = 10;
        original.BayesianAnalysis.Iterations = 5000;
        original.BayesianAnalysis.WarmupIterations = 2500;

        var xElement = original.ToXElement();
        var restored = new MAAnalysis(model, xElement);

        Assert.AreEqual(original.ForecastingTimeSteps, restored.ForecastingTimeSteps);
    }

    /// <summary>
    /// Tests that null XElement throws ArgumentNullException.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_XmlSerialization_NullXElement_ThrowsArgumentNullException()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);

        var analysis = new MAAnalysis(model, null!);
    }

    /// <summary>
    /// Tests that ToXElement creates valid structure.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_CreatesValidStructure()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        var xElement = analysis.ToXElement();

        Assert.AreEqual("MAAnalysis", xElement.Name.LocalName);
        Assert.IsNotNull(xElement.Attribute("ForecastingTimeSteps"));
    }

    /// <summary>
    /// Tests that XML serialization preserves Bayesian settings.
    /// </summary>
    [TestMethod]
    public void Test_XmlSerialization_PreservesBayesianSettings()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var original = new MAAnalysis(model);
        original.BayesianAnalysis.Iterations = 8000;
        original.BayesianAnalysis.WarmupIterations = 4000;
        original.BayesianAnalysis.ThinningInterval = 5;

        var xElement = original.ToXElement();
        var restored = new MAAnalysis(model, xElement);

        Assert.AreEqual(original.BayesianAnalysis.Iterations, restored.BayesianAnalysis.Iterations);
        Assert.AreEqual(original.BayesianAnalysis.WarmupIterations, restored.BayesianAnalysis.WarmupIterations);
        Assert.AreEqual(original.BayesianAnalysis.ThinningInterval, restored.BayesianAnalysis.ThinningInterval);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Tests that valid configuration passes validation.
    /// </summary>
    [TestMethod]
    public void Test_Validate_ValidConfiguration_ReturnsTrue()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts, order: 1);
        var analysis = new MAAnalysis(model);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that analysis propagates model validation.
    /// </summary>
    [TestMethod]
    public void Test_Validate_InvalidModel_PropagatesModelValidation()
    {
        var model = new MovingAverage(); // No time series
        var analysis = new MAAnalysis(model);

        var (isValid, messages) = analysis.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that negative forecasting steps get handled appropriately.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NegativeForecastingSteps_HandledCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);
        analysis.ForecastingTimeSteps = -5;

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(analysis.ForecastingTimeSteps >= 0 || !isValid);
    }

    #endregion

    #region ClearResults Tests

    /// <summary>
    /// Tests that ClearResults resets all results.
    /// </summary>
    [TestMethod]
    public void Test_ClearResults_ResetsAllResults()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated);
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// Tests that changing ForecastingTimeSteps raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void Test_PropertyChange_ForecastingTimeSteps_RaisesPropertyChanged()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MAAnalysis.ForecastingTimeSteps))
                propertyChanged = true;
        };

        analysis.ForecastingTimeSteps = 15;

        Assert.IsTrue(propertyChanged);
    }

    /// <summary>
    /// Tests that same value doesn't raise PropertyChanged.
    /// </summary>
    [TestMethod]
    public void Test_PropertyChange_SameValue_DoesNotRaisePropertyChanged()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);
        analysis.ForecastingTimeSteps = 10;

        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MAAnalysis.ForecastingTimeSteps))
                propertyChanged = true;
        };

        analysis.ForecastingTimeSteps = 10; // Same value

        Assert.IsFalse(propertyChanged);
    }

    #endregion

    #region BayesianAnalysis Configuration Tests

    /// <summary>
    /// Tests that Iterations can be configured.
    /// </summary>
    [TestMethod]
    public void Test_BayesianAnalysis_Iterations_CanBeConfigure()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        analysis.BayesianAnalysis.Iterations = 10000;

        Assert.AreEqual(10000, analysis.BayesianAnalysis.Iterations);
    }

    /// <summary>
    /// Tests that WarmupIterations can be configured.
    /// </summary>
    [TestMethod]
    public void Test_BayesianAnalysis_WarmupIterations_CanBeConfigure()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        analysis.BayesianAnalysis.WarmupIterations = 5000;

        Assert.AreEqual(5000, analysis.BayesianAnalysis.WarmupIterations);
    }

    /// <summary>
    /// Tests that ThinningInterval can be configured.
    /// </summary>
    [TestMethod]
    public void Test_BayesianAnalysis_ThinningInterval_CanBeConfigure()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        analysis.BayesianAnalysis.ThinningInterval = 10;

        Assert.AreEqual(10, analysis.BayesianAnalysis.ThinningInterval);
    }

    /// <summary>
    /// Tests that PointEstimator can be set to PosteriorMean.
    /// </summary>
    [TestMethod]
    public void Test_BayesianAnalysis_PointEstimator_PosteriorMean()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMean;

        Assert.AreEqual(RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMean, analysis.BayesianAnalysis.PointEstimator);
    }

    /// <summary>
    /// Tests that PointEstimator can be set to PosteriorMode.
    /// </summary>
    [TestMethod]
    public void Test_BayesianAnalysis_PointEstimator_PosteriorMode()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;

        Assert.AreEqual(RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode, analysis.BayesianAnalysis.PointEstimator);
    }

    #endregion

    #region Model Property Reference Tests

    /// <summary>
    /// Tests that MovingAverage property returns correct model reference.
    /// </summary>
    [TestMethod]
    public void Test_MovingAverage_ReturnsCorrectModelReference()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts, order: 2);

        var analysis = new MAAnalysis(model);

        Assert.AreSame(model, analysis.MovingAverage);
        Assert.AreEqual(2, analysis.MovingAverage.Order);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests analysis with minimal data.
    /// </summary>
    [TestMethod]
    public void Test_EdgeCase_MinimalData()
    {
        var ts = CreateShortTimeSeries();
        var model = new MovingAverage(ts, order: 1);
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var analysis = new MAAnalysis(model);

        var (isValid, _) = analysis.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests analysis with higher order MA model.
    /// </summary>
    [TestMethod]
    public void Test_EdgeCase_HigherOrderMA()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts, order: 5);

        var analysis = new MAAnalysis(model);

        var (isValid, _) = analysis.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests analysis without intercept.
    /// </summary>
    [TestMethod]
    public void Test_EdgeCase_NoIntercept()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts, order: 1, includeIntercept: false);

        var analysis = new MAAnalysis(model);

        var (isValid, _) = analysis.Validate();
        Assert.IsTrue(isValid);
    }

    #endregion

    #region ForecastingTimeSteps Tests

    /// <summary>
    /// Tests setting forecasting time steps.
    /// </summary>
    [TestMethod]
    public void Test_ForecastingTimeSteps_SetAndGet()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        analysis.ForecastingTimeSteps = 20;

        Assert.AreEqual(20, analysis.ForecastingTimeSteps);
    }

    /// <summary>
    /// Tests that reasonable forecasting steps are accepted.
    /// </summary>
    [TestMethod]
    public void Test_ForecastingTimeSteps_ReasonableValue_Accepted()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var model = new MovingAverage(ts);
        var analysis = new MAAnalysis(model);

        analysis.ForecastingTimeSteps = 30;

        var (isValid, _) = analysis.Validate();
        Assert.IsTrue(isValid);
    }

    #endregion
}
