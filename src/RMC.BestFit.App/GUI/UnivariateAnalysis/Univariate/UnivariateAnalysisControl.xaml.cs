using System;
using System.Collections.Generic;
using System.Diagnostics;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlotControls;
using RMC.BestFit.Models;
using ModelAnalyses = RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Data;
using FrameworkUI;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and interacting with univariate analysis results, including frequency plots,
    /// chronology plots, and various diagnostic plots for Bayesian statistical analysis.
    /// </summary>
    public partial class UnivariateAnalysisControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnivariateAnalysisControl"/> class.
        /// </summary>
        public UnivariateAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
            // DataGrid Binding.StringFormat must be set before first render — see
            // RatingCurveAnalysisControl constructor for the rationale.
            SetColumnStringFormats();
        }

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(UnivariateAnalysis), typeof(UnivariateAnalysisControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the univariate analysis element being displayed and managed by this control.
        /// </summary>
        public UnivariateAnalysis Element
        {
            get { return (UnivariateAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element dependency property changes.
        /// Manages event handler subscriptions and unsubscriptions for the element.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as UnivariateAnalysisControl == null) return;
            var thisControl = (UnivariateAnalysisControl)d;

            // Remove handlers and detach old element's plots
            if (e.OldValue != null)
            {
                UnivariateAnalysis oldElement = e.OldValue as UnivariateAnalysis;
                if (oldElement != null)
                {
                    oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                    thisControl.FrequencyPlotHost.Content = null;
                    thisControl.ChronologyPlotHost.Content = null;
                    thisControl.FrequencyPlotToolbar.Plot = null;
                    thisControl.ChronologyPlotToolbar.Plot = null;
                    // NOTE: PropertiesCalled is wired in XAML (PropertiesCalled="PlotToolbar_PropertiesCalled"),
                    // so no programmatic += / -= is needed here. Adding one would cause a duplicate
                    // handler invocation that toggles the properties panel closed immediately after it opens.
                }
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as UnivariateAnalysis;
            if (newElement == null) return;

            // Reset _isLoaded so the next Loaded event re-runs one-time setup steps
            // for the new element (U1).
            thisControl._isLoaded = false;

            // Subscribe Element-scoped handler
            newElement.PropertyChanged += thisControl.Element_PropertyChanged;

            // Attach plots, wire toolbars, and bind axis titles inside a bridge-suspension
            // block so WPF visual-tree attachment doesn't record spurious undo entries
            // (theme-deferred Background / PlotAreaBackground PropertyChanged on Bayesian
            // plots that live in non-selected TabItems). PropertiesCalled /
            // PlotPropertiesCalled are wired in XAML on every toolbar and every Bayesian
            // sub-control — no programmatic += needed.
            using (newElement.SuspendPlotBridges())
            {
                thisControl.FrequencyPlotHost.Content = newElement.FrequencyPlot;
                thisControl.FrequencyPlotToolbar.Plot = newElement.FrequencyPlot;
                thisControl.ChronologyPlotHost.Content = newElement.ChronologyPlot;
                thisControl.ChronologyPlotToolbar.Plot = newElement.ChronologyPlot;

                thisControl.BindAxisTitles();

                thisControl.HistogramControl.SetPlot(newElement.BayesianPlots.HistogramPlot);
                thisControl.KernelDensityControl.SetPlot(newElement.BayesianPlots.KernelDensityPlot);
                thisControl.AutocorrelationControl.SetPlot(newElement.BayesianPlots.AutocorrelationPlot);
                thisControl.MarkovChainTraceControl.SetPlot(newElement.BayesianPlots.MarkovChainTracePlot);
                thisControl.MeanLikelihoodControl.SetPlot(newElement.BayesianPlots.MeanLikelihoodPlot);
                thisControl.BivariateHeatMapControl.SetPlot(newElement.BayesianPlots.BivariateHeatMapPlot);
                thisControl.InfluenceDiagnosticsControl.SetPlot(newElement.BayesianPlots.InfluenceDiagnosticsPlot);
            }
        }

        /// <summary>
        /// Gets a value indicating whether a plot was clicked by the user.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Raised when plot properties are requested to be displayed or modified.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Delegate for the <see cref="PlotPropertiesCalled"/> event.
        /// </summary>
        /// <param name="plot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">Indicates whether to open the properties panel.</param>
        /// <param name="propertyExpander">The property expander to use for displaying properties.</param>
        /// <param name="selectedObject">The currently selected object in the plot.</param>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);

        /// <summary>
        /// Raised when a plot or toolbar control within the preview area is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for the <see cref="PreviewControlClicked"/> event.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar was clicked.</param>
        /// <param name="plot">The plot that was clicked.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Array of hexadecimal color codes used for rendering multiple plot series with unique colors.
        /// </summary>
        private string[] _colorHexCodes;

        /// <summary>
        /// Indicates whether the control has completed its initial load operations.
        /// Set to false by ElementCallback when a new element is assigned so the next
        /// Loaded event re-runs one-time setup steps (U1).
        /// </summary>
        private bool _isLoaded = false;



        /// <summary>
        /// Handles the Loaded event of the user control. Initializes plot settings, updates visualizations,
        /// and binds data grids when the control is first loaded.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            // Data refresh on every load. Plot hosts, toolbars, axis-title bindings, and
            // Element.PropertyChanged are wired once per Element in ElementCallback; this
            // method just refreshes tabular / bound content. Undo is suspended because
            // Bayesian plots live in non-selected TabItems and WPF's deferred theme
            // application produces Background/PlotAreaBackground PropertyChanged events
            // that would otherwise dirty the element.
            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                UpdateFrequencyPlot();
                UpdateChronologyPlot();
                BindFrequencyCurveDataGrid();

                // One-time setup: column headers and summary grid layout do not need to
                // re-run on every transient visual-tree cycle — only on the first load
                // after a new Element is assigned (U1).
                if (!_isLoaded)
                {
                    SetFrequencyCurveTableColumnHeaders();
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
        /// Handles the Unloaded event of the user control. Resets <see cref="_isLoaded"/> so the
        /// next Loaded event re-runs one-time setup steps (U1).
        /// Unsubscribes event handlers to prevent memory leaks and spurious dirty state
        /// from WPF property change notifications during control teardown.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Reset _isLoaded so the next Loaded event re-runs one-time setup steps.
            // Element-scoped lifecycle (PropertyChanged, plot hosts, toolbars) is owned by
            // ElementCallback and survives unload/reload cycles — no additional teardown needed.
            _isLoaded = false;
        }

        /// <summary>
        /// Handles property changed events from the Element. Updates plots, data grids, and table headers
        /// in response to changes in analysis results, credible interval width, and parameter values.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The property changed event arguments.</param>
        /// <remarks>
        /// The model layer's <c>Model_PropertyChanged</c> handler owns the re-process decision for
        /// <c>ParameterTimeIndex</c> (re-runs <see cref="ModelAnalyses.UnivariateAnalysis.CreateFrequencyAnalysisResultsAsync"/>)
        /// and <c>Alpha</c> (re-runs <see cref="ModelAnalyses.UnivariateAnalysis.CreateChronologyResultsAsync"/>);
        /// this control only flips the cursor to Wait when a re-process is about to run and resets it
        /// when the corresponding <c>AnalysisResults</c> / <c>ChronologyAnalysisResults</c> notification
        /// arrives, marshaling onto the UI thread via <see cref="ResetWaitCursor"/> since the model
        /// layer's reprocess fires on a background <c>TaskScheduler.Default</c>.
        /// </remarks>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Marshal to UI thread if called from a background thread. The model layer's
            // ReprocessIfEstimated path uses TaskScheduler.Default and AnalysisBase.RaisePropertyChange
            // does NOT marshal — so AnalysisResults / ChronologyAnalysisResults notifications can
            // arrive here on a worker thread. WPF DataGrid and OxyPlot mutations from a worker
            // thread throw InvalidOperationException ("calling thread cannot access this object").
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Element_PropertyChanged(sender, e)));
                return;
            }

            if (e.PropertyName == nameof(Element.AnalysisResults))
            {
                BindFrequencyCurveDataGrid();
                BindSummaryStatisticsDataGrid();
                UpdateFrequencyPlot();
                ResetWaitCursor();
            }
            if (e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth))
            {
                SetFrequencyCurveTableColumnHeaders();
            }
            if (e.PropertyName == nameof(Element.ChronologyAnalysisResults))
            {
                UpdateChronologyPlot();
                ResetWaitCursor();
            }
            if ((e.PropertyName == nameof(Element.UnivariateDistribution.ParameterTimeIndex) ||
                 e.PropertyName == nameof(Element.UnivariateDistribution.Alpha))
                && Element.UnivariateDistribution.IsNonstationary == true
                && Element.IsEstimated)
            {
                // Cursor wait while the model layer's ReprocessIfEstimated runs the recompute.
                // Reset is wired to the AnalysisResults / ChronologyAnalysisResults notification above.
                Mouse.OverrideCursor = Cursors.Wait;
            }
            if ((e.PropertyName == nameof(Element.BayesianAnalysis.PointEstimator)
                 || e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth)
                 || e.PropertyName == nameof(Element.ProbabilityOrdinates))
                && Element.IsEstimated)
            {
                // Cursor wait while the model layer's ReprocessIfEstimated runs the recompute.
                // Reset is wired to the AnalysisResults notification above.
                Mouse.OverrideCursor = Cursors.Wait;
            }
            if (e.PropertyName == nameof(Element.InputData))
            {
                using (Element.SuspendPlotBridges())
                {
                    BindAxisTitles();
                }
                UpdateFrequencyPlot();
                UpdateChronologyPlot();
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

        // Note: TimeIndexChanged and AlphaChanged methods were removed; re-process for
        // ParameterTimeIndex and Alpha is now owned by the model layer's
        // Model_PropertyChanged handler in src/RMC.BestFit/Analyses/Univariate/UnivariateAnalysis.cs.
        // Only cursor management remains in this file; see Element_PropertyChanged above.

        /// <summary>
        /// Handles the LostFocus event by resetting the PlotClicked flag.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles mouse down events on the user control. Performs hit testing to determine if a plot or
        /// toolbar was clicked and raises the appropriate event.
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
        /// Handles the properties called event from the plot toolbar by forwarding it to subscribers.
        /// </summary>
        /// <param name="targetPlot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">Indicates whether to open the properties panel.</param>
        /// <param name="propertyExpander">The property expander to use.</param>
        /// <param name="selectedObject">The selected object in the plot.</param>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        /// <summary>
        /// Gets the currently selected plot based on which tab item is active.
        /// </summary>
        /// <returns>The currently active plot, or null if no plot tab is selected.</returns>
        public Plot GetCurrentPlot()
        {
            if (ChronologyResultsTabItem.IsSelected == true)
            {
                return Element?.ChronologyPlot;
            }
            else if (FrequencyResultsTabItem.IsSelected == true)
            {
                return Element?.FrequencyPlot;
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
        /// Gets the toolbar for the currently selected plot based on which tab item is active.
        /// </summary>
        /// <returns>The toolbar for the currently active plot, or null if no plot tab is selected.</returns>
        public OxyPlotToolbar GetCurrentPlotToolbar()
        {
            if (ChronologyResultsTabItem.IsSelected == true)
            {
                return ChronologyPlotToolbar;
            }
            else if (FrequencyResultsTabItem.IsSelected == true)
            {
                return FrequencyPlotToolbar;
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
        /// Binds axis titles on Element-owned plots to the InputData.UnitLabel and IndexLabel properties.
        /// Called after plot attachment and when InputData changes.
        /// </summary>
        private void BindAxisTitles()
        {
            if (Element == null) return;

            // Frequency plot: Y-axis title = UnitLabel
            var freqYAxis = Element.FrequencyPlot?.Axes.FirstOrDefault(a => a.Key == "Yaxis");
            if (freqYAxis != null)
            {
                if (Element.InputData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(freqYAxis, Element.InputData, nameof(InputData.UnitLabel), Element.InputData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(freqYAxis, string.Empty);
            }

            // Chronology plot: Y-axis title = UnitLabel
            var chronYAxis = Element.ChronologyPlot?.Axes.FirstOrDefault(a => a.Key == "Yaxis");
            if (chronYAxis != null)
            {
                if (Element.InputData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(chronYAxis, Element.InputData, nameof(InputData.UnitLabel), Element.InputData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(chronYAxis, string.Empty);
            }

            // Chronology plot: X-axis title = IndexLabel
            var chronXAxis = Element.ChronologyPlot?.Axes.FirstOrDefault(a => a.Key == "Xaxis");
            if (chronXAxis != null)
            {
                if (Element.InputData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(chronXAxis, Element.InputData, nameof(InputData.IndexLabel), Element.InputData.IndexLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(chronXAxis, string.Empty);
            }
        }

        /// <summary>
        /// Updates the frequency plot with current analysis results, data points, and statistical curves.
        /// Handles both estimated results (curves and intervals) and raw data visualization with appropriate
        /// filtering for logarithmic axes.
        /// </summary>
        private void UpdateFrequencyPlot()
        {
            var plot = Element?.FrequencyPlot;
            if (plot == null) return;

            // Lookup-or-create named series so user-customized styling survives Save/Open.
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
                    StrokeThickness = 1,
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
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Solid,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
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
            var quantilePriors = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "QuantilePrior")
                ?? new ScatterErrorSeries
                {
                    Name = "QuantilePrior",
                    Title = "Quantile Prior",
                    MarkerFill = Colors.Red,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Square,
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                if (Element.InputData != null && Element.BayesianAnalysis != null)
                {
                    // Add Curves
                    if (Element.BayesianAnalysis.IsEstimated == true && Element.AnalysisResults != null)
                    {
                        var ciPoints = new List<Point3D>();
                        var prdPoints = new List<OxyPlot.DataPoint>();
                        var mdPoints = new List<OxyPlot.DataPoint>();
                        for (int i = 0; i < Element.ProbabilityOrdinates.Count; i++)
                        {
                            var p = Element.ProbabilityOrdinates[i];
                            var up = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                            var lo = Element.AnalysisResults.ConfidenceIntervals[i, 0];
                            var prd = Element.AnalysisResults.MeanCurve[i];
                            var md = Element.AnalysisResults.ModeCurve[i];
                            ciPoints.Add(new Point3D(p, lo, up));
                            prdPoints.Add(new OxyPlot.DataPoint(p, prd));
                            mdPoints.Add(new OxyPlot.DataPoint(p, md));
                        }

                        // Credible Intervals
                        credibleIntervals.ItemsSource = ciPoints;
                        credibleIntervals.DataFieldX = "X";
                        credibleIntervals.DataFieldY = "Y";
                        credibleIntervals.DataFieldX2 = "X";
                        credibleIntervals.DataFieldY2 = "Z";
                        credibleIntervals.Title = (Element.BayesianAnalysis.CredibleIntervalWidth * 100).ToString("F0") + "% Credible Intervals";
                        credibleIntervals.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(credibleIntervals);

                        // Predictive
                        posteriorPredictive.ItemsSource = prdPoints;
                        posteriorPredictive.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(posteriorPredictive);

                        // Point Estimator
                        posteriorMode.Title = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";
                        posteriorMode.ItemsSource = mdPoints;
                        posteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(posteriorMode);
                    }

                    // See if Y-axis is logarithmic
                    bool logAxis = false;
                    foreach (var axis in plot.Axes)
                    {
                        if (axis.Key == "Yaxis" && axis as LogarithmicAxis != null)
                        {
                            logAxis = true;
                            break;
                        }
                    }

                    // Exact data
                    frequencyExactData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                    frequencyExactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                    frequencyExactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.ExactSeries.Count > 0) plot.Series.Add(frequencyExactData);

                    // low outliers data
                    frequencyLowOutlierData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                    frequencyLowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                    frequencyLowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.NumberOfLowOutliers > 0) plot.Series.Add(frequencyLowOutlierData);

                    // uncertain data
                    frequencyUncertainData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.UncertainSeries : Element.InputData.DataFrame.UncertainSeries.Where(x => x.Value > 1E-16);
                    frequencyUncertainData.DataFieldX = nameof(UncertainData.PlottingPosition);
                    frequencyUncertainData.DataFieldY = nameof(UncertainData.Value);
                    frequencyUncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                    frequencyUncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                    frequencyUncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.UncertainSeries.Count > 0) plot.Series.Add(frequencyUncertainData);

                    // interval data
                    frequencyIntervalData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.IntervalSeries : Element.InputData.DataFrame.IntervalSeries.Where(x => x.Value > 1E-16);
                    frequencyIntervalData.DataFieldX = nameof(IntervalData.PlottingPosition);
                    frequencyIntervalData.DataFieldY = nameof(IntervalData.Value);
                    frequencyIntervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                    frequencyIntervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                    frequencyIntervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.IntervalSeries.Count > 0) plot.Series.Add(frequencyIntervalData);

                    // quantile priors
                    quantilePriors.ItemsSource = Element.UnivariateDistribution.QuantilePriors;
                    quantilePriors.DataFieldX = nameof(QuantilePrior.Alpha);
                    quantilePriors.DataFieldY = nameof(QuantilePrior.MeanValue);
                    quantilePriors.DataFieldLowerErrorY = nameof(QuantilePrior.LowerValue);
                    quantilePriors.DataFieldUpperErrorY = nameof(QuantilePrior.UpperValue);
                    quantilePriors.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.UnivariateDistribution.EnableQuantilePriors && Element.UnivariateDistribution.QuantilePriors.Count > 0) plot.Series.Add(quantilePriors);

                    // Add Alternatives
                    if (Element.BayesianAnalysis.IsEstimated == true && Element.AnalysisResults != null)
                        AlternativeSelector.AddAllChecked();
                }

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        /// <summary>
        /// Handles the event when an alternative analysis is added to the selector. Adds the alternative's
        /// frequency curves (credible intervals, predictive, and mode) to the frequency plot with unique colors.
        /// </summary>
        /// <param name="analysisItem">The analysis alternative item to add to the plot.</param>
        private void AlternativeSelector_AnalysisAdded(AnalysisAlternativeItem analysisItem)
        {
            var plot = Element?.FrequencyPlot;
            if (plot == null) return;

            // Remove series
            plot.Series.Remove(analysisItem.CredibleIntervals);
            plot.Series.Remove(analysisItem.PosteriorPredictive);
            plot.Series.Remove(analysisItem.PosteriorMode);

            var alternative = analysisItem.Alternative;
            if (alternative.IsEstimated == true && alternative.AnalysisResults != null)
            {
                // Get unique color
                var index = plot.Series.Count;
                Color lineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[index]);
                Color fillColor = Color.FromArgb(100, lineColor.R, lineColor.G, lineColor.B);

                for (int i = 4; i < _colorHexCodes.Length; i++)
                {
                    var templineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[i]);
                    var tempfillColor = Color.FromArgb(100, templineColor.R, templineColor.G, templineColor.B);
                    bool colorExists = false;
                    for (int j = 0; j < plot.Series.Count; j++)
                    {
                        if (plot.Series[j].Name.Contains("Mode") && plot.Series[j].Color == templineColor)
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
                for (int i = 0; i < ((ModelAnalyses.IProbabilityOrdinates)alternative).ProbabilityOrdinates.Count; i++)
                {
                    var p = ((ModelAnalyses.IProbabilityOrdinates)alternative).ProbabilityOrdinates[i];
                    var up = alternative.AnalysisResults.ConfidenceIntervals[i, 1];
                    var lo = alternative.AnalysisResults.ConfidenceIntervals[i, 0];
                    var prd = alternative.AnalysisResults.MeanCurve[i];
                    var md = alternative.AnalysisResults.ModeCurve[i];
                    ciPoints.Add(new Point3D(p, lo, up));
                    prdPoints.Add(new OxyPlot.DataPoint(p, prd));
                    mdPoints.Add(new OxyPlot.DataPoint(p, md));
                }

                // Series labels
                double ciWidth = alternative.BayesianAnalysis.CredibleIntervalWidth;
                string ciLabel = "% Credible Intervals";
                string predLabel = " - Posterior Predictive";
                string pointLabel = " - " + (alternative.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode");
                if (alternative.GetType() == typeof(B17CAnalysis))
                {
                    ciLabel = "% Confidence Intervals";
                    predLabel = " - Expected Probability";
                    pointLabel = " - " + (alternative.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Mean Parameters" : "Computed");
                }

                // Credible Intervals
                analysisItem.CredibleIntervals.Name = "CredibleIntervals_" + index;
                analysisItem.CredibleIntervals.Title = alternative.Name + " - " + (ciWidth * 100).ToString("F0") + ciLabel;
                analysisItem.CredibleIntervals.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                analysisItem.CredibleIntervals.ItemsSource = ciPoints;
                analysisItem.CredibleIntervals.DataFieldX = "X";
                analysisItem.CredibleIntervals.DataFieldY = "Y";
                analysisItem.CredibleIntervals.DataFieldX2 = "X";
                analysisItem.CredibleIntervals.DataFieldY2 = "Z";
                analysisItem.CredibleIntervals.Fill = fillColor;
                analysisItem.CredibleIntervals.Color = Colors.Transparent;
                plot.Series.Add(analysisItem.CredibleIntervals);

                // Predictive
                analysisItem.PosteriorPredictive.Name = "PosteriorPredictive_" + index;
                analysisItem.PosteriorPredictive.Title = alternative.Name + predLabel;
                analysisItem.PosteriorPredictive.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                analysisItem.PosteriorPredictive.ItemsSource = prdPoints;
                analysisItem.PosteriorPredictive.Color = lineColor;
                plot.Series.Add(analysisItem.PosteriorPredictive);

                // Point Estimator
                analysisItem.PosteriorMode.Name = "PosteriorMode_" + index;
                analysisItem.PosteriorMode.Title = alternative.Name + pointLabel;
                analysisItem.PosteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                analysisItem.PosteriorMode.ItemsSource = mdPoints;
                analysisItem.PosteriorMode.Color = lineColor;
                plot.Series.Add(analysisItem.PosteriorMode);

                plot.InvalidatePlot(true);

            }

        }

        /// <summary>
        /// Handles the event when an alternative analysis is removed from the selector. Removes the alternative's
        /// series from the frequency plot and refreshes the display.
        /// </summary>
        /// <param name="analysisItem">The analysis alternative item to remove from the plot.</param>
        private void AlternativeSelector_AnalysisRemoved(AnalysisAlternativeItem analysisItem)
        {
            var plot = Element?.FrequencyPlot;
            if (plot == null) return;
            plot.Series.Remove(analysisItem.CredibleIntervals);
            plot.Series.Remove(analysisItem.PosteriorPredictive);
            plot.Series.Remove(analysisItem.PosteriorMode);
            plot.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles mouse click on text block to toggle the visibility of the filter section.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowFilterToggleButton.IsChecked = !ShowFilterToggleButton.IsChecked;
        }

        /// <summary>
        /// Updates the chronology plot with time series data, threshold information, and nonstationary analysis results.
        /// Displays data points, credible intervals, and mean trends over time for nonstationary distributions.
        /// </summary>
        private void UpdateChronologyPlot()
        {
            var plot = Element?.ChronologyPlot;
            if (plot == null) return;

            var chronologyExactData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
                ?? new ScatterPointSeries
                {
                    Name = "ExactData",
                    Title = "Exact Data",
                    MarkerFill = Colors.Black,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 4,
                    MarkerType = MarkerType.Circle,
                };
            var chronologyLowOutlierData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 4,
                    MarkerType = MarkerType.Cross,
                };
            var chronologyUncertainData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "UncertainData")
                ?? new ScatterErrorSeries
                {
                    Name = "UncertainData",
                    Title = "Uncertain Data",
                    MarkerFill = Colors.Green,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 4,
                    MarkerType = MarkerType.Diamond,
                };
            var chronologyIntervalData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "IntervalData")
                ?? new ScatterErrorSeries
                {
                    Name = "IntervalData",
                    Title = "Interval Data",
                    MarkerFill = Colors.Cyan,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 4,
                    MarkerType = MarkerType.Circle,
                };
            var credibleIntervalsChrono = plot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "CredibleIntervals")
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
            var meanChrono = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorPredictive")
                ?? new LineSeries
                {
                    Name = "PosteriorPredictive",
                    Title = "Mean",
                    Color = Colors.Blue,
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Dash,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                if (Element.InputData != null && Element.UnivariateDistribution != null && Element.UnivariateDistribution.IsNonstationary != false)
                {
                    // Exact data
                    chronologyExactData.ItemsSource = Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                    chronologyExactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.Index, d.Value); };
                    chronologyExactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.ExactSeries.Count > 0) plot.Series.Add(chronologyExactData);

                    // low outliers data
                    chronologyLowOutlierData.ItemsSource = Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                    chronologyLowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.Index, d.Value); };
                    chronologyLowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.NumberOfLowOutliers > 0) plot.Series.Add(chronologyLowOutlierData);

                    // uncertain data
                    chronologyUncertainData.ItemsSource = Element.InputData.DataFrame.UncertainSeries;
                    chronologyUncertainData.DataFieldX = nameof(UncertainData.Index);
                    chronologyUncertainData.DataFieldY = nameof(UncertainData.Value);
                    chronologyUncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                    chronologyUncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                    chronologyUncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.UncertainSeries.Count > 0) plot.Series.Add(chronologyUncertainData);

                    // interval data
                    chronologyIntervalData.ItemsSource = Element.InputData.DataFrame.IntervalSeries;
                    chronologyIntervalData.DataFieldX = nameof(IntervalData.Index);
                    chronologyIntervalData.DataFieldY = nameof(IntervalData.Value);
                    chronologyIntervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                    chronologyIntervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                    chronologyIntervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.IntervalSeries.Count > 0) plot.Series.Add(chronologyIntervalData);

                    // threshold data (dynamic count — created inline)
                    UpdateThresholdPlotSeries();

                    // frequency results
                    if (Element.BayesianAnalysis != null &&
                        Element.BayesianAnalysis.IsEstimated == true &&
                        Element.ChronologyAnalysisResults != null &&
                        Element.UnivariateDistribution != null &&
                        Element.UnivariateDistribution.IsNonstationary == true &&
                        Element.InputData.DataFrame.FullTimeSeries.Count > 0)
                    {
                        var ciPoints = new List<Point3D>();
                        var prdPoints = new List<OxyPlot.DataPoint>();
                        var mdPoints = new List<OxyPlot.DataPoint>();
                        int t = Element.InputData.DataFrame.FullTimeSeries.First().Index;
                        for (int i = 0; i < Element.ChronologyAnalysisResults.ModeCurve.Length; i++)
                        {
                            var up = Element.ChronologyAnalysisResults.ConfidenceIntervals[i, 1];
                            var lo = Element.ChronologyAnalysisResults.ConfidenceIntervals[i, 0];
                            var prd = Element.ChronologyAnalysisResults.MeanCurve[i];
                            var md = Element.ChronologyAnalysisResults.ModeCurve[i];
                            ciPoints.Add(new Point3D(t, lo, up));
                            prdPoints.Add(new OxyPlot.DataPoint(t, prd));
                            mdPoints.Add(new OxyPlot.DataPoint(t, md));
                            t += 1;
                        }

                        // Credible Intervals
                        credibleIntervalsChrono.ItemsSource = ciPoints;
                        credibleIntervalsChrono.DataFieldX = "X";
                        credibleIntervalsChrono.DataFieldY = "Y";
                        credibleIntervalsChrono.DataFieldX2 = "X";
                        credibleIntervalsChrono.DataFieldY2 = "Z";
                        credibleIntervalsChrono.Title = (Element.BayesianAnalysis.CredibleIntervalWidth * 100).ToString("F0") + "% Credible Intervals";
                        credibleIntervalsChrono.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(credibleIntervalsChrono);

                        // mean
                        meanChrono.ItemsSource = prdPoints;
                        meanChrono.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(meanChrono);
                    }
                }

                if (plot.Visibility == Visibility.Visible)
                    plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        /// <summary>
        /// Updates the threshold data series on the chronology plot by clearing existing threshold series
        /// and adding new ones from the current input data.
        /// </summary>
        private void UpdateThresholdPlotSeries()
        {
            var plot = Element?.ChronologyPlot;
            if (plot == null) return;
            // Clear the threshold data series from plot
            for (int i = plot.Series.Count - 1; i >= 0; i -= 1)
            {
                if (plot.Series[i].GetType() == typeof(AreaSeries))
                    plot.Series.RemoveAt(i);
            }
            // Add back in the threshold data
            for (int i = 0; i <= Element.InputData.DataFrame.ThresholdSeries.Count - 1; i++)
                AddThresholdDataPoint((ThresholdData)Element.InputData.DataFrame.ThresholdSeries[i], i + 1, i == 0 ? true : false);
        }

        /// <summary>
        /// Adds a threshold data point to the chronology plot as an area series.
        /// </summary>
        /// <param name="data">The threshold data to add.</param>
        /// <param name="index">The index of this threshold in the series.</param>
        /// <param name="showInLegend">Indicates whether to show this threshold in the legend.</param>
        private void AddThresholdDataPoint(ThresholdData data, int index, bool showInLegend = false)
        {
            // Get start and end indices in case they are the same
            double startIndex = data.StartIndex;
            double endIndex = data.EndIndex;
            if (endIndex == startIndex)
            {
                startIndex -= 0.5;
                endIndex += 0.5;
            }
            // Create a new area series for the threshold and add to the chronology plot
            AreaSeries areaSeries = new AreaSeries() { Name = "ThresholdData_" + index + "_" + data.StartIndex.ToString().Replace("-", "_") + "_" + data.Index.ToString().Replace("-", "_") };
            if (showInLegend == true)
            {
                areaSeries.Title = "Threshold Data";
                areaSeries.TrackerFormatString = "Threshold " + data.StartIndex.ToString() + "-" + data.EndIndex.ToString() + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                areaSeries.RenderInLegend = true;
            }
            else
            {
                areaSeries.Title = "Threshold " + data.StartIndex.ToString() + "-" + data.EndIndex.ToString();
                areaSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                areaSeries.RenderInLegend = false;
            }
            areaSeries.Fill = Color.FromArgb(100, 250, 128, 114);
            areaSeries.Color = Color.FromArgb(255, 250, 128, 114);
            areaSeries.LineStyle = LineStyle.Solid;
            areaSeries.BrokenLineThickness = 1;
            areaSeries.StrokeThickness = 1;
            ((OxyPlot.Series.AreaSeries)areaSeries.InternalSeries).Points.Clear();
            ((OxyPlot.Series.AreaSeries)areaSeries.InternalSeries).Points2.Clear();
            ((OxyPlot.Series.AreaSeries)areaSeries.InternalSeries).Points.Add(new DataPoint(startIndex, 0));
            ((OxyPlot.Series.AreaSeries)areaSeries.InternalSeries).Points2.Add(new DataPoint(startIndex, data.Value));
            ((OxyPlot.Series.AreaSeries)areaSeries.InternalSeries).Points.Add(new DataPoint(endIndex, 0));
            ((OxyPlot.Series.AreaSeries)areaSeries.InternalSeries).Points2.Add(new DataPoint(endIndex, data.Value));
            Element?.ChronologyPlot?.Series.Add(areaSeries);
        }

        #endregion

        /// <summary>
        /// Binds the frequency curve data grid with analysis results including probability ordinates,
        /// credible intervals, predictive values, and mode values.
        /// </summary>
        private void BindFrequencyCurveDataGrid()
        {
            FrequencyCurveTable.ItemsSource = null;

            if (Element == null || Element.BayesianAnalysis == null)
                return;

            ModeColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

            if (Element.BayesianAnalysis.IsEstimated != true || Element.AnalysisResults == null)
                return;

            var curvePoints = new List<FrequencyCurvePoint>();
            for (int i = 0; i < Element.ProbabilityOrdinates.Count; i++)
            {
                var p = Element.ProbabilityOrdinates[i];
                var up = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                var lo = Element.AnalysisResults.ConfidenceIntervals[i, 0];
                var prd = Element.AnalysisResults.MeanCurve[i];
                var md = Element.AnalysisResults.ModeCurve[i];
                curvePoints.Add(new FrequencyCurvePoint(p, up, lo, prd, md));
            }
            FrequencyCurveTable.ItemsSource = curvePoints;
            FrequencyCurveTable.Items.Refresh();
        }

        /// <summary>
        /// Sets the column headers for the frequency curve table based on the current credible interval width.
        /// </summary>
        private void SetFrequencyCurveTableColumnHeaders()
        {
            double alpha = (1 - Element.BayesianAnalysis.CredibleIntervalWidth) / 2;
            UpperColumn.Header = ((1 - alpha) * 100).ToString("F1") + "% CI";
            LowerColumn.Header = (alpha * 100).ToString("F1") + "% CI";
        }

        /// <summary>
        /// Sets the string format for data grid columns based on user settings.
        /// </summary>
        private void SetColumnStringFormats()
        {
            // Update the column binding string format
            UpperColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            LowerColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            PredictiveColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            ModeColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
        }

        /// <summary>
        /// Binds the summary statistics data grid with parameter values and goodness-of-fit statistics.
        /// Displays distribution parameters, moments (mean, std dev, skewness, kurtosis), and model comparison criteria (AIC, BIC, DIC, WAIC, LOO-CV, RMSE).
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

                for (int i = 0; i < Element.UnivariateDistribution.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.UnivariateDistribution.Parameters[i].DisplayName, double.NaN));
                }
                summaryStats.Add(new SummaryStatistic("Minimum", double.NaN));
                summaryStats.Add(new SummaryStatistic("Maximum", double.NaN));
                summaryStats.Add(new SummaryStatistic("Mean", double.NaN));
                summaryStats.Add(new SummaryStatistic("Std Dev", double.NaN));
                summaryStats.Add(new SummaryStatistic("Skewness", double.NaN));
                summaryStats.Add(new SummaryStatistic("Kurtosis", double.NaN));
                summaryStats.Add(new SummaryStatistic("AIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("BIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("DIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("WAIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("LOO-CV", double.NaN));
                summaryStats.Add(new SummaryStatistic("RMSE", double.NaN));
                //summaryStats.Add(new SummaryStatistic("ERL", double.NaN));
            }
            else
            {
                for (int i = 0; i < Element.UnivariateDistribution.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.UnivariateDistribution.Parameters[i].DisplayName, Element.UnivariateDistribution.Parameters[i].Value));
                }
                var min = Element.UnivariateDistribution.Distribution.Minimum;
                var max = Element.UnivariateDistribution.Distribution.Maximum;

                summaryStats.Add(new SummaryStatistic("Minimum", min < -1E12 ? double.NegativeInfinity : min));
                summaryStats.Add(new SummaryStatistic("Maximum", max > 1E12 ? double.PositiveInfinity : max));
                summaryStats.Add(new SummaryStatistic("Mean", Element.UnivariateDistribution.Distribution.Mean));
                summaryStats.Add(new SummaryStatistic("Std Dev", Element.UnivariateDistribution.Distribution.StandardDeviation));
                summaryStats.Add(new SummaryStatistic("Skewness", Element.UnivariateDistribution.Distribution.Skewness));
                summaryStats.Add(new SummaryStatistic("Kurtosis", Element.UnivariateDistribution.Distribution.Kurtosis));
                summaryStats.Add(new SummaryStatistic("AIC", Element.AnalysisResults.AIC));
                summaryStats.Add(new SummaryStatistic("BIC", Element.AnalysisResults.BIC));
                summaryStats.Add(new SummaryStatistic("DIC", Element.AnalysisResults.DIC));
                summaryStats.Add(new SummaryStatistic("WAIC", Element.BayesianAnalysis.WAIC));
                summaryStats.Add(new SummaryStatistic("LOO-CV", Element.BayesianAnalysis.LOOIC));
                summaryStats.Add(new SummaryStatistic("RMSE", Element.AnalysisResults.RMSE));
                //summaryStats.Add(new SummaryStatistic("ERL", Element.FrequencyAnalysisResults.ERL));
            }

            SummaryStatisticsTable.ItemsSource = summaryStats;
            SummaryStatisticsTable.Items.Refresh();
        }

        /// <summary>
        /// Handles the loading of each row in the summary statistics table. Adds visual separators
        /// (borders) before certain rows to group related statistics.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The data grid row event arguments.</param>
        private void SummaryStatisticsTable_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (((SummaryStatistic)e.Row.DataContext).Name == "Minimum" ||
                ((SummaryStatistic)e.Row.DataContext).Name == "AIC" ||
                ((SummaryStatistic)e.Row.DataContext).Name == "ERL")
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
