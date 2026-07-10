using Numerics.Distributions.Copulas;
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
    /// User control for displaying and editing rating curve analysis properties.
    /// Provides interface for configuring analysis settings, input data, parameter priors,
    /// Bayesian options, and initiating the estimation process.
    /// </summary>
    public partial class RatingCurveAnalysisPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RatingCurveAnalysisPropertiesControl"/> class.
        /// </summary>
        public RatingCurveAnalysisPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
            this.Unloaded += UserControl_Unloaded;
        }

        /// <summary>
        /// Stores the previous name value for validation and rollback purposes.
        /// </summary>
        private string _previousName;

        /// <summary>The <see cref="TimeSeriesCollection"/> currently subscribed (named handlers).</summary>
        private IElementCollection _subscribedTimeSeriesCollection;

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(RatingCurveAnalysis), typeof(RatingCurveAnalysisPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the rating curve analysis element whose properties are being displayed and edited.
        /// </summary>
        public RatingCurveAnalysis Element
        {
            get { return (RatingCurveAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Element"/> property changes.
        /// Initializes the control with the new element's data and sets up event handlers.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as RatingCurveAnalysisPropertiesControl == null) return;
            var thisControl = (RatingCurveAnalysisPropertiesControl)d;

            // Unsubscribe from old element
            if (e.OldValue is RatingCurveAnalysis oldElement)
                oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as RatingCurveAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Element_PropertyChanged;
            thisControl._previousName = newElement.Name;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadTimeSeries();


        }

        /// <summary>
        /// Dependency property for the <see cref="ExistingNames"/> property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(RatingCurveAnalysisPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Gets the array of existing element names used for validation to prevent duplicate names.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Gets the observable collection of available time series elements for data binding.
        /// Used to populate stage and discharge data selection dropdowns.
        /// </summary>
        public ObservableCollection<TimeSeriesElement> TimeSeriesElements { get; private set; } = new ObservableCollection<TimeSeriesElement>();


        #region Property Attributes

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Name field.
        /// Displays property attributes for the element's name.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Description field.
        /// Displays property attributes for the element's description.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CreationDate field.
        /// Displays property attributes for the element's creation date.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the LastModified field.
        /// Displays property attributes for the element's last modified date.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the StageDataComboBox.
        /// Displays property attributes for the stage data selection.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void StageDataComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.StageData), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the DischargeDataComboBox.
        /// Displays property attributes for the discharge data selection.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void DischargeDataComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.DischargeData), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the NumberOfSegments field.
        /// Displays property attributes for the rating curve's number of segments.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void NumberOfSegments_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.RatingCurve.NumberOfSegments), Element.RatingCurve);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the UseJeffreysRule field.
        /// Displays property attributes for the Jeffrey's rule for scale setting.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void UseJeffreysRule_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.RatingCurve.UseJeffreysRuleForScale), Element.RatingCurve);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CredibleInterval field.
        /// Displays property attributes for the credible interval width setting.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void CredibleInterval_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.BayesianAnalysis.CredibleIntervalWidth), Element.BayesianAnalysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the OutputLength field.
        /// Displays property attributes for the MCMC output length setting.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void OutputLength_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.BayesianAnalysis.OutputLength), Element.BayesianAnalysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the MinStage field.
        /// Displays property attributes for the minimum stage value.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void MinStage_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.MinStage), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the MaxStage field.
        /// Displays property attributes for the maximum stage value.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void MaxStage_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.MaxStage), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the StageBins field.
        /// Displays property attributes for the number of stage bins.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void StageBins_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.StageBins), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the UseDefaults field.
        /// Displays property attributes for the use default stage bins setting.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void UseDefaults_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UseDefaultStageBins), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for tab items.
        /// Displays class-level attributes for the element.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The mouse button event arguments.</param>
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetClassAttributes(Element);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the ParameterPriorsControl.
        /// Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object instance containing the property.</param>
        private void ParameterPriorsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the BayesianOptionsControl.
        /// Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object instance containing the property.</param>
        private void BayesianOptionsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the BayesianOutputControl.
        /// Displays property attributes for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object instance containing the property.</param>
        private void BayesianOutputControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }


        #endregion

        /// <summary>
        /// Handles the GotFocus event for the Name field.
        /// Stores the current name and retrieves the list of existing names for validation.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event for the Name field.
        /// Reverts to the previous name if the new name is invalid.
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
        /// Handles the Unloaded event of the user control. Unsubscribes from element event handlers
        /// to prevent memory leaks and stale event subscriptions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeTimeSeriesCollection();
        }

        /// <summary>
        /// Handles property changes on the <see cref="Element"/>.
        /// Can be used to respond to changes in element properties.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Model object replaced (e.g., during undo) — push to sub-controls
            if (e.PropertyName == nameof(Element.RatingCurve))
            {
                ParameterPriorsControl.Model = Element.RatingCurve;
            }
            // BayesianAnalysis replaced — push to sub-controls
            if (e.PropertyName == nameof(Element.BayesianAnalysis))
            {
                BayesianOptionsControl.Analysis = Element.BayesianAnalysis;
                BayesianOutputControl.Analysis = Element.BayesianAnalysis;
            }
        }

        /// <summary>
        /// Loads available time series elements from the project.
        /// Populates the <see cref="TimeSeriesElements"/> collection and sets up event handlers
        /// to track additions and removals of time series elements.
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
        /// Handles the SelectionChanged event for the StageDataComboBox.
        /// Updates the visual state and tooltip to indicate whether a valid selection has been made.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void StageDataComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // First-load NRE guard (CLAUDE.md "WPF DataContext Pattern").
            if (Element == null) return;
            if (Element.StageData == null || Element.StageData.Name == null)
            {
                ((Border)StageDataComboBox.InnerContent).BorderThickness = new Thickness(1);
                StageDataComboBox.ToolTip = "Please select a valid stage time series.";
            }
            else
            {
                ((Border)StageDataComboBox.InnerContent).BorderThickness = new Thickness(0);
                StageDataComboBox.ToolTip = null;
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event for the DischargeDataComboBox.
        /// Updates the visual state and tooltip to indicate whether a valid selection has been made.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void DischargeDataComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // First-load NRE guard (CLAUDE.md "WPF DataContext Pattern").
            if (Element == null) return;
            if (Element.DischargeData == null || Element.DischargeData.Name == null)
            {
                ((Border)DischargeDataComboBox.InnerContent).BorderThickness = new Thickness(1);
                DischargeDataComboBox.ToolTip = "Please select a valid discharge time series.";
            }
            else
            {
                ((Border)DischargeDataComboBox.InnerContent).BorderThickness = new Thickness(0);
                DischargeDataComboBox.ToolTip = null;
            }
        }

        /// <summary>
        /// Handles the Loaded event for the StageDataComboBox.
        /// Sets up the sorted data binding for the combo box items.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void StageDataComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cmbo = (ComboBox)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = TimeSeriesElements, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(IElement.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
        }

        /// <summary>
        /// Handles the Loaded event for the DischargeDataComboBox.
        /// Sets up the sorted data binding for the combo box items.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void DischargeDataComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cmbo = (ComboBox)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = TimeSeriesElements, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(IElement.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
        }

        /// <summary>
        /// Handles the Click event for the Estimate button.
        /// Validates the analysis configuration and initiates the Bayesian estimation process.
        /// Manages UI state during the estimation including progress reporting and window disabling.
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
                System.Diagnostics.Debug.WriteLine($"RatingCurveAnalysisPropertiesControl.EstimateButton_Click: {ex}");
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
        /// Cancels the currently running Bayesian analysis.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the KeyDown event for the properties panel.
        /// Cancels the analysis when the Escape key is pressed.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The key event arguments.</param>
        private void Properties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element.CancelAnalysis();
        }


    }
}
