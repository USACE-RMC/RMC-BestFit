using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Source-level regression tests for the distributions grid delete and re-bind wiring in
    /// <c>MixtureAnalysisPropertiesControl</c>.
    /// </summary>
    /// <remarks>
    /// Constructing the full WPF control requires the application resource graph, so these tests
    /// pin the control-code wiring that prevents the application crash reported when deleting the
    /// second default distribution: the grid's own delete loop mutated the bound collection with no
    /// exception handling, and the re-entrant grid re-bind inside that dispatch could write back
    /// into the collection during its own change notification.
    /// </remarks>
    [TestClass]
    public class MixtureDistributionGridSafetyTests
    {
        /// <summary>
        /// Verifies the delete handler cancels the grid's own delete and defers the removal.
        /// </summary>
        [TestMethod]
        public void PreviewDeleteRows_CancelsGridDeleteAndDefersRemoval()
        {
            string source = ReadControlSource();
            string method = ExtractMethodSource(source, "DistributionDataGrid_PreviewDeleteRows");

            StringAssert.Contains(method, "cancel = true;");
            StringAssert.Contains(method, "Dispatcher.BeginInvoke");
            StringAssert.Contains(method, "RemoveAt(rowIndex)");
        }

        /// <summary>
        /// Verifies the grid re-bind suppresses combo-box selection write-backs.
        /// </summary>
        [TestMethod]
        public void ElementPropertyChanged_SuppressesSelectionDuringRebind()
        {
            string source = ReadControlSource();
            string method = ExtractMethodSource(source, "Element_PropertyChanged");

            StringAssert.Contains(method, "_suppressDistributionSelection = true;");
            StringAssert.Contains(method, "DistributionDataGrid.ItemsSource = null;");
            StringAssert.Contains(method, "_suppressDistributionSelection = false;");
        }

        /// <summary>
        /// Verifies the combo-box handler early-returns while the grid re-binds.
        /// </summary>
        [TestMethod]
        public void DistributionComboBoxSelectionChanged_EarlyReturnsWhileSuppressed()
        {
            string source = ReadControlSource();
            string method = ExtractMethodSource(source, "DistributionComboBox_SelectionChanged");

            StringAssert.Contains(method, "if (_suppressDistributionSelection) return;");
        }

        /// <summary>
        /// Reads the MixtureAnalysisPropertiesControl source file.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The source text for <c>MixtureAnalysisPropertiesControl.xaml.cs</c>.</returns>
        private static string ReadControlSource([CallerFilePath] string sourceFilePath = "")
        {
            return File.ReadAllText(Path.Combine(
                FindRepoRoot(sourceFilePath),
                "src",
                "RMC.BestFit.App",
                "GUI",
                "UnivariateAnalysis",
                "Mixture",
                "MixtureAnalysisPropertiesControl.xaml.cs"));
        }

        /// <summary>
        /// Extracts a private method body from a C# source file using brace counting.
        /// </summary>
        /// <param name="source">The C# source text.</param>
        /// <param name="methodName">The method whose body is extracted.</param>
        /// <returns>The method source from its declaration through its closing brace.</returns>
        private static string ExtractMethodSource(string source, string methodName)
        {
            int declarationIndex = source.IndexOf("void " + methodName + "(", StringComparison.Ordinal);
            Assert.IsTrue(declarationIndex >= 0, $"Method {methodName} was not found.");
            int braceIndex = source.IndexOf('{', declarationIndex);
            int depth = 0;
            for (int i = braceIndex; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(declarationIndex, i - declarationIndex + 1);
                }
            }
            Assert.Fail($"Method {methodName} has unbalanced braces.");
            return string.Empty;
        }

        /// <summary>
        /// Walks up from a source file path to the repository root containing the src folder.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The repository root directory.</returns>
        private static string FindRepoRoot(string sourceFilePath)
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFilePath)!);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
                directory = directory.Parent;
            Assert.IsNotNull(directory, "Repository root was not found.");
            return directory!.FullName;
        }
    }
}
