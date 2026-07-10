using GenericControls;
using Numerics.Utilities;
using FrameworkInterfaces;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
    /// User control for managing properties and settings of a time series analysis, including ARIMAX model configuration,
    /// covariate data, and Bayesian estimation options.
    /// </summary>
    public partial class TimeSeriesAnalysisPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSeriesAnalysisPropertiesControl"/> class.
        /// </summary>
        public TimeSeriesAnalysisPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
            this.Loaded += UserControl_Loaded;
            this.Unloaded += UserControl_Unloaded;
            CovariateDataGrid.RowType = typeof(CovariateData);
        }

        /// <summary>
        /// Stores the previous name of the element for validation purposes.
        /// </summary>
        private string _previousName;

        /// <summary>The <see cref="TimeSeriesCollection"/> currently subscribed (named handlers).</summary>
        private IElementCollection _subscribedTimeSeriesCollection;

        /// <summary>The <see cref="TimeSeriesAnalysis"/> currently subscribed by named handlers.</summary>
        private TimeSeriesAnalysis _subscribedElement;

        /// <summary>The <see cref="ARIMAX"/> currently subscribed by named handlers.</summary>
        private ARIMAX _subscribedARIMAX;

        /// <summary>
        /// Dependency property for the Element property. This represents the TimeSeriesAnalysis model being configured.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(TimeSeriesAnalysis), typeof(TimeSeriesAnalysisPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the TimeSeriesAnalysis element whose properties are being edited.
        /// </summary>
        public TimeSeriesAnalysis Element
        {
            get { return (TimeSeriesAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element dependency property changes.
        /// Handles attaching event handlers and initializing the control with the new element.
        /// </summary>
        /// <param name="d">The dependency object that owns the property.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as TimeSeriesAnalysisPropertiesControl == null) return;
            var thisControl = (TimeSeriesAnalysisPropertiesControl)d;
            thisControl.DetachElementHandlers();

            if (e.NewValue == null) return;
            var newElement = e.NewValue as TimeSeriesAnalysis;
            if (newElement == null) return;

            thisControl.AttachElementHandlers(newElement);
            thisControl._previousName = newElement.Name;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadTimeSeries();
            thisControl.UpdateTrainingSteps();
            thisControl.UpdateStepSplitDisplay();

        }

        /// <summary>
        /// Dependency property for the existing names property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(TimeSeriesAnalysisPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Gets or sets the array of existing element names for validation purposes.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Gets the collection of available time series elements from the project.
        /// </summary>
        public ObservableCollection<TimeSeriesElement> TimeSeriesElements { get; private set; } = new ObservableCollection<TimeSeriesElement>();

        /// <summary>
        /// Gets the collection of available transform types for the time series data.
        /// </summary>
        public ObservableCollection<TransformTypeItem> TransformTypeItems { get; private set; } = new ObservableCollection<TransformTypeItem>()
        {
            new TransformTypeItem("None", RMC.BestFit.Models.Transform.None),
            new TransformTypeItem("Logarithmic", RMC.BestFit.Models.Transform.Logarithmic),
            new TransformTypeItem("Box-Cox", RMC.BestFit.Models.Transform.BoxCox),
            new TransformTypeItem("Yeo-Johnson", RMC.BestFit.Models.Transform.YeoJohnson)
        };

        /// <summary>
        /// Gets the collection of available trend types for the ARIMAX model.
        /// </summary>
        public ObservableCollection<TrendTypeItem> TrendTypeItems { get; private set; } = new ObservableCollection<TrendTypeItem>()
        {
            new TrendTypeItem("None", ARIMAX.Trend.None),
            new TrendTypeItem("Linear", ARIMAX.Trend.Linear),
            new TrendTypeItem("Quadratic", ARIMAX.Trend.Quadratic),
            new TrendTypeItem("Cubic", ARIMAX.Trend.Cubic)
        };

        /// <summary>
        /// Gets the collection of available covariate-extension methods for the ARIMAX model.
        /// </summary>
        public ObservableCollection<CovariateExtensionItem> CovariateExtensionOptions { get; private set; } = new ObservableCollection<CovariateExtensionItem>()
        {
            new CovariateExtensionItem("None", ARIMAX.CovariateExtensionMethod.None),
            new CovariateExtensionItem("Block Bootstrap", ARIMAX.CovariateExtensionMethod.BlockBootstrap),
            new CovariateExtensionItem("KNN", ARIMAX.CovariateExtensionMethod.KNN)
        };


        #region Property Attributes

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Name control. Displays property attributes for the Name property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Description control. Displays property attributes for the Description property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CreationDate control. Displays property attributes for the CreationDate property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the LastModified control. Displays property attributes for the LastModified property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the TimeSeriesData combo box. Displays property attributes for the TimeSeriesData property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void TimeSeriesDataComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.TimeSeriesData), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Covariate data grid. Displays property attributes for the Covariates property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void CovariateDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Covariates), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the TransformType control. Displays property attributes for the TransformType property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void TransformType_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ARIMAX.TransformType), Element.ARIMAX);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the IncludeIntercept control. Displays property attributes for the IncludeIntercept property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void InlcudeIntercept_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ARIMAX.IncludeIntercept), Element.ARIMAX);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the TrendType control. Displays property attributes for the TrendType property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void TrendType_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ARIMAX.TrendType), Element.ARIMAX);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the IncludeSeasonality control. Displays property attributes for the IncludeSeasonality property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void InlcudeSeasonality_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ARIMAX.IncludeSeasonality), Element.ARIMAX);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the XOrderB control. Displays property attributes for the XOrderB property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void XOrderB_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ARIMAX.XOrderB), Element.ARIMAX);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the AROrderP control. Displays property attributes for the AROrderP property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void AROrderP_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ARIMAX.AROrderP), Element.ARIMAX);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the MAOrderQ control. Displays property attributes for the MAOrderQ property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void MAOrderQ_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ARIMAX.MAOrderQ), Element.ARIMAX);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the UseJeffreysRule control. Displays property attributes for the UseJeffreysRuleForScale property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UseJeffreysRule_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ARIMAX.UseJeffreysRuleForScale), Element.ARIMAX);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the TabItem control. Displays class-level attributes for the Element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetClassAttributes(Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the CovariateExtension field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void CovariateExtension_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CovariateExtension), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the ValidationSteps field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void ValidationSteps_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Validation Steps", "In-sample prediction window. Computed as Total Observations minus Training Steps.");
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the ParameterPriorsControl. Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object that owns the property.</param>
        private void ParameterPriorsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the BayesianOptionsControl. Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object that owns the property.</param>
        private void BayesianOptionsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the BayesianOutputControl. Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object that owns the property.</param>
        private void BayesianOutputControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }


        #endregion

        /// <summary>
        /// Handles the GotFocus event for the Name control. Stores the previous name and retrieves existing element names for validation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event for the Name control. Reverts to the previous name if the new name is invalid.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Name_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ElementName.NameTextBox.IsValid)
                return;
            if (Element != null)
                Element.Name = _previousName;
        }

        /// <summary>
        /// Handles the Loaded event by reattaching element-scoped handlers after WPF unload/reload cycles.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            AttachElementHandlers(Element);
            LoadTimeSeries();
            UpdateTrainingSteps();
            UpdateStepSplitDisplay();
        }

        /// <summary>
        /// Handles the Unloaded event of the user control. Unsubscribes from element event handlers
        /// to prevent memory leaks and stale event subscriptions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeTimeSeriesCollection();
            DetachElementHandlers();
        }

        /// <summary>
        /// Handles property changes on the Element object and updates dependent UI components.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the name of the property that changed.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, Element)) return;

            if (e.PropertyName == nameof(Element.ARIMAX) || e.PropertyName == nameof(Element.TimeSeriesData))
            {
                // ARIMAX object may have been swapped (e.g., during undo). Re-subscribe
                // the PropertyChanged handler so live step-split updates keep working.
                AttachARIMAXHandler(Element.ARIMAX);
                UpdateTrainingSteps();
                UpdateStepSplitDisplay();
            }
            // Model object replaced (e.g., during undo) — push to sub-controls
            if (e.PropertyName == nameof(Element.ARIMAX))
            {
                ParameterPriorsControl.Model = Element.ARIMAX;
            }
            // BayesianAnalysis replaced — push to sub-controls
            if (e.PropertyName == nameof(Element.BayesianAnalysis))
            {
                BayesianOptionsControl.Analysis = Element.BayesianAnalysis;
                BayesianOutputControl.Analysis = Element.BayesianAnalysis;
            }
        }

        /// <summary>
        /// Handles property changes on the inner ARIMAX model so the read-only Validation Steps
        /// and Total Observations displays stay in sync when Training Steps changes programmatically
        /// (e.g., via the Reset button or the 80% default rule).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the name of the property that changed.</param>
        private void ARIMAX_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, _subscribedARIMAX)) return;

            if (e.PropertyName == nameof(ARIMAX.TrainingTimeSteps)
                || e.PropertyName == nameof(ARIMAX.UseDefaultTrainingSteps)
                || e.PropertyName == nameof(ARIMAX.TimeSeries))
            {
                UpdateTrainingSteps();
                UpdateStepSplitDisplay();
            }
        }

        /// <summary>
        /// Attaches element-scoped handlers for the supplied analysis.
        /// </summary>
        /// <param name="element">The analysis element to observe.</param>
        private void AttachElementHandlers(TimeSeriesAnalysis element)
        {
            if (element == null) return;

            if (!ReferenceEquals(_subscribedElement, element))
            {
                if (_subscribedElement != null)
                    _subscribedElement.PropertyChanged -= Element_PropertyChanged;

                _subscribedElement = element;
                _subscribedElement.PropertyChanged -= Element_PropertyChanged;
                _subscribedElement.PropertyChanged += Element_PropertyChanged;
            }

            AttachARIMAXHandler(element.ARIMAX);
        }

        /// <summary>
        /// Attaches the model-level property handler to the supplied ARIMAX instance.
        /// </summary>
        /// <param name="arimax">The ARIMAX model to observe.</param>
        private void AttachARIMAXHandler(ARIMAX arimax)
        {
            if (ReferenceEquals(_subscribedARIMAX, arimax)) return;

            if (_subscribedARIMAX != null)
                _subscribedARIMAX.PropertyChanged -= ARIMAX_PropertyChanged;

            _subscribedARIMAX = arimax;
            if (_subscribedARIMAX != null)
            {
                _subscribedARIMAX.PropertyChanged -= ARIMAX_PropertyChanged;
                _subscribedARIMAX.PropertyChanged += ARIMAX_PropertyChanged;
            }
        }

        /// <summary>
        /// Detaches all element-scoped handlers held by this properties control.
        /// </summary>
        private void DetachElementHandlers()
        {
            if (_subscribedARIMAX != null)
            {
                _subscribedARIMAX.PropertyChanged -= ARIMAX_PropertyChanged;
                _subscribedARIMAX = null;
            }

            if (_subscribedElement != null)
            {
                _subscribedElement.PropertyChanged -= Element_PropertyChanged;
                _subscribedElement = null;
            }
        }

        /// <summary>
        /// Updates the minimum and maximum values for the training steps control based on the current model parameters and data length.
        /// </summary>
        private void UpdateTrainingSteps()
        {
            if (Element == null || Element.TimeSeriesData == null || Element.TimeSeriesData.TimeSeries == null) return;
            int maxTrainingSteps = Element.TimeSeriesData.TimeSeries.Count;
            int minTrainingSteps = maxTrainingSteps > 0
                ? Math.Min(Math.Max(10, Element.ARIMAX.Parameters.Count), maxTrainingSteps)
                : 0;
            var trainingStepsControl = (NumericUpDown)TrainingSteps.InnerContent;
            trainingStepsControl.Minimum = minTrainingSteps;
            trainingStepsControl.Maximum = maxTrainingSteps;
            trainingStepsControl.Value = Math.Min(Math.Max(Element.ARIMAX.TrainingTimeSteps, minTrainingSteps), maxTrainingSteps);
            trainingStepsControl.ToolTip = $"Must be between {minTrainingSteps} and {maxTrainingSteps} based on the number of parameters and data points.";
        }

        /// <summary>
        /// Refreshes the read-only Total Observations and Validation Steps TextBlocks. Validation
        /// Steps is computed as <c>Total - Training</c> so the user can see the three-way split
        /// (Training + Validation + Forecast) update live as they adjust Training Steps.
        /// </summary>
        private void UpdateStepSplitDisplay()
        {
            var validationText = ValidationSteps?.InnerContent as TextBlock;
            if (validationText == null) return;

            if (Element == null || Element.TimeSeriesData == null || Element.TimeSeriesData.TimeSeries == null)
            {
                validationText.Text = "-";
                return;
            }
            int total = Element.TimeSeriesData.TimeSeries.Count;
            int training = Element.ARIMAX != null ? Element.ARIMAX.TrainingTimeSteps : 0;
            int validation = Math.Max(0, total - training);
            validationText.Text = validation.ToString();
        }

        /// <summary>
        /// Handles the Reset button click. Restores the 80% training default rule and zeros out
        /// the forecast horizon so the user can start from the out-of-the-box configuration.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            Element.UseDefaultTrainingSteps = true;
            Element.ForecastSteps = 0;
        }

        /// <summary>
        /// Loads all available time series elements from the project and sets up event handlers for collection changes.
        /// </summary>
        private void LoadTimeSeries()
        {
            TimeSeriesElements.Clear();
            if (Element == null) return;
            foreach (IElementCollection collection in Element.ParentCollection.ParentProject.ElementCollections)
            {
                if (collection.GetType() == typeof(TimeSeriesCollection))
                {
                    UnsubscribeTimeSeriesCollection();
                    _subscribedTimeSeriesCollection = collection;
                    collection.ElementAdded += OnTimeSeriesElementAdded;
                    collection.ElementRemoved += OnTimeSeriesElementRemoved;
                    foreach (IElement element in collection)
                    {
                        if (element is TimeSeriesElement ts) TimeSeriesElements.Add(ts);
                    }
                    break;
                }
            }
        }

        /// <summary>Handles a new TimeSeriesElement being added to the project.</summary>
        private void OnTimeSeriesElementAdded(IElement x)
        {
            if (x is TimeSeriesElement ts && !TimeSeriesElements.Contains(ts)) TimeSeriesElements.Add(ts);
        }

        /// <summary>Handles a TimeSeriesElement being removed from the project.</summary>
        private void OnTimeSeriesElementRemoved(IElement x)
        {
            if (x is TimeSeriesElement ts) TimeSeriesElements.Remove(ts);
        }

        /// <summary>Unsubscribes named TimeSeries handlers and clears the tracker field.</summary>
        private void UnsubscribeTimeSeriesCollection()
        {
            if (_subscribedTimeSeriesCollection != null)
            {
                _subscribedTimeSeriesCollection.ElementAdded -= OnTimeSeriesElementAdded;
                _subscribedTimeSeriesCollection.ElementRemoved -= OnTimeSeriesElementRemoved;
                _subscribedTimeSeriesCollection = null;
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event for the TimeSeriesData combo box. Validates the selection and updates the UI accordingly.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void TimeSeriesDataComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // First-load NRE guard (CLAUDE.md "WPF DataContext Pattern").
            if (Element == null) return;
            if (Element.TimeSeriesData == null || Element.TimeSeriesData.Name == null)
            {
                ((Border)TimeSeriesDataComboBox.InnerContent).BorderThickness = new Thickness(1);
                TimeSeriesDataComboBox.ToolTip = "Please select a valid time series.";
            }
            else
            {
                ((Border)TimeSeriesDataComboBox.InnerContent).BorderThickness = new Thickness(0);
                TimeSeriesDataComboBox.ToolTip = null;
            }
        }

        /// <summary>
        /// Handles the Loaded event for the TimeSeriesData combo box. Sets up the items source with sorted time series elements.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void TimeSeriesDataComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cmbo = (ComboBox)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = TimeSeriesElements, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(IElement.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
        }


        /// <summary>
        /// Handles the Click event for the Estimate button. Initiates the Bayesian estimation process with progress tracking and UI management.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates the analysis configuration
        /// 2. Disables UI elements during estimation
        /// 3. Sets up progress reporting
        /// 4. Executes the Bayesian estimation asynchronously
        /// 5. Re-enables UI elements when complete
        /// </remarks>
        private async void EstimateButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if the analysis is valid
            if (Element.IsValid == false)
            {
                GenericControls.MessageBox.Show("Cannot perform the analysis because the inputs are invalid.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;

            // Disable all open windows
            FrameworkUI.ShellPublicVariables.SimulationInProgress = true;
            ((FrameworkUI.MainWindow)Application.Current.MainWindow).DisableMenuStrip();
            ((FrameworkUI.MainWindow)Application.Current.MainWindow).DisableProjectExplorer();
            ((FrameworkUI.MainWindow)Application.Current.MainWindow).DisableMessageWindow();
            ((FrameworkUI.MainWindow)Application.Current.MainWindow).DisableOpenWindows();
            // Disable Properties
            PropertiesExpander.IsEnabled = false;
            CovariateExpander.IsEnabled = false;
            ARMAExpander.IsEnabled = false;
            ParameterPriorsExpander.IsEnabled = false;
            Options_TabItem.IsEnabled = false;
            Output_TabItem.IsEnabled = false;

            // Set up progress bar
            AnalysisProgressDisplayHelper.ShowInitial(ProgressBar, ProgressTextBlock);

            // Show cancel button
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
                // Close out progress bar
                ProgressBar.Value = 100;
                ProgressTextBlock.Text = "Simulation Complete";
                Mouse.OverrideCursor = null;
                EstimateButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Hidden;
                ProgressTextBlock.Visibility = Visibility.Hidden;

                // Close cancel button
                EstimateButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Hidden;

                // Enable all windows
                FrameworkUI.ShellPublicVariables.SimulationInProgress = false;
                ((FrameworkUI.MainWindow)Application.Current.MainWindow).EnableMenuStrip();
                ((FrameworkUI.MainWindow)Application.Current.MainWindow).EnableProjectExplorer();
                ((FrameworkUI.MainWindow)Application.Current.MainWindow).EnableMessageWindow();
                ((FrameworkUI.MainWindow)Application.Current.MainWindow).EnableOpenWindows();
                // Enable Properties
                PropertiesExpander.IsEnabled = true;
                CovariateExpander.IsEnabled = true;
                ARMAExpander.IsEnabled = true;
                ParameterPriorsExpander.IsEnabled = true;
                Options_TabItem.IsEnabled = true;
                Output_TabItem.IsEnabled = true;
            };


            // Perform Bayesian estimation. Await so post-await exceptions land in the catch
            // (mirrors CoincidentFrequencyPropertiesControl.EstimateButton_Click); without
            // the await, a synchronous throw inside RunAsync sets the returned Task to faulted
            // and the exception is lost. CS4014.
            try
            {
                await WaitCursorHelper.RunWithVisibleWaitCursorAsync(Dispatcher, async () =>
                {
                    await Element.RunAsync(progressReporter);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TimeSeriesAnalysisPropertiesControl.EstimateButton_Click: {ex}");
                GenericControls.MessageBox.Show("An unexpected error occurred. Please verify your analysis inputs and settings." + Environment.NewLine + "If the issue persists, contact the Risk Management Center.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressCleanupEnabled = true;
                progressReporter.IndicateTaskEnded();
            }


        }

        /// <summary>
        /// Handles the Click event for the Cancel button. Cancels the ongoing analysis.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the KeyDown event for the Properties control. Cancels the analysis when Escape key is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Properties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the mouse button down event on the TrainingSteps field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void TrainingSteps_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.TrainingTimeSteps), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the ForecastSteps field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void ForecastingSteps_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ForecastSteps), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the DiffOrderD control. (Reserved for future implementation)
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void DiffOrderD_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }
    }
}
