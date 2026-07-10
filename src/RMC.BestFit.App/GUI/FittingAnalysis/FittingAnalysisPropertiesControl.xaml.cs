using FrameworkInterfaces;
using FrameworkUI;
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
using Numerics.Utilities;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and editing properties of a fitting analysis.
    /// Provides interface for configuring analysis parameters, selecting input data,
    /// and executing distribution fitting operations.
    /// </summary>
    public partial class FittingAnalysisPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FittingAnalysisPropertiesControl"/> class.
        /// </summary>
        public FittingAnalysisPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// Stores the previous name value to enable validation rollback.
        /// </summary>
        private string _previousName;

        /// <summary>
        /// Tracks the currently subscribed InputDataCollection to enable proper unsubscription.
        /// </summary>
        private IElementCollection _subscribedInputDataCollection;

        /// <summary>
        /// Dependency property for the Element property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(FittingAnalysis), typeof(FittingAnalysisPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the fitting analysis element whose properties are displayed and edited in this control.
        /// </summary>
        public FittingAnalysis Element
        {
            get { return (FittingAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element property changes.
        /// Updates property attributes and loads available input data.
        /// </summary>
        /// <param name="d">The dependency object where the property changed.</param>
        /// <param name="e">Event data containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as FittingAnalysisPropertiesControl == null) return;
            var thisControl = (FittingAnalysisPropertiesControl)d;

            // Clean up old element subscriptions
            thisControl.UnsubscribeInputDataCollection();

            if (e.NewValue == null) return;
            var newElement = e.NewValue as FittingAnalysis;
            if (newElement == null) return;

            // Initialize previous name for rollback safety
            thisControl._previousName = newElement.Name;

            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadInputData();
        }

        /// <summary>
        /// Dependency property for the existing names property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(FittingAnalysisPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Gets or sets the array of existing names for this element type.
        /// Used for name validation to prevent duplicates.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Gets the observable collection of available input data elements that can be selected for the fitting analysis.
        /// </summary>
        public ObservableCollection<InputData> InputDataList { get; private set; } = new ObservableCollection<InputData>();

        #region Property Attributes

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Name property control.
        /// Displays property attributes for the Name property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event data.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Description property control.
        /// Displays property attributes for the Description property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event data.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the CreationDate property control.
        /// Displays property attributes for the CreationDate property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event data.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the LastModified property control.
        /// Displays property attributes for the LastModified property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event data.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the InputData property control.
        /// Displays property attributes for the InputData property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event data.</param>
        private void InputData_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.InputData), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the ProbabilityOrdinates control.
        /// Displays default attributes describing the probability ordinates.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event data.</param>
        private void ProbabilityOrdinatesControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Probability Ordinates", "Exceedance probabilities used to plot the probability distribution.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the TabItem control.
        /// Displays class-level attributes for the Element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event data.</param>
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetClassAttributes(Element);
        }

        #endregion

        /// <summary>
        /// Handles the GotFocus event on the Name control.
        /// Stores the current name and retrieves existing names for validation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event on the Name control.
        /// Restores the previous name if the new name is invalid.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void Name_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ElementName.NameTextBox.IsValid)
                return;
            if (Element != null)
                Element.Name = _previousName;
        }

        /// <summary>
        /// Loads the list of available input data elements from the project's InputDataCollection.
        /// Subscribes to collection change events to keep the list synchronized.
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
        /// Handles the ElementAdded event from the InputDataCollection.
        /// </summary>
        /// <param name="element">The element that was added.</param>
        private void OnInputDataElementAdded(IElement element)
        {
            InputDataList.Add((InputData)element);
        }

        /// <summary>
        /// Handles the ElementRemoved event from the InputDataCollection.
        /// </summary>
        /// <param name="element">The element that was removed.</param>
        private void OnInputDataElementRemoved(IElement element)
        {
            InputDataList.Remove((InputData)element);
        }

        /// <summary>
        /// Unsubscribes from the currently tracked InputDataCollection's events.
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
        /// Handles the SelectionChanged event on the InputData combo box.
        /// Validates that a valid input data element has been selected and updates the UI accordingly.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void InputDataComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;
            if (Element.InputData == null || Element.InputData.Name == null)
            {
                ((Border)InputDataComboBox.InnerContent).BorderThickness = new Thickness(1);
                InputDataComboBox.ToolTip = "Select valid input data.";
            }
            else
            {
                ((Border)InputDataComboBox.InnerContent).BorderThickness = new Thickness(0);
                InputDataComboBox.ToolTip = null;
            }
        }

        /// <summary>
        /// Handles the Click event of the Fit button.
        /// Validates the analysis configuration, disables the UI to prevent concurrent modifications,
        /// and executes the distribution fitting operation asynchronously.
        /// Displays progress feedback using a progress bar and a cancel button.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private async void FitButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if the fitting analysis is valid
            if (Element.IsValid == false)
            {
                GenericControls.MessageBox.Show("Cannot perform distribution fitting because the inputs are invalid.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;

            // Disable all windows
            ShellPublicVariables.SimulationInProgress = true;
            ((MainWindow)Application.Current.MainWindow).DisableMenuStrip();
            ((MainWindow)Application.Current.MainWindow).DisableProjectExplorer();
            ((MainWindow)Application.Current.MainWindow).DisableMessageWindow();
            ((MainWindow)Application.Current.MainWindow).DisableOpenWindows();
            // Disable tab items to prevent property changes during analysis
            General_TabItem.IsEnabled = false;
            Options_TabItem.IsEnabled = false;

            // Set up progress bar
            AnalysisProgressDisplayHelper.ShowInitial(ProgressBar, ProgressTextBlock);

            // Show cancel button
            FitButton.Visibility = Visibility.Hidden;
            CancelButton.Visibility = Visibility.Visible;

            // Set up progress reporter
            var progressReporter = new SafeProgressReporter(nameof(FittingAnalysis));
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
                ProgressTextBlock.Text = "Analysis Complete";
                Mouse.OverrideCursor = null;
                FitButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Hidden;
                ProgressTextBlock.Visibility = Visibility.Hidden;

                // Close cancel button
                FitButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Hidden;

                // Re-enable all windows
                ShellPublicVariables.SimulationInProgress = false;
                ((MainWindow)Application.Current.MainWindow).EnableMenuStrip();
                ((MainWindow)Application.Current.MainWindow).EnableProjectExplorer();
                ((MainWindow)Application.Current.MainWindow).EnableMessageWindow();
                ((MainWindow)Application.Current.MainWindow).EnableOpenWindows();
                // Re-enable tab items
                General_TabItem.IsEnabled = true;
                Options_TabItem.IsEnabled = true;
            };

            // Perform Fitting Analysis.
            try
            {
                await WaitCursorHelper.RunWithVisibleWaitCursorAsync(Dispatcher, async () =>
                {
                    await Element.RunAsync(progressReporter);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FittingAnalysisPropertiesControl.EstimateButton_Click: {ex}");
                    GenericControls.MessageBox.Show("An unexpected error occurred. Please verify your analysis inputs and settings." + Environment.NewLine + "If the issue persists, contact the Risk Management Center.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressCleanupEnabled = true;
                progressReporter.IndicateTaskEnded();
            }
        }

        /// <summary>
        /// Handles the Click event of the Cancel button.
        /// Cancels the currently running distribution fitting analysis.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles key down events for the control. Cancels the analysis if Escape is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The key event arguments.</param>
        private void FittingAnalysisProperties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the Loaded event of the ComboBox control.
        /// Sets up a sorted collection view for the InputDataList and binds it to the combo box.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
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
        /// Handles the Unloaded event of the UserControl.
        /// Cleans up event subscriptions to prevent memory leaks.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeInputDataCollection();
        }
    }
}
