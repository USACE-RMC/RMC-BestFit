using DatabaseManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;
using System;
using System.Data;
using System.IO;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// Unit tests for <see cref="B17CAnalysis"/>, the UI wrapper for Bulletin 17C flood frequency analysis.
/// </summary>
/// <remarks>
/// All tests require STA thread because the constructor creates OxyPlot WPF Plot objects.
/// Tests focus on: constructor defaults, property change notifications, IsEstimated flag,
/// InputData, ProbabilityOrdinates, plot initialization, and focused SQLite persistence
/// regressions that can run without computational estimation.
/// </remarks>
[TestClass]
public class B17CAnalysisTests
{
    private static UnivariateAnalysisCollection? _collection;
    private static readonly object ProjectPathLock = new();

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
        var b17 = new B17CAnalysis("TestB17", _collection!);

        Assert.AreEqual("TestB17", b17.Name);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.NameOnDisk"/> matches the constructor name.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var b17 = new B17CAnalysis("DiskB17", _collection!);

        Assert.AreEqual("DiskB17", b17.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.IsEstimated"/> is <c>false</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var b17 = new B17CAnalysis("EstB17", _collection!);

        Assert.IsFalse(b17.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.InputData"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_InputData_IsNullInitially()
    {
        var b17 = new B17CAnalysis("InputB17", _collection!);

        Assert.IsNull(b17.InputData);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.ProbabilityOrdinates"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_ProbabilityOrdinates_IsNotNull()
    {
        var b17 = new B17CAnalysis("ProbB17", _collection!);

        Assert.IsNotNull(b17.ProbabilityOrdinates);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.FrequencyPlot"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_FrequencyPlot_IsNotNull()
    {
        var b17 = new B17CAnalysis("PlotB17", _collection!);

        Assert.IsNotNull(b17.FrequencyPlot);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.CanCopyFromExternal"/> returns <c>false</c>.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsFalse()
    {
        var b17 = new B17CAnalysis("CopyB17", _collection!);

        Assert.IsFalse(b17.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var b17 = new B17CAnalysis("PropB17", _collection!);
        var raised = new List<string>();
        b17.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        b17.Description = "A description";

        Assert.IsTrue(raised.Contains(nameof(B17CAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var b17 = new B17CAnalysis("PropB172", _collection!);
        b17.Description = "Same";

        var raised = new List<string>();
        b17.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        b17.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(B17CAnalysis.Description));
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.CreationDate"/> and <see cref="B17CAnalysis.LastModified"/>
    /// are within a few seconds of construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var b17 = new B17CAnalysis("DateB17", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(b17.CreationDate >= before && b17.CreationDate <= after);
        Assert.IsTrue(b17.LastModified >= before && b17.LastModified <= after);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.CancelAnalysis"/> does not throw
    /// when no analysis is running.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var b17 = new B17CAnalysis("CancelB17", _collection!);

        // Must not throw
        b17.CancelAnalysis();
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.ClearResults"/> does not throw
    /// and leaves IsEstimated as false.
    /// </summary>
    [STATestMethod]
    public void ClearResults_WhenNotEstimated_DoesNotThrow()
    {
        var b17 = new B17CAnalysis("ClearB17", _collection!);

        // Must not throw
        b17.ClearResults();

        Assert.IsFalse(b17.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.InnerAnalysis"/> is the model-layer analysis
    /// instance backing this UI wrapper.
    /// </summary>
    [STATestMethod]
    public void InnerAnalysis_IsNotNull()
    {
        var b17 = new B17CAnalysis("InnerB17", _collection!);

        Assert.IsNotNull(b17.InnerAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.AnalysisResults"/> is null until the analysis runs.
    /// </summary>
    [STATestMethod]
    public void AnalysisResults_BeforeRun_IsNull()
    {
        var b17 = new B17CAnalysis("ResultsB17", _collection!);

        Assert.IsNull(b17.AnalysisResults);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.BayesianAnalysis"/> reflects the inner analysis.
    /// </summary>
    [STATestMethod]
    public void BayesianAnalysis_IsNotNull()
    {
        var b17 = new B17CAnalysis("BAB17", _collection!);

        Assert.IsNotNull(b17.BayesianAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.BayesianPlots"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void BayesianPlots_IsNotNull()
    {
        var b17 = new B17CAnalysis("BPB17", _collection!);

        Assert.IsNotNull(b17.BayesianPlots);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.RaisePreviewSaved"/> does not throw and does not
    /// flip cancel from <c>false</c> when there are no subscribers to PreviewObjectSaved.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var b17 = new B17CAnalysis("PrevB17", _collection!);
        bool cancel = false;

        b17.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.GetMarginalModel"/> returns the underlying
    /// distribution (so the analysis can serve as a marginal on a bivariate distribution).
    /// </summary>
    [STATestMethod]
    public void GetMarginalModel_IsNotNull()
    {
        var b17 = new B17CAnalysis("MarginalB17", _collection!);

        Assert.IsNotNull(b17.GetMarginalModel());
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.ElementImageResourceKey"/> returns the expected key.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var b17 = new B17CAnalysis("ImgKeyB17", _collection!);

        Assert.AreEqual("B17AnalysisIcon", b17.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies the static <see cref="B17CAnalysis.CollectionName"/> SQLite-table-name constant.
    /// </summary>
    [TestMethod]
    public void CollectionName_IsExpectedConstant()
    {
        Assert.AreEqual("<Bulletin 17C>", B17CAnalysis.CollectionName);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.IsValid"/> is <c>false</c> on a freshly constructed
    /// instance because <c>InputData</c> has not been assigned yet.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_IsFalseInitiallyDueToMissingInputData()
    {
        var b17 = new B17CAnalysis("IsValidB17", _collection!);

        Assert.IsFalse(b17.IsValid);
    }

    /// <summary>
    /// Verifies that the Name setter records a property change to <c>nameof(Name)</c> when set
    /// to a different value.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var b17 = new B17CAnalysis("NameB17", _collection!);
        var raised = new List<string>();
        b17.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        b17.Name = "NameB17-Renamed";

        Assert.IsTrue(raised.Contains(nameof(B17CAnalysis.Name)));
        Assert.AreEqual("NameB17-Renamed", b17.Name);
    }

    /// <summary>
    /// Verifies that <see cref="B17CAnalysis.Open(SQLiteManager)"/> restores the cached
    /// GMM report text from the Bulletin 17C SQLite table.
    /// </summary>
    [STATestMethod]
    public void Open_LoadsCachedGMMReport()
    {
        string tempPath = NewTempProjectPath();
        const string analysisName = "ReportB17";
        const string report = "BULLETIN 17C ESTIMATION REPORT\ncached report text";

        try
        {
            using (var sqlite = CreateB17CReportFixture(tempPath, analysisName, report))
            {
                var analysis = new B17CAnalysis(analysisName, _collection!);

                analysis.Open(sqlite);

                Assert.AreEqual(report, analysis.GMMReport);
            }
        }
        finally
        {
            DeleteTempProject(tempPath);
        }
    }

    /// <summary>
    /// Verifies that saving after opening a cached report does not overwrite the report with
    /// an empty generated value when the restored GMM has no transient optimizer.
    /// </summary>
    [STATestMethod]
    public void Save_AfterOpen_PreservesCachedGMMReport_WhenLiveGenerationUnavailable()
    {
        string tempPath = NewTempProjectPath();
        const string analysisName = "PersistReportB17";
        const string report = "BULLETIN 17C ESTIMATION REPORT\nmust survive save";

        lock (ProjectPathLock)
        {
            var project = BestFitProject.GetInstance();
            string previousFullFileName = project.FullFileName;

            try
            {
                var analysis = new B17CAnalysis(analysisName, _collection!);
                using (var sqlite = CreateB17CReportFixture(tempPath, analysisName, report))
                {
                    analysis.Open(sqlite);
                    if (sqlite.DataBaseOpen)
                    {
                        sqlite.Close();
                    }
                }

                project.FullFileName = tempPath;
                analysis.Save();

                Assert.AreEqual(report, ReadB17CReport(tempPath, analysisName));
            }
            finally
            {
                project.FullFileName = previousFullFileName;
                DeleteTempProject(tempPath);
            }
        }
    }

    /// <summary>
    /// Verifies that clearing B17C results clears the cached GMM report and notifies bindings.
    /// </summary>
    [STATestMethod]
    public void ClearResults_ClearsCachedGMMReport()
    {
        string tempPath = NewTempProjectPath();
        const string analysisName = "ClearReportB17";
        const string report = "BULLETIN 17C ESTIMATION REPORT\nstale text";

        try
        {
            using (var sqlite = CreateB17CReportFixture(tempPath, analysisName, report))
            {
                var analysis = new B17CAnalysis(analysisName, _collection!);
                analysis.Open(sqlite);
                var raised = new List<string>();
                analysis.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

                analysis.ClearResults();

                Assert.AreEqual(string.Empty, analysis.GMMReport);
                CollectionAssert.Contains(raised, nameof(B17CAnalysis.GMMReport));
            }
        }
        finally
        {
            DeleteTempProject(tempPath);
        }
    }

    /// <summary>
    /// Creates a unique temporary project path for SQLite-backed persistence tests.
    /// </summary>
    /// <returns>A project path under the process temporary directory.</returns>
    private static string NewTempProjectPath()
    {
        return Path.Combine(Path.GetTempPath(), $"rmcbf-b17c-report-{Guid.NewGuid():N}.bestfit");
    }

    /// <summary>
    /// Deletes a temporary project file if it exists.
    /// </summary>
    /// <param name="path">The temporary project path to remove.</param>
    private static void DeleteTempProject(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Creates a minimal Bulletin 17C table fixture containing a cached GMM report.
    /// </summary>
    /// <param name="path">The SQLite project path to create.</param>
    /// <param name="analysisName">The analysis name stored in the fixture row.</param>
    /// <param name="report">The cached report text stored in the fixture row.</param>
    /// <returns>An open SQLite manager for the created fixture.</returns>
    private static SQLiteManager CreateB17CReportFixture(string path, string analysisName, string report)
    {
        var table = new DataTable(B17CAnalysis.CollectionName);
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("GMMReport", typeof(string));
        table.Rows.Add(analysisName, report);

        var sqlite = new SQLiteManager(path);
        sqlite.Open();
        sqlite.SaveDataTable(table);
        return sqlite;
    }

    /// <summary>
    /// Reads the cached GMM report value for a Bulletin 17C analysis from a SQLite project file.
    /// </summary>
    /// <param name="path">The SQLite project path to inspect.</param>
    /// <param name="analysisName">The analysis row name to read.</param>
    /// <returns>The stored GMM report text.</returns>
    private static string ReadB17CReport(string path, string analysisName)
    {
        using var sqlite = new SQLiteManager(path);
        sqlite.Open();
        var table = sqlite.GetTableManager(B17CAnalysis.CollectionName);
        int rowIndex = table.SearchColumn(0, table.NumberOfRows - 1, "Name", analysisName, true, true);
        Assert.IsTrue(rowIndex >= 0, "Expected the Bulletin 17C analysis row to exist.");
        return table.GetCell("GMMReport", rowIndex)?.ToString() ?? string.Empty;
    }
}
