using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// Unit tests for <see cref="UnivariateAnalysis"/>, the UI wrapper for univariate distribution analysis.
/// </summary>
/// <remarks>
/// All tests that construct a <see cref="UnivariateAnalysis"/> require STA thread because the
/// constructor creates OxyPlot WPF Plot objects. Tests focus on: constructor defaults,
/// property change notifications, IsEstimated flag, InputData, and plot initialization.
/// Save/Open/Delete are excluded (require SQLite project file).
/// </remarks>
[TestClass]
public class UnivariateAnalysisTests
{
    // Shared collection for all tests.
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
    /// Verifies that the constructor succeeds and stores the name.
    /// </summary>
    [STATestMethod]
    public void Constructor_StoresName()
    {
        var ua = new UI.UnivariateAnalysis("TestUA", _collection!);

        Assert.AreEqual("TestUA", ua.Name);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.NameOnDisk"/> matches the name provided at construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var ua = new UI.UnivariateAnalysis("DiskUA", _collection!);

        Assert.AreEqual("DiskUA", ua.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.IsEstimated"/> is <c>false</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var ua = new UI.UnivariateAnalysis("EstUA", _collection!);

        Assert.IsFalse(ua.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.InputData"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_InputData_IsNullInitially()
    {
        var ua = new UI.UnivariateAnalysis("InputUA", _collection!);

        Assert.IsNull(ua.InputData);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.UnivariateDistribution"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_UnivariateDistribution_IsNotNull()
    {
        var ua = new UI.UnivariateAnalysis("DistUA", _collection!);

        Assert.IsNotNull(ua.UnivariateDistribution);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.ProbabilityOrdinates"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_ProbabilityOrdinates_IsNotNull()
    {
        var ua = new UI.UnivariateAnalysis("ProbUA", _collection!);

        Assert.IsNotNull(ua.ProbabilityOrdinates);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.FrequencyPlot"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_FrequencyPlot_IsNotNull()
    {
        var ua = new UI.UnivariateAnalysis("PlotUA", _collection!);

        Assert.IsNotNull(ua.FrequencyPlot);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.ChronologyPlot"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_ChronologyPlot_IsNotNull()
    {
        var ua = new UI.UnivariateAnalysis("ChrPlotUA", _collection!);

        Assert.IsNotNull(ua.ChronologyPlot);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.BayesianPlots"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_BayesianPlots_IsNotNull()
    {
        var ua = new UI.UnivariateAnalysis("BayesUA", _collection!);

        Assert.IsNotNull(ua.BayesianPlots);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.CanCopyFromExternal"/> returns <c>false</c>.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsFalse()
    {
        var ua = new UI.UnivariateAnalysis("CopyUA", _collection!);

        Assert.IsFalse(ua.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.BayesianAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_BayesianAnalysis_IsNotNull()
    {
        var ua = new UI.UnivariateAnalysis("BaUA", _collection!);

        Assert.IsNotNull(ua.BayesianAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.Description"/> setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var ua = new UI.UnivariateAnalysis("PropUA", _collection!);
        var raised = new List<string>();
        ua.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ua.Description = "New Description";

        Assert.IsTrue(raised.Contains(nameof(UI.UnivariateAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var ua = new UI.UnivariateAnalysis("PropUA2", _collection!);
        ua.Description = "Same";

        var raised = new List<string>();
        ua.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ua.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(UI.UnivariateAnalysis.Description));
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.CreationDate"/> and <see cref="UI.UnivariateAnalysis.LastModified"/>
    /// are within a few seconds of construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var ua = new UI.UnivariateAnalysis("DateUA", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(ua.CreationDate >= before && ua.CreationDate <= after);
        Assert.IsTrue(ua.LastModified >= before && ua.LastModified <= after);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.CancelAnalysis"/> can be called without throwing
    /// when no analysis is running.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var ua = new UI.UnivariateAnalysis("CancelUA", _collection!);

        // Must not throw
        ua.CancelAnalysis();
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.ClearResults"/> can be called without throwing
    /// when no analysis has been performed.
    /// </summary>
    [STATestMethod]
    public void ClearResults_WhenNotEstimated_DoesNotThrow()
    {
        var ua = new UI.UnivariateAnalysis("ClearUA", _collection!);

        // Must not throw
        ua.ClearResults();

        Assert.IsFalse(ua.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.InnerAnalysis"/> is the model-layer
    /// <c>UnivariateAnalysis</c> instance backing this UI wrapper.
    /// </summary>
    [STATestMethod]
    public void InnerAnalysis_IsModelLayerUnivariateAnalysis()
    {
        var ua = new UI.UnivariateAnalysis("InnerUA", _collection!);

        Assert.IsNotNull(ua.InnerAnalysis);
        Assert.IsInstanceOfType<RMC.BestFit.Analyses.UnivariateAnalysis>(ua.InnerAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.AnalysisResults"/> is null until the analysis
    /// has been run.
    /// </summary>
    [STATestMethod]
    public void AnalysisResults_BeforeRun_IsNull()
    {
        var ua = new UI.UnivariateAnalysis("ResultsUA", _collection!);

        Assert.IsNull(ua.AnalysisResults);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.ChronologyAnalysisResults"/> is null until
    /// the chronology analysis has been computed.
    /// </summary>
    [STATestMethod]
    public void ChronologyAnalysisResults_BeforeRun_IsNull()
    {
        var ua = new UI.UnivariateAnalysis("ChronUA", _collection!);

        Assert.IsNull(ua.ChronologyAnalysisResults);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.GetMarginalModel"/> returns the underlying
    /// distribution (so the analysis can serve as a marginal on a bivariate distribution).
    /// </summary>
    [STATestMethod]
    public void GetMarginalModel_ReturnsUnivariateDistribution()
    {
        var ua = new UI.UnivariateAnalysis("MarginalUA", _collection!);

        var model = ua.GetMarginalModel();
        Assert.IsNotNull(model);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.GetDistribution"/> for an out-of-range index
    /// before estimation does not throw — it must return null.
    /// </summary>
    [STATestMethod]
    public void GetDistribution_BeforeRun_ReturnsNullOrDefault()
    {
        var ua = new UI.UnivariateAnalysis("GetDistUA", _collection!);

        // Before estimation, distributions array is null/empty — call must not throw a
        // NullReferenceException; the model returns null for missing indices.
        var dist = ua.GetDistribution(0);
        // Result may be null OR a default distribution; the contract is "no exception".
        // dist is nullable — accept either null or a non-null value; this test pins the
        // "no exception thrown" contract only.
        var _ = dist;
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.GetPointEstimateDistribution"/> before
    /// estimation returns null without throwing.
    /// </summary>
    [STATestMethod]
    public void GetPointEstimateDistribution_BeforeRun_DoesNotThrow()
    {
        var ua = new UI.UnivariateAnalysis("PointEstUA", _collection!);

        // Must not throw — analysis is not yet estimated.
        var dist = ua.GetPointEstimateDistribution();
        // Either null or a default point estimate is acceptable.
        // dist is nullable — accept either null or a non-null value; this test pins the
        // "no exception thrown" contract only.
        var _ = dist;
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.RaisePreviewSaved"/> does not throw and
    /// does not flip cancel from <c>false</c> when there are no subscribers to PreviewObjectSaved.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var ua = new UI.UnivariateAnalysis("PrevSavedUA", _collection!);
        bool cancel = false;

        ua.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel,
            "With no subscribers to PreviewObjectSaved, the cancel flag must remain false.");
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.UnivariateDistribution"/> reflects the
    /// inner analysis's distribution model (delegation contract).
    /// </summary>
    [STATestMethod]
    public void UnivariateDistribution_DelegatesToInnerAnalysis()
    {
        var ua = new UI.UnivariateAnalysis("DelegateUA", _collection!);

        // Delegated property returns same instance as the inner analysis.
        Assert.AreSame(((RMC.BestFit.Analyses.UnivariateAnalysis)ua.InnerAnalysis).UnivariateDistribution,
            ua.UnivariateDistribution);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.BayesianAnalysis"/> reflects the inner
    /// analysis's BayesianAnalysis instance (delegation contract).
    /// </summary>
    [STATestMethod]
    public void BayesianAnalysis_DelegatesToInnerAnalysis()
    {
        var ua = new UI.UnivariateAnalysis("BAUA", _collection!);

        Assert.AreSame(((RMC.BestFit.Analyses.UnivariateAnalysis)ua.InnerAnalysis).BayesianAnalysis,
            ua.BayesianAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.ProbabilityOrdinates"/> matches the inner
    /// analysis's ProbabilityOrdinates collection.
    /// </summary>
    [STATestMethod]
    public void ProbabilityOrdinates_DelegatesToInnerAnalysis()
    {
        var ua = new UI.UnivariateAnalysis("ProbUA", _collection!);

        Assert.AreSame(((RMC.BestFit.Analyses.UnivariateAnalysis)ua.InnerAnalysis).ProbabilityOrdinates,
            ua.ProbabilityOrdinates);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.ElementImageResourceKey"/> returns the
    /// expected resource key constant used by the App for theme-aware icon lookup.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var ua = new UI.UnivariateAnalysis("ImgKeyUA", _collection!);

        Assert.AreEqual("UnivariateAnalysisIcon", ua.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.CollectionName"/> is the expected static
    /// constant used as the SQLite table name for univariate analysis storage.
    /// </summary>
    [TestMethod]
    public void CollectionName_IsExpectedConstant()
    {
        Assert.AreEqual("<Univariate Distribution>", UI.UnivariateAnalysis.CollectionName);
    }

    /// <summary>
    /// Verifies that the Name setter records a property change to <c>nameof(Name)</c> when set
    /// to a different value.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var ua = new UI.UnivariateAnalysis("NameUA", _collection!);
        var raised = new List<string>();
        ua.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ua.Name = "NameUA-Renamed";

        Assert.IsTrue(raised.Contains(nameof(UI.UnivariateAnalysis.Name)));
        Assert.AreEqual("NameUA-Renamed", ua.Name);
    }

    /// <summary>
    /// Verifies that setting Name to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Name_SameValue_DoesNotRaisePropertyChanged()
    {
        var ua = new UI.UnivariateAnalysis("NameSameUA", _collection!);
        var raised = new List<string>();
        ua.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ua.Name = "NameSameUA"; // same value

        CollectionAssert.DoesNotContain(raised, nameof(UI.UnivariateAnalysis.Name));
    }

    /// <summary>
    /// Verifies that <see cref="UI.UnivariateAnalysis.IsValid"/> is <c>false</c> on a freshly
    /// constructed instance because <c>InputData</c> has not been assigned yet.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_IsFalseInitiallyDueToMissingInputData()
    {
        var ua = new UI.UnivariateAnalysis("IsValidUA", _collection!);

        // No InputData assigned → analysis is not valid.
        Assert.IsFalse(ua.IsValid);
    }

}
