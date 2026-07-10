using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using FrameworkUI;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using OxyPlot;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GenericControls;
using OxyPlot.Axes;
using OxyPlotControls;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and interacting with time series data, including time series plots,
    /// seasonality analysis, and summary statistics.
    /// </summary>
    public partial class TimeSeriesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSeriesControl"/> class.
        /// Wires the TimeSeriesTable events for undo/redo suppress/unsuppress management.
        /// </summary>
        public TimeSeriesControl()
        {
            InitializeComponent();
            DataContext = this;
            TimeSeriesTable.PreviewPasteData += TimeSeriesTable_PreviewPasteData;
            TimeSeriesTable.DataPasted += TimeSeriesTable_DataPasted;
            TimeSeriesTable.PreviewAddRows += TimeSeriesTable_PreviewAddRows;
            TimeSeriesTable.RowsAdded += TimeSeriesTable_RowsAdded;
            TimeSeriesTable.PreviewDeleteRows += TimeSeriesTable_PreviewDeleteRows;
            TimeSeriesTable.RowsDeleted += TimeSeriesTable_RowsDeleted;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
        }

        #region Members

        /// <summary>
        /// Dependency property for the time series element.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(TimeSeriesElement), typeof(TimeSeriesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the time series element that contains the data and settings for the control.
        /// </summary>
        public TimeSeriesElement Element
        {
            get { return (TimeSeriesElement)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element property changes.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TimeSeriesControl thisControl) return;

            // Remove handlers and detach plots
            if (e.OldValue is TimeSeriesElement oldElement)
            {
                // Unsubscribe from ActualModel.Updated
                if (oldElement.TimeSeriesPlot?.ActualModel != null)
                    oldElement.TimeSeriesPlot.ActualModel.Updated -= thisControl.TimeSeriesPlotModelUpdated;

                oldElement.PropertyChanged -= thisControl.ElementPropertyChanged;
                thisControl.UnsubscribeCollectionChanged(oldElement);

                // Detach plots from hosts
                thisControl.TimeSeriesPlotHost.Content = null;
                thisControl.SeasonalityPlotHost.Content = null;
                thisControl.ACFPlotHost.Content = null;
                thisControl.PACFPlotHost.Content = null;
                thisControl.PlotToolbar.Plot = null;
                thisControl.PlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.SeasonalityPlotToolbar.Plot = null;
                thisControl.SeasonalityPlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.ACFPlotToolbar.Plot = null;
                thisControl.ACFPlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
                thisControl.PACFPlotToolbar.Plot = null;
                thisControl.PACFPlotToolbar.PropertiesCalled -= thisControl.PlotToolbar_PropertiesCalled;
            }

            if (e.NewValue == null) return;
            if (e.NewValue is not TimeSeriesElement newElement) return;

            // Reset _isLoaded so the next Loaded event re-initializes plots for the new element
            thisControl._isLoaded = false;

            // Add handlers
            newElement.PropertyChanged += thisControl.ElementPropertyChanged;
            thisControl.SubscribeCollectionChanged(newElement);

            // Suspend plot bridges during visual tree attachment to prevent WPF
            // DependencyProperty changes (AppearanceChanged on Series) from
            // recording spurious undo entries.
            using (newElement.SuspendPlotBridges())
            {
                // Attach Element's plots to ContentControl hosts.
                thisControl.TimeSeriesPlotHost.Content = newElement.TimeSeriesPlot;
                thisControl.SeasonalityPlotHost.Content = newElement.SeasonalityPlot;
                thisControl.ACFPlotHost.Content = newElement.ACFPlot;
                thisControl.PACFPlotHost.Content = newElement.PACFPlot;

                // Wire toolbars
                thisControl.PlotToolbar.Plot = newElement.TimeSeriesPlot;
                thisControl.SeasonalityPlotToolbar.Plot = newElement.SeasonalityPlot;
                thisControl.ACFPlotToolbar.Plot = newElement.ACFPlot;
                thisControl.PACFPlotToolbar.Plot = newElement.PACFPlot;

                // Subscribe toolbar PropertiesCalled → control-level PlotPropertiesCalled
                thisControl.PlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.SeasonalityPlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.ACFPlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
                thisControl.PACFPlotToolbar.PropertiesCalled += thisControl.PlotToolbar_PropertiesCalled;
            }
        }

        /// <summary>
        /// Indicates whether the control has been loaded.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// When true, suppresses UI updates (plot refresh, summary stats recalculation) during
        /// bulk operations like paste, add rows, and delete rows. Uses try/finally pattern for safety.
        /// </summary>
        private bool _suppressUIUpdate;

        /// <summary>Dirty flag indicating the time series plot needs to be redrawn.</summary>
        private bool _timeSeriesPlotDirty;
        /// <summary>Dirty flag indicating the seasonality plot needs to be redrawn.</summary>
        private bool _seasonalityPlotDirty;
        /// <summary>Dirty flag indicating the ACF plot needs to be redrawn.</summary>
        private bool _acfPlotDirty;
        /// <summary>Dirty flag indicating the PACF plot needs to be redrawn.</summary>
        private bool _pacfPlotDirty;
        /// <summary>Dirty flag indicating the summary statistics need to be recalculated.</summary>
        private bool _summaryStatsDirty;

        /// <summary>
        /// Tracks whether SuppressCollectionChanged was already active when PreviewAddRows fired.
        /// When true (paste in progress), RowsAdded skips Reset and UI refresh — DataPasted handles it.
        /// </summary>
        private bool _wasSuppressedBeforeAdd;

        /// <summary>
        /// Tracks whether SuppressCollectionChanged was already active when PreviewDeleteRows fired.
        /// When true (paste in progress), RowsDeleted skips Reset and UI refresh — DataPasted handles it.
        /// </summary>
        private bool _wasSuppressedBeforeDelete;

        /// <summary>
        /// Title used when warning about row deletion in a regular time series.
        /// </summary>
        internal const string RegularRowDeleteWarningTitle = "Delete Rows from Regular Time Series";

        /// <summary>
        /// Message used when warning about row deletion in a regular time series.
        /// </summary>
        internal const string RegularRowDeleteWarningMessage =
            "This time series has a regular time interval. Deleting rows will remove the selected values and then recompute the Date Time column so the record remains continuous from the start date. Existing values after the deleted rows may move to earlier dates." +
            "\n\nTo keep these dates and mark the values as missing, cancel this action and clear the selected Value cells instead." +
            "\n\nContinue deleting rows?";

        /// <summary>
        /// Title used when warning about row insertion in a regular time series.
        /// </summary>
        internal const string RegularRowInsertWarningTitle = "Insert Rows in Regular Time Series";

        /// <summary>
        /// Message used when warning about row insertion in a regular time series.
        /// </summary>
        internal const string RegularRowInsertWarningMessage =
            "This time series has a regular time interval. Inserting rows before the end will add missing values and then recompute the Date Time column so the record remains continuous. Existing values at and after the insertion point may move to later dates." +
            "\n\nTo extend the record without shifting existing data, use Add Row(s) instead." +
            "\n\nContinue inserting rows?";

        /// <summary>
        /// Gets the time series plot from the Element.
        /// </summary>
        public Plot TimeSeriesPlot => Element?.TimeSeriesPlot;

        /// <summary>
        /// Gets the seasonality plot from the Element.
        /// </summary>
        public Plot SeasonalityPlot => Element?.SeasonalityPlot;

        /// <summary>
        /// Gets the ACF plot from the Element.
        /// </summary>
        public Plot ACFPlot => Element?.ACFPlot;

        /// <summary>
        /// Gets the PACF plot from the Element.
        /// </summary>
        public Plot PACFPlot => Element?.PACFPlot;

        /// <summary>
        /// Gets a value indicating whether the plot area was clicked.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Event raised when the preview control is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for the PreviewControlClicked event.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar was clicked.</param>
        /// <param name="plot">The plot that was interacted with.</param>
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
        /// List of summary statistics for the time series.
        /// </summary>
        private List<SummaryStatistic> _summaryStatistics = new List<SummaryStatistic>();


        /// <summary>
        /// Array of hex color codes used to assign unique colors to alternative time series lines.
        /// </summary>
        private string[] _colorHexCodes;

        /// <summary>Cached border thickness for the top separator in summary statistics rows.</summary>
        private static readonly Thickness _topBorderThickness = new Thickness(0, 2, 0, 0);
        /// <summary>Cached border thickness for the bottom separator in summary statistics rows.</summary>
        private static readonly Thickness _bottomBorderThickness = new Thickness(0, 0, 0, 2);
        /// <summary>Cached zero-thickness border for normal summary statistics rows.</summary>
        private static readonly Thickness _noBorderThickness = new Thickness(0);
        /// <summary>Cached black brush for summary statistics row separators.</summary>
        private static readonly SolidColorBrush _borderBrush = new SolidColorBrush(Colors.Black);

        #endregion

        /// <summary>
        /// Handles the Loaded event of the user control to initialize plot settings and update visualizations.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            // Re-subscribe to events that were unsubscribed in Unloaded (needed every Loaded).
            // Unsubscribe first to prevent duplicate subscriptions.
            UnsubscribeCollectionChanged(Element);
            SubscribeCollectionChanged(Element);
            if (Element.TimeSeriesPlot?.ActualModel != null)
                Element.TimeSeriesPlot.ActualModel.Updated += TimeSeriesPlotModelUpdated;

            // Always mark dirty and update on load. Plots must re-render whenever the
            // control enters the visual tree (first open, close/reopen, tab switch back).
            // Disable undo during plot updates so that Series.Clear/Add,
            // axis format changes, and annotation rebuilds don't record as undo entries.
            MarkAllDirty();
            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                UpdateCurrentTabContent();
                UpdateUSGSTextBox();
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
            _isLoaded = true;
        }

        /// <summary>
        /// Handles the Unloaded event of the user control to unsubscribe from collection changed events.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            if (Element != null)
            {
                UnsubscribeCollectionChanged(Element);

                // Unsubscribe from ActualModel.Updated to prevent stale handler execution
                if (Element.TimeSeriesPlot?.ActualModel != null)
                    Element.TimeSeriesPlot.ActualModel.Updated -= TimeSeriesPlotModelUpdated;
            }
        }

        /// <summary>
        /// Handles property changes on the Element to update the UI accordingly.
        /// Handles whole-collection replacement (EntryMethod change, USGS download) via the
        /// <see cref="TimeSeriesElement.TimeSeries"/> property name. Per-item changes are
        /// handled by <see cref="TimeSeries_CollectionChanged"/>.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments containing the property name that changed.</param>
        private void ElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Element.TimeSeries))
            {
                // The entire TimeSeries collection instance was replaced (e.g., EntryMethod change, USGS download).
                // Unsubscribe from old, resubscribe to new.
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    UnsubscribeCollectionChanged(Element);
                    SubscribeCollectionChanged(Element);
                    // Disable undo during plot updates so that Series.Clear/Add, axis changes,
                    // and annotation rebuilds from TimeSeries replacement don't record as undo entries.
                    MarkAllDirty();
                    bool wasUndoEnabled = Element.IsUndoEnabled;
                    Element.IsUndoEnabled = false;
                    try
                    {
                        UpdateCurrentTabContent();
                        UpdateUSGSTextBox();
                    }
                    finally
                    {
                        Element.IsUndoEnabled = wasUndoEnabled;
                    }
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
            else if (e.PropertyName == nameof(Element.EntryMethod) ||
                     e.PropertyName == nameof(Element.SeriesType))
            {
                // Entry method or series type changed — update USGS tab visibility and seasonality plot.
                UpdateUSGSTextBox();
                _seasonalityPlotDirty = true;
                if (SeasonalityTab.IsSelected) { UpdateSeasonalityPlot(); _seasonalityPlotDirty = false; }
            }
        }

        #region CollectionChanged Undo/Redo Support

        /// <summary>
        /// Subscribes to the element time series collection-changed event.
        /// Called during UserControl_Loaded and ElementCallback (new element).
        /// </summary>
        /// <param name="element">The time series element to subscribe to.</param>
        private void SubscribeCollectionChanged(TimeSeriesElement element)
        {
            if (element?.TimeSeries == null) return;
            element.TimeSeries.CollectionChanged += TimeSeries_CollectionChanged;
        }

        /// <summary>
        /// Unsubscribes from the element time series collection-changed event.
        /// Called during UserControl_Unloaded and ElementCallback (old element).
        /// </summary>
        /// <param name="element">The time series element to unsubscribe from.</param>
        private void UnsubscribeCollectionChanged(TimeSeriesElement element)
        {
            if (element?.TimeSeries == null) return;
            element.TimeSeries.CollectionChanged -= TimeSeries_CollectionChanged;
        }

        /// <summary>
        /// Handles collection-changed events fired during undo/redo replay
        /// and during individual cell edits (Replace from clone-and-replace pattern).
        /// Updates plots and summary statistics to reflect the restored data.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments describing the collection change.</param>
        private void TimeSeries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressUIUpdate) return;
            if (e.Action == NotifyCollectionChangedAction.Replace ||
                e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Disable undo during data-driven plot refreshes so that Series.Clear/Add,
                // axis format changes, and annotation rebuilds don't record as separate undo entries.
                MarkAllDirty();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try
                {
                    UpdateCurrentTabContent();
                }
                finally
                {
                    Element.IsUndoEnabled = wasUndoEnabled;
                }
            }
        }

        /// <summary>
        /// Handles the <see cref="NumericControls.TimeSeriesTable.PreviewPasteData"/> event.
        /// Suppresses collection changed notifications and UI updates before paste begins.
        /// </summary>
        /// <param name="clipboardData">The clipboard data being pasted.</param>
        /// <param name="cancelPaste">Whether to cancel the paste operation.</param>
        private void TimeSeriesTable_PreviewPasteData(string[][] clipboardData, ref bool cancelPaste)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _suppressUIUpdate = true;
            Element.TimeSeries.SuppressCollectionChanged = true;
        }

        /// <summary>
        /// Handles the <see cref="NumericControls.TimeSeriesTable.DataPasted"/> event.
        /// Unsuppresses collection changed, raises reset, and refreshes the UI after paste completes.
        /// </summary>
        private void TimeSeriesTable_DataPasted()
        {
            Element.TimeSeries.SuppressCollectionChanged = false;
            Element.TimeSeries.RaiseCollectionChangedReset();   // fires with _suppressUIUpdate still true → CollectionChanged handler returns early
            _suppressUIUpdate = false;                           // unsuppress AFTER Reset to avoid double plot/stats update
            if (_isLoaded)
            {
                // Temporarily disable undo so plot updates (Series.Clear/Add, axis format changes,
                // annotation rebuilds) don't get recorded as separate undo entries.
                // Only the data Reset above should be the single undo action for paste.
                MarkAllDirty();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try
                {
                    UpdateCurrentTabContent();
                }
                finally
                {
                    Element.IsUndoEnabled = wasUndoEnabled;
                }
            }
            Mouse.OverrideCursor = null;
        }

        /// <summary>
        /// Handles the <see cref="NumericControls.TimeSeriesTable.PreviewAddRows"/> event.
        /// Suppresses collection changed notifications and UI updates before rows are added.
        /// </summary>
        /// <param name="startRowIndex">The starting row index for the new rows.</param>
        /// <param name="nRows">The number of rows to add.</param>
        /// <param name="cancel">Whether to cancel the add rows operation.</param>
        private void TimeSeriesTable_PreviewAddRows(int startRowIndex, int nRows, ref bool cancel)
        {
            if (ShouldConfirmRegularRowInsert(Element?.TimeSeries?.TimeInterval ?? TimeInterval.Irregular, Element?.TimeSeries?.Count ?? 0, startRowIndex, nRows)
                && !ConfirmRegularRowInsert())
            {
                cancel = true;
                return;
            }

            // Save whether suppression was already active (e.g., from PreviewPasteData)
            _wasSuppressedBeforeAdd = Element.TimeSeries.SuppressCollectionChanged;
            _suppressUIUpdate = true;
            Element.TimeSeries.SuppressCollectionChanged = true;
        }

        /// <summary>
        /// Handles the <see cref="NumericControls.TimeSeriesTable.RowsAdded"/> event.
        /// If suppression was already active (paste in progress), skips the Reset and UI refresh
        /// because <see cref="TimeSeriesTable_DataPasted"/> will handle it.
        /// Otherwise, unsuppresses, raises a single Reset, and refreshes the UI.
        /// </summary>
        /// <param name="startRowIndex">The starting row index of added rows.</param>
        /// <param name="nRows">The number of rows added.</param>
        private void TimeSeriesTable_RowsAdded(int startRowIndex, int nRows)
        {
            if (_wasSuppressedBeforeAdd)
            {
                // Paste is in progress — leave SuppressCollectionChanged true.
                // DataPasted handler will unsuppress, raise Reset, and update UI.
                return;
            }
            Element.TimeSeries.SuppressCollectionChanged = false;
            Element.TimeSeries.RaiseCollectionChangedReset();   // fires with _suppressUIUpdate still true
            _suppressUIUpdate = false;                           // unsuppress AFTER Reset
            if (_isLoaded)
            {
                MarkAllDirty();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateCurrentTabContent(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        /// <summary>
        /// Handles the <see cref="NumericControls.TimeSeriesTable.PreviewDeleteRows"/> event.
        /// Suppresses collection changed notifications and UI updates before rows are deleted.
        /// </summary>
        /// <param name="rowindices">The indices of rows to be deleted.</param>
        /// <param name="cancel">Whether to cancel the delete operation.</param>
        private void TimeSeriesTable_PreviewDeleteRows(List<int> rowindices, ref bool cancel)
        {
            if (ShouldConfirmRegularRowDelete(Element?.TimeSeries?.TimeInterval ?? TimeInterval.Irregular, Element?.TimeSeries?.Count ?? 0, rowindices)
                && !ConfirmRegularRowDelete())
            {
                cancel = true;
                return;
            }

            // Save whether suppression was already active (e.g., from PreviewPasteData)
            _wasSuppressedBeforeDelete = Element.TimeSeries.SuppressCollectionChanged;
            _suppressUIUpdate = true;
            Element.TimeSeries.SuppressCollectionChanged = true;
        }

        /// <summary>
        /// Determines whether inserting rows should warn that regular time-series dates will be recomputed.
        /// </summary>
        /// <param name="timeInterval">The time interval of the series being edited.</param>
        /// <param name="existingRowCount">The number of rows in the series before insertion.</param>
        /// <param name="startRowIndex">The insertion index requested by the table editor.</param>
        /// <param name="nRows">The number of rows requested for insertion.</param>
        /// <returns><see langword="true"/> when the insert can shift existing regular-series dates; otherwise, <see langword="false"/>.</returns>
        internal static bool ShouldConfirmRegularRowInsert(TimeInterval timeInterval, int existingRowCount, int startRowIndex, int nRows)
        {
            return timeInterval != TimeInterval.Irregular
                && existingRowCount > 0
                && nRows > 0
                && startRowIndex >= 0
                && startRowIndex < existingRowCount;
        }

        /// <summary>
        /// Determines whether deleting rows should warn that regular time-series dates will be recomputed.
        /// </summary>
        /// <param name="timeInterval">The time interval of the series being edited.</param>
        /// <param name="existingRowCount">The number of rows in the series before deletion.</param>
        /// <param name="rowIndices">The row indices requested for deletion.</param>
        /// <returns><see langword="true"/> when at least one valid regular-series row would be deleted; otherwise, <see langword="false"/>.</returns>
        internal static bool ShouldConfirmRegularRowDelete(TimeInterval timeInterval, int existingRowCount, IEnumerable<int> rowIndices)
        {
            return timeInterval != TimeInterval.Irregular
                && existingRowCount > 0
                && rowIndices != null
                && rowIndices.Any(index => index >= 0 && index < existingRowCount);
        }

        /// <summary>
        /// Shows the regular time-series insertion confirmation dialog.
        /// </summary>
        /// <returns><see langword="true"/> when the user chooses to continue inserting rows; otherwise, <see langword="false"/>.</returns>
        private static bool ConfirmRegularRowInsert()
        {
            return GenericControls.MessageBox.Show(
                RegularRowInsertWarningMessage,
                RegularRowInsertWarningTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Shows the regular time-series deletion confirmation dialog.
        /// </summary>
        /// <returns><see langword="true"/> when the user chooses to continue deleting rows; otherwise, <see langword="false"/>.</returns>
        private static bool ConfirmRegularRowDelete()
        {
            return GenericControls.MessageBox.Show(
                RegularRowDeleteWarningMessage,
                RegularRowDeleteWarningTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Handles the <see cref="NumericControls.TimeSeriesTable.RowsDeleted"/> event.
        /// If suppression was already active (paste in progress), skips the Reset and UI refresh
        /// because <see cref="TimeSeriesTable_DataPasted"/> will handle it.
        /// Otherwise, unsuppresses, raises a single Reset, and refreshes the UI.
        /// </summary>
        /// <param name="rowindices">The indices of deleted rows.</param>
        private void TimeSeriesTable_RowsDeleted(List<int> rowindices)
        {
            if (_wasSuppressedBeforeDelete)
            {
                // Paste is in progress — leave SuppressCollectionChanged true.
                // DataPasted handler will unsuppress, raise Reset, and update UI.
                return;
            }
            Element.TimeSeries.SuppressCollectionChanged = false;
            Element.TimeSeries.RaiseCollectionChangedReset();   // fires with _suppressUIUpdate still true
            _suppressUIUpdate = false;                           // unsuppress AFTER Reset
            if (_isLoaded)
            {
                MarkAllDirty();
                bool wasUndoEnabled = Element.IsUndoEnabled;
                Element.IsUndoEnabled = false;
                try { UpdateCurrentTabContent(); }
                finally { Element.IsUndoEnabled = wasUndoEnabled; }
            }
        }

        #endregion

        #region Lazy Update Helpers

        /// <summary>
        /// Marks all plots and summary statistics as dirty, requiring a refresh
        /// the next time their tab becomes visible.
        /// </summary>
        private void MarkAllDirty()
        {
            _timeSeriesPlotDirty = true;
            _seasonalityPlotDirty = true;
            _acfPlotDirty = true;
            _pacfPlotDirty = true;
            _summaryStatsDirty = true;
        }

        /// <summary>
        /// Updates only the content of the currently visible tab. Plots on hidden tabs
        /// remain dirty and will be updated lazily when the user switches to them.
        /// Summary stats are always updated if dirty because they are visible alongside
        /// the plot on the DataTab.
        /// </summary>
        private void UpdateCurrentTabContent()
        {
            if (Element == null) return;

            // Primary plot always updates — it's the core visualization and the update
            // is cheap (just rebinding ItemsSource on a LineSeries + InvalidatePlot).
            if (_timeSeriesPlotDirty) { UpdateTimeSeriesPlot(); _timeSeriesPlotDirty = false; }

            // Secondary analysis plots lazy-load when their tab becomes visible
            // (these involve expensive computations: autocorrelation, seasonal statistics).
            if (SeasonalityTab.IsSelected && _seasonalityPlotDirty) { UpdateLazyAnalysisPlotWithWaitCursor(UpdateSeasonalityPlot, () => _seasonalityPlotDirty = false); }
            else if (ACFTab.IsSelected && _acfPlotDirty) { UpdateLazyAnalysisPlotWithWaitCursor(UpdateACFPlot, () => _acfPlotDirty = false); }
            else if (PACFTab.IsSelected && _pacfPlotDirty) { UpdateLazyAnalysisPlotWithWaitCursor(UpdatePACFPlot, () => _pacfPlotDirty = false); }

            // Summary stats always update — they're fundamental to the data view.
            if (_summaryStatsDirty) { UpdateSummaryStats(); _summaryStatsDirty = false; }
        }

        /// <summary>
        /// Handles tab selection changes to lazily update dirty plots when the user switches tabs.
        /// </summary>
        /// <param name="sender">The TabControl that raised the event.</param>
        /// <param name="e">Event arguments containing the selection change details.</param>
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != MainTabControl) return;
            if (Element == null) return;
            // Primary plot + summary stats are always kept current (see UpdateCurrentTabContent).
            // Only secondary analysis plots need lazy update on tab switch.
            if (SeasonalityTab.IsSelected && _seasonalityPlotDirty) { UpdateLazyAnalysisPlotWithWaitCursor(UpdateSeasonalityPlot, () => _seasonalityPlotDirty = false); }
            else if (ACFTab.IsSelected && _acfPlotDirty) { UpdateLazyAnalysisPlotWithWaitCursor(UpdateACFPlot, () => _acfPlotDirty = false); }
            else if (PACFTab.IsSelected && _pacfPlotDirty) { UpdateLazyAnalysisPlotWithWaitCursor(UpdatePACFPlot, () => _pacfPlotDirty = false); }
        }

        /// <summary>
        /// Runs a lazy secondary analysis plot update with visible wait-cursor feedback.
        /// </summary>
        /// <param name="updatePlotAction">The plot update action to run.</param>
        /// <param name="markCleanAction">The action that clears the corresponding dirty flag.</param>
        /// <remarks>
        /// Secondary time-series analysis plots compute seasonal statistics and correlation
        /// diagnostics, which can be slow for long records.
        /// </remarks>
        private void UpdateLazyAnalysisPlotWithWaitCursor(Action updatePlotAction, Action markCleanAction)
        {
            WaitCursorHelper.RunWithVisibleWaitCursor(Dispatcher, () =>
            {
                updatePlotAction();
                markCleanAction();
            });
        }

        #endregion

        #region Plots

        /// <summary>
        /// Gets the currently selected plot based on which tab item is active.
        /// </summary>
        /// <returns>The currently active plot, or null if no plot tab is selected.</returns>
        public Plot GetCurrentPlot()
        {
            if (DataTab.IsSelected) return Element?.TimeSeriesPlot;
            if (SeasonalityTab.IsSelected) return Element?.SeasonalityPlot;
            if (ACFTab.IsSelected) return Element?.ACFPlot;
            if (PACFTab.IsSelected) return Element?.PACFPlot;
            return null;
        }

        /// <summary>
        /// Gets the toolbar for the currently selected plot based on which tab item is active.
        /// </summary>
        /// <returns>The toolbar for the currently active plot, or null if no plot tab is selected.</returns>
        public OxyPlotToolbar GetCurrentPlotToolbar()
        {
            if (DataTab.IsSelected) return PlotToolbar;
            if (SeasonalityTab.IsSelected) return SeasonalityPlotToolbar;
            if (ACFTab.IsSelected) return ACFPlotToolbar;
            if (PACFTab.IsSelected) return PACFPlotToolbar;
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
        /// Handles the PreviewMouseDown event on the control to detect clicks on the plot or toolbar.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments containing mouse button information.</param>
        private void Control_PreviewMouseDown(object sender, MouseButtonEventArgs e)
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
        /// Handles the LostFocus event on the control to reset the PlotClicked state.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the Updated event of the time series plot model to adjust the date-time axis format based on the time span.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void TimeSeriesPlotModelUpdated(object sender, EventArgs e)
        {
            if (Element?.TimeSeriesPlot == null) return;
            foreach (var axis in Element.TimeSeriesPlot.Axes)
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
        /// Updates the time series plot with the current time series data.
        /// Clears existing series, re-adds the primary LineSeries with current data,
        /// suspends bridge recording during the update, and rebuilds bridges afterward.
        /// Re-adds any checked alternative time series after the primary update.
        /// </summary>
        private void UpdateTimeSeriesPlot()
        {
            var plot = Element?.TimeSeriesPlot;
            if (plot == null) return;

            string valueStringFormat = UserSettings.ValueStringFormat;
            var timeSeriesLine = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "TimeSeriesLine")
                ?? new LineSeries
                {
                    Name = "TimeSeriesLine",
                    Title = "Time Series Data",
                    Color = System.Windows.Media.Color.FromRgb(26, 161, 226),
                    MarkerFill = Colors.Transparent,
                    StrokeThickness = 1,
                    LineStyle = OxyPlot.LineStyle.Solid,
                };

            // Always set data binding and rendering properties — handles both newly created
            // series and reused series from CreateDefaultTimeSeriesPlot(). Clear DataFieldX/Y
            // to prevent conflicts with the Mapping function.
            timeSeriesLine.DataFieldX = null;
            timeSeriesLine.DataFieldY = null;
            timeSeriesLine.Decimator = OxyPlot.Decimator.Decimate;
            timeSeriesLine.MinimumSegmentLength = 4.0;
            timeSeriesLine.Mapping = item =>
            {
                var o = (Numerics.Data.SeriesOrdinate<DateTime, double>)item;
                return new OxyPlot.DataPoint(OxyPlot.Axes.DateTimeAxis.ToDouble(o.Index), o.Value);
            };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                if (Element.TimeSeries == null) return;

                timeSeriesLine.ItemsSource = Element.TimeSeries;
                timeSeriesLine.TrackerFormatString = "{0}" + Environment.NewLine +
                    "{1}: {2}" + Environment.NewLine +
                    "{3}: {4:" + valueStringFormat + "}";
                plot.Series.Add(timeSeriesLine);
            }

            Element.RebuildSeriesAndAnnotationBridges(plot);
            plot.InvalidatePlot(true);

            AlternativeTimeSeriesSelector?.AddAllChecked();
        }

        /// <summary>
        /// Updates the seasonality plot. For Peak Discharge/Stage data, displays a monthly frequency
        /// histogram. For all other data types, displays monthly summary statistics with confidence intervals.
        /// Toggles the warning label based on the returned status.
        /// </summary>
        private void UpdateSeasonalityPlot()
        {
            if (Element == null) return;
            var plot = Element.SeasonalityPlot;
            if (plot == null)
            {
                SeasonalityWarning.Visibility = Visibility.Visible;
                return;
            }

            string valueStringFormat = UserSettings.ValueStringFormat;

            // Look up named series before Clear(). If a series doesn't exist (old project files),
            // create it with default properties so the plot always renders.
            var confidenceInterval = plot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "ConfidenceInterval")
                ?? new AreaSeries
                {
                    Name = "ConfidenceInterval",
                    Title = "90% Confidence Interval",
                    Fill = ColorFromHex("#4A688CAF"),
                    Color = ColorFromHex("#FF353B7A"),
                    MarkerFill = Colors.Transparent,
                    StrokeThickness = 1,
                    DataFieldX = "X1",
                    DataFieldX2 = "X2",
                    DataFieldY = "Y1",
                    DataFieldY2 = "Y2",
                    Decimator = OxyPlot.Decimator.Decimate,
                };
            var innerConfidenceInterval = plot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "InnerConfidenceInterval")
                ?? new AreaSeries
                {
                    Name = "InnerConfidenceInterval",
                    Title = "50% Confidence Interval",
                    Fill = ColorFromHex("#7529D372"),
                    Color = ColorFromHex("#FF353B7A"),
                    MarkerFill = Colors.Transparent,
                    StrokeThickness = 1,
                    DataFieldX = "X1",
                    DataFieldX2 = "X2",
                    DataFieldY = "Y1",
                    DataFieldY2 = "Y2",
                    Decimator = OxyPlot.Decimator.Decimate,
                };
            var medianLine = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "MedianLine")
                ?? new LineSeries
                {
                    Name = "MedianLine",
                    Title = "Median",
                    Color = Colors.Blue,
                    MarkerFill = Colors.Transparent,
                    StrokeThickness = 2,
                    LineStyle = OxyPlot.LineStyle.Solid,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
            var meanLine = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "MeanLine")
                ?? new LineSeries
                {
                    Name = "MeanLine",
                    Title = "Mean",
                    Color = Colors.Blue,
                    MarkerFill = Colors.Transparent,
                    StrokeThickness = 2,
                    LineStyle = OxyPlot.LineStyle.DashDot,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
            var minLine = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "MinLine")
                ?? new LineSeries
                {
                    Name = "MinLine",
                    Title = "Minimum",
                    Color = Colors.Black,
                    MarkerFill = Colors.Transparent,
                    StrokeThickness = 2,
                    LineStyle = OxyPlot.LineStyle.Dot,
                    Visibility = Visibility.Hidden,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
            var maxLine = plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "MaxLine")
                ?? new LineSeries
                {
                    Name = "MaxLine",
                    Title = "Maximum",
                    Color = Colors.Black,
                    MarkerFill = Colors.Transparent,
                    StrokeThickness = 2,
                    LineStyle = OxyPlot.LineStyle.Dot,
                    Visibility = Visibility.Hidden,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
            var histogramSeries = plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "Histogram")
                ?? new HistogramSeries
                {
                    Name = "Histogram",
                    Title = "Seasonality Plot",
                    FillColor = ColorFromHex("#7529D372"),
                    StrokeColor = System.Windows.Media.Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1,
                };
            var yAxis = plot.Axes.FirstOrDefault(a => a.Key == "Yaxis");

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();

                // Seasonality statistics require at least 2 distinct years of data.
                if (Element.TimeSeries == null || Element.TimeSeries.Count <= 2 ||
                    Element.TimeSeries.Select(s => s.Index.Year).Distinct().Count() < 2)
                {
                    Element.RebuildSeriesAndAnnotationBridges(plot);
                    plot.InvalidatePlot(true);
                    SeasonalityWarning.Visibility = Visibility.Visible;
                    return;
                }

                if (Element.IsPeakSeasonality)
                {
                    PopulateSeasonalityHistogram(plot, yAxis, valueStringFormat, histogramSeries);
                }
                else
                {
                    PopulateSeasonalityConfidenceIntervals(plot, yAxis, valueStringFormat,
                        confidenceInterval, innerConfidenceInterval, medianLine, meanLine, minLine, maxLine);
                }
            }

            Element.RebuildSeriesAndAnnotationBridges(plot);
            plot.InvalidatePlot(true);
            SeasonalityWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Populates the seasonality plot with a monthly frequency histogram for peak data.
        /// </summary>
        private void PopulateSeasonalityHistogram(Plot plot, OxyPlot.Wpf.Axis yAxis, string valueStringFormat, HistogramSeries histogramSeries)
        {
            var frequency = Element.TimeSeries.MonthlyFrequency();
            double sum = frequency.Sum();
            if (sum == 0) return;

            var seasonPoints = new List<OxyPlot.Series.HistogramItem>();
            for (int i = 1; i <= 12; i++)
            {
                var lo = OxyPlot.Axes.DateTimeAxis.ToDouble(new DateTime(2020, i, 1));
                var hi = OxyPlot.Axes.DateTimeAxis.ToDouble(new DateTime(i < 12 ? 2020 : 2021, i < 12 ? i + 1 : 1, 1));
                seasonPoints.Add(new OxyPlot.Series.HistogramItem(lo, hi, frequency[i - 1] / sum * (hi - lo)));
            }

            // Set axis bounds and titles. Clear any prior UnitLabel binding from non-peak
            // mode before assigning the literal "Relative Frequency" title (Convention 12:
            // peak mode uses a fixed title, not a data-bound one).
            if (yAxis != null)
            {
                PlotAxisTitleDefaults.SetTitleIfDefault(yAxis, "Relative Frequency", Element?.UnitLabel);
                yAxis.StringFormat = "N2";
            }
            foreach (var axis in plot.Axes)
            {
                if (axis.Position == OxyPlot.Axes.AxisPosition.Bottom)
                {
                    axis.Minimum = OxyPlot.Axes.DateTimeAxis.ToDouble(new DateTime(2020, 1, 1));
                    axis.Maximum = OxyPlot.Axes.DateTimeAxis.ToDouble(new DateTime(2020, 12, 31));
                }
            }

            histogramSeries.ItemsSource = seasonPoints;
            histogramSeries.TrackerFormatString = "{0}" + Environment.NewLine +
                "{1}: {2:MMM}" + Environment.NewLine +
                "{3}: {4:" + valueStringFormat + "}";
            plot.Series.Add(histogramSeries);
        }

        /// <summary>
        /// Populates the seasonality plot with monthly summary statistics and confidence intervals.
        /// </summary>
        private void PopulateSeasonalityConfidenceIntervals(Plot plot, OxyPlot.Wpf.Axis yAxis, string valueStringFormat,
            AreaSeries confidenceInterval, AreaSeries innerConfidenceInterval,
            LineSeries medianLine, LineSeries meanLine, LineSeries minLine, LineSeries maxLine)
        {
            // Bind the Y-axis title to TimeSeriesElement.UnitLabel reactively (Convention 12)
            // so live edits to the upstream UnitLabel propagate without re-running the analysis.
            // Imperative `axis.Title = Element.UnitLabel` would not update on UnitLabel change
            // until the next Update*Plot fires.
            if (yAxis != null)
            {
                PlotAxisTitleDefaults.BindTitleIfDefault(yAxis, Element, nameof(TimeSeriesElement.UnitLabel), Element.UnitLabel, "Relative Frequency");
                yAxis.StringFormat = "N0";
            }

            // Reset axis bounds to auto
            foreach (var axis in plot.Axes)
            {
                if (axis.Position == OxyPlot.Axes.AxisPosition.Bottom)
                {
                    axis.Minimum = double.NaN;
                    axis.Maximum = double.NaN;
                }
            }

            var stats = Element.TimeSeries.MonthlySummaryStatistics();
            var confidencePoints = new List<AreaPoint>();
            var innerConfidencePoints = new List<AreaPoint>();
            var medianLinePoints = new List<OxyPlot.DataPoint>();
            var meanLinePoints = new List<OxyPlot.DataPoint>();
            var minLinePoints = new List<OxyPlot.DataPoint>();
            var maxLinePoints = new List<OxyPlot.DataPoint>();

            for (int i = 0; i < stats.GetLength(0); i++)
            {
                double dVal = OxyPlot.Axes.DateTimeAxis.ToDouble(new DateTime(2020, i + 1, 1));
                minLinePoints.Add(new OxyPlot.DataPoint(dVal, stats[i, 0]));
                confidencePoints.Add(new AreaPoint(new OxyPlot.DataPoint(dVal, stats[i, 1]), new OxyPlot.DataPoint(dVal, stats[i, 5])));
                innerConfidencePoints.Add(new AreaPoint(new OxyPlot.DataPoint(dVal, stats[i, 2]), new OxyPlot.DataPoint(dVal, stats[i, 4])));
                medianLinePoints.Add(new OxyPlot.DataPoint(dVal, stats[i, 3]));
                maxLinePoints.Add(new OxyPlot.DataPoint(dVal, stats[i, 6]));
                meanLinePoints.Add(new OxyPlot.DataPoint(dVal, stats[i, 7]));
            }

            // Set data and tracker format on series looked up by name
            var trackerFormat = "{0}" + Environment.NewLine +
                "{1}: {2:MMM}" + Environment.NewLine +
                "{3}: {4:" + valueStringFormat + "}";

            if (confidenceInterval != null) { confidenceInterval.ItemsSource = confidencePoints; confidenceInterval.TrackerFormatString = trackerFormat; plot.Series.Add(confidenceInterval); }
            if (innerConfidenceInterval != null) { innerConfidenceInterval.ItemsSource = innerConfidencePoints; innerConfidenceInterval.TrackerFormatString = trackerFormat; plot.Series.Add(innerConfidenceInterval); }
            if (medianLine != null) { medianLine.ItemsSource = medianLinePoints; medianLine.TrackerFormatString = trackerFormat; plot.Series.Add(medianLine); }
            if (meanLine != null) { meanLine.ItemsSource = meanLinePoints; meanLine.TrackerFormatString = trackerFormat; plot.Series.Add(meanLine); }
            if (minLine != null) { minLine.ItemsSource = minLinePoints; minLine.TrackerFormatString = trackerFormat; plot.Series.Add(minLine); }
            if (maxLine != null) { maxLine.ItemsSource = maxLinePoints; maxLine.TrackerFormatString = trackerFormat; plot.Series.Add(maxLine); }
        }

        /// <summary>
        /// Updates the ACF (Autocorrelation Function) plot showing serial correlation at various lags.
        /// Toggles the missing data warning based on the returned status.
        /// </summary>
        private void UpdateACFPlot()
        {
            if (Element == null) return;
            var plot = Element.ACFPlot;
            if (plot == null) return;

            // Look up the named histogram series before Clear().
            var acfSeries = plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "Autocorrelation")
                ?? new HistogramSeries
                {
                    Name = "Autocorrelation",
                    Title = "Autocorrelation",
                    FillColor = System.Windows.Media.Color.FromArgb(125, 104, 140, 175),
                    StrokeColor = System.Windows.Media.Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1,
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (Element.TimeSeries == null || Element.TimeSeries.Count < 10)
                {
                    Element.RebuildSeriesAndAnnotationBridges(plot);
                    plot.InvalidatePlot(true);
                    ACFMissingDataWarning.Visibility = Visibility.Collapsed;
                    return;
                }

                // Guard against missing data — Autocorrelation.Function cannot handle NaN values
                if (Element.TimeSeries.HasMissingValues)
                {
                    Element.RebuildSeriesAndAnnotationBridges(plot);
                    plot.InvalidatePlot(true);
                    ACFMissingDataWarning.Visibility = Visibility.Visible;
                    return;
                }

                var data = Element.TimeSeries.Select(s => s.Value).ToArray();
                var acfItems = new List<OxyPlot.Series.HistogramItem>();
                var acf = Autocorrelation.Function(data);
                for (int i = 0; i < acf.GetLength(0); i++)
                {
                    acfItems.Add(new OxyPlot.Series.HistogramItem(i, i + 1, acf[i, 1]));
                }

                acfSeries.ItemsSource = acfItems;
                acfSeries.TrackerFormatString = "{0}" + Environment.NewLine +
                    "{1}: {2:0}" + Environment.NewLine +
                    "{3}: {4:0.000000}";
                plot.Series.Add(acfSeries);

                var ci = Autocorrelation.CorrelationConfidenceInterval(data.Length);
                plot.Annotations.Add(new LineAnnotation
                {
                    Name = "LowerCI",
                    Text = "2.5% CI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = OxyPlot.LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Y = ci[0]
                });
                plot.Annotations.Add(new LineAnnotation
                {
                    Name = "UpperCI",
                    Text = "97.5% CI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = OxyPlot.LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    Y = ci[1]
                });

                foreach (var axis in plot.Axes)
                {
                    if (axis.Key == "Yaxis")
                    {
                        axis.Minimum = Math.Round((Math.Min(ci[0], Tools.Min(acf.GetColumn(1))) - 0.1) * 20) / 20;
                    }
                }
            }

            Element.RebuildSeriesAndAnnotationBridges(plot);
            plot.InvalidatePlot(true);
            ACFMissingDataWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Updates the PACF (Partial Autocorrelation Function) plot showing conditional correlation at various lags.
        /// Toggles the missing data warning based on the returned status.
        /// </summary>
        private void UpdatePACFPlot()
        {
            if (Element == null) return;
            var plot = Element.PACFPlot;
            if (plot == null) return;

            // Look up the named histogram series before Clear().
            var pacfSeries = plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "PartialAutocorrelation")
                ?? new HistogramSeries
                {
                    Name = "PartialAutocorrelation",
                    Title = "Partial Autocorrelation",
                    FillColor = System.Windows.Media.Color.FromArgb(125, 104, 140, 175),
                    StrokeColor = System.Windows.Media.Color.FromArgb(255, 53, 59, 122),
                    StrokeThickness = 1,
                };

            using (Element.SuspendPlotBridges())
            {
                plot.Series.Clear();
                plot.Annotations.Clear();

                if (Element.TimeSeries == null || Element.TimeSeries.Count < 10)
                {
                    Element.RebuildSeriesAndAnnotationBridges(plot);
                    plot.InvalidatePlot(true);
                    PACFMissingDataWarning.Visibility = Visibility.Collapsed;
                    return;
                }

                // Guard against missing data — Autocorrelation.Function cannot handle NaN values
                if (Element.TimeSeries.HasMissingValues)
                {
                    Element.RebuildSeriesAndAnnotationBridges(plot);
                    plot.InvalidatePlot(true);
                    PACFMissingDataWarning.Visibility = Visibility.Visible;
                    return;
                }

                var data = Element.TimeSeries.Select(s => s.Value).ToArray();
                var pacfItems = new List<OxyPlot.Series.HistogramItem>();
                var pacf = Autocorrelation.Function(data, -1, Autocorrelation.Type.Partial);
                for (int i = 0; i < pacf.GetLength(0); i++)
                {
                    pacfItems.Add(new OxyPlot.Series.HistogramItem(i, i + 1, pacf[i, 1]));
                }

                pacfSeries.ItemsSource = pacfItems;
                pacfSeries.TrackerFormatString = "{0}" + Environment.NewLine +
                    "{1}: {2:0}" + Environment.NewLine +
                    "{3}: {4:0.000000}";
                plot.Series.Add(pacfSeries);

                var ci = Autocorrelation.CorrelationConfidenceInterval(data.Length);
                plot.Annotations.Add(new LineAnnotation
                {
                    Name = "LowerCI",
                    Text = "2.5% CI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = OxyPlot.LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Y = ci[0]
                });
                plot.Annotations.Add(new LineAnnotation
                {
                    Name = "UpperCI",
                    Text = "97.5% CI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = OxyPlot.LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    Y = ci[1]
                });

                foreach (var axis in plot.Axes)
                {
                    if (axis.Key == "Yaxis")
                    {
                        axis.Maximum = Math.Round((Math.Max(ci[1], Tools.Max(pacf.GetColumn(1))) + 0.1) * 20) / 20;
                        axis.Minimum = Math.Round((Math.Min(ci[0], Tools.Min(pacf.GetColumn(1))) - 0.1) * 20) / 20;
                    }
                }
            }

            Element.RebuildSeriesAndAnnotationBridges(plot);
            plot.InvalidatePlot(true);
            PACFMissingDataWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a hex color string (e.g., "#FF353B7A" or "#4A688CAF") to a <see cref="Color"/>.
        /// </summary>
        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        #endregion

        #region Summary Statistics

        /// <summary>
        /// Updates the summary statistics data grid with statistics calculated from the time series data.
        /// </summary>
        private void UpdateSummaryStats()
        {
            _summaryStatistics.Clear();

            if (Element == null || Element.TimeSeries == null)
            {
                SummaryStatisticsDataGrid.Items.Refresh();
                return;
            }

            var stats = Element.TimeSeries.SummaryStatistics();
            foreach (var kvp in stats)
                _summaryStatistics.Add(new SummaryStatistic(kvp.Key, kvp.Value));

            if (SummaryStatisticsDataGrid.ItemsSource != _summaryStatistics)
                SummaryStatisticsDataGrid.ItemsSource = _summaryStatistics;
            SummaryStatisticsDataGrid.Items.Refresh();
        }

        /// <summary>
        /// Handles the LoadingRow event of the summary statistics data grid to apply custom row styling.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments containing the row being loaded.</param>
        private void SummaryStatisticsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is not SummaryStatistic stat) return;

            if (stat.Name == "Minimum")
            {
                e.Row.BorderThickness = _topBorderThickness;
                e.Row.BorderBrush = _borderBrush;
            }
            else if (stat.Name == "Kurtosis")
            {
                e.Row.BorderThickness = _bottomBorderThickness;
                e.Row.BorderBrush = _borderBrush;
            }
            else
                e.Row.BorderThickness = _noBorderThickness;
        }

        #endregion

        /// <summary>
        /// Updates the USGS text box with raw USGS data if the entry method is USGS and the series type is peak data.
        /// </summary>
        private void UpdateUSGSTextBox()
        {
            if (Element == null) return;
            if (Element.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.USGS &&
                (Element.SeriesType == TimeSeriesDownload.TimeSeriesType.PeakDischarge ||
                Element.SeriesType == TimeSeriesDownload.TimeSeriesType.PeakStage))
            {
                USGSTextFileTab.Visibility = Visibility.Visible;
                TextRange textRange = new TextRange(USGSRawTextBox.Document.ContentStart, USGSRawTextBox.Document.ContentEnd);
                textRange.Text = Element.USGSRawText;
            }
            else
            {
                USGSTextFileTab.Visibility = Visibility.Collapsed;
                TextRange textRange = new TextRange(USGSRawTextBox.Document.ContentStart, USGSRawTextBox.Document.ContentEnd);
                textRange.Text = string.Empty;
            }
        }

        #region Alternative Time Series

        /// <summary>
        /// Handles the event when an alternative time series is added to the selector.
        /// Assigns a unique color and adds the alternative's line series to the time series plot.
        /// </summary>
        /// <param name="item">The time series alternative item to add to the plot.</param>
        private void AlternativeTimeSeriesSelector_TimeSeriesAdded(TimeSeriesAlternativeItem item)
        {
            var plot = Element?.TimeSeriesPlot;
            if (plot == null) return;

            // Remove first (idempotent — prevents duplicates if re-added after data change)
            plot.Series.Remove(item.TimeSeriesLine);

            var alternative = item.Alternative;
            if (alternative.TimeSeries == null || alternative.TimeSeries.Count == 0) return;

            // Assign a unique color from the shared color list
            Color lineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[0]);
            for (int i = 4; i < _colorHexCodes.Length; i++)
            {
                var tempColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[i]);
                bool colorExists = false;
                for (int j = 0; j < plot.Series.Count; j++)
                {
                    if (plot.Series[j].Color == tempColor)
                    {
                        colorExists = true;
                        break;
                    }
                }
                if (colorExists == false || i == _colorHexCodes.Length - 1)
                {
                    lineColor = tempColor;
                    break;
                }
            }

            item.TimeSeriesLine.Title = alternative.Name;
            item.TimeSeriesLine.Color = lineColor;
            item.TimeSeriesLine.ItemsSource = alternative.TimeSeries;
            item.TimeSeriesLine.Mapping = tsItem =>
            {
                var o = (Numerics.Data.SeriesOrdinate<DateTime, double>)tsItem;
                return new OxyPlot.DataPoint(OxyPlot.Axes.DateTimeAxis.ToDouble(o.Index), o.Value);
            };
            item.TimeSeriesLine.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";

            plot.Series.Add(item.TimeSeriesLine);
            plot.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles the event when an alternative time series is removed from the selector.
        /// Removes the alternative's line series from the time series plot and refreshes the display.
        /// </summary>
        /// <param name="item">The time series alternative item to remove from the plot.</param>
        private void AlternativeTimeSeriesSelector_TimeSeriesRemoved(TimeSeriesAlternativeItem item)
        {
            var plot = Element?.TimeSeriesPlot;
            if (plot == null) return;
            plot.Series.Remove(item.TimeSeriesLine);
            plot.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles the mouse click on the "Alternative Time Series" text block to toggle
        /// the visibility of the alternatives panel.
        /// </summary>
        /// <param name="sender">The TextBlock that was clicked.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void AlternativesTextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowAlternativesToggleButton.IsChecked = !ShowAlternativesToggleButton.IsChecked;
        }

        #endregion

    }
}
