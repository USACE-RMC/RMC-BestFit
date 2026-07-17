using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Source-level regression tests for plotting-position refresh wiring in <c>InputDataControl</c>.
    /// </summary>
    /// <remarks>
    /// Constructing the full WPF control requires the application resource graph, so these tests
    /// pin the control-code wiring that refreshes computed plotting-position grid columns.
    /// </remarks>
    [TestClass]
    public class InputDataControlPlottingPositionRefreshTests
    {
        /// <summary>
        /// Verifies the element-level plotting-position event refreshes row wrappers.
        /// </summary>
        [TestMethod]
        public void ElementPropertyChanged_RefreshesPlottingPositionColumns()
        {
            string source = ReadInputDataControlSource();
            string method = ExtractMethodSource(source, "ElementPropertyChanged");

            StringAssert.Contains(method, "nameof(Data.PlottingPosition)");
            StringAssert.Contains(method, "RefreshPlottingPositionColumns();");
        }

        /// <summary>
        /// Verifies plotting-position refresh stays limited to row-wrapper notifications.
        /// </summary>
        [TestMethod]
        public void RefreshPlottingPositionColumns_NotifiesRowsWithoutRebindingOrUpdatingPlots()
        {
            string source = ReadInputDataControlSource();
            string method = ExtractMethodSource(source, "RefreshPlottingPositionColumns");

            StringAssert.Contains(method, "ExactDataOrdinates.OfType<ExactDataRowItem>()");
            StringAssert.Contains(method, "UncertainDataOrdinates.OfType<UncertainDataRowItem>()");
            StringAssert.Contains(method, "IntervalDataOrdinates.OfType<IntervalDataRowItem>()");
            StringAssert.Contains(method, "rowItem.RefreshPlottingPosition();");
            Assert.IsFalse(method.Contains("BindExactDataGrid", StringComparison.Ordinal));
            Assert.IsFalse(method.Contains("BindUncertainDataGrid", StringComparison.Ordinal));
            Assert.IsFalse(method.Contains("BindIntervalDataGrid", StringComparison.Ordinal));
            Assert.IsFalse(method.Contains("UpdateControl", StringComparison.Ordinal));
            Assert.IsFalse(method.Contains("ValidateTable", StringComparison.Ordinal));
        }

        /// <summary>
        /// Reads the InputDataControl source file.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The source text for <c>InputDataControl.xaml.cs</c>.</returns>
        private static string ReadInputDataControlSource([CallerFilePath] string sourceFilePath = "")
        {
            return File.ReadAllText(Path.Combine(
                FindRepoRoot(sourceFilePath),
                "src",
                "RMC.BestFit.App",
                "GUI",
                "InputData",
                "InputDataControl.xaml.cs"));
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

            foreach (string searchRoot in searchRoots)
            {
                if (string.IsNullOrEmpty(searchRoot)) continue;
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
