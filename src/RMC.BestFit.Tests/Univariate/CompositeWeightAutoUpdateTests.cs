using System.Reflection;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using DataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Tests that the model-average weights inside a <see cref="CompositeAnalysis"/>
/// auto-refresh when a sub-analysis re-runs, without the user having to toggle
/// the <see cref="CompositeAnalysis.ModelAverageMethod"/> setter.
/// </summary>
/// <remarks>
/// <para>
/// Regression for the bug where re-fitting a child analysis left the composite's
/// weights at zero until the user manually flipped <c>ModelAverageMethod</c> to
/// force a recompute. Root cause: <c>UnivariateAnalysis.RunAsync</c> sets
/// <c>AnalysisResults</c> to its new value BEFORE flipping <c>IsEstimated</c> to
/// <c>true</c> (see UnivariateAnalysis.cs lines 503 → 510), so the
/// <c>WeightedAnalysis_PropertyChanged</c> handler that listened only for
/// <c>"AnalysisResults"</c> ran <c>EstimateModelWeights</c> while the child still
/// reported <c>IsEstimated == false</c>; the validity filter at
/// CompositeAnalysis.cs:561 then dropped the freshly-fit child from the weighted
/// average. The fix is to also listen for <c>"IsEstimated"</c>.
/// </para>
/// </remarks>
[TestClass]
public class CompositeWeightAutoUpdateTests
{
    #region Inline test fixtures

    private static readonly double[] InlineFloodData = new Normal(15000.0, 5000.0)
        .GenerateRandomValues(40, 12345);

    private static DataFrame CreateDataFrame()
    {
        var df = new DataFrame();
        for (int i = 0; i < InlineFloodData.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, InlineFloodData[i]));
        return df;
    }

    /// <summary>
    /// Builds a <see cref="UnivariateAnalysis"/> already marked as estimated, with an
    /// injected <see cref="MCMCResults"/> + a populated <see cref="UncertaintyAnalysisResults"/>.
    /// AIC and DIC are stored on AnalysisResults / BayesianAnalysis respectively at the
    /// supplied values so the composite's weight calculation has deterministic inputs.
    /// </summary>
    private static UnivariateAnalysis CreateFitChild(double[] mapValues, double aic, double dic)
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);

        // Inject MCMC results.
        var output = new List<ParameterSet>(50);
        for (int i = 0; i < 50; i++)
            output.Add(new ParameterSet((double[])mapValues.Clone(), 0.0));
        var mcmc = new MCMCResults(new ParameterSet((double[])mapValues.Clone(), 0.0), output, 0.10);
        analysis.BayesianAnalysis.OutputLength = 50;
        analysis.BayesianAnalysis.SetCustomMCMCResults(mcmc, skipInformationCriteria: true);

        // Force IsEstimated true on the child. AnalysisBase exposes a protected setter.
        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        isEstField.SetValue(analysis, true);

        // Set DIC on the BayesianAnalysis (writable; mirrors the post-MCMC setter path).
        var dicProp = typeof(BayesianAnalysis).GetProperty("DIC",
            BindingFlags.Instance | BindingFlags.Public)!;
        dicProp.SetValue(analysis.BayesianAnalysis, dic);

        // Stash an UncertaintyAnalysisResults with a known AIC. The default ctor produces
        // an empty results object the composite can read AIC/BIC/RMSE from.
        var resultsProp = typeof(UnivariateAnalysis).GetProperty("AnalysisResults",
            BindingFlags.Instance | BindingFlags.Public)!;
        var newResults = new UncertaintyAnalysisResults { AIC = aic };
        resultsProp.SetValue(analysis, newResults);
        return analysis;
    }

    private static CompositeAnalysis CreateModelAverageComposite(
        UnivariateAnalysis childA, double weightA,
        UnivariateAnalysis childB, double weightB,
        AverageMethod method = AverageMethod.AIC)
    {
        var composite = new CompositeAnalysis();
        composite.ProbabilityOrdinates.Add(0.99);
        composite.ProbabilityOrdinates.Add(0.5);
        composite.ProbabilityOrdinates.Add(0.01);
        composite.CompositeDistributionType = CompositeType.ModelAverage;
        composite.ModelAverageMethod = method;
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childA, weightA));
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childB, weightB));
        return composite;
    }

    /// <summary>
    /// Mimics what <c>UnivariateAnalysis.RunAsync</c> does at the *end* of a re-fit:
    /// (1) assign a new <c>AnalysisResults</c> object, then (2) set <c>IsEstimated</c>
    /// back to true. Important: the order matches production code so the test exercises
    /// the exact event sequence the handler must cope with.
    /// </summary>
    private static void SimulateChildReFit(UnivariateAnalysis child, double newAic, double newDic)
    {
        // Step 1 (matches UnivariateAnalysis.ClearResults): drop AnalysisResults and IsEstimated.
        var resultsProp = typeof(UnivariateAnalysis).GetProperty("AnalysisResults",
            BindingFlags.Instance | BindingFlags.Public)!;
        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var raise = typeof(AnalysisBase).GetMethod("RaisePropertyChange",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        resultsProp.SetValue(child, null);
        raise.Invoke(child, new object?[] { nameof(child.AnalysisResults) });
        isEstField.SetValue(child, false);
        raise.Invoke(child, new object?[] { nameof(child.IsEstimated) });

        // Step 2 (matches the post-MCMC ordering at UnivariateAnalysis.cs:503 → 510):
        // assign new AnalysisResults FIRST, fire its event, THEN flip IsEstimated.
        var newResults = new UncertaintyAnalysisResults { AIC = newAic };
        resultsProp.SetValue(child, newResults);
        raise.Invoke(child, new object?[] { nameof(child.AnalysisResults) });

        // DIC is on BayesianAnalysis; set before IsEstimated flips so the composite
        // sees a consistent valid state when EstimateModelWeights runs.
        var dicProp = typeof(BayesianAnalysis).GetProperty("DIC",
            BindingFlags.Instance | BindingFlags.Public)!;
        dicProp.SetValue(child.BayesianAnalysis, newDic);

        isEstField.SetValue(child, true);
        raise.Invoke(child, new object?[] { nameof(child.IsEstimated) });
    }

    #endregion

    #region Auto-update on re-fit

    /// <summary>
    /// After a child analysis re-fits, the composite's per-child weights must reflect
    /// the new AIC values without the user having to toggle ModelAverageMethod.
    /// </summary>
    [TestMethod]
    public void ModelAverage_AIC_WeightsAutoUpdateAfterChildReFit()
    {
        // Initial fit: childA has AIC 100 (better), childB has AIC 110 (worse).
        var childA = CreateFitChild(new[] { 15000.0, 5000.0 }, aic: 100.0, dic: 200.0);
        var childB = CreateFitChild(new[] { 16000.0, 5500.0 }, aic: 110.0, dic: 210.0);
        var composite = CreateModelAverageComposite(childA, 0.5, childB, 0.5);
        // Trigger initial weight estimation by toggling the method (mirrors how the UI
        // currently bootstraps the composite).
        composite.EstimateModelWeights();
        double initialWeightA = composite.Analyses[0].Weight;
        double initialWeightB = composite.Analyses[1].Weight;

        // Sanity: childA should outweigh childB at AIC 100 vs 110.
        Assert.IsTrue(initialWeightA > initialWeightB,
            "Initial weights should favor the child with the lower AIC.");

        // Now simulate childB re-running with a much better AIC (60) — childB should
        // jump to dominating the model-averaged composite.
        SimulateChildReFit(childB, newAic: 60.0, newDic: 150.0);

        double newWeightA = composite.Analyses[0].Weight;
        double newWeightB = composite.Analyses[1].Weight;

        Assert.IsTrue(newWeightB > newWeightA,
            $"After childB re-fits with AIC=60, its weight ({newWeightB}) must exceed " +
            $"childA's ({newWeightA}). Failure means weights did NOT auto-recompute on re-fit.");
        Assert.IsTrue(newWeightB > 0.5,
            "ChildB's weight should dominate the average given a 40-point AIC advantage.");
        Assert.AreEqual(1.0, newWeightA + newWeightB, 1e-9, "Weights should sum to 1.");
    }

    /// <summary>
    /// Auto-update also fires when the averaging method is DIC and a child's
    /// <see cref="BayesianAnalysis.DIC"/> changes via re-fit.
    /// </summary>
    [TestMethod]
    public void ModelAverage_DIC_WeightsAutoUpdateAfterChildReFit()
    {
        var childA = CreateFitChild(new[] { 15000.0, 5000.0 }, aic: 100.0, dic: 200.0);
        var childB = CreateFitChild(new[] { 16000.0, 5500.0 }, aic: 110.0, dic: 210.0);
        var composite = CreateModelAverageComposite(childA, 0.5, childB, 0.5,
            method: AverageMethod.DIC);
        composite.EstimateModelWeights();
        double initialWeightA = composite.Analyses[0].Weight;

        // ChildA's DIC drops dramatically — should pull the average toward A even more.
        SimulateChildReFit(childA, newAic: 100.0, newDic: 150.0);

        double newWeightA = composite.Analyses[0].Weight;
        Assert.IsTrue(newWeightA > initialWeightA,
            $"ChildA's weight should increase after its DIC improves from 200 to 150 " +
            $"({initialWeightA} → {newWeightA}).");
    }

    /// <summary>
    /// Explicit IsEstimated-only event (with AnalysisResults already set ahead of time)
    /// should still trigger weight recomputation. This isolates the new
    /// <c>nameof(IsEstimated)</c> branch in <c>WeightedAnalysis_PropertyChanged</c>
    /// from the AnalysisResults branch.
    /// </summary>
    [TestMethod]
    public void IsEstimated_PropertyChange_TriggersWeightRecompute()
    {
        var childA = CreateFitChild(new[] { 15000.0, 5000.0 }, aic: 100.0, dic: 200.0);
        var childB = CreateFitChild(new[] { 16000.0, 5500.0 }, aic: 110.0, dic: 210.0);
        var composite = CreateModelAverageComposite(childA, 0.5, childB, 0.5);
        composite.EstimateModelWeights();

        // Sneak childB's AIC down to 50 WITHOUT firing AnalysisResults — write the value
        // directly on the existing results object. Without an IsEstimated handler, this
        // change would be invisible to the composite until a manual toggle.
        childB.AnalysisResults!.AIC = 50.0;

        // Fire IsEstimated as if the child completed a re-fit.
        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var raise = typeof(AnalysisBase).GetMethod("RaisePropertyChange",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        isEstField.SetValue(childB, false);
        raise.Invoke(childB, new object?[] { nameof(childB.IsEstimated) });
        isEstField.SetValue(childB, true);
        raise.Invoke(childB, new object?[] { nameof(childB.IsEstimated) });

        Assert.IsTrue(composite.Analyses[1].Weight > composite.Analyses[0].Weight,
            "ChildB should dominate after dropping AIC 110 → 50 if the IsEstimated " +
            "handler correctly triggers weight recomputation.");
    }

    /// <summary>
    /// Auto-update is a no-op when the composite is NOT in ModelAverage mode
    /// (Mixture / CompetingRisks composites use user-supplied weights, not data-driven).
    /// </summary>
    [TestMethod]
    public void IsEstimated_DoesNotMutateWeights_InMixtureMode()
    {
        var childA = CreateFitChild(new[] { 15000.0, 5000.0 }, aic: 100.0, dic: 200.0);
        var childB = CreateFitChild(new[] { 16000.0, 5500.0 }, aic: 110.0, dic: 210.0);
        var composite = new CompositeAnalysis();
        composite.ProbabilityOrdinates.Add(0.99);
        composite.ProbabilityOrdinates.Add(0.5);
        composite.ProbabilityOrdinates.Add(0.01);
        composite.CompositeDistributionType = CompositeType.Mixture;
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childA, 0.3));
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childB, 0.7));

        SimulateChildReFit(childB, newAic: 50.0, newDic: 100.0);

        // EstimateModelWeights short-circuits on non-ModelAverage modes
        // (CompositeAnalysis.cs:537), so the user-supplied weights survive.
        Assert.AreEqual(0.3, composite.Analyses[0].Weight, 1e-9);
        Assert.AreEqual(0.7, composite.Analyses[1].Weight, 1e-9);
    }

    #endregion
}
