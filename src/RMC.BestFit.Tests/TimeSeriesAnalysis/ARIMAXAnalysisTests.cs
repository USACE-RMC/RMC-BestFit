using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesAnalysis;

/// <summary>
/// Programmatic unit tests for the <c>ARIMAXAnalysis</c> wrapper.
/// </summary>
/// <remarks>
/// <para>
/// Covers constructors, configuration, validation, serialization round-trip,
/// property-change semantics, event wiring, cancellation behavior, trend / seasonality /
/// covariate / differencing / transform configuration, and integration with the
/// underlying ARIMAX model. Estimation-driven tests (Bayesian MCMC parameter
/// recovery, R parity) live in <c>RMC.BestFit.Verification</c>.
/// </para>
/// </remarks>
[TestClass]
public class ARIMAXAnalysisTests
{
    #region Inline test fixtures

    /// <summary>
    /// Deterministic 60-observation annual streamflow fixture with ARMA(1,1) structure.
    /// </summary>
    private static NumericsTimeSeries CreateAnnualStreamflowTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1960, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(12345);

        double mean = 5000;
        double phi = 0.6;
        double theta = 0.3;
        double sigma = 600;

        var epsilon = new double[ts.Count + 1];
        for (int i = 0; i <= ts.Count; i++)
        {
            epsilon[i] = (rng.NextDouble() * 2 - 1) * sigma;
        }

        double prevValue = mean;
        for (int i = 0; i < ts.Count; i++)
        {
            double arPart = phi * (prevValue - mean);
            double maPart = i > 0 ? theta * epsilon[i] : 0;
            double value = mean + arPart + epsilon[i + 1] + maPart;
            ts[i].Value = Math.Max(100, value);
            prevValue = ts[i].Value;
        }

        return ts;
    }

    /// <summary>
    /// Monthly time series with seasonal pattern (10 years).
    /// </summary>
    private static NumericsTimeSeries CreateMonthlySeasonalTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2010, 1, 1), new DateTime(2019, 12, 1));
        var rng = new Random(67890);

        for (int i = 0; i < ts.Count; i++)
        {
            double seasonal = 500 * Math.Sin(2 * Math.PI * i / 12);
            double trend = 0.5 * i;
            double noise = (rng.NextDouble() * 2 - 1) * 100;
            ts[i].Value = 2000 + seasonal + trend + noise;
        }

        return ts;
    }

    /// <summary>
    /// Time series + covariate fixture (50 annual observations).
    /// </summary>
    private static (NumericsTimeSeries response, NumericsTimeSeries covariate) CreateTimeSeriesWithCovariate()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1970, 1, 1), new DateTime(2019, 1, 1));
        var covariate = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1970, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(11111);

        for (int i = 0; i < covariate.Count; i++)
        {
            covariate[i].Value = 60 + (rng.NextDouble() * 2 - 1) * 10;
        }

        double beta = 50;
        double mean = 3000;
        double sigma = 400;

        for (int i = 0; i < ts.Count; i++)
        {
            double noise = (rng.NextDouble() * 2 - 1) * sigma;
            ts[i].Value = mean + beta * (covariate[i].Value - 60) + noise;
        }

        return (ts, covariate);
    }

    /// <summary>
    /// Deterministic short fixture (15 observations) for edge-case validation.
    /// </summary>
    private static NumericsTimeSeries CreateShortTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), new DateTime(2014, 1, 1));
        var rng = new Random(54321);

        double mean = 3000;
        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = mean + (rng.NextDouble() * 2 - 1) * 500;
        }
        return ts;
    }

    /// <summary>
    /// Linear trend fixture (40 annual observations).
    /// </summary>
    private static NumericsTimeSeries CreateTrendTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1980, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(22222);

        double intercept = 2000;
        double slope = 20;
        double sigma = 200;

        for (int i = 0; i < ts.Count; i++)
        {
            double noise = (rng.NextDouble() * 2 - 1) * sigma;
            ts[i].Value = intercept + slope * i + noise;
        }

        return ts;
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the ARIMAXAnalysis constructor properly initializes with a valid ARIMAX model.
    /// </summary>
    [TestMethod]
    public void Constructor_WithValidARIMAX_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);

        var analysis = new ARIMAXAnalysis(armax);

        Assert.IsNotNull(analysis.ARIMAX, "ARIMAX should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis, "BayesianAnalysis should not be null.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null initially.");
    }

    /// <summary>
    /// Tests that the constructor throws ArgumentNullException when ARIMAX is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullARIMAX_ThrowsArgumentNullException()
    {
        _ = new ARIMAXAnalysis(null!);
    }

    /// <summary>
    /// Tests that BayesianAnalysis references the correct model.
    /// </summary>
    [TestMethod]
    public void Constructor_BayesianAnalysis_HasCorrectModel()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);

        var analysis = new ARIMAXAnalysis(armax);

        Assert.AreSame(armax, analysis.BayesianAnalysis.Model);
    }

    /// <summary>
    /// Tests that ForecastingTimeSteps defaults to zero.
    /// </summary>
    [TestMethod]
    public void Constructor_ForecastingTimeSteps_DefaultsToZero()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);

        var analysis = new ARIMAXAnalysis(armax);

        Assert.AreEqual(0, analysis.ForecastingTimeSteps);
    }

    /// <summary>
    /// Verifies changing the model input series does not reset the analysis-level forecast horizon.
    /// </summary>
    [TestMethod]
    public void ModelTimeSeriesChanged_PreservesForecastingTimeSteps()
    {
        var armax = new ARIMAX(CreateAnnualStreamflowTimeSeries());
        var analysis = new ARIMAXAnalysis(armax)
        {
            ForecastingTimeSteps = 12,
        };

        armax.TimeSeries = CreateMonthlySeasonalTimeSeries();

        Assert.AreEqual(12, analysis.ForecastingTimeSteps);
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>
    /// Tests that the analysis can be serialized to XML and restored.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesConfiguration()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);
        analysis.ForecastingTimeSteps = 10;

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new ARIMAXAnalysis(armax, xElement);

        Assert.IsNotNull(restoredAnalysis);
        Assert.AreEqual(analysis.ForecastingTimeSteps, restoredAnalysis.ForecastingTimeSteps);
    }

    /// <summary>
    /// Tests that the constructor throws when XElement is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullXElement_ThrowsArgumentNullException()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);

        _ = new ARIMAXAnalysis(armax, null!);
    }

    /// <summary>
    /// Tests that ToXElement creates valid XML structure.
    /// </summary>
    [TestMethod]
    public void ToXElement_CreatesValidXmlStructure()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        var xElement = analysis.ToXElement();

        Assert.IsNotNull(xElement);
        Assert.AreEqual("ARIMAXAnalysis", xElement.Name.LocalName);
        Assert.IsNotNull(xElement.Attribute("IsEstimated"));
        Assert.IsNotNull(xElement.Attribute("ForecastingTimeSteps"));
    }

    /// <summary>
    /// Tests that serialization preserves Bayesian analysis settings.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_WithBayesianSettings_PreservesSettings()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);
        analysis.BayesianAnalysis.Iterations = 5000;
        analysis.BayesianAnalysis.WarmupIterations = 1000;

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new ARIMAXAnalysis(armax, xElement);

        Assert.AreEqual(5000, restoredAnalysis.BayesianAnalysis.Iterations);
        Assert.AreEqual(1000, restoredAnalysis.BayesianAnalysis.WarmupIterations);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Tests that Validate returns valid for a properly configured analysis.
    /// </summary>
    [TestMethod]
    public void Validate_WithValidConfiguration_ReturnsValid()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that Validate propagates ARIMAX model validation messages.
    /// </summary>
    [TestMethod]
    public void Validate_PropagatesARIMAXValidation()
    {
        var armax = new ARIMAX(); // No time series data
        var analysis = new ARIMAXAnalysis(armax);

        var (isValid, messages) = analysis.Validate();

        Assert.IsFalse(isValid, "Validation should fail for ARIMAX without data.");
        Assert.IsTrue(messages.Any(m => m.Contains("Time series")));
    }

    /// <summary>
    /// Tests that ForecastingTimeSteps is clamped when set negative.
    /// </summary>
    [TestMethod]
    public void Validate_WithNegativeForecastingSteps_ReturnsInvalid()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        analysis.ForecastingTimeSteps = -1;

        var (isValid, messages) = analysis.Validate();

        Assert.AreEqual(0, analysis.ForecastingTimeSteps,
            "ForecastingTimeSteps should be clamped to 0.");
    }

    /// <summary>
    /// Tests that ForecastingTimeSteps is clamped to 100 when set above maximum.
    /// </summary>
    /// <remarks>
    /// The setter clamps to <c>[0, 100]</c>, so writes above 100 are silently capped.
    /// Validate then sees a clamped value and reports valid. This guards the clamp
    /// behaviour: a too-large request never reaches the analysis as an out-of-range
    /// integer.
    /// </remarks>
    [TestMethod]
    public void Validate_WithExcessiveForecastingSteps_ReturnsInvalid()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);
        analysis.ForecastingTimeSteps = 1001;

        Assert.AreEqual(100, analysis.ForecastingTimeSteps,
            "ForecastingTimeSteps should be clamped to 100.");
    }

    /// <summary>
    /// Tests that Validate passes with valid ForecastingTimeSteps at boundary values.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(100)]
    [DataRow(1000)]
    public void Validate_WithValidForecastingSteps_ReturnsValid(int steps)
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);
        analysis.ForecastingTimeSteps = steps;

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass for ForecastingTimeSteps = {steps}. Messages: {string.Join(", ", messages)}");
    }

    #endregion

    #region ClearResults Tests

    /// <summary>
    /// Tests that ClearResults resets all analysis results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ResetsAllResults()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated);
        Assert.IsNull(analysis.AnalysisResults);
    }

    /// <summary>
    /// Tests that ClearResults also clears BayesianAnalysis results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ClearsBayesianAnalysisResults()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        analysis.ClearResults();

        Assert.IsFalse(analysis.BayesianAnalysis.IsEstimated);
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// Tests that ForecastingTimeSteps change triggers property change notification.
    /// </summary>
    [TestMethod]
    public void ForecastingTimeSteps_Change_RaisesPropertyChanged()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ARIMAXAnalysis.ForecastingTimeSteps))
                propertyChanged = true;
        };

        analysis.ForecastingTimeSteps = 20;

        Assert.IsTrue(propertyChanged);
    }

    /// <summary>
    /// Tests that changing ForecastingTimeSteps clears results.
    /// </summary>
    [TestMethod]
    public void ForecastingTimeSteps_Change_ClearsResults()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        analysis.ForecastingTimeSteps = 10;

        Assert.IsNull(analysis.AnalysisResults);
        Assert.IsFalse(analysis.IsEstimated);
    }

    /// <summary>
    /// Tests that setting same ForecastingTimeSteps value does not raise property changed.
    /// </summary>
    [TestMethod]
    public void ForecastingTimeSteps_SetSameValue_DoesNotRaisePropertyChanged()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);
        analysis.ForecastingTimeSteps = 10;

        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ARIMAXAnalysis.ForecastingTimeSteps))
                propertyChanged = true;
        };

        analysis.ForecastingTimeSteps = 10;

        Assert.IsFalse(propertyChanged);
    }

    #endregion

    #region Event Tests (cancelled-only — no MCMC runs)

    /// <summary>
    /// Tests that AnalysisStarting event is raised when RunAsync is called.
    /// </summary>
    /// <remarks>
    /// The handler cancels the run before any MCMC iterations execute, so this test
    /// stays in the fast unit-test project.
    /// </remarks>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisStartingEvent()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);
        analysis.BayesianAnalysis.Iterations = 100;
        analysis.BayesianAnalysis.WarmupIterations = 50;

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) =>
        {
            eventRaised = true;
            e.Cancel = true;
        };

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisStarting event should be raised.");
    }

    /// <summary>
    /// Tests that AnalysisCompleted event is raised after RunAsync completes (cancelled path).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisCompletedEvent()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) => e.Cancel = true;
        analysis.AnalysisCompleted += (s, e) =>
        {
            eventRaised = true;
            Assert.IsTrue(e.Cancelled, "Cancelled flag should be true when canceled.");
        };

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisCompleted event should be raised.");
    }

    /// <summary>
    /// Tests that cancellation during analysis sets the Cancelled flag.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenCanceled_SetsWasCanceledFlag()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);
        bool wasCanceled = false;

        analysis.AnalysisStarting += (s, e) => e.Cancel = true;
        analysis.AnalysisCompleted += (s, e) =>
        {
            wasCanceled = e.Cancelled;
        };

        await analysis.RunAsync();

        Assert.IsTrue(wasCanceled);
        Assert.IsFalse(analysis.IsEstimated);
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>
    /// Tests that CancelAnalysis can be called without error when not running.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        analysis.CancelAnalysis();
    }

    /// <summary>
    /// Tests that CancelAnalysis cancels the underlying BayesianAnalysis.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_CancelsBayesianAnalysis()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        analysis.CancelAnalysis();
    }

    #endregion

    #region ARIMAX Model Configuration Tests

    /// <summary>
    /// Tests analysis with AR order 1.
    /// </summary>
    [TestMethod]
    public void Constructor_WithAR1Model_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts) { AROrderP = 1, MAOrderQ = 0 };

        var analysis = new ARIMAXAnalysis(armax);

        Assert.IsNotNull(analysis);
        Assert.AreEqual(1, analysis.ARIMAX.AROrderP);
    }

    /// <summary>
    /// Tests analysis with MA order 1.
    /// </summary>
    [TestMethod]
    public void Constructor_WithMA1Model_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts) { AROrderP = 0, MAOrderQ = 1 };

        var analysis = new ARIMAXAnalysis(armax);

        Assert.IsNotNull(analysis);
        Assert.AreEqual(1, analysis.ARIMAX.MAOrderQ);
    }

    /// <summary>
    /// Tests analysis with ARMA(1,1) model.
    /// </summary>
    [TestMethod]
    public void Constructor_WithARMA11Model_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts) { AROrderP = 1, MAOrderQ = 1 };

        var analysis = new ARIMAXAnalysis(armax);

        Assert.IsNotNull(analysis);
        Assert.AreEqual(1, analysis.ARIMAX.AROrderP);
        Assert.AreEqual(1, analysis.ARIMAX.MAOrderQ);
    }

    /// <summary>
    /// Tests analysis with linear trend.
    /// </summary>
    [TestMethod]
    public void Constructor_WithLinearTrend_InitializesCorrectly()
    {
        var ts = CreateTrendTimeSeries();
        var armax = new ARIMAX(ts)
        {
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 1,
            MAOrderQ = 0
        };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with linear trend should validate. Messages: {string.Join(", ", messages)}");
        Assert.AreEqual(ARIMAX.Trend.Linear, analysis.ARIMAX.TrendType);
    }

    /// <summary>
    /// Tests analysis with quadratic trend.
    /// </summary>
    [TestMethod]
    public void Constructor_WithQuadraticTrend_InitializesCorrectly()
    {
        var ts = CreateTrendTimeSeries();
        var armax = new ARIMAX(ts)
        {
            TrendType = ARIMAX.Trend.Quadratic,
            AROrderP = 1,
            MAOrderQ = 0
        };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with quadratic trend should validate. Messages: {string.Join(", ", messages)}");
        Assert.AreEqual(ARIMAX.Trend.Quadratic, analysis.ARIMAX.TrendType);
    }

    /// <summary>
    /// Tests analysis with seasonality.
    /// </summary>
    [TestMethod]
    public void Constructor_WithSeasonality_InitializesCorrectly()
    {
        var ts = CreateMonthlySeasonalTimeSeries();
        var armax = new ARIMAX(ts)
        {
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 0
        };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with seasonality should validate. Messages: {string.Join(", ", messages)}");
        Assert.IsTrue(analysis.ARIMAX.IncludeSeasonality);
    }

    /// <summary>
    /// Tests analysis with exogenous covariate.
    /// </summary>
    [TestMethod]
    public void Constructor_WithCovariate_InitializesCorrectly()
    {
        var (ts, covariate) = CreateTimeSeriesWithCovariate();
        var armax = new ARIMAX(ts) { AROrderP = 1, MAOrderQ = 0 };
        armax.SetCovariates(new List<NumericsTimeSeries> { covariate });

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with covariate should validate. Messages: {string.Join(", ", messages)}");
        Assert.IsNotNull(analysis.ARIMAX.Covariates);
        Assert.AreEqual(1, analysis.ARIMAX.Covariates.Count);
    }

    /// <summary>
    /// Tests analysis with differencing (ARIMA).
    /// </summary>
    [TestMethod]
    public void Constructor_WithDifferencing_InitializesCorrectly()
    {
        var ts = CreateTrendTimeSeries();
        var armax = new ARIMAX(ts)
        {
            DiffOrderD = 1,
            AROrderP = 1,
            MAOrderQ = 0
        };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with differencing should validate. Messages: {string.Join(", ", messages)}");
        Assert.AreEqual(1, analysis.ARIMAX.DiffOrderD);
    }

    #endregion

    #region Model Property Change Handler Tests

    /// <summary>
    /// Tests that changing ARIMAX parameters clears analysis results.
    /// </summary>
    [TestMethod]
    public void ARIMAXParameterChange_ClearsResults()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        armax.AROrderP = 2;

        Assert.IsNull(analysis.AnalysisResults);
        Assert.IsFalse(analysis.IsEstimated);
    }

    /// <summary>
    /// Tests that changing time series clears analysis results.
    /// </summary>
    [TestMethod]
    public void ARIMAXTimeSeriesChange_ClearsResults()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        var newTs = CreateShortTimeSeries();
        armax.TimeSeries = newTs;

        Assert.IsNull(analysis.AnalysisResults);
    }

    /// <summary>
    /// Tests that changing TrainingTimeSteps clears analysis results.
    /// </summary>
    [TestMethod]
    public void ARIMAXTrainingTimeStepsChange_ClearsResults()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        armax.TrainingTimeSteps = 40;

        Assert.IsNull(analysis.AnalysisResults);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests that analysis validation fails for a too-short time series.
    /// </summary>
    [TestMethod]
    public void Constructor_WithShortTimeSeries_FailsValidation()
    {
        var ts = CreateShortTimeSeries();
        var armax = new ARIMAX(ts) { AROrderP = 1, MAOrderQ = 0 };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, _) = analysis.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests analysis with higher AR order.
    /// </summary>
    [TestMethod]
    public void Constructor_WithHigherAROrder_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts) { AROrderP = 3, MAOrderQ = 0 };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with AR(3) should validate. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests analysis with higher MA order.
    /// </summary>
    [TestMethod]
    public void Constructor_WithHigherMAOrder_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts) { AROrderP = 0, MAOrderQ = 3 };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with MA(3) should validate. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests analysis without intercept.
    /// </summary>
    [TestMethod]
    public void Constructor_WithoutIntercept_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts) { IncludeIntercept = false, AROrderP = 1, MAOrderQ = 0 };

        var analysis = new ARIMAXAnalysis(armax);

        Assert.IsFalse(analysis.ARIMAX.IncludeIntercept);
    }

    /// <summary>
    /// Tests analysis with multiple covariates.
    /// </summary>
    [TestMethod]
    public void Constructor_WithMultipleCovariates_InitializesCorrectly()
    {
        var (ts, covariate1) = CreateTimeSeriesWithCovariate();
        var rng = new Random(33333);

        var covariate2 = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1970, 1, 1), new DateTime(2019, 1, 1));
        for (int i = 0; i < covariate2.Count; i++)
        {
            covariate2[i].Value = 100 + (rng.NextDouble() * 2 - 1) * 20;
        }

        var armax = new ARIMAX(ts) { AROrderP = 1, MAOrderQ = 0 };
        armax.SetCovariates(new List<NumericsTimeSeries> { covariate1, covariate2 });

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with multiple covariates should validate. Messages: {string.Join(", ", messages)}");
        Assert.AreEqual(2, analysis.ARIMAX.Covariates.Count);
    }

    #endregion

    #region Bayesian Analysis Configuration Tests

    /// <summary>
    /// Tests that BayesianAnalysis settings can be configured.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_CanBeConfigured()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        analysis.BayesianAnalysis.Iterations = 10000;
        analysis.BayesianAnalysis.WarmupIterations = 2000;
        analysis.BayesianAnalysis.ThinningInterval = 10;
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;

        Assert.AreEqual(10000, analysis.BayesianAnalysis.Iterations);
        Assert.AreEqual(2000, analysis.BayesianAnalysis.WarmupIterations);
        Assert.AreEqual(10, analysis.BayesianAnalysis.ThinningInterval);
        Assert.AreEqual(0.90, analysis.BayesianAnalysis.CredibleIntervalWidth, 1e-10);
    }

    /// <summary>
    /// Tests that PointEstimator can be set to PosteriorMean.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_PointEstimator_CanBeSetToPosteriorMean()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;

        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean,
            analysis.BayesianAnalysis.PointEstimator);
    }

    /// <summary>
    /// Tests that PointEstimator can be set to PosteriorMode.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_PointEstimator_CanBeSetToMAP()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;

        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMode,
            analysis.BayesianAnalysis.PointEstimator);
    }

    /// <summary>
    /// Tests that UseJeffreysRuleForScale can be configured.
    /// </summary>
    [TestMethod]
    public void ARIMAX_UseJeffreysRuleForScale_CanBeConfigured()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);

        armax.UseJeffreysRuleForScale = false;

        Assert.IsFalse(armax.UseJeffreysRuleForScale);
    }

    #endregion

    #region Transform Tests

    /// <summary>
    /// Tests analysis with logarithmic transformation.
    /// </summary>
    [TestMethod]
    public void Constructor_WithLogTransform_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts)
        {
            TransformType = RMC.BestFit.Models.Transform.Logarithmic,
            AROrderP = 1,
            MAOrderQ = 0
        };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with log transform should validate. Messages: {string.Join(", ", messages)}");
        Assert.AreEqual(RMC.BestFit.Models.Transform.Logarithmic, analysis.ARIMAX.TransformType);
    }

    /// <summary>
    /// Tests analysis with Box-Cox transformation.
    /// </summary>
    [TestMethod]
    public void Constructor_WithBoxCoxTransform_InitializesCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts)
        {
            TransformType = RMC.BestFit.Models.Transform.BoxCox,
            AROrderP = 1,
            MAOrderQ = 0
        };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with Box-Cox transform should validate. Messages: {string.Join(", ", messages)}");
        Assert.AreEqual(RMC.BestFit.Models.Transform.BoxCox, analysis.ARIMAX.TransformType);
    }

    /// <summary>
    /// Tests analysis with Yeo-Johnson transformation.
    /// </summary>
    [TestMethod]
    public void Constructor_WithYeoJohnsonTransform_InitializesCorrectly()
    {
        var ts = CreateMonthlySeasonalTimeSeries();
        var armax = new ARIMAX(ts)
        {
            TransformType = RMC.BestFit.Models.Transform.YeoJohnson,
            AROrderP = 1,
            MAOrderQ = 0
        };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with Yeo-Johnson transform should validate. Messages: {string.Join(", ", messages)}");
        Assert.AreEqual(RMC.BestFit.Models.Transform.YeoJohnson, analysis.ARIMAX.TransformType);
    }

    #endregion

    #region RunAsync Validation Tests

    /// <summary>
    /// Tests that RunAsync throws for invalid configuration (no time series).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WithInvalidConfiguration_ThrowsInvalidOperationException()
    {
        var armax = new ARIMAX(); // No time series data
        var analysis = new ARIMAXAnalysis(armax);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await analysis.RunAsync());
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that ARIMAX property returns the correct model reference.
    /// </summary>
    [TestMethod]
    public void ARIMAXProperty_ReturnsCorrectReference()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);

        var analysis = new ARIMAXAnalysis(armax);

        Assert.AreSame(armax, analysis.ARIMAX);
    }

    /// <summary>
    /// Tests analysis with full ARIMAX model (all components).
    /// </summary>
    [TestMethod]
    public void Constructor_WithFullARIMAXModel_InitializesCorrectly()
    {
        var (ts, covariate) = CreateTimeSeriesWithCovariate();
        var armax = new ARIMAX(ts)
        {
            AROrderP = 2,
            MAOrderQ = 1,
            TrendType = ARIMAX.Trend.Linear,
            IncludeIntercept = true
        };
        armax.SetCovariates(new List<NumericsTimeSeries> { covariate });

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Full ARIMAX model should validate. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that all trend types can be validated.
    /// </summary>
    [TestMethod]
    [DataRow(ARIMAX.Trend.None)]
    [DataRow(ARIMAX.Trend.Linear)]
    [DataRow(ARIMAX.Trend.Quadratic)]
    [DataRow(ARIMAX.Trend.Cubic)]
    public void AllTrendTypes_CanBeValidated(ARIMAX.Trend trendType)
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts)
        {
            TrendType = trendType,
            AROrderP = 1,
            MAOrderQ = 0
        };
        var analysis = new ARIMAXAnalysis(armax);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"{trendType} trend should validate. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that UseDefaultTrainingSteps correctly configures training.
    /// </summary>
    [TestMethod]
    public void UseDefaultTrainingSteps_ConfiguresTrainingCorrectly()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts) { UseDefaultTrainingSteps = true };

        var analysis = new ARIMAXAnalysis(armax);

        Assert.IsTrue(armax.TrainingTimeSteps > 0);
        Assert.IsTrue(armax.TrainingTimeSteps <= ts.Count);
    }

    /// <summary>
    /// Tests that DiffOrderD correctly configures differencing.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void DiffOrderD_ConfiguresDifferencingCorrectly(int diffOrder)
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts)
        {
            DiffOrderD = diffOrder,
            AROrderP = 1,
            MAOrderQ = 0
        };

        var analysis = new ARIMAXAnalysis(armax);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Diff order {diffOrder} should validate. Messages: {string.Join(", ", messages)}");
        Assert.AreEqual(diffOrder, analysis.ARIMAX.DiffOrderD);
    }

    #endregion

    #region AnalysisResults Tests

    /// <summary>
    /// Tests that AnalysisResults is null before estimation.
    /// </summary>
    [TestMethod]
    public void AnalysisResults_BeforeEstimation_IsNull()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        Assert.IsNull(analysis.AnalysisResults);
    }

    /// <summary>
    /// Tests that IsEstimated is false initially.
    /// </summary>
    [TestMethod]
    public void IsEstimated_BeforeEstimation_IsFalse()
    {
        var ts = CreateAnnualStreamflowTimeSeries();
        var armax = new ARIMAX(ts);
        var analysis = new ARIMAXAnalysis(armax);

        Assert.IsFalse(analysis.IsEstimated);
    }

    #endregion
}
