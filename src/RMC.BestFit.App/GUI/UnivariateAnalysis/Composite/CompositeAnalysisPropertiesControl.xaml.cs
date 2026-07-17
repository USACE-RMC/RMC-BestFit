using Numerics.Data.Statistics;
using Numerics.Utilities;
using FrameworkInterfaces;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using ModelAnalyses = RMC.BestFit.Analyses;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and editing the properties of a composite distribution analysis,
    /// including input data selection, composite type configuration, distribution weighting, and Bayesian analysis settings.
    /// </summary>
    public partial class CompositeAnalysisPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeAnalysisPropertiesControl"/> class.
        /// Sets up the control and configures the composite function data grid.
        /// </summary>
        public CompositeAnalysisPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
            CompositeFunctionDataGrid.RowType = typeof(WeightedUnivariateAnalysis);
            this.Unloaded += UserControl_Unloaded;
        }

        /// <summary>
        /// Stores the previous name of the element for validation and rollback purposes.
        /// </summary>
        private string _previousName;

        /// <summary>The <see cref="InputDataCollection"/> currently subscribed (named handlers).</summary>
        private IElementCollection _subscribedInputDataCollection;

        /// <summary>The inner ComboBox used for optional input-data overlay selection.</summary>
        private ComboBox _inputDataInnerComboBox;

        /// <summary>The <see cref="UnivariateAnalysisCollection"/> currently subscribed (named handlers).</summary>
        private IElementCollection _subscribedUnivariateCollection;

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// Enables data binding for the composite analysis element.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(CompositeAnalysis), typeof(CompositeAnalysisPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the composite analysis element whose properties are displayed in this control.
        /// </summary>
        public CompositeAnalysis Element
        {
            get { return (CompositeAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Element"/> dependency property changes.
        /// Initializes event handlers and loads related data collections.
        /// </summary>
        /// <param name="d">The dependency object on which the property changed.</param>
        /// <param name="e">Event arguments containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as CompositeAnalysisPropertiesControl == null) return;
            var thisControl = (CompositeAnalysisPropertiesControl)d;

            // Unsubscribe from old element
            if (e.OldValue is CompositeAnalysis oldElement)
                oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;

            thisControl.UnsubscribeInputDataCollection();
            thisControl.ClearInputDataSelectionItems();

            if (e.NewValue == null) return;
            var newElement = e.NewValue as CompositeAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Element_PropertyChanged;
            thisControl._previousName = newElement.Name;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadInputData();
            thisControl.LoadUnivariateAnalyses();
            thisControl.UpdateInputDataValidationBorder();
            thisControl.SelectCurrentInputDataItem(thisControl._inputDataInnerComboBox);
        }

        /// <summary>
        /// Dependency property for the existing names property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(CompositeAnalysisPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Array of existing names for this element type.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Gets the observable collection of available optional input-data overlay choices.
        /// </summary>
        public ObservableCollection<InputDataSelectionItem> InputDataList { get; private set; } = new ObservableCollection<InputDataSelectionItem>();

        /// <summary>
        /// Gets the observable collection of available univariate-flavored analysis elements
        /// in the project that are eligible to serve as composite children.
        /// </summary>
        /// <remarks>
        /// Holds any sibling that implements <see cref="IUnivariate"/> EXCEPT another
        /// <see cref="CompositeAnalysis"/> � composite-of-composite is rejected at the
        /// model layer (would risk circular references) so it is filtered out here so
        /// the user cannot pick it from the data grid in the first place. Currently
        /// this includes <see cref="UnivariateAnalysis"/> and <see cref="B17CAnalysis"/>;
        /// other <see cref="IUnivariate"/> siblings (Mixture, PointProcess) are also
        /// surfaced if they ever land in the same parent collection.
        /// </remarks>
        public ObservableCollection<IUnivariate> UnivariateAnalysisList { get; private set; } = new ObservableCollection<IUnivariate>();

        /// <summary>
        /// Gets the observable collection of composite distribution type options.
        /// </summary>
        public ObservableCollection<CompositeTypeItem> CompositeTypeList { get; private set; } = new ObservableCollection<CompositeTypeItem>()
        {
            new CompositeTypeItem("Competing Risks", ModelAnalyses.CompositeType.CompetingRisks, "The distributions are competing to be the maximum (or minimum) event."),
            new CompositeTypeItem("Mixture Distribution", ModelAnalyses.CompositeType.Mixture, "The distributions are treated as mixture distribution with user-defined weights."),
            new CompositeTypeItem("Model Average", ModelAnalyses.CompositeType.ModelAverage, "The distributions are combined as a model average.")
        };

        /// <summary>
        /// Gets the observable collection of model averaging method options.
        /// </summary>
        public ObservableCollection<AverageMethodItem> AverageMethodList { get; private set; } = new ObservableCollection<AverageMethodItem>()
        {
            new AverageMethodItem("AIC", ModelAnalyses.AverageMethod.AIC, "The distributions are weighted based on the Akaike Information Criteria (AIC)."),
            new AverageMethodItem("BIC", ModelAnalyses.AverageMethod.BIC, "The distributions are weighted based on the Bayesian Information Criteria (BIC)."),
            new AverageMethodItem("DIC", ModelAnalyses.AverageMethod.DIC, "The distributions are weighted based on the Deviance Information Criteria (DIC)."),
            new AverageMethodItem("WAIC", ModelAnalyses.AverageMethod.WAIC, "The distributions are weighted based on the Watanabe-Akaike Information Criteria (WAIC)."),
            new AverageMethodItem("LOO-CV", ModelAnalyses.AverageMethod.LOOIC, "The distributions are weighted based on Leave-One-Out Cross-Validation via Pareto Smoothed Importance Sampling (PSIS-LOO)."),
            new AverageMethodItem("RMSE", ModelAnalyses.AverageMethod.RMSE, "The distributions are weighted based on the Root Mean Square Error (RMSE)."),
            new AverageMethodItem("Equal", ModelAnalyses.AverageMethod.Equal, "Each distribution is given equal weight.")
        };

        /// <summary>
        /// Gets the observable collection of dependency type options for competing risks.
        /// </summary>
        public ObservableCollection<DependencyItem> DependencyList { get; private set; } = new ObservableCollection<DependencyItem>()
        {
            new DependencyItem("Independent", Probability.DependencyType.Independent, "The distributions are treated as independent."),
            new DependencyItem("Perfectly Negative", Probability.DependencyType.PerfectlyNegative, "The distributions are treated as perfectly negatively dependent."),
            new DependencyItem("Perfectly Positive", Probability.DependencyType.PerfectlyPositive, "The distributions are treated as perfectly positively dependent.")
        };

        /// <summary>
        /// Gets the observable collection of credible interval width options for Bayesian analysis.
        /// </summary>
        public ObservableCollection<CredibleIntervalItem> CredibleIntervalItems { get; private set; } = new ObservableCollection<CredibleIntervalItem>()
        {
            new CredibleIntervalItem("90%", 0.9),
            new CredibleIntervalItem("95%", 0.95),
            new CredibleIntervalItem("98%", 0.98),
            new CredibleIntervalItem("99%", 0.99)
        };

        /// <summary>
        /// Gets the observable collection of point estimator options for Bayesian analysis.
        /// </summary>
        public ObservableCollection<PointEstimatorItem> PointEstimatorItems { get; private set; } = new ObservableCollection<PointEstimatorItem>()
        {
            new PointEstimatorItem("Posterior Mean", BayesianAnalysis.PointEstimateType.PosteriorMean, "Calculates the average of all posterior samples, providing a balanced summary of the parameter estimates."),
            new PointEstimatorItem("Posterior Mode", BayesianAnalysis.PointEstimateType.PosteriorMode, "Selects the most likely parameter values for the posterior distribution (the Maximum A Posteriori estimate)."),
        };

        #region Property Attributes

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Name control. Displays property attributes for the element name.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Description control. Displays property attributes for the element description.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CreationDate control. Displays property attributes for the creation date.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the LastModified control. Displays property attributes for the last modified date.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the InputDataComboBox control. Displays property attributes for the input data.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void InputDataComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.InputData), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CompositeType control. Displays property attributes for the composite distribution type.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void CompositeType_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CompositeDistributionType), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the AverageMethod control. Displays property attributes for the model averaging method.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void AverageMethod_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ModelAverageMethod), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Dependency control. Displays property attributes for the dependency type.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void Dependency_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Dependency), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the IsMaximum control. Displays property attributes for the IsMaximum property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void IsMaximum_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.IsMaximum), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CompositeFunctionDataGrid control. Displays property attributes for the analyses collection.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void CompositeFunctionDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Analyses), Element);
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
        /// Handles the PreviewMouseLeftButtonDown event for the ProbabilityOrdinatesControl. Displays property attributes for the probability ordinates.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void ProbabilityOrdinatesControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Probability Ordinates", "The exceedance probabilities used for plotting the probability distribution.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the TabItem. Displays class-level attributes for the element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetClassAttributes(Element);
        }

        #endregion

        /// <summary>
        /// Handles the GotFocus event for the Name control. Saves the previous name and loads existing names for validation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event for the Name control. Reverts to the previous name if validation fails.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Name_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ElementName.NameTextBox.IsValid)
                return;
            if (Element != null)
                Element.Name = _previousName;
        }

        /// <summary>
        /// Handles the Unloaded event for the user control. Unsubscribes element event handlers
        /// to prevent memory leaks and stale event callbacks.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeInputDataCollection();
            UnsubscribeUnivariateAnalysisCollection();
            ClearInputDataSelectionItems();
        }

        /// <summary>
        /// Handles property change events from the Element. Updates the data grid styling when the analyses collection changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Marshal to UI thread if called from a background thread (e.g. during MCMC).
            // CompositeAnalysis re-raises "Analyses" for every child-analysis property change,
            // so this handler is invoked on an MCMC pool thread while a batch run is active.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Element_PropertyChanged(sender, e)));
                return;
            }

            // Analyses collection replaced (e.g., during undo) � refresh data grid
            if (e.PropertyName == nameof(Element.Analyses))
            {
                SetDataGridStyle();
            }
            if (e.PropertyName == nameof(Element.InputData))
            {
                UpdateInputDataValidationBorder();
                SelectCurrentInputDataItem(_inputDataInnerComboBox);
            }
            // BayesianAnalysis replaced � push to sub-controls
            if (e.PropertyName == nameof(Element.BayesianAnalysis))
            {
                // Composite doesn't have direct BA sub-controls but notify for consistency
            }
        }

        /// <summary>
        /// Loads the available input data elements from the project and subscribes to collection change events.
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
                    collection.ElementAdded += OnInputDataAdded;
                    collection.ElementRemoved += OnInputDataRemoved;
                    foreach (IElement element in collection)
                        if (element is InputData id) AddInputDataSelectionItem(id);
                    break;
                }
            }
        }

        /// <summary>Handles a new InputData element being added to the project.</summary>
        private void OnInputDataAdded(IElement x)
        {
            if (x is InputData id) AddInputDataSelectionItem(id);
        }

        /// <summary>Handles an InputData element being removed from the project.</summary>
        private void OnInputDataRemoved(IElement x)
        {
            if (x is InputData id) RemoveInputDataSelectionItem(id);
        }

        /// <summary>Unsubscribes named InputData handlers and clears the tracker field.</summary>
        private void UnsubscribeInputDataCollection()
        {
            if (_subscribedInputDataCollection != null)
            {
                _subscribedInputDataCollection.ElementAdded -= OnInputDataAdded;
                _subscribedInputDataCollection.ElementRemoved -= OnInputDataRemoved;
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
        /// Handles the SelectionChanged event for the InputDataComboBox. Validates the selection and updates visual feedback.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs"/> instance containing the event data.</param>
        private void InputDataComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // First-load NRE guard (the WPF DataContext pattern).
            if (Element == null) return;
            UpdateInputDataValidationBorder();
        }

        /// <summary>
        /// Toggles the input-data selection border based on real invalid selections.
        /// </summary>
        /// <remarks>
        /// A <c>null</c> input data value is the explicit no-overlay selection and is valid.
        /// Only a real input-data reference with a missing name should show the invalid state.
        /// </remarks>
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
        /// Handles the Loaded event for the InputDataComboBox. Sets up the sorted data source for the combo box.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void InputDataComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cmbo = (ComboBox)sender;
            _inputDataInnerComboBox = cmbo;
            CollectionViewSource csv = new CollectionViewSource() { Source = InputDataList, IsLiveSortingRequested = true };
            csv.LiveSortingProperties.Add(nameof(InputDataSelectionItem.SortKey));
            csv.SortDescriptions.Add(new SortDescription(nameof(InputDataSelectionItem.SortKey), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
            SelectCurrentInputDataItem(cmbo);
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

        /// <summary>
        /// Handles the SelectionChanged event for the CompositeType control. Adjusts the UI based on the selected composite distribution type.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs"/> instance containing the event data.</param>
        private void CompositeType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;
            if (Element.CompositeDistributionType == ModelAnalyses.CompositeType.CompetingRisks)
            {
                AverageMethod.Visibility = Visibility.Collapsed;
                Dependency.Visibility = Visibility.Visible;
                IsMaximum.Visibility = Visibility.Visible;
                CompositeFunctionDataGrid.Columns[1].Visibility = Visibility.Collapsed;
                CompositeFunctionDataGrid.Columns[1].IsReadOnly = true;
                ((DataGridTextColumn)CompositeFunctionDataGrid.Columns[1]).ClearValue(DataGridTextColumn.ForegroundProperty);
                ((DataGridTextColumn)CompositeFunctionDataGrid.Columns[1]).FontStyle = FontStyles.Normal;
            }
            else if (Element.CompositeDistributionType == ModelAnalyses.CompositeType.Mixture)
            {
                AverageMethod.Visibility = Visibility.Collapsed;
                Dependency.Visibility = Visibility.Collapsed;
                IsMaximum.Visibility = Visibility.Collapsed;
                CompositeFunctionDataGrid.Columns[1].Visibility = Visibility.Visible;
                CompositeFunctionDataGrid.Columns[1].IsReadOnly = false;
                ((DataGridTextColumn)CompositeFunctionDataGrid.Columns[1]).ClearValue(DataGridTextColumn.ForegroundProperty);
                ((DataGridTextColumn)CompositeFunctionDataGrid.Columns[1]).FontStyle = FontStyles.Normal;
            }
            else if (Element.CompositeDistributionType == ModelAnalyses.CompositeType.ModelAverage)
            {
                AverageMethod.Visibility = Visibility.Visible;
                Dependency.Visibility = Visibility.Collapsed;
                IsMaximum.Visibility = Visibility.Collapsed;
                CompositeFunctionDataGrid.Columns[1].Visibility = Visibility.Visible;
                CompositeFunctionDataGrid.Columns[1].IsReadOnly = true;
                ((DataGridTextColumn)CompositeFunctionDataGrid.Columns[1]).Foreground = Brushes.Gray;
                ((DataGridTextColumn)CompositeFunctionDataGrid.Columns[1]).FontStyle = FontStyles.Italic;
            }
        }

        /// <summary>
        /// Loads the available univariate-flavored analysis elements from the project and
        /// subscribes to collection change events via NAMED handlers (Convention 8).
        /// Accepts any <see cref="IUnivariate"/> sibling EXCEPT <see cref="CompositeAnalysis"/>
        /// (composite-of-composite is rejected at the model layer; filtered here so it never
        /// reaches the picker).
        /// </summary>
        private void LoadUnivariateAnalyses()
        {
            UnivariateAnalysisList.Clear();
            if (Element == null) return;
            foreach (IElementCollection collection in Element.ParentCollection.ParentProject.ElementCollections)
            {
                if (collection.GetType() == typeof(UnivariateAnalysisCollection))
                {
                    UnsubscribeUnivariateAnalysisCollection();
                    _subscribedUnivariateCollection = collection;
                    collection.ElementAdded += OnUnivariateAnalysisAdded;
                    collection.ElementRemoved += OnUnivariateAnalysisRemoved;
                    foreach (IElement element in collection)
                    {
                        if (IsEligibleCompositeChild(element, out IUnivariate iu))
                            UnivariateAnalysisList.Add(iu);
                    }
                    break;
                }
            }
        }

        /// <summary>Handles an analysis being added to the project.</summary>
        private void OnUnivariateAnalysisAdded(IElement x)
        {
            if (IsEligibleCompositeChild(x, out IUnivariate iu) && !UnivariateAnalysisList.Contains(iu))
                UnivariateAnalysisList.Add(iu);
        }

        /// <summary>Handles an analysis being removed from the project.</summary>
        private void OnUnivariateAnalysisRemoved(IElement x)
        {
            if (x is IUnivariate iu) UnivariateAnalysisList.Remove(iu);
        }

        /// <summary>
        /// Returns whether the given element can serve as a child of a CompositeAnalysis.
        /// Eligible: any IUnivariate sibling except another CompositeAnalysis.
        /// </summary>
        private static bool IsEligibleCompositeChild(IElement element, out IUnivariate iu)
        {
            if (element is IUnivariate u && element is not CompositeAnalysis)
            {
                iu = u;
                return true;
            }
            iu = null!;
            return false;
        }

        /// <summary>Unsubscribes named UnivariateAnalysis handlers and clears the tracker field.</summary>
        private void UnsubscribeUnivariateAnalysisCollection()
        {
            if (_subscribedUnivariateCollection != null)
            {
                _subscribedUnivariateCollection.ElementAdded -= OnUnivariateAnalysisAdded;
                _subscribedUnivariateCollection.ElementRemoved -= OnUnivariateAnalysisRemoved;
                _subscribedUnivariateCollection = null;
            }
        }

        /// <summary>
        /// Handles the Loaded event for combo boxes in the data grid. Sets up the sorted data source for univariate analyses.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void DataGridComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cmbo = (ComboBox)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = UnivariateAnalysisList, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(IUnivariate.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
        }

        /// <summary>
        /// Sets the data grid cell style for the weight column, including validation triggers for invalid weights.
        /// </summary>
        private void SetDataGridStyle()
        {
            var weightStyle = new Style(typeof(DataGridCell), (Style)FindResource("Right_CellStyle"));
            DataTrigger dt1 = new DataTrigger();
            dt1.Binding = new Binding("IsWeightValid");
            dt1.Value = false;
            dt1.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF7B6AF"))));
            dt1.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(Colors.Red)));
            dt1.Setters.Add(new Setter(DataGridCell.ToolTipProperty, new ToolTip() { Content = "The weight must be between 0 and 1" }));
            weightStyle.Triggers.Add(dt1);
            WeightColumn.CellStyle = weightStyle;
        }

        /// <summary>
        /// Handles the Click event for the EstimateButton. Validates the analysis configuration and runs the composite distribution estimation asynchronously.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private async void EstimateButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if the fitting analysis is valid
            if (Element.IsValid == false)
            {
                GenericControls.MessageBox.Show("Cannot perform the analysis because the inputs are invalid.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
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
            CompositeOptionsExpander.IsEnabled = false;
            Options_TabItem.IsEnabled = false;

            // Set up progress bar
            AnalysisProgressDisplayHelper.ShowInitial(ProgressBar, ProgressTextBlock);

            // Toggle Estimate ? Cancel so a second click cancels rather than starting a
            // concurrent run (the previous behavior crashed the project: re-clicking
            // Estimate started a second RunAsync against the same composite while the
            // first was still active).
            EstimateButton.Visibility = Visibility.Hidden;
            CancelButton.Visibility = Visibility.Visible;

            // Set up progress reporter
            var progressReporter = new SafeProgressReporter(nameof(UnivariateAnalysis));
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
                    EstimateButton.IsEnabled = true;
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
                    CompositeOptionsExpander.IsEnabled = true;
                    Options_TabItem.IsEnabled = true;
                }));
            };


            // Perform Bayesian estimation
            try
            {
                await WaitCursorHelper.RunWithVisibleWaitCursorAsync(Dispatcher, async () =>
                {
                    await Element.RunAsync(progressReporter);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CompositeAnalysisPropertiesControl.EstimateButton_Click: {ex}");
                GenericControls.MessageBox.Show("An unexpected error occurred. Please verify your analysis inputs and settings." + Environment.NewLine + "If the issue persists, contact the Risk Management Center.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressCleanupEnabled = true;
                progressReporter.IndicateTaskEnded();
            }

        }

        /// <summary>
        /// Handles the Cancel button click event. Cancels the running analysis.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Element?.CancelAnalysis();
        }

        /// <summary>
        /// Handles key-down events for the control. Cancels the running analysis if Escape is pressed.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The key event arguments.</param>
        private void CompositeAnalysisProperties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element?.CancelAnalysis();
        }

    }
}
