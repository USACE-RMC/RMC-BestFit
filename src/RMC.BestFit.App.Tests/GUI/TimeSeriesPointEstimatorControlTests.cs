using GenericControls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using OxyPlot;
using RMC.BestFit.Analyses;
using RMC.BestFit.App.Tests.GUI.Support;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using RMC_BestFit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ModelAnalysis = RMC.BestFit.Analyses.ARIMAXAnalysis;
using UiAnalysis = RMC.BestFit.UI.TimeSeriesAnalysis;
using WpfLineSeries = OxyPlot.Wpf.LineSeries;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Exercises point-estimator changes through the loaded time-series properties panel and main control.
    /// </summary>
    /// <remarks>
    /// Restored posterior draws and uncertainty results keep these dispatcher and binding contracts
    /// deterministic. No optimizer, sampler, forecast simulation, or verification fixture is run.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class TimeSeriesPointEstimatorControlTests
    {
        /// <summary>Locates private members used only to restore state or deliver a queued callback.</summary>
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>Releases resources owned by this STA test after its controls and workers have detached.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            WpfTestHost.DrainDispatcher();
            WpfTestHost.ReleaseResources();
        }

        /// <summary>
        /// Switching the actual bound combo mean to mode and back preserves the fit and updates all primary views.
        /// </summary>
        [STATestMethod]
        public void LoadedProperties_PointEstimatorRoundTrip_PreservesFitAndRefreshesViews()
        {
            using var fixture = new ControlFixture();
            double[] originalCurve = (double[])fixture.Analysis.AnalysisResults.ModeCurve.Clone();

            fixture.Select(BayesianAnalysis.PointEstimateType.PosteriorMode);
            Assert.IsFalse(originalCurve.SequenceEqual(fixture.Analysis.AnalysisResults.ModeCurve),
                "Distinct stored mean and MAP parameters must produce different point curves.");
            fixture.AssertDisplayed(BayesianAnalysis.PointEstimateType.PosteriorMode);

            fixture.Select(BayesianAnalysis.PointEstimateType.PosteriorMean);
            fixture.AssertDisplayed(BayesianAnalysis.PointEstimateType.PosteriorMean);
            CollectionAssert.AreEqual(originalCurve, fixture.Analysis.AnalysisResults.ModeCurve);
            fixture.AssertPosteriorPreserved();
        }

        /// <summary>
        /// The properties panel can detach and reattach without losing refreshes or invalidating the retained fit.
        /// </summary>
        [STATestMethod]
        public void Properties_UnloadReload_PointEstimatorRoundTripStillRefreshes()
        {
            using var fixture = new ControlFixture();
            RaiseLifecycle(fixture.Properties, FrameworkElement.UnloadedEvent);
            fixture.Select(BayesianAnalysis.PointEstimateType.PosteriorMode);
            fixture.AssertDisplayed(BayesianAnalysis.PointEstimateType.PosteriorMode);

            RaiseLifecycle(fixture.Properties, FrameworkElement.LoadedEvent);
            fixture.Select(BayesianAnalysis.PointEstimateType.PosteriorMean);
            fixture.AssertDisplayed(BayesianAnalysis.PointEstimateType.PosteriorMean);
            fixture.Select(BayesianAnalysis.PointEstimateType.PosteriorMode);
            fixture.Select(BayesianAnalysis.PointEstimateType.PosteriorMean);
            fixture.AssertDisplayed(BayesianAnalysis.PointEstimateType.PosteriorMean);
            fixture.AssertPosteriorPreserved();
        }

        /// <summary>
        /// Rapid selections complete through the analysis gate and publish the final selected estimator.
        /// </summary>
        [STATestMethod]
        public void Properties_RapidEstimatorChanges_EndAtTheFinalSelection()
        {
            using var fixture = new ControlFixture();
            int completedBefore = fixture.Notifications.Count;
            foreach (BayesianAnalysis.PointEstimateType estimator in new[]
            {
                BayesianAnalysis.PointEstimateType.PosteriorMode,
                BayesianAnalysis.PointEstimateType.PosteriorMean,
                BayesianAnalysis.PointEstimateType.PosteriorMode,
                BayesianAnalysis.PointEstimateType.PosteriorMean
            })
                fixture.Combo.SelectedValue = estimator;

            fixture.WaitForResults(completedBefore + 4);
            fixture.AssertDisplayed(BayesianAnalysis.PointEstimateType.PosteriorMean);
            fixture.AssertPosteriorPreserved();
        }

        /// <summary>
        /// A worker notification queued for the previous element cannot access WPF or rebind the replacement.
        /// </summary>
        [STATestMethod]
        public void Properties_ElementReplacement_IgnoresQueuedPreviousElementNotification()
        {
            using var fixture = new ControlFixture();
            UiAnalysis replacement = CreateElement();
            MethodInfo handler = typeof(TimeSeriesAnalysisPropertiesControl).GetMethod(
                "Element_PropertyChanged", PrivateInstance)
                ?? throw new InvalidOperationException("The properties notification handler was not found.");

            // Do not pump the dispatcher until the element has changed: this models a notification
            // already in flight when the user opens a different analysis.
            Task queued = Task.Run(() => handler.Invoke(fixture.Properties, new object[]
            {
                fixture.Element, new PropertyChangedEventArgs(nameof(UiAnalysis.ARIMAX))
            }));
            Assert.IsTrue(SpinWait.SpinUntil(() => queued.IsCompleted, TimeSpan.FromSeconds(5)),
                "A worker-side notification must enqueue its UI work without blocking.");
            queued.GetAwaiter().GetResult();

            fixture.Properties.Element = replacement;
            WpfTestHost.DrainDispatcher();

            Assert.AreSame(replacement, fixture.Properties.Element);
            var output = (BayesianOutputControl)fixture.Properties.FindName("BayesianOutputControl");
            Assert.AreSame(replacement.BayesianAnalysis, output.Analysis,
                "The queued old notification must not restore the old Bayesian options binding.");
            Assert.AreSame(replacement.ARIMAX,
                typeof(TimeSeriesAnalysisPropertiesControl).GetField("_subscribedARIMAX", PrivateInstance)
                    ?.GetValue(fixture.Properties));
            Assert.IsTrue(replacement.IsEstimated);
            fixture.AssertPosteriorPreserved();
        }

        /// <summary>Creates sixty annual source ordinates using the existing AR-analysis inline-fixture recipe.</summary>
        /// <returns>The complete 1960 through 2019 series.</returns>
        /// <remarks>
        /// This mirrors ARAnalysisTests.CreateAnnualStreamflowTimeSeries: the seeded arithmetic creates
        /// source data only. It does not generate an MCMC chain or estimate any model.
        /// </remarks>
        private static TimeSeries CreateSource()
        {
            var series = new TimeSeries(TimeInterval.OneYear, new DateTime(1960, 1, 1), new DateTime(2019, 1, 1));
            var random = new Random(12345);
            double previous = 5000;
            for (int i = 0; i < series.Count; i++)
            {
                double innovation = (random.NextDouble() * 2 - 1) * 600;
                previous = 5000 + 0.6 * (previous - 5000) + innovation;
                series[i].Value = Math.Max(100, previous);
            }
            Assert.AreEqual(60, series.Count);
            return series;
        }

        /// <summary>Creates six retained parameter draws and a distinct stored MAP without estimation.</summary>
        /// <param name="baseline">The configured model's valid default parameter vector.</param>
        /// <returns>The injected posterior result object.</returns>
        private static MCMCResults CreatePosterior(double[] baseline)
        {
            var draws = new List<ParameterSet>();
            for (int i = 0; i < 6; i++)
            {
                var values = (double[])baseline.Clone();
                for (int j = 0; j < values.Length; j++)
                    values[j] += (i - 2) * 0.005 * Math.Max(1, Math.Abs(values[j]));
                draws.Add(new ParameterSet(values, -10 + i));
            }
            var map = (double[])baseline.Clone();
            map[0] += 0.1 * Math.Max(1, Math.Abs(map[0]));
            return new MCMCResults(new ParameterSet(map, 0), draws, alpha: 0.1);
        }

        /// <summary>Restores an estimated UI element while retaining its normal production event subscriptions.</summary>
        /// <returns>The element with complete source data, posterior, and non-null uncertainty bands.</returns>
        private static UiAnalysis CreateElement()
        {
            WpfTestHost.EnsureResources();
            var collection = new TimeSeriesAnalysisCollection(BestFitProject.GetInstance());
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var source = new TimeSeriesElement("Source" + suffix) { TimeSeries = CreateSource() };
            var element = new UiAnalysis("Analysis" + suffix, collection) { TimeSeriesData = source };
            element.ARIMAX.AROrderP = 1;
            element.ARIMAX.MAOrderQ = 1;
            var analysis = (ModelAnalysis)element.InnerAnalysis;
            MCMCResults posterior = CreatePosterior(element.ARIMAX.Parameters.Select(parameter => parameter.Value).ToArray());
            analysis.BayesianAnalysis.SetCustomMCMCResults(posterior, skipInformationCriteria: true);

            int count = source.TimeSeries.Count;
            var intervals = new double[count, 3];
            var mean = new double[count];
            for (int i = 0; i < count; i++)
            {
                intervals[i, 0] = source.TimeSeries[i].Index.ToOADate();
                intervals[i, 1] = 4000 + i;
                intervals[i, 2] = 6000 + i;
                mean[i] = 5000 + i;
            }
            var results = new UncertaintyAnalysisResults
            {
                ModeCurve = new double[count], MeanCurve = mean, ConfidenceIntervals = intervals
            };
            typeof(ModelAnalysis).GetProperty(nameof(ModelAnalysis.AnalysisResults))
                .GetSetMethod(nonPublic: true).Invoke(analysis, new object[] { results });
            typeof(AnalysisBase).GetProperty(nameof(AnalysisBase.IsEstimated))
                .GetSetMethod(nonPublic: true).Invoke(analysis, new object[] { true });
            Task initialize = analysis.UpdatePointEstimateResultsAsync();
            PumpUntil(() => initialize.IsCompleted, "Initial point-result construction did not complete.");
            initialize.GetAwaiter().GetResult();
            Assert.IsTrue(element.IsEstimated);
            return element;
        }

        /// <summary>Raises a framework lifecycle event on the instantiated control without opening a window.</summary>
        /// <param name="element">The control receiving the lifecycle transition.</param>
        /// <param name="routedEvent">Loaded or Unloaded.</param>
        private static void RaiseLifecycle(FrameworkElement element, RoutedEvent routedEvent)
        {
            element.RaiseEvent(new RoutedEventArgs(routedEvent));
            WpfTestHost.DrainDispatcher();
        }

        /// <summary>Services dispatcher callbacks until an asynchronous contract completes or its bounded wait fails.</summary>
        /// <param name="completed">The completion predicate evaluated on the STA thread.</param>
        /// <param name="message">The assertion message for an incomplete operation.</param>
        private static void PumpUntil(Func<bool> completed, string message)
        {
            var watch = Stopwatch.StartNew();
            while (!completed() && watch.Elapsed < TimeSpan.FromSeconds(5))
            {
                WpfTestHost.DrainDispatcher();
                Thread.Sleep(10);
            }
            WpfTestHost.DrainDispatcher();
            Assert.IsTrue(completed(), message);
        }

        /// <summary>Owns the loaded controls and captures immutable result evidence for one isolated fixture.</summary>
        private sealed class ControlFixture : IDisposable
        {
            /// <summary>The retained posterior instance.</summary>
            private readonly MCMCResults _posterior;
            /// <summary>The original flattened posterior draw values.</summary>
            private readonly double[] _drawValues;
            /// <summary>The retained credible-interval array.</summary>
            private readonly double[,] _intervals;
            /// <summary>The original credible-interval values.</summary>
            private readonly double[] _intervalValues;
            /// <summary>The retained posterior predictive mean curve.</summary>
            private readonly double[] _meanCurve;
            /// <summary>The original predictive mean values.</summary>
            private readonly double[] _meanValues;
            /// <summary>The analysis gate used to detect completed queued postprocessing.</summary>
            private readonly SemaphoreSlim _gate;

            /// <summary>Initializes a restored fit, its ordinary loaded views, and the actual bound combo.</summary>
            public ControlFixture()
            {
                Element = CreateElement();
                Analysis = (ModelAnalysis)Element.InnerAnalysis;
                _posterior = Analysis.BayesianAnalysis.Results;
                _drawValues = _posterior.Output.SelectMany(draw => draw.Values).ToArray();
                _intervals = Analysis.AnalysisResults.ConfidenceIntervals;
                _intervalValues = _intervals.Cast<double>().ToArray();
                _meanCurve = Analysis.AnalysisResults.MeanCurve;
                _meanValues = (double[])_meanCurve.Clone();
                _gate = (SemaphoreSlim)typeof(AnalysisBase).GetField("_reprocessGate", PrivateInstance).GetValue(Analysis);
                Control = new TimeSeriesAnalysisControl { Element = Element };
                Properties = new TimeSeriesAnalysisPropertiesControl { Element = Element };
                RaiseLifecycle(Control, FrameworkElement.LoadedEvent);
                RaiseLifecycle(Properties, FrameworkElement.LoadedEvent);
                var output = (BayesianOutputControl)Properties.FindName("BayesianOutputControl");
                Combo = (ComboBox)((ContentPropertyControl)output.FindName("PointEstimator")).InnerContent;
                Notifications = new ResultNotifications(Analysis);
                AssertDisplayed(BayesianAnalysis.PointEstimateType.PosteriorMean);
            }

            /// <summary>Gets the UI element with the normal wrapper event wiring.</summary>
            public UiAnalysis Element { get; }
            /// <summary>Gets the injected model analysis.</summary>
            public ModelAnalysis Analysis { get; }
            /// <summary>Gets the actual main control.</summary>
            public TimeSeriesAnalysisControl Control { get; }
            /// <summary>Gets the actual properties panel.</summary>
            public TimeSeriesAnalysisPropertiesControl Properties { get; }
            /// <summary>Gets the bound point-estimator combo.</summary>
            public ComboBox Combo { get; }
            /// <summary>Gets the cross-thread results notification counter.</summary>
            public ResultNotifications Notifications { get; }

            /// <summary>Selects an estimator through the real two-way binding and waits for its result notification.</summary>
            /// <param name="estimator">The mean or mode selection.</param>
            public void Select(BayesianAnalysis.PointEstimateType estimator)
            {
                int expectedNotifications = Notifications.Count + 1;
                Combo.SelectedValue = estimator;
                WaitForResults(expectedNotifications);
            }

            /// <summary>Waits for the queued results and asserts that no UI subscriber invalidated the fit.</summary>
            /// <param name="expectedNotifications">The required cumulative completed-results count.</param>
            public void WaitForResults(int expectedNotifications)
            {
                PumpUntil(() => !Analysis.IsEstimated ||
                    (Notifications.Count >= expectedNotifications && _gate.CurrentCount == 1),
                    "Point-estimator reprocessing did not publish its completed results.");
                Assert.IsTrue(Analysis.IsEstimated,
                    "A point-estimator change must not invalidate a fit because a properties subscriber accesses WPF from a worker thread.");
                Assert.IsTrue(Notifications.Count >= expectedNotifications);
            }

            /// <summary>Checks selected parameters, plot geometry, result rows, and summary labels after reprocessing.</summary>
            /// <param name="estimator">The expected selected point estimator.</param>
            public void AssertDisplayed(BayesianAnalysis.PointEstimateType estimator)
            {
                WpfTestHost.DrainDispatcher();
                double[] expected = estimator == BayesianAnalysis.PointEstimateType.PosteriorMean
                    ? _posterior.PosteriorMean.Values : _posterior.MAP.Values;
                string title = estimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";
                Assert.AreEqual(estimator, Combo.SelectedValue);
                CollectionAssert.AreEqual(expected, Element.ARIMAX.Parameters.Select(parameter => parameter.Value).ToArray());
                var summary = ((DataGrid)Control.FindName("SummaryStatisticsTable")).ItemsSource.Cast<SummaryStatistic>().ToArray();
                for (int i = 0; i < expected.Length; i++)
                    Assert.AreEqual(expected[i], summary[i].Value, "Every parameter summary follows the selected estimator.");
                Assert.AreEqual(title, ((DataGridTextColumn)Control.FindName("StatValueColumn")).Header);
                Assert.AreEqual(title, ((DataGridTextColumn)Control.FindName("ModeColumn")).Header);
                WpfLineSeries line = Element.TimeSeriesPlot.Series.OfType<WpfLineSeries>().Single(series => series.Name == "PosteriorMode");
                Assert.AreEqual(title, line.Title);
                CollectionAssert.AreEqual(Analysis.AnalysisResults.ModeCurve,
                    line.ItemsSource.Cast<DataPoint>().Select(point => point.Y).ToArray());
                var rows = ((DataGrid)Control.FindName("TimeSeriesTable")).ItemsSource.Cast<FrequencyCurvePoint>().ToArray();
                CollectionAssert.AreEqual(Analysis.AnalysisResults.ModeCurve, rows.Select(row => row.Mode).ToArray());
                Assert.AreEqual(60, rows.Length);
            }

            /// <summary>Checks retained posterior and non-null uncertainty arrays by identity and by value.</summary>
            public void AssertPosteriorPreserved()
            {
                Assert.AreSame(_posterior, Analysis.BayesianAnalysis.Results);
                CollectionAssert.AreEqual(_drawValues, _posterior.Output.SelectMany(draw => draw.Values).ToArray());
                Assert.IsNotNull(Analysis.AnalysisResults.ConfidenceIntervals);
                Assert.AreSame(_intervals, Analysis.AnalysisResults.ConfidenceIntervals);
                CollectionAssert.AreEqual(_intervalValues, Analysis.AnalysisResults.ConfidenceIntervals.Cast<double>().ToArray());
                Assert.AreSame(_meanCurve, Analysis.AnalysisResults.MeanCurve);
                CollectionAssert.AreEqual(_meanValues, Analysis.AnalysisResults.MeanCurve);
            }

            /// <summary>Detaches the fixture's controls and notification counter from retained model objects.</summary>
            public void Dispose()
            {
                RaiseLifecycle(Properties, FrameworkElement.UnloadedEvent);
                RaiseLifecycle(Control, FrameworkElement.UnloadedEvent);
                Properties.Element = null;
                Control.Element = null;
                Notifications.Dispose();
                WpfTestHost.DrainDispatcher();
            }
        }

        /// <summary>Counts completed results on their publishing thread without introducing WPF access.</summary>
        private sealed class ResultNotifications : IDisposable
        {
            /// <summary>The observed analysis.</summary>
            private readonly INotifyPropertyChanged _source;
            /// <summary>The completed-results notification count.</summary>
            private int _count;

            /// <summary>Subscribes to results notifications.</summary>
            /// <param name="source">The analysis being observed.</param>
            public ResultNotifications(INotifyPropertyChanged source)
            {
                _source = source;
                _source.PropertyChanged += OnPropertyChanged;
            }

            /// <summary>Gets the current cross-thread count.</summary>
            public int Count => Volatile.Read(ref _count);

            /// <summary>Counts only published analysis-result changes.</summary>
            /// <param name="sender">The publishing analysis.</param>
            /// <param name="args">The changed property name.</param>
            private void OnPropertyChanged(object sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName == nameof(ModelAnalysis.AnalysisResults))
                    Interlocked.Increment(ref _count);
            }

            /// <summary>Removes the event handler when its fixture is finished.</summary>
            public void Dispose()
            {
                _source.PropertyChanged -= OnPropertyChanged;
            }
        }
    }
}
