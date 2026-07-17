using DatabaseManager;
using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;
using System;
using System.Data;
using System.IO;
using System.Linq;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// Unit tests for <see cref="UnivariateAnalysisCollection"/>, the project-level container of
/// univariate analysis instances (including <see cref="UnivariateAnalysis"/>,
/// <see cref="PointProcessAnalysis"/>, <see cref="MixtureAnalysis"/>, and <see cref="B17CAnalysis"/>).
/// </summary>
/// <remarks>
/// Tests focus on the collection name constant and the IElementCollection contract.
/// SQLite-dependent methods are excluded — covered by integration tests.
/// </remarks>
[TestClass]
public class UnivariateAnalysisCollectionTests
{
    /// <summary>
    /// Creates a minimal project metadata table with the current v2 schema marker.
    /// </summary>
    /// <param name="projectPath">The full path to write into the project metadata row.</param>
    /// <returns>A populated project metadata table.</returns>
    private static DataTable CreateProjectTable(string projectPath)
    {
        var table = new DataTable("Project");
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("FullFileName", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("CreationDate", typeof(string));
        table.Columns.Add("LastModified", typeof(string));
        table.Columns.Add("SoftwareVersion", typeof(string));

        string now = FrameworkInterfaces.Utilities.Tools.DateToUniversalString(new DateTime(2026, 1, 1));
        table.Rows.Add("DuplicateIndexFixture", projectPath, "", now, now, "2.0 Beta-4");
        return table;
    }

    /// <summary>
    /// Creates a minimal subtype table for analysis wrappers whose open path does not require model state.
    /// </summary>
    /// <param name="tableName">The SQLite table name to create.</param>
    /// <param name="elementName">The analysis element name stored in the row.</param>
    /// <returns>A populated subtype table containing one analysis row.</returns>
    private static DataTable CreateMinimalSubtypeTable(string tableName, string elementName)
    {
        var table = new DataTable(tableName);
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("CreationDate", typeof(string));
        table.Columns.Add("LastModified", typeof(string));
        table.Columns.Add("InputData", typeof(string));

        string now = FrameworkInterfaces.Utilities.Tools.DateToUniversalString(new DateTime(2026, 1, 1));
        table.Rows.Add(elementName, "", now, now, "");
        return table;
    }

    /// <summary>
    /// Creates a minimal project database with duplicate parent-index rows and single subtype rows.
    /// </summary>
    /// <param name="projectPath">The SQLite project path to create.</param>
    private static void CreateProjectWithDuplicateParentRows(string projectPath)
    {
        var parentTable = new DataTable("Univariate Distribution Analysis");
        parentTable.Columns.Add("Name", typeof(string));
        parentTable.Columns.Add("Type", typeof(string));

        parentTable.Rows.Add("Test", typeof(RMC.BestFit.UI.UnivariateAnalysis).ToString());
        parentTable.Rows.Add("Test", typeof(RMC.BestFit.UI.UnivariateAnalysis).ToString());
        parentTable.Rows.Add("Orphan", typeof(RMC.BestFit.UI.UnivariateAnalysis).ToString());
        parentTable.Rows.Add("LP3 - Method of Moments", typeof(B17CAnalysis).ToString());
        parentTable.Rows.Add("LP3 - Method of Moments", typeof(B17CAnalysis).ToString());

        var sqlite = new SQLiteManager(projectPath);
        sqlite.Open();
        try
        {
            sqlite.SaveDataTable(CreateProjectTable(projectPath));
            sqlite.SaveDataTable(parentTable);
            sqlite.SaveDataTable(CreateMinimalSubtypeTable(RMC.BestFit.UI.UnivariateAnalysis.CollectionName, "Test"));
        }
        finally
        {
            if (sqlite.DataBaseOpen) sqlite.Close();
        }
    }

    /// <summary>
    /// Verifies that <see cref="UnivariateAnalysisCollection.Name"/> returns the expected constant string.
    /// </summary>
    [TestMethod]
    public void Name_ReturnsExpectedConstant()
    {
        var project = BestFitProject.GetInstance();
        var collection = new UnivariateAnalysisCollection(project);

        Assert.AreEqual("Univariate Distribution Analysis", collection.Name);
    }

    /// <summary>
    /// Verifies that <see cref="UnivariateAnalysisCollection"/> implements <see cref="IElementCollection"/>.
    /// </summary>
    [TestMethod]
    public void ImplementsIElementCollection()
    {
        var project = BestFitProject.GetInstance();
        var collection = new UnivariateAnalysisCollection(project);

        Assert.IsInstanceOfType<IElementCollection>(collection);
    }

    /// <summary>
    /// Verifies that <see cref="IElementCollection.ParentProject"/> is set correctly.
    /// </summary>
    [TestMethod]
    public void ParentProject_IsSetCorrectly()
    {
        var project = BestFitProject.GetInstance();
        var collection = new UnivariateAnalysisCollection(project);

        Assert.AreEqual(project, collection.ParentProject);
    }

    /// <summary>
    /// Verifies that a freshly constructed collection is not dirty.
    /// </summary>
    [TestMethod]
    public void NewCollection_IsNotDirty()
    {
        var collection = new UnivariateAnalysisCollection(BestFitProject.GetInstance());
        Assert.IsFalse(collection.IsDirty);
    }

    /// <summary>
    /// Verifies that <see cref="UnivariateAnalysisCollection.InsertFromExternalProject"/> is a
    /// no-op (univariate-family analyses cannot be copied from external projects). The
    /// implementation always returns without modifying state, regardless of inputs.
    /// </summary>
    [TestMethod]
    public void InsertFromExternalProject_AnyType_NoThrow()
    {
        var collection = new UnivariateAnalysisCollection(BestFitProject.GetInstance());

        collection.InsertFromExternalProject(0, "UnivariateAnalysis", "Some.Path.UnivariateAnalysis", string.Empty);
        collection.InsertFromExternalProject(0, "ignored", "Some.Other.Type", string.Empty);
    }

    /// <summary>
    /// Verifies that duplicate parent-index rows are collapsed before analysis wrappers are constructed.
    /// </summary>
    [TestMethod]
    public void BuildLoadEntries_DuplicateParentRows_ReturnsOneEntryAndMarksRewrite()
    {
        string projectPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bestfit");
        SQLiteManager? sqlite = null;

        try
        {
            CreateProjectWithDuplicateParentRows(projectPath);
            sqlite = new SQLiteManager(projectPath);
            sqlite.Open();

            var dtView = sqlite.GetTableManager("Univariate Distribution Analysis");
            string[] names = Array.ConvertAll(dtView.GetColumn("Name"), value => value?.ToString() ?? string.Empty);
            string[] types = Array.ConvertAll(dtView.GetColumn("Type"), value => value?.ToString() ?? string.Empty);
            var loadEntries = UnivariateAnalysisCollection.BuildLoadEntries(
                sqlite,
                names,
                types,
                validateStoredRows: true,
                out bool needsIndexRewrite);

            Assert.AreEqual(1, loadEntries.Count,
                "Only the subtype-backed analysis should be returned; duplicate and orphan parent rows are skipped.");
            Assert.AreEqual("Test", loadEntries[0].ElementName);
            Assert.AreEqual(typeof(RMC.BestFit.UI.UnivariateAnalysis).ToString(), loadEntries[0].ElementType);
            Assert.IsTrue(needsIndexRewrite,
                "Skipping stale parent-index rows should request an index rewrite on the next save.");
        }
        finally
        {
            if (sqlite?.DataBaseOpen == true)
            {
                sqlite.Close();
            }

            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }
}
