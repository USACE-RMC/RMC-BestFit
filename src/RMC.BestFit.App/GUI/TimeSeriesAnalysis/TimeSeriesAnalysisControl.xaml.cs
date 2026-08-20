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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using FrameworkUI;
using System.Windows.Media.Media3D;
using Numerics.Distributions;
using Numerics.Data.Statistics;
using Numerics;
using System.Windows.Data;
using Numerics.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and managing time series analysis results, including plots, residuals,
    /// and statistical diagnostics for ARIMAX models with Bayesian estimation.
    /// </summary>
    public partial class TimeSeriesAnalysisControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSeriesAnalysisControl"/> class.
        /// </summary>
        public TimeSeriesAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
            // DataGrid Binding.StringFormat must be set before first render.
            SetColumnStringFormats();
        }

        /// <summary>
        /// Dependency property for the Element property. This represents the TimeSeriesAnalysis model being displayed.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(TimeSeriesAnalysis), typeof(TimeSeriesAnalysisControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the TimeSeriesAnalysis element that contains the model and analysis results.
        /// </summary>
        public TimeSeriesAnalysis Element
        {
            get { return (TimeSeriesAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element dependency property changes.
        /// Handles attaching and detaching event handlers for the new and old elements.
        /// </summary>
        /// <param name="d">The dependency object that owns the property.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as TimeSeriesAnalysisControl == null) return;
            var thisControl = (TimeSeriesAnalysisControl)d;

            // Remove handlers
            if (e.OldValue != null)
            {
                TimeSeriesAnalysis oldElement = e.OldValue as TimeSeriesAnalysis;
                if (oldElement != null)
                {
                    oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;

                    // Unsubscribe adaptive date-format handlers from the old element's plot models (TSA1).
                    if (oldElement.TimeSeriesPlot?.ActualModel != null)
                        oldElement.TimeSeriesPlot.ActualModel.Updated -= thisControl.TimeSeriesPlotModelUpdated;
                    if (oldElement.ResidualPlot?.ActualModel != null)
                        oldElement.ResidualPlot.ActualModel.Updated -= thisControl.ResidualPlotModelUpdated;

                    thisControl.TimeSeriesPlotHost.Content = null;
                    thisControl.ResidualPlotHost.Content = null;
                    thisControl.ResidualHistogramPlotHost.Content = null;
                    thisControl.ResidualQQPlotHost.Content = null;
                    thisControl.ResidualACFPlotHost.Content = null;
                    thisControl.ResidualPACFPlotHost.Content = null;
                    thisControl.PlotToolbar.Plot = null;
                    thisControl.ResidualsPlotToolbar.Plot = null;
                    thisControl.HistogramPlotToolbar.Plot = null;
                    thisControl.QQPlotToolbar.Plot = null;
                    thisControl.ACFPlotToolbar.Plot = null;
                    thisControl.PACFPlotToolbar.Plot = null;
                    // NOTE: PropertiesCalled is wired in XAML for every toolbar — no programmatic -= needed.
                }
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as TimeSeriesAnalysis;
            if (newElement == null) return;

            // Reset _isLoaded so the next Loaded event triggers a full data refresh for
            // the new element (TSA2).
            thisControl._isLoaded = false;

            // Subscribe Element-scoped handler
            newElement.PropertyChanged += thisControl.Element_PropertyChanged;

            // Attach plots, wire toolbars, and wire Bayesian sub-control plots inside a
            // bridge-suspension block. PropertiesCalled / PlotPropertiesCalled are wired
            // in XAML — no programmatic += needed.
            using (newElement.SuspendPlotBridges())
            {
                thisControl.TimeSeriesPlotHost.Content = newElement.TimeSeriesPlot;
                thisControl.PlotToolbar.Plot = newElement.TimeSeriesPlot;
                thisControl.ResidualPlotHost.Content = newElement.ResidualPlot;
                thisControl.ResidualsPlotToolbar.Plot = newElement.ResidualPlot;
                thisControl.ResidualHistogramPlotHost.Content = newElement.ResidualHistogramPlot;
                thisControl.HistogramPlotToolbar.Plot = newElement.ResidualHistogramPlot;
                thisControl.ResidualQQPlotHost.Content = newElement.ResidualQQPlot;
                thisControl.QQPlotToolbar.Plot = newElement.ResidualQQPlot;
                thisControl.ResidualACFPlotHost.Content = newElement.ResidualACFPlot;
                thisControl.ACFPlotToolbar.Plot = newElement.ResidualACFPlot;
                thisControl.ResidualPACFPlotHost.Content = newElement.ResidualPACFPlot;
                thisControl.PACFPlotToolbar.Plot = newElement.ResidualPACFPlot;

                thisControl.HistogramControl.SetPlot(newElement.BayesianPlots.HistogramPlot);
                thisControl.KernelDensityControl.SetPlot(newElement.BayesianPlots.KernelDensityPlot);
                thisControl.AutocorrelationControl.SetPlot(newElement.BayesianPlots.AutocorrelationPlot);
                thisControl.MarkovChainTraceControl.SetPlot(newElement.BayesianPlots.MarkovChainTracePlot);
                thisControl.MeanLikelihoodControl.SetPlot(newElement.BayesianPlots.MeanLikelihoodPlot);
                thisControl.BivariateHeatMapControl.SetPlot(newElement.BayesianPlots.BivariateHeatMapPlot);
                thisControl.InfluenceDiagnosticsControl.SetPlot(newElement.BayesianPlots.InfluenceDiagnosticsPlot);

                // Re-establish axis-title Bindings wiped by DeserializePlotSettings (must be
                // inside suspension to prevent "Change Title" undo entries from axis PropertyChanged)
                thisControl.BindAxisTitles();
            }

            // Subscribe adaptive date-format handlers to the new element's plot ActualModels (TSA1).
            // These are Element-scoped (attached to plots owned by the element), so they are
            // wired here rather than in Loaded/Unloaded.
            if (newElement.TimeSeriesPlot?.ActualModel != null)
                newElement.TimeSeriesPlot.ActualModel.Updated += thisControl.TimeSeriesPlotModelUpdated;
            if (newElement.ResidualPlot?.ActualModel != null)
                newElement.ResidualPlot.ActualModel.Updated += thisControl.ResidualPlotModelUpdated;
        }

        /// <summary>
        /// Binds axis titles on Element-owned plots to the TimeSeriesData.UnitLabel property.
        /// Called after plot attachment and when TimeSeriesData changes.
        /// </summary>
        /// <remarks>
        /// The time-series Y-axis reflects the unit of the series; the X-axis is a DateTimeAxis
        /// and remains titled by the factory default. The residual-vs-fitted plot X-axis also
        /// follows the modeled series unit label, while residual diagnostic plots retain their
        /// factory-default residual, density, quantile, ACF, and PACF titles.
        /// </remarks>
        private void BindAxisTitles()
        {
            if (Element == null) return;

            var yAxis = Element.TimeSeriesPlot?.Axes.FirstOrDefault(a => a.Key == "Yaxis");
            if (yAxis != null)
            {
                if (Element.TimeSeriesData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(yAxis, Element.TimeSeriesData, nameof(TimeSeriesElement.UnitLabel), Element.TimeSeriesData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(yAxis, string.Empty);
            }

            var residualYAxis = ResidualPlot?.Axes.FirstOrDefault(a => a.Key == "Yaxis");
            if (residualYAxis != null)
                PlotAxisTitleDefaults.SetTitleIfDefault(residualYAxis, "Residual");

            var residualXAxis = ResidualPlot?.Axes.FirstOrDefault(a => a.Key == "Xaxis");
            if (residualXAxis != null)
            {
                if (Element.TimeSeriesData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(residualXAxis, Element.TimeSeriesData, nameof(TimeSeriesElement.UnitLabel), Element.TimeSeriesData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(residualXAxis, string.Empty);
            }
        }

        /// <summary>Convenience accessor for the Element-owned time series plot.</summary>
        private Plot TimeSeriesPlot => Element?.TimeSeriesPlot;
        /// <summary>Convenience accessor for the Element-owned residual plot.</summary>
        private Plot ResidualPlot => Element?.ResidualPlot;
        /// <summary>Convenience accessor for the Element-owned residual histogram plot.</summary>
        private Plot ResidualHistogramPlot => Element?.ResidualHistogramPlot;
        /// <summary>Convenience accessor for the Element-owned residual QQ plot.</summary>
        private Plot ResidualQQPlot => Element?.ResidualQQPlot;
        /// <summary>Convenience accessor for the Element-owned residual ACF plot.</summary>
        private Plot ResidualACFPlot => Element?.ResidualACFPlot;
        /// <summary>Convenience accessor for the Element-owned residual PACF plot.</summary>
        private Plot ResidualPACFPlot => Element?.ResidualPACFPlot;

        /// <summary>
        /// Gets a value indicating whether a plot has been clicked by the user.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Event raised when plot properties are requested to be displayed or modified.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Delegate for the PlotPropertiesCalled event.
        /// </summary>
        /// <param name="plot">The plot whose properties are being called.</param>
        /// <param name="openProperties">Indicates whether to open the properties panel.</param>
        /// <param name="propertyExpander">The property expander control.</param>
        /// <param name="selectedObject">The selected object in the plot.</param>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);

        /// <summary>
        /// Event raised when the preview control is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for the PreviewControlClicked event.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot area was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar was clicked.</param>
        /// <param name="plot">The plot control that was involved in the click.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Indicates whether the control has completed its initial load operations.
        /// Set to false by ElementCallback when a new element is assigned so the next
        /// Loaded event re-runs one-time setup steps (TSA2).
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// Array of residuals from the ARIMAX model fit.
        /// </summary>
        private double[] _residuals = null;

        /// <summary>
        /// Array of color hex codes used for distinguishing multiple series in plots.
        /// </summary>
        private string[] _colorHexCodes;

        #region Plot Series

        #endregion

        /// <summary>
        /// Handles the Loaded event of the UserControl. Initializes plots, data grids, and settings when the control is first loaded.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            // Data refresh on every load. Plot hosts, toolbars, and Element.PropertyChanged
            // are wired once per Element in ElementCallback.
            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                using (Element.SuspendPlotBridges())
                {
                    BindAxisTitles();
                }
                UpdatePlots();
                BindTimeSeriesDataGrid();
                if (!_isLoaded) SetTableColumnHeaders();
                BindSummaryStatisticsDataGrid();
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
            _isLoaded = true;
        }

        /// <summary>
        /// Handles the Unloaded event of the user control. Resets <see cref="_isLoaded"/> so the
        /// next Loaded event triggers a full data refresh (TSA2).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Reset _isLoaded so the next Loaded event re-runs data-refresh steps.
            // Element-scoped lifecycle (PropertyChanged, plot hosts, toolbars) is owned by
            // ElementCallback and survives unload/reload cycles — no additional teardown needed.
            _isLoaded = false;
        }

        /// <summary>
        /// Handles property changes on the Element object and updates the appropriate UI components.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the name of the property that changed.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Marshal to UI thread if called from a background thread.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Element_PropertyChanged(sender, e)));
                return;
            }

            if (!ReferenceEquals(sender, Element)) return;

            if (e.PropertyName == nameof(Element.TimeSeriesData))
            {
                using (Element.SuspendPlotBridges())
                {
                    BindAxisTitles();
                }
            }
            if (e.PropertyName == nameof(Element.AnalysisResults))
            {
                UpdatePlots();
                BindTimeSeriesDataGrid();
                BindSummaryStatisticsDataGrid();
                ResetWaitCursor();
            }
            if (e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth))
            {
                SetTableColumnHeaders();
            }
            // TrainingTimeSteps / ForecastingTimeSteps changes are owned by the analysis
            // layer (Phase 6 whitelist + Phase 3b reprocess pattern). The AnalysisResults
            // PropertyChanged branch above refreshes plots and tables when the rebuild
            // completes — no separate App-side trigger needed. Note: TrainingTimeSteps
            // changes the fit and follows the ClearResults path (no wait cursor); only
            // ForecastSteps reprocesses.
            if ((e.PropertyName == nameof(Element.BayesianAnalysis.PointEstimator)
                 || e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth)
                 || e.PropertyName == nameof(Element.ForecastSteps)
                 || e.PropertyName == nameof(Element.CovariateExtension))
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
        /// Handles the LostFocus event of the UserControl. Resets the PlotClicked property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the PreviewMouseDown event to detect clicks on plots and toolbars.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
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
        /// Handles the PropertiesCalled event from the plot toolbar and forwards it to the control's event handler.
        /// </summary>
        /// <param name="targetPlot">The plot whose properties are being requested.</param>
        /// <param name="openProperties">Indicates whether to open the properties panel.</param>
        /// <param name="propertyExpander">The property expander control.</param>
        /// <param name="selectedObject">The selected object in the plot.</param>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        /// <summary>
        /// Gets the currently selected plot based on which tab is active.
        /// </summary>
        /// <returns>The currently selected Plot control, or null if no plot tab is selected.</returns>
        public Plot GetCurrentPlot()
        {
            if (PlotTabItem.IsSelected == true)
            {
                return TimeSeriesPlot;
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
            else if (ResidualACFTab.IsSelected == true)
            {
                return ResidualACFPlot;
            }
            else if (ResidualPACFTab.IsSelected == true)
            {
                return ResidualPACFPlot;
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
        /// Gets the toolbar for the currently selected plot based on which tab is active.
        /// </summary>
        /// <returns>The toolbar for the currently selected plot, or null if no plot tab is selected.</returns>
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
            else if (ResidualACFTab.IsSelected == true)
            {
                return ACFPlotToolbar;
            }
            else if (ResidualPACFTab.IsSelected == true)
            {
                return PACFPlotToolbar;
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
        /// Handles the Updated event of the time series plot model. Adjusts the date/time axis format based on the date range displayed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void TimeSeriesPlotModelUpdated(object sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, Element?.TimeSeriesPlot?.ActualModel)) return;

            foreach (var axis in TimeSeriesPlot.Axes)
            {
                if (axis.Key == "Xaxis" && axis as OxyPlot.Wpf.DateTimeAxis != null)
                {
                    var dateTimeAxis = (OxyPlot.Axes.DateTimeAxis)axis.InternalAxis;
                    double min = dateTimeAxis.ActualMinimum;
                    double max = dateTimeAxis.ActualMaximum;
                    var dateSpan = dateTimeAxis.ConvertToDateTime(max).Subtract(dateTimeAxis.ConvertToDateTime(min));
                    if (dateSpan.TotalDays > 365 * 4)
                    {
                        dateTimeAxis.StringFormat = "yyyy";
                    }
                    else if (dateSpan.TotalDays > 365)
                    {
                        dateTimeAxis.StringFormat = "MMM-yyyy";
                    }
                    else if (dateSpan.TotalDays > 90)
                    {
                        dateTimeAxis.StringFormat = "MMM-yyyy";
                    }
                    else if (dateSpan.TotalDays > 2)
                    {
                        dateTimeAxis.StringFormat = "dd-MMM";
                    }
                    else
                    {
                        dateTimeAxis.StringFormat = "HH:mm";
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Handles the Updated event of the residual plot model. Adjusts the date/time axis format based on the date range displayed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void ResidualPlotModelUpdated(object sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, Element?.ResidualPlot?.ActualModel)) return;

            foreach (var axis in ResidualPlot.Axes)
            {
                if (axis.Key == "Xaxis" && axis as OxyPlot.Wpf.DateTimeAxis != null)
                {
                    var dateTimeAxis = (OxyPlot.Axes.DateTimeAxis)axis.InternalAxis;
                    double min = dateTimeAxis.ActualMinimum;
                    double max = dateTimeAxis.ActualMaximum;
                    var dateSpan = dateTimeAxis.ConvertToDateTime(max).Subtract(dateTimeAxis.ConvertToDateTime(min));
                    if (dateSpan.TotalDays > 365 * 4)
                    {
                        dateTimeAxis.StringFormat = "yyyy";
                    }
                    else if (dateSpan.TotalDays > 365)
                    {
                        dateTimeAxis.StringFormat = "MMM-yyyy";
                    }
                    else if (dateSpan.TotalDays > 90)
                    {
                        dateTimeAxis.StringFormat = "MMM-yyyy";
                    }
                    else if (dateSpan.TotalDays > 2)
                    {
                        dateTimeAxis.StringFormat = "dd-MMM";
                    }
                    else
                    {
                        dateTimeAxis.StringFormat = "HH:mm";
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Updates all diagnostic plots with the latest analysis results.
        /// </summary>
        private void UpdatePlots()
        {
            UpdateTimeSeriesPlot();
            GetResiduals();
            UpdateResidualsPlot();
            UpdateHistogramPlot();
            UpdateQQPlot();
            UpdateACFPlot();
            UpdatePACFPlot();
        }

        /// <summary>
        /// Updates the time series plot with the observed data, fitted curves, and credible intervals.
        /// </summary>
        /// <remarks>
        /// All series and annotation mutations live inside a single <c>SuspendPlotBridges</c>
        /// block so plot-undo bridges do not record programmatic changes. <c>InvalidatePlot(true)</c>
        /// fires once as the last statement inside the block, and <c>RebuildSeriesAndAnnotationBridges</c>
        /// is called after the block closes to reattach bridges to the new series/annotations.
        /// </remarks>
        private void UpdateTimeSeriesPlot()
        {
            if (TimeSeriesPlot == null) return;

            // Look up named series/annotations before Clear(). If found (from deserialization
            // or a prior update), reuse so user-customized styling survives Save/Open. Otherwise
            // create with defaults. Matches canonical TimeSeriesControl pattern.
            var timeSeriesLine = TimeSeriesPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "TimeSeries")
                ?? new LineSeries
                {
                    Name = "TimeSeries",
                    Title = "Time Series",
                    Color = Colors.Red,
                    MarkerFill = Colors.Transparent,
                    StrokeThickness = 2,
                    LineStyle = OxyPlot.LineStyle.Dot,
                };
            var trainingCredibleIntervals = TimeSeriesPlot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "TrainingCredibleIntervals")
                ?? new AreaSeries
                {
                    Name = "TrainingCredibleIntervals",
                    Fill = Color.FromArgb(75, 104, 140, 175),
                    Color = Color.FromArgb(255, 53, 59, 122),
                    LineStyle = LineStyle.Solid,
                    BrokenLineThickness = 1,
                    StrokeThickness = 1,
                    Decimator = OxyPlot.Decimator.Decimate,
                };
            var predictionCredibleIntervals = TimeSeriesPlot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "PredictionCredibleIntervals")
                ?? new AreaSeries
                {
                    Name = "PredictionCredibleIntervals",
                    Fill = Color.FromArgb(75, 220, 20, 60),
                    Color = Color.FromArgb(255, 255, 0, 0),
                    LineStyle = LineStyle.Solid,
                    BrokenLineThickness = 1,
                    StrokeThickness = 1,
                    Decimator = OxyPlot.Decimator.Decimate,
                };
            var posteriorPredictive = TimeSeriesPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorPredictive")
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
            var posteriorMode = TimeSeriesPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorMode")
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
            string timeSeriesTracker = "{0}" + Environment.NewLine
                + "{1}: {2}" + Environment.NewLine
                + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
            var trainingLine = TimeSeriesPlot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "TrainingLine")
                ?? new LineAnnotation
                {
                    Name = "TrainingLine",
                    Text = "End of Training Period",
                    Type = OxyPlot.Annotations.LineAnnotationType.Vertical,
                    Color = Colors.Black,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 3,
                    TextLinePosition = 0.3,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                };

            using (Element?.SuspendPlotBridges())
            {
                TimeSeriesPlot.Series.Clear();
                for (int i = TimeSeriesPlot.Annotations.Count - 1; i >= 0; i--)
                {
                    if (TimeSeriesPlot.Annotations[i].Name == "TrainingLine")
                    {
                        TimeSeriesPlot.Annotations.RemoveAt(i);
                        break;
                    }
                }

                if (Element != null && Element.TimeSeriesData != null && Element.TimeSeriesData.TimeSeries != null
                    && Element.ARIMAX != null && Element.ARIMAX.TimeSeries != null && Element.ARIMAX.TimeSeries.Count > 0)
                {
                    // Add Curves
                    if (Element.BayesianAnalysis != null && Element.BayesianAnalysis.IsEstimated == true && Element.AnalysisResults != null)
                    {
                        var ciPoints = new List<Point3D>();
                        var prdPoints = new List<OxyPlot.DataPoint>();
                        var mdPoints = new List<OxyPlot.DataPoint>();

                        // first get mode curve
                        var modeDates = TimeSeriesResultTimeline.CreateDates(Element.ARIMAX.TimeSeries, Element.AnalysisResults.ModeCurve.Length);
                        for (int i = 0; i < Element.AnalysisResults.ModeCurve.Length; i++)
                        {
                            var index = modeDates[i].ToOADate();
                            var md = Element.AnalysisResults.ModeCurve[i];
                            mdPoints.Add(new OxyPlot.DataPoint(index, md));
                        }

                        // next get the full prediction
                        var predictionDates = TimeSeriesResultTimeline.CreateDates(Element.ARIMAX.TimeSeries, Element.AnalysisResults.MeanCurve.Length);
                        for (int i = 0; i < Element.AnalysisResults.MeanCurve.Length; i++)
                        {
                            var index = predictionDates[i].ToOADate();
                            var lo = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                            var up = Element.AnalysisResults.ConfidenceIntervals[i, 2];
                            var prd = Element.AnalysisResults.MeanCurve[i];
                            ciPoints.Add(new Point3D(index, lo, up));
                            prdPoints.Add(new OxyPlot.DataPoint(index, prd));
                        }

                        // Split CI points at the training boundary so the training fit and the
                        // validation+forecast prediction render in distinct colors. Include the
                        // boundary index in BOTH sublists so the two AreaSeries touch without a seam.
                        int splitIdx = Math.Max(0, Math.Min(ciPoints.Count - 1, Element.ARIMAX.TrainingTimeSteps - 1));
                        var trainingCi = ciPoints.GetRange(0, splitIdx + 1);
                        var predictionCi = splitIdx < ciPoints.Count - 1
                            ? ciPoints.GetRange(splitIdx, ciPoints.Count - splitIdx)
                            : null;

                        var ciPct = (Element.BayesianAnalysis.CredibleIntervalWidth * 100).ToString("F0");

                        // Training Credible Intervals (light blue)
                        trainingCredibleIntervals.ItemsSource = trainingCi;
                        trainingCredibleIntervals.DataFieldX = "X";
                        trainingCredibleIntervals.DataFieldY = "Y";
                        trainingCredibleIntervals.DataFieldX2 = "X";
                        trainingCredibleIntervals.DataFieldY2 = "Z";
                        trainingCredibleIntervals.Title = ciPct + "% Credible Intervals — Training";
                        trainingCredibleIntervals.TrackerFormatString = timeSeriesTracker;
                        TimeSeriesPlot.Series.Add(trainingCredibleIntervals);

                        // Prediction Credible Intervals (light red) — only if there's a non-trivial
                        // validation + forecast window beyond the training cutoff.
                        if (predictionCi != null)
                        {
                            predictionCredibleIntervals.ItemsSource = predictionCi;
                            predictionCredibleIntervals.DataFieldX = "X";
                            predictionCredibleIntervals.DataFieldY = "Y";
                            predictionCredibleIntervals.DataFieldX2 = "X";
                            predictionCredibleIntervals.DataFieldY2 = "Z";
                            predictionCredibleIntervals.Title = ciPct + "% Credible Intervals — Prediction";
                            predictionCredibleIntervals.TrackerFormatString = timeSeriesTracker;
                            TimeSeriesPlot.Series.Add(predictionCredibleIntervals);
                        }

                        // Predictive
                        posteriorPredictive.ItemsSource = prdPoints;
                        posteriorPredictive.TrackerFormatString = timeSeriesTracker;
                        TimeSeriesPlot.Series.Add(posteriorPredictive);

                        // Point Estimator
                        posteriorMode.Title = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";
                        posteriorMode.ItemsSource = mdPoints;
                        posteriorMode.TrackerFormatString = timeSeriesTracker;
                        TimeSeriesPlot.Series.Add(posteriorMode);
                    }

                    timeSeriesLine.ItemsSource = Element.TimeSeriesData.TimeSeries;
                    timeSeriesLine.Mapping = item => { var o = (Numerics.Data.SeriesOrdinate<DateTime, double>)item; return new OxyPlot.DataPoint(OxyPlot.Axes.DateTimeAxis.ToDouble(o.Index), o.Value); };
                    timeSeriesLine.TrackerFormatString = timeSeriesTracker;
                    TimeSeriesPlot.Series.Add(timeSeriesLine);

                    int trainIdx = Element.ARIMAX.TrainingTimeSteps - 1;
                    if (trainIdx >= 0 && trainIdx < Element.ARIMAX.TimeSeries.Count)
                    {
                        trainingLine.X = Element.ARIMAX.TimeSeries[trainIdx].Index.ToOADate();
                        TimeSeriesPlot.Annotations.Add(trainingLine);
                    }
                }

                TimeSeriesPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(TimeSeriesPlot);
        }

        /// <summary>
        /// Handles the AnalysisAdded event from the alternative selector. Adds the alternative analysis curves to the time series plot.
        /// </summary>
        /// <param name="analysisItem">The analysis alternative item that was added.</param>
        private void AlternativeSelector_AnalysisAdded(AnalysisAlternativeItem analysisItem)
        {
            // Remove series
            TimeSeriesPlot.Series.Remove(analysisItem.CredibleIntervals);
            TimeSeriesPlot.Series.Remove(analysisItem.PosteriorPredictive);
            TimeSeriesPlot.Series.Remove(analysisItem.PosteriorMode);

            var alternative = (TimeSeriesAnalysis)analysisItem.Alternative;
            if (alternative.BayesianAnalysis.IsEstimated == true && alternative.AnalysisResults != null)
            {
                // Get unique color
                var index = TimeSeriesPlot.Series.Count;
                Color lineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[index]);
                Color fillColor = Color.FromArgb(100, lineColor.R, lineColor.G, lineColor.B);

                for (int i = 4; i < _colorHexCodes.Length; i++)
                {
                    var templineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[i]);
                    var tempfillColor = Color.FromArgb(100, templineColor.R, templineColor.G, templineColor.B);
                    bool colorExists = false;
                    for (int j = 0; j < TimeSeriesPlot.Series.Count; j++)
                    {
                        if (TimeSeriesPlot.Series[j].Name.Contains("Mode") && TimeSeriesPlot.Series[j].Color == templineColor)
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
                string timeSeriesTracker = "{0}" + Environment.NewLine
                    + "{1}: {2}" + Environment.NewLine
                    + "{3}: {4:" + UserSettings.ValueStringFormat + "}";

                // first get mode curve
                var modeDates = TimeSeriesResultTimeline.CreateDates(alternative.ARIMAX.TimeSeries, alternative.AnalysisResults.ModeCurve.Length);
                for (int i = 0; i < alternative.AnalysisResults.ModeCurve.Length; i++)
                {
                    var idx = modeDates[i].ToOADate();
                    var md = alternative.AnalysisResults.ModeCurve[i];
                    mdPoints.Add(new OxyPlot.DataPoint(idx, md));
                }

                // next get full prediction
                var predictionDates = TimeSeriesResultTimeline.CreateDates(alternative.ARIMAX.TimeSeries, alternative.AnalysisResults.MeanCurve.Length);
                for (int i = 0; i < alternative.AnalysisResults.MeanCurve.Length; i++)
                {
                    var idx = predictionDates[i].ToOADate();
                    var lo = alternative.AnalysisResults.ConfidenceIntervals[i, 1];
                    var up = alternative.AnalysisResults.ConfidenceIntervals[i, 2];
                    var prd = alternative.AnalysisResults.MeanCurve[i];
                    ciPoints.Add(new Point3D(idx, lo, up));
                    prdPoints.Add(new OxyPlot.DataPoint(idx, prd));
                }

                // Credible Intervals
                analysisItem.CredibleIntervals.Name = "CredibleIntervals_" + index;
                analysisItem.CredibleIntervals.Title = alternative.Name + " - " + (alternative.BayesianAnalysis.CredibleIntervalWidth * 100).ToString("F0") + "% Credible Intervals";
                analysisItem.CredibleIntervals.ItemsSource = ciPoints;
                analysisItem.CredibleIntervals.DataFieldX = "X";
                analysisItem.CredibleIntervals.DataFieldY = "Y";
                analysisItem.CredibleIntervals.DataFieldX2 = "X";
                analysisItem.CredibleIntervals.DataFieldY2 = "Z";
                analysisItem.CredibleIntervals.Fill = fillColor;
                analysisItem.CredibleIntervals.Color = Colors.Transparent;
                analysisItem.CredibleIntervals.TrackerFormatString = timeSeriesTracker;
                TimeSeriesPlot.Series.Add(analysisItem.CredibleIntervals);

                // Predictive
                analysisItem.PosteriorPredictive.Name = "PosteriorPredictive_" + index;
                analysisItem.PosteriorPredictive.Title = alternative.Name + " - Posterior Predictive";
                analysisItem.PosteriorPredictive.ItemsSource = prdPoints;
                analysisItem.PosteriorPredictive.Color = lineColor;
                analysisItem.PosteriorPredictive.TrackerFormatString = timeSeriesTracker;
                TimeSeriesPlot.Series.Add(analysisItem.PosteriorPredictive);

                // Point Estimator
                analysisItem.PosteriorMode.Name = "PosteriorMode_" + index;
                analysisItem.PosteriorMode.Title = alternative.Name + " - " + (alternative.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode");
                analysisItem.PosteriorMode.ItemsSource = mdPoints;
                analysisItem.PosteriorMode.Color = lineColor;
                analysisItem.PosteriorMode.TrackerFormatString = timeSeriesTracker;
                TimeSeriesPlot.Series.Add(analysisItem.PosteriorMode);

                TimeSeriesPlot.InvalidatePlot(true);

            }

        }

        /// <summary>
        /// Handles the AnalysisRemoved event from the alternative selector. Removes the alternative analysis curves from the time series plot.
        /// </summary>
        /// <param name="analysisItem">The analysis alternative item that was removed.</param>
        private void AlternativeSelector_AnalysisRemoved(AnalysisAlternativeItem analysisItem)
        {
            TimeSeriesPlot.Series.Remove(analysisItem.CredibleIntervals);
            TimeSeriesPlot.Series.Remove(analysisItem.PosteriorPredictive);
            TimeSeriesPlot.Series.Remove(analysisItem.PosteriorMode);
            TimeSeriesPlot.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles the MouseLeftButtonUp event on the text block. Toggles the filter visibility.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowFilterToggleButton.IsChecked = !ShowFilterToggleButton.IsChecked;
        }

        /// <summary>
        /// Calculates and stores the residuals from the fitted ARIMAX model using the current parameter values.
        /// </summary>
        private void GetResiduals()
        {
            _residuals = null;
            if (Element == null || Element.BayesianAnalysis == null || Element.IsValid == false || Element.BayesianAnalysis.IsEstimated == false) return;
            _residuals = Element.ARIMAX.Residuals(Element.ARIMAX.Parameters.Select(x => x.Value).ToArray());
        }

        /// <summary>
        /// Updates the residuals plot with the current residuals from the model fit.
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

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid == true && Element.BayesianAnalysis.IsEstimated == true)
                {
                    var points = new List<DataPoint>();
                    int residualCount = Math.Min(_residuals!.Length, Element.ARIMAX.TrainingTimeSeries.Count);
                    for (int i = 0; i < residualCount; i++)
                    {
                        points.Add(new DataPoint(Element.ARIMAX.TrainingTimeSeries[i].Index.ToOADate(), _residuals[i]));
                    }

                    residualsSeries.ItemsSource = points;
                    residualsSeries.Mapping = item => { var d = (DataPoint)item; return new OxyPlot.Series.ScatterPoint(d.X, d.Y); };
                    residualsSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    ResidualPlot.Series.Add(residualsSeries);
                    ResidualPlot.Annotations.Add(zeroLine);
                }

                ResidualPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(ResidualPlot);
        }

        /// <summary>
        /// Updates the histogram plot showing the distribution of residuals with an overlaid normal density curve.
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

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid == true && Element.BayesianAnalysis.IsEstimated == true)
                {
                    var values = _residuals;
                    var histogram = new Histogram(values);
                    var histItems = new List<OxyPlot.Series.HistogramItem>();
                    double sum = 0;
                    for (int i = 0; i < histogram.NumberOfBins; i++)
                        sum += histogram[i].Frequency * histogram.BinWidth;
                    for (int i = 0; i < histogram.NumberOfBins; i++)
                        histItems.Add(new OxyPlot.Series.HistogramItem(histogram[i].LowerBound, histogram[i].UpperBound, histogram[i].Frequency * histogram.BinWidth / sum));

                    histogramSeries.ItemsSource = histItems;
                    histogramSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}";
                    ResidualHistogramPlot.Series.Add(histogramSeries);

                    var normal = new Normal(0, Element.ARIMAX.Parameters.Last().Value);
                    var normalPdf = normal.CreatePDFGraph();
                    var normalPdfPoints = new List<DataPoint>();
                    for (int i = 0; i < normalPdf.GetLength(0); i++)
                        normalPdfPoints.Add(new DataPoint(normalPdf[i, 0], normalPdf[i, 1]));

                    normalDensitySeries.ItemsSource = normalPdfPoints;
                    normalDensitySeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.0000}" + Environment.NewLine + "{3}: {4:0.000000}";
                    ResidualHistogramPlot.Series.Add(normalDensitySeries);
                }

                ResidualHistogramPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(ResidualHistogramPlot);
        }

        /// <summary>
        /// Updates the Q-Q (quantile-quantile) plot for assessing normality of residuals.
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

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid == true && Element.BayesianAnalysis.IsEstimated == true)
                {
                    var values = new double[_residuals.Length];
                    Array.Copy(_residuals, values, _residuals.Length);
                    Array.Sort(values);
                    var muSigma = Statistics.MeanStandardDeviation(values);
                    var normal = new Normal(muSigma.Item1, muSigma.Item2);
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

                ResidualQQPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(ResidualQQPlot);
        }

        /// <summary>
        /// Updates the ACF (Autocorrelation Function) plot to check for correlation in residuals at different lags.
        /// </summary>
        private void UpdateACFPlot()
        {
            if (ResidualACFPlot == null) return;

            var acfSeries = ResidualACFPlot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "Autocorrelation")
                ?? new HistogramSeries
                {
                    Name = "Autocorrelation",
                    Title = "Autocorrelation",
                    FillColor = Color.FromArgb(125, 104, 140, 175),
                    StrokeColor = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1,
                };
            var acfLowerLine = ResidualACFPlot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "LowerCI")
                ?? new LineAnnotation
                {
                    Name = "LowerCI",
                    Text = "2.5% CI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Top,
                };
            var acfUpperLine = ResidualACFPlot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "UpperCI")
                ?? new LineAnnotation
                {
                    Name = "UpperCI",
                    Text = "97.5% CI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                };

            using (Element?.SuspendPlotBridges())
            {
                ResidualACFPlot.Series.Clear();
                for (int i = ResidualACFPlot.Annotations.Count - 1; i >= 0; i--)
                {
                    if (ResidualACFPlot.Annotations[i].Name == "LowerCI" || ResidualACFPlot.Annotations[i].Name == "UpperCI")
                    {
                        ResidualACFPlot.Annotations.RemoveAt(i);
                    }
                }

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid == true && Element.BayesianAnalysis.IsEstimated == true)
                {
                    var acfItems = new List<OxyPlot.Series.HistogramItem>();
                    var acf = Autocorrelation.Function(_residuals);
                    for (int i = 0; i < acf.GetLength(0); i++)
                    {
                        acfItems.Add(new OxyPlot.Series.HistogramItem(i, i + 1, acf[i, 1]));
                    }

                    acfSeries.ItemsSource = acfItems;
                    acfSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:0.000000}";
                    ResidualACFPlot.Series.Add(acfSeries);

                    var ci = Autocorrelation.CorrelationConfidenceInterval(_residuals.Length);
                    acfLowerLine.Y = ci[0];
                    acfUpperLine.Y = ci[1];
                    ResidualACFPlot.Annotations.Add(acfLowerLine);
                    ResidualACFPlot.Annotations.Add(acfUpperLine);

                    foreach (var axis in ResidualACFPlot.Axes)
                    {
                        if (axis.Key == "Yaxis")
                        {
                            axis.Minimum = Math.Round((Math.Min(ci[0], Tools.Min(acf.GetColumn(1))) - 0.1) * 20) / 20;
                        }
                    }
                }

                ResidualACFPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(ResidualACFPlot);
        }

        /// <summary>
        /// Updates the PACF (Partial Autocorrelation Function) plot to check for partial correlation in residuals at different lags.
        /// </summary>
        private void UpdatePACFPlot()
        {
            if (ResidualPACFPlot == null) return;

            var pacfSeries = ResidualPACFPlot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "PartialAutocorrelation")
                ?? new HistogramSeries
                {
                    Name = "PartialAutocorrelation",
                    Title = "Partial Autocorrelation",
                    FillColor = Color.FromArgb(125, 104, 140, 175),
                    StrokeColor = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1,
                };
            var pacfLowerLine = ResidualPACFPlot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "LowerCI")
                ?? new LineAnnotation
                {
                    Name = "LowerCI",
                    Text = "2.5% CI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Top,
                };
            var pacfUpperLine = ResidualPACFPlot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "UpperCI")
                ?? new LineAnnotation
                {
                    Name = "UpperCI",
                    Text = "97.5% CI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                };

            using (Element?.SuspendPlotBridges())
            {
                ResidualPACFPlot.Series.Clear();
                for (int i = ResidualPACFPlot.Annotations.Count - 1; i >= 0; i--)
                {
                    if (ResidualPACFPlot.Annotations[i].Name == "LowerCI" || ResidualPACFPlot.Annotations[i].Name == "UpperCI")
                    {
                        ResidualPACFPlot.Annotations.RemoveAt(i);
                    }
                }

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid == true && Element.BayesianAnalysis.IsEstimated == true)
                {
                    var pacfItems = new List<OxyPlot.Series.HistogramItem>();
                    var pacf = Autocorrelation.Function(_residuals, -1, Autocorrelation.Type.Partial);
                    for (int i = 0; i < pacf.GetLength(0); i++)
                    {
                        pacfItems.Add(new OxyPlot.Series.HistogramItem(i, i + 1, pacf[i, 1]));
                    }

                    pacfSeries.ItemsSource = pacfItems;
                    pacfSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:0.000000}";
                    ResidualPACFPlot.Series.Add(pacfSeries);

                    var ci = Autocorrelation.CorrelationConfidenceInterval(_residuals.Length);
                    pacfLowerLine.Y = ci[0];
                    pacfUpperLine.Y = ci[1];
                    ResidualPACFPlot.Annotations.Add(pacfLowerLine);
                    ResidualPACFPlot.Annotations.Add(pacfUpperLine);

                    foreach (var axis in ResidualPACFPlot.Axes)
                    {
                        if (axis.Key == "Yaxis")
                        {
                            axis.Maximum = Math.Round((Math.Max(ci[1], Tools.Max(pacf.GetColumn(1))) + 0.1) * 20) / 20;
                            axis.Minimum = Math.Round((Math.Min(ci[0], Tools.Min(pacf.GetColumn(1))) - 0.1) * 20) / 20;
                        }
                    }
                }

                ResidualPACFPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(ResidualPACFPlot);
        }

        #endregion

        /// <summary>
        /// Binds the time series data grid with the analysis results including predictions and credible intervals.
        /// </summary>
        private void BindTimeSeriesDataGrid()
        {
            TimeSeriesTable.ItemsSource = null;

            if (Element == null || Element.BayesianAnalysis == null || Element.BayesianAnalysis.IsEstimated != true || Element.AnalysisResults == null)  return;

            ModeColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

            if (Element.ARIMAX?.TimeSeries == null || Element.ARIMAX.TimeSeries.Count == 0) return;

            var dates = TimeSeriesResultTimeline.CreateDates(Element.ARIMAX.TimeSeries, Element.AnalysisResults.MeanCurve.Length);
            var curvePoints = new List<FrequencyCurvePoint>();
            for (int i = 0; i < Element.AnalysisResults.MeanCurve.Length; i++)
            {
                var lo = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                var up = Element.AnalysisResults.ConfidenceIntervals[i, 2];
                var prd = Element.AnalysisResults.MeanCurve[i];
                var md = i < Element.AnalysisResults.ModeCurve.Length ? Element.AnalysisResults.ModeCurve[i] : double.NaN;
                curvePoints.Add(new FrequencyCurvePoint(dates[i], up, lo, prd, md));
            }
            TimeSeriesTable.ItemsSource = curvePoints;
            TimeSeriesTable.Items.Refresh();
        }

        /// <summary>
        /// Sets the column headers for the time series table based on the credible interval width.
        /// </summary>
        private void SetTableColumnHeaders()
        {
            double alpha = (1 - Element.BayesianAnalysis.CredibleIntervalWidth) / 2;
            UpperColumn.Header = ((1 - alpha) * 100).ToString("F1") + "% CI";
            LowerColumn.Header = (alpha * 100).ToString("F1") + "% CI";
        }

        /// <summary>
        /// Sets the string format for numeric columns in the time series table based on user settings.
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
        /// Binds the summary statistics data grid with model parameters, goodness-of-fit metrics, and significance indicators.
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
                for (int i = 0; i < Element.ARIMAX.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.ARIMAX.Parameters[i].DisplayName, double.NaN));
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
                for (int i = 0; i < Element.ARIMAX.Parameters.Count; i++)
                {
                    string signif = "";
                    if (i < Element.ARIMAX.Parameters.Count - 1)
                    {
                        var x = Element.BayesianAnalysis.Results.Output.Select(set => set.Values[i]).ToArray();
                        var kde = new KernelDensity(x);
                        double p = kde.CCDF(0);
                        if (p > 0.999 || p < 0.001)
                        {
                            signif = "***";
                        }
                        else if (p > 0.99 || p < 0.01)
                        {
                            signif = "**";
                        }
                        else if (p > 0.95 || p < 0.05)
                        {
                            signif = "*";
                        }
                        else if (p > 0.9 || p < 0.1)
                        {
                            signif = ".";
                        }
                        else
                        {
                            signif = " ";
                        }
                    }
                    summaryStats.Add(new SummaryStatistic(Element.ARIMAX.Parameters[i].DisplayName, Element.ARIMAX.Parameters[i].Value, double.NaN, signif));
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
        /// Handles the LoadingRow event for the summary statistics table. Adds visual separators before certain rows.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the row being loaded.</param>
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
