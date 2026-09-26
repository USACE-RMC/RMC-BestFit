using System.Data;
using System.IO;
using System.Xml.Linq;
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
    /// Repairs saved positions for every explicit observation type and persists only the correction.
    /// </summary>
    /// <param name="kind">The explicit observation type used below the threshold.</param>
    [STATestMethod]
    [DataRow("exact")]
    [DataRow("uncertain")]
    [DataRow("interval")]
    public void OpenAffectedPositions_SaveReopenPreservesDataAndPlots(string kind)
    {
        using var scope = new ProjectFileScope();
        CreateProjectTable(scope.ProjectPath);
        var collection = new InputDataCollection(scope.Project);
        var seed = new UI.InputData("RepairInput", collection);
        UI.InputData? opened = null;
        UI.InputData? reopened = null;
        try
        {
            PopulateRepairFixture(seed.DataFrame, kind);
            var observations = ExplicitObservations(seed.DataFrame);
            observations[0].PlottingPosition = 0.6;
            observations[1].PlottingPosition = 0.5;
            observations[2].PlottingPosition = 0.1;
            var saved = seed.DataFrame.ToXElement();
            CollectionAssert.AreEqual(new[] { 0.6, 0.5, 0.1 },
                ExplicitObservations(new DataFrame(saved)).Select(row => row.PlottingPosition).ToArray(),
                "Direct model deserialization must preserve the supplied positions exactly.");
            GetAxis(seed.FrequencyPlot, "Yaxis").Title = "Custom flood magnitude";
            GetAxis(seed.ChronologyPlot, "Xaxis").Title = "Custom record";
            collection.Add(seed);

            opened = new UI.InputData(seed.Name, new InputDataCollection(scope.Project), true);
            Assert.IsTrue(opened.IsDirty, "A changed saved position must survive constructor dirty-state initialization.");
            var repaired = ExplicitObservations(opened.DataFrame);
            Assert.AreEqual(7d / 15d, repaired[0].PlottingPosition, 1e-14);
            Assert.AreEqual(11d / 15d, repaired[1].PlottingPosition, 1e-14);
            Assert.AreEqual(0.1, repaired[2].PlottingPosition, 1e-14);
            Assert.AreEqual(SourceXml(saved), SourceXml(opened.DataFrame.ToXElement()));
            Assert.AreEqual("Custom flood magnitude", GetAxis(opened.FrequencyPlot, "Yaxis").Title);
            Assert.AreEqual("Custom record", GetAxis(opened.ChronologyPlot, "Xaxis").Title);
            opened.Save();
            Assert.IsFalse(opened.IsDirty);

            reopened = new UI.InputData(seed.Name, new InputDataCollection(scope.Project), true);
            Assert.IsFalse(reopened.IsDirty, "Already repaired positions must not mark the reopened element dirty.");
            Assert.AreEqual(opened.DataFrame.ToXElement().ToString(), reopened.DataFrame.ToXElement().ToString());
            Assert.AreEqual("Custom flood magnitude", GetAxis(reopened.FrequencyPlot, "Yaxis").Title);
            Assert.AreEqual("Custom record", GetAxis(reopened.ChronologyPlot, "Xaxis").Title);
        }
        finally
        {
            Messenger.GetInstance().Clear(seed);
            if (opened != null) Messenger.GetInstance().Clear(opened);
            if (reopened != null) Messenger.GetInstance().Clear(reopened);
        }
    }

    /// <summary>
    /// Leaves unaffected supplied positions and invalid affected frames loadable without dirtying them.
    /// </summary>
    /// <param name="invalid">Whether to retain a below-threshold observation with an invalid plotting parameter.</param>
    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OpenUnchangedOrInvalidFrame_PreservesSuppliedPositions(bool invalid)
    {
        using var scope = new ProjectFileScope();
        CreateProjectTable(scope.ProjectPath);
        var collection = new InputDataCollection(scope.Project);
        var seed = new UI.InputData("PreservedInput", collection);
        UI.InputData? opened = null;
        try
        {
            PopulateRepairFixture(seed.DataFrame, "exact");
            if (!invalid) seed.DataFrame.ThresholdSeries.Clear();
            var saved = seed.DataFrame.ToXElement();
            foreach (var attribute in saved.Descendants().Attributes("PlottingPosition")) attribute.Value = "0.321";
            if (invalid) saved.SetAttributeValue("PlottingParameter", "1");
            collection.Add(seed);
            using (var sqlite = new SQLiteManager(scope.ProjectPath))
            {
                sqlite.Open();
                var table = sqlite.GetTableManager(collection.Name);
                table.EditCell(0, "DataFrame", saved.ToString());
                table.ApplyEdits();
            }
            opened = new UI.InputData(seed.Name, new InputDataCollection(scope.Project), true);
            Assert.IsFalse(opened.IsDirty);
            CollectionAssert.AreEqual(new[] { 0.321, 0.321, 0.321 },
                ExplicitObservations(opened.DataFrame).Select(row => row.PlottingPosition).ToArray());
            Assert.AreEqual(!invalid, opened.DataFrame.Validate().IsValid);
            if (invalid) Assert.IsFalse(opened.IsValid);
        }
        finally
        {
            Messenger.GetInstance().Clear(seed);
            if (opened != null) Messenger.GetInstance().Clear(opened);
        }
    }

    /// <summary>
    /// Preserves the existing version-one migration dirty flag even without an affected frame.
    /// </summary>
    [STATestMethod]
    public void OpenVersionOne_PreservesMigrationDirtyFlag()
    {
        using var scope = new ProjectFileScope();
        CreateProjectTable(scope.ProjectPath);
        var collection = new InputDataCollection(scope.Project);
        var seed = new UI.InputData("LegacyInput", collection);
        UI.InputData? opened = null;
        try
        {
            collection.Add(seed);
            using (var sqlite = new SQLiteManager(scope.ProjectPath))
            {
                sqlite.Open();
                var table = sqlite.GetTableManager("Project");
                table.EditCell(0, "SoftwareVersion", "1.0");
                table.ApplyEdits();
            }
            opened = new UI.InputData(seed.Name, new InputDataCollection(scope.Project), true);
            Assert.IsTrue(opened.IsDirty);
        }
        finally
        {
            Messenger.GetInstance().Clear(seed);
            if (opened != null) Messenger.GetInstance().Clear(opened);
        }
    }

    /// <summary>
    /// Verifies that opening a version 1.0 project whose stored low-outlier threshold the current
    /// guard rejects (it would censor more than half the record) warns instead of silently
    /// clearing the low outliers, and leaves no exact value flagged.
    /// </summary>
    [STATestMethod]
    public void OpenVersionOne_RejectedLowOutlierThreshold_WarnsAndLeavesNoLowOutliers()
    {
        using var scope = new ProjectFileScope();
        CreateProjectTable(scope.ProjectPath);
        var collection = new InputDataCollection(scope.Project);
        var seed = new UI.InputData("V1RejectedThresholdInput", collection);
        UI.InputData? opened = null;
        try
        {
            collection.Add(seed);
            double[] values = Enumerable.Range(1, 10).Select(v => (double)v).ToArray();
            WriteVersion1LowOutlierFixture(scope.ProjectPath, collection.Name, seed.Name,
                useMultipleGrubbsBeckTest: false, lowOutlierThreshold: 100, exactValues: values);
            SetSoftwareVersion(scope.ProjectPath, "1.0");
            string expectedReason = CaptureRejectedThresholdReason(values, 100);

            opened = new UI.InputData(seed.Name, new InputDataCollection(scope.Project), true);

            BasicMessageItem? warning = MessengerMessage(opened, "ID-WNG-021");
            Assert.IsNotNull(warning, "A version 1.0 project's rejected low-outlier threshold must warn instead of silently clearing.");
            Assert.AreEqual(
                $"The low-outlier settings saved with this version 1.0 project could not be applied ({expectedReason}), so no observations are flagged as low outliers. Review the low-outlier threshold or the Multiple Grubbs-Beck test setting before running an analysis on this input data.",
                warning!.Description);
            Assert.IsFalse(opened.DataFrame.ExactSeries.Cast<ExactData>().Any(row => row.IsLowOutlier),
                "The rejected threshold must leave no exact value flagged as a low outlier.");
        }
        finally
        {
            Messenger.GetInstance().Clear(seed);
            if (opened != null) Messenger.GetInstance().Clear(opened);
        }
    }

    /// <summary>
    /// Verifies that opening a version 1.0 project whose stored low-outlier threshold the current
    /// guard accepts applies the outliers normally and does not raise the migration warning.
    /// </summary>
    [STATestMethod]
    public void OpenVersionOne_AcceptedLowOutlierThreshold_AppliesWithoutWarning()
    {
        using var scope = new ProjectFileScope();
        CreateProjectTable(scope.ProjectPath);
        var collection = new InputDataCollection(scope.Project);
        var seed = new UI.InputData("V1AcceptedThresholdInput", collection);
        UI.InputData? opened = null;
        try
        {
            collection.Add(seed);
            double[] values = Enumerable.Range(1, 10).Select(v => (double)v).ToArray();
            WriteVersion1LowOutlierFixture(scope.ProjectPath, collection.Name, seed.Name,
                useMultipleGrubbsBeckTest: false, lowOutlierThreshold: 5, exactValues: values);
            SetSoftwareVersion(scope.ProjectPath, "1.0");

            opened = new UI.InputData(seed.Name, new InputDataCollection(scope.Project), true);

            Assert.IsNull(MessengerMessage(opened, "ID-WNG-021"), "A stored threshold the guard accepts must not warn.");
            double[] flagged = opened.DataFrame.ExactSeries.Cast<ExactData>()
                .Where(row => row.IsLowOutlier).Select(row => row.Value).OrderBy(v => v).ToArray();
            CollectionAssert.AreEqual(new[] { 1d, 2d, 3d, 4d }, flagged,
                "Values below the accepted threshold must still be flagged as low outliers.");
        }
        finally
        {
            Messenger.GetInstance().Clear(seed);
            if (opened != null) Messenger.GetInstance().Clear(opened);
        }
    }

    /// <summary>
    /// Verifies that re-opening the same element after its stored threshold becomes acceptable
    /// clears the prior migration warning instead of leaving it stale or duplicating it.
    /// </summary>
    [STATestMethod]
    public void OpenVersionOne_ReopenWithAcceptedThreshold_ClearsPriorWarningWithoutDuplicating()
    {
        using var scope = new ProjectFileScope();
        CreateProjectTable(scope.ProjectPath);
        var collection = new InputDataCollection(scope.Project);
        var seed = new UI.InputData("V1ReopenInput", collection);
        try
        {
            collection.Add(seed);
            double[] values = Enumerable.Range(1, 10).Select(v => (double)v).ToArray();
            WriteVersion1LowOutlierFixture(scope.ProjectPath, collection.Name, seed.Name,
                useMultipleGrubbsBeckTest: false, lowOutlierThreshold: 100, exactValues: values);
            SetSoftwareVersion(scope.ProjectPath, "1.0");

            seed.Open();
            Assert.IsNotNull(MessengerMessage(seed, "ID-WNG-021"), "Precondition: the rejected threshold must warn on the first open.");

            WriteVersion1LowOutlierFixture(scope.ProjectPath, collection.Name, seed.Name,
                useMultipleGrubbsBeckTest: false, lowOutlierThreshold: 5, exactValues: values);

            seed.Open();

            var matches = Messenger.GetInstance().AllMessageItems()
                .Where(m => ReferenceEquals(m.Source, seed) && m.Code == "ID-WNG-021").ToArray();
            Assert.AreEqual(0, matches.Length, "The stale warning must be removed, not left behind or duplicated, once the settings apply cleanly.");
        }
        finally
        {
            Messenger.GetInstance().Clear(seed);
        }
    }

    /// <summary>
    /// Populates a five-year record containing two observed values below its perception threshold.
    /// </summary>
    /// <param name="frame">The frame to populate.</param>
    /// <param name="kind">The type of below-threshold observations.</param>
    private static void PopulateRepairFixture(DataFrame frame, string kind)
    {
        if (kind == "uncertain")
        {
            frame.UncertainSeries.Add(new UncertainData(0, new Numerics.Distributions.Normal(80, 2)));
            frame.UncertainSeries.Add(new UncertainData(3, new Numerics.Distributions.Normal(50, 2)));
        }
        else if (kind == "interval")
        {
            frame.IntervalSeries.Add(new IntervalData(0, 79, 80, 81));
            frame.IntervalSeries.Add(new IntervalData(3, 49, 50, 51));
        }
        else
        {
            frame.ExactSeries.Add(new ExactData(0, 80, isLowOutlier: true));
            frame.ExactSeries.Add(new ExactData(3, 50));
        }
        frame.ExactSeries.Add(new ExactData(4, 200));
        frame.ThresholdSeries.Add(new ThresholdData(0, 4, 100));
        frame.CalculatePlottingPositions();
    }

    /// <summary>
    /// Gets explicit observations in index order independent of their series type.
    /// </summary>
    /// <param name="frame">The frame to inspect.</param>
    /// <returns>The ordered observations.</returns>
    private static Models.Data[] ExplicitObservations(DataFrame frame)
    {
        return frame.ExactSeries.Cast<Models.Data>().Concat(frame.UncertainSeries)
            .Concat(frame.IntervalSeries).OrderBy(row => row.Index).ToArray();
    }

    /// <summary>
    /// Removes only derived positions to compare all persisted source fields.
    /// </summary>
    /// <param name="source">The serialized data frame.</param>
    /// <returns>The serialized source fields with positions omitted.</returns>
    private static string SourceXml(System.Xml.Linq.XElement source)
    {
        var copy = new System.Xml.Linq.XElement(source);
        foreach (var attribute in copy.Descendants().Attributes("PlottingPosition").ToList()) attribute.Remove();
        return copy.ToString();
    }

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
    /// Writes version 1.0 systematic-data and low-outlier settings columns directly into an
    /// existing InputData collection row, mirroring the legacy schema that
    /// <see cref="UI.InputData"/>'s version 1.0 open path reads (<c>SystematicDataList</c>,
    /// <c>LowOutlierThresholdValue</c>, <c>UseMultipleGrubbsBeckTest</c>).
    /// </summary>
    /// <param name="projectPath">The SQLite project path to modify.</param>
    /// <param name="tableName">The InputData collection table name.</param>
    /// <param name="elementName">The name of the row to update.</param>
    /// <param name="useMultipleGrubbsBeckTest">The stored Multiple Grubbs-Beck Test toggle.</param>
    /// <param name="lowOutlierThreshold">The stored low-outlier threshold value.</param>
    /// <param name="exactValues">The systematic (exact) record values, one per sequential year index.</param>
    private static void WriteVersion1LowOutlierFixture(string projectPath, string tableName, string elementName,
        bool useMultipleGrubbsBeckTest, double lowOutlierThreshold, double[] exactValues)
    {
        var systematicDataList = new XElement("SystematicDataList",
            exactValues.Select((value, index) => new XElement("Row",
                new XAttribute("Year", index),
                new XAttribute("Value", value),
                new XAttribute("PlottingPosition", (index + 1d) / (exactValues.Length + 1d)),
                new XAttribute("IsLowOutlier", false))));

        using var sqlite = new SQLiteManager(projectPath);
        sqlite.Open();
        var table = sqlite.GetTableManager(tableName);
        if (!table.ColumnNames.Contains("SystematicDataList")) table.AddColumn("SystematicDataList", typeof(string));
        if (!table.ColumnNames.Contains("LowOutlierThresholdValue")) table.AddColumn("LowOutlierThresholdValue", typeof(double));

        int rowIndex = table.SearchColumn(0, table.NumberOfRows - 1, "Name", elementName, true, true);
        table.EditCell(rowIndex, "SystematicDataList", systematicDataList.ToString());
        table.EditCell(rowIndex, "LowOutlierThresholdValue", lowOutlierThreshold);
        table.EditCell(rowIndex, nameof(UI.InputData.UseMultipleGrubbsBeckTest), useMultipleGrubbsBeckTest);
        table.ApplyEdits();
    }

    /// <summary>
    /// Sets the project's stored software version, used to route element opens through the
    /// version 1.0 migration path.
    /// </summary>
    /// <param name="projectPath">The SQLite project path to modify.</param>
    /// <param name="version">The software version string to store.</param>
    private static void SetSoftwareVersion(string projectPath, string version)
    {
        using var sqlite = new SQLiteManager(projectPath);
        sqlite.Open();
        var table = sqlite.GetTableManager("Project");
        table.EditCell(0, "SoftwareVersion", version);
        table.ApplyEdits();
    }

    /// <summary>
    /// Computes the exact reason text the production low-outlier threshold guard throws for a
    /// rejected threshold, so the warning-text assertion does not need to guess the runtime's
    /// exact <see cref="ArgumentException.Message"/> formatting.
    /// </summary>
    /// <param name="values">The exact-series values used by the fixture under test.</param>
    /// <param name="threshold">The rejected low-outlier threshold value.</param>
    /// <returns>The caught exception's message with any trailing period removed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the supplied threshold is not
    /// actually rejected by the current guard, which would make the calling test meaningless.</exception>
    private static string CaptureRejectedThresholdReason(double[] values, double threshold)
    {
        var probe = new DataFrame();
        for (int i = 0; i < values.Length; i++) probe.ExactSeries.Add(new ExactData(i, values[i]));
        probe.LowOutlierThreshold = threshold;
        try
        {
            probe.SetLowOutliersFromThreshold();
            throw new InvalidOperationException("Fixture threshold must be rejected by the current guard for this test to be meaningful.");
        }
        catch (ArgumentException ex)
        {
            return ex.Message.TrimEnd('.');
        }
    }

    /// <summary>
    /// Returns the active messenger item for the supplied source and code.
    /// </summary>
    /// <param name="source">The expected owner of the message.</param>
    /// <param name="code">The stable message code to find.</param>
    /// <returns>The matching message item, or <c>null</c> when no matching message is active.</returns>
    private static BasicMessageItem? MessengerMessage(object source, string code)
    {
        return Messenger.GetInstance().AllMessageItems()
            .OfType<BasicMessageItem>()
            .FirstOrDefault(m => ReferenceEquals(m.Source, source) && m.Code == code);
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
