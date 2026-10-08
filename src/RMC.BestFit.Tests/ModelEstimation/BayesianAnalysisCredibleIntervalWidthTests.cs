using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Unit tests for the <c>BayesianAnalysis.CredibleIntervalWidth</c>
/// setter. Verifies that changing the CI width on an analysis does not wipe the
/// MCMC fit and produces only the expected PropertyChanged signals. These are
/// programmatic event-wiring tests — no MCMC chain is run. Chain-running parity
/// tests live in RMC.BestFit.Verification.
/// </summary>
/// <remarks>
/// <para>
/// The contract: alpha = 1 - CIWidth only affects the LowerCI/UpperCI percentiles
/// stored on each <c>ParameterResults[i].SummaryStatistics</c>. The chain itself
/// (MarkovChains, AcceptanceRates, MeanLogLikelihood, Output) is invariant under
/// alpha. The setter does not call <c>ClearResults()</c>, which would force the
/// user to rerun chains just to widen CI bands; instead it preserves
/// <c>Results</c> and reprocesses ParameterResults summary statistics in place
/// via <c>MCMCResults.RecomputeParameterResults</c>.
/// </para>
/// </remarks>
[TestClass]
public class BayesianAnalysisCredibleIntervalWidthTests
{
    /// <summary>
    /// Creates exact Data Frame.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame CreateExactDataFrame()
    {
        var values = new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };
        var df = new BestFitDataFrame();
        for (int i = 0; i < values.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        return df;
    }

    /// <summary>
    /// Creates fresh Bayesian.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BayesianAnalysis CreateFreshBayesian()
    {
        var df = CreateExactDataFrame();
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        return new BayesianAnalysis(model);
    }

    /// <summary>Verifies that credible interval width does not throw for set on fresh analysis.</summary>
    [TestMethod]
    public void CredibleIntervalWidth_Set_OnFreshAnalysis_DoesNotThrow()
    {
        var bayes = CreateFreshBayesian();

        // Setter must tolerate a not-estimated analysis (Results == null, IsEstimated == false).
        bayes.CredibleIntervalWidth = 0.95;

        Assert.AreEqual(0.95, bayes.CredibleIntervalWidth);
        Assert.IsFalse(bayes.IsEstimated);
        Assert.IsNull(bayes.Results);
    }

    /// <summary>Verifies that credible interval width set fires credible interval width property changed.</summary>
    [TestMethod]
    public void CredibleIntervalWidth_Set_FiresCredibleIntervalWidthPropertyChanged()
    {
        var bayes = CreateFreshBayesian();
        bool ciWidthChanged = false;
        bayes.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.CredibleIntervalWidth)) ciWidthChanged = true;
        };

        bayes.CredibleIntervalWidth = 0.95;

        Assert.IsTrue(ciWidthChanged, "CredibleIntervalWidth PropertyChanged must fire so column-header relabeling and reprocess wiring run.");
    }

    /// <summary>Verifies that credible interval width set on fresh analysis does not fire results property changed.</summary>
    [TestMethod]
    public void CredibleIntervalWidth_Set_OnFreshAnalysis_DoesNotFireResultsPropertyChanged()
    {
        var bayes = CreateFreshBayesian();
        bool resultsChanged = false;
        bool isEstimatedChanged = false;
        bayes.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Results)) resultsChanged = true;
            if (e.PropertyName == nameof(BayesianAnalysis.IsEstimated)) isEstimatedChanged = true;
        };

        bayes.CredibleIntervalWidth = 0.95;

        // On a fresh analysis (no MCMC results), setter takes the IsEstimated==false branch and
        // does not call RecomputeParameterResults. Results remains null and no Results event fires.
        Assert.IsFalse(resultsChanged, "Results PropertyChanged must NOT fire on a fresh analysis — implies the old destructive ClearResults path was taken.");
        Assert.IsFalse(isEstimatedChanged, "IsEstimated PropertyChanged must NOT fire on a fresh analysis.");
    }

    /// <summary>Verifies that credible interval width is no op when set same value.</summary>
    [TestMethod]
    public void CredibleIntervalWidth_Set_SameValue_IsNoOp()
    {
        var bayes = CreateFreshBayesian();
        var original = bayes.CredibleIntervalWidth;
        int events = 0;
        bayes.PropertyChanged += (_, _) => events++;

        // Set to the current value — guarded by `if (_credibleIntervalWidth != value)`.
        bayes.CredibleIntervalWidth = original;

        Assert.AreEqual(0, events, "Setting CredibleIntervalWidth to its current value must not raise PropertyChanged.");
    }

    /// <summary>Verifies that credible interval width set different value does not reset is estimated on fresh analysis.</summary>
    [TestMethod]
    public void CredibleIntervalWidth_Set_DifferentValue_DoesNotResetIsEstimatedOnFreshAnalysis()
    {
        var bayes = CreateFreshBayesian();
        Assert.IsFalse(bayes.IsEstimated);

        bayes.CredibleIntervalWidth = 0.95;

        // The new setter no longer routes through ClearResults, so it cannot accidentally
        // toggle IsEstimated. (Previously ClearResults() set IsEstimated = false unconditionally,
        // which was harmless on a fresh analysis but invalidated estimated analyses.)
        Assert.IsFalse(bayes.IsEstimated);
    }

    /// <summary>Verifies that analysis bayesian analysis property changed dispatches credible interval width.</summary>
    [TestMethod]
    public void Analysis_BayesianAnalysisPropertyChanged_DispatchesCredibleIntervalWidth()
    {
        // Verifies the parent UnivariateAnalysis BayesianAnalysis_PropertyChanged handler has
        // a CredibleIntervalWidth branch — the no-op default fall-through would silently
        // drop the change without scheduling a reprocess. We can't observe the fire-and-forget
        // task on a fresh analysis (IsEstimated guard skips it), but the setter MUST still
        // raise PropertyChanged so the App relabels column headers.
        var df = CreateExactDataFrame();
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);

        bool propagated = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.CredibleIntervalWidth)) propagated = true;
        };

        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95;

        Assert.IsTrue(propagated, "UnivariateAnalysis must propagate CredibleIntervalWidth PropertyChanged to its own listeners.");
    }
}
