using System.Data;
using System.IO;
using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.InputData;

/// <summary>
/// SQLite persistence tests for <see cref="InputDataCollection"/>.
/// </summary>
/// <remarks>
/// These tests use the real <see cref="BestFitProject"/> singleton only as the parent
/// project path provider, and keep that mutation isolated because project state is global.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class InputDataCollectionPersistenceTests
{
    /// <summary>
    /// Verifies that duplicate single-table rows are opened once, preserve element data,
    /// and are compacted on the next collection save.
    /// </summary>
    [STATestMethod]
    public void OpenDuplicateRowsThenSave_CompactsTableAndPreservesElementData()
    {
        using var scope = new ProjectFileScope();
        CreateProjectTable(scope.ProjectPath);
        UI.InputData? seed = null;
        UI.InputData? opened = null;

        try
        {
            var seedCollection = new InputDataCollection(scope.Project);
            seed = new UI.InputData("DuplicateInput", seedCollection)
            {
                Description = "Seeded input data",
                UnitLabel = "Flow (cfs)",
                IndexLabel = "Water Year"
            };
            seed.DataFrame.ExactSeries.Add(new ExactData(2001, 1234.5));
            seedCollection.Add(seed);

            DuplicateFirstRow(scope.ProjectPath, seedCollection.Name);
            Assert.AreEqual(2, CountRows(scope.ProjectPath, seedCollection.Name), "Fixture should contain duplicate rows.");

            var openedCollection = new InputDataCollection(scope.Project);
            openedCollection.Open();
            var openedElements = openedCollection.Cast<IElement>().ToList();

            Assert.AreEqual(1, openedElements.Count, "Duplicate rows should load as one collection element.");
            opened = (UI.InputData)openedElements[0];
            Assert.AreEqual("DuplicateInput", opened.Name);
            Assert.AreEqual("Seeded input data", opened.Description);
            Assert.AreEqual("Flow (cfs)", opened.UnitLabel);
            Assert.AreEqual("Water Year", opened.IndexLabel);
            Assert.AreEqual(1, opened.DataFrame.ExactSeries.Count);
            Assert.AreEqual(1234.5, opened.DataFrame.ExactSeries[0].Value, 1e-12);
            Assert.IsTrue(openedCollection.IsDirty, "Skipping a duplicate row should mark the collection for compaction.");

            openedCollection.Save();

            Assert.IsFalse(openedCollection.IsDirty);
            Assert.AreEqual(1, CountRows(scope.ProjectPath, seedCollection.Name), "Collection save should rewrite one row per element.");
        }
        finally
        {
            if (seed != null) Messenger.GetInstance().Clear(seed);
            if (opened != null) Messenger.GetInstance().Clear(opened);
        }
    }

    /// <summary>
    /// Verifies that custom plot axis titles survive the real save/open path.
    /// </summary>
    [STATestMethod]
    public void SaveOpen_PreservesCustomAxisTitles()
    {
        using var scope = new ProjectFileScope();
        CreateProjectTable(scope.ProjectPath);
        UI.InputData? seed = null;
        UI.InputData? opened = null;

        try
        {
            var seedCollection = new InputDataCollection(scope.Project);
            seed = new UI.InputData("AxisPersistenceInput", seedCollection)
            {
                UnitLabel = "Flow (cfs)",
                IndexLabel = "Water Year"
            };
            GetAxis(seed.ChronologyPlot, "Yaxis").Title = "2-Day Volume";
            GetAxis(seed.ChronologyPlot, "Xaxis").Title = "Period of Record";
            seedCollection.Add(seed);

            var openedCollection = new InputDataCollection(scope.Project);
            openedCollection.Open();
            opened = openedCollection.Cast<UI.InputData>().Single();

            Assert.AreEqual("Flow (cfs)", opened.UnitLabel);
            Assert.AreEqual("Water Year", opened.IndexLabel);
            Assert.AreEqual("2-Day Volume", GetAxis(opened.ChronologyPlot, "Yaxis").Title);
            Assert.AreEqual("Period of Record", GetAxis(opened.ChronologyPlot, "Xaxis").Title);
        }
        finally
        {
            if (seed != null) Messenger.GetInstance().Clear(seed);
            if (opened != null) Messenger.GetInstance().Clear(opened);
        }
    }

    /// <summary>
    /// Creates the minimal project metadata table needed by element open paths.
    /// </summary>
    /// <param name="projectPath">The SQLite project path to write into the metadata row.</param>
    private static void CreateProjectTable(string projectPath)
    {
        var table = new DataTable("Project");
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("FullFileName", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("CreationDate", typeof(string));
        table.Columns.Add("LastModified", typeof(string));
        table.Columns.Add("SoftwareVersion", typeof(string));

        string now = FrameworkInterfaces.Utilities.Tools.DateToUniversalString(new DateTime(2026, 1, 1));
        table.Rows.Add("InputDataPersistenceFixture", projectPath, "Fixture", now, now, "2.0 Beta-4");

        using var sqlite = new SQLiteManager(projectPath);
        sqlite.Open();
        sqlite.SaveDataTable(table);
    }

    /// <summary>
    /// Duplicates the first row of a table to simulate stale single-table collection persistence.
    /// </summary>
    /// <param name="projectPath">The SQLite project path to modify.</param>
    /// <param name="tableName">The table containing the row to duplicate.</param>
    private static void DuplicateFirstRow(string projectPath, string tableName)
    {
        using var sqlite = new SQLiteManager(projectPath);
        sqlite.Open();
        var table = sqlite.GetTableManager(tableName);
        object[] firstRow = table.GetRow(0);

        table.AddRow();
        int newRowIndex = table.NumberOfRows - 1;
        for (int i = 0; i < table.ColumnNames.Length; i++)
        {
            table.EditCell(newRowIndex, table.ColumnNames[i], firstRow[i]);
        }

        table.ApplyEdits();
    }

    /// <summary>
    /// Counts rows in a SQLite table.
    /// </summary>
    /// <param name="projectPath">The SQLite project path to inspect.</param>
    /// <param name="tableName">The table name to count.</param>
    /// <returns>The number of rows in the table.</returns>
    private static int CountRows(string projectPath, string tableName)
    {
        using var sqlite = new SQLiteManager(projectPath);
        sqlite.Open();
        return sqlite.GetTableManager(tableName).NumberOfRows;
    }

    /// <summary>
    /// Gets an axis by key from a plot.
    /// </summary>
    /// <param name="plot">The plot to inspect.</param>
    /// <param name="key">The axis key.</param>
    /// <returns>The matching axis.</returns>
    private static OxyPlot.Wpf.Axis GetAxis(OxyPlot.Wpf.Plot plot, string key)
    {
        return plot.Axes.First(axis => axis.Key == key);
    }

    /// <summary>
    /// Owns temporary project path mutation on the singleton project.
    /// </summary>
    private sealed class ProjectFileScope : IDisposable
    {
        private readonly string _previousFullFileName;

        /// <summary>
        /// Initializes a new temporary project-file scope.
        /// </summary>
        public ProjectFileScope()
        {
            Project = BestFitProject.GetInstance();
            _previousFullFileName = Project.FullFileName;
            ProjectPath = Path.Combine(Path.GetTempPath(), $"rmcbf-inputdata-persistence-{Guid.NewGuid():N}.bestfit");
            Project.FullFileName = ProjectPath;
            Messenger.GetInstance().Clear(Project);
        }

        /// <summary>
        /// Gets the singleton project instance under test.
        /// </summary>
        public BestFitProject Project { get; }

        /// <summary>
        /// Gets the temporary SQLite project path.
        /// </summary>
        public string ProjectPath { get; }

        /// <summary>
        /// Restores singleton project state and removes the temporary project file.
        /// </summary>
        public void Dispose()
        {
            Project.FullFileName = _previousFullFileName;
            ForceSetIsDirty(Project, false);
            Messenger.GetInstance().Clear(Project);
            if (File.Exists(ProjectPath))
            {
                File.Delete(ProjectPath);
            }
        }

        /// <summary>
        /// Invokes the protected dirty-state setter used to restore singleton project state.
        /// </summary>
        /// <param name="target">The saveable object whose dirty flag should be changed.</param>
        /// <param name="value">The dirty-state value to assign.</param>
        private static void ForceSetIsDirty(object target, bool value)
        {
            for (Type? type = target.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(
                    "SetIsDirty",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(bool) },
                    modifiers: null);
                if (method != null)
                {
                    method.Invoke(target, new object[] { value });
                    return;
                }
            }

            throw new InvalidOperationException($"SetIsDirty(bool) was not found on {target.GetType().FullName}.");
        }
    }
}
