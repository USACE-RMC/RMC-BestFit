using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using ModelAnalyses = RMC.BestFit.Analyses;
using UIUnivariateAnalysis = RMC.BestFit.UI.UnivariateAnalysis;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// Unit tests for <see cref="CompositeAnalysis"/>, the UI wrapper for composite distribution analysis.
/// </summary>
/// <remarks>
/// All tests require STA thread because the constructor creates OxyPlot WPF Plot objects.
/// Tests focus on: constructor defaults, property change notifications, IsEstimated flag,
/// Analyses collection, and plot initialization. Save/Open/Delete are excluded (require SQLite).
/// </remarks>
[TestClass]
public class CompositeAnalysisTests
{
    private static UnivariateAnalysisCollection? _collection;

    /// <summary>
    /// Creates a shared collection backed by the singleton BestFitProject.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _collection = new UnivariateAnalysisCollection(BestFitProject.GetInstance());
    }

    /// <summary>
    /// Verifies that the constructor stores the provided name.
    /// </summary>
    [STATestMethod]
    public void Constructor_StoresName()
    {
        var ca = new CompositeAnalysis("TestCA", _collection!);

        Assert.AreEqual("TestCA", ca.Name);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.NameOnDisk"/> matches the constructor name.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var ca = new CompositeAnalysis("DiskCA", _collection!);

        Assert.AreEqual("DiskCA", ca.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.IsEstimated"/> is <c>false</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var ca = new CompositeAnalysis("EstCA", _collection!);

        Assert.IsFalse(ca.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.InputData"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_InputData_IsNullInitially()
    {
        var ca = new CompositeAnalysis("InputCA", _collection!);

        Assert.IsNull(ca.InputData);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.Analyses"/> is not null and empty after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_Analyses_IsNotNullAndEmpty()
    {
        var ca = new CompositeAnalysis("AnalysesCA", _collection!);

        Assert.IsNotNull(ca.Analyses);
        Assert.AreEqual(0, ca.Analyses.Count, "CompositeAnalysis should start with an empty Analyses collection.");
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.ProbabilityOrdinates"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_ProbabilityOrdinates_IsNotNull()
    {
        var ca = new CompositeAnalysis("ProbCA", _collection!);

        Assert.IsNotNull(ca.ProbabilityOrdinates);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.FrequencyPlot"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_FrequencyPlot_IsNotNull()
    {
        var ca = new CompositeAnalysis("PlotCA", _collection!);

        Assert.IsNotNull(ca.FrequencyPlot);
    }

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var ca = new CompositeAnalysis("PropCA", _collection!);
        var raised = new List<string>();
        ca.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ca.Description = "A description";

        Assert.IsTrue(raised.Contains(nameof(CompositeAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var ca = new CompositeAnalysis("PropCA2", _collection!);
        ca.Description = "Same";

        var raised = new List<string>();
        ca.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ca.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(CompositeAnalysis.Description));
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.CreationDate"/> and <see cref="CompositeAnalysis.LastModified"/>
    /// are within a few seconds of construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var ca = new CompositeAnalysis("DateCA", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(ca.CreationDate >= before && ca.CreationDate <= after);
        Assert.IsTrue(ca.LastModified >= before && ca.LastModified <= after);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.ClearResults"/> does not throw
    /// and leaves IsEstimated as false.
    /// </summary>
    [STATestMethod]
    public void ClearResults_WhenNotEstimated_DoesNotThrow()
    {
        var ca = new CompositeAnalysis("ClearCA", _collection!);

        // Must not throw
        ca.ClearResults();

        Assert.IsFalse(ca.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.InnerAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void InnerAnalysis_IsNotNull()
    {
        var ca = new CompositeAnalysis("InnerCA", _collection!);
        Assert.IsNotNull(ca.InnerAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.AnalysisResults"/> is null until the analysis runs.
    /// </summary>
    [STATestMethod]
    public void AnalysisResults_BeforeRun_IsNull()
    {
        var ca = new CompositeAnalysis("ResCA", _collection!);
        Assert.IsNull(ca.AnalysisResults);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.BayesianAnalysis"/> is not null.
    /// </summary>
    [STATestMethod]
    public void BayesianAnalysis_IsNotNull()
    {
        var ca = new CompositeAnalysis("BACA", _collection!);
        Assert.IsNotNull(ca.BayesianAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.RaisePreviewSaved"/> does not throw or flip
    /// cancel when there are no PreviewObjectSaved subscribers.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var ca = new CompositeAnalysis("PrevCA", _collection!);
        bool cancel = false;

        ca.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.GetMarginalModel"/> returns null —
    /// CompositeAnalysis cannot serve as a marginal because it has no owned InputData.
    /// </summary>
    [STATestMethod]
    public void GetMarginalModel_ReturnsNullForComposite()
    {
        var ca = new CompositeAnalysis("MarginalCA", _collection!);

        Assert.IsNull(ca.GetMarginalModel(),
            "CompositeAnalysis cannot serve as a marginal — GetMarginalModel returns null per IUnivariate doc.");
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.ElementImageResourceKey"/> returns the expected key.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var ca = new CompositeAnalysis("ImgKeyCA", _collection!);
        Assert.AreEqual("CompositeAnalysisIcon", ca.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies the static <see cref="CompositeAnalysis.CollectionName"/> constant.
    /// </summary>
    [TestMethod]
    public void CollectionName_IsExpectedConstant()
    {
        Assert.AreEqual("<Composite Distribution>", CompositeAnalysis.CollectionName);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.IsValid"/> is <c>false</c> on a freshly
    /// constructed instance because no component analyses have been added.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_IsFalseInitiallyDueToNoComponents()
    {
        var ca = new CompositeAnalysis("IsValidCA", _collection!);

        Assert.IsFalse(ca.IsValid,
            "Composite analysis is invalid until at least one component is added.");
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.Name"/> setter raises PropertyChanged on rename.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var ca = new CompositeAnalysis("NameCA", _collection!);
        var raised = new List<string>();
        ca.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ca.Name = "NameCA-Renamed";

        Assert.IsTrue(raised.Contains(nameof(CompositeAnalysis.Name)));
        Assert.AreEqual("NameCA-Renamed", ca.Name);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.Analyses"/> is not null and starts empty.
    /// </summary>
    [STATestMethod]
    public void Analyses_StartsEmpty()
    {
        var ca = new CompositeAnalysis("AnalysesCA", _collection!);

        Assert.IsNotNull(ca.Analyses);
        Assert.AreEqual(0, ca.Analyses.Count);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.CompositeDistributionType"/> default is
    /// <see cref="RMC.BestFit.Analyses.CompositeType.CompetingRisks"/>.
    /// </summary>
    [STATestMethod]
    public void CompositeDistributionType_DefaultIsCompetingRisks()
    {
        var ca = new CompositeAnalysis("DistTypeCA", _collection!);

        Assert.AreEqual(RMC.BestFit.Analyses.CompositeType.CompetingRisks, ca.CompositeDistributionType);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.IsMaximum"/> default is <c>true</c>
    /// (competing risks selects the per-realization maximum).
    /// </summary>
    [STATestMethod]
    public void IsMaximum_DefaultIsTrue()
    {
        var ca = new CompositeAnalysis("IsMaxCA", _collection!);

        Assert.IsTrue(ca.IsMaximum);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.ModelAverageMethod"/> default is
    /// <see cref="RMC.BestFit.Analyses.AverageMethod.DIC"/>.
    /// </summary>
    [STATestMethod]
    public void ModelAverageMethod_DefaultIsDIC()
    {
        var ca = new CompositeAnalysis("AvgMethodCA", _collection!);

        Assert.AreEqual(RMC.BestFit.Analyses.AverageMethod.DIC, ca.ModelAverageMethod);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.IsMaximum"/> setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void IsMaximum_Setter_RaisesPropertyChanged()
    {
        var ca = new CompositeAnalysis("IsMaxSetCA", _collection!);
        var raised = new List<string>();
        ca.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ca.IsMaximum = false;

        Assert.IsTrue(raised.Contains(nameof(CompositeAnalysis.IsMaximum)));
        Assert.IsFalse(ca.IsMaximum);
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.IsMaximum"/> set to same value does NOT raise.
    /// </summary>
    [STATestMethod]
    public void IsMaximum_SameValue_DoesNotRaisePropertyChanged()
    {
        var ca = new CompositeAnalysis("IsMaxSameCA", _collection!);

        var raised = new List<string>();
        ca.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ca.IsMaximum = true; // same as default

        CollectionAssert.DoesNotContain(raised, nameof(CompositeAnalysis.IsMaximum));
    }

    /// <summary>
    /// Verifies that <see cref="CompositeAnalysis.CancelAnalysis"/> does not throw when not running.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var ca = new CompositeAnalysis("CancelCA", _collection!);

        // Must not throw
        ca.CancelAnalysis();
    }

    /// <summary>
    /// Regression test: <see cref="CompositeAnalysis.CancelAnalysis"/> must delegate
    /// to the inner model analysis so the App's CancelButton actually stops the run.
    /// The previous implementation was an empty <c>return;</c> stub, which silently
    /// dropped every Cancel button click on the model floor — the parallel loops in
    /// <c>CreateFrequencyAnalysisResultsAsync</c> never observed the cancel and the
    /// simulation kept running until natural completion.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_FlipsInnerAnalysisCancellationToken()
    {
        var ca = new CompositeAnalysis("CancelTokenCA", _collection!);
        var inner = (RMC.BestFit.Analyses.CompositeAnalysis)ca.InnerAnalysis;

        Assert.IsFalse(inner.CancellationTokenSource.IsCancellationRequested,
            "Sanity: a fresh inner analysis should have an un-canceled CancellationTokenSource.");

        ca.CancelAnalysis();

        Assert.IsTrue(inner.CancellationTokenSource.IsCancellationRequested,
            "After CompositeAnalysis.CancelAnalysis(), the inner model analysis's " +
            "CancellationTokenSource must be canceled — otherwise the App's Cancel " +
            "button is a visual-only no-op and the parallel simulation runs to completion.");
    }

    /// <summary>
    /// Verifies the posterior-resampling seed is preserved by copy and participates in
    /// the Bayesian-settings undo bridge.
    /// </summary>
    [STATestMethod]
    public void PRNGSeed_CopyAndUndoRedo_PreserveValue()
    {
        var composite = new CompositeAnalysis("SeedComposite", _collection!);
        int originalSeed = composite.BayesianAnalysis.PRNGSeed;
        composite.UndoManager.Clear();

        composite.BayesianAnalysis.PRNGSeed = 112358;
        Assert.IsTrue(composite.UndoManager.CanUndo);
        var copy = (CompositeAnalysis)composite.Copy("SeedCompositeCopy");
        Assert.AreEqual(112358, copy.BayesianAnalysis.PRNGSeed);

        composite.UndoManager.Undo();
        Assert.AreEqual(originalSeed, composite.BayesianAnalysis.PRNGSeed);
        composite.UndoManager.Redo();
        Assert.AreEqual(112358, composite.BayesianAnalysis.PRNGSeed);
    }
    /// <summary>
    /// Verifies that a child <see cref="RMC.BestFit.UI.UnivariateAnalysis.IsEstimated"/> event recomputes
    /// App-bound DIC model-average weights after the batch-run event order completes.
    /// </summary>
    [STATestMethod]
    public void ChildIsEstimatedChange_RecomputesDicWeightsAfterBatchOrderedRefit()
    {
        VerifyChildIsEstimatedChangeRecomputesWeights(ModelAnalyses.AverageMethod.DIC);
    }

    /// <summary>
    /// Verifies that a child <see cref="RMC.BestFit.UI.UnivariateAnalysis.IsEstimated"/> event recomputes
    /// App-bound WAIC model-average weights after the batch-run event order completes.
    /// </summary>
    [STATestMethod]
    public void ChildIsEstimatedChange_RecomputesWaicWeightsAfterBatchOrderedRefit()
    {
        VerifyChildIsEstimatedChangeRecomputesWeights(ModelAnalyses.AverageMethod.WAIC);
    }

    /// <summary>
    /// Verifies that a child <see cref="RMC.BestFit.UI.UnivariateAnalysis.IsEstimated"/> event recomputes
    /// App-bound LOOIC model-average weights after the batch-run event order completes.
    /// </summary>
    [STATestMethod]
    public void ChildIsEstimatedChange_RecomputesLooicWeightsAfterBatchOrderedRefit()
    {
        VerifyChildIsEstimatedChangeRecomputesWeights(ModelAnalyses.AverageMethod.LOOIC);
    }

    /// <summary>
    /// Verifies child completions clear stale composite results once while later completions only refresh weights.
    /// </summary>
    [STATestMethod]
    public void ChildCompletion_InvalidatesCompositeResultsOnceAndRefreshesLaterWeightsQuietly()
    {
        var childA = CreateEstimatedChild("StaleChildA", 200.0);
        var childB = CreateEstimatedChild("StaleChildB", 210.0);
        var composite = new CompositeAnalysis("StaleComposite", _collection!)
        {
            CompositeDistributionType = ModelAnalyses.CompositeType.ModelAverage,
            ModelAverageMethod = ModelAnalyses.AverageMethod.DIC
        };

        composite.Analyses.Add(new WeightedUnivariateAnalysis { UnivariateAnalysis = childA, Weight = 0.5 });
        composite.Analyses.Add(new WeightedUnivariateAnalysis { UnivariateAnalysis = childB, Weight = 0.5 });

        var innerComposite = (ModelAnalyses.CompositeAnalysis)composite.InnerAnalysis;
        innerComposite.RestoreAnalysisResults(CreateResults(1.0));

        int analysisResultsEvents = 0;
        int isEstimatedEvents = 0;
        int analysesEvents = 0;
        composite.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CompositeAnalysis.AnalysisResults)) analysisResultsEvents++;
            if (e.PropertyName == nameof(CompositeAnalysis.IsEstimated)) isEstimatedEvents++;
            if (e.PropertyName == nameof(CompositeAnalysis.Analyses)) analysesEvents++;
        };

        SimulateBatchOrderedChildRefit(childB, 150.0);

        Assert.AreEqual(1, analysisResultsEvents,
            "The first child refit should clear stale composite results once.");
        Assert.AreEqual(1, isEstimatedEvents,
            "The first child refit should transition the composite from estimated to unestimated once.");
        Assert.AreEqual(0, analysesEvents,
            "Child completion should not raise composite Analyses; row-level Weight notifications are sufficient.");
        Assert.IsNull(composite.AnalysisResults);
        Assert.IsFalse(composite.IsEstimated);
        Assert.IsTrue(composite.Analyses[1].Weight > composite.Analyses[0].Weight,
            "The completed child should still refresh model-average weights after stale results are cleared.");

        SimulateBatchOrderedChildRefit(childA, 160.0);

        Assert.AreEqual(1, analysisResultsEvents,
            "Once composite results are already clear, later child completions must not clear plots/tables again.");
        Assert.AreEqual(1, isEstimatedEvents,
            "Later child completions should leave the already-unestimated composite quiet.");
        Assert.AreEqual(0, analysesEvents,
            "Later weight-only refreshes should still avoid composite Analyses notifications.");
        AssertWeightsMatchInnerAnalysis(composite);
    }

    /// <summary>
    /// Exercises the UI wrapper event path shared by DIC, WAIC, and LOOIC weighting.
    /// </summary>
    /// <param name="method">The information criterion to use for model-average weighting.</param>
    private static void VerifyChildIsEstimatedChangeRecomputesWeights(ModelAnalyses.AverageMethod method)
    {
        var childA = CreateEstimatedChild($"{method}ChildA", 200.0);
        var childB = CreateEstimatedChild($"{method}ChildB", 210.0);
        var composite = new CompositeAnalysis($"{method}Composite", _collection!)
        {
            CompositeDistributionType = ModelAnalyses.CompositeType.ModelAverage,
            ModelAverageMethod = method
        };

        composite.Analyses.Add(new WeightedUnivariateAnalysis { UnivariateAnalysis = childA, Weight = 0.5 });
        composite.Analyses.Add(new WeightedUnivariateAnalysis { UnivariateAnalysis = childB, Weight = 0.5 });

        Assert.IsTrue(composite.Analyses[0].Weight > composite.Analyses[1].Weight,
            "Sanity: the lower initial criterion should give child A the larger model-average weight.");

        double initialWeightA = composite.Analyses[0].Weight;
        double initialWeightB = composite.Analyses[1].Weight;
        int analysisResultsEvents = 0;
        int analysesEvents = 0;
        composite.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CompositeAnalysis.AnalysisResults)) analysisResultsEvents++;
            if (e.PropertyName == nameof(CompositeAnalysis.Analyses)) analysesEvents++;
        };

        SimulateBatchOrderedChildRefit(childB, 150.0, () =>
        {
            Assert.AreEqual(initialWeightA, composite.Analyses[0].Weight, 1e-12,
                $"{method} weights should not refresh while the child still has IsEstimated=false.");
            Assert.AreEqual(initialWeightB, composite.Analyses[1].Weight, 1e-12,
                $"{method} weights should wait for the final IsEstimated=true signal.");
        });

        Assert.IsTrue(composite.Analyses[1].Weight > composite.Analyses[0].Weight,
            $"{method} weights should refresh after the child IsEstimated event so the improved child B dominates.");
        Assert.AreEqual(0, analysisResultsEvents,
            "When composite results are already clear, child completion should not emit plot/table-clearing notifications.");
        Assert.AreEqual(0, analysesEvents,
            "Weight-only child completion should not raise composite Analyses notifications.");
        AssertWeightsMatchInnerAnalysis(composite);
    }

    /// <summary>
    /// Creates a UI child analysis whose inner model analysis has completed synthetic results.
    /// </summary>
    /// <param name="name">The child analysis name.</param>
    /// <param name="criterion">The criterion value to store on the child.</param>
    /// <returns>An estimated UI child analysis with synthetic results.</returns>
    private static UIUnivariateAnalysis CreateEstimatedChild(string name, double criterion)
    {
        var child = new UIUnivariateAnalysis(name, _collection!);
        var inner = GetInnerUnivariateAnalysis(child);

        SetInformationCriteria(inner.BayesianAnalysis, criterion);
        inner.BayesianAnalysis.IsEstimated = true;
        SetAnalysisResults(inner, CreateResults(criterion));
        SetAnalysisEstimated(inner, true);

        return child;
    }

    /// <summary>
    /// Simulates the production batch sequence where a child result update is raised before
    /// the child analysis flips <see cref="RMC.BestFit.UI.UnivariateAnalysis.IsEstimated"/> back to true.
    /// </summary>
    /// <param name="child">The child analysis to refit synthetically.</param>
    /// <param name="criterion">The new criterion value to apply.</param>
    /// <param name="afterIntermediateResults">Optional assertion hook after results are present but before final estimation.</param>
    private static void SimulateBatchOrderedChildRefit(UIUnivariateAnalysis child, double criterion, Action? afterIntermediateResults = null)
    {
        var inner = GetInnerUnivariateAnalysis(child);

        SetAnalysisResults(inner, null);
        SetAnalysisEstimated(inner, false);
        RaiseAnalysisPropertyChanged(inner, nameof(ModelAnalyses.UnivariateAnalysis.AnalysisResults));
        RaiseAnalysisPropertyChanged(inner, nameof(ModelAnalyses.UnivariateAnalysis.IsEstimated));

        SetInformationCriteria(inner.BayesianAnalysis, criterion);
        SetAnalysisResults(inner, CreateResults(criterion));
        RaiseAnalysisPropertyChanged(inner, nameof(ModelAnalyses.UnivariateAnalysis.AnalysisResults));
        afterIntermediateResults?.Invoke();

        SetAnalysisEstimated(inner, true);
        RaiseAnalysisPropertyChanged(inner, nameof(ModelAnalyses.UnivariateAnalysis.IsEstimated));
    }

    /// <summary>
    /// Gets the model-layer analysis from a UI univariate wrapper.
    /// </summary>
    /// <param name="child">The UI child analysis.</param>
    /// <returns>The child model-layer univariate analysis.</returns>
    private static ModelAnalyses.UnivariateAnalysis GetInnerUnivariateAnalysis(UIUnivariateAnalysis child)
    {
        return (ModelAnalyses.UnivariateAnalysis)child.InnerAnalysis;
    }

    /// <summary>
    /// Creates synthetic frequency-analysis results with deterministic goodness-of-fit values.
    /// </summary>
    /// <param name="criterion">The value to use for scalar goodness-of-fit metrics.</param>
    /// <returns>A new uncertainty result object.</returns>
    private static UncertaintyAnalysisResults CreateResults(double criterion)
    {
        return new UncertaintyAnalysisResults
        {
            AIC = criterion,
            BIC = criterion,
            RMSE = criterion
        };
    }

    /// <summary>
    /// Stores deterministic information criteria on a child Bayesian analysis.
    /// </summary>
    /// <param name="bayesianAnalysis">The Bayesian analysis to update.</param>
    /// <param name="criterion">The criterion value to assign.</param>
    private static void SetInformationCriteria(BayesianAnalysis bayesianAnalysis, double criterion)
    {
        SetPrivateDoubleProperty(bayesianAnalysis, nameof(BayesianAnalysis.DIC), criterion);
        SetPrivateDoubleProperty(bayesianAnalysis, nameof(BayesianAnalysis.WAIC), criterion);
        SetPrivateDoubleProperty(bayesianAnalysis, nameof(BayesianAnalysis.LOOIC), criterion);
    }

    /// <summary>
    /// Assigns the private-set <see cref="ModelAnalyses.UnivariateAnalysis.AnalysisResults"/> property.
    /// </summary>
    /// <param name="analysis">The model analysis to update.</param>
    /// <param name="results">The synthetic results, or <c>null</c> to clear them.</param>
    private static void SetAnalysisResults(ModelAnalyses.UnivariateAnalysis analysis, UncertaintyAnalysisResults? results)
    {
        typeof(ModelAnalyses.UnivariateAnalysis)
            .GetProperty(nameof(ModelAnalyses.UnivariateAnalysis.AnalysisResults), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(analysis, results);
    }

    /// <summary>
    /// Assigns the protected estimated-state backing field without raising an event.
    /// </summary>
    /// <param name="analysis">The model analysis to update.</param>
    /// <param name="isEstimated">The estimated-state value.</param>
    private static void SetAnalysisEstimated(ModelAnalyses.UnivariateAnalysis analysis, bool isEstimated)
    {
        typeof(ModelAnalyses.AnalysisBase)
            .GetField("_isEstimated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(analysis, isEstimated);
    }

    /// <summary>
    /// Raises a model-layer property change so the UI wrapper forwarding path is exercised.
    /// </summary>
    /// <param name="analysis">The model analysis that should raise the event.</param>
    /// <param name="propertyName">The property name to raise.</param>
    private static void RaiseAnalysisPropertyChanged(ModelAnalyses.UnivariateAnalysis analysis, string propertyName)
    {
        typeof(ModelAnalyses.AnalysisBase)
            .GetMethod("RaisePropertyChange", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(analysis, new object?[] { propertyName });
    }

    /// <summary>
    /// Assigns a BayesianAnalysis property with a private setter.
    /// </summary>
    /// <param name="target">The Bayesian analysis object to update.</param>
    /// <param name="propertyName">The criterion property name.</param>
    /// <param name="value">The value to assign.</param>
    private static void SetPrivateDoubleProperty(BayesianAnalysis target, string propertyName, double value)
    {
        target.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(true)!
            .Invoke(target, new object[] { value });
    }

    /// <summary>
    /// Verifies UI-displayed weights are synchronized with the model-layer composite weights.
    /// </summary>
    /// <param name="composite">The UI composite analysis to inspect.</param>
    private static void AssertWeightsMatchInnerAnalysis(CompositeAnalysis composite)
    {
        var inner = (ModelAnalyses.CompositeAnalysis)composite.InnerAnalysis;
        for (int i = 0; i < composite.Analyses.Count; i++)
        {
            Assert.AreEqual(inner.Analyses[i].Weight, composite.Analyses[i].Weight, 1e-12,
                $"UI weight at index {i} should match the inner model weight.");
        }
    }
}
