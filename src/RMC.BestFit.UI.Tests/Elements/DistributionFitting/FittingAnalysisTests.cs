using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.DistributionFitting;

/// <summary>
/// Unit tests for <see cref="FittingAnalysis"/>, the UI wrapper for distribution fitting.
/// </summary>
/// <remarks>
/// All tests that construct a <see cref="FittingAnalysis"/> require STA thread because the
/// constructor creates OxyPlot WPF Plot objects.  Tests focus on: constructor defaults,
/// property change notifications, IsEstimated flag, FittedDistributions list, and
/// DistributionList content. Save/Open/Delete are excluded (require SQLite project file).
/// </remarks>
[TestClass]
public class FittingAnalysisTests
{
    // Shared collection for all tests. The collection is constructed once; each test
    // creates its own FittingAnalysis to avoid cross-test contamination.
    private static FittingAnalysisCollection? _collection;

    /// <summary>
    /// Creates a shared collection backed by the singleton BestFitProject.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _collection = new FittingAnalysisCollection(BestFitProject.GetInstance());
    }

    /// <summary>
    /// Verifies that the constructor succeeds and produces an element with the expected name.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void Constructor_StoresName()
    {
        var fa = new FittingAnalysis("Test Fitting", _collection!);

        Assert.AreEqual("Test Fitting", fa.Name);
    }

    /// <summary>
    /// Verifies that NameOnDisk matches the name provided at construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var fa = new FittingAnalysis("DiskTest", _collection!);

        Assert.AreEqual("DiskTest", fa.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.IsEstimated"/> is <c>false</c> after construction
    /// (no analysis has been run yet).
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var fa = new FittingAnalysis("EstTest", _collection!);

        Assert.IsFalse(fa.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.InputData"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_InputData_IsNullInitially()
    {
        var fa = new FittingAnalysis("InputTest", _collection!);

        Assert.IsNull(fa.InputData);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.FittedDistributions"/> is not null after construction.
    /// </summary>
    /// <remarks>
    /// The inner model analysis pre-populates the list with unfitted distribution stubs
    /// (one per distribution type), so the count may be non-zero before <see cref="FittingAnalysis.RunAsync"/>
    /// is called. This test only guards that the list is accessible.
    /// </remarks>
    [STATestMethod]
    public void FittedDistributions_IsNotNull_AfterConstruction()
    {
        var fa = new FittingAnalysis("FitList", _collection!);

        Assert.IsNotNull(fa.FittedDistributions);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.DistributionList"/> contains all 15 required
    /// univariate distributions.
    /// </summary>
    [STATestMethod]
    public void DistributionList_ContainsAllFifteenDistributions()
    {
        var fa = new FittingAnalysis("DistList", _collection!);

        var list = fa.DistributionList;

        Assert.IsNotNull(list);
        Assert.AreEqual(15, list.Count, "DistributionList must contain all 15 supported distributions.");
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.DistributionList"/> includes a
    /// <see cref="Normal"/> distribution.
    /// </summary>
    [STATestMethod]
    public void DistributionList_ContainsNormal()
    {
        var fa = new FittingAnalysis("DistNormal", _collection!);
        var list = fa.DistributionList;

        Assert.IsTrue(list.Any(d => d is Normal), "DistributionList must contain a Normal distribution.");
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.DistributionList"/> includes a
    /// <see cref="GeneralizedExtremeValue"/> distribution.
    /// </summary>
    [STATestMethod]
    public void DistributionList_ContainsGEV()
    {
        var fa = new FittingAnalysis("DistGEV", _collection!);
        var list = fa.DistributionList;

        Assert.IsTrue(list.Any(d => d is GeneralizedExtremeValue),
            "DistributionList must contain a GeneralizedExtremeValue distribution.");
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.DistributionList"/> includes a
    /// <see cref="LogPearsonTypeIII"/> distribution.
    /// </summary>
    [STATestMethod]
    public void DistributionList_ContainsLogPearsonTypeIII()
    {
        var fa = new FittingAnalysis("DistLP3", _collection!);
        var list = fa.DistributionList;

        Assert.IsTrue(list.Any(d => d is LogPearsonTypeIII),
            "DistributionList must contain a LogPearsonTypeIII distribution.");
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.ProbabilityOrdinates"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void ProbabilityOrdinates_IsNotNull_AfterConstruction()
    {
        var fa = new FittingAnalysis("ProbOrd", _collection!);

        Assert.IsNotNull(fa.ProbabilityOrdinates);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.FrequencyPlot"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void FrequencyPlot_IsNotNull_AfterConstruction()
    {
        var fa = new FittingAnalysis("FreqPlot", _collection!);

        Assert.IsNotNull(fa.FrequencyPlot);
    }

    /// <summary>
    /// Verifies that all 5 plots are initialized by the constructor.
    /// </summary>
    [STATestMethod]
    public void Constructor_AllFivePlots_AreNotNull()
    {
        var fa = new FittingAnalysis("AllPlots", _collection!);

        Assert.IsNotNull(fa.FrequencyPlot, "FrequencyPlot must not be null.");
        Assert.IsNotNull(fa.PDFPlot, "PDFPlot must not be null.");
        Assert.IsNotNull(fa.CDFPlot, "CDFPlot must not be null.");
        Assert.IsNotNull(fa.PPPlot, "PPPlot must not be null.");
        Assert.IsNotNull(fa.QQPlot, "QQPlot must not be null.");
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.CanCopyFromExternal"/> returns <c>false</c>.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsFalse()
    {
        var fa = new FittingAnalysis("CopyExt", _collection!);

        Assert.IsFalse(fa.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var fa = new FittingAnalysis("PropTest", _collection!);
        var raised = new List<string>();
        fa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        fa.Description = "New Description";

        Assert.IsTrue(raised.Contains(nameof(FittingAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that the Description setter does NOT raise PropertyChanged when value is unchanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var fa = new FittingAnalysis("PropTest2", _collection!);
        fa.Description = "Same";

        var raised = new List<string>();
        fa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        fa.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(FittingAnalysis.Description));
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.ClearResults"/> can be called without throwing
    /// when no analysis has been performed.
    /// </summary>
    [STATestMethod]
    public void ClearResults_WhenNotEstimated_DoesNotThrow()
    {
        var fa = new FittingAnalysis("ClearTest", _collection!);

        // Must not throw
        fa.ClearResults();

        Assert.IsFalse(fa.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.CancelAnalysis"/> can be called without throwing
    /// when no analysis is running.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var fa = new FittingAnalysis("CancelTest", _collection!);

        // Must not throw
        fa.CancelAnalysis();
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.CreationDate"/> and <see cref="FittingAnalysis.LastModified"/>
    /// are recent timestamps (within a few seconds of construction).
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var fa = new FittingAnalysis("DateTest", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(fa.CreationDate >= before && fa.CreationDate <= after);
        Assert.IsTrue(fa.LastModified >= before && fa.LastModified <= after);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.ElementImageResourceKey"/> returns the
    /// expected resource key for the App's theme-aware icon lookup.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var fa = new FittingAnalysis("ImgKeyFA", _collection!);

        Assert.AreEqual("FittingAnalysisIcon", fa.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.RaisePreviewSaved"/> does not throw or flip cancel
    /// when there are no PreviewObjectSaved subscribers.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var fa = new FittingAnalysis("PrevFA", _collection!);
        bool cancel = false;

        fa.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.IsValid"/> is <c>false</c> on a freshly constructed
    /// instance because <c>InputData</c> has not been assigned yet.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_IsFalseInitiallyDueToMissingInputData()
    {
        var fa = new FittingAnalysis("IsValidFA", _collection!);

        Assert.IsFalse(fa.IsValid);
    }

    /// <summary>
    /// Verifies that <see cref="FittingAnalysis.Name"/> setter raises PropertyChanged on rename.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var fa = new FittingAnalysis("NameFA", _collection!);
        var raised = new List<string>();
        fa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        fa.Name = "NameFA-Renamed";

        Assert.IsTrue(raised.Contains(nameof(FittingAnalysis.Name)));
        Assert.AreEqual("NameFA-Renamed", fa.Name);
    }

}
