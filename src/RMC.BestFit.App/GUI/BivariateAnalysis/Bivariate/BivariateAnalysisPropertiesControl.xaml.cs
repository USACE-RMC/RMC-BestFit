using FrameworkInterfaces;
using GenericControls;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
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
using Numerics.Distributions.Copulas;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for configuring and managing properties of a bivariate analysis.
    /// Provides controls for selecting marginal distributions, copula types, estimation methods,
    /// Bayesian settings, and parameter priors. Also manages the execution of Bayesian estimation.
    /// </summary>
    public partial class BivariateAnalysisPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BivariateAnalysisPropertiesControl"/> class.
        /// </summary>
        public BivariateAnalysisPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
            this.Unloaded += UserControl_Unloaded;
        }

        /// <summary>
        /// Stores the previous name of the element for validation purposes.
        /// </summary>
        private string _previousName;

        /// <summary>
        /// The <see cref="UnivariateAnalysisCollection"/> currently subscribed via
        /// <see cref="OnUnivariateAnalysisAdded"/> / <see cref="OnUnivariateAnalysisRemoved"/>.
        /// Tracked so we can unsubscribe when the Element changes or the control unloads,
        /// preventing stale lambdas from accumulating across open/close cycles.
        /// </summary>
        private IElementCollection _subscribedUnivariateCollection;

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(BivariateAnalysis), typeof(BivariateAnalysisPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the bivariate analysis element whose properties are being configured.
        /// </summary>
        public BivariateAnalysis Element
        {
            get { return (BivariateAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback invoked when the Element dependency property changes.
        /// Subscribes to property change events and initializes the control state.
        /// </summary>
        /// <param name="d">The dependency object on which the property changed.</param>
        /// <param name="e">Event arguments containing old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as BivariateAnalysisPropertiesControl == null) return;
            var thisControl = (BivariateAnalysisPropertiesControl)d;

            // Unsubscribe from old element
            if (e.OldValue is BivariateAnalysis oldElement)
                oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as BivariateAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Element_PropertyChanged;
            thisControl._previousName = newElement.Name;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadUnivariateAnalyses();


        }

        /// <summary>
        /// Dependency property for the existing names property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(BivariateAnalysisPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Gets or sets the array of existing element names used for validation to prevent duplicate names.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Gets the observable collection of univariate analyses available for selection as marginal distributions.
        /// </summary>
        public ObservableCollection<IUnivariate> UnivariateAnalysisList { get; private set; } = new ObservableCollection<IUnivariate>();

        /// <summary>
        /// Gets the observable collection of available copula types for bivariate modeling.
        /// Includes Ali-Mikhail-Haq, Clayton, Frank, Gumbel, Joe, and Normal copulas.
        /// </summary>
        /// <remarks>
        /// The Student's t copula is intentionally omitted from the user-facing dropdown:
        /// its 2-parameter likelihood (?, ?) is much more expensive to evaluate than the
        /// closed-form 1-parameter copulas (each MCMC likelihood call requires inverse-CDF
        /// evaluations on the univariate Student's t), and ? is only weakly identified at
        /// typical hydrologic sample sizes. The copula itself (<c>CopulaType.StudentT</c>)
        /// remains fully supported in the model library and is exercised by the verification
        /// suite � it can be re-enabled here once either a better prior is plumbed through
        /// or runtime costs are reduced.
        /// </remarks>
        public ObservableCollection<CopulaItem> CopulaList { get; private set; } = new ObservableCollection<CopulaItem>()
        { new CopulaItem("Ali-Mikhail-Haq", CopulaType.AliMikhailHaq),
          new CopulaItem("Clayton", CopulaType.Clayton),
          new CopulaItem("Frank", CopulaType.Frank),
          new CopulaItem("Gumbel", CopulaType.Gumbel),
          new CopulaItem("Joe", CopulaType.Joe),
          new CopulaItem("Normal", CopulaType.Normal),
        };

        /// <summary>
        /// Gets the observable collection of available copula estimation methods.
        /// Includes Inference from Margins (IFM) and Pseudo-Likelihood methods.
        /// </summary>
        public ObservableCollection<CopulaEstimationItem> MethodList { get; private set; } = new ObservableCollection<CopulaEstimationItem>()
        {
            new CopulaEstimationItem("Inference from Margins", CopulaEstimationMethod.InferenceFromMargins, "Estimates the copula using fitted marginal distributions (Inference from Margins, IFM)."),
            new CopulaEstimationItem("Pseudo-Likelihood", CopulaEstimationMethod.PseudoLikelihood, "Estimates the copula using empirical ranks from the marginal data (Pseudo-Likelihood).")
        };

        /// <summary>
        /// Gets the observable collection of credible interval width options for Bayesian estimation.
        /// Includes 90%, 95%, 98%, and 99% intervals.
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
        /// Handles the PreviewMouseLeftButtonDown event for the Name control.
        /// Displays property attributes for the element name.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Description control.
        /// Displays property attributes for the element description.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CreationDate control.
        /// Displays property attributes for the element creation date.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the LastModified control.
        /// Displays property attributes for the element's last modified date.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the MarginalX combo box.
        /// Displays property attributes for the X marginal distribution.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void MarginalXComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.MarginalX), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the MarginalY combo box.
        /// Displays property attributes for the Y marginal distribution.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void MarginalYComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.MarginalY), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the Copula control.
        /// Displays property attributes for the selected copula type.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void Copula_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.BivariateDistribution.Copula), Element.BivariateDistribution);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the EstimationMethod control.
        /// Displays property attributes for the copula estimation method.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void EstimationMethod_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.BivariateDistribution.CopulaEstimationMethod), Element.BivariateDistribution);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the CredibleInterval control.
        /// Displays property attributes for the credible interval width.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void CredibleInterval_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.BayesianAnalysis.CredibleIntervalWidth), Element.BayesianAnalysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the OutputLength control.
        /// Displays property attributes for the MCMC output length.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void OutputLength_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.BayesianAnalysis.OutputLength), Element.BayesianAnalysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for tab items.
        /// Displays class-level attributes for the element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetClassAttributes(Element);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the ParameterPriorsControl.
        /// Displays property attributes for the specified property and class object.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void ParameterPriorsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the BayesianOptionsControl.
        /// Displays property attributes for the specified property and class object.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void BayesianOptionsControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the ShowPropertyAttributes event from the BayesianOutputControl.
        /// Displays property attributes for the specified property and class object.
        /// </summary>
        /// <param name="propertyName">The name of the property to display attributes for.</param>
        /// <param name="classObject">The object containing the property.</param>
        private void BayesianOutputControl_ShowPropertyAttributes(string propertyName, object classObject)
        {
            PropertyAttributes.GetPropertyAttributes(propertyName, classObject);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the OrderedDataGridControl.
        /// Displays default attributes for the X-Y ordinates data grid.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void OrderedDataGridControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("X-Y Ordinates", "The X-Y values for computing joint exceedance probabilities.");
        }

        #endregion

        /// <summary>
        /// Handles the GotFocus event for the Name control.
        /// Saves the previous name and updates the list of existing names for validation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event for the Name control.
        /// Restores the previous name if validation fails.
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
        /// Handles the Unloaded event of the user control. Unsubscribes from element event handlers
        /// to prevent memory leaks and stale event subscriptions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeUnivariateAnalysisCollection();
        }

        /// <summary>
        /// Handles property changes on the Element object.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Model object replaced (e.g., during undo) � push to sub-controls
            if (e.PropertyName == nameof(Element.BivariateDistribution))
            {
                ParameterPriorsControl.Model = Element.BivariateDistribution;
            }
            // BayesianAnalysis replaced � push to sub-controls
            if (e.PropertyName == nameof(Element.BayesianAnalysis))
            {
                BayesianOptionsControl.Analysis = Element.BayesianAnalysis;
                BayesianOutputControl.Analysis = Element.BayesianAnalysis;
            }
        }

        /// <summary>
        /// Loads all univariate analyses from the project into the <see cref="UnivariateAnalysisList"/>
        /// collection. Subscribes to element added/removed events via NAMED handlers (not anonymous
        /// lambdas) so they can be unsubscribed deterministically � see Convention 8 in the project coding standards.
        /// Calls <see cref="UnsubscribeUnivariateAnalysisCollection"/> first so repeated invocations
        /// (re-loads after Element change, or after a transient Unload/Reload cycle) cannot
        /// double-subscribe.
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
                        if (element is IUnivariate uni && uni.GetMarginalModel() is not null)
                        {
                            UnivariateAnalysisList.Add(uni);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Handles a new IUnivariate being added to the project. Accept any IUnivariate that
        /// exposes a marginal model � CompositeAnalysis returns null from GetMarginalModel
        /// because it does not own paired input data, so it is filtered out.
        /// </summary>
        private void OnUnivariateAnalysisAdded(IElement x)
        {
            if (x is IUnivariate uni && uni.GetMarginalModel() is not null && !UnivariateAnalysisList.Contains(uni))
                UnivariateAnalysisList.Add(uni);
        }

        /// <summary>
        /// Handles an IUnivariate being removed from the project.
        /// </summary>
        private void OnUnivariateAnalysisRemoved(IElement x)
        {
            if (x is IUnivariate uni)
                UnivariateAnalysisList.Remove(uni);
        }

        /// <summary>
        /// Unsubscribes the named handlers from the tracked <see cref="UnivariateAnalysisCollection"/>
        /// and clears the tracker field. Called from <see cref="UserControl_Unloaded"/> and from
        /// <see cref="LoadUnivariateAnalyses"/> before re-subscription.
        /// </summary>
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
        /// Handles the SelectionChanged event for the MarginalX combo box.
        /// Provides visual feedback if no valid marginal-X distribution is selected.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void MarginalXComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // First-load NRE guard: WPF can fire SelectionChanged during DataContext
            // resolution before Element is assigned (the WPF DataContext pattern).
            if (Element == null) return;
            if (Element.MarginalX == null || Element.MarginalX.Name == null)
            {
                ((Border)MarginalXComboBox.InnerContent).BorderThickness = new Thickness(1);
                MarginalXComboBox.ToolTip = "Please select a valid marginal-X univariate analysis.";
            }
            else
            {
                ((Border)MarginalXComboBox.InnerContent).BorderThickness = new Thickness(0);
                MarginalXComboBox.ToolTip = null;
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event for the MarginalY combo box.
        /// Provides visual feedback if no valid marginal-Y distribution is selected.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void MarginalYComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // First-load NRE guard (the WPF DataContext pattern).
            if (Element == null) return;
            if (Element.MarginalY == null || Element.MarginalY.Name == null)
            {
                ((Border)MarginalYComboBox.InnerContent).BorderThickness = new Thickness(1);
                MarginalYComboBox.ToolTip = "Please select a valid marginal-Y univariate analysis.";
            }
            else
            {
                ((Border)MarginalYComboBox.InnerContent).BorderThickness = new Thickness(0);
                MarginalYComboBox.ToolTip = null;
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event for the Copula combo box.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void CopulaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        /// <summary>
        /// Handles the Loaded event for the MarginalX combo box.
        /// Sets up the sorted item source for displaying univariate analyses.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void MarginalXComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cmbo = (ComboBox)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = UnivariateAnalysisList, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(IElement.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
        }

        /// <summary>
        /// Handles the Loaded event for the MarginalY combo box.
        /// Sets up the sorted item source for displaying univariate analyses.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void MarginalYComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cmbo = (ComboBox)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = UnivariateAnalysisList, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(IElement.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
        }

        /// <summary>
        /// Handles the Click event for the Estimate button. Initiates the Bayesian estimation process
        /// for the bivariate analysis with progress reporting and UI state management.
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
                System.Diagnostics.Debug.WriteLine($"BivariateAnalysisPropertiesControl.EstimateButton_Click: {ex}");
                GenericControls.MessageBox.Show("An unexpected error occurred. Please verify your analysis inputs and settings." + Environment.NewLine + "If the issue persists, contact the Risk Management Center.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressCleanupEnabled = true;
                progressReporter.IndicateTaskEnded();
            }


        }

        /// <summary>
        /// Handles the Click event for the Cancel button. Cancels the currently running analysis.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the KeyDown event for the control. Cancels the analysis if Escape key is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The key event data.</param>
        private void BivariateAnalysisProperties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the ColumnsAutoGenerated event for the OrderedDataGridControl.
        /// Applies user-defined string formatting to numeric columns.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The collection of auto-generated columns.</param>
        private void OrderedDataGridControl_ColumnsAutoGenerated(object sender, ObservableCollection<DataGridColumn> e)
        {
            foreach (var column in e)
            {
                if (column is DataGridTextColumn textCol)
                {
                    textCol.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";

                    // The Y ordinate is stored as the P1 property on DistributionRowItem (single-param
                    // Deterministic distribution). The auto-generated header defaults to the distribution's
                    // parameter display name ("Value" for Deterministic). Rewrite it to "Y" for the
                    // bivariate XY-ordinates grid. Matched by binding path so the fix is not fragile
                    // against changes in the Deterministic distribution's parameter name.
                    if (textCol.Binding is Binding binding && binding.Path?.Path == "P1")
                    {
                        textCol.Header = "Y";
                    }
                }
            }
        }


    }
}
