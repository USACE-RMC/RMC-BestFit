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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RMC_BestFit
{
    /// <summary>
    /// A user control that displays a bivariate heat map and contour plot showing the joint distribution
    /// of two parameters from a Bayesian analysis. Visualizes parameter correlations and dependencies.
    /// </summary>
    public partial class BivariateHeatMapControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BivariateHeatMapControl"/> class.
        /// </summary>
        public BivariateHeatMapControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Identifies the <see cref="Analysis"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(BayesianAnalysis), typeof(BivariateHeatMapControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis object that provides data for the bivariate heat map visualization.
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
            if (d as BivariateHeatMapControl == null) return;
            var thisControl = (BivariateHeatMapControl)d;

            // Unsubscribe from old element
            if (e.OldValue is BayesianAnalysis oldElement)
                oldElement.PropertyChanged -= thisControl.Analysis_PropertyChanged;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as BayesianAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Analysis_PropertyChanged;

            // Initialize combos and UI now that Analysis is available
            thisControl.LoadParameterComboBox();
            thisControl.UpdatePlot();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control should use simplified (non-Bayesian)
        /// terminology. When true, the plot title omits "Posterior" (e.g. "Joint Density of µ and s"
        /// instead of "Joint Posterior Density of µ and s"). Set to true for B17C bootstrap/GMM
        /// contexts where the parameter samples are not posterior draws.
        /// </summary>
        public bool SimpleView { get; set; } = false;

        /// <summary>
        /// Tracks whether the control has been loaded and initialized.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// The plot object. Injected by the parent control via <see cref="SetPlot"/>
        /// (the parent obtains it from <c>BayesianController.BivariateHeatMapPlot</c>).
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
        /// by populating the parameter combo boxes and updating the plot.
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
            if (e.PropertyName == nameof(Analysis.Model) || e.PropertyName == nameof(Analysis.ParameterNames))
            {
                LoadParameterComboBox();
            }
        }

        /// <summary>
        /// Populates both X and Y parameter combo boxes with the display names of all parameters
        /// from the analysis model. Sets the X parameter to the first parameter and the Y parameter
        /// to the second parameter by default.
        /// </summary>
        private void LoadParameterComboBox()
        {
            if (Analysis == null || Analysis.ParameterNames == null) return;
            var parms = Analysis.ParameterNames.ToList();
            XParameterComboBox.ItemsSource = null;
            XParameterComboBox.ItemsSource = parms;
            XParameterComboBox.SelectedIndex = 0;

            YParameterComboBox.ItemsSource = null;
            YParameterComboBox.ItemsSource = parms;
            YParameterComboBox.SelectedIndex = 1;
        }

        /// <summary>
        /// Handles the SelectionChanged event of the XParameterComboBox.
        /// Updates the plot to display the bivariate distribution for the newly selected X parameter.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void XParameterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded == true) UpdatePlot();
        }

        /// <summary>
        /// Handles the SelectionChanged event of the YParameterComboBox.
        /// Updates the plot to display the bivariate distribution for the newly selected Y parameter.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void YParameterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
        /// Axis title bindings are re-established for X and Y parameter combo boxes.
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
        /// Updates the bivariate heat map and contour plot with the current X and Y parameter data from the analysis results.
        /// Creates a 2D density grid by binning the parameter samples, then displays both a heat map and contour lines
        /// to visualize the joint distribution and correlation between the two selected parameters.
        /// </summary>
        private void UpdatePlot()
        {
            if (_plot == null) return;

            // Set plot title and axis titles to reflect the selected parameter pair. Suppress
            // PropertyChanged so the PlotUndoManager does not record these as undoable actions
            // — they track combo selections, not user intent.
            string xParamName = XParameterComboBox.SelectedValue as string ?? "";
            string yParamName = YParameterComboBox.SelectedValue as string ?? "";
            string densityLabel = SimpleView ? "Joint Density" : "Joint Posterior Density";
            string plotTitle = (string.IsNullOrEmpty(xParamName) || string.IsNullOrEmpty(yParamName))
                ? densityLabel
                : $"{densityLabel} of {xParamName} and {yParamName}";
            SetPlotTitle(plotTitle);
            foreach (var axis in _plot.Axes)
            {
                if (axis.Position == OxyPlot.Axes.AxisPosition.Bottom)
                {
                    axis.SuppressPropertyChanged = true;
                    axis.Title = xParamName;
                    axis.SuppressPropertyChanged = false;
                }
                else if (axis.Position == OxyPlot.Axes.AxisPosition.Left)
                {
                    axis.SuppressPropertyChanged = true;
                    axis.Title = yParamName;
                    axis.SuppressPropertyChanged = false;
                }
            }

            // Ensure color axis has correct gradient (may be lost during plot settings deserialization)
            foreach (var axis in _plot.Axes)
            {
                if (axis is LinearColorAxis colorAxis)
                    ApplyHeatMapGradient(colorAxis);
            }

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

                int Xindex = Math.Max(0, XParameterComboBox.SelectedIndex);
                int Yindex = Math.Max(0, YParameterComboBox.SelectedIndex);

                // Get histogram bins
                var histX = Analysis.Results.ParameterResults[Xindex].Histogram;
                var histY = Analysis.Results.ParameterResults[Yindex].Histogram;
                if (histX == null || histY == null)
                    return;

                int n = histX.NumberOfBins;
                double minX = histX[0].LowerBound;
                double maxX = histX[n - 1].UpperBound;
                double minY = histY[0].LowerBound;
                double maxY = histY[n - 1].UpperBound;

                // Create stratification bins
                int bins = 16;
                double dx = (maxX - minX) / (bins - 1);
                double dy = (maxY - minY) / (bins - 1);

                // E7: Guard against degenerate posteriors where all samples are identical
                // (dx == 0 or dy == 0). Division by zero would silently map all samples
                // to bin 0, producing NaN/Infinity in the data array.
                if (dx == 0 || dy == 0)
                {
                    _plot.InvalidatePlot(true);
                    return;
                }

                double[] xVals = new double[bins];
                double[] yVals = new double[bins];
                xVals[0] = minX;
                yVals[0] = minY;
                for (int i = 1; i < bins; i++)
                {
                    xVals[i] = xVals[i - 1] + dx;
                    yVals[i] = yVals[i - 1] + dy;
                }

                double[,] data = new double[bins, bins];
                double minZ = double.MaxValue;
                double maxZ = double.MinValue;

                // Ensure output values are available and match expected length
                //if (Analysis.OutputLength != Analysis.Results.Output[0].Values.Length)
                //    return;

                for (int i = 0; i < Analysis.OutputLength; i++)
                {
                    var x = Analysis.Results.Output[i].Values != null ? Analysis.Results.Output[i].Values[Xindex] : 0;
                    var y = Analysis.Results.Output[i].Values != null ? Analysis.Results.Output[i].Values[Yindex] : 0;
                    var xId = Math.Max(0, Math.Min(bins - 1, (int)Math.Floor((x - minX) / dx)));
                    var yID = Math.Max(0, Math.Min(bins - 1, (int)Math.Floor((y - minY) / dy)));
                    data[xId, yID] += 1d / Analysis.OutputLength;
                    minZ = Math.Min(minZ, data[xId, yID]);
                    maxZ = Math.Max(maxZ, data[xId, yID]);
                }

                int zBins = 6;
                double dz = (maxZ - minZ) / (zBins - 1);
                double[] zVals = new double[zBins];
                zVals[0] = Math.Round(minZ + dz, 5);
                for (int i = 1; i < zBins; i++)
                    zVals[i] = Math.Round(zVals[i - 1] + dz, 5);

                // Exception: series count varies per parameter pair selection — runtime-variable
                // inline creation is intentional per CLAUDE.md "Series Preservation exceptions".
                _plot.Series.Add(new HeatMapSeries()
                {
                    Title = "Bivariate HeatMap",
                    RenderInLegend = false,
                    Data = data,
                    X0 = minX,
                    X1 = maxX,
                    Y0 = minY,
                    Y1 = maxY,
                    CoordinateDefinition = OxyPlot.Series.HeatMapCoordinateDefinition.Center,
                    Interpolate = true,
                    TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000}" + Environment.NewLine + "{3}: {4:0.000000}"
                });

                _plot.Series.Add(new ContourSeries()
                {
                    Title = "Bivariate Contour",
                    RenderInLegend = false,
                    Data = data,
                    RowCoordinates = yVals.ToArray(),
                    ColumnCoordinates = xVals.ToArray(),
                    ContourLevels = zVals,
                    ContourColors = new Color[] { Colors.Black },
                    TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000}" + Environment.NewLine + "{3}: {4:0.000000}"
                });

                _plot.InvalidatePlot(true);

                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        /// <summary>
        /// Applies the standard blue-to-red diverging gradient to a <see cref="LinearColorAxis"/>
        /// for the bivariate heat map visualization.
        /// </summary>
        /// <param name="colorAxis">The color axis to configure.</param>
        private static void ApplyHeatMapGradient(LinearColorAxis colorAxis)
        {
            colorAxis.PaletteSize = 1000;
            colorAxis.HighColor = Colors.Transparent;
            colorAxis.LowColor = Colors.Transparent;
            var gradientStops = new GradientStopCollection();
            gradientStops.Add(new GradientStop() { Color = Color.FromRgb(215, 25, 28), Offset = 1.0 });
            gradientStops.Add(new GradientStop() { Color = Color.FromRgb(253, 174, 97), Offset = 0.75 });
            gradientStops.Add(new GradientStop() { Color = Color.FromRgb(255, 255, 191), Offset = 0.5 });
            gradientStops.Add(new GradientStop() { Color = Color.FromRgb(171, 217, 233), Offset = 0.25 });
            gradientStops.Add(new GradientStop() { Color = Color.FromRgb(44, 123, 182), Offset = 0.0 });
            colorAxis.GradientStops = gradientStops;
        }
    }
}
