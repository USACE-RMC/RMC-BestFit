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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using OxyPlotControls;
using GenericControls;
using NumericControls.Distributions.Univariate;
using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Media.Media3D;
using Numerics.Sampling;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and managing input data including exact, uncertain, interval, and threshold data.
    /// Provides comprehensive visualization through multiple plot types and data grids for statistical analysis.
    /// </summary>
    public partial class InputDataControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InputDataControl"/> class.
        /// Sets up the row types for the various data grids.
        /// </summary>
        public InputDataControl()
        {
            InitializeComponent();
            DataContext = this;

            ExactDataGrid.RowType = typeof(ExactDataRowItem);
            UncertainDataGrid.RowType = typeof(UncertainDataRowItem);
            IntervalDataGrid.RowType = typeof(IntervalDataRowItem);
            ThresholdDataGrid.RowType = typeof(ThresholdDataRowItem);
        }

        #region Members

        /// <summary>
        /// Dependency property for the Element property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(InputData), typeof(InputDataControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the input data element being displayed and managed by this control.
        /// </summary>
        public InputData Element
        {
            get { return (InputData)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element dependency property changes.
        /// Handles event subscription and data grid binding for the new element.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event args containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as InputDataControl == null) return;
            var thisControl = (InputDataControl)d;

            // Remove handlers from old element and detach plots
            if (e.OldValue != null)
            {
                InputData oldElement = e.OldValue as InputData;
                if (oldElement != null)
                {
                    oldElement.PropertyChanged -= thisControl.ElementPropertyChanged;
                    thisControl.UnsubscribeSeriesCollectionChanged(oldElement);
                }
                // Detach plots from ContentControl hosts
                thisControl.DetachPlots();
                // Null all toolbar plots and unsubscribe PropertiesCalled
                thisControl.PlotToolbar.Plot = null;
                thisControl.PlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.SeasonalityPlotToolbar.Plot = null;
                thisControl.SeasonalityPlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.DensityPlotToolbar.Plot = null;
                thisControl.DensityPlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.HistogramPlotToolbar.Plot = null;
                thisControl.HistogramPlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.QQPlotToolbar.Plot = null;
                thisControl.QQPlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.ACFPlotToolbar.Plot = null;
                thisControl.ACFPlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.PACFPlotToolbar.Plot = null;
                thisControl.PACFPlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.ThresholdDiagnosticsToolbar.Plot = null;
                thisControl.ThresholdDiagnosticsToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as InputData;
            if (newElement == null) return;

            // Reset _isLoaded so the next Loaded event re-initializes plots for the new element
            thisControl._isLoaded = false;

            // Add handlers
            newElement.PropertyChanged += thisControl.ElementPropertyChanged;

            // Bind data grids
            thisControl.BindExactDataGrid();
            thisControl.BindUncertainDataGrid();
            thisControl.BindIntervalDataGrid();
            thisControl.BindThresholdDataGrid();

            // Subscribe to model CollectionChanged events so undo/redo replay propagates to UI.
            thisControl.SubscribeSeriesCollectionChanged(newElement);

            // Suspend plot bridges during visual tree attachment to prevent WPF
            // DependencyProperty changes from recording spurious undo entries.
            using (newElement.SuspendPlotBridges())
            {
                // Attach plots to ContentControl hosts
                thisControl.AttachPlots();

                // Wire toolbars to plots
                thisControl.PlotToolbar.Plot = thisControl.ChronologyPlotRadioButton.IsChecked == true
                    ? newElement.ChronologyPlot : newElement.FrequencyPlot;
                thisControl.SeasonalityPlotToolbar.Plot = newElement.SeasonalityPlot;
                thisControl.DensityPlotToolbar.Plot = newElement.DensityPlot;
                thisControl.HistogramPlotToolbar.Plot = newElement.HistogramPlot;
                thisControl.QQPlotToolbar.Plot = newElement.QQPlot;
                thisControl.ACFPlotToolbar.Plot = newElement.ACFPlot;
                thisControl.PACFPlotToolbar.Plot = newElement.PACFPlot;
                // Threshold diagnostics toolbar binds to whichever plot is currently selected
                if (thisControl.MRLPlotRadioButton.IsChecked == true)
                    thisControl.ThresholdDiagnosticsToolbar.Plot = newElement.MRLPlot;
                else if (thisControl.ModifiedScalePlotRadioButton.IsChecked == true)
                    thisControl.ThresholdDiagnosticsToolbar.Plot = newElement.ModifiedScalePlot;
                else
                    thisControl.ThresholdDiagnosticsToolbar.Plot = newElement.ShapePlot;

                // Subscribe toolbar PropertiesCalled ? control-level PlotPropertiesCalled
                thisControl.PlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.SeasonalityPlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.DensityPlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.HistogramPlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.QQPlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.ACFPlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.PACFPlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.ThresholdDiagnosticsToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
            }
        }

        /// <summary>
        /// Attaches the Element's Plot objects to the ContentControl hosts in the visual tree.
        /// </summary>
        private void AttachPlots()
        {
            if (Element == null) return;
            ChronologyPlotHost.Content = Element.ChronologyPlot;
            FrequencyPlotHost.Content = Element.FrequencyPlot;
            SeasonalityPlotHost.Content = Element.SeasonalityPlot;
            DensityPlotHost.Content = Element.DensityPlot;
            HistogramPlotHost.Content = Element.HistogramPlot;
            QQPlotHost.Content = Element.QQPlot;
            ACFPlotHost.Content = Element.ACFPlot;
            PACFPlotHost.Content = Element.PACFPlot;
            MRLPlotHost.Content = Element.MRLPlot;
            ModifiedScalePlotHost.Content = Element.ModifiedScalePlot;
            ShapePlotHost.Content = Element.ShapePlot;
        }

        /// <summary>
        /// Detaches Plot objects from the ContentControl hosts to allow reuse with other elements.
        /// </summary>
        private void DetachPlots()
        {
            ChronologyPlotHost.Content = null;
            FrequencyPlotHost.Content = null;
            SeasonalityPlotHost.Content = null;
            DensityPlotHost.Content = null;
            HistogramPlotHost.Content = null;
            QQPlotHost.Content = null;
            ACFPlotHost.Content = null;
            PACFPlotHost.Content = null;
            MRLPlotHost.Content = null;
            ModifiedScalePlotHost.Content = null;
            ShapePlotHost.Content = null;
        }


        /// <summary>
        /// Indicates whether the control has been loaded.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// Tracks whether the exact data grid is in read-only mode (non-Manual entry method).
        /// Used to apply read-only cell styles to Index/DateTime and Value columns.
        /// </summary>
        private bool _isExactDataReadOnly;

        /// <summary>
        /// Guard flag to suppress CollectionChanged handlers during Bind methods and bulk operations
        /// (add/delete/paste). During these operations, the UI is directly managing RowItems, so
        /// the CollectionChanged handler should not interfere. Matches the _suppressModelUpdate
        /// pattern in ProbabilityOrdinatesControl.
        /// </summary>
        private bool _suppressUIUpdate;

        // Dirty flags for lazy plot/stats/tests updates — only the visible tab is updated immediately;
        // hidden tabs are updated on demand when the user switches to them.
        /// <summary>Dirty flag indicating the chronology plot needs to be redrawn.</summary>
        private bool _chronologyPlotDirty;
        /// <summary>Dirty flag indicating the frequency plot needs to be redrawn.</summary>
        private bool _frequencyPlotDirty;
        /// <summary>Dirty flag indicating the seasonality plot needs to be redrawn.</summary>
        private bool _seasonalityPlotDirty;
        /// <summary>Dirty flag indicating the density plot needs to be redrawn.</summary>
        private bool _densityPlotDirty;
        /// <summary>Dirty flag indicating the histogram plot needs to be redrawn.</summary>
        private bool _histogramPlotDirty;
        /// <summary>Dirty flag indicating the QQ plot needs to be redrawn.</summary>
        private bool _qqPlotDirty;
        /// <summary>Dirty flag indicating the ACF plot needs to be redrawn.</summary>
        private bool _acfPlotDirty;
        /// <summary>Dirty flag indicating the PACF plot needs to be redrawn.</summary>
        private bool _pacfPlotDirty;
        /// <summary>Dirty flag indicating the threshold diagnostics plots need to be redrawn.</summary>
        private bool _thresholdDiagnosticsDirty;

        /// <summary>Cached mean residual life result for threshold diagnostic plots.</summary>
        private MeanResidualLifeResult _mrlResult;
        /// <summary>Cached parameter stability result for threshold diagnostic plots.</summary>
        private ParameterStabilityResult _stabilityResult;

        /// <summary>Dirty flag indicating the summary statistics need to be recalculated.</summary>
        private bool _summaryStatsDirty;
        /// <summary>Dirty flag indicating the hypothesis tests need to be recalculated.</summary>
        private bool _hypothesisTestsDirty;

        /// <summary>Cached border thickness for the top separator in summary statistics rows.</summary>
        private static readonly Thickness _topBorderThickness = new Thickness(0, 2, 0, 0);
        /// <summary>Cached border thickness for the bottom separator in summary statistics rows.</summary>
        private static readonly Thickness _bottomBorderThickness = new Thickness(0, 0, 0, 2);
        /// <summary>Cached zero-thickness border for normal summary statistics rows.</summary>
        private static readonly Thickness _noBorderThickness = new Thickness(0);
        /// <summary>Cached black brush for summary statistics row separators.</summary>
        private static readonly SolidColorBrush _borderBrush = new SolidColorBrush(Colors.Black);

        /// <summary>
        /// Gets a value indicating whether a plot was clicked.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Event raised when the preview control or toolbar is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for the PreviewControlClicked event.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar was clicked.</param>
        /// <param name="plot">The plot that was clicked.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Raised when plot properties are requested to be displayed or modified.
        /// Re-raises the PropertiesCalled event from the active toolbar so that MainProjectNode
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
        /// Gets the observable collection of exact data ordinates for grid display.
        /// </summary>
        public ObservableCollection<object> ExactDataOrdinates { get; private set; } = new ObservableCollection<object>();

        /// <summary>
        /// Gets the observable collection of uncertain data ordinates for grid display.
        /// </summary>
        public ObservableCollection<object> UncertainDataOrdinates { get; private set; } = new ObservableCollection<object>();

        /// <summary>
        /// Gets the observable collection of interval data ordinates for grid display.
        /// </summary>
        public ObservableCollection<object> IntervalDataOrdinates { get; private set; } = new ObservableCollection<object>();

        /// <summary>
        /// Gets the observable collection of threshold data ordinates for grid display.
        /// </summary>
        public ObservableCollection<object> ThresholdDataOrdinates { get; private set; } = new ObservableCollection<object>();

        /// <summary>
        /// List of summary statistics for the input data.
        /// </summary>
        private List<SummaryStatistic> _summaryStatistics = new List<SummaryStatistic>();

        /// <summary>
        /// List of hypothesis test results for the input data.
        /// </summary>
        private List<HypothesisTestResult> _hypothesisTestResults = new List<HypothesisTestResult>();

        #region Plot Convenience Properties

        /// <summary>Gets the chronology plot from the element.</summary>
        public Plot ChronologyPlot => Element?.ChronologyPlot;
        /// <summary>Gets the frequency plot from the element.</summary>
        public Plot FrequencyPlot => Element?.FrequencyPlot;
        /// <summary>Gets the seasonality plot from the element.</summary>
        public Plot SeasonalityPlot => Element?.SeasonalityPlot;
        /// <summary>Gets the density plot from the element.</summary>
        public Plot DensityPlot => Element?.DensityPlot;
        /// <summary>Gets the histogram plot from the element.</summary>
        public Plot HistogramPlot => Element?.HistogramPlot;
        /// <summary>Gets the QQ plot from the element.</summary>
        public Plot QQPlot => Element?.QQPlot;
        /// <summary>Gets the ACF plot from the element.</summary>
        public Plot ACFPlot => Element?.ACFPlot;
        /// <summary>Gets the PACF plot from the element.</summary>
        public Plot PACFPlot => Element?.PACFPlot;
        /// <summary>Gets the MRL plot from the element.</summary>
        public Plot MRLPlot => Element?.MRLPlot;
        /// <summary>Gets the modified scale plot from the element.</summary>
        public Plot ModifiedScalePlot => Element?.ModifiedScalePlot;
        /// <summary>Gets the shape plot from the element.</summary>
        public Plot ShapePlot => Element?.ShapePlot;

        #endregion

        #endregion

        // TODO: Plot change events and undo-redo logic removed pending OxyPlot library updates.

        /// <summary>
        /// Handles the Loaded event of the user control.
        /// Initializes plot settings and updates the control display on first load.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            // Always mark dirty and update on load. Plots must re-render whenever the
            // control enters the visual tree (first open, close/reopen, tab switch back).
            // Disable undo during plot updates so that Series.Clear/Add don't record as undo entries.
            MarkAllDirty();
            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                UpdateControl();
                UpdateUSGSTextBox();
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
            _isLoaded = true;

            // Re-subscribe to series CollectionChanged on every Loaded event.
            // Unloaded unsubscribes to prevent leaks, so we must re-subscribe here
            // because Loaded/Unloaded fire on tab switches but ElementCallback does not.
            if (Element != null)
            {
                UnsubscribeSeriesCollectionChanged(Element);
                SubscribeSeriesCollectionChanged(Element);
            }
        }

        /// <summary>
        /// Handles the Unloaded event for the control.
        /// Unsubscribes from collection changed events to prevent memory leaks.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The routed event args.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            UnsubscribeSeriesCollectionChanged(Element);
        }

        /// <summary>
        /// Handles property changed events from the Element.
        /// Rebinds data grids and updates control when relevant properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing the property name that changed.</param>
        private void ElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Element.ExactDataMethod) ||
                e.PropertyName == nameof(Element.IsProcessed) ||
                e.PropertyName == nameof(Element.DataFrame.PlottingParameter) ||
                e.PropertyName == nameof(Element.DataFrame.LowOutlierThreshold) ||
                e.PropertyName == "LowOutliers" ||
                e.PropertyName == "TimeSeries")
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    MarkAllDirty();
                    BindExactDataGrid();
                    // Disable undo during plot updates so Series.Clear/Add don't record as undo entries
                    bool wasUndoEnabled = Element.IsUndoEnabled;
                    Element.IsUndoEnabled = false;
                    try { UpdateControl(); }
                    finally { Element.IsUndoEnabled = wasUndoEnabled; }
                    UpdateUSGSTextBox();
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
            else if (e.PropertyName == nameof(Element.Threshold))
            {
                Element?.UpdateThresholdAnnotation();
            }
            else if (e.PropertyName == nameof(Data.PlottingPosition))
            {
                RefreshPlottingPositionColumns();
            }
        }

        /// <summary>
        /// Notifies grid row wrappers that plotting positions were recomputed on the underlying data.
        /// </summary>
        /// <remarks>
        /// <see cref="DataFrame.CalculatePlottingPositions"/> updates model rows while collection
        /// notifications are suppressed, then raises one aggregate plotting-position event. The
        /// grids bind through row wrappers, so the wrappers must forward that computed-property
        /// notification without rebinding the grids or triggering validation.
        /// </remarks>
        private void RefreshPlottingPositionColumns()
        {
            foreach (ExactDataRowItem rowItem in ExactDataOrdinates.OfType<ExactDataRowItem>())
            {
                rowItem.RefreshPlottingPosition();
            }

            foreach (UncertainDataRowItem rowItem in UncertainDataOrdinates.OfType<UncertainDataRowItem>())
            {
                rowItem.RefreshPlottingPosition();
            }

            foreach (IntervalDataRowItem rowItem in IntervalDataOrdinates.OfType<IntervalDataRowItem>())
            {
                rowItem.RefreshPlottingPosition();
            }
        }

        /// <summary>
        /// Subscribes to CollectionChanged events on all four DataSeries in the specified element.
        /// Called from ElementCallback (after binding grids) and UserControl_Loaded (re-subscribe on tab switch).
        /// </summary>
        /// <remarks>
        /// This replaces the old UndoManager.StateChanged approach. When undo/redo replays a collection
        /// action, the bridge modifies the model series which fires CollectionChanged. The bridge's own
        /// handler skips re-recording (IsExecutingAction check), but this UI handler still receives the
        /// event and updates the RowItems accordingly — matching the ProbabilityOrdinatesControl pattern.
        /// </remarks>
        /// <param name="element">The InputData element whose series to subscribe to.</param>
        private void SubscribeSeriesCollectionChanged(InputData element)
        {
            if (element?.DataFrame == null) return;
            element.DataFrame.ExactSeries.CollectionChanged += ExactSeries_CollectionChanged;
            element.DataFrame.UncertainSeries.CollectionChanged += UncertainSeries_CollectionChanged;
            element.DataFrame.IntervalSeries.CollectionChanged += IntervalSeries_CollectionChanged;
            element.DataFrame.ThresholdSeries.CollectionChanged += ThresholdSeries_CollectionChanged;
        }

        /// <summary>
        /// Unsubscribes from CollectionChanged events on all four DataSeries in the specified element.
        /// Called from ElementCallback (before switching elements) and UserControl_Unloaded (prevent leaks).
        /// </summary>
        /// <param name="element">The InputData element whose series to unsubscribe from.</param>
        private void UnsubscribeSeriesCollectionChanged(InputData element)
        {
            if (element?.DataFrame == null) return;
            element.DataFrame.ExactSeries.CollectionChanged -= ExactSeries_CollectionChanged;
            element.DataFrame.UncertainSeries.CollectionChanged -= UncertainSeries_CollectionChanged;
            element.DataFrame.IntervalSeries.CollectionChanged -= IntervalSeries_CollectionChanged;
            element.DataFrame.ThresholdSeries.CollectionChanged -= ThresholdSeries_CollectionChanged;
        }

        /// <summary>
        /// Handles CollectionChanged from the ExactSeries model collection.
        /// On Replace (cell edit or cell-edit undo): updates the RowItem's ordinate in-place via SetOrdinate,
        /// preserving DataGrid cell focus. On Reset (undo of add/delete/paste): full rebind from model state.
        /// </summary>
        /// <param name="sender">The ExactSeries that changed.</param>
        /// <param name="e">Event args describing the change.</param>
        private void ExactSeries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressUIUpdate) return;

            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                // Suppress PropertyChanged handlers during SetOrdinate to prevent redundant
                // ValidateTable calls (SetOrdinate fires 5× NotifyPropertyChanged).
                _suppressUIUpdate = true;
                try
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        var rowItem = (ExactDataRowItem)ExactDataOrdinates[e.NewStartingIndex + i];
                        rowItem.SetOrdinate((ExactData)e.NewItems[i]);
                    }
                }
                finally
                {
                    _suppressUIUpdate = false;
                }
                // Exact data affects everything: all plots, summary stats, and hypothesis tests
                MarkAllDirty();
                // Disable undo during plot updates so Series.Clear/Add don't record as undo entries
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Full rebuild — undo of paste/add/delete
                MarkAllDirty();
                BindExactDataGrid();
                // Disable undo during plot updates so Series.Clear/Add don't record as undo entries
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles CollectionChanged from the UncertainSeries model collection.
        /// On Replace: updates the RowItem's ordinate in-place. On Reset: full rebind.
        /// </summary>
        /// <param name="sender">The UncertainSeries that changed.</param>
        /// <param name="e">Event args describing the change.</param>
        private void UncertainSeries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressUIUpdate) return;

            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                _suppressUIUpdate = true;
                try
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        var rowItem = (UncertainDataRowItem)UncertainDataOrdinates[e.NewStartingIndex + i];
                        rowItem.SetOrdinate((UncertainData)e.NewItems[i]);
                    }
                }
                finally
                {
                    _suppressUIUpdate = false;
                }
                // Non-exact data only affects chronology, frequency, and summary stats
                MarkDataFrameDirty();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                MarkDataFrameDirty();
                BindUncertainDataGrid();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles CollectionChanged from the IntervalSeries model collection.
        /// On Replace: updates the RowItem's ordinate in-place. On Reset: full rebind.
        /// </summary>
        /// <param name="sender">The IntervalSeries that changed.</param>
        /// <param name="e">Event args describing the change.</param>
        private void IntervalSeries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressUIUpdate) return;

            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                _suppressUIUpdate = true;
                try
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        var rowItem = (IntervalDataRowItem)IntervalDataOrdinates[e.NewStartingIndex + i];
                        rowItem.SetOrdinate((IntervalData)e.NewItems[i]);
                    }
                }
                finally
                {
                    _suppressUIUpdate = false;
                }
                // Non-exact data only affects chronology, frequency, and summary stats
                MarkDataFrameDirty();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                MarkDataFrameDirty();
                BindIntervalDataGrid();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles CollectionChanged from the ThresholdSeries model collection.
        /// On Replace: updates the RowItem's ordinate in-place. On Reset: full rebind.
        /// </summary>
        /// <param name="sender">The ThresholdSeries that changed.</param>
        /// <param name="e">Event args describing the change.</param>
        private void ThresholdSeries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressUIUpdate) return;

            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                _suppressUIUpdate = true;
                try
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        var rowItem = (ThresholdDataRowItem)ThresholdDataOrdinates[e.NewStartingIndex + i];
                        rowItem.SetOrdinate((ThresholdData)e.NewItems[i]);
                    }
                }
                finally
                {
                    _suppressUIUpdate = false;
                }
                // Non-exact data only affects chronology, frequency, and summary stats
                MarkDataFrameDirty();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                MarkDataFrameDirty();
                BindThresholdDataGrid();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Updates all plots, summary statistics, and hypothesis tests in the control.
        /// Configures the index slider based on exact data series quartiles.
        /// </summary>
        private void UpdateControl()
        {
            // Show/hide threshold diagnostics tab based on POT method and time series availability
            if (Element.ExactDataMethod == InputData.ExactDataEntryType.PeaksOverThresholdSeries
                && Element.TimeSeriesElement?.TimeSeries != null)
            {
                ThresholdDiagnosticsTab.Visibility = Visibility.Visible;
            }
            else
            {
                ThresholdDiagnosticsTab.Visibility = Visibility.Collapsed;
            }

            // Set the hypothesis test split index slider BEFORE updating visible content,
            // so that if the Hypothesis Tests tab is already selected (e.g., restored layout),
            // UpdateHypothesisTests() uses a valid slider value instead of the default 0.
            if (Element != null && Element.DataFrame != null && Element.DataFrame.ExactSeries != null && Element.DataFrame.ExactSeries.Count > 0)
            {
                int min = Element.DataFrame.ExactSeries[(int)Math.Floor(Element.DataFrame.ExactSeries.Count * 0.25)].Index;
                int max = Element.DataFrame.ExactSeries[(int)Math.Floor(Element.DataFrame.ExactSeries.Count * 0.75)].Index;
                int value = Element.DataFrame.ExactSeries[(int)Math.Floor(Element.DataFrame.ExactSeries.Count / 2d)].Index;
                if (max <= min || value <= min || value >= max)
                {
                    max = min + 2;
                    value = min + 1;
                }
                IndexSlider.Minimum = min;
                IndexSlider.Maximum = max;
                IndexSlider.Value = value;
            }

            // Update only the currently visible content; other tabs are updated lazily on switch
            UpdateVisibleContent();
        }

        /// <summary>
        /// Updates the USGS text box with raw USGS data if the entry method is USGS peak data.
        /// Shows a fallback message for backwards compatibility with older project files.
        /// </summary>
        private void UpdateUSGSTextBox()
        {
            if (Element == null) return;

            if (Element.ExactDataMethod == InputData.ExactDataEntryType.USGSPeakDischarge ||
                Element.ExactDataMethod == InputData.ExactDataEntryType.USGSPeakStage)
            {
                USGSTextFileTab.Visibility = Visibility.Visible;
                var textRange = new TextRange(USGSRawTextBox.Document.ContentStart, USGSRawTextBox.Document.ContentEnd);
                string rawText = Element.DataFrame.USGSRawText;
                textRange.Text = string.IsNullOrEmpty(rawText)
                    ? "The source text file is not available. Please try downloading the data again."
                    : rawText;
            }
            else
            {
                USGSTextFileTab.Visibility = Visibility.Collapsed;
                var textRange = new TextRange(USGSRawTextBox.Document.ContentStart, USGSRawTextBox.Document.ContentEnd);
                textRange.Text = string.Empty;
            }
        }

        /// <summary>
        /// Handles property changed events from exact data row items.
        /// Validates the data grid and updates the control when non-plotting position properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing the property name that changed.</param>
        private void ExactDataRowItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressUIUpdate) return;
            if (e.PropertyName != nameof(Data.PlottingPosition))
            {
                if (Element.DataFrame.ExactSeries.SuppressCollectionChanged == false)
                {
                    ExactDataGrid.ValidateTable();
                }
            }
        }

        /// <summary>
        /// Handles property changed events from uncertain data row items.
        /// Validates the data grid and updates the control when non-plotting position properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing the property name that changed.</param>
        private void UncertainDataRowItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressUIUpdate) return;
            if (e.PropertyName != nameof(Data.PlottingPosition))
            {
                if (Element.DataFrame.UncertainSeries.SuppressCollectionChanged == false)
                {
                    UncertainDataGrid.ValidateTable();
                }
            }
        }

        /// <summary>
        /// Handles property changed events from interval data row items.
        /// Validates the data grid and updates the control when non-plotting position properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing the property name that changed.</param>
        private void IntervalDataRowItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressUIUpdate) return;
            if (e.PropertyName != nameof(Data.PlottingPosition))
            {
                if (Element.DataFrame.IntervalSeries.SuppressCollectionChanged == false)
                {
                    IntervalDataGrid.ValidateTable();
                }
            }
        }

        /// <summary>
        /// Handles property changed events from threshold data row items.
        /// Validates the data grid and updates the control when non-plotting position properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing the property name that changed.</param>
        private void ThresholdDataRowItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressUIUpdate) return;
            if (e.PropertyName != nameof(Data.PlottingPosition))
            {
                if (Element.DataFrame.ThresholdSeries.SuppressCollectionChanged == false)
                {
                    ThresholdDataGrid.ValidateTable();
                }
            }
        }

        #region Lazy Update Helpers

        /// <summary>
        /// Marks all plots, summary statistics, and hypothesis tests as dirty.
        /// Used when exact data changes or element properties change (everything needs refresh).
        /// </summary>
        private void MarkAllDirty()
        {
            _chronologyPlotDirty = true;
            _frequencyPlotDirty = true;
            _seasonalityPlotDirty = true;
            _densityPlotDirty = true;
            _histogramPlotDirty = true;
            _qqPlotDirty = true;
            _acfPlotDirty = true;
            _pacfPlotDirty = true;
            _thresholdDiagnosticsDirty = true;
            _summaryStatsDirty = true;
            _hypothesisTestsDirty = true;
        }

        /// <summary>
        /// Marks only the items that depend on non-exact data series (Uncertain, Interval, Threshold) as dirty.
        /// These series only affect the chronology plot, frequency plot, and summary statistics.
        /// Seasonality, density, histogram, QQ, ACF, PACF, and hypothesis tests use exact data only.
        /// </summary>
        private void MarkDataFrameDirty()
        {
            _chronologyPlotDirty = true;
            _frequencyPlotDirty = true;
            _summaryStatsDirty = true;
        }

        /// <summary>
        /// Updates only the content of the currently visible tab. Plots/stats/tests on hidden tabs
        /// remain dirty and will be updated lazily when the user switches to them via
        /// <see cref="MainTabControl_SelectionChanged"/>.
        /// </summary>
        private void UpdateVisibleContent()
        {
            if (Element == null) return;

            // Update visible plot
            if (DataFrameTab.IsSelected)
            {
                if (_chronologyPlotDirty) { UpdateChronologyPlot(); _chronologyPlotDirty = false; }
                if (_frequencyPlotDirty) { UpdateFrequencyPlot(); _frequencyPlotDirty = false; }
            }
            else if (SeasonalityTab.IsSelected && _seasonalityPlotDirty) { UpdateLazyContentWithWaitCursor(UpdateSeasonalityPlot, () => _seasonalityPlotDirty = false); }
            else if (DensityTab.IsSelected && _densityPlotDirty) { UpdateLazyContentWithWaitCursor(UpdateDensityPlot, () => _densityPlotDirty = false); }
            else if (HistogramTab.IsSelected && _histogramPlotDirty) { UpdateHistogramPlot(); _histogramPlotDirty = false; }
            else if (QQTab.IsSelected && _qqPlotDirty) { UpdateQQPlot(); _qqPlotDirty = false; }
            else if (ACFTab.IsSelected && _acfPlotDirty) { UpdateLazyContentWithWaitCursor(UpdateACFPlot, () => _acfPlotDirty = false); }
            else if (PACFTab.IsSelected && _pacfPlotDirty) { UpdateLazyContentWithWaitCursor(UpdatePACFPlot, () => _pacfPlotDirty = false); }
            else if (ThresholdDiagnosticsTab.IsSelected && _thresholdDiagnosticsDirty) { UpdateLazyContentWithWaitCursor(UpdateThresholdDiagnosticsPlots, () => _thresholdDiagnosticsDirty = false); }

            // Update visible non-plot tabs
            if (SummaryStatisticsTab.IsSelected && _summaryStatsDirty) { UpdateSummaryStats(); _summaryStatsDirty = false; }
            if (HypothesisTestsTab.IsSelected && _hypothesisTestsDirty) { UpdateLazyContentWithWaitCursor(UpdateHypothesisTests, () => _hypothesisTestsDirty = false); }
        }

        /// <summary>
        /// Handles tab selection changes to lazily update dirty content when the user switches tabs.
        /// </summary>
        /// <param name="sender">The TabControl that raised the event.</param>
        /// <param name="e">Event arguments containing the selection change details.</param>
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != MainTabControl) return;
            if (Element == null) return;
            if (DataFrameTab.IsSelected)
            {
                if (_chronologyPlotDirty) { UpdateChronologyPlot(); _chronologyPlotDirty = false; }
                if (_frequencyPlotDirty) { UpdateFrequencyPlot(); _frequencyPlotDirty = false; }
            }
            else if (SeasonalityTab.IsSelected && _seasonalityPlotDirty) { UpdateLazyContentWithWaitCursor(UpdateSeasonalityPlot, () => _seasonalityPlotDirty = false); }
            else if (DensityTab.IsSelected && _densityPlotDirty) { UpdateLazyContentWithWaitCursor(UpdateDensityPlot, () => _densityPlotDirty = false); }
            else if (HistogramTab.IsSelected && _histogramPlotDirty) { UpdateHistogramPlot(); _histogramPlotDirty = false; }
            else if (QQTab.IsSelected && _qqPlotDirty) { UpdateQQPlot(); _qqPlotDirty = false; }
            else if (ACFTab.IsSelected && _acfPlotDirty) { UpdateLazyContentWithWaitCursor(UpdateACFPlot, () => _acfPlotDirty = false); }
            else if (PACFTab.IsSelected && _pacfPlotDirty) { UpdateLazyContentWithWaitCursor(UpdatePACFPlot, () => _pacfPlotDirty = false); }
            else if (ThresholdDiagnosticsTab.IsSelected && _thresholdDiagnosticsDirty) { UpdateLazyContentWithWaitCursor(UpdateThresholdDiagnosticsPlots, () => _thresholdDiagnosticsDirty = false); }
            else if (SummaryStatisticsTab.IsSelected && _summaryStatsDirty) { UpdateSummaryStats(); _summaryStatsDirty = false; }
            else if (HypothesisTestsTab.IsSelected && _hypothesisTestsDirty) { UpdateLazyContentWithWaitCursor(UpdateHypothesisTests, () => _hypothesisTestsDirty = false); }
        }

        /// <summary>
        /// Runs a lazy content update with visible wait-cursor feedback and marks the content clean afterward.
        /// </summary>
        /// <param name="updateAction">The lazy update action to run.</param>
        /// <param name="markCleanAction">The action that clears the corresponding dirty flag.</param>
        /// <remarks>
        /// Dirty flags are cleared only after the update action completes successfully.
        /// </remarks>
        private void UpdateLazyContentWithWaitCursor(Action updateAction, Action markCleanAction)
        {
            WaitCursorHelper.RunWithVisibleWaitCursor(Dispatcher, () =>
            {
                updateAction();
                markCleanAction();
            });
        }

        #endregion

        #region Plots

        /// <summary>
        /// Gets the currently selected plot based on which tab and radio button is active.
        /// </summary>
        /// <returns>The currently active plot, or null if no plot tab is selected.</returns>
        public Plot GetCurrentPlot()
        {
            if (DataFrameTab.IsSelected == true)
            {
                if (ChronologyPlotRadioButton.IsChecked == true) return ChronologyPlot;
                if (FrequencyPlotRadioButton.IsChecked == true) return FrequencyPlot;
                return null;
            }
            if (SeasonalityTab.IsSelected == true) return SeasonalityPlot;
            if (DensityTab.IsSelected == true) return DensityPlot;
            if (HistogramTab.IsSelected == true) return HistogramPlot;
            if (QQTab.IsSelected == true) return QQPlot;
            if (ACFTab.IsSelected == true) return ACFPlot;
            if (PACFTab.IsSelected == true) return PACFPlot;
            if (ThresholdDiagnosticsTab.IsSelected == true)
            {
                if (MRLPlotRadioButton.IsChecked == true) return MRLPlot;
                if (ModifiedScalePlotRadioButton.IsChecked == true) return ModifiedScalePlot;
                if (ShapePlotRadioButton.IsChecked == true) return ShapePlot;
            }
            return null;
        }

        /// <summary>
        /// Gets the toolbar for the currently selected plot based on which tab is active.
        /// </summary>
        /// <returns>The toolbar for the currently active plot, or null if no plot tab is selected.</returns>
        public OxyPlotToolbar GetCurrentPlotToolbar()
        {
            if (DataFrameTab.IsSelected == true) return PlotToolbar;
            if (SeasonalityTab.IsSelected == true) return SeasonalityPlotToolbar;
            if (DensityTab.IsSelected == true) return DensityPlotToolbar;
            if (HistogramTab.IsSelected == true) return HistogramPlotToolbar;
            if (QQTab.IsSelected == true) return QQPlotToolbar;
            if (ACFTab.IsSelected == true) return ACFPlotToolbar;
            if (PACFTab.IsSelected == true) return PACFPlotToolbar;
            if (ThresholdDiagnosticsTab.IsSelected == true) return ThresholdDiagnosticsToolbar;
            return null;
        }

        /// <summary>
        /// Handles PropertiesCalled from any toolbar and re-raises it as the control-level PlotPropertiesCalled event.
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
        /// Handles the PreviewMouseDown event for the user control.
        /// Determines which plot and toolbar were clicked and raises the appropriate event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
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
        /// Handles the LostFocus event for the user control.
        /// Resets the PlotClicked flag when focus is lost.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the Checked event for the chronology plot radio button.
        /// Shows the chronology plot and hides the frequency plot.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void ChronologyPlotRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            ChronologyPlotHost.Visibility = Visibility.Visible;
            FrequencyPlotHost.Visibility = Visibility.Hidden;
            PlotToolbar.Plot = ChronologyPlot;
            ChronologyPlot?.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles the Checked event for the frequency plot radio button.
        /// Shows the frequency plot and hides the chronology plot.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void FrequencyPlotRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            ChronologyPlotHost.Visibility = Visibility.Hidden;
            FrequencyPlotHost.Visibility = Visibility.Visible;
            PlotToolbar.Plot = FrequencyPlot;
            FrequencyPlot?.InvalidatePlot(true);
        }

        /// <summary>
        /// Updates the chronology plot with current exact, low outlier, uncertain, interval, and threshold data.
        /// Configures data series with appropriate styling and tracker formats.
        /// </summary>
        private void UpdateChronologyPlot()
        {
            if (Element == null) return;
            var plot = Element.ChronologyPlot;
            if (plot == null) return;
            var valueStringFormat = UserSettings.ValueStringFormat;

            // Look up named series before Clear
            var exactData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
                ?? new ScatterPointSeries
                {
                    Name = "ExactData",
                    Title = "Exact Data",
                    MarkerFill = Colors.Black,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 4,
                    MarkerType = MarkerType.Circle
                };

            var lowOutlierData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 4,
                    MarkerType = MarkerType.Cross
                };

            var uncertainData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "UncertainData")
                ?? new ScatterErrorSeries
                {
                    Name = "UncertainData",
                    Title = "Uncertain Data",
                    MarkerFill = Colors.Green,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 4,
                    MarkerType = MarkerType.Diamond
                };

            var intervalData = plot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "IntervalData")
                ?? new ScatterErrorSeries
                {
                    Name = "IntervalData",
                    Title = "Interval Data",
                    MarkerFill = Colors.Cyan,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 4,
                    MarkerType = MarkerType.Circle
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                // Exact data
                exactData.ItemsSource = Element.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                exactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.Index, d.Value); };
                exactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.ExactSeries.Count > 0) plot.Series.Add(exactData);

                // Low outlier data
                lowOutlierData.ItemsSource = Element.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                lowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.Index, d.Value); };
                lowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.NumberOfLowOutliers > 0) plot.Series.Add(lowOutlierData);

                // Uncertain data
                uncertainData.ItemsSource = Element.DataFrame.UncertainSeries;
                uncertainData.DataFieldX = nameof(UncertainData.Index);
                uncertainData.DataFieldY = nameof(UncertainData.Value);
                uncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                uncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                uncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.UncertainSeries.Count > 0) plot.Series.Add(uncertainData);

                // Interval data
                intervalData.ItemsSource = Element.DataFrame.IntervalSeries;
                intervalData.DataFieldX = nameof(IntervalData.Index);
                intervalData.DataFieldY = nameof(IntervalData.Value);
                intervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                intervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                intervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.IntervalSeries.Count > 0) plot.Series.Add(intervalData);

                // Threshold data
                UpdateThresholdPlotSeries(valueStringFormat);

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        /// <summary>
        /// Updates the threshold data series displayed on the chronology plot.
        /// Removes existing threshold area series and adds updated ones from the DataFrame.
        /// </summary>
        /// <param name="valueStringFormat">Format string for data values in tracker tooltips.</param>
        private void UpdateThresholdPlotSeries(string valueStringFormat = "N4")
        {
            var plot = Element.ChronologyPlot;
            if (plot == null) return;

            // Clear the threshold data series from plot
            for (int i = plot.Series.Count - 1; i >= 0; i -= 1)
            {
                if (plot.Series[i].GetType() == typeof(AreaSeries))
                    plot.Series.RemoveAt(i);
            }
            // Add back in the threshold data
            for (int i = 0; i <= Element.DataFrame.ThresholdSeries.Count - 1; i++)
                AddThresholdDataPoint((ThresholdData)Element.DataFrame.ThresholdSeries[i], i + 1, i == 0 ? true : false, valueStringFormat);
        }

        /// <summary>
        /// Adds a threshold data point as an area series to the chronology plot.
        /// Creates a shaded region representing the threshold value over a specified index range.
        /// </summary>
        /// <param name="data">The threshold data to plot.</param>
        /// <param name="index">The series index for naming purposes.</param>
        /// <param name="showInLegend">Whether to display this series in the legend.</param>
        /// <param name="valueStringFormat">Format string for data values in tracker tooltips.</param>
        private void AddThresholdDataPoint(ThresholdData data, int index, bool showInLegend = false, string valueStringFormat = "N4")
        {
            var plot = Element.ChronologyPlot;
            if (plot == null) return;

            // Get start and end indexes in case they are the same
            double startIndex = data.StartIndex;
            double endIndex = data.EndIndex;
            if (endIndex == startIndex)
            {
                startIndex -= 0.5;
                endIndex += 0.5;
            }
            // Create a new area series for the threshold and add to the chronology plot
            AreaSeries areaSeries = new AreaSeries() { Name = "ThresholdData_" + index + "_" + data.StartIndex.ToString(CultureInfo.InvariantCulture).Replace("-", "_") + "_" + data.Index.ToString(CultureInfo.InvariantCulture).Replace("-", "_") };
            if (showInLegend == true)
            {
                areaSeries.Title = "Threshold Data";
                areaSeries.TrackerFormatString = "Threshold " + data.StartIndex.ToString() + "-" + data.EndIndex.ToString() + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                areaSeries.RenderInLegend = true;
            }
            else
            {
                areaSeries.Title = "Threshold " + data.StartIndex.ToString() + "-" + data.EndIndex.ToString();
                areaSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
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
            plot.Series.Add(areaSeries);
        }

        /// <summary>
        /// Updates the frequency plot with current exact, low outlier, uncertain, and interval data.
        /// Handles logarithmic Y-axis filtering and configures data series with plotting positions.
        /// </summary>
        private void UpdateFrequencyPlot()
        {
            if (Element == null) return;
            var plot = Element.FrequencyPlot;
            if (plot == null) return;
            var valueStringFormat = UserSettings.ValueStringFormat;

            // Look up named series before Clear
            var exactData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
                ?? new ScatterPointSeries
                {
                    Name = "ExactData",
                    Title = "Exact Data",
                    MarkerFill = Colors.Black,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle
                };

            var lowOutlierData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Cross
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
                    MarkerType = MarkerType.Diamond
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
                    MarkerType = MarkerType.Circle
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

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
                exactData.ItemsSource = logAxis == true ? Element.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false && x.Value > 1E-16) : Element.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                exactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                exactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.ExactSeries.Count > 0) plot.Series.Add(exactData);

                // Low outlier data
                lowOutlierData.ItemsSource = logAxis == true ? Element.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true && x.Value > 1E-16) : Element.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                lowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                lowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.NumberOfLowOutliers > 0) plot.Series.Add(lowOutlierData);

                // Uncertain data
                uncertainData.ItemsSource = logAxis == true ? Element.DataFrame.UncertainSeries : Element.DataFrame.UncertainSeries.Where(x => x.Value > 1E-16);
                uncertainData.DataFieldX = nameof(UncertainData.PlottingPosition);
                uncertainData.DataFieldY = nameof(UncertainData.Value);
                uncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                uncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                uncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.UncertainSeries.Count > 0) plot.Series.Add(uncertainData);

                // Interval data
                intervalData.ItemsSource = logAxis == true ? Element.DataFrame.IntervalSeries : Element.DataFrame.IntervalSeries.Where(x => x.Value > 1E-16);
                intervalData.DataFieldX = nameof(IntervalData.PlottingPosition);
                intervalData.DataFieldY = nameof(IntervalData.Value);
                intervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                intervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                intervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.IntervalSeries.Count > 0) plot.Series.Add(intervalData);

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        /// <summary>
        /// Updates the seasonality plot displaying monthly frequency distribution.
        /// Creates a histogram showing the distribution of data across calendar months.
        /// Requires at least 10 exact data points and valid DateTime values.
        /// </summary>
        private void UpdateSeasonalityPlot()
        {
            if (Element == null) return;
            var plot = Element.SeasonalityPlot;
            if (plot == null) { SeasonalityWarning.Visibility = Visibility.Visible; return; }
            var valueStringFormat = UserSettings.ValueStringFormat;

            // Look up named series before Clear
            var histogramSeries = plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "Histogram")
                ?? new HistogramSeries
                {
                    Name = "Histogram",
                    Title = "Seasonality Plot",
                    FillColor = ColorFromHex("#7529D372"),
                    StrokeColor = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                if (Element.DataFrame.ExactSeries.Count < 10)
                {
                    plot.InvalidatePlot(true);
                    SeasonalityWarning.Visibility = Visibility.Visible;
                    return;
                }

                var timeSeries = new TimeSeries(TimeInterval.Irregular);
                for (int i = 0; i < Element.DataFrame.ExactSeries.Count; i++)
                {
                    var ordinate = new SeriesOrdinate<DateTime, double>(((ExactData)Element.DataFrame.ExactSeries[i]).DateTime, Element.DataFrame.ExactSeries[i].Value);
                    if (ordinate.Index != default(DateTime))
                        timeSeries.Add(ordinate);
                }
                if (timeSeries.Count == 0)
                {
                    plot.InvalidatePlot(true);
                    SeasonalityWarning.Visibility = Visibility.Visible;
                    return;
                }

                var frequency = timeSeries.MonthlyFrequency();
                double sum = frequency.Sum();
                var seasonPoints = new List<OxyPlot.Series.HistogramItem>();
                for (int i = 1; i <= 12; i++)
                {
                    var lo = OxyPlot.Axes.DateTimeAxis.ToDouble(new DateTime(2020, i, 1));
                    var hi = OxyPlot.Axes.DateTimeAxis.ToDouble(new DateTime(i < 12 ? 2020 : 2021, i < 12 ? i + 1 : 1, 1));
                    seasonPoints.Add(new OxyPlot.Series.HistogramItem(lo, hi, frequency[i - 1] / sum * (hi - lo)));
                }

                for (int i = 0; i < plot.Axes.Count; i++)
                {
                    if (plot.Axes[i].Position == OxyPlot.Axes.AxisPosition.Bottom)
                    {
                        plot.Axes[i].Minimum = OxyPlot.Axes.DateTimeAxis.ToDouble(new DateTime(2020, 1, 1));
                        plot.Axes[i].Maximum = OxyPlot.Axes.DateTimeAxis.ToDouble(new DateTime(2020, 12, 31));
                    }
                }

                histogramSeries.ItemsSource = seasonPoints;
                histogramSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:MMM}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                plot.Series.Add(histogramSeries);

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
            SeasonalityWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Updates the density plot with a kernel density estimate of the combined data.
        /// Creates a smooth probability density function from exact, uncertain, and interval data.
        /// Requires at least 10 exact data points.
        /// </summary>
        private void UpdateDensityPlot()
        {
            if (Element == null) return;
            var plot = Element.DensityPlot;
            if (plot == null) { DensityWarning.Visibility = Visibility.Visible; return; }
            var valueStringFormat = UserSettings.ValueStringFormat;

            // Look up named series before Clear
            var densitySeries = plot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "Density")
                ?? new AreaSeries
                {
                    Name = "Density",
                    Title = "Density Plot",
                    Fill = Color.FromArgb(125, 104, 140, 175),
                    Color = Color.FromArgb(255, 53, 59, 122),
                    LineStyle = LineStyle.Solid,
                    BrokenLineThickness = 1,
                    StrokeThickness = 1,
                    Decimator = OxyPlot.Decimator.Decimate
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                if (Element.DataFrame.ExactSeries.Count < 10)
                {
                    plot.InvalidatePlot(true);
                    DensityWarning.Visibility = Visibility.Visible;
                    return;
                }

                var values = Element.DataFrame.ExactSeries.ValuesToList();
                values.AddRange(Element.DataFrame.UncertainSeries.ValuesToList());
                values.AddRange(Element.DataFrame.IntervalSeries.ValuesToList());

                var density = new KernelDensity(values);
                var xValues = Stratify.XValues(new StratificationOptions(Tools.Min(values), Tools.Max(values), 1000));
                var pdf = density.CreatePDFGraph(xValues);
                var postPoints = new List<Point3D>();
                for (int i = 0; i < pdf.GetLength(0); i++)
                    postPoints.Add(new Point3D(pdf[i, 0], 0, pdf[i, 1]));

                densitySeries.ItemsSource = postPoints;
                densitySeries.DataFieldX = nameof(Point3D.X);
                densitySeries.DataFieldY = nameof(Point3D.Y);
                densitySeries.DataFieldX2 = nameof(Point3D.X);
                densitySeries.DataFieldY2 = nameof(Point3D.Z);
                densitySeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.0000}" + Environment.NewLine + "{3}: {4:0.000000}";
                plot.Series.Add(densitySeries);

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
            DensityWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Updates the histogram plot with frequency distribution of the combined data.
        /// Uses Sturges' rule to determine the number of bins for the histogram.
        /// Requires at least 10 exact data points.
        /// </summary>
        private void UpdateHistogramPlot()
        {
            if (Element == null) return;
            var plot = Element.HistogramPlot;
            if (plot == null) { HistogramWarning.Visibility = Visibility.Visible; return; }
            var valueStringFormat = UserSettings.ValueStringFormat;

            // Look up named series before Clear
            var histogramSeries = plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "Histogram")
                ?? new HistogramSeries
                {
                    Name = "Histogram",
                    Title = "Histogram Plot",
                    FillColor = Color.FromArgb(125, 104, 140, 175),
                    StrokeColor = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                if (Element.DataFrame.ExactSeries.Count < 10)
                {
                    plot.InvalidatePlot(true);
                    HistogramWarning.Visibility = Visibility.Visible;
                    return;
                }

                var values = Element.DataFrame.ExactSeries.ValuesToList();
                values.AddRange(Element.DataFrame.UncertainSeries.ValuesToList());
                values.AddRange(Element.DataFrame.IntervalSeries.ValuesToList());

                // Use Sturges' rule
                int k = (int)(1 + 3.322 * Math.Log(values.Count));
                var histogram = new Histogram(values, k);
                var histItems = new List<OxyPlot.Series.HistogramItem>();
                double sum = 0;
                for (int i = 0; i < histogram.NumberOfBins; i++)
                    sum += histogram[i].Frequency * histogram.BinWidth;
                for (int i = 0; i < histogram.NumberOfBins; i++)
                    histItems.Add(new OxyPlot.Series.HistogramItem(histogram[i].LowerBound, histogram[i].UpperBound, histogram[i].Frequency * histogram.BinWidth / sum));

                histogramSeries.ItemsSource = histItems;
                histogramSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + valueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000000}";
                plot.Series.Add(histogramSeries);

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
            HistogramWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Handles the Checked event for the real space radio button.
        /// Updates the QQ plot to display data in real space.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void RealSpaceRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateQQPlot();
        }

        /// <summary>
        /// Handles the Checked event for the log space radio button.
        /// Updates the QQ plot to display data in logarithmic space.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void LogSpaceRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateQQPlot();
        }

        /// <summary>
        /// Updates the QQ (Quantile-Quantile) plot comparing data to standardized normal distribution.
        /// Displays data in either real space or log10 space based on the radio button selection.
        /// Includes a 1:1 reference line for comparison.
        /// Requires at least 10 exact data points.
        /// </summary>
        private void UpdateQQPlot()
        {
            if (Element == null) return;
            var plot = Element.QQPlot;
            if (plot == null) { QQWarning.Visibility = Visibility.Visible; return; }
            if (Element.DataFrame == null) { QQWarning.Visibility = Visibility.Visible; return; }
            bool useLogSpace = LogSpaceRadioButton.IsChecked == true;
            var valueStringFormat = UserSettings.ValueStringFormat;

            // Look up named series before Clear
            var exactData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
                ?? new ScatterPointSeries
                {
                    Name = "ExactData",
                    Title = "Exact Data",
                    MarkerFill = Colors.Black,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle
                };

            var lowOutlierData = plot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Cross
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
                    MarkerType = MarkerType.Diamond
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
                    MarkerType = MarkerType.Circle
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (Element.DataFrame.ExactSeries.Count < 10)
                {
                    plot.InvalidatePlot(true);
                    QQWarning.Visibility = Visibility.Visible;
                    return;
                }

                Element.DataFrame.SetStandardizedValues();

                // Exact data
                exactData.ItemsSource = Element.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                exactData.DataFieldX = useLogSpace ? nameof(ExactData.StandardizedLog10Value) : nameof(ExactData.StandardizedValue);
                exactData.DataFieldY = useLogSpace ? nameof(ExactData.Log10Value) : nameof(ExactData.Value);
                exactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.ExactSeries.Count > 0) plot.Series.Add(exactData);

                // Low outlier data
                lowOutlierData.ItemsSource = Element.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                lowOutlierData.DataFieldX = useLogSpace ? nameof(ExactData.StandardizedLog10Value) : nameof(ExactData.StandardizedValue);
                lowOutlierData.DataFieldY = useLogSpace ? nameof(ExactData.Log10Value) : nameof(ExactData.Value);
                lowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.NumberOfLowOutliers > 0) plot.Series.Add(lowOutlierData);

                // Uncertain data
                uncertainData.ItemsSource = Element.DataFrame.UncertainSeries;
                uncertainData.DataFieldX = useLogSpace ? nameof(UncertainData.StandardizedLog10Value) : nameof(UncertainData.StandardizedValue);
                uncertainData.DataFieldY = useLogSpace ? nameof(UncertainData.Log10Value) : nameof(UncertainData.Value);
                uncertainData.DataFieldLowerErrorY = useLogSpace ? nameof(UncertainData.Log10LowerValue) : nameof(UncertainData.LowerValue);
                uncertainData.DataFieldUpperErrorY = useLogSpace ? nameof(UncertainData.Log10UpperValue) : nameof(UncertainData.UpperValue);
                uncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.UncertainSeries.Count > 0) plot.Series.Add(uncertainData);

                // Interval data
                intervalData.ItemsSource = Element.DataFrame.IntervalSeries;
                intervalData.DataFieldX = useLogSpace ? nameof(IntervalData.StandardizedLog10Value) : nameof(IntervalData.StandardizedValue);
                intervalData.DataFieldY = useLogSpace ? nameof(IntervalData.Log10Value) : nameof(IntervalData.Value);
                intervalData.DataFieldLowerErrorY = useLogSpace ? nameof(IntervalData.Log10LowerValue) : nameof(IntervalData.LowerValue);
                intervalData.DataFieldUpperErrorY = useLogSpace ? nameof(IntervalData.Log10UpperValue) : nameof(IntervalData.UpperValue);
                intervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                if (Element.DataFrame.IntervalSeries.Count > 0) plot.Series.Add(intervalData);

                var anno1 = new LineAnnotation()
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
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Top
                };
                plot.Annotations.Add(anno1);

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
            QQWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Updates the ACF (Autocorrelation Function) plot showing serial correlation at various lags.
        /// Displays correlation values with confidence interval bounds.
        /// Requires at least 10 exact data points.
        /// </summary>
        private void UpdateACFPlot()
        {
            if (Element == null) return;
            var plot = Element.ACFPlot;
            if (plot == null) { ACFWarning.Visibility = Visibility.Visible; return; }

            // Look up named series before Clear
            var acfSeries = plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "Autocorrelation")
                ?? new HistogramSeries
                {
                    Name = "Autocorrelation",
                    Title = "Autocorrelation",
                    FillColor = Color.FromArgb(125, 104, 140, 175),
                    StrokeColor = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (Element.DataFrame.ExactSeries.Count < 10)
                {
                    plot.InvalidatePlot(true);
                    ACFWarning.Visibility = Visibility.Visible;
                    return;
                }

                var acfItems = new List<OxyPlot.Series.HistogramItem>();
                var acf = Element.DataFrame.ExactSeries.Autocorrelation();
                for (int i = 0; i < acf.GetLength(0); i++)
                {
                    acfItems.Add(new OxyPlot.Series.HistogramItem(i, i + 1, acf[i, 1]));
                }

                acfSeries.ItemsSource = acfItems;
                acfSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:0.000000}";
                plot.Series.Add(acfSeries);

                var ci = Autocorrelation.CorrelationConfidenceInterval(Element.DataFrame.ExactSeries.Count);
                var anno1 = new LineAnnotation()
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
                    Y = ci[0]
                };

                var anno2 = new LineAnnotation()
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
                    Y = ci[1]
                };

                plot.Annotations.Add(anno1);
                plot.Annotations.Add(anno2);

                foreach (var axis in plot.Axes)
                {
                    if (axis.Key == "Yaxis")
                    {
                        axis.Minimum = Math.Round((Math.Min(ci[0], Tools.Min(acf.GetColumn(1))) - 0.1) * 20) / 20;
                    }
                }

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
            ACFWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Updates the PACF (Partial Autocorrelation Function) plot showing conditional correlation at various lags.
        /// Displays partial correlation values with confidence interval bounds.
        /// Requires at least 10 exact data points.
        /// </summary>
        private void UpdatePACFPlot()
        {
            if (Element == null) return;
            var plot = Element.PACFPlot;
            if (plot == null) { PACFWarning.Visibility = Visibility.Visible; return; }

            // Look up named series before Clear
            var pacfSeries = plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "PartialAutocorrelation")
                ?? new HistogramSeries
                {
                    Name = "PartialAutocorrelation",
                    Title = "Partial Autocorrelation",
                    FillColor = Color.FromArgb(125, 104, 140, 175),
                    StrokeColor = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (Element.DataFrame.ExactSeries.Count < 10)
                {
                    plot.InvalidatePlot(true);
                    PACFWarning.Visibility = Visibility.Visible;
                    return;
                }

                var pacfItems = new List<OxyPlot.Series.HistogramItem>();
                var pacf = Element.DataFrame.ExactSeries.PartialAutocorrelation();
                for (int i = 0; i < pacf.GetLength(0); i++)
                {
                    pacfItems.Add(new OxyPlot.Series.HistogramItem(i, i + 1, pacf[i, 1]));
                }

                pacfSeries.ItemsSource = pacfItems;
                pacfSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:0.000000}";
                plot.Series.Add(pacfSeries);

                var ci = Autocorrelation.CorrelationConfidenceInterval(Element.DataFrame.ExactSeries.Count);
                var anno1 = new LineAnnotation()
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
                    Y = ci[0]
                };

                var anno2 = new LineAnnotation()
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
                    Y = ci[1]
                };

                plot.Annotations.Add(anno1);
                plot.Annotations.Add(anno2);

                foreach (var axis in plot.Axes)
                {
                    if (axis.Key == "Yaxis")
                    {
                        axis.Maximum = Math.Round((Math.Max(ci[1], Tools.Max(pacf.GetColumn(1))) + 0.1) * 20) / 20;
                        axis.Minimum = Math.Round((Math.Min(ci[0], Tools.Min(pacf.GetColumn(1))) - 0.1) * 20) / 20;
                    }
                }

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
            PACFWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Computes and draws all three threshold diagnostic plots (MRL, Modified Scale, Shape).
        /// Requires a time series with at least 20 observations.
        /// </summary>
        /// <remarks>
        /// Callers that invoke this method from a lazy UI path should wrap it in wait-cursor
        /// feedback because the GPD MLE fitting can take noticeable time.
        /// </remarks>
        private void UpdateThresholdDiagnosticsPlots()
        {
            if (ThresholdDiagnosticsTab.Visibility != Visibility.Visible) return;
            if (Element == null) return;

            var ts = Element.TimeSeriesElement?.TimeSeries;
            if (ts == null || ts.Count < 20) return;

            var values = ts.Select(s => s.Value).ToList();
            var sorted = values.OrderBy(v => v).ToArray();
            double uMin = sorted[(int)(sorted.Length * 0.5)];
            double uMax = sorted[sorted.Length - 1];

            _mrlResult = ThresholdDiagnostics.ComputeMeanResidualLife(values, uMin, uMax);
            _stabilityResult = ThresholdDiagnostics.ComputeParameterStability(values, uMin, uMax);

            DrawMRLPlot(UserSettings.ValueStringFormat);
            DrawModifiedScalePlot(UserSettings.ValueStringFormat);
            DrawShapePlot(UserSettings.ValueStringFormat);

            ShowSelectedDiagnosticPlot();
        }

        /// <summary>
        /// Draws the Mean Residual Life plot with confidence interval band, mean excess line,
        /// and current threshold annotation.
        /// </summary>
        /// <param name="valueStringFormat">Format string for data values in tracker tooltips.</param>
        private void DrawMRLPlot(string valueStringFormat = "N4")
        {
            var plot = Element.MRLPlot;
            if (plot == null) return;

            // Look up named series before Clear
            var ciSeries = plot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "MRL_CI")
                ?? new AreaSeries
                {
                    Name = "MRL_CI",
                    Title = "95% CI",
                    Fill = Color.FromArgb(75, 104, 140, 175),
                    Color = Color.FromArgb(255, 53, 59, 122),
                    LineStyle = LineStyle.Solid,
                    StrokeThickness = 1,
                    Decimator = OxyPlot.Decimator.Decimate
                };

            var lineSeries = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "MeanExcess")
                ?? new LineSeries
                {
                    Name = "MeanExcess",
                    Title = "Mean Excess",
                    Color = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 2,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (_mrlResult == null || _mrlResult.Points.Count == 0)
                {
                    plot.InvalidatePlot(true);
                    return;
                }

                // CI band (add first so it renders behind the line)
                ciSeries.ItemsSource = _mrlResult.Points
                    .Select(p => new Point3D(p.Threshold, p.LowerCI, p.UpperCI)).ToList();
                ciSeries.DataFieldX = "X";
                ciSeries.DataFieldY = "Y";
                ciSeries.DataFieldX2 = "X";
                ciSeries.DataFieldY2 = "Z";
                ciSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + valueStringFormat + "}";
                plot.Series.Add(ciSeries);

                // Mean excess line
                lineSeries.ItemsSource = _mrlResult.Points
                    .Select(p => new DataPoint(p.Threshold, p.MeanExcess)).ToList();
                lineSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + valueStringFormat + "}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                plot.Series.Add(lineSeries);

                AddThresholdAnnotation(plot);

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        /// <summary>
        /// Draws the modified scale parameter stability plot with confidence interval band,
        /// parameter line, and current threshold annotation.
        /// </summary>
        /// <param name="valueStringFormat">Format string for data values in tracker tooltips.</param>
        private void DrawModifiedScalePlot(string valueStringFormat = "N4")
        {
            var plot = Element.ModifiedScalePlot;
            if (plot == null) return;

            // Look up named series before Clear
            var ciSeries = plot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "ModifiedScale_CI")
                ?? new AreaSeries
                {
                    Name = "ModifiedScale_CI",
                    Title = "95% CI",
                    Fill = Color.FromArgb(75, 104, 140, 175),
                    Color = Color.FromArgb(255, 53, 59, 122),
                    LineStyle = LineStyle.Solid,
                    StrokeThickness = 1,
                    Decimator = OxyPlot.Decimator.Decimate
                };

            var lineSeries = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "ModifiedScale")
                ?? new LineSeries
                {
                    Name = "ModifiedScale",
                    Title = "Modified Scale",
                    Color = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 2,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (_stabilityResult == null || _stabilityResult.Points.Count == 0)
                {
                    plot.InvalidatePlot(true);
                    return;
                }

                // CI band
                ciSeries.ItemsSource = _stabilityResult.Points
                    .Select(p => new Point3D(p.Threshold, p.ModifiedScaleLowerCI, p.ModifiedScaleUpperCI)).ToList();
                ciSeries.DataFieldX = "X";
                ciSeries.DataFieldY = "Y";
                ciSeries.DataFieldX2 = "X";
                ciSeries.DataFieldY2 = "Z";
                ciSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + valueStringFormat + "}";
                plot.Series.Add(ciSeries);

                // Modified scale line
                lineSeries.ItemsSource = _stabilityResult.Points
                    .Select(p => new DataPoint(p.Threshold, p.ModifiedScale)).ToList();
                lineSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + valueStringFormat + "}" + Environment.NewLine + "{3}: {4:" + valueStringFormat + "}";
                plot.Series.Add(lineSeries);

                AddThresholdAnnotation(plot);

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        /// <summary>
        /// Draws the shape parameter stability plot with confidence interval band,
        /// parameter line, and current threshold annotation.
        /// </summary>
        /// <param name="valueStringFormat">Format string for data values in tracker tooltips.</param>
        private void DrawShapePlot(string valueStringFormat = "N4")
        {
            var plot = Element.ShapePlot;
            if (plot == null) return;

            // Look up named series before Clear
            var ciSeries = plot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "Shape_CI")
                ?? new AreaSeries
                {
                    Name = "Shape_CI",
                    Title = "95% CI",
                    Fill = Color.FromArgb(75, 104, 140, 175),
                    Color = Color.FromArgb(255, 53, 59, 122),
                    LineStyle = LineStyle.Solid,
                    StrokeThickness = 1,
                    Decimator = OxyPlot.Decimator.Decimate
                };

            var lineSeries = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "Shape")
                ?? new LineSeries
                {
                    Name = "Shape",
                    Title = "Shape",
                    Color = Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 2,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (_stabilityResult == null || _stabilityResult.Points.Count == 0)
                {
                    plot.InvalidatePlot(true);
                    return;
                }

                // CI band
                ciSeries.ItemsSource = _stabilityResult.Points
                    .Select(p => new Point3D(p.Threshold, p.ShapeLowerCI, p.ShapeUpperCI)).ToList();
                ciSeries.DataFieldX = "X";
                ciSeries.DataFieldY = "Y";
                ciSeries.DataFieldX2 = "X";
                ciSeries.DataFieldY2 = "Z";
                ciSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + valueStringFormat + "}";
                plot.Series.Add(ciSeries);

                // Shape line
                lineSeries.ItemsSource = _stabilityResult.Points
                    .Select(p => new DataPoint(p.Threshold, p.Shape)).ToList();
                lineSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:" + valueStringFormat + "}" + Environment.NewLine + "{3}: {4:0.000000}";
                plot.Series.Add(lineSeries);

                AddThresholdAnnotation(plot);

                plot.InvalidatePlot(true);
            }
            Element.RebuildSeriesAndAnnotationBridges(plot);
        }

        /// <summary>
        /// Adds a red dashed vertical line annotation at the current threshold value to the specified plot.
        /// </summary>
        /// <param name="plot">The OxyPlot plot control to add the annotation to.</param>
        private void AddThresholdAnnotation(Plot plot)
        {
            if (Element.Threshold > 0)
            {
                plot.Annotations.Add(new LineAnnotation()
                {
                    Type = OxyPlot.Annotations.LineAnnotationType.Vertical,
                    X = Element.Threshold,
                    Color = Colors.Red,
                    TextColor = Colors.Red,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 1.5,
                    Text = "Threshold",
                    TextLinePosition = 0.95
                });
            }
        }

        /// <summary>
        /// Converts a hex color string to a <see cref="Color"/>.
        /// Supports formats: #AARRGGBB, #RRGGBB.
        /// </summary>
        /// <param name="hex">The hex color string.</param>
        /// <returns>The parsed color.</returns>
        private static Color ColorFromHex(string hex)
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }

        /// <summary>
        /// Handles the Checked event for the threshold diagnostic radio buttons.
        /// Shows the selected plot and hides the others.
        /// </summary>
        /// <param name="sender">The radio button that was checked.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void ThresholdDiagnosticRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (Element != null && ThresholdDiagnosticsTab?.IsSelected == true && _thresholdDiagnosticsDirty)
            {
                UpdateLazyContentWithWaitCursor(UpdateThresholdDiagnosticsPlots, () => _thresholdDiagnosticsDirty = false);
            }

            ShowSelectedDiagnosticPlot();
        }

        /// <summary>
        /// Shows the diagnostic plot corresponding to the currently selected radio button
        /// and collapses the other two. Also rebinds the toolbar to the visible plot.
        /// </summary>
        private void ShowSelectedDiagnosticPlot()
        {
            // Guard against calls during XAML initialization when controls are not yet created
            if (MRLPlotHost == null || ModifiedScalePlotRadioButton == null || ShapePlotRadioButton == null) return;

            MRLPlotHost.Visibility = MRLPlotRadioButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ModifiedScalePlotHost.Visibility = ModifiedScalePlotRadioButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ShapePlotHost.Visibility = ShapePlotRadioButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // Rebind toolbar to the currently visible plot
            if (MRLPlotRadioButton.IsChecked == true)
                ThresholdDiagnosticsToolbar.Plot = MRLPlot;
            else if (ModifiedScalePlotRadioButton.IsChecked == true)
                ThresholdDiagnosticsToolbar.Plot = ModifiedScalePlot;
            else
                ThresholdDiagnosticsToolbar.Plot = ShapePlot;

            InvalidateSelectedDiagnosticPlot();
        }

        /// <summary>
        /// Gets the threshold diagnostic plot selected by the diagnostic radio buttons.
        /// </summary>
        /// <returns>The selected threshold diagnostic plot, or null when the controls are not ready.</returns>
        private Plot GetSelectedDiagnosticPlot()
        {
            if (MRLPlotRadioButton?.IsChecked == true) return MRLPlot;
            if (ModifiedScalePlotRadioButton?.IsChecked == true) return ModifiedScalePlot;
            if (ShapePlotRadioButton?.IsChecked == true) return ShapePlot;
            return null;
        }

        /// <summary>
        /// Invalidates the selected threshold diagnostic plot after its host becomes visible.
        /// </summary>
        /// <remarks>
        /// OxyPlot can skip rendering a plot that was invalidated while its host was collapsed.
        /// Scheduling another invalidation after the visibility switch avoids a blank plot until
        /// the user triggers an unrelated toolbar action.
        /// </remarks>
        private void InvalidateSelectedDiagnosticPlot()
        {
            Plot selectedPlot = GetSelectedDiagnosticPlot();
            if (selectedPlot == null) return;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                selectedPlot.InvalidatePlot(true);
            }));
        }

        #endregion

        #region Exact Data Grid

        /// <summary>
        /// Binds the exact data grid to the Element's exact data series.
        /// Configures read-only or editable mode based on the data entry method.
        /// </summary>
        private void BindExactDataGrid()
        {
            _suppressUIUpdate = true;
            Element.DataFrame.ExactSeries.SuppressCollectionChanged = true;

            // Clear ordinates
            for (int i = ExactDataOrdinates.Count - 1; i >= 0; i--)
            {
                var rowItem = (ExactDataRowItem)ExactDataOrdinates[i];
                rowItem.PropertyChanged -= ExactDataRowItem_PropertyChanged;
                ExactDataOrdinates.RemoveAt(i);
            }

            // Clear existing bindings
            ExactDataGridToolBar.DataGrid = null;
            ExactDataGrid.ItemsSource = null;

            // Check if manual entry or not
            bool showDateTime = Element.ExactDataMethod != InputData.ExactDataEntryType.Manual;
            if (Element.ExactDataMethod == InputData.ExactDataEntryType.Manual)
            {
                ExactDataGrid.IsReadOnly = false;
                ExactDataGrid.CanUserAddInsertDeleteRows = true;
                _isExactDataReadOnly = false;
            }
            else
            {
                ExactDataGrid.IsReadOnly = true;
                ExactDataGrid.CanUserAddInsertDeleteRows = false;
                _isExactDataReadOnly = true;
            }

            // Add dummy rows
            ExactDataOrdinates.Add(new ExactDataRowItem(ExactDataOrdinates, new ExactData(), Element.DataFrame.ExactSeries, showDateTime));

            // Add all data items
            for (int i = 0; i < Element.DataFrame.ExactSeries.Count; i++)
            {
                var data = (ExactData)Element.DataFrame.ExactSeries[i];
                var rowItem = new ExactDataRowItem(ExactDataOrdinates, data, Element.DataFrame.ExactSeries, showDateTime);
                rowItem.PropertyChanged += ExactDataRowItem_PropertyChanged;
                ExactDataOrdinates.Add(rowItem);
            }

            // Bind data grids
            ExactDataGridToolBar.DataGrid = ExactDataGrid;
            ExactDataGrid.ItemsSource = ExactDataOrdinates;
            ExactDataGrid.Items.Refresh();

            Element.DataFrame.ExactSeries.SuppressCollectionChanged = false;
            _suppressUIUpdate = false;
        }

        /// <summary>
        /// Handles the AutoGeneratedColumns event for the exact data grid.
        /// Removes the dummy row after columns have been generated.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args for the event.</param>
        private void ExactDataGrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            // Remove dummy row.
            ExactDataOrdinates.RemoveAt(0);
        }

        /// <summary>
        /// Handles the AutoGeneratingColumn event for the exact data grid.
        /// Configures column formatting, width, and styles based on property names.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing column generation details.</param>
        private void ExactDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == nameof(ExactDataRowItem.Index))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.Width = new DataGridLength(0.25, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "F0";
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource(_isExactDataReadOnly ? "Center_ReadOnly_CellStyle" : "Center_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            if (e.PropertyName == nameof(ExactDataRowItem.DateTime))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.Width = new DataGridLength(0.25, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "g";
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource(_isExactDataReadOnly ? "Center_ReadOnly_CellStyle" : "Center_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(ExactDataRowItem.Value))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.Width = new DataGridLength(0.25, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "{0:" + UserSettings.ValueStringFormat + "}";
                ((Binding)((DataGridTextColumn)e.Column).Binding).Converter = new DoubleToNAConverter();
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource(_isExactDataReadOnly ? "Right_ReadOnly_CellStyle" : "Right_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(ExactDataRowItem.PlottingPosition))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.IsReadOnly = true;
                e.Column.Width = new DataGridLength(0.25, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "F6";
                ((Binding)((DataGridTextColumn)e.Column).Binding).Converter = new DoubleToNAConverter();
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Right_ReadOnly_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(ExactDataRowItem.IsLowOutlier))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                e.Column.IsReadOnly = true;
                ((DataGridCheckBoxColumn)e.Column).CellStyle = (Style)FindResource("Disabled_CellStyle");
                ((DataGridCheckBoxColumn)e.Column).ElementStyle = (Style)FindResource(typeof(CheckBox));
                ((DataGridCheckBoxColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
        }

        /// <summary>
        /// Handles the PreviewAddRows event for the exact data grid.
        /// Creates new exact data rows and adds them to the collection and ordinate list.
        /// </summary>
        /// <remarks>
        /// Suppresses CollectionChanged during the bulk insert to prevent the bridge from
        /// recording individual Add actions. A single Reset fires afterward so that the bridge
        /// records one undo action for the entire add operation.
        /// </remarks>
        /// <param name="startRowIndex">The index where new rows should be inserted.</param>
        /// <param name="nRows">The number of rows to add.</param>
        /// <param name="cancelAddRows">Reference to a boolean indicating whether to cancel the add operation.</param>
        private void ExactDataGrid_PreviewAddRows(int startRowIndex, int nRows, ref bool cancelAddRows)
        {
            cancelAddRows = true;
            bool showDateTime = Element.ExactDataMethod != InputData.ExactDataEntryType.Manual;

            _suppressUIUpdate = true;
            bool wasSuppressed = Element.DataFrame.ExactSeries.SuppressCollectionChanged;
            Element.DataFrame.ExactSeries.SuppressCollectionChanged = true;

            for (int i = startRowIndex; i < startRowIndex + nRows; i++)
            {
                var data = new ExactData();
                Element.DataFrame.ExactSeries.Insert(i, data);
                var rowItem = new ExactDataRowItem(ExactDataOrdinates, data, Element.DataFrame.ExactSeries, showDateTime);
                rowItem.PropertyChanged += ExactDataRowItem_PropertyChanged;
                ExactDataOrdinates.Insert(i, rowItem);
            }

            Element.DataFrame.ExactSeries.SuppressCollectionChanged = wasSuppressed;
            // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
            if (!wasSuppressed)
            {
                Element.DataFrame.ExactSeries.RaiseCollectionChangedReset();
            }
            _suppressUIUpdate = false;
            if (!wasSuppressed && _isLoaded)
            {
                MarkAllDirty();
                ExactDataGrid.ValidateTable();
                ExactDataGrid.Items.Refresh();
                // Disable undo during plot updates so Series.Clear/Add don't record as undo entries
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles the PreviewDeleteRows event for the exact data grid.
        /// Removes selected rows from both the data series and ordinate collection.
        /// Handles sorted views by mapping display indices to underlying collection indices.
        /// </summary>
        /// <remarks>
        /// Suppresses CollectionChanged during bulk removal to prevent the bridge from
        /// recording individual Remove actions. A single Reset fires afterward so that the bridge
        /// records one undo action for the entire delete operation.
        /// </remarks>
        /// <param name="rowindices">List of row indices to delete.</param>
        /// <param name="cancel">Reference to a boolean indicating whether to cancel the delete operation.</param>
        private void ExactDataGrid_PreviewDeleteRows(List<int> rowindices, ref bool cancel)
        {
            cancel = true;
            rowindices.Sort();

            // Get the underlying item collection from the ItemsSource
            ICollectionView cv = CollectionViewSource.GetDefaultView(ExactDataGrid.ItemsSource);
            IList itemList = null;
            if (cv is CollectionView collectionView && collectionView.SourceCollection is IList list)
            {
                itemList = list;
            }
            else
            {
                itemList = ExactDataGrid.ItemsSource as IList;
            }

            // Build a list of the items in the current sort order
            List<object> rowList = new List<object>();
            IEnumerator sortedList = CollectionViewSource.GetDefaultView(itemList).GetEnumerator();
            for (int i = 0; i < itemList.Count; i++)
            {
                if (!sortedList.MoveNext())
                    break;
                rowList.Add(sortedList.Current);
            }

            // For each unique row (in sorted order), determine its index in the underlying collection
            List<int> uniqueSortedRows = new List<int>();
            foreach (int rowIndex in rowindices)
            {
                int index = itemList.IndexOf(rowList[rowIndex]);
                uniqueSortedRows.Add(index);
            }
            // Sort the underlying indices
            uniqueSortedRows.Sort();

            // Suppress collection changed during bulk removal to prevent mid-loop
            // CollectionChanged handler ? BindExactDataGrid ? ExactDataOrdinates rebuild ? index misalignment.
            _suppressUIUpdate = true;
            bool wasSuppressed = Element.DataFrame.ExactSeries.SuppressCollectionChanged;
            Element.DataFrame.ExactSeries.SuppressCollectionChanged = true;

            // Remove the items starting from the highest index so that removals do not affect remaining indices
            for (int i = uniqueSortedRows.Count - 1; i >= 0; i--)
            {
                Element.DataFrame.ExactSeries.RemoveAt(uniqueSortedRows[i]);
                var rowItem = (ExactDataRowItem)ExactDataOrdinates[uniqueSortedRows[i]];
                rowItem.PropertyChanged -= ExactDataRowItem_PropertyChanged;
                ExactDataOrdinates.RemoveAt(uniqueSortedRows[i]);
            }

            Element.DataFrame.ExactSeries.SuppressCollectionChanged = wasSuppressed;
            // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
            if (!wasSuppressed)
            {
                Element.DataFrame.ExactSeries.RaiseCollectionChangedReset();
            }
            _suppressUIUpdate = false;
            if (!wasSuppressed && _isLoaded)
            {
                MarkAllDirty();
                ExactDataGrid.ValidateTable();
                ExactDataGrid.Items.Refresh();
                // Disable undo during plot updates so Series.Clear/Add don't record as undo entries
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles the PreviewPasteData event for the exact data grid.
        /// Suppresses collection changed events during the paste operation so that
        /// individual cell edits and row additions do not fire intermediate events.
        /// </summary>
        /// <param name="clipboardData">The clipboard data being pasted.</param>
        /// <param name="cancelPaste">Reference to a boolean indicating whether to cancel the paste operation.</param>
        private void ExactDataGrid_PreviewPasteData(string[][] clipboardData, ref bool cancelPaste)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _suppressUIUpdate = true;
            Element.DataFrame.ExactSeries.SuppressCollectionChanged = true;
        }

        /// <summary>
        /// Handles the DataPasted event for the exact data grid.
        /// Re-enables collection changed events and raises a single Reset so that the bridge
        /// records one undo action for the entire paste. Validates and refreshes the control.
        /// </summary>
        private void ExactDataGrid_DataPasted()
        {
            try
            {
                // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
                Element.DataFrame.ExactSeries.SuppressCollectionChanged = false;
                Element.DataFrame.ExactSeries.RaiseCollectionChangedReset();
                _suppressUIUpdate = false;

                // Full resync needed after paste (grid modified cells + added rows)
                BindExactDataGrid();
                MarkAllDirty();
                ExactDataGrid.ValidateTable();

                // Disable undo during plot updates so Series.Clear/Add don't record as undo entries
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
            finally
            {
                _suppressUIUpdate = false;
                Element.DataFrame.ExactSeries.SuppressCollectionChanged = false;
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Handles the MouseDown event for the exact data grid.
        /// Commits edits and clears selection when clicking in empty space below rows.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void ExactDataGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Exit if not clicked in empty space below bottom row.
            if (e.OriginalSource.GetType() != typeof(ScrollViewer)) { return; }
            // Commit edits
            ExactDataGrid.CommitEdit();
            // Clear Selection
            ExactDataGrid.UnselectAllCells();
            // Move focus to force validation redraw
            ExactDataGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        #endregion

        #region Uncertain Data Grid

        /// <summary>
        /// Binds the uncertain data grid to the Element's uncertain data series.
        /// Configures the grid for editable uncertain data with probability distributions.
        /// </summary>
        private void BindUncertainDataGrid()
        {
            _suppressUIUpdate = true;
            Element.DataFrame.UncertainSeries.SuppressCollectionChanged = true;

            // Unsubscribe PropertyChanged from old RowItems to prevent memory leaks
            for (int i = UncertainDataOrdinates.Count - 1; i >= 0; i--)
            {
                var rowItem = (UncertainDataRowItem)UncertainDataOrdinates[i];
                rowItem.PropertyChanged -= UncertainDataRowItem_PropertyChanged;
                UncertainDataOrdinates.RemoveAt(i);
            }

            // Clear existing bindings
            UncertainDataGridToolBar.DataGrid = null;
            UncertainDataGrid.ItemsSource = null;

            // Add all data items
            for (int i = 0; i < Element.DataFrame.UncertainSeries.Count; i++)
            {
                var data = (UncertainData)Element.DataFrame.UncertainSeries[i];
                var rowItem = new UncertainDataRowItem(UncertainDataOrdinates, data, Element.DataFrame, Element.DataFrame.UncertainSeries);
                rowItem.PropertyChanged += UncertainDataRowItem_PropertyChanged;
                UncertainDataOrdinates.Add(rowItem);
            }

            // Bind data grids
            UncertainDataGridToolBar.DataGrid = UncertainDataGrid;
            UncertainDataGrid.ItemsSource = UncertainDataOrdinates;
            UncertainDataGrid.Items.Refresh();

            Element.DataFrame.UncertainSeries.SuppressCollectionChanged = false;
            _suppressUIUpdate = false;
        }

        /// <summary>
        /// Handles the AutoGeneratedColumns event for the uncertain data grid.
        /// Placeholder for post-generation column configuration.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args for the event.</param>
        private void UncertainDataGrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            // Remove dummy row.
            //UncertainDataOrdinates.RemoveAt(0);
        }

        /// <summary>
        /// Handles the PreviewAddRows event for the uncertain data grid.
        /// Creates new uncertain data rows with default distributions.
        /// </summary>
        /// <param name="startRowIndex">The index where new rows should be inserted.</param>
        /// <param name="nRows">The number of rows to add.</param>
        /// <param name="cancelAddRows">Reference to a boolean indicating whether to cancel the add operation.</param>
        private void UncertainDataGrid_PreviewAddRows(int startRowIndex, int nRows, ref bool cancelAddRows)
        {
            cancelAddRows = true;

            _suppressUIUpdate = true;
            bool wasSuppressed = Element.DataFrame.UncertainSeries.SuppressCollectionChanged;
            Element.DataFrame.UncertainSeries.SuppressCollectionChanged = true;

            for (int i = startRowIndex; i < startRowIndex + nRows; i++)
            {
                var data = new UncertainData();
                Element.DataFrame.UncertainSeries.Insert(i, data);
                var rowItem = new UncertainDataRowItem(UncertainDataOrdinates, data, Element.DataFrame, Element.DataFrame.UncertainSeries);
                rowItem.PropertyChanged += UncertainDataRowItem_PropertyChanged;
                UncertainDataOrdinates.Insert(i, rowItem);
            }

            Element.DataFrame.UncertainSeries.SuppressCollectionChanged = wasSuppressed;
            // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
            if (!wasSuppressed)
            {
                Element.DataFrame.UncertainSeries.RaiseCollectionChangedReset();
            }
            _suppressUIUpdate = false;
            if (!wasSuppressed && _isLoaded)
            {
                MarkDataFrameDirty();
                UncertainDataGrid.ValidateTable();
                UncertainDataGrid.Items.Refresh();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles the <c>PreviewDeleteRows</c> event for <c>UncertainDataGrid</c>.
        /// </summary>
        /// <param name="rowindices">The row indices affected by the operation.</param>
        /// <param name="cancel">Set to <see langword="true"/> to cancel the operation.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void UncertainDataGrid_PreviewDeleteRows(List<int> rowindices, ref bool cancel)
        {
            cancel = true;
            rowindices.Sort();

            // Get the underlying item collection from the ItemsSource
            ICollectionView cv = CollectionViewSource.GetDefaultView(UncertainDataGrid.ItemsSource);
            IList itemList = null;
            if (cv is CollectionView collectionView && collectionView.SourceCollection is IList list)
            {
                itemList = list;
            }
            else
            {
                itemList = UncertainDataGrid.ItemsSource as IList;
            }

            // Build a list of the items in the current sort order
            List<object> rowList = new List<object>();
            IEnumerator sortedList = CollectionViewSource.GetDefaultView(itemList).GetEnumerator();
            for (int i = 0; i < itemList.Count; i++)
            {
                if (!sortedList.MoveNext())
                    break;
                rowList.Add(sortedList.Current);
            }

            // For each unique row (in sorted order), determine its index in the underlying collection
            List<int> uniqueSortedRows = new List<int>();
            foreach (int rowIndex in rowindices)
            {
                int index = itemList.IndexOf(rowList[rowIndex]);
                uniqueSortedRows.Add(index);
            }
            // Sort the underlying indices
            uniqueSortedRows.Sort();

            // Suppress collection changed during bulk removal to prevent mid-loop
            // CollectionChanged handler ? BindUncertainDataGrid ? UncertainDataOrdinates rebuild ? index misalignment.
            _suppressUIUpdate = true;
            bool wasSuppressed = Element.DataFrame.UncertainSeries.SuppressCollectionChanged;
            Element.DataFrame.UncertainSeries.SuppressCollectionChanged = true;

            // Remove the items starting from the highest index so that removals do not affect remaining indices
            for (int i = uniqueSortedRows.Count - 1; i >= 0; i--)
            {
                Element.DataFrame.UncertainSeries.RemoveAt(uniqueSortedRows[i]);
                var rowItem = (UncertainDataRowItem)UncertainDataOrdinates[uniqueSortedRows[i]];
                rowItem.PropertyChanged -= UncertainDataRowItem_PropertyChanged;
                UncertainDataOrdinates.RemoveAt(uniqueSortedRows[i]);
            }

            Element.DataFrame.UncertainSeries.SuppressCollectionChanged = wasSuppressed;
            // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
            if (!wasSuppressed)
            {
                Element.DataFrame.UncertainSeries.RaiseCollectionChangedReset();
            }
            _suppressUIUpdate = false;
            if (!wasSuppressed && _isLoaded)
            {
                MarkDataFrameDirty();
                UncertainDataGrid.ValidateTable();
                UncertainDataGrid.Items.Refresh();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles the <c>PreviewPasteData</c> event for <c>UncertainDataGrid</c>.
        /// </summary>
        /// <param name="clipboardData">The pasted clipboard cells.</param>
        /// <param name="cancelPaste">Set to <see langword="true"/> to cancel the operation.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void UncertainDataGrid_PreviewPasteData(string[][] clipboardData, ref bool cancelPaste)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _suppressUIUpdate = true;
            Element.DataFrame.UncertainSeries.SuppressCollectionChanged = true;
        }

        /// <summary>
        /// Handles the <c>DataPasted</c> event for <c>UncertainDataGrid</c>.
        /// </summary>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void UncertainDataGrid_DataPasted()
        {
            try
            {
                // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
                Element.DataFrame.UncertainSeries.SuppressCollectionChanged = false;
                Element.DataFrame.UncertainSeries.RaiseCollectionChangedReset();
                _suppressUIUpdate = false;

                // Full resync needed after paste
                BindUncertainDataGrid();
                MarkDataFrameDirty();
                UncertainDataGrid.ValidateTable();

                // Disable undo during plot updates so Series.Clear/Add don't record as undo entries
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
            finally
            {
                _suppressUIUpdate = false;
                Element.DataFrame.UncertainSeries.SuppressCollectionChanged = false;
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Handles the <c>MouseDown</c> event for <c>UncertainDataGrid</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void UncertainDataGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Exit if not clicked in empty space below bottom row.
            if (e.OriginalSource.GetType() != typeof(ScrollViewer)) { return; }
            // Commit edits
            UncertainDataGrid.CommitEdit();
            // Clear Selection
            UncertainDataGrid.UnselectAllCells();
            // Move focus to force validation redraw
            UncertainDataGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        /// <summary>
        /// Handles the Opened event for the distribution selection popup.
        /// Initializes the distribution selector with available distributions and current selection.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args for the event.</param>
        private void DistributionPopup_Opened(object sender, EventArgs e)
        {
            if (Element == null) return;
            Popup p = (Popup)sender;
            if (p.Child == null) return;
            var b = (Border)p.Child;
            DistributionSelectorControl dsc = (DistributionSelectorControl)b.Child;
            UnivariateDistributionBase dist = null;
            if (p.DataContext.GetType() == typeof(UncertainDataRowItem))
            {
                dist = ((UncertainDataRowItem)p.DataContext).Distribution.Clone();
            }

            var PriorDistributionsList = new List<UnivariateDistributionBase>()
            {
              new GammaDistribution(),
              new GeneralizedBeta(),
              new LnNormal(),
              new LogNormal(),
              new Normal(),
              new Pert(),
              new StudentT(),
              new Triangular(),
              new TruncatedNormal(),
              new Uniform()
            };

            dsc.Distributions = PriorDistributionsList;
            dsc.SelectedDistribution = dist;
        }

        /// <summary>
        /// Handles the Closed event for the distribution selection popup.
        /// Updates the uncertain data row's distribution with the selected distribution if valid.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args for the event.</param>
        private void DistributionPopup_Closed(object sender, EventArgs e)
        {
            if (Element == null) return;
            Popup p = (Popup)sender;
            if (p.Child == null) return;
            var b = (Border)p.Child;
            DistributionSelectorControl dsc = (DistributionSelectorControl)b.Child;
            if (p.DataContext.GetType() == typeof(UncertainDataRowItem) && dsc.SelectedDistribution.ParametersValid == true)
            {
                ((UncertainDataRowItem)p.DataContext).Distribution = dsc.SelectedDistribution.Clone();
            }
            dsc.SelectedDistribution = null;
        }

        #endregion

        #region Interval Data Grid

        /// <summary>
        /// Binds the interval data grid to the Element's interval data series.
        /// Configures the grid for editable interval data with lower and upper bounds.
        /// </summary>
        private void BindIntervalDataGrid()
        {
            _suppressUIUpdate = true;
            Element.DataFrame.IntervalSeries.SuppressCollectionChanged = true;

            // Unsubscribe PropertyChanged from old RowItems to prevent memory leaks
            for (int i = IntervalDataOrdinates.Count - 1; i >= 0; i--)
            {
                var rowItem = (IntervalDataRowItem)IntervalDataOrdinates[i];
                rowItem.PropertyChanged -= IntervalDataRowItem_PropertyChanged;
                IntervalDataOrdinates.RemoveAt(i);
            }

            // Clear existing bindings
            IntervalDataGridToolBar.DataGrid = null;
            IntervalDataGrid.ItemsSource = null;

            // Add dummy rows
            IntervalDataOrdinates.Add(new IntervalDataRowItem(IntervalDataOrdinates, new IntervalData(), Element.DataFrame, Element.DataFrame.IntervalSeries));

            // Add all data items
            for (int i = 0; i < Element.DataFrame.IntervalSeries.Count; i++)
            {
                var data = (IntervalData)Element.DataFrame.IntervalSeries[i];
                var rowItem = new IntervalDataRowItem(IntervalDataOrdinates, data, Element.DataFrame, Element.DataFrame.IntervalSeries);
                rowItem.PropertyChanged += IntervalDataRowItem_PropertyChanged;
                IntervalDataOrdinates.Add(rowItem);
            }

            // Bind data grids
            IntervalDataGridToolBar.DataGrid = IntervalDataGrid;
            IntervalDataGrid.ItemsSource = IntervalDataOrdinates;
            IntervalDataGrid.Items.Refresh();

            Element.DataFrame.IntervalSeries.SuppressCollectionChanged = false;
            _suppressUIUpdate = false;
        }

        /// <summary>
        /// Handles the <c>AutoGeneratedColumns</c> event for <c>IntervalDataGrid</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void IntervalDataGrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            // Remove dummy row. 
            IntervalDataOrdinates.RemoveAt(0);
        }

        /// <summary>
        /// Handles the <c>AutoGeneratingColumn</c> event for <c>IntervalDataGrid</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void IntervalDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == nameof(IntervalDataRowItem.Index))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.Width = new DataGridLength(0.2, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "F0";
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Center_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(IntervalDataRowItem.LowerValue))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.Width = new DataGridLength(0.2, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "{0:" + UserSettings.ValueStringFormat + "}";
                ((Binding)((DataGridTextColumn)e.Column).Binding).Converter = new DoubleToNAConverter();
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Right_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(IntervalDataRowItem.Value))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.Width = new DataGridLength(0.2, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "{0:" + UserSettings.ValueStringFormat + "}";
                ((Binding)((DataGridTextColumn)e.Column).Binding).Converter = new DoubleToNAConverter();
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Right_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(IntervalDataRowItem.UpperValue))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.Width = new DataGridLength(0.2, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "{0:" + UserSettings.ValueStringFormat + "}";
                ((Binding)((DataGridTextColumn)e.Column).Binding).Converter = new DoubleToNAConverter();
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Right_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(IntervalDataRowItem.PlottingPosition))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 150;
                e.Column.IsReadOnly = true;
                e.Column.Width = new DataGridLength(0.2, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "F6";
                ((Binding)((DataGridTextColumn)e.Column).Binding).Converter = new DoubleToNAConverter();
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Right_ReadOnly_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }

        }

        /// <summary>
        /// Handles the <c>PreviewAddRows</c> event for <c>IntervalDataGrid</c>.
        /// </summary>
        /// <param name="startRowIndex">The first row index affected by the operation.</param>
        /// <param name="nRows">The number of rows affected by the operation.</param>
        /// <param name="cancelAddRows">Set to <see langword="true"/> to cancel the operation.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void IntervalDataGrid_PreviewAddRows(int startRowIndex, int nRows, ref bool cancelAddRows)
        {
            cancelAddRows = true;

            _suppressUIUpdate = true;
            bool wasSuppressed = Element.DataFrame.IntervalSeries.SuppressCollectionChanged;
            Element.DataFrame.IntervalSeries.SuppressCollectionChanged = true;

            for (int i = startRowIndex; i < startRowIndex + nRows; i++)
            {
                var data = new IntervalData();
                Element.DataFrame.IntervalSeries.Insert(i, data);
                var rowItem = new IntervalDataRowItem(IntervalDataOrdinates, data, Element.DataFrame, Element.DataFrame.IntervalSeries);
                rowItem.PropertyChanged += IntervalDataRowItem_PropertyChanged;
                IntervalDataOrdinates.Insert(i, rowItem);
            }

            Element.DataFrame.IntervalSeries.SuppressCollectionChanged = wasSuppressed;
            // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
            if (!wasSuppressed)
            {
                Element.DataFrame.IntervalSeries.RaiseCollectionChangedReset();
            }
            _suppressUIUpdate = false;
            if (!wasSuppressed && _isLoaded)
            {
                MarkDataFrameDirty();
                IntervalDataGrid.ValidateTable();
                IntervalDataGrid.Items.Refresh();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles the <c>PreviewDeleteRows</c> event for <c>IntervalDataGrid</c>.
        /// </summary>
        /// <param name="rowindices">The row indices affected by the operation.</param>
        /// <param name="cancel">Set to <see langword="true"/> to cancel the operation.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void IntervalDataGrid_PreviewDeleteRows(List<int> rowindices, ref bool cancel)
        {
            cancel = true;
            rowindices.Sort();

            // Get the underlying item collection from the ItemsSource
            ICollectionView cv = CollectionViewSource.GetDefaultView(IntervalDataGrid.ItemsSource);
            IList itemList = null;
            if (cv is CollectionView collectionView && collectionView.SourceCollection is IList list)
            {
                itemList = list;
            }
            else
            {
                itemList = IntervalDataGrid.ItemsSource as IList;
            }

            // Build a list of the items in the current sort order
            List<object> rowList = new List<object>();
            IEnumerator sortedList = CollectionViewSource.GetDefaultView(itemList).GetEnumerator();
            for (int i = 0; i < itemList.Count; i++)
            {
                if (!sortedList.MoveNext())
                    break;
                rowList.Add(sortedList.Current);
            }

            // For each unique row (in sorted order), determine its index in the underlying collection
            List<int> uniqueSortedRows = new List<int>();
            foreach (int rowIndex in rowindices)
            {
                int index = itemList.IndexOf(rowList[rowIndex]);
                uniqueSortedRows.Add(index);
            }
            // Sort the underlying indices
            uniqueSortedRows.Sort();

            // Suppress collection changed during bulk removal to prevent mid-loop
            // CollectionChanged handler ? BindIntervalDataGrid ? IntervalDataOrdinates rebuild ? index misalignment.
            _suppressUIUpdate = true;
            bool wasSuppressed = Element.DataFrame.IntervalSeries.SuppressCollectionChanged;
            Element.DataFrame.IntervalSeries.SuppressCollectionChanged = true;

            // Remove the items starting from the highest index so that removals do not affect remaining indices
            for (int i = uniqueSortedRows.Count - 1; i >= 0; i--)
            {
                Element.DataFrame.IntervalSeries.RemoveAt(uniqueSortedRows[i]);
                var rowItem = (IntervalDataRowItem)IntervalDataOrdinates[uniqueSortedRows[i]];
                rowItem.PropertyChanged -= IntervalDataRowItem_PropertyChanged;
                IntervalDataOrdinates.RemoveAt(uniqueSortedRows[i]);
            }

            Element.DataFrame.IntervalSeries.SuppressCollectionChanged = wasSuppressed;
            // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
            if (!wasSuppressed)
            {
                Element.DataFrame.IntervalSeries.RaiseCollectionChangedReset();
            }
            _suppressUIUpdate = false;
            if (!wasSuppressed && _isLoaded)
            {
                MarkDataFrameDirty();
                IntervalDataGrid.ValidateTable();
                IntervalDataGrid.Items.Refresh();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles the <c>PreviewPasteData</c> event for <c>IntervalDataGrid</c>.
        /// </summary>
        /// <param name="clipboardData">The pasted clipboard cells.</param>
        /// <param name="cancelPaste">Set to <see langword="true"/> to cancel the operation.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void IntervalDataGrid_PreviewPasteData(string[][] clipboardData, ref bool cancelPaste)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _suppressUIUpdate = true;
            Element.DataFrame.IntervalSeries.SuppressCollectionChanged = true;
        }

        /// <summary>
        /// Handles the <c>DataPasted</c> event for <c>IntervalDataGrid</c>.
        /// </summary>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void IntervalDataGrid_DataPasted()
        {
            try
            {
                // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
                Element.DataFrame.IntervalSeries.SuppressCollectionChanged = false;
                Element.DataFrame.IntervalSeries.RaiseCollectionChangedReset();
                _suppressUIUpdate = false;

                // Full resync needed after paste
                BindIntervalDataGrid();
                MarkDataFrameDirty();
                IntervalDataGrid.ValidateTable();

                // Disable undo during plot updates so Series.Clear/Add don't record as undo entries
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
            finally
            {
                _suppressUIUpdate = false;
                Element.DataFrame.IntervalSeries.SuppressCollectionChanged = false;
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Handles the <c>MouseDown</c> event for <c>IntervalDataGrid</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void IntervalDataGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Exit if not clicked in empty space below bottom row.
            if (e.OriginalSource.GetType() != typeof(ScrollViewer)) { return; }
            // Commit edits
            IntervalDataGrid.CommitEdit();
            // Clear Selection
            IntervalDataGrid.UnselectAllCells();
            // Move focus to force validation redraw
            IntervalDataGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        #endregion

        #region Threshold Data Grid

        /// <summary>
        /// Binds the threshold data grid to the Element's threshold data series.
        /// Configures the grid for editable threshold data with start/end indices and values.
        /// </summary>
        private void BindThresholdDataGrid()
        {
            _suppressUIUpdate = true;
            Element.DataFrame.ThresholdSeries.SuppressCollectionChanged = true;

            // Unsubscribe PropertyChanged from old RowItems to prevent memory leaks
            for (int i = ThresholdDataOrdinates.Count - 1; i >= 0; i--)
            {
                var rowItem = (ThresholdDataRowItem)ThresholdDataOrdinates[i];
                rowItem.PropertyChanged -= ThresholdDataRowItem_PropertyChanged;
                ThresholdDataOrdinates.RemoveAt(i);
            }

            // Clear existing bindings
            ThresholdDataGridToolBar.DataGrid = null;
            ThresholdDataGrid.ItemsSource = null;

            // Add dummy rows
            ThresholdDataOrdinates.Add(new ThresholdDataRowItem(ThresholdDataOrdinates, new ThresholdData(), Element.DataFrame.ThresholdSeries));

            // Add all data items
            for (int i = 0; i < Element.DataFrame.ThresholdSeries.Count; i++)
            {
                var data = (ThresholdData)Element.DataFrame.ThresholdSeries[i];
                var rowItem = new ThresholdDataRowItem(ThresholdDataOrdinates, data, Element.DataFrame.ThresholdSeries);
                rowItem.PropertyChanged += ThresholdDataRowItem_PropertyChanged;
                ThresholdDataOrdinates.Add(rowItem);
            }

            // Bind data grids
            ThresholdDataGridToolBar.DataGrid = ThresholdDataGrid;
            ThresholdDataGrid.ItemsSource = ThresholdDataOrdinates;
            ThresholdDataGrid.Items.Refresh();

            Element.DataFrame.ThresholdSeries.SuppressCollectionChanged = false;
            _suppressUIUpdate = false;
        }

        /// <summary>
        /// Handles the <c>AutoGeneratedColumns</c> event for <c>ThresholdDataGrid</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void ThresholdDataGrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            // Remove dummy row. 
            ThresholdDataOrdinates.RemoveAt(0);
        }

        /// <summary>
        /// Handles the <c>AutoGeneratingColumn</c> event for <c>ThresholdDataGrid</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void ThresholdDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == nameof(ThresholdDataRowItem.StartIndex))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 200;
                e.Column.Width = new DataGridLength(0.25, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "F0";
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Center_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(ThresholdDataRowItem.EndIndex))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 200;
                e.Column.Width = new DataGridLength(0.25, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "F0";
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Center_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(ThresholdDataRowItem.Value))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 200;
                e.Column.Width = new DataGridLength(0.25, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "{0:" + UserSettings.ValueStringFormat + "}";
                ((Binding)((DataGridTextColumn)e.Column).Binding).Converter = new DoubleToNAConverter();
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Right_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
            else if (e.PropertyName == nameof(ThresholdDataRowItem.NumberAbove))
            {
                e.Column.MinWidth = 10;
                e.Column.MaxWidth = 200;
                e.Column.Width = new DataGridLength(0.25, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).Binding.StringFormat = "F0";
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Right_CellStyle");
                ((DataGridTextColumn)e.Column).HeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
            }
        }

        /// <summary>
        /// Handles the <c>PreviewAddRows</c> event for <c>ThresholdDataGrid</c>.
        /// </summary>
        /// <param name="startRowIndex">The first row index affected by the operation.</param>
        /// <param name="nRows">The number of rows affected by the operation.</param>
        /// <param name="cancelAddRows">Set to <see langword="true"/> to cancel the operation.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void ThresholdDataGrid_PreviewAddRows(int startRowIndex, int nRows, ref bool cancelAddRows)
        {
            cancelAddRows = true;

            _suppressUIUpdate = true;
            bool wasSuppressed = Element.DataFrame.ThresholdSeries.SuppressCollectionChanged;
            Element.DataFrame.ThresholdSeries.SuppressCollectionChanged = true;

            for (int i = startRowIndex; i < startRowIndex + nRows; i++)
            {
                var data = new ThresholdData();
                Element.DataFrame.ThresholdSeries.Insert(i, data);
                var rowItem = new ThresholdDataRowItem(ThresholdDataOrdinates, data, Element.DataFrame.ThresholdSeries);
                rowItem.PropertyChanged += ThresholdDataRowItem_PropertyChanged;
                ThresholdDataOrdinates.Insert(i, rowItem);
            }

            Element.DataFrame.ThresholdSeries.SuppressCollectionChanged = wasSuppressed;
            // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
            if (!wasSuppressed)
            {
                Element.DataFrame.ThresholdSeries.RaiseCollectionChangedReset();
            }
            _suppressUIUpdate = false;
            if (!wasSuppressed && _isLoaded)
            {
                MarkDataFrameDirty();
                ThresholdDataGrid.ValidateTable();
                ThresholdDataGrid.Items.Refresh();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles the <c>PreviewDeleteRows</c> event for <c>ThresholdDataGrid</c>.
        /// </summary>
        /// <param name="rowindices">The row indices affected by the operation.</param>
        /// <param name="cancel">Set to <see langword="true"/> to cancel the operation.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void ThresholdDataGrid_PreviewDeleteRows(List<int> rowindices, ref bool cancel)
        {
            cancel = true;
            rowindices.Sort();

            // Get the underlying item collection from the ItemsSource
            ICollectionView cv = CollectionViewSource.GetDefaultView(ThresholdDataGrid.ItemsSource);
            IList itemList = null;
            if (cv is CollectionView collectionView && collectionView.SourceCollection is IList list)
            {
                itemList = list;
            }
            else
            {
                itemList = ThresholdDataGrid.ItemsSource as IList;
            }

            // Build a list of the items in the current sort order
            List<object> rowList = new List<object>();
            IEnumerator sortedList = CollectionViewSource.GetDefaultView(itemList).GetEnumerator();
            for (int i = 0; i < itemList.Count; i++)
            {
                if (!sortedList.MoveNext())
                    break;
                rowList.Add(sortedList.Current);
            }

            // For each unique row (in sorted order), determine its index in the underlying collection
            List<int> uniqueSortedRows = new List<int>();
            foreach (int rowIndex in rowindices)
            {
                int index = itemList.IndexOf(rowList[rowIndex]);
                uniqueSortedRows.Add(index);
            }
            // Sort the underlying indices
            uniqueSortedRows.Sort();

            // Suppress collection changed during bulk removal to prevent mid-loop
            // CollectionChanged handler ? BindThresholdDataGrid ? ThresholdDataOrdinates rebuild ? index misalignment.
            _suppressUIUpdate = true;
            bool wasSuppressed = Element.DataFrame.ThresholdSeries.SuppressCollectionChanged;
            Element.DataFrame.ThresholdSeries.SuppressCollectionChanged = true;

            // Remove the items starting from the highest index so that removals do not affect remaining indices
            for (int i = uniqueSortedRows.Count - 1; i >= 0; i--)
            {
                Element.DataFrame.ThresholdSeries.RemoveAt(uniqueSortedRows[i]);
                var rowItem = (ThresholdDataRowItem)ThresholdDataOrdinates[uniqueSortedRows[i]];
                rowItem.PropertyChanged -= ThresholdDataRowItem_PropertyChanged;
                ThresholdDataOrdinates.RemoveAt(uniqueSortedRows[i]);
            }

            Element.DataFrame.ThresholdSeries.SuppressCollectionChanged = wasSuppressed;
            // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
            if (!wasSuppressed)
            {
                Element.DataFrame.ThresholdSeries.RaiseCollectionChangedReset();
            }
            _suppressUIUpdate = false;
            if (!wasSuppressed && _isLoaded)
            {
                MarkDataFrameDirty();
                ThresholdDataGrid.ValidateTable();
                ThresholdDataGrid.Items.Refresh();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles the <c>PreviewPasteData</c> event for <c>ThresholdDataGrid</c>.
        /// </summary>
        /// <param name="clipboardData">The pasted clipboard cells.</param>
        /// <param name="cancelPaste">Set to <see langword="true"/> to cancel the operation.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void ThresholdDataGrid_PreviewPasteData(string[][] clipboardData, ref bool cancelPaste)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _suppressUIUpdate = true;
            Element.DataFrame.ThresholdSeries.SuppressCollectionChanged = true;
        }

        /// <summary>
        /// Handles the <c>DataPasted</c> event for <c>ThresholdDataGrid</c>.
        /// </summary>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void ThresholdDataGrid_DataPasted()
        {
            try
            {
                // Keep _suppressUIUpdate = true so CollectionChanged handler skips the Reset
                Element.DataFrame.ThresholdSeries.SuppressCollectionChanged = false;
                Element.DataFrame.ThresholdSeries.RaiseCollectionChangedReset();
                _suppressUIUpdate = false;

                // Full resync needed after paste
                BindThresholdDataGrid();
                MarkDataFrameDirty();
                ThresholdDataGrid.ValidateTable();

                // Disable undo during plot updates so Series.Clear/Add don't record as undo entries
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateControl(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
            finally
            {
                _suppressUIUpdate = false;
                Element.DataFrame.ThresholdSeries.SuppressCollectionChanged = false;
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Handles the <c>MouseDown</c> event for <c>ThresholdDataGrid</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void ThresholdDataGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Exit if not clicked in empty space below bottom row.
            if (e.OriginalSource.GetType() != typeof(ScrollViewer)) { return; }
            // Commit edits
            ThresholdDataGrid.CommitEdit();
            // Clear Selection
            ThresholdDataGrid.UnselectAllCells();
            // Move focus to force validation redraw
            ThresholdDataGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }


        #endregion

        #region Summary Statistics

        /// <summary>
        /// Updates the summary statistics data grid with descriptive statistics.
        /// Calculates statistics for both exact data only and all data (nonparametric).
        /// </summary>
        private void UpdateSummaryStats()
        {
            SummaryStatisticsDataGrid.ItemsSource = null;
            _summaryStatistics.Clear();

            if (Element == null || Element.DataFrame == null || Element.DataFrame.ExactSeries == null) return;

            var stats = Element.DataFrame.SummaryStatisticsExactDataOnly();
            var allStats = Element.DataFrame.SummaryStatisticsAllData();
            for (int i = 0; i < stats.Count; i++)
                _summaryStatistics.Add(new SummaryStatistic(stats.ElementAt(i).Key, stats.ElementAt(i).Value, allStats.ElementAt(i).Value));

            // Column Headers
            var headerStyle = new Style(typeof(DataGridColumnHeader), ExactDataColumn.HeaderStyle);
            headerStyle.Setters.Add(new Setter(ToolTipProperty, new TextBlock() { Text = "The summary statistics using only the exact data sample.", FontWeight = System.Windows.FontWeights.Normal, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap }));
            ExactDataColumn.HeaderStyle = headerStyle;

            headerStyle = new Style(typeof(DataGridColumnHeader), NonparametricColumn.HeaderStyle);
            headerStyle.Setters.Add(new Setter(ToolTipProperty, new TextBlock() { Text = "The summary statistics for all data, including interval and threshold data. A nonparametric distribution is created from the plotting positions.", FontWeight = System.Windows.FontWeights.Normal, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap }));
            NonparametricColumn.HeaderStyle = headerStyle;

            SummaryStatisticsDataGrid.ItemsSource = _summaryStatistics;
            SummaryStatisticsDataGrid.Items.Refresh();
        }

        /// <summary>
        /// Handles the LoadingRow event for the summary statistics data grid.
        /// Applies border styling to separate different sections of statistics.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing row loading data.</param>
        private void SummaryStatisticsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (((SummaryStatistic)e.Row.DataContext).Name == "Minimum")
            {
                e.Row.BorderThickness = _topBorderThickness;
                e.Row.BorderBrush = _borderBrush;
            }
            else if (((SummaryStatistic)e.Row.DataContext).Name == "Kurtosis (of log)")
            {
                e.Row.BorderThickness = _bottomBorderThickness;
                e.Row.BorderBrush = _borderBrush;
            }
            else
                e.Row.BorderThickness = _noBorderThickness;
        }



        #endregion

        #region Hypothesis Tests

        /// <summary>
        /// Handles the ValueChanged event for the index slider.
        /// Updates hypothesis tests when the split point for two-sample tests changes.
        /// </summary>
        /// <param name="value">The new slider value.</param>
        private void IndexSlider_ValueChanged(double value)
        {
            if (_isLoaded == true)
                UpdateHypothesisTests();
        }

        /// <summary>
        /// Handles the Checked event for the use log values checkbox.
        /// Updates hypothesis tests to use logarithmic transformed data.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void UseLogValues_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == true)
                UpdateHypothesisTests();
        }

        /// <summary>
        /// Handles the Unchecked event for the use log values checkbox.
        /// Updates hypothesis tests to use original (non-transformed) data.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void UseLogValues_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == true)
                UpdateHypothesisTests();
        }

        /// <summary>
        /// Updates the hypothesis tests data grid with statistical test results.
        /// Performs various tests including normality, autocorrelation, stationarity, and variance tests.
        /// Provides p-values and inferences for each test.
        /// </summary>
        private void UpdateHypothesisTests()
        {
            
            HypothesisDataGrid.ItemsSource = null;
            _hypothesisTestResults.Clear();

            if (Element == null || Element.DataFrame == null || Element.DataFrame.ExactSeries == null) return;

            var results = Element.DataFrame.SummaryHypothesisTest((int)IndexSlider.Value, UseLogValues.IsChecked == true ? true : false);

            for (int i = 0; i < results.Count; i++)
            {
                double pval = results.ElementAt(i).Value;
                string pvalStrg = pval > 1E-4
                    ? NumberFormatHelper.FormatDouble(pval, "N4")
                    : pval < 1E-15
                        ? "< " + NumberFormatHelper.FormatDouble(1E-15, "E0")
                        : NumberFormatHelper.FormatDouble(pval, "E2");
                string sig = pval < 1E-3 ? " ***" : pval < 1E-2 ? " **" : pval < 0.05 ? " *" : pval < 0.1 ? " ." : "  ";
                _hypothesisTestResults.Add(new HypothesisTestResult(results.ElementAt(i).Key, pvalStrg, sig));

                if (i == 0)
                {
                    // Jarque-Bera test
                    if (results.ElementAt(i).Value < 0.1)
                        _hypothesisTestResults[i].Inference = "The data is not Normally distributed.";
                    else
                        _hypothesisTestResults[i].Inference = "The data is Normally distributed.";
                }
                if (i == 1)
                {
                    // Ljung-Box test
                    if (results.ElementAt(i).Value < 0.1)
                        _hypothesisTestResults[i].Inference = "The autocorrelation of the data is not zero.";
                    else
                        _hypothesisTestResults[i].Inference = "The autocorrelation of the data is zero.";
                }
                if (i >=2 && i <= 5)
                {
                    // Wald-Wolfowitz/Mann-Whitney/Mann-Kendall/Linear trend
                    if (results.ElementAt(i).Value < 0.1)
                        _hypothesisTestResults[i].Inference = "The data is not stationary.";
                    else
                        _hypothesisTestResults[i].Inference = "The data is stationary.";

                }
                if (i == 6)
                {
                    // Equal variance t test
                    if (results.ElementAt(i).Value < 0.1)
                        _hypothesisTestResults[i].Inference = "The two samples (assuming equal variance) do not have the same mean.";
                    else
                        _hypothesisTestResults[i].Inference = "The two samples (assuming equal variance) have the same mean.";
                }
                if (i == 7)
                {
                    // Unequal variance t test
                    if (results.ElementAt(i).Value < 0.1)
                        _hypothesisTestResults[i].Inference = "The two samples (assuming unequal variance) do not have the same mean.";
                    else
                        _hypothesisTestResults[i].Inference = "The two samples (assuming unequal variance) have the same mean.";
                }
                if (i == 8)
                {
                    // F test
                    if (results.ElementAt(i).Value < 0.1)
                        _hypothesisTestResults[i].Inference = "The two samples do not have the same variance.";
                    else
                        _hypothesisTestResults[i].Inference = "The two samples have the same variance.";
                }
                if (i == 9)
                {
                    // unimodality test
                    if (results.ElementAt(i).Value < 0.1)
                        _hypothesisTestResults[i].Inference = "The data is multimodal.";
                    else
                        _hypothesisTestResults[i].Inference = "The data is unimodal.";
                }
            }

            HypothesisDataGrid.ItemsSource = _hypothesisTestResults;
            HypothesisDataGrid.Items.Refresh();
        }




        #endregion


    }
}
