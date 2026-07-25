using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Source-level regression tests for MAP and GMM influence diagnostic labels.
    /// </summary>
    /// <remarks>
    /// The control requires the full WPF resource graph, so this fast regression pins the
    /// shared plot wording without constructing the UserControl.
    /// </remarks>
    [TestClass]
    public class InfluenceDiagnosticsControlLabelTests
    {
        /// <summary>
        /// Verifies both MAP and GMM combined plots use estimator-neutral influence wording.
        /// </summary>
        [TestMethod]
        public void CombinedLeveragePlots_UseTotalInfluenceWording()
        {
            string source = ReadControlSource();

            Assert.AreEqual(
                2,
                CountOccurrences(source, "SetPlotTitle(\"Combined Leverage (% of Total Influence)\");"),
                "MAP and GMM must use the same combined-leverage title.");
            Assert.AreEqual(
                2,
                CountOccurrences(source, "SetAxisTitle(\"XAxis\", \"% of Total Influence\");"),
                "MAP and GMM must use the same total-influence axis label.");
            Assert.AreEqual(
                0,
                CountOccurrences(source, "% of Total Information"),
                "The combined index must not be presented as a conserved information total.");
            StringAssert.Contains(source, "of total combined influence.");
        }

        /// <summary>
        /// Counts non-overlapping occurrences of a value in source text.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="value">The value to count.</param>
        /// <returns>The number of non-overlapping occurrences.</returns>
        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        /// <summary>
        /// Reads the influence diagnostic control source file.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The control source.</returns>
        private static string ReadControlSource([CallerFilePath] string sourceFilePath = "")
        {
            return File.ReadAllText(Path.Combine(
                FindRepoRoot(sourceFilePath),
                "src",
                "RMC.BestFit.App",
                "GUI",
                "Support",
                "Controls",
                "InfluenceDiagnosticsControl.xaml.cs"));
        }

        /// <summary>
        /// Finds the repository root from a source or test-output path.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The repository root.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the source tree cannot be found.</exception>
        private static string FindRepoRoot(string sourceFilePath)
        {
            string[] searchRoots =
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
                    if (Directory.Exists(Path.Combine(directory.FullName, "src", "RMC.BestFit.App")))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException("Could not locate the RMC.BestFit source tree.");
        }
    }
}
