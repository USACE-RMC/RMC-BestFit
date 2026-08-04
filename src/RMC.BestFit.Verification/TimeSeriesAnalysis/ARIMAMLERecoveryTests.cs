using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Verifies maximum-likelihood parameter recovery for the <see cref="ARIMA"/> model
/// against deterministic synthetic generating parameters and committed R reference values.
/// </summary>
[TestClass]
public class ARIMAMLERecoveryTests
{
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
