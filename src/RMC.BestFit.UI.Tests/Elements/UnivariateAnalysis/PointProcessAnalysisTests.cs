using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using UiInputData = RMC.BestFit.UI.InputData;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// Unit tests for <see cref="PointProcessAnalysis"/>, the UI wrapper for POT analysis.
/// </summary>
/// <remarks>
/// All tests require STA thread because the constructor creates OxyPlot WPF Plot objects.
/// Tests focus on: constructor defaults, property change notifications, IsEstimated flag,
/// InputData, and plot initialization. Save/Open/Delete are excluded (require SQLite).
/// </remarks>
[TestClass]
public class PointProcessAnalysisTests
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
    /// Creates a small exact-data frame with one exact value per index.
    /// </summary>
    /// <param name="startIndex">The first index in the series.</param>
    /// <returns>A data frame with ten exact observations.</returns>
    private static DataFrame CreateExactDataFrame(int startIndex = 2000)
    {
        var df = new DataFrame();
        for (int i = 0; i < 10; i++)
        {
            df.ExactSeries.Add(new ExactData(startIndex + i, 1500.0 + 100.0 * i));
        }

        return df;
    }

    /// <summary>
    /// Creates an input-data element backed by the supplied exact-data method.
    /// </summary>
    /// <param name="name">The input-data element name.</param>
    /// <param name="method">The exact-data entry method.</param>
    /// <param name="threshold">The user-entered POT threshold.</param>
    /// <returns>A configured input-data element.</returns>
    private static UiInputData CreateInputData(string name, UiInputData.ExactDataEntryType method, double threshold)
    {
        var inputCollection = new InputDataCollection(BestFitProject.GetInstance());
        var inputData = new UiInputData(name, inputCollection)
        {
            ExactDataMethod = method,
            Threshold = threshold,
            DataFrame = CreateExactDataFrame()
        };

        return inputData;
    }

    /// <summary>
    /// Verifies that the constructor stores the provided name.
    /// </summary>
    [STATestMethod]
    public void Constructor_StoresName()
    {
        var ppa = new PointProcessAnalysis("TestPPA", _collection!);

        Assert.AreEqual("TestPPA", ppa.Name);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.NameOnDisk"/> matches the constructor name.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var ppa = new PointProcessAnalysis("DiskPPA", _collection!);

        Assert.AreEqual("DiskPPA", ppa.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.IsEstimated"/> is <c>false</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var ppa = new PointProcessAnalysis("EstPPA", _collection!);

        Assert.IsFalse(ppa.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.InputData"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_InputData_IsNullInitially()
    {
        var ppa = new PointProcessAnalysis("InputPPA", _collection!);

        Assert.IsNull(ppa.InputData);
    }

    /// <summary>
    /// Verifies that POT input data supplies the user-entered threshold to model defaults.
    /// </summary>
    [STATestMethod]
    public void InputData_PeaksOverThresholdDefaults_UsesInputThreshold()
    {
        var inputData = CreateInputData(
            "POTDefaultsInput",
            UiInputData.ExactDataEntryType.PeaksOverThresholdSeries,
            1000.0);
        var ppa = new PointProcessAnalysis("POTDefaultsPPA", _collection!);

        ppa.InputData = inputData;

        Assert.AreEqual(1000.0, ppa.PointProcess.Threshold, 1e-10);
        Assert.AreEqual(inputData.DataFrame.ExactSeries.IndexSpan(), ppa.PointProcess.TotalYears, 1e-10);
    }

    /// <summary>
    /// Verifies that manual point-process inputs survive input-data changes while defaults are disabled.
    /// </summary>
    [STATestMethod]
    public void InputDataChanged_UseDefaultsFalse_PreservesManualThresholdAndYears()
    {
        var inputData = CreateInputData(
            "POTManualInput",
            UiInputData.ExactDataEntryType.PeaksOverThresholdSeries,
            1000.0);
        var ppa = new PointProcessAnalysis("POTManualPPA", _collection!);
        ppa.PointProcess.UseDefaults = false;
        ppa.PointProcess.Threshold = 4321.0;
        ppa.PointProcess.TotalYears = 42.0;

        ppa.InputData = inputData;
        inputData.Threshold = 900.0;

        Assert.AreEqual(4321.0, ppa.PointProcess.Threshold, 1e-10);
        Assert.AreEqual(42.0, ppa.PointProcess.TotalYears, 1e-10);
    }

    /// <summary>
    /// Verifies that re-enabling defaults restores the POT input threshold, not the exact-data minimum.
    /// </summary>
    [STATestMethod]
    public void UseDefaults_Reenabled_RestoresPeaksOverThresholdInputThreshold()
    {
        var inputData = CreateInputData(
            "POTToggleInput",
            UiInputData.ExactDataEntryType.PeaksOverThresholdSeries,
            1000.0);
        var ppa = new PointProcessAnalysis("POTTogglePPA", _collection!);
        ppa.InputData = inputData;
        ppa.PointProcess.UseDefaults = false;
        ppa.PointProcess.Threshold = 4321.0;

        ppa.PointProcess.UseDefaults = true;

        Assert.AreEqual(1000.0, ppa.PointProcess.Threshold, 1e-10);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.ProbabilityOrdinates"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_ProbabilityOrdinates_IsNotNull()
    {
        var ppa = new PointProcessAnalysis("ProbPPA", _collection!);

        Assert.IsNotNull(ppa.ProbabilityOrdinates);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.FrequencyPlot"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_FrequencyPlot_IsNotNull()
    {
        var ppa = new PointProcessAnalysis("PlotPPA", _collection!);

        Assert.IsNotNull(ppa.FrequencyPlot);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.BayesianAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_BayesianAnalysis_IsNotNull()
    {
        var ppa = new PointProcessAnalysis("BaPPA", _collection!);

        Assert.IsNotNull(ppa.BayesianAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.CanCopyFromExternal"/> returns <c>false</c>.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsFalse()
    {
        var ppa = new PointProcessAnalysis("CopyPPA", _collection!);

        Assert.IsFalse(ppa.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var ppa = new PointProcessAnalysis("PropPPA", _collection!);
        var raised = new List<string>();
        ppa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ppa.Description = "A description";

        Assert.IsTrue(raised.Contains(nameof(PointProcessAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var ppa = new PointProcessAnalysis("PropPPA2", _collection!);
        ppa.Description = "Same";

        var raised = new List<string>();
        ppa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ppa.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(PointProcessAnalysis.Description));
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.CreationDate"/> and <see cref="PointProcessAnalysis.LastModified"/>
    /// are within a few seconds of construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var ppa = new PointProcessAnalysis("DatePPA", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(ppa.CreationDate >= before && ppa.CreationDate <= after);
        Assert.IsTrue(ppa.LastModified >= before && ppa.LastModified <= after);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.CancelAnalysis"/> does not throw
    /// when no analysis is running.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var ppa = new PointProcessAnalysis("CancelPPA", _collection!);

        // Must not throw
        ppa.CancelAnalysis();
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.ClearResults"/> does not throw
    /// and leaves IsEstimated as false.
    /// </summary>
    [STATestMethod]
    public void ClearResults_WhenNotEstimated_DoesNotThrow()
    {
        var ppa = new PointProcessAnalysis("ClearPPA", _collection!);

        // Must not throw
        ppa.ClearResults();

        Assert.IsFalse(ppa.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.InnerAnalysis"/> is not null.
    /// </summary>
    [STATestMethod]
    public void InnerAnalysis_IsNotNull()
    {
        var ppa = new PointProcessAnalysis("InnerPPA", _collection!);
        Assert.IsNotNull(ppa.InnerAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.AnalysisResults"/> is null until the
    /// analysis runs.
    /// </summary>
    [STATestMethod]
    public void AnalysisResults_BeforeRun_IsNull()
    {
        var ppa = new PointProcessAnalysis("ResPPA", _collection!);
        Assert.IsNull(ppa.AnalysisResults);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.BayesianAnalysis"/> is not null.
    /// </summary>
    [STATestMethod]
    public void BayesianAnalysis_IsNotNull()
    {
        var ppa = new PointProcessAnalysis("BAPPA", _collection!);
        Assert.IsNotNull(ppa.BayesianAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.BayesianPlots"/> is not null.
    /// </summary>
    [STATestMethod]
    public void BayesianPlots_IsNotNull()
    {
        var ppa = new PointProcessAnalysis("BPPPA", _collection!);
        Assert.IsNotNull(ppa.BayesianPlots);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.PointProcess"/> is not null.
    /// </summary>
    [STATestMethod]
    public void PointProcess_IsNotNull()
    {
        var ppa = new PointProcessAnalysis("PPPPA", _collection!);
        Assert.IsNotNull(ppa.PointProcess);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.RaisePreviewSaved"/> does not throw or
    /// flip cancel when there are no PreviewObjectSaved subscribers.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var ppa = new PointProcessAnalysis("PrevPPA", _collection!);
        bool cancel = false;

        ppa.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.GetMarginalModel"/> returns a valid
    /// marginal model.
    /// </summary>
    [STATestMethod]
    public void GetMarginalModel_IsNotNull()
    {
        var ppa = new PointProcessAnalysis("MarginalPPA", _collection!);
        Assert.IsNotNull(ppa.GetMarginalModel());
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.ElementImageResourceKey"/> returns the
    /// expected resource key.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var ppa = new PointProcessAnalysis("ImgKeyPPA", _collection!);
        Assert.AreEqual("PointProcessAnalysisIcon", ppa.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies the static <see cref="PointProcessAnalysis.CollectionName"/> constant.
    /// </summary>
    [TestMethod]
    public void CollectionName_IsExpectedConstant()
    {
        Assert.AreEqual("<Point Process>", PointProcessAnalysis.CollectionName);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.IsValid"/> is <c>false</c> on a freshly
    /// constructed instance (no InputData yet).
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_IsFalseInitiallyDueToMissingInputData()
    {
        var ppa = new PointProcessAnalysis("IsValidPPA", _collection!);
        Assert.IsFalse(ppa.IsValid);
    }

    /// <summary>
    /// Verifies that <see cref="PointProcessAnalysis.Name"/> setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var ppa = new PointProcessAnalysis("NamePPA", _collection!);
        var raised = new List<string>();
        ppa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ppa.Name = "NamePPA-Renamed";

        Assert.IsTrue(raised.Contains(nameof(PointProcessAnalysis.Name)));
        Assert.AreEqual("NamePPA-Renamed", ppa.Name);
    }
}
