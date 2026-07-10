using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Source-level regression tests for read-only input data grid selection behavior.
    /// </summary>
    /// <remarks>
    /// These tests pin the lightweight wiring that lets read-only exact data remain copyable
    /// and keeps selected cells selected when focus leaves a grid.
    /// </remarks>
    [TestClass]
    public class InputDataControlReadOnlySelectionTests
    {
        /// <summary>
        /// Verifies non-manual exact data stays read-only and cannot add/delete rows.
        /// </summary>
        [TestMethod]
        public void BindExactDataGrid_NonManualMode_RemainsReadOnlyWithoutRowEdits()
        {
            string source = ReadInputDataControlSource();
            string method = ExtractMethodSource(source, "BindExactDataGrid");

            StringAssert.Contains(method, "Element.ExactDataMethod == InputData.ExactDataEntryType.Manual");
            StringAssert.Contains(method, "ExactDataGrid.IsReadOnly = true;");
            StringAssert.Contains(method, "ExactDataGrid.CanUserAddInsertDeleteRows = false;");
            Assert.IsFalse(method.Contains("ExactDataGrid.IsEnabled = false", StringComparison.Ordinal));
        }

        /// <summary>
        /// Verifies input data grids do not clear selection after focus leaves the grid.
        /// </summary>
        [TestMethod]
        public void InputDataGrids_DoNotWireLostKeyboardFocusSelectionClear()
        {
            string xaml = ReadInputDataControlXaml();
            string source = ReadInputDataControlSource();

            Assert.IsFalse(xaml.Contains("LostKeyboardFocus=\"InputDataGrid_LostKeyboardFocus\"", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("InputDataGrid_LostKeyboardFocus", StringComparison.Ordinal));
            StringAssert.Contains(xaml, "Name=\"ExactDataGrid\"");
            StringAssert.Contains(xaml, "Name=\"UncertainDataGrid\"");
            StringAssert.Contains(xaml, "Name=\"IntervalDataGrid\"");
            StringAssert.Contains(xaml, "Name=\"ThresholdDataGrid\"");
        }

        /// <summary>
        /// Verifies the summary statistics grid keeps manual selectable cell styles.
        /// </summary>
        [TestMethod]
        public void SummaryStatisticsDataGrid_UsesReadOnlyCopyPasteGridWithManualAlignmentCellStyles()
        {
            string xaml = ReadInputDataControlXaml();
            string grid = ExtractXamlElement(xaml, "<cntrls:CopyPasteDataGrid Name=\"SummaryStatisticsDataGrid\"", "</cntrls:CopyPasteDataGrid>");

            StringAssert.Contains(grid, "Style=\"{DynamicResource CopyPasteDataGridStyle}\"");
            StringAssert.Contains(grid, "SelectionMode=\"Extended\"");
            StringAssert.Contains(grid, "SelectionUnit=\"Cell\"");
            StringAssert.Contains(grid, "IsReadOnly=\"True\"");
            StringAssert.Contains(grid, "CanUserDeleteRows=\"False\"");
            StringAssert.Contains(grid, "CanUserAddRows=\"False\"");
            StringAssert.Contains(grid, "CellStyle=\"{DynamicResource Left_CellStyle}\"");
            Assert.AreEqual(2, CountOccurrences(grid, "CellStyle=\"{DynamicResource Right_CellStyle}\""));
            Assert.IsFalse(grid.Contains("LostKeyboardFocus", StringComparison.Ordinal));
        }

        /// <summary>
        /// Verifies the hypothesis tests grid does not bypass selectable cell styles.
        /// </summary>
        [TestMethod]
        public void HypothesisDataGrid_UsesSelectableCellStylesForAllColumns()
        {
            string xaml = ReadInputDataControlXaml();
            string grid = ExtractXamlElement(xaml, "<cntrls:CopyPasteDataGrid Name=\"HypothesisDataGrid\"", "</cntrls:CopyPasteDataGrid>");

            StringAssert.Contains(grid, "Style=\"{DynamicResource CopyPasteDataGridStyle}\"");
            StringAssert.Contains(grid, "SelectionMode=\"Extended\"");
            StringAssert.Contains(grid, "SelectionUnit=\"Cell\"");
            StringAssert.Contains(grid, "IsReadOnly=\"True\"");
            Assert.AreEqual(2, CountOccurrences(grid, "BasedOn=\"{StaticResource Left_CellStyle}\""));
            Assert.AreEqual(2, CountOccurrences(grid, "CellStyle=\"{DynamicResource Center_CellStyle}\""));
            Assert.IsFalse(grid.Contains("<Style TargetType=\"DataGridCell\">", StringComparison.Ordinal));
            Assert.IsFalse(grid.Contains("BasedOn=\"{StaticResource {x:Type DataGridCell}}\"", StringComparison.Ordinal));
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
        /// Reads the InputDataControl C# source file.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The source text for <c>InputDataControl.xaml.cs</c>.</returns>
        private static string ReadInputDataControlSource([CallerFilePath] string sourceFilePath = "")
        {
            return File.ReadAllText(Path.Combine(FindRepoRoot(sourceFilePath), "src", "RMC.BestFit.App", "GUI", "InputData", "InputDataControl.xaml.cs"));
        }

        /// <summary>
        /// Reads the InputDataControl XAML source file.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The source text for <c>InputDataControl.xaml</c>.</returns>
        private static string ReadInputDataControlXaml([CallerFilePath] string sourceFilePath = "")
        {
            return File.ReadAllText(Path.Combine(FindRepoRoot(sourceFilePath), "src", "RMC.BestFit.App", "GUI", "InputData", "InputDataControl.xaml"));
        }

        /// <summary>
        /// Extracts a private method body from a C# source file using brace counting.
        /// </summary>
        /// <param name="source">The C# source text.</param>
        /// <param name="methodName">The method name to extract.</param>
        /// <returns>The method source, including its signature and braces.</returns>
        private static string ExtractMethodSource(string source, string methodName)
        {
            int start = source.IndexOf("private void " + methodName, StringComparison.Ordinal);
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
        /// Finds the repository root from the test output directory.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The absolute repository root.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the source tree cannot be found.</exception>
        private static string FindRepoRoot(string sourceFilePath)
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
