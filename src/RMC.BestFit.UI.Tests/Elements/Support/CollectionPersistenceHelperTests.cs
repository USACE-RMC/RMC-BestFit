using DatabaseManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for <see cref="CollectionPersistenceHelper"/>.
/// </summary>
/// <remarks>
/// These tests exercise the repair policy used by UI collections when stale SQLite rows are
/// encountered during project open.
/// </remarks>
[TestClass]
public class CollectionPersistenceHelperTests
{
    /// <summary>
    /// Verifies that duplicate and blank rows in a single-table collection are skipped.
    /// </summary>
    [TestMethod]
    public void BuildSingleTableLoadEntries_DuplicateAndBlankRows_ReturnsFirstNamesAndMarksRewrite()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = CreateSingleTableFixture(path);
            var table = sqlite.GetTableManager("Input Data");

            List<string> entries = CollectionPersistenceHelper.BuildSingleTableLoadEntries(table, out bool needsRewrite);

            CollectionAssert.AreEqual(new[] { "A", "B" }, entries);
            Assert.IsTrue(needsRewrite);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    /// <summary>
    /// Verifies that malformed typed parent rows are skipped while valid first rows are retained.
    /// </summary>
    [TestMethod]
    public void BuildTypedLoadEntries_MalformedRows_ReturnsValidRowsAndMarksRewrite()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = CreateTypedFixture(path);
            var tables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "KnownAnalysis", "Known Analysis" }
            };
            string[] names = { "A", "A", "Orphan", "Unknown", "", "NoType", "LegacyDefault" };
            string[] types =
            {
                "Test.Namespace.KnownAnalysis",
                "Test.Namespace.KnownAnalysis",
                "Test.Namespace.KnownAnalysis",
                "Test.Namespace.UnknownAnalysis",
                "Test.Namespace.KnownAnalysis"
            };

            List<(string ElementName, string ElementType)> entries =
                CollectionPersistenceHelper.BuildTypedLoadEntries(
                    sqlite,
                    names,
                    types,
                    tables,
                    validateStoredRows: true,
                    defaultElementType: "KnownAnalysis",
                    out bool needsRewrite);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("A", entries[0].ElementName);
            Assert.AreEqual("LegacyDefault", entries[1].ElementName);
            Assert.IsTrue(needsRewrite);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    /// <summary>
    /// Creates a temporary file name for SQLite-backed helper tests.
    /// </summary>
    /// <returns>A unique SQLite project path in the temporary directory.</returns>
    private static string NewTempFile()
    {
        return Path.Combine(Path.GetTempPath(), $"rmcbf-helper-{Guid.NewGuid():N}.bestfit");
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
    /// Creates a single-table collection fixture with duplicate and blank rows.
    /// </summary>
    /// <param name="path">The SQLite path to create.</param>
    /// <returns>An open SQLite manager.</returns>
    private static SQLiteManager CreateSingleTableFixture(string path)
    {
        var table = new DataTable("Input Data");
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add("A");
        table.Rows.Add("A");
        table.Rows.Add("");
        table.Rows.Add("B");

        var sqlite = new SQLiteManager(path);
        sqlite.Open();
        sqlite.SaveDataTable(table);
        return sqlite;
    }

    /// <summary>
    /// Creates a typed subtype fixture with rows for valid and legacy-default entries.
    /// </summary>
    /// <param name="path">The SQLite path to create.</param>
    /// <returns>An open SQLite manager.</returns>
    private static SQLiteManager CreateTypedFixture(string path)
    {
        var table = new DataTable("Known Analysis");
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add("A");
        table.Rows.Add("LegacyDefault");

        var sqlite = new SQLiteManager(path);
        sqlite.Open();
        sqlite.SaveDataTable(table);
        return sqlite;
    }
}
