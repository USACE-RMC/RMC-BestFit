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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RMC_BestFit
{
    /// <summary>
    /// A user control that displays a histogram visualization of posterior parameter distributions
    /// from a Bayesian analysis, with optional prior distribution overlay.
    /// </summary>
    public partial class HistogramControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HistogramControl"/> class.
        /// </summary>
        public HistogramControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Identifies the <see cref="Analysis"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(BayesianAnalysis), typeof(HistogramControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis object that provides data for the histogram visualization.
        /// </summary>
        public BayesianAnalysis Analysis
        {
            get { return (BayesianAnalysis)GetValue(AnalysisProperty); }
            set { SetValue(AnalysisProperty, value); }
        }

        /// <summary>
        /// Handles property change callbacks for the <see cref="Analysis"/> dependency property.
        /// Subscribes to property change events on the new analysis object.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event data containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as HistogramControl == null) return;
            var thisControl = (HistogramControl)d;

            // Unsubscribe from old element
            if (e.OldValue is BayesianAnalysis oldElement)
                oldElement.PropertyChanged -= thisControl.Analysis_PropertyChanged;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as BayesianAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Analysis_PropertyChanged;

            // Initialize combo and UI now that Analysis is available
            thisControl.LoadParameterComboBox();
            thisControl.LoadPercentileHeaders();
            thisControl.UpdatePlot();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control should display in simplified view mode.
        /// When true, hides advanced diagnostic columns like Gelman-Rubin and ESS statistics.
        /// </summary>
        public bool SimpleView { get; set; } = false;

        /// <summary>
        /// Tracks whether the control has been loaded and initialized.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// The plot object. Injected by the parent control via <see cref="SetPlot"/>
        /// (the parent obtains it from <c>BayesianController.HistogramPlot</c>).
        /// </summary>
        private Plot _plot;

        /// <summary>
        /// Gets the plot displayed by this control. Set via <see cref="SetPlot"/> by the parent control.
        /// </summary>
        public Plot Plot => _plot;

        /// <summary>
        /// Gets a value indicating whether the plot area has been clicked by the user.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Occurs when the control is clicked, providing information about what area was clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Represents the method that will handle the <see cref="PreviewControlClicked"/> event.
        /// </summary>
        /// <param name="plotClicked">True if the plot area was clicked.</param>
        /// <param name="toolbarClicked">True if the toolbar area was clicked.</param>
        /// <param name="plot">The plot object that was clicked.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Occurs when plot properties are requested to be displayed or modified.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Represents the method that will handle the <see cref="PlotPropertiesCalled"/> event.
        /// </summary>
        /// <param name="plot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">True if the properties panel should be opened.</param>
        /// <param name="propertyExpander">The property expander control to display.</param>
        /// <param name="selectedObject">The object whose properties are being displayed.</param>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);




        /// <summary>
        /// Handles the Loaded event of the UserControl. Initializes the control on first load
        /// by populating the parameter combo box, setting up percentile headers, and updating the plot.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == false)
            {
                // One-time setup: visibility config is set once on first load.
                LoadParameterComboBox();
                LoadPercentileHeaders();
                if (SimpleView)
                {
                    ShowPriorDistribution.Visibility = Visibility.Collapsed;
                    GelmanRubinColumn.Visibility = Visibility.Collapsed;
                    ESSColumn.Visibility = Visibility.Collapsed;
                }
            }
            _isLoaded = true;
            // Always refresh on load so Analysis changes between unload and reload are applied.
            UpdatePlot();
        }

        /// <summary>
        /// Handles property changes on the Analysis object. Updates the plot and UI elements
        /// when relevant analysis properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing the name of the property that changed.</param>
        private void Analysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Analysis.IsEstimated))
            {
                UpdatePlot();
            }
            if (e.PropertyName == nameof(Analysis.CredibleIntervalWidth))
            {
                // Relabel column headers (90% LowerCI -> 95% LowerCI) AND rebind the data
                // table so the LowerCI/UpperCI cell values reflect the new alpha. Headers
                // alone aren't enough — the cached ItemsSource holds stale percentiles.
                LoadPercentileHeaders();
                UpdatePlot();
            }
            if (e.PropertyName == nameof(Analysis.Model) ||
                e.PropertyName == nameof(Analysis.ParameterNames) ||
                e.PropertyName == nameof(Analysis.Results))
            {
                LoadParameterComboBox();
            }
        }

        /// <summary>
        /// Populates the parameter combo box with the display names of all parameters
        /// from the analysis model.
        /// </summary>
        private void LoadParameterComboBox()
        {
            if (Analysis == null || Analysis.ParameterNames == null) return;
            var selectedParameter = ParameterComboBox.SelectedValue as string;
            var parms = GetSampledParameterNames();
            ParameterComboBox.ItemsSource = null;
            ParameterComboBox.ItemsSource = parms;
            int selectedIndex = selectedParameter == null ? -1 : parms.IndexOf(selectedParameter);
            ParameterComboBox.SelectedIndex = selectedIndex >= 0
                ? selectedIndex
                : parms.Count > 0 ? 0 : -1;
        }

        /// <summary>
        /// Gets names aligned with the coordinates stored in the MCMC results.
        /// </summary>
        /// <returns>Sampled parameter names; the derived final mixture weight is omitted for new K-1 results.</returns>
        private List<string> GetSampledParameterNames()
        {
            var names = Analysis.ParameterNames!.ToList();
            if (Analysis.Model is MixtureModel mixtureModel &&
                mixtureModel.Mixture is not null &&
                mixtureModel.Mixture.Distributions.Length > 1 &&
                Analysis.Results?.ParameterResults?.Length == names.Count - 1)
            {
                names.RemoveAt(mixtureModel.Mixture.Distributions.Length - 1);
            }
            return names;
        }

        /// <summary>
        /// Maps a stored diagnostic index to its public model-parameter index.
        /// </summary>
        /// <param name="storedIndex">The stored diagnostic index.</param>
        /// <returns>The corresponding public model-parameter index.</returns>
        private int GetModelParameterIndex(int storedIndex)
        {
            if (Analysis.Model is MixtureModel mixtureModel &&
                mixtureModel.Mixture is not null &&
                mixtureModel.Mixture.Distributions.Length > 1 &&
                Analysis.Results?.ParameterResults?.Length == Analysis.Model.Parameters.Count - 1)
            {
                int derivedWeightIndex = mixtureModel.Mixture.Distributions.Length - 1;
                return storedIndex < derivedWeightIndex ? storedIndex : storedIndex + 1;
            }
            return storedIndex;
        }

        /// <summary>
        /// Updates the percentile column headers in the data grid based on the current
        /// credible interval width from the analysis.
        /// </summary>
        private void LoadPercentileHeaders()
        {
            if (Analysis == null) return;
            var alpha = (1 - Analysis.CredibleIntervalWidth) / 2d;
            Percentile1Column.Header = (alpha * 100).ToString("F1") + "%";
            Percentile3Column.Header = ((1 - alpha) * 100).ToString("F1") + "%";
        }

        /// <summary>
        /// Handles the Checked event of the ShowPriorDistribution checkbox.
        /// Updates the plot to display the prior distribution overlay.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void ShowPriorDistribution_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == true) UpdatePlot();
        }

        /// <summary>
        /// Handles the Unchecked event of the ShowPriorDistribution checkbox.
        /// Updates the plot to remove the prior distribution overlay.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void ShowPriorDistribution_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == true) UpdatePlot();
        }

        /// <summary>
        /// Handles the SelectionChanged event of the ParameterComboBox.
        /// Updates the plot to display the histogram for the newly selected parameter.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void ParameterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded == true) UpdatePlot();
        }

        /// <summary>
        /// Handles the PreviewMouseDown event of the UserControl. Performs hit testing
        /// to determine if the plot or toolbar was clicked and raises the appropriate event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_plot == null) return;
            var plotHitResult = VisualTreeHelper.HitTest(_plot, e.GetPosition(_plot));
            var toolbarHitResult = VisualTreeHelper.HitTest(PlotToolbar, e.GetPosition(PlotToolbar));
            PreviewControlClicked?.Invoke(plotHitResult != null, toolbarHitResult != null, _plot);
            PlotClicked = plotHitResult != null;
        }

        /// <summary>
        /// Handles the LostFocus event of the UserControl. Resets the PlotClicked state.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the PropertiesCalled event from the plot toolbar.
        /// Forwards the event to subscribers of the PlotPropertiesCalled event.
        /// </summary>
        /// <param name="targetPlot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">True if the properties panel should be opened.</param>
        /// <param name="propertyExpander">The property expander control to display.</param>
        /// <param name="selectedObject">The object whose properties are being displayed.</param>
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
        /// Sets the plot title. Suppresses PropertyChanged so the change is not recorded
        /// as an undoable action — the title tracks combo selection, not user intent.
        /// </summary>
        private void SetPlotTitle(string title)
        {
            if (_plot == null) return;
            _plot.SuppressPropertyChanged = true;
            _plot.Title = title;
            _plot.SuppressPropertyChanged = false;
        }

        /// <summary>
        /// Updates the histogram plot with the current parameter data from the analysis results.
        /// Displays the posterior distribution as a histogram and optionally overlays the prior distribution.
        /// Also updates the summary statistics table.
        /// </summary>
        private void UpdatePlot()
        {
            if (_plot == null) return;

            // Set plot title and axis title to reflect the selected parameter. Suppress
            // PropertyChanged so the PlotUndoManager does not record these as undoable
            // actions — they track combo selection, not user intent.
            string paramName = ParameterComboBox.SelectedValue as string ?? "";
            string histLabel = SimpleView ? "Marginal Histogram" : "Marginal Posterior Histogram";
            SetPlotTitle(string.IsNullOrEmpty(paramName) ? histLabel : $"{histLabel} of {paramName}");
            foreach (var axis in _plot.Axes)
            {
                if (axis.Position == OxyPlot.Axes.AxisPosition.Bottom)
                {
                    axis.SuppressPropertyChanged = true;
                    axis.Title = paramName;
                    axis.SuppressPropertyChanged = false;
                }
            }

            // Lookup-or-create named series so user-customized styling survives Save/Open.
            var priorSeries = _plot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "PriorDensity")
                ?? new AreaSeries
                {
                    Name = "PriorDensity",
                    Title = "Prior Density",
                    Fill = Color.FromArgb(125, 104, 140, 175),
                    Color = Color.FromArgb(255, 53, 59, 122),
                    LineStyle = LineStyle.Solid,
                    BrokenLineThickness = 1,
                    StrokeThickness = 1,
                    Decimator = OxyPlot.Decimator.Decimate,
                };
            var posteriorSeries = _plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "PosteriorDensity")
                ?? new HistogramSeries
                {
                    Name = "PosteriorDensity",
                    Title = "Posterior Histogram",
                    FillColor = Color.FromArgb(75, 220, 20, 60),
                    StrokeColor = Color.FromArgb(255, 255, 0, 0),
                    StrokeThickness = 1,
                };

            // BC3: Clear series in a single block without an intermediate InvalidatePlot
            // so the plot never renders in a transient blank state between clear and repopulate.
            _plot.Series.Clear();

            KDEDataGrid.ItemsSource = null;
            KDEDataGrid.ItemsSource = new List<Numerics.Sampling.MCMC.ParameterStatistics>() { new Numerics.Sampling.MCMC.ParameterStatistics() { Rhat = double.NaN, ESS = double.NaN, Mean = double.NaN, StandardDeviation = double.NaN, LowerCI = double.NaN, Median = double.NaN, UpperCI = double.NaN } };
            KDEDataGrid.Items.Refresh();

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

                int index = ParameterComboBox.SelectedIndex;
                if (Analysis.Results.ParameterResults == null ||
                    index < 0 ||
                    index >= Analysis.Results.ParameterResults.Length)
                {
                    _plot.InvalidatePlot(true);
                    return;
                }

                if (ShowPriorDistribution.IsChecked == true && Analysis.Model != null)
                {
                    int modelParameterIndex = GetModelParameterIndex(index);
                    var pdf = Analysis.Model.Parameters[modelParameterIndex].PriorDistribution.CreatePDFGraph();
                    priorSeries.Title = Analysis.Model is MixtureModel mixtureModel &&
                        mixtureModel.Mixture is not null &&
                        modelParameterIndex < mixtureModel.Mixture.Distributions.Length - 1
                            ? "Configured Prior Factor"
                            : "Prior Density";
                    var priorPoints = new List<Point3D>();
                    for (int i = 0; i < pdf.GetLength(0); i++)
                        priorPoints.Add(new Point3D(pdf[i, 0], 0, pdf[i, 1]));

                    priorSeries.ItemsSource = priorPoints;
                    priorSeries.DataFieldX = nameof(Point3D.X);
                    priorSeries.DataFieldY = nameof(Point3D.Y);
                    priorSeries.DataFieldX2 = nameof(Point3D.X);
                    priorSeries.DataFieldY2 = nameof(Point3D.Z);
                    priorSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.0000}" + Environment.NewLine + "{3}: {4:0.000000}";
                    _plot.Series.Add(priorSeries);
                }

                var histogram = Analysis.Results.ParameterResults[index].Histogram;
                if (histogram == null || histogram.NumberOfBins == 0)
                    return;

                double sum = 0;
                for (int i = 0; i < histogram.NumberOfBins; i++)
                    sum += histogram[i].Frequency * histogram.BinWidth;

                var histogramItems = new List<OxyPlot.Series.HistogramItem>();
                for (int i = 0; i < histogram.NumberOfBins; i++)
                    histogramItems.Add(new OxyPlot.Series.HistogramItem(histogram[i].LowerBound, histogram[i].UpperBound, histogram[i].Frequency / sum * histogram.BinWidth));

                posteriorSeries.ItemsSource = histogramItems;
                posteriorSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.0000}" + Environment.NewLine + "{3}: {4:0.000000}";
                _plot.Series.Add(posteriorSeries);
                _plot.InvalidatePlot(true);

                // Update stats table
                KDEDataGrid.ItemsSource = new List<Numerics.Sampling.MCMC.ParameterStatistics>() { Analysis.Results.ParameterResults[index].SummaryStatistics };
                KDEDataGrid.Items.Refresh();

                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }
    }
}
