using FrameworkInterfaces;
using RMC.BestFit.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents the method that will handle the event when a time series alternative is added.
    /// </summary>
    /// <param name="item">The time series alternative item that was added.</param>
    public delegate void TimeSeriesAddedEventHandler(TimeSeriesAlternativeItem item);

    /// <summary>
    /// Represents the method that will handle the event when a time series alternative is removed.
    /// </summary>
    /// <param name="item">The time series alternative item that was removed.</param>
    public delegate void TimeSeriesRemovedEventHandler(TimeSeriesAlternativeItem item);

    /// <summary>
    /// User control for managing and displaying alternative time series for comparison purposes.
    /// Provides functionality to select and compare multiple time series elements
    /// against the current time series element on the Time Series plot.
    /// </summary>
    /// <remarks>
    /// This control follows the same pattern as <see cref="AlternativeControl"/> but is adapted for
    /// <see cref="TimeSeriesElement"/> instead of <see cref="IAnalysisElement"/>. Since time series
    /// data is raw observational data (no Bayesian analysis), the control is simpler: each alternative
    /// has a single <see cref="OxyPlot.Wpf.LineSeries"/> rather than credible intervals and posteriors.
    /// </remarks>
    public partial class AlternativeTimeSeriesControl : UserControl
    {
        /// <summary>
        /// Tracks the parent collection currently subscribed to for ElementAdded/Removed events.
        /// </summary>
        private IElementCollection _subscribedCollection;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlternativeTimeSeriesControl"/> class.
        /// </summary>
        public AlternativeTimeSeriesControl()
        {
            InitializeComponent();
            DataContext = this;
            Unloaded += UserControl_Unloaded;
        }

        /// <summary>
        /// Handles the Unloaded event. Unsubscribes from parent collection events.
        /// </summary>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeCollection();
        }

        /// <summary>
        /// Unsubscribes from the currently tracked parent collection's ElementAdded/Removed events.
        /// </summary>
        private void UnsubscribeCollection()
        {
            if (_subscribedCollection != null)
            {
                _subscribedCollection.ElementAdded -= OnElementAdded;
                _subscribedCollection.ElementRemoved -= OnElementRemoved;
                _subscribedCollection = null;
            }
        }

        /// <summary>
        /// Identifies the <see cref="Element"/> dependency property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(TimeSeriesElement), typeof(AlternativeTimeSeriesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the current time series element that alternative time series are compared against.
        /// </summary>
        public TimeSeriesElement Element
        {
            get { return (TimeSeriesElement)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Element"/> property changes.
        /// Handles cleanup of old element handlers and initialization of new element.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AlternativeTimeSeriesControl thisControl) return;

            // Remove handlers from old element
            if (e.OldValue is TimeSeriesElement)
            {
                thisControl.UnsubscribeCollection();
                // Detach PropertyChanged from all existing items
                foreach (var item in thisControl.TimeSeriesList)
                {
                    item.PropertyChanged -= thisControl.Alternative_PropertyChanged;
                    item.Detach();
                }
                thisControl.TimeSeriesList.Clear();
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as TimeSeriesElement;
            if (newElement == null) return;

            thisControl.LoadTimeSeries();
        }

        /// <summary>
        /// Gets the observable collection of time series alternative items available for comparison.
        /// </summary>
        public ObservableCollection<TimeSeriesAlternativeItem> TimeSeriesList { get; private set; } = new ObservableCollection<TimeSeriesAlternativeItem>();

        /// <summary>
        /// Occurs when a time series alternative is added to the comparison set.
        /// </summary>
        public event TimeSeriesAddedEventHandler TimeSeriesAdded;

        /// <summary>
        /// Occurs when a time series alternative is removed from the comparison set.
        /// </summary>
        public event TimeSeriesRemovedEventHandler TimeSeriesRemoved;

        /// <summary>
        /// Loads all available time series alternatives from the parent collection, excluding the current element.
        /// Sets up event handlers for collection changes and property changes on individual alternatives.
        /// </summary>
        private void LoadTimeSeries()
        {
            TimeSeriesList.Clear();

            // Subscribe to parent collection using named methods (allows unsubscription)
            UnsubscribeCollection();
            _subscribedCollection = Element.ParentCollection;
            _subscribedCollection.ElementAdded += OnElementAdded;
            _subscribedCollection.ElementRemoved += OnElementRemoved;

            foreach (IElement element in Element.ParentCollection)
            {
                if (element is TimeSeriesElement tsElement && element.NameOnDisk != Element.NameOnDisk)
                {
                    var item = new TimeSeriesAlternativeItem(tsElement);
                    item.PropertyChanged += Alternative_PropertyChanged;
                    TimeSeriesList.Add(item);
                }
            }
        }

        /// <summary>
        /// Handles ElementAdded on the parent collection. Adds a new alternative item if it's a TimeSeriesElement.
        /// </summary>
        private void OnElementAdded(IElement x)
        {
            if (x is TimeSeriesElement tsElement)
            {
                var item = new TimeSeriesAlternativeItem(tsElement);
                item.PropertyChanged += Alternative_PropertyChanged;
                TimeSeriesList.Add(item);
            }
        }

        /// <summary>
        /// Handles ElementRemoved on the parent collection. Removes the matching alternative item.
        /// </summary>
        private void OnElementRemoved(IElement x)
        {
            if (x is TimeSeriesElement)
            {
                foreach (TimeSeriesAlternativeItem item in TimeSeriesList)
                {
                    if (item.Alternative.NameOnDisk == x.NameOnDisk)
                    {
                        item.PropertyChanged -= Alternative_PropertyChanged;
                        item.Detach();
                        TimeSeriesList.Remove(item);
                        TimeSeriesRemoved?.Invoke(item);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Handles property changes on time series alternative items.
        /// Raises <see cref="TimeSeriesAdded"/> or <see cref="TimeSeriesRemoved"/> events based on
        /// the IsChecked state, and refreshes when time series data changes.
        /// </summary>
        /// <param name="sender">The time series alternative item whose property changed.</param>
        /// <param name="e">Event arguments containing the property name that changed.</param>
        private void Alternative_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var item = (TimeSeriesAlternativeItem)sender;
            if (e.PropertyName == nameof(TimeSeriesAlternativeItem.IsChecked))
            {
                if (item.IsChecked == true)
                {
                    TimeSeriesAdded?.Invoke(item);
                }
                else
                {
                    TimeSeriesRemoved?.Invoke(item);
                }
            }
            else if (e.PropertyName == nameof(TimeSeriesAlternativeItem.Alternative.TimeSeries))
            {
                if (item.IsChecked == true)
                {
                    TimeSeriesAdded?.Invoke(item);
                }
            }
        }

        /// <summary>
        /// Adds all alternatives to the plot that are currently checked on.
        /// </summary>
        public void AddAllChecked()
        {
            foreach (TimeSeriesAlternativeItem item in TimeSeriesList)
            {
                if (item.IsChecked == true)
                {
                    TimeSeriesAdded?.Invoke(item);
                }
            }
        }

        /// <summary>
        /// Removes all alternatives from the plot.
        /// </summary>
        public void RemoveAll()
        {
            foreach (TimeSeriesAlternativeItem item in TimeSeriesList)
            {
                TimeSeriesRemoved?.Invoke(item);
            }
        }

        /// <summary>
        /// Handles the Loaded event of the alternative list box.
        /// Sets up the collection view with sorting by time series name in ascending order.
        /// </summary>
        /// <param name="sender">The list view control that was loaded.</param>
        /// <param name="e">Event arguments for the loaded event.</param>
        private void AlternativeListBox_Loaded(object sender, RoutedEventArgs e)
        {
            ListView listBox = (ListView)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = TimeSeriesList, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription("Alternative.Name", ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            listBox.ItemsSource = view;
        }
    }
}
