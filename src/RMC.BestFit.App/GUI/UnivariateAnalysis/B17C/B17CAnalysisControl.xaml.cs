using System;
using System.Collections.Generic;
using System.Diagnostics;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlotControls;
using RMC.BestFit.Models;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using System.Windows.Data;
using FrameworkUI;
using FrameworkInterfaces;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Runtime.Serialization.Formatters;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying B17C frequency analysis results, including frequency curves,
    /// kernel density plots, histograms, and bivariate heat maps. This control provides visualization
    /// and tabular display of Bulletin 17C statistical analysis results.
    /// </summary>
    public partial class B17CAnalysisControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="B17CAnalysisControl"/> class.
        /// </summary>
        public B17CAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
            // DataGrid Binding.StringFormat must be set before first render.
            SetColumnStringFormats();
        }

        /// <summary>
        /// Dependency property for the Element property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(B17CAnalysis), typeof(B17CAnalysisControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the B17C analysis element associated with this control.
        /// </summary>
        public B17CAnalysis Element
        {
            get { return (B17CAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element dependency property changes.
        /// Handles event subscription and unsubscription for the new and old element values.
        /// </summary>
        /// <param name="d">The dependency object that changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as B17CAnalysisControl == null) return;
            var thisControl = (B17CAnalysisControl)d;

            // Remove handlers and detach plots from old element
            if (e.OldValue is B17CAnalysis oldElement)
            {
                oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                thisControl.FrequencyPlotHost.Content = null;
                thisControl.FrequencyPlotToolbar.Plot = null;
                // NOTE: PropertiesCalled is wired in XAML — no programmatic -= needed.
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as B17CAnalysis;
            if (newElement == null) return;

            // Subscribe Element-scoped handler
            newElement.PropertyChanged += thisControl.Element_PropertyChanged;

            // Attach plots, wire toolbars, bind axis titles, and wire Bayesian sub-control
            // plots inside a bridge-suspension block. PropertiesCalled / PlotPropertiesCalled
            // are wired in XAML — no programmatic += needed.
            using (newElement.SuspendPlotBridges())
            {
                thisControl.FrequencyPlotHost.Content = newElement.FrequencyPlot;
                thisControl.FrequencyPlotToolbar.Plot = newElement.FrequencyPlot;

                thisControl.BindAxisTitles();

                // B17C uses GMM (Generalized Method of Moments) for parameter estimation, not Bayesian MCMC.
                // Uncertainty quantification is performed via parametric bootstrap on the link-function residuals,
                // not posterior samples. As a result, the Autocorrelation, MarkovChainTrace, and MeanLikelihood
                // Bayesian sub-controls are intentionally omitted from this control. KernelDensity, Histogram,
                // BivariateHeatMap, and InfluenceDiagnostics are reused — the latter operates in GMM mode
                // (Mode="GMM" in XAML, GMMAnalysis dependency-property bound to the analysis's GMM estimator)
                // and switches axis titles dynamically via SetAxisTitle/SetPlotTitle in its UpdatePlot path.
                thisControl.KernelDensityControl.SetPlot(newElement.BayesianPlots.KernelDensityPlot);
                thisControl.HistogramControl.SetPlot(newElement.BayesianPlots.HistogramPlot);
                thisControl.BivariateHeatMapControl.SetPlot(newElement.BayesianPlots.BivariateHeatMapPlot);
                thisControl.InfluenceDiagnosticsControl.SetPlot(newElement.BayesianPlots.InfluenceDiagnosticsPlot);
            }
        }

        /// <summary>
        /// Gets a value indicating whether a plot has been clicked.
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
        /// Event raised when the preview control is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for handling preview control clicked events.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot area was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar was clicked.</param>
        /// <param name="plot">The plot that was clicked.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Array of color hex codes used for series coloring.
        /// </summary>
        private string[] _colorHexCodes;



        /// <summary>
        /// Handles the Loaded event of the user control. Initializes plots and data grids on first load.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            // Data refresh on every load. Plot hosts, toolbars, axis-title bindings, and
            // Element.PropertyChanged are wired once per Element in ElementCallback.
            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                UpdateFrequencyPlot();
                BindFrequencyCurveDataGrid();
                SetFrequencyCurveTableColumnHeaders();
                BindSummaryStatisticsDataGrid();
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
        }

        /// <summary>
        /// Handles the Unloaded event of the user control. Unsubscribes all element event handlers
        /// to prevent memory leaks and stale event callbacks.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Pattern B (canonical): Element-scoped lifecycle (PropertyChanged, plot
            // hosts, toolbars) is owned by ElementCallback and survives unload/reload
            // cycles. This control has no control-scoped subscriptions to tear down,
            // so Unloaded is intentionally a no-op.
        }

        /// <summary>
        /// Handles property changed events from the Element. Updates plots and data grids when analysis results change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing the property name that changed.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Marshal to UI thread if called from a background thread (model-layer
            // RaisePropertyChange does not marshal; MCMC completion fires on a worker thread).
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
            if (e.PropertyName == nameof(Element.InputData))
            {
                using (Element.SuspendPlotBridges())
                {
                    BindAxisTitles();
                }
                UpdateFrequencyPlot();
            }
            if ((e.PropertyName == nameof(Element.BayesianAnalysis.PointEstimator)
                 || e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth)
                 || e.PropertyName == nameof(Element.ProbabilityOrdinates))
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

        // PreviewSaved removed — element now owns plots and saves them directly in Save().

        /// <summary>
        /// Binds the frequency plot Y-axis title to the InputData.UnitLabel property.
        /// Called after element attachment and when InputData changes.
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
        }

        /// <summary>
        /// Handles the LostFocus event of the user control. Resets the PlotClicked flag.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the PreviewMouseDown event of the user control. Determines if a plot or toolbar was clicked
        /// and raises the appropriate event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
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
        /// Gets the currently selected plot based on which tab is active.
        /// </summary>
        /// <returns>The currently selected plot, or null if no plot is selected.</returns>
        public Plot GetCurrentPlot()
        {
            if (FrequencyResultsTabItem.IsSelected == true)
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
            else if (InfluenceTabItem.IsSelected == true)
            {
                return InfluenceDiagnosticsControl.Plot;
            }
            return null;
        }

        /// <summary>
        /// Gets the toolbar for the currently selected plot based on which tab is active.
        /// </summary>
        /// <returns>The toolbar for the currently selected plot, or null if no toolbar is available.</returns>
        public OxyPlotToolbar GetCurrentPlotToolbar()
        {
            if (FrequencyResultsTabItem.IsSelected == true)
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
            else if (InfluenceTabItem.IsSelected == true)
            {
                return InfluenceDiagnosticsControl.PlotToolbar;
            }
            return null;
        }

        /// <summary>
        /// Updates the frequency plot with current analysis results, including data series and fitted curves.
        /// Filters data based on whether the Y-axis is logarithmic.
        /// </summary>
        private void UpdateFrequencyPlot()
        {
            var plot = Element?.FrequencyPlot;
            if (plot == null) return;

            //var _upperEMA = new double[] { 12619.9864194, 11782.5706101, 10731.9334542, 9977.8772295, 9257.195421, 8353.4073954, 7705.0585317, 7085.7770685, 6309.7666737, 5753.5811499, 5222.8497774, 4558.3715505, 4081.6831725, 3623.9275407, 3036.9614379, 2591.8125345, 2124.369657, 1828.0832226, 1407.6975357, 1071.7446477, 907.0869561, 720.1777659, 595.9935624, 482.2186881, 418.8820542 };
            //var _lowerEMA = new double[] { 6068.5341507, 5925.6324102, 5725.4227251, 5564.7209184, 5395.4475558, 5157.3327345, 4965.2840088, 4761.8914929, 4473.6102744, 4239.0039294, 3988.1606166, 3627.9995253, 3330.8579175, 3010.2443484, 2549.747631, 2174.434113, 1772.9783406, 1520.4105225, 1164.7971414, 873.9362442, 723.5050134, 542.8057413, 417.5851356, 302.5246695, 240.2499231 };
            //var _computedEMA = new double[] { 8797.3443312, 8420.9236185, 7922.0715246, 7543.8158703, 7164.8324691, 6662.7550689, 6282.1037964, 5900.6709747, 5395.068108, 5011.3047972, 4626.1001511, 4113.8116407, 3722.9294361, 3327.7175622, 2794.7151663, 2378.0475015, 1939.884402, 1665.5634645, 1280.9789607, 972.1849074, 817.4013537, 637.3618677, 515.1825078, 402.2005095, 339.3764133 };

            //Color emaLineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[4]);
            //Color emaFillColor = Color.FromArgb(100, emaLineColor.R, emaLineColor.G, emaLineColor.B);

            //var ciEMA = new AreaSeries
            //{
            //    Name = "EMAIntervals",
            //    Title = "EMA - 90% Confidence Intervals",
            //    Fill = emaFillColor,
            //    Color = emaLineColor,
            //    LineStyle = LineStyle.Dot,
            //    BrokenLineThickness = 1,
            //    StrokeThickness = 1,
            //};

            //var modeEMA = new LineSeries
            //{
            //    Name = "EMAMode",
            //    Title = "EMA - Computed",
            //    Color = emaLineColor,
            //    StrokeThickness = 0,
            //    LineStyle = LineStyle.Solid,
            //    MarkerSize = 2,
            //    MarkerType = MarkerType.Diamond,
            //    MarkerStroke = Colors.Black,
            //    MarkerStrokeThickness = 1,
            //};


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
                };
            var posteriorPredictive = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorPredictive")
                ?? new LineSeries
                {
                    Name = "PosteriorPredictive",
                    Title = "Expected Probability",
                    Color = Colors.Blue,
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Dash,
                };
            var posteriorMode = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorMode")
                ?? new LineSeries
                {
                    Name = "PosteriorMode",
                    Title = "Computed",
                    Color = Colors.Black,
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Solid,
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
                    if (Element.IsEstimated == true && Element.AnalysisResults != null)
                    {
                        //var emaCIPoints = new List<Point3D>();
                        //var emaMdPoints = new List<Point>();

                        var ciPoints = new List<Point3D>();
                        var prdPoints = new List<Point>();
                        var mdPoints = new List<Point>();
                        for (int i = 0; i < Element.ProbabilityOrdinates.Count; i++)
                        {
                            var p = Element.ProbabilityOrdinates[i];
                            var up = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                            var lo = Element.AnalysisResults.ConfidenceIntervals[i, 0];
                            var prd = Element.AnalysisResults.MeanCurve[i];
                            var md = Element.AnalysisResults.ModeCurve[i];
                            ciPoints.Add(new Point3D(p, lo, up));
                            prdPoints.Add(new Point(p, prd));
                            mdPoints.Add(new Point(p, md));

                            //emaCIPoints.Add(new Point3D(p, _lowerEMA[i], _upperEMA[i]));
                            //emaMdPoints.Add(new Point(p, _computedEMA[i]));
                        }

                        // Credible Intervals
                        credibleIntervals.ItemsSource = ciPoints;
                        credibleIntervals.DataFieldX = "X";
                        credibleIntervals.DataFieldY = "Y";
                        credibleIntervals.DataFieldX2 = "X";
                        credibleIntervals.DataFieldY2 = "Z";
                        credibleIntervals.Title = (Element.BayesianAnalysis.CredibleIntervalWidth * 100).ToString("F0") + "% Confidence Intervals";
                        credibleIntervals.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(credibleIntervals);

                        // Predictive
                        posteriorPredictive.ItemsSource = prdPoints;
                        posteriorPredictive.DataFieldX = "X";
                        posteriorPredictive.DataFieldY = "Y";
                        posteriorPredictive.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(posteriorPredictive);

                        // Point Estimator
                        posteriorMode.ItemsSource = mdPoints;
                        posteriorMode.DataFieldX = "X";
                        posteriorMode.DataFieldY = "Y";
                        posteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        plot.Series.Add(posteriorMode);

                        //// EMA Credible Intervals
                        //ciEMA.ItemsSource = emaCIPoints;
                        //ciEMA.DataFieldX = "X";
                        //ciEMA.DataFieldY = "Y";
                        //ciEMA.DataFieldX2 = "X";
                        //ciEMA.DataFieldY2 = "Z";
                        //plot.Series.Add(ciEMA);

                        //// EAM Point Estimator
                        //modeEMA.ItemsSource = emaMdPoints;
                        //modeEMA.DataFieldX = "X";
                        //modeEMA.DataFieldY = "Y";
                        //plot.Series.Add(modeEMA);
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
                    frequencyExactData.DataFieldX = nameof(ExactData.PlottingPosition);
                    frequencyExactData.DataFieldY = nameof(ExactData.Value);
                    frequencyExactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.ExactSeries.Count > 0) plot.Series.Add(frequencyExactData);

                    // Low outliers data
                    frequencyLowOutlierData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                    frequencyLowOutlierData.DataFieldX = nameof(ExactData.PlottingPosition);
                    frequencyLowOutlierData.DataFieldY = nameof(ExactData.Value);
                    frequencyLowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.NumberOfLowOutliers > 0) plot.Series.Add(frequencyLowOutlierData);

                    // Uncertain data
                    frequencyUncertainData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.UncertainSeries : Element.InputData.DataFrame.UncertainSeries.Where(x => x.Value > 1E-16);
                    frequencyUncertainData.DataFieldX = nameof(UncertainData.PlottingPosition);
                    frequencyUncertainData.DataFieldY = nameof(UncertainData.Value);
                    frequencyUncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                    frequencyUncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                    frequencyUncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.UncertainSeries.Count > 0) plot.Series.Add(frequencyUncertainData);

                    // Interval data
                    frequencyIntervalData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.IntervalSeries : Element.InputData.DataFrame.IntervalSeries.Where(x => x.Value > 1E-16);
                    frequencyIntervalData.DataFieldX = nameof(IntervalData.PlottingPosition);
                    frequencyIntervalData.DataFieldY = nameof(IntervalData.Value);
                    frequencyIntervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                    frequencyIntervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                    frequencyIntervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.IntervalSeries.Count > 0) plot.Series.Add(frequencyIntervalData);

                    // Quantile penalties
                    quantilePriors.ItemsSource = Element.QuantilePenalties;
                    quantilePriors.DataFieldX = nameof(QuantilePenalty.AEP);
                    quantilePriors.DataFieldY = nameof(QuantilePenalty.MeanValue);
                    quantilePriors.DataFieldLowerErrorY = nameof(QuantilePenalty.LowerValue);
                    quantilePriors.DataFieldUpperErrorY = nameof(QuantilePenalty.UpperValue);
                    quantilePriors.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.QuantilePenalties.Where(q => q.Enabled).Count() > 0) plot.Series.Add(quantilePriors);

                    // Add Alternatives
                    if (Element.IsEstimated == true && Element.AnalysisResults != null)
                        AlternativeSelector.AddAllChecked();
                }

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        /// <summary>
        /// Handles the AnalysisAdded event from the alternative selector. Adds the analysis alternative's
        /// curves to the frequency plot with unique colors.
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
            if (alternative.AnalysisResults != null)
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
                var prdPoints = new List<Point>();
                var mdPoints = new List<Point>();
                for (int i = 0; i < ((IProbabilityOrdinates)alternative).ProbabilityOrdinates.Count; i++)
                {
                    var p = ((IProbabilityOrdinates)alternative).ProbabilityOrdinates[i];
                    var up = alternative.AnalysisResults.ConfidenceIntervals[i, 1];
                    var lo = alternative.AnalysisResults.ConfidenceIntervals[i, 0];
                    var prd = alternative.AnalysisResults.MeanCurve[i];
                    var md = alternative.AnalysisResults.ModeCurve[i];
                    ciPoints.Add(new Point3D(p, lo, up));
                    prdPoints.Add(new Point(p, prd));
                    mdPoints.Add(new Point(p, md));
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
                analysisItem.PosteriorPredictive.DataFieldX = "X";
                analysisItem.PosteriorPredictive.DataFieldY = "Y";
                analysisItem.PosteriorPredictive.Color = lineColor;
                plot.Series.Add(analysisItem.PosteriorPredictive);

                // Point Estimator
                analysisItem.PosteriorMode.Name = "PosteriorMode_" + index;
                analysisItem.PosteriorMode.Title = alternative.Name + pointLabel;
                analysisItem.PosteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                analysisItem.PosteriorMode.ItemsSource = mdPoints;
                analysisItem.PosteriorMode.DataFieldX = "X";
                analysisItem.PosteriorMode.DataFieldY = "Y";
                analysisItem.PosteriorMode.Color = lineColor;
                plot.Series.Add(analysisItem.PosteriorMode);

                plot.InvalidatePlot(true);

            }

        }

        /// <summary>
        /// Handles the AnalysisRemoved event from the alternative selector. Removes the analysis alternative's
        /// curves from the frequency plot.
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
        /// Handles the MouseLeftButtonUp event on a TextBlock. Toggles the show filter button.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowFilterToggleButton.IsChecked = !ShowFilterToggleButton.IsChecked;
        }

        #endregion

        /// <summary>
        /// Binds the frequency curve data grid to the current analysis results, displaying probability ordinates
        /// and corresponding quantile values.
        /// </summary>
        private void BindFrequencyCurveDataGrid()
        {
            FrequencyCurveTable.ItemsSource = null;

            if (Element == null || Element.BayesianAnalysis == null)
                return;

            ModeColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Mean Parameters" : "Computed";

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
        /// Sets the frequency curve table column headers based on the credible interval width.
        /// </summary>
        private void SetFrequencyCurveTableColumnHeaders()
        {
            double alpha = (1 - Element.BayesianAnalysis.CredibleIntervalWidth) / 2;
            UpperColumn.Header = ((1 - alpha) * 100).ToString("F1") + "% CI";
            LowerColumn.Header = (alpha * 100).ToString("F1") + "% CI";
        }

        /// <summary>
        /// Sets the string format for numeric columns in the frequency curve table based on user settings.
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
        /// Binds the summary statistics data grid to the current analysis results, displaying distribution
        /// parameters and statistical measures.
        /// </summary>
        private void BindSummaryStatisticsDataGrid()
        {
            SummaryStatisticsTable.ItemsSource = null;

            // Move the Element null check ABOVE the StatValueColumn.Header dereference so a future
            // caller that bypasses the existing pre-check (all current call sites already guard)
            // doesn't NRE on Element.BayesianAnalysis. The original NaN-row branch below covers the
            // not-yet-estimated case visually.
            if (Element == null) return;

            StatValueColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Mean Parameters" : "Computed";

            var summaryStats = new List<SummaryStatistic>();
            if (Element.IsEstimated != true || Element.AnalysisResults == null)
            {

                for (int i = 0; i < Element.Bulletin17CDistribution.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.Bulletin17CDistribution.Parameters[i].DisplayName, double.NaN));
                }
                summaryStats.Add(new SummaryStatistic("Minimum", double.NaN));
                summaryStats.Add(new SummaryStatistic("Maximum", double.NaN));
                summaryStats.Add(new SummaryStatistic("Mean", double.NaN));
                summaryStats.Add(new SummaryStatistic("Std Dev", double.NaN));
                summaryStats.Add(new SummaryStatistic("Skewness", double.NaN));
                summaryStats.Add(new SummaryStatistic("Kurtosis", double.NaN));
                summaryStats.Add(new SummaryStatistic("Pseudo AIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("Pseudo BIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("RMSE", double.NaN));
            }
            else
            {
                for (int i = 0; i < Element.Bulletin17CDistribution.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.Bulletin17CDistribution.Parameters[i].DisplayName, Element.Bulletin17CDistribution.Parameters[i].Value));
                }
                var min = Element.Bulletin17CDistribution.Distribution.Minimum;
                var max = Element.Bulletin17CDistribution.Distribution.Maximum;

                summaryStats.Add(new SummaryStatistic("Minimum", min < -1E12 ? double.NegativeInfinity : min));
                summaryStats.Add(new SummaryStatistic("Maximum", max > 1E12 ? double.PositiveInfinity : max));
                summaryStats.Add(new SummaryStatistic("Mean", Element.Bulletin17CDistribution.Distribution.Mean));
                summaryStats.Add(new SummaryStatistic("Std Dev", Element.Bulletin17CDistribution.Distribution.StandardDeviation));
                summaryStats.Add(new SummaryStatistic("Skewness", Element.Bulletin17CDistribution.Distribution.Skewness));
                summaryStats.Add(new SummaryStatistic("Kurtosis", Element.Bulletin17CDistribution.Distribution.Kurtosis));
                summaryStats.Add(new SummaryStatistic("Pseudo AIC", Element.AnalysisResults.AIC));
                summaryStats.Add(new SummaryStatistic("Pseudo BIC", Element.AnalysisResults.BIC));
                summaryStats.Add(new SummaryStatistic("RMSE", Element.AnalysisResults.RMSE));
            }

            SummaryStatisticsTable.ItemsSource = summaryStats;
            SummaryStatisticsTable.Items.Refresh();
        }

        /// <summary>
        /// Handles the LoadingRow event of the summary statistics table. Adds visual separators between
        /// different groups of statistics.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Data grid row event arguments.</param>
        private void SummaryStatisticsTable_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (((SummaryStatistic)e.Row.DataContext).Name == "Minimum" ||
                ((SummaryStatistic)e.Row.DataContext).Name == "Pseudo AIC")
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
