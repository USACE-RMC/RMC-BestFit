using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;

namespace RMC.BestFit.Verification.TimeSeriesModels;

/// <summary>
/// Verifies maximum-likelihood parameter recovery for the <see cref="MovingAverage"/> model
/// against deterministic synthetic generating parameters and committed R reference values.
/// </summary>
[TestClass]
public class MovingAverageMLERecoveryTests
{
    #region MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of MA(1) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MA(1) model: Y(t) = μ + ε(t) + θ₁ε(t-1), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = 10, θ₁ = 0.6, σ = 5.
    /// A 5% tolerance is used because MLE with large samples (10,000 obs) should achieve high precision.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_MA1()
    {
        var data = SyntheticTimeSeriesData.GetMA1Data(10, 0.6, 5, 10000);
        var model = new MovingAverage(data.TimeSeries, order: 1, includeIntercept: true)
        {
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
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.05), "Estimated parameter is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of MA(2) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MA(2) model: Y(t) = μ + ε(t) + θ₁ε(t-1) + θ₂ε(t-2), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = -10, θ₁ = 0.5, θ₂ = -0.3, σ = 2.
    /// Higher-order MA models are more complex but MLE should still achieve 5% precision with large samples.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_MA2()
    {
        var data = SyntheticTimeSeriesData.GetMA2Data(-10, 0.5, -0.3, 2, 10000);
        var model = new MovingAverage(data.TimeSeries, order: 2, includeIntercept: true)
        {
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
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.05), "Estimated parameter is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of MA(3) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MA(3) model: Y(t) = μ + ε(t) + θ₁ε(t-1) + θ₂ε(t-2) + θ₃ε(t-3), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = 25, θ₁ = 0.6, θ₂ = 0.5, θ₃ = 0.7, σ = 2.
    /// MA(3) models have more parameters but MLE with large samples should still achieve 5% precision.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_MA3()
    {
        var data = SyntheticTimeSeriesData.GetMA3Data(25, 0.6, 0.5, 0.7, 2, 10000);
        var model = new MovingAverage(data.TimeSeries, order: 3, includeIntercept: true)
        {
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
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.05), "Estimated parameter is incorrect.");
        }
    }

    #endregion

    #region R Validation Tests

    /// <summary>
    /// Tests MLE estimation of MA(1) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit produces comparable parameter estimates to R within 10% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Classic Box-Jenkins airline passenger dataset (144 monthly observations, 1949-1960).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- arima(AirPassengers, order = c(0, 0, 1))
    /// # intercept = 275.1437, ma1 = 0.9057, sigma = 67.92643
    /// </code>
    /// </para>
    /// <para>
    /// This test validates against real-world data where the true parameters are unknown,
    /// using R's well-established implementation as the reference. A 10% tolerance accounts
    /// for differences in optimization algorithms and likelihood formulations.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_MA1_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_MA1_RTest();
        var model = new MovingAverage(data.TimeSeries, order: 1, includeIntercept: true)
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

    /// <summary>
    /// Tests MLE estimation of MA(5) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit produces comparable parameter estimates to R within 10% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Classic Box-Jenkins airline passenger dataset (144 monthly observations, 1949-1960).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- arima(AirPassengers, order = c(0, 0, 5))
    /// # intercept = 178.0185, ma1 = 1.7192, ma2 = 1.9524, ma3 = 1.8639, ma4 = 1.5302, ma5 = 0.7577, sigma = 34.39477
    /// </code>
    /// </para>
    /// <para>
    /// Higher-order MA models are more challenging to estimate. This test validates that
    /// RMC-BestFit can handle MA(5) complexity on real data.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_MA5_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_MA5_RTest();
        var model = new MovingAverage(data.TimeSeries, order: 5, includeIntercept: true)
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
