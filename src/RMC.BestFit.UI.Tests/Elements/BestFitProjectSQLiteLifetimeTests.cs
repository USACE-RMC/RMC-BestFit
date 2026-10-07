using System.Data.SQLite;
using System.IO;
using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements;

/// <summary>Protects project ownership, layout saves, and retry behavior around real SQLite failures.</summary>
[TestClass]
[DoNotParallelize]
public class BestFitProjectSQLiteLifetimeTests
{
    /// <summary>An exception raised while project metadata is open must release the connection.</summary>
    [STATestMethod]
    public void Open_LegacyNotificationFailure_ReleasesDatabaseAndAllowsRetry()
    {
        using var scope = new SQLitePersistenceTestScope();
        scope.Execute("UPDATE Project SET SoftwareVersion='1.0'");
        var error = new InvalidOperationException("injected legacy notification failure");
        BestFitProject.Version1EventHandler handler = (bool legacy, ref bool cancel) => throw error;
        scope.Project.OpenedVersion1 += handler;
        try
        {
            Assert.AreSame(error, Assert.ThrowsException<InvalidOperationException>(() => scope.Project.Open()));
            Assert.IsTrue(scope.Project.IsUndoEnabled);
            scope.AssertReleased();
        }
        finally
        {
            scope.Project.OpenedVersion1 -= handler;
        }
        scope.Execute("UPDATE Project SET SoftwareVersion='2.0'");
        scope.Project.Open();
        scope.Project.Save();
        scope.AssertReleased();
    }

    /// <summary>Layout-only writes preserve project timestamps and skip all child-save previews.</summary>
    [STATestMethod]
    public void Save_LayoutOnly_PreservesTimestampAndSkipsChildren()
    {
        using var scope = new SQLitePersistenceTestScope();
        DateTime original = scope.Project.LastModified;
        int previews = 0;
        PreviewObjectSavedEventHandler handler = (ISave sender, ref bool cancel) => previews++;
        foreach (var collection in scope.Collections)
            collection.PreviewObjectSaved += handler;
        try
        {
            scope.Project.AvalonDockLayout = "<Layout Test='first' />";
            Assert.IsFalse(scope.Project.IsDirty);
            Assert.IsTrue(scope.Project.LayoutDirty);
            scope.Project.Save();
            Assert.AreEqual(original, scope.Project.LastModified);
            Assert.AreEqual(0, previews);
            Assert.IsFalse(scope.Project.LayoutDirty);
            Assert.AreEqual("<Layout Test='first' />", scope.Scalar("SELECT AvalonDockLayout FROM Project"));
            scope.Project.AvalonDockLayout = "<Layout Test='second' />";
            scope.Project.Save();
            Assert.AreEqual("<Layout Test='first' />", scope.Scalar("SELECT AvalonDockLayoutPrevious FROM Project"));
            Assert.AreEqual(original, scope.Project.LastModified);
            scope.AssertReleased();
        }
        finally
        {
            foreach (var collection in scope.Collections)
                collection.PreviewObjectSaved -= handler;
        }
    }

    /// <summary>A failed layout write retains pending state and can be retried on the same project.</summary>
    [STATestMethod]
    public void Save_LayoutWriteFailure_PreservesPendingStateAndAllowsRetry()
    {
        using var scope = new SQLitePersistenceTestScope();
        DateTime original = scope.Project.LastModified;
        scope.Project.ProjectExplorerLayout = "<Root Test='pending' />";
        scope.Execute("CREATE TRIGGER reject_layout BEFORE UPDATE ON Project BEGIN SELECT RAISE(ABORT, 'injected layout failure'); END");
        var error = Assert.ThrowsException<SQLiteException>(() => scope.Project.Save());
        StringAssert.Contains(error.Message, "injected layout failure");
        Assert.IsTrue(scope.Project.LayoutDirty);
        Assert.AreEqual(original, scope.Project.LastModified);
        scope.AssertReleased();
        scope.Execute("DROP TRIGGER reject_layout");
        scope.Project.Save();
        Assert.IsFalse(scope.Project.LayoutDirty);
        Assert.AreEqual("<Root Test='pending' />", scope.Scalar("SELECT ProjectExplorerLayout FROM Project"));
        scope.AssertReleased();
    }

    /// <summary>A child failure must release the project connection without raising project-save success.</summary>
    /// <param name="index">The collection containing the child whose write fails.</param>
    [STATestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)]
    public void Save_ChildWriteFailure_ReleasesDatabaseAndAllowsRetry(int index)
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = scope.Collections[index];
        var element = ElementCollectionSQLiteLifetimeTests.CreateElement(index, "Child", collection);
        collection.Add(element);
        scope.Project.Save();
        element.Description = "pending child edit";
        string table = ElementCollectionSQLiteLifetimeTests.StorageTable(index, collection);
        scope.Execute($"CREATE TRIGGER reject_child BEFORE UPDATE ON [{table}] BEGIN SELECT RAISE(ABORT, 'injected child failure'); END");
        int saved = 0;
        ObjectSavedEventHandler handler = sender => saved++;
        scope.Project.ObjectSaved += handler;
        try
        {
            StringAssert.Contains(Assert.ThrowsException<SQLiteException>(() => scope.Project.Save()).Message, "injected child failure");
            Assert.AreEqual(0, saved);
            Assert.IsTrue(element.IsDirty);
            scope.AssertReleased();
            scope.Execute("DROP TRIGGER reject_child");
            scope.Project.Save();
            Assert.AreEqual(1, saved);
            Assert.IsFalse(element.IsDirty);
            Assert.AreEqual("pending child edit", scope.Scalar($"SELECT Description FROM [{table}] WHERE Name='Child'"));
            scope.AssertReleased();
        }
        finally
        {
            scope.Project.ObjectSaved -= handler;
        }
    }

    /// <summary>Successful full saves notify after release and retain collection order after reopen.</summary>
    [STATestMethod]
    public void Save_FullSave_PreservesMetadataOrderAndNotificationTiming()
    {
        using var scope = new SQLitePersistenceTestScope();
        var collection = scope.Collections[1];
        collection.Add(new UI.InputData("First", collection));
        collection.Insert(0, new UI.InputData("Second", collection));
        scope.Project.Description = "project metadata";
        int saved = 0;
        ObjectSavedEventHandler handler = sender => { scope.AssertReleased(); saved++; };
        scope.Project.ObjectSaved += handler;
        try
        {
            scope.Project.Save();
            Assert.AreEqual(1, saved);
            Assert.IsFalse(scope.Project.IsDirty);
            Assert.AreEqual("project metadata", scope.Scalar("SELECT Description FROM Project"));
        }
        finally { scope.Project.ObjectSaved -= handler; }
        scope.Project.Close();
        scope.Project.Open();
        Assert.AreEqual(scope.Project.Name, scope.Project.NameOnDisk);
        CollectionAssert.AreEqual(new[] { "First", "Second" }, scope.Collections[1].Select(item => item.Name).ToArray());
        scope.AssertReleased();
    }

    /// <summary>Maintenance preserves the existing journal mode and leaves a usable project.</summary>
    [STATestMethod]
    public void CompactAndOptimize_PreserveJournalModeAndReleaseDatabase()
    {
        using var scope = new SQLitePersistenceTestScope();
        object? journalMode = scope.Scalar("PRAGMA journal_mode");
        scope.Project.Compact();
        scope.AssertReleased();
        scope.Project.Optimize();
        scope.AssertReleased();
        Assert.AreEqual(journalMode, scope.Scalar("PRAGMA journal_mode"));
        Assert.AreEqual("ok", scope.Scalar("PRAGMA integrity_check"));
        scope.Project.Save();
        scope.AssertReleased();
    }

    /// <summary>The exclusive-file probe must detect an actual open provider connection.</summary>
    [STATestMethod]
    public void FileReleaseProbe_DetectsLiveProviderConnection()
    {
        using var scope = new SQLitePersistenceTestScope();
        using var connection = new SQLiteConnection($"Data Source={scope.FilePath};Pooling=False");
        connection.Open();
        Assert.ThrowsException<IOException>(() => scope.AssertReleased());
        connection.Close();
        scope.AssertReleased();
        GC.KeepAlive(connection);
    }
}
