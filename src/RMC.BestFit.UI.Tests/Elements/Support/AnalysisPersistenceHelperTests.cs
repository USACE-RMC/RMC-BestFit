using DatabaseManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;
using System;
using System.Data;
using System.IO;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for <see cref="AnalysisPersistenceHelper"/>.
/// </summary>
/// <remarks>
/// Tests focus on defensive read/write behavior for optional result columns.
/// </remarks>
[TestClass]
public class AnalysisPersistenceHelperTests
{
    /// <summary>
    /// Verifies that an empty MCMC blob is treated as absent.
    /// </summary>
    [TestMethod]
    public void TryLoadMCMCResults_EmptyBlob_ReturnsNull()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = CreateResultFixture(path, Array.Empty<byte>(), "<Results>");
            var table = sqlite.GetTableManager("Analysis");

            var results = AnalysisPersistenceHelper.TryLoadMCMCResults(table, 0, "Analysis");

            Assert.IsNull(results);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    /// <summary>
    /// Verifies that invalid result XML is skipped instead of throwing.
    /// </summary>
    [TestMethod]
    public void TryLoadAnalysisResults_InvalidXml_ReturnsNull()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = CreateResultFixture(path, Array.Empty<byte>(), "<bad");
            var table = sqlite.GetTableManager("Analysis");

            var results = AnalysisPersistenceHelper.TryLoadAnalysisResults(table, "AnalysisResults", 0, "Analysis");

            Assert.IsNull(results);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    /// <summary>
    /// Verifies that null result values serialize to empty persisted cells.
    /// </summary>
    [TestMethod]
    public void SerializeNullResults_ReturnsEmptyValues()
    {
        CollectionAssert.AreEqual(Array.Empty<byte>(), AnalysisPersistenceHelper.SerializeMCMCResults(null));
        Assert.AreEqual(string.Empty, AnalysisPersistenceHelper.SerializeAnalysisResults(null));
    }

    /// <summary>
    /// Creates a temporary file name for SQLite-backed helper tests.
    /// </summary>
    /// <returns>A unique SQLite project path in the temporary directory.</returns>
    private static string NewTempFile()
    {
        return Path.Combine(Path.GetTempPath(), $"rmcbf-analysis-helper-{Guid.NewGuid():N}.bestfit");
    }

    /// <summary>
    /// Deletes a temporary SQLite project file when it exists.
    /// </summary>
    /// <param name="path">The path to delete.</param>
    private static void DeleteTempFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Creates a result-column fixture for safe deserialization tests.
    /// </summary>
    /// <param name="path">The SQLite path to create.</param>
    /// <param name="mcmcResults">The MCMC blob to store.</param>
    /// <param name="analysisResults">The analysis result XML to store.</param>
    /// <returns>An open SQLite manager.</returns>
    private static SQLiteManager CreateResultFixture(string path, byte[] mcmcResults, string analysisResults)
    {
        var table = new DataTable("Analysis");
        table.Columns.Add("MCMCResults", typeof(byte[]));
        table.Columns.Add("AnalysisResults", typeof(string));
        table.Rows.Add(mcmcResults, analysisResults);

        var sqlite = new SQLiteManager(path);
        sqlite.Open();
        sqlite.SaveDataTable(table);
        return sqlite;
    }
}
