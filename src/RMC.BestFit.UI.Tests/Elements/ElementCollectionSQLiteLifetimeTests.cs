using System.IO;
using System.Reflection;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements;

/// <summary>Protects collection connection ownership and state restoration after persistence failures.</summary>
[TestClass]
[DoNotParallelize]
public class ElementCollectionSQLiteLifetimeTests
{
    /// <summary>Gets the seven production collection families.</summary>
    public static IEnumerable<object[]> CollectionCases => Enumerable.Range(0, 7).Select(index => new object[] { index });

    /// <summary>Gets the five collections that compact their single storage table.</summary>
    public static IEnumerable<object[]> SingleTableCases => new[] { 0, 1, 2, 5, 6 }.Select(index => new object[] { index });

    /// <summary>Combines collection families with the Add and Insert paths.</summary>
    /// <returns>The collection index and whether to insert.</returns>
    public static IEnumerable<object[]> InsertionCases()
    {
        foreach (object[] row in CollectionCases)
            foreach (bool insert in new[] { false, true })
                yield return new object[] { row[0], insert };
    }

    /// <summary>An acquisition failure must not leave later additions in load-only mode.</summary>
    /// <param name="index">The project collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(CollectionCases))]
    public void Open_AcquisitionFailure_RestoresOpeningAndAllowsRetry(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = (ElementCollectionBase)scope.Collections[index];
        scope.Project.FullFileName = Path.Combine(scope.DirectoryPath, "missing", "invalid.bestfit");
        Assert.ThrowsException<System.Data.SQLite.SQLiteException>(() => collection.Open());
        Assert.IsFalse(ReadFlag(collection, "_opening"), "Failed acquisition must restore normal collection operation.");
        scope.Project.FullFileName = scope.FilePath;
        collection.Open();
        Assert.IsFalse(ReadFlag(collection, "_opening"));
        scope.AssertReleased();
    }

    /// <summary>A collection preview cancellation must prevent any connection acquisition.</summary>
    /// <param name="index">The project collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(CollectionCases))]
    public void Save_CollectionPreviewCanceled_DoesNotAcquireDatabase(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = (ElementCollectionBase)scope.Collections[index];
        collection.PreviewObjectSaved += CancelSave;
        scope.Project.FullFileName = Path.Combine(scope.DirectoryPath, "missing", "invalid.bestfit");
        try
        {
            collection.Save();
            Assert.IsFalse(ReadFlag(collection, "_savingAll"));
            Assert.IsFalse(File.Exists(scope.Project.FullFileName));
        }
        finally
        {
            collection.PreviewObjectSaved -= CancelSave;
            scope.Project.FullFileName = scope.FilePath;
        }
    }

    /// <summary>Adding a new item must release the outer connection when the child cannot save.</summary>
    /// <param name="index">The project collection index.</param>
    /// <param name="insert">Whether to use Insert rather than Add.</param>
    [STATestMethod]
    [DataRow(0, false)] [DataRow(1, false)] [DataRow(2, false)] [DataRow(3, false)]
    [DataRow(4, false)] [DataRow(5, false)] [DataRow(6, false)]
    [DataRow(0, true)] [DataRow(1, true)] [DataRow(2, true)] [DataRow(3, true)]
    [DataRow(4, true)] [DataRow(5, true)] [DataRow(6, true)]
    public void AddOrInsert_ChildSaveFailure_ReleasesOuterConnection(int index, bool insert)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = (ElementCollectionBase)scope.Collections[index];
        var error = new InvalidOperationException("injected child save failure");
        IElement element = CreateFailingElement(index, collection, error);
        var actual = Assert.ThrowsException<InvalidOperationException>(() =>
        {
            if (insert) collection.Insert(0, element); else collection.Add(element);
        });
        Assert.AreSame(error, actual);
        Assert.AreSame(element, collection[0], "Retain the established in-memory insertion behavior on failure.");
        scope.AssertReleased();
    }

    /// <summary>Successful collection persistence must preserve child order and concrete types.</summary>
    /// <param name="index">The project collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(CollectionCases))]
    public void SaveAndOpen_PreservesEstablishedInsertionOrderAndTypes(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = (ElementCollectionBase)scope.Collections[index];
        var first = CreateElement(index, "First", collection);
        var second = CreateElement(index, "Second", collection);
        collection.Add(first);
        collection.Insert(0, second);
        collection.Save();
        scope.AssertReleased();
        FrameworkInterfaces.Messaging.Messenger.GetInstance().Clear(first);
        FrameworkInterfaces.Messaging.Messenger.GetInstance().Clear(second);
        collection.Clear();
        collection.Open();
        // Typed collections rewrite their index; the existing single-table collections retain stored row order.
        string[] expectedOrder = index is 3 or 4 ? ["Second", "First"] : ["First", "Second"];
        CollectionAssert.AreEqual(expectedOrder, collection.Select(item => item.Name).ToArray());
        Assert.IsTrue(collection.All(item => item.GetType() == first.GetType()));
        Assert.IsFalse(ReadFlag(collection, "_opening"));
        Assert.IsFalse(ReadFlag(collection, "_savingAll"));
        scope.AssertReleased();
    }

    /// <summary>Deleting a collection must release its database immediately.</summary>
    /// <param name="index">The project collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(CollectionCases))]
    public void Delete_Success_ReleasesDatabase(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = (ElementCollectionBase)scope.Collections[index];
        collection.Add(CreateElement(index, "Delete me", collection));
        collection.Save();
        collection.Delete();
        scope.AssertReleased();
    }

    /// <summary>Existing rows must be attached without an unnecessary second child save.</summary>
    /// <param name="index">The project collection index.</param>
    /// <param name="insert">Whether to use Insert rather than Add.</param>
    [STATestMethod]
    [DynamicData(nameof(InsertionCases), DynamicDataSourceType.Method)]
    public void AddOrInsert_ExistingRow_DoesNotSaveChildAgain(int index, bool insert)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = (ElementCollectionBase)scope.Collections[index];
        var element = CreateElement(index, "Existing", collection);
        element.Description = "Stored description";
        element.Save();
        int saved = 0;
        element.ObjectSaved += _ => saved++;
        if (insert) collection.Insert(0, element); else collection.Add(element);
        Assert.AreEqual(0, saved);
        Assert.AreSame(element, collection.Single());
        Assert.AreEqual(1L, scope.Scalar($"SELECT COUNT(*) FROM [{StorageTable(index, collection)}] WHERE Name='Existing'"));
        Assert.AreEqual("Stored description", scope.Scalar($"SELECT Description FROM [{StorageTable(index, collection)}] WHERE Name='Existing'"));
        scope.AssertReleased();
    }

    /// <summary>Actual child writes must reset the save flag after failure and remain retryable.</summary>
    /// <param name="index">The project collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(CollectionCases))]
    public void Save_ChildWriteFailure_RestoresSavingFlagAndAllowsRetry(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = (ElementCollectionBase)scope.Collections[index];
        var element = CreateElement(index, "Child", collection);
        element.Description = "Original";
        collection.Add(element);
        collection.Save();
        element.Description = "Pending edit";
        string table = StorageTable(index, collection);
        scope.Execute($"CREATE TRIGGER reject_collection_child BEFORE UPDATE ON [{table}] BEGIN SELECT RAISE(ABORT, 'collection child failure'); END");
        int saved = 0;
        collection.ObjectSaved += _ => saved++;

        StringAssert.Contains(Assert.ThrowsException<System.Data.SQLite.SQLiteException>(() => collection.Save()).Message, "collection child failure");

        Assert.IsFalse(ReadFlag(collection, "_savingAll"));
        Assert.IsTrue(element.IsDirty);
        Assert.AreEqual(0, saved);
        Assert.AreEqual("Original", scope.Scalar($"SELECT Description FROM [{table}] WHERE Name='Child'"));
        scope.AssertReleased();
        scope.Execute("DROP TRIGGER reject_collection_child");
        collection.Save();
        Assert.IsFalse(ReadFlag(collection, "_savingAll"));
        Assert.IsFalse(element.IsDirty);
        Assert.AreEqual(1, saved);
        Assert.AreEqual("Pending edit", scope.Scalar($"SELECT Description FROM [{table}] WHERE Name='Child'"));
        scope.AssertReleased();
    }

    /// <summary>Compaction must retain its retry flag when a child fails after the table is dropped.</summary>
    /// <param name="index">The single-table collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(SingleTableCases))]
    public void Save_CompactionChildFailure_RetainsCompactionFlagAndAllowsRetry(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = PrepareCompaction(scope, index);
        var error = new InvalidOperationException("injected compaction child failure");
        ClearCollection(collection);
        collection.Add(CreateFailingElement(index, collection, error));
        Assert.IsTrue(ReadCompactionFlag(collection));

        Assert.AreSame(error, Assert.ThrowsException<InvalidOperationException>(() => collection.Save()));

        Assert.IsTrue(ReadCompactionFlag(collection));
        Assert.IsFalse(ReadFlag(collection, "_savingAll"));
        scope.AssertReleased();
        ClearCollection(collection);
        collection.Add(CreateElement(index, "Recovered", collection));
        collection.Save();
        Assert.IsFalse(ReadCompactionFlag(collection));
        Assert.IsFalse(ReadFlag(collection, "_savingAll"));
        Assert.AreEqual("Recovered", scope.Scalar($"SELECT Name FROM [{collection.Name}]"));
        scope.AssertReleased();
    }

    /// <summary>Compaction previews must cancel before any table drop or child write.</summary>
    /// <param name="index">The single-table collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(SingleTableCases))]
    public void Save_CompactionItemPreviewCanceled_PreservesTableAndRetryFlag(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = PrepareCompaction(scope, index);
        var element = collection.Single();
        element.PreviewObjectSaved += CancelSave;
        int saved = 0;
        collection.ObjectSaved += _ => saved++;
        try
        {
            collection.Save();
            Assert.AreEqual(2L, scope.Scalar($"SELECT COUNT(*) FROM [{collection.Name}]"));
            Assert.AreEqual(0, saved);
            Assert.IsTrue(ReadCompactionFlag(collection));
            Assert.IsFalse(ReadFlag(collection, "_savingAll"));
            scope.AssertReleased();
        }
        finally { element.PreviewObjectSaved -= CancelSave; }
        collection.Save();
        Assert.IsFalse(ReadCompactionFlag(collection));
        Assert.AreEqual(1L, scope.Scalar($"SELECT COUNT(*) FROM [{collection.Name}]"));
        Assert.AreEqual(1, saved);
        scope.AssertReleased();
    }

    /// <summary>Ordinary child-preview cancellation must retain the dirty child without saving it.</summary>
    /// <param name="index">The project collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(CollectionCases))]
    public void Save_ItemPreviewCanceled_SkipsDirtyChild(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = (ElementCollectionBase)scope.Collections[index];
        var element = CreateElement(index, "Child", collection);
        element.Description = "Original";
        collection.Add(element);
        collection.Save();
        DateTime priorTimestamp = element.LastModified;
        element.Description = "Pending edit";
        int childSaved = 0;
        element.ObjectSaved += _ => childSaved++;
        element.PreviewObjectSaved += CancelSave;
        try
        {
            collection.Save();
            Assert.AreEqual(0, childSaved);
            Assert.IsTrue(element.IsDirty);
            Assert.AreEqual(priorTimestamp, element.LastModified);
            Assert.AreEqual("Original", scope.Scalar($"SELECT Description FROM [{StorageTable(index, collection)}] WHERE Name='Child'"));
            Assert.IsFalse(ReadFlag(collection, "_savingAll"));
            scope.AssertReleased();
        }
        finally { element.PreviewObjectSaved -= CancelSave; }
        collection.Save();
        Assert.AreEqual(1, childSaved);
        Assert.IsFalse(element.IsDirty);
        scope.AssertReleased();
    }

    /// <summary>Supported external imports release both databases and preserve the requested insertion order.</summary>
    /// <param name="index">The destination collection index.</param>
    /// <param name="coincident">Whether to import the second supported bivariate subtype.</param>
    [STATestMethod]
    [DataRow(0, false)] [DataRow(1, false)] [DataRow(4, false)] [DataRow(4, true)]
    [DataRow(5, false)] [DataRow(6, false)]
    public void InsertFromExternal_SupportedType_PreservesOrderAndReleasesSource(int index, bool coincident)
    {
        using var source = new SQLitePersistenceTestScope();
        using var destination = new SQLitePersistenceTestScope();
        var sourceCollection = source.Collections[index];
        var element = coincident ? new CoincidentFrequencyAnalysis("Imported", sourceCollection)
            : CreateElement(index, "Imported", sourceCollection);
        element.Description = "External metadata";
        sourceCollection.Add(element);
        sourceCollection.Save();
        var collection = (ElementCollectionBase)destination.Collections[index];
        collection.Add(CreateElement(index, "First", collection));
        collection.Add(CreateElement(index, "Last", collection));

        collection.InsertFromExternalProject(1, element.Name, element.GetType().FullName!, source.FilePath);

        CollectionAssert.AreEqual(new[] { "First", "Imported", "Last" }, collection.Select(item => item.Name).ToArray());
        Assert.AreEqual(element.GetType(), collection[1].GetType());
        Assert.AreEqual("External metadata", collection[1].Description);
        source.AssertReleased();
        destination.AssertReleased();
    }

    /// <summary>Unsupported external types preserve the no-op contract and release any source manager.</summary>
    /// <param name="index">The project collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(CollectionCases))]
    public void InsertFromExternal_UnsupportedType_IsNoOpAndReleasesSource(int index)
    {
        using var source = new SQLitePersistenceTestScope();
        using var destination = new SQLitePersistenceTestScope();
        var collection = destination.Collections[index];
        collection.InsertFromExternalProject(0, "Ignored", "Unsupported.Element", source.FilePath);
        Assert.AreEqual(0, collection.Count);
        source.AssertReleased();
        destination.AssertReleased();
        if (index is 2 or 3)
        {
            string type = index == 2 ? typeof(FittingAnalysis).FullName! : typeof(UI.UnivariateAnalysis).FullName!;
            collection.InsertFromExternalProject(0, "Ignored", type, string.Empty);
            Assert.AreEqual(0, collection.Count);
        }
    }

    /// <summary>Actual child parse failures restore load state and release outer and inner connections.</summary>
    /// <param name="kind">The child type with an existing uncaught parser failure.</param>
    [STATestMethod]
    [DataRow("InputData")] [DataRow("FittingAnalysis")] [DataRow("UnivariateAnalysis")]
    [DataRow("B17CAnalysis")] [DataRow("MixtureAnalysis")] [DataRow("PointProcessAnalysis")]
    [DataRow("CompositeAnalysis")] [DataRow("CoincidentFrequencyAnalysis")]
    public void Open_ChildPayloadFailure_RestoresOpeningAndAllowsRetry(string kind)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = SeedMalformedChild(scope, kind, out string repairSql);
        ClearCollection(collection);
        try
        {
            AssertParserFailure(kind, collection.Open);
            Assert.IsFalse(ReadFlag(collection, "_opening"));
            scope.AssertReleased();
            scope.Execute(repairSql);
            collection.Open();
            Assert.AreEqual(1, collection.Count);
            Assert.AreEqual(kind, collection[0].GetType().Name);
            Assert.IsFalse(ReadFlag(collection, "_opening"));
            scope.AssertReleased();
        }
        finally { ClearProjectMessages(scope.Project); }
    }

    /// <summary>Supported import parse failures must release the source without inserting a child.</summary>
    /// <param name="kind">The supported child type whose payload throws.</param>
    [STATestMethod]
    [DataRow("InputData")] [DataRow("CoincidentFrequencyAnalysis")]
    public void InsertFromExternal_ChildPayloadFailure_ReleasesSourceAndAllowsRetry(string kind)
    {
        using var source = new SQLitePersistenceTestScope();
        using var destination = new SQLitePersistenceTestScope();
        var sourceCollection = SeedMalformedChild(source, kind, out string repairSql);
        int index = kind == "InputData" ? 1 : 4;
        var collection = destination.Collections[index];
        string type = sourceCollection[0].GetType().FullName!;
        try
        {
            AssertParserFailure(kind, () => collection.InsertFromExternalProject(0, "Child", type, source.FilePath));
            Assert.AreEqual(0, collection.Count);
            source.AssertReleased();
            destination.AssertReleased();
            source.Execute(repairSql);
            collection.InsertFromExternalProject(0, "Child", type, source.FilePath);
            Assert.AreEqual(1, collection.Count);
            source.AssertReleased();
            destination.AssertReleased();
        }
        finally { ClearProjectMessages(destination.Project); }
    }

    /// <summary>Delete acquisition failures preserve collection contents and permit a later retry.</summary>
    /// <param name="index">The project collection index.</param>
    [STATestMethod]
    [DynamicData(nameof(CollectionCases))]
    public void Delete_AcquisitionFailure_PreservesContentsAndAllowsRetry(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = scope.Collections[index];
        collection.Add(CreateElement(index, "Child", collection));
        scope.Project.FullFileName = Path.Combine(scope.DirectoryPath, "missing", "invalid.bestfit");
        try
        {
            Assert.ThrowsException<System.Data.SQLite.SQLiteException>(collection.Delete);
            Assert.AreEqual(1, collection.Count);
        }
        finally { scope.Project.FullFileName = scope.FilePath; }
        scope.AssertReleased();
        collection.Delete();
        scope.AssertReleased();
    }

    /// <summary>Gets the element data table, accounting for typed collection index tables.</summary>
    /// <param name="index">The project collection index.</param>
    /// <param name="collection">The production collection.</param>
    /// <returns>The fixture element's data table.</returns>
    internal static string StorageTable(int index, IElementCollection collection) => index switch
    {
        3 => UI.UnivariateAnalysis.CollectionName,
        4 => UI.BivariateAnalysis.CollectionName,
        _ => collection.Name
    };

    /// <summary>Loads a single-table collection with a real blank row requiring compaction.</summary>
    /// <param name="scope">The private project fixture.</param>
    /// <param name="index">The single-table collection index.</param>
    /// <returns>The loaded collection with its compaction flag set.</returns>
    private static ElementCollectionBase PrepareCompaction(SQLitePersistenceTestScope scope, int index)
    {
        var collection = (ElementCollectionBase)scope.Collections[index];
        collection.Add(CreateElement(index, "Fail", collection));
        collection.Save();
        scope.Execute($"INSERT INTO [{collection.Name}] (Name) VALUES ('')");
        ClearCollection(collection);
        collection.Open();
        Assert.AreEqual(1, collection.Count);
        Assert.IsTrue(ReadCompactionFlag(collection));
        return collection;
    }

    /// <summary>Reads the concrete collection's persisted compaction-retry state.</summary>
    /// <param name="collection">The single-table collection.</param>
    /// <returns>Whether its next save must compact the storage table.</returns>
    private static bool ReadCompactionFlag(ElementCollectionBase collection) =>
        (bool)collection.GetType().GetField("_needsTableCompaction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(collection)!;

    /// <summary>Clears only test-owned collection children and their bridge/message subscriptions.</summary>
    /// <param name="collection">The collection to reset before loading.</param>
    private static void ClearCollection(ElementCollectionBase collection)
    {
        foreach (IElement element in collection)
        {
            element.GetType().GetMethod("DisposeBridges", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(element, null);
            Messenger.GetInstance().Clear(element);
        }
        collection.Clear();
    }

    /// <summary>Removes messages from test-owned children, including constructors that failed before insertion.</summary>
    /// <param name="project">The private test project.</param>
    private static void ClearProjectMessages(BestFitProject project)
    {
        foreach (IElement element in Messenger.GetInstance().AllMessageItems().Select(item => item.Source).OfType<IElement>()
                     .Where(element => ReferenceEquals(element.ParentCollection.ParentProject, project)).Distinct().ToArray())
            Messenger.GetInstance().Clear(element);
    }

    /// <summary>Checks the existing uncaught parser exception type without reflection wrappers.</summary>
    /// <param name="kind">The malformed child type.</param>
    /// <param name="action">The load operation.</param>
    private static void AssertParserFailure(string kind, Action action)
    {
        if (kind == "CoincidentFrequencyAnalysis") Assert.ThrowsException<FormatException>(action);
        else Assert.ThrowsException<System.Xml.XmlException>(action);
    }

    /// <summary>Stores a real child, corrupts its existing parse boundary, and supplies a repair statement.</summary>
    /// <param name="scope">The private source project.</param>
    /// <param name="kind">The child type.</param>
    /// <param name="repairSql">Receives fixture SQL restoring the original readable payload.</param>
    /// <returns>The affected production collection.</returns>
    private static ElementCollectionBase SeedMalformedChild(SQLitePersistenceTestScope scope, string kind, out string repairSql)
    {
        int index = kind switch { "InputData" => 1, "FittingAnalysis" => 2, "CoincidentFrequencyAnalysis" => 4, _ => 3 };
        var collection = (ElementCollectionBase)scope.Collections[index];
        IElement element = kind switch
        {
            "B17CAnalysis" => new B17CAnalysis("Child", collection),
            "MixtureAnalysis" => new MixtureAnalysis("Child", collection),
            "PointProcessAnalysis" => new PointProcessAnalysis("Child", collection),
            "CompositeAnalysis" => new CompositeAnalysis("Child", collection),
            "CoincidentFrequencyAnalysis" => new CoincidentFrequencyAnalysis("Child", collection),
            _ => CreateElement(index, "Child", collection)
        };
        if (kind is "UnivariateAnalysis" or "B17CAnalysis" or "MixtureAnalysis" or "PointProcessAnalysis")
        {
            var input = new UI.InputData("Source", scope.Collections[1]);
            scope.Collections[1].Add(input);
            switch (element)
            {
                case UI.UnivariateAnalysis value: value.InputData = input; break;
                case B17CAnalysis value: value.InputData = input; break;
                case MixtureAnalysis value: value.InputData = input; break;
                case PointProcessAnalysis value: value.InputData = input; break;
            }
        }
        collection.Add(element);
        collection.Save();
        string table = kind switch
        {
            "B17CAnalysis" => B17CAnalysis.CollectionName,
            "MixtureAnalysis" => MixtureAnalysis.CollectionName,
            "PointProcessAnalysis" => PointProcessAnalysis.CollectionName,
            "CompositeAnalysis" => CompositeAnalysis.CollectionName,
            "CoincidentFrequencyAnalysis" => CoincidentFrequencyAnalysis.CollectionName,
            _ => StorageTable(index, collection)
        };
        string column = kind switch
        {
            "InputData" => "SystematicDataList", "FittingAnalysis" => "OutputFrequencyOrdinates",
            "UnivariateAnalysis" => "UnivariateDistribution", "B17CAnalysis" => "Bulletin17CDistribution",
            "MixtureAnalysis" => "MixtureDistribution", "PointProcessAnalysis" => "PointProcess",
            "CompositeAnalysis" => "CorrelationMatrix", "CoincidentFrequencyAnalysis" => "XValues",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if (kind is "InputData" or "FittingAnalysis")
        {
            scope.Execute("UPDATE Project SET SoftwareVersion='1.0'");
            scope.Execute($"ALTER TABLE [{table}] ADD [{column}] TEXT");
            repairSql = "UPDATE Project SET SoftwareVersion='2.0'";
        }
        else
        {
            string original = scope.Scalar($"SELECT [{column}] FROM [{table}] WHERE Name='Child'")?.ToString() ?? string.Empty;
            repairSql = $"UPDATE [{table}] SET [{column}]='{original.Replace("'", "''")}' WHERE Name='Child'";
        }
        string malformed = kind == "CoincidentFrequencyAnalysis" ? "not-a-double" : "<broken";
        scope.Execute($"UPDATE [{table}] SET [{column}]='{malformed}' WHERE Name='Child'");
        return collection;
    }

    /// <summary>Reads a protected lifecycle flag to verify that failures restore its contract.</summary>
    /// <param name="collection">The collection under test.</param>
    /// <param name="name">The lifecycle field.</param>
    /// <returns>The current flag value.</returns>
    private static bool ReadFlag(ElementCollectionBase collection, string name) =>
        (bool)typeof(ElementCollectionBase).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(collection)!;

    /// <summary>Cancels the existing collection preview event.</summary>
    /// <param name="sender">The event source.</param>
    /// <param name="cancel">Receives cancellation.</param>
    private static void CancelSave(object sender, ref bool cancel) => cancel = true;

    /// <summary>Creates a real element for a project collection without estimation.</summary>
    /// <param name="index">The collection index.</param>
    /// <param name="name">The fixture name.</param>
    /// <param name="collection">The parent collection.</param>
    /// <returns>A production element.</returns>
    internal static IElement CreateElement(int index, string name, IElementCollection collection) => index switch
    {
        0 => new TimeSeriesElement(name, collection),
        1 => new UI.InputData(name, collection),
        2 => new UI.FittingAnalysis(name, collection),
        3 => new UI.UnivariateAnalysis(name, collection),
        4 => new UI.BivariateAnalysis(name, collection),
        5 => new UI.RatingCurveAnalysis(name, collection),
        6 => new UI.TimeSeriesAnalysis(name, collection),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    /// <summary>Creates a child that fails at the collection's real child-save boundary.</summary>
    /// <param name="index">The collection index.</param>
    /// <param name="collection">The parent collection.</param>
    /// <param name="error">The sentinel exception.</param>
    /// <returns>A typed child with a failing Save override.</returns>
    private static IElement CreateFailingElement(int index, IElementCollection collection, Exception error) => index switch
    {
        0 => new FailingTimeSeries(collection, error),
        1 => new FailingInputData(collection, error),
        2 => new FailingFitting(collection, error),
        3 => new FailingUnivariate(collection, error),
        4 => new FailingBivariate(collection, error),
        5 => new FailingRatingCurve(collection, error),
        6 => new FailingTimeSeriesAnalysis(collection, error),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    /// <summary>Time-series child failing at the persistence boundary.</summary>
    private sealed class FailingTimeSeries(IElementCollection parent, Exception error) : TimeSeriesElement("Fail", parent)
    {
        /// <summary>Propagates the injected persistence failure.</summary>
        public override void Save() => throw error;
    }
    /// <summary>Input-data child failing at the persistence boundary.</summary>
    private sealed class FailingInputData(IElementCollection parent, Exception error) : UI.InputData("Fail", parent)
    {
        /// <summary>Propagates the injected persistence failure.</summary>
        public override void Save() => throw error;
    }
    /// <summary>Fitting child failing at the persistence boundary.</summary>
    private sealed class FailingFitting(IElementCollection parent, Exception error) : UI.FittingAnalysis("Fail", parent)
    {
        /// <summary>Propagates the injected persistence failure.</summary>
        public override void Save() => throw error;
    }
    /// <summary>Univariate child failing at the persistence boundary.</summary>
    private sealed class FailingUnivariate(IElementCollection parent, Exception error) : UI.UnivariateAnalysis("Fail", parent)
    {
        /// <summary>Propagates the injected persistence failure.</summary>
        public override void Save() => throw error;
    }
    /// <summary>Bivariate child failing at the persistence boundary.</summary>
    private sealed class FailingBivariate(IElementCollection parent, Exception error) : UI.BivariateAnalysis("Fail", parent)
    {
        /// <summary>Propagates the injected persistence failure.</summary>
        public override void Save() => throw error;
    }
    /// <summary>Rating-curve child failing at the persistence boundary.</summary>
    private sealed class FailingRatingCurve(IElementCollection parent, Exception error) : UI.RatingCurveAnalysis("Fail", parent)
    {
        /// <summary>Propagates the injected persistence failure.</summary>
        public override void Save() => throw error;
    }
    /// <summary>Time-series analysis child failing at the persistence boundary.</summary>
    private sealed class FailingTimeSeriesAnalysis(IElementCollection parent, Exception error) : UI.TimeSeriesAnalysis("Fail", parent)
    {
        /// <summary>Propagates the injected persistence failure.</summary>
        public override void Save() => throw error;
    }
}
