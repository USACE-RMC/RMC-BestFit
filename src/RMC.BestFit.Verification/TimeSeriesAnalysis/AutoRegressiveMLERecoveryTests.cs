using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using System.Collections.Generic;
using System.Diagnostics;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Verifies maximum-likelihood parameter recovery for the <see cref="AutoRegressive"/> model
/// against deterministic synthetic generating parameters and committed R reference values.
/// </summary>
[TestClass]
public class AutoRegressiveMLERecoveryTests
{
    #region Estimation Tests

    /// <summary>
    /// Tests MLE estimation of AR(1) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The AR(1) model: Y(t) = μ + φ₁(Y(t-1) - μ) + ε(t), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.6, σ = 5.
    /// A 5% tolerance is used because MLE with large samples (10,000 obs) should achieve high precision.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_AR1()
    {
        var data = SyntheticTimeSeriesData.GetAR1Data(10, 0.6, 5, 10000);
        var model = new AutoRegressive(data.TimeSeries, order: 1, includeIntercept: true);
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
    /// Tests MLE estimation of AR(2) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The AR(2) model: Y(t) = μ + φ₁(Y(t-1) - μ) + φ₂(Y(t-2) - μ) + ε(t), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = -10, φ₁ = 0.75, φ₂ = -0.5, σ = 2.
    /// Higher-order AR models are more complex but MLE should still achieve 5% precision with large samples.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_AR2()
    {
        var data = SyntheticTimeSeriesData.GetAR2Data(-10, 0.75, -0.5, 2, 10000);
        var model = new AutoRegressive(data.TimeSeries, order: 2, includeIntercept: true);
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
    /// Tests MLE estimation of AR(3) parameters against known true values from synthetic data.
    /// Uses a 10,000-observation time series and validates that the optimizer recovers the generating
    /// parameters within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The AR(3) model: Y(t) = μ + φ₁(Y(t-1) - μ) + φ₂(Y(t-2) - μ) + φ₃(Y(t-3) - μ) + ε(t).
    /// </para>
    /// <para>
    /// True parameters: μ = 25, φ₁ = 0.75, φ₂ = -0.5, φ₃ = 0.3, σ = 2.
    /// AR(3) models have more parameters but MLE with large samples should still achieve 5% precision.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_AR3()
    {
        var data = SyntheticTimeSeriesData.GetAR3Data(25, 0.75, -0.5, 0.3, 2, 10000);
        var model = new AutoRegressive(data.TimeSeries, order: 3, includeIntercept: true);
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
    /// Tests MLE estimation of AR(1) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit produces comparable parameter estimates to R within 10% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Classic Box-Jenkins airline passenger dataset (144 monthly observations, 1949-1960).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- arima(AirPassengers, order = c(1, 0, 0))
    /// # intercept = 332.7286, ar1 = 0.9588, sigma = 33.27161
    /// </code>
    /// </para>
    /// <para>
    /// This test validates against real-world data where the true parameters are unknown,
    /// using R's well-established implementation as the reference. A 10% tolerance accounts
    /// for differences in optimization algorithms and likelihood formulations.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_AR1_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_AR1_RTest();
        var model = new AutoRegressive(data.TimeSeries, order: 1, includeIntercept: true)
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
    /// Tests MLE estimation of AR(5) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit produces comparable parameter estimates to R within 10% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Classic Box-Jenkins airline passenger dataset (144 monthly observations, 1949-1960).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- arima(AirPassengers, order = c(5, 0, 0))
    /// # intercept = 421.1273, ar1 = 1.2988, ar2 = -0.5323, ar3 = 0.1441, ar4 = -0.1929, ar5 = 0.2588, sigma = 30.01
    /// </code>
    /// </para>
    /// <para>
    /// Higher-order AR models are more challenging to estimate. This test validates that
    /// RMC-BestFit can handle AR(5) complexity on real data.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_AR5_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_AR5_RTest();
        var model = new AutoRegressive(data.TimeSeries, order: 5, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
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
