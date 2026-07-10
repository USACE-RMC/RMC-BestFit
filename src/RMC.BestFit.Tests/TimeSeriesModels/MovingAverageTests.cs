using Numerics.Data;
using RMC.BestFit.Models;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Programmatic unit tests for the <c>MovingAverage</c> time-series model.
/// </summary>
/// <remarks>
/// <para>
/// MA(q) models represent the current observation as a weighted sum of past
/// error terms plus a constant mean:
/// Y(t) = μ + ε(t) + θ₁ε(t-1) + ... + θqε(t-q), where ε(t) ~ N(0, σ²).
/// </para>
/// <para>
/// These tests cover constructors, property round-trips, parameter layout,
/// log-likelihood evaluation at fixed parameters, prediction, generation,
/// invertibility, cloning, serialization, and validation. Estimation-driven
/// tests (MLE / R parity) live in <c>RMC.BestFit.Verification</c>.
/// </para>
/// </remarks>
[TestClass]
public class MovingAverageTests
{
    #region Inline test fixtures

    private const int FixtureSize = 50;
    private const int LongFixtureSize = 200;
    private const int MonthlyFixtureSize = 240;

    /// <summary>
    /// Deterministic MA(1)-like fixture (50 annual observations) generated with a
    /// fixed seed so this file does not depend on the Verification project.
    /// </summary>
    private static NumericsTimeSeries CreateSampleTimeSeries()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1970, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(12345);

        const double mean = 500.0;
        const double theta = 0.5;
        const double sigma = 50.0;

        var epsilon = new double[ts.Count + 1];
        for (int i = 0; i <= ts.Count; i++)
        {
            epsilon[i] = (rng.NextDouble() * 2.0 - 1.0) * sigma;
        }

        for (int i = 0; i < ts.Count; i++)
        {
            double value = mean + epsilon[i + 1];
            if (i > 0)
            {
                value += theta * epsilon[i];
            }
            ts[i].Value = value;
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
    /// Deterministic monthly fixture (240 monthly observations) with seasonal pattern + trend + noise.
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

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the empty constructor creates a default MA(1) model with expected initial values.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new MovingAverage();

        Assert.IsNotNull(model);
        Assert.AreEqual(1, model.Order);
        Assert.IsTrue(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that the constructor with time series properly assigns the data to the model.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithTimeSeries_SetsData()
    {
        var ts = CreateSampleTimeSeries();

        var model = new MovingAverage(ts);

        Assert.AreSame(ts, model.TimeSeries);
        Assert.AreEqual(1, model.Order);
    }

    /// <summary>
    /// Tests that specifying the order parameter correctly sets the MA order.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithOrder_SetsMAOrder()
    {
        var ts = CreateSampleTimeSeries();

        var model = new MovingAverage(ts, order: 3);

        Assert.AreEqual(3, model.Order);
    }

    /// <summary>
    /// Tests that setting includeIntercept to false excludes the intercept parameter.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithNoIntercept_SetsIncludeIntercept()
    {
        var ts = CreateSampleTimeSeries();

        var model = new MovingAverage(ts, includeIntercept: false);

        Assert.IsFalse(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that a model can be restored from an XElement with all properties preserved.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var ts = CreateSampleTimeSeries();
        var original = new MovingAverage(ts, order: 2);
        original.Parameters[0].Value = 555.0;
        var xElement = original.ToXElement();

        var restored = new MovingAverage(ts, xElement);

        Assert.AreEqual(2, restored.Order);
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
        var model = new MovingAverage();
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
        var model = new MovingAverage(ts);

        model.Order = 4;

        Assert.AreEqual(4, model.Order);
    }

    /// <summary>
    /// Tests that changing the Order property updates the parameter count accordingly.
    /// </summary>
    [TestMethod]
    public void Test_Order_Change_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);
        int initialParams = model.NumberOfParameters;

        model.Order = 3;

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
        var model = new MovingAverage(ts);

        model.IncludeIntercept = false;

        Assert.IsFalse(model.IncludeIntercept);
    }

    /// <summary>
    /// Tests that changing IncludeIntercept updates the parameter count by one.
    /// </summary>
    [TestMethod]
    public void Test_IncludeIntercept_Change_UpdatesParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, includeIntercept: true);
        int initialParams = model.NumberOfParameters;

        model.IncludeIntercept = false;

        Assert.AreEqual(initialParams - 1, model.NumberOfParameters);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>
    /// Tests that MA(1) without intercept has exactly 2 parameters (theta and sigma).
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_MA1_TwoParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1, includeIntercept: false);

        // MA(1) without intercept: θ₁ + σ = 2 parameters
        Assert.AreEqual(2, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that MA(1) with intercept has exactly 3 parameters (mu, theta, and sigma).
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_MA1_WithIntercept_ThreeParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1, includeIntercept: true);

        // MA(1) with intercept: μ + θ₁ + σ = 3 parameters
        Assert.AreEqual(3, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that MA(3) with intercept has exactly 5 parameters (mu, three thetas, and sigma).
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_MA3_FiveParameters()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 3, includeIntercept: true);

        // MA(3) with intercept: μ + θ₁ + θ₂ + θ₃ + σ = 5 parameters
        Assert.AreEqual(5, model.NumberOfParameters);
    }

    /// <summary>
    /// Tests that parameters have appropriate names (Intercept, MA, Scale).
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_HasCorrectNames()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 2, includeIntercept: true);

        Assert.IsTrue(model.Parameters[0].Name.Contains("Intercept"));
        Assert.IsTrue(model.Parameters[1].Name.Contains("MA"));
        Assert.IsTrue(model.Parameters[2].Name.Contains("MA"));
        Assert.IsTrue(model.Parameters[3].Name.Contains("Scale"));
    }

    /// <summary>
    /// Tests that MA coefficient parameters have bounds of [-2, 2] for invertibility.
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultParameters_MABounds()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);

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
        var model = new MovingAverage(ts);

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
        var model = new MovingAverage(ts);
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
    /// Tests that the data log-likelihood returns a finite value for valid parameters.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(dataLogLH));
    }

    /// <summary>
    /// Tests that the data log-likelihood returns a finite value for MA(2) models.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_MA2_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 2);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
    }

    /// <summary>
    /// Tests that the data log-likelihood returns NegativeInfinity when time series is null.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_NullTimeSeries_ReturnsNegativeInfinity()
    {
        var model = new MovingAverage();

        double result = model.DataLogLikelihood(new double[] { 0, 0, 1 });

        Assert.IsTrue(double.IsNegativeInfinity(result));
    }

    /// <summary>
    /// Tests that the data log-likelihood returns NegativeInfinity when parameters contain NaN.
    /// </summary>
    [TestMethod]
    public void Test_DataLogLikelihood_NaNParameter_ReturnsNegativeInfinity()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts);

        double result = model.DataLogLikelihood(new double[] { double.NaN, 0, 1 });

        Assert.IsTrue(double.IsNegativeInfinity(result));
    }

    /// <summary>
    /// Tests that the prior log-likelihood returns a finite value for valid parameters.
    /// </summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_ReturnsFiniteValue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    /// <summary>
    /// Tests that pointwise log-likelihood returns an array with one element per training observation.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_ReturnsCorrectCount()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 2);
        // Use full series for training to get predictable count
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        // MA model uses all training observations for likelihood
        Assert.AreEqual(ts.Count, pointwise.Length);
    }

    /// <summary>
    /// Tests that the sum of pointwise log-likelihoods equals the total data log-likelihood.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_SumEqualsDataLogLikelihood()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);
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
    /// Tests that pointwise log-likelihood components all have the Exact data type.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_ReturnsCorrectTypes()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        Assert.IsTrue(components.All(c => c.Type == DataComponentType.Exact));
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
        var model = new MovingAverage(ts);
        // Use full series for training so prediction matches observed length
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction = model.Predict(forecastSteps: 0);

        Assert.AreEqual(ts.Count, prediction.Length);
    }

    /// <summary>
    /// Tests that Predict with forecast steps returns extended length (TrainingTimeSteps + forecast).
    /// </summary>
    [TestMethod]
    public void Test_Predict_WithForecast_ReturnsExtendedLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var prediction = model.Predict(forecastSteps: 10);

        Assert.AreEqual(ts.Count + 10, prediction.Length);
    }

    /// <summary>
    /// Tests that deterministic forecasts (seed=-1) produce identical results on repeated calls.
    /// </summary>
    [TestMethod]
    public void Test_Predict_DeterministicForecast_NoSeed()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);
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
        var model = new MovingAverage(ts, order: 1);
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
        var model = new MovingAverage(ts, order: 1);
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
        var model = new MovingAverage();
        model.Predict();
    }

    /// <summary>
    /// Tests that MA forecasts converge to the mean after q steps (MA memory exhausted).
    /// </summary>
    [TestMethod]
    public void Test_Predict_ConvergesToMean()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 2);
        // Use full series for training
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        // Set intercept to known value
        model.Parameters[0].Value = 500.0;
        model.Parameters[1].Value = 0.3;  // theta_1
        model.Parameters[2].Value = 0.2;  // theta_2
        model.Parameters[3].Value = 50.0; // sigma

        var prediction = model.Predict(forecastSteps: 10, seed: -1);

        // After q steps of forecasting, MA effect decays to zero
        double expectedMean = 500.0;
        for (int i = model.TrainingTimeSteps + 2; i < prediction.Length; i++)
        {
            Assert.AreEqual(expectedMean, prediction[i], 0.1);
        }
    }

    #endregion

    #region GenerateRandomSeries Tests

    /// <summary>
    /// Tests that GenerateRandomSeries returns a time series of the specified length.
    /// </summary>
    [TestMethod]
    public void Test_GenerateRandomSeries_ReturnsCorrectLength()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts);

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
        var model = new MovingAverage(ts);

        var generated1 = model.GenerateRandomSeries(50, seed: 12345);
        var generated2 = model.GenerateRandomSeries(50, seed: 12345);

        for (int i = 0; i < generated1.Count; i++)
        {
            Assert.AreEqual(generated1[i].Value, generated2[i].Value, 1e-10);
        }
    }

    #endregion

    #region Invertibility Tests

    /// <summary>
    /// Tests that MA(1) with coefficient |theta| less than 1 is invertible.
    /// </summary>
    [TestMethod]
    public void Test_IsInvertible_MA1_InvertibleCoefficient()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.5;

        Assert.IsTrue(model.IsInvertible());
    }

    /// <summary>
    /// Tests that MA(1) with coefficient |theta| greater than 1 is not invertible.
    /// </summary>
    [TestMethod]
    public void Test_IsInvertible_MA1_NonInvertibleCoefficient()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 1.1;

        Assert.IsFalse(model.IsInvertible());
    }

    /// <summary>
    /// Tests that MA(1) with unit root (theta = 1) is not invertible.
    /// </summary>
    [TestMethod]
    public void Test_IsInvertible_MA1_UnitRoot()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 1.0;

        Assert.IsFalse(model.IsInvertible());
    }

    /// <summary>
    /// Tests that MA(2) with small coefficients satisfies the invertibility conditions.
    /// </summary>
    [TestMethod]
    public void Test_IsInvertible_MA2_InvertibleCoefficients()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 2);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.3;     // θ₁
        model.Parameters[idx + 1].Value = 0.2; // θ₂

        Assert.IsTrue(model.IsInvertible());
    }

    /// <summary>
    /// Tests that MA(2) violating invertibility conditions (sum of thetas greater than 1) is detected.
    /// </summary>
    [TestMethod]
    public void Test_IsInvertible_MA2_NonInvertibleCoefficients()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 2);

        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 0.8;     // θ₁
        model.Parameters[idx + 1].Value = 0.5; // θ₂

        // θ₁ + θ₂ = 1.3 > 1
        Assert.IsFalse(model.IsInvertible());
    }

    #endregion

    #region Clone Tests

    /// <summary>
    /// Tests that Clone creates an independent copy with the same order.
    /// </summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var ts = CreateSampleTimeSeries();
        var original = new MovingAverage(ts, order: 2);

        var clone = (MovingAverage)original.Clone();

        Assert.AreNotSame(original, clone);
        Assert.AreEqual(original.Order, clone.Order);
    }

    /// <summary>
    /// Tests that modifying original parameters does not affect cloned parameters.
    /// </summary>
    [TestMethod]
    public void Test_Clone_ParametersAreIndependent()
    {
        var ts = CreateSampleTimeSeries();
        var original = new MovingAverage(ts);
        double originalValue = original.Parameters[0].Value;

        var clone = (MovingAverage)original.Clone();
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
        var original = new MovingAverage(ts, includeIntercept: false);

        var clone = (MovingAverage)original.Clone();

        Assert.AreEqual(original.IncludeIntercept, clone.IncludeIntercept);
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Tests that ToXElement creates an element with the correct root name.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsMovingAverageElement()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts);

        var xElement = model.ToXElement();

        Assert.AreEqual("MovingAverage", xElement.Name.LocalName);
    }

    /// <summary>
    /// Tests that ToXElement includes the Order attribute with the correct value.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsOrderAttribute()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 3);

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
        var original = new MovingAverage(ts, order: 3, includeIntercept: false);

        var xElement = original.ToXElement();
        var restored = new MovingAverage(ts, xElement);

        Assert.AreEqual(original.Order, restored.Order);
        Assert.AreEqual(original.IncludeIntercept, restored.IncludeIntercept);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Tests that a properly configured model passes validation.
    /// </summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that validation fails when time series is null.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NullTimeSeries_ReturnsFalse()
    {
        var model = new MovingAverage();

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Time series")));
    }

    /// <summary>
    /// Tests that validation fails when time series has fewer than 10 observations.
    /// </summary>
    [TestMethod]
    public void Test_Validate_TooShortTimeSeries_ReturnsFalse()
    {
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), new DateTime(2004, 1, 1));
        for (int i = 0; i < ts.Count; i++) ts[i].Value = i;
        var model = new MovingAverage(ts);

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
        var model = new MovingAverage(ts);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("regular time interval")));
    }

    /// <summary>
    /// Tests that validation fails when order is set to zero.
    /// </summary>
    [TestMethod]
    public void Test_Validate_InvalidOrder_ReturnsFalse()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts);
        model.Order = 0;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that validation fails when order exceeds the maximum allowed value of 10.
    /// </summary>
    [TestMethod]
    public void Test_Validate_OrderTooHigh_ReturnsFalse()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts);
        model.Order = 15; // Exceeds maximum of 10

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Tests that validation includes a warning when MA coefficients violate invertibility.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NonInvertible_IncludesWarning()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);
        int idx = model.IncludeIntercept ? 1 : 0;
        model.Parameters[idx].Value = 1.5;

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(messages.Any(m => m.Contains("Warning") && m.Contains("invertibility")));
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
        var model = new MovingAverage(ts, order: 1);

        var newValues = new double[] { 500.0, 0.4, 40.0 };
        model.SetParameterValues(newValues);

        Assert.AreEqual(500.0, model.Parameters[0].Value);
        Assert.AreEqual(0.4, model.Parameters[1].Value);
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
        var model = new MovingAverage(ts);

        model.SetParameterValues(null!);
    }

    /// <summary>
    /// Tests that SetParameterValues throws ArgumentException when array length is wrong.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_SetParameterValues_WrongCount_ThrowsException()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts);

        model.SetParameterValues(new double[] { 1.0 });
    }

    #endregion

    #region Engineering Application Tests

    /// <summary>
    /// Tests MA(1) model for transient measurement error analysis in hydrologic data.
    /// </summary>
    [TestMethod]
    public void Test_MA_TransientMeasurementError()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);
        Assert.IsFalse(double.IsNegativeInfinity(dataLogLH));
    }

    /// <summary>
    /// Tests MA model with monthly time series for seasonal analysis applications.
    /// </summary>
    [TestMethod]
    public void Test_MA_MonthlyAnalysis()
    {
        var ts = CreateMonthlyTimeSeries();
        var model = new MovingAverage(ts, order: 1);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests MA(5) model to verify higher-order models work correctly.
    /// </summary>
    [TestMethod]
    public void Test_MA_HigherOrderModel()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 5);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        // MA(5) should have: μ + θ₁ + θ₂ + θ₃ + θ₄ + θ₅ + σ = 7 parameters
        Assert.AreEqual(7, model.NumberOfParameters);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests MA model with the minimum valid time series length (15 observations).
    /// </summary>
    [TestMethod]
    public void Test_MA_MinimumValidTimeSeries()
    {
        var ts = CreateShortTimeSeries();
        var model = new MovingAverage(ts, order: 1);
        // Use full series for training to ensure validation passes with 15 observations
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = ts.Count;

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests MA model with the maximum allowed order of 10.
    /// </summary>
    [TestMethod]
    public void Test_MA_MaximumOrder()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 10);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests MA model without intercept on zero-mean data (common preprocessing approach).
    /// </summary>
    [TestMethod]
    public void Test_MA_NoInterceptWithZeroMean()
    {
        // Create zero-mean time series
        var ts = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1970, 1, 1), new DateTime(2019, 1, 1));
        var rng = new Random(12345);
        for (int i = 0; i < ts.Count; i++)
        {
            ts[i].Value = rng.NextDouble() * 100 - 50; // [-50, 50]
        }

        var model = new MovingAverage(ts, includeIntercept: false);

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
        var model = new MovingAverage(ts);

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
        var model = new MovingAverage(ts, order: 1);
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
        var original = new MovingAverage(ts, order: 1);
        original.TransformType = RMC.BestFit.Models.Transform.BoxCox;

        var xElement = original.ToXElement();
        var restored = new MovingAverage(ts, xElement);

        Assert.AreEqual(original.TransformType, restored.TransformType);
    }

    /// <summary>
    /// Tests that Clone preserves the TransformType property.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesTransformType()
    {
        var ts = CreateSampleTimeSeries();
        var original = new MovingAverage(ts, order: 1);
        original.TransformType = RMC.BestFit.Models.Transform.YeoJohnson;

        var clone = (MovingAverage)original.Clone();

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
        var model = new MovingAverage(ts);
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
        var model = new MovingAverage(ts);
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
        var model = new MovingAverage(ts);

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
        var original = new MovingAverage(ts, order: 1);
        original.UseDefaultTrainingSteps = false;
        original.TrainingTimeSteps = 35;

        var xElement = original.ToXElement();
        var restored = new MovingAverage(ts, xElement);

        Assert.AreEqual(original.TrainingTimeSteps, restored.TrainingTimeSteps);
    }

    /// <summary>
    /// Tests that Clone preserves training configuration.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesTrainingTimeSteps()
    {
        var ts = CreateSampleTimeSeries();
        var original = new MovingAverage(ts, order: 1);
        original.UseDefaultTrainingSteps = false;
        original.TrainingTimeSteps = 35;

        var clone = (MovingAverage)original.Clone();

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
        var model = new MovingAverage(ts);

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
        var original = new MovingAverage(ts, order: 1);
        original.UseJeffreysRuleForScale = false;

        var xElement = original.ToXElement();
        var restored = new MovingAverage(ts, xElement);

        Assert.AreEqual(original.UseJeffreysRuleForScale, restored.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// Tests that Clone preserves the UseJeffreysRuleForScale property.
    /// </summary>
    [TestMethod]
    public void Test_Clone_PreservesUseJeffreysRuleForScale()
    {
        var ts = CreateSampleTimeSeries();
        var original = new MovingAverage(ts, order: 1);
        original.UseJeffreysRuleForScale = false;

        var clone = (MovingAverage)original.Clone();

        Assert.AreEqual(original.UseJeffreysRuleForScale, clone.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// Tests that PriorLogLikelihood returns finite values with Jeffreys rule enabled.
    /// </summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_WithJeffreysRule()
    {
        var ts = CreateSampleTimeSeries();
        var model = new MovingAverage(ts, order: 1);
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
        var model = new MovingAverage(ts, order: 1);
        model.UseJeffreysRuleForScale = false;

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    #endregion
}
