using OxyPlot.Wpf;
using OxyPlot;
using OxyPlotControls;
using FrameworkInterfaces;
using FrameworkUI;
using RMC.BestFit.Models;
using ModelAnalyses = RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Xml.Linq;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and managing mixture distribution analysis results and visualizations.
    /// Provides multiple tabs for frequency plots, kernel density estimation, histograms, bivariate analysis,
    /// and Bayesian diagnostic plots including mean likelihood, autocorrelation, and Markov chain traces.
    /// </summary>
    public partial class MixtureAnalysisControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MixtureAnalysisControl"/> class.
        /// Sets up the component and initializes the color scheme for plot series.
        /// </summary>
        public MixtureAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
            // DataGrid Binding.StringFormat must be set before first render.
            SetColumnStringFormats();
        }

        /// <summary>
        /// Identifies the <see cref="Element"/> dependency property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(MixtureAnalysis), typeof(MixtureAnalysisControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the mixture analysis element that this control visualizes.
        /// </summary>
        public MixtureAnalysis Element
        {
            get { return (MixtureAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Element"/> dependency property changes.
        /// Manages event handler subscriptions and unsubscriptions for the old and new elements.
        /// </summary>
        /// <param name="d">The dependency object on which the property changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as MixtureAnalysisControl == null) return;
            var thisControl = (MixtureAnalysisControl)d;

            // Remove handlers and detach old element's plots
            if (e.OldValue != null)
            {
                MixtureAnalysis oldElement = e.OldValue as MixtureAnalysis;
                if (oldElement != null)
                {
                    oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                    thisControl.FrequencyPlotHost.Content = null;
                    thisControl.FrequencyPlotToolbar.Plot = null;
                    // NOTE: PropertiesCalled is wired in XAML — no programmatic -= needed.
                }
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as MixtureAnalysis;
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
        /// Convenience accessor for the Element-owned frequency plot.
        /// </summary>
        private Plot FrequencyPlot => Element?.FrequencyPlot;

        /// <summary>
        /// Gets a value indicating whether a plot area was clicked by the user.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Raised when the plot properties dialog is requested.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Represents the method that handles the <see cref="PlotPropertiesCalled"/> event.
        /// </summary>
        /// <param name="plot">The plot for which properties were requested.</param>
        /// <param name="openProperties">Indicates whether to open the properties dialog.</param>
        /// <param name="propertyExpander">The property expander section to display.</param>
        /// <param name="selectedObject">The selected object in the properties dialog.</param>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);

        /// <summary>
        /// Raised when the preview control is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Represents the method that handles the <see cref="PreviewControlClicked"/> event.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot area was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar area was clicked.</param>
        /// <param name="plot">The plot that was clicked.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Array of hexadecimal color codes used for rendering multiple plot series with distinct colors.
        /// </summary>
        private string[] _colorHexCodes;



        /// <summary>
        /// Handles the Loaded event of the UserControl. Initializes plots, data grids, and column formats on first load.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
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
        /// Handles property changes on the associated Element. Updates the UI when analysis results or credible interval width changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the name of the changed property.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Marshal to UI thread if called from a background thread.
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
                // Refresh the frequency plot so data points reflect the new InputData (S1).
                // Mirror PointProcessAnalysisControl:217.
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


        /// <summary>
        /// Binds the frequency plot Y-axis title to the InputData.UnitLabel property.
        /// Called after element attachment and when InputData changes.
        /// </summary>
        private void BindAxisTitles()
        {
            if (Element == null) return;

            // Frequency plot: Y-axis title = UnitLabel
            var freqYAxis = FrequencyPlot?.Axes.FirstOrDefault(a => a.Key == "Yaxis");
            if (freqYAxis != null)
            {
                if (Element.InputData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(freqYAxis, Element.InputData, nameof(InputData.UnitLabel), Element.InputData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(freqYAxis, string.Empty);
            }
        }

        /// <summary>
        /// Handles the LostFocus event of the UserControl. Resets the PlotClicked property to false.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the PreviewMouseDown event of the UserControl. Detects clicks on plots and toolbars and raises appropriate events.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
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
        /// Handles the PropertiesCalled event from the plot toolbar. Forwards the event to subscribers.
        /// </summary>
        /// <param name="targetPlot">The plot for which properties were requested.</param>
        /// <param name="openProperties">Indicates whether to open the properties dialog.</param>
        /// <param name="propertyExpander">The property expander section to display.</param>
        /// <param name="selectedObject">The selected object in the properties dialog.</param>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        /// <summary>
        /// Gets the plot control that is currently displayed in the active tab.
        /// </summary>
        /// <returns>The currently active Plot control, or null if no plot tab is selected.</returns>
        public Plot GetCurrentPlot()
        {
            if (FrequencyResultsTabItem.IsSelected == true)
            {
                return FrequencyPlot;
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
        /// Gets the plot toolbar that corresponds to the currently displayed plot in the active tab.
        /// </summary>
        /// <returns>The currently active OxyPlotToolbar control, or null if no plot tab is selected.</returns>
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
        /// Updates the frequency plot with the current analysis results including data points, curves,
        /// credible intervals, and alternative analyses. Clears and rebuilds all plot series.
        /// </summary>
        private void UpdateFrequencyPlot()
        {
            if (FrequencyPlot == null) return;

            // Lookup-or-create named series so user-customized styling survives Save/Open.
            var credibleIntervals = FrequencyPlot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "CredibleIntervals")
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
            var posteriorPredictive = FrequencyPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorPredictive")
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
            var posteriorMode = FrequencyPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorMode")
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
            var frequencyExactData = FrequencyPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
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
            var frequencyLowOutlierData = FrequencyPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Cross,
                };
            var frequencyUncertainData = FrequencyPlot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "UncertainData")
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
            var frequencyIntervalData = FrequencyPlot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "IntervalData")
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
            var quantilePriors = FrequencyPlot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "QuantilePrior")
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

            using (Element?.SuspendPlotBridges())
            {
                FrequencyPlot.Series.Clear();

                if (Element != null && Element.InputData != null && Element.BayesianAnalysis != null)
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
                        FrequencyPlot.Series.Add(credibleIntervals);

                        // Predictive
                        posteriorPredictive.ItemsSource = prdPoints;
                        posteriorPredictive.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        FrequencyPlot.Series.Add(posteriorPredictive);

                        // Point Estimator
                        posteriorMode.Title = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";
                        posteriorMode.ItemsSource = mdPoints;
                        posteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        FrequencyPlot.Series.Add(posteriorMode);
                    }

                    // See if Y-axis is logarithmic
                    bool logAxis = false;
                    foreach (var axis in FrequencyPlot.Axes)
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
                    if (Element.InputData.DataFrame.ExactSeries.Count > 0) FrequencyPlot.Series.Add(frequencyExactData);

                    // low outliers data
                    frequencyLowOutlierData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                    frequencyLowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                    frequencyLowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.NumberOfLowOutliers > 0) FrequencyPlot.Series.Add(frequencyLowOutlierData);

                    // uncertain data
                    frequencyUncertainData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.UncertainSeries : Element.InputData.DataFrame.UncertainSeries.Where(x => x.Value > 1E-16);
                    frequencyUncertainData.DataFieldX = nameof(UncertainData.PlottingPosition);
                    frequencyUncertainData.DataFieldY = nameof(UncertainData.Value);
                    frequencyUncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                    frequencyUncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                    frequencyUncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.UncertainSeries.Count > 0) FrequencyPlot.Series.Add(frequencyUncertainData);

                    // interval data
                    frequencyIntervalData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.IntervalSeries : Element.InputData.DataFrame.IntervalSeries.Where(x => x.Value > 1E-16);
                    frequencyIntervalData.DataFieldX = nameof(IntervalData.PlottingPosition);
                    frequencyIntervalData.DataFieldY = nameof(IntervalData.Value);
                    frequencyIntervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                    frequencyIntervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                    frequencyIntervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.IntervalSeries.Count > 0) FrequencyPlot.Series.Add(frequencyIntervalData);

                    // quantile priors
                    quantilePriors.ItemsSource = Element.MixtureDistribution.QuantilePriors;
                    quantilePriors.DataFieldX = nameof(QuantilePrior.Alpha);
                    quantilePriors.DataFieldY = nameof(QuantilePrior.MeanValue);
                    quantilePriors.DataFieldLowerErrorY = nameof(QuantilePrior.LowerValue);
                    quantilePriors.DataFieldUpperErrorY = nameof(QuantilePrior.UpperValue);
                    quantilePriors.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.MixtureDistribution.EnableQuantilePriors && Element.MixtureDistribution.QuantilePriors.Count > 0) FrequencyPlot.Series.Add(quantilePriors);

                    // Add Alternatives
                    if (Element.BayesianAnalysis.IsEstimated == true && Element.AnalysisResults != null)
                        AlternativeSelector.AddAllChecked();
                }

                FrequencyPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(FrequencyPlot);
        }

        /// <summary>
        /// Handles the event when an alternative analysis is added to the frequency plot.
        /// Creates and adds series for the alternative's credible intervals, posterior predictive, and posterior mode/mean.
        /// </summary>
        /// <param name="analysisItem">The analysis alternative item containing the alternative analysis and associated plot series.</param>
        private void AlternativeSelector_AnalysisAdded(AnalysisAlternativeItem analysisItem)
        {
            if (Element == null || FrequencyPlot == null) return;

            // Remove series
            FrequencyPlot.Series.Remove(analysisItem.CredibleIntervals);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorPredictive);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorMode);

            var alternative = analysisItem.Alternative;
            if (alternative.IsEstimated == true && alternative.AnalysisResults != null)
            {
                // Get unique color
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
                        if (FrequencyPlot.Series[j].Name.Contains("Mode") && FrequencyPlot.Series[j].Color == templineColor)
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
                FrequencyPlot.Series.Add(analysisItem.CredibleIntervals);

                // Predictive
                analysisItem.PosteriorPredictive.Name = "PosteriorPredictive_" + index;
                analysisItem.PosteriorPredictive.Title = alternative.Name + predLabel;
                analysisItem.PosteriorPredictive.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                analysisItem.PosteriorPredictive.ItemsSource = prdPoints;
                analysisItem.PosteriorPredictive.Color = lineColor;
                FrequencyPlot.Series.Add(analysisItem.PosteriorPredictive);

                // Point Estimator
                analysisItem.PosteriorMode.Name = "PosteriorMode_" + index;
                analysisItem.PosteriorMode.Title = alternative.Name + pointLabel;
                analysisItem.PosteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                analysisItem.PosteriorMode.ItemsSource = mdPoints;
                analysisItem.PosteriorMode.Color = lineColor;
                FrequencyPlot.Series.Add(analysisItem.PosteriorMode);

                FrequencyPlot.InvalidatePlot(true);

            }

        }

        /// <summary>
        /// Handles the event when an alternative analysis is removed from the frequency plot.
        /// Removes all series associated with the alternative analysis and refreshes the plot.
        /// </summary>
        /// <param name="analysisItem">The analysis alternative item to be removed.</param>
        private void AlternativeSelector_AnalysisRemoved(AnalysisAlternativeItem analysisItem)
        {
            if (Element == null || FrequencyPlot == null) return;

            FrequencyPlot.Series.Remove(analysisItem.CredibleIntervals);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorPredictive);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorMode);
            FrequencyPlot.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles the MouseLeftButtonUp event on a TextBlock. Toggles the filter visibility.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowFilterToggleButton.IsChecked = !ShowFilterToggleButton.IsChecked;
        }

        #endregion

        /// <summary>
        /// Binds the frequency curve data grid to the analysis results, displaying probability ordinates
        /// with corresponding credible intervals, posterior predictive, and mode/mean values.
        /// </summary>
        private void BindFrequencyCurveDataGrid()
        {
            FrequencyCurveTable.ItemsSource = null;

            if (Element == null || Element.BayesianAnalysis == null || Element.BayesianAnalysis.IsEstimated != true || Element.AnalysisResults == null)
                return;

            ModeColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

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
        /// Sets the column headers for the frequency curve table based on the credible interval width.
        /// </summary>
        private void SetFrequencyCurveTableColumnHeaders()
        {
            double alpha = (1 - Element.BayesianAnalysis.CredibleIntervalWidth) / 2;
            UpperColumn.Header = ((1 - alpha) * 100).ToString("F1") + "% CI";
            LowerColumn.Header = (alpha * 100).ToString("F1") + "% CI";
        }

        /// <summary>
        /// Sets the string format for all data grid columns based on user settings.
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
        /// Binds the summary statistics data grid to display distribution parameters, moments,
        /// and goodness-of-fit statistics from the analysis results.
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
                if (Element.MixtureDistribution.IsZeroInflated)
                {
                    summaryStats.Add(new SummaryStatistic("Weight 0", double.NaN));
                }
                if (Element.MixtureDistribution.Mixture.Distributions.Count() == 1)
                {
                    summaryStats.Add(new SummaryStatistic("Weight 1", double.NaN));
                }

                for (int i = 0; i < Element.MixtureDistribution.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.MixtureDistribution.Parameters[i].DisplayName, double.NaN));
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
            }
            else
            {
                if (Element.MixtureDistribution.IsZeroInflated)
                {
                    summaryStats.Add(new SummaryStatistic("Weight 0", Element.MixtureDistribution.Mixture.ZeroWeight));
                }
                if (Element.MixtureDistribution.Mixture.Distributions.Count() == 1)
                {
                    summaryStats.Add(new SummaryStatistic("Weight 1", Element.MixtureDistribution.Mixture.Weights[0]));
                }

                for (int i = 0; i < Element.MixtureDistribution.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.MixtureDistribution.Parameters[i].DisplayName, Element.MixtureDistribution.Parameters[i].Value));
                }

                var min = Element.MixtureDistribution.Mixture.Minimum;
                var max = Element.MixtureDistribution.Mixture.Maximum;

                summaryStats.Add(new SummaryStatistic("Minimum", min < -1E12 ? double.NegativeInfinity : min));
                summaryStats.Add(new SummaryStatistic("Maximum", max > 1E12 ? double.PositiveInfinity : max));
                summaryStats.Add(new SummaryStatistic("Mean", Element.MixtureDistribution.Mixture.Mean));
                summaryStats.Add(new SummaryStatistic("Std Dev", Element.MixtureDistribution.Mixture.StandardDeviation));
                summaryStats.Add(new SummaryStatistic("Skewness", Element.MixtureDistribution.Mixture.Skewness));
                summaryStats.Add(new SummaryStatistic("Kurtosis", Element.MixtureDistribution.Mixture.Kurtosis));
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
        /// Handles the LoadingRow event of the SummaryStatisticsTable. Applies visual separators
        /// between different groups of statistics (parameters, moments, and goodness-of-fit measures).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the row being loaded.</param>
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

