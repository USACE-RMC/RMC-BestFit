using Numerics.Sampling;
using OxyPlot.Wpf;
using OxyPlot;
using OxyPlotControls;
using FrameworkInterfaces;
using RMC.BestFit.Models;
using ModelAnalyses = RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using FrameworkUI;
using System.Windows.Media.Media3D;
using Numerics.Distributions;
using Numerics.Data.Statistics;
using Numerics;
using System.Xml.Serialization;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for rating curve analysis visualization and interaction.
    /// Provides plotting capabilities for rating curves, residuals, histograms, and diagnostic plots,
    /// along with data tables for analysis results and summary statistics.
    /// </summary>
    public partial class RatingCurveAnalysisControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RatingCurveAnalysisControl"/> class.
        /// Sets up default color schemes for plot visualization.
        /// </summary>
        public RatingCurveAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
            // Column StringFormat must be set before first render (Binding.StringFormat
            // throws "Binding cannot be changed after it has been used" if re-assigned
            // on a used binding). Constructor is the only safe place — once per control
            // instance, regardless of Element or Load/Unload cycles.
            SetColumnStringFormats();
        }

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(RatingCurveAnalysis), typeof(RatingCurveAnalysisControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the rating curve analysis element to display and analyze.
        /// </summary>
        public RatingCurveAnalysis Element
        {
            get { return (RatingCurveAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Element"/> property changes.
        /// Manages event handler subscriptions and unsubscriptions for the old and new elements.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as RatingCurveAnalysisControl == null) return;
            var thisControl = (RatingCurveAnalysisControl)d;

            // Remove handlers
            if (e.OldValue != null)
            {
                RatingCurveAnalysis oldElement = e.OldValue as RatingCurveAnalysis;
                if (oldElement != null)
                {
                    oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                    thisControl.RatingCurvePlotHost.Content = null;
                    thisControl.ResidualPlotHost.Content = null;
                    thisControl.ResidualHistogramPlotHost.Content = null;
                    thisControl.ResidualQQPlotHost.Content = null;
                    thisControl.PlotToolbar.Plot = null;
                    thisControl.ResidualsPlotToolbar.Plot = null;
                    thisControl.HistogramPlotToolbar.Plot = null;
                    thisControl.QQPlotToolbar.Plot = null;
                    // NOTE: PropertiesCalled is wired in XAML for every toolbar — no programmatic -= needed.
                }
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as RatingCurveAnalysis;
            if (newElement == null) return;

            // Reset _isLoaded so the next Loaded event re-runs one-time setup steps
            // for the new element (mirrors BivariateAnalysisControl:85).
            thisControl._isLoaded = false;

            // Subscribe Element-scoped handler
            newElement.PropertyChanged += thisControl.Element_PropertyChanged;

            // Attach Element-owned plots to hosts and wire toolbars. Suspend plot bridges
            // so WPF visual-tree attachment doesn't record "Change Title" / axis-style
            // undo entries. PropertiesCalled / PlotPropertiesCalled are wired in XAML on
            // every toolbar and every Bayesian sub-control — no programmatic += needed.
            using (newElement.SuspendPlotBridges())
            {
                thisControl.RatingCurvePlotHost.Content = newElement.RatingCurvePlot;
                thisControl.PlotToolbar.Plot = newElement.RatingCurvePlot;
                thisControl.ResidualPlotHost.Content = newElement.ResidualPlot;
                thisControl.ResidualsPlotToolbar.Plot = newElement.ResidualPlot;
                thisControl.ResidualHistogramPlotHost.Content = newElement.ResidualHistogramPlot;
                thisControl.HistogramPlotToolbar.Plot = newElement.ResidualHistogramPlot;
                thisControl.ResidualQQPlotHost.Content = newElement.ResidualQQPlot;
                thisControl.QQPlotToolbar.Plot = newElement.ResidualQQPlot;

                thisControl.HistogramControl.SetPlot(newElement.BayesianPlots.HistogramPlot);
                thisControl.KernelDensityControl.SetPlot(newElement.BayesianPlots.KernelDensityPlot);
                thisControl.AutocorrelationControl.SetPlot(newElement.BayesianPlots.AutocorrelationPlot);
                thisControl.MarkovChainTraceControl.SetPlot(newElement.BayesianPlots.MarkovChainTracePlot);
                thisControl.MeanLikelihoodControl.SetPlot(newElement.BayesianPlots.MeanLikelihoodPlot);
                thisControl.BivariateHeatMapControl.SetPlot(newElement.BayesianPlots.BivariateHeatMapPlot);
                thisControl.InfluenceDiagnosticsControl.SetPlot(newElement.BayesianPlots.InfluenceDiagnosticsPlot);

                thisControl.BindAxisTitles();
            }
        }

        /// <summary>
        /// Indicates whether the control has completed its initial load operations.
        /// Set to false by ElementCallback when a new element is assigned so the next
        /// Loaded event re-runs one-time setup steps.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>Convenience accessor for the Element-owned rating curve plot.</summary>
        private Plot RatingCurvePlot => Element?.RatingCurvePlot;
        /// <summary>Convenience accessor for the Element-owned residual plot.</summary>
        private Plot ResidualPlot => Element?.ResidualPlot;
        /// <summary>Convenience accessor for the Element-owned residual histogram plot.</summary>
        private Plot ResidualHistogramPlot => Element?.ResidualHistogramPlot;
        /// <summary>Convenience accessor for the Element-owned residual QQ plot.</summary>
        private Plot ResidualQQPlot => Element?.ResidualQQPlot;

        /// <summary>
        /// Gets a value indicating whether a plot area was clicked by the user.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Raised when plot properties are requested to be displayed.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Delegate for the <see cref="PlotPropertiesCalled"/> event.
        /// </summary>
        /// <param name="plot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">Indicates whether to open the properties panel.</param>
        /// <param name="propertyExpander">The property expander control to display.</param>
        /// <param name="selectedObject">The selected object in the plot.</param>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);

        /// <summary>
        /// Raised when the preview control area is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for the <see cref="PreviewControlClicked"/> event.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot area was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar area was clicked.</param>
        /// <param name="plot">The plot that was clicked.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Stores the calculated residuals from the rating curve analysis.
        /// </summary>
        private double[] _residuals = null;

        /// <summary>
        /// Array of hexadecimal color codes used for plot series styling.
        /// </summary>
        private string[] _colorHexCodes;

        #region Plot Series

        /// <summary>
        /// POCO bound to the rating-curve observed-scatter series. Public properties
        /// (not ValueTuple fields) are what OxyPlot's tracker format string reflects
        /// over when it resolves placeholders like <c>{Date}</c>.
        /// </summary>
        private sealed class StageDischargeObservation
        {
            public string Date { get; }
            public double Stage { get; }
            public double Discharge { get; }
            public StageDischargeObservation(DateTime date, double stage, double discharge)
            {
                Date = date.ToString("yyyy-MM-dd");
                Stage = stage;
                Discharge = discharge;
            }
        }

        #endregion

        /// <summary>
        /// Handles the Loaded event of the user control.
        /// Initializes plots, data grids, and visual settings when the control is first loaded.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            // Data refresh on every load. Plot hosts and Element.PropertyChanged are wired
            // once per Element in ElementCallback; this method just refreshes tabular /
            // bound content so the UI reflects whatever state the Element is currently in
            // (fresh load, returning from a tab switch, etc.).
            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                UpdatePlots();
                BindRatingCurveDataGrid();

                // One-time setup: column headers and summary grid layout do not need to
                // re-run on every transient visual-tree cycle — only on the first load
                // after a new Element is assigned (RC1).
                if (!_isLoaded)
                {
                    SetRatingCurveTableColumnHeaders();
                    BindSummaryStatisticsDataGrid();
                }
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
            _isLoaded = true;
        }

        /// <summary>
        /// Handles the Unloaded event of the user control. Unsubscribes from element event handlers
        /// to prevent memory leaks and stale event subscriptions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Reset _isLoaded so the next Loaded event re-runs one-time setup steps (RC2).
            // Element-scoped lifecycle (PropertyChanged, plot hosts, toolbars) is owned by
            // ElementCallback and survives unload/reload cycles — no additional teardown needed.
            _isLoaded = false;
        }

        /// <summary>
        /// Handles property changes on the <see cref="Element"/>.
        /// Updates plots and data displays when relevant properties change.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Marshal to UI thread if called from a background thread. The model layer's
            // ReprocessIfEstimated path uses TaskScheduler.Default and AnalysisBase.RaisePropertyChange
            // does NOT marshal — so the AnalysisResults notification after a stage-bin reprocess
            // can arrive here on a worker thread.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Element_PropertyChanged(sender, e)));
                return;
            }

            if (e.PropertyName == nameof(Element.StageData) || e.PropertyName == nameof(Element.DischargeData))
            {
                // Re-bind axis titles to the new source TimeSeriesElement. Wrap in
                // SuspendPlotBridges so default-axis title updates don't
                // record "Change Title" undo entries (matches Convention 12).
                using (Element.SuspendPlotBridges())
                {
                    BindAxisTitles();
                }
            }
            if (e.PropertyName == nameof(Element.AnalysisResults))
            {
                UpdatePlots();
                BindRatingCurveDataGrid();
                BindSummaryStatisticsDataGrid();
                ResetWaitCursor();
            }
            if (e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth))
            {
                SetRatingCurveTableColumnHeaders();
            }
            // MinStage/MaxStage/StageBins reprocess is now owned by RatingCurveAnalysis itself
            // (Phase 3a). The setter dispatches CreateUncertaintyAnalysisResultsAsync as a
            // fire-and-forget task and the AnalysisResults PropertyChanged branch above
            // refreshes the plots and tables when the rebuild completes.
            if ((e.PropertyName == nameof(Element.BayesianAnalysis.PointEstimator)
                 || e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth)
                 || e.PropertyName == nameof(Element.MinStage)
                 || e.PropertyName == nameof(Element.MaxStage)
                 || e.PropertyName == nameof(Element.StageBins))
                && Element.IsEstimated)
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

        #region Plots

        /// <summary>
        /// Handles the LostFocus event of the user control.
        /// Resets the <see cref="PlotClicked"/> flag when focus is lost.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the PreviewMouseDown event of the user control.
        /// Determines which plot or toolbar was clicked and raises appropriate events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Plot plot = GetCurrentPlot();
            OxyPlotToolbar toolbar = GetCurrentPlotToolbar();
            System.Windows.Media.HitTestResult plotHitResult = null;
            System.Windows.Media.HitTestResult toolbarHitResult = null;
            if (plot != null)
            {
                plotHitResult = VisualTreeHelper.HitTest(plot, e.GetPosition(plot));
            }
            if (toolbar != null)
            {
                toolbarHitResult = VisualTreeHelper.HitTest(toolbar, e.GetPosition(toolbar));
            }
            PreviewControlClicked?.Invoke(plotHitResult != null, toolbarHitResult != null, plot);
            PlotClicked = plotHitResult != null;
        }

        /// <summary>
        /// Handles the PropertiesCalled event from the plot toolbar.
        /// Forwards the event to subscribers of <see cref="PlotPropertiesCalled"/>.
        /// </summary>
        /// <param name="targetPlot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">Indicates whether to open the properties panel.</param>
        /// <param name="propertyExpander">The property expander control to display.</param>
        /// <param name="selectedObject">The selected object in the plot.</param>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        /// <summary>
        /// Gets the currently selected plot based on the active tab item.
        /// </summary>
        /// <returns>The currently active <see cref="Plot"/> object, or null if no plot tab is selected.</returns>
        public Plot GetCurrentPlot()
        {
            if (PlotTabItem.IsSelected == true)
            {
                return RatingCurvePlot;
            }
            else if (ResidualsPlotTab.IsSelected == true)
            {
                return ResidualPlot;
            }
            else if (ResidualHistogramTab.IsSelected == true)
            {
                return ResidualHistogramPlot;
            }
            else if (ResidualQQTab.IsSelected == true)
            {
                return ResidualQQPlot;
            }
            else if (KernelDensityTabItem.IsSelected == true)
            {
                return KernelDensityControl.Plot;
            }
            else if (HistogramTabItem.IsSelected == true)
            {
                return HistogramControl.Plot;
            }
            else if (BivariateTabItem.IsSelected == true)
            {
                return BivariateHeatMapControl.Plot;
            }
            else if (MeanLikelihoodTabItem.IsSelected == true)
            {
                return MeanLikelihoodControl.Plot;
            }
            else if (AutocorrelationTabItem.IsSelected == true)
            {
                return AutocorrelationControl.Plot;
            }
            else if (MarkovChainTabItem.IsSelected == true)
            {
                return MarkovChainTraceControl.Plot;
            }
            else if (InfluenceDiagnosticsTabItem.IsSelected == true)
            {
                return InfluenceDiagnosticsControl.Plot;
            }
            return null;
        }

        /// <summary>
        /// Gets the toolbar for the currently selected plot based on the active tab item.
        /// </summary>
        /// <returns>The <see cref="OxyPlotToolbar"/> for the active plot, or null if no plot tab is selected.</returns>
        public OxyPlotToolbar GetCurrentPlotToolbar()
        {
            if (PlotTabItem.IsSelected == true)
            {
                return PlotToolbar;
            }
            else if (ResidualsPlotTab.IsSelected == true)
            {
                return ResidualsPlotToolbar;
            }
            else if (ResidualHistogramTab.IsSelected == true)
            {
                return HistogramPlotToolbar;
            }
            else if (ResidualQQTab.IsSelected == true)
            {
                return QQPlotToolbar;
            }
            else if (KernelDensityTabItem.IsSelected == true)
            {
                return KernelDensityControl.PlotToolbar;
            }
            else if (HistogramTabItem.IsSelected == true)
            {
                return HistogramControl.PlotToolbar;
            }
            else if (BivariateTabItem.IsSelected == true)
            {
                return BivariateHeatMapControl.PlotToolbar;
            }
            else if (MeanLikelihoodTabItem.IsSelected == true)
            {
                return MeanLikelihoodControl.PlotToolbar;
            }
            else if (AutocorrelationTabItem.IsSelected == true)
            {
                return AutocorrelationControl.PlotToolbar;
            }
            else if (MarkovChainTabItem.IsSelected == true)
            {
                return MarkovChainTraceControl.PlotToolbar;
            }
            else if (InfluenceDiagnosticsTabItem.IsSelected == true)
            {
                return InfluenceDiagnosticsControl.PlotToolbar;
            }
            return null;
        }

        /// <summary>
        /// Binds the rating-curve plot axis titles to the upstream TimeSeriesElement unit labels
        /// via reactive WPF bindings. Y-axis tracks <see cref="RatingCurveAnalysis.StageData"/>'s
        /// UnitLabel; X-axis tracks <see cref="RatingCurveAnalysis.DischargeData"/>'s UnitLabel.
        /// Called from <see cref="ElementCallback"/> (inside SuspendPlotBridges) and from
        /// <see cref="Element_PropertyChanged"/> when the source references change. Matches
        /// the canonical pattern in Convention 12.
        /// </summary>
        private void BindAxisTitles()
        {
            if (Element == null) return;
            if (RatingCurvePlot == null) return;

            var yAxis = RatingCurvePlot.Axes.FirstOrDefault(a => a.Key == "Yaxis");
            if (yAxis != null)
            {
                if (Element.StageData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(yAxis, Element.StageData, nameof(TimeSeriesElement.UnitLabel), Element.StageData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(yAxis, string.Empty);
            }

            var xAxis = RatingCurvePlot.Axes.FirstOrDefault(a => a.Key == "Xaxis");
            if (xAxis != null)
            {
                if (Element.DischargeData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(xAxis, Element.DischargeData, nameof(TimeSeriesElement.UnitLabel), Element.DischargeData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(xAxis, string.Empty);
            }
        }

        /// <summary>
        /// Updates all diagnostic plots with the latest analysis results.
        /// Refreshes the rating curve, residuals, histogram, and Q-Q plots.
        /// </summary>
        private void UpdatePlots()
        {
            UpdateRatingCurvePlot();
            GetResiduals();
            UpdateResidualsPlot();
            UpdateHistogramPlot();
            UpdateQQPlot();
        }

        /// <summary>
        /// Updates the main rating curve plot with observed data and fitted curves.
        /// Displays the stage-discharge data, credible intervals, posterior predictive, and point estimate curves.
        /// </summary>
        private void UpdateRatingCurvePlot()
        {
            if (RatingCurvePlot == null) return;

            // Lookup-or-create named series so user-customized styling survives Save/Open.
            var stageDischargeData = RatingCurvePlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "StageDischargeData")
                ?? new ScatterPointSeries
                {
                    Name = "StageDischargeData",
                    Title = "Stage-Discharge Data",
                    MarkerFill = Colors.Red,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                };
            var credibleIntervals = RatingCurvePlot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "CredibleIntervals")
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
            var posteriorPredictive = RatingCurvePlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorPredictive")
                ?? new LineSeries
                {
                    Name = "PosteriorPredictive",
                    Title = "Posterior Predictive",
                    Color = Colors.Blue,
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Dash,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
            var posteriorMode = RatingCurvePlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorMode")
                ?? new LineSeries
                {
                    Name = "PosteriorMode",
                    Title = "Posterior Mode",
                    Color = Colors.Black,
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Solid,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };

            using (Element?.SuspendPlotBridges())
            {
                RatingCurvePlot.Series.Clear();

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid != false)
                {
                    // Load the stage-discharge data using the same date-inner-join the
                    // model uses for the fit — observations with a date in only one series
                    // are excluded so the plotted scatter exactly matches the points the
                    // likelihood iterated. The observation date rides along as a public
                    // property on the POCO so the tracker format can resolve {Date}.
                    var stageDischageData = Element.GetAlignedObservations()
                        .Select(o => new StageDischargeObservation(o.Date, o.Stage, o.Discharge))
                        .ToList();

                    // Tracker format string used by every series on the rating curve plot.
                    string curveTracker = "{0}" + Environment.NewLine
                        + "{1}: {2:N0}" + Environment.NewLine
                        + "{3}: {4:N2}";
                    string observedTracker = "{0}" + Environment.NewLine
                        + "Date: {Date}" + Environment.NewLine
                        + "{1}: {2:N0}" + Environment.NewLine
                        + "{3}: {4:N2}";

                    // Add Curves
                    if (Element.BayesianAnalysis.IsEstimated == true && Element.AnalysisResults != null
                        && Element.AnalysisResults.ModeCurve.Length == Element.StageBins)
                    {
                        var ciPoints = new List<Point3D>();
                        var prdPoints = new List<OxyPlot.DataPoint>();
                        var mdPoints = new List<OxyPlot.DataPoint>();
                        for (int i = 0; i < Element.StageBins; i++)
                        {
                            var q = Element.AnalysisResults.ConfidenceIntervals[i, 0];
                            var lo = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                            var up = Element.AnalysisResults.ConfidenceIntervals[i, 2];
                            var prd = Element.AnalysisResults.MeanCurve[i];
                            var md = Element.AnalysisResults.ModeCurve[i];

                            ciPoints.Add(new Point3D(q, lo, up));
                            // Axes are swapped: X-axis = discharge (prd/md), Y-axis = stage (q)
                            prdPoints.Add(new OxyPlot.DataPoint(prd, q));
                            mdPoints.Add(new OxyPlot.DataPoint(md, q));
                        }

                        // Credible Intervals — keep DataFieldX/Y/X2/Y2 (swapped axes + dual boundary)
                        credibleIntervals.ItemsSource = ciPoints;
                        credibleIntervals.DataFieldX = "Y";
                        credibleIntervals.DataFieldY = "X";
                        credibleIntervals.DataFieldX2 = "Z";
                        credibleIntervals.DataFieldY2 = "X";
                        credibleIntervals.Title = (Element.BayesianAnalysis.CredibleIntervalWidth * 100).ToString("F0") + "% Credible Intervals";
                        credibleIntervals.TrackerFormatString = curveTracker;
                        RatingCurvePlot.Series.Add(credibleIntervals);

                        // Predictive
                        posteriorPredictive.ItemsSource = prdPoints;
                        posteriorPredictive.TrackerFormatString = curveTracker;
                        RatingCurvePlot.Series.Add(posteriorPredictive);

                        // Point Estimator
                        posteriorMode.Title = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";
                        posteriorMode.ItemsSource = mdPoints;
                        posteriorMode.TrackerFormatString = curveTracker;
                        RatingCurvePlot.Series.Add(posteriorMode);
                    }

                    // Add observed data.
                    stageDischargeData.ItemsSource = stageDischageData;
                    stageDischargeData.Mapping = item =>
                    {
                        var obs = (StageDischargeObservation)item;
                        return new OxyPlot.Series.ScatterPoint(obs.Discharge, obs.Stage);
                    };
                    stageDischargeData.TrackerFormatString = observedTracker;
                    RatingCurvePlot.Series.Add(stageDischargeData);
                }

                RatingCurvePlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(RatingCurvePlot);
        }

        /// <summary>
        /// Handles the addition of an alternative analysis to the plot.
        /// Adds the alternative's curves to the main rating curve plot with unique colors.
        /// </summary>
        /// <param name="analysisItem">The alternative analysis item to add to the plot.</param>
        private void AlternativeSelector_AnalysisAdded(AnalysisAlternativeItem analysisItem)
        {
            // Remove series
            RatingCurvePlot.Series.Remove(analysisItem.CredibleIntervals);
            RatingCurvePlot.Series.Remove(analysisItem.PosteriorPredictive);
            RatingCurvePlot.Series.Remove(analysisItem.PosteriorMode);

            var alternative = (RatingCurveAnalysis)analysisItem.Alternative;
            if (alternative.BayesianAnalysis.IsEstimated == true && alternative.AnalysisResults != null)
            {
                // Get unique color
                var index = RatingCurvePlot.Series.Count;
                Color lineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[index]);
                Color fillColor = Color.FromArgb(100, lineColor.R, lineColor.G, lineColor.B);

                for (int i = 4; i < _colorHexCodes.Length; i++)
                {
                    var templineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[i]);
                    var tempfillColor = Color.FromArgb(100, templineColor.R, templineColor.G, templineColor.B);
                    bool colorExists = false;
                    for (int j = 0; j < RatingCurvePlot.Series.Count; j++)
                    {
                        if (RatingCurvePlot.Series[j].Name.Contains("Mode") && RatingCurvePlot.Series[j].Color == templineColor)
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

                var ciPoints = new List<Point3D>();
                var prdPoints = new List<OxyPlot.DataPoint>();
                var mdPoints = new List<OxyPlot.DataPoint>();
                string curveTracker = "{0}" + Environment.NewLine
                    + "{1}: {2:N0}" + Environment.NewLine
                    + "{3}: {4:N2}";
                for (int i = 0; i < alternative.StageBins; i++)
                {
                    var q = alternative.AnalysisResults.ConfidenceIntervals[i, 0];
                    var lo = alternative.AnalysisResults.ConfidenceIntervals[i, 1];
                    var up = alternative.AnalysisResults.ConfidenceIntervals[i, 2];
                    var prd = alternative.AnalysisResults.MeanCurve[i];
                    var md = alternative.AnalysisResults.ModeCurve[i];

                    ciPoints.Add(new Point3D(q, lo, up));
                    // Axes are swapped: X-axis = discharge (prd/md), Y-axis = stage (q)
                    prdPoints.Add(new OxyPlot.DataPoint(prd, q));
                    mdPoints.Add(new OxyPlot.DataPoint(md, q));
                }

                // Credible Intervals — keep DataFieldX/Y/X2/Y2 (swapped axes + dual boundary)
                analysisItem.CredibleIntervals.Name = "CredibleIntervals_" + index;
                analysisItem.CredibleIntervals.Title = alternative.Name + " - " + (alternative.BayesianAnalysis.CredibleIntervalWidth * 100).ToString("F0") + "% Credible Intervals";
                analysisItem.CredibleIntervals.ItemsSource = ciPoints;
                analysisItem.CredibleIntervals.DataFieldX = "Y";
                analysisItem.CredibleIntervals.DataFieldY = "X";
                analysisItem.CredibleIntervals.DataFieldX2 = "Z";
                analysisItem.CredibleIntervals.DataFieldY2 = "X";
                analysisItem.CredibleIntervals.Fill = fillColor;
                analysisItem.CredibleIntervals.Color = Colors.Transparent;
                analysisItem.CredibleIntervals.TrackerFormatString = curveTracker;
                RatingCurvePlot.Series.Add(analysisItem.CredibleIntervals);

                // Predictive
                analysisItem.PosteriorPredictive.Name = "PosteriorPredictive_" + index;
                analysisItem.PosteriorPredictive.Title = alternative.Name + " - Posterior Predictive";
                analysisItem.PosteriorPredictive.ItemsSource = prdPoints;
                analysisItem.PosteriorPredictive.Color = lineColor;
                analysisItem.PosteriorPredictive.TrackerFormatString = curveTracker;
                RatingCurvePlot.Series.Add(analysisItem.PosteriorPredictive);

                // Point Estimator
                analysisItem.PosteriorMode.Name = "PosteriorMode_" + index;
                analysisItem.PosteriorMode.Title = alternative.Name + " - " + (alternative.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode");
                analysisItem.PosteriorMode.ItemsSource = mdPoints;
                analysisItem.PosteriorMode.Color = lineColor;
                analysisItem.PosteriorMode.TrackerFormatString = curveTracker;
                RatingCurvePlot.Series.Add(analysisItem.PosteriorMode);

                RatingCurvePlot.InvalidatePlot(true);

            }

        }

        /// <summary>
        /// Handles the removal of an alternative analysis from the plot.
        /// Removes the alternative's curves from the main rating curve plot.
        /// </summary>
        /// <param name="analysisItem">The alternative analysis item to remove from the plot.</param>
        private void AlternativeSelector_AnalysisRemoved(AnalysisAlternativeItem analysisItem)
        {
            RatingCurvePlot.Series.Remove(analysisItem.CredibleIntervals);
            RatingCurvePlot.Series.Remove(analysisItem.PosteriorPredictive);
            RatingCurvePlot.Series.Remove(analysisItem.PosteriorMode);
            RatingCurvePlot.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles the MouseLeftButtonUp event on the filter toggle text block.
        /// Toggles the visibility of the filter panel.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowFilterToggleButton.IsChecked = !ShowFilterToggleButton.IsChecked;
        }

        /// <summary>
        /// Calculates residuals from the rating curve model using the current parameter estimates.
        /// </summary>
        private void GetResiduals()
        {
            _residuals = null;
            if (Element == null || Element.BayesianAnalysis == null || Element.IsValid == false || Element.BayesianAnalysis.IsEstimated == false) return;
            _residuals = Element.RatingCurve.Residuals(Element.RatingCurve.Parameters.Select(x => x.Value).ToArray());
        }

        /// <summary>
        /// Updates the residuals plot showing residuals versus fitted values.
        /// Includes a zero reference line for visual assessment of model fit.
        /// </summary>
        private void UpdateResidualsPlot()
        {
            if (ResidualPlot == null) return;

            var residualsSeries = ResidualPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "Residuals")
                ?? new ScatterPointSeries
                {
                    Name = "Residuals",
                    Title = "Residuals",
                    MarkerFill = Colors.Black,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                };
            var zeroLine = ResidualPlot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "ZeroLine")
                ?? new LineAnnotation
                {
                    Name = "ZeroLine",
                    Text = "",
                    Type = OxyPlot.Annotations.LineAnnotationType.LinearEquation,
                    Color = Colors.Black,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 0.9,
                    Intercept = 0,
                    Slope = 0,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Top,
                };

            using (Element?.SuspendPlotBridges())
            {
                ResidualPlot.Series.Clear();
                for (int i = ResidualPlot.Annotations.Count - 1; i >= 0; i--)
                {
                    if (ResidualPlot.Annotations[i].Name == "ZeroLine")
                    {
                        ResidualPlot.Annotations.RemoveAt(i);
                        break;
                    }
                }

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid != false && Element.BayesianAnalysis.IsEstimated == true
                    && _residuals != null && _residuals.Length > 0)
                {
                    var fittedValues = Element.RatingCurve.FittedValues(Element.RatingCurve.Parameters.Select(x => x.Value).ToArray());
                    var points = new List<DataPoint>();
                    int pairCount = Math.Min(fittedValues.Length, _residuals.Length);
                    for (int i = 0; i < pairCount; i++)
                    {
                        double fitted = fittedValues[i];
                        double residual = _residuals[i];
                        if (double.IsNaN(fitted) || double.IsInfinity(fitted)) continue;
                        if (double.IsNaN(residual) || double.IsInfinity(residual)) continue;
                        points.Add(new DataPoint(fitted, residual));
                    }

                    residualsSeries.ItemsSource = points;
                    residualsSeries.Mapping = item => { var d = (DataPoint)item; return new OxyPlot.Series.ScatterPoint(d.X, d.Y); };
                    residualsSeries.TrackerFormatString = "{0}" + Environment.NewLine
                        + "{1}: {2:N3}" + Environment.NewLine
                        + "{3}: {4:N4}";
                    ResidualPlot.Series.Add(residualsSeries);
                    ResidualPlot.Annotations.Add(zeroLine);
                }

                ResidualPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(ResidualPlot);
        }

        /// <summary>
        /// Updates the histogram plot showing the distribution of residuals.
        /// Overlays a normal density curve for comparison with theoretical normal distribution.
        /// </summary>
        private void UpdateHistogramPlot()
        {
            if (ResidualHistogramPlot == null) return;

            var histogramSeries = ResidualHistogramPlot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "Residuals")
                ?? new HistogramSeries
                {
                    Name = "Residuals",
                    Title = "Residuals",
                    FillColor = Color.FromArgb(125, 104, 140, 175),
                    StrokeColor = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1,
                };
            var normalDensitySeries = ResidualHistogramPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "NormalDensity")
                ?? new LineSeries
                {
                    Name = "NormalDensity",
                    Title = "Normal Density",
                    Color = Colors.Black,
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Solid,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };

            using (Element?.SuspendPlotBridges())
            {
                ResidualHistogramPlot.Series.Clear();

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid != false && Element.BayesianAnalysis.IsEstimated == true
                    && _residuals != null && _residuals.Length > 0)
                {
                    var values = _residuals.Where(r => !double.IsNaN(r) && !double.IsInfinity(r)).ToArray();
                    if (values.Length >= 2)
                    {
                        // Use Sturges' rule
                        int k = (int)(1 + 3.322 * Math.Log(values.Length));
                        var histogram = new Histogram(values, k);
                        var histItems = new List<OxyPlot.Series.HistogramItem>();
                        double sum = 0;
                        for (int i = 0; i < histogram.NumberOfBins; i++)
                            sum += histogram[i].Frequency * histogram.BinWidth;
                        for (int i = 0; i < histogram.NumberOfBins; i++)
                            histItems.Add(new OxyPlot.Series.HistogramItem(histogram[i].LowerBound, histogram[i].UpperBound, histogram[i].Frequency * histogram.BinWidth / sum));

                        histogramSeries.ItemsSource = histItems;
                        histogramSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}";
                        ResidualHistogramPlot.Series.Add(histogramSeries);

                        double scale = Element.RatingCurve.Parameters.Last().Value;
                        if (!double.IsNaN(scale) && !double.IsInfinity(scale) && scale > 0)
                        {
                            var normal = new Normal(0, scale);
                            var normalPdf = normal.CreatePDFGraph();
                            var normalPdfPoints = new List<DataPoint>();
                            for (int i = 0; i < normalPdf.GetLength(0); i++)
                                normalPdfPoints.Add(new DataPoint(normalPdf[i, 0], normalPdf[i, 1]));

                            normalDensitySeries.ItemsSource = normalPdfPoints;
                            normalDensitySeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.0000}" + Environment.NewLine + "{3}: {4:0.000000}";
                            ResidualHistogramPlot.Series.Add(normalDensitySeries);
                        }
                    }
                }

                ResidualHistogramPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(ResidualHistogramPlot);
        }

        /// <summary>
        /// Updates the quantile-quantile (Q-Q) plot for assessing normality of residuals.
        /// Compares theoretical normal quantiles against observed residual quantiles with a 1:1 reference line.
        /// </summary>
        private void UpdateQQPlot()
        {
            if (ResidualQQPlot == null) return;

            var qqSeries = ResidualQQPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "Residuals")
                ?? new ScatterPointSeries
                {
                    Name = "Residuals",
                    Title = "Residuals",
                    MarkerFill = Colors.Black,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                };
            var qqLine = ResidualQQPlot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "OneToOneLine")
                ?? new LineAnnotation
                {
                    Name = "OneToOneLine",
                    Text = "1:1 Line",
                    Type = OxyPlot.Annotations.LineAnnotationType.LinearEquation,
                    Color = Colors.Black,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 0.9,
                    Intercept = 0,
                    Slope = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Top,
                };

            using (Element?.SuspendPlotBridges())
            {
                ResidualQQPlot.Series.Clear();
                for (int i = ResidualQQPlot.Annotations.Count - 1; i >= 0; i--)
                {
                    if (ResidualQQPlot.Annotations[i].Name == "OneToOneLine")
                    {
                        ResidualQQPlot.Annotations.RemoveAt(i);
                        break;
                    }
                }

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid != false && Element.BayesianAnalysis.IsEstimated == true
                    && _residuals != null && _residuals.Length > 0)
                {
                    var values = _residuals.Where(r => !double.IsNaN(r) && !double.IsInfinity(r)).ToArray();
                    if (values.Length >= 2)
                    {
                        Array.Sort(values);
                        var muSigma = Statistics.MeanStandardDeviation(values);
                        double mu = muSigma.Item1;
                        double sigma = muSigma.Item2;
                        if (!double.IsNaN(mu) && !double.IsInfinity(mu) &&
                            !double.IsNaN(sigma) && !double.IsInfinity(sigma) && sigma > 0)
                        {
                            var normal = new Normal(mu, sigma);
                            var pp = PlottingPositions.Weibull(values.Length);
                            var qq = new List<DataPoint>();
                            for (int i = 0; i < values.Length; i++)
                            {
                                qq.Add(new DataPoint(normal.InverseCDF(pp[i]), values[i]));
                            }

                            qqSeries.ItemsSource = qq;
                            qqSeries.Mapping = item => { var d = (DataPoint)item; return new OxyPlot.Series.ScatterPoint(d.X, d.Y); };
                            qqSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            ResidualQQPlot.Series.Add(qqSeries);
                            ResidualQQPlot.Annotations.Add(qqLine);
                        }
                    }
                }

                ResidualQQPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(ResidualQQPlot);
        }

        #endregion

        /// <summary>
        /// Binds the rating curve data grid with analysis results.
        /// Populates the table with stage values, credible intervals, and point estimates.
        /// </summary>
        private void BindRatingCurveDataGrid()
        {
            RatingCurveTable.ItemsSource = null;

            if (Element == null || Element.BayesianAnalysis == null || Element.BayesianAnalysis.IsEstimated != true
                || Element.AnalysisResults == null
                || Element.AnalysisResults.ModeCurve.Length != Element.StageBins)
                return;

            ModeColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

            var curvePoints = new List<FrequencyCurvePoint>();
            for (int i = 0; i < Element.StageBins; i++)
            {
                var s = Element.AnalysisResults.ConfidenceIntervals[i, 0];
                var lo = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                var up = Element.AnalysisResults.ConfidenceIntervals[i, 2];
                var prd = Element.AnalysisResults.MeanCurve[i];
                var md = Element.AnalysisResults.ModeCurve[i];
                curvePoints.Add(new FrequencyCurvePoint(s, 0, up, lo, prd, md));
            }
            RatingCurveTable.ItemsSource = curvePoints;
            RatingCurveTable.Items.Refresh();
        }

        /// <summary>
        /// Sets the column headers for the rating curve table based on the credible interval width.
        /// Updates the upper and lower confidence interval column headers with appropriate percentile values.
        /// </summary>
        private void SetRatingCurveTableColumnHeaders()
        {
            double alpha = (1 - Element.BayesianAnalysis.CredibleIntervalWidth) / 2;
            UpperColumn.Header = ((1 - alpha) * 100).ToString("F1") + "% CI";
            LowerColumn.Header = (alpha * 100).ToString("F1") + "% CI";
        }

        /// <summary>
        /// Sets the string format for numeric columns in the rating curve table.
        /// Applies user-defined formatting to ensure consistent number display.
        /// </summary>
        private void SetColumnStringFormats()
        {
            // Update the column binding string format
            XColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            UpperColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            LowerColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            PredictiveColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            ModeColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
        }

        /// <summary>
        /// Binds the summary statistics data grid with parameter estimates and model fit statistics.
        /// Displays parameter values, AIC, BIC, DIC, and RMSE for the current analysis.
        /// </summary>
        private void BindSummaryStatisticsDataGrid()
        {
            SummaryStatisticsTable.ItemsSource = null;

            if (Element == null || Element.BayesianAnalysis == null)
                return;

            StatValueColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

            var summaryStats = new List<SummaryStatistic>();
            if (Element.BayesianAnalysis.IsEstimated == false || Element.AnalysisResults == null)
            {

                for (int i = 0; i < Element.RatingCurve.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.RatingCurve.Parameters[i].DisplayName, double.NaN));
                }
                summaryStats.Add(new SummaryStatistic("AIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("BIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("DIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("WAIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("LOO-CV", double.NaN));
                summaryStats.Add(new SummaryStatistic("RMSE", double.NaN));
            }
            else
            {
                for (int i = 0; i < Element.RatingCurve.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.RatingCurve.Parameters[i].DisplayName, Element.RatingCurve.Parameters[i].Value));
                }
                summaryStats.Add(new SummaryStatistic("AIC", Element.AnalysisResults.AIC));
                summaryStats.Add(new SummaryStatistic("BIC", Element.AnalysisResults.BIC));
                summaryStats.Add(new SummaryStatistic("DIC", Element.AnalysisResults.DIC));
                summaryStats.Add(new SummaryStatistic("WAIC", Element.BayesianAnalysis.WAIC));
                summaryStats.Add(new SummaryStatistic("LOO-CV", Element.BayesianAnalysis.LOOIC));
                summaryStats.Add(new SummaryStatistic("RMSE", Element.AnalysisResults.RMSE));
            }

            SummaryStatisticsTable.ItemsSource = summaryStats;
            SummaryStatisticsTable.Items.Refresh();
        }

        /// <summary>
        /// Handles the LoadingRow event for the summary statistics table.
        /// Applies custom formatting to create visual separation between parameter groups.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The data grid row event arguments.</param>
        private void SummaryStatisticsTable_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (((SummaryStatistic)e.Row.DataContext).Name == "Minimum" ||
                ((SummaryStatistic)e.Row.DataContext).Name == "AIC")
            {
                e.Row.BorderThickness = new Thickness(0, 2, 0, 0);
                e.Row.SetResourceReference(Border.BorderBrushProperty, "EnvironmentWindowText");
            }
            else
            {
                e.Row.BorderThickness = new Thickness(0, 0, 0, 0);
            }
        }



    }
}
