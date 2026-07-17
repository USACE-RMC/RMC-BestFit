using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// Unit tests for <see cref="MixtureAnalysis"/>, the UI wrapper for mixture distribution analysis.
/// </summary>
/// <remarks>
/// All tests require STA thread because the constructor creates OxyPlot WPF Plot objects.
/// Tests focus on: constructor defaults, property change notifications, IsEstimated flag,
/// Distributions collection, and plot initialization. Save/Open/Delete are excluded (require SQLite).
/// </remarks>
[TestClass]
public class MixtureAnalysisTests
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
        var ma = new MixtureAnalysis("TestMA", _collection!);

        Assert.AreEqual("TestMA", ma.Name);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.NameOnDisk"/> matches the constructor name.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var ma = new MixtureAnalysis("DiskMA", _collection!);

        Assert.AreEqual("DiskMA", ma.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.IsEstimated"/> is <c>false</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var ma = new MixtureAnalysis("EstMA", _collection!);

        Assert.IsFalse(ma.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.InputData"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_InputData_IsNullInitially()
    {
        var ma = new MixtureAnalysis("InputMA", _collection!);

        Assert.IsNull(ma.InputData);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.Distributions"/> is not null and pre-populated
    /// with the default two Normal distributions.
    /// </summary>
    [STATestMethod]
    public void Constructor_Distributions_IsPrePopulated()
    {
        var ma = new MixtureAnalysis("DistMA", _collection!);

        Assert.IsNotNull(ma.Distributions);
        Assert.AreEqual(2, ma.Distributions.Count,
            "MixtureAnalysis should start with two default Normal distributions.");
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.ProbabilityOrdinates"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_ProbabilityOrdinates_IsNotNull()
    {
        var ma = new MixtureAnalysis("ProbMA", _collection!);

        Assert.IsNotNull(ma.ProbabilityOrdinates);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.FrequencyPlot"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_FrequencyPlot_IsNotNull()
    {
        var ma = new MixtureAnalysis("PlotMA", _collection!);

        Assert.IsNotNull(ma.FrequencyPlot);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.CanCopyFromExternal"/> returns <c>false</c>.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsFalse()
    {
        var ma = new MixtureAnalysis("CopyMA", _collection!);

        Assert.IsFalse(ma.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var ma = new MixtureAnalysis("PropMA", _collection!);
        var raised = new List<string>();
        ma.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ma.Description = "A description";

        Assert.IsTrue(raised.Contains(nameof(MixtureAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var ma = new MixtureAnalysis("PropMA2", _collection!);
        ma.Description = "Same";

        var raised = new List<string>();
        ma.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ma.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(MixtureAnalysis.Description));
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.CreationDate"/> and <see cref="MixtureAnalysis.LastModified"/>
    /// are within a few seconds of construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var ma = new MixtureAnalysis("DateMA", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(ma.CreationDate >= before && ma.CreationDate <= after);
        Assert.IsTrue(ma.LastModified >= before && ma.LastModified <= after);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.CancelAnalysis"/> does not throw
    /// when no analysis is running.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var ma = new MixtureAnalysis("CancelMA", _collection!);

        // Must not throw
        ma.CancelAnalysis();
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.ClearResults"/> does not throw
    /// and leaves IsEstimated as false.
    /// </summary>
    [STATestMethod]
    public void ClearResults_WhenNotEstimated_DoesNotThrow()
    {
        var ma = new MixtureAnalysis("ClearMA", _collection!);

        // Must not throw
        ma.ClearResults();

        Assert.IsFalse(ma.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.InnerAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void InnerAnalysis_IsNotNull()
    {
        var ma = new MixtureAnalysis("InnerMA", _collection!);

        Assert.IsNotNull(ma.InnerAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.AnalysisResults"/> is null until the analysis runs.
    /// </summary>
    [STATestMethod]
    public void AnalysisResults_BeforeRun_IsNull()
    {
        var ma = new MixtureAnalysis("ResMA", _collection!);

        Assert.IsNull(ma.AnalysisResults);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.BayesianAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void BayesianAnalysis_IsNotNull()
    {
        var ma = new MixtureAnalysis("BAMA", _collection!);

        Assert.IsNotNull(ma.BayesianAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.BayesianPlots"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void BayesianPlots_IsNotNull()
    {
        var ma = new MixtureAnalysis("BPMA", _collection!);

        Assert.IsNotNull(ma.BayesianPlots);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.MixtureDistribution"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void MixtureDistribution_IsNotNull()
    {
        var ma = new MixtureAnalysis("MDMA", _collection!);

        Assert.IsNotNull(ma.MixtureDistribution);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.RaisePreviewSaved"/> does not throw with no subscribers.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var ma = new MixtureAnalysis("PrevMA", _collection!);
        bool cancel = false;

        ma.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.GetMarginalModel"/> returns the underlying mixture
    /// model (so the analysis can serve as a marginal on a bivariate distribution).
    /// </summary>
    [STATestMethod]
    public void GetMarginalModel_IsNotNull()
    {
        var ma = new MixtureAnalysis("MarginalMA", _collection!);

        Assert.IsNotNull(ma.GetMarginalModel());
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.ElementImageResourceKey"/> returns the expected key.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var ma = new MixtureAnalysis("ImgKeyMA", _collection!);

        Assert.AreEqual("MixtureAnalysisIcon", ma.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies the static <see cref="MixtureAnalysis.CollectionName"/> SQLite-table-name constant.
    /// </summary>
    [TestMethod]
    public void CollectionName_IsExpectedConstant()
    {
        Assert.AreEqual("<Mixture Distribution>", MixtureAnalysis.CollectionName);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.IsValid"/> is <c>false</c> on a freshly constructed
    /// instance because <c>InputData</c> has not been assigned.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_IsFalseInitiallyDueToMissingInputData()
    {
        var ma = new MixtureAnalysis("IsValidMA", _collection!);

        Assert.IsFalse(ma.IsValid);
    }

    /// <summary>
    /// Verifies that <see cref="MixtureAnalysis.Name"/> setter raises PropertyChanged on rename.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var ma = new MixtureAnalysis("NameMA", _collection!);
        var raised = new List<string>();
        ma.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ma.Name = "NameMA-Renamed";

        Assert.IsTrue(raised.Contains(nameof(MixtureAnalysis.Name)));
        Assert.AreEqual("NameMA-Renamed", ma.Name);
    }
}
