using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Source-level regression tests for POT diagnostic lazy-load wiring in <c>InputDataControl</c>.
    /// </summary>
    /// <remarks>
    /// Constructing the full WPF control requires the application resource graph, so these tests
    /// pin the control-code wiring that prevents blank lazy-loaded POT diagnostic plots.
    /// </remarks>
    [TestClass]
    public class InputDataControlPotDiagnosticTests
    {
        /// <summary>
        /// Verifies radio-button changes compute POT diagnostics only when the diagnostics are dirty.
        /// </summary>
        [TestMethod]
        public void ThresholdDiagnosticRadioButtonChecked_ComputesDirtyDiagnosticsWithWaitCursor()
        {
            string source = ReadInputDataControlSource();
            string method = ExtractMethodSource(source, "ThresholdDiagnosticRadioButton_Checked");

            StringAssert.Contains(method, "_thresholdDiagnosticsDirty");
            StringAssert.Contains(method, "UpdateLazyContentWithWaitCursor(UpdateThresholdDiagnosticsPlots");
            StringAssert.Contains(method, "ShowSelectedDiagnosticPlot();");
        }

        /// <summary>
        /// Verifies the selected diagnostic plot is invalidated after its host becomes visible.
        /// </summary>
        [TestMethod]
        public void ShowSelectedDiagnosticPlot_InvalidatesSelectedPlotAfterVisibilitySwitch()
        {
            string source = ReadInputDataControlSource();
            string method = ExtractMethodSource(source, "ShowSelectedDiagnosticPlot");
            string invalidationMethod = ExtractMethodSource(source, "InvalidateSelectedDiagnosticPlot");

            StringAssert.Contains(method, "ThresholdDiagnosticsToolbar.Plot");
            StringAssert.Contains(method, "InvalidateSelectedDiagnosticPlot();");
            StringAssert.Contains(invalidationMethod, "Dispatcher.BeginInvoke(DispatcherPriority.Loaded");
            StringAssert.Contains(invalidationMethod, "selectedPlot.InvalidatePlot(true)");
        }

        /// <summary>
        /// Verifies wait-cursor ownership lives in the shared helper instead of the POT compute method.
        /// </summary>
        [TestMethod]
        public void UpdateThresholdDiagnosticsPlots_DoesNotResetWaitCursorInternally()
        {
            string source = ReadInputDataControlSource();
            string method = ExtractMethodSource(source, "UpdateThresholdDiagnosticsPlots");

            Assert.IsFalse(method.Contains("Mouse.OverrideCursor", StringComparison.Ordinal),
                "POT diagnostics should not clear the shared wait cursor before the dispatcher reset can paint.");
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
