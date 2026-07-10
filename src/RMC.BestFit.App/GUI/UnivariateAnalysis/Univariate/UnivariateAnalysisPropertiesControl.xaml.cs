using FrameworkInterfaces;
using GenericControls;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Numerics.Distributions;
using Numerics.Utilities;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using System.Linq;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and editing properties of a univariate analysis, including
    /// distribution selection, trend models, parameter priors, quantile priors, and Bayesian estimation options.
    /// </summary>
    public partial class UnivariateAnalysisPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnivariateAnalysisPropertiesControl"/> class.
        /// </summary>
        public UnivariateAnalysisPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
            this.Unloaded += UserControl_Unloaded;
        }

        /// <summary>
        /// Stores the previous name of the element for validation and rollback purposes.
        /// </summary>
        private string _previousName;

        /// <summary>
        /// Tracks the InputDataCollection currently subscribed to, so it can be unsubscribed when the control unloads or the element changes.
        /// </summary>
        private IElementCollection _subscribedInputDataCollection;

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(UnivariateAnalysis), typeof(UnivariateAnalysisPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the univariate analysis element whose properties are being displayed and edited.
        /// </summary>
        public UnivariateAnalysis Element
        {
            get { return (UnivariateAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element dependency property changes.
        /// Subscribes to property changed events and initializes the control with the new element's data.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as UnivariateAnalysisPropertiesControl == null) return;
            var thisControl = (UnivariateAnalysisPropertiesControl)d;

            // Unsubscribe from old element
            if (e.OldValue is UnivariateAnalysis oldElement)
            {
                oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as UnivariateAnalysis;
            if (newElement == null) return;

            // Initialize previous name for rollback safety
            thisControl._previousName = newElement.Name;

            newElement.PropertyChanged += thisControl.Element_PropertyChanged;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadInputData();
            thisControl.BindTrendModelDataGrid();

        }

        /// <summary>
        /// Dependency property for the existing names property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(UnivariateAnalysisPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Gets or sets an array of existing names for this element type, used for name uniqueness validation.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Cached reference to the Distribution combo box for programmatic binding updates during undo.
        /// Cannot use x:Name due to ContentPropertyControl XAML scoping; captured via Loaded event.
        /// </summary>
        private System.Windows.Controls.ComboBox _distributionComboBox;

        /// <summary>
        /// Gets the observable collection of available input data elements that can be selected for analysis.
        /// </summary>
        public ObservableCollection<InputData> InputDataList { get; private set; } = new ObservableCollection<InputData>();

        /// <summary>
        /// Stores the trend model row items for display in the data grid.
        /// </summary>
        private List<TrendModelRowItem> _trendModelRowItems = new List<TrendModelRowItem>();

        /// <summary>
        /// When true, suppresses <see cref="TrendModelComboBox_SelectionChanged"/> to prevent
        /// re-entrant SetTrendModel calls during <see cref="BindTrendModelDataGrid"/>.
        /// Clearing DataGrid.ItemsSource can fire ComboBox SelectionChanged events that would
        /// overwrite a freshly restored distribution during undo.
        /// </summary>
        private bool _suppressTrendModelSelection = false;

        /// <summary>
        /// Gets the observable collection of available univariate distribution types that can be selected.
        /// </summary>
        public ObservableCollection<UnivariateDistributionItem> DistributionList { get; private set; } = new ObservableCollection<UnivariateDistributionItem>()
        { new UnivariateDistributionItem("Exponential", UnivariateDistributionType.Exponential),
          new UnivariateDistributionItem("Gamma", UnivariateDistributionType.GammaDistribution),
          new UnivariateDistributionItem("Generalized Extreme Value", UnivariateDistributionType.GeneralizedExtremeValue),
          new UnivariateDistributionItem("Generalized Logistic", UnivariateDistributionType.GeneralizedLogistic),
          new UnivariateDistributionItem("Generalized Normal", UnivariateDistributionType.GeneralizedNormal),
          new UnivariateDistributionItem("Generalized Pareto", UnivariateDistributionType.GeneralizedPareto),
          new UnivariateDistributionItem("Gumbel (EVI)", UnivariateDistributionType.Gumbel),
          new UnivariateDistributionItem("Kappa-4", UnivariateDistributionType.KappaFour),
          new UnivariateDistributionItem("Ln-Normal", UnivariateDistributionType.LnNormal),
          new UnivariateDistributionItem("Logistic", UnivariateDistributionType.Logistic),
          new UnivariateDistributionItem("Log-Normal", UnivariateDistributionType.LogNormal),
          new UnivariateDistributionItem("Log-Pearson Type III", UnivariateDistributionType.LogPearsonTypeIII),
          new UnivariateDistributionItem("Normal", UnivariateDistributionType.Normal),
          new UnivariateDistributionItem("Pearson Type III", UnivariateDistributionType.PearsonTypeIII),
          new UnivariateDistributionItem("Weibull", UnivariateDistributionType.Weibull),
        };

        /// <summary>
        /// Gets the observable collection of available trend model types for nonstationary distributions.
        /// </summary>
        public ObservableCollection<TrendModelItem> TrendFunctionItems { get; private set; } = new ObservableCollection<TrendModelItem>()
        {
            new TrendModelItem("Constant", TrendModelType.Constant),
            new TrendModelItem("Cubic", TrendModelType.Cubic),
            new TrendModelItem("Exponential", TrendModelType.Exponential),
            new TrendModelItem("Linear", TrendModelType.Linear),
            new TrendModelItem("Logistic", TrendModelType.Logistic),
            new TrendModelItem("Power", TrendModelType.Power),
            new TrendModelItem("Quadratic", TrendModelType.Quadratic),
            new TrendModelItem("Sinusoidal", TrendModelType.Sinusoidal),
            new TrendModelItem("Step Function", TrendModelType.StepFunction)
        };


        #region Property Attributes

        /// <summary>
        /// Handles mouse button down event on the Name field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles mouse button down event on the Description field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles mouse button down event on the CreationDate field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles mouse button down event on the LastModified field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles mouse button down event on the InputData field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void InputData_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.InputData), Element);
        }

        /// <summary>
        /// Handles mouse button down event on the Distribution field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void Distribution_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UnivariateDistribution.Distribution), Element.UnivariateDistribution);
        }

        /// <summary>
        /// Handles mouse button down event on the UseJeffreysRule field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void UseJeffreysRule_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UnivariateDistribution.UseJeffreysRuleForScale), Element.UnivariateDistribution);
        }

        /// <summary>
        /// Handles mouse button down event on the IsNonstationary field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void IsNonstationary_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UnivariateDistribution.IsNonstationary), Element.UnivariateDistribution);
        }

        /// <summary>
        /// Handles mouse button down event on the ParameterTimeStep field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void ParameterTimeStep_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UnivariateDistribution.ParameterTimeIndex), Element.UnivariateDistribution);
        }

        /// <summary>
        /// Handles mouse button down event on the Alpha field to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void Alpha_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UnivariateDistribution.Alpha), Element.UnivariateDistribution);
        }

        /// <summary>
        /// Handles mouse button down event on the TrendModelDataGrid to display property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void TrendModelDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UnivariateDistribution.TrendModels), Element.UnivariateDistribution);
        }

        /// <summary>
        /// Handles mouse button down event on the ProbabilityOrdinatesControl to display default property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void ProbabilityOrdinatesControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Probability Ordinates", "The exceedance probabilities used for plotting the probability distribution.");
        }

        /// <summary>
        /// Handles mouse button down event on a TabItem to display class-level property attributes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetClassAttributes(Element);
        }

        /// <summary>
        /// Handles requests from the ParameterPriorsControl to show property attributes.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void ParameterPriorsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles requests from the QuantilePriorsControl to show property attributes.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void QuantilePriorsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles requests from the BayesianOptionsControl to show property attributes.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void BayesianOptionsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles requests from the BayesianOutputControl to show property attributes.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void BayesianOutputControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        #endregion

        /// <summary>
        /// Handles the GotFocus event for the Name field. Stores the current name and retrieves existing names for validation.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event for the Name field. Restores the previous name if validation fails.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void Name_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ElementName.NameTextBox.IsValid)
                return;
            if (Element != null)
                Element.Name = _previousName;
        }

        /// <summary>
        /// Handles property changed events from the Element. Responds to changes in trend models and data frame properties.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Element.UnivariateDistribution.TrendModels) || e.PropertyName == nameof(Element.UnivariateDistribution.Distribution))
            {
                BindTrendModelDataGrid();
            }
            if (e.PropertyName == nameof(Element.UnivariateDistribution.DataFrame) || e.PropertyName == nameof(Element.UnivariateDistribution.IsNonstationary))
            {
                UpdateTimeIndexConstraints();
            }
            // Distribution object replaced (e.g., during undo) — explicitly push the new object
            // to sub-controls and combo box. Do NOT rely on WPF multi-level binding path refresh.
            if (e.PropertyName == nameof(Element.UnivariateDistribution))
            {
                ParameterPriorsControl.Model = Element.UnivariateDistribution;
                QuantilePriorsControl.Model = Element.UnivariateDistribution;
                if (_distributionComboBox != null)
                    _distributionComboBox.SelectedValue = Element.UnivariateDistribution?.DistributionType;
                BindTrendModelDataGrid();
                UpdateTimeIndexConstraints();
            }
            // BayesianAnalysis object replaced (during distribution undo, RestoreDistributionFromSnapshot
            // creates a new inner analysis) — explicitly push to sub-controls.
            if (e.PropertyName == nameof(Element.BayesianAnalysis))
            {
                BayesianOptionsControl.Analysis = Element.BayesianAnalysis;
                BayesianOutputControl.Analysis = Element.BayesianAnalysis;
            }
        }

        /// <summary>
        /// Loads the available input data elements from the project and subscribes to collection change events.
        /// Unsubscribes from any previously subscribed collection before subscribing to the new one.
        /// </summary>
        private void LoadInputData()
        {
            UnsubscribeInputDataCollection();
            InputDataList.Clear();
            foreach (IElementCollection collection in Element.ParentCollection.ParentProject.ElementCollections)
            {
                if (collection.GetType() == typeof(InputDataCollection))
                {
                    _subscribedInputDataCollection = collection;
                    collection.ElementAdded += OnInputDataElementAdded;
                    collection.ElementRemoved += OnInputDataElementRemoved;
                    foreach (IElement element in collection)
                        InputDataList.Add((InputData)element);
                    break;
                }
            }
        }

        /// <summary>
        /// Handles the addition of an input data element to the subscribed collection.
        /// </summary>
        /// <param name="element">The element that was added.</param>
        private void OnInputDataElementAdded(IElement element)
        {
            InputDataList.Add((InputData)element);
        }

        /// <summary>
        /// Handles the removal of an input data element from the subscribed collection.
        /// </summary>
        /// <param name="element">The element that was removed.</param>
        private void OnInputDataElementRemoved(IElement element)
        {
            InputDataList.Remove((InputData)element);
        }

        /// <summary>
        /// Unsubscribes from the currently tracked InputDataCollection and clears the reference.
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
        /// Handles the Unloaded event of the user control. Unsubscribes from InputDataCollection events to prevent memory leaks.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeInputDataCollection();
        }

        /// <summary>
        /// Handles selection changes in the InputData combo box. Validates the selection and updates time index constraints.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void InputDataComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;
            if (Element.InputData == null || Element.InputData.Name == null)
            {
                ((Border)InputDataComboBox.InnerContent).BorderThickness = new Thickness(1);
                InputDataComboBox.ToolTip = "Please select a valid input data.";
            }
            else
            {
                ((Border)InputDataComboBox.InnerContent).BorderThickness = new Thickness(0);
                InputDataComboBox.ToolTip = null;

                UpdateTimeIndexConstraints();
            }
        }

        /// <summary>
        /// Handles selection changes in the Distribution combo box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void DistributionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        /// <summary>
        /// Captures the Distribution combo box reference when it is loaded.
        /// Required because the ComboBox is inside a ContentPropertyControl and cannot have x:Name in XAML.
        /// </summary>
        /// <param name="sender">The combo box.</param>
        /// <param name="e">Routed event arguments.</param>
        private void DistributionComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            _distributionComboBox = sender as System.Windows.Controls.ComboBox;
        }

        /// <summary>
        /// Handles the Loaded event for the combo box. Sets up a sorted view of the input data list.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            ComboBox cmbo = (ComboBox)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = InputDataList, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(IElement.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
        }

        /// <summary>
        /// Handles the Estimate button click event. Validates inputs, disables the UI, and runs the Bayesian analysis asynchronously.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
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
            ParameterPriorsExpander.IsEnabled = false;
            QuantilePriorsExpander.IsEnabled = false;
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
                ParameterPriorsExpander.IsEnabled = true;
                QuantilePriorsExpander.IsEnabled = true;
                Options_TabItem.IsEnabled = true;
                Output_TabItem.IsEnabled = true;
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
                System.Diagnostics.Debug.WriteLine($"UnivariateAnalysisPropertiesControl.EstimateButton_Click: {ex}");
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
            Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles key down events for the control. Cancels the analysis if Escape is pressed.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The key event arguments.</param>
        private void UnivariateAnalysisProperties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the Checked event for the IsNonstationary checkbox. Updates time index constraints when nonstationary analysis is enabled.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void IsNonstationary_Checked(object sender, RoutedEventArgs e)
        {
            UpdateTimeIndexConstraints();
        }

        /// <summary>
        /// Handles the Unchecked event for the IsNonstationary checkbox.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void IsNonstationary_Unchecked(object sender, RoutedEventArgs e)
        {
            //TrendModelDataGrid.ItemsSource = null;
            //TrendModelDataGrid.UpdateLayout();
        }

        /// <summary>
        /// Updates the minimum and maximum constraints for the parameter time index based on the input data time series range.
        /// </summary>
        private void UpdateTimeIndexConstraints()
        {
            if (Element != null && Element.UnivariateDistribution.IsNonstationary == true &&
                Element.InputData != null &&
                Element.InputData.DataFrame != null &&
                Element.InputData.DataFrame.FullTimeSeries.Count > 0)
            {
                ParameterTimeStep.MinValue = Element.InputData.DataFrame.FullTimeSeries.First().Index;
                ParameterTimeStep.MaxValue = Element.InputData.DataFrame.FullTimeSeries.Last().Index + 100;
            }
        }

        /// <summary>
        /// Binds the trend model data grid with the current distribution's parameter trend models.
        /// Sets up column headers with tooltips and populates the grid with trend model row items.
        /// </summary>
        private void BindTrendModelDataGrid()
        {
            _suppressTrendModelSelection = true;
            try
            {
                TrendModelDataGrid.ItemsSource = null;
                if (Element == null || Element.UnivariateDistribution == null) return;

                _trendModelRowItems = new List<TrendModelRowItem>();
                for (int i = 0; i < Element.UnivariateDistribution.TrendModels.Count; i++)
                {
                    _trendModelRowItems.Add(new TrendModelRowItem(Element.UnivariateDistribution.TrendModels[i].OwnerName, Element.UnivariateDistribution.TrendModels[i].Type));
                }

                // Parameter Column Header
                var style = new Style(typeof(DataGridColumnHeader), ParameterNameColumn.HeaderStyle);
                style.Setters.Add(new Setter(ToolTipProperty, new TextBlock() { Text = "The model parameter names.", FontWeight = System.Windows.FontWeights.Normal, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap }));
                ParameterNameColumn.HeaderStyle = style;

                // Distribution Column Header
                style = new Style(typeof(DataGridColumnHeader), TrendModelColumn.HeaderStyle);
                style.Setters.Add(new Setter(ToolTipProperty, new TextBlock() { Text = "Set the parameter trend model type.", FontWeight = System.Windows.FontWeights.Normal, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap }));
                TrendModelColumn.HeaderStyle = style;

                TrendModelDataGrid.ItemsSource = _trendModelRowItems;
                TrendModelDataGrid.Items.Refresh();
            }
            finally
            {
                _suppressTrendModelSelection = false;
            }
        }

        /// <summary>
        /// Handles selection changes in the trend model combo box. Updates the distribution's trend models when changes are detected.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void TrendModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressTrendModelSelection) return;
            if (Element == null || Element.UnivariateDistribution == null || _trendModelRowItems == null) return;
            for (int i = 0; i < _trendModelRowItems.Count; i++)
            {
                if (_trendModelRowItems[i].Value != Element.UnivariateDistribution.TrendModels[i].Type)
                {
                    Element.UnivariateDistribution.SetTrendModel(i, _trendModelRowItems[i].Value);
                }
            }
        }

    }
}
