using Numerics.Data;
using RMC.BestFit.Models;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Programmatic unit tests for the <c>AutoRegressive</c> time-series model.
/// </summary>
/// <remarks>
/// <para>
/// AR(p) models represent a time series as a linear combination of its past values:
/// Y(t) = μ + φ₁(Y(t-1) - μ) + ... + φₚ(Y(t-p) - μ) + ε(t),
/// where ε(t) ~ N(0, σ²).
/// </para>
/// <para>
/// These tests cover constructors, property round-trips, parameter layout,
/// log-likelihood evaluation at fixed parameters, prediction, generation,
/// stationarity, cloning, serialization, and validation. Estimation-driven
/// tests (MLE recovery / R parity) live in <c>RMC.BestFit.Verification</c>.
/// </para>
/// </remarks>
[TestClass]
public class AutoRegressiveTests
{
    #region Inline test fixtures

    /// <summary>
    /// Deterministic AR(1)-like fixture (50 annual observations) generated with a
    /// fixed seed so this file does not depend on the Verification project's
    /// shared <c>TestData</c>. The seed and structure match the original
    /// Verification helper so behaviour matches.
    /// </summary>
    private static NumericsTimeSeries CreateSampleTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1970, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(12345);

        // Generate AR(1)-like data
        const double mean = 500.0;
        const double phi = 0.7;
        const double sigma = 50.0;
        double prevValue = mean;

        for (int i = 0; i < ts.Count; i++)
        {
            double innovation = rng.NextDouble() * 2.0 - 1.0;
            double value = mean + phi * (prevValue - mean) + sigma * innovation;
            ts[i].Value = value;
            prevValue = value;
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
            ts[i].Value = 100.0 + i * 5.0;
        }
        return ts;
    }

    /// <summary>
    /// Linear-trend fixture used by validation tests that require well-behaved data.
    /// </summary>
    private static NumericsTimeSeries CreateTrendTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1970, 1, 1), new DateTime(2019, 1, 1));
        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = 100.0 + i * 2.0; // Linear trend
        }
        return ts;
    }

    /// <summary>
    /// Finite fixture that makes the Box-Cox lambda objective non-finite.
    /// </summary>
    private static NumericsTimeSeries CreateBoxCoxLambdaFailureTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1960, 1, 1), new DateTime(2019, 1, 1));

        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = i == 0 ? 0.0 : 10.0;
        }

        return ts;
    }

    /// <summary>
    /// Finite fixture that makes the Yeo-Johnson lambda objective non-finite.
    /// </summary>
    private static NumericsTimeSeries CreateYeoJohnsonLambdaFailureTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1960, 1, 1), new DateTime(2019, 1, 1));

        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = -double.MaxValue;
        }

        return ts;
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the empty constructor creates a default AR(1) model with expected initial values.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new AutoRegressive();

        Assert.IsNotNull(model);
        Assert.AreEqual(1, model.Order);
        Assert.IsTrue(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that the constructor with time series correctly assigns the data to the model.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithTimeSeries_SetsData()
    {
        var ts = CreateSampleTimeSeries();

        var model = new AutoRegressive(ts);

        Assert.AreSame(ts, model.TimeSeries);
        Assert.AreEqual(1, model.Order);
    }

    /// <summary>
    /// Tests that the constructor with order parameter correctly sets the AR order.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithOrder_SetsOrder()
    {
        var ts = CreateSampleTimeSeries();

        var model = new AutoRegressive(ts, order: 3);

        Assert.AreEqual(3, model.Order);
    }

    /// <summary>
    /// Tests that the constructor with includeIntercept=false correctly disables the intercept term.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithNoIntercept_SetsIncludeIntercept()
    {
        var ts = CreateSampleTimeSeries();

        var model = new AutoRegressive(ts, includeIntercept: false);

        Assert.IsFalse(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that the XElement constructor correctly restores model state from XML serialization.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var ts = CreateSampleTimeSeries();
        var original = new AutoRegressive(ts, order: 3);
        original.Parameters[0].Value = 555.0;
        var xElement = original.ToXElement();

        var restored = new AutoRegressive(ts, xElement);

        Assert.AreEqual(3, restored.Order);
        Assert.AreEqual(555.0, restored.Parameters[0].Value);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Tests that the NumericsTimeSeries property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_TimeSeries_SetAndGet()
    {
        var model = new AutoRegressive();
        var ts = CreateSampleTimeSeries();

        model.TimeSeries = ts;

        Assert.AreSame(ts, model.TimeSeries);
    }

    /// <summary>
    /// Tests that the Order property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_Order_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

        model.Order = 4;

        Assert.AreEqual(4, model.Order);
    }

    /// <summary>
    /// Tests that changing the Order property updates the number of model parameters accordingly.
    /// </summary>
    [TestMethod]
    public void Test_Order_Change_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);
        int initialParams = model.NumberOfParameters;

        model.Order = 3;

        // Should have 2 more AR parameters
        Assert.AreEqual(initialParams + 2, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that the IncludeIntercept property can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_IncludeIntercept_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

        model.IncludeIntercept = false;

        Assert.IsFalse(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that toggling IncludeIntercept updates the number of parameters by one.
    /// </summary>
    [TestMethod]
    public void Test_IncludeIntercept_Change_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, includeIntercept: true);
        int initialParams = model.NumberOfParameters;

        model.IncludeIntercept = false;

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
        var model = new AutoRegressive(ts, order: 1, includeIntercept: false);

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
        var model = new AutoRegressive(ts, order: 1, includeIntercept: true);

        // AR(1) with intercept: μ + φ₁ + σ = 3 parameters
        Assert.AreEqual(3, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that AR(3) with intercept has exactly 5 parameters (mu, phi1, phi2, phi3, and sigma).
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_AR3_FiveParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 3, includeIntercept: true);

        // AR(3) with intercept: μ + φ₁ + φ₂ + φ₃ + σ = 5 parameters
        Assert.AreEqual(5, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that AR parameters have correctly formatted names (Intercept, AR, Scale).
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_HasCorrectNames()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 2, includeIntercept: true);

        Assert.IsTrue(model.Parameters[0].Name.Contains("Intercept"));
        Assert.IsTrue(model.Parameters[1].Name.Contains("AR"));
        Assert.IsTrue(model.Parameters[2].Name.Contains("AR"));
        Assert.IsTrue(model.Parameters[3].Name.Contains("Scale"));
    }

    /// <summary>
    /// Tests that AR coefficient parameters have correct bounds of [-2, 2].
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_ARBounds()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);

        // AR parameters should have bounds [-2, 2]
        var arParam = model.Parameters.First(p => p.Name.Contains("AR"));
        Assert.AreEqual(-2.0, arParam.LowerBound);
        Assert.AreEqual(2.0, arParam.UpperBound);
    }

    /// <summary>
    /// Tests that the scale (sigma) parameter is constrained to positive values.
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_ScaleIsPositive()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

        var scaleParam = model.Parameters.First(p => p.Name.Contains("Scale"));
        Assert.IsTrue(scaleParam.IsPositive);
        Assert.IsTrue(scaleParam.LowerBound > 0);
    }

    /// <summary>
    /// Tests that the intercept parameter is initialized to the training time series mean.
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_InterceptInitializedToMean()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);
        // Use full series for training so training mean equals full series mean
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var interceptParam = model.Parameters.First(p => p.Name.Contains("Intercept"));
        double tsMean = ts.MeanValue();

        Assert.AreEqual(tsMean, interceptParam.Value, 1e-6);
    }

    #endregion

    #region LogLikelihood Tests

    /// <summary>
    /// Tests that DataLogLikelihood returns a finite value for valid AR(1) parameters.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(dataLogLH));
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns a finite value for valid AR(2) parameters.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_AR2_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 2);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns a finite value for valid AR(3) parameters.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_AR3_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 3);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns negative infinity when time series is null
    /// (canonical "impossible likelihood" sentinel per project conventions).
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_NullTimeSeries_ReturnsNegativeInfinity()
    {
        var model = new AutoRegressive();

        double result = model.DataLogLikelihood(new double[] { 0, 0, 1 });

        Assert.IsTrue(double.IsNegativeInfinity(result) || result == double.MinValue,
            $"Expected NegativeInfinity (or MinValue) sentinel for null time series; got {result}.");
    }

    /// <summary>
    /// Tests that DataLogLikelihood returns negative infinity when parameters contain NaN.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_NaNParameter_ReturnsNegativeInfinity()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

        double result = model.DataLogLikelihood(new double[] { double.NaN, 0, 1 });

        Assert.IsTrue(double.IsNegativeInfinity(result) || result == double.MinValue,
            $"Expected NegativeInfinity (or MinValue) sentinel for NaN parameter; got {result}.");
    }

    /// <summary>
    /// Tests that PriorLogLikelihood returns a finite value for valid default parameters.
    /// </summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    /// <summary>
    /// Tests that PointwiseDataLogLikelihood returns the correct number of observations (TrainingTimeSteps - order).
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_ReturnsCorrectCount()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 2);
        // Use full series for training to get predictable count
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        // Should have TrainingTimeSteps - Order observations
        Assert.AreEqual(ts.Count - 2, pointwise.Length);
    }

    /// <summary>
    /// Tests that the sum of pointwise log-likelihoods equals the total data log-likelihood.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_SumEqualsDataLogLikelihood()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);
        double dataLogLH = model.DataLogLikelihood(parameters);

        double sum = pointwise.Sum();
        Assert.AreEqual(dataLogLH, sum, 1e-6);
    }

    /// <summary>
    /// Tests that all pointwise data log-likelihood components are marked as Exact type.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_ReturnsCorrectTypes()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        Assert.IsTrue(components.All(c => c.Type == DataComponentType.Exact));
    }

    #endregion

    #region Predict Tests

    /// <summary>
    /// Tests that Predict with zero forecast steps returns predictions matching the TrainingTimeSteps length.
    /// </summary>
    [TestMethod]
    public void Test_Predict_NoForecast_ReturnsObservedLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);
        // Use full series for training so prediction matches observed length
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction = model.Predict(forecastSteps: 0);

        Assert.AreEqual(ts.Count, prediction.Length);
    }

    /// <summary>
    /// Tests that Predict with forecast steps returns extended predictions beyond TrainingTimeSteps.
    /// </summary>
    [TestMethod]
    public void Test_Predict_WithForecast_ReturnsExtendedLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction = model.Predict(forecastSteps: 10);

        Assert.AreEqual(ts.Count + 10, prediction.Length);
    }

    /// <summary>
    /// Tests that deterministic forecast (seed=-1) produces identical results across calls.
    /// </summary>
    [TestMethod]
    public void Test_Predict_DeterministicForecast_NoSeed()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);
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
    /// Tests that stochastic forecast with the same seed produces reproducible results.
    /// </summary>
    [TestMethod]
    public void Test_Predict_StochasticForecast_WithSeed()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction1 = model.Predict(forecastSteps: 5, seed: 12345);
        var prediction2 = model.Predict(forecastSteps: 5, seed: 12345);

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
        var model = new AutoRegressive(ts, order: 1);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction1 = model.Predict(forecastSteps: 5, seed: 12345);
        var prediction2 = model.Predict(forecastSteps: 5, seed: 54321);

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
    /// Tests that Predict throws InvalidOperationException when time series is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_Predict_NullTimeSeries_ThrowsException()
    {
        var model = new AutoRegressive();
        model.Predict();
    }

    /// <summary>
    /// Tests that long-term AR(1) forecast converges to the mean for stationary coefficients.
    /// </summary>
    [TestMethod]
    public void Test_Predict_ConvergesToMean()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        // Set stationary coefficient
        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.5;

        double mean = model.Parameters[0].Value;
        var prediction = model.Predict(forecastSteps: 100, seed: -1);

        // Long-term forecast should approach mean
        double lastForecast = prediction[prediction.Length - 1];
        Assert.AreEqual(mean, lastForecast, mean * 0.1); // Within 10% of mean
    }

    #endregion

    #region GenerateRandomSeries Tests

    /// <summary>
    /// Tests that GenerateRandomSeries produces a time series of the requested length.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomSeries_ReturnsCorrectLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

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
        var model = new AutoRegressive(ts);

        var generated1 = model.GenerateRandomSeries(50, seed: 12345);
        var generated2 = model.GenerateRandomSeries(50, seed: 12345);

        for (int i = 0; i < generated1.Count; i++)
        {
            Assert.AreEqual(generated1[i].Value, generated2[i].Value, 1e-10);
        }
    }

    #endregion

    #region Stationarity Tests

    /// <summary>
    /// Tests that IsStationary returns true for AR(1) with coefficient magnitude less than 1.
    /// </summary>
    [TestMethod]
    public void Test_IsStationary_AR1_StationaryCoefficient()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.5;

        Assert.IsTrue(model.IsStationary());
    }

    /// <summary>
    /// Tests that IsStationary returns false for AR(1) with coefficient magnitude greater than 1.
    /// </summary>
    [TestMethod]
    public void Test_IsStationary_AR1_NonStationaryCoefficient()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 1.1;

        Assert.IsFalse(model.IsStationary());
    }

    /// <summary>
    /// Tests that IsStationary returns false for AR(1) with unit root (coefficient = 1).
    /// </summary>
    [TestMethod]
    public void Test_IsStationary_AR1_UnitRoot()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 1.0;

        Assert.IsFalse(model.IsStationary());
    }

    /// <summary>
    /// Tests that IsStationary returns true for AR(2) with coefficients satisfying stationarity conditions.
    /// </summary>
    [TestMethod]
    public void Test_IsStationary_AR2_StationaryCoefficients()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 2);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.3;     // φ₁
        model.Parameters[idx + 1].Value = 0.2; // φ₂

        // φ₁ + φ₂ < 1, φ₂ - φ₁ < 1, |φ₂| < 1
        Assert.IsTrue(model.IsStationary());
    }

    /// <summary>
    /// Tests that IsStationary returns false for AR(2) with coefficients violating stationarity conditions.
    /// </summary>
    [TestMethod]
    public void Test_IsStationary_AR2_NonStationaryCoefficients()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 2);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.8;     // φ₁
        model.Parameters[idx + 1].Value = 0.5; // φ₂

        // φ₁ + φ₂ = 1.3 > 1
        Assert.IsFalse(model.IsStationary());
    }

    /// <summary>
    /// Tests that IsStationary returns true for higher-order AR when sum of absolute coefficients is less than 1.
    /// </summary>
    [TestMethod]
    public void Test_IsStationary_HigherOrder_SufficientCondition()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 3);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.2;
        model.Parameters[idx + 1].Value = 0.2;
        model.Parameters[idx + 2].Value = 0.2;

        // sum(|φᵢ|) = 0.6 < 1
        Assert.IsTrue(model.IsStationary());
    }

    #endregion

    #region Clone Tests

    /// <summary>
    /// Tests that Clone creates an independent copy with the same Order.
    /// </summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var ts = CreateSampleTimeSeries();
        var original = new AutoRegressive(ts, order: 2);

        var clone = (AutoRegressive)original.Clone();

        Assert.AreNotSame(original, clone);
        Assert.AreEqual(original.Order, clone.Order);
    }

    /// <summary>
    /// Tests that Clone creates a deep copy where parameter changes do not affect the original.
    /// </summary>
    [TestMethod]
    public void Test_Clone_ParametersAreIndependent()
    {
        var ts = CreateSampleTimeSeries();
        var original = new AutoRegressive(ts);
        double originalValue = original.Parameters[0].Value;

        var clone = (AutoRegressive)original.Clone();
        original.Parameters[0].Value = 99999;

        Assert.AreEqual(originalValue, clone.Parameters[0].Value);
    }

    /// <summary>
    /// Tests that Clone preserves the IncludeIntercept property.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesIncludeIntercept()
    {
        var ts = CreateSampleTimeSeries();
        var original = new AutoRegressive(ts, includeIntercept: false);

        var clone = (AutoRegressive)original.Clone();

        Assert.AreEqual(original.IncludeIntercept, clone.IncludeIntercept);
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Tests that ToXElement produces an XML element with the correct root name.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAutoRegressiveElement()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

        var xElement = model.ToXElement();

        Assert.AreEqual("AutoRegressive", xElement.Name.LocalName);
    }

    /// <summary>
    /// Tests that ToXElement includes the Order attribute with the correct value.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsOrderAttribute()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 3);

        var xElement = model.ToXElement();

        Assert.AreEqual("3", xElement.Attribute("Order")?.Value);
    }

    /// <summary>
    /// Tests that serialization round-trip preserves all model properties.
    /// </summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var ts = CreateSampleTimeSeries();
        var original = new AutoRegressive(ts, order: 3, includeIntercept: false);

        var xElement = original.ToXElement();
        var restored = new AutoRegressive(ts, xElement);

        Assert.AreEqual(original.Order, restored.Order);
        Assert.AreEqual(original.IncludeIntercept, restored.IncludeIntercept);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Tests that Validate returns true for a properly configured valid model.
    /// </summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that Validate returns false when the time series is null.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NullTimeSeries_ReturnsFalse()
    {
        var model = new AutoRegressive();

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
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), new DateTime(2004, 1, 1));
        for (int i = 0; i < ts.Count; i++) ts[i].Value = i;
        var model = new AutoRegressive(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("10 observations")));
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
        var model = new AutoRegressive(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("regular time interval")));
    }

    /// <summary>
    /// Tests that Validate returns false when Order is zero or negative.
    /// </summary>
    [TestMethod]
    public void Test_Validate_InvalidOrder_ReturnsFalse()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);
        model.Order = 0;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that Validate returns false when Order exceeds the maximum allowed (10).
    /// </summary>
    [TestMethod]
    public void Test_Validate_OrderTooHigh_ReturnsFalse()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);
        model.Order = 15; // Exceeds maximum of 10

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that Validate returns false when Order exceeds the time series length.
    /// </summary>
    [TestMethod]
    public void Test_Validate_OrderExceedsLength_ReturnsFalse()
    {
        var ts = CreateShortTimeSeries();
        var model = new AutoRegressive(ts);
        model.Order = ts.Count + 1;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that Validate includes a warning message when model is non-stationary.
    /// </summary>
    /// <remarks>
    /// The current AR implementation emits a "Warning: ..." message containing the word
    /// "stationarity" when a non-stationary parameter set is detected — see
    /// <c>AutoRegressive.Validate</c>. Match case-insensitively so the test stays valid
    /// across minor wording tweaks.
    /// </remarks>
    [TestMethod]
    public void Test_Validate_NonStationary_IncludesWarning()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);
        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 1.5;

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(
            messages.Any(m => m.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0
                              && m.IndexOf("stationar", StringComparison.OrdinalIgnoreCase) >= 0),
            "Expected a warning message mentioning stationarity for non-stationary parameters. " +
            $"Got: [{string.Join(" | ", messages)}]");
    }

    /// <summary>
    /// Verifies that Box-Cox lambda solver failures are captured as validation errors.
    /// </summary>
    [TestMethod]
    public void Test_BoxCoxTransform_LambdaFitFailure_ReturnsValidationError()
    {
        var model = new AutoRegressive(CreateBoxCoxLambdaFailureTimeSeries(), order: 1);

        model.TransformType = RMC.BestFit.Models.Transform.BoxCox;
        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Box-Cox lambda estimation failed")),
            $"Expected a Box-Cox lambda validation message. Got: [{string.Join(" | ", messages)}]");
    }

    /// <summary>
    /// Verifies that Yeo-Johnson lambda solver failures are captured as validation errors.
    /// </summary>
    [TestMethod]
    public void Test_YeoJohnsonTransform_LambdaFitFailure_ReturnsValidationError()
    {
        var model = new AutoRegressive(CreateYeoJohnsonLambdaFailureTimeSeries(), order: 1);

        model.TransformType = RMC.BestFit.Models.Transform.YeoJohnson;
        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Yeo-Johnson lambda estimation failed")),
            $"Expected a Yeo-Johnson lambda validation message. Got: [{string.Join(" | ", messages)}]");
    }
    #endregion

    #region SetParameterValues Tests

    /// <summary>
    /// Tests that SetParameterValues correctly updates all model parameter values.
    /// </summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);

        var newValues = new double[] { 500.0, 0.6, 40.0 };
        model.SetParameterValues(newValues);

        Assert.AreEqual(500.0, model.Parameters[0].Value);
        Assert.AreEqual(0.6, model.Parameters[1].Value);
        Assert.AreEqual(40.0, model.Parameters[2].Value);
    }

    /// <summary>
    /// Tests that SetParameterValues throws ArgumentNullException when passed null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_SetParameterValues_NullParameters_ThrowsException()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

        model.SetParameterValues(null!);
    }

    /// <summary>
    /// Tests that SetParameterValues throws ArgumentException when array length does not match.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_SetParameterValues_WrongCount_ThrowsException()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

        model.SetParameterValues(new double[] { 1.0 });
    }

    #endregion

    #region Engineering Application Tests

    /// <summary>
    /// Tests that AR(1) model validates and computes valid log-likelihood for annual streamflow data.
    /// </summary>
    [TestMethod]
    public void Test_AR_AnnualStreamflow()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);
        Assert.IsFalse(double.IsNegativeInfinity(dataLogLH));
    }

    /// <summary>
    /// Tests that AR(2) model validates correctly for modeling higher-order persistence.
    /// </summary>
    [TestMethod]
    public void Test_AR_HigherOrderPersistence()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 2);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests that AR model validates correctly on data with a linear trend.
    /// </summary>
    [TestMethod]
    public void Test_AR_TrendData()
    {
        var ts = CreateTrendTimeSeries();
        var model = new AutoRegressive(ts, order: 1);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests that AR(1) model validates on the minimum valid time series length (15 observations).
    /// </summary>
    [TestMethod]
    public void Test_AR_MinimumValidTimeSeries()
    {
        var ts = CreateShortTimeSeries();
        var model = new AutoRegressive(ts, order: 1);
        // Use full series for training to ensure validation passes with 15 observations
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests that the maximum allowed order (10) validates correctly with sufficient data.
    /// </summary>
    [TestMethod]
    public void Test_AR_MaximumOrder()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 10);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests that AR model without intercept validates correctly on zero-mean data.
    /// </summary>
    [TestMethod]
    public void Test_AR_NoInterceptWithZeroMean()
    {
        // Create zero-mean time series
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1970, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(12345);
        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = rng.NextDouble() * 100 - 50; // [-50, 50]
        }

        var model = new AutoRegressive(ts, includeIntercept: false);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
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
        var model = new AutoRegressive(ts);

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
        var model = new AutoRegressive(ts, order: 1);
        model.TransformType = RMC.BestFit.Models.Transform.Logarithmic;

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
        var original = new AutoRegressive(ts, order: 1);
        original.TransformType = RMC.BestFit.Models.Transform.BoxCox;

        var xElement = original.ToXElement();
        var restored = new AutoRegressive(ts, xElement);

        Assert.AreEqual(original.TransformType, restored.TransformType);
    }

    /// <summary>
    /// Tests that Clone preserves the TransformType property.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesTransformType()
    {
        var ts = CreateSampleTimeSeries();
        var original = new AutoRegressive(ts, order: 1);
        original.TransformType = RMC.BestFit.Models.Transform.YeoJohnson;

        var clone = (AutoRegressive)original.Clone();

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
        var model = new AutoRegressive(ts);
        model.UseDefaultTrainingSteps = false;

        model.TrainingTimeSteps = 35;

        Assert.AreEqual(35, model.TrainingTimeSteps);
    }

    /// <summary>
    /// Tests that ForecastingTimeSteps is computed correctly from NumericsTimeSeries length and TrainingTimeSteps.
    /// </summary>
    [TestMethod]
    public void Test_ForecastingTimeSteps_ComputedCorrectly()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = 40;

        // ForecastingTimeSteps = NumericsTimeSeries.Count - TrainingTimeSteps
        Assert.AreEqual(ts.Count - 40, model.ForecastingTimeSteps);
    }

    /// <summary>
    /// Tests that UseDefaultTrainingSteps can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Test_UseDefaultTrainingSteps_SetAndGet()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts);

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
        var original = new AutoRegressive(ts, order: 1);
        original.UseDefaultTrainingSteps = false;
        original.TrainingTimeSteps = 35;

        var xElement = original.ToXElement();
        var restored = new AutoRegressive(ts, xElement);

        Assert.AreEqual(original.TrainingTimeSteps, restored.TrainingTimeSteps);
    }

    /// <summary>
    /// Tests that Clone preserves training configuration.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesTrainingTimeSteps()
    {
        var ts = CreateSampleTimeSeries();
        var original = new AutoRegressive(ts, order: 1);
        original.UseDefaultTrainingSteps = false;
        original.TrainingTimeSteps = 35;

        var clone = (AutoRegressive)original.Clone();

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
        var model = new AutoRegressive(ts);

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
        var original = new AutoRegressive(ts, order: 1);
        original.UseJeffreysRuleForScale = false;

        var xElement = original.ToXElement();
        var restored = new AutoRegressive(ts, xElement);

        Assert.AreEqual(original.UseJeffreysRuleForScale, restored.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// Tests that Clone preserves the UseJeffreysRuleForScale property.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesUseJeffreysRuleForScale()
    {
        var ts = CreateSampleTimeSeries();
        var original = new AutoRegressive(ts, order: 1);
        original.UseJeffreysRuleForScale = false;

        var clone = (AutoRegressive)original.Clone();

        Assert.AreEqual(original.UseJeffreysRuleForScale, clone.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// Tests that PriorLogLikelihood returns finite values with Jeffreys rule enabled.
    /// </summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_WithJeffreysRule()
    {
        var ts = CreateSampleTimeSeries();
        var model = new AutoRegressive(ts, order: 1);
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
        var model = new AutoRegressive(ts, order: 1);
        model.UseJeffreysRuleForScale = false;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    #endregion
}
