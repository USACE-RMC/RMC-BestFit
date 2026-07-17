using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Source-level regression tests for App data grid cell style inheritance.
    /// </summary>
    [TestClass]
    public class DataGridSelectableStyleAuditTests
    {
        private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        private static readonly HashSet<string> SelectableCellStyleBases = new HashSet<string>(StringComparer.Ordinal)
        {
            "{StaticResource Left_CellStyle}",
            "{StaticResource Center_CellStyle}",
            "{StaticResource Right_CellStyle}",
            "{StaticResource Center_ReadOnly_CellStyle}",
            "{StaticResource Right_ReadOnly_CellStyle}",
            "{StaticResource CopyPasteDataGridCellStyle}",
            "{StaticResource ValidationDataGridCellStyle}"
        };

        /// <summary>
        /// Verifies every App-owned DataGridCell style inherits a selectable framework cell style.
        /// </summary>
        [TestMethod]
        public void AppDataGridCellStyles_InheritSelectableCellStyle()
        {
            var failures = new List<string>();

            foreach (string xamlFile in EnumerateAppXamlFiles())
            {
                XDocument document = XDocument.Load(xamlFile);
                foreach (XElement style in document.Descendants().Where(IsDataGridCellStyle))
                {
                    string basedOn = (string)style.Attribute("BasedOn") ?? string.Empty;
                    if (!SelectableCellStyleBases.Contains(basedOn))
                    {
                        failures.Add(RelativePath(xamlFile) + ": DataGridCell style uses BasedOn='" + basedOn + "'.");
                    }
                }
            }

            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Verifies App XAML does not use the WPF implicit DataGridCell style as a base.
        /// </summary>
        [TestMethod]
        public void AppXaml_DoesNotUseImplicitDataGridCellBaseStyle()
        {
            var failures = EnumerateAppXamlFiles()
                .Where(file => File.ReadAllText(file).Contains("BasedOn=\"{StaticResource {x:Type DataGridCell}}\"", StringComparison.Ordinal))
                .Select(RelativePath)
                .ToList();

            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Verifies local CopyPasteDataGrid row styles preserve themed row selection behavior.
        /// </summary>
        [TestMethod]
        public void CopyPasteDataGridRowStyles_InheritThemedRowStyle()
        {
            var failures = new List<string>();

            foreach (string xamlFile in EnumerateAppXamlFiles())
            {
                XDocument document = XDocument.Load(xamlFile);
                foreach (XElement rowStyle in document.Descendants().Where(element => element.Name.LocalName == "CopyPasteDataGrid.RowStyle"))
                {
                    XElement style = rowStyle.Elements().FirstOrDefault(element => element.Name.LocalName == "Style");
                    string basedOn = (string)style?.Attribute("BasedOn") ?? string.Empty;
                    if (!string.Equals(basedOn, "{StaticResource CopyPasteDataGridRowStyle}", StringComparison.Ordinal))
                    {
                        failures.Add(RelativePath(xamlFile) + ": CopyPasteDataGrid.RowStyle uses BasedOn='" + basedOn + "'.");
                    }
                }
            }

            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Verifies local KDE summary grid cell styles inherit the centered selectable cell style.
        /// </summary>
        [TestMethod]
        public void KdeCellStyles_InheritCenterCellStyle()
        {
            var failures = new List<string>();
            int styleCount = 0;

            foreach (string xamlFile in EnumerateAppXamlFiles())
            {
                XDocument document = XDocument.Load(xamlFile);
                foreach (XElement style in document.Descendants().Where(IsKdeCellStyle))
                {
                    styleCount++;
                    string basedOn = (string)style.Attribute("BasedOn") ?? string.Empty;
                    if (!string.Equals(basedOn, "{StaticResource Center_CellStyle}", StringComparison.Ordinal))
                    {
                        failures.Add(RelativePath(xamlFile) + ": KDECellStyle uses BasedOn='" + basedOn + "'.");
                    }
                }
            }

            Assert.AreEqual(3, styleCount);
            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Verifies FittingAnalysis tooltip-heavy summary columns inherit the right-aligned selectable cell style.
        /// </summary>
        [TestMethod]
        public void FittingAnalysisTooltipCellStyles_InheritSelectableCellStyles()
        {
            string xaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "RMC.BestFit.App", "GUI", "FittingAnalysis", "FittingAnalysisControl.xaml"));
            string summaryGrid = ExtractXamlElement(xaml, "<cntrls:CopyPasteDataGrid Name=\"SummaryStatisticsDataGrid\"", "</cntrls:CopyPasteDataGrid>");
            string aicBicGrid = ExtractXamlElement(xaml, "<cntrls:CopyPasteDataGrid x:Name=\"AICBICTable\"", "</cntrls:CopyPasteDataGrid>");

            Assert.AreEqual(15, CountOccurrences(summaryGrid, "BasedOn=\"{StaticResource Right_CellStyle}\""));
            Assert.AreEqual(1, CountOccurrences(aicBicGrid, "BasedOn=\"{StaticResource Left_CellStyle}\""));
            StringAssert.Contains(aicBicGrid, "BasedOn=\"{StaticResource CopyPasteDataGridRowStyle}\"");
        }

        /// <summary>
        /// Verifies the composite weight validation style keeps the themed right-aligned base style.
        /// </summary>
        [TestMethod]
        public void CompositeWeightColumnStyle_InheritsRightCellStyle()
        {
            string source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "RMC.BestFit.App", "GUI", "UnivariateAnalysis", "Composite", "CompositeAnalysisPropertiesControl.xaml.cs"));

            StringAssert.Contains(source, "new Style(typeof(DataGridCell), (Style)FindResource(\"Right_CellStyle\"))");
            Assert.IsFalse(source.Contains("var weightStyle = new Style();", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("WeightColumn.CellStyle.Setters", StringComparison.Ordinal));
        }

        /// <summary>
        /// Verifies code-behind does not construct unbased DataGridCell styles.
        /// </summary>
        [TestMethod]
        public void AppCodeBehind_DoesNotCreateUnbasedDataGridCellStyles()
        {
            var failures = new List<string>();

            foreach (string sourceFile in EnumerateAppSourceFiles())
            {
                string source = File.ReadAllText(sourceFile);
                if (source.Contains("new Style(typeof(DataGridCell))", StringComparison.Ordinal))
                {
                    failures.Add(RelativePath(sourceFile) + ": creates an unbased DataGridCell style.");
                }

                if (source.Contains("new Style();", StringComparison.Ordinal)
                    && source.Contains("CellStyle", StringComparison.Ordinal))
                {
                    failures.Add(RelativePath(sourceFile) + ": creates a parameterless style in a file that assigns cell styles.");
                }
            }

            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Determines whether an element is a DataGridCell style declaration.
        /// </summary>
        /// <param name="element">The element to inspect.</param>
        /// <returns><c>true</c> when the element is a DataGridCell style.</returns>
        private static bool IsDataGridCellStyle(XElement element)
        {
            if (element.Name.LocalName != "Style") return false;

            string targetType = (string)element.Attribute("TargetType") ?? string.Empty;
            return string.Equals(targetType, "DataGridCell", StringComparison.Ordinal)
                || string.Equals(targetType, "{x:Type DataGridCell}", StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether an element is the KDE cell style declaration.
        /// </summary>
        /// <param name="element">The element to inspect.</param>
        /// <returns><c>true</c> when the element is the KDE cell style.</returns>
        private static bool IsKdeCellStyle(XElement element)
        {
            return element.Name.LocalName == "Style"
                && string.Equals((string)element.Attribute(XamlNamespace + "Key"), "KDECellStyle", StringComparison.Ordinal);
        }

        /// <summary>
        /// Finds App XAML files in the source tree.
        /// </summary>
        /// <returns>The App XAML file paths.</returns>
        private static IEnumerable<string> EnumerateAppXamlFiles()
        {
            string appRoot = Path.Combine(FindRepoRoot(), "src", "RMC.BestFit.App");
            return Directory.EnumerateFiles(appRoot, "*.xaml", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds App C# source files in the source tree.
        /// </summary>
        /// <returns>The App source file paths.</returns>
        private static IEnumerable<string> EnumerateAppSourceFiles()
        {
            string appRoot = Path.Combine(FindRepoRoot(), "src", "RMC.BestFit.App");
            return Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Counts non-overlapping occurrences of a pattern.
        /// </summary>
        /// <param name="source">The source text to inspect.</param>
        /// <param name="pattern">The pattern to count.</param>
        /// <returns>The number of occurrences.</returns>
        private static int CountOccurrences(string source, string pattern)
        {
            return source
                .Split(new[] { pattern }, StringSplitOptions.None)
                .Length - 1;
        }

        /// <summary>
        /// Extracts a XAML element from the source text.
        /// </summary>
        /// <param name="source">The XAML source text.</param>
        /// <param name="startToken">The opening token for the element.</param>
        /// <param name="endToken">The closing token for the element.</param>
        /// <returns>The element source including its opening and closing tags.</returns>
        private static string ExtractXamlElement(string source, string startToken, string endToken)
        {
            int start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, startToken + " should exist.");

            int end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.IsTrue(end > start, endToken + " should exist.");

            return source.Substring(start, end - start + endToken.Length);
        }

        /// <summary>
        /// Converts an absolute path to a repository-relative path.
        /// </summary>
        /// <param name="path">The absolute path.</param>
        /// <returns>The repository-relative path.</returns>
        private static string RelativePath(string path)
        {
            return Path.GetRelativePath(FindRepoRoot(), path);
        }

        /// <summary>
        /// Finds the repository root from the test output directory.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The absolute repository root.</returns>
        private static string FindRepoRoot([CallerFilePath] string sourceFilePath = "")
        {
            string[] searchRoots = new[]
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(sourceFilePath)
            };

            foreach (string searchRoot in searchRoots.Where(root => !string.IsNullOrEmpty(root)))
            {
                DirectoryInfo directory = new DirectoryInfo(searchRoot);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, "src", "RMC.BestFit.App");
                    if (Directory.Exists(candidate))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException("Could not locate repository root from the test output directory.");
        }
    }
}
