using System.Data.SQLite;
using System.IO;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements;

/// <summary>Owns a private project and temporary files for persistence regression tests.</summary>
internal sealed class SQLitePersistenceTestScope : IDisposable
{
    private bool _releaseProbeFailed;
    /// <summary>Creates a project without changing the application singleton.</summary>
    internal SQLitePersistenceTestScope()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "BestFitSQLiteLifetime", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        FilePath = Path.Combine(DirectoryPath, "Lifetime.bestfit");
        Project = (BestFitProject)Activator.CreateInstance(typeof(BestFitProject), nonPublic: true)!;
        Project.CreateNew(FilePath);
    }

    /// <summary>Gets the test-owned directory.</summary>
    internal string DirectoryPath { get; }

    /// <summary>Gets the original project file path.</summary>
    internal string FilePath { get; }

    /// <summary>Gets the private project instance.</summary>
    internal BestFitProject Project { get; }

    /// <summary>Gets the collections initialized by the private project constructor.</summary>
    internal IReadOnlyList<IElementCollection> Collections => Project.ElementCollections
        ?? throw new InvalidOperationException("The project did not initialize its collections.");

    /// <summary>Executes fixture SQL against the real provider with deterministic disposal.</summary>
    /// <param name="sql">The fixture statement.</param>
    internal void Execute(string sql)
    {
        using var connection = new SQLiteConnection($"Data Source={Project.FullFileName};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Reads a scalar fixture value using a separate real connection.</summary>
    /// <param name="sql">The fixture query.</param>
    /// <returns>The provider value.</returns>
    internal object? Scalar(string sql)
    {
        using var connection = new SQLiteConnection($"Data Source={Project.FullFileName};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    /// <summary>Probes immediate file release before any project or test cleanup.</summary>
    internal void AssertReleased()
    {
        try
        {
            using var stream = new FileStream(Project.FullFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.IsTrue(stream.Length > 0);
        }
        catch (IOException)
        {
            _releaseProbeFailed = true;
            throw;
        }
    }

    /// <summary>Removes only messages, state, and files owned by this test.</summary>
    public void Dispose()
    {
        foreach (IElementCollection collection in Collections)
        {
            foreach (IElement element in collection)
                Messenger.GetInstance().Clear(element);
            Messenger.GetInstance().Clear(collection);
        }
        Project.Close();
        Messenger.GetInstance().Clear(Project);
        try { Directory.Delete(DirectoryPath, recursive: true); }
        catch (IOException error) when (_releaseProbeFailed)
        {
            // Preserve the earlier resource-release assertion when running against a leaking implementation.
            System.Diagnostics.Debug.WriteLine(error);
        }
    }
}
