using System.Linq;
using Numerics.Data;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;

namespace RMC.BestFit.Verification.TimeSeriesModels;

/// <summary>
/// Unit tests for the <see cref="ARIMAX"/> class.
/// Tests ARIMAX (AutoRegressive Moving Average with eXogenous variables) time-series models.
/// </summary>
/// <remarks>
/// <para>
/// ARIMAX models combine ARMA with exogenous variables and additional features:
/// Y(t) = μ + γ(t) + ψ(t) + β*X(t) + φ*Y(t-p) + θ*ε(t-q) + ε(t)
/// where:
/// - μ is the intercept
/// - γ(t) is the trend component
/// - ψ(t) is the seasonal component
/// - β*X(t) represents exogenous covariates
/// - φ*Y(t-p) is the AR component
/// - θ*ε(t-q) is the MA component
/// </para>
/// </remarks>
[TestClass]
public class ARIMAXTests
{
    #region Test Data Helper

    /// <summary>
    /// Creates a sample annual time series with 60 observations.
    /// </summary>
    private static TimeSeries CreateSampleTimeSeries()
    {
        var ts = new TimeSeries(TimeInterval.OneYear, new DateTime(1960, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(12345);

        // Generate AR(1)-like data with trend
        double mean = 1000;
        double phi = 0.6;
        double sigma = 100;
        double trendSlope = 5;
        double prevValue = mean;

        for (int i = 0; i < ts.Count; i++)
        {
            double innovation = rng.NextDouble() * 2 - 1;
            double trend = trendSlope * i;
            double value = mean + trend + phi * (prevValue - mean - trendSlope * (i - 1)) + sigma * innovation;
            ts[i].Value = value;
            prevValue = value;
        }

        return ts;
    }

    /// <summary>
    /// Creates a monthly time series for seasonal analysis (240 observations).
    /// </summary>
    private static TimeSeries CreateMonthlyTimeSeries()
    {
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), new DateTime(2019, 12, 1));
        var rng = new Random(54321);

        for (int i = 0; i < ts.Count; i++)
        {
            // Add seasonal pattern + trend + noise
            double seasonal = 50 * Math.Sin(2 * Math.PI * i / 12);
            double trend = 0.5 * i;
            double noise = rng.NextDouble() * 20 - 10;
            ts[i].Value = 500 + seasonal + trend + noise;
        }

        return ts;
    }

    /// <summary>
    /// Creates a short time series with 15 years of data.
    /// </summary>
    private static TimeSeries CreateShortTimeSeries()
    {
        var ts = new TimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), new DateTime(2014, 1, 1));
        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = 100 + i * 10;
        }
        return ts;
    }

    /// <summary>
    /// Creates a covariate time series for testing exogenous variables.
    /// </summary>
    private static TimeSeries CreateCovariateTimeSeries(TimeSeries target)
    {
        var ts = new TimeSeries(target.TimeInterval, target.First().Index, target.Last().Index);
        var rng = new Random(67890);

        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = 0.5 * target[i].Value + rng.NextDouble() * 50;
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
    /// Tests that the TimeSeries property can be set and retrieved correctly.
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
        Assert.IsTrue(scaleParam.IsPositive);
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
    /// Tests that DataLogLikelihood returns zero when no time series is assigned.
    /// </summary>
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

        // Should have reasonable number of observations
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

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that validation fails when the time series is too short for the model.
    /// </summary>
    [TestMethod]
    public void Test_Validate_TooShortTimeSeries_ReturnsFalse()
    {
        var ts = new TimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), new DateTime(2004, 1, 1));
        for (int i = 0; i < ts.Count; i++) ts[i].Value = i;
        var model = new ARIMAX(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
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
        int paramsWithIntercept = model.NumberOfParameters;

        model.IncludeIntercept = true;

        // Should have one more parameter with intercept
        Assert.AreEqual(paramsWithIntercept + 1, model.NumberOfParameters);
    }

    #endregion

    #region MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of ARIMAX(1,1) parameters against known true values from synthetic data.
    /// ARIMAX is configured with no transforms, differencing, seasonality, or covariates.
    /// Validates that the optimizer can recover the generating parameters within 10% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX11()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX11Data(10, 0.6, 0.3, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters (10% tolerance for mixed models)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX(2,1) parameters against known true values from synthetic data.
    /// Validates that the optimizer can recover the generating parameters within 10% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX21()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX21Data(10, 0.5, -0.3, 0.3, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 2,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX(1,2) parameters against known true values from synthetic data.
    /// Validates that the optimizer can recover the generating parameters within 10% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX12()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX12Data(10, 0.5, 0.3, 0.5, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 2,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX(2,2) parameters against known true values from synthetic data.
    /// Validates that the optimizer can recover the generating parameters within 10% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX22()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX22Data(10, 0.5, -0.3, 0.3, -0.2, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 2,
            MAOrderQ = 2,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests that ARIMAX model can fit pure AR(1) data when configured with p=1, q=0.
    /// Validates that ARIMAX subsumes AR as a special case.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX_FitsAR1()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX_AR1Data(10, 0.6, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.05), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests that ARIMAX model can fit pure MA(1) data when configured with p=0, q=1.
    /// Validates that ARIMAX subsumes MA as a special case.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX_FitsMA1()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX_MA1Data(10, 0.5, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.05), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region ARIMA (Differencing) MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of ARIMA(1,1,0) parameters against known true values from synthetic data.
    /// ARIMA(1,1,0) is an AR(1) model applied to first-differenced data.
    /// Validates that the optimizer can recover the generating parameters within 15% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA110()
    {
        var data = SyntheticTimeSeriesData.GetARIMA110Data(0.5, 0.6, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(1,1,0) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance for differenced models)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMA(0,1,1) parameters against known true values from synthetic data.
    /// ARIMA(0,1,1) is an IMA(1,1) model - MA(1) applied to first-differenced data.
    /// Common model for exponential smoothing equivalents.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA011()
    {
        var data = SyntheticTimeSeriesData.GetARIMA011Data(0.3, 0.5, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            DiffOrderD = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(0,1,1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMA(1,1,1) parameters against known true values from synthetic data.
    /// ARIMA(1,1,1) is the classic Box-Jenkins model combining AR, differencing, and MA components.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA111()
    {
        var data = SyntheticTimeSeriesData.GetARIMA111Data(0.3, 0.6, 0.4, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(1,1,1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMA(1,2,0) parameters against known true values from synthetic data.
    /// ARIMA(1,2,0) uses second-order differencing, suitable for data with quadratic trends.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA120()
    {
        var data = SyntheticTimeSeriesData.GetARIMA120Data(0.1, 0.5, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 2,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(1,2,0) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance for second-order diff)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMA(2,1,1) parameters against known true values from synthetic data.
    /// Higher-order ARIMA model with AR(2), first-order differencing, and MA(1).
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA211()
    {
        var data = SyntheticTimeSeriesData.GetARIMA211Data(0.2, 0.5, -0.25, 0.3, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 2,
            DiffOrderD = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(2,1,1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region Trend Model MLE Estimation Tests

    #region Standalone Trend Tests (No AR/MA Components)

    /// <summary>
    /// Tests MLE estimation of linear trend only (no AR/MA components).
    /// Parameters: [μ, γ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_LinearTrend_Only()
    {
        var data = SyntheticTimeSeriesData.GetLinearTrendData(100.0, 0.5, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 0,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Linear trend only model fitting failed.");
        // Assert that estimated parameters are close to true parameters (10% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of quadratic trend only (no AR/MA components).
    /// Parameters: [μ, γ1, γ2, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_QuadraticTrend_Only()
    {
        var data = SyntheticTimeSeriesData.GetQuadraticTrendData(100.0, 0.5, 0.001, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Quadratic,
            AROrderP = 0,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Quadratic trend only model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of cubic trend only (no AR/MA components).
    /// Parameters: [μ, γ1, γ2, γ3, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_CubicTrend_Only()
    {
        var data = SyntheticTimeSeriesData.GetCubicTrendData(100.0, 0.3, 0.001, 0.000001, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Cubic,
            AROrderP = 0,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Cubic trend only model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance for more complex model)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region Trend Tests with AR/MA Components

    /// <summary>
    /// Tests MLE estimation of AR(1) with linear trend parameters against known true values.
    /// Parameters: [μ, γ (slope), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_LinearTrend()
    {
        var data = SyntheticTimeSeriesData.GetAR1LinearTrendData(100.0, 0.5, 0.6, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Linear trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of AR(1) with quadratic trend parameters against known true values.
    /// Parameters: [μ, γ1 (linear), γ2 (quadratic), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_QuadraticTrend()
    {
        var data = SyntheticTimeSeriesData.GetAR1QuadraticTrendData(100.0, 0.5, 0.001, 0.5, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Quadratic,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Quadratic trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance for quadratic)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of AR(1) with cubic trend parameters against known true values.
    /// Parameters: [μ, γ1 (linear), γ2 (quadratic), γ3 (cubic), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_CubicTrend()
    {
        var data = SyntheticTimeSeriesData.GetAR1CubicTrendData(100.0, 0.3, 0.001, 0.000001, 0.4, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Cubic,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Cubic trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (25% tolerance for cubic)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of MA(1) with linear trend parameters against known true values.
    /// Parameters: [μ, γ (slope), θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_MA1_LinearTrend()
    {
        var data = SyntheticTimeSeriesData.GetMA1LinearTrendData(100.0, 0.5, 0.5, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 0,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "MA(1) + Linear trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARMA(1,1) with linear trend parameters against known true values.
    /// Parameters: [μ, γ (slope), φ, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_LinearTrend()
    {
        var data = SyntheticTimeSeriesData.GetARIMA11LinearTrendData(100.0, 0.5, 0.5, 0.3, 5.0, 1000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARMA(1,1) + Linear trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #endregion

    #region Seasonality MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of AR(1) with Fourier seasonality parameters against known true values.
    /// Parameters: [μ, ψ1 (sin), ψ2 (cos), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_Seasonal()
    {
        var data = SyntheticTimeSeriesData.GetAR1SeasonalData(100.0, 20.0, 10.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Seasonal model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of MA(1) with Fourier seasonality parameters against known true values.
    /// Parameters: [μ, ψ1 (sin), ψ2 (cos), θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_MA1_Seasonal()
    {
        var data = SyntheticTimeSeriesData.GetMA1SeasonalData(100.0, 20.0, 10.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            IncludeSeasonality = true,
            AROrderP = 0,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "MA(1) + Seasonal model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARMA(1,1) with Fourier seasonality parameters against known true values.
    /// Parameters: [μ, ψ1 (sin), ψ2 (cos), φ, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_Seasonal()
    {
        var data = SyntheticTimeSeriesData.GetARIMA11SeasonalData(100.0, 20.0, 10.0, 0.5, 0.3, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARMA(1,1) + Seasonal model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of AR(1) with linear trend and Fourier seasonality.
    /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_TrendSeasonal()
    {
        var data = SyntheticTimeSeriesData.GetAR1TrendSeasonalData(100.0, 0.3, 20.0, 10.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Trend + Seasonal model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance for combined)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region Trend + Seasonality (No Differencing) MLE Estimation Tests


    #endregion

    #region Covariate (ARIMAX) MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of ARIMAX with a single covariate and AR(1) errors.
    /// Parameters: [μ, β, φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_WithCovariate()
    {
        var data = SyntheticTimeSeriesData.GetAR1WithCovariateData(50.0, 2.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        model.SetCovariates(new List<TimeSeries> { data.Covariate });
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMAX with single covariate + AR(1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX with a single covariate and MA(1) errors.
    /// Parameters: [μ, β, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_MA1_WithCovariate()
    {
        var data = SyntheticTimeSeriesData.GetMA1WithCovariateData(50.0, 2.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 1,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        model.SetCovariates(new List<TimeSeries> { data.Covariate });
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMAX with single covariate + MA(1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX with a single covariate and ARMA(1,1) errors.
    /// Parameters: [μ, β, φ, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_WithCovariate()
    {
        var data = SyntheticTimeSeriesData.GetARIMA11WithCovariateData(50.0, 2.0, 0.5, 0.3, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 1,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        model.SetCovariates(new List<TimeSeries> { data.Covariate });
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMAX with single covariate + ARMA(1,1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX with two covariates and AR(1) errors.
    /// Parameters: [μ, β1, β2, φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_WithTwoCovariates()
    {
        var data = SyntheticTimeSeriesData.GetAR1WithTwoCovariatesData(50.0, 2.0, -1.5, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        model.SetCovariates(data.Covariates);
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMAX with two covariates + AR(1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region Full Combination MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of ARIMAX with trend, seasonality, and AR(1).
    /// A comprehensive test combining multiple features.
    /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_Full_TrendSeasonalAR1()
    {
        var data = SyntheticTimeSeriesData.GetFullARIMAX_TrendSeasonalAR1Data(100.0, 0.3, 20.0, 10.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Full ARIMAX (Trend + Seasonal + AR1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (25% tolerance for complex)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARMA(1,1) with trend and seasonality (no differencing).
    /// Comprehensive combination test without differencing since it removes trends.
    /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_Full_TrendSeasonalARMA11()
    {
        var data = SyntheticTimeSeriesData.GetFullARIMAX_TrendSeasonalARIMA11Data(100.0, 0.2, 15.0, 8.0, 0.4, 0.3, 3.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            IncludeSeasonality = true,
            DiffOrderD = 0,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Full ARIMAX (Trend + Seasonal + ARMA11) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (30% tolerance for most complex)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.30), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region Transform MLE Estimation Tests

    /// <summary>
    /// Tests that AR(1) model with logarithmic transform produces a valid fit on positive data.
    /// This is a validation test that the transform mechanism works correctly.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_LogTransform()
    {
        var data = SyntheticTimeSeriesData.GetAR1PositiveData(500.0, 0.5, 20.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TransformType = RMC.BestFit.Models.Transform.Logarithmic,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) with Log transform model fitting failed.");
        // Transform tests focus on successful fit rather than exact parameter recovery
        // since the true parameters are on the original scale
        Assert.IsTrue(mle.BestParameterSet.Fitness > double.NegativeInfinity, "Log-likelihood should be finite.");
    }

    /// <summary>
    /// Tests that AR(1) model with Box-Cox transform produces a valid fit on positive data.
    /// Box-Cox automatically estimates the optimal lambda parameter.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_BoxCoxTransform()
    {
        var data = SyntheticTimeSeriesData.GetAR1PositiveData(500.0, 0.5, 20.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TransformType = RMC.BestFit.Models.Transform.BoxCox,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) with Box-Cox transform model fitting failed.");
        Assert.IsTrue(mle.BestParameterSet.Fitness > double.NegativeInfinity, "Log-likelihood should be finite.");
    }

    /// <summary>
    /// Tests that AR(1) model with Yeo-Johnson transform produces a valid fit on mixed-sign data.
    /// Yeo-Johnson handles both positive and negative values (unlike log/Box-Cox).
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_YeoJohnsonTransform()
    {
        var data = SyntheticTimeSeriesData.GetAR1MixedSignData(0.0, 0.5, 10.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TransformType = RMC.BestFit.Models.Transform.YeoJohnson,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) with Yeo-Johnson transform model fitting failed.");
        Assert.IsTrue(mle.BestParameterSet.Fitness > double.NegativeInfinity, "Log-likelihood should be finite.");
    }

    /// <summary>
    /// Tests parameter recovery on lognormal data using log transform.
    /// When data is generated with log transform, the model should recover original-scale parameters.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_Lognormal_LogTransform()
    {
        // Parameters are on original scale: mu=150, phi=0.5, sigma=0.3 (log-scale innovation SD)
        var data = SyntheticTimeSeriesData.GetLognormalAR1Data(150.0, 0.5, 0.3, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TransformType = Models.Transform.Logarithmic,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Lognormal AR(1) with Log transform model fitting failed.");
        // For lognormal data with log transform, original-scale parameters [μ, φ, σ] should be recovered
        Assert.AreEqual(data.TrueParameters[0], mle.BestParameterSet.Values[0], Math.Abs(data.TrueParameters[0] * 0.15), "Mu parameter incorrect.");
        Assert.AreEqual(data.TrueParameters[1], mle.BestParameterSet.Values[1], 0.15, "Phi parameter incorrect.");
        Assert.AreEqual(data.TrueParameters[2], mle.BestParameterSet.Values[2], Math.Abs(data.TrueParameters[2] * 0.20), "Sigma parameter incorrect.");
    }

    /// <summary>
    /// Tests that ARMA(1,1) with trend and logarithmic transform produces a valid fit.
    /// Combines transform with other model features.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_Trend_LogTransform()
    {
        var data = SyntheticTimeSeriesData.GetAR1PositiveData(500.0, 0.5, 20.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TransformType = RMC.BestFit.Models.Transform.Logarithmic,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARMA(1,1) + Trend + Log transform model fitting failed.");
        Assert.IsTrue(mle.BestParameterSet.Fitness > double.NegativeInfinity, "Log-likelihood should be finite.");
    }

    #endregion

    #region Covariate Extension Tests

    /// <summary>
    /// Tests that CovariateExtensionMethod.None throws when covariates are insufficient for Predict.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_Predict_CovariateExtensionNone_ThrowsWhenInsufficient()
    {
        // Create time series and covariate (same length)
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None
        };
        model.TrainingTimeSteps = 100; // Use all data for training
        model.SetCovariates(new List<TimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 }); // mu, beta, phi, sigma

        // Try to forecast 10 steps beyond available data - should throw
        // Covariates only have 100 points, but we need 110 (100 training + 10 forecast)
        model.Predict(model.Parameters.Select(p => p.Value).ToArray(), forecastSteps: 10);
    }

    /// <summary>
    /// Tests that CovariateExtensionMethod.BlockBootstrap extends covariates successfully for Predict.
    /// </summary>
    [TestMethod]
    public void Test_Predict_CovariateExtensionBlockBootstrap_ExtendsCovariates()
    {
        // Create time series and covariate
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.BlockBootstrap
        };
        model.TrainingTimeSteps = 100; // Use all data for training
        model.SetCovariates(new List<TimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 }); // mu, beta, phi, sigma

        // Forecast 20 steps beyond available data - should succeed with block bootstrap
        var result = model.Predict(model.Parameters.Select(p => p.Value).ToArray(), forecastSteps: 20, seed: 12345);

        // Verify we got the expected number of predictions
        Assert.AreEqual(120, result.Y.Length, "Should have 100 training + 20 forecast steps.");
        Assert.IsTrue(result.CovariatePart.Skip(100).Any(c => c != 0), "Covariate effect should be non-zero in forecast period.");
    }

    /// <summary>
    /// Tests that CovariateExtensionMethod.KNN extends covariates successfully for Predict.
    /// </summary>
    [TestMethod]
    public void Test_Predict_CovariateExtensionKNN_ExtendsCovariates()
    {
        // Create time series and covariate
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN
        };
        model.TrainingTimeSteps = 100; // Use all data for training
        model.SetCovariates(new List<TimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 }); // mu, beta, phi, sigma

        // Forecast 20 steps beyond available data - should succeed with KNN
        var result = model.Predict(model.Parameters.Select(p => p.Value).ToArray(), forecastSteps: 20, seed: 12345);

        // Verify we got the expected number of predictions
        Assert.AreEqual(120, result.Y.Length, "Should have 100 training + 20 forecast steps.");
        Assert.IsTrue(result.CovariatePart.Skip(100).Any(c => c != 0), "Covariate effect should be non-zero in forecast period.");
    }

    /// <summary>
    /// Tests that providing forecastCovariates overrides the CovariateExtension setting.
    /// </summary>
    [TestMethod]
    public void Test_Predict_ExplicitForecastCovariates_OverridesExtensionSetting()
    {
        // Create time series and covariate
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None // Would throw if used
        };
        model.TrainingTimeSteps = 100; // Use all data for training
        model.SetCovariates(new List<TimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 }); // mu, beta, phi, sigma

        // Provide explicit forecast covariates
        var forecastCov = new TimeSeries(TimeInterval.OneMonth, new DateTime(2008, 5, 1),
            Enumerable.Range(0, 20).Select(i => 60.0 + i * 0.5).ToArray());

        // Should succeed because we provided explicit forecast covariates
        var result = model.Predict(model.Parameters.Select(p => p.Value).ToArray(),
            forecastSteps: 20, seed: 12345, forecastCovariates: new List<TimeSeries> { forecastCov });

        Assert.AreEqual(120, result.Y.Length, "Should have 100 training + 20 forecast steps.");
    }

    /// <summary>
    /// Tests that GenerateRandomValues with CovariateExtensionMethod.None throws when sampleSize exceeds covariate length.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_GenerateRandomValues_CovariateExtensionNone_ThrowsWhenInsufficient()
    {
        // Create time series and short covariate
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None
        };
        model.SetCovariates(new List<TimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 }); // mu, beta, phi, sigma

        // Try to generate 200 values with only 100 covariate observations - should throw
        model.GenerateRandomValues(200, seed: 12345);
    }

    /// <summary>
    /// Tests that GenerateRandomValues with CovariateExtensionMethod.BlockBootstrap extends covariates.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_CovariateExtensionBlockBootstrap_ExtendsCovariates()
    {
        // Create time series and covariate
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.BlockBootstrap
        };
        model.SetCovariates(new List<TimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 }); // mu, beta, phi, sigma

        // Generate 200 values with only 100 covariate observations - should succeed
        var generated = model.GenerateRandomValues(200, seed: 12345);

        Assert.AreEqual(200, generated.Length, "Should generate 200 values.");
        Assert.IsTrue(generated.All(v => !double.IsNaN(v) && !double.IsInfinity(v)), "All values should be finite.");
    }

    /// <summary>
    /// Tests that GenerateRandomValues with CovariateExtensionMethod.KNN extends covariates.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_CovariateExtensionKNN_ExtendsCovariates()
    {
        // Create time series and covariate
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN
        };
        model.SetCovariates(new List<TimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 }); // mu, beta, phi, sigma

        // Generate 200 values with only 100 covariate observations - should succeed
        var generated = model.GenerateRandomValues(200, seed: 12345);

        Assert.AreEqual(200, generated.Length, "Should generate 200 values.");
        Assert.IsTrue(generated.All(v => !double.IsNaN(v) && !double.IsInfinity(v)), "All values should be finite.");
    }

    /// <summary>
    /// Tests that providing explicit generateCovariates overrides the CovariateExtension setting for GenerateRandomValues.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomValues_ExplicitCovariates_OverridesExtensionSetting()
    {
        // Create time series and short covariate
        var values = Enumerable.Range(0, 100).Select(i => 50.0 + i * 0.1 + new Random(i).NextDouble() * 5).ToArray();
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var covariate = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 100).Select(i => 10.0 + i * 0.5).ToArray());

        var model = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None // Would throw if used
        };
        model.SetCovariates(new List<TimeSeries> { covariate });
        model.SetDefaultParameters();
        model.SetParameterValues(new[] { 50.0, 0.1, 0.5, 1.0 }); // mu, beta, phi, sigma

        // Provide explicit covariates for generation
        var extendedCov = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 200).Select(i => 10.0 + i * 0.5).ToArray());

        // Should succeed because we provided explicit covariates
        var generated = model.GenerateRandomValues(200, seed: 12345, generateCovariates: new List<TimeSeries> { extendedCov });

        Assert.AreEqual(200, generated.Length, "Should generate 200 values.");
    }

    /// <summary>
    /// Tests that CovariateExtension property is serialized and deserialized correctly.
    /// </summary>
    [TestMethod]
    public void Test_CovariateExtension_XmlSerialization()
    {
        // Create model with specific CovariateExtension
        var values = Enumerable.Range(0, 50).Select(i => 50.0 + i * 0.1).ToArray();
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);

        var original = new ARIMAX(ts)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN
        };

        // Serialize
        var xElement = original.ToXElement();

        // Deserialize
        var restored = new ARIMAX(ts, xElement);

        Assert.AreEqual(ARIMAX.CovariateExtensionMethod.KNN, restored.CovariateExtension,
            "CovariateExtension should be preserved through serialization.");
    }

    #endregion

    #region R Validation Tests

    /// <summary>
    /// Tests MLE estimation of ARMA(1,1) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit's ARIMAX model (configured as ARMA) produces comparable parameter estimates to R.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Classic Box-Jenkins airline passenger dataset (144 monthly observations, 1949-1960).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- arima(AirPassengers, order = c(1, 0, 1))
    /// # intercept = 315.2158, ar1 = 0.9325, ma1 = 0.4273, sigma = 30.99516
    /// </code>
    /// </para>
    /// <para>
    /// This test validates that ARIMAX configured with AROrderP=1, MAOrderQ=1, and no covariates
    /// produces estimates comparable to R's arima() function. A 10% tolerance accounts for
    /// differences in optimization algorithms and the complexity of mixed ARMA models.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_ARMA11_RTest();
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        Assert.AreEqual(true, mle.IsEstimated, "ARMA(1,1) model fitting failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of simple linear regression on real macroeconomic data against R's lm() results.
    /// Validates that RMC-BestFit's ARIMAX model (configured as regression) produces comparable estimates to R.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Quarterly US macroeconomic data - Consumption regressed on Income (188 observations, 1970-2016).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- lm(consumption ~ income)
    /// # intercept = 0.54510, income = 0.28060, sigma = 0.6026
    /// </code>
    /// </para>
    /// <para>
    /// Model: Consumption = a + b × Income + ε
    /// </para>
    /// <para>
    /// This test validates ARIMAX configured with AROrderP=0, MAOrderQ=0, and Income as a single covariate.
    /// A 10% tolerance accounts for differences in optimization algorithms.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_SimpleRegression_RValidation()
    {
        var data = RealTimeSeriesData.GetSimpleLinearRegression_RTest();
        var model = new ARIMAX(data.Y)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.Y.Count;
        model.SetCovariates(new List<TimeSeries> { data.X });
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        Assert.AreEqual(true, mle.IsEstimated, "Simple regression model fitting failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of multiple linear regression on real macroeconomic data against R's lm() results.
    /// Validates that RMC-BestFit's ARIMAX model produces comparable estimates to R for multiple predictors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Quarterly US macroeconomic data - Consumption regressed on Income, Production, Savings, and
    /// Unemployment (188 observations, 1970-2016).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- lm(consumption ~ income + production + savings + unemployment)
    /// # intercept = 0.26729, income = 0.71449, production = 0.04589,
    /// # savings = -0.04527, unemployment = -0.20477, sigma = 0.3286
    /// </code>
    /// </para>
    /// <para>
    /// Model: Consumption = a + β₁×Income + β₂×Production + β₃×Savings + β₄×Unemployment + ε
    /// </para>
    /// <para>
    /// This test validates ARIMAX configured with AROrderP=0, MAOrderQ=0, and four covariates.
    /// A 10% tolerance accounts for differences in optimization algorithms.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_MultipleRegression_RValidation()
    {
        var data = RealTimeSeriesData.GetMultipleLinearRegression_RTest();
        var model = new ARIMAX(data.Y)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.Y.Count;
        model.SetCovariates(data.X);
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        Assert.AreEqual(true, mle.IsEstimated, "Multiple regression model fitting failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    #endregion
}
