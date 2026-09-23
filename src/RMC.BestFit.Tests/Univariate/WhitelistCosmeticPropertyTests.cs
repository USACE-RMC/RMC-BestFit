using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Whitelist gate tests. Verifies that mutating a *cosmetic*
/// (post-fit / non-likelihood-affecting) inner-model property does not trigger
/// <c>UnivariateAnalysis.ClearResults</c> side-effects on the analysis.
/// </summary>
/// <remarks>
/// <para>
/// These tests directly gate the destructive-property whitelist policy. A regression
/// that adds a cosmetic property (or removes a destructive one from the list) would
/// silently change clear behavior — these tests catch the silent change.
/// </para>
/// <para>
/// Detection strategy: <c>UnivariateAnalysis.ClearResults</c> always raises
/// <c>AnalysisResults</c> PropertyChanged (even when AnalysisResults is already null).
/// So if the whitelist branch is hit by a cosmetic change, the test sees the spurious
/// AnalysisResults event. Conversely, if the cosmetic property is correctly excluded,
/// the test sees zero AnalysisResults events.
/// </para>
/// </remarks>
[TestClass]
public class WhitelistCosmeticPropertyTests
{
    /// <summary>
    /// Creates fresh Analysis.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static UnivariateAnalysis CreateFreshAnalysis()
    {
        var df = new BestFitDataFrame();
        var values = new[] { 12500.0, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };
        for (int i = 0; i < values.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        return new UnivariateAnalysis(dist);
    }

    /// <summary>Verifies that univariate analysis alpha change does not trigger clear results.</summary>
    [TestMethod]
    public void UnivariateAnalysis_AlphaChange_DoesNotTriggerClearResults()
    {
        // Alpha is cosmetic (controls nonstationary chronology eval) — must NOT clear results.
        var analysis = CreateFreshAnalysis();
        bool analysisResultsFired = false;
        bool isEstimatedFired = false;
        bool bayesResultsFired = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UnivariateAnalysis.AnalysisResults)) analysisResultsFired = true;
            if (e.PropertyName == nameof(UnivariateAnalysis.IsEstimated)) isEstimatedFired = true;
        };
        analysis.BayesianAnalysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Results)) bayesResultsFired = true;
        };

        // Mutate a cosmetic model property — fires Model_PropertyChanged on the analysis.
        analysis.UnivariateDistribution.Alpha = 0.05;

        Assert.IsFalse(analysisResultsFired,
            "Alpha is cosmetic — AnalysisResults PropertyChanged must NOT fire (would imply ClearResults was called).");
        Assert.IsFalse(isEstimatedFired,
            "Alpha is cosmetic — IsEstimated PropertyChanged must NOT fire.");
        Assert.IsFalse(bayesResultsFired,
            "Alpha is cosmetic — BayesianAnalysis.Results PropertyChanged must NOT fire.");
    }

    /// <summary>Verifies that univariate analysis parameter time index change does not trigger clear results.</summary>
    [TestMethod]
    public void UnivariateAnalysis_ParameterTimeIndexChange_DoesNotTriggerClearResults()
    {
        // ParameterTimeIndex is cosmetic (controls nonstationary post-fit display) — must NOT clear.
        var analysis = CreateFreshAnalysis();
        bool analysisResultsFired = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UnivariateAnalysis.AnalysisResults)) analysisResultsFired = true;
        };

        analysis.UnivariateDistribution.ParameterTimeIndex = 1;

        Assert.IsFalse(analysisResultsFired,
            "ParameterTimeIndex is cosmetic — AnalysisResults PropertyChanged must NOT fire.");
    }

    /// <summary>Verifies that univariate analysis alpha change propagates alpha property changed.</summary>
    [TestMethod]
    public void UnivariateAnalysis_AlphaChange_PropagatesAlphaPropertyChanged()
    {
        // Cosmetic properties must still propagate PropertyChange so UI bindings refresh.
        var analysis = CreateFreshAnalysis();
        bool alphaFired = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UnivariateDistribution.Alpha)) alphaFired = true;
        };

        analysis.UnivariateDistribution.Alpha = 0.05;

        Assert.IsTrue(alphaFired, "Alpha PropertyChanged must propagate up through Model_PropertyChanged for UI binding.");
    }
}
