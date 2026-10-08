using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Sampling.MCMC;
using RMC.BestFit.UI;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// End-to-end compatibility checks for the supplied version-2 point-process example project.
/// </summary>
[TestClass]
public class PointProcessProjectCompatibilityTests
{
    /// <summary>
    /// Verifies all three saved point-process analyses and their model/result content survive
    /// opening, saving, and reopening a temporary copy without changing the original example.
    /// </summary>
    [STATestMethod]
    [DoNotParallelize]
    public void Version2Example_OpenSaveReopen_PreservesAllPointProcessContent()
    {
        string source = FindExampleProject();
        string originalHash = ComputeHash(source);
        string copy = Path.Combine(Path.GetTempPath(), $"BestFit-V2PointProcess-{Guid.NewGuid():N}.bestfit");
        var expected = ReadPointProcessRows(source);
        Assert.AreEqual(3, expected.Count);
        File.Copy(source, copy);
        try
        {
            BestFitProject opened = CreateIsolatedProject(copy);
            opened.Open();
            AssertProjectContent(expected, opened);

            foreach (PointProcessAnalysis analysis in opened.ElementCollections!
                .OfType<UnivariateAnalysisCollection>().Single().OfType<PointProcessAnalysis>())
            {
                analysis.Save();
            }
            opened.Save();

            var saved = ReadPointProcessRows(copy);
            Assert.AreEqual(3, saved.Count);
            foreach (var entry in expected)
            {
                Assert.IsTrue(saved.TryGetValue(entry.Key, out var actual));
                AssertXmlContentPreserved(XElement.Parse(entry.Value.ModelXml), XElement.Parse(actual.ModelXml));
                AssertXmlContentPreserved(XElement.Parse(entry.Value.BayesianXml), XElement.Parse(actual.BayesianXml));
                AssertXmlContentPreserved(XElement.Parse(entry.Value.ResultsXml), XElement.Parse(actual.ResultsXml));
                AssertXmlContentPreserved(XElement.Parse(entry.Value.AnalysisXml), XElement.Parse(actual.AnalysisXml));
                AssertEquivalentValue(entry.Value.ProbabilityOrdinates, actual.ProbabilityOrdinates, entry.Key + "/ProbabilityOrdinates");
                using JsonDocument expectedMcmc = JsonDocument.Parse(Numerics.Tools.Decompress(entry.Value.McmcBlob));
                using JsonDocument actualMcmc = JsonDocument.Parse(Numerics.Tools.Decompress(actual.McmcBlob));
                AssertJsonContentPreserved(expectedMcmc.RootElement, actualMcmc.RootElement, entry.Key + "/MCMCResults");
            }

            BestFitProject reopened = CreateIsolatedProject(copy);
            reopened.Open();
            AssertProjectContent(expected, reopened);
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
    /// Finds the checked-in version-2 project relative to the test output directory.
    /// </summary>
    /// <returns>The example project's absolute path.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the source checkout cannot be located.</exception>
    private static string FindExampleProject()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "examples", "4-univariate-distribution-analysis",
                "3-point-process-analysis", "point-process-examples.bestfit");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("The version-2 point-process example project was not found in the source checkout.");
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
    /// Reads persisted point-process payloads through a read-only SQLite connection.
    /// </summary>
    /// <param name="path">The project to inspect.</param>
    /// <returns>The saved payloads keyed by analysis name.</returns>
    private static Dictionary<string, (string ModelXml, string BayesianXml, string ResultsXml, string AnalysisXml,
        string ProbabilityOrdinates, byte[] McmcBlob)> ReadPointProcessRows(string path)
    {
        var rows = new Dictionary<string, (string, string, string, string, string, byte[])>(StringComparer.Ordinal);
        using var connection = new SQLiteConnection($"Data Source={path};Read Only=True;");
        connection.Open();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Name, PointProcess, BayesianAnalysis, AnalysisResults, AnalysisXml, ProbabilityOrdinates, MCMCResults FROM '<Point Process>' ORDER BY rowid";
        using SQLiteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(reader.GetString(0), (reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), (byte[])reader.GetValue(6)));
        }
        return rows;
    }

    /// <summary>
    /// Checks in-memory models and results against every persisted version-2 field.
    /// </summary>
    /// <param name="expected">The original saved payloads.</param>
    /// <param name="project">The opened temporary project.</param>
    private static void AssertProjectContent(
        Dictionary<string, (string ModelXml, string BayesianXml, string ResultsXml, string AnalysisXml,
            string ProbabilityOrdinates, byte[] McmcBlob)> expected,
        BestFitProject project)
    {
        PointProcessAnalysis[] analyses = project.ElementCollections!
            .OfType<UnivariateAnalysisCollection>().Single().OfType<PointProcessAnalysis>().ToArray();
        Assert.AreEqual(3, analyses.Length);
        foreach (PointProcessAnalysis analysis in analyses)
        {
            Assert.IsTrue(expected.TryGetValue(analysis.Name, out var original));
            Assert.IsTrue(analysis.IsEstimated, analysis.Name + " lost its estimated state.");
            Assert.IsNotNull(analysis.BayesianAnalysis.Results, analysis.Name + " lost its MCMC results.");
            Assert.IsNotNull(analysis.AnalysisResults, analysis.Name + " lost its saved curves.");
            AssertXmlContentPreserved(XElement.Parse(original.ModelXml), analysis.PointProcess.ToXElement());
            AssertXmlContentPreserved(XElement.Parse(original.BayesianXml), analysis.BayesianAnalysis.ToXElement());
            AssertXmlContentPreserved(XElement.Parse(original.ResultsXml), analysis.AnalysisResults.ToXElement());
            AssertXmlContentPreserved(XElement.Parse(original.AnalysisXml),
                ((RMC.BestFit.Analyses.PointProcessAnalysis)analysis.InnerAnalysis).ToXElement());
            using JsonDocument expectedMcmc = JsonDocument.Parse(Numerics.Tools.Decompress(original.McmcBlob));
            using JsonDocument actualMcmc = JsonDocument.Parse(MCMCResults.ToByteArray(analysis.BayesianAnalysis.Results!));
            AssertJsonContentPreserved(expectedMcmc.RootElement, actualMcmc.RootElement, analysis.Name + "/MCMCResults");
        }
    }

    /// <summary>
    /// Compares XML semantically, permitting only recognized correlation placeholders and new
    /// optional seed/exposure-origin attributes to differ.
    /// </summary>
    /// <param name="expected">The original XML.</param>
    /// <param name="actual">The restored or reserialized XML.</param>
    private static void AssertXmlContentPreserved(XElement expected, XElement actual)
    {
        expected = new XElement(expected);
        actual = new XElement(actual);
        NormalizeMatrixPlaceholders(expected);
        NormalizeMatrixPlaceholders(actual);
        Assert.AreEqual(expected.Name, actual.Name);
        foreach (XAttribute attribute in expected.Attributes())
        {
            XAttribute? restored = actual.Attribute(attribute.Name);
            Assert.IsNotNull(restored, $"Missing {expected.Name}/@{attribute.Name}.");
            AssertEquivalentValue(attribute.Value, restored.Value, expected.Name + "/@" + attribute.Name);
        }
        foreach (XAttribute attribute in actual.Attributes().Where(attribute => expected.Attribute(attribute.Name) == null))
        {
            Assert.IsTrue(
                (actual.Name.LocalName == "Distribution" && attribute.Name.LocalName == "PRNGSeed") ||
                (actual.Name.LocalName == "PointProcessModel" && attribute.Name.LocalName == "IsTotalYearsInferred"),
                $"Unexpected serialized metadata {actual.Name}/@{attribute.Name}.");
        }
        XElement[] expectedChildren = expected.Elements().ToArray();
        XElement[] actualChildren = actual.Elements().ToArray();
        Assert.AreEqual(expectedChildren.Length, actualChildren.Length, expected.Name + " child count changed.");
        for (int index = 0; index < expectedChildren.Length; index++)
            AssertXmlContentPreserved(expectedChildren[index], actualChildren[index]);
        if (expectedChildren.Length == 0)
            AssertEquivalentValue(expected.Value.Trim(), actual.Value.Trim(), expected.Name.ToString());
    }

    /// <summary>
    /// Removes only the empty and all-zero correlation representations approved for normalization.
    /// </summary>
    /// <param name="element">The test-local XML copy to normalize.</param>
    private static void NormalizeMatrixPlaceholders(XElement element)
    {
        foreach (XElement matrix in element.Descendants("CorrelationMatrix").ToArray())
        {
            XElement[] rows = matrix.Elements("Correlation_Row").ToArray();
            if (rows.Length == 0 && string.IsNullOrWhiteSpace(matrix.Value))
            {
                matrix.Remove();
                continue;
            }
            if (rows.Length > 0 && rows.All(row => row.Value.Split('|').Length == rows.Length) &&
                rows.SelectMany(row => row.Value.Split('|')).All(value =>
                    double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) && parsed == 0d))
            {
                matrix.Remove();
            }
        }
    }

    /// <summary>
    /// Compares scalar and delimited numeric values independently of XML formatting.
    /// </summary>
    /// <param name="expected">The original value.</param>
    /// <param name="actual">The restored value.</param>
    /// <param name="context">The field being compared.</param>
    private static void AssertEquivalentValue(string expected, string actual, string context)
    {
        if (expected == actual) return;
        if (bool.TryParse(expected, out bool expectedBoolean) && bool.TryParse(actual, out bool actualBoolean))
        {
            Assert.AreEqual(expectedBoolean, actualBoolean, context);
            return;
        }
        string[] expectedTokens = expected.Split(new[] { '|', ',' });
        string[] actualTokens = actual.Split(new[] { '|', ',' });
        Assert.AreEqual(expectedTokens.Length, actualTokens.Length, context);
        for (int index = 0; index < expectedTokens.Length; index++)
        {
            if (double.TryParse(expectedTokens[index], NumberStyles.Any, CultureInfo.InvariantCulture, out double expectedNumber) &&
                double.TryParse(actualTokens[index], NumberStyles.Any, CultureInfo.InvariantCulture, out double actualNumber))
            {
                Assert.IsTrue(expectedNumber.Equals(actualNumber), context + " numeric value changed.");
            }
            else
            {
                Assert.AreEqual(expectedTokens[index], actualTokens[index], context);
            }
        }
    }

    /// <summary>
    /// Verifies every saved JSON field and array item survives MCMC result restoration, allowing
    /// only additional optional object properties in the newer serializer.
    /// </summary>
    /// <param name="expected">The original JSON value.</param>
    /// <param name="actual">The restored JSON value.</param>
    /// <param name="context">The field path being compared.</param>
    private static void AssertJsonContentPreserved(JsonElement expected, JsonElement actual, string context)
    {
        Assert.AreEqual(expected.ValueKind, actual.ValueKind, context);
        if (expected.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in expected.EnumerateObject())
            {
                Assert.IsTrue(actual.TryGetProperty(property.Name, out JsonElement restored), context + "/" + property.Name);
                AssertJsonContentPreserved(property.Value, restored, context + "/" + property.Name);
            }
        }
        else if (expected.ValueKind == JsonValueKind.Array)
        {
            Assert.AreEqual(expected.GetArrayLength(), actual.GetArrayLength(), context);
            JsonElement.ArrayEnumerator actualItems = actual.EnumerateArray();
            int index = 0;
            foreach (JsonElement expectedItem in expected.EnumerateArray())
            {
                Assert.IsTrue(actualItems.MoveNext(), context);
                AssertJsonContentPreserved(expectedItem, actualItems.Current, context + "/" + index);
                index++;
            }
        }
        else if (expected.ValueKind == JsonValueKind.Number)
        {
            Assert.IsTrue(expected.GetDouble().Equals(actual.GetDouble()), context);
        }
        else
        {
            Assert.AreEqual(expected.ToString(), actual.ToString(), context);
        }
    }
}
