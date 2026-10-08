using System.Reflection;
using System.IO;
using System.Data;
using DatabaseManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data.Statistics;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// Tests correlation-matrix ownership and persistence on the UI composite wrapper.
/// </summary>
[TestClass]
public class CompositeCorrelationMatrixTests
{
    private static readonly object ProjectPathLock = new object();
    private static UnivariateAnalysisCollection? _collection;

    /// <summary>
    /// Creates a shared collection backed by the singleton project.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _collection = new UnivariateAnalysisCollection(BestFitProject.GetInstance());
    }

    /// <summary>
    /// Verifies the UI property delegates defensive matrix ownership to the core analysis.
    /// </summary>
    [STATestMethod]
    public void CorrelationMatrix_PropertyOwnsDefensiveCopies()
    {
        var analysis = new CompositeAnalysis("MatrixProperty", _collection!);
        var source = new[,] { { 1d, 0.45d }, { 0.45d, 1d } };

        analysis.CorrelationMatrix = source;
        source[0, 1] = 0.8d;
        double[,] exposed = analysis.CorrelationMatrix;
        exposed[1, 0] = 0.7d;

        Assert.AreEqual(0.45d, analysis.CorrelationMatrix[0, 1], 0d);
        Assert.AreEqual(0.45d, analysis.CorrelationMatrix[1, 0], 0d);
    }

    /// <summary>
    /// Verifies copying a UI composite preserves an independent matrix value.
    /// </summary>
    [STATestMethod]
    public void Copy_PreservesIndependentCorrelationMatrix()
    {
        var original = new CompositeAnalysis("MatrixCopy", _collection!)
        {
            CorrelationMatrix = new[,] { { 1d, 0.35d }, { 0.35d, 1d } }
        };

        var copy = (CompositeAnalysis)original.Copy("MatrixCopyResult");
        double[,] copiedMatrix = copy.CorrelationMatrix;
        copiedMatrix[0, 1] = 0.9d;

        Assert.AreEqual(0.35d, copy.CorrelationMatrix[0, 1], 0d);
        Assert.AreEqual(0.35d, original.CorrelationMatrix[0, 1], 0d);
    }

    /// <summary>
    /// Verifies the SQLite schema appends the correlation payload without disturbing prior columns.
    /// </summary>
    [STATestMethod]
    public void RequiredColumns_AppendsCorrelationMatrixColumn()
    {
        var property = typeof(CompositeAnalysis).GetProperty(
            "RequiredColumns",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var columns = (Dictionary<string, Type>)property.GetValue(null)!;

        Assert.IsTrue(columns.ContainsKey(nameof(CompositeAnalysis.CorrelationMatrix)));
        Assert.AreEqual(nameof(CompositeAnalysis.CorrelationMatrix), columns.Keys.Last());
    }

    /// <summary>
    /// Verifies a saved UI composite restores its correlation matrix and posterior-resampling
    /// seed through SQLite.
    /// </summary>
    [STATestMethod]
    [DoNotParallelize]
    public void SaveAndOpen_RoundTripCorrelationMatrix()
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-CompositeMatrix-{Guid.NewGuid():N}.db");
        lock (ProjectPathLock)
        {
            BestFitProject project = BestFitProject.GetInstance();
            string previousPath = project.FullFileName;
            try
            {
                project.FullFileName = path;
                var collection = new UnivariateAnalysisCollection(project);
                var original = new CompositeAnalysis("MatrixPersistence", collection)
                {
                    Dependency = Probability.DependencyType.CorrelationMatrix,
                    CorrelationMatrix = new[,] { { 1d, 0.625d }, { 0.625d, 1d } }
                };
                original.BayesianAnalysis.PRNGSeed = 987654;

                original.Save();
                var restored = new CompositeAnalysis("MatrixPersistence", collection);
                restored.Open();

                Assert.AreEqual(0.625d, restored.CorrelationMatrix[0, 1], 0d);
                Assert.AreEqual(0.625d, restored.CorrelationMatrix[1, 0], 0d);
                Assert.AreEqual(Probability.DependencyType.CorrelationMatrix, restored.Dependency);
                Assert.AreEqual(987654, restored.BayesianAnalysis.PRNGSeed);
            }
            finally
            {
                project.FullFileName = previousPath;
                DeleteDatabaseFiles(path);
            }
        }
    }

    /// <summary>
    /// Verifies optional legacy SQLite matrix storage does not discard composite configuration,
    /// child references, weights, Bayesian settings, or probability ordinates.
    /// </summary>
    /// <param name="includeMatrixColumn">Whether the legacy schema contains the optional matrix column.</param>
    /// <param name="matrixXml">The optional matrix cell value.</param>
    [STATestMethod]
    [DataRow(false, null)]
    [DataRow(true, null)]
    [DataRow(true, "")]
    [DataRow(true, "  \t\r\n  ")]
    [DataRow(true, "<CorrelationMatrix />")]
    [DataRow(true, "<CorrelationMatrix><Correlation_Row>0|0</Correlation_Row><Correlation_Row>0|0</Correlation_Row></CorrelationMatrix>")]
    [DoNotParallelize]
    public void Open_LegacyOptionalMatrixStorage_PreservesCompleteCompositeConfiguration(
        bool includeMatrixColumn,
        string? matrixXml)
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-LegacyComposite-{Guid.NewGuid():N}.db");
        lock (ProjectPathLock)
        {
            BestFitProject project = BestFitProject.GetInstance();
            string previousPath = project.FullFileName;
            try
            {
                project.FullFileName = path;
                var collection = new UnivariateAnalysisCollection(project);
                var first = new RMC.BestFit.UI.UnivariateAnalysis("LegacyChildOne", collection);
                var second = new RMC.BestFit.UI.UnivariateAnalysis("LegacyChildTwo", collection);
                collection.Add(first);
                collection.Add(second);
                WriteLegacyCompositeRow(path, includeMatrixColumn, matrixXml);

                var restored = new CompositeAnalysis("LegacyComposite", collection);
                var sqlite = new SQLiteManager(path);
                try
                {
                    restored.Open(sqlite);
                }
                finally
                {
                    if (sqlite.DataBaseOpen) sqlite.Close();
                }

                Assert.IsNull(restored.CorrelationMatrix);
                Assert.AreEqual(Probability.DependencyType.CorrelationMatrix, restored.Dependency);
                Assert.AreEqual(RMC.BestFit.Analyses.CompositeType.CompetingRisks, restored.CompositeDistributionType);
                Assert.IsFalse(restored.IsMaximum);
                Assert.AreEqual(2, restored.Analyses.Count);
                Assert.AreSame(first, restored.Analyses[0].UnivariateAnalysis);
                Assert.AreSame(second, restored.Analyses[1].UnivariateAnalysis);
                Assert.AreEqual(0.25d, restored.Analyses[0].Weight, 0d);
                Assert.AreEqual(0.75d, restored.Analyses[1].Weight, 0d);
                Assert.AreEqual(987654, restored.BayesianAnalysis.PRNGSeed);
                CollectionAssert.AreEqual(new[] { 0.5d, 0.9d, 0.99d }, restored.ProbabilityOrdinates.ToArray());
            }
            finally
            {
                project.FullFileName = previousPath;
                DeleteDatabaseFiles(path);
            }
        }
    }

    /// <summary>
    /// Writes a literal legacy composite row with an optional correlation-matrix column.
    /// </summary>
    /// <param name="path">The temporary database path.</param>
    /// <param name="includeMatrixColumn">Whether to include the optional matrix column.</param>
    /// <param name="matrixXml">The optional matrix cell value.</param>
    private static void WriteLegacyCompositeRow(string path, bool includeMatrixColumn, string? matrixXml)
    {
        var table = new DataTable(CompositeAnalysis.CollectionName);
        foreach (string column in new[]
        {
            "Name", "CompositeDistributionType", "ModelAverageMethod", "Dependency",
            "IsMaximum", "Analyses", "BayesianAnalysis", "ProbabilityOrdinates"
        })
        {
            table.Columns.Add(column, typeof(string));
        }
        if (includeMatrixColumn)
            table.Columns.Add("CorrelationMatrix", typeof(string));
        DataRow row = table.NewRow();
        row["Name"] = "LegacyComposite";
        row["CompositeDistributionType"] = "CompetingRisks";
        row["ModelAverageMethod"] = "AIC";
        row["Dependency"] = "CorrelationMatrix";
        row["IsMaximum"] = "False";
        row["Analyses"] = "<Analyses><WeightedUnivariateAnalysis UnivariateAnalysis=\"LegacyChildOne\" Weight=\"0.25\" /><WeightedUnivariateAnalysis UnivariateAnalysis=\"LegacyChildTwo\" Weight=\"0.75\" /></Analyses>";
        row["BayesianAnalysis"] = "<BayesianAnalysis PRNGSeed=\"987654\" />";
        row["ProbabilityOrdinates"] = "0.5|0.9|0.99";
        if (includeMatrixColumn)
            row["CorrelationMatrix"] = matrixXml is null ? DBNull.Value : matrixXml;
        table.Rows.Add(row);
        var sqlite = new SQLiteManager(path);
        sqlite.Open();
        try
        {
            sqlite.SaveDataTable(table);
        }
        finally
        {
            sqlite.Close();
        }
    }

    /// <summary>
    /// Deletes the temporary SQLite database and optional sidecar files created by a test.
    /// </summary>
    /// <param name="path">The primary database path.</param>
    private static void DeleteDatabaseFiles(string path)
    {
        System.Data.SQLite.SQLiteConnection.ClearAllPools();
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            if (File.Exists(candidate)) File.Delete(candidate);
        }
    }
}
