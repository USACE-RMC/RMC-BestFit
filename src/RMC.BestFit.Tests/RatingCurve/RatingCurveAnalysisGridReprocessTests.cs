using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Tests.RatingCurve;

/// <summary>
/// Phase 3a unit tests for the <c>RatingCurveAnalysis</c> grid setters.
/// Verifies that <c>MinStage</c>, <c>MaxStage</c>, and <c>StageBins</c> changes
/// reprocess the uncertainty grid without wiping the MCMC fit. These are
/// programmatic event-wiring tests — no MCMC chain is run. Chain-running parity
/// tests live in RMC.BestFit.Verification.
/// </summary>
/// <remarks>
/// <para>
/// The contract: changing the evaluation grid on an estimated analysis preserves
/// <c>BayesianAnalysis.Results</c> (the MCMC chain output) and only reprocesses
/// <c>AnalysisResults</c>. Before Phase 3a the setter called <c>ClearResults()</c>,
/// forcing the user to rerun chains just to widen the grid. After Phase 3a it
/// fires-and-forgets <c>CreateUncertaintyAnalysisResultsAsync</c> at the new grid.
/// </para>
/// </remarks>
[TestClass]
public class RatingCurveAnalysisGridReprocessTests
{
    private static readonly double[] s_stage = { 5.0, 6.5, 7.5, 9.0, 10.5, 12.0, 14.0 };
    private static readonly double[] s_discharge = { 110.0, 300.0, 600.0, 1200.0, 2000.0, 3500.0, 5100.0 };

    /// <summary>
    /// Creates fresh Analysis.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static RatingCurveAnalysis CreateFreshAnalysis()
    {
        var stage = new Numerics.Data.TimeSeries(TimeInterval.OneDay, new DateTime(2000, 1, 1), s_stage);
        var discharge = new Numerics.Data.TimeSeries(TimeInterval.OneDay, new DateTime(2000, 1, 1), s_discharge);
        var model = new RMC.BestFit.Models.RatingCurve(stage, discharge, numberOfSegments: 1);
        return new RatingCurveAnalysis(model);
    }

    /// <summary>
    /// Supports the <c>TrackClearSideEffects</c> helper.
    /// </summary>
    /// <param name="analysis">The analysis value.</param>
    /// <param name="assertNoClear">The assertNoClear value.</param>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static void TrackClearSideEffects(
        RatingCurveAnalysis analysis,
        out Action<string> assertNoClear)
    {
        bool resultsChanged = false;
        bool isEstimatedChanged = false;
        bool bayesResultsChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.AnalysisResults)) resultsChanged = true;
            if (e.PropertyName == nameof(RatingCurveAnalysis.IsEstimated)) isEstimatedChanged = true;
        };
        analysis.BayesianAnalysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Results)) bayesResultsChanged = true;
        };

        assertNoClear = (label) =>
        {
            // On a fresh analysis (IsEstimated == false), the new setter takes the
            // early-return branch in ReprocessOrClearUncertaintyGrid and does not touch
            // AnalysisResults. The old destructive setter would have called ClearResults()
            // unconditionally, firing both AnalysisResults and IsEstimated PropertyChanged.
            Assert.IsFalse(resultsChanged, $"{label}: AnalysisResults PropertyChanged must NOT fire on a fresh analysis grid edit.");
            Assert.IsFalse(isEstimatedChanged, $"{label}: IsEstimated PropertyChanged must NOT fire on a fresh analysis grid edit.");
            Assert.IsFalse(bayesResultsChanged, $"{label}: BayesianAnalysis.Results PropertyChanged must NOT fire — implies ClearResults cascaded into the MCMC.");
        };
    }

    /// <summary>Verifies that min stage change on fresh analysis does not clear mcmc.</summary>
    [TestMethod]
    public void MinStage_ChangeOnFreshAnalysis_DoesNotClearMcmc()
    {
        var analysis = CreateFreshAnalysis();
        TrackClearSideEffects(analysis, out var assertNoClear);
        bool minStageChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MinStage)) minStageChanged = true;
        };

        analysis.MinStage = 1.0;

        Assert.IsTrue(minStageChanged, "MinStage PropertyChanged must fire so the App and Validate refresh.");
        assertNoClear("MinStage");
    }

    /// <summary>Verifies that max stage change on fresh analysis does not clear mcmc.</summary>
    [TestMethod]
    public void MaxStage_ChangeOnFreshAnalysis_DoesNotClearMcmc()
    {
        var analysis = CreateFreshAnalysis();
        TrackClearSideEffects(analysis, out var assertNoClear);
        bool maxStageChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.MaxStage)) maxStageChanged = true;
        };

        analysis.MaxStage = 100.0;

        Assert.IsTrue(maxStageChanged);
        assertNoClear("MaxStage");
    }

    /// <summary>Verifies that stage bins change on fresh analysis does not clear mcmc.</summary>
    [TestMethod]
    public void StageBins_ChangeOnFreshAnalysis_DoesNotClearMcmc()
    {
        var analysis = CreateFreshAnalysis();
        TrackClearSideEffects(analysis, out var assertNoClear);
        bool stageBinsChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RatingCurveAnalysis.StageBins)) stageBinsChanged = true;
        };

        analysis.StageBins = 600;

        Assert.IsTrue(stageBinsChanged);
        assertNoClear("StageBins");
    }

    /// <summary>Verifies that stage bins is no op when set same value.</summary>
    [TestMethod]
    public void StageBins_SetSameValue_IsNoOp()
    {
        var analysis = CreateFreshAnalysis();
        var current = analysis.StageBins;
        int events = 0;
        analysis.PropertyChanged += (_, _) => events++;

        // Guarded by `if (_stageBins != value)` — no event should fire.
        analysis.StageBins = current;

        Assert.AreEqual(0, events, "Setting StageBins to its current value must not raise PropertyChanged.");
    }

    /// <summary>Verifies that clear uncertainty analysis results preserves mcmc and is estimated for .</summary>
    [TestMethod]
    public void ClearUncertaintyAnalysisResults_PreservesMcmcAndIsEstimated()
    {
        var analysis = CreateFreshAnalysis();

        // On a fresh analysis BayesianAnalysis.Results is null and IsEstimated is false;
        // the helper must still be safe to call and must not mutate either.
        analysis.ClearUncertaintyAnalysisResults();

        Assert.IsNull(analysis.AnalysisResults);
        Assert.IsFalse(analysis.IsEstimated);
        Assert.IsNull(analysis.BayesianAnalysis.Results);
    }
}
