using Numerics.Distributions;
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
    /// User control for configuring and managing mixture distribution analysis properties.
    /// Provides settings for distribution selection, parameter priors, quantile priors,
    /// Bayesian analysis options, and execution controls for running the analysis.
    /// </summary>
    public partial class MixtureAnalysisPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MixtureAnalysisPropertiesControl"/> class.
        /// Sets up the component and configures the distribution data grid.
        /// </summary>
        public MixtureAnalysisPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
            // RowType set to null — enum types cannot be default-constructed by DataGrid toolbar
            // (Activator.CreateInstance on an enum yields the 0-value, which is not a supported mixture type).
            // Add/remove is handled via PreviewAddRows/PreviewDeleteRows to inject Normal as the default.
            DistributionDataGrid.RowType = null;
            DistributionDataGrid.PreviewAddRows += DistributionDataGrid_PreviewAddRows;
            DistributionDataGrid.PreviewDeleteRows += DistributionDataGrid_PreviewDeleteRows;
            this.Unloaded += UserControl_Unloaded;
        }

        /// <summary>
        /// Stores the previous name value for validation and rollback purposes.
        /// </summary>
        private string _previousName;

        /// <summary>
        /// Tracks the InputDataCollection currently subscribed to, so it can be unsubscribed when the control unloads or the element changes.
        /// </summary>
        private IElementCollection _subscribedInputDataCollection;

        /// <summary>
        /// Identifies the <see cref="Element"/> dependency property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(MixtureAnalysis), typeof(MixtureAnalysisPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the mixture analysis element whose properties are being configured.
        /// </summary>
        public MixtureAnalysis Element
        {
            get { return (MixtureAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Element"/> dependency property changes.
        /// Subscribes to property change events, loads input data, and refreshes the distribution data grid.
        /// </summary>
        /// <param name="d">The dependency object on which the property changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as MixtureAnalysisPropertiesControl == null) return;
            var thisControl = (MixtureAnalysisPropertiesControl)d;

            // Unsubscribe from old element — both PropertyChanged (element-scoped) and
            // InputDataCollection subscriptions so the new element's LoadInputData starts clean.
            if (e.OldValue is MixtureAnalysis oldElement)
            {
                oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                thisControl.UnsubscribeInputDataCollection();
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as MixtureAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Element_PropertyChanged;
            thisControl._previousName = newElement.Name;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadInputData();
            thisControl.DistributionDataGrid.ItemsSource = newElement.Distributions;
            thisControl.DistributionDataGrid.Items.Refresh();
        }

        /// <summary>
        /// Identifies the <see cref="ExistingNames"/> dependency property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(MixtureAnalysisPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Gets or sets the array of existing analysis names used for name uniqueness validation.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Gets the observable collection of available input data sets from the project.
        /// </summary>
        public ObservableCollection<InputData> InputDataList { get; private set; } = new ObservableCollection<InputData>();

        /// <summary>
        /// Gets the observable collection of available univariate distribution types for mixture modeling.
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
        /// Gets the observable collection of available credible interval width options for Bayesian analysis.
        /// </summary>
        public ObservableCollection<CredibleIntervalItem> CredibleIntervalItems { get; private set; } = new ObservableCollection<CredibleIntervalItem>()
        {
            new CredibleIntervalItem("90%", 0.9),
            new CredibleIntervalItem("95%", 0.95),
            new CredibleIntervalItem("98%", 0.98),
            new CredibleIntervalItem("99%", 0.99)
        };

        #region Property Attributes

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Name control.
        /// Displays property attributes for the Name property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Description control.
        /// Displays property attributes for the Description property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the CreationDate control.
        /// Displays property attributes for the CreationDate property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the LastModified control.
        /// Displays property attributes for the LastModified property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the InputData control.
        /// Displays property attributes for the InputData property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void InputData_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.InputData), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the IsZeroInflated control.
        /// Displays property attributes for the IsZeroInflated property of the mixture distribution.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void IsZeroInflated_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.MixtureDistribution.IsZeroInflated), Element.MixtureDistribution);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the DistributionDataGrid control.
        /// Displays property attributes for the Distributions property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void DistributionDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Distributions), Element);
        }

        /// <summary>
        /// Handles the SelectionChanged event on distribution type combo boxes in the DataGrid.
        /// Updates the corresponding entry in the Element's Distributions collection.
        /// </summary>
        private void DistributionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;
            var comboBox = sender as System.Windows.Controls.ComboBox;
            if (comboBox == null) return;
            var selectedItem = comboBox.SelectedValue;
            if (selectedItem == null || !(selectedItem is UnivariateDistributionType newType)) return;

            // Find the row index for this combo box
            var row = DistributionDataGrid.ItemContainerGenerator.ContainerFromItem(comboBox.DataContext);
            if (row == null) return;
            int index = DistributionDataGrid.ItemContainerGenerator.IndexFromContainer(row);
            if (index < 0 || index >= Element.Distributions.Count) return;

            if (Element.Distributions[index] != newType)
            {
                Element.Distributions[index] = newType;
            }
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the UseJeffreysRule control.
        /// Displays property attributes for the UseJeffreysRuleForScale property of the mixture distribution.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void UseJeffreysRule_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.MixtureDistribution.UseJeffreysRuleForScale), Element.MixtureDistribution);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the ProbabilityOrdinatesControl.
        /// Displays default attributes for the Probability Ordinates property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void ProbabilityOrdinatesControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Probability Ordinates", "The exceedance probabilities used for plotting the probability distribution.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on a TabItem.
        /// Displays class-level attributes for the Element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
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
        /// Handles the GotFocus event on the Name control. Stores the current name and retrieves existing names for validation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event on the Name control. Restores the previous name if validation fails.
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
        /// Intercepts the DataGrid add-row operation. Cancels the default add (which would create the
        /// wrong enum default) and manually adds <see cref="UnivariateDistributionType.Normal"/> instead.
        /// Enforces a maximum of 3 distributions.
        /// </summary>
        /// <param name="startRowIndex">The starting row index for the new rows.</param>
        /// <param name="nRows">The number of rows to add.</param>
        /// <param name="cancelAddRows">Set to <see langword="true"/> to cancel the default add operation.</param>
        private void DistributionDataGrid_PreviewAddRows(int startRowIndex, int nRows, ref bool cancelAddRows)
        {
            cancelAddRows = true;
            if (Element == null) return;
            for (int i = 0; i < nRows && Element.Distributions.Count < 3; i++)
                Element.Distributions.Add(UnivariateDistributionType.Normal);
        }

        /// <summary>
        /// Intercepts the DataGrid delete-row operation. Enforces a minimum of 1 distribution.
        /// </summary>
        private void DistributionDataGrid_PreviewDeleteRows(List<int> rowIndices, ref bool cancel)
        {
            if (Element == null) { cancel = true; return; }
            // Prevent deleting all distributions — at least 1 must remain
            if (Element.Distributions.Count - rowIndices.Count < 1)
            {
                cancel = true;
                return;
            }
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
        /// Handles property changes on the associated Element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the name of the changed property.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Model object replaced (e.g., during undo) — push to sub-controls
            if (e.PropertyName == nameof(Element.MixtureDistribution))
            {
                ParameterPriorsControl.Model = Element.MixtureDistribution;
                QuantilePriorsControl.Model = Element.MixtureDistribution;
            }
            // BayesianAnalysis replaced — push to sub-controls
            if (e.PropertyName == nameof(Element.BayesianAnalysis))
            {
                BayesianOptionsControl.Analysis = Element.BayesianAnalysis;
                BayesianOutputControl.Analysis = Element.BayesianAnalysis;
            }
            // Distributions collection changed — refresh the data grid
            if (e.PropertyName == nameof(Element.Distributions))
            {
                DistributionDataGrid.ItemsSource = null;
                DistributionDataGrid.ItemsSource = Element.Distributions;
            }
        }

        /// <summary>
        /// Loads the available input data sets from the project's InputDataCollection.
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
        /// Handles the SelectionChanged event on the InputDataComboBox. Provides visual feedback for invalid selection.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The selection changed event data.</param>
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
        /// Handles the Loaded event on a ComboBox. Sets up sorting for the input data list and binds it to the combo box.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
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
        /// Handles the Click event on the Estimate button. Validates inputs, disables UI elements,
        /// sets up progress reporting, and executes the Bayesian mixture analysis asynchronously.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
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
            MixtureOptionsExpander.IsEnabled = false;
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
                MixtureOptionsExpander.IsEnabled = true;
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
                System.Diagnostics.Debug.WriteLine($"MixtureAnalysisPropertiesControl.EstimateButton_Click: {ex}");
                GenericControls.MessageBox.Show("An unexpected error occurred. Please verify your analysis inputs and settings." + Environment.NewLine + "If the issue persists, contact the Risk Management Center.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressCleanupEnabled = true;
                progressReporter.IndicateTaskEnded();
            }


        }

        /// <summary>
        /// Handles the Click event on the Cancel button. Cancels the currently running analysis.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the KeyDown event on the MixtureAnalysisProperties control.
        /// Cancels the analysis if the Escape key is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The key event data.</param>
        private void MixtureAnalysisProperties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element.CancelAnalysis();
        }

    }
}

