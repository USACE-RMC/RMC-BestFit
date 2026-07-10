using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Source-level regression tests for <c>TimeSeriesAnalysisControl</c> defaults that are
    /// difficult to exercise without constructing the full WPF control graph.
    /// </summary>
    [TestClass]
    public class TimeSeriesAnalysisControlSourceTests
    {
        /// <summary>
        /// Verifies the training-period annotation label is placed 30 percent along the line by default.
        /// </summary>
        [TestMethod]
        public void TrainingPeriodAnnotation_TextLinePosition_DefaultsToThirtyPercent()
        {
            string source = ReadAppSource(Path.Combine("GUI", "TimeSeriesAnalysis", "TimeSeriesAnalysisControl.xaml.cs"));

            StringAssert.Contains(source, "Text = \"End of Training Period\",");
            StringAssert.Contains(source, "TextLinePosition = 0.3,");
        }

        /// <summary>
        /// Reads an App source file using a byte-preserving single-byte decoding.
        /// </summary>
        /// <param name="relativePath">The source path relative to the App project root.</param>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The source file text.</returns>
        private static string ReadAppSource(string relativePath, [CallerFilePath] string sourceFilePath = "")
        {
            string path = Path.Combine(FindRepoRoot(sourceFilePath), "src", "RMC.BestFit.App", relativePath);
            return Encoding.Latin1.GetString(File.ReadAllBytes(path));
        }

        /// <summary>
        /// Finds the repository root by walking up from a known source file.
        /// </summary>
        /// <param name="sourceFilePath">A source file path inside the repository.</param>
        /// <returns>The absolute repository root path.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the repository root cannot be found.</exception>
        private static string FindRepoRoot(string sourceFilePath)
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFilePath)!);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "RMC.BestFit.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate repository root.");
        }
    }
}
