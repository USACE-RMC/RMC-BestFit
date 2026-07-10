using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace RMC.BestFit.App.Tests.GUI.PropertiesControls
{
    /// <summary>
    /// Regression tests for WPF properties controls that detach their element during close.
    /// </summary>
    /// <remarks>
    /// MainProjectNode.PropertiesClosed sets each properties control Element to null. WPF bindings can
    /// raise SelectionChanged during that detach, so handlers must guard before reading Element members.
    /// </remarks>
    [TestClass]
    public class PropertiesControlCloseSafetyTests
    {
        /// <summary>
        /// Verifies SelectionChanged handlers do not dereference Element before a null guard.
        /// </summary>
        /// <remarks>
        /// This protects properties controls from close-time binding callbacks after Element has been
        /// intentionally detached.
        /// </remarks>
        [TestMethod]
        public void SelectionChangedHandlers_DereferenceElementOnlyAfterNullGuard()
        {
            string guiRoot = FindAppGuiRoot();
            var violations = new List<string>();

            foreach (string filePath in Directory.EnumerateFiles(guiRoot, "*PropertiesControl.xaml.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!Regex.IsMatch(lines[i], @"private\s+void\s+\w*SelectionChanged\s*\("))
                    {
                        continue;
                    }

                    int endLine = FindMethodEndLine(lines, i);
                    if (endLine < i)
                    {
                        violations.Add($"{RelativeToGuiRoot(guiRoot, filePath)}:{i + 1} could not be parsed.");
                        continue;
                    }

                    int guardLine = -1;
                    int elementDerefLine = -1;
                    for (int j = i; j <= endLine; j++)
                    {
                        string code = StripLineComment(lines[j]);
                        if (guardLine < 0 && Regex.IsMatch(code, @"if\s*\(\s*Element\s*==\s*null(?:\s*\|\|.*?)?\)\s*return\s*;"))
                        {
                            guardLine = j;
                        }

                        if (elementDerefLine < 0 && Regex.IsMatch(code, @"\bElement\s*\."))
                        {
                            elementDerefLine = j;
                        }
                    }

                    if (elementDerefLine >= 0 && (guardLine < 0 || guardLine > elementDerefLine))
                    {
                        violations.Add($"{RelativeToGuiRoot(guiRoot, filePath)}:{i + 1}");
                    }
                }
            }

            Assert.IsFalse(
                violations.Any(),
                "SelectionChanged handlers must guard Element before dereferencing it: " + string.Join(", ", violations));
        }

        /// <summary>
        /// Verifies the TimeSeriesAnalysis properties control detaches all tracked element handlers when unloaded.
        /// </summary>
        [TestMethod]
        public void TimeSeriesAnalysisPropertiesControl_UnloadedDetachesTrackedHandlers()
        {
            string source = File.ReadAllText(Path.Combine(FindAppGuiRoot(), "TimeSeriesAnalysis", "TimeSeriesAnalysisPropertiesControl.xaml.cs"));

            StringAssert.Contains(source, "this.Loaded += UserControl_Loaded;");
            StringAssert.Contains(source, "private void DetachElementHandlers()");
            StringAssert.Contains(source, "DetachElementHandlers();");
            StringAssert.Contains(source, "if (!ReferenceEquals(sender, Element)) return;");
            StringAssert.Contains(source, "if (!ReferenceEquals(sender, _subscribedARIMAX)) return;");
        }

        /// <summary>
        /// Verifies closing a TimeSeriesAnalysis document clears the control's element reference.
        /// </summary>
        [TestMethod]
        public void MainProjectNode_DocumentClosed_ClearsTimeSeriesAnalysisElement()
        {
            string source = File.ReadAllText(Path.Combine(FindAppGuiRoot(), "MainProjectNode.cs"));
            int blockStart = source.IndexOf("documentControl as TimeSeriesAnalysisControl", StringComparison.Ordinal);
            int detachIndex = source.IndexOf("cntrl.Element = null;", blockStart, StringComparison.Ordinal);

            Assert.IsTrue(blockStart >= 0, "The TimeSeriesAnalysisControl close block must exist.");
            Assert.IsTrue(detachIndex > blockStart, "DocumentClosed must clear TimeSeriesAnalysisControl.Element.");
        }

        /// <summary>
        /// Verifies the TimeSeriesAnalysis document control ignores stale element and plot events.
        /// </summary>
        [TestMethod]
        public void TimeSeriesAnalysisControl_IgnoresStaleEvents()
        {
            string source = File.ReadAllText(Path.Combine(FindAppGuiRoot(), "TimeSeriesAnalysis", "TimeSeriesAnalysisControl.xaml.cs"));

            StringAssert.Contains(source, "if (!ReferenceEquals(sender, Element)) return;");
            StringAssert.Contains(source, "if (!ReferenceEquals(sender, Element?.TimeSeriesPlot?.ActualModel)) return;");
            StringAssert.Contains(source, "if (!ReferenceEquals(sender, Element?.ResidualPlot?.ActualModel)) return;");
        }

        /// <summary>
        /// Finds the App GUI source directory from the test output directory.
        /// </summary>
        /// <returns>The absolute path to the App GUI source directory.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the repository root cannot be found.</exception>
        /// <remarks>
        /// Tests run from bin/Debug, so this walks parents until the source tree is found.
        /// </remarks>
        private static string FindAppGuiRoot([CallerFilePath] string sourceFilePath = "")
        {
            string[] searchRoots = new[]
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(sourceFilePath)
            };

            foreach (string searchRoot in searchRoots)
            {
                if (string.IsNullOrEmpty(searchRoot))
                {
                    continue;
                }

                DirectoryInfo directory = new DirectoryInfo(searchRoot);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, "src", "RMC.BestFit.App", "GUI");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException("Could not locate src/RMC.BestFit.App/GUI from the test output directory.");
        }

        /// <summary>
        /// Finds the ending line for a C# method beginning at the supplied line.
        /// </summary>
        /// <param name="lines">The file lines to inspect.</param>
        /// <param name="startLine">The line containing the method signature.</param>
        /// <returns>The zero-based ending line for the method, or -1 when no matching brace is found.</returns>
        /// <remarks>
        /// The properties-control handlers are simple method bodies, so brace counting is sufficient here.
        /// </remarks>
        private static int FindMethodEndLine(string[] lines, int startLine)
        {
            int depth = 0;
            bool foundOpeningBrace = false;

            for (int i = startLine; i < lines.Length; i++)
            {
                string code = StripLineComment(lines[i]);
                depth += CountChar(code, '{');
                if (depth > 0)
                {
                    foundOpeningBrace = true;
                }

                depth -= CountChar(code, '}');
                if (foundOpeningBrace && depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Counts occurrences of a character in a string.
        /// </summary>
        /// <param name="value">The string to inspect.</param>
        /// <param name="character">The character to count.</param>
        /// <returns>The number of occurrences.</returns>
        /// <remarks>
        /// This avoids LINQ allocations inside the small source scan loop.
        /// </remarks>
        private static int CountChar(string value, char character)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == character)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Removes a line comment from a line of source text.
        /// </summary>
        /// <param name="line">The line to clean.</param>
        /// <returns>The line content before the first line-comment token.</returns>
        /// <remarks>
        /// The scan only needs to avoid matching guards or Element dereferences inside comments.
        /// </remarks>
        private static string StripLineComment(string line)
        {
            int index = line.IndexOf("//", StringComparison.Ordinal);
            return index < 0 ? line : line.Substring(0, index);
        }

        /// <summary>
        /// Formats a source path relative to the App GUI directory.
        /// </summary>
        /// <param name="guiRoot">The App GUI source root.</param>
        /// <param name="filePath">The file path to format.</param>
        /// <returns>A path relative to the App GUI source root.</returns>
        /// <remarks>
        /// Relative paths keep assertion failures concise and stable across workstations.
        /// </remarks>
        private static string RelativeToGuiRoot(string guiRoot, string filePath)
        {
            return Path.GetRelativePath(guiRoot, filePath).Replace('\\', '/');
        }
    }
}
