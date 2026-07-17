using System;
using System.Collections.Generic;
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
using System.Windows.Threading;
using FrameworkUI;
using Numerics.Distributions;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and managing Point Process Analysis results and visualizations.
    /// Provides frequency plots, statistical tables, and various diagnostic plots for analyzing
    /// point process data including POT (Peaks Over Threshold) and AMS (Annual Maximum Series) approaches.
    /// </summary>
    public partial class PointProcessAnalysisControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PointProcessAnalysisControl"/> class.
        /// </summary>
        public PointProcessAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
            // DataGrid Binding.StringFormat must be set before first render.
            SetColumnStringFormats();
        }

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(PointProcessAnalysis), typeof(PointProcessAnalysisControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Point Process Analysis element being displayed in this control.
        /// </summary>
        public PointProcessAnalysis Element
        {
            get { return (PointProcessAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Element"/> property changes.
        /// Handles event subscription management and initializes the control with the new element.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as PointProcessAnalysisControl == null) return;
            var thisControl = (PointProcessAnalysisControl)d;

            // Remove handlers and detach old element's plots
            if (e.OldValue != null)
            {
                PointProcessAnalysis oldElement = e.OldValue as PointProcessAnalysis;
                if (oldElement != null)
                {
                    oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                    thisControl.FrequencyPlotHost.Content = null;
                    thisControl.FrequencyPlotToolbar.Plot = null;
                    // NOTE: PropertiesCalled is wired in XAML — no programmatic -= needed.
                }
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as PointProcessAnalysis;
            if (newElement == null) return;

            // Subscribe Element-scoped handler
            newElement.PropertyChanged += thisControl.Element_PropertyChanged;
            thisControl.SetAMSData();

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
        /// Gets a value indicating whether a plot was clicked.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Raised when plot properties are requested to be displayed or modified.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Delegate for handling plot properties called events.
        /// </summary>
        /// <param name="plot">The plot for which properties are being accessed.</param>
        /// <param name="openProperties">Indicates whether to open the properties panel.</param>
        /// <param name="propertyExpander">The property expander control.</param>
        /// <param name="selectedObject">The selected object for property display.</param>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);

        /// <summary>
        /// Raised when the preview control is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for handling preview control clicked events.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot area was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar was clicked.</param>
        /// <param name="plot">The plot that was interacted with.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Array of color hex codes used for rendering plot series.
        /// </summary>
        private string[] _colorHexCodes;

        /// <summary>
        /// Data frame containing Annual Maximum Series data derived from the POT data.
        /// </summary>
        private DataFrame amsDataFrame = new DataFrame();



        /// <summary>
        /// Handles the Loaded event for the user control. Initializes plots and data grids on first load.
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
        /// Handles property changed events from the Element. Updates plots and data displays accordingly.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing the name of the changed property.</param>
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
            if (e.PropertyName == nameof(Element.InputData) || e.PropertyName == nameof(Element.InputData.DataFrame) || e.PropertyName == nameof(Element.PointProcess.TotalYears))
            {
                SetAMSData();
                UpdateFrequencyPlot();
            }
            if (e.PropertyName == nameof(Element.InputData))
            {
                using (Element.SuspendPlotBridges())
                {
                    BindAxisTitles();
                }
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

        /// <summary>
        /// Initializes or updates the Annual Maximum Series (AMS) data frame from the input POT data.
        /// </summary>
        private void SetAMSData()
        {
            if (Element == null || Element.InputData == null || Element.InputData.DataFrame == null) return;
            amsDataFrame = Element.InputData.DataFrame.Clone();
            amsDataFrame.ApplyLangbeinConversion(Element.PointProcess.Lambda);
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
        /// Handles the LostFocus event for the user control. Resets the PlotClicked flag.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the PreviewMouseDown event for the user control. Detects clicks on plots and toolbars.
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
        /// Handles the PropertiesCalled event from plot toolbars. Raises the PlotPropertiesCalled event.
        /// </summary>
        /// <param name="targetPlot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">Indicates whether to open the properties panel.</param>
        /// <param name="propertyExpander">The property expander control.</param>
        /// <param name="selectedObject">The selected object for property display.</param>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        /// <summary>
        /// Gets the currently selected plot based on the active tab.
        /// </summary>
        /// <returns>The current plot, or null if no tab is selected.</returns>
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
        /// Gets the toolbar for the currently selected plot based on the active tab.
        /// </summary>
        /// <returns>The current plot toolbar, or null if no tab is selected.</returns>
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
        /// Handles the Checked event for the POT checkbox. Updates the frequency plot.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void POTCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateFrequencyPlot();
        }

        /// <summary>
        /// Handles the Unchecked event for the POT checkbox. Updates the frequency plot.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void POTCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateFrequencyPlot();
        }

        /// <summary>
        /// Updates the frequency plot with the current analysis results, data series, and fitted curves.
        /// Clears existing series and rebuilds the plot based on current settings and available data.
        /// </summary>
        private void UpdateFrequencyPlot()
        {
            if (FrequencyPlot == null) return;

            // Seasonal series default colors — used only if series doesn't already exist in plot.
            var seasonalColors = GenericControls.GeneralMethods.RandomColorsShortList;
            var s1Color = (Color)ColorConverter.ConvertFromString(seasonalColors[1]);
            var s1Default = Color.FromArgb(200, s1Color.R, s1Color.G, s1Color.B);
            var s2Color = (Color)ColorConverter.ConvertFromString(seasonalColors[2]);
            var s2Default = Color.FromArgb(200, s2Color.R, s2Color.G, s2Color.B);

            // Lookup-or-create named series so user-customized styling survives Save/Open.
            var frequencyExactData = FrequencyPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
                ?? new ScatterPointSeries
                {
                    Name = "ExactData",
                    Title = "POT Exact Data",
                    MarkerFill = Color.FromArgb(64, Colors.Black.R, Colors.Black.G, Colors.Black.B),
                    MarkerStroke = Color.FromArgb(128, Colors.Black.R, Colors.Black.G, Colors.Black.B),
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                };
            var frequencyLowOutlierData = FrequencyPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "POT Low Outlier Data",
                    MarkerStroke = Color.FromArgb(64, Colors.Red.R, Colors.Red.G, Colors.Red.B),
                    MarkerStrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Cross,
                };
            var frequencyUncertainData = FrequencyPlot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "UncertainData")
                ?? new ScatterErrorSeries
                {
                    Name = "UncertainData",
                    Title = "POT Uncertain Data",
                    MarkerFill = Color.FromArgb(64, Colors.Green.R, Colors.Green.G, Colors.Green.B),
                    MarkerStroke = Color.FromArgb(128, Colors.Black.R, Colors.Black.G, Colors.Black.B),
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Diamond,
                    ErrorBarColor = Color.FromArgb(128, Colors.Black.R, Colors.Black.G, Colors.Black.B),
                };
            var frequencyIntervalData = FrequencyPlot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "IntervalData")
                ?? new ScatterErrorSeries
                {
                    Name = "IntervalData",
                    Title = "POT Interval Data",
                    MarkerFill = Color.FromArgb(64, Colors.Cyan.R, Colors.Cyan.G, Colors.Cyan.B),
                    MarkerStroke = Color.FromArgb(128, Colors.Black.R, Colors.Black.G, Colors.Black.B),
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                    ErrorBarColor = Color.FromArgb(128, Colors.Black.R, Colors.Black.G, Colors.Black.B),
                };
            var amsFrequencyExactData = FrequencyPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "AMSExactData")
                ?? new ScatterPointSeries
                {
                    Name = "AMSExactData",
                    Title = "AMS Exact Data",
                    MarkerFill = Colors.Black,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                };
            var amsFrequencyLowOutlierData = FrequencyPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "AMSLowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "AMSLowOutlierData",
                    Title = "AMS Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Cross,
                };
            // Note: prior code used Name="UncertainData" for AMS (same as POT) — pre-existing name
            // collision bug. Renamed to "AMSUncertainData" here so lookup-or-create resolves correctly.
            var amsFrequencyUncertainData = FrequencyPlot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "AMSUncertainData")
                ?? new ScatterErrorSeries
                {
                    Name = "AMSUncertainData",
                    Title = "AMS Uncertain Data",
                    MarkerFill = Colors.Green,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Diamond,
                };
            var amsFrequencyIntervalData = FrequencyPlot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "AMSIntervalData")
                ?? new ScatterErrorSeries
                {
                    Name = "AMSIntervalData",
                    Title = "AMS Interval Data",
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
            var posteriorModeS1 = FrequencyPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorModeS1")
                ?? new LineSeries
                {
                    Name = "PosteriorModeS1",
                    Title = "Posterior Mode",
                    Color = s1Default,
                    MarkerStroke = s1Default,
                    LineStyle = LineStyle.Solid,
                    StrokeThickness = 1.5,
                    MarkerType = MarkerType.Triangle,
                    MarkerFill = Colors.Transparent,
                    MarkerSize = 3,
                    MarkerStrokeThickness = 1.5,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
            var posteriorModeS2 = FrequencyPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorModeS2")
                ?? new LineSeries
                {
                    Name = "PosteriorModeS2",
                    Title = "Posterior Mode",
                    Color = s2Default,
                    MarkerStroke = s2Default,
                    LineStyle = LineStyle.Solid,
                    StrokeThickness = 1.5,
                    MarkerType = MarkerType.Triangle,
                    MarkerFill = Colors.Transparent,
                    MarkerSize = 3,
                    MarkerStrokeThickness = 1.5,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };

            using (Element?.SuspendPlotBridges())
            {
                FrequencyPlot.Series.Clear();

                if (Element != null && Element.InputData != null && Element.BayesianAnalysis != null)
                {
                    // Add Curves
                    if (Element.BayesianAnalysis.IsEstimated == true && Element.AnalysisResults != null)
                    {
                        // Add sub-distributions
                        if (Element.PointProcess.IsSeasonal == true && SeasonalCheckbox.IsChecked == true)
                        {
                            var gev1 = ((CompetingRisks)Element.AnalysisResults.ParentDistribution).Distributions[0];
                            var gev2 = ((CompetingRisks)Element.AnalysisResults.ParentDistribution).Distributions[1];
                            var points1 = new List<OxyPlot.DataPoint>();
                            var points2 = new List<OxyPlot.DataPoint>();
                            for (int j = 0; j < Element.ProbabilityOrdinates.Count; j++)
                            {
                                var x = Element.ProbabilityOrdinates[j];
                                points1.Add(new OxyPlot.DataPoint(x, gev1.InverseCDF(1 - x)));
                                points2.Add(new OxyPlot.DataPoint(x, gev2.InverseCDF(1 - x)));
                            }

                            // Season 1
                            posteriorModeS1.Title = (Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode") + " - Season 1";
                            posteriorModeS1.ItemsSource = points1;
                            posteriorModeS1.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            FrequencyPlot.Series.Add(posteriorModeS1);

                            // Season 2
                            posteriorModeS2.Title = (Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode") + " - Season 2";
                            posteriorModeS2.ItemsSource = points2;
                            posteriorModeS2.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            FrequencyPlot.Series.Add(posteriorModeS2);
                        }

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

                    // POT Data
                    if (POTCheckbox.IsChecked == true)
                    {
                        frequencyExactData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                        frequencyExactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                        frequencyExactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        if (Element.InputData.DataFrame.ExactSeries.Count > 0) FrequencyPlot.Series.Add(frequencyExactData);

                        frequencyLowOutlierData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                        frequencyLowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                        frequencyLowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        if (Element.InputData.DataFrame.NumberOfLowOutliers > 0) FrequencyPlot.Series.Add(frequencyLowOutlierData);

                        frequencyUncertainData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.UncertainSeries : Element.InputData.DataFrame.UncertainSeries.Where(x => x.Value > 1E-16);
                        frequencyUncertainData.DataFieldX = nameof(UncertainData.PlottingPosition);
                        frequencyUncertainData.DataFieldY = nameof(UncertainData.Value);
                        frequencyUncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                        frequencyUncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                        frequencyUncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        if (Element.InputData.DataFrame.UncertainSeries.Count > 0) FrequencyPlot.Series.Add(frequencyUncertainData);

                        frequencyIntervalData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.IntervalSeries : Element.InputData.DataFrame.IntervalSeries.Where(x => x.Value > 1E-16);
                        frequencyIntervalData.DataFieldX = nameof(IntervalData.PlottingPosition);
                        frequencyIntervalData.DataFieldY = nameof(IntervalData.Value);
                        frequencyIntervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                        frequencyIntervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                        frequencyIntervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                        if (Element.InputData.DataFrame.IntervalSeries.Count > 0) FrequencyPlot.Series.Add(frequencyIntervalData);
                    }

                    // AMS Data
                    amsFrequencyExactData.ItemsSource = logAxis == true ? amsDataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false && x.Value > 1E-16) : amsDataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                    amsFrequencyExactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                    amsFrequencyExactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (amsDataFrame.ExactSeries.Count > 0) FrequencyPlot.Series.Add(amsFrequencyExactData);

                    amsFrequencyLowOutlierData.ItemsSource = logAxis == true ? amsDataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true && x.Value > 1E-16) : amsDataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                    amsFrequencyLowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                    amsFrequencyLowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (amsDataFrame.NumberOfLowOutliers > 0) FrequencyPlot.Series.Add(amsFrequencyLowOutlierData);

                    amsFrequencyUncertainData.ItemsSource = logAxis == true ? amsDataFrame.UncertainSeries : amsDataFrame.UncertainSeries.Where(x => x.Value > 1E-16);
                    amsFrequencyUncertainData.DataFieldX = nameof(UncertainData.PlottingPosition);
                    amsFrequencyUncertainData.DataFieldY = nameof(UncertainData.Value);
                    amsFrequencyUncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                    amsFrequencyUncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                    amsFrequencyUncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (amsDataFrame.UncertainSeries.Count > 0) FrequencyPlot.Series.Add(amsFrequencyUncertainData);

                    amsFrequencyIntervalData.ItemsSource = logAxis == true ? amsDataFrame.IntervalSeries : amsDataFrame.IntervalSeries.Where(x => x.Value > 1E-16);
                    amsFrequencyIntervalData.DataFieldX = nameof(IntervalData.PlottingPosition);
                    amsFrequencyIntervalData.DataFieldY = nameof(IntervalData.Value);
                    amsFrequencyIntervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                    amsFrequencyIntervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                    amsFrequencyIntervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (amsDataFrame.IntervalSeries.Count > 0) FrequencyPlot.Series.Add(amsFrequencyIntervalData);

                    // quantile priors
                    quantilePriors.ItemsSource = Element.PointProcess.QuantilePriors;
                    quantilePriors.DataFieldX = nameof(QuantilePrior.Alpha);
                    quantilePriors.DataFieldY = nameof(QuantilePrior.MeanValue);
                    quantilePriors.DataFieldLowerErrorY = nameof(QuantilePrior.LowerValue);
                    quantilePriors.DataFieldUpperErrorY = nameof(QuantilePrior.UpperValue);
                    quantilePriors.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.PointProcess.EnableQuantilePriors && Element.PointProcess.QuantilePriors.Count > 0) FrequencyPlot.Series.Add(quantilePriors);

                    // Add Alternatives
                    if (Element.BayesianAnalysis.IsEstimated == true && Element.AnalysisResults != null)
                        AlternativeSelector.AddAllChecked();
                }

                FrequencyPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(FrequencyPlot);
        }

        /// <summary>
        /// Handles the AnalysisAdded event from the alternative selector. Adds the alternative analysis curves to the frequency plot.
        /// </summary>
        /// <param name="analysisItem">The analysis item being added.</param>
        private void AlternativeSelector_AnalysisAdded(AnalysisAlternativeItem analysisItem)
        {
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
        /// Handles the AnalysisRemoved event from the alternative selector. Removes the alternative analysis curves from the frequency plot.
        /// </summary>
        /// <param name="analysisItem">The analysis item being removed.</param>
        private void AlternativeSelector_AnalysisRemoved(AnalysisAlternativeItem analysisItem)
        {
            FrequencyPlot.Series.Remove(analysisItem.CredibleIntervals);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorPredictive);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorMode);
            FrequencyPlot.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles the MouseLeftButtonUp event for the text block. Toggles the show filter toggle button.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowFilterToggleButton.IsChecked = !ShowFilterToggleButton.IsChecked;
        }

        #endregion

        /// <summary>
        /// Binds the frequency curve data grid to the analysis results.
        /// Populates the grid with probability ordinates and their corresponding quantile estimates.
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
        /// Sets the column headers for the frequency curve table based on the credible interval width.
        /// </summary>
        private void SetFrequencyCurveTableColumnHeaders()
        {
            double alpha = (1 - Element.BayesianAnalysis.CredibleIntervalWidth) / 2;
            UpperColumn.Header = ((1 - alpha) * 100).ToString("F1") + "% CI";
            LowerColumn.Header = (alpha * 100).ToString("F1") + "% CI";
        }

        /// <summary>
        /// Sets the string formats for data grid columns based on user settings.
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
        /// Binds the summary statistics data grid to the analysis results.
        /// Displays distribution parameters, moments, and goodness-of-fit statistics.
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

                for (int i = 0; i < Element.PointProcess.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.PointProcess.Parameters[i].DisplayName, double.NaN));
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
                for (int i = 0; i < Element.PointProcess.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.PointProcess.Parameters[i].DisplayName, Element.PointProcess.Parameters[i].Value));
                }
                var min = Element.PointProcess.Distribution.Minimum;
                var max = Element.PointProcess.Distribution.Maximum;

                summaryStats.Add(new SummaryStatistic("Minimum", min < -1E12 ? double.NegativeInfinity : min));
                summaryStats.Add(new SummaryStatistic("Maximum", max > 1E12 ? double.PositiveInfinity : max));
                summaryStats.Add(new SummaryStatistic("Mean", Element.PointProcess.Distribution.Mean));
                summaryStats.Add(new SummaryStatistic("Std Dev", Element.PointProcess.Distribution.StandardDeviation));
                summaryStats.Add(new SummaryStatistic("Skewness", Element.PointProcess.Distribution.Skewness));
                summaryStats.Add(new SummaryStatistic("Kurtosis", Element.PointProcess.Distribution.Kurtosis));
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
        /// Handles the LoadingRow event for the summary statistics table.
        /// Applies custom border styling to separate sections of statistics.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">DataGrid row event arguments.</param>
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
