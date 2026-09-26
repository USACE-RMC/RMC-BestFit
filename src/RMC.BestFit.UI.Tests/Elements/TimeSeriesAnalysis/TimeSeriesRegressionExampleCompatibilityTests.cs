using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace RMC.BestFit.UI.Tests.Elements.TimeSeriesAnalysis;

/// <summary>
/// End-to-end open check for the time-series regression example project.
/// </summary>
[TestClass]
public class TimeSeriesRegressionExampleCompatibilityTests
{
    /// <summary>
    /// Verifies that opening a temporary copy of the example keeps each saved ARIMAX model,
    /// including its covariate coefficients, exactly as stored.
    /// </summary>
    /// <remarks>
    /// The residuals of the multiple-regression analysis at the restored coefficients reproduce
    /// the fitted residual RMS of 0.341147 recorded for the example; rebuilding default
    /// coefficients when the covariates were reattached on open gave 0.660229.
    /// </remarks>
    [STATestMethod]
    [DoNotParallelize]
    public void RegressionExample_Open_KeepsSavedCovariateModels()
    {
        string source = FindExampleProject();
        string originalHash = ComputeHash(source);
        Dictionary<string, string> savedModels = ReadSavedModels(source);
        Assert.AreEqual(2, savedModels.Count);
        string copy = Path.Combine(Path.GetTempPath(), $"BestFit-TimeSeriesRegression-{Guid.NewGuid():N}.bestfit");
        File.Copy(source, copy);
        try
        {
            BestFitProject opened = CreateIsolatedProject(copy);
            opened.Open();

            UI.TimeSeriesAnalysis[] analyses = opened.ElementCollections!
                .OfType<TimeSeriesAnalysisCollection>().Single().OfType<UI.TimeSeriesAnalysis>().ToArray();
            Assert.AreEqual(2, analyses.Length);
            foreach (UI.TimeSeriesAnalysis analysis in analyses)
            {
                Assert.IsTrue(savedModels.TryGetValue(analysis.Name, out string? saved), analysis.Name);
                Assert.IsTrue(XNode.DeepEquals(XElement.Parse(saved), analysis.ARIMAX.ToXElement()),
                    analysis.Name + ": the opened model must match its saved cell.");
            }

            UI.TimeSeriesAnalysis multiple = analyses.Single(analysis => analysis.Name == "Multiple Linear Regression");
            double[] residuals = multiple.ARIMAX
                .Residuals(multiple.ARIMAX.Parameters.Select(parameter => parameter.Value).ToArray())
                .Where(double.IsFinite)
                .ToArray();
            Assert.AreEqual(0.341147, Math.Sqrt(residuals.Average(residual => residual * residual)), 5E-7,
                "The residual RMS at the restored coefficients must match the fitted value.");
        }
        finally
        {
            SQLiteConnection.ClearAllPools();
            foreach (string candidate in new[] { copy, copy + "-wal", copy + "-shm" })
            {
                if (File.Exists(candidate)) File.Delete(candidate);
            }
            Assert.AreEqual(originalHash, ComputeHash(source), "The original example project must remain unchanged.");
        }
    }

    /// <summary>
    /// Creates a test-local project instance without replacing the application's singleton.
    /// </summary>
    /// <param name="path">The temporary project path.</param>
    /// <returns>An isolated project instance targeting the temporary copy.</returns>
    private static BestFitProject CreateIsolatedProject(string path)
    {
        var project = (BestFitProject)Activator.CreateInstance(typeof(BestFitProject), nonPublic: true)!;
        project.FullFileName = path;
        return project;
    }

    /// <summary>
    /// Finds the checked-in time-series regression example relative to the test output directory.
    /// </summary>
    /// <returns>The example project's absolute path.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the source checkout cannot be located.</exception>
    private static string FindExampleProject()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "examples", "7-time-series-analysis",
                "3-time-series-regression-example", "time-series-regression-example.bestfit");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("The time-series regression example project was not found in the source checkout.");
    }

    /// <summary>
    /// Computes a SHA-256 hash without modifying the file.
    /// </summary>
    /// <param name="path">The file to hash.</param>
    /// <returns>The hexadecimal SHA-256 hash.</returns>
    private static string ComputeHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>
    /// Reads each saved ARIMAX model through a read-only SQLite connection.
    /// </summary>
    /// <param name="path">The project to inspect.</param>
    /// <returns>The saved model XML keyed by analysis name.</returns>
    private static Dictionary<string, string> ReadSavedModels(string path)
    {
        var models = new Dictionary<string, string>(StringComparer.Ordinal);
        using var connection = new SQLiteConnection($"Data Source={path};Read Only=True;");
        connection.Open();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Name, ARIMAX FROM 'Time Series Analysis' ORDER BY rowid";
        using SQLiteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            models.Add(reader.GetString(0), reader.GetString(1));
        }
        return models;
    }
}
