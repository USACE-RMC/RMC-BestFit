using System.Reflection;
using System.Text.RegularExpressions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Documentation;

/// <summary>
/// Enforces the source, API, navigation, and compiled-example contracts for the
/// peer-review technical reference.
/// </summary>
[TestClass]
public class TechnicalReferenceDocumentationTests
{
    private static readonly Regex MarkdownLinkRegex = new(
        @"!?\[[^\]]*\]\((?<target>[^)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownSnippetRegex = new(
        @"<!--\s*snippet:\s*(?<id>[A-Za-z0-9_.-]+)\s*-->\s*\r?\n```(?:cs|csharp)\s*\r?\n(?<code>.*?)\r?\n```",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SourceSnippetRegex = new(
        @"^[ \t]*#region\s+doc:(?<id>[A-Za-z0-9_.-]+)\s*\r?\n(?<code>.*?)^[ \t]*#endregion\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

    /// <summary>
    /// Verifies that documentation never imports deleted core namespaces.
    /// </summary>
    [TestMethod]
    public void Documentation_DoesNotUseDeletedNamespaces()
    {
        string docsRoot = Path.Combine(FindRepositoryRoot(), "docs");
        string[] forbiddenPatterns =
        {
            @"(?m)^\s*using\s+RMC\.BestFit;\s*$",
            @"(?m)^\s*using\s+RMC\.BestFit\.Model;\s*$",
            @"(?m)^\s*namespace\s+RMC\.BestFit;\s*$",
            @"`RMC\.BestFit`\s*\|\s*Data"
        };

        var violations = new List<string>();
        foreach (string path in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            foreach (string pattern in forbiddenPatterns)
            {
                if (Regex.IsMatch(text, pattern))
                {
                    violations.Add(Path.GetRelativePath(FindRepositoryRoot(), path));
                    break;
                }
            }
        }

        Assert.AreEqual(0, violations.Count,
            "Deleted namespace references remain in: " + string.Join(", ", violations));
    }

    /// <summary>
    /// Verifies that relative Markdown links resolve to files in the repository.
    /// </summary>
    [TestMethod]
    public void Documentation_LocalLinksResolve()
    {
        string repositoryRoot = FindRepositoryRoot();
        string docsRoot = Path.Combine(repositoryRoot, "docs");
        var brokenLinks = new List<string>();

        foreach (string path in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            foreach (Match match in MarkdownLinkRegex.Matches(text))
            {
                string target = match.Groups["target"].Value.Trim().Trim('<', '>');
                int titleSeparator = target.IndexOf(" \"", StringComparison.Ordinal);
                if (titleSeparator >= 0)
                {
                    target = target[..titleSeparator];
                }

                if (target.Length == 0 || target.StartsWith('#') ||
                    Uri.TryCreate(target, UriKind.Absolute, out _))
                {
                    continue;
                }

                string relativeTarget = Uri.UnescapeDataString(target.Split('#')[0])
                    .Replace('/', Path.DirectorySeparatorChar);
                string resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, relativeTarget));
                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                {
                    brokenLinks.Add($"{Path.GetRelativePath(repositoryRoot, path)} -> {target}");
                }
            }
        }

        Assert.AreEqual(0, brokenLinks.Count,
            "Broken local documentation links:" + Environment.NewLine + string.Join(Environment.NewLine, brokenLinks));
    }

    /// <summary>
    /// Verifies that every C# block on a completed page is copied exactly from a
    /// source region that compiles as part of this test project.
    /// </summary>
    [TestMethod]
    public void CompletedPages_CSharpBlocksMatchCompiledSourceRegions()
    {
        string repositoryRoot = FindRepositoryRoot();
        string docsRoot = Path.Combine(repositoryRoot, "docs");
        IReadOnlyDictionary<string, string> sourceSnippets = LoadSourceSnippets(repositoryRoot);
        var failures = new List<string>();

        foreach (string path in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!text.Contains("<!-- technical-reference-status: complete -->", StringComparison.Ordinal))
            {
                continue;
            }

            int csharpBlockCount = Regex.Matches(text, @"(?m)^```(?:cs|csharp)\s*$").Count;
            MatchCollection snippetMatches = MarkdownSnippetRegex.Matches(text);
            if (csharpBlockCount != snippetMatches.Count)
            {
                failures.Add($"{Path.GetRelativePath(repositoryRoot, path)} has {csharpBlockCount} C# blocks but {snippetMatches.Count} snippet markers.");
            }

            foreach (Match snippet in snippetMatches)
            {
                string id = snippet.Groups["id"].Value;
                if (!sourceSnippets.TryGetValue(id, out string? sourceCode))
                {
                    failures.Add($"{Path.GetRelativePath(repositoryRoot, path)} references missing snippet '{id}'.");
                    continue;
                }

                string markdownCode = NormalizeSnippet(snippet.Groups["code"].Value);
                if (!string.Equals(markdownCode, sourceCode, StringComparison.Ordinal))
                {
                    failures.Add($"{Path.GetRelativePath(repositoryRoot, path)} does not match compiled snippet '{id}'.");
                }
            }
        }

        Assert.AreEqual(0, failures.Count,
            "Documentation snippet failures:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies that all exported scientific types have a reviewer-facing entry in
    /// the API traceability matrix.
    /// </summary>
    [TestMethod]
    public void TraceabilityMatrix_CoversAllExportedScientificTypes()
    {
        string traceabilityPath = Path.Combine(
            FindRepositoryRoot(), "docs", "technical-reference", "api-traceability.md");
        string traceability = File.ReadAllText(traceabilityPath);

        Type[] scientificTypes = typeof(IModel).Assembly.GetExportedTypes()
            .Where(type => type.Namespace is not null &&
                (type.Namespace.StartsWith("RMC.BestFit.Models", StringComparison.Ordinal) ||
                 type.Namespace.StartsWith("RMC.BestFit.Analyses", StringComparison.Ordinal) ||
                 type.Namespace.StartsWith("RMC.BestFit.Estimation", StringComparison.Ordinal) ||
                 type.Namespace.StartsWith("RMC.BestFit.Diagnostics", StringComparison.Ordinal)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        string[] missing = scientificTypes
            .Where(type => !Regex.IsMatch(
                traceability,
                $@"(?<![A-Za-z0-9_]){Regex.Escape(type.Name.Split('`')[0])}(?![A-Za-z0-9_])"))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.AreEqual(0, missing.Length,
            "Exported scientific types missing from api-traceability.md:" +
            Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// Verifies reference-anchor integrity on completed technical-reference pages.
    /// </summary>
    [TestMethod]
    public void CompletedPages_CitationAnchorsResolve()
    {
        string repositoryRoot = FindRepositoryRoot();
        string technicalRoot = Path.Combine(repositoryRoot, "docs", "technical-reference");
        var failures = new List<string>();

        foreach (string path in Directory.EnumerateFiles(technicalRoot, "*.md", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!text.Contains("<!-- technical-reference-status: complete -->", StringComparison.Ordinal))
            {
                continue;
            }

            string[] citations = Regex.Matches(text, @"\[[0-9]+\]\(#ref-(?<id>[0-9]+)\)")
                .Select(match => match.Groups["id"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (string citation in citations)
            {
                if (!text.Contains($"<a id=\"ref-{citation}\"></a>", StringComparison.Ordinal))
                {
                    failures.Add($"{Path.GetRelativePath(repositoryRoot, path)} is missing anchor ref-{citation}.");
                }
            }
        }

        Assert.AreEqual(0, failures.Count,
            "Citation anchor failures:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static IReadOnlyDictionary<string, string> LoadSourceSnippets(string repositoryRoot)
    {
        string sourceRoot = Path.Combine(
            repositoryRoot, "src", "RMC.BestFit.Tests", "Documentation", "Examples");
        var snippets = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            foreach (Match match in SourceSnippetRegex.Matches(text))
            {
                string id = match.Groups["id"].Value;
                Assert.IsFalse(snippets.ContainsKey(id), $"Duplicate compiled documentation snippet id '{id}'.");
                snippets[id] = NormalizeSnippet(match.Groups["code"].Value);
            }
        }

        return snippets;
    }

    private static string NormalizeSnippet(string value)
    {
        string[] lines = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim('\n', '\r')
            .Split('\n');
        int commonIndent = lines
            .Where(line => line.Trim().Length > 0)
            .Select(line => line.TakeWhile(char.IsWhiteSpace).Count())
            .DefaultIfEmpty(0)
            .Min();

        return string.Join("\n", lines.Select(line =>
            line.Length >= commonIndent ? line[commonIndent..].TrimEnd() : line.TrimEnd()));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RMC.BestFit.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the RMC-BestFit repository root.");
    }
}
