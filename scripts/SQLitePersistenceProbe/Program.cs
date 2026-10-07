using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

/// <summary>Captures logical persistence behavior from a supplied, independently built BestFit runtime.</summary>
internal static class Program
{
    private static Type? _connectionType;
    private static dynamic? _messenger;
    /// <summary>Exercises an example copy without estimating or changing its source file.</summary>
    /// <param name="args">Source example, working copy, and report paths.</param>
    /// <returns>Zero only after a successful save/reopen and integrity check.</returns>
    [STAThread]
    private static int Main(string[] args)
    {
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string file = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null;
        };
        return Run(args);
    }

    /// <summary>Runs after dependency resolution has been configured for the captured runtime.</summary>
    /// <param name="args">Source example, working copy, and report paths.</param>
    /// <returns>Zero after successful persistence and integrity checks.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Expected source, working-copy, and report paths.");
        string source = Path.GetFullPath(args[0]);
        string working = Path.GetFullPath(args[1]);
        if (source.Equals(working, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A separate working copy is required.");
        Directory.CreateDirectory(Path.GetDirectoryName(working)!);
        File.Copy(source, working, overwrite: false);
        var ui = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "RMC.BestFit.UI.dll"));
        var framework = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "FrameworkInterfaces.dll"));
        var provider = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "System.Data.SQLite.dll"));
        _connectionType = provider.GetType("System.Data.SQLite.SQLiteConnection", throwOnError: true)!;
        _messenger = framework.GetType("FrameworkInterfaces.Messaging.Messenger", throwOnError: true)!.GetMethod("GetInstance")!.Invoke(null, null)!;
        dynamic project = Activator.CreateInstance(ui.GetType("RMC.BestFit.UI.BestFitProject", throwOnError: true)!, nonPublic: true)!;
        var stages = new SortedDictionary<string, object>(StringComparer.Ordinal);
        try
        {
            stages["source"] = Snapshot(working);
            project.FullFileName = working;
            project.Open();
            stages["opened"] = Snapshot(working);
            object[] collections = ((IEnumerable)project.ElementCollections).Cast<object>().ToArray();
            stages["components"] = CaptureComponents(collections);
            // Exercise every serializer, including saved analysis results, without rerunning the science.
            foreach (var collection in collections)
                foreach (dynamic element in (IEnumerable)collection)
                    element.Save();
            project.Save();
            stages["saved"] = Snapshot(working);
            ClearProject(project);
            project.FullFileName = working;
            project.Open();
            stages["reopened"] = Snapshot(working);
            collections = ((IEnumerable)project.ElementCollections).Cast<object>().ToArray();
            stages["reopenedComponents"] = CaptureComponents(collections);
            using (var exclusive = new FileStream(working, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            // Serialize the freshly hydrated objects so a reopen that loses saved state is observable.
            foreach (var collection in collections)
                foreach (dynamic element in (IEnumerable)collection)
                    element.Save();
            project.Save();
            stages["resaved"] = Snapshot(working);
            using (var exclusive = new FileStream(working, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            File.WriteAllText(args[2], JsonSerializer.Serialize(stages, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS {Path.GetFileName(source)}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            ClearProject(project);
        }
    }

    /// <summary>Captures the loaded collection and element order without changing their state.</summary>
    /// <param name="collections">The current project collections.</param>
    /// <returns>Ordered collection names and their ordered concrete element identities.</returns>
    private static object[] CaptureComponents(object[] collections) => collections.Select(collection => new
    {
        Name = (string)((dynamic)collection).Name,
        Elements = ((IEnumerable)collection).Cast<object>().Select(element => new
        {
            Name = (string)((dynamic)element).Name,
            Type = element.GetType().FullName
        }).ToArray()
    }).ToArray();

    /// <summary>Releases test-owned project state and messenger entries.</summary>
    /// <param name="project">The isolated project.</param>
    private static void ClearProject(dynamic project)
    {
        foreach (IEnumerable collection in (IEnumerable)project.ElementCollections)
        {
            foreach (object element in collection) _messenger!.Clear(element);
            _messenger!.Clear((object)collection);
        }
        project.Close();
        _messenger!.Clear((object)project);
    }

    /// <summary>Hashes every logical cell and retains schema, row order, and column identity.</summary>
    /// <param name="path">The copied project database.</param>
    /// <returns>A logical snapshot with only explicit path and modification-time normalization.</returns>
    private static object Snapshot(string path)
    {
        using var connection = (DbConnection)Activator.CreateInstance(_connectionType!, $"Data Source={path};Pooling=False;Read Only=True")!;
        connection.Open();
        using var integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check";
        string? check = Convert.ToString(integrity.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (check != "ok") throw new InvalidDataException($"Integrity check failed: {check}");
        using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = "SELECT type,name,tbl_name,sql FROM sqlite_master ORDER BY type,name";
        var schemas = new List<string?[]>();
        var tables = new List<string>();
        using (var reader = schemaCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                schemas.Add(Enumerable.Range(0, 4).Select(index => reader.IsDBNull(index) ? null : reader.GetString(index)).ToArray());
                if (reader.GetString(0) == "table") tables.Add(reader.GetString(1));
            }
        }
        var contents = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (string table in tables)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM \"{table.Replace("\"", "\"\"")}\" ORDER BY rowid";
            using var reader = command.ExecuteReader();
            string[] columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            var rows = new List<string[]>();
            while (reader.Read())
            {
                var values = new string[reader.FieldCount];
                for (int index = 0; index < values.Length; index++)
                {
                    object value = reader.GetValue(index);
                    if (columns[index] == "LastModified") values[index] = "<LAST_MODIFIED>";
                    else if (table == "Project" && columns[index] == "FullFileName") values[index] = "<PROJECT_PATH>";
                    else values[index] = HashCell(value);
                }
                rows.Add(values);
            }
            contents[table] = new { Columns = columns, Rows = rows };
        }
        return new { Integrity = check, Schemas = schemas, Tables = contents };
    }

    /// <summary>Preserves provider value identity while avoiding enormous result blobs in the report.</summary>
    /// <param name="value">The actual provider cell.</param>
    /// <returns>Type, byte length, and SHA-256 of the exact value representation.</returns>
    private static string HashCell(object value)
    {
        if (value is DBNull) return "null";
        byte[] bytes = value is byte[] blob ? blob : Encoding.UTF8.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        return $"{value.GetType().Name}:{bytes.Length}:{Convert.ToHexString(SHA256.HashData(bytes))}";
    }
}
