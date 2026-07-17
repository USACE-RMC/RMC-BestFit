using FrameworkInterfaces;
using FrameworkUI;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using OxyPlot;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Numerics;
using Numerics.Data.Statistics;
using OxyPlotControls;
using Numerics.Sampling;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and managing distribution fitting analysis results.
    /// Provides visualization of fitted distributions through multiple plot types (frequency, PDF, CDF, P-P, Q-Q)
    /// and displays goodness-of-fit statistics in data grids.
    /// </summary>
    public partial class FittingAnalysisControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FittingAnalysisControl"/> class.
        /// Sets up color schemes and initializes the distribution column list for the data grid.
        /// </summary>
        public FittingAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
            _columns.Add(ExpColumn);
            _columns.Add(GammaColumn);
            _columns.Add(GeneralizedExtremeValueColumn);
            _columns.Add(GeneralizedLogisticColumn);
            _columns.Add(GeneralizedNormalColumn);
            _columns.Add(GeneralizedParetoColumn);
            _columns.Add(GumbelColumn);
            _columns.Add(KappaColumn);
            _columns.Add(LnNormalColumn);
            _columns.Add(LogisticColumn);
            _columns.Add(LogNormalColumn);
            _columns.Add(LogPearsonTypeIIIColumn);
            _columns.Add(NormalColumn);
            _columns.Add(PearsonTypeIIIColumn);
            _columns.Add(WeibullColumn);
        }

        #region Members

        /// <summary>
        /// Dependency property for the Element property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(FittingAnalysis), typeof(FittingAnalysisControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the fitting analysis element displayed by this control.
        /// </summary>
        public FittingAnalysis Element
        {
            get { return (FittingAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element property changes.
        /// Manages event handler subscriptions and plot attachment for the old and new element instances.
        /// </summary>
        /// <param name="d">The dependency object where the property changed.</param>
        /// <param name="e">Event data containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as FittingAnalysisControl == null) return;
            var thisControl = (FittingAnalysisControl)d;

            // Remove handlers and detach plots from old element
            if (e.OldValue is FittingAnalysis oldElement)
            {
                oldElement.PropertyChanged -= thisControl.ElementPropertyChanged;

                // Detach plots from hosts
                thisControl.FrequencyPlotHost.Content = null;
                thisControl.PDFPlotHost.Content = null;
                thisControl.CDFPlotHost.Content = null;
                thisControl.PPPlotHost.Content = null;
                thisControl.QQPlotHost.Content = null;

                // Clear toolbar
                thisControl.PlotToolbar.Plot = null;
                thisControl.PlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as FittingAnalysis;
            if (newElement == null) return;

            // Reset control-scoped caches so the next Loaded refresh rebuilds them
            thisControl._seriesCreated = false;
            thisControl._aicBicStylesCreated = false;
            thisControl._lastFilterIndex = -1;

            // Add handlers
            newElement.PropertyChanged += thisControl.ElementPropertyChanged;

            // Attach Element-owned plots to ContentControl hosts.
            // Suspend plot bridges to prevent spurious undo entries from WPF property change notifications.
            using (newElement.SuspendPlotBridges())
            {
                thisControl.FrequencyPlotHost.Content = newElement.FrequencyPlot;
                thisControl.PDFPlotHost.Content = newElement.PDFPlot;
                thisControl.CDFPlotHost.Content = newElement.CDFPlot;
                thisControl.PPPlotHost.Content = newElement.PPPlot;
                thisControl.QQPlotHost.Content = newElement.QQPlot;

                // Wire toolbar to the initially visible plot (Frequency)
                thisControl.PlotToolbar.Plot = newElement.FrequencyPlot;
                thisControl.PlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;

                // Bind axis titles to InputData.UnitLabel (must be inside suspension
                // to prevent "Change Title" undo entries from axis PropertyChanged)
                thisControl.BindAxisTitles();
            }
        }

        /// <summary>
        /// Indicates whether the frequency plot needs updating before display.
        /// </summary>
        private bool _frequencyPlotDirty = true;

        /// <summary>
        /// Indicates whether the PDF plot needs updating before display.
        /// </summary>
        private bool _pdfPlotDirty = true;

        /// <summary>
        /// Indicates whether the CDF plot needs updating before display.
        /// </summary>
        private bool _cdfPlotDirty = true;

        /// <summary>
        /// Indicates whether the P-P plot needs updating before display.
        /// </summary>
        private bool _ppPlotDirty = true;

        /// <summary>
        /// Indicates whether the Q-Q plot needs updating before display.
        /// </summary>
        private bool _qqPlotDirty = true;

        /// <summary>
        /// Indicates whether LineSeries objects have been created for the current filter set.
        /// </summary>
        private bool _seriesCreated = false;

        /// <summary>
        /// Indicates whether AIC/BIC/RMSE column header styles have been created.
        /// </summary>
        private bool _aicBicStylesCreated = false;

        /// <summary>
        /// Tracks the last filter combo box index to avoid redundant FilteredDistributions recreation.
        /// </summary>
        private int _lastFilterIndex = -1;

        /// <summary>
        /// Indicates whether all checkboxes are being checked or unchecked programmatically.
        /// </summary>
        private bool _checkingAll = false;

        /// <summary>
        /// Indicates whether a single checkbox is being modified programmatically.
        /// </summary>
        private bool _singleCheck = false;

        /// <summary>
        /// Gets a value indicating whether a plot area was clicked by the user.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Occurs when the preview control (plot or toolbar) is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Represents the method that handles the PreviewControlClicked event.
        /// </summary>
        /// <param name="plotClicked">True if the plot area was clicked.</param>
        /// <param name="toolbarClicked">True if the toolbar area was clicked.</param>
        /// <param name="plot">The plot that was clicked.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Raised when plot properties are requested to be displayed or modified.
        /// Re-raises the PropertiesCalled event from the toolbar so that MainProjectNode
        /// only needs to subscribe to a single event on the control.
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
        /// Gets the collection of fitted distributions filtered by the current selection criteria.
        /// </summary>
        public ObservableCollection<FittedDistribution> FilteredDistributions { get; private set;  }

        /// <summary>
        /// Collection of fitted distribution statistics for display in the summary data grid.
        /// </summary>
        private ObservableCollection<FittedDistributionStatistic> _fittedDistributionStatistics = new ObservableCollection<FittedDistributionStatistic>();

        /// <summary>
        /// List of data grid columns for each distribution type.
        /// </summary>
        private List<DataGridTextColumn> _columns = new List<DataGridTextColumn>();

        #region Plot Series

        /// <summary>
        /// Array of color hex codes used for plotting different distribution series.
        /// </summary>
        private string[] _colorHexCodes;

        /// <summary>
        /// List of line series for displaying distributions on the frequency plot.
        /// </summary>
        private List<LineSeries> _frequencyDistributionSeriesList = new List<LineSeries>();

        /// <summary>
        /// List of line series for displaying distributions on the PDF plot.
        /// </summary>
        private List<LineSeries> _pdfDistributionSeriesList = new List<LineSeries>();

        /// <summary>
        /// List of line series for displaying distributions on the CDF plot.
        /// </summary>
        private List<LineSeries> _cdfDistributionSeriesList = new List<LineSeries>();

        /// <summary>
        /// List of line series for displaying distributions on the P-P plot.
        /// </summary>
        private List<LineSeries> _ppDistributionSeriesList = new List<LineSeries>();

        /// <summary>
        /// List of line series for displaying distributions on the Q-Q plot.
        /// </summary>
        private List<LineSeries> _qqDistributionSeriesList = new List<LineSeries>();

        #endregion

        #endregion

        /// <summary>
        /// Handles the Loaded event of the user control.
        /// Initializes the control by updating distribution handlers, filtered distributions, data grids, and plots.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            // Data refresh on every load. Plot hosts, toolbars, and Element.PropertyChanged
            // are wired once per Element in ElementCallback.
            // UpdateFittedDistributionHandlers re-subscribes per-distribution PropertyChanged
            // handlers (control-scoped); it is internally idempotent via an unsub-then-sub
            // pattern so running it on every load is safe.
            UpdateFittedDistributionHandlers();
            UpdateFilteredDistributions();
            UpdateAICBICDataGrid();
            UpdateSummaryStatisticsDataGrid();

            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                CreateLineSeries();
                MarkAllPlotsDirty();
                UpdateCurrentPlot();
                UpdateSeriesVisibility();
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
        }

        /// <summary>
        /// Handles the Unloaded event of the user control.
        /// Unsubscribes per-distribution PropertyChanged handlers to prevent memory leaks.
        /// Does NOT reset _isLoaded — the data hasn't changed, so no rebuild is needed on re-show.
        /// Re-initialization is triggered by ElementCallback (new element) or ElementPropertyChanged (data change).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Pattern B (canonical): Element-scoped lifecycle (PropertyChanged, plot
            // hosts, toolbars) is owned by ElementCallback and survives Unload/Reload
            // cycles. Unsub the CONTROL-scoped per-distribution handlers here — Loaded
            // re-subscribes them via UpdateFittedDistributionHandlers() (idempotent).
            if (Element == null) return;

            if (Element.FittedDistributions != null)
            {
                for (int i = 0; i < Element.FittedDistributions.Count; i++)
                    Element.FittedDistributions[i].PropertyChanged -= DistributionPropertyChanged;
            }
        }

        /// <summary>
        /// Handles property changes on the Element object.
        /// Refreshes the control when the IsEstimated or InputData properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing the name of the property that changed.</param>
        private void ElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Element.IsEstimated))
            {
                // Distributions — estimation results changed, series and filters need recreation
                _seriesCreated = false;
                _lastFilterIndex = -1;
                UpdateFittedDistributionHandlers();
                UpdateFilteredDistributions();
                // Data grids
                UpdateAICBICDataGrid();
                UpdateSummaryStatisticsDataGrid();
                // Plots — wrap in SuspendPlotBridges since these are data operations
                UpdateAllPlotsAndSeries();
            }
            if (e.PropertyName == nameof(Element.ProbabilityOrdinates))
            {
                // Ordinates don't change the MLE fit itself, but the frequency plot
                // (quantile curves per fitted distribution) and the summary-statistics
                // table both iterate ProbabilityOrdinates to compute/display values.
                // Refresh them in place; keep fitted parameters untouched.
                if (Element?.IsEstimated == true)
                {
                    UpdateSummaryStatisticsDataGrid();
                    UpdateAllPlotsAndSeries();
                }
            }
            if (e.PropertyName == nameof(Element.InputData))
            {
                // Suspend plot bridges while rebinding axis titles to prevent
                // "Change Title" undo entries from axis PropertyChanged notifications.
                using (Element.SuspendPlotBridges())
                {
                    BindAxisTitles();
                }
                UpdateAllPlotsAndSeries();
            }
        }

        /// <summary>
        /// Handles the "Select All" checkbox checked event.
        /// Shows results for all filtered distributions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_singleCheck == true) return;
            if (Element == null || FilteredDistributions == null) return;
            _checkingAll = true;
            for (int i = 0; i < FilteredDistributions.Count; i++)
                FilteredDistributions[i].ShowResults = true;
            _checkingAll = false;
            UpdateSeriesVisibility();

        }

        /// <summary>
        /// Handles the "Select All" checkbox unchecked event.
        /// Hides results for all filtered distributions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_singleCheck == true) return;
            if (Element == null || FilteredDistributions == null) return;
            _checkingAll = true;
            for (int i = 0; i < FilteredDistributions.Count; i++)
                FilteredDistributions[i].ShowResults = false;
            _checkingAll = false;
            UpdateSeriesVisibility();
        }

        /// <summary>
        /// Handles the distribution type combo box selection changed event.
        /// Filters distributions by the selected number of parameters (2, 3, or 4 parameter distributions).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Distributions — filter changed, so series need recreation
            _seriesCreated = false;
            UpdateFilteredDistributions();
            // Data grids
            UpdateAICBICDataGrid();
            // Plots
            UpdateAllPlotsAndSeries();
        }

        /// <summary>
        /// Updates the filtered distributions collection based on the selected filter criteria.
        /// Filters by the number of distribution parameters (all, 2, 3, or 4 parameters).
        /// Skips recreation if the filter index hasn't changed and FilteredDistributions is already populated.
        /// </summary>
        private void UpdateFilteredDistributions()
        {
            if (Element == null || Element.FittedDistributions == null) return;

            int currentFilter = TypeComboBox.SelectedIndex;

            // Skip recreation if filter hasn't changed and collection already exists
            if (currentFilter == _lastFilterIndex && FilteredDistributions != null)
                return;

            _lastFilterIndex = currentFilter;

            if (currentFilter == 0)
            {
                FilteredDistributions = new ObservableCollection<FittedDistribution>(Element.FittedDistributions.ToList());
            }
            else if (currentFilter == 1)
            {
                FilteredDistributions = new ObservableCollection<FittedDistribution>(Element.FittedDistributions.Where(x => x.Distribution.NumberOfParameters == 2).ToList());
            }
            else if (currentFilter == 2)
            {
                FilteredDistributions = new ObservableCollection<FittedDistribution>(Element.FittedDistributions.Where(x => x.Distribution.NumberOfParameters == 3).ToList());
            }
            else if (currentFilter == 3)
            {
                FilteredDistributions = new ObservableCollection<FittedDistribution>(Element.FittedDistributions.Where(x => x.Distribution.NumberOfParameters == 4).ToList());
            }
            else
            {
                FilteredDistributions = null;
            }
        }

        /// <summary>
        /// Updates property change event handlers for all fitted distributions.
        /// Ensures each distribution has the DistributionPropertyChanged handler attached.
        /// </summary>
        private void UpdateFittedDistributionHandlers()
        {
            if (Element == null || Element.FittedDistributions == null) return;
            for (int i = 0; i < Element.FittedDistributions.Count; i++)
            {
                Element.FittedDistributions[i].PropertyChanged -= DistributionPropertyChanged;
                Element.FittedDistributions[i].PropertyChanged += DistributionPropertyChanged;
            }
        }

        /// <summary>
        /// Handles property changes on individual fitted distributions.
        /// Updates the "Select All" checkbox state when individual distributions are shown or hidden.
        /// </summary>
        /// <param name="sender">The fitted distribution that changed.</param>
        /// <param name="e">Event data containing the name of the property that changed.</param>
        private void DistributionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FittedDistribution.ShowResults))
            {
                if (_checkingAll == true) return;
                _singleCheck = true;
                if (FilteredDistributions.Where(x => x.ShowResults == true).Count() == FilteredDistributions.Count())
                {
                    SelectAllCheckBox.IsChecked = true;
                }
                else
                {
                    SelectAllCheckBox.IsChecked = false;
                }
                UpdateSeriesVisibility();
                _singleCheck = false;
            }
        }

        #region Plots

        /// <summary>
        /// Gets the currently selected plot based on which radio button is checked.
        /// Returns the Element-owned plot object.
        /// </summary>
        /// <returns>The currently active plot, or null if no plot is selected.</returns>
        public Plot GetCurrentPlot()
        {
            if (FrequencyPlotRadioButton.IsChecked == true) return Element?.FrequencyPlot;
            if (PDFPlotRadioButton.IsChecked == true) return Element?.PDFPlot;
            if (CDFPlotRadioButton.IsChecked == true) return Element?.CDFPlot;
            if (PPPlotRadioButton.IsChecked == true) return Element?.PPPlot;
            if (QQPlotRadioButton.IsChecked == true) return Element?.QQPlot;
            return null;
        }

        /// <summary>
        /// Gets the toolbar for the currently selected plot. FittingAnalysis uses a single shared toolbar.
        /// </summary>
        /// <returns>The plot toolbar.</returns>
        public OxyPlotToolbar GetCurrentPlotToolbar()
        {
            return PlotToolbar;
        }

        /// <summary>
        /// Handles PropertiesCalled from the toolbar and re-raises it as the control-level PlotPropertiesCalled event.
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
        /// Handles the PreviewMouseDown event of the user control.
        /// Determines which plot area or toolbar was clicked and raises the PreviewControlClicked event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event data.</param>
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Plot plot = GetCurrentPlot();
            OxyPlotToolbar toolbar = GetCurrentPlotToolbar();

            if (plot == null || toolbar == null)
            {
                PlotClicked = false;
                return;
            }

            var plotHitResult = VisualTreeHelper.HitTest(plot, e.GetPosition(plot));
            var toolbarHitResult = VisualTreeHelper.HitTest(toolbar, e.GetPosition(toolbar));
            PreviewControlClicked?.Invoke(plotHitResult != null, toolbarHitResult != null, plot);
            PlotClicked = plotHitResult != null;
        }

        /// <summary>
        /// Handles the LostFocus event of the user control.
        /// Resets the PlotClicked property when the control loses focus.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Binds axis titles on Element-owned plots to the InputData.UnitLabel property.
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

            // PDF plot: X-axis title = UnitLabel
            var pdfXAxis = Element.PDFPlot?.Axes.FirstOrDefault(a => a.Key == "Xaxis");
            if (pdfXAxis != null)
            {
                if (Element.InputData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(pdfXAxis, Element.InputData, nameof(InputData.UnitLabel), Element.InputData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(pdfXAxis, string.Empty);
            }

            // CDF plot: X-axis title = UnitLabel
            var cdfXAxis = Element.CDFPlot?.Axes.FirstOrDefault(a => a.Key == "Xaxis");
            if (cdfXAxis != null)
            {
                if (Element.InputData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(cdfXAxis, Element.InputData, nameof(InputData.UnitLabel), Element.InputData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(cdfXAxis, string.Empty);
            }
        }

        /// <summary>
        /// Updates all plots and series with current data, wrapping in SuspendPlotBridges
        /// since these are data operations (not user visual edits).
        /// Only the currently visible plot is updated immediately; others are deferred.
        /// </summary>
        private void UpdateAllPlotsAndSeries()
        {
            if (Element == null) return;
            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                CreateLineSeries();
                MarkAllPlotsDirty();
                UpdateCurrentPlot();
                UpdateSeriesVisibility();
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
        }

        /// <summary>
        /// Creates line series for all filtered distributions across all plot types.
        /// Each distribution is assigned a unique color for consistent visualization across plots.
        /// Series are only recreated when the filter set changes (tracked by _seriesCreated flag).
        /// </summary>
        private void CreateLineSeries()
        {
            // Skip recreation if series already exist for the current filter set
            if (_seriesCreated && _frequencyDistributionSeriesList.Count == (FilteredDistributions?.Count ?? 0))
                return;

            _frequencyDistributionSeriesList.Clear();
            _pdfDistributionSeriesList.Clear();
            _cdfDistributionSeriesList.Clear();
            _ppDistributionSeriesList.Clear();
            _qqDistributionSeriesList.Clear();

            if (Element == null || Element.IsValid == false || FilteredDistributions == null) return;


            for (int i = 0; i < FilteredDistributions.Count; i++)
            {

                Color seriesColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[i]);

                // Frequency Plot
                _frequencyDistributionSeriesList.Add(new LineSeries()
                {
                    Name = FilteredDistributions[i].Distribution.Type.ToString(),
                    Title = FilteredDistributions[i].Distribution.DisplayName,
                    TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}",
                    StrokeThickness = 1.5,
                    BrokenLineStyle = LineStyle.Solid,
                    Color = seriesColor,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0
                });

                // PDF Plot
                _pdfDistributionSeriesList.Add(new LineSeries()
                {
                    Name = FilteredDistributions[i].Distribution.Type.ToString(),
                    Title = FilteredDistributions[i].Distribution.DisplayName,
                    TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}",
                    StrokeThickness = 1.5,
                    BrokenLineStyle = LineStyle.Solid,
                    Color = seriesColor,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0
                });

                // CDF Plot
                _cdfDistributionSeriesList.Add(new LineSeries()
                {
                    Name = FilteredDistributions[i].Distribution.Type.ToString(),
                    Title = FilteredDistributions[i].Distribution.DisplayName,
                    TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}",
                    StrokeThickness = 1.5,
                    BrokenLineStyle = LineStyle.Solid,
                    Color = seriesColor,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0
                });

                // PP Plot
                _ppDistributionSeriesList.Add(new LineSeries()
                {
                    Name = FilteredDistributions[i].Distribution.Type.ToString(),
                    Title = FilteredDistributions[i].Distribution.DisplayName,
                    TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:0.000000000}",
                    StrokeThickness = 1.5,
                    BrokenLineStyle = LineStyle.Solid,
                    Color = seriesColor,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0
                });

                // QQ Plot
                _qqDistributionSeriesList.Add(new LineSeries()
                {
                    Name = FilteredDistributions[i].Distribution.Type.ToString(),
                    Title = FilteredDistributions[i].Distribution.DisplayName,
                    TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}",
                    StrokeThickness = 1.5,
                    BrokenLineStyle = LineStyle.Solid,
                    Color = seriesColor,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0
                });
            }

            _seriesCreated = true;
        }

        /// <summary>
        /// Updates the visibility of distribution series on all plots based on the ShowResults property of each distribution.
        /// Also manages the visibility and width of data grid columns.
        /// </summary>
        private void UpdateSeriesVisibility()
        {

            if (Element == null || Element.IsValid == false || FilteredDistributions == null) return;



            for (int i = 0; i < FilteredDistributions.Count; i++)
            {
                if (FilteredDistributions[i].ShowResults == true)
                {
                    _frequencyDistributionSeriesList[i].Visibility = Visibility.Visible;
                    _pdfDistributionSeriesList[i].Visibility = Visibility.Visible;
                    _cdfDistributionSeriesList[i].Visibility = Visibility.Visible;
                    _ppDistributionSeriesList[i].Visibility = Visibility.Visible;
                    _qqDistributionSeriesList[i].Visibility = Visibility.Visible;
                }
                else
                {
                    _frequencyDistributionSeriesList[i].Visibility = Visibility.Hidden;
                    _pdfDistributionSeriesList[i].Visibility = Visibility.Hidden;
                    _cdfDistributionSeriesList[i].Visibility = Visibility.Hidden;
                    _ppDistributionSeriesList[i].Visibility = Visibility.Hidden;
                    _qqDistributionSeriesList[i].Visibility = Visibility.Hidden;
                }
            }

            double visCount = Math.Max(1, FilteredDistributions.Where(x => x.ShowResults == true).Count());

            // Build a HashSet for O(1) Contains checks instead of O(n) on ObservableCollection
            var filteredSet = new HashSet<FittedDistribution>(FilteredDistributions);

            for (int i = 0; i < Element.FittedDistributions.Count; i++)
            {
                if (Element.FittedDistributions[i].ShowResults == true && filteredSet.Contains(Element.FittedDistributions[i]))
                {
                    _columns[i].Visibility = Visibility.Visible;
                    _columns[i].Width = new DataGridLength((TabControl.ActualWidth - 120) / visCount);
                }
                else
                {
                    _columns[i].Visibility = Visibility.Hidden;
                }
            }
            // Column visibility/width changes are handled by WPF automatically — no Items.Refresh() needed
        }

        /// <summary>
        /// Marks all plot dirty flags so they will be refreshed on next view.
        /// </summary>
        private void MarkAllPlotsDirty()
        {
            _frequencyPlotDirty = true;
            _pdfPlotDirty = true;
            _cdfPlotDirty = true;
            _ppPlotDirty = true;
            _qqPlotDirty = true;
        }

        /// <summary>
        /// Updates only the currently visible plot. Other plots are marked dirty
        /// and will be updated lazily when the user switches to them.
        /// </summary>
        private void UpdateCurrentPlot()
        {
            if (FrequencyPlotRadioButton.IsChecked == true) { UpdateDirtyPlotWithWaitCursor(UpdateFrequencyPlot, () => _frequencyPlotDirty = false); }
            else if (PDFPlotRadioButton.IsChecked == true) { UpdateDirtyPlotWithWaitCursor(UpdatePDFPlot, () => _pdfPlotDirty = false); }
            else if (CDFPlotRadioButton.IsChecked == true) { UpdateDirtyPlotWithWaitCursor(UpdateCDFPlot, () => _cdfPlotDirty = false); }
            else if (PPPlotRadioButton.IsChecked == true) { UpdateDirtyPlotWithWaitCursor(UpdatePPPlot, () => _ppPlotDirty = false); }
            else if (QQPlotRadioButton.IsChecked == true) { UpdateDirtyPlotWithWaitCursor(UpdateQQPlot, () => _qqPlotDirty = false); }
        }

        /// <summary>
        /// Runs a dirty plot update with visible wait-cursor feedback and clears its dirty flag afterward.
        /// </summary>
        /// <param name="updatePlotAction">The plot update action to run.</param>
        /// <param name="markCleanAction">The action that clears the corresponding dirty flag.</param>
        /// <remarks>
        /// Fitted distribution plots can be expensive for large data sets and many selected
        /// distributions, so dirty redraws use the same wait-cursor feedback as long analyses.
        /// </remarks>
        private void UpdateDirtyPlotWithWaitCursor(Action updatePlotAction, Action markCleanAction)
        {
            WaitCursorHelper.RunWithVisibleWaitCursor(Dispatcher, () =>
            {
                updatePlotAction();
                markCleanAction();
            });
        }

        #region Frequency Plot

        /// <summary>
        /// Handles the Checked event of the frequency plot radio button.
        /// Displays the frequency plot and hides all other plots.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void FrequencyPlotRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            FrequencyPlotHost.Visibility = Visibility.Visible;
            PDFPlotHost.Visibility = Visibility.Hidden;
            CDFPlotHost.Visibility = Visibility.Hidden;
            PPPlotHost.Visibility = Visibility.Hidden;
            QQPlotHost.Visibility = Visibility.Hidden;
            //
            PlotToolbar.Plot = Element?.FrequencyPlot;
            if (_frequencyPlotDirty) { UpdateDirtyPlotWithWaitCursor(UpdateFrequencyPlot, () => _frequencyPlotDirty = false); }
            else Element?.FrequencyPlot?.InvalidatePlot(true);
        }

        /// <summary>
        /// Updates the frequency plot with distribution curves and data points.
        /// The frequency plot displays exceedance probability (x-axis) versus values (y-axis).
        /// </summary>
        private void UpdateFrequencyPlot()
        {
            if (Element?.FrequencyPlot == null) return;
            var plot = Element.FrequencyPlot;

            // Resolve named series via lookup-or-create so user-customized styling survives Save/Open.
            var exactData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
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
            var lowOutlierData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Cross,
                };
            var uncertainData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "UncertainData")
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
            var intervalData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "IntervalData")
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

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                if (Element == null || Element.InputData == null || Element.IsValid == false || FilteredDistributions == null)
                {
                    plot.InvalidatePlot(true);
                }
                else
                {
                    // Add Curves
                    if (Element.IsEstimated == true)
                    {
                        for (int i = 0; i < FilteredDistributions.Count; i++)
                        {
                            if (FilteredDistributions[i].FitSucceeded == true)
                            {
                                // Load curve data
                                var data = new List<DataPoint>();
                                for (int j = 0; j < Element.ProbabilityOrdinates.Count; j++)
                                {
                                    var x = Element.ProbabilityOrdinates[j];
                                    var y = FilteredDistributions[i].Distribution.InverseCDF(1 - x);
                                    data.Add(new DataPoint(x, y));
                                }
                                _frequencyDistributionSeriesList[i].ItemsSource = data;
                                if (FilteredDistributions[i].ShowResults == true)
                                {
                                    _frequencyDistributionSeriesList[i].Visibility = Visibility.Visible;
                                }
                                else
                                {
                                    _frequencyDistributionSeriesList[i].Visibility = Visibility.Hidden;
                                }
                                plot.Series.Add(_frequencyDistributionSeriesList[i]);
                            }
                            else
                            {
                                _frequencyDistributionSeriesList[i].ItemsSource = null;
                                _frequencyDistributionSeriesList[i].Visibility = Visibility.Hidden;
                            }
                        }
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
                    exactData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                    exactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                    exactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.ExactSeries.Count > 0) plot.Series.Add(exactData);

                    // low outliers data
                    lowOutlierData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                    lowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                    lowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.NumberOfLowOutliers > 0) plot.Series.Add(lowOutlierData);

                    // uncertain data
                    uncertainData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.UncertainSeries : Element.InputData.DataFrame.UncertainSeries.Where(x => x.Value > 1E-16);
                    uncertainData.DataFieldX = nameof(UncertainData.PlottingPosition);
                    uncertainData.DataFieldY = nameof(UncertainData.Value);
                    uncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                    uncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                    uncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.UncertainSeries.Count > 0) plot.Series.Add(uncertainData);

                    // interval data
                    intervalData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.IntervalSeries : Element.InputData.DataFrame.IntervalSeries.Where(x => x.Value > 1E-16);
                    intervalData.DataFieldX = nameof(IntervalData.PlottingPosition);
                    intervalData.DataFieldY = nameof(IntervalData.Value);
                    intervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                    intervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                    intervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                    if (Element.InputData.DataFrame.IntervalSeries.Count > 0) plot.Series.Add(intervalData);

                    plot.InvalidatePlot(true);
                }
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

        #region PDF Plot

        /// <summary>
        /// Handles the Checked event of the PDF plot radio button.
        /// Displays the PDF plot and hides all other plots.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void PDFPlotRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            FrequencyPlotHost.Visibility = Visibility.Hidden;
            PDFPlotHost.Visibility = Visibility.Visible;
            CDFPlotHost.Visibility = Visibility.Hidden;
            PPPlotHost.Visibility = Visibility.Hidden;
            QQPlotHost.Visibility = Visibility.Hidden;
            //
            PlotToolbar.Plot = Element?.PDFPlot;
            if (_pdfPlotDirty) { UpdateDirtyPlotWithWaitCursor(UpdatePDFPlot, () => _pdfPlotDirty = false); }
            else Element?.PDFPlot?.InvalidatePlot(true);
        }

        /// <summary>
        /// Updates the PDF (Probability Density Function) plot with distribution curves and a histogram of the input data.
        /// </summary>
        private void UpdatePDFPlot()
        {
            if (Element?.PDFPlot == null) return;
            var plot = Element.PDFPlot;

            var histogramSeries = plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "Histogram")
                ?? new HistogramSeries
                {
                    Name = "Histogram",
                    Title = "Input Data Histogram",
                    FillColor = Color.FromArgb(100, 176, 224, 230),
                    StrokeThickness = 1,
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                if (Element != null && Element.InputData != null && Element.IsValid != false && FilteredDistributions != null)
                {
                    // Create histogram
                    var xValues = Element.InputData.DataFrame.ExactSeries.ValuesToList();
                    xValues.AddRange(Element.InputData.DataFrame.UncertainSeries.ValuesToList());
                    xValues.AddRange(Element.InputData.DataFrame.IntervalSeries.ValuesToList());
                    // Use Sturges' rule
                    int k = (int)(1 + 3.322 * Math.Log(xValues.Count));
                    var histogram = new Histogram(xValues, k);
                    var histogramItems = new List<OxyPlot.Series.HistogramItem>();
                    double sum = 0;
                    for (int i = 0; i < histogram.NumberOfBins; i++)
                    {
                        sum += histogram[i].Frequency * histogram.BinWidth;
                    }
                    for (int i = 0; i < histogram.NumberOfBins; i++)
                    {
                        histogramItems.Add(new OxyPlot.Series.HistogramItem(histogram[i].LowerBound, histogram[i].UpperBound, histogram[i].Frequency / sum * histogram.BinWidth));
                    }
                    histogramSeries.ItemsSource = histogramItems;
                    histogramSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}";
                    plot.Series.Add(histogramSeries);

                    // Add Curves
                    if (Element.IsEstimated == true)
                    {
                        // Precompute x-value range once — same for all distributions
                        double minX = Math.Min(Math.Min(Element.InputData.DataFrame.ExactSeries.MinimumValue(), Element.InputData.DataFrame.UncertainSeries.MinimumValue()), Element.InputData.DataFrame.IntervalSeries.MinimumValue());
                        double maxX = Math.Max(Math.Max(Element.InputData.DataFrame.ExactSeries.MaximumValue(), Element.InputData.DataFrame.UncertainSeries.MaximumValue()), Element.InputData.DataFrame.IntervalSeries.MaximumValue());
                        double startX = minX - Math.Pow(10, (int)Math.Floor(Math.Log10(minX)));
                        double endX = maxX + Math.Pow(10, (int)Math.Floor(Math.Log10(maxX)));
                        var pdfXValues = Stratify.XValues(new StratificationOptions(startX, endX, 1000));

                        for (int i = 0; i < FilteredDistributions.Count; i++)
                        {
                            if (FilteredDistributions[i].FitSucceeded == true)
                            {
                                // Load curve data using precomputed x-values
                                var pdf = FilteredDistributions[i].Distribution.CreatePDFGraph(pdfXValues);
                                var data = new List<DataPoint>();
                                for (int j = 0; j < pdf.GetLength(0); j++)
                                {
                                    var x = pdf[j, 0];
                                    var y = pdf[j, 1];
                                    data.Add(new DataPoint(x, y));
                                }
                                _pdfDistributionSeriesList[i].ItemsSource = data;
                                if (FilteredDistributions[i].ShowResults == true)
                                {
                                    _pdfDistributionSeriesList[i].Visibility = Visibility.Visible;
                                }
                                else
                                {
                                    _pdfDistributionSeriesList[i].Visibility = Visibility.Hidden;
                                }
                                plot.Series.Add(_pdfDistributionSeriesList[i]);
                            }
                            else
                            {
                                _pdfDistributionSeriesList[i].ItemsSource = null;
                                _pdfDistributionSeriesList[i].Visibility = Visibility.Hidden;
                            }
                        }
                    }
                }

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

        #region CDF Plot

        /// <summary>
        /// Handles the Checked event of the CDF plot radio button.
        /// Displays the CDF plot and hides all other plots.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void CDFPlotRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            FrequencyPlotHost.Visibility = Visibility.Hidden;
            PDFPlotHost.Visibility = Visibility.Hidden;
            CDFPlotHost.Visibility = Visibility.Visible;
            PPPlotHost.Visibility = Visibility.Hidden;
            QQPlotHost.Visibility = Visibility.Hidden;
            //
            PlotToolbar.Plot = Element?.CDFPlot;
            if (_cdfPlotDirty) { UpdateDirtyPlotWithWaitCursor(UpdateCDFPlot, () => _cdfPlotDirty = false); }
            else Element?.CDFPlot?.InvalidatePlot(true);
        }

        /// <summary>
        /// Updates the CDF (Cumulative Distribution Function) plot with distribution curves and data points.
        /// </summary>
        private void UpdateCDFPlot()
        {
            if (Element?.CDFPlot == null) return;
            var plot = Element.CDFPlot;

            var exactData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
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
            var lowOutlierData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Cross,
                };
            var uncertainData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "UncertainData")
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
            var intervalData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "IntervalData")
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

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                if (Element != null && Element.InputData != null && Element.IsValid != false && FilteredDistributions != null)
                {
                    // Add Curves
                    if (Element.IsEstimated == true)
                    {
                        // Precompute x-value range once — same for all distributions
                        double minX = Math.Min(Math.Min(Element.InputData.DataFrame.ExactSeries.MinimumValue(), Element.InputData.DataFrame.UncertainSeries.MinimumValue()), Element.InputData.DataFrame.IntervalSeries.MinimumValue());
                        double maxX = Math.Max(Math.Max(Element.InputData.DataFrame.ExactSeries.MaximumValue(), Element.InputData.DataFrame.UncertainSeries.MaximumValue()), Element.InputData.DataFrame.IntervalSeries.MaximumValue());
                        double startX = minX - Math.Pow(10, (int)Math.Floor(Math.Log10(minX)));
                        double endX = maxX + Math.Pow(10, (int)Math.Floor(Math.Log10(maxX)));
                        var cdfXValues = Stratify.XValues(new StratificationOptions(startX, endX, 1000));

                        for (int i = 0; i < FilteredDistributions.Count; i++)
                        {
                            if (FilteredDistributions[i].FitSucceeded == true)
                            {
                                // Load curve data using precomputed x-values
                                var cdf = FilteredDistributions[i].Distribution.CreateCDFGraph(cdfXValues);
                                var data = new List<DataPoint>();
                                for (int j = 0; j < cdf.GetLength(0); j++)
                                {
                                    var x = cdf[j, 0];
                                    var y = cdf[j, 1];
                                    data.Add(new DataPoint(x, y));
                                }
                                _cdfDistributionSeriesList[i].ItemsSource = data;
                                if (FilteredDistributions[i].ShowResults == true)
                                {
                                    _cdfDistributionSeriesList[i].Visibility = Visibility.Visible;
                                }
                                else
                                {
                                    _cdfDistributionSeriesList[i].Visibility = Visibility.Hidden;
                                }
                                plot.Series.Add(_cdfDistributionSeriesList[i]);
                            }
                            else
                            {
                                _cdfDistributionSeriesList[i].ItemsSource = null;
                                _cdfDistributionSeriesList[i].Visibility = Visibility.Hidden;
                            }
                        }
                    }

                    // Exact data
                    exactData.ItemsSource = Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                    exactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.Value, d.PlottingPositionComplement); };
                    exactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}";
                    if (Element.InputData.DataFrame.ExactSeries.Count > 0) plot.Series.Add(exactData);

                    // low outliers data
                    lowOutlierData.ItemsSource = Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                    lowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.Value, d.PlottingPositionComplement); };
                    lowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}";
                    if (Element.InputData.DataFrame.NumberOfLowOutliers > 0) plot.Series.Add(lowOutlierData);

                    // uncertain data
                    uncertainData.ItemsSource = Element.InputData.DataFrame.UncertainSeries;
                    uncertainData.DataFieldX = nameof(UncertainData.Value);
                    uncertainData.DataFieldY = nameof(UncertainData.PlottingPositionComplement);
                    uncertainData.DataFieldLowerErrorX = nameof(UncertainData.LowerValue);
                    uncertainData.DataFieldUpperErrorX = nameof(UncertainData.UpperValue);
                    uncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}";
                    if (Element.InputData.DataFrame.UncertainSeries.Count > 0) plot.Series.Add(uncertainData);

                    // interval data
                    intervalData.ItemsSource = Element.InputData.DataFrame.IntervalSeries;
                    intervalData.DataFieldX = nameof(IntervalData.Value);
                    intervalData.DataFieldY = nameof(IntervalData.PlottingPositionComplement);
                    intervalData.DataFieldLowerErrorX = nameof(IntervalData.LowerValue);
                    intervalData.DataFieldUpperErrorX = nameof(IntervalData.UpperValue);
                    intervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}";
                    if (Element.InputData.DataFrame.IntervalSeries.Count > 0) plot.Series.Add(intervalData);
                }

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

        #region P-P Plot

        /// <summary>
        /// Handles the Checked event of the P-P plot radio button.
        /// Displays the P-P plot and hides all other plots.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void PPPlotRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            FrequencyPlotHost.Visibility = Visibility.Hidden;
            PDFPlotHost.Visibility = Visibility.Hidden;
            CDFPlotHost.Visibility = Visibility.Hidden;
            PPPlotHost.Visibility = Visibility.Visible;
            QQPlotHost.Visibility = Visibility.Hidden;
            //
            PlotToolbar.Plot = Element?.PPPlot;
            if (_ppPlotDirty) { UpdateDirtyPlotWithWaitCursor(UpdatePPPlot, () => _ppPlotDirty = false); }
            else Element?.PPPlot?.InvalidatePlot(true);
        }

        /// <summary>
        /// Updates the P-P (Probability-Probability) plot with distribution curves.
        /// Compares theoretical probabilities against empirical probabilities to assess goodness-of-fit.
        /// </summary>
        private void UpdatePPPlot()
        {
            if (Element?.PPPlot == null) return;
            var plot = Element.PPPlot;

            var oneToOneLine = plot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "OneToOneLine")
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

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (Element != null && Element.InputData != null && Element.IsValid != false && FilteredDistributions != null)
                {
                    // Load percentiles
                    var xValues = Element.InputData.DataFrame.ExactSeries.Select(x => x.Value).ToList();
                    xValues.AddRange(Element.InputData.DataFrame.UncertainSeries.Select(x => x.Value).ToList());
                    xValues.AddRange(Element.InputData.DataFrame.IntervalSeries.Select(x => x.Value).ToList());
                    xValues.Sort();
                    var pValues = Element.InputData.DataFrame.ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
                    pValues.AddRange(Element.InputData.DataFrame.UncertainSeries.Select(x => x.PlottingPositionComplement).ToList());
                    pValues.AddRange(Element.InputData.DataFrame.IntervalSeries.Select(x => x.PlottingPositionComplement).ToList());
                    pValues.Sort();

                    // Add Curves
                    if (Element.IsEstimated == true)
                    {
                        for (int i = 0; i < FilteredDistributions.Count; i++)
                        {
                            if (FilteredDistributions[i].FitSucceeded == true)
                            {
                                // Load curve data
                                var data = new List<DataPoint>();
                                for (int j = 0; j < xValues.Count; j++)
                                {
                                    var x = FilteredDistributions[i].Distribution.CDF(xValues[j]);
                                    var y = pValues[j];
                                    data.Add(new DataPoint(x, y));
                                }
                                _ppDistributionSeriesList[i].ItemsSource = data;
                                if (FilteredDistributions[i].ShowResults == true)
                                {
                                    _ppDistributionSeriesList[i].Visibility = Visibility.Visible;
                                }
                                else
                                {
                                    _ppDistributionSeriesList[i].Visibility = Visibility.Hidden;
                                }
                                plot.Series.Add(_ppDistributionSeriesList[i]);
                            }
                            else
                            {
                                _ppDistributionSeriesList[i].ItemsSource = null;
                                _ppDistributionSeriesList[i].Visibility = Visibility.Hidden;
                            }
                        }
                    }

                    plot.Annotations.Add(oneToOneLine);
                }

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

        #region Q-Q Plot

        /// <summary>
        /// Handles the Checked event of the Q-Q plot radio button.
        /// Displays the Q-Q plot and hides all other plots.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void QQPlotRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            FrequencyPlotHost.Visibility = Visibility.Hidden;
            PDFPlotHost.Visibility = Visibility.Hidden;
            CDFPlotHost.Visibility = Visibility.Hidden;
            PPPlotHost.Visibility = Visibility.Hidden;
            QQPlotHost.Visibility = Visibility.Visible;
            //
            PlotToolbar.Plot = Element?.QQPlot;
            if (_qqPlotDirty) { UpdateDirtyPlotWithWaitCursor(UpdateQQPlot, () => _qqPlotDirty = false); }
            else Element?.QQPlot?.InvalidatePlot(true);
        }

        /// <summary>
        /// Updates the Q-Q (Quantile-Quantile) plot with distribution curves.
        /// Compares theoretical quantiles against empirical quantiles to assess goodness-of-fit.
        /// </summary>
        private void UpdateQQPlot()
        {
            if (Element?.QQPlot == null) return;
            var plot = Element.QQPlot;

            var oneToOneLine = plot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "OneToOneLine")
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

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (Element != null && Element.InputData != null && Element.IsValid != false && FilteredDistributions != null)
                {
                    // Load percentiles
                    var xValues = Element.InputData.DataFrame.ExactSeries.Select(x => x.Value).ToList();
                    xValues.AddRange(Element.InputData.DataFrame.UncertainSeries.Select(x => x.Value).ToList());
                    xValues.AddRange(Element.InputData.DataFrame.IntervalSeries.Select(x => x.Value).ToList());
                    xValues.Sort();
                    var pValues = Element.InputData.DataFrame.ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
                    pValues.AddRange(Element.InputData.DataFrame.UncertainSeries.Select(x => x.PlottingPositionComplement).ToList());
                    pValues.AddRange(Element.InputData.DataFrame.IntervalSeries.Select(x => x.PlottingPositionComplement).ToList());
                    pValues.Sort();

                    // Add Curves
                    if (Element.IsEstimated == true)
                    {
                        for (int i = 0; i < FilteredDistributions.Count; i++)
                        {
                            if (FilteredDistributions[i].FitSucceeded == true)
                            {
                                // Load curve data
                                var data = new List<DataPoint>();
                                for (int j = 0; j < xValues.Count; j++)
                                {
                                    var x = xValues[j];
                                    var y = FilteredDistributions[i].Distribution.InverseCDF(pValues[j]);
                                    data.Add(new DataPoint(x, y));
                                }
                                _qqDistributionSeriesList[i].ItemsSource = data;
                                if (FilteredDistributions[i].ShowResults == true)
                                {
                                    _qqDistributionSeriesList[i].Visibility = Visibility.Visible;
                                }
                                else
                                {
                                    _qqDistributionSeriesList[i].Visibility = Visibility.Hidden;
                                }
                                plot.Series.Add(_qqDistributionSeriesList[i]);
                            }
                            else
                            {
                                _qqDistributionSeriesList[i].ItemsSource = null;
                                _qqDistributionSeriesList[i].Visibility = Visibility.Hidden;
                            }
                        }
                    }

                    plot.Annotations.Add(oneToOneLine);
                }

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

        #endregion

        #region Data Grids

        /// <summary>
        /// Updates the AIC/BIC data grid with goodness-of-fit statistics for filtered distributions.
        /// Column header tooltip styles are created only once on first call.
        /// </summary>
        private void UpdateAICBICDataGrid()
        {
            if (AICBICTable == null || FilteredDistributions == null) return;
            AICBICTable.ItemsSource = null;
            AICBICTable.ItemsSource = FilteredDistributions;

            // Create column header tooltip styles only once — they never change
            if (!_aicBicStylesCreated)
            {
                // AIC Column Header
                var aicStyle = new Style(typeof(DataGridColumnHeader), AICColumn.HeaderStyle);
                aicStyle.Setters.Add(new Setter(ToolTipProperty, new TextBlock() { Text = "Akaike Information Criteria (AIC). The smaller the value the better the fit. Left or right click to sort.", FontWeight = System.Windows.FontWeights.Normal, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap }));
                AICColumn.HeaderStyle = aicStyle;
                //
                // BIC Column Header
                var bicStyle = new Style(typeof(DataGridColumnHeader), BICColumn.HeaderStyle);
                bicStyle.Setters.Add(new Setter(ToolTipProperty, new TextBlock() { Text = "Bayesian Information Criteria (BIC). The smaller the value the better the fit. Left or right click to sort.", FontWeight = System.Windows.FontWeights.Normal, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap }));
                BICColumn.HeaderStyle = bicStyle;
                //
                // RMSE Column Header
                var rmseStyle = new Style(typeof(DataGridColumnHeader), RMSEColumn.HeaderStyle);
                rmseStyle.Setters.Add(new Setter(ToolTipProperty, new TextBlock() { Text = "Root Mean Square Error (RMSE). The smaller the value the better the fit. Left or right click to sort.", FontWeight = System.Windows.FontWeights.Normal, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap }));
                RMSEColumn.HeaderStyle = rmseStyle;

                _aicBicStylesCreated = true;
            }

            AICBICTable.Items.Refresh();
        }

        /// <summary>
        /// Updates the summary statistics data grid with distribution parameters and statistical measures.
        /// Displays location, scale, shape parameters, summary statistics, and probability quantiles for each distribution.
        /// </summary>
        /// <remarks>
        /// Always rebuilds the row list from scratch. Row names encode the probability ordinate
        /// values, and cell bindings to <c>Value[i]</c> (an array indexer) don't fire
        /// <see cref="System.ComponentModel.INotifyPropertyChanged"/>, so in-place mutation of
        /// <see cref="FittedDistributionStatistic.Value"/> is invisible to the DataGrid. Forcing a
        /// full rebind on every call — via <c>ItemsSource = null</c> then re-assigning — is the
        /// only way to guarantee the table reflects the current ordinates and fitted parameters.
        /// Call frequency is low (post-estimation and per user ordinate edit) so the rebuild cost
        /// is negligible.
        /// </remarks>
        private void UpdateSummaryStatisticsDataGrid()
        {
            if (SummaryStatisticsDataGrid == null) return;

            // Detach current binding so WPF releases its view on the old list.
            SummaryStatisticsDataGrid.ItemsSource = null;

            // Build the row list from scratch: 10 fixed rows + one per ordinate (names encode the
            // ordinate value, so they must be rebuilt on every ordinate change).
            _fittedDistributionStatistics.Clear();
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Location", new double[15], new string[15]));
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Scale", new double[15], new string[15]));
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Shape", new double[15], new string[15]));
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Shape2", new double[15], new string[15]));
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Minimum", new double[15], new string[15]));
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Maximum", new double[15], new string[15]));
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Mean", new double[15], new string[15]));
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Std Dev", new double[15], new string[15]));
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Skewness", new double[15], new string[15]));
            _fittedDistributionStatistics.Add(new FittedDistributionStatistic("Kurtosis", new double[15], new string[15]));
            if (Element?.ProbabilityOrdinates != null)
            {
                for (int i = 0; i < Element.ProbabilityOrdinates.Count; i++)
                {
                    _fittedDistributionStatistics.Add(new FittedDistributionStatistic(Element.ProbabilityOrdinates[i].ToString(), new double[15], new string[15]));
                }
            }

            // Populate values before binding. `FittedDistributionStatistic.Value` is a plain array
            // with no change notification, so once the DataGrid is bound it will NOT re-read in-place
            // mutations — populate first, then bind.
            //
            // When ordinates are invalid (out of order, out of [0,1] range, empty), skip population
            // so the table renders blank — mirrors UpdateFrequencyPlot's `Element.IsValid == false`
            // guard at line 925, which clears the plot under the same condition.
            if (Element?.IsEstimated == true && Element.IsValid == true)
            {
                for (int i = 0; i < Element.FittedDistributions.Count; i++)
                {
                    // parameters
                    var parms = Element.FittedDistributions[i].Distribution.GetParameters;
                    for (int j = 0; j < 4; j++)
                    {
                        _fittedDistributionStatistics[j].Value[i] = j < parms.Length ? parms[j] : double.NaN;
                    }
                    var min = Element.FittedDistributions[i].Distribution.Minimum;
                    var max = Element.FittedDistributions[i].Distribution.Maximum;
                    // summary stats
                    _fittedDistributionStatistics[4].Value[i] = min < -1E12 ? double.NegativeInfinity : min;
                    _fittedDistributionStatistics[5].Value[i] = max > 1E12 ? double.PositiveInfinity : max;
                    _fittedDistributionStatistics[6].Value[i] = Element.FittedDistributions[i].Distribution.Mean;
                    _fittedDistributionStatistics[7].Value[i] = Element.FittedDistributions[i].Distribution.StandardDeviation;
                    _fittedDistributionStatistics[8].Value[i] = Element.FittedDistributions[i].Distribution.Skewness;
                    _fittedDistributionStatistics[9].Value[i] = Element.FittedDistributions[i].Distribution.Kurtosis;
                    // probabilities
                    for (int j = 0; j < Element.ProbabilityOrdinates.Count; j++)
                    {
                        _fittedDistributionStatistics[10 + j].Value[i] = Element.FittedDistributions[i].Distribution.InverseCDF(1 - Element.ProbabilityOrdinates[j]);
                    }
                    // tool tip
                    string toolTip = Element.FittedDistributions[i].Distribution.DisplayName;
                    var parmString = Element.FittedDistributions[i].Distribution.ParametersToString;
                    for (int j = 0; j < Element.FittedDistributions[i].Distribution.NumberOfParameters; j++)
                    {
                        double p = double.NaN;
                        double.TryParse(parmString[j, 1], NumberStyles.Any, CultureInfo.InvariantCulture, out p);
                        toolTip += Environment.NewLine + parmString[j, 0] + " = " + p.ToString("N4");
                    }
                    for (int j = 0; j < _fittedDistributionStatistics.Count; j++)
                    {
                        _fittedDistributionStatistics[j].ToolTip[i] = toolTip;
                    }
                }
            }

            // Bind AFTER population so the first render shows current values.
            SummaryStatisticsDataGrid.ItemsSource = _fittedDistributionStatistics;
        }

        /// <summary>
        /// Handles the LoadingRow event of the summary statistics data grid.
        /// Applies custom borders to visually separate parameter rows from summary statistic rows.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing the row being loaded.</param>
        private void SummaryStatisticsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (((FittedDistributionStatistic)e.Row.DataContext).Name == "Minimum")
            {
                e.Row.BorderThickness = new Thickness(0, 2, 0, 0);
                e.Row.SetResourceReference(Border.BorderBrushProperty, "EnvironmentWindowText");
            }
            else if (((FittedDistributionStatistic)e.Row.DataContext).Name == "Kurtosis")
            {
                e.Row.BorderThickness = new Thickness(0, 0, 0, 2);
                e.Row.SetResourceReference(Border.BorderBrushProperty, "EnvironmentWindowText");
            }
            else
            {
                e.Row.BorderThickness = new Thickness(0, 0, 0, 0);
            }
        }



        #endregion

    }
}
