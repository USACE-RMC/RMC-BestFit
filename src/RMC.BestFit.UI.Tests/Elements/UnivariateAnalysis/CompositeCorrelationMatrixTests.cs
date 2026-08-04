using System.Reflection;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                    CorrelationMatrix = new[,] { { 1d, 0.625d }, { 0.625d, 1d } }
                };
                original.BayesianAnalysis.PRNGSeed = 987654;

                original.Save();
                var restored = new CompositeAnalysis("MatrixPersistence", collection);
                restored.Open();

                Assert.AreEqual(0.625d, restored.CorrelationMatrix[0, 1], 0d);
                Assert.AreEqual(0.625d, restored.CorrelationMatrix[1, 0], 0d);
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
