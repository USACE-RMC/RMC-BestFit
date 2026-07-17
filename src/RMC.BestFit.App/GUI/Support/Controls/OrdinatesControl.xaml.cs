using NumericControls;
using Numerics.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
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

namespace RMC_BestFit
{
    /// <summary>
    /// A user control that provides an interactive interface for managing double value ordinates.
    /// This control displays a data grid where users can enter and modify ordinate values for analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b> Authors: </b>
    /// <list type="bullet">
    /// <item><description>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public partial class OrdinatesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrdinatesControl"/> class.
        /// Sets up the control and subscribes to collection change events for ordinate row items.
        /// </summary>
        public OrdinatesControl()
        {
            InitializeComponent();
            _rowItems.CollectionChanged += RowItems_CollectionChanged;
            DataGrid.PreviewPasteData += DataGrid_PreviewPasteData;
            DataGrid.DataPasted += DataGrid_DataPasted;
        }

        /// <summary>
        /// Identifies the <see cref="Ordinates"/> dependency property.
        /// This property represents the ordinates to be displayed and edited.
        /// </summary>
        public static readonly DependencyProperty OrdinatesProperty = DependencyProperty.Register(nameof(Ordinates), typeof(ObservableCollection<double>), typeof(OrdinatesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the ordinates for this control.
        /// </summary>
        public ObservableCollection<double> Ordinates
        {
            get { return (ObservableCollection<double>)GetValue(OrdinatesProperty); }
            set { SetValue(OrdinatesProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="RowItemType"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty RowItemTypeProperty = DependencyProperty.Register(
            nameof(RowItemType), typeof(Type), typeof(OrdinatesControl), new PropertyMetadata(typeof(OrdinateRowItem)));

        /// <summary>
        /// Gets or sets the concrete <see cref="OrdinateRowItem"/> subclass used to wrap each
        /// ordinate value. Defaults to <see cref="OrdinateRowItem"/>; callers pass
        /// <c>typeof(XOrdinateRowItem)</c> or <c>typeof(YOrdinateRowItem)</c> via XAML
        /// (using <c>{x:Type ...}</c>) to drive the auto-generated column header text via
        /// <see cref="GenericControls.DataGridRowItem.PropertyDisplayName"/>.
        /// </summary>
        /// <remarks>
        /// The supplied type must derive from <see cref="OrdinateRowItem"/> and expose a public
        /// constructor of the shape <c>(ObservableCollection&lt;object&gt; parentList, double value)</c>
        /// — both <see cref="XOrdinateRowItem"/> and <see cref="YOrdinateRowItem"/> meet this
        /// contract. Reflection-based construction is used so XAML can configure the type
        /// without a generic at the control class.
        /// </remarks>
        public Type RowItemType
        {
            get { return (Type)GetValue(RowItemTypeProperty); }
            set { SetValue(RowItemTypeProperty, value); }
        }

        /// <summary>
        /// Constructs a row item of the configured <see cref="RowItemType"/> using
        /// reflection. Centralizes the <c>Activator.CreateInstance</c> call so the three
        /// callers (initial element load + Add branch of <see cref="Ordinates_CollectionChanged"/>)
        /// share the same factory path.
        /// </summary>
        /// <param name="value">The ordinate value to wrap.</param>
        /// <returns>A newly-allocated row item of type <see cref="RowItemType"/>.</returns>
        private OrdinateRowItem CreateRowItem(double value)
        {
            return (OrdinateRowItem)Activator.CreateInstance(RowItemType, _rowItems, value);
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Ordinates"/> property changes.
        /// Handles cleanup of the old ordinates and initialization of the new ordinates, including event subscriptions and data grid population.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as OrdinatesControl == null) return;
            var thisControl = (OrdinatesControl)d;

            // Remove old stuff
            // Clear old row items
            for (int i = thisControl._rowItems.Count - 1; i >= 0; i--)
            {
                thisControl._rowItems.RemoveAt(i);
            }
            // Clear data grid
            thisControl.DataGrid.ItemsSource = null;
            // Remove handlers
            var oldOrdinates = (ObservableCollection<double>)e.OldValue;
            if (oldOrdinates != null)
                oldOrdinates.CollectionChanged -= thisControl.Ordinates_CollectionChanged;

            // Set up new stuff
            if (e.NewValue == null) return;
            var newOrdinates = e.NewValue as ObservableCollection<double>;
            if (newOrdinates != null)
            {
                // Add row items
                foreach (double ordinate in newOrdinates)
                {
                    thisControl._rowItems.Add(thisControl.CreateRowItem(ordinate));
                }
                // Set data grid
                thisControl.DataGrid.ItemsSource = thisControl._rowItems;
                // Set RowType so the grid can create new rows even when empty.
                // Uses RowItemType so click-to-add rows are X/Y-typed when configured.
                thisControl.DataGrid.RowType = thisControl.RowItemType;
                // Add handlers
                newOrdinates.CollectionChanged += thisControl.Ordinates_CollectionChanged;
            }

        }

        /// <summary>
        /// Flag to suppress UI updates when model changes are being propagated to prevent circular update loops.
        /// </summary>
        private bool _suppressUIUpdate = false;

        /// <summary>
        /// Flag to suppress model updates when UI changes are being propagated to prevent circular update loops.
        /// </summary>
        private bool _suppressModelUpdate = false;

        /// <summary>
        /// Observable collection of ordinate row items displayed in the data grid.
        /// </summary>
        private ObservableCollection<object> _rowItems = new ObservableCollection<object>();

        /// <summary>
        /// Handles the automatic column generation event for the data grid.
        /// Configures column styling, width, and tooltips for the column.
        /// </summary>
        /// <param name="sender">The data grid generating the column.</param>
        /// <param name="e">Event arguments containing the column being generated.</param>
        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == nameof(OrdinateRowItem.Value))
            {
                e.Column.MinWidth = 20;
                e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                ((DataGridTextColumn)e.Column).CellStyle = (Style)FindResource("Right_CellStyle");

                // Create header style based on Center_ColumnHeaderStyle with tooltip
                // Note: Must use Style constructor with basedOn parameter to preserve the style inheritance chain
                var baseHeaderStyle = (Style)FindResource("Center_ColumnHeaderStyle");
                var headerStyle = new Style(typeof(DataGridColumnHeader), baseHeaderStyle);

                headerStyle.Setters.Add(new Setter(ToolTipProperty, new TextBlock()
                {
                    Text = "Enter the ordinate values in ascending order.",
                    FontWeight = FontWeights.Normal,
                    TextAlignment = TextAlignment.Left,
                    TextWrapping = TextWrapping.Wrap
                }));
                ((DataGridTextColumn)e.Column).HeaderStyle = headerStyle;
            }
        }

        /// <summary>
        /// Handles the addition of new rows to the data grid.
        /// Synchronizes the addition of new ordinates to the underlying model.
        /// </summary>
        /// <param name="startRowIndex">The index where the first new row was added.</param>
        /// <param name="nRows">The number of rows that were added.</param>
        private void DataGrid_RowsAdded(int startRowIndex, int nRows)
        {
            _suppressUIUpdate = true;
            //bool wasSuppressed = Ordinates.SuppressCollectionChanged;
            //Ordinates.SuppressCollectionChanged = true;
            for (int i = 0; i < nRows; i++)
            {
                Ordinates.Insert(startRowIndex + i, ((OrdinateRowItem)_rowItems[startRowIndex + i]).Value);
            }
            //Ordinates.SuppressCollectionChanged = wasSuppressed;
            //if (!wasSuppressed)
            //{
                //Ordinates.RaiseCollectionChangedReset();
            //}
            _suppressUIUpdate = false;
        }

        /// <summary>
        /// Handles the deletion of rows from the data grid.
        /// Synchronizes the removal of ordinates from the underlying model.
        /// </summary>
        /// <param name="rowindices">The list of row indices that were deleted.</param>
        private void DataGrid_RowsDeleted(List<int> rowindices)
        {
            _suppressUIUpdate = true;
            //bool wasSuppressed = Ordinates.SuppressCollectionChanged;
            //Ordinates.SuppressCollectionChanged = true;
            for (int i = rowindices.Count - 1; i >= 0; i--)
            {
                Ordinates.RemoveAt(rowindices[i]);
            }
            //Ordinates.SuppressCollectionChanged = wasSuppressed;
            //if (!wasSuppressed)
            //{
            //    Ordinates.RaiseCollectionChangedReset();
            //}
            _suppressUIUpdate = false;
        }

        /// <summary>
        /// Handles collection change events for the row items.
        /// Subscribes or unsubscribes property change handlers for items as they are added or removed.
        /// </summary>
        /// <param name="sender">The observable collection that changed.</param>
        /// <param name="e">Event arguments containing information about the collection change.</param>
        private void RowItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (OrdinateRowItem item in e.NewItems)
                {
                    item.PropertyChanged += OrdinateRowItem_PropertyChanged;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (OrdinateRowItem item in e.OldItems)
                {
                    item.PropertyChanged -= OrdinateRowItem_PropertyChanged;
                }
            }
        }

        /// <summary>
        /// Handles property change events for individual ordinate row items.
        /// Updates the underlying model when a value changes in the UI.
        /// </summary>
        /// <param name="sender">The ordinate row item that changed.</param>
        /// <param name="e">Event arguments containing the name of the property that changed.</param>
        private void OrdinateRowItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(OrdinateRowItem.Value)) return;
            if (_suppressModelUpdate) return;

            int rowIndex = _rowItems.IndexOf((OrdinateRowItem)sender);
            if (rowIndex >= 0 && rowIndex < Ordinates.Count)
            {
                _suppressUIUpdate = true;
                Ordinates[rowIndex] = ((OrdinateRowItem)sender).Value;
                _suppressUIUpdate = false;
            }
        }

        /// <summary>
        /// Handles collection change events for the ordinates in the model.
        /// Synchronizes changes from the model to the UI by updating the row items collection.
        /// </summary>
        /// <param name="sender">The ordinates collection that changed.</param>
        /// <param name="e">Event arguments containing information about the collection change.</param>
        private void Ordinates_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressUIUpdate == false)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        _rowItems.Insert(e.NewStartingIndex + i, CreateRowItem((double)e.NewItems[i]));
                    }
                }
                else if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    for (int i = e.OldItems.Count - 1; i >= 0; i--)
                    {
                        _rowItems.RemoveAt(e.OldStartingIndex + i);
                    }
                }
                else if (e.Action == NotifyCollectionChangedAction.Replace)
                {
                    _suppressModelUpdate = true;
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        ((OrdinateRowItem)_rowItems[e.NewStartingIndex + i]).Value = (double)e.NewItems[i];
                    }
                    _suppressModelUpdate = false;
                }
                else if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    // Rebuild UI row items from the model state
                    _suppressModelUpdate = true;
                    _rowItems.Clear();
                    if (Ordinates != null)
                    {
                        foreach (double ordinate in Ordinates)
                        {
                            _rowItems.Add(CreateRowItem(ordinate));
                        }
                    }
                    DataGrid.ItemsSource = _rowItems;
                    _suppressModelUpdate = false;
                }
            }
        }

        /// <summary>
        /// Handles the preview paste data event from the data grid.
        /// Suppresses collection changed events during the paste operation to prevent
        /// individual events for each cell change.
        /// </summary>
        /// <param name="clipboardData">The clipboard data being pasted.</param>
        /// <param name="cancelPaste">Reference parameter to cancel the paste operation if needed.</param>
        private void DataGrid_PreviewPasteData(string[][] clipboardData, ref bool cancelPaste)
        {
            //if (Ordinates != null)
            //    Ordinates.SuppressCollectionChanged = true;
            Mouse.OverrideCursor = Cursors.Wait;
        }

        /// <summary>
        /// Handles the data pasted event from the data grid.
        /// Re-enables collection changed events and raises a single Reset event to notify
        /// listeners that the collection has been modified.
        /// </summary>
        private void DataGrid_DataPasted()
        {
            //if (Ordinates != null)
            //{
            //    Ordinates.SuppressCollectionChanged = false;
            //    Ordinates.RaiseCollectionChangedReset();
            //}
            Mouse.OverrideCursor = null;
        }

    }
}
