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
            foreach (Plot plot in GetDiagnosticPlots(element))
                plot.Series.Add(new LineSeries { Name = "StaleDiagnostic" });
            var diagnosticsTab = (TabItem)control.FindName("ThresholdDiagnosticsTab");
            diagnosticsTab.Visibility = Visibility.Visible;

            try
            {
                InvokeUpdateThresholdDiagnosticsPlots(control);
            }
            catch (TargetInvocationException ex)
            {
                Assert.Fail($"Updating the threshold diagnostics with an invalid smoothing period threw {ex.InnerException}");
            }

            foreach (Plot plot in GetDiagnosticPlots(element))
                Assert.AreEqual(0, plot.Series.Count, $"The '{plot.Title}' plot must be cleared rather than keep a stale curve.");
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
            if (Application.Current == null)
                _ = new Application();
            if (!Application.Current.Resources.Contains("Left_CellStyle"))
                Application.Current.Resources["Left_CellStyle"] = new Style(typeof(DataGridCell));
        }
    }
}
