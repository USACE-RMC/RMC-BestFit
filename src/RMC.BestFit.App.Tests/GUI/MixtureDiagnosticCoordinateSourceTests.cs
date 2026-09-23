using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using OxyPlot.Wpf;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using RMC_BestFit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Pins sampled-coordinate behavior in the parameter-indexed Bayesian diagnostic controls.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class MixtureDiagnosticCoordinateSourceTests
    {
        /// <summary>
        /// Verifies results installed after control binding replace the stale full-K names with K-1 sampled names.
        /// </summary>
        [STATestMethod]
        public void KMinusOneResultsInstalledAfterBinding_RefreshEverySelector()
        {
            MixtureModel model = CreateModel(componentCount: 2);
            var analysis = new BayesianAnalysis(model);
            object[] controls = CreateControls();
            BindAnalysis(controls, analysis);
            AssertSelectorNames(controls, model.Parameters.Select(parameter => parameter.DisplayName).ToArray());

            MCMCResults results = CreateResults(model.NumberOfParameters - 1);
            analysis.SetCustomMCMCResults(results, skipInformationCriteria: true);

            string[] expected = GetStoredParameterNames(model, results.ParameterResults.Length);
            AssertSelectorNames(controls, expected);
            Assert.AreEqual(model.Parameters[2].DisplayName, expected[1], "The second stored coordinate must be component 1 parameter 1, not w2.");
            Assert.IsFalse(expected.Contains(model.Parameters[1].DisplayName), "The derived w2 name must not appear in K-1 diagnostics.");
        }

        /// <summary>
        /// Verifies K-1 naming for three-component and zero-inflated mixtures.
        /// </summary>
        [STATestMethod]
        public void KMinusOneResults_MapThreeComponentAndZeroInflatedMixtures()
        {
            foreach ((int componentCount, bool zeroInflated) in new[] { (3, false), (2, true) })
            {
                MixtureModel model = CreateModel(componentCount, zeroInflated);
                var analysis = new BayesianAnalysis(model);
                object[] controls = CreateControls();
                BindAnalysis(controls, analysis);
                MCMCResults results = CreateResults(model.NumberOfParameters - 1);

                analysis.SetCustomMCMCResults(results, skipInformationCriteria: true);

                AssertSelectorNames(controls, GetStoredParameterNames(model, results.ParameterResults.Length));
            }
        }

        /// <summary>
        /// Verifies legacy full-K results retain identity naming in every selector.
        /// </summary>
        [STATestMethod]
        public void LegacyFullKResults_RetainEveryPublicParameterName()
        {
            MixtureModel model = CreateModel(componentCount: 2);
            var analysis = new BayesianAnalysis(model);
            object[] controls = CreateControls();
            BindAnalysis(controls, analysis);
            MCMCResults results = CreateResults(model.NumberOfParameters);

            analysis.SetCustomMCMCResults(results, skipInformationCriteria: true);

            AssertSelectorNames(controls, model.Parameters.Select(parameter => parameter.DisplayName).ToArray());
        }

        /// <summary>
        /// Verifies result refreshes preserve valid selections and update controls even when IsEstimated remains true.
        /// </summary>
        [STATestMethod]
        public void ResultsRefresh_PreservesSelectionsAndUpdatesAlreadyEstimatedControls()
        {
            MixtureModel model = CreateModel(componentCount: 2);
            var analysis = new BayesianAnalysis(model);
            object[] controls = CreateControls();
            BindAnalysis(controls, analysis);
            MCMCResults sampledResults = CreateResults(model.NumberOfParameters - 1);
            analysis.SetCustomMCMCResults(sampledResults, skipInformationCriteria: true);
            string[] sampledNames = GetStoredParameterNames(model, sampledResults.ParameterResults.Length);

            SetEverySelection(controls, sampledNames[^1]);
            analysis.CredibleIntervalWidth = 0.95;
            AssertEverySelection(controls, sampledNames[^1]);

            MCMCResults legacyResults = CreateResults(model.NumberOfParameters, offset: 10.0);
            analysis.SetCustomMCMCResults(legacyResults, skipInformationCriteria: true);
            AssertSelectorNames(controls, model.Parameters.Select(parameter => parameter.DisplayName).ToArray());
            AssertEverySelection(controls, sampledNames[^1]);
        }

        /// <summary>
        /// Verifies heat-map normalization and autocorrelation bounds use the retained result count.
        /// </summary>
        [STATestMethod]
        public void RetainedDrawDiagnostics_IgnoreConfiguredOutputLength()
        {
            EnsureApplicationResources();
            MixtureModel model = CreateModel(componentCount: 2);
            var analysis = new BayesianAnalysis(model)
            {
                OutputLength = 1000
            };
            MCMCResults results = CreateResults(model.NumberOfParameters - 1, drawCount: 24);
            SetAutocorrelations(results);
            analysis.SetCustomMCMCResults(results, skipInformationCriteria: true);
            using var controller = new BayesianController();

            var autocorrelation = new AutocorrelationControl();
            autocorrelation.SetPlot(controller.AutocorrelationPlot);
            autocorrelation.Analysis = analysis;

            double[] expectedInterval = Autocorrelation.CorrelationConfidenceInterval(
                results.Output.Count,
                analysis.CredibleIntervalWidth);
            LineAnnotation lower = controller.AutocorrelationPlot.Annotations
                .OfType<LineAnnotation>()
                .Single(annotation => annotation.Name == "LowerCI");
            LineAnnotation upper = controller.AutocorrelationPlot.Annotations
                .OfType<LineAnnotation>()
                .Single(annotation => annotation.Name == "UpperCI");
            Assert.AreEqual(expectedInterval[0], lower.Y, 1E-12);
            Assert.AreEqual(expectedInterval[1], upper.Y, 1E-12);

            var heatMap = new BivariateHeatMapControl();
            heatMap.SetPlot(controller.BivariateHeatMapPlot);
            heatMap.Analysis = analysis;
            HeatMapSeries series = controller.BivariateHeatMapPlot.Series.OfType<HeatMapSeries>().Single();
            double probabilityMass = series.Data.Cast<double>().Sum();
            Assert.AreEqual(1.0, probabilityMass, 1E-12);
        }

        /// <summary>
        /// Verifies transient missing selections do not index outside stored diagnostic coordinates.
        /// </summary>
        [STATestMethod]
        public void MissingSelections_DoNotThrowOrReadResults()
        {
            MixtureModel model = CreateModel(componentCount: 2);
            var analysis = new BayesianAnalysis(model);
            MCMCResults results = CreateResults(model.NumberOfParameters - 1);
            SetAutocorrelations(results);
            analysis.SetCustomMCMCResults(results, skipInformationCriteria: true);
            object[] controls = CreateControls();
            using var controller = new BayesianController();
            SetPlots(controls, controller);
            BindAnalysis(controls, analysis);

            foreach (object control in controls)
                ((FrameworkElement)control).RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            foreach (ComboBox selector in GetSelectors(controls))
                selector.SelectedIndex = -1;

            Assert.IsTrue(GetSelectors(controls).All(selector => selector.SelectedIndex == -1));
        }

        /// <summary>
        /// Creates a valid mixture model with deterministic inline data.
        /// </summary>
        /// <param name="componentCount">The number of Normal components.</param>
        /// <param name="zeroInflated">Whether the mixture includes a fixed zero mass.</param>
        /// <returns>A configured full-K mixture model.</returns>
        private static MixtureModel CreateModel(int componentCount, bool zeroInflated = false)
        {
            var dataFrame = new DataFrame();
            double[] values = zeroInflated
                ? new[] { 0.0, 0.0, 0.5, 0.8, 1.1, 1.4, 2.0, 2.6 }
                : new[] { -2.0, -1.2, -0.5, 0.2, 0.9, 1.7, 2.5, 3.1 };
            for (int i = 0; i < values.Length; i++)
                dataFrame.ExactSeries.Add(new ExactData(2000 + i, values[i]));
            var types = Enumerable.Repeat(UnivariateDistributionType.Normal, componentCount).ToList();
            return new MixtureModel(dataFrame, types, zeroInflated);
        }

        /// <summary>
        /// Creates deterministic processed MCMC results without running a sampler.
        /// </summary>
        /// <param name="parameterCount">The stored parameter-vector length.</param>
        /// <param name="drawCount">The number of retained draws.</param>
        /// <param name="offset">A value added to every generated coordinate.</param>
        /// <returns>Processed MCMC results.</returns>
        private static MCMCResults CreateResults(int parameterCount, int drawCount = 20, double offset = 0.0)
        {
            var output = new List<ParameterSet>(drawCount);
            for (int drawIndex = 0; drawIndex < drawCount; drawIndex++)
            {
                var values = new double[parameterCount];
                for (int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
                    values[parameterIndex] = offset + parameterIndex + 0.05 * drawIndex;
                output.Add(new ParameterSet(values, -drawIndex));
            }
            return new MCMCResults(output[^1], output, alpha: 0.10);
        }

        /// <summary>
        /// Supplies deterministic autocorrelation arrays for output-only MCMC results.
        /// </summary>
        /// <param name="results">The results to update.</param>
        private static void SetAutocorrelations(MCMCResults results)
        {
            foreach (ParameterResults parameter in results.ParameterResults)
            {
                parameter.Autocorrelation = new double[,]
                {
                    { 0.0, 1.0 },
                    { 1.0, 0.2 },
                    { 2.0, 0.1 }
                };
            }
        }

        /// <summary>
        /// Creates one instance of every parameter-indexed Bayesian diagnostic control.
        /// </summary>
        /// <returns>The five diagnostic controls.</returns>
        private static object[] CreateControls()
        {
            EnsureApplicationResources();
            return new object[]
            {
                new AutocorrelationControl(),
                new BivariateHeatMapControl(),
                new HistogramControl(),
                new KernelDensityControl(),
                new MarkovChainTraceControl()
            };
        }

        /// <summary>
        /// Supplies the application-level data-grid style required when controls are constructed outside the full app.
        /// </summary>
        private static void EnsureApplicationResources()
        {
            if (Application.Current == null)
                _ = new Application();
            if (!Application.Current.Resources.Contains("Center_CellStyle"))
                Application.Current.Resources["Center_CellStyle"] = new Style(typeof(DataGridCell));
        }

        /// <summary>
        /// Binds one Bayesian analysis to every diagnostic control.
        /// </summary>
        /// <param name="controls">The controls to bind.</param>
        /// <param name="analysis">The analysis supplied to each control.</param>
        private static void BindAnalysis(IEnumerable<object> controls, BayesianAnalysis analysis)
        {
            foreach (object control in controls)
            {
                switch (control)
                {
                    case AutocorrelationControl autocorrelation:
                        autocorrelation.Analysis = analysis;
                        break;
                    case BivariateHeatMapControl heatMap:
                        heatMap.Analysis = analysis;
                        break;
                    case HistogramControl histogram:
                        histogram.Analysis = analysis;
                        break;
                    case KernelDensityControl density:
                        density.Analysis = analysis;
                        break;
                    case MarkovChainTraceControl trace:
                        trace.Analysis = analysis;
                        break;
                }
            }
        }

        /// <summary>
        /// Assigns the standard Bayesian plots to every diagnostic control.
        /// </summary>
        /// <param name="controls">The controls receiving plots.</param>
        /// <param name="controller">The controller that owns the plots.</param>
        private static void SetPlots(IEnumerable<object> controls, BayesianController controller)
        {
            foreach (object control in controls)
            {
                switch (control)
                {
                    case AutocorrelationControl autocorrelation:
                        autocorrelation.SetPlot(controller.AutocorrelationPlot);
                        break;
                    case BivariateHeatMapControl heatMap:
                        heatMap.SetPlot(controller.BivariateHeatMapPlot);
                        break;
                    case HistogramControl histogram:
                        histogram.SetPlot(controller.HistogramPlot);
                        break;
                    case KernelDensityControl density:
                        density.SetPlot(controller.KernelDensityPlot);
                        break;
                    case MarkovChainTraceControl trace:
                        trace.SetPlot(controller.MarkovChainTracePlot);
                        break;
                }
            }
        }

        /// <summary>
        /// Gets all parameter selectors from the supplied controls.
        /// </summary>
        /// <param name="controls">The diagnostic controls.</param>
        /// <returns>Every single- and dual-parameter selector.</returns>
        private static IEnumerable<ComboBox> GetSelectors(IEnumerable<object> controls)
        {
            foreach (object control in controls)
            {
                switch (control)
                {
                    case AutocorrelationControl autocorrelation:
                        yield return autocorrelation.ParameterComboBox;
                        break;
                    case BivariateHeatMapControl heatMap:
                        yield return heatMap.XParameterComboBox;
                        yield return heatMap.YParameterComboBox;
                        break;
                    case HistogramControl histogram:
                        yield return histogram.ParameterComboBox;
                        break;
                    case KernelDensityControl density:
                        yield return density.ParameterComboBox;
                        break;
                    case MarkovChainTraceControl trace:
                        yield return trace.ParameterComboBox;
                        break;
                }
            }
        }

        /// <summary>
        /// Gets names matching the stored result shape.
        /// </summary>
        /// <param name="model">The full-K mixture model.</param>
        /// <param name="storedCount">The stored parameter count.</param>
        /// <returns>Names in stored-coordinate order.</returns>
        private static string[] GetStoredParameterNames(MixtureModel model, int storedCount)
        {
            var names = model.Parameters.Select(parameter => parameter.DisplayName).ToList();
            if (storedCount == names.Count - 1)
                names.RemoveAt(model.Mixture!.Distributions.Length - 1);
            return names.ToArray();
        }

        /// <summary>
        /// Asserts every selector contains the expected names and stored-coordinate count.
        /// </summary>
        /// <param name="controls">The controls containing selectors.</param>
        /// <param name="expected">The expected names.</param>
        private static void AssertSelectorNames(IEnumerable<object> controls, string[] expected)
        {
            foreach (ComboBox selector in GetSelectors(controls))
            {
                string[] actual = selector.Items.Cast<string>().ToArray();
                CollectionAssert.AreEqual(expected, actual);
                Assert.AreEqual(expected.Length, selector.Items.Count);
            }
        }

        /// <summary>
        /// Selects the same named parameter in every selector.
        /// </summary>
        /// <param name="controls">The controls containing selectors.</param>
        /// <param name="parameterName">The parameter name to select.</param>
        private static void SetEverySelection(IEnumerable<object> controls, string parameterName)
        {
            foreach (ComboBox selector in GetSelectors(controls))
                selector.SelectedValue = parameterName;
        }

        /// <summary>
        /// Asserts every selector retains the expected named parameter.
        /// </summary>
        /// <param name="controls">The controls containing selectors.</param>
        /// <param name="parameterName">The expected selected parameter name.</param>
        private static void AssertEverySelection(IEnumerable<object> controls, string parameterName)
        {
            foreach (ComboBox selector in GetSelectors(controls))
                Assert.AreEqual(parameterName, selector.SelectedValue as string);
        }
    }
}
