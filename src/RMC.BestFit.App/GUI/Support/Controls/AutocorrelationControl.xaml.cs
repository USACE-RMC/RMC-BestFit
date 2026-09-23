using Numerics;
using Numerics.Data.Statistics;
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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RMC_BestFit
{
    /// <summary>
    /// A user control that displays an autocorrelation function (ACF) plot for parameter samples
    /// from a Bayesian analysis, showing the correlation between samples at different lags.
    /// </summary>
    public partial class AutocorrelationControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutocorrelationControl"/> class.
        /// </summary>
        public AutocorrelationControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Identifies the <see cref="Analysis"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(BayesianAnalysis), typeof(AutocorrelationControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis object that provides data for the autocorrelation visualization.
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
            if (d as AutocorrelationControl == null) return;
            var thisControl = (AutocorrelationControl)d;

            // Unsubscribe from old element
            if (e.OldValue is BayesianAnalysis oldElement)
                oldElement.PropertyChanged -= thisControl.Analysis_PropertyChanged;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as BayesianAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Analysis_PropertyChanged;

            // Initialize combo and UI now that Analysis is available
            thisControl.LoadParameterComboBox();
            thisControl.UpdatePlot();
        }

        /// <summary>
        /// Tracks whether the control has been loaded and initialized.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// The plot object. Injected by the parent control via <see cref="SetPlot"/>
        /// (the parent obtains it from <c>BayesianController.AutocorrelationPlot</c>).
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
        /// by populating the parameter combo box and updating the plot.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == false)
            {
                // One-time setup: populate combo on first load.
                LoadParameterComboBox();
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
                // The autocorrelation confidence-interval lines are computed from
                // CredibleIntervalWidth at plot time. Refresh the plot when alpha changes.
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
        /// Handles the SelectionChanged event of the ParameterComboBox.
        /// Updates the plot to display the autocorrelation for the newly selected parameter.
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
        /// Updates the autocorrelation plot with the current parameter data from the analysis results.
        /// Displays the autocorrelation function values as bars and overlays confidence interval lines.
        /// Adjusts the Y-axis minimum to accommodate negative correlation values.
        /// </summary>
        private void UpdatePlot()
        {
            if (_plot == null) return;

            // Set plot title to reflect the selected parameter. Suppress PropertyChanged so
            // the PlotUndoManager does not record this as an undoable action — the title
            // tracks combo selection, not user intent.
            string paramName = ParameterComboBox.SelectedValue as string ?? "";
            SetPlotTitle(string.IsNullOrEmpty(paramName) ? "Autocorrelation" : $"Autocorrelation of {paramName}");

            // Lookup-or-create named series/annotations so user-customized styling survives Save/Open.
            var acfSeries = _plot.Series.OfType<HistogramSeries>().FirstOrDefault(s => s.Name == "Autocorrelation")
                ?? new HistogramSeries
                {
                    Name = "Autocorrelation",
                    Title = "Autocorrelation",
                    FillColor = Color.FromArgb(75, 220, 20, 60),
                    StrokeColor = Color.FromArgb(255, 255, 0, 0),
                    StrokeThickness = 1,
                };
            var lowerCI = _plot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "LowerCI")
                ?? new LineAnnotation
                {
                    Name = "LowerCI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Top,
                };
            var upperCI = _plot.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Name == "UpperCI")
                ?? new LineAnnotation
                {
                    Name = "UpperCI",
                    Type = OxyPlot.Annotations.LineAnnotationType.Horizontal,
                    Color = Colors.Black,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    TextLinePosition = 1,
                    TextHorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextVerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                };

            // BC2: Clear series/annotations in a single block without an intermediate InvalidatePlot
            // so the plot never renders in a transient blank state between clear and repopulate.
            _plot.Series.Clear();
            _plot.Annotations.Clear();

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
                int retainedDrawCount = Analysis.Results.Output.Count;
                if (Analysis.Results.ParameterResults == null ||
                    index < 0 ||
                    index >= Analysis.Results.ParameterResults.Length ||
                    retainedDrawCount == 0)
                {
                    _plot.InvalidatePlot(true);
                    return;
                }

                var acfItems = new List<OxyPlot.Series.HistogramItem>();
                var acf = Analysis.Results.ParameterResults[index].Autocorrelation;
                for (int i = 0; i < acf.GetLength(0); i++)
                {
                    acfItems.Add(new OxyPlot.Series.HistogramItem(i, i + 1, acf[i, 1]));
                }

                acfSeries.ItemsSource = acfItems;
                acfSeries.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0}" + Environment.NewLine + "{3}: {4:0.000000}";
                _plot.Series.Add(acfSeries);

                var ci = Autocorrelation.CorrelationConfidenceInterval(retainedDrawCount, Analysis.CredibleIntervalWidth);
                var alpha = (1 - Analysis.CredibleIntervalWidth) / 2d;

                lowerCI.Text = (alpha * 100).ToString("F1") + "% CI";
                lowerCI.Y = ci[0];

                upperCI.Text = ((1 - alpha) * 100).ToString("F1") + "% CI";
                upperCI.Y = ci[1];

                _plot.Annotations.Add(lowerCI);
                _plot.Annotations.Add(upperCI);

                foreach (var axis in _plot.Axes)
                {
                    if (axis.Key == "Yaxis")
                    {
                        axis.Minimum = Math.Round((Math.Min(ci[0], Tools.Min(acf.GetColumn(1))) - 0.05) * 20) / 20;
                    }
                }

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
