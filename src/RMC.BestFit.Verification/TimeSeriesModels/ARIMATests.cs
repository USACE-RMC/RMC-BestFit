using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;

namespace RMC.BestFit.Verification.TimeSeriesModels;

/// <summary>
/// Unit tests for the <see cref="ARIMA"/> class.
/// Tests autoregressive moving average (ARIMA) time-series models.
/// </summary>
/// <remarks>
/// <para>
/// ARIMA(p,q) models combine autoregressive (AR) and moving average (MA) components:
/// Y(t) = μ + φ₁(Y(t-1) - μ) + ... + φₚ(Y(t-p) - μ) + ε(t) + θ₁ε(t-1) + ... + θqε(t-q)
/// where ε(t) ~ N(0, σ²)
/// </para>
/// <para>
/// Common use cases in hydrology include modeling streamflow persistence,
/// annual flood series, and precipitation patterns.
/// </para>
/// </remarks>
[TestClass]
public class ARIMATests
{
    #region Test Data Helper

    /// <summary>
    /// Creates a sample time series with 50 observations.
    /// </summary>
    private static TimeSeries CreateSampleTimeSeries()
    {
        var ts = new TimeSeries(TimeInterval.OneYear, new DateTime(1970, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(12345);

        // Generate AR(1)-like data with persistence
        double mean = 1000;
        double phi = 0.6;
        double sigma = 100;
        double prevValue = mean;

        for (int i = 0; i < ts.Count; i++)
        {
            double innovation = rng.NextDouble() * 2 - 1; // Uniform [-1, 1]
            double value = mean + phi * (prevValue - mean) + sigma * innovation;
            ts[i].Value = value;
            prevValue = value;
        }

        return ts;
    }

    /// <summary>
    /// Creates a short time series for edge case testing.
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
    /// Creates a monthly time series for seasonal analysis.
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

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the empty constructor creates a default ARIMA(1,0) model with intercept.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new ARIMA();

        Assert.IsNotNull(model);
        Assert.AreEqual(1, model.POrder);
        Assert.AreEqual(0, model.QOrder);
        Assert.IsTrue(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that the constructor with time series correctly assigns the data.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithTimeSeries_SetsData()
    {
        var ts = CreateSampleTimeSeries();

        var model = new ARIMA(ts);

        Assert.AreSame(ts, model.TimeSeries);
        Assert.AreEqual(1, model.POrder);
        Assert.AreEqual(0, model.QOrder);
    }

    /// <summary>
    /// Tests that the constructor with pOrder and qOrder correctly sets ARIMA orders.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithOrders_SetsARIMAOrders()
    {
        var ts = CreateSampleTimeSeries();

        var model = new ARIMA(ts, pOrder: 2, qOrder: 1);

        Assert.AreEqual(2, model.POrder);
        Assert.AreEqual(1, model.QOrder);
    }

    /// <summary>
    /// Tests that the constructor with includeIntercept=false correctly disables the intercept.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithNoIntercept_SetsIncludeIntercept()
    {
        var ts = CreateSampleTimeSeries();

        var model = new ARIMA(ts, includeIntercept: false);

        Assert.IsFalse(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that the XElement constructor correctly restores a serialized model.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 2, qOrder: 1);
        original.Parameters[0].Value = 999.0;
        var xElement = original.ToXElement();

        var restored = new ARIMA(ts, xElement);

        Assert.AreEqual(2, restored.POrder);
        Assert.AreEqual(1, restored.QOrder);
        Assert.AreEqual(999.0, restored.Parameters[0].Value);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Tests that the TimeSeries property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_TimeSeries_SetAndGet()
    {
        var model = new ARIMA();
        var ts = CreateSampleTimeSeries();

        model.TimeSeries = ts;

        Assert.AreSame(ts, model.TimeSeries);
    }

    /// <summary>
    /// Tests that the POrder property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_POrder_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        model.POrder = 3;

        Assert.AreEqual(3, model.POrder);
    }

    /// <summary>
    /// Tests that changing POrder updates the parameter count accordingly.
    /// </summary>
    [TestMethod]
    public void Test_POrder_Change_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        int initialParams = model.NumberOfParameters;

        model.POrder = 3;

        // Should have 2 more AR parameters
        Assert.AreEqual(initialParams + 2, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that the QOrder property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_QOrder_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        model.QOrder = 2;

        Assert.AreEqual(2, model.QOrder);
    }

    /// <summary>
    /// Tests that changing QOrder updates the parameter count accordingly.
    /// </summary>
    [TestMethod]
    public void Test_QOrder_Change_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        int initialParams = model.NumberOfParameters;

        model.QOrder = 2;

        // Should have 2 more MA parameters
        Assert.AreEqual(initialParams + 2, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that the IncludeIntercept property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_IncludeIntercept_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        model.IncludeIntercept = false;

        Assert.IsFalse(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that disabling IncludeIntercept reduces the parameter count by one.
    /// </summary>
    [TestMethod]
    public void Test_IncludeIntercept_Change_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, includeIntercept: true);
        int initialParams = model.NumberOfParameters;

        model.IncludeIntercept = false;

        // Should have 1 fewer parameter (no intercept)
        Assert.AreEqual(initialParams - 1, model.NumberOfParameters);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>
    /// Tests that AR(1) without intercept has exactly 2 parameters (phi and sigma).
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_AR1_TwoParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0, includeIntercept: false);

        // AR(1) without intercept: φ₁ + σ = 2 parameters
        Assert.AreEqual(2, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that AR(1) with intercept has exactly 3 parameters (mu, phi, and sigma).
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_AR1_WithIntercept_ThreeParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0, includeIntercept: true);

        // AR(1) with intercept: μ + φ₁ + σ = 3 parameters
        Assert.AreEqual(3, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that ARIMA(2,1) with intercept has exactly 5 parameters.
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_ARIMA21_FiveParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 2, qOrder: 1, includeIntercept: true);

        // ARIMA(2,1) with intercept: μ + φ₁ + φ₂ + θ₁ + σ = 5 parameters
        Assert.AreEqual(5, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that parameters have correct names (Intercept, AR, MA, Scale).
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_HasCorrectNames()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 2, qOrder: 1, includeIntercept: true);

        Assert.IsTrue(model.Parameters[0].Name.Contains("Intercept"));
        Assert.IsTrue(model.Parameters[1].Name.Contains("AR"));
        Assert.IsTrue(model.Parameters[2].Name.Contains("AR"));
        Assert.IsTrue(model.Parameters[3].Name.Contains("MA"));
        Assert.IsTrue(model.Parameters[4].Name.Contains("Scale"));
    }

    /// <summary>
    /// Tests that AR parameters have bounds [-2, 2] for stationarity constraints.
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_ARBounds()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);

        // AR parameters should have bounds [-2, 2]
        var arParam = model.Parameters.First(p => p.Name.Contains("AR"));
        Assert.AreEqual(-2.0, arParam.LowerBound);
        Assert.AreEqual(2.0, arParam.UpperBound);
    }

    /// <summary>
    /// Tests that MA parameters have bounds [-2, 2] for invertibility constraints.
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_MABounds()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 0, qOrder: 1);

        // MA parameters should have bounds [-2, 2]
        var maParam = model.Parameters.First(p => p.Name.Contains("MA"));
        Assert.AreEqual(-2.0, maParam.LowerBound);
        Assert.AreEqual(2.0, maParam.UpperBound);
    }

    /// <summary>
    /// Tests that the scale parameter is constrained to be positive.
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_ScaleIsPositive()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        var scaleParam = model.Parameters.First(p => p.Name.Contains("Scale"));
        Assert.IsTrue(scaleParam.IsPositive);
        Assert.IsTrue(scaleParam.LowerBound > 0);
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
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(dataLogLH));
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns a finite value for AR(2) model.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_AR2_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 2, qOrder: 0);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns a finite value for ARIMA(1,1) model.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_ARIMA11_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 1);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns MinValue when TimeSeries is null.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_NullTimeSeries_ReturnsMinValue()
    {
        var model = new ARIMA();

        double result = model.DataLogLikelihood(new double[] { 0, 0, 1 });

        Assert.AreEqual(double.MinValue, result);
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns MinValue when a parameter is NaN.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_NaNParameter_ReturnsMinValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        double result = model.DataLogLikelihood(new double[] { double.NaN, 0, 1 });

        Assert.AreEqual(double.MinValue, result);
    }

    /// <summary>
    /// Tests that PriorLogLikelihood returns a finite value for valid parameters.
    /// </summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    /// <summary>
    /// Tests that PointwiseDataLogLikelihood returns correct count (TrainingTimeSteps - POrder observations).
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_ReturnsCorrectCount()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 2, qOrder: 0);
        // Use full series for training to get predictable count
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        // Should have TrainingTimeSteps - POrder observations
        Assert.AreEqual(ts.Count - 2, pointwise.Length);
    }

    /// <summary>
    /// Tests that the sum of pointwise log-likelihoods equals the total data log-likelihood.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_SumEqualsDataLogLikelihood()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);
        double dataLogLH = model.DataLogLikelihood(parameters);

        double sum = pointwise.Sum();
        Assert.AreEqual(dataLogLH, sum, 1e-6);
    }

    #endregion

    #region Predict Tests

    /// <summary>
    /// Tests that Predict with no forecast steps returns fitted values of TrainingTimeSteps length.
    /// </summary>
    [TestMethod]
    public void Test_Predict_NoForecast_ReturnsObservedLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);
        // Use full series for training so prediction matches observed length
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction = model.Predict(forecastSteps: 0);

        Assert.AreEqual(ts.Count, prediction.Length);
    }

    /// <summary>
    /// Tests that Predict with forecast steps returns extended array (TrainingTimeSteps + forecastSteps).
    /// </summary>
    [TestMethod]
    public void Test_Predict_WithForecast_ReturnsExtendedLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction = model.Predict(forecastSteps: 10);

        Assert.AreEqual(ts.Count + 10, prediction.Length);
    }

    /// <summary>
    /// Tests that deterministic forecasts (seed=-1) produce identical results.
    /// </summary>
    [TestMethod]
    public void Test_Predict_DeterministicForecast_NoSeed()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction1 = model.Predict(forecastSteps: 5, seed: -1);
        var prediction2 = model.Predict(forecastSteps: 5, seed: -1);

        // Deterministic forecasts should be identical
        for (int i = 0; i < prediction1.Length; i++)
        {
            Assert.AreEqual(prediction1[i], prediction2[i], 1e-10);
        }
    }

    /// <summary>
    /// Tests that stochastic forecasts with the same seed produce identical results.
    /// </summary>
    [TestMethod]
    public void Test_Predict_StochasticForecast_WithSeed()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction1 = model.Predict(forecastSteps: 5, seed: 12345);
        var prediction2 = model.Predict(forecastSteps: 5, seed: 12345);

        // Same seed should produce same results
        for (int i = 0; i < prediction1.Length; i++)
        {
            Assert.AreEqual(prediction1[i], prediction2[i], 1e-10);
        }
    }

    /// <summary>
    /// Tests that stochastic forecasts with different seeds produce different results.
    /// </summary>
    [TestMethod]
    public void Test_Predict_StochasticForecast_DifferentSeeds()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction1 = model.Predict(forecastSteps: 5, seed: 12345);
        var prediction2 = model.Predict(forecastSteps: 5, seed: 54321);

        // Different seeds should produce different forecast values
        bool anyDifferent = false;
        // Loop through forecast portion (starts at TrainingTimeSteps)
        for (int i = model.TrainingTimeSteps; i < prediction1.Length; i++)
        {
            if (Math.Abs(prediction1[i] - prediction2[i]) > 1e-10)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.IsTrue(anyDifferent);
    }

    /// <summary>
    /// Tests that Predict throws InvalidOperationException when TimeSeries is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_Predict_NullTimeSeries_ThrowsException()
    {
        var model = new ARIMA();
        model.Predict();
    }

    #endregion

    #region GenerateRandomSeries Tests

    /// <summary>
    /// Tests that GenerateRandomSeries returns a series with the requested length.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomSeries_ReturnsCorrectLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        var generated = model.GenerateRandomSeries(100, seed: 12345);

        Assert.AreEqual(100, generated.Count);
    }

    /// <summary>
    /// Tests that GenerateRandomSeries with the same seed produces identical results.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomSeries_SameSeed_SameResults()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        var generated1 = model.GenerateRandomSeries(50, seed: 12345);
        var generated2 = model.GenerateRandomSeries(50, seed: 12345);

        for (int i = 0; i < generated1.Count; i++)
        {
            Assert.AreEqual(generated1[i].Value, generated2[i].Value, 1e-10);
        }
    }

    #endregion

    #region Stationarity and Invertibility Tests

    /// <summary>
    /// Tests that IsStationary returns true for AR(1) with coefficient less than 1.
    /// </summary>
    [TestMethod]
    public void Test_IsStationary_AR1_StationaryCoefficient()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);

        // Set φ₁ = 0.5 (stationary)
        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.5;

        Assert.IsTrue(model.IsStationary());
    }

    /// <summary>
    /// Tests that IsStationary returns false for AR(1) with coefficient greater than 1.
    /// </summary>
    [TestMethod]
    public void Test_IsStationary_AR1_NonStationaryCoefficient()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);

        // Set φ₁ = 1.1 (non-stationary)
        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 1.1;

        Assert.IsFalse(model.IsStationary());
    }

    /// <summary>
    /// Tests that IsInvertible returns true for MA(1) with coefficient less than 1.
    /// </summary>
    [TestMethod]
    public void Test_IsInvertible_MA1_InvertibleCoefficient()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 0, qOrder: 1);

        // Set θ₁ = 0.5 (invertible)
        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.5;

        Assert.IsTrue(model.IsInvertible());
    }

    /// <summary>
    /// Tests that IsInvertible returns false for MA(1) with coefficient greater than 1.
    /// </summary>
    [TestMethod]
    public void Test_IsInvertible_MA1_NonInvertibleCoefficient()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 0, qOrder: 1);

        // Set θ₁ = 1.1 (non-invertible)
        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 1.1;

        Assert.IsFalse(model.IsInvertible());
    }

    /// <summary>
    /// Tests that IsStationary returns true when there is no AR component (pure MA).
    /// </summary>
    [TestMethod]
    public void Test_IsStationary_NoARComponent_ReturnsTrue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 0, qOrder: 1);

        Assert.IsTrue(model.IsStationary());
    }

    /// <summary>
    /// Tests that IsInvertible returns true when there is no MA component (pure AR).
    /// </summary>
    [TestMethod]
    public void Test_IsInvertible_NoMAComponent_ReturnsTrue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);

        Assert.IsTrue(model.IsInvertible());
    }

    #endregion

    #region Clone Tests

    /// <summary>
    /// Tests that Clone creates an independent copy with the same property values.
    /// </summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 2, qOrder: 1);

        var clone = (ARIMA)original.Clone();

        Assert.AreNotSame(original, clone);
        Assert.AreEqual(original.POrder, clone.POrder);
        Assert.AreEqual(original.QOrder, clone.QOrder);
    }

    /// <summary>
    /// Tests that modifying parameters in the original does not affect the clone.
    /// </summary>
    [TestMethod]
    public void Test_Clone_ParametersAreIndependent()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts);
        double originalValue = original.Parameters[0].Value;

        var clone = (ARIMA)original.Clone();
        original.Parameters[0].Value = 99999;

        Assert.AreEqual(originalValue, clone.Parameters[0].Value);
    }

    /// <summary>
    /// Tests that Clone preserves the IncludeIntercept setting.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesIncludeIntercept()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, includeIntercept: false);

        var clone = (ARIMA)original.Clone();

        Assert.AreEqual(original.IncludeIntercept, clone.IncludeIntercept);
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Tests that ToXElement produces an element with the correct root name.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsARIMAElement()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        var xElement = model.ToXElement();

        Assert.AreEqual("ARIMA", xElement.Name.LocalName);
    }

    /// <summary>
    /// Tests that ToXElement includes the POrder attribute.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsPOrderAttribute()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 2);

        var xElement = model.ToXElement();

        Assert.AreEqual("2", xElement.Attribute("POrder")?.Value);
    }

    /// <summary>
    /// Tests that ToXElement includes the QOrder attribute.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsQOrderAttribute()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, qOrder: 1);

        var xElement = model.ToXElement();

        Assert.AreEqual("1", xElement.Attribute("QOrder")?.Value);
    }

    /// <summary>
    /// Tests that serialization and deserialization preserves all model properties.
    /// </summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 2, qOrder: 1, includeIntercept: false);

        var xElement = original.ToXElement();
        var restored = new ARIMA(ts, xElement);

        Assert.AreEqual(original.POrder, restored.POrder);
        Assert.AreEqual(original.QOrder, restored.QOrder);
        Assert.AreEqual(original.IncludeIntercept, restored.IncludeIntercept);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Tests that Validate returns true for a properly configured model.
    /// </summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that Validate returns false when TimeSeries is null.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NullTimeSeries_ReturnsFalse()
    {
        var model = new ARIMA();

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Time series")));
    }

    /// <summary>
    /// Tests that Validate returns false when the time series has fewer than 10 observations.
    /// </summary>
    [TestMethod]
    public void Test_Validate_TooShortTimeSeries_ReturnsFalse()
    {
        var ts = new TimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), new DateTime(2004, 1, 1));
        for (int i = 0; i < ts.Count; i++) ts[i].Value = i;
        var model = new ARIMA(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("10 observations")));
    }

    /// <summary>
    /// Tests that Validate returns false when POrder is negative.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NegativePOrder_ReturnsFalse()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);
        model.POrder = -1;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that Validate returns false when QOrder is negative.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NegativeQOrder_ReturnsFalse()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);
        model.QOrder = -1;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that Validate returns false when both POrder and QOrder are zero.
    /// </summary>
    [TestMethod]
    public void Test_Validate_BothOrdersZero_ReturnsFalse()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 0, qOrder: 0);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("greater than 0")));
    }

    /// <summary>
    /// Tests that Validate includes a warning when the model is non-stationary.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NonStationary_IncludesWarning()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 1.5; // Non-stationary

        var (isValid, messages) = model.Validate();

        // Should still be valid (warning, not error)
        Assert.IsTrue(messages.Any(m => m.Contains("WARNING") && m.Contains("stationarity")));
    }

    #endregion

    #region SetParameterValues Tests

    /// <summary>
    /// Tests that SetParameterValues correctly updates all parameter values.
    /// </summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);

        var newValues = new double[] { 1000.0, 0.5, 50.0 };
        model.SetParameterValues(newValues);

        Assert.AreEqual(1000.0, model.Parameters[0].Value);
        Assert.AreEqual(0.5, model.Parameters[1].Value);
        Assert.AreEqual(50.0, model.Parameters[2].Value);
    }

    /// <summary>
    /// Tests that SetParameterValues throws ArgumentNullException for null input.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_SetParameterValues_NullParameters_ThrowsException()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        model.SetParameterValues(null!);
    }

    /// <summary>
    /// Tests that SetParameterValues throws ArgumentException for wrong parameter count.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_SetParameterValues_WrongCount_ThrowsException()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        model.SetParameterValues(new double[] { 1.0 });
    }

    #endregion

    #region Engineering Application Tests

    /// <summary>
    /// Tests that AR(1) model is suitable for streamflow persistence analysis.
    /// </summary>
    [TestMethod]
    public void Test_ARIMA_StreamflowPersistence()
    {
        // Streamflow often exhibits year-to-year persistence
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);
        Assert.IsFalse(double.IsNegativeInfinity(dataLogLH));
    }

    /// <summary>
    /// Tests that ARIMA model works with monthly time series data.
    /// </summary>
    [TestMethod]
    public void Test_ARIMA_MonthlyAnalysis()
    {
        var ts = CreateMonthlyTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 1);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests that higher-order ARIMA(3,2) model has correct parameter count.
    /// </summary>
    [TestMethod]
    public void Test_ARIMA_HigherOrderModel()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 3, qOrder: 2);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        // ARIMA(3,2) should have: μ + φ₁ + φ₂ + φ₃ + θ₁ + θ₂ + σ = 7 parameters
        Assert.AreEqual(7, model.NumberOfParameters);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests that ARIMA model works with minimum valid time series length.
    /// </summary>
    [TestMethod]
    public void Test_ARIMA_MinimumValidTimeSeries()
    {
        var ts = CreateShortTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        // Use full series for training to ensure validation passes with 15 observations
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests that ARIMA model can be created with large AR and MA orders.
    /// </summary>
    [TestMethod]
    public void Test_ARIMA_LargeOrders()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 5, qOrder: 5);

        Assert.AreEqual(5, model.POrder);
        Assert.AreEqual(5, model.QOrder);
    }

    /// <summary>
    /// Tests that pure MA model (p=0) is valid.
    /// </summary>
    [TestMethod]
    public void Test_ARIMA_PureMA()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 0, qOrder: 2);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests that pure AR model (q=0) is valid.
    /// </summary>
    [TestMethod]
    public void Test_ARIMA_PureAR()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 2, qOrder: 0);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    #endregion

    #region DOrder (Differencing) Tests

    /// <summary>
    /// Tests that the DOrder property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_DOrder_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        model.DOrder = 1;

        Assert.AreEqual(1, model.DOrder);
    }

    /// <summary>
    /// Tests that ARIMA(1,1,0) model validates correctly.
    /// </summary>
    [TestMethod]
    public void Test_ARIMA110_Validate()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        model.DOrder = 1;

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that ARIMA(1,2,1) model validates correctly.
    /// </summary>
    [TestMethod]
    public void Test_ARIMA121_Validate()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 1);
        model.DOrder = 2;

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that DOrder is preserved in XML serialization.
    /// </summary>
    [TestMethod]
    public void Test_DOrder_XmlSerialization()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 1, qOrder: 1);
        original.DOrder = 2;

        var xElement = original.ToXElement();
        var restored = new ARIMA(ts, xElement);

        Assert.AreEqual(original.DOrder, restored.DOrder);
    }

    /// <summary>
    /// Tests that Clone preserves the DOrder property.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesDOrder()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 1, qOrder: 0);
        original.DOrder = 1;

        var clone = (ARIMA)original.Clone();

        Assert.AreEqual(original.DOrder, clone.DOrder);
    }

    /// <summary>
    /// Tests that DifferencedSeries property returns the differenced data when DOrder > 0.
    /// DifferencedSeries is based on TrainingTimeSeries, so its length is TrainingTimeSteps - DOrder.
    /// </summary>
    [TestMethod]
    public void Test_DifferencedSeries_ReturnsCorrectLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        // Use full series for training to make the test predictable
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;
        model.DOrder = 1;

        var differenced = model.DifferencedSeries;

        // Differencing reduces the training series length by DOrder
        Assert.AreEqual(ts.Count - 1, differenced.Count);
    }

    /// <summary>
    /// Tests that DifferencedSeries returns the training series when DOrder = 0.
    /// </summary>
    [TestMethod]
    public void Test_DifferencedSeries_DOrderZero_ReturnsOriginal()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        // Use full series for training to make the test predictable
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;
        model.DOrder = 0;

        var differenced = model.DifferencedSeries;

        Assert.AreEqual(ts.Count, differenced.Count);
    }

    #endregion

    #region Transform Tests

    /// <summary>
    /// Tests that TransformType property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_TransformType_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        model.TransformType = RMC.BestFit.Models.Transform.Logarithmic;

        Assert.AreEqual(RMC.BestFit.Models.Transform.Logarithmic, model.TransformType);
    }

    /// <summary>
    /// Tests that model with Logarithmic transform validates correctly.
    /// </summary>
    [TestMethod]
    public void Test_LogTransform_Validate()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        model.TransformType = RMC.BestFit.Models.Transform.Logarithmic;

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that model with BoxCox transform validates correctly.
    /// </summary>
    [TestMethod]
    public void Test_BoxCoxTransform_Validate()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        model.TransformType = RMC.BestFit.Models.Transform.BoxCox;

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that TransformType is preserved in XML serialization.
    /// </summary>
    [TestMethod]
    public void Test_TransformType_XmlSerialization()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 1, qOrder: 0);
        original.TransformType = RMC.BestFit.Models.Transform.Logarithmic;

        var xElement = original.ToXElement();
        var restored = new ARIMA(ts, xElement);

        Assert.AreEqual(original.TransformType, restored.TransformType);
    }

    /// <summary>
    /// Tests that Clone preserves the TransformType property.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesTransformType()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 1, qOrder: 0);
        original.TransformType = RMC.BestFit.Models.Transform.YeoJohnson;

        var clone = (ARIMA)original.Clone();

        Assert.AreEqual(original.TransformType, clone.TransformType);
    }

    #endregion

    #region TrainingTimeSteps Tests

    /// <summary>
    /// Tests that TrainingTimeSteps can be set and retrieved correctly.
    /// Must set UseDefaultTrainingSteps = false to prevent auto-calculation.
    /// </summary>
    [TestMethod]
    public void Test_TrainingTimeSteps_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);
        model.UseDefaultTrainingSteps = false;

        model.TrainingTimeSteps = 35;

        Assert.AreEqual(35, model.TrainingTimeSteps);
    }

    /// <summary>
    /// Tests that ForecastingTimeSteps is computed correctly from TimeSeries length and TrainingTimeSteps.
    /// </summary>
    [TestMethod]
    public void Test_ForecastingTimeSteps_ComputedCorrectly()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = 40;

        // ForecastingTimeSteps = TimeSeries.Count - TrainingTimeSteps
        Assert.AreEqual(ts.Count - 40, model.ForecastingTimeSteps);
    }

    /// <summary>
    /// Tests that UseDefaultTrainingSteps can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_UseDefaultTrainingSteps_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        model.UseDefaultTrainingSteps = false;

        Assert.IsFalse(model.UseDefaultTrainingSteps);
    }

    /// <summary>
    /// Tests that TrainingTimeSteps is preserved in XML serialization.
    /// </summary>
    [TestMethod]
    public void Test_TrainingTimeSteps_XmlSerialization()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 1, qOrder: 0);
        original.UseDefaultTrainingSteps = false;
        original.TrainingTimeSteps = 35;

        var xElement = original.ToXElement();
        var restored = new ARIMA(ts, xElement);

        Assert.AreEqual(original.TrainingTimeSteps, restored.TrainingTimeSteps);
    }

    /// <summary>
    /// Tests that Clone preserves training configuration.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesTrainingTimeSteps()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 1, qOrder: 0);
        original.UseDefaultTrainingSteps = false;
        original.TrainingTimeSteps = 35;

        var clone = (ARIMA)original.Clone();

        Assert.AreEqual(original.TrainingTimeSteps, clone.TrainingTimeSteps);
        Assert.AreEqual(original.UseDefaultTrainingSteps, clone.UseDefaultTrainingSteps);
    }

    #endregion

    #region UseJeffreysRuleForScale Tests

    /// <summary>
    /// Tests that UseJeffreysRuleForScale can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_UseJeffreysRuleForScale_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts);

        model.UseJeffreysRuleForScale = false;

        Assert.IsFalse(model.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// Tests that UseJeffreysRuleForScale is preserved in XML serialization.
    /// </summary>
    [TestMethod]
    public void Test_UseJeffreysRuleForScale_XmlSerialization()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 1, qOrder: 0);
        original.UseJeffreysRuleForScale = false;

        var xElement = original.ToXElement();
        var restored = new ARIMA(ts, xElement);

        Assert.AreEqual(original.UseJeffreysRuleForScale, restored.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// Tests that Clone preserves the UseJeffreysRuleForScale property.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesUseJeffreysRuleForScale()
    {
        var ts = CreateSampleTimeSeries();
        var original = new ARIMA(ts, pOrder: 1, qOrder: 0);
        original.UseJeffreysRuleForScale = false;

        var clone = (ARIMA)original.Clone();

        Assert.AreEqual(original.UseJeffreysRuleForScale, clone.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// Tests that PriorLogLikelihood returns finite values with Jeffreys rule enabled.
    /// </summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_WithJeffreysRule()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        model.UseJeffreysRuleForScale = true;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    /// <summary>
    /// Tests that PriorLogLikelihood returns finite values without Jeffreys rule.
    /// </summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_WithoutJeffreysRule()
    {
        var ts = CreateSampleTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, qOrder: 0);
        model.UseJeffreysRuleForScale = false;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    #endregion

    #region MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of ARIMA(1,1) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 10% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ARIMA(1,1) model: Y(t) = μ + φ₁(Y(t-1) - μ) + ε(t) + θ₁ε(t-1), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.6, θ₁ = 0.3, σ = 5.
    /// A 10% tolerance is used because mixed ARMA models are more challenging to estimate than
    /// pure AR or MA models due to parameter interaction and identification issues.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA11()
    {
        var data = SyntheticTimeSeriesData.GetARIMA11Data(10, 0.6, 0.3, 5, 10000);
        var model = new ARIMA(data.TimeSeries, pOrder: 1, qOrder: 1, includeIntercept: true);
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
    /// Tests MLE estimation of ARIMA(2,1) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 10% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ARIMA(2,1) model: Y(t) = μ + φ₁(Y(t-1) - μ) + φ₂(Y(t-2) - μ) + ε(t) + θ₁ε(t-1).
    /// </para>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.5, φ₂ = -0.3, θ₁ = 0.3, σ = 5.
    /// Higher-order AR components add complexity to estimation.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA21()
    {
        var data = SyntheticTimeSeriesData.GetARIMA21Data(10, 0.5, -0.3, 0.3, 5, 10000);
        var model = new ARIMA(data.TimeSeries, pOrder: 2, qOrder: 1, includeIntercept: true);
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
    /// Tests MLE estimation of ARIMA(1,2) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 10% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ARIMA(1,2) model: Y(t) = μ + φ₁(Y(t-1) - μ) + ε(t) + θ₁ε(t-1) + θ₂ε(t-2).
    /// </para>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.5, θ₁ = 0.3, θ₂ = 0.5, σ = 5.
    /// Higher-order MA components add complexity to estimation.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA12()
    {
        var data = SyntheticTimeSeriesData.GetARIMA12Data(10, 0.5, 0.3, 0.5, 5, 10000);
        var model = new ARIMA(data.TimeSeries, pOrder: 1, qOrder: 2, includeIntercept: true);
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
    /// Tests MLE estimation of ARIMA(2,2) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 10% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ARIMA(2,2) model: Y(t) = μ + φ₁(Y(t-1) - μ) + φ₂(Y(t-2) - μ) + ε(t) + θ₁ε(t-1) + θ₂ε(t-2).
    /// </para>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.5, φ₂ = -0.3, θ₁ = 0.3, θ₂ = -0.2, σ = 5.
    /// This is the most complex ARMA model tested, with both higher-order AR and MA components.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA22()
    {
        var data = SyntheticTimeSeriesData.GetARIMA22Data(10, 0.5, -0.3, 0.3, -0.2, 5, 10000);
        var model = new ARIMA(data.TimeSeries, pOrder: 2, qOrder: 2, includeIntercept: true);
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
    /// Tests that ARIMA model can fit pure AR(1) data when configured with p=1, q=0.
    /// Validates that ARIMA subsumes AR as a special case, recovering parameters within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When q=0, ARIMA(p,0) reduces to a pure AR(p) model.
    /// </para>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.6, σ = 5.
    /// Pure AR models are easier to estimate than mixed ARMA models, allowing tighter 5% tolerance.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA_FitsAR1()
    {
        var data = SyntheticTimeSeriesData.GetARIMA_AR1Data(10, 0.6, 5, 10000);
        var model = new ARIMA(data.TimeSeries, pOrder: 1, qOrder: 0, includeIntercept: true);
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
    /// Tests that ARIMA model can fit pure MA(1) data when configured with p=0, q=1.
    /// Validates that ARIMA subsumes MA as a special case, recovering parameters within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When p=0, ARIMA(0,q) reduces to a pure MA(q) model.
    /// </para>
    /// <para>
    /// True parameters: μ = 10, θ₁ = 0.5, σ = 5.
    /// Pure MA models are easier to estimate than mixed ARMA models, allowing tighter 5% tolerance.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA_FitsMA1()
    {
        var data = SyntheticTimeSeriesData.GetARIMA_MA1Data(10, 0.5, 5, 10000);
        var model = new ARIMA(data.TimeSeries, pOrder: 0, qOrder: 1, includeIntercept: true);
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

    #region R Validation Tests

    /// <summary>
    /// Tests MLE estimation of ARMA(1,1) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit produces comparable parameter estimates to R within 10% tolerance.
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
    /// This test validates against real-world data where the true parameters are unknown,
    /// using R's well-established implementation as the reference. A 10% tolerance accounts
    /// for differences in optimization algorithms, likelihood formulations, and the complexity
    /// of mixed ARMA models with parameter interactions.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_ARMA11_RTest();
        var model = new ARIMA(data.TimeSeries, pOrder: 1, qOrder: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    #endregion
}
