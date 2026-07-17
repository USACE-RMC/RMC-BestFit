using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Source-level regression tests for batch and standalone progress responsiveness wiring.
    /// </summary>
    [TestClass]
    public class BatchProgressResponsivenessSourceTests
    {
        /// <summary>
        /// Verifies the batch window routes runner events through the progress coalescer.
        /// </summary>
        [TestMethod]
        public void BatchRunWindow_UsesProgressCoalescerForRunnerEvents()
        {
            string source = ReadRepoFile("src/RMC.BestFit.App/GUI/Support/Controls/BatchRunWindow.xaml.cs");
            string method = ExtractMethodSource(source, "SimulateButton_Click");

            StringAssert.Contains(method, "using var progressUpdates = new BatchProgressUpdateDispatcher");
            StringAssert.Contains(method, "progressUpdates.MarkStarting(analysis);");
            StringAssert.Contains(method, "progressUpdates.ApplyResult(result);");
            StringAssert.Contains(method, "progressUpdates.PostProgress(e.Analysis, e.Progress);");
            StringAssert.Contains(method, "await progressUpdates.FlushAsync();");
            Assert.IsFalse(method.Contains("BatchRunCoordinator.ApplyProgress(", StringComparison.Ordinal),
                "Per-analysis progress should be coalesced instead of applied directly from the runner event.");
        }

        /// <summary>
        /// Verifies the coalescer keeps only latest progress and drops stale completion updates.
        /// </summary>
        [TestMethod]
        public void BatchProgressUpdateDispatcher_CoalescesAndDropsStaleProgress()
        {
            string source = ReadRepoFile("src/RMC.BestFit.App/GUI/Support/BatchProgressUpdateDispatcher.cs");

            StringAssert.Contains(source, "MinimumFlushIntervalMilliseconds = 50");
            StringAssert.Contains(source, "_pendingProgress[analysis] = progress;");
            StringAssert.Contains(source, "if (_flushQueued) return;");
            StringAssert.Contains(source, "_completedAnalyses.Contains(analysis)");
            StringAssert.Contains(source, "_pendingProgress.Remove(result.Analysis);");
            StringAssert.Contains(source, "DispatcherPriority.Background");
        }

        /// <summary>
        /// Verifies the shared standalone helper maps the processing phase to indeterminate UI.
        /// </summary>
        [TestMethod]
        public void AnalysisProgressDisplayHelper_MapsProcessingPhaseToIndeterminate()
        {
            string source = ReadRepoFile("src/RMC.BestFit.App/GUI/Support/AnalysisProgressDisplayHelper.cs");

            StringAssert.Contains(source, "ProcessingThreshold = 99");
            StringAssert.Contains(source, "progress >= ProcessingThreshold");
            StringAssert.Contains(source, "progressBar.IsIndeterminate = true;");
            StringAssert.Contains(source, "Processing Results...");
            StringAssert.Contains(source, "progress.ToString(\"N0\") + \"% Complete\"");
        }

        /// <summary>
        /// Reads a file from the repository root.
        /// </summary>
        /// <param name="relativePath">The repository-relative file path.</param>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The file contents.</returns>
        private static string ReadRepoFile(string relativePath, [CallerFilePath] string sourceFilePath = "")
        {
            return File.ReadAllText(Path.Combine(FindRepoRoot(sourceFilePath), relativePath));
        }

        /// <summary>
        /// Extracts a method body from a C# source file using brace counting.
        /// </summary>
        /// <param name="source">The C# source text.</param>
        /// <param name="methodName">The method name to extract.</param>
        /// <returns>The method source, including its signature and braces.</returns>
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
            DirectoryInfo directory = new FileInfo(sourceFilePath).Directory;
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RMC.BestFit.sln")))
            {
                directory = directory.Parent;
            }

            Assert.IsNotNull(directory, "Repository root could not be found.");
            return directory.FullName;
        }
    }
}
