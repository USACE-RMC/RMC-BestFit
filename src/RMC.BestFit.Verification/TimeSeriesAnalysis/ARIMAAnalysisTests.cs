using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Preserves non-discovered historical Bayesian calculations for <see cref="ARIMAAnalysis"/>.
/// </summary>
/// <remarks>
/// <para>
/// The ARIMA(p,q) model combines autoregressive and moving average components:
/// Y(t) = μ + φ₁(Y(t-1) - μ) + ... + φₚ(Y(t-p) - μ) + ε(t) + θ₁ε(t-1) + ... + θqε(t-q),
/// where ε(t) ~ N(0, σ²).
/// </para>
/// <para>
/// No method in this class is discovered as a Verification test. Current independent numerical
/// evidence is owned by <see cref="TimeSeriesIndependentOracleTests"/>,
/// <see cref="TimeSeriesIndependentRecoveryTests"/>, and <see cref="TimeSeriesChunk13OracleTests"/>.
/// Constructor, state, configuration, and serialization contracts belong to <c>RMC.BestFit.Tests</c>.
/// </para>
/// </remarks>
[TestClass]
public class ARIMAAnalysisTests
{
    #region Estimation Tests

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMA(1,1) parameters against known true values from synthetic data.
    /// The historical 1,000-observation calculation requires every generating parameter to lie
    /// in its central 90% posterior interval and every R-hat to be finite and below 1.10.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ARIMA(1,1) model: Y(t) = μ + φ₁(Y(t-1) - μ) + ε(t) + θ₁ε(t-1), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.6, θ₁ = 0.3, σ = 5.
    /// </para>
    /// </remarks>
    public async Task Test_EstimateParameters_ARIMA11()
    {
        var data = SyntheticTimeSeriesData.GetARIMA11Data(10, 0.6, 0.3, 5, 1000);
        var model = new ARIMA(data.TimeSeries, pOrder: 1, qOrder: 1, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        LegacyRecoveryAssertions.AssertCredibleIntervalRecovery(
            model.GetType().Name, model, analysis.BayesianAnalysis, data.TrueParameters);
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMA(2,1) parameters against known true values from synthetic data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.5, φ₂ = -0.3, θ₁ = 0.3, σ = 5.
    /// </para>
    /// </remarks>
    public async Task Test_EstimateParameters_ARIMA21()
    {
        var data = SyntheticTimeSeriesData.GetARIMA21Data(10, 0.5, -0.3, 0.3, 5, 1000);
        var model = new ARIMA(data.TimeSeries, pOrder: 2, qOrder: 1, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        LegacyRecoveryAssertions.AssertCredibleIntervalRecovery(
            model.GetType().Name, model, analysis.BayesianAnalysis, data.TrueParameters);
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMA(1,2) parameters against known true values from synthetic data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.5, θ₁ = 0.3, θ₂ = 0.5, σ = 5.
    /// </para>
    /// </remarks>
    public async Task Test_EstimateParameters_ARIMA12()
    {
        var data = SyntheticTimeSeriesData.GetARIMA12Data(10, 0.5, 0.3, 0.5, 5, 1000);
        var model = new ARIMA(data.TimeSeries, pOrder: 1, qOrder: 2, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        LegacyRecoveryAssertions.AssertCredibleIntervalRecovery(
            model.GetType().Name, model, analysis.BayesianAnalysis, data.TrueParameters);
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMA(2,2) parameters against known true values from synthetic data.
    /// Uses a 40% tolerance because mixed higher-order ARMA models are challenging to identify.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True parameters: μ = 10, φ₁ = 0.5, φ₂ = -0.3, θ₁ = 0.3, θ₂ = -0.2, σ = 5.
    /// </para>
    /// </remarks>
    public async Task Test_EstimateParameters_ARIMA22()
    {
        var data = SyntheticTimeSeriesData.GetARIMA22Data(10, 0.5, -0.3, 0.3, -0.2, 5, 1000);
        var model = new ARIMA(data.TimeSeries, pOrder: 2, qOrder: 2, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        LegacyRecoveryAssertions.AssertCredibleIntervalRecovery(
            model.GetType().Name, model, analysis.BayesianAnalysis, data.TrueParameters);
    }

    /// <summary>
    /// Tests that ARIMA model can fit pure AR(1) data when configured with p=1, q=0 using Bayesian estimation.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 10, φ₁ = 0.6, σ = 5.</para>
    /// </remarks>
    public async Task Test_EstimateParameters_ARIMA_FitsAR1()
    {
        var data = SyntheticTimeSeriesData.GetARIMA_AR1Data(10, 0.6, 5, 1000);
        var model = new ARIMA(data.TimeSeries, pOrder: 1, qOrder: 0, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        LegacyRecoveryAssertions.AssertCredibleIntervalRecovery(
            model.GetType().Name, model, analysis.BayesianAnalysis, data.TrueParameters);
    }

    /// <summary>
    /// Tests that ARIMA model can fit pure MA(1) data when configured with p=0, q=1 using Bayesian estimation.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 10, θ₁ = 0.5, σ = 5.</para>
    /// </remarks>
    public async Task Test_EstimateParameters_ARIMA_FitsMA1()
    {
        var data = SyntheticTimeSeriesData.GetARIMA_MA1Data(10, 0.5, 5, 1000);
        var model = new ARIMA(data.TimeSeries, pOrder: 0, qOrder: 1, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        LegacyRecoveryAssertions.AssertCredibleIntervalRecovery(
            model.GetType().Name, model, analysis.BayesianAnalysis, data.TrueParameters);
    }

    #endregion

    #region R Validation Tests

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARMA(1,1) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit produces comparable parameter estimates to R within 15% tolerance.
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
    /// </remarks>
    public async Task Test_EstimateParameters_ARMA11_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_ARMA11_RTest();
        var model = new ARIMA(data.TimeSeries, pOrder: 1, qOrder: 1, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAAnalysis(model);
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
