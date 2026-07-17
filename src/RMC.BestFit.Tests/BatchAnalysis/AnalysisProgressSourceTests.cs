using System.Runtime.CompilerServices;

namespace RMC.BestFit.Tests.BatchAnalysis;

/// <summary>
/// Source-level regression tests for analysis progress phase ordering.
/// </summary>
[TestClass]
public class AnalysisProgressSourceTests
{
    /// <summary>
    /// Verifies Bayesian-backed run methods report final completion after setting estimated state.
    /// </summary>
    [TestMethod]
    public void BayesianRunMethods_ReportCompleteAfterEstimatedState()
    {
        string[] files =
        {
            "src/RMC.BestFit/Analyses/Univariate/UnivariateAnalysis.cs",
            "src/RMC.BestFit/Analyses/Univariate/PointProcessAnalysis.cs",
            "src/RMC.BestFit/Analyses/Univariate/MixtureAnalysis.cs",
            "src/RMC.BestFit/Analyses/Univariate/CompetingRiskAnalysis.cs",
            "src/RMC.BestFit/Analyses/Bivariate/BivariateAnalysis.cs",
            "src/RMC.BestFit/Analyses/RatingCurve/RatingCurveAnalysis.cs",
            "src/RMC.BestFit/Analyses/TimeSeries/ARAnalysis.cs",
            "src/RMC.BestFit/Analyses/TimeSeries/ARIMAAnalysis.cs",
            "src/RMC.BestFit/Analyses/TimeSeries/ARIMAXAnalysis.cs",
            "src/RMC.BestFit/Analyses/TimeSeries/MAAnalysis.cs",
            "src/RMC.BestFit/Analyses/SpatialExtremes/SpatialGEVAnalysis.cs"
        };

        foreach (string file in files)
        {
            string method = ExtractMethodSource(ReadRepoFile(file), "RunAsync");
            AssertSourceOrder(method, "AnalysisProgress.ReportProcessingResults(progressReporter);", "IsEstimated = BayesianAnalysis.IsEstimated;", file);
            AssertSourceOrder(method, "IsEstimated = BayesianAnalysis.IsEstimated;", "AnalysisProgress.ReportComplete(progressReporter);", file);
        }
    }

    /// <summary>
    /// Verifies B17C final completion is reported after final property notifications.
    /// </summary>
    [TestMethod]
    public void Bulletin17C_RunAsync_ReportsCompleteAfterFinalNotifications()
    {
        string method = ExtractMethodSource(
            ReadRepoFile("src/RMC.BestFit/Analyses/Univariate/Bulletin17CAnalysis.cs"),
            "RunAsync");

        AssertSourceOrder(method, "AnalysisProgress.ReportProcessingResults(progressReporter);", "await CreateFrequencyAnalysisResultsAsync();", "B17C");
        AssertSourceOrder(method, "RaisePropertyChange(nameof(GMM));", "AnalysisProgress.ReportComplete(progressReporter);", "B17C");
        StringAssert.Contains(method, "progressReporter?.ReportProgress(1);");
        StringAssert.Contains(method, "AnalysisProgress.CreatePhaseReporter(progressReporter, 1, 98, \"B17C Uncertainty\")");
    }

    /// <summary>
    /// Verifies result-only dependent analyses report final completion from their run methods.
    /// </summary>
    [TestMethod]
    public void DependentRunMethods_ReportCompleteAfterEstimatedNotification()
    {
        AssertDependentRunMethodOrdering(
            "src/RMC.BestFit/Analyses/Univariate/CompositeAnalysis.cs",
            "CreateFrequencyAnalysisResultsAsync");
        AssertDependentRunMethodOrdering(
            "src/RMC.BestFit/Analyses/Bivariate/CoincidentFrequencyAnalysis.cs",
            "CreateFrequencyAnalysisResultsAsync");
    }

    /// <summary>
    /// Asserts ordering for a dependent result-only analysis.
    /// </summary>
    /// <param name="relativePath">The repository-relative source path.</param>
    /// <param name="resultMethodName">The result-building method name.</param>
    private static void AssertDependentRunMethodOrdering(string relativePath, string resultMethodName)
    {
        string source = ReadRepoFile(relativePath);
        string runMethod = ExtractMethodSource(source, "RunAsync");
        string resultMethod = ExtractMethodSource(source, "public async Task " + resultMethodName);

        AssertSourceOrder(runMethod, "RaisePropertyChange(nameof(IsEstimated));", "AnalysisProgress.ReportComplete(progressReporter);", relativePath);
        Assert.IsFalse(resultMethod.Contains("AnalysisProgress.ReportComplete(progressReporter);", StringComparison.Ordinal),
            relativePath + " should not report final completion from the result-building method.");
    }

    /// <summary>
    /// Asserts that one source fragment appears before another.
    /// </summary>
    /// <param name="source">The source text to inspect.</param>
    /// <param name="first">The expected earlier fragment.</param>
    /// <param name="second">The expected later fragment.</param>
    /// <param name="context">Context shown in assertion failures.</param>
    private static void AssertSourceOrder(string source, string first, string second, string context)
    {
        int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
        int secondIndex = source.IndexOf(second, StringComparison.Ordinal);

        Assert.IsTrue(firstIndex >= 0, context + " should contain: " + first);
        Assert.IsTrue(secondIndex >= 0, context + " should contain: " + second);
        Assert.IsTrue(firstIndex < secondIndex, context + " should order '" + first + "' before '" + second + "'.");
    }

    /// <summary>
    /// Reads a file from the repository root.
    /// </summary>
    /// <param name="relativePath">The repository-relative path.</param>
    /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
    /// <returns>The file contents.</returns>
    private static string ReadRepoFile(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(sourceFilePath), relativePath), System.Text.Encoding.Default);
    }

    /// <summary>
    /// Extracts a method body from C# source using brace counting.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="methodName">The method name to extract.</param>
    /// <returns>The method source, including signature and braces.</returns>
    private static string ExtractMethodSource(string source, string methodName)
    {
        int start = source.IndexOf(methodName + "(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, methodName + " should exist.");

        int openingBrace = source.IndexOf('{', start);
        Assert.IsTrue(openingBrace > start, methodName + " should have an opening brace.");

        int depth = 0;
        for (int i = openingBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(start, i - start + 1);
                }
            }
        }

        Assert.Fail(methodName + " should have a closing brace.");
        return string.Empty;
    }

    /// <summary>
    /// Finds the repository root from the test file path.
    /// </summary>
    /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
    /// <returns>The repository root path.</returns>
    private static string FindRepoRoot(string sourceFilePath)
    {
        DirectoryInfo? directory = new FileInfo(sourceFilePath).Directory;
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RMC.BestFit.sln")))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "Repository root could not be found.");
        return directory!.FullName;
    }
}
