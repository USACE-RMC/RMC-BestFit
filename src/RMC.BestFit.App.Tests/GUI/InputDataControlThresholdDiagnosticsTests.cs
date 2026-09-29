using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using OxyPlot.Wpf;
using RMC.BestFit.UI;
using RMC_BestFit;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using RMC.BestFit.Models;
using RMC.BestFit.App.Tests.GUI.Support;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Behavioral regression tests for the peaks-over-threshold diagnostics in
    /// <see cref="InputDataControl"/>.
    /// </summary>
    /// <remarks>
    /// The control is built outside the running application with the single application-level
    /// style its XAML resolves statically. The diagnostics update is invoked directly because the
    /// lazy tab-selection path that normally calls it needs a loaded, visible control.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class InputDataControlThresholdDiagnosticsTests
    {
        /// <summary>Releases resources owned by the current test's STA thread.</summary>
        [TestCleanup]
        public void Cleanup() => WpfTestHost.ReleaseResources();

        /// <summary>Absent and short source data discard cached diagnostics, curves, and threshold annotations.</summary>
        /// <param name="sourceLength">A nonnegative series length, -1 for no source element, or -2 for a null source series.</param>
        /// <remarks>Cached results are constructed directly, so this regression never invokes GPD fitting.</remarks>
        [STATestMethod]
        [DataRow(-2)]
        [DataRow(-1)]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(19)]
        public void InsufficientSource_ClearsCachedResultsSeriesAndAnnotations(int sourceLength)
        {
            EnsureApplicationResources();
            var element = new InputData("Short diagnostics", _collection);
            if (sourceLength != -1)
            {
                var source = new TimeSeriesElement("Short diagnostic source");
                source.TimeSeries = sourceLength == -2 ? null : CreateSeries(sourceLength);
                element.TimeSeriesElement = source;
            }
            var control = new InputDataControl { Element = element };
            try
            {
                typeof(InputDataControl).GetField("_mrlResult", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(control, new MeanResidualLifeResult(new List<MRLPoint>()));
                typeof(InputDataControl).GetField("_stabilityResult", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(control, new ParameterStabilityResult(new List<StabilityPoint>()));
                foreach (Plot plot in GetDiagnosticPlots(element))
                {
                    plot.Series.Add(new LineSeries { Name = "StaleDiagnostic" });
                    plot.Annotations.Add(new LineAnnotation { X = 50d });
                }
                ((TabItem)control.FindName("ThresholdDiagnosticsTab")).Visibility = Visibility.Visible;

                InvokeUpdateThresholdDiagnosticsPlots(control);

                foreach (Plot plot in GetDiagnosticPlots(element))
                {
                    Assert.AreEqual(0, plot.Series.Count, "Stale diagnostic curves must be removed.");
                    Assert.AreEqual(0, plot.Annotations.Count, "Stale threshold annotations must be removed.");
                }
                Assert.IsNull(typeof(InputDataControl).GetField("_mrlResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control));
                Assert.IsNull(typeof(InputDataControl).GetField("_stabilityResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control));
            }
            finally
            {
                control.Element = null;
                WpfTestHost.DrainDispatcher();
            }
        }

        /// <summary>A source-value edit invalidates clean diagnostics even before extraction has occurred.</summary>
        /// <remarks>The diagnostic tab remains unselected, preventing numerical fitting during this notification test.</remarks>
        [STATestMethod]
        public void UnprocessedSourceValueEdit_MarksPreviouslyCleanDiagnosticsDirty()
        {
            EnsureApplicationResources();
            var source = new TimeSeriesElement("Unprocessed diagnostic source") { TimeSeries = CreateSeries(19) };
            var element = new InputData("Unprocessed diagnostics", _collection)
            {
                ExactDataMethod = InputData.ExactDataEntryType.PeaksOverThresholdSeries,
                TimeSeriesElement = source
            };
            var control = new InputDataControl { Element = element };
            try
            {
                ((TabItem)control.FindName("ThresholdDiagnosticsTab")).IsSelected = false;
                var dirty = typeof(InputDataControl).GetField("_thresholdDiagnosticsDirty", BindingFlags.Instance | BindingFlags.NonPublic);
                dirty.SetValue(control, false);
                Assert.IsFalse(element.IsProcessed);

                source.TimeSeries[0].Value += 1d;

                Assert.IsTrue((bool)dirty.GetValue(control), "Source edits must invalidate diagnostics without needing an IsProcessed transition.");
                Assert.IsFalse(element.IsProcessed);
            }
            finally
            {
                control.Element = null;
                WpfTestHost.DrainDispatcher();
            }
        }


        /// <summary>Real source lifecycle changes remove obsolete diagnostics without forcing the hidden tab visible.</summary>
        /// <param name="operation">The source removal or replacement to perform.</param>
        /// <param name="selected">Whether diagnostics were selected before the source changed.</param>
        /// <remarks>All sources have at most nineteen rows, so neither setup nor mutation can invoke diagnostic fitting.</remarks>
        [STATestMethod]
        [DataRow("RemoveElement", true)]
        [DataRow("RemoveElement", false)]
        [DataRow("NullSeries", true)]
        [DataRow("NullSeries", false)]
        [DataRow("ClearSource", true)]
        [DataRow("ClearSource", false)]
        [DataRow("ReplaceShort", true)]
        [DataRow("ReplaceShort", false)]
        public void SourceLifecycle_ClearsObsoleteCachesAndPlotsThroughActualNotifications(string operation, bool selected)
        {
            EnsureApplicationResources();
            var source = new TimeSeriesElement("Lifecycle diagnostic source") { TimeSeries = CreateSeries(19) };
            var element = new InputData("Lifecycle diagnostics", _collection)
            {
                ExactDataMethod = InputData.ExactDataEntryType.PeaksOverThresholdSeries,
                TimeSeriesElement = source
            };
            var control = new InputDataControl { Element = element };
            try
            {
                control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                var diagnosticsTab = (TabItem)control.FindName("ThresholdDiagnosticsTab");
                diagnosticsTab.IsSelected = selected;
                if (!selected) ((TabItem)control.FindName("DataFrameTab")).IsSelected = true;
                WpfTestHost.DrainDispatcher();
                Assert.AreEqual(Visibility.Visible, diagnosticsTab.Visibility);
                SeedCachedDiagnostics(control, element);

                switch (operation)
                {
                    case "RemoveElement": element.TimeSeriesElement = null; break;
                    case "NullSeries": source.TimeSeries = null; break;
                    case "ClearSource": source.TimeSeries.Clear(); break;
                    case "ReplaceShort": source.TimeSeries = CreateSeries(1); break;
                    default: throw new ArgumentException("Unknown source lifecycle operation.", nameof(operation));
                }
                WpfTestHost.DrainDispatcher();

                if (operation == "RemoveElement" || operation == "NullSeries")
                    Assert.AreEqual(Visibility.Collapsed, diagnosticsTab.Visibility,
                        "The control must reflect source absence through its real notification path.");
                AssertDiagnosticsCleared(control, element);
            }
            finally
            {
                control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                control.Element = null;
                WpfTestHost.DrainDispatcher();
            }
        }

        /// <summary>Seeds stale caches and graphics only after control initialization and tab selection are complete.</summary>
        /// <param name="control">The initialized control.</param>
        /// <param name="element">The element owning the diagnostic plots.</param>
        private static void SeedCachedDiagnostics(InputDataControl control, InputData element)
        {
            typeof(InputDataControl).GetField("_mrlResult", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(control, new MeanResidualLifeResult(new List<MRLPoint>()));
            typeof(InputDataControl).GetField("_stabilityResult", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(control, new ParameterStabilityResult(new List<StabilityPoint>()));
            foreach (Plot plot in GetDiagnosticPlots(element))
            {
                plot.Series.Add(new LineSeries { Name = "StaleLifecycleDiagnostic" });
                plot.Annotations.Add(new LineAnnotation { X = 50d });
            }
            typeof(InputDataControl).GetField("_thresholdDiagnosticsDirty", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(control, false);
        }

        /// <summary>Checks that unavailable source data cannot leave cached or displayed diagnostic results.</summary>
        /// <param name="control">The updated control.</param>
        /// <param name="element">The element owning the diagnostic plots.</param>
        private static void AssertDiagnosticsCleared(InputDataControl control, InputData element)
        {
            Assert.IsNull(typeof(InputDataControl).GetField("_mrlResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control));
            Assert.IsNull(typeof(InputDataControl).GetField("_stabilityResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control));
            foreach (Plot plot in GetDiagnosticPlots(element))
            {
                Assert.AreEqual(0, plot.Series.Count, "Unavailable source data must not retain old diagnostic curves.");
                Assert.AreEqual(0, plot.Annotations.Count, "Unavailable source data must not retain old threshold annotations.");
            }
        }

        /// <summary>Creates a small dated source series without fitting or random generation.</summary>
        /// <param name="count">The number of daily observations.</param>
        /// <returns>The inline source series.</returns>
        private static TimeSeries CreateSeries(int count)
        {
            var series = new TimeSeries(TimeInterval.OneDay);
            for (int i = 0; i < count; i++)
                series.Add(new SeriesOrdinate<DateTime, double>(new DateTime(2021, 1, 1).AddDays(i), 10d + i));
            return series;
        }
        /// <summary>
        /// Shared input-data collection used to construct the element under test.
        /// </summary>
        private static InputDataCollection _collection;

        /// <summary>
        /// Creates a shared collection backed by the singleton project.
        /// </summary>
        /// <param name="_">The MSTest context.</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            _collection = new InputDataCollection(BestFitProject.GetInstance());
        }

        /// <summary>
        /// Verifies that updating the threshold diagnostics with a smoothing period the smoothing
        /// function cannot use clears the three diagnostic plots instead of throwing.
        /// </summary>
        /// <remarks>
        /// A moving-average period equal to the series length makes Numerics' moving-window
        /// smoothing throw. Before the period check, that exception escaped the dispatcher and
        /// closed the application when the POT Diagnostics tab was opened.
        /// </remarks>
        [STATestMethod]
        public void InvalidSmoothingPeriod_ClearsDiagnosticPlotsWithoutThrowing()
        {
            EnsureApplicationResources();
            InputData element = CreateInputDataWithInvalidSmoothingPeriod();
            var control = new InputDataControl { Element = element };
            try
            {
                foreach (Plot plot in GetDiagnosticPlots(element))
                    plot.Series.Add(new LineSeries { Name = "StaleDiagnostic" });
                var diagnosticsTab = (TabItem)control.FindName("ThresholdDiagnosticsTab");
                diagnosticsTab.Visibility = Visibility.Visible;
                InvokeUpdateThresholdDiagnosticsPlots(control);
                foreach (Plot plot in GetDiagnosticPlots(element))
                    Assert.AreEqual(0, plot.Series.Count, $"The '{plot.Title}' plot must be cleared rather than keep a stale curve.");
            }
            catch (TargetInvocationException ex)
            {
                Assert.Fail($"Updating the threshold diagnostics with an invalid smoothing period threw {ex.InnerException}");
            }
            finally
            {
                control.Element = null;
                WpfTestHost.DrainDispatcher();
            }
        }

        /// <summary>
        /// Creates an input-data element linked to a 30-step daily series with a moving-average
        /// smoothing period equal to the series length.
        /// </summary>
        /// <returns>An element whose smoothing period is invalid for its smoothing function.</returns>
        private static InputData CreateInputDataWithInvalidSmoothingPeriod()
        {
            var series = new TimeSeries(TimeInterval.OneDay);
            DateTime date = new DateTime(2021, 1, 1);
            for (int i = 0; i < 30; i++)
            {
                series.Add(new SeriesOrdinate<DateTime, double>(date, 10.0 + (i * 7 % 11)));
                date = TimeSeries.AddTimeInterval(date, TimeInterval.OneDay);
            }

            var timeSeriesElement = new TimeSeriesElement("PotDiagnosticsInvalidPeriodSeries") { TimeSeries = series };
            var element = new InputData("PotDiagnosticsInvalidPeriod", _collection)
            {
                TimeSeriesElement = timeSeriesElement,
                SmoothingFunction = SmoothingFunctionType.MovingAverage
            };
            element.Period = series.Count;

            Assert.IsFalse(element.IsSmoothingPeriodValid(), "Precondition: the smoothing period equals the series length.");
            return element;
        }

        /// <summary>
        /// Returns the three peaks-over-threshold diagnostic plots owned by an input-data element.
        /// </summary>
        /// <param name="element">The element that owns the plots.</param>
        /// <returns>The mean-residual-life, modified-scale, and shape plots.</returns>
        private static IEnumerable<Plot> GetDiagnosticPlots(InputData element)
        {
            yield return element.MRLPlot;
            yield return element.ModifiedScalePlot;
            yield return element.ShapePlot;
        }

        /// <summary>
        /// Invokes the control's private threshold-diagnostics update.
        /// </summary>
        /// <param name="control">The control to update.</param>
        /// <exception cref="InvalidOperationException">Thrown when the update method no longer exists.</exception>
        /// <exception cref="TargetInvocationException">Thrown when the update itself throws.</exception>
        private static void InvokeUpdateThresholdDiagnosticsPlots(InputDataControl control)
        {
            MethodInfo update = typeof(InputDataControl).GetMethod("UpdateThresholdDiagnosticsPlots", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("InputDataControl.UpdateThresholdDiagnosticsPlots was not found.");
            update.Invoke(control, null);
        }

        /// <summary>
        /// Supplies the application-level data-grid cell style that <see cref="InputDataControl"/>
        /// resolves as a static resource when it is constructed outside the full application.
        /// </summary>
        private static void EnsureApplicationResources()
        {
            WpfTestHost.EnsureResources();
        }
    }
}
