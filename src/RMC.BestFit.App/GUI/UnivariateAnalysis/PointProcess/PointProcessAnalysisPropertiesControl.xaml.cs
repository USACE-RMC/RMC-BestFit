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
using Numerics.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for managing and editing properties of a Point Process Analysis element.
    /// Provides an interface for configuring analysis parameters, input data, priors, Bayesian options,
    /// and executing the analysis estimation process.
    /// </summary>
    public partial class PointProcessAnalysisPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PointProcessAnalysisPropertiesControl"/> class.
        /// </summary>
        public PointProcessAnalysisPropertiesControl()
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
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(PointProcessAnalysis), typeof(PointProcessAnalysisPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Point Process Analysis element whose properties are being edited.
        /// </summary>
        public PointProcessAnalysis Element
        {
            get { return (PointProcessAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Element"/> property changes.
        /// Handles event subscription and initializes property attributes and input data.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as PointProcessAnalysisPropertiesControl == null) return;
            var thisControl = (PointProcessAnalysisPropertiesControl)d;

            // Unsubscribe from old element — both PropertyChanged (element-scoped) and
            // InputDataCollection subscriptions so the new element's LoadInputData starts clean.
            if (e.OldValue is PointProcessAnalysis oldElement)
            {
                oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                thisControl.UnsubscribeInputDataCollection();
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as PointProcessAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Element_PropertyChanged;
            thisControl._previousName = newElement.Name;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadInputData();
            thisControl.UpdateComboBoxes();

        }

        /// <summary>
        /// Dependency property for the existing names property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(PointProcessAnalysisPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Gets or sets the array of existing names for this element type.
        /// Used for validating that the element name is unique.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Gets the observable collection of available input data elements that can be selected for analysis.
        /// </summary>
        public ObservableCollection<InputData> InputDataList { get; private set; } = new ObservableCollection<InputData>();

        /// <summary>
        /// Backing field for the <see cref="MonthOptions"/> property. Allocated once to avoid creating a new list on every XAML binding call.
        /// </summary>
        private static readonly List<MonthItem> _monthOptions = new List<MonthItem>()
        {
            new MonthItem("January", 1),
            new MonthItem("February", 2),
            new MonthItem("March", 3),
            new MonthItem("April", 4),
            new MonthItem("May", 5),
            new MonthItem("June", 6),
            new MonthItem("July", 7),
            new MonthItem("August", 8),
            new MonthItem("September", 9),
            new MonthItem("October", 10),
            new MonthItem("November", 11),
            new MonthItem("December", 12),
        };

        /// <summary>
        /// Gets the list of available month options for configuring seasonal analysis.
        /// </summary>
        public List<MonthItem> MonthOptions => _monthOptions;

        /// <summary>
        /// Backing field for the <see cref="TimeBlockOptions"/> property. Allocated once to avoid creating a new list on every XAML binding call.
        /// </summary>
        private static readonly List<TimeBlockItem> _timeBlockOptions = new List<TimeBlockItem>()
        {
            new TimeBlockItem("Calendar Year", TimeBlockWindow.CalendarYear),
            new TimeBlockItem("Water Year", TimeBlockWindow.WaterYear),
        };

        /// <summary>
        /// Gets the list of available time block window options (Calendar Year or Water Year).
        /// </summary>
        public List<TimeBlockItem> TimeBlockOptions => _timeBlockOptions;

        #region Property Attributes

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Name control.
        /// Displays property attributes for the Name property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Description control.
        /// Displays property attributes for the Description property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CreationDate control.
        /// Displays property attributes for the CreationDate property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the LastModified control.
        /// Displays property attributes for the LastModified property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the InputData control.
        /// Displays property attributes for the InputData property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void InputData_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.InputData), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Threshold control.
        /// Displays property attributes for the Threshold property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void Threshold_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.PointProcess.Threshold), Element.PointProcess);
        }


        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the TotalYears control.
        /// Displays property attributes for the TotalYears property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void TotalYears_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.PointProcess.TotalYears), Element.PointProcess);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the UseDefaults control.
        /// Displays property attributes for the UseDefaults property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void UseDefaults_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.PointProcess.UseDefaults), Element.PointProcess);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the IsSeasonal control.
        /// Displays property attributes for the IsSeasonal property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void IsSeasonal_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.PointProcess.IsSeasonal), Element.PointProcess);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the TimeBlock control.
        /// Displays property attributes for the TimeBlock property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void TimeBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.PointProcess.TimeBlock), Element.PointProcess);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the StartMonth control.
        /// Displays property attributes for the StartMonth property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void StartMonth_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.PointProcess.StartMonth), Element.PointProcess);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the UseJeffreysRule control.
        /// Displays property attributes for the UseJeffreysRuleForScale property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void UseJeffreysRule_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.PointProcess.UseJeffreysRuleForScale), Element.PointProcess);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the ProbabilityOrdinatesControl.
        /// Displays default attributes for the Probability Ordinates feature.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void ProbabilityOrdinatesControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Probability Ordinates", "The exceedance probabilities used for plotting the probability distribution.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the TabItem control.
        /// Displays class-level attributes for the Element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetClassAttributes(Element);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the ParameterPriorsControl.
        /// Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void ParameterPriorsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the QuantilePriorsControl.
        /// Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void QuantilePriorsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the BayesianOptionsControl.
        /// Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void BayesianOptionsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the BayesianOutputControl.
        /// Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void BayesianOutputControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        #endregion

        /// <summary>
        /// Handles the GotFocus event for the Name control.
        /// Stores the current name and populates the existing names list for validation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event for the Name control.
        /// Reverts to the previous name if validation fails.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Name_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ElementName.NameTextBox.IsValid)
                return;
            if (Element != null)
                Element.Name = _previousName;
        }

        /// <summary>
        /// Handles the Unloaded event for the user control. Unsubscribes from the InputDataCollection
        /// to prevent memory leaks during visual-tree cycles.
        /// </summary>
        /// <remarks>
        /// Element.PropertyChanged is element-scoped and is managed exclusively in
        /// <see cref="ElementCallback"/> so it survives Unload/Reload cycles.
        /// </remarks>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeInputDataCollection();
        }

        /// <summary>
        /// Handles property changed events from the Element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing the name of the changed property.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Model object replaced (e.g., during undo) — push to sub-controls
            if (e.PropertyName == nameof(Element.PointProcess))
            {
                ParameterPriorsControl.Model = Element.PointProcess;
                QuantilePriorsControl.Model = Element.PointProcess;
                UpdateComboBoxes();
            }
            // BayesianAnalysis replaced — push to sub-controls
            if (e.PropertyName == nameof(Element.BayesianAnalysis))
            {
                BayesianOptionsControl.Analysis = Element.BayesianAnalysis;
                BayesianOutputControl.Analysis = Element.BayesianAnalysis;
            }
        }

        /// <summary>
        /// Loads the available input data elements from the project into the InputDataList collection.
        /// Unsubscribes from any previously subscribed collection, then subscribes to the new one
        /// using named handlers to allow clean unsubscription.
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
        /// Handles the SelectionChanged event for the InputData combo box.
        /// Updates visual validation indicators based on whether valid input data is selected.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Selection changed event arguments.</param>
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
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event for the Distribution combo box.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Selection changed event arguments.</param>
        private void DistributionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        /// <summary>
        /// Handles the Loaded event for combo box controls.
        /// Sets up sorted data binding for the combo box with live sorting enabled.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cmbo = (ComboBox)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = InputDataList, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(IElement.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
        }

        /// <summary>
        /// Handles the Click event for the Estimate button.
        /// Validates inputs and initiates the Bayesian analysis estimation process asynchronously,
        /// displaying progress feedback and managing UI state during execution.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
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
                System.Diagnostics.Debug.WriteLine($"PointProcessAnalysisPropertiesControl.EstimateButton_Click: {ex}");
                GenericControls.MessageBox.Show("An unexpected error occurred. Please verify your analysis inputs and settings." + Environment.NewLine + "If the issue persists, contact the Risk Management Center.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressCleanupEnabled = true;
                progressReporter.IndicateTaskEnded();
            }


        }

        /// <summary>
        /// Handles the Click event for the Cancel button.
        /// Requests cancellation of the currently running analysis.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the KeyDown event for the properties control.
        /// Cancels the analysis when the Escape key is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Key event arguments.</param>
        private void UnivariateAnalysisProperties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the Checked event for the IsSeasonal checkbox.
        /// Updates the visibility of seasonal-related combo boxes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void IsSeasonal_Checked(object sender, RoutedEventArgs e)
        {
            UpdateComboBoxes();
        }

        /// <summary>
        /// Handles the Unchecked event for the IsSeasonal checkbox.
        /// Updates the visibility of seasonal-related combo boxes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void IsSeasonal_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateComboBoxes();
        }

        /// <summary>
        /// Handles the SelectionChanged event for the TimeBlock combo box.
        /// Updates the visibility of dependent controls based on the selected time block.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Selection changed event arguments.</param>
        private void TimeBlockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateComboBoxes();
        }

        /// <summary>
        /// Updates the visibility of time block and start month controls based on
        /// the seasonal analysis setting and selected time block window type.
        /// </summary>
        private void UpdateComboBoxes()
        {
            if (Element == null || Element.PointProcess == null) return;
            if (Element.PointProcess.IsSeasonal == true)
            {
                TimeBlock.Visibility = Visibility.Visible;
                StartMonth.Visibility = Element.PointProcess.TimeBlock == TimeBlockWindow.CalendarYear ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                TimeBlock.Visibility = Visibility.Collapsed;
                StartMonth.Visibility = Visibility.Collapsed;
            }

        }


    }
}
