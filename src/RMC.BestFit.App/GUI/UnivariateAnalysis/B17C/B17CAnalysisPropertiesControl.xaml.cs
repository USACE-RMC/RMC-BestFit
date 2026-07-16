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
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using NumericControls;
using System.Windows.Media;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for editing B17C frequency analysis properties, including input data selection,
    /// distribution parameters, parameter priors, quantile priors, and uncertainty analysis settings.
    /// This control provides the interface for configuring and executing Bulletin 17C statistical analyses.
    /// </summary>
    public partial class B17CAnalysisPropertiesControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="B17CAnalysisPropertiesControl"/> class.
        /// </summary>
        public B17CAnalysisPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
            this.Unloaded += UserControl_Unloaded;
        }

        /// <summary>
        /// Stores the previous name value for validation and rollback purposes.
        /// </summary>
        private string _previousName;

        /// <summary>
        /// Cached reference to the distribution ComboBox for programmatic selection during undo.
        /// </summary>
        private ComboBox _distributionComboBox;

        /// <summary>
        /// Tracks the currently subscribed InputDataCollection for clean unsubscription.
        /// </summary>
        private IElementCollection _subscribedInputDataCollection;

        /// <summary>
        /// Dependency property for the Element property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(B17CAnalysis), typeof(B17CAnalysisPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the B17C analysis element being edited by this control.
        /// </summary>
        public B17CAnalysis Element
        {
            get { return (B17CAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element dependency property changes.
        /// Initializes property attributes, loads input data, and binds data grids.
        /// </summary>
        /// <param name="d">The dependency object that changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as B17CAnalysisPropertiesControl == null) return;
            var thisControl = (B17CAnalysisPropertiesControl)d;

            // Unsubscribe from old element
            if (e.OldValue is B17CAnalysis oldElement)
            {
                oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as B17CAnalysis;
            if (newElement == null) return;

            // Initialize previous name for rollback safety
            thisControl._previousName = newElement.Name;

            newElement.PropertyChanged += thisControl.Element_PropertyChanged;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.LoadInputData();
            thisControl.ParametersTable.ItemsSource = newElement.ParameterPenalties;
            thisControl.QuantileTable.ItemsSource = newElement.QuantilePenalties;
        }

        /// <summary>
        /// Dependency property for the existing names property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(B17CAnalysisPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Array of existing names for this element type.
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
        /// Gets the observable collection of available univariate distributions for B17C analysis.
        /// </summary>
        public ObservableCollection<UnivariateDistributionItem> DistributionList { get; private set; } = new ObservableCollection<UnivariateDistributionItem>()
        {
            new UnivariateDistributionItem("Exponential", UnivariateDistributionType.Exponential),
            new UnivariateDistributionItem("Gamma", UnivariateDistributionType.GammaDistribution),
            new UnivariateDistributionItem("Log-Normal", UnivariateDistributionType.LogNormal),
            new UnivariateDistributionItem("Log-Pearson Type III", UnivariateDistributionType.LogPearsonTypeIII),
            new UnivariateDistributionItem("Normal", UnivariateDistributionType.Normal),
            new UnivariateDistributionItem("Pearson Type III", UnivariateDistributionType.PearsonTypeIII),
        };

        /// <summary>
        /// Backing field for the <see cref="AEPOptions"/> property. Allocated once to avoid creating a new list on every XAML binding call.
        /// </summary>
        private static readonly List<double> _aepOptions = new List<double>() { 0.1, 0.05, 0.02, 0.01, 0.005, 0.002, 0.001, 0.0005, 0.0002, 0.0001, 0.00005, 0.00002, 0.00001, 0.000005, 0.000002, 0.000001 };

        /// <summary>
        /// Gets the list of available annual exceedance probability (AEP) options for quantile priors.
        /// </summary>
        public List<double> AEPOptions => _aepOptions;

        /// <summary>
        /// Gets the observable collection of available confidence interval widths.
        /// </summary>
        public ObservableCollection<CredibleIntervalItem> CredibleIntervalItems { get; private set; } = new ObservableCollection<CredibleIntervalItem>()
        {
            new CredibleIntervalItem("90%", 0.9),
            new CredibleIntervalItem("95%", 0.95),
            new CredibleIntervalItem("98%", 0.98),
            new CredibleIntervalItem("99%", 0.99)
        };

        /// <summary>
        /// Gets the observable collection of available Generalized Method of Moments (GMM) estimation strategies.
        /// </summary>
        public ObservableCollection<GMMEstimationTypeItem> EstimationMethodList { get; private set; } = new ObservableCollection<GMMEstimationTypeItem>()
        {
            new GMMEstimationTypeItem("One-Step", GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep),
            new GMMEstimationTypeItem("Two-Step", GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep),
            new GMMEstimationTypeItem("Iterative", GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative),
        };

        /// <summary>
        /// Gets the observable collection of available uncertainty quantification methods for B17C analysis.
        /// </summary>
        public ObservableCollection<B17CUncertaintyOptionItem> UncertaintyMethodList { get; private set; } = new ObservableCollection<B17CUncertaintyOptionItem>()
        {
            new B17CUncertaintyOptionItem("Multivariate Normal", RMC.BestFit.Analyses.UncertaintyMethod.MultivariateNormal, "Parameters are sampled using the asymptotic covariance matrix and the first-order accurate multivariate normal method."),
            new B17CUncertaintyOptionItem("Linked Multivariate Normal", RMC.BestFit.Analyses.UncertaintyMethod.LinkedMultivariateNormal, "Parameters are sampled using variance-stabilizing link transformations with the multivariate normal method."),
            new B17CUncertaintyOptionItem("Bootstrap", RMC.BestFit.Analyses.UncertaintyMethod.Bootstrap, "Parameters are sampled using the first-order accurate parametric bootstrap method."),
            new B17CUncertaintyOptionItem("Bias-Corrected Bootstrap", RMC.BestFit.Analyses.UncertaintyMethod.BiasCorrectedBootstrap, "Parameters are sampled using the second-order accurate and bias-corrected bootstrap method."),
        };

        /// <summary>
        /// Gets the observable collection of available point estimator options.
        /// </summary>
        public ObservableCollection<PointEstimatorItem> PointEstimatorItems { get; private set; } = new ObservableCollection<PointEstimatorItem>()
        {
            new PointEstimatorItem("Computed", BayesianAnalysis.PointEstimateType.PosteriorMode, "The computed parameter values for the distribution using the Generalized Method of Moments (GMM)."),
            new PointEstimatorItem("Mean Parameters", BayesianAnalysis.PointEstimateType.PosteriorMean, "Calculates the average of all samples, providing a balanced summary of the parameter estimates."),
        };

        #region Property Attributes

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Name field. Displays property attributes for the Name property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Description field. Displays property attributes for the Description property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the CreationDate field. Displays property attributes for the CreationDate property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the LastModified field. Displays property attributes for the LastModified property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the InputData field. Displays property attributes for the InputData property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void InputData_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.InputData), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Distribution field. Displays property attributes for the Distribution property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void Distribution_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Bulletin17CDistribution.Distribution), Element.Bulletin17CDistribution);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the ParametersTable. Displays default attributes for parameter information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void ParametersTable_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Parameter Information", "Enter distribution parameter information, such as USGS regional skew data.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the QuantileTable. Displays default attributes for quantile information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void QuantileTable_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Quantile Information", "Enter distribution quantile information, such as USGS regional quantile regression data.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the UncertaintyMethod field. Displays property attributes for the UncertaintyMethod property.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void UncertaintyMethod_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UncertaintyMethod), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the ConfidenceInterval field. Displays default attributes for confidence interval width.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void ConfidenceInterval_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Confidence Interval", "Specifies the confidence interval width. For example, a 90% confidence interval indicates that the true value lies within the interval with 90% probability.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the OutputLength field. Displays default attributes for output length.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void OutputLength_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Output Length", "Specifies the number of parameter sets to output. For example, outputting 10,000 sets is recommended to ensure an accurate 90% confidence interval.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the PRNGSeed field. Displays default attributes for the random number generator seed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void PRNGSeed_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("PRNG Seed", "Specifies the pseudo random number generator seed for the uncertainty analysis, ensuring repeatability.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the PointEstimator field. Displays default attributes for point estimator selection.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void PointEstimator_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Point Estimator", "Specifies the point estimator for summarizing the parent distribution. Choose 'Mean Parameters' for the average or 'Computed' for the GMM fit.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the ProbabilityOrdinatesControl. Displays default attributes for probability ordinates.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void ProbabilityOrdinatesControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.SetDefaultAttributes("Probability Ordinates", "The exceedance probabilities used for plotting the probability distribution.");
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on a TabItem. Displays class-level attributes for the Element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetClassAttributes(Element);
        }

        #endregion

        /// <summary>
        /// Handles the GotFocus event on the Name field. Stores the current name and retrieves existing element names for validation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event on the Name field. Reverts to the previous name if the new name is invalid.
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
        /// Handles the Unloaded event of the user control. Unsubscribes control-scoped event handlers
        /// to prevent stale callbacks during visual-tree cycles.
        /// </summary>
        /// <remarks>
        /// Element.PropertyChanged is element-scoped and is managed exclusively in
        /// <see cref="ElementCallback"/> so it survives Unload/Reload visual-tree cycles.
        /// Only the InputDataCollection subscription (control-scoped) is released here.
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
        /// <param name="e">Event arguments containing the property name that changed.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Marshal to the UI thread if called from a background thread (the model-layer
            // RaisePropertyChange does not marshal, and run-time notifications can arrive on
            // worker threads). Rebinding ItemsSource cross-thread throws InvalidOperationException.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Element_PropertyChanged(sender, e)));
                return;
            }

            // Distribution object replaced (e.g., during undo restoration) — explicitly rebind
            // penalty grids and sync distribution combo box. Do NOT rely on WPF multi-level binding
            // path refresh for nested object replacement.
            if (e.PropertyName == nameof(Element.Bulletin17CDistribution))
            {
                ParametersTable.ItemsSource = null;
                ParametersTable.ItemsSource = Element.ParameterPenalties;
                QuantileTable.ItemsSource = null;
                QuantileTable.ItemsSource = Element.QuantilePenalties;
                if (_distributionComboBox != null)
                    _distributionComboBox.SelectedValue = Element.Bulletin17CDistribution?.DistributionType;
            }
            // Parameter penalties list was rebuilt (distribution type changed or SetDefaultParameters ran).
            // "Parameters" and "SetDefaultParameters" are inner Bulletin17CDistribution property names
            // forwarded via B17CAnalysis.PropertyChanged — no matching public property on B17CAnalysis,
            // so they remain as literal strings.
            if (e.PropertyName == nameof(Element.ParameterPenalties) || e.PropertyName == "Parameters" || e.PropertyName == "SetDefaultParameters")
            {
                ParametersTable.ItemsSource = null;
                ParametersTable.ItemsSource = Element.ParameterPenalties;
            }
            // Quantile penalties list was rebuilt
            if (e.PropertyName == nameof(Element.QuantilePenalties))
            {
                QuantileTable.ItemsSource = null;
                QuantileTable.ItemsSource = Element.QuantilePenalties;
            }
        }

        /// <summary>
        /// Loads the available input data from the project into the InputDataList collection.
        /// Subscribes to element addition and removal events to keep the list synchronized.
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
        /// Handles an input data element being added to the project.
        /// </summary>
        private void OnInputDataElementAdded(IElement x) => InputDataList.Add((InputData)x);

        /// <summary>
        /// Handles an input data element being removed from the project.
        /// </summary>
        private void OnInputDataElementRemoved(IElement x) => InputDataList.Remove((InputData)x);

        /// <summary>
        /// Unsubscribes from the currently tracked InputDataCollection to prevent memory leaks.
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
        /// Handles the SelectionChanged event of the InputDataComboBox. Updates visual validation indicators
        /// based on whether valid input data is selected.
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
        /// Handles the SelectionChanged event of the DistributionComboBox.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Selection changed event arguments.</param>
        private void DistributionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        /// <summary>
        /// Captures a reference to the Distribution ComboBox for programmatic selection during undo.
        /// </summary>
        private void DistributionComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            _distributionComboBox = sender as ComboBox;
        }

        /// <summary>
        /// Handles the Loaded event of the ComboBox. Sets up sorted input data as the item source.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
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
        /// Handles the Click event of the EstimateButton. Validates inputs, configures the UI for simulation,
        /// and executes the B17C frequency analysis asynchronously with progress reporting.
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

            // Set up progress bar
            AnalysisProgressDisplayHelper.ShowInitial(ProgressBar, ProgressTextBlock, "Fitting with GMM...");

            // Show cancel button
            EstimateButton.Visibility = Visibility.Hidden;
            CancelButton.Visibility = Visibility.Visible;

            // Set up progress reporter
            var progressReporter = new SafeProgressReporter(nameof(B17CAnalysis));
            progressReporter.ProgressReported += (SafeProgressReporter reporter, double progress, double progressDelta) =>
            {
                AnalysisProgressDisplayHelper.PostProgress(Dispatcher, ProgressBar, ProgressTextBlock, progress, "Fitting with GMM...");
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
                System.Diagnostics.Debug.WriteLine($"B17CAnalysisPropertiesControl.EstimateButton_Click: {ex}");
                GenericControls.MessageBox.Show("An unexpected error occurred. Please verify your analysis inputs and settings." + Environment.NewLine + "If the issue persists, contact the Risk Management Center.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressCleanupEnabled = true;
                progressReporter.IndicateTaskEnded();
            }


        }

        /// <summary>
        /// Handles the Click event of the CancelButton. Cancels the currently running analysis.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Element.CancelAnalysis();
        }

        /// <summary>
        /// Handles the KeyDown event for the analysis properties control. Cancels the analysis when Escape key is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Key event arguments.</param>
        private void AnalysisProperties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Element.CancelAnalysis();
        }


    }
}
