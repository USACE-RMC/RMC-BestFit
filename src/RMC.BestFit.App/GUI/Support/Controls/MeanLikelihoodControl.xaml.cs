using OxyPlot;
using OxyPlot.Wpf;
using OxyPlotControls;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
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
    /// User control for displaying and visualizing the mean log-likelihood values across iterations in a Bayesian analysis.
    /// This control provides a time-series plot showing how the mean log-likelihood evolves during the MCMC sampling process.
    /// </summary>
    public partial class MeanLikelihoodControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MeanLikelihoodControl"/> class.
        /// </summary>
        public MeanLikelihoodControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Dependency property for the <see cref="Analysis"/> property.
        /// </summary>
        public static readonly DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(BayesianAnalysis), typeof(MeanLikelihoodControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis instance containing the mean log-likelihood data to be visualized.
        /// </summary>
        public BayesianAnalysis Analysis
        {
            get { return (BayesianAnalysis)GetValue(AnalysisProperty); }
            set { SetValue(AnalysisProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Analysis dependency property changes.
        /// Subscribes to property changed events on the new analysis instance.
        /// </summary>
        /// <param name="d">The dependency object that owns the property.</param>
        /// <param name="e">Event data containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as MeanLikelihoodControl == null) return;
            var thisControl = (MeanLikelihoodControl)d;

            // Unsubscribe from old element
            if (e.OldValue is BayesianAnalysis old)
                old.PropertyChanged -= thisControl.Analysis_PropertyChanged;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as BayesianAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Analysis_PropertyChanged;

        }

        /// <summary>
        /// Flag indicating whether the control has been loaded.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// The plot object. Injected by the parent control via <see cref="SetPlot"/>
        /// (the parent obtains it from <c>BayesianController.MeanLikelihoodPlot</c>).
        /// </summary>
        private Plot _plot;

        /// <summary>
        /// Gets the plot displayed by this control. Set via <see cref="SetPlot"/> by the parent control.
        /// </summary>
        public Plot Plot => _plot;

        /// <summary>
        /// Gets a value indicating whether the plot area was clicked by the user.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Raised when the user clicks on the control, providing information about which part was clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for the <see cref="PreviewControlClicked"/> event.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot area was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar area was clicked.</param>
        /// <param name="plot">The plot control that was clicked.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Raised when the plot properties dialog is requested.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Delegate for the <see cref="PlotPropertiesCalled"/> event.
        /// </summary>
        /// <param name="plot">The plot control for which properties are requested.</param>
        /// <param name="openProperties">Indicates whether the properties dialog should be opened.</param>
        /// <param name="propertyExpander">The property expander control to use.</param>
        /// <param name="selectedObject">The currently selected object in the properties dialog.</param>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);


        /// <summary>
        /// Event handler for when the user control is loaded. Initializes the plot with available data.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == false)
            {
                // One-time setup (none needed for this control beyond UpdatePlot).
            }
            _isLoaded = true;
            // Always refresh on load so Analysis changes between unload and reload are applied.
            UpdatePlot();
        }

        /// <summary>
        /// Event handler for property changes in the Analysis object. Updates the plot when the estimation status changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing the property name that changed.</param>
        private void Analysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Analysis.IsEstimated))
            {
                UpdatePlot();
            }
        }

        /// <summary>
        /// Event handler for mouse down events on the control. Determines if the plot or toolbar was clicked and raises the PreviewControlClicked event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing mouse button information.</param>
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_plot == null) return;
            var plotHitResult = VisualTreeHelper.HitTest(_plot, e.GetPosition(_plot));
            var toolbarHitResult = VisualTreeHelper.HitTest(PlotToolbar, e.GetPosition(PlotToolbar));
            PreviewControlClicked?.Invoke(plotHitResult != null, toolbarHitResult != null, _plot);
            PlotClicked = plotHitResult != null;
        }

        /// <summary>
        /// Event handler for when the control loses focus. Resets the PlotClicked flag.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Event handler for when plot properties are requested from the toolbar. Raises the PlotPropertiesCalled event.
        /// </summary>
        /// <param name="targetPlot">The plot for which properties are requested.</param>
        /// <param name="openProperties">Indicates whether to open the properties dialog.</param>
        /// <param name="propertyExpander">The property expander control to use.</param>
        /// <param name="selectedObject">The currently selected object.</param>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        /// <summary>
        /// Sets the external plot to display in this control. Called by parent controls
        /// that own the Plot object (Pattern B). The plot is attached to PlotHost and PlotToolbar.
        /// </summary>
        /// <param name="plot">The external plot to display.</param>
        public void SetPlot(Plot plot)
        {
            _plot = plot;
            PlotHost.Content = _plot;
            PlotToolbar.Plot = _plot;
        }

        /// <summary>
        /// Updates the plot with the latest mean log-likelihood data from the analysis results.
        /// Displays the evolution of mean log-likelihood across all iterations.
        /// </summary>
        private void UpdatePlot()
        {
            if (_plot == null) return;

            // Lookup-or-create named series so user-customized styling survives Save/Open.
            var lineSeries = _plot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "MeanLogLikelihood")
                ?? new LineSeries
                {
                    Name = "MeanLogLikelihood",
                    Title = "Mean Log-Likelihood",
                    LineStyle = LineStyle.Solid,
                    Color = Colors.Crimson,
                    StrokeThickness = 2,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };

            // BC2: Clear series in a single block without an intermediate InvalidatePlot
            // so the plot never renders in a transient blank state between clear and repopulate.
            _plot.Series.Clear();

            if (Analysis == null || Analysis.IsEstimated == false || Analysis.Results == null)
            {
                _plot.InvalidatePlot(true);
                return;
            }

            if (Analysis.IsEstimated == true)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {

                var points = new List<DataPoint>();
                for (int i = 0; i < Analysis.Results.MeanLogLikelihood.Count; i++)
                    points.Add(new DataPoint(i + 1, Analysis.Results.MeanLogLikelihood[i]));

                lineSeries.ItemsSource = points;
                lineSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:0.000000}";
                _plot.Series.Add(lineSeries);
                _plot.InvalidatePlot(true);

                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

    }
}
