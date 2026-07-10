using RMC.BestFit.Estimation;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for configuring Bayesian analysis simulation options.
    /// Provides interface elements for setting MCMC parameters, simulation defaults,
    /// and advanced algorithm settings for Bayesian statistical inference.
    /// </summary>
    public partial class BayesianOptionsControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BayesianOptionsControl"/> class.
        /// </summary>
        public BayesianOptionsControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Occurs when property attributes should be displayed for a specific property.
        /// </summary>
        public event ShowPropertyAttributesEventHandler ShowPropertyAttributes;

        /// <summary>
        /// Represents the method that will handle the event when property attributes should be shown.
        /// </summary>
        /// <param name="propertyName">The name of the property whose attributes should be displayed.</param>
        /// <param name="classObject">The object instance containing the property.</param>
        public delegate void ShowPropertyAttributesEventHandler(string propertyName, object classObject);

        /// <summary>
        /// Identifies the <see cref="Analysis"/> dependency property.
        /// </summary>
        public static DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(BayesianAnalysis), typeof(BayesianOptionsControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis object whose options are being configured.
        /// </summary>
        public BayesianAnalysis Analysis
        {
            get { return (BayesianAnalysis)GetValue(AnalysisProperty); }
            set { SetValue(AnalysisProperty, value); }
        }

        /// <summary>
        /// Gets the collection of sampler type items for the sampler type combo box.
        /// </summary>
        public ObservableCollection<SamplerTypeItem> SamplerTypeItems { get; private set; } = new ObservableCollection<SamplerTypeItem>()
        {
            new SamplerTypeItem("DE-MCzs", BayesianAnalysis.SamplerType.DEMCzs, "Differential Evolution MCMC with snooker update. Recommended default sampler."),
            new SamplerTypeItem("DE-MCz", BayesianAnalysis.SamplerType.DEMCz, "Differential Evolution MCMC with z-matrix history."),
            new SamplerTypeItem("ARWMH", BayesianAnalysis.SamplerType.ARWMH, "Adaptive Random Walk Metropolis-Hastings with covariance adaptation."),
            new SamplerTypeItem("NUTS", BayesianAnalysis.SamplerType.NUTS, "No-U-Turn Sampler. Automatic trajectory tuning via binary tree doubling.")
        };

        /// <summary>
        /// The previously subscribed analysis for PropertyChanged events.
        /// </summary>
        private BayesianAnalysis _subscribedAnalysis;

        /// <summary>
        /// Callback method invoked when the <see cref="Analysis"/> property changes.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d is not BayesianOptionsControl thisControl) return;

            // Unsubscribe from old analysis
            if (thisControl._subscribedAnalysis != null)
            {
                thisControl._subscribedAnalysis.PropertyChanged -= thisControl.Analysis_PropertyChanged;
                thisControl._subscribedAnalysis = null;
            }

            if (e.NewValue == null) return;
            if (e.NewValue is not BayesianAnalysis newElement) return;

            // Subscribe to new analysis for sampler type changes
            newElement.PropertyChanged += thisControl.Analysis_PropertyChanged;
            thisControl._subscribedAnalysis = newElement;

            // Set initial visibility
            thisControl.UpdateAdvancedOptionsVisibility();
        }

        /// <summary>
        /// Handles PropertyChanged events from the analysis to update control visibility when sampler type changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Property changed event arguments.</param>
        private void Analysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Type))
            {
                UpdateAdvancedOptionsVisibility();
            }
        }

        /// <summary>
        /// Updates the visibility of advanced option controls based on the currently selected sampler type.
        /// </summary>
        private void UpdateAdvancedOptionsVisibility()
        {
            if (Analysis == null) return;

            var type = Analysis.Type;

            // DEMCz/DEMCzs controls
            bool isDEMC = type == BayesianAnalysis.SamplerType.DEMCz || type == BayesianAnalysis.SamplerType.DEMCzs;
            JumpParameter.Visibility = isDEMC ? Visibility.Visible : Visibility.Collapsed;
            JumpThreshold.Visibility = isDEMC ? Visibility.Visible : Visibility.Collapsed;
            SnookerThreshold.Visibility = type == BayesianAnalysis.SamplerType.DEMCzs ? Visibility.Visible : Visibility.Collapsed;
            NoiseParameter.Visibility = isDEMC ? Visibility.Visible : Visibility.Collapsed;

            // ARWMH controls
            bool isARWMH = type == BayesianAnalysis.SamplerType.ARWMH;
            ScaleParameter.Visibility = isARWMH ? Visibility.Visible : Visibility.Collapsed;
            BetaParameter.Visibility = isARWMH ? Visibility.Visible : Visibility.Collapsed;

            // NUTS controls
            bool isNUTS = type == BayesianAnalysis.SamplerType.NUTS;
            MaxTreeDepth.Visibility = isNUTS ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Identifies the <see cref="ActualPropertyWidth"/> dependency property.
        /// </summary>
        public static DependencyProperty ActualPropertyWidthProperty = DependencyProperty.Register(nameof(ActualPropertyWidth), typeof(double), typeof(BayesianOptionsControl), new UIPropertyMetadata(200d));

        /// <summary>
        /// Gets or sets the actual width of property labels in the control layout.
        /// </summary>
        public double ActualPropertyWidth
        {
            get { return (double)GetValue(ActualPropertyWidthProperty); }
            set { SetValue(ActualPropertyWidthProperty, value); }
        }

        #region PreviewMouseLeftButtonDown Handlers

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the sampler type combo box.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void SamplerType_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.Type), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the number of chains input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void NumberOfChains_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.NumberOfChains), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the thinning interval input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void ThinningInterval_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.ThinningInterval), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the warmup iterations input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void WarmupIterations_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.WarmupIterations), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the iterations input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void Iterations_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.Iterations), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the PRNG seed input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void PRNGSeed_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.PRNGSeed), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the initial iterations input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void InitialIterations_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.InitialIterations), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the use simulation defaults checkbox.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void UseSimulationDefaults_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.UseSimulationDefaults), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the jump parameter input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void JumpParameter_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.Jump), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the jump threshold input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void JumpThreshold_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.JumpThreshold), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the snooker threshold input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void SnookerThreshold_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.SnookerThreshold), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the noise parameter input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void NoiseParameter_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.Noise), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the scale parameter input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void ScaleParameter_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.Scale), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the beta parameter input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void BetaParameter_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.Beta), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the max tree depth input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void MaxTreeDepth_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.MaxTreeDepth), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the use advanced simulation defaults checkbox.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void UseAdvancedSimulationDefaults_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.UseAdvancedSimulationDefaults), Analysis);
        }

        #endregion

        /// <summary>
        /// Handles the PropertyChanged event for the warmup iterations control.
        /// Updates the control's actual property width when the warmup iterations control width changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Property changed event arguments containing the property name that changed.</param>
        private void WarmupIterations_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WarmupIterations.ActualPropertyWidth))
            {
                ActualPropertyWidth = WarmupIterations.ActualPropertyWidth;
            }
        }

        /// <summary>
        /// Handles the PropertyChanged event for the sampler type control.
        /// Updates the control's actual property width when the sampler type control width changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Property changed event arguments containing the property name that changed.</param>
        private void SamplerType_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SamplerType.ActualPropertyWidth))
            {
                ActualPropertyWidth = SamplerType.ActualPropertyWidth;
            }
        }
    }
}
