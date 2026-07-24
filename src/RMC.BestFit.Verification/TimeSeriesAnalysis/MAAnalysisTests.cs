using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Computational verification tests for the <see cref="MAAnalysis"/> class.
/// </summary>
/// <remarks>
/// <para>
/// The MA(q) model represents the current observation as a weighted sum of past
/// error terms plus a constant mean:
/// Y(t) = μ + ε(t) + θ₁ε(t-1) + ... + θqε(t-q),
/// where ε(t) ~ N(0, σ²).
/// </para>
/// <para>
/// All tests in this class run a Bayesian MCMC chain. They are SLOW and live in the
/// Verification project. Programmatic tests (constructor, property round-trip,
/// validation, serialization) live in <c>RMC.BestFit.Verification.TimeSeriesAnalysis</c>.
/// </para>
/// </remarks>
[TestClass]
public class MAAnalysisTests
{
    #region Estimation Tests

    /// <summary>
    /// Tests Bayesian MCMC estimation of MA(1) parameters against known true values from synthetic data.
    /// Uses a 500-observation time series and validates that the posterior mode recovers the generating
    /// parameters within 25% tolerance, accounting for Monte Carlo variability.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MA(1) model: Y(t) = μ + ε(t) + θ₁ε(t-1), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = 10, θ₁ = 0.6, σ = 5.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_MA1()
    {
        var data = SyntheticTimeSeriesData.GetMA1Data(10, 0.6, 5, 500);
        var model = new MovingAverage(data.TimeSeries, order: 1, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new MAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of MA(2) parameters against known true values from synthetic data.
    /// Uses a 500-observation time series and validates that the posterior mode recovers the generating
    /// parameters within 30% tolerance, accounting for Monte Carlo variability.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MA(2) model: Y(t) = μ + ε(t) + θ₁ε(t-1) + θ₂ε(t-2), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = -10, θ₁ = 0.5, θ₂ = -0.3, σ = 2.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_MA2()
    {
        var data = SyntheticTimeSeriesData.GetMA2Data(-10, 0.5, -0.3, 2, 500);
        var model = new MovingAverage(data.TimeSeries, order: 2, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new MAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.30), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of MA(3) parameters against known true values from synthetic data.
    /// Uses a 500-observation time series and validates that the posterior mode recovers the generating
    /// parameters within 25% tolerance, accounting for Monte Carlo variability.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MA(3) model: Y(t) = μ + ε(t) + θ₁ε(t-1) + θ₂ε(t-2) + θ₃ε(t-3), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = 25, θ₁ = 0.6, θ₂ = 0.5, θ₃ = 0.7, σ = 2.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_MA3()
    {
        var data = SyntheticTimeSeriesData.GetMA3Data(25, 0.6, 0.5, 0.7, 2, 500);
        var model = new MovingAverage(data.TimeSeries, order: 3, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new MAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region R Validation Tests

    /// <summary>
    /// Tests Bayesian MCMC estimation of MA(1) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit produces comparable parameter estimates to R within 15% tolerance.
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
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_MA1_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_MA1_RTest();
        var model = new MovingAverage(data.TimeSeries, order: 1, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new MAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.15),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of MA(5) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit produces comparable parameter estimates to R within 15% tolerance.
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
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_MA5_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_MA5_RTest();
        var model = new MovingAverage(data.TimeSeries, order: 5, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new MAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.15),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    #endregion
}
