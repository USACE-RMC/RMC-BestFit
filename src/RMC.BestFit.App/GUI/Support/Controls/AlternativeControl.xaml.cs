using FrameworkInterfaces;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace RMC_BestFit
{
    /// <summary>
    /// Represents the method that will handle the event when an analysis alternative is added.
    /// </summary>
    /// <param name="analysisItem">The analysis alternative item that was added.</param>
    public delegate void AnalysisAddedEventHandler(AnalysisAlternativeItem analysisItem);

    /// <summary>
    /// Represents the method that will handle the event when an analysis alternative is removed.
    /// </summary>
    /// <param name="analysisItem">The analysis alternative item that was removed.</param>
    public delegate void AnalysisRemovedEventHandler(AnalysisAlternativeItem analysisItem);

    /// <summary>
    /// User control for managing and displaying alternative analyses for comparison purposes.
    /// Provides functionality to select, check, and compare multiple analysis alternatives
    /// against the current analysis element.
    /// </summary>
    public partial class AlternativeControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AlternativeControl"/> class.
        /// </summary>
        public AlternativeControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Identifies the <see cref="Element"/> dependency property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(IAnalysisElement), typeof(AlternativeControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the current analysis element that alternative analyses are compared against.
        /// </summary>
        public IAnalysisElement Element
        {
            get { return (IAnalysisElement)GetValue(ElementProperty); }
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
            if (d == null) return;
            if (d as AlternativeControl == null) return;
            var thisControl = (AlternativeControl)d;

            // Remove handlers
            if (e.OldValue != null)
            {
                IAnalysisElement oldElement = e.OldValue as IAnalysisElement;
                if (oldElement != null)
                {
                    thisControl.UnsubscribeParentCollection();
                    thisControl.ClearAnalysisListSubscriptions();
                }
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as IAnalysisElement;
            if (newElement == null) return;

            thisControl.LoadAnalyses();

        }

        /// <summary>
        /// Tracks the parent collection currently subscribed to for ElementAdded/ElementRemoved
        /// events so the subscription can be cleanly torn down when <see cref="Element"/> swaps.
        /// </summary>
        private IElementCollection _subscribedParentCollection;

        /// <summary>
        /// Gets the observable collection of analysis alternative items available for comparison.
        /// </summary>
        public ObservableCollection<AnalysisAlternativeItem> AnalysisList { get; private set; } = new ObservableCollection<AnalysisAlternativeItem>();

        /// <summary>
        /// Gets or sets an optional runtime-type filter for the alternatives list. When non-null,
        /// only sibling elements whose <see cref="object.GetType"/> matches this value are
        /// surfaced as alternatives — useful when an analysis should only be compared against
        /// other analyses of the same kind (e.g. a Coincident Frequency analysis comparing
        /// only to other Coincident Frequency analyses, not arbitrary bivariate analyses).
        /// </summary>
        /// <remarks>
        /// Must be set before <see cref="Element"/> is assigned (typically in the parent
        /// control's constructor) so the filter is in effect when <see cref="LoadAnalyses"/>
        /// runs. Defaults to null (no filtering — all <see cref="IAnalysisElement"/> siblings
        /// are accepted, preserving the original behavior used by Composite/Univariate/B17C/etc.).
        /// </remarks>
        public Type AlternativeFilterType { get; set; }

        /// <summary>
        /// Occurs when an analysis alternative is added to the comparison set.
        /// </summary>
        public event AnalysisAddedEventHandler AnalysisAdded;

        /// <summary>
        /// Occurs when an analysis alternative is removed from the comparison set.
        /// </summary>
        public event AnalysisRemovedEventHandler AnalysisRemoved;

        /// <summary>
        /// Loads all available analysis alternatives from the parent collection, excluding the current element.
        /// Sets up event handlers for collection changes and property changes on individual alternatives.
        /// Idempotent: defensively unsubscribes prior subscriptions before re-binding so repeated
        /// Element swaps do not accumulate stale handlers (NC-8).
        /// </summary>
        private void LoadAnalyses()
        {
            UnsubscribeParentCollection();
            ClearAnalysisListSubscriptions();
            AnalysisList.Clear();

            _subscribedParentCollection = Element.ParentCollection;
            _subscribedParentCollection.ElementAdded += OnParentCollectionElementAdded;
            _subscribedParentCollection.ElementRemoved += OnParentCollectionElementRemoved;

            foreach (IElement element in Element.ParentCollection)
            {
                if (element as IAnalysisElement != null && element.NameOnDisk != Element.NameOnDisk && PassesFilter(element))
                {
                    AnalysisList.Add(new AnalysisAlternativeItem((IAnalysisElement)element));
                    AnalysisList.Last().PropertyChanged += Alternative_PropertyChanged;
                }
            }
        }

        /// <summary>
        /// Handles ElementAdded events from the subscribed ParentCollection. Adds the new
        /// element to <see cref="AnalysisList"/> if it is an IAnalysisElement and passes the
        /// optional <see cref="AlternativeFilterType"/> filter.
        /// </summary>
        /// <param name="x">The element that was added to the parent collection.</param>
        private void OnParentCollectionElementAdded(IElement x)
        {
            if (x as IAnalysisElement != null && PassesFilter(x))
            {
                AnalysisList.Add(new AnalysisAlternativeItem((IAnalysisElement)x));
                AnalysisList.Last().PropertyChanged += Alternative_PropertyChanged;
            }
        }

        /// <summary>
        /// Handles ElementRemoved events from the subscribed ParentCollection. Removes the
        /// matching item from <see cref="AnalysisList"/>, unhooks its PropertyChanged handler,
        /// and notifies subscribers via <see cref="AnalysisRemoved"/>.
        /// </summary>
        /// <param name="x">The element that was removed from the parent collection.</param>
        private void OnParentCollectionElementRemoved(IElement x)
        {
            if (x as IAnalysisElement != null)
            {
                AnalysisAlternativeItem item = AnalysisList.FirstOrDefault(alternative => alternative.Alternative.NameOnDisk == x.NameOnDisk);
                if (item != null)
                {
                    item.PropertyChanged -= Alternative_PropertyChanged;
                    AnalysisList.Remove(item);
                    AnalysisRemoved?.Invoke(item);
                    item.Dispose();
                }
            }
        }

        /// <summary>
        /// Removes ElementAdded/ElementRemoved subscriptions from the previously tracked
        /// parent collection. Safe to call when <see cref="_subscribedParentCollection"/>
        /// is null.
        /// </summary>
        private void UnsubscribeParentCollection()
        {
            if (_subscribedParentCollection != null)
            {
                _subscribedParentCollection.ElementAdded -= OnParentCollectionElementAdded;
                _subscribedParentCollection.ElementRemoved -= OnParentCollectionElementRemoved;
                _subscribedParentCollection = null;
            }
        }

        /// <summary>
        /// Detaches the per-item PropertyChanged handler from every alternative currently
        /// in <see cref="AnalysisList"/>. Called before clearing the list so item-level
        /// subscriptions don't outlive the items.
        /// </summary>
        private void ClearAnalysisListSubscriptions()
        {
            foreach (AnalysisAlternativeItem item in AnalysisList)
            {
                item.PropertyChanged -= Alternative_PropertyChanged;
                item.Dispose();
            }
        }

        /// <summary>
        /// Returns true when the candidate element should be surfaced as an alternative.
        /// Honors <see cref="AlternativeFilterType"/> — when null, all candidates pass.
        /// </summary>
        /// <param name="candidate">The candidate sibling element being considered.</param>
        /// <returns>True if the candidate's runtime type matches the configured filter, or no filter is set.</returns>
        private bool PassesFilter(IElement candidate)
        {
            if (AlternativeFilterType == null) return true;
            return candidate != null && candidate.GetType() == AlternativeFilterType;
        }

        /// <summary>
        /// Handles property changes on analysis alternative items.
        /// Raises <see cref="AnalysisAdded"/> or <see cref="AnalysisRemoved"/> events based on
        /// the IsChecked state, and refreshes when analysis results or estimation status changes.
        /// </summary>
        /// <param name="sender">The analysis alternative item whose property changed.</param>
        /// <param name="e">Event arguments containing the property name that changed.</param>
        private void Alternative_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var item = (AnalysisAlternativeItem)sender;
            if (e.PropertyName == nameof(AnalysisAlternativeItem.IsChecked))
            {
                if (item.IsChecked == true)
                {
                    AnalysisAdded?.Invoke(item);
                }
                else
                {
                    AnalysisRemoved?.Invoke(item);
                }
            }
            else if (e.PropertyName == nameof(AnalysisAlternativeItem.Alternative.AnalysisResults) ||
                e.PropertyName == nameof(AnalysisAlternativeItem.Alternative.IsEstimated))
            {
                if (item.IsChecked == true)
                {
                    AnalysisAdded?.Invoke(item);
                }
            }
        }

        /// <summary>
        /// Add all alternatives to plot that are checked on.
        /// </summary>
        public void AddAllChecked()
        {
            foreach (AnalysisAlternativeItem item in AnalysisList)
            {
                if (item.IsChecked == true)
                {
                    AnalysisAdded?.Invoke(item);
                }
            }
        }

        /// <summary>
        /// Remove all alternatives from plot.
        /// </summary>
        public void RemoveAll()
        {
            foreach (AnalysisAlternativeItem item in AnalysisList)
            {
                AnalysisRemoved?.Invoke(item);
            }
        }

        /// <summary>
        /// Handles the Loaded event of the alternative list box.
        /// Sets up the collection view with sorting by analysis name in ascending order.
        /// </summary>
        /// <param name="sender">The list view control that was loaded.</param>
        /// <param name="e">Event arguments for the loaded event.</param>
        private void AlternativeListBox_Loaded(object sender, RoutedEventArgs e)
        {
            ListView listBox = (ListView)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = AnalysisList, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(AnalysisAlternativeItem.Alternative.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            listBox.ItemsSource = view;
        }
    }
}
