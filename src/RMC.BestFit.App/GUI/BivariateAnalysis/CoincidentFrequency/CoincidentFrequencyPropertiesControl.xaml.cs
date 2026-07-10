using FrameworkInterfaces;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and editing the properties of a
    /// <see cref="CoincidentFrequencyAnalysis"/>: upstream link, X / Y ordinates,
    /// the M×N response surface, output bin count, and credible-interval width.
    /// </summary>
    public partial class CoincidentFrequencyPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CoincidentFrequencyPropertiesControl"/> class.
        /// </summary>
        public CoincidentFrequencyPropertiesControl()
        {
            InitializeComponent();
            // DataContext must be set AFTER InitializeComponent() to avoid NullReferenceExceptions
            // from bindings that fire during XAML parsing when Element is still null.
            DataContext = this;
        }

        #region Element DependencyProperty + ElementCallback

        /// <summary>Stores the previous name for rollback when validation fails.</summary>
        private string _previousName;

        /// <summary>
        /// Tracks the <see cref="InputDataCollection"/> currently subscribed via
        /// <see cref="OnInputDataElementAdded"/> / <see cref="OnInputDataElementRemoved"/>.
        /// Used by <see cref="UnsubscribeInputDataCollection"/> to reliably unhook without
        /// capturing a lambda that cannot be unhooked.
        /// </summary>
        private IElementCollection _subscribedInputDataCollection;

        /// <summary>The inner ComboBox used for optional input-data overlay selection.</summary>
        private ComboBox _inputDataInnerComboBox;

        /// <summary>
        /// Tracks the <see cref="BivariateAnalysisCollection"/> currently subscribed via
        /// <see cref="OnBivariateAnalysisElementAdded"/> / <see cref="OnBivariateAnalysisElementRemoved"/>.
        /// </summary>
        private IElementCollection _subscribedBivariateAnalysesCollection;

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(
            nameof(Element),
            typeof(CoincidentFrequencyAnalysis),
            typeof(CoincidentFrequencyPropertiesControl),
            new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the coincident frequency analysis element whose properties are displayed.
        /// </summary>
        public CoincidentFrequencyAnalysis Element
        {
            get { return (CoincidentFrequencyAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback invoked when the <see cref="Element"/> dependency property changes.
        /// </summary>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as CoincidentFrequencyPropertiesControl == null) return;
            var thisControl = (CoincidentFrequencyPropertiesControl)d;

            // Unsubscribe from old element
            if (e.OldValue is CoincidentFrequencyAnalysis oldElement)
            {
                oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
            }

            // Unsubscribe collection trackers before re-subscribing for the new element.
            thisControl.UnsubscribeInputDataCollection();
            thisControl.UnsubscribeBivariateAnalysesCollection();
            thisControl.ClearInputDataSelectionItems();

            if (e.NewValue == null) return;
            var newElement = e.NewValue as CoincidentFrequencyAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Element_PropertyChanged;
            thisControl._previousName = newElement.Name;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadInputData();
            thisControl.LoadBivariateAnalyses();
            // X / Y ordinate grids are owned by the two <local:OrdinatesControl> instances
            // bound to Element.XValues / Element.YValues — they handle their own model↔UI
            // sync via the OrdinatesProperty DependencyProperty callback.
            //
            // Reflect the initial validity of InputData / BivariateAnalysis in the combo
            // borders. Without these calls, the XAML default red border stays visible
            // after a saved element is reopened with a valid selection.
            thisControl.UpdateInputDataValidationBorder();
            thisControl.UpdateAnalysisValidationBorder();
            thisControl.SelectCurrentInputDataItem(thisControl._inputDataInnerComboBox);
        }

        /// <summary>
        /// Forwards element property changes into UI sync routines. The M×N response grid
        /// lives in the document control (CoincidentFrequencyControl), not here, so this
        /// handler just marshals to the UI thread for any property-bound state on this side.
        /// </summary>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Element_PropertyChanged(sender, e)));
                return;
            }
            // The response grid is in the document control. X/Y wrapper rows are synced via
            // Model{X,Y}Values_CollectionChanged below; only property-bound combo state lives here.
            if (e.PropertyName == nameof(Element.InputData))
            {
                UpdateInputDataValidationBorder();
                SelectCurrentInputDataItem(_inputDataInnerComboBox);
            }
        }

        #endregion

        #region Combo box option lists (static — referenced by XAML bindings)

        /// <summary>List of optional input-data overlay choices.</summary>
        public ObservableCollection<InputDataSelectionItem> InputDataList { get; private set; }
            = new ObservableCollection<InputDataSelectionItem>();

        /// <summary>List of fitted bivariate analyses available as upstream input.</summary>
        public ObservableCollection<BivariateAnalysis> BivariateAnalysisList { get; private set; }
            = new ObservableCollection<BivariateAnalysis>();

        /// <summary>
        /// Credible interval width options exposed to the <see cref="BayesianOutputControl"/> ComboBox.
        /// Allocated once per class (not per instance) to avoid unnecessary allocations on
        /// every XAML binding call. Matches the canonical pattern used by
        /// <c>InputDataPropertiesControl</c>.
        /// </summary>
        private static readonly ObservableCollection<CredibleIntervalItem> _credibleIntervalItems
            = new ObservableCollection<CredibleIntervalItem>
            {
                new CredibleIntervalItem("90%", 0.90),
                new CredibleIntervalItem("95%", 0.95),
                new CredibleIntervalItem("98%", 0.98),
                new CredibleIntervalItem("99%", 0.99),
            };

        /// <summary>Gets the credible interval width options for the CI ComboBox.</summary>
        public ObservableCollection<CredibleIntervalItem> CredibleIntervalItems => _credibleIntervalItems;

        private static readonly ObservableCollection<PointEstimatorItem> _pointEstimatorItems
            = new ObservableCollection<PointEstimatorItem>()
            {
                new PointEstimatorItem("Posterior Mean", BayesianAnalysis.PointEstimateType.PosteriorMean, "Calculates the average of all posterior samples, providing a balanced summary of the parameter estimates."),
                new PointEstimatorItem("Posterior Mode", BayesianAnalysis.PointEstimateType.PosteriorMode, "Selects the most likely parameter values for the posterior distribution (the Maximum A Posteriori estimate)."),
            };

        /// <summary>
        /// Gets the observable collection of point estimator options for Bayesian analysis.
        /// </summary>
        public ObservableCollection<PointEstimatorItem> PointEstimatorItems => _pointEstimatorItems;


        #endregion

        #region Property attributes

        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>Name</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }
        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>Description</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }
        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>InputDataComboBox</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void InputDataComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetPropertyAttributes(nameof(Element.InputData), Element);
        }
        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>CreationDate</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }
        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>LastModified</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }
        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>AnalysisComboBox</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void AnalysisComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetPropertyAttributes(nameof(Element.BivariateAnalysis), Element);
        }
        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>XOrdinateDataGrid</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void XOrdinateDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetPropertyAttributes(nameof(Element.XValues), Element);
        }
        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>YOrdinateDataGrid</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void YOrdinateDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetPropertyAttributes(nameof(Element.YValues), Element);
        }
        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CredibleInterval control. Displays property attributes for the credible interval width.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void CredibleInterval_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.BayesianAnalysis.CredibleIntervalWidth), Element.BayesianAnalysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the PointEstimator control. Displays property attributes for the point estimator type.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void PointEstimator_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.BayesianAnalysis.PointEstimator), Element.BayesianAnalysis);
        }
        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>NumberOfBins</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void NumberOfBins_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetPropertyAttributes(nameof(Element.NumberOfBins), Element);
        }
        /// <summary>
        /// Handles the <c>PreviewMouseLeftButtonDown</c> event for <c>TabItem</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element != null) PropertyAttributes.GetClassAttributes(Element);
        }

        #endregion

        #region Name focus / rollback

        /// <summary>
        /// Handles the <c>GotFocus</c> event for <c>Name</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            if (Element != null) _previousName = Element.Name;
        }

        /// <summary>
        /// Handles the <c>LostFocus</c> event for <c>Name</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void Name_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            if (string.IsNullOrEmpty(Element.Name) || Element.Name == _previousName) return;

            // Validate uniqueness in parent collection.
            foreach (IElement sibling in Element.ParentCollection)
            {
                if (!ReferenceEquals(sibling, Element) && sibling.Name == Element.Name)
                {
                    GenericControls.MessageBox.Show(
                        $"An element named '{Element.Name}' already exists. Reverting.",
                        "Duplicate Name",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    Element.Name = _previousName;
                    return;
                }
            }
            _previousName = Element.Name;
        }

        #endregion

        #region Loaded / Unloaded

        /// <summary>
        /// Handles the Loaded event. Collection subscriptions are set up in
        /// <see cref="ElementCallback"/>; this method is a no-op for future use.
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // No-op: collection subscriptions are owned by ElementCallback.
        }

        /// <summary>
        /// Handles the Unloaded event. Unsubscribes the collection change handlers to prevent
        /// memory leaks and clears input-data selection wrappers when this control is replaced.
        /// </summary>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeInputDataCollection();
            UnsubscribeBivariateAnalysesCollection();
            ClearInputDataSelectionItems();
        }

        #endregion

        #region Input data combo

        /// <summary>
        /// Populates <see cref="InputDataList"/> from the project's <see cref="InputDataCollection"/>.
        /// Subscribes <see cref="OnInputDataElementAdded"/> / <see cref="OnInputDataElementRemoved"/>
        /// via named methods so the subscription can be reliably unhooked by
        /// <see cref="UnsubscribeInputDataCollection"/>. InputData is optional on CFA — it
        /// provides an observed-data overlay on the Frequency Plot.
        /// </summary>
        private void LoadInputData()
        {
            UnsubscribeInputDataCollection();
            ClearInputDataSelectionItems();
            InputDataList.Add(InputDataSelectionItem.CreateNone());
            if (Element == null) return;
            foreach (IElementCollection collection in Element.ParentCollection.ParentProject.ElementCollections)
            {
                if (collection.GetType() == typeof(InputDataCollection))
                {
                    _subscribedInputDataCollection = collection;
                    collection.ElementAdded += OnInputDataElementAdded;
                    collection.ElementRemoved += OnInputDataElementRemoved;
                    foreach (IElement element in collection)
                        if (element is InputData id) AddInputDataSelectionItem(id);
                    break;
                }
            }
        }

        /// <summary>
        /// Handles an element being added to the <see cref="InputDataCollection"/>.
        /// </summary>
        private void OnInputDataElementAdded(IElement element)
        {
            if (element is InputData id) AddInputDataSelectionItem(id);
        }

        /// <summary>
        /// Handles an element being removed from the <see cref="InputDataCollection"/>.
        /// </summary>
        private void OnInputDataElementRemoved(IElement element)
        {
            if (element is InputData id) RemoveInputDataSelectionItem(id);
        }

        /// <summary>
        /// Unsubscribes the named handlers from the tracked <see cref="InputDataCollection"/>
        /// and clears the tracker field.
        /// </summary>
        private void UnsubscribeInputDataCollection()
        {
            if (_subscribedInputDataCollection != null)
            {
                _subscribedInputDataCollection.ElementAdded -= OnInputDataElementAdded;
                _subscribedInputDataCollection.ElementRemoved -= OnInputDataElementRemoved;
                _subscribedInputDataCollection = null;
            }
        }

        /// <summary>
        /// Adds a real input-data selection item if it is not already represented.
        /// </summary>
        /// <param name="inputData">The input data element to add.</param>
        /// <remarks>
        /// The no-overlay item is created only by <see cref="LoadInputData"/>; this helper
        /// is for project collection additions.
        /// </remarks>
        private void AddInputDataSelectionItem(InputData inputData)
        {
            if (FindInputDataSelectionItem(inputData) != null) return;
            InputDataList.Add(new InputDataSelectionItem(inputData));
        }

        /// <summary>
        /// Removes and disposes a real input-data selection item.
        /// </summary>
        /// <param name="inputData">The input data element to remove.</param>
        /// <remarks>
        /// Disposing the wrapper releases the rename subscription held for ComboBox display.
        /// </remarks>
        private void RemoveInputDataSelectionItem(InputData inputData)
        {
            var item = FindInputDataSelectionItem(inputData);
            if (item == null) return;
            InputDataList.Remove(item);
            item.Dispose();
        }

        /// <summary>
        /// Finds the selection item wrapping the supplied input data reference.
        /// </summary>
        /// <param name="inputData">The input data reference to find.</param>
        /// <returns>The matching selection item, or <c>null</c> when absent.</returns>
        /// <remarks>
        /// Reference matching preserves identity semantics used by the analysis wrapper.
        /// </remarks>
        private InputDataSelectionItem FindInputDataSelectionItem(InputData inputData)
        {
            foreach (InputDataSelectionItem item in InputDataList)
            {
                if (ReferenceEquals(item.Value, inputData))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Disposes all current input-data selection items and clears the list.
        /// </summary>
        /// <remarks>
        /// This is called before rebuilding the list and when the control unloads to avoid
        /// retaining stale wrappers through input-data rename subscriptions.
        /// </remarks>
        private void ClearInputDataSelectionItems()
        {
            foreach (InputDataSelectionItem item in InputDataList)
            {
                item.Dispose();
            }

            InputDataList.Clear();
        }

        /// <summary>
        /// Handles loading of the optional input-data ComboBox.
        /// </summary>
        /// <param name="sender">The ComboBox being loaded.</param>
        /// <param name="e">The routed event data.</param>
        /// <remarks>
        /// CFA keeps the project collection order for real input data and pins the no-overlay
        /// item first by insertion order.
        /// </remarks>
        private void InputDataComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cmbo)
            {
                _inputDataInnerComboBox = cmbo;
                cmbo.ItemsSource = InputDataList;
                SelectCurrentInputDataItem(cmbo);
            }
        }

        /// <summary>
        /// Validates the InputData selection and toggles the red border and tooltip on the
        /// surrounding content property control accordingly. Mirrors
        /// <see cref="CompositeAnalysisPropertiesControl.InputDataComboBox_SelectionChanged"/>.
        /// </summary>
        /// <param name="sender">The ComboBox raising the event.</param>
        /// <param name="e">The selection-changed event data.</param>
        private void InputDataComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateInputDataValidationBorder();
        }

        /// <summary>
        /// Toggles the red border + tooltip on <see cref="InputDataComboBox"/> based on
        /// whether <see cref="CoincidentFrequencyAnalysis.InputData"/> is non-null. Called
        /// from the SelectionChanged handler AND from <see cref="ElementCallback"/> after
        /// the new element binds, so the border reflects the initial state — not just
        /// later user-driven changes.
        /// </summary>
        private void UpdateInputDataValidationBorder()
        {
            if (Element == null) return;
            if (Element.InputData != null && Element.InputData.Name == null)
            {
                ((Border)InputDataComboBox.InnerContent).BorderThickness = new Thickness(1);
                InputDataComboBox.ToolTip = "Please select a valid input data.";
            }
            else
            {
                ((Border)InputDataComboBox.InnerContent).BorderThickness = new Thickness(0);
                InputDataComboBox.ToolTip = null;
            }
        }

        /// <summary>
        /// Selects the ComboBox item that matches the element's current input-data reference.
        /// </summary>
        /// <param name="comboBox">The ComboBox to synchronize.</param>
        /// <remarks>
        /// WPF can treat a <c>null</c> SelectedValue as no selection, so this method explicitly
        /// selects the UI-only <c>&lt;None&gt;</c> item when the analysis overlay is null.
        /// </remarks>
        private void SelectCurrentInputDataItem(ComboBox comboBox)
        {
            if (comboBox == null || Element == null) return;
            foreach (object comboBoxItem in comboBox.Items)
            {
                if (comboBoxItem is InputDataSelectionItem item && ReferenceEquals(item.Value, Element.InputData))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        #endregion

        #region Bivariate analysis combo

        /// <summary>
        /// Populates <see cref="BivariateAnalysisList"/> from the project's
        /// <see cref="BivariateAnalysisCollection"/>. Subscribes named handlers so the
        /// subscription can be reliably unhooked by <see cref="UnsubscribeBivariateAnalysesCollection"/>.
        /// </summary>
        private void LoadBivariateAnalyses()
        {
            BivariateAnalysisList.Clear();
            if (Element == null) return;
            foreach (IElementCollection collection in Element.ParentCollection.ParentProject.ElementCollections)
            {
                if (collection.GetType() == typeof(BivariateAnalysisCollection))
                {
                    UnsubscribeBivariateAnalysesCollection();
                    _subscribedBivariateAnalysesCollection = collection;
                    collection.ElementAdded += OnBivariateAnalysisElementAdded;
                    collection.ElementRemoved += OnBivariateAnalysisElementRemoved;
                    foreach (IElement element in collection)
                    {
                        if (element is BivariateAnalysis ba && !ReferenceEquals(ba, Element))
                            BivariateAnalysisList.Add(ba);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Handles an element being added to the <see cref="BivariateAnalysisCollection"/>.
        /// </summary>
        private void OnBivariateAnalysisElementAdded(IElement element)
        {
            if (element is BivariateAnalysis ba && !BivariateAnalysisList.Contains(ba))
                BivariateAnalysisList.Add(ba);
        }

        /// <summary>
        /// Handles an element being removed from the <see cref="BivariateAnalysisCollection"/>.
        /// </summary>
        private void OnBivariateAnalysisElementRemoved(IElement element)
        {
            if (element is BivariateAnalysis ba) BivariateAnalysisList.Remove(ba);
        }

        /// <summary>
        /// Unsubscribes the named handlers from the tracked <see cref="BivariateAnalysisCollection"/>
        /// and clears the tracker field.
        /// </summary>
        private void UnsubscribeBivariateAnalysesCollection()
        {
            if (_subscribedBivariateAnalysesCollection != null)
            {
                _subscribedBivariateAnalysesCollection.ElementAdded -= OnBivariateAnalysisElementAdded;
                _subscribedBivariateAnalysesCollection.ElementRemoved -= OnBivariateAnalysisElementRemoved;
                _subscribedBivariateAnalysesCollection = null;
            }
        }

        /// <summary>
        /// Handles the <c>Loaded</c> event for <c>AnalysisComboBox</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void AnalysisComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            if (sender is ComboBox cmbo)
                cmbo.ItemsSource = BivariateAnalysisList;
        }

        /// <summary>
        /// Validates the Bivariate-Analysis selection and toggles the red border and tooltip
        /// on the surrounding content property control. Mirrors the InputData
        /// border-toggle logic.
        /// </summary>
        /// <param name="sender">The ComboBox raising the event.</param>
        /// <param name="e">The selection-changed event data.</param>
        private void AnalysisComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAnalysisValidationBorder();
        }

        /// <summary>
        /// Toggles the red border + tooltip on <see cref="AnalysisComboBox"/> based on
        /// whether <see cref="CoincidentFrequencyAnalysis.BivariateAnalysis"/> is non-null.
        /// Called from the SelectionChanged handler AND from <see cref="ElementCallback"/>
        /// after the new element binds.
        /// </summary>
        private void UpdateAnalysisValidationBorder()
        {
            if (Element == null) return;
            if (Element.BivariateAnalysis == null || Element.BivariateAnalysis.Name == null)
            {
                ((Border)AnalysisComboBox.InnerContent).BorderThickness = new Thickness(1);
                AnalysisComboBox.ToolTip = "Please select a valid bivariate analysis.";
            }
            else
            {
                ((Border)AnalysisComboBox.InnerContent).BorderThickness = new Thickness(0);
                AnalysisComboBox.ToolTip = null;
            }
        }

        #endregion

        #region BayesianOutputControl

        /// <summary>
        /// Handles the <see cref="BayesianOutputControl.ShowPropertyAttributes"/> event by
        /// asking the inline <c>PropertyAttributes</c> panel to display the description and
        /// validation rules for the requested property. Mirrors the canonical handler in
        /// <see cref="BivariateAnalysisPropertiesControl"/> — the delegate signature is
        /// <c>(string propertyName, object classObject)</c>, not the default WPF
        /// <c>RoutedEventHandler</c>.
        /// </summary>
        /// <param name="propertyName">Name of the property whose attributes should be shown.</param>
        /// <param name="classObject">The object that owns the property (typically the Bayesian analysis instance).</param>
        private void BayesianOutputControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        #endregion

        #region Estimate

        /// <summary>
        /// Handles the <c>Click</c> event for <c>EstimateButton</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private async void EstimateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            if (!Element.IsValid)
            {
                GenericControls.MessageBox.Show(
                    "Cannot perform the analysis because the inputs are invalid.",
                    "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;

            // Disable all open windows and main-window controls so the user cannot navigate
            // away or kick off a second analysis on the same control while this one runs.
            // Mirrors UnivariateAnalysisPropertiesControl.EstimateButton_Click.
            FrameworkUI.ShellPublicVariables.SimulationInProgress = true;
            ((FrameworkUI.MainWindow)Application.Current.MainWindow).DisableMenuStrip();
            ((FrameworkUI.MainWindow)Application.Current.MainWindow).DisableProjectExplorer();
            ((FrameworkUI.MainWindow)Application.Current.MainWindow).DisableMessageWindow();
            ((FrameworkUI.MainWindow)Application.Current.MainWindow).DisableOpenWindows();
            // Disable Properties
            PropertiesExpander.IsEnabled = false;
            XOrdinatesExpander.IsEnabled = false;
            YOrdinatesExpander.IsEnabled = false;
            Output_TabItem.IsEnabled = false;

            // Set up progress bar
            AnalysisProgressDisplayHelper.ShowInitial(ProgressBar, ProgressTextBlock);

            // Show cancel button
            EstimateButton.Visibility = Visibility.Hidden;
            CancelButton.Visibility = Visibility.Visible;

            var progressReporter = new SafeProgressReporter(nameof(CoincidentFrequencyAnalysis));
            progressReporter.ProgressReported += (SafeProgressReporter reporter, double progress, double progressDelta) =>
            {
                AnalysisProgressDisplayHelper.PostProgress(Dispatcher, ProgressBar, ProgressTextBlock, progress);
            };
            bool progressCleanupEnabled = false;
            progressReporter.TaskEnded += () =>
            {
                if (!progressCleanupEnabled) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Close out progress bar
                    ProgressBar.Value = 100;
                    ProgressTextBlock.Text = "Analysis Complete";
                    Mouse.OverrideCursor = null;
                    ProgressBar.Visibility = Visibility.Hidden;
                    ProgressTextBlock.Visibility = Visibility.Hidden;

                    // Restore Estimate button.
                    EstimateButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Hidden;

                    // Re-enable the main window.
                    FrameworkUI.ShellPublicVariables.SimulationInProgress = false;
                    ((FrameworkUI.MainWindow)Application.Current.MainWindow).EnableMenuStrip();
                    ((FrameworkUI.MainWindow)Application.Current.MainWindow).EnableProjectExplorer();
                    ((FrameworkUI.MainWindow)Application.Current.MainWindow).EnableMessageWindow();
                    ((FrameworkUI.MainWindow)Application.Current.MainWindow).EnableOpenWindows();
                    // Enable Properties
                    PropertiesExpander.IsEnabled = true;
                    XOrdinatesExpander.IsEnabled = true;
                    YOrdinatesExpander.IsEnabled = true;
                    Output_TabItem.IsEnabled = true;
                }));
            };

            try
            {
                await WaitCursorHelper.RunWithVisibleWaitCursorAsync(Dispatcher, async () =>
                {
                    await Element.RunAsync(progressReporter);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CoincidentFrequencyPropertiesControl.EstimateButton_Click: {ex}");
                GenericControls.MessageBox.Show(
                    "An unexpected error occurred. Please verify your analysis inputs and settings." +
                    Environment.NewLine + "If the issue persists, contact the Risk Management Center.",
                    "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressCleanupEnabled = true;
                progressReporter.IndicateTaskEnded();
            }
        }

        /// <summary>
        /// Cancels the currently running analysis. UI cleanup is finalized after the awaited
        /// analysis task returns in <see cref="EstimateButton_Click"/>.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Element?.CancelAnalysis();
        }

        #endregion

        #region Keyboard

        /// <summary>
        /// Routed key-down handler for the properties root. Cancels the running analysis when Escape is pressed.
        /// </summary>
        private void Properties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element?.CancelAnalysis();
        }

        #endregion
    }
}
