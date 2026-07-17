using OxyPlot;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// User control for configuring Bayesian analysis output options.
    /// Provides interface elements for setting credible intervals, output length,
    /// and point estimator methods for Bayesian statistical analysis results.
    /// </summary>
    public partial class BayesianOutputControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BayesianOutputControl"/> class.
        /// </summary>
        public BayesianOutputControl()
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
        public static DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(BayesianAnalysis), typeof(BayesianOutputControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis object whose output options are being configured.
        /// </summary>
        public BayesianAnalysis Analysis
        {
            get { return (BayesianAnalysis)GetValue(AnalysisProperty); }
            set { SetValue(AnalysisProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Analysis"/> property changes.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as BayesianOutputControl == null) return;
            var thisControl = (BayesianOutputControl)d;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as BayesianAnalysis;
            if (newElement == null) return;
        }

        /// <summary>
        /// Identifies the <see cref="ActualPropertyWidth"/> dependency property.
        /// </summary>
        public static DependencyProperty ActualPropertyWidthProperty = DependencyProperty.Register(nameof(ActualPropertyWidth), typeof(double), typeof(BayesianOutputControl), new UIPropertyMetadata(200d));

        /// <summary>
        /// Gets or sets the actual width of property labels in the control layout.
        /// </summary>
        public double ActualPropertyWidth
        {
            get { return (double)GetValue(ActualPropertyWidthProperty); }
            set { SetValue(ActualPropertyWidthProperty, value); }
        }

        /// <summary>
        /// Gets the collection of available credible interval options for Bayesian analysis.
        /// Contains predefined interval widths of 90%, 95%, 98%, and 99%.
        /// </summary>
        public ObservableCollection<CredibleIntervalItem> CredibleIntervalItems { get; private set; } = new ObservableCollection<CredibleIntervalItem>()
        {
            new CredibleIntervalItem("90%", 0.9),
            new CredibleIntervalItem("95%", 0.95),
            new CredibleIntervalItem("98%", 0.98),
            new CredibleIntervalItem("99%", 0.99)
        };

        /// <summary>
        /// Gets the collection of available point estimator methods for Bayesian analysis.
        /// Includes Posterior Mean and Posterior Mode (MAP) estimators.
        /// </summary>
        public ObservableCollection<PointEstimatorItem> PointEstimatorItems { get; private set; } = new ObservableCollection<PointEstimatorItem>()
        {
            new PointEstimatorItem("Posterior Mean", BayesianAnalysis.PointEstimateType.PosteriorMean, "Calculates the average of all posterior samples, providing a balanced summary of the parameter estimates."),
            new PointEstimatorItem("Posterior Mode", BayesianAnalysis.PointEstimateType.PosteriorMode, "Selects the most likely parameter values for the posterior distribution (the Maximum A Posteriori estimate)."),
        };

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the credible interval input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void CredibleInterval_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.CredibleIntervalWidth), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the output length input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void OutputLength_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.OutputLength), Analysis);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the point estimator input.
        /// Raises the <see cref="ShowPropertyAttributes"/> event to display property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void PointEstimator_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Analysis.PointEstimator), Analysis);
        }

        /// <summary>
        /// Handles the PropertyChanged event for the credible interval control.
        /// Updates the control's actual property width when the credible interval control width changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Property changed event arguments containing the property name that changed.</param>
        private void CredibleInterval_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CredibleInterval.ActualPropertyWidth))
            {
                ActualPropertyWidth = CredibleInterval.ActualPropertyWidth;
            }
        }
    }
}
