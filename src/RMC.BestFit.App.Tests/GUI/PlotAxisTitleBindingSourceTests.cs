using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Source-level regression tests for data-label-bound plot axis title wiring.
    /// </summary>
    /// <remarks>
    /// Constructing every WPF plot control requires the full App resource graph, so these tests
    /// pin the scoped binding policy in source: data/time-series label axes use
    /// PlotAxisTitleDefaults, while Bayesian selector-driven controls remain outside this helper.
    /// </remarks>
    [TestClass]
    public class PlotAxisTitleBindingSourceTests
    {
        private static readonly string[] ScopedAxisTitleFiles =
        {
            Path.Combine("GUI", "FittingAnalysis", "FittingAnalysisControl.xaml.cs"),
            Path.Combine("GUI", "UnivariateAnalysis", "Univariate", "UnivariateAnalysisControl.xaml.cs"),
            Path.Combine("GUI", "UnivariateAnalysis", "B17C", "B17CAnalysisControl.xaml.cs"),
            Path.Combine("GUI", "UnivariateAnalysis", "PointProcess", "PointProcessAnalysisControl.xaml.cs"),
            Path.Combine("GUI", "UnivariateAnalysis", "Mixture", "MixtureAnalysisControl.xaml.cs"),
            Path.Combine("GUI", "UnivariateAnalysis", "Composite", "CompositeAnalysisControl.xaml.cs"),
            Path.Combine("GUI", "BivariateAnalysis", "Bivariate", "BivariateAnalysisControl.xaml.cs"),
            Path.Combine("GUI", "BivariateAnalysis", "CoincidentFrequency", "CoincidentFrequencyControl.xaml.cs"),
            Path.Combine("GUI", "RatingCurveAnalysis", "RatingCurveAnalysisControl.xaml.cs"),
            Path.Combine("GUI", "TimeSeriesAnalysis", "TimeSeriesAnalysisControl.xaml.cs"),
            Path.Combine("GUI", "TimeSeriesData", "TimeSeries", "TimeSeriesControl.xaml.cs")
        };

        private static readonly string[] BayesianSelectorDrivenFiles =
        {
            Path.Combine("GUI", "Support", "Controls", "HistogramControl.xaml.cs"),
            Path.Combine("GUI", "Support", "Controls", "KernelDensityControl.xaml.cs"),
            Path.Combine("GUI", "Support", "Controls", "MarkovChainTraceControl.xaml.cs"),
            Path.Combine("GUI", "Support", "Controls", "BivariateHeatMapControl.xaml.cs"),
            Path.Combine("GUI", "Support", "Controls", "InfluenceDiagnosticsControl.xaml.cs")
        };

        /// <summary>
        /// Verifies every scoped App control routes data-label axis title updates through the helper.
        /// </summary>
        [TestMethod]
        public void ScopedDataLabelAxisTitleControls_UseDefaultAwareHelper()
        {
            var failures = new List<string>();

            foreach (string relativePath in ScopedAxisTitleFiles)
            {
                string source = ReadAppSource(relativePath);
                if (!source.Contains("PlotAxisTitleDefaults.", StringComparison.Ordinal))
                {
                    failures.Add(relativePath + " does not use PlotAxisTitleDefaults.");
                }
            }

            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Verifies scoped App controls do not bypass the helper with direct axis-title bindings.
        /// </summary>
        [TestMethod]
        public void ScopedDataLabelAxisTitleControls_DoNotBindAxisTitlePropertyDirectly()
        {
            var failures = new List<string>();

            foreach (string relativePath in ScopedAxisTitleFiles)
            {
                string source = ReadAppSource(relativePath);
                if (source.Contains("BindingOperations.SetBinding", StringComparison.Ordinal)
                    || source.Contains("BindingOperations.ClearBinding", StringComparison.Ordinal)
                    || source.Contains("Axis.TitleProperty", StringComparison.Ordinal)
                    || source.Contains("OxyPlot.Wpf.Axis.TitleProperty", StringComparison.Ordinal))
                {
                    failures.Add(relativePath + " still binds or clears Axis.TitleProperty directly.");
                }
            }

            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Verifies the time-series residual plot binds its fitted-value axis to the selected
        /// time-series unit label.
        /// </summary>
        [TestMethod]
        public void TimeSeriesAnalysisResidualPlot_BindsXAxisToTimeSeriesUnitLabel()
        {
            string source = ReadAppSource(Path.Combine("GUI", "TimeSeriesAnalysis", "TimeSeriesAnalysisControl.xaml.cs"));

            StringAssert.Contains(source, "var residualXAxis = ResidualPlot?.Axes.FirstOrDefault(a => a.Key == \"Xaxis\");");
            StringAssert.Contains(source, "PlotAxisTitleDefaults.BindTitleIfDefault(residualXAxis, Element.TimeSeriesData, nameof(TimeSeriesElement.UnitLabel), Element.TimeSeriesData.UnitLabel);");
            StringAssert.Contains(source, "PlotAxisTitleDefaults.SetTitleIfDefault(residualYAxis, \"Residual\");");
        }

        /// <summary>
        /// Verifies Bayesian selector-driven plot controls remain outside the data-label helper scope.
        /// </summary>
        [TestMethod]
        public void BayesianSelectorDrivenControls_DoNotUseDataLabelAxisTitleHelper()
        {
            var failures = new List<string>();

            foreach (string relativePath in BayesianSelectorDrivenFiles)
            {
                string source = ReadAppSource(relativePath);
                if (source.Contains("PlotAxisTitleDefaults", StringComparison.Ordinal))
                {
                    failures.Add(relativePath + " should keep selector-driven axis-title behavior.");
                }
            }

            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
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
        /// Finds the repository root from the test output directory.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The absolute repository root.</returns>
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
