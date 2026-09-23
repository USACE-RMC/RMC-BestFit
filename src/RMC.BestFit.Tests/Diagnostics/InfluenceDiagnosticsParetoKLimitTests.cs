using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Verifies how <see cref="InfluenceDiagnostics"/> classifies observations whose Pareto k could not
/// be estimated and how the default problematic-observation limit is chosen.
/// </summary>
[TestClass]
public class InfluenceDiagnosticsParetoKLimitTests
{
    /// <summary>
    /// Creates an exact observation with the given Pareto k.
    /// </summary>
    /// <param name="index">Observation index.</param>
    /// <param name="k">Pareto k value.</param>
    /// <returns>The observation influence.</returns>
    private static ObservationInfluence Obs(int index, double k)
        => new(index: index, paretoK: k, elpdLoo: -2.0, value: index, dataType: DataComponentType.Exact, count: 1);

    /// <summary>
    /// A Pareto k that could not be estimated is an unreliable pointwise unit: it counts above every
    /// category limit and makes the diagnostics unreliable, while the mean and maximum ignore it.
    /// </summary>
    [TestMethod]
    public void NaNParetoK_CountsAboveEveryLimit_AndIsUnreliable()
    {
        var observations = new ObservationInfluence[200];
        for (int i = 0; i < observations.Length; i++)
            observations[i] = Obs(i, 0.2);
        observations[7] = Obs(7, double.NaN);

        var diag = new InfluenceDiagnostics(observations);

        Assert.AreEqual(1, diag.CountParetoKAbove05);
        Assert.AreEqual(1, diag.CountParetoKAbove07);
        Assert.AreEqual(1, diag.CountParetoKAbove10);
        Assert.IsFalse(diag.IsReliable);
        Assert.AreEqual(0.2, diag.MeanParetoK, 1e-12);
        Assert.AreEqual(0.2, diag.MaxParetoK, 1e-12);
        Assert.AreEqual(ParetoKCategory.VeryBad, diag[7].Category);
    }

    /// <summary>
    /// Without an explicit threshold the problematic observations are selected with the instance's
    /// own limit (the draw-count limit for Bayesian diagnostics), and unestimated Pareto k values
    /// are always included, ordered first.
    /// </summary>
    [TestMethod]
    public void GetProblematicObservations_DefaultsToInstanceLimit_AndIncludesNaN()
    {
        const double drawCountLimit = 0.375803649418215;
        var diag = new InfluenceDiagnostics(
            new[] { 0.1, 0.41, double.NaN, 0.8 },
            new[] { -1.0, -2.0, -3.0, -4.0 },
            dataComponents: null,
            drawCountLimit);

        int[] defaultSelection = diag.GetProblematicObservations().Select(o => o.Index).ToArray();
        int[] explicitSelection = diag.GetProblematicObservations(0.7).Select(o => o.Index).ToArray();

        CollectionAssert.AreEqual(new[] { 2, 3, 1 }, defaultSelection);
        CollectionAssert.AreEqual(new[] { 2, 3 }, explicitSelection);
    }

    /// <summary>
    /// Diagnostics built through the public constructors keep the fixed 0.7 limit as the default.
    /// </summary>
    [TestMethod]
    public void GetProblematicObservations_LegacyDiagnostics_DefaultToFixedLimit()
    {
        var diag = new InfluenceDiagnostics(new[] { Obs(0, 0.1), Obs(1, 0.41), Obs(2, 0.75) });

        int[] selection = diag.GetProblematicObservations().Select(o => o.Index).ToArray();

        CollectionAssert.AreEqual(new[] { 2 }, selection);
    }
}
