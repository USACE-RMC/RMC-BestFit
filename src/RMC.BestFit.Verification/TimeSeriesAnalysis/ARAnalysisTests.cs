using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;
using Numerics.Data;
using System.Text.Json;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Computational verification tests for the <see cref="ARAnalysis"/> class.
/// </summary>
/// <remarks>
/// <para>
/// The AR(p) model uses lagged values of the series to predict the current value:
/// Y(t) = μ + φ₁(Y(t-1) - μ) + ... + φₚ(Y(t-p) - μ) + ε(t),
/// where ε(t) ~ N(0, σ²).
/// </para>
/// <para>
/// All tests in this class run a Bayesian MCMC chain. They are SLOW and live in the
/// Verification project. Programmatic tests (constructor, property round-trip,
/// validation, serialization) live in <c>RMC.BestFit.Verification.TimeSeriesAnalysis</c>.
/// </para>
/// </remarks>
[TestClass]
public class ARAnalysisTests
{
    #region Estimation Tests

    /// <summary>
    /// Tests Bayesian AR(1) recovery, diagnostics, and prediction against the independent 1,000-observation fixture.
    /// </summary>
    [TestMethod]
    public async Task Test_EstimateParameters_AR1()
    {
        JsonElement fixture = Phase5TimeSeriesRecoveryTests.LoadFixture("ar");
        double[] truth = Phase5TimeSeriesRecoveryTests.GetArTruth(fixture);
        var model = new AutoRegressive(
            Phase5TimeSeriesRecoveryTests.CreateSeries(fixture, "raw", TimeInterval.OneMonth),
            order: 1,
            includeIntercept: true)
        {
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = 1000;

        var analysis = new ARAnalysis(model);
        Phase5TimeSeriesRecoveryTests.AssertResolvedBayesianDefaults(
            "AR",
            analysis.BayesianAnalysis,
            model.NumberOfParameters);
        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation failed.");
        Phase5TimeSeriesRecoveryTests.AssertBayesianRecovery(
            "AR",
            model,
            analysis.BayesianAnalysis,
            truth);
        Phase5TimeSeriesRecoveryTests.AssertArPrediction(model, truth, fixture);
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of AR(2) parameters against known true values from synthetic data.
    /// </summary>
    [TestMethod]
    public async Task Test_EstimateParameters_AR2()
    {
        var data = SyntheticTimeSeriesData.GetAR2Data(-10, 0.75, -0.5, 2, 500);
        var model = new AutoRegressive(data.TimeSeries, order: 2, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), "Estimated parameter is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of AR(3) parameters against known true values from synthetic data.
    /// </summary>
    [TestMethod]
    public async Task Test_EstimateParameters_AR3()
    {
        var data = SyntheticTimeSeriesData.GetAR3Data(25, 0.75, -0.5, 0.3, 2, 500);
        var model = new AutoRegressive(data.TimeSeries, order: 3, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), "Estimated parameter is incorrect.");
        }
    }

    #endregion

    #region R Validation Tests

    /// <summary>
    /// Tests Bayesian MCMC estimation of AR(1) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit produces comparable parameter estimates to R within 15% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Classic Box-Jenkins airline passenger dataset (144 monthly observations, 1949-1960).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- arima(AirPassengers, order = c(1, 0, 0))
    /// # intercept = 280.2987, ar1 = 0.9646, sigma = 36.02121
    /// </code>
    /// </para>
    /// <para>
    /// A 15% tolerance accounts for differences in optimization algorithms, likelihood formulations,
    /// and the inherent Monte Carlo variability in Bayesian MCMC estimation.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_AR1_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_AR1_RTest();
        var model = new AutoRegressive(data.TimeSeries, order: 1, includeIntercept: true)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARAnalysis(model);
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
