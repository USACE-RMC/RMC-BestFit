using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

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
}
