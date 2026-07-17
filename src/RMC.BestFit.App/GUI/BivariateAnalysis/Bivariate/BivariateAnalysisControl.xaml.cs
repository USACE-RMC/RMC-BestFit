using System;
using System.Collections.Generic;
using System.Diagnostics;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlotControls;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using FrameworkInterfaces;
using System.Threading.Tasks;
using System.Windows.Threading;
using Numerics.Sampling;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and interacting with bivariate analysis results.
    /// Provides visualization of copula fits, frequency curves, Bayesian diagnostics,
    /// and summary statistics for bivariate distribution analysis.
    /// </summary>
    public partial class BivariateAnalysisControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BivariateAnalysisControl"/> class.
        /// </summary>
        public BivariateAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
            // DataGrid Binding.StringFormat must be set before first render.
            SetColumnStringFormats();
        }

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(BivariateAnalysis), typeof(BivariateAnalysisControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the bivariate analysis element associated with this control.
        /// </summary>
        public BivariateAnalysis Element
        {
            get { return (BivariateAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback invoked when the Element dependency property changes.
        /// Manages event handler subscriptions for the old and new element instances.
        /// </summary>
        /// <param name="d">The dependency object on which the property changed.</param>
        /// <param name="e">Event arguments containing old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as BivariateAnalysisControl == null) return;
            var thisControl = (BivariateAnalysisControl)d;

            // Remove handlers
            if (e.OldValue != null)
            {
                BivariateAnalysis oldElement = e.OldValue as BivariateAnalysis;
                if (oldElement != null)
                {
                    oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                    thisControl.CopulaPlotHost.Content = null;
                    thisControl.CopulaPlotToolbar.Plot = null;
                    // NOTE: PropertiesCalled is wired in XAML — no programmatic -= needed.
                }
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as BivariateAnalysis;
            if (newElement == null) return;

            // Reset _isLoaded so the next Loaded event refreshes bound content
            thisControl._isLoaded = false;

            // Subscribe Element-scoped handler
            newElement.PropertyChanged += thisControl.Element_PropertyChanged;

            // Attach plot, wire toolbar, and wire Bayesian sub-control plots inside a
            // bridge-suspension block. PropertiesCalled / PlotPropertiesCalled are wired
            // in XAML — no programmatic += needed.
            using (newElement.SuspendPlotBridges())
            {
                thisControl.CopulaPlotHost.Content = newElement.CopulaPlot;
                thisControl.CopulaPlotToolbar.Plot = newElement.CopulaPlot;

                thisControl.HistogramControl.SetPlot(newElement.BayesianPlots.HistogramPlot);
                thisControl.KernelDensityControl.SetPlot(newElement.BayesianPlots.KernelDensityPlot);
                thisControl.AutocorrelationControl.SetPlot(newElement.BayesianPlots.AutocorrelationPlot);
                thisControl.MarkovChainTraceControl.SetPlot(newElement.BayesianPlots.MarkovChainTracePlot);
                thisControl.MeanLikelihoodControl.SetPlot(newElement.BayesianPlots.MeanLikelihoodPlot);
                thisControl.InfluenceDiagnosticsControl.SetPlot(newElement.BayesianPlots.InfluenceDiagnosticsPlot);

                thisControl.BindAxisTitles();
            }
        }

        /// <summary>
        /// Indicates whether the control has completed its initial load operations.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// Indicates whether the copula plot needs updating before display.
        /// Set to true whenever the data feeding the copula plot changes; reset after rendering.
        /// </summary>
        private bool _copulaPlotDirty = true;

        /// <summary>
        /// Convenience accessor for the Element-owned copula plot.
        /// </summary>
        private Plot CopulaPlot => Element?.CopulaPlot;

        /// <summary>
        /// Gets a value indicating whether a plot area was clicked during the last mouse event.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Occurs when plot properties are requested to be displayed or modified.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Delegate for the <see cref="PlotPropertiesCalled"/> event.
        /// </summary>
        /// <param name="plot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">Indicates whether to open the properties dialog.</param>
        /// <param name="propertyExpander">The property expander control.</param>
        /// <param name="selectedObject">The currently selected object in the properties.</param>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);

        /// <summary>
        /// Occurs when the user clicks within the preview control area.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for the <see cref="PreviewControlClicked"/> event.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot area was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar area was clicked.</param>
        /// <param name="plot">The plot associated with the click event.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        #region Plot Series

        /// <summary>
        /// POCO bound to the bivariate observed-scatter series.
        /// </summary>
        /// <remarks>
        /// OxyPlot tracker format strings resolve public item properties such as
        /// <c>{Index}</c>, so this type carries the matched input-data index with
        /// each plotted X-Y observation.
        /// </remarks>
        internal sealed class CopulaPlotObservation
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CopulaPlotObservation"/> class.
            /// </summary>
            /// <param name="index">The shared exact-data index for the matched X-Y pair.</param>
            /// <param name="x">The plotted X-coordinate.</param>
            /// <param name="y">The plotted Y-coordinate.</param>
            /// <remarks>
            /// The coordinates are either raw values or plotting-position complements,
            /// depending on the current copula plot axis mode.
            /// </remarks>
            internal CopulaPlotObservation(int index, double x, double y)
            {
                Index = index;
                X = x;
                Y = y;
            }

            /// <summary>
            /// Gets the shared input-data index for this matched X-Y pair.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Gets the plotted X-coordinate.
            /// </summary>
            public double X { get; }

            /// <summary>
            /// Gets the plotted Y-coordinate.
            /// </summary>
            public double Y { get; }
        }

        #endregion



        /// <summary>
        /// Handles the Loaded event of the user control. Initializes plots, data grids, and UI elements
        /// on the first load.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            // Data refresh on every load. Plot host, toolbar, and Element.PropertyChanged
            // are wired once per Element in ElementCallback.
            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                BindAxisTitles();

                // Tab-lazy rendering: only render when the copula tab is currently visible
                // AND data has changed since the last render. Mirror FittingAnalysisControl.
                if (_copulaPlotDirty && CopulaResultsTabItem.IsSelected == true)
                {
                    UpdateCopulaPlotWithWaitCursor();
                }

                BindFrequencyCurveDataGrid();

                // One-time setup: column headers and summary grid layout do not change
                // on every transient visual-tree cycle — only re-run on the first load
                // after a new Element is assigned (BV3).
                if (!_isLoaded)
                {
                    SetFrequencyCurveTableColumnHeaders();
                    BindSummaryStatisticsDataGrid();
                }
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
            _isLoaded = true;
        }

        /// <summary>
        /// Handles the Unloaded event of the user control. Unsubscribes from element event handlers
        /// to prevent memory leaks and stale event subscriptions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Pattern B (canonical): Unloaded only flips _isLoaded back off so the next
            // Loaded runs a data refresh. Element-scoped lifecycle (PropertyChanged,
            // plot hosts, toolbars) is owned by ElementCallback.
            _isLoaded = false;
        }

        /// <summary>
        /// Handles property changes on the Element object and updates the UI accordingly.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Marshal to UI thread if called from a background thread (e.g. during MCMC).
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Element_PropertyChanged(sender, e)));
                return;
            }

            if (e.PropertyName == nameof(Element.MarginalX) || e.PropertyName == nameof(Element.MarginalY))
            {
                BindAxisTitles();
                _copulaPlotDirty = true;
            }
            if (e.PropertyName == nameof(Element.AnalysisResults))
            {
                _copulaPlotDirty = true;
                if (CopulaResultsTabItem.IsSelected == true)
                {
                    UpdateCopulaPlot();
                    _copulaPlotDirty = false;
                }
                BindFrequencyCurveDataGrid();
                BindSummaryStatisticsDataGrid();
                ResetWaitCursor();
            }
            if (e.PropertyName == nameof(Element.IsEstimated))
            {
                _copulaPlotDirty = true;
                if (CopulaResultsTabItem.IsSelected == true)
                {
                    UpdateCopulaPlot();
                    _copulaPlotDirty = false;
                }
                BindFrequencyCurveDataGrid();
                BindSummaryStatisticsDataGrid();
            }
            if (e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth))
            {
                SetFrequencyCurveTableColumnHeaders();
            }
            if ((e.PropertyName == nameof(Element.BayesianAnalysis.PointEstimator)
                 || e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth)
                 || e.PropertyName == nameof(Element.XYOrdinates))
                && Element.IsEstimated)
            {
                // Cursor wait while the model layer's ReprocessIfEstimated runs the recompute
                // on TaskScheduler.Default. Reset is wired to the AnalysisResults notification above.
                Mouse.OverrideCursor = Cursors.Wait;
            }

        }

        /// <summary>
        /// Clears <see cref="Mouse.OverrideCursor"/> when it is currently set to <see cref="Cursors.Wait"/>.
        /// Marshals onto the UI thread when called from a background-thread PropertyChanged
        /// notification (the model layer's <c>ReprocessIfEstimated</c> path uses
        /// <c>TaskScheduler.Default</c>).
        /// </summary>
        private void ResetWaitCursor()
        {
            // Reset at Background priority so the Render-priority cursor frame from the
            // earlier `Mouse.OverrideCursor = Cursors.Wait` is guaranteed to flush before
            // the reset runs. Without this, fast reprocesses (UpdatePointEstimateResultsAsync)
            // can reset the cursor before the OS visually picks up the change — the user
            // sees no wait cursor at all when the mouse is stationary.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (Mouse.OverrideCursor == Cursors.Wait)
                    Mouse.OverrideCursor = null;
            }));
        }

        #region Plots

        /// <summary>
        /// Handles the LostFocus event of the user control. Resets the PlotClicked state.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles mouse down events on the user control to detect clicks on plots or toolbars.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse button event data.</param>
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Plot plot = GetCurrentPlot();
            OxyPlotToolbar toolbar = GetCurrentPlotToolbar();
            System.Windows.Media.HitTestResult plotHitResult = null;
            System.Windows.Media.HitTestResult toolbarHitResult = null;
            if (plot != null)
            {
                plotHitResult = VisualTreeHelper.HitTest(plot, e.GetPosition(plot));
            }
            if (toolbar != null)
            {
                toolbarHitResult = VisualTreeHelper.HitTest(toolbar, e.GetPosition(toolbar));
            }
            PreviewControlClicked?.Invoke(plotHitResult != null, toolbarHitResult != null, plot);
            PlotClicked = plotHitResult != null;
        }

        /// <summary>
        /// Handles the properties called event from plot toolbars and forwards it to subscribers.
        /// </summary>
        /// <param name="targetPlot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">Indicates whether to open the properties dialog.</param>
        /// <param name="propertyExpander">The property expander control.</param>
        /// <param name="selectedObject">The currently selected object.</param>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        /// <summary>
        /// Gets the currently selected plot based on which tab is active.
        /// </summary>
        /// <returns>The currently selected Plot object, or null if no plot is selected.</returns>
        public Plot GetCurrentPlot()
        {
            if (CopulaResultsTabItem.IsSelected == true)
            {
                return CopulaPlot;
            }
            else if (KernelDensityTabItem.IsSelected == true)
            {
                return KernelDensityControl.Plot;
            }
            else if (HistogramTabItem.IsSelected == true)
            {
                return HistogramControl.Plot;
            }
            else if (MeanLikelihoodTabItem.IsSelected == true)
            {
                return MeanLikelihoodControl.Plot;
            }
            else if (AutocorrelationTabItem.IsSelected == true)
            {
                return AutocorrelationControl.Plot;
            }
            else if (MarkovChainTabItem.IsSelected == true)
            {
                return MarkovChainTraceControl.Plot;
            }
            else if (InfluenceDiagnosticsTabItem.IsSelected == true)
            {
                return InfluenceDiagnosticsControl.Plot;
            }
            return null;
        }

        /// <summary>
        /// Gets the toolbar for the currently selected plot based on which tab is active.
        /// </summary>
        /// <returns>The currently selected OxyPlotToolbar object, or null if no toolbar is found.</returns>
        public OxyPlotToolbar GetCurrentPlotToolbar()
        {
            if (CopulaResultsTabItem.IsSelected == true)
            {
                return CopulaPlotToolbar;
            }
            else if (KernelDensityTabItem.IsSelected == true)
            {
                return KernelDensityControl.PlotToolbar;
            }
            else if (HistogramTabItem.IsSelected == true)
            {
                return HistogramControl.PlotToolbar;
            }
            else if (MeanLikelihoodTabItem.IsSelected == true)
            {
                return MeanLikelihoodControl.PlotToolbar;
            }
            else if (AutocorrelationTabItem.IsSelected == true)
            {
                return AutocorrelationControl.PlotToolbar;
            }
            else if (MarkovChainTabItem.IsSelected == true)
            {
                return MarkovChainTraceControl.PlotToolbar;
            }
            else if (InfluenceDiagnosticsTabItem.IsSelected == true)
            {
                return InfluenceDiagnosticsControl.PlotToolbar;
            }
            return null;
        }

        /// <summary>
        /// Binds the copula plot axis titles to the upstream <see cref="InputData.UnitLabel"/>
        /// of each marginal's InputData via reactive WPF bindings, with a "Marginal Y - {0}" /
        /// "Marginal X - {0}" StringFormat prefix. When the user toggles the axis type to
        /// probability mode, the bindings are cleared and literal "Marginal Y - Probability" /
        /// "Marginal X - Probability" titles are assigned. Numerical StringFormat ("N0" for
        /// values, "N2" for probabilities) is set imperatively in the same wrap because it
        /// is tied to the same mode switch. Matches the canonical pattern in Convention 12.
        /// </summary>
        private void BindAxisTitles()
        {
            if (Element == null || CopulaPlot == null) return;
            if (Element.MarginalX == null || Element.MarginalY == null || Element.MarginalX.InputData == null || Element.MarginalY.InputData == null) return;

            bool isProbabilityMode = AxisTypeComboBox.SelectedIndex == 1;

            using (Element.SuspendPlotBridges())
            {
                var yAxis = CopulaPlot.Axes.FirstOrDefault(a => a.Key == "Yaxis");
                if (yAxis != null)
                {
                    if (isProbabilityMode)
                    {
                        PlotAxisTitleDefaults.SetTitleIfDefault(yAxis, "Marginal Y - Probability", $"Marginal Y - {Element.MarginalY.InputData.UnitLabel}");
                    }
                    else
                    {
                        PlotAxisTitleDefaults.BindTitleIfDefault(yAxis, Element.MarginalY.InputData, nameof(InputData.UnitLabel), $"Marginal Y - {Element.MarginalY.InputData.UnitLabel}", "Marginal Y - Probability", "Marginal Y - {0}");
                    }
                    yAxis.StringFormat = isProbabilityMode ? "N2" : "N0";
                }

                var xAxis = CopulaPlot.Axes.FirstOrDefault(a => a.Key == "Xaxis");
                if (xAxis != null)
                {
                    if (isProbabilityMode)
                    {
                        PlotAxisTitleDefaults.SetTitleIfDefault(xAxis, "Marginal X - Probability", $"Marginal X - {Element.MarginalX.InputData.UnitLabel}");
                    }
                    else
                    {
                        PlotAxisTitleDefaults.BindTitleIfDefault(xAxis, Element.MarginalX.InputData, nameof(InputData.UnitLabel), $"Marginal X - {Element.MarginalX.InputData.UnitLabel}", "Marginal X - Probability", "Marginal X - {0}");
                    }
                    xAxis.StringFormat = isProbabilityMode ? "N2" : "N0";
                }
            }
        }

        /// <summary>
        /// Updates the copula plot with exact data and either simulated scatter data or contour lines
        /// based on the selected plot type (scatter, density contour, or exceedance probability contour).
        /// </summary>
        private void UpdateCopulaPlot()
        {
            if (CopulaPlot == null) return;

            // Lookup-or-create named series so user-customized styling survives Save/Open.
            var xyExactData = CopulaPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
                ?? new ScatterPointSeries
                {
                    Name = "ExactData",
                    Title = "Exact Data",
                    MarkerFill = Color.FromArgb(255, 75, 129, 250),
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 4,
                    MarkerType = MarkerType.Circle,
                };
            var xyCopulaData = CopulaPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "CopulaData")
                ?? new ScatterPointSeries
                {
                    Name = "CopulaData",
                    Title = "Simulated Data",
                    MarkerFill = Color.FromArgb(175, 250, 109, 109),
                    MarkerStroke = Color.FromArgb(175, 178, 83, 83),
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Square,
                };
            // Contour series: preserve user-edited styling (Color, LineStyle, StrokeThickness)
            // by snapshotting from any existing ContourLines series before recreating.
            var existingContour = CopulaPlot.Series.OfType<ContourSeries>().FirstOrDefault(s => s.Name == "ContourLines");
            var contourColor = existingContour?.Color ?? Colors.Black;
            var contourLineStyle = existingContour?.LineStyle ?? OxyPlot.LineStyle.Solid;
            var contourStrokeThickness = existingContour?.StrokeThickness ?? 1.0;
            var contourFontSize = existingContour?.FontSize ?? 10.0;
            bool useProbabilityAxis = AxisTypeComboBox.SelectedIndex == 1;
            string xyTracker = CreateCopulaScatterTrackerFormat(useProbabilityAxis);
            string observedTracker = CreateObservedCopulaTrackerFormat(useProbabilityAxis);
            string contourTracker = CreateContourTrackerFormat(useProbabilityAxis);

            using (Element?.SuspendPlotBridges())
            {
                CopulaPlot.Series.Clear();

                if (Element != null && Element.BayesianAnalysis != null && Element.IsValid != false)
                {
                    var exactData = CreateObservedCopulaPlotPoints(
                        Element.MarginalX.InputData.DataFrame.ExactSeries.Select(d => (ExactData)d),
                        Element.MarginalY.InputData.DataFrame.ExactSeries.Select(d => (ExactData)d),
                        useProbabilityAxis);

                    if (Element.BayesianAnalysis.IsEstimated == true)
                    {
                        // Next, load the contour plot or scatter plot.
                        var xDist = Element.MarginalX.GetMarginalModel()!.Distribution!.Clone();
                        var yDist = Element.MarginalY.GetMarginalModel()!.Distribution!.Clone();
                        var copula = Element.BivariateDistribution.Copula.Clone();

                        if (PlotTypeComboBox.SelectedIndex == 0)
                        {
                            // Create Scatter Plot

                            // Simulated data
                            var copulaData = new List<DataPoint>();
                            var xyData = Element.BivariateDistribution.GenerateRandomValues(Math.Min(10000, Element.BayesianAnalysis.OutputLength), Element.BayesianAnalysis.PRNGSeed);
                            for (int i = 0; i < xyData.GetLength(0); i++)
                            {
                                var x = AxisTypeComboBox.SelectedIndex == 0 ? xyData[i, 0] : xDist.CDF(xyData[i, 0]);
                                var y = AxisTypeComboBox.SelectedIndex == 0 ? xyData[i, 1] : yDist.CDF(xyData[i, 1]);
                                copulaData.Add(new DataPoint(x, y));
                            }
                            // Simulated data
                            xyCopulaData.ItemsSource = copulaData;
                            xyCopulaData.Mapping = item => { var d = (DataPoint)item; return new OxyPlot.Series.ScatterPoint(d.X, d.Y); };
                            xyCopulaData.TrackerFormatString = xyTracker;
                            CopulaPlot.Series.Add(xyCopulaData);
                        }
                        else if (PlotTypeComboBox.SelectedIndex == 1)
                        {
                            // Create Density Contour Plot

                            // Create x and y bins
                            double p = 1d / Math.Pow(10, Math.Ceiling(Math.Log10(exactData.Count) + 2));
                            var minX = xDist.InverseCDF(p);
                            var maxX = xDist.InverseCDF(1 - p);
                            var minY = yDist.InverseCDF(p);
                            var maxY = yDist.InverseCDF(1 - p);
                            int n = 100;

                            var xVals = Stratify.XValues(new StratificationOptions(minX, maxX, n - 1), false);
                            var yVals = Stratify.XValues(new StratificationOptions(minY, maxY, n - 1), false);
                            var xF = xVals.Select(xx => xx.LowerBound).ToList();
                            var yF = yVals.Select(yy => yy.LowerBound).ToList();
                            xF.Add(xVals.Last().UpperBound);
                            yF.Add(yVals.Last().UpperBound);

                            var Fx = xDist.CDF(xF);
                            var Fy = yDist.CDF(yF);
                            var pdf = new double[n, n];
                            double minf = double.MaxValue;
                            double maxf = double.MinValue;

                            Parallel.For(0, n, (int i) =>
                            {
                                for (int j = 0; j < n; j++)
                                {
                                    pdf[i, j] = copula.LogPDF(Fx[i], Fy[j]) + xDist.LogPDF(xF[i]) + yDist.LogPDF(yF[j]);
                                    minf = Math.Min(minf, pdf[i, j]);
                                    maxf = Math.Max(maxf, pdf[i, j]);
                                }
                            });

                            minf = Math.Log(1d / Math.Pow(10, Math.Ceiling(Math.Log10(1d / Math.Exp(maxf)) + 2.5)));
                            var zVals = Stratify.XValues(new StratificationOptions(minf, maxf, 9), false);
                            var z = zVals.Select(zz => zz.LowerBound).ToList();
                            z.Add(zVals.Last().UpperBound);

                            // Theme-aware label background
                            var plotBg = (CopulaPlot.PlotAreaBackground as SolidColorBrush)?.Color ?? Colors.White;
                            var labelBg = Color.FromArgb(200, plotBg.R, plotBg.G, plotBg.B);

                            // ContourSeries properties like RowCoordinates/ColumnCoordinates/Data/ContourLevels
                            // are rebuilt each update (they depend on the selected plot type and data). User-edited
                            // styling (Color, LineStyle, StrokeThickness) is preserved via the snapshot above.
                            var contourSeries = new ContourSeries
                            {
                                Name = "ContourLines",
                                Title = "Contours",
                                Color = contourColor,
                                LineStyle = contourLineStyle,
                                StrokeThickness = contourStrokeThickness,
                                FontSize = contourFontSize,
                                LabelBackground = labelBg,
                                LabelFormatString = "G3",
                                RowCoordinates = AxisTypeComboBox.SelectedIndex == 0 ? yF.ToArray() : Fy,
                                ColumnCoordinates = AxisTypeComboBox.SelectedIndex == 0 ? xF.ToArray() : Fx,
                                Data = pdf,
                                ContourLevels = z.ToArray(),
                                TrackerFormatString = contourTracker,
                            };
                            CopulaPlot.Series.Add(contourSeries);
                        }
                        else if (PlotTypeComboBox.SelectedIndex == 2)
                        {
                            // Create Exceedance Probability Contour Plot

                            // Create x and y bins
                            double p = 1d / Math.Pow(10, Math.Ceiling(Math.Log10(exactData.Count) + 2));
                            var minX = xDist.InverseCDF(p);
                            var maxX = xDist.InverseCDF(1 - p);
                            var minY = yDist.InverseCDF(p);
                            var maxY = yDist.InverseCDF(1 - p);
                            int n = 100;

                            var xVals = Stratify.XValues(new StratificationOptions(minX, maxX, n - 1), false);
                            var yVals = Stratify.XValues(new StratificationOptions(minY, maxY, n - 1), false);
                            var xF = xVals.Select(xx => xx.LowerBound).ToList();
                            var yF = yVals.Select(yy => yy.LowerBound).ToList();
                            xF.Add(xVals.Last().UpperBound);
                            yF.Add(yVals.Last().UpperBound);

                            var Fx = xDist.CDF(xF);
                            var Fy = yDist.CDF(yF);
                            var cdf = new double[n, n];

                            Parallel.For(0, n, (int i) =>
                            {
                                for (int j = 0; j < n; j++)
                                {
                                    cdf[i, j] = copula.ANDJointExceedanceProbability(Fx[i], Fy[j]);
                                }
                            });

                            // Theme-aware label background
                            var plotBg = (CopulaPlot.PlotAreaBackground as SolidColorBrush)?.Color ?? Colors.White;
                            var labelBg = Color.FromArgb(200, plotBg.R, plotBg.G, plotBg.B);

                            var contourSeries = new ContourSeries
                            {
                                Name = "ContourLines",
                                Title = "Contours",
                                Color = contourColor,
                                LineStyle = contourLineStyle,
                                StrokeThickness = contourStrokeThickness,
                                FontSize = contourFontSize,
                                LabelBackground = labelBg,
                                LabelFormatString = "G3",
                                RowCoordinates = AxisTypeComboBox.SelectedIndex == 0 ? yF.ToArray() : Fy,
                                ColumnCoordinates = AxisTypeComboBox.SelectedIndex == 0 ? xF.ToArray() : Fx,
                                Data = cdf,
                                ContourLevels = new double[] { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5 },
                                TrackerFormatString = contourTracker,
                            };
                            CopulaPlot.Series.Add(contourSeries);
                        }
                    }

                    // Add Exact data
                    xyExactData.ItemsSource = exactData;
                    xyExactData.Mapping = item => { var d = (CopulaPlotObservation)item; return new OxyPlot.Series.ScatterPoint(d.X, d.Y); };
                    xyExactData.TrackerFormatString = observedTracker;
                    CopulaPlot.Series.Add(xyExactData);
                }

                CopulaPlot.InvalidatePlot(true);
            }
            Element?.RebuildSeriesAndAnnotationBridges(CopulaPlot);
        }

        /// <summary>
        /// Creates the tracker format string for simulated copula scatter points.
        /// </summary>
        /// <param name="useProbabilityAxis">Whether the X and Y coordinates are probabilities.</param>
        /// <returns>The tracker format string for simulated copula points.</returns>
        /// <remarks>
        /// Simulated points do not map back to input data, so the format does not include
        /// an input-data index.
        /// </remarks>
        internal static string CreateCopulaScatterTrackerFormat(bool useProbabilityAxis)
        {
            return useProbabilityAxis
                ? "{0}" + Environment.NewLine + "{1}: {2:0.000000}" + Environment.NewLine + "{3}: {4:0.000000}"
                : "{0}" + Environment.NewLine + "{1}: {2:" + FrameworkUI.UserSettings.ValueStringFormat + "}" + Environment.NewLine + "{3}: {4:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
        }

        /// <summary>
        /// Creates the tracker format string for observed exact-data copula points.
        /// </summary>
        /// <param name="useProbabilityAxis">Whether the X and Y coordinates are probabilities.</param>
        /// <returns>The tracker format string for observed exact-data points.</returns>
        /// <remarks>
        /// The <c>{Index}</c> placeholder is resolved by OxyPlot against
        /// <see cref="CopulaPlotObservation.Index"/>.
        /// </remarks>
        internal static string CreateObservedCopulaTrackerFormat(bool useProbabilityAxis)
        {
            string xyTracker = CreateCopulaScatterTrackerFormat(useProbabilityAxis);
            return "{0}" + Environment.NewLine + "Index: {Index}" + Environment.NewLine + xyTracker.Substring(("{0}" + Environment.NewLine).Length);
        }

        /// <summary>
        /// Creates the tracker format string for copula contour series.
        /// </summary>
        /// <param name="useProbabilityAxis">Whether the X and Y coordinates are probabilities.</param>
        /// <returns>The tracker format string for contour points.</returns>
        /// <remarks>
        /// Contour points represent gridded model values, not input observations, so the
        /// format intentionally does not include an input-data index.
        /// </remarks>
        internal static string CreateContourTrackerFormat(bool useProbabilityAxis)
        {
            return CreateCopulaScatterTrackerFormat(useProbabilityAxis) + Environment.NewLine + "{5}: {6:0.000000}";
        }

        /// <summary>
        /// Creates observed copula plot points from matched exact data.
        /// </summary>
        /// <param name="xData">Exact data from the X marginal.</param>
        /// <param name="yData">Exact data from the Y marginal.</param>
        /// <param name="useProbabilityAxis">Whether to plot plotting-position complements instead of raw values.</param>
        /// <returns>Observed copula plot points matched by exact-data index.</returns>
        /// <remarks>
        /// The merge excludes low outliers, sorts by <c>Index</c>, and advances
        /// both series when indexes match so the plotted observed points mirror the fitted
        /// paired subset.
        /// </remarks>
        internal static List<CopulaPlotObservation> CreateObservedCopulaPlotPoints(
            IEnumerable<ExactData> xData,
            IEnumerable<ExactData> yData,
            bool useProbabilityAxis)
        {
            var dataX = xData
                .Where(d => d != null && d.IsLowOutlier == false)
                .OrderBy(d => d.Index)
                .ToList();
            var dataY = yData
                .Where(d => d != null && d.IsLowOutlier == false)
                .OrderBy(d => d.Index)
                .ToList();

            var result = new List<CopulaPlotObservation>();
            int i = 0;
            int j = 0;
            while (i < dataX.Count && j < dataY.Count)
            {
                int idxX = dataX[i].Index;
                int idxY = dataY[j].Index;
                if (idxX == idxY)
                {
                    double x = useProbabilityAxis ? dataX[i].PlottingPositionComplement : dataX[i].Value;
                    double y = useProbabilityAxis ? dataY[j].PlottingPositionComplement : dataY[j].Value;
                    result.Add(new CopulaPlotObservation(idxX, x, y));
                    i++;
                    j++;
                }
                else if (idxX < idxY) i++;
                else j++;
            }

            return result;
        }

        #endregion

        /// <summary>
        /// Binds the frequency curve data grid with X-Y ordinates and their associated confidence intervals
        /// and point estimates from the analysis results.
        /// </summary>
        private void BindFrequencyCurveDataGrid()
        {
            FrequencyCurveTable.ItemsSource = null;

            if (Element == null || Element.BayesianAnalysis == null || Element.BayesianAnalysis.IsEstimated != true || Element.AnalysisResults == null)
                return;

            ModeColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

            var curvePoints = new List<FrequencyCurvePoint>();
            for (int i = 0; i < Element.XYOrdinates.Count; i++)
            {
                var x = Element.XYOrdinates[i].X;
                var y = Element.XYOrdinates[i].Y.Mean;
                var up = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                var lo = Element.AnalysisResults.ConfidenceIntervals[i, 0];
                var prd = Element.AnalysisResults.MeanCurve[i];
                var md = Element.AnalysisResults.ModeCurve[i];
                curvePoints.Add(new FrequencyCurvePoint(x, y, up, lo, prd, md));
            }
            FrequencyCurveTable.ItemsSource = curvePoints;
            FrequencyCurveTable.Items.Refresh();
        }

        /// <summary>
        /// Sets the frequency curve table column headers for the credible interval columns
        /// based on the configured credible interval width.
        /// </summary>
        private void SetFrequencyCurveTableColumnHeaders()
        {
            double alpha = (1 - Element.BayesianAnalysis.CredibleIntervalWidth) / 2;
            UpperColumn.Header = ((1 - alpha) * 100).ToString("F1") + "% CI";
            LowerColumn.Header = (alpha * 100).ToString("F1") + "% CI";
        }

        /// <summary>
        /// Sets the string format for frequency curve table columns based on user settings.
        /// </summary>
        private void SetColumnStringFormats()
        {
            // Update the column binding string format
            XColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            YColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            UpperColumn.Binding.StringFormat = "E4";
            LowerColumn.Binding.StringFormat = "E4";
            PredictiveColumn.Binding.StringFormat = "E4";
            ModeColumn.Binding.StringFormat = "E4";
        }

        /// <summary>
        /// Binds the summary statistics data grid with distribution parameters and goodness-of-fit metrics.
        /// Displays NaN values if the analysis has not been estimated.
        /// </summary>
        private void BindSummaryStatisticsDataGrid()
        {
            SummaryStatisticsTable.ItemsSource = null;

            if (Element == null || Element.BayesianAnalysis == null)
                return;

            StatValueColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

            var summaryStats = new List<SummaryStatistic>();
            if (Element.BayesianAnalysis.IsEstimated == false || Element.AnalysisResults == null)
            {

                for (int i = 0; i < Element.BivariateDistribution.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.BivariateDistribution.Parameters[i].DisplayName, double.NaN));
                }
                summaryStats.Add(new SummaryStatistic("AIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("BIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("DIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("WAIC", double.NaN));
                summaryStats.Add(new SummaryStatistic("LOO-CV", double.NaN));
                summaryStats.Add(new SummaryStatistic("RMSE", double.NaN));
            }
            else
            {
                for (int i = 0; i < Element.BivariateDistribution.Parameters.Count; i++)
                {
                    summaryStats.Add(new SummaryStatistic(Element.BivariateDistribution.Parameters[i].DisplayName, Element.BivariateDistribution.Parameters[i].Value));
                }
                summaryStats.Add(new SummaryStatistic("AIC", Element.AnalysisResults.AIC));
                summaryStats.Add(new SummaryStatistic("BIC", Element.AnalysisResults.BIC));
                summaryStats.Add(new SummaryStatistic("DIC", Element.AnalysisResults.DIC));
                summaryStats.Add(new SummaryStatistic("WAIC", Element.BayesianAnalysis.WAIC));
                summaryStats.Add(new SummaryStatistic("LOO-CV", Element.BayesianAnalysis.LOOIC));
                summaryStats.Add(new SummaryStatistic("RMSE", Element.AnalysisResults.RMSE));
            }

            SummaryStatisticsTable.ItemsSource = summaryStats;
            SummaryStatisticsTable.Items.Refresh();
        }

        /// <summary>
        /// Handles the LoadingRow event for the summary statistics table to apply custom row styling.
        /// Adds a border above rows that start a new section (e.g., AIC statistics).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The data grid row event arguments.</param>
        private void SummaryStatisticsTable_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (((SummaryStatistic)e.Row.DataContext).Name == "Minimum" ||
                ((SummaryStatistic)e.Row.DataContext).Name == "AIC")
            {
                e.Row.BorderThickness = new Thickness(0, 2, 0, 0);
                e.Row.SetResourceReference(Border.BorderBrushProperty, "EnvironmentWindowText");
            }
            else
            {
                e.Row.BorderThickness = new Thickness(0, 0, 0, 0);
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event of the inner tab control.
        /// Renders the copula plot lazily when the user activates the Copula tab and the data has changed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void InnerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;
            if (_copulaPlotDirty && CopulaResultsTabItem.IsSelected == true)
            {
                UpdateCopulaPlotWithWaitCursor();
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event for the X parameter combo box.
        /// Updates plot labels and redraws the copula plot.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void XParameterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded)
            {
                BindAxisTitles();
                _copulaPlotDirty = true;
                if (CopulaResultsTabItem.IsSelected == true)
                {
                    UpdateCopulaPlotWithWaitCursor();
                }
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event for the Y parameter combo box.
        /// Updates plot labels and redraws the copula plot.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The selection changed event arguments.</param>
        private void YParameterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded)
            {
                BindAxisTitles();
                _copulaPlotDirty = true;
                if (CopulaResultsTabItem.IsSelected == true)
                {
                    UpdateCopulaPlotWithWaitCursor();
                }
            }
        }

        /// <summary>
        /// Updates the dirty copula plot with visible wait-cursor feedback and marks it clean afterward.
        /// </summary>
        /// <remarks>
        /// Copula redraws can generate simulated samples or contours, so the lazy path uses the
        /// shared wait-cursor helper even though the analysis results are already available.
        /// </remarks>
        private void UpdateCopulaPlotWithWaitCursor()
        {
            WaitCursorHelper.RunWithVisibleWaitCursor(Dispatcher, () =>
            {
                UpdateCopulaPlot();
                _copulaPlotDirty = false;
            });
        }
    }
}
