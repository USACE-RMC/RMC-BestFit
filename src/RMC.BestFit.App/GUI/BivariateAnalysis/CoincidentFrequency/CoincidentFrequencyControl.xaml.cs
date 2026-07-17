using OxyPlot;
using OxyPlot.Wpf;
using OxyPlotControls;
using FrameworkUI;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and interacting with coincident frequency analysis results.
    /// Provides the stage-frequency plot (AEP vs Z with 5%/Mean/95%/Mode bands) and the
    /// tabular AEP-by-Z output.
    /// </summary>
    public partial class CoincidentFrequencyControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CoincidentFrequencyControl"/> class.
        /// </summary>
        public CoincidentFrequencyControl()
        {
            InitializeComponent();
            DataContext = this;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
            // Restrict the alternatives selector to only show other Coincident Frequency
            // analyses — comparing a CFA against a plain BivariateAnalysis is not meaningful.
            // Must be set BEFORE the AlternativeSelector.Element binding fires LoadAnalyses().
            AlternativeSelector.AlternativeFilterType = typeof(CoincidentFrequencyAnalysis);
            // DataGrid Binding.StringFormat must be set before first render.
            SetColumnStringFormats();
        }

        /// <summary>
        /// Color palette used to assign distinct colors to alternative-analysis series in the
        /// frequency plot. Mirrors the canonical pattern from <see cref="CompositeAnalysisControl"/>.
        /// </summary>
        private string[] _colorHexCodes;

        #region Element DependencyProperty + ElementCallback

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(
            nameof(Element),
            typeof(CoincidentFrequencyAnalysis),
            typeof(CoincidentFrequencyControl),
            new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the coincident frequency analysis element associated with this control.
        /// </summary>
        public CoincidentFrequencyAnalysis Element
        {
            get { return (CoincidentFrequencyAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback invoked when the <see cref="Element"/> dependency property changes.
        /// Manages event handler subscriptions for the old and new element instances.
        /// </summary>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as CoincidentFrequencyControl == null) return;
            var thisControl = (CoincidentFrequencyControl)d;

            // Remove handlers from old element.
            if (e.OldValue != null)
            {
                CoincidentFrequencyAnalysis oldElement = e.OldValue as CoincidentFrequencyAnalysis;
                if (oldElement != null)
                {
                    oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                    thisControl.FrequencyPlotHost.Content = null;
                    thisControl.FrequencyPlotToolbar.Plot = null;
                    // PropertiesCalled is wired in XAML — no programmatic -= needed.
                }
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as CoincidentFrequencyAnalysis;
            if (newElement == null) return;

            // Subscribe Element-scoped handler.
            newElement.PropertyChanged += thisControl.Element_PropertyChanged;

            // Attach plot, wire toolbar, and bind axis titles inside a bridge-suspension block
            // so visual-tree attachment does not record undo entries.
            using (newElement.SuspendPlotBridges())
            {
                thisControl.FrequencyPlotHost.Content = newElement.FrequencyPlot;
                thisControl.FrequencyPlotToolbar.Plot = newElement.FrequencyPlot;
                thisControl.BindAxisTitles();
            }
        }

        #endregion

        #region Plot / events

        /// <summary>
        /// Convenience accessor for the Element-owned frequency plot.
        /// </summary>
        private Plot FrequencyPlot => Element?.FrequencyPlot;

        /// <summary>
        /// Gets a value indicating whether a plot area was clicked during the last mouse event.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Occurs when plot properties are requested to be displayed or modified.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Delegate for the <see cref="PlotPropertiesCalled"/> event.
        /// </summary>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);

        /// <summary>
        /// Occurs when the user clicks within the preview control area.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for the <see cref="PreviewControlClicked"/> event.
        /// </summary>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Forwards the toolbar's PropertiesCalled event to subscribers (MainProjectNode).
        /// </summary>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        #endregion

        #region Loaded / Unloaded

        /// <summary>
        /// Handles the Loaded event of the user control. Refreshes plot and tabular data on every
        /// load. Element-scoped lifecycle (PropertyChanged, plot host, toolbar) is owned by
        /// <see cref="ElementCallback"/> and survives unload/reload cycles.
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                UpdateFrequencyPlot();
                BindFrequencyCurveDataGrid();
                SetFrequencyCurveTableColumnHeaders();
                // Initial population of the M×N response data grid. Without this call the
                // grid sits empty until the user touches an X / Y ordinate (the only path
                // that previously triggered RebuildBivariateGrid via PropertyChanged).
                RebuildBivariateGrid();
                // Initial population of the Summary Statistics grid. Without this call the
                // grid sits empty on reopen until the user changes PointEstimator (the only
                // path that previously triggered the bind).
                BindSummaryStatisticsDataGrid();
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
        }

        /// <summary>
        /// Handles the Unloaded event. Element-scoped lifecycle is owned by ElementCallback;
        /// this control has no control-scoped subscriptions to tear down.
        /// </summary>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // No-op: Pattern B (canonical).
        }

        /// <summary>
        /// Forwards element property changes into UI refresh routines.
        /// </summary>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Element_PropertyChanged(sender, e)));
                return;
            }

            if (e.PropertyName == nameof(Element.AnalysisResults) ||
                e.PropertyName == nameof(Element.IsEstimated) ||
                e.PropertyName == nameof(Element.ZOutputValues))
            {
                UpdateFrequencyPlot();
                BindFrequencyCurveDataGrid();
                // Refresh summary statistics on every fit completion (the grid was already
                // initialized in UserControl_Loaded; this keeps it in sync as the analysis
                // is re-fit or undone).
                BindSummaryStatisticsDataGrid();
                ResetWaitCursor();
            }
            if (e.PropertyName == nameof(Element.InputData))
            {
                using (Element?.SuspendPlotBridges())
                {
                    BindAxisTitles();
                }
                UpdateFrequencyPlot();
            }
            // BivariateResponse changes come from three sources:
            //   1. X / Y ordinate add/remove (auto-resize) — needs grid rebuild for new dims.
            //   2. Undo of a cell edit — needs grid rebuild so the displayed value reverts.
            //   3. The user typing in a cell — does NOT need a rebuild (the grid already shows
            //      the new value, and rebuilding kills focus).
            // Case 3 is excluded by the _suppressBivariateResponseRebuild flag set inside
            // ResponseTable_ColumnChanged / BivariateDataGrid_DataPasted while the cell-edit
            // assignment is in flight.
            if (e.PropertyName == nameof(Element.BivariateResponse) && !_suppressBivariateResponseRebuild)
            {
                RebuildBivariateGrid();
            }
            if (e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth))
            {
                SetFrequencyCurveTableColumnHeaders();
                UpdateFrequencyPlot();
                BindFrequencyCurveDataGrid();
                BindSummaryStatisticsDataGrid();
            }
            if (e.PropertyName == nameof(Element.BayesianAnalysis.PointEstimator))
            {
                UpdateFrequencyPlot();
                BindFrequencyCurveDataGrid();
                BindSummaryStatisticsDataGrid();
            }
            // CoincidentFrequencyAnalysis exception: BayesianAnalysis.CredibleIntervalWidth and
            // XValues/YValues call ClearResults (no per-realisation AEP cache). Only PointEstimator
            // reprocesses via UpdatePointEstimateResultsAsync.
            if (e.PropertyName == nameof(Element.BayesianAnalysis.PointEstimator) && Element.IsEstimated)
            {
                // Cursor wait while the model layer's ReprocessIfEstimated runs the recompute
                // on TaskScheduler.Default. Reset is wired to the AnalysisResults notification above.
                Mouse.OverrideCursor = Cursors.Wait;
            }
        }

        /// <summary>
        /// Clears <see cref="Mouse.OverrideCursor"/> when it is currently set to <see cref="Cursors.Wait"/>.
        /// Marshals onto the UI thread when called from a background-thread PropertyChanged
        /// notification (the model layer's <c>ReprocessIfEstimated</c> path uses
        /// <c>TaskScheduler.Default</c>).
        /// </summary>
        private void ResetWaitCursor()
        {
            // Reset at Background priority so the Render-priority cursor frame from the
            // earlier `Mouse.OverrideCursor = Cursors.Wait` is guaranteed to flush before
            // the reset runs. Without this, fast reprocesses (UpdatePointEstimateResultsAsync)
            // can reset the cursor before the OS visually picks up the change — the user
            // sees no wait cursor at all when the mouse is stationary.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (Mouse.OverrideCursor == Cursors.Wait)
                    Mouse.OverrideCursor = null;
            }));
        }

        /// <summary>
        /// Binds the frequency plot Y-axis title to <see cref="InputData.UnitLabel"/> when an
        /// <see cref="InputData"/> overlay is selected, or clears the binding when it is null.
        /// Called from <see cref="ElementCallback"/> and when <see cref="CoincidentFrequencyAnalysis.InputData"/>
        /// changes. Must be called inside a <see cref="CoincidentFrequencyAnalysis.SuspendPlotBridges"/> block.
        /// </summary>
        private void BindAxisTitles()
        {
            if (Element == null) return;

            var yAxis = FrequencyPlot?.Axes.FirstOrDefault(a => a.Key == "Yaxis");
            if (yAxis == null) return;

            if (Element.InputData != null)
                PlotAxisTitleDefaults.BindTitleIfDefault(yAxis, Element.InputData, nameof(InputData.UnitLabel), Element.InputData.UnitLabel);
            else
                PlotAxisTitleDefaults.SetTitleIfDefault(yAxis, "Response (Z)");
        }

        #endregion

        #region Mouse / focus

        /// <summary>
        /// Handles the <c>LostFocus</c> event for <c>UserControl</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the <c>PreviewMouseDown</c> event for <c>UserControl</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Plot plot = GetCurrentPlot();
            OxyPlotToolbar toolbar = GetCurrentPlotToolbar();
            System.Windows.Media.HitTestResult plotHit = null;
            System.Windows.Media.HitTestResult toolbarHit = null;
            if (plot != null) plotHit = VisualTreeHelper.HitTest(plot, e.GetPosition(plot));
            if (toolbar != null) toolbarHit = VisualTreeHelper.HitTest(toolbar, e.GetPosition(toolbar));
            PreviewControlClicked?.Invoke(plotHit != null, toolbarHit != null, plot);
            PlotClicked = plotHit != null;
        }

        /// <summary>
        /// Gets the currently visible plot. Returns the frequency plot only when the
        /// Frequency Plot tab is selected; other tabs host no plot.
        /// </summary>
        public Plot GetCurrentPlot()
        {
            if (FrequencyPlotTabItem.IsSelected == true) return FrequencyPlot;
            return null;
        }

        /// <summary>
        /// Gets the toolbar for the currently visible plot. Returns the frequency-plot toolbar
        /// only when the Frequency Plot tab is selected; other tabs host no toolbar.
        /// </summary>
        public OxyPlotToolbar GetCurrentPlotToolbar()
        {
            if (FrequencyPlotTabItem.IsSelected == true) return FrequencyPlotToolbar;
            return null;
        }

        #endregion

        #region UpdateFrequencyPlot

        /// <summary>
        /// Updates the stage-frequency plot. Uses the canonical lookup-or-create pattern so
        /// user-edited series styling survives Save/Open: each series is resolved by Name from
        /// the current <c>plot.Series</c>; if not found, a freshly default-styled instance is
        /// created. Wrapped in <c>Element.SuspendPlotBridges()</c> to prevent series-clear and
        /// ItemsSource assignments from polluting the undo stack.
        /// </summary>
        private void UpdateFrequencyPlot()
        {
            var plot = FrequencyPlot;
            if (plot == null) return;

            // Resolve OUTSIDE the using block so user-customized styling is preserved.
            // Canonical series styling matches CompositeAnalysisControl: a single AreaSeries
            // for the credible-interval band (translucent blue fill, solid dark-blue border),
            // a dashed-blue Posterior Predictive line, and a solid-black Posterior Mode line.
            var credibleIntervals = plot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "CredibleIntervals")
                ?? new AreaSeries
                {
                    Name = "CredibleIntervals",
                    Fill = Color.FromArgb(75, 104, 140, 175),
                    Color = Color.FromArgb(255, 53, 59, 122),
                    LineStyle = LineStyle.Solid,
                    BrokenLineThickness = 1,
                    StrokeThickness = 1,
                    Decimator = OxyPlot.Decimator.Decimate,
                };
            var posteriorPredictive = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorPredictive")
                ?? new LineSeries
                {
                    Name = "PosteriorPredictive",
                    Title = "Posterior Predictive",
                    Color = Colors.Blue,
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Dash,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
            var posteriorMode = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorMode")
                ?? new LineSeries
                {
                    Name = "PosteriorMode",
                    Title = "Posterior Mode",
                    Color = Colors.Black,
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Solid,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
            // InputData overlay series — observed Z values plotted at their empirical AEPs.
            // Mirrors CompositeAnalysisControl.UpdateFrequencyPlot so the frequency plot shows
            // observed data when the user picks an InputData overlay (the bug fix the user asked
            // for: "When input data changes in CFA properties it should update the frequency plot").
            var frequencyExactData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
                ?? new ScatterPointSeries
                {
                    Name = "ExactData",
                    Title = "Exact Data",
                    MarkerFill = Colors.Black,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                };
            var frequencyLowOutlierData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Cross,
                };
            var frequencyUncertainData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "UncertainData")
                ?? new ScatterErrorSeries
                {
                    Name = "UncertainData",
                    Title = "Uncertain Data",
                    MarkerFill = Colors.Green,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Diamond,
                };
            var frequencyIntervalData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "IntervalData")
                ?? new ScatterErrorSeries
                {
                    Name = "IntervalData",
                    Title = "Interval Data",
                    MarkerFill = Colors.Cyan,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                };
            using (Element?.SuspendPlotBridges())
            {
                plot.Series.Clear();

                if (Element != null && Element.AnalysisResults != null && Element.ZOutputValues != null)
                {
                    var z = Element.ZOutputValues;
                    var ar = Element.AnalysisResults;
                    int n = z.Length;

                    var ciPoints = new List<Point3D>(n);
                    var prdPoints = new List<DataPoint>(n);
                    var mdPoints = new List<DataPoint>(n);

                    bool hasCI = ar.ConfidenceIntervals != null;
                    bool hasMean = ar.MeanCurve != null;
                    bool hasMode = ar.ModeCurve != null;

                    for (int k = 0; k < n; k++)
                    {
                        if (hasCI)
                        {
                            double lower = ar.ConfidenceIntervals[k, 0];
                            double upper = ar.ConfidenceIntervals[k, 1];
                            // Point3D layout for the AreaSeries CI band — see DataField mapping
                            // below. X = z (response), Y = lower AEP, Z = upper AEP.
                            ciPoints.Add(new Point3D(z[k], lower, upper));
                        }
                        if (hasMean) prdPoints.Add(new DataPoint(ar.MeanCurve[k], z[k]));
                        if (hasMode) mdPoints.Add(new DataPoint(ar.ModeCurve[k], z[k]));
                    }

                    if (hasCI)
                    {
                        // Horizontal CI band at fixed Z varying along AEP (X axis). Mirrors the
                        // CompositeAnalysisControl AreaSeries band, but with X/Y swapped because
                        // CFA plots AEP on X and Z on Y (the inverse of Composite's orientation).
                        //   primary edge:   X=Point3D.Y=lower_AEP, Y=Point3D.X=z
                        //   secondary edge: X=Point3D.Z=upper_AEP, Y=Point3D.X=z
                        credibleIntervals.ItemsSource = ciPoints;
                        credibleIntervals.DataFieldX = "Y";
                        credibleIntervals.DataFieldY = "X";
                        credibleIntervals.DataFieldX2 = "Z";
                        credibleIntervals.DataFieldY2 = "X";
                        credibleIntervals.Title = (Element.BayesianAnalysis.CredibleIntervalWidth * 100).ToString("F0") + "% Credible Intervals";
                        credibleIntervals.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(credibleIntervals);
                    }
                    if (hasMean)
                    {
                        posteriorPredictive.ItemsSource = prdPoints;
                        posteriorPredictive.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(posteriorPredictive);
                    }
                    if (hasMode)
                    {
                        posteriorMode.Title = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";
                        posteriorMode.ItemsSource = mdPoints;
                        posteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(posteriorMode);
                    }

                    // Re-add any alternative analyses that the user has checked. Each call
                    // routes to AlternativeSelector_AnalysisAdded which appends the alternative's
                    // CredibleIntervals, PosteriorPredictive, and PosteriorMode series.
                    if (Element.IsEstimated == true)
                        AlternativeSelector.AddAllChecked();
                }

                // InputData overlay — observed data points plotted at their empirical AEPs.
                // Rendered whenever an InputData overlay is selected, regardless of estimation
                // state, so the user can preview the dataset before running CFA. Mirrors the
                // canonical pattern in CompositeAnalysisControl.UpdateFrequencyPlot.
                if (Element?.InputData != null)
                {
                    // Detect log-scale Y axis so we can filter out non-positive values that
                    // would crash the log axis. Mirrors Composite's handling.
                    bool logAxis = false;
                    foreach (var axis in plot.Axes)
                    {
                        if (axis.Key == "Yaxis" && axis is LogarithmicAxis)
                        {
                            logAxis = true;
                            break;
                        }
                    }
                    var df = Element.InputData.DataFrame;

                    // Exact data (non-low-outlier observations).
                    frequencyExactData.ItemsSource = logAxis
                        ? df.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false && x.Value > 1E-16)
                        : df.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                    frequencyExactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                    frequencyExactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (df.ExactSeries.Count > 0) plot.Series.Add(frequencyExactData);

                    // Low-outlier observations (rendered with a distinct marker).
                    frequencyLowOutlierData.ItemsSource = logAxis
                        ? df.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true && x.Value > 1E-16)
                        : df.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                    frequencyLowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                    frequencyLowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (df.NumberOfLowOutliers > 0) plot.Series.Add(frequencyLowOutlierData);

                    // Uncertain observations (point with error bars on Y).
                    frequencyUncertainData.ItemsSource = logAxis
                        ? df.UncertainSeries
                        : df.UncertainSeries.Where(x => x.Value > 1E-16);
                    frequencyUncertainData.DataFieldX = nameof(UncertainData.PlottingPosition);
                    frequencyUncertainData.DataFieldY = nameof(UncertainData.Value);
                    frequencyUncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                    frequencyUncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                    frequencyUncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (df.UncertainSeries.Count > 0) plot.Series.Add(frequencyUncertainData);

                    // Interval observations.
                    frequencyIntervalData.ItemsSource = logAxis
                        ? df.IntervalSeries
                        : df.IntervalSeries.Where(x => x.Value > 1E-16);
                    frequencyIntervalData.DataFieldX = nameof(IntervalData.PlottingPosition);
                    frequencyIntervalData.DataFieldY = nameof(IntervalData.Value);
                    frequencyIntervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                    frequencyIntervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                    frequencyIntervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (df.IntervalSeries.Count > 0) plot.Series.Add(frequencyIntervalData);
                }

                plot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

        #region Tabular results

        /// <summary>
        /// Binds the frequency curve table to one row per Z output bin.
        /// </summary>
        private void BindFrequencyCurveDataGrid()
        {
            FrequencyCurveTable.ItemsSource = null;

            if (Element?.BayesianAnalysis == null) return;

            ModeColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

            if (Element.AnalysisResults == null || Element.ZOutputValues == null) return;

            var z = Element.ZOutputValues;
            var ar = Element.AnalysisResults;
            int n = z.Length;

            var rows = new List<CoincidentFrequencyTablePoint>(n);
            for (int k = 0; k < n; k++)
            {
                double lower = ar.ConfidenceIntervals != null ? ar.ConfidenceIntervals[k, 0] : double.NaN;
                double upper = ar.ConfidenceIntervals != null ? ar.ConfidenceIntervals[k, 1] : double.NaN;
                double mean = ar.MeanCurve != null ? ar.MeanCurve[k] : double.NaN;
                double mode = ar.ModeCurve != null ? ar.ModeCurve[k] : double.NaN;
                rows.Add(new CoincidentFrequencyTablePoint(z[k], lower, upper, mean, mode));
            }
            FrequencyCurveTable.ItemsSource = rows;
            FrequencyCurveTable.Items.Refresh();
        }

        /// <summary>
        /// Updates Lower/Upper column headers from the current credible-interval width
        /// (e.g. "5.0% CI" / "95.0% CI" for CI=0.90).
        /// </summary>
        private void SetFrequencyCurveTableColumnHeaders()
        {
            if (Element?.BayesianAnalysis == null) return;
            double alpha = (1.0 - Element.BayesianAnalysis.CredibleIntervalWidth) / 2.0;
            UpperColumn.Header = ((1 - alpha) * 100).ToString("F1") + "% CI";
            LowerColumn.Header = (alpha * 100).ToString("F1") + "% CI";
        }

        /// <summary>
        /// Sets the column StringFormats. Called from the constructor (must run before first
        /// render) so binding values display in the user's preferred format.
        /// </summary>
        private void SetColumnStringFormats()
        {
            // ZColumn holds the response (Z) output value — not an AEP — so no special format
            // is applied; the default binding format is sufficient for a linear response value.
            // AEP columns use scientific notation to convey small probabilities clearly.
            UpperColumn.Binding.StringFormat = "E4";
            LowerColumn.Binding.StringFormat = "E4";
            PredictiveColumn.Binding.StringFormat = "E4";
            ModeColumn.Binding.StringFormat = "E4";
        }

        /// <summary>
        /// Binds the summary statistics datagrid. Mirrors the CompositeAnalysisControl pattern:
        /// obtains an empirical distribution, then computes Min/Max/Mean/StdDev/Skewness/Kurtosis
        /// from its central moments.
        /// </summary>
        private void BindSummaryStatisticsDataGrid()
        {
            var stats = new System.Collections.ObjectModel.ObservableCollection<SummaryStatistic>();
            if (Element?.BayesianAnalysis != null)
            {
                StatValueColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";
            }

            if (Element?.IsEstimated == true)
            {
                var empDist = Element.GetPointEstimateDistribution();
                if (empDist != null && Element.ZOutputValues != null && Element.ZOutputValues.Length > 0)
                {
                    try
                    {
                        var z = Element.ZOutputValues;
                        var moments = empDist.CentralMoments(1000); // [mean, stdDev, skew, kurt]
                        stats.Add(new SummaryStatistic("Minimum", z[0]));
                        stats.Add(new SummaryStatistic("Maximum", z[z.Length - 1]));
                        stats.Add(new SummaryStatistic("Mean", moments[0]));
                        stats.Add(new SummaryStatistic("Std Dev", moments[1]));
                        stats.Add(new SummaryStatistic("Skewness", moments[2]));
                        stats.Add(new SummaryStatistic("Kurtosis", moments[3]));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"BindSummaryStatisticsDataGrid: {ex.Message}");
                    }
                }
            }
            if (stats.Count == 0)
            {
                foreach (var name in new[] { "Minimum", "Maximum", "Mean", "Std Dev", "Skewness", "Kurtosis" })
                    stats.Add(new SummaryStatistic(name, double.NaN));
            }
            SummaryStatisticsTable.ItemsSource = stats;
        }

        #endregion

        #region M×N Response Grid (Bivariate Response tab)

        private System.Data.DataTable _responseTable;
        private bool _isResponsePasting;

        /// <summary>
        /// True while the current cell edit is being pushed back to <c>Element.BivariateResponse</c>.
        /// The setter fires <c>PropertyChanged(nameof(BivariateResponse))</c> which would otherwise
        /// re-enter <see cref="Element_PropertyChanged"/> and trigger <see cref="RebuildBivariateGrid"/>,
        /// rebuilding the DataTable mid-edit and killing the user's keyboard focus. The flag suppresses
        /// the rebuild while a self-originated cell-edit assignment is in flight; it does NOT suppress
        /// rebuilds from other sources (auto-resize from X/Y change, undo replay).
        /// </summary>
        private bool _suppressBivariateResponseRebuild;

        /// <summary>
        /// Rebuilds the M×N response DataTable from the current Element.BivariateResponse.
        /// X values become row headers (via LoadingRow); Y values become column headers (via
        /// AutoGeneratingColumn, which strips a "&gt;i" uniqueness suffix).
        /// </summary>
        private void RebuildBivariateGrid()
        {
            if (Element == null) return;

            BivariateDataGrid.ItemsSource = null;
            if (_responseTable != null) _responseTable.ColumnChanged -= ResponseTable_ColumnChanged;

            _responseTable = new System.Data.DataTable();
            int rows = Element.XValues.Count;
            int cols = Element.YValues.Count;

            for (int j = 0; j < cols; j++)
            {
                // ">i" suffix on column key to allow duplicate Y values without DataTable conflict
                // (TotalRisk pattern). AutoGeneratingColumn handler strips the suffix for display.
                string key = Element.YValues[j].ToString(System.Globalization.CultureInfo.InvariantCulture).Replace(".", "_") + ">" + (j + 1);
                _responseTable.Columns.Add(key, typeof(double));
            }
            for (int i = 0; i < rows; i++)
                _responseTable.Rows.Add(_responseTable.NewRow());

            var response = Element.BivariateResponse;
            if (response != null)
            {
                int rR = response.GetLength(0);
                int rC = response.GetLength(1);
                for (int i = 0; i < Math.Min(rows, rR); i++)
                    for (int j = 0; j < Math.Min(cols, rC); j++)
                        _responseTable.Rows[i][j] = response[i, j];
            }

            _responseTable.ColumnChanged += ResponseTable_ColumnChanged;
            BivariateDataGrid.ItemsSource = _responseTable.DefaultView;
            ScheduleBivariateGridValidation();
        }

        /// <summary>
        /// Handles the <c>ColumnChanged</c> event for <c>ResponseTable</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void ResponseTable_ColumnChanged(object sender, System.Data.DataColumnChangeEventArgs e)
        {
            if (Element == null || _isResponsePasting) return;
            if (Element.IsUndoEnabled == false) return; // skip during undo replay / Open

            int row = _responseTable.Rows.IndexOf(e.Row);
            int col = e.Column.Ordinal;
            if (row < 0 || col < 0) return;
            if (Element.BivariateResponse == null) return;
            if (row >= Element.BivariateResponse.GetLength(0) || col >= Element.BivariateResponse.GetLength(1)) return;

            double value = CoincidentFrequencyResponseGridValidator.ConvertCellValue(e.Row[col]) ?? double.NaN;
            // Snapshot, mutate one cell, assign back through the property setter (records snapshot undo).
            // Suppress the resulting BivariateResponse PropertyChanged from triggering a self-rebuild
            // — the user's edit is already reflected in the DataGrid, and rebuilding kills focus.
            var next = (double[,])Element.BivariateResponse.Clone();
            next[row, col] = value;
            _suppressBivariateResponseRebuild = true;
            try { Element.BivariateResponse = next; }
            finally { _suppressBivariateResponseRebuild = false; }
            ValidateBivariateGridCells();
        }

        /// <summary>
        /// Handles the <c>AutoGeneratingColumn</c> event for <c>BivariateResultsTable</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void BivariateResultsTable_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Strip the ">i" suffix and restore "." in place of "_".
            string raw = e.PropertyName ?? string.Empty;
            int gt = raw.IndexOf('>');
            string header = gt >= 0 ? raw.Substring(0, gt) : raw;
            header = header.Replace("_", ".");
            e.Column = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding("[" + raw + "]")
                {
                    StringFormat = "G",
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
                },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 50,
            };
        }

        /// <summary>
        /// Handles the <c>AutoGeneratedColumns</c> event for <c>BivariateResultsTable</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void BivariateResultsTable_AutoGeneratedColumns(object sender, EventArgs e)
        {
            // Reserved for post-generation styling (not currently needed).
        }

        /// <summary>
        /// Handles the <c>LoadingRow</c> event for <c>BivariateResultsTable</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void BivariateResultsTable_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (Element == null) return;
            int idx = e.Row.GetIndex();
            if (idx >= 0 && idx < Element.XValues.Count)
                e.Row.Header = Element.XValues[idx].ToString(System.Globalization.CultureInfo.InvariantCulture);
            ScheduleBivariateGridRowValidation(idx);
        }

        /// <summary>
        /// Handles the <c>PreviewPasteData</c> event for <c>BivariateDataGrid</c>.
        /// </summary>
        /// <param name="clipboardData">The pasted clipboard cells.</param>
        /// <param name="cancelPaste">Set to <see langword="true"/> to cancel the operation.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void BivariateDataGrid_PreviewPasteData(string[][] clipboardData, ref bool cancelPaste)
        {
            _isResponsePasting = true;
            Mouse.OverrideCursor = Cursors.Wait;
        }

        /// <summary>
        /// Handles the <c>DataPasted</c> event for <c>BivariateDataGrid</c>.
        /// </summary>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void BivariateDataGrid_DataPasted()
        {
            _isResponsePasting = false;
            try
            {
                if (Element == null || _responseTable == null) return;
                int rows = _responseTable.Rows.Count;
                int cols = _responseTable.Columns.Count;
                var next = new double[rows, cols];
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        next[i, j] = CoincidentFrequencyResponseGridValidator.ConvertCellValue(_responseTable.Rows[i][j]) ?? double.NaN;
                // Suppress the rebuild that would otherwise fire from the BivariateResponse
                // PropertyChanged — the DataGrid already reflects the pasted values.
                _suppressBivariateResponseRebuild = true;
                try { Element.BivariateResponse = next; }
                finally { _suppressBivariateResponseRebuild = false; }
                ValidateBivariateGridCells();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Queues whole-grid response-surface validation after WPF has generated visible cells.
        /// </summary>
        /// <remarks>
        /// DataGrid cells are virtualized; immediate validation after assigning ItemsSource can
        /// run before cell containers exist. Scheduling at Loaded priority lets visible cells
        /// be painted without forcing scroll or realization side effects.
        /// </remarks>
        private void ScheduleBivariateGridValidation()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ScheduleBivariateGridValidation));
                return;
            }

            Dispatcher.BeginInvoke(new Action(ValidateBivariateGridCells), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Queues validation for a row after WPF has generated its visible cell containers.
        /// </summary>
        /// <param name="rowIndex">The zero-based row index.</param>
        /// <remarks>
        /// This keeps validation styling correct when the grid virtualizes rows and creates
        /// cells later as the user scrolls.
        /// </remarks>
        private void ScheduleBivariateGridRowValidation(int rowIndex)
        {
            if (rowIndex < 0) return;
            Dispatcher.BeginInvoke(
                new Action(() => ValidateBivariateGridRow(rowIndex)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Validates and styles every currently realized response-grid cell.
        /// </summary>
        /// <remarks>
        /// Neighboring cells are revalidated together because one edit can make adjacent
        /// values invalid under the strict-increase surface rule.
        /// </remarks>
        private void ValidateBivariateGridCells()
        {
            if (_responseTable == null) return;

            var response = BuildResponseSurfaceFromTable();
            for (int i = 0; i < _responseTable.Rows.Count; i++)
                for (int j = 0; j < _responseTable.Columns.Count; j++)
                    ApplyBivariateCellValidation(i, j, response);
        }

        /// <summary>
        /// Validates and styles every currently realized cell in a response-grid row.
        /// </summary>
        /// <param name="rowIndex">The zero-based row index.</param>
        /// <remarks>
        /// Row-level validation is used by <see cref="BivariateResultsTable_LoadingRow"/> so
        /// virtualized rows receive the same validation styling as initially visible rows.
        /// </remarks>
        private void ValidateBivariateGridRow(int rowIndex)
        {
            if (_responseTable == null || rowIndex < 0 || rowIndex >= _responseTable.Rows.Count) return;

            var response = BuildResponseSurfaceFromTable();
            for (int j = 0; j < _responseTable.Columns.Count; j++)
                ApplyBivariateCellValidation(rowIndex, j, response);
        }

        /// <summary>
        /// Builds a nullable response surface from the editable DataTable.
        /// </summary>
        /// <returns>A nullable response surface where <c>null</c> represents a blank or non-convertible cell.</returns>
        /// <remarks>
        /// Keeping blanks as <c>null</c> lets the validation layer distinguish missing
        /// entries from true zero response values.
        /// </remarks>
        private double?[,] BuildResponseSurfaceFromTable()
        {
            int rows = _responseTable?.Rows.Count ?? 0;
            int cols = _responseTable?.Columns.Count ?? 0;
            var response = new double?[rows, cols];

            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    response[i, j] = CoincidentFrequencyResponseGridValidator.ConvertCellValue(_responseTable.Rows[i][j]);

            return response;
        }

        /// <summary>
        /// Applies validation styling to one realized response-grid cell.
        /// </summary>
        /// <param name="row">The zero-based row index.</param>
        /// <param name="column">The zero-based column index.</param>
        /// <param name="response">The nullable response surface used for validation.</param>
        /// <remarks>
        /// The method does nothing for virtualized cells that do not currently have a
        /// visual container; those cells are validated when WPF later loads their row.
        /// </remarks>
        private void ApplyBivariateCellValidation(int row, int column, double?[,] response)
        {
            var cell = GetDataGridCell(BivariateDataGrid, row, column);
            if (cell == null) return;

            var validation = CoincidentFrequencyResponseGridValidator.ValidateCell(response, row, column);
            SetCellValidation(cell, validation);
        }

        /// <summary>
        /// Applies or clears the invalid-cell visual state.
        /// </summary>
        /// <param name="cell">The DataGrid cell to update.</param>
        /// <param name="validation">The validation result for the cell.</param>
        /// <remarks>
        /// The visual treatment mirrors the TotalRisk bivariate-response grid while using
        /// BestFit's response-surface validation rules.
        /// </remarks>
        private static void SetCellValidation(DataGridCell cell, ResponseGridCellValidationResult validation)
        {
            if (!validation.IsValid)
            {
                cell.SetResourceReference(Control.BackgroundProperty, "DataGrid.Cell.Error.Background");
                cell.SetResourceReference(Control.BorderBrushProperty, "DataGrid.Cell.Error.Border");
                cell.BorderThickness = new Thickness(1.0);
                cell.ToolTip = validation.ToolTip;
                return;
            }

            cell.ClearValue(Control.BorderThicknessProperty);
            cell.ClearValue(Control.BorderBrushProperty);
            cell.ClearValue(Control.BackgroundProperty);
            cell.ClearValue(Control.PaddingProperty);
            cell.ClearValue(FrameworkElement.MarginProperty);
            cell.ClearValue(FrameworkElement.ToolTipProperty);
        }

        /// <summary>
        /// Gets a DataGrid cell visual when it is currently realized.
        /// </summary>
        /// <param name="dataGrid">The DataGrid that owns the cell.</param>
        /// <param name="rowIndex">The zero-based row index.</param>
        /// <param name="columnIndex">The zero-based column index.</param>
        /// <returns>The realized cell, or <c>null</c> when the row/cell is virtualized.</returns>
        /// <remarks>
        /// This lookup intentionally does not scroll the grid into view, avoiding focus
        /// movement or unexpected virtualization churn while the user is editing.
        /// </remarks>
        private static DataGridCell GetDataGridCell(DataGrid dataGrid, int rowIndex, int columnIndex)
        {
            if (dataGrid == null || rowIndex < 0 || columnIndex < 0 || columnIndex >= dataGrid.Columns.Count)
                return null;

            var row = dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
            if (row == null) return null;

            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            return presenter?.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
        }

        /// <summary>
        /// Finds the first visual child of the requested type.
        /// </summary>
        /// <typeparam name="T">The child type to find.</typeparam>
        /// <param name="parent">The parent visual.</param>
        /// <returns>The first matching child, or <c>null</c> when none exists.</returns>
        /// <remarks>
        /// DataGrid rows place cells inside a <see cref="DataGridCellsPresenter"/>; this
        /// helper keeps that lookup local to the response-grid validation code.
        /// </remarks>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;

                var descendant = FindVisualChild<T>(child);
                if (descendant != null) return descendant;
            }

            return null;
        }

        #endregion

        #region Frequency Plot tab — alternatives, header click

        /// <summary>
        /// Handles the <c>MouseLeftButtonUp</c> event for <c>TextBlock</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Header text click toggles the +/- ToggleButton, mirroring Composite.
            ShowFilterToggleButton.IsChecked = !(ShowFilterToggleButton.IsChecked ?? false);
        }

        /// <summary>
        /// Handles the AnalysisAdded event from the AlternativeSelector. Adds the alternative
        /// analysis's CredibleIntervals (AreaSeries), PosteriorPredictive, and PosteriorMode
        /// series to the frequency plot. Mirrors <see cref="CompositeAnalysisControl.AlternativeSelector_AnalysisAdded"/>
        /// but with the X (AEP) and Y (Z response) axes swapped for the CFA orientation —
        /// AreaSeries DataField mappings draw a horizontal CI band at fixed Z that varies
        /// in AEP, rather than a vertical band at fixed AEP that varies in discharge.
        /// </summary>
        /// <param name="analysisItem">The selected alternative analysis. Guaranteed to be a
        /// <see cref="CoincidentFrequencyAnalysis"/> by the <c>AlternativeFilterType</c> set
        /// on <c>AlternativeSelector</c> in the constructor.</param>
        private void AlternativeSelector_AnalysisAdded(AnalysisAlternativeItem analysisItem)
        {
            // Defensive: remove any stale instances of this item's series that might have been
            // added on a previous AnalysisAdded firing (mirrors Composite).
            FrequencyPlot.Series.Remove(analysisItem.CredibleIntervals);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorPredictive);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorMode);

            var alternative = analysisItem.Alternative as CoincidentFrequencyAnalysis;
            if (alternative == null) return;
            if (alternative.IsEstimated != true || alternative.AnalysisResults == null || alternative.ZOutputValues == null) return;

            // Pick a unique color: walk the palette, skip colors already used by another
            // alternative's PosteriorMode series, and fall back to the last color if all
            // are taken. Mirrors CompositeAnalysisControl's color-uniqueness logic.
            var index = FrequencyPlot.Series.Count;
            Color lineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[index]);
            Color fillColor = Color.FromArgb(100, lineColor.R, lineColor.G, lineColor.B);

            for (int i = 4; i < _colorHexCodes.Length; i++)
            {
                var templineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[i]);
                var tempfillColor = Color.FromArgb(100, templineColor.R, templineColor.G, templineColor.B);
                bool colorExists = false;
                for (int j = 0; j < FrequencyPlot.Series.Count; j++)
                {
                    if (FrequencyPlot.Series[j].Name != null
                        && FrequencyPlot.Series[j].Name.Contains("Mode")
                        && FrequencyPlot.Series[j].Color == templineColor)
                    {
                        colorExists = true;
                        break;
                    }
                }
                if (colorExists == false || i == _colorHexCodes.Length - 1)
                {
                    lineColor = templineColor;
                    fillColor = tempfillColor;
                    break;
                }
            }

            // Build the data: one Point3D per Z bin holding (z, lower_AEP, upper_AEP).
            // The DataField mapping (below) draws an AreaSeries that, at each z, stretches
            // horizontally from lower_AEP to upper_AEP along the X axis.
            var z = alternative.ZOutputValues;
            var ar = alternative.AnalysisResults;
            int n = z.Length;
            var ciPoints = new List<Point3D>(n);
            var prdPoints = new List<DataPoint>(n);
            var mdPoints = new List<DataPoint>(n);
            for (int i = 0; i < n; i++)
            {
                double lo = ar.ConfidenceIntervals != null ? ar.ConfidenceIntervals[i, 0] : double.NaN;
                double up = ar.ConfidenceIntervals != null ? ar.ConfidenceIntervals[i, 1] : double.NaN;
                double prd = ar.MeanCurve != null ? ar.MeanCurve[i] : double.NaN;
                double md = ar.ModeCurve != null ? ar.ModeCurve[i] : double.NaN;
                ciPoints.Add(new Point3D(z[i], lo, up));
                prdPoints.Add(new DataPoint(prd, z[i]));
                mdPoints.Add(new DataPoint(md, z[i]));
            }

            // Series labels.
            double ciWidth = alternative.BayesianAnalysis.CredibleIntervalWidth;
            string ciLabel = "% Credible Intervals";
            string predLabel = " - Posterior Predictive";
            string pointLabel = " - " + (alternative.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode");

            // Credible Intervals — AreaSeries with X/Y swapped vs the Composite layout to
            // produce a horizontal band at fixed z (Y) varying along AEP (X).
            //   primary curve:   X=Point3D.Y=lo,  Y=Point3D.X=z
            //   secondary curve: X=Point3D.Z=up,  Y=Point3D.X=z
            analysisItem.CredibleIntervals.Name = "CredibleIntervals_" + index;
            analysisItem.CredibleIntervals.Title = alternative.Name + " - " + (ciWidth * 100).ToString("F0") + ciLabel;
            analysisItem.CredibleIntervals.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
            analysisItem.CredibleIntervals.ItemsSource = ciPoints;
            analysisItem.CredibleIntervals.DataFieldX = "Y";
            analysisItem.CredibleIntervals.DataFieldY = "X";
            analysisItem.CredibleIntervals.DataFieldX2 = "Z";
            analysisItem.CredibleIntervals.DataFieldY2 = "X";
            analysisItem.CredibleIntervals.Decimator = OxyPlot.Decimator.Decimate;
            analysisItem.CredibleIntervals.Fill = fillColor;
            analysisItem.CredibleIntervals.Color = Colors.Transparent;
            FrequencyPlot.Series.Add(analysisItem.CredibleIntervals);

            // Posterior Predictive — LineSeries with default DataPoint X/Y mapping (X=AEP, Y=z).
            analysisItem.PosteriorPredictive.Name = "PosteriorPredictive_" + index;
            analysisItem.PosteriorPredictive.Title = alternative.Name + predLabel;
            analysisItem.PosteriorPredictive.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
            analysisItem.PosteriorPredictive.ItemsSource = prdPoints;
            analysisItem.PosteriorPredictive.Decimator = OxyPlot.Decimator.Decimate;
            analysisItem.PosteriorPredictive.MinimumSegmentLength = 4.0;
            analysisItem.PosteriorPredictive.Color = lineColor;
            FrequencyPlot.Series.Add(analysisItem.PosteriorPredictive);

            // Point Estimator (Posterior Mean / Mode) — LineSeries with default DataPoint mapping.
            analysisItem.PosteriorMode.Name = "PosteriorMode_" + index;
            analysisItem.PosteriorMode.Title = alternative.Name + pointLabel;
            analysisItem.PosteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
            analysisItem.PosteriorMode.ItemsSource = mdPoints;
            analysisItem.PosteriorMode.Decimator = OxyPlot.Decimator.Decimate;
            analysisItem.PosteriorMode.MinimumSegmentLength = 4.0;
            analysisItem.PosteriorMode.Color = lineColor;
            FrequencyPlot.Series.Add(analysisItem.PosteriorMode);

            FrequencyPlot.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles the AnalysisRemoved event from the AlternativeSelector. Removes the
        /// alternative's CredibleIntervals, PosteriorPredictive, and PosteriorMode series
        /// from the frequency plot. Mirrors <see cref="CompositeAnalysisControl.AlternativeSelector_AnalysisRemoved"/>.
        /// </summary>
        /// <param name="analysisItem">The alternative analysis item being deselected.</param>
        private void AlternativeSelector_AnalysisRemoved(AnalysisAlternativeItem analysisItem)
        {
            FrequencyPlot.Series.Remove(analysisItem.CredibleIntervals);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorPredictive);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorMode);
            FrequencyPlot.InvalidatePlot(true);
        }

        #endregion
    }
}
