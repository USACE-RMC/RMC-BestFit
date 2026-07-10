using Numerics.Data;
using RMC.BestFit.Models;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Programmatic unit tests for the <c>ARIMAX</c> time-series model.
/// </summary>
/// <remarks>
/// <para>
/// ARIMAX (AutoRegressive Integrated Moving Average with eXogenous variables) models
/// combine ARIMA(p, d, q) dynamics with optional trend, seasonality, and exogenous
/// covariate components:
/// Y(t) = μ + γ(t) + ψ(t) + β·X(t) + φ·Y(t-p) + θ·ε(t-q) + ε(t).
/// </para>
/// <para>
/// These tests cover constructors, property round-trips, parameter layout,
/// log-likelihood evaluation at fixed parameters, prediction, generation,
/// covariate extension settings, cloning, serialization, and validation.
/// Estimation-driven tests (MLE / R parity) live in <c>RMC.BestFit.Verification</c>.
/// </para>
/// </remarks>
[TestClass]
public class ARIMAXTests
{
    #region Inline test fixtures

    /// <summary>
    /// Deterministic annual fixture (60 observations, 1960-2019) generated with a
    /// fixed seed so this file does not depend on the Verification project's
    /// shared <c>TestData</c>.
    /// </summary>
    private static NumericsTimeSeries CreateSampleTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1960, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(12345);

        // Generate AR(1)-like data with linear trend
        const double mean = 1000.0;
        const double phi = 0.6;
        const double sigma = 100.0;
        const double trendSlope = 5.0;
        double prevValue = mean;

        for (int i = 0; i < ts.Count; i++)
        {
            double innovation = rng.NextDouble() * 2.0 - 1.0;
            double trend = trendSlope * i;
            double value = mean + trend + phi * (prevValue - mean - trendSlope * (i - 1)) + sigma * innovation;
            ts[i].Value = value;
            prevValue = value;
        }

        return ts;
    }

    /// <summary>
    /// Deterministic monthly fixture (240 observations, 2000-2019) with seasonal
    /// pattern + linear trend + noise.
    /// </summary>
    private static NumericsTimeSeries CreateMonthlyTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), new DateTime(2019, 12, 1));
        var rng = new Random(54321);

        for (int i = 0; i < ts.Count; i++)
        {
            double seasonal = 50.0 * Math.Sin(2.0 * Math.PI * i / 12.0);
            double trend = 0.5 * i;
            double noise = rng.NextDouble() * 20.0 - 10.0;
            ts[i].Value = 500.0 + seasonal + trend + noise;
        }

        return ts;
    }

    /// <summary>
    /// Deterministic short fixture (15 annual observations) for edge-case validation.
    /// </summary>
    private static NumericsTimeSeries CreateShortTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), new DateTime(2014, 1, 1));
        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = 100.0 + i * 10.0;
        }
        return ts;
    }

    /// <summary>
    /// Deterministic covariate fixture aligned to the supplied target time series.
    /// </summary>
    private static NumericsTimeSeries CreateCovariateTimeSeries(NumericsTimeSeries target)
    {
        var ts = new NumericsTimeSeries(target.TimeInterval, target.First().Index, target.Last().Index);
        var rng = new Random(67890);

        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = 0.5 * target[i].Value + rng.NextDouble() * 50.0;
        }

        return ts;
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the empty constructor creates a default ARIMAX model with AR(1) order.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new ARIMAX();

        Assert.IsNotNull(model);
        Assert.AreEqual(1, model.AROrderP);
        Assert.AreEqual(0, model.DiffOrderD);
        Assert.AreEqual(0, model.MAOrderQ);
        Assert.IsTrue(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that the constructor with time series correctly assigns the data.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithTimeSeries_SetsData()
    {
        var ts = CreateSampleTimeSeries();

        var model = new ARIMAX(ts);

        Assert.AreSame(ts, model.TimeSeries);
    }

    /// <summary>
    /// Tests that an ARIMAX model can be restored from an XElement with all properties preserved.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMAX(ts);
        original.AROrderP = 2;
        original.MAOrderQ = 1;
        original.TrendType = ARIMAX.Trend.Linear;
        var xElement = original.ToXElement();

        var restored = new ARIMAX(ts, xElement);

        Assert.AreEqual(2, restored.AROrderP);
        Assert.AreEqual(1, restored.MAOrderQ);
        Assert.AreEqual(ARIMAX.Trend.Linear, restored.TrendType);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Tests that the NumericsTimeSeries property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_TimeSeries_SetAndGet()
    {
        var model = new ARIMAX();
        var ts = CreateSampleTimeSeries();

        model.TimeSeries = ts;

        Assert.AreSame(ts, model.TimeSeries);
    }

    /// <summary>
    /// Verifies that assigning a different time series resets manual training edits to the default split.
    /// </summary>
    [TestMethod]
    public void TimeSeries_NewSeries_ResetsTrainingSplitToDefault()
    {
        var originalSeries = CreateSampleTimeSeries();
        var replacementSeries = CreateMonthlyTimeSeries();
        var model = new ARIMAX(originalSeries);

        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = 40;

        model.TimeSeries = replacementSeries;

        Assert.IsTrue(model.UseDefaultTrainingSteps);
        Assert.AreEqual((int)Math.Floor(0.8 * replacementSeries.Count), model.TrainingTimeSteps);
    }

    /// <summary>
    /// Verifies that editing the currently attached series preserves an explicit manual training window.
    /// </summary>
    [TestMethod]
    public void TimeSeries_CollectionChanged_PreservesManualTrainingSplit()
    {
        var series = CreateSampleTimeSeries();
        var model = new ARIMAX(series);

        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = 40;

        var nextDate = NumericsTimeSeries.AddTimeInterval(series.Last().Index, series.TimeInterval);
        series.Add(new SeriesOrdinate<DateTime, double>(nextDate, 1200.0));

        Assert.IsFalse(model.UseDefaultTrainingSteps);
        Assert.AreEqual(40, model.TrainingTimeSteps);
    }

    /// <summary>
    /// Verifies that editing the currently attached series recomputes the split while the default rule is enabled.
    /// </summary>
    [TestMethod]
    public void TimeSeries_CollectionChanged_RecomputesDefaultTrainingSplit()
    {
        var series = CreateMonthlyTimeSeries();
        var model = new ARIMAX(series);

        var nextDate = NumericsTimeSeries.AddTimeInterval(series.Last().Index, series.TimeInterval);
        series.Add(new SeriesOrdinate<DateTime, double>(nextDate, 640.0));

        Assert.IsTrue(model.UseDefaultTrainingSteps);
        Assert.AreEqual((int)Math.Floor(0.8 * series.Count), model.TrainingTimeSteps);
    }

    /// <summary>
    /// Tests that the AR order (p) property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_AROrderP_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        model.AROrderP = 3;

        Assert.AreEqual(3, model.AROrderP);
    }

    /// <summary>
    /// Tests that the MA order (q) property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_MAOrderQ_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        model.MAOrderQ = 2;

        Assert.AreEqual(2, model.MAOrderQ);
    }

    /// <summary>
    /// Tests that the differencing order (d) property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_DiffOrderD_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        model.DiffOrderD = 1;

        Assert.AreEqual(1, model.DiffOrderD);
    }

    /// <summary>
    /// Tests that the IncludeIntercept property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_IncludeIntercept_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        model.IncludeIntercept = false;

        Assert.IsFalse(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that the TrendType property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_TrendType_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        model.TrendType = ARIMAX.Trend.Quadratic;

        Assert.AreEqual(ARIMAX.Trend.Quadratic, model.TrendType);
    }

    /// <summary>
    /// Tests that the IncludeSeasonality property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_IncludeSeasonality_SetAndGet()
    {
        var ts = CreateMonthlyTimeSeries();
        var model = new ARIMAX(ts);

        model.IncludeSeasonality = true;

        Assert.IsTrue(model.IncludeSeasonality);
    }

    /// <summary>
    /// Tests that the TransformType property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_TransformType_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        model.TransformType = RMC.BestFit.Models.Transform.Logarithmic;

        Assert.AreEqual(RMC.BestFit.Models.Transform.Logarithmic, model.TransformType);
    }

    #endregion

    #region Parameter Tests

    /// <summary>
    /// Tests that an AR(1) model has exactly 3 parameters: intercept, AR coefficient, and scale.
    /// </summary>
    [TestMethod]
    public void Test_NumberOfParameters_AR1()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 1;
        model.MAOrderQ = 0;
        model.TrendType = ARIMAX.Trend.None;
        model.IncludeSeasonality = false;

        // AR(1) with intercept: μ + φ₁ + σ = 3 parameters
        Assert.AreEqual(3, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that an ARMA(1,1) model has exactly 4 parameters: intercept, AR, MA, and scale.
    /// </summary>
    [TestMethod]
    public void Test_NumberOfParameters_ARMA11()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 1;
        model.MAOrderQ = 1;
        model.TrendType = ARIMAX.Trend.None;

        // ARMA(1,1) with intercept: μ + φ₁ + θ₁ + σ = 4 parameters
        Assert.AreEqual(4, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that adding a linear trend increases the parameter count by 1.
    /// </summary>
    [TestMethod]
    public void Test_NumberOfParameters_WithLinearTrend()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 1;
        model.MAOrderQ = 0;
        model.TrendType = ARIMAX.Trend.Linear;

        // AR(1) with intercept + linear trend: μ + γ + φ₁ + σ = 4 parameters
        Assert.AreEqual(4, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that adding a quadratic trend increases the parameter count by 2.
    /// </summary>
    [TestMethod]
    public void Test_NumberOfParameters_WithQuadraticTrend()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 1;
        model.MAOrderQ = 0;
        model.TrendType = ARIMAX.Trend.Quadratic;

        // AR(1) with intercept + quadratic trend: μ + γ₁ + γ₂ + φ₁ + σ = 5 parameters
        Assert.AreEqual(5, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that the model includes a positive scale parameter for the error term.
    /// </summary>
    [TestMethod]
    public void Test_Parameters_HasScaleParameter()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        var scaleParam = model.Parameters.FirstOrDefault(p => p.Name.Contains("Scale") || p.Name.Contains("sigma"));

        Assert.IsNotNull(scaleParam);
        Assert.IsTrue(scaleParam!.IsPositive);
    }

    /// <summary>
    /// Tests that SetParameterValues correctly updates all model parameters.
    /// </summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 1;
        model.MAOrderQ = 0;
        model.TrendType = ARIMAX.Trend.None;

        var newValues = new double[] { 1000.0, 0.5, 50.0 };
        model.SetParameterValues(newValues);

        Assert.AreEqual(1000.0, model.Parameters[0].Value);
        Assert.AreEqual(0.5, model.Parameters[1].Value);
        Assert.AreEqual(50.0, model.Parameters[2].Value);
    }

    /// <summary>
    /// Tests that SetParameterValues throws ArgumentNullException when passed null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_SetParameterValues_NullParameters_ThrowsException()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        model.SetParameterValues(null!);
    }

    #endregion

    #region LogLikelihood Tests

    /// <summary>
    /// Tests that DataLogLikelihood returns a finite value for valid parameters.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(dataLogLH));
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns a finite value when a trend component is included.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_WithTrend_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.TrendType = ARIMAX.Trend.Linear;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns a finite value for an ARMA(2,1) model.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_ARMA21_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 2;
        model.MAOrderQ = 1;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns zero (the canonical "no data" value) when no time series is assigned.
    /// </summary>
    /// <remarks>
    /// ARIMAX uses 0.0 as a sentinel for the no-time-series case rather than
    /// <c>double.NegativeInfinity</c>. The behaviour here is verified to match the model
    /// implementation; do not change it without updating <c>ARIMAX.DataLogLikelihood</c> too.
    /// </remarks>
    [TestMethod]
    public void Test_DataLogLikelihood_NullTimeSeries_ReturnsZero()
    {
        var model = new ARIMAX();

        double result = model.DataLogLikelihood(model.Parameters.Select(p => p.Value).ToArray());

        Assert.AreEqual(0.0, result);
    }

    /// <summary>
    /// Tests that PriorLogLikelihood returns a finite value for valid parameters.
    /// </summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    /// <summary>
    /// Tests that PointwiseDataLogLikelihood returns the correct number of observation-level likelihoods.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_ReturnsCorrectCount()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 2;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        // Should have a positive number of observations not exceeding the series length
        Assert.IsTrue(pointwise.Length > 0);
        Assert.IsTrue(pointwise.Length <= ts.Count);
    }

    /// <summary>
    /// Tests that the sum of pointwise log-likelihoods equals the total data log-likelihood.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_SumEqualsTotal()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);
        double dataLogLH = model.DataLogLikelihood(parameters);

        double sum = pointwise.Sum();
        Assert.AreEqual(dataLogLH, sum, 1e-6);
    }

    #endregion

    #region Predict Tests

    /// <summary>
    /// Tests that Predict returns non-null predicted values.
    /// </summary>
    [TestMethod]
    public void Test_Predict_ReturnsValues()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var result = model.Predict(parameters);

        Assert.IsNotNull(result.Y);
        Assert.IsTrue(result.Y.Length > 0);
    }

    /// <summary>
    /// Tests that Predict returns an array with length at least equal to training time steps.
    /// </summary>
    [TestMethod]
    public void Test_Predict_WithForecast_ReturnsCorrectLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var result = model.Predict(parameters);

        // Result length should match TrainingTimeSteps + default ForecastingTimeSteps
        Assert.IsTrue(result.Y.Length >= model.TrainingTimeSteps);
    }

    /// <summary>
    /// Tests that Predict produces reproducible results when using the same random seed.
    /// </summary>
    [TestMethod]
    public void Test_Predict_SameSeed_Reproducible()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var result1 = model.Predict(parameters, seed: 12345);
        var result2 = model.Predict(parameters, seed: 12345);

        for (int i = 0; i < result1.Y.Length; i++)
        {
            Assert.AreEqual(result1.Y[i], result2.Y[i], 1e-10);
        }
    }

    /// <summary>
    /// Tests that Predict returns all tuple components (Y, Intercept, Trend, Seasonal, Covariate, AR, MA parts).
    /// </summary>
    [TestMethod]
    public void Test_Predict_ReturnsTupleComponents()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 1;
        model.MAOrderQ = 1;
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var result = model.Predict(parameters);

        // Verify all tuple components are returned
        Assert.IsNotNull(result.Y);
        Assert.IsNotNull(result.InterceptPart);
        Assert.IsNotNull(result.TrendPart);
        Assert.IsNotNull(result.SeasonalityPart);
        Assert.IsNotNull(result.CovariatePart);
        Assert.IsNotNull(result.ARPart);
        Assert.IsNotNull(result.MAPart);

        // All should have same length
        int expectedLength = result.Y.Length;
        Assert.AreEqual(expectedLength, result.InterceptPart.Length);
        Assert.AreEqual(expectedLength, result.ARPart.Length);
        Assert.AreEqual(expectedLength, result.MAPart.Length);
    }

    #endregion

    #region GenerateRandomValues Tests

    /// <summary>
    /// Tests that GenerateRandomValues returns an array of the requested length.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_ReturnsCorrectLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        var generated = model.GenerateRandomValues(100, seed: 12345);

        Assert.AreEqual(100, generated.Length);
    }

    /// <summary>
    /// Tests that GenerateRandomValues produces reproducible results when using the same seed.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_SameSeed_Reproducible()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        var generated1 = model.GenerateRandomValues(50, seed: 12345);
        var generated2 = model.GenerateRandomValues(50, seed: 12345);

        for (int i = 0; i < generated1.Length; i++)
        {
            Assert.AreEqual(generated1[i], generated2[i], 1e-10);
        }
    }

    #endregion

    #region Clone Tests

    /// <summary>
    /// Tests that Clone creates a new independent ARIMAX instance with matching properties.
    /// </summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMAX(ts);
        original.AROrderP = 2;
        original.MAOrderQ = 1;

        var clone = (ARIMAX)original.Clone();

        Assert.AreNotSame(original, clone);
        Assert.AreEqual(original.AROrderP, clone.AROrderP);
        Assert.AreEqual(original.MAOrderQ, clone.MAOrderQ);
    }

    /// <summary>
    /// Tests that modifying the original model's parameters does not affect the cloned model.
    /// </summary>
    [TestMethod]
    public void Test_Clone_ParametersAreIndependent()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMAX(ts);
        double originalValue = original.Parameters[0].Value;

        var clone = (ARIMAX)original.Clone();
        original.Parameters[0].Value = 99999;

        Assert.AreEqual(originalValue, clone.Parameters[0].Value);
    }

    /// <summary>
    /// Tests that Clone preserves all configuration settings including trend, seasonality, and differencing.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesConfiguration()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMAX(ts);
        original.TrendType = ARIMAX.Trend.Quadratic;
        original.IncludeSeasonality = true;
        original.DiffOrderD = 1;

        var clone = (ARIMAX)original.Clone();

        Assert.AreEqual(original.TrendType, clone.TrendType);
        Assert.AreEqual(original.IncludeSeasonality, clone.IncludeSeasonality);
        Assert.AreEqual(original.DiffOrderD, clone.DiffOrderD);
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Tests that ToXElement returns an element with the correct root name "ARIMAX".
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsARIMAXElement()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        var xElement = model.ToXElement();

        Assert.AreEqual("ARIMAX", xElement.Name.LocalName);
    }

    /// <summary>
    /// Tests that ToXElement includes attributes for AR, MA, and differencing orders.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsOrderAttributes()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 2;
        model.MAOrderQ = 1;
        model.DiffOrderD = 1;

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement.Attribute("AROrderP"));
        Assert.IsNotNull(xElement.Attribute("MAOrderQ"));
        Assert.IsNotNull(xElement.Attribute("DiffOrderD"));
    }

    /// <summary>
    /// Tests that serializing and deserializing an ARIMAX model preserves all properties.
    /// </summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMAX(ts);
        original.AROrderP = 2;
        original.MAOrderQ = 1;
        original.DiffOrderD = 1;
        original.TrendType = ARIMAX.Trend.Linear;
        original.IncludeIntercept = false;

        var xElement = original.ToXElement();
        var restored = new ARIMAX(ts, xElement);

        Assert.AreEqual(original.AROrderP, restored.AROrderP);
        Assert.AreEqual(original.MAOrderQ, restored.MAOrderQ);
        Assert.AreEqual(original.DiffOrderD, restored.DiffOrderD);
        Assert.AreEqual(original.TrendType, restored.TrendType);
        Assert.AreEqual(original.IncludeIntercept, restored.IncludeIntercept);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Tests that a properly configured ARIMAX model passes validation.
    /// </summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that validation fails when no time series is assigned to the model.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NullTimeSeries_ReturnsFalse()
    {
        var model = new ARIMAX();

        var (isValid, _) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that validation fails when the time series is too short for the model.
    /// </summary>
    [TestMethod]
    public void Test_Validate_TooShortTimeSeries_ReturnsFalse()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), new DateTime(2004, 1, 1));
        for (int i = 0; i < ts.Count; i++) ts[i].Value = i;
        var model = new ARIMAX(ts);

        var (isValid, _) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that validation rejects irregular time intervals.
    /// </summary>
    [TestMethod]
    public void Test_Validate_IrregularTimeSeries_ReturnsFalse()
    {
        var ts = new NumericsTimeSeries(TimeInterval.Irregular);
        var start = new DateTime(2000, 1, 1);
        for (int i = 0; i < 50; i++)
            ts.Add(new SeriesOrdinate<DateTime, double>(start.AddDays(i * i + 1), i + 1.0));
        var model = new ARIMAX(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("regular time interval")));
    }

    #endregion

    #region Trend Tests

    /// <summary>
    /// Tests that adding a linear trend increases the parameter count by exactly 1.
    /// </summary>
    [TestMethod]
    public void Test_LinearTrend_IncreasesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.TrendType = ARIMAX.Trend.None;
        int baseParams = model.NumberOfParameters;

        model.TrendType = ARIMAX.Trend.Linear;

        Assert.AreEqual(baseParams + 1, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that adding a quadratic trend increases the parameter count by exactly 2.
    /// </summary>
    [TestMethod]
    public void Test_QuadraticTrend_IncreasesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.TrendType = ARIMAX.Trend.None;
        int baseParams = model.NumberOfParameters;

        model.TrendType = ARIMAX.Trend.Quadratic;

        Assert.AreEqual(baseParams + 2, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that adding a cubic trend increases the parameter count by exactly 3.
    /// </summary>
    [TestMethod]
    public void Test_CubicTrend_IncreasesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.TrendType = ARIMAX.Trend.None;
        int baseParams = model.NumberOfParameters;

        model.TrendType = ARIMAX.Trend.Cubic;

        Assert.AreEqual(baseParams + 3, model.NumberOfParameters);
    }

    #endregion

    #region Differencing Tests

    /// <summary>
    /// Tests that first-order differencing (d=1) works correctly and produces a finite log-likelihood.
    /// </summary>
    [TestMethod]
    public void Test_Differencing_Order1_Works()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.DiffOrderD = 1;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Tests that second-order differencing (d=2) works correctly and produces a finite log-likelihood.
    /// </summary>
    [TestMethod]
    public void Test_Differencing_Order2_Works()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.DiffOrderD = 2;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    #endregion

    #region Transform Tests

    /// <summary>
    /// Tests that the logarithmic transform works correctly and produces a finite log-likelihood.
    /// </summary>
    [TestMethod]
    public void Test_LogTransform_Works()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.TransformType = RMC.BestFit.Models.Transform.Logarithmic;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Tests that the model works correctly with no transform applied.
    /// </summary>
    [TestMethod]
    public void Test_NoTransform_Works()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.TransformType = RMC.BestFit.Models.Transform.None;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    #endregion

    #region Seasonality Tests

    /// <summary>
    /// Tests that enabling seasonality increases the parameter count (adds Fourier terms).
    /// </summary>
    [TestMethod]
    public void Test_Seasonality_IncreasesParameters()
    {
        var ts = CreateMonthlyTimeSeries();
        var model = new ARIMAX(ts);
        model.IncludeSeasonality = false;
        int baseParams = model.NumberOfParameters;

        model.IncludeSeasonality = true;

        // Seasonality adds sin/cos pairs (Fourier terms)
        Assert.IsTrue(model.NumberOfParameters > baseParams);
    }

    /// <summary>
    /// Tests that monthly seasonality produces a finite log-likelihood value.
    /// </summary>
    [TestMethod]
    public void Test_MonthlySeasonality_LogLikelihood()
    {
        var ts = CreateMonthlyTimeSeries();
        var model = new ARIMAX(ts);
        model.IncludeSeasonality = true;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Verifies that monthly records infer a twelve-step annual seasonal cycle.
    /// </summary>
    [TestMethod]
    public void MonthlyTimeSeries_SeasonalPeriod_IsTwelve()
    {
        var model = new ARIMAX(CreateMonthlyTimeSeries());

        Assert.AreEqual(12, model.SeasonalPeriod);
    }

    /// <summary>
    /// Verifies that annual records infer a decadal seasonal cycle.
    /// </summary>
    [TestMethod]
    public void AnnualTimeSeries_SeasonalPeriod_IsTen()
    {
        var model = new ARIMAX(CreateSampleTimeSeries());

        Assert.AreEqual(10, model.SeasonalPeriod);
    }

    /// <summary>
    /// Verifies that annual seasonality varies over the inferred decadal cycle.
    /// </summary>
    [TestMethod]
    public void AnnualSeasonality_FourierTerms_AreNonDegenerate()
    {
        var model = new ARIMAX(CreateSampleTimeSeries())
        {
            IncludeSeasonality = true,
        };
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        int sinIndex = model.Parameters.FindIndex(p => p.Name.StartsWith("Seasonality Sin", StringComparison.Ordinal));
        int cosIndex = model.Parameters.FindIndex(p => p.Name.StartsWith("Seasonality Cos", StringComparison.Ordinal));
        parameters[sinIndex] = 1.0;
        parameters[cosIndex] = 0.0;

        var result = model.Predict(parameters, seed: -1);
        double range = result.SeasonalityPart.Take(10).Max() - result.SeasonalityPart.Take(10).Min();

        Assert.IsTrue(range > 1.0, "Annual seasonality should vary over the ten-year inferred cycle.");
    }

    #endregion

    #region Covariate Tests

    /// <summary>
    /// Tests that SetCovariates wires a single covariate into the model and the parameter
    /// list grows to accommodate the new beta coefficient.
    /// </summary>
    [TestMethod]
    public void Test_SetCovariates_SingleCovariate_AddsParameter()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts)
        {
            AROrderP = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = ts.Count;
        model.SetDefaultParameters();
        int baseParams = model.NumberOfParameters;

        var cov = CreateCovariateTimeSeries(ts);
        model.SetCovariates(new List<NumericsTimeSeries> { cov });

        Assert.IsTrue(model.NumberOfParameters > baseParams,
            "Adding a covariate should increase the parameter count.");
    }

    /// <summary>
    /// Tests that DataLogLikelihood remains finite after attaching a covariate.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_WithCovariate_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts)
        {
            AROrderP = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = ts.Count;
        var cov = CreateCovariateTimeSeries(ts);
        model.SetCovariates(new List<NumericsTimeSeries> { cov });

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(dataLogLH));
    }

    #endregion

    #region Engineering Application Tests

    /// <summary>
    /// Tests a typical streamflow analysis scenario with AR(1) and linear trend.
    /// </summary>
    [TestMethod]
    public void Test_ARIMAX_StreamflowWithTrend()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 1;
        model.TrendType = ARIMAX.Trend.Linear;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);
        Assert.IsFalse(double.IsNegativeInfinity(dataLogLH));
    }

    /// <summary>
    /// Tests a typical monthly data scenario with AR(1) and seasonal component.
    /// </summary>
    [TestMethod]
    public void Test_ARIMAX_MonthlyWithSeasonality()
    {
        var ts = CreateMonthlyTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 1;
        model.IncludeSeasonality = true;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests an ARIMA(1,1,1) model configuration for nonstationary time series.
    /// </summary>
    [TestMethod]
    public void Test_ARIMAX_ARIMA111()
    {
        // ARIMA(1,1,1) model
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 1;
        model.DiffOrderD = 1;
        model.MAOrderQ = 1;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests that the model validation fails when time series is too short.
    /// ARIMAX models require sufficient observations for parameter estimation.
    /// </summary>
    [TestMethod]
    public void Test_ARIMAX_ShortTimeSeries_FailsValidation()
    {
        var ts = CreateShortTimeSeries();
        var model = new ARIMAX(ts);

        var (isValid, _) = model.Validate();
        Assert.IsFalse(isValid, "Short time series should fail validation for ARIMAX.");
    }

    /// <summary>
    /// Tests that high AR and MA orders can be set correctly.
    /// </summary>
    [TestMethod]
    public void Test_ARIMAX_HighOrders()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.AROrderP = 5;
        model.MAOrderQ = 3;

        Assert.AreEqual(5, model.AROrderP);
        Assert.AreEqual(3, model.MAOrderQ);
    }

    /// <summary>
    /// Tests that removing the intercept decreases the parameter count by 1.
    /// </summary>
    [TestMethod]
    public void Test_ARIMAX_NoIntercept()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMAX(ts);
        model.IncludeIntercept = false;
        int paramsWithoutIntercept = model.NumberOfParameters;

        model.IncludeIntercept = true;

        // Should have one more parameter with intercept
        Assert.AreEqual(paramsWithoutIntercept + 1, model.NumberOfParameters);
    }

    #endregion

    #region CovariateExtension Tests

    /// <summary>
    /// Tests that CovariateExtensionMethod.None throws when covariates are insufficient for Predict.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_Predict_CovariateExtensionNone_ThrowsWhenInsufficient()
    {
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None
        };
        model.TrainingTimeSteps = 100;
        model.SetCovariates(new List<NumericsTimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 }); // mu, beta, phi, sigma

        // Try to forecast 10 steps beyond available data — should throw
        // Covariates only have 100 points, but we need 110 (100 training + 10 forecast)
        model.Predict(model.Parameters.Select(p => p.Value).ToArray(), forecastSteps: 10);
    }

    /// <summary>
    /// Tests that CovariateExtensionMethod.BlockBootstrap extends covariates successfully for Predict.
    /// </summary>
    [TestMethod]
    public void Test_Predict_CovariateExtensionBlockBootstrap_ExtendsCovariates()
    {
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.BlockBootstrap
        };
        model.TrainingTimeSteps = 100;
        model.SetCovariates(new List<NumericsTimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 });

        var result = model.Predict(model.Parameters.Select(p => p.Value).ToArray(), forecastSteps: 20, seed: 12345);

        Assert.AreEqual(120, result.Y.Length, "Should have 100 training + 20 forecast steps.");
        Assert.IsTrue(result.CovariatePart.Skip(100).Any(c => c != 0),
            "Covariate effect should be non-zero in forecast period.");
    }

    /// <summary>
    /// Tests that CovariateExtensionMethod.KNN extends covariates successfully for Predict.
    /// </summary>
    [TestMethod]
    public void Test_Predict_CovariateExtensionKNN_ExtendsCovariates()
    {
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN
        };
        model.TrainingTimeSteps = 100;
        model.SetCovariates(new List<NumericsTimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 });

        var result = model.Predict(model.Parameters.Select(p => p.Value).ToArray(), forecastSteps: 20, seed: 12345);

        Assert.AreEqual(120, result.Y.Length, "Should have 100 training + 20 forecast steps.");
        Assert.IsTrue(result.CovariatePart.Skip(100).Any(c => c != 0),
            "Covariate effect should be non-zero in forecast period.");
    }

    /// <summary>
    /// Tests that providing forecastCovariates overrides the CovariateExtension setting.
    /// </summary>
    [TestMethod]
    public void Test_Predict_ExplicitForecastCovariates_OverridesExtensionSetting()
    {
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None // Would throw if used
        };
        model.TrainingTimeSteps = 100;
        model.SetCovariates(new List<NumericsTimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 });

        var forecastCov = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2008, 5, 1),
            Enumerable.Range(0, 20).Select(i => 60.0 + i * 0.5).ToArray());

        var result = model.Predict(model.Parameters.Select(p => p.Value).ToArray(),
            forecastSteps: 20, seed: 12345, forecastCovariates: new List<NumericsTimeSeries> { forecastCov });

        Assert.AreEqual(120, result.Y.Length, "Should have 100 training + 20 forecast steps.");
    }

    /// <summary>
    /// Tests that GenerateRandomValues with CovariateExtensionMethod.None throws when sampleSize exceeds covariate length.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_GenerateRandomValues_CovariateExtensionNone_ThrowsWhenInsufficient()
    {
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None
        };
        model.SetCovariates(new List<NumericsTimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 });

        // Try to generate 200 values with only 100 covariate observations — should throw
        model.GenerateRandomValues(200, seed: 12345);
    }

    /// <summary>
    /// Tests that GenerateRandomValues with CovariateExtensionMethod.BlockBootstrap extends covariates.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_CovariateExtensionBlockBootstrap_ExtendsCovariates()
    {
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.BlockBootstrap
        };
        model.SetCovariates(new List<NumericsTimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 });

        var generated = model.GenerateRandomValues(200, seed: 12345);

        Assert.AreEqual(200, generated.Length, "Should generate 200 values.");
        Assert.IsTrue(generated.All(v => !double.IsNaN(v) && !double.IsInfinity(v)),
            "All values should be finite.");
    }

    /// <summary>
    /// Tests that GenerateRandomValues with CovariateExtensionMethod.KNN extends covariates.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_CovariateExtensionKNN_ExtendsCovariates()
    {
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN
        };
        model.SetCovariates(new List<NumericsTimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 });

        var generated = model.GenerateRandomValues(200, seed: 12345);

        Assert.AreEqual(200, generated.Length, "Should generate 200 values.");
        Assert.IsTrue(generated.All(v => !double.IsNaN(v) && !double.IsInfinity(v)),
            "All values should be finite.");
    }

    /// <summary>
    /// Tests that providing explicit generateCovariates overrides the CovariateExtension setting for GenerateRandomValues.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_ExplicitCovariates_OverridesExtensionSetting()
    {
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None // Would throw if used
        };
        model.SetCovariates(new List<NumericsTimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 });

        var extendedCov = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 200).Select(i => 10.0 + i * 0.5).ToArray());

        var generated = model.GenerateRandomValues(200, seed: 12345,
            generateCovariates: new List<NumericsTimeSeries> { extendedCov });

        Assert.AreEqual(200, generated.Length, "Should generate 200 values.");
    }

    /// <summary>
    /// Tests that CovariateExtension property is serialized and deserialized correctly.
    /// </summary>
    [TestMethod]
    public void Test_CovariateExtension_XmlSerialization()
    {
        var values = Enumerable.Range(0, 50).Select(i => 50.0 + i * 0.1).ToArray();
        var ts = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);

        var original = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN
        };

        var xElement = original.ToXElement();
        var restored = new ARIMAX(ts, xElement);

        Assert.AreEqual(ARIMAX.CovariateExtensionMethod.KNN, restored.CovariateExtension,
            "CovariateExtension should be preserved through serialization.");
    }

    #endregion
}
