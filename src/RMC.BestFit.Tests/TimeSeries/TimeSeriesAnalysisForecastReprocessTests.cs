using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.TimeSeries;

/// <summary>
/// Unit tests for the four time-series analyses' <c>ForecastingTimeSteps</c>
/// setters. Verifies that horizon changes reprocess the predictive output without
/// wiping the MCMC fit. Programmatic event-wiring tests — no MCMC chain is run.
/// Chain-running parity tests live in RMC.BestFit.Verification.
/// </summary>
/// <remarks>
/// <para>
/// The contract: changing the forecast horizon on an estimated analysis preserves
/// <c>BayesianAnalysis.Results</c> (the MCMC chain output) and only reprocesses
/// <c>AnalysisResults</c>. The setters do not call <c>ClearResults()</c>, which would
/// force the user to rerun chains just to extend the horizon; instead each
/// fires-and-forgets <c>CreateUncertaintyAnalysisResultsAsync</c> at the new horizon.
/// </para>
/// </remarks>
[TestClass]
public class TimeSeriesAnalysisForecastReprocessTests
{
    private static readonly double[] s_obs = { 10.0, 11.5, 13.2, 12.1, 14.8, 15.3, 14.9, 16.0, 15.7, 17.1 };

    /// <summary>
    /// Creates series.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static Numerics.Data.TimeSeries MakeSeries() =>
        new(TimeInterval.OneDay, new DateTime(2000, 1, 1), s_obs);

    /// <summary>
    /// Asserts that forecast Change Does Not Clear Mcmc.
    /// </summary>
    /// <param name="analysis">The analysis value.</param>
    /// <param name="bayes">The bayes value.</param>
    /// <param name="setForecast">The setForecast value.</param>
    /// <param name="label">The label value.</param>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static void AssertForecastChange_DoesNotClearMcmc(
        AnalysisBase analysis,
        BayesianAnalysis bayes,
        Action setForecast,
        string label)
    {
        bool resultsChanged = false;
        bool isEstimatedChanged = false;
        bool bayesResultsChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "AnalysisResults") resultsChanged = true;
            if (e.PropertyName == "IsEstimated") isEstimatedChanged = true;
        };
        bayes.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Results)) bayesResultsChanged = true;
        };

        setForecast();

        Assert.IsFalse(resultsChanged, $"{label}: AnalysisResults PropertyChanged must NOT fire on a fresh-analysis horizon edit.");
        Assert.IsFalse(isEstimatedChanged, $"{label}: IsEstimated PropertyChanged must NOT fire on a fresh-analysis horizon edit.");
        Assert.IsFalse(bayesResultsChanged, $"{label}: BayesianAnalysis.Results PropertyChanged must NOT fire — implies ClearResults cascaded into the MCMC.");
    }

    /// <summary>Verifies that AR analysis forecast horizon change on fresh analysis does not clear mcmc.</summary>
    [TestMethod]
    public void ARAnalysis_ForecastHorizon_ChangeOnFreshAnalysis_DoesNotClearMcmc()
    {
        var model = new AutoRegressive(MakeSeries(), order: 1);
        var analysis = new ARAnalysis(model);

        AssertForecastChange_DoesNotClearMcmc(
            analysis,
            analysis.BayesianAnalysis,
            () => analysis.ForecastingTimeSteps = 25,
            "ARAnalysis");

        Assert.AreEqual(25, analysis.ForecastingTimeSteps);
    }

    /// <summary>Verifies that MA analysis forecast horizon change on fresh analysis does not clear mcmc.</summary>
    [TestMethod]
    public void MAAnalysis_ForecastHorizon_ChangeOnFreshAnalysis_DoesNotClearMcmc()
    {
        var model = new MovingAverage(MakeSeries(), order: 1);
        var analysis = new MAAnalysis(model);

        AssertForecastChange_DoesNotClearMcmc(
            analysis,
            analysis.BayesianAnalysis,
            () => analysis.ForecastingTimeSteps = 25,
            "MAAnalysis");

        Assert.AreEqual(25, analysis.ForecastingTimeSteps);
    }

    /// <summary>Verifies that ARIMA analysis forecast horizon change on fresh analysis does not clear mcmc.</summary>
    [TestMethod]
    public void ARIMAAnalysis_ForecastHorizon_ChangeOnFreshAnalysis_DoesNotClearMcmc()
    {
        var model = new ARIMA(MakeSeries(), pOrder: 1, dOrder: 0, qOrder: 0);
        var analysis = new ARIMAAnalysis(model);

        AssertForecastChange_DoesNotClearMcmc(
            analysis,
            analysis.BayesianAnalysis,
            () => analysis.ForecastingTimeSteps = 25,
            "ARIMAAnalysis");

        Assert.AreEqual(25, analysis.ForecastingTimeSteps);
    }

    /// <summary>Verifies that ARIMAX analysis forecast horizon change on fresh analysis does not clear mcmc.</summary>
    [TestMethod]
    public void ARIMAXAnalysis_ForecastHorizon_ChangeOnFreshAnalysis_DoesNotClearMcmc()
    {
        var model = new ARIMAX(MakeSeries());
        var analysis = new ARIMAXAnalysis(model);

        AssertForecastChange_DoesNotClearMcmc(
            analysis,
            analysis.BayesianAnalysis,
            () => analysis.ForecastingTimeSteps = 25,
            "ARIMAXAnalysis");

        Assert.AreEqual(25, analysis.ForecastingTimeSteps);
    }

    /// <summary>Verifies that AR analysis forecast horizon clamps to valid range.</summary>
    [TestMethod]
    public void ARAnalysis_ForecastHorizon_ClampsToValidRange()
    {
        // Range [0, 100] enforced by setter. Out-of-range values are clamped.
        var model = new AutoRegressive(MakeSeries(), order: 1);
        var analysis = new ARAnalysis(model);

        analysis.ForecastingTimeSteps = -5;
        Assert.AreEqual(0, analysis.ForecastingTimeSteps);

        analysis.ForecastingTimeSteps = 1000;
        Assert.AreEqual(100, analysis.ForecastingTimeSteps);
    }

    /// <summary>Verifies that AR analysis preserves mcmc and is estimated for clear uncertainty analysis results.</summary>
    [TestMethod]
    public void ARAnalysis_ClearUncertaintyAnalysisResults_PreservesMcmcAndIsEstimated()
    {
        var model = new AutoRegressive(MakeSeries(), order: 1);
        var analysis = new ARAnalysis(model);

        // On a fresh analysis BayesianAnalysis.Results is null and IsEstimated is false;
        // the helper must still be safe to call and must not mutate either.
        analysis.ClearUncertaintyAnalysisResults();

        Assert.IsNull(analysis.AnalysisResults);
        Assert.IsFalse(analysis.IsEstimated);
        Assert.IsNull(analysis.BayesianAnalysis.Results);
    }
}
