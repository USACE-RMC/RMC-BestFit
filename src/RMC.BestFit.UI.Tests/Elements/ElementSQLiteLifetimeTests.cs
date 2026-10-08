using System.Data;
using System.IO;
using System.Reflection;
using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements;

/// <summary>Verifies database ownership at every persisted element boundary.</summary>
/// <remarks>Fixtures use private projects, real temporary SQLite files, and no estimators.</remarks>
[TestClass]
[DoNotParallelize]
public class ElementSQLiteLifetimeTests
{
    /// <summary>Lists the twelve independently implemented element persistence paths.</summary>
    /// <returns>Element names used by the fixture factory.</returns>
    public static IEnumerable<object[]> ElementCases()
    {
        foreach (string kind in new[] { "InputData", "TimeSeriesElement", "FittingAnalysis", "UnivariateAnalysis",
                     "B17CAnalysis", "MixtureAnalysis", "PointProcessAnalysis", "CompositeAnalysis",
                     "BivariateAnalysis", "CoincidentFrequencyAnalysis", "RatingCurveAnalysis", "TimeSeriesAnalysis" })
            yield return new object[] { kind };
    }

    /// <summary>Combines the element paths with caller ownership and undo state.</summary>
    /// <returns>Element name, initial connection state, and initial undo state.</returns>
    public static IEnumerable<object[]> OwnershipCases()
    {
        foreach (object[] row in ElementCases())
            foreach (bool initiallyOpen in new[] { false, true })
                foreach (bool undoEnabled in new[] { false, true })
                    yield return new object[] { row[0], initiallyOpen, undoEnabled };
    }

    /// <summary>Lists persisted payloads whose existing deserializers propagate parse errors.</summary>
    /// <returns>Element names and direct versus external-copy load paths.</returns>
    public static IEnumerable<object[]> MalformedPayloadCases()
    {
        foreach (string kind in new[] { "InputData", "FittingAnalysis", "UnivariateAnalysis", "B17CAnalysis",
                     "MixtureAnalysis", "PointProcessAnalysis", "CompositeAnalysis", "CoincidentFrequencyAnalysis" })
            foreach (bool externalCopy in new[] { false, true })
                yield return new object[] { kind, externalCopy };
    }

    /// <summary>Combines real malformed payloads with caller ownership and undo state.</summary>
    /// <returns>Element name, initial connection state, and initial undo state.</returns>
    public static IEnumerable<object[]> MalformedOwnershipCases()
    {
        foreach (object[] row in MalformedPayloadCases().Where(row => !(bool)row[1]))
            foreach (bool initiallyOpen in new[] { false, true })
                foreach (bool undoEnabled in new[] { false, true })
                    yield return new object[] { row[0], initiallyOpen, undoEnabled };
    }

    /// <summary>Lists tolerated malformed payloads with caller ownership and undo state.</summary>
    /// <returns>Element name, initial connection state, and initial undo state.</returns>
    public static IEnumerable<object[]> ToleratedPayloadCases()
    {
        foreach (string kind in new[] { "TimeSeriesElement", "BivariateAnalysis", "RatingCurveAnalysis", "TimeSeriesAnalysis" })
            foreach (bool initiallyOpen in new[] { false, true })
                foreach (bool undoEnabled in new[] { false, true })
                    yield return new object[] { kind, initiallyOpen, undoEnabled };
    }

    /// <summary>Ensures the file-handle assertion detects this provider's active connection.</summary>
    [STATestMethod]
    public void ExclusiveFileProbe_DetectsOpenConnectionAndSucceedsAfterClose()
    {
        using var fixture = new ElementFixture("InputData");
        using (var sqlite = new SQLiteManager(fixture.Path))
        {
            sqlite.Open();
            Assert.ThrowsException<IOException>(() => AssertExclusiveAccess(fixture.Path));
            sqlite.Close();
            AssertExclusiveAccess(fixture.Path);
        }
        AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Preserves caller connection ownership and undo state after a successful load.</summary>
    /// <param name="kind">The element persistence path.</param>
    /// <param name="initiallyOpen">Whether the caller supplies an open connection.</param>
    /// <param name="undoEnabled">The caller's undo setting.</param>
    [STATestMethod]
    [DynamicData(nameof(OwnershipCases), DynamicDataSourceType.Method)]
    public void Open_Success_PreservesConnectionOwnershipAndUndo(string kind, bool initiallyOpen, bool undoEnabled)
    {
        using var fixture = new ElementFixture(kind);
        fixture.Seed();
        using var sqlite = new SQLiteManager(fixture.Path);
        if (initiallyOpen) sqlite.Open();
        fixture.Element.IsUndoEnabled = undoEnabled;

        Open(fixture.Element, sqlite);

        AssertConnectionState(sqlite, initiallyOpen);
        Assert.AreEqual(undoEnabled, fixture.Element.IsUndoEnabled);
        Assert.AreEqual("Original description", fixture.Element.Description);
        Assert.AreEqual(kind is "BivariateAnalysis" or "CoincidentFrequencyAnalysis", fixture.Element.IsDirty,
            "Preserve the existing post-load notification dirty-state behavior.");
        if (initiallyOpen) Assert.IsTrue(sqlite.GetTableManager(fixture.TableName).NumberOfRows > 0);
        else AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Closes only method-owned connections when reading an element fails.</summary>
    /// <param name="kind">The element persistence path.</param>
    /// <param name="initiallyOpen">Whether the caller supplies an open connection.</param>
    /// <param name="undoEnabled">The caller's undo setting.</param>
    [STATestMethod]
    [DynamicData(nameof(OwnershipCases), DynamicDataSourceType.Method)]
    public void Open_TableReadFailure_PreservesExceptionOwnershipAndUndo(string kind, bool initiallyOpen, bool undoEnabled)
    {
        using var fixture = new ElementFixture(kind);
        fixture.Seed();
        using var sqlite = new FailingSQLiteManager(fixture.Path);
        if (initiallyOpen) sqlite.Open();
        sqlite.FailTableRead = true;
        fixture.Element.IsUndoEnabled = undoEnabled;

        var actual = Assert.ThrowsException<InvalidOperationException>(() => Open(fixture.Element, sqlite));

        Assert.AreSame(sqlite.Failure, actual);
        AssertConnectionState(sqlite, initiallyOpen);
        Assert.AreEqual(undoEnabled, fixture.Element.IsUndoEnabled);
        sqlite.FailTableRead = false;
        if (initiallyOpen) Assert.IsTrue(sqlite.GetTableManager(fixture.TableName).NumberOfRows > 0);
        else AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Cleans up a connection even when its acquisition throws after opening it.</summary>
    /// <param name="kind">The element persistence path.</param>
    [STATestMethod]
    [DynamicData(nameof(ElementCases), DynamicDataSourceType.Method)]
    public void Open_PartialAcquisitionFailure_ClosesConnectionAndRestoresUndo(string kind)
    {
        using var fixture = new ElementFixture(kind);
        fixture.Seed();
        using var sqlite = new FailingSQLiteManager(fixture.Path) { FailAfterOpen = true };
        fixture.Element.IsUndoEnabled = false;

        var actual = Assert.ThrowsException<InvalidOperationException>(() => Open(fixture.Element, sqlite));

        Assert.AreSame(sqlite.Failure, actual);
        AssertConnectionState(sqlite, false);
        Assert.IsFalse(fixture.Element.IsUndoEnabled);
        AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Checks direct and external-copy load ownership against real saved data.</summary>
    /// <param name="kind">The element persistence path.</param>
    [STATestMethod]
    [DynamicData(nameof(ElementCases), DynamicDataSourceType.Method)]
    public void OpenAndCopyFromExternal_ReleaseOwnedConnections(string kind)
    {
        using var fixture = new ElementFixture(kind);
        fixture.Seed();

        fixture.Element.Open();
        AssertExclusiveAccess(fixture.Path);
        var copied = (ElementBase)fixture.Element.CopyFromExternal(fixture.Element.Name, fixture.Path);
        fixture.Track(copied);

        Assert.AreEqual("Original description", copied.Description);
        AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Releases owned managers when actual persisted payload parsing fails.</summary>
    /// <param name="kind">The element persistence path.</param>
    /// <param name="externalCopy">Whether to create a copy from the external database.</param>
    [STATestMethod]
    [DynamicData(nameof(MalformedPayloadCases), DynamicDataSourceType.Method)]
    public void Open_MalformedPayload_ReleasesOwnedConnection(string kind, bool externalCopy)
    {
        using var fixture = new ElementFixture(kind);
        fixture.SeedMalformedPayload(kind);
        fixture.Element.IsUndoEnabled = false;

        AssertPayloadFailure(kind, () =>
        {
            if (externalCopy) fixture.Track((ElementBase)fixture.Element.CopyFromExternal(fixture.Element.Name, fixture.Path));
            else fixture.Element.Open();
        });

        Assert.IsFalse(fixture.Element.IsUndoEnabled);
        AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Preserves borrowed ownership and undo state after actual deserialization errors.</summary>
    /// <param name="kind">The element persistence path.</param>
    /// <param name="initiallyOpen">Whether the caller supplies an open connection.</param>
    /// <param name="undoEnabled">The caller's undo setting.</param>
    [STATestMethod]
    [DynamicData(nameof(MalformedOwnershipCases), DynamicDataSourceType.Method)]
    public void Open_MalformedPayload_PreservesBorrowedOwnershipAndUndo(string kind, bool initiallyOpen, bool undoEnabled)
    {
        using var fixture = new ElementFixture(kind);
        fixture.SeedMalformedPayload(kind);
        using var sqlite = new SQLiteManager(fixture.Path);
        if (initiallyOpen) sqlite.Open();
        fixture.Element.IsUndoEnabled = undoEnabled;

        AssertPayloadFailure(kind, () => Open(fixture.Element, sqlite));

        Assert.AreEqual(undoEnabled, fixture.Element.IsUndoEnabled);
        AssertConnectionState(sqlite, initiallyOpen);
        if (initiallyOpen) Assert.AreEqual(1, sqlite.GetTableManager(fixture.TableName).NumberOfRows);
        else AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Preserves existing tolerance of malformed optional payloads and caller ownership.</summary>
    /// <param name="kind">The element persistence path.</param>
    /// <param name="initiallyOpen">Whether the caller supplies an open connection.</param>
    /// <param name="undoEnabled">The caller's undo setting.</param>
    [STATestMethod]
    [DynamicData(nameof(ToleratedPayloadCases), DynamicDataSourceType.Method)]
    public void Open_ToleratedMalformedPayload_PreservesExistingSuccess(string kind, bool initiallyOpen, bool undoEnabled)
    {
        using var fixture = new ElementFixture(kind);
        fixture.Seed();
        if (kind == "TimeSeriesElement")
            fixture.Execute($"UPDATE [{fixture.TableName}] SET [TimeSeriesCompressed]=NULL, [TimeSeries]='<broken';");
        else
            fixture.Execute($"UPDATE [{fixture.TableName}] SET [BayesianAnalysis]='<broken';");
        using var sqlite = new SQLiteManager(fixture.Path);
        if (initiallyOpen) sqlite.Open();
        fixture.Element.IsUndoEnabled = undoEnabled;

        Open(fixture.Element, sqlite);

        AssertConnectionState(sqlite, initiallyOpen);
        Assert.AreEqual(undoEnabled, fixture.Element.IsUndoEnabled);
        Assert.AreEqual(kind == "BivariateAnalysis", fixture.Element.IsDirty);
        Assert.AreEqual("Original description", fixture.Element.Description);
        if (initiallyOpen) Assert.AreEqual(1, sqlite.GetTableManager(fixture.TableName).NumberOfRows);
        else AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Checks dirty and clean save timestamps and close-before-notification ordering.</summary>
    /// <param name="kind">The element persistence path.</param>
    [STATestMethod]
    [DynamicData(nameof(ElementCases), DynamicDataSourceType.Method)]
    public void Save_DirtyThenClean_PreservesTimestampIdentityAndNotificationOrdering(string kind)
    {
        using var fixture = new ElementFixture(kind);
        fixture.Seed();
        var oldTimestamp = new DateTime(2001, 2, 3, 4, 5, 6);
        SetLastModified(fixture.Element, oldTimestamp);
        fixture.Element.Description = "Changed description";
        fixture.Element.Name = "Renamed";
        int saved = 0;
        fixture.Element.ObjectSaved += _ =>
        {
            AssertExclusiveAccess(fixture.Path);
            Assert.IsFalse(fixture.Element.IsDirty);
            Assert.AreEqual("Renamed", fixture.Element.NameOnDisk);
            Assert.IsFalse(fixture.Element.UndoManager.HasChangedSinceSave);
            saved++;
        };

        fixture.Element.Save();

        Assert.AreEqual(1, saved);
        Assert.AreNotEqual(oldTimestamp, fixture.Element.LastModified);
        DateTime savedTimestamp = fixture.Element.LastModified;
        fixture.Element.Save();
        Assert.AreEqual(2, saved);
        Assert.AreEqual(savedTimestamp, fixture.Element.LastModified);
        Assert.AreEqual("Changed description", fixture.ReadCell("Description"));
        AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Exercises an actual SQLite write failure and verifies a subsequent retry.</summary>
    /// <param name="kind">The element persistence path.</param>
    [STATestMethod]
    [DynamicData(nameof(ElementCases), DynamicDataSourceType.Method)]
    public void Save_TriggerFailure_RetainsRetryStateAndReleasesConnection(string kind)
    {
        using var fixture = new ElementFixture(kind);
        fixture.Seed();
        DateTime originalTimestamp = fixture.Element.LastModified;
        string originalNameOnDisk = fixture.Element.NameOnDisk;
        fixture.Element.Description = "Pending edit";
        int saved = 0;
        fixture.Element.ObjectSaved += _ => saved++;
        fixture.InstallFailureTrigger("UPDATE");

        Exception actual = Assert.ThrowsException<System.Data.SQLite.SQLiteException>(() => fixture.Element.Save());

        StringAssert.Contains(actual.Message, "element-lifetime-forced-failure");
        Assert.IsTrue(fixture.Element.IsDirty);
        Assert.AreEqual(originalTimestamp, fixture.Element.LastModified);
        Assert.AreEqual(originalNameOnDisk, fixture.Element.NameOnDisk);
        Assert.IsTrue(fixture.Element.UndoManager.HasChangedSinceSave);
        Assert.AreEqual(0, saved);
        AssertExclusiveAccess(fixture.Path);
        Assert.AreEqual("Original description", fixture.ReadCell("Description"));
        fixture.RemoveFailureTrigger();
        fixture.Element.Save();
        Assert.AreEqual(1, saved);
        Assert.IsFalse(fixture.Element.IsDirty);
        Assert.AreEqual("Pending edit", fixture.ReadCell("Description"));
        AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Ensures failed and successful deletes release resources before notifications.</summary>
    /// <param name="kind">The element persistence path.</param>
    [STATestMethod]
    [DynamicData(nameof(ElementCases), DynamicDataSourceType.Method)]
    public void Delete_TriggerFailureThenRetry_ReleasesConnectionAndNotifiesOnlyOnSuccess(string kind)
    {
        using var fixture = new ElementFixture(kind);
        fixture.Seed();
        fixture.InstallFailureTrigger("DELETE");
        int deleted = 0;
        fixture.Element.Deleted += _ =>
        {
            AssertExclusiveAccess(fixture.Path);
            deleted++;
        };

        Exception actual = Assert.ThrowsException<System.Data.SQLite.SQLiteException>(() => fixture.Element.Delete());

        StringAssert.Contains(actual.Message, "element-lifetime-forced-failure");
        Assert.AreEqual(0, deleted);
        AssertExclusiveAccess(fixture.Path);
        Assert.AreEqual(1, fixture.RowCount());
        fixture.RemoveFailureTrigger();
        fixture.Element.Delete();
        Assert.AreEqual(1, deleted);
        Assert.AreEqual(0, fixture.RowCount());
        AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Preserves the null-name guards before any save or delete database acquisition.</summary>
    /// <param name="kind">The element persistence path.</param>
    [STATestMethod]
    [DynamicData(nameof(ElementCases), DynamicDataSourceType.Method)]
    public void SaveAndDelete_NullName_ReturnBeforeAcquisitionOrNotifications(string kind)
    {
        using var fixture = new ElementFixture(kind);
        fixture.Seed();
        fixture.Element.GetType().GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fixture.Element, null);
        DateTime priorTimestamp = fixture.Element.LastModified;
        bool priorDirty = fixture.Element.IsDirty;
        int saved = 0;
        int deleted = 0;
        fixture.Element.ObjectSaved += _ => saved++;
        fixture.Element.Deleted += _ => deleted++;

        // The existing exclusive handle makes any accidental SQLite acquisition fail immediately.
        using (var handle = new FileStream(fixture.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            fixture.Element.Save();
            fixture.Element.Delete();
        }

        Assert.AreEqual(0, saved);
        Assert.AreEqual(0, deleted);
        Assert.AreEqual(priorTimestamp, fixture.Element.LastModified);
        Assert.AreEqual(priorDirty, fixture.Element.IsDirty);
        Assert.AreEqual(1, fixture.RowCount());
    }

    /// <summary>Checks the early-return branches without changing their dirty/undo behavior.</summary>
    /// <param name="kind">The element persistence path.</param>
    /// <param name="missingRow">Whether the CFA table exists without the requested row.</param>
    /// <param name="initiallyOpen">Whether the caller owns an already-open connection.</param>
    [STATestMethod]
    [DataRow("CoincidentFrequencyAnalysis", false, false)]
    [DataRow("CoincidentFrequencyAnalysis", false, true)]
    [DataRow("CoincidentFrequencyAnalysis", true, false)]
    [DataRow("CoincidentFrequencyAnalysis", true, true)]
    [DataRow("FittingAnalysis", false, false)]
    [DataRow("FittingAnalysis", false, true)]
    public void Open_EarlyReturn_PreservesBorrowedConnectionAndUndo(string kind, bool missingRow, bool initiallyOpen)
    {
        using var fixture = new ElementFixture(kind);
        if (kind == "FittingAnalysis")
        {
            fixture.Seed();
            fixture.Execute("UPDATE [Project] SET [SoftwareVersion]='1.0';");
            fixture.Execute($"ALTER TABLE [{fixture.TableName}] ADD [OutputFrequencyOrdinates] TEXT;");
            fixture.Execute($"UPDATE [{fixture.TableName}] SET [OutputFrequencyOrdinates]='<OutputFrequencyOrdinates />';");
        }
        else if (missingRow)
        {
            fixture.Seed();
            fixture.Execute($"DELETE FROM [{fixture.TableName}];");
        }
        fixture.Element.IsUndoEnabled = false;
        using var sqlite = new SQLiteManager(fixture.Path);
        if (initiallyOpen) sqlite.Open();

        Open(fixture.Element, sqlite);

        AssertConnectionState(sqlite, initiallyOpen);
        Assert.IsFalse(fixture.Element.IsUndoEnabled);
        if (!initiallyOpen) AssertExclusiveAccess(fixture.Path);
    }

    /// <summary>Dispatches to the public borrowed-manager overload without reflection wrapping exceptions.</summary>
    /// <param name="element">The element to restore.</param>
    /// <param name="sqlite">The caller-supplied database manager.</param>
    private static void Open(ElementBase element, SQLiteManager sqlite)
    {
        switch (element)
        {
            case UI.InputData value: value.Open(sqlite); break;
            case TimeSeriesElement value: value.Open(sqlite); break;
            case FittingAnalysis value: value.Open(sqlite); break;
            case UI.UnivariateAnalysis value: value.Open(sqlite); break;
            case B17CAnalysis value: value.Open(sqlite); break;
            case MixtureAnalysis value: value.Open(sqlite); break;
            case PointProcessAnalysis value: value.Open(sqlite); break;
            case CompositeAnalysis value: value.Open(sqlite); break;
            case UI.BivariateAnalysis value: value.Open(sqlite); break;
            case CoincidentFrequencyAnalysis value: value.Open(sqlite); break;
            case UI.RatingCurveAnalysis value: value.Open(sqlite); break;
            case UI.TimeSeriesAnalysis value: value.Open(sqlite); break;
            default: throw new ArgumentOutOfRangeException(nameof(element));
        }
    }

    /// <summary>Checks both the manager flag and its actual provider connection state.</summary>
    /// <param name="sqlite">The manager under observation.</param>
    /// <param name="open">The expected state.</param>
    private static void AssertConnectionState(SQLiteManager sqlite, bool open)
    {
        Assert.AreEqual(open, sqlite.DataBaseOpen, "The caller-visible ownership state changed.");
        Assert.AreEqual(open ? ConnectionState.Open : ConnectionState.Closed, sqlite.DbConnection.State);
    }

    /// <summary>Attempts an exclusive native file open without forcing collection or clearing pools.</summary>
    /// <param name="path">The database whose handles must have been released.</param>
    private static void AssertExclusiveAccess(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.IsTrue(stream.Length > 0);
    }

    /// <summary>Checks the existing parser's exact public exception type.</summary>
    /// <param name="kind">The malformed element type.</param>
    /// <param name="action">The production load operation.</param>
    private static void AssertPayloadFailure(string kind, Action action)
    {
        if (kind == "CoincidentFrequencyAnalysis") Assert.ThrowsException<FormatException>(action);
        else Assert.ThrowsException<System.Xml.XmlException>(action);
    }

    /// <summary>Sets a deterministic prior timestamp without changing the production clock.</summary>
    /// <param name="element">The element being saved.</param>
    /// <param name="timestamp">The prior timestamp.</param>
    private static void SetLastModified(ElementBase element, DateTime timestamp)
    {
        element.GetType().GetField("_lastModified", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(element, timestamp);
    }

    /// <summary>Injects errors after real acquisition and at the public table-read boundary.</summary>
    private sealed class FailingSQLiteManager : SQLiteManager
    {
        /// <summary>Constructs a real manager before enabling failure injection.</summary>
        /// <param name="path">The fixture database path.</param>
        public FailingSQLiteManager(string path) : base(path) { }

        /// <summary>Gets the exact exception whose identity must survive cleanup.</summary>
        public InvalidOperationException Failure { get; } = new("element-lifetime-injected-failure");

        /// <summary>Gets or sets whether a completed physical open should throw.</summary>
        public bool FailAfterOpen { get; set; }

        /// <summary>Gets or sets whether a table read should throw.</summary>
        public bool FailTableRead { get; set; }

        /// <summary>Opens the real connection and optionally fails before returning.</summary>
        public override void Open()
        {
            base.Open();
            if (FailAfterOpen) throw Failure;
        }

        /// <summary>Optionally fails before obtaining the requested real table.</summary>
        /// <param name="tableName">The requested table.</param>
        /// <returns>The real table manager when failure injection is disabled.</returns>
        public override DataTableView GetTableManager(string tableName)
        {
            if (FailTableRead) throw Failure;
            return base.GetTableManager(tableName);
        }
    }

    /// <summary>Owns a private project and its temporary element persistence file.</summary>
    private sealed class ElementFixture : IDisposable
    {
        private readonly List<ElementBase> _elements = new();

        /// <summary>Creates a private project with the minimal project metadata table.</summary>
        /// <param name="kind">The element persistence path to exercise.</param>
        public ElementFixture(string kind)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bestfit-element-lifetime-{Guid.NewGuid():N}.bestfit");
            Project = (BestFitProject)Activator.CreateInstance(typeof(BestFitProject), nonPublic: true)!;
            Project.FullFileName = Path;
            Element = kind switch
            {
                "InputData" => new UI.InputData("Element", Collection<InputDataCollection>()),
                "TimeSeriesElement" => new TimeSeriesElement("Element", Collection<TimeSeriesCollection>()),
                "FittingAnalysis" => new FittingAnalysis("Element", Collection<FittingAnalysisCollection>()),
                "UnivariateAnalysis" => new UI.UnivariateAnalysis("Element", Collection<UnivariateAnalysisCollection>()),
                "B17CAnalysis" => new B17CAnalysis("Element", Collection<UnivariateAnalysisCollection>()),
                "MixtureAnalysis" => new MixtureAnalysis("Element", Collection<UnivariateAnalysisCollection>()),
                "PointProcessAnalysis" => new PointProcessAnalysis("Element", Collection<UnivariateAnalysisCollection>()),
                "CompositeAnalysis" => new CompositeAnalysis("Element", Collection<UnivariateAnalysisCollection>()),
                "BivariateAnalysis" => new UI.BivariateAnalysis("Element", Collection<BivariateAnalysisCollection>()),
                "CoincidentFrequencyAnalysis" => new CoincidentFrequencyAnalysis("Element", Collection<BivariateAnalysisCollection>()),
                "RatingCurveAnalysis" => new UI.RatingCurveAnalysis("Element", Collection<RatingCurveAnalysisCollection>()),
                "TimeSeriesAnalysis" => new UI.TimeSeriesAnalysis("Element", Collection<TimeSeriesAnalysisCollection>()),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            _elements.Add(Element);
            TableName = kind switch
            {
                "UnivariateAnalysis" => UI.UnivariateAnalysis.CollectionName,
                "B17CAnalysis" => B17CAnalysis.CollectionName,
                "MixtureAnalysis" => MixtureAnalysis.CollectionName,
                "PointProcessAnalysis" => PointProcessAnalysis.CollectionName,
                "CompositeAnalysis" => CompositeAnalysis.CollectionName,
                "BivariateAnalysis" => UI.BivariateAnalysis.CollectionName,
                "CoincidentFrequencyAnalysis" => CoincidentFrequencyAnalysis.CollectionName,
                _ => Element.ParentCollection.Name
            };
            var projectTable = new DataTable("Project");
            projectTable.Columns.Add("Name", typeof(string));
            projectTable.Columns.Add("SoftwareVersion", typeof(string));
            projectTable.Rows.Add("Lifetime fixture", "2.0.1");
            using var sqlite = new SQLiteManager(Path);
            sqlite.Open();
            sqlite.SaveDataTable(projectTable);
        }

        /// <summary>Gets the temporary database path.</summary>
        public string Path { get; }

        /// <summary>Gets the test-local project.</summary>
        public BestFitProject Project { get; }

        /// <summary>Gets the production element being exercised.</summary>
        public ElementBase Element { get; }

        /// <summary>Gets the table containing the element's persisted state.</summary>
        public string TableName { get; }

        /// <summary>Obtains one of the private project's production collections.</summary>
        /// <typeparam name="T">The collection type.</typeparam>
        /// <returns>The matching collection.</returns>
        private T Collection<T>() => Project.ElementCollections!.OfType<T>().Single();

        /// <summary>Tracks a copied element for local subscription and message cleanup.</summary>
        /// <param name="element">The additional test-owned element.</param>
        public void Track(ElementBase element) => _elements.Add(element);

        /// <summary>Saves a real initial row and its production schema.</summary>
        public void Seed()
        {
            Element.Description = "Original description";
            Element.Save();
        }

        /// <summary>Writes invalid data at an existing unhandled deserialization boundary.</summary>
        /// <param name="kind">The element persistence path.</param>
        /// <remarks>Only these payloads currently propagate parser failures; tolerated payloads are not reclassified.</remarks>
        public void SeedMalformedPayload(string kind)
        {
            if (kind is "UnivariateAnalysis" or "B17CAnalysis" or "MixtureAnalysis" or "PointProcessAnalysis")
            {
                var collection = Collection<InputDataCollection>();
                var input = new UI.InputData("Source", collection);
                Track(input);
                collection.Add(input);
                switch (Element)
                {
                    case UI.UnivariateAnalysis value: value.InputData = input; break;
                    case B17CAnalysis value: value.InputData = input; break;
                    case MixtureAnalysis value: value.InputData = input; break;
                    case PointProcessAnalysis value: value.InputData = input; break;
                }
            }
            Seed();
            string column = kind switch
            {
                "InputData" => "SystematicDataList",
                "FittingAnalysis" => "OutputFrequencyOrdinates",
                "UnivariateAnalysis" => "UnivariateDistribution",
                "B17CAnalysis" => "Bulletin17CDistribution",
                "MixtureAnalysis" => "MixtureDistribution",
                "PointProcessAnalysis" => "PointProcess",
                "CompositeAnalysis" => "CorrelationMatrix",
                "CoincidentFrequencyAnalysis" => "XValues",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            if (kind is "InputData" or "FittingAnalysis")
            {
                Execute("UPDATE [Project] SET [SoftwareVersion]='1.0';");
                Execute($"ALTER TABLE [{TableName}] ADD [{column}] TEXT;");
            }
            string payload = kind == "CoincidentFrequencyAnalysis" ? "not-a-double" : "<broken";
            Execute($"UPDATE [{TableName}] SET [{column}]='{payload}';");
        }

        /// <summary>Executes fixture-only SQL against a deterministically owned connection.</summary>
        /// <param name="sql">The trusted test SQL.</param>
        public void Execute(string sql)
        {
            using var sqlite = new SQLiteManager(Path);
            sqlite.Open();
            using var command = sqlite.DbConnection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        /// <summary>Creates an actual database failure for a persisted row mutation.</summary>
        /// <param name="operation">The literal UPDATE or DELETE operation.</param>
        public void InstallFailureTrigger(string operation) => Execute(
            $"CREATE TRIGGER [ElementLifetimeFailure] BEFORE {operation} ON [{TableName}] BEGIN SELECT RAISE(ABORT, 'element-lifetime-forced-failure'); END;");

        /// <summary>Removes the fault so the production operation can be retried.</summary>
        public void RemoveFailureTrigger() => Execute("DROP TRIGGER [ElementLifetimeFailure];");

        /// <summary>Reads a scalar from the sole fixture row.</summary>
        /// <param name="column">The column to inspect.</param>
        /// <returns>The persisted text.</returns>
        public string ReadCell(string column)
        {
            using var sqlite = new SQLiteManager(Path);
            sqlite.Open();
            return sqlite.GetTableManager(TableName).GetCell(column, 0)?.ToString() ?? string.Empty;
        }

        /// <summary>Counts surviving rows in the production element table.</summary>
        /// <returns>The row count.</returns>
        public int RowCount()
        {
            using var sqlite = new SQLiteManager(Path);
            sqlite.Open();
            return sqlite.GetTableManager(TableName).NumberOfRows;
        }

        /// <summary>Removes fixture messages and files without GC or pool clearing.</summary>
        public void Dispose()
        {
            // A failed CopyFromExternal can create an element before it throws and returns no reference.
            var messageSources = Messenger.GetInstance().AllMessageItems().Select(message => message.Source)
                .OfType<ElementBase>().Where(element => ReferenceEquals(element.ParentCollection.ParentProject, Project));
            foreach (var element in _elements.Concat(messageSources).Distinct().ToArray())
            {
                element.GetType().GetMethod("DisposeBridges", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(element, null);
                Messenger.GetInstance().Clear(element);
            }
            Messenger.GetInstance().Clear(Project);
            foreach (string candidate in new[] { Path, Path + "-wal", Path + "-shm" })
            {
                try { if (File.Exists(candidate)) File.Delete(candidate); }
                catch (IOException ex)
                {
                    // Preserve a failed test's leaked file rather than hiding its original assertion.
                    System.Diagnostics.Debug.WriteLine($"Element lifetime fixture cleanup: {ex.Message}");
                }
            }
        }
    }
}
