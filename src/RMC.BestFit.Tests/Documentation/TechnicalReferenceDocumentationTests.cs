using System.Reflection;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// Verifies that both publication manifests resolve, contain ready pages, and
    /// exclude internal working records.
    /// </summary>
    [TestMethod]
    public void PublicationBookManifests_ContainOnlyPublicationReadyPages()
    {
        string repositoryRoot = FindRepositoryRoot();
        string technicalRoot = Path.Combine(repositoryRoot, "docs", "technical-reference");
        string verificationRoot = Path.Combine(repositoryRoot, "docs", "verification");
        string[] technicalPages = LoadPublicationManifest(technicalRoot);
        string[] verificationPages = LoadPublicationManifest(verificationRoot);

        Assert.AreEqual(technicalPages.Length, technicalPages.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "The technical-reference publication manifest contains duplicate pages.");
        Assert.AreEqual(verificationPages.Length, verificationPages.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "The verification-report publication manifest contains duplicate pages.");
        Assert.IsFalse(technicalPages.Any(path => path.EndsWith("review-findings.md", StringComparison.OrdinalIgnoreCase)),
            "The technical-reference publication manifest must exclude the internal review-findings register.");
        Assert.IsTrue(verificationPages.All(path =>
                Path.GetRelativePath(verificationRoot, path).StartsWith("report" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)),
            "Every verification-report publication page must reside under docs/verification/report.");

        AssertRelativeOrder(technicalRoot, technicalPages, new[]
        {
            "data/time-series-data.md", "data/input-data.md", "analysis/distribution-fitting.md",
            "analysis/univariate.md", "analysis/bulletin-17c.md", "distributions/point-process.md",
            "distributions/competing-risks.md", "distributions/mixture.md", "distributions/composite.md",
            "analysis/bivariate.md", "analysis/coincident-frequency.md", "analysis/rating-curve.md",
            "analysis/autoregressive.md", "analysis/moving-average.md", "analysis/arima.md",
            "analysis/arimax.md", "spatial/spatial-extremes.md"
        });
        AssertRelativeOrder(verificationRoot, verificationPages, new[]
        {
            "report/time-series-data.md", "report/input-data.md", "report/data-distributions-b17c.md",
            "report/point-process-analysis.md", "report/competing-risk-analysis.md",
            "report/mixture-analysis.md", "report/composite-analysis.md", "report/bivariate-analyses.md",
            "report/rating-curve.md", "report/time-series-analyses.md", "report/spatial-extremes.md"
        });

        foreach (string path in technicalPages)
        {
            StringAssert.Contains(File.ReadAllText(path), "<!-- technical-reference-status: complete -->",
                $"Technical-reference publication page is not complete: {Path.GetRelativePath(repositoryRoot, path)}");
        }

        foreach (string path in verificationPages)
        {
            StringAssert.Contains(File.ReadAllText(path), "<!-- verification-status: publication-draft -->",
                $"Verification-report publication page is not ready: {Path.GetRelativePath(repositoryRoot, path)}");
        }

        string[] omittedCompletePages = Directory.EnumerateFiles(technicalRoot, "*.md", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("<!-- technical-reference-status: complete -->", StringComparison.Ordinal))
            .Except(technicalPages, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.AreEqual(0, omittedCompletePages.Length,
            "Completed technical-reference pages missing from the publication manifest:" +
            Environment.NewLine + string.Join(Environment.NewLine, omittedCompletePages));
    }

    /// <summary>
    /// Verifies that public manuscripts do not expose internal issue identifiers,
    /// findings registers, or phase-closeout chronology.
    /// </summary>
    [TestMethod]
    public void PublicationBooks_ExcludeInternalReviewHistory()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] publicPages = LoadPublicationManifest(
                Path.Combine(repositoryRoot, "docs", "technical-reference"))
            .Concat(LoadPublicationManifest(Path.Combine(repositoryRoot, "docs", "verification")))
            .ToArray();
        string[] forbiddenPatterns =
        {
            @"(?<!RMC-)\bTR-[0-9]{3}\b",
            @"\breview-findings\b",
            @"\breview findings register\b",
            @"\bverification-finalization-plan\b",
            @"\btest-inventory\b",
            @"\bpre-correction\b",
            @"\bcloseout\b",
            @"\bPhase [0-9]+\b",
            @"\bBatch [0-9]+(?:\.[0-9]+)?\b"
        };
        var violations = new List<string>();

        foreach (string path in publicPages)
        {
            string text = File.ReadAllText(path);
            foreach (string pattern in forbiddenPatterns)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                {
                    violations.Add($"{Path.GetRelativePath(repositoryRoot, path)} matches {pattern}");
                }
            }
        }

        Assert.AreEqual(0, violations.Count,
            "Internal review history leaked into a publication manuscript:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Verifies that local fragment links in both publication manuscripts resolve
    /// to a heading or explicit anchor.
    /// </summary>
    [TestMethod]
    public void PublicationBooks_FragmentLinksResolve()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] publicPages = LoadPublicationManifest(
                Path.Combine(repositoryRoot, "docs", "technical-reference"))
            .Concat(LoadPublicationManifest(Path.Combine(repositoryRoot, "docs", "verification")))
            .ToArray();
        var failures = new List<string>();

        foreach (string sourcePath in publicPages)
        {
            string source = File.ReadAllText(sourcePath);
            foreach (Match match in MarkdownLinkRegex.Matches(source))
            {
                string target = match.Groups["target"].Value.Trim().Trim('<', '>');
                int titleSeparator = target.IndexOf(" \"", StringComparison.Ordinal);
                if (titleSeparator >= 0)
                {
                    target = target[..titleSeparator];
                }

                int fragmentSeparator = target.IndexOf('#');
                if (fragmentSeparator < 0 || Uri.TryCreate(target, UriKind.Absolute, out _))
                {
                    continue;
                }

                string filePart = Uri.UnescapeDataString(target[..fragmentSeparator]);
                string fragment = Uri.UnescapeDataString(target[(fragmentSeparator + 1)..]);
                if (fragment.Length == 0)
                {
                    continue;
                }

                string targetPath = filePart.Length == 0
                    ? sourcePath
                    : Path.GetFullPath(Path.Combine(
                        Path.GetDirectoryName(sourcePath)!,
                        filePart.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(targetPath) || !LoadMarkdownAnchors(targetPath).Contains(fragment))
                {
                    failures.Add($"{Path.GetRelativePath(repositoryRoot, sourcePath)} -> {target}");
                }
            }
        }

        Assert.AreEqual(0, failures.Count,
            "Broken publication fragment links:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies that the controlled metadata agrees with both report front matters.
    /// </summary>
    [TestMethod]
    public void PublicationMetadata_MatchesReportFrontMatter()
    {
        string repositoryRoot = FindRepositoryRoot();
        string metadataPath = Path.Combine(repositoryRoot, "docs", "report-metadata.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(metadataPath));
        JsonElement metadata = document.RootElement;
        string technicalFrontMatter = File.ReadAllText(Path.Combine(
            repositoryRoot, "docs", "technical-reference", "front-matter.md"));
        string verificationFrontMatter = File.ReadAllText(Path.Combine(
            repositoryRoot, "docs", "verification", "report", "report-documentation.md"));
        foreach ((string reportKey, string frontMatter) in new[]
                 {
                     ("technical_reference", technicalFrontMatter),
                     ("verification_report", verificationFrontMatter)
                 })
        {
            JsonElement report = metadata.GetProperty(reportKey);
            bool hasCheckpoint = report.TryGetProperty("checkpoint", out JsonElement checkpoint);
            foreach (string propertyName in new[]
                     {
                         "release_status", "publication_date_display", "bestfit_commit",
                         "numerics_source_commit", "numerics_package_baseline"
                     })
            {
                string expected = (hasCheckpoint && checkpoint.TryGetProperty(propertyName, out JsonElement value)
                    ? value : metadata.GetProperty(propertyName)).GetString()!;
                StringAssert.Contains(frontMatter, expected,
                    $"{reportKey} front matter does not contain controlled metadata '{propertyName}'.");
            }

            foreach (JsonElement author in metadata.GetProperty("authors").EnumerateArray())
            {
                StringAssert.Contains(frontMatter, author.GetProperty("name").GetString()!,
                    $"{reportKey} front matter omits a controlled author name.");
            }
        }
    }

    /// <summary>
    /// Verifies that each scientific verification chapter states both its evidence
    /// basis and its reported outcome.
    /// </summary>
    [TestMethod]
    public void VerificationReport_ScientificChaptersStateMethodsAndResults()
    {
        string repositoryRoot = FindRepositoryRoot();
        string reportRoot = Path.Combine(repositoryRoot, "docs", "verification", "report");
        string[] scientificChapters =
        {
            "data-distributions-b17c.md",
            "estimation-diagnostics.md",
            "point-process-analysis.md",
            "competing-risk-analysis.md",
            "mixture-analysis.md",
            "composite-analysis.md",
            "bivariate-analyses.md",
            "rating-curve.md",
            "time-series-analyses.md",
            "spatial-extremes.md"
        };
        var failures = new List<string>();

        foreach (string chapter in scientificChapters)
        {
            string text = File.ReadAllText(Path.Combine(reportRoot, chapter));
            if (!Regex.IsMatch(text, @"\b(oracle|recovery|coverage|published benchmark)\b", RegexOptions.IgnoreCase))
            {
                failures.Add($"{chapter} does not state an oracle, recovery, coverage, or published-benchmark basis.");
            }

            if (!Regex.IsMatch(text, @"\b(result|results|pass|passed)\b", RegexOptions.IgnoreCase))
            {
                failures.Add($"{chapter} does not state a result.");
            }

            if (!Regex.IsMatch(text, @"\b(tolerance|acceptance|accepted|criterion|criteria)\b", RegexOptions.IgnoreCase))
            {
                failures.Add($"{chapter} does not state an acceptance rule or tolerance.");
            }
        }

        Assert.AreEqual(0, failures.Count,
            "Verification-report evidence-contract failures:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Loads every compile-checked documentation snippet (the code between <c>#region doc:id</c> markers) under the Examples folder.
    /// </summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <returns>The normalized snippets keyed by identifier.</returns>
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

    /// <summary>
    /// Loads and resolves a publication manifest relative to its documentation root.
    /// </summary>
    /// <param name="documentationRoot">The documentation directory containing <c>book-order.txt</c>.</param>
    /// <returns>The absolute publication page paths in manifest order.</returns>
    private static string[] LoadPublicationManifest(string documentationRoot)
    {
        string manifestPath = Path.Combine(documentationRoot, "book-order.txt");
        return File.ReadAllLines(manifestPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => Path.GetFullPath(Path.Combine(
                documentationRoot,
                line.Replace('/', Path.DirectorySeparatorChar))))
            .Select(path =>
            {
                Assert.IsTrue(File.Exists(path), $"Publication manifest entry does not resolve: {path}");
                return path;
            })
            .ToArray();
    }

    /// <summary>
    /// Verifies that selected publication pages appear in the stated relative order.
    /// </summary>
    /// <param name="documentationRoot">The documentation root used to make paths relative.</param>
    /// <param name="manifestPaths">The absolute manifest paths in publication order.</param>
    /// <param name="expectedOrder">The selected relative paths in required order.</param>
    private static void AssertRelativeOrder(
        string documentationRoot,
        IReadOnlyList<string> manifestPaths,
        IReadOnlyList<string> expectedOrder)
    {
        string[] relativePaths = manifestPaths
            .Select(path => Path.GetRelativePath(documentationRoot, path).Replace('\\', '/'))
            .ToArray();
        int previousIndex = -1;
        foreach (string expectedPath in expectedOrder)
        {
            int index = Array.FindIndex(relativePaths,
                path => string.Equals(path, expectedPath, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(index > previousIndex,
                $"Publication page '{expectedPath}' is missing or outside the required software order.");
            previousIndex = index;
        }
    }

    /// <summary>
    /// Loads explicit anchors and GitHub-style heading anchors from a Markdown file.
    /// </summary>
    /// <param name="path">The Markdown file path.</param>
    /// <returns>The case-sensitive anchor identifiers declared by the file.</returns>
    private static HashSet<string> LoadMarkdownAnchors(string path)
    {
        string text = File.ReadAllText(path);
        var anchors = Regex.Matches(text, "<a\\s+(?:name|id)=['\\\"](?<id>[^'\\\"]+)['\\\"]", RegexOptions.IgnoreCase)
            .Select(match => match.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (Match heading in Regex.Matches(text, @"(?m)^#{1,6}\s+(?<title>.+?)\s*$"))
        {
            string headingText = heading.Groups["title"].Value.Trim();
            Match explicitAnchor = Regex.Match(headingText, @"\s*\{#(?<id>[^}]+)\}\s*$");
            if (explicitAnchor.Success)
            {
                anchors.Add(explicitAnchor.Groups["id"].Value);
                continue;
            }

            string baseAnchor = SlugifyMarkdownHeading(headingText);
            int occurrence = occurrences.TryGetValue(baseAnchor, out int existing) ? existing + 1 : 0;
            occurrences[baseAnchor] = occurrence;
            anchors.Add(occurrence == 0 ? baseAnchor : $"{baseAnchor}-{occurrence}");
        }

        return anchors;
    }

    /// <summary>
    /// Converts a Markdown heading to the anchor convention used by the report builder.
    /// </summary>
    /// <param name="heading">The raw Markdown heading.</param>
    /// <returns>The normalized anchor identifier.</returns>
    private static string SlugifyMarkdownHeading(string heading)
    {
        string withoutMarkup = Regex.Replace(heading, @"[`*_~<>]", string.Empty)
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        bool previousWasSeparator = false;
        foreach (char character in withoutMarkup.ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    /// <summary>
    /// Trims the blank edges of a snippet, removes the common indentation, and right-trims every line.
    /// </summary>
    /// <param name="value">The raw snippet text.</param>
    /// <returns>The normalized snippet.</returns>
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

    /// <summary>
    /// Walks up from the test output directory to the directory that contains <c>RMC.BestFit.sln</c>.
    /// </summary>
    /// <returns>The repository root path.</returns>
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
