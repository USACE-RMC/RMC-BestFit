using OxyPlot;
using OxyPlot.Wpf;
using OxyPlotControls;
using FrameworkInterfaces;
using FrameworkUI;
using RMC.BestFit.Models;
using ModelAnalyses = RMC.BestFit.Analyses;
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
using System.Windows.Threading;
using System.Xml.Linq;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and managing composite distribution analysis results, including frequency plots,
    /// data visualization, and statistical summaries.
    /// </summary>
    public partial class CompositeAnalysisControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeAnalysisControl"/> class.
        /// Sets up the control and initializes color codes for plot series.
        /// </summary>
        public CompositeAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
            _colorHexCodes = GenericControls.GeneralMethods.RandomColorsLongList;
            // DataGrid Binding.StringFormat must be set before first render.
            SetColumnStringFormats();
        }

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// Enables data binding for the composite analysis element.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(CompositeAnalysis), typeof(CompositeAnalysisControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the composite analysis element displayed in this control.
        /// </summary>
        public CompositeAnalysis Element
        {
            get { return (CompositeAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Element"/> dependency property changes.
        /// Manages event handler subscriptions and unsubscriptions for the old and new element values.
        /// </summary>
        /// <param name="d">The dependency object on which the property changed.</param>
        /// <param name="e">Event arguments containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as CompositeAnalysisControl == null) return;
            var thisControl = (CompositeAnalysisControl)d;

            // Remove handlers
            if (e.OldValue != null)
            {
                CompositeAnalysis oldElement = e.OldValue as CompositeAnalysis;
                if (oldElement != null)
                {
                    oldElement.PropertyChanged -= thisControl.Element_PropertyChanged;
                    thisControl.FrequencyPlotHost.Content = null;
                    thisControl.FrequencyPlotToolbar.Plot = null;
                    // NOTE: PropertiesCalled is wired in XAML — no programmatic -= needed.
                }
            }

            if (e.NewValue == null) return;
            var newElement = e.NewValue as CompositeAnalysis;
            if (newElement == null) return;

            // Subscribe Element-scoped handler
            newElement.PropertyChanged += thisControl.Element_PropertyChanged;

            // Attach plot, wire toolbar, and bind axis titles inside a bridge-suspension
            // block. PropertiesCalled is wired in XAML — no programmatic += needed.
            using (newElement.SuspendPlotBridges())
            {
                thisControl.FrequencyPlotHost.Content = newElement.FrequencyPlot;
                thisControl.FrequencyPlotToolbar.Plot = newElement.FrequencyPlot;
                thisControl.BindAxisTitles();
            }
        }

        /// <summary>
        /// Convenience accessor for the Element-owned frequency plot.
        /// </summary>
        private Plot FrequencyPlot => Element?.FrequencyPlot;

        /// <summary>
        /// Gets a value indicating whether a plot was clicked.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        /// <summary>
        /// Event raised when plot properties are requested.
        /// </summary>
        public event PlotPropertiesCalledEventHandler PlotPropertiesCalled;

        /// <summary>
        /// Handles the PropertiesCalled event from the plot toolbar. Forwards to the control's PlotPropertiesCalled event.
        /// </summary>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        /// <summary>
        /// Delegate for the <see cref="PlotPropertiesCalled"/> event.
        /// </summary>
        /// <param name="plot">The plot for which properties are requested.</param>
        /// <param name="openProperties">Indicates whether to open the properties dialog.</param>
        /// <param name="propertyExpander">The property expander control.</param>
        /// <param name="selectedObject">The selected object in the plot.</param>
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties, OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);

        /// <summary>
        /// Event raised when the preview control is clicked.
        /// </summary>
        public event PreviewControlClickedEventHandler PreviewControlClicked;

        /// <summary>
        /// Delegate for the <see cref="PreviewControlClicked"/> event.
        /// </summary>
        /// <param name="plotClicked">Indicates whether the plot was clicked.</param>
        /// <param name="toolbarClicked">Indicates whether the toolbar was clicked.</param>
        /// <param name="plot">The plot that was clicked.</param>
        public delegate void PreviewControlClickedEventHandler(bool plotClicked, bool toolbarClicked, Plot plot);

        /// <summary>
        /// Array of hex color codes used for plot series styling.
        /// </summary>
        private string[] _colorHexCodes;


        /// <summary>
        /// Handles the Loaded event of the user control. Initializes plot settings and data bindings.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;

            // Data refresh on every load. Plot host, toolbar, axis-title bindings, and
            // Element.PropertyChanged are wired once per Element in ElementCallback.
            bool wasUndoEnabled = Element.IsUndoEnabled;
            Element.IsUndoEnabled = false;
            try
            {
                UpdateFrequencyPlot();
                BindFrequencyCurveDataGrid();
                SetFrequencyCurveTableColumnHeaders();
                BindSummaryStatisticsDataGrid();
            }
            finally
            {
                Element.IsUndoEnabled = wasUndoEnabled;
            }
        }

        /// <summary>
        /// Handles the Unloaded event of the user control. Unsubscribes all element event handlers
        /// to prevent memory leaks and stale event callbacks.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Pattern B (canonical): Element-scoped lifecycle (PropertyChanged, plot
            // hosts, toolbars) is owned by ElementCallback and survives unload/reload
            // cycles. This control has no control-scoped subscriptions to tear down,
            // so Unloaded is intentionally a no-op.
        }

        /// <summary>
        /// Handles property change events from the Element. Updates the UI when analysis results or input data change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Marshal to UI thread if called from a background thread.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Element_PropertyChanged(sender, e)));
                return;
            }

            if (e.PropertyName == nameof(Element.AnalysisResults))
            {
                BindFrequencyCurveDataGrid();
                BindSummaryStatisticsDataGrid();
                UpdateFrequencyPlot();
                ResetWaitCursor();
            }
            if (e.PropertyName == nameof(Element.InputData))
            {
                using (Element?.SuspendPlotBridges())
                {
                    BindAxisTitles();
                }
                UpdateFrequencyPlot();
            }
            if (e.PropertyName == nameof(Element.BayesianAnalysis.CredibleIntervalWidth))
            {
                SetFrequencyCurveTableColumnHeaders();
                UpdateFrequencyPlot();
                BindFrequencyCurveDataGrid();
                BindSummaryStatisticsDataGrid();
            }
            if (e.PropertyName == nameof(Element.BayesianAnalysis.PointEstimator))
            {
                UpdateFrequencyPlot();
                BindFrequencyCurveDataGrid();
                BindSummaryStatisticsDataGrid();
            }
            // CompositeAnalysis exception: BayesianAnalysis.CredibleIntervalWidth calls ClearResults
            // (no per-realisation distribution cache), so no wait cursor for that property.
            // BayesianAnalysis.PointEstimator and ProbabilityOrdinates DO reprocess via
            // UpdatePointEstimateResultsAsync / CreateFrequencyAnalysisResultsAsync.
            if ((e.PropertyName == nameof(Element.BayesianAnalysis.PointEstimator)
                 || e.PropertyName == nameof(Element.ProbabilityOrdinates))
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

        /// <summary>
        /// Binds the frequency plot Y-axis title to the InputData.UnitLabel property.
        /// Called after element attachment and when InputData changes.
        /// </summary>
        private void BindAxisTitles()
        {
            if (Element == null) return;

            // Frequency plot: Y-axis title = UnitLabel
            var freqYAxis = FrequencyPlot?.Axes.FirstOrDefault(a => a.Key == "Yaxis");
            if (freqYAxis != null)
            {
                if (Element.InputData != null)
                    PlotAxisTitleDefaults.BindTitleIfDefault(freqYAxis, Element.InputData, nameof(InputData.UnitLabel), Element.InputData.UnitLabel);
                else
                    PlotAxisTitleDefaults.SetTitleIfDefault(freqYAxis, string.Empty);
            }
        }

        /// <summary>
        /// Handles the LostFocus event of the user control. Resets the PlotClicked flag.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            PlotClicked = false;
        }

        /// <summary>
        /// Handles the PreviewMouseDown event of the user control. Determines if the plot or toolbar was clicked and raises the PreviewControlClicked event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Plot plot = FrequencyPlot;
            OxyPlotToolbar toolbar = FrequencyPlotToolbar;
            if (plot == null || toolbar == null) return;
            var plotHitResult = VisualTreeHelper.HitTest(plot, e.GetPosition(plot));
            var toolbarHitResult = VisualTreeHelper.HitTest(toolbar, e.GetPosition(toolbar));
            PreviewControlClicked?.Invoke(plotHitResult != null, toolbarHitResult != null, plot);
            PlotClicked = plotHitResult != null;
        }

        /// <summary>
        /// Handles the Checked event of the SubDistCheckbox. Updates the frequency plot to show sub-distributions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void SubDistCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateFrequencyPlot();
        }

        /// <summary>
        /// Handles the Unchecked event of the SubDistCheckbox. Updates the frequency plot to hide sub-distributions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void SubDistCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateFrequencyPlot();
        }

        /// <summary>
        /// Updates the frequency plot with the current analysis results and input data.
        /// Adds or removes series based on the available data and user selections.
        /// </summary>
        private void UpdateFrequencyPlot()
        {
            if (FrequencyPlot == null) return;

            // Lookup-or-create named series so user-customized styling survives Save/Open.
            var frequencyExactData = FrequencyPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "ExactData")
                ?? new ScatterPointSeries
                {
                    Name = "ExactData",
                    Title = "Exact Data",
                    MarkerFill = Colors.Black,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                };
            var frequencyLowOutlierData = FrequencyPlot.Series.OfType<ScatterPointSeries>().FirstOrDefault(s => s.Name == "LowOutlierData")
                ?? new ScatterPointSeries
                {
                    Name = "LowOutlierData",
                    Title = "Low Outlier Data",
                    MarkerStroke = Colors.Red,
                    MarkerStrokeThickness = 2,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Cross,
                };
            var frequencyUncertainData = FrequencyPlot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "UncertainData")
                ?? new ScatterErrorSeries
                {
                    Name = "UncertainData",
                    Title = "Uncertain Data",
                    MarkerFill = Colors.Green,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Diamond,
                };
            var frequencyIntervalData = FrequencyPlot.Series.OfType<ScatterErrorSeries>().FirstOrDefault(s => s.Name == "IntervalData")
                ?? new ScatterErrorSeries
                {
                    Name = "IntervalData",
                    Title = "Interval Data",
                    MarkerFill = Colors.Cyan,
                    MarkerStroke = Colors.Black,
                    MarkerStrokeThickness = 1,
                    MarkerSize = 3,
                    MarkerType = MarkerType.Circle,
                };
            var credibleIntervals = FrequencyPlot.Series.OfType<AreaSeries>().FirstOrDefault(s => s.Name == "CredibleIntervals")
                ?? new AreaSeries
                {
                    Name = "CredibleIntervals",
                    Fill = Color.FromArgb(75, 104, 140, 175),
                    Color = Color.FromArgb(255, 53, 59, 122),
                    LineStyle = LineStyle.Solid,
                    BrokenLineThickness = 1,
                    StrokeThickness = 1,
                    Decimator = OxyPlot.Decimator.Decimate,
                };
            var posteriorPredictive = FrequencyPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorPredictive")
                ?? new LineSeries
                {
                    Name = "PosteriorPredictive",
                    Title = "Posterior Predictive",
                    Color = Colors.Blue,
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Dash,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };
            var posteriorMode = FrequencyPlot.Series.OfType<LineSeries>().FirstOrDefault(s => s.Name == "PosteriorMode")
                ?? new LineSeries
                {
                    Name = "PosteriorMode",
                    Title = "Posterior Mode",
                    Color = Colors.Black,
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Solid,
                    Decimator = OxyPlot.Decimator.Decimate,
                    MinimumSegmentLength = 4.0,
                };

            using (Element?.SuspendPlotBridges())
            {
                FrequencyPlot.Series.Clear();

                if (Element == null)
                {
                    FrequencyPlot.InvalidatePlot(true);
                }
                else
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    try
                    {
                        // Add Curves
                        if (Element.IsEstimated == true && Element.AnalysisResults != null)
                        {
                            // Add sub-distributions (dynamic count — per filtered distribution;
                            // documented exception to the lookup-or-create rule — series count depends
                            // on runtime state).
                            if (Element.AnalysesValid == true && SubDistCheckbox.IsChecked == true)
                            {
                                var colorHexCodes = GenericControls.GeneralMethods.RandomColorsShortList;
                                for (int i = 0; i < Element.Analyses.Count; i++)
                                {
                                    var points = new List<OxyPlot.DataPoint>();
                                    for (int j = 0; j < Element.Analyses[i].UnivariateAnalysis.ProbabilityOrdinates.Count; j++)
                                    {
                                        var x = Element.Analyses[i].UnivariateAnalysis.ProbabilityOrdinates[j];
                                        var y = Element.Analyses[i].UnivariateAnalysis.AnalysisResults.ModeCurve[j];
                                        points.Add(new OxyPlot.DataPoint(x, y));
                                    }

                                    var color = (Color)ColorConverter.ConvertFromString(colorHexCodes[i + 1]);
                                    var newLineSeries = new LineSeries()
                                    {
                                        Name = "series_" + i.ToString(),
                                        Title = (Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode") + " - " + Element.Analyses[i].UnivariateAnalysis.Name,
                                        LineStyle = OxyPlot.LineStyle.Solid,
                                        Color = Color.FromArgb(200, color.R, color.G, color.B),
                                        StrokeThickness = 1.5,
                                        MarkerType = MarkerType.Triangle,
                                        MarkerStroke = Color.FromArgb(200, color.R, color.G, color.B),
                                        MarkerFill = Colors.Transparent,
                                        MarkerSize = 3,
                                        MarkerStrokeThickness = 1.5,
                                        ItemsSource = points,
                                        Decimator = OxyPlot.Decimator.Decimate,
                                        MinimumSegmentLength = 4.0,
                                        TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}"
                                    };

                                    FrequencyPlot.Series.Add(newLineSeries);
                                }
                            }

                            var ciPoints = new List<Point3D>();
                            var prdPoints = new List<OxyPlot.DataPoint>();
                            var mdPoints = new List<OxyPlot.DataPoint>();
                            for (int i = 0; i < Element.ProbabilityOrdinates.Count; i++)
                            {
                                var p = Element.ProbabilityOrdinates[i];
                                var up = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                                var lo = Element.AnalysisResults.ConfidenceIntervals[i, 0];
                                var prd = Element.AnalysisResults.MeanCurve[i];
                                var md = Element.AnalysisResults.ModeCurve[i];
                                ciPoints.Add(new Point3D(p, lo, up));
                                prdPoints.Add(new OxyPlot.DataPoint(p, prd));
                                mdPoints.Add(new OxyPlot.DataPoint(p, md));
                            }

                            // Credible Intervals
                            credibleIntervals.ItemsSource = ciPoints;
                            credibleIntervals.DataFieldX = "X";
                            credibleIntervals.DataFieldY = "Y";
                            credibleIntervals.DataFieldX2 = "X";
                            credibleIntervals.DataFieldY2 = "Z";
                            credibleIntervals.Title = (Element.BayesianAnalysis.CredibleIntervalWidth * 100).ToString("F0") + "% Credible Intervals";
                            credibleIntervals.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            FrequencyPlot.Series.Add(credibleIntervals);

                            // Predictive
                            posteriorPredictive.ItemsSource = prdPoints;
                            posteriorPredictive.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            FrequencyPlot.Series.Add(posteriorPredictive);

                            // Point Estimator
                            posteriorMode.Title = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";
                            posteriorMode.ItemsSource = mdPoints;
                            posteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            FrequencyPlot.Series.Add(posteriorMode);
                        }

                        // See if Y-axis is logarithmic
                        bool logAxis = false;
                        foreach (var axis in FrequencyPlot.Axes)
                        {
                            if (axis.Key == "Yaxis" && axis as LogarithmicAxis != null)
                            {
                                logAxis = true;
                                break;
                            }
                        }

                        if (Element.InputData != null)
                        {
                            // Exact data
                            frequencyExactData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == false);
                            frequencyExactData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                            frequencyExactData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            if (Element.InputData.DataFrame.ExactSeries.Count > 0) FrequencyPlot.Series.Add(frequencyExactData);

                            // low outliers data
                            frequencyLowOutlierData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true && x.Value > 1E-16) : Element.InputData.DataFrame.ExactSeries.Where(x => ((ExactData)x).IsLowOutlier == true);
                            frequencyLowOutlierData.Mapping = item => { var d = (ExactData)item; return new OxyPlot.Series.ScatterPoint(d.PlottingPosition, d.Value); };
                            frequencyLowOutlierData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            if (Element.InputData.DataFrame.NumberOfLowOutliers > 0) FrequencyPlot.Series.Add(frequencyLowOutlierData);

                            // Uncertain data
                            frequencyUncertainData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.UncertainSeries : Element.InputData.DataFrame.UncertainSeries.Where(x => x.Value > 1E-16);
                            frequencyUncertainData.DataFieldX = nameof(UncertainData.PlottingPosition);
                            frequencyUncertainData.DataFieldY = nameof(UncertainData.Value);
                            frequencyUncertainData.DataFieldLowerErrorY = nameof(UncertainData.LowerValue);
                            frequencyUncertainData.DataFieldUpperErrorY = nameof(UncertainData.UpperValue);
                            frequencyUncertainData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            if (Element.InputData.DataFrame.UncertainSeries.Count > 0) FrequencyPlot.Series.Add(frequencyUncertainData);

                            // interval data
                            frequencyIntervalData.ItemsSource = logAxis == true ? Element.InputData.DataFrame.IntervalSeries : Element.InputData.DataFrame.IntervalSeries.Where(x => x.Value > 1E-16);
                            frequencyIntervalData.DataFieldX = nameof(IntervalData.PlottingPosition);
                            frequencyIntervalData.DataFieldY = nameof(IntervalData.Value);
                            frequencyIntervalData.DataFieldLowerErrorY = nameof(IntervalData.LowerValue);
                            frequencyIntervalData.DataFieldUpperErrorY = nameof(IntervalData.UpperValue);
                            frequencyIntervalData.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2:0.000000000}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                            if (Element.InputData.DataFrame.IntervalSeries.Count > 0) FrequencyPlot.Series.Add(frequencyIntervalData);
                        }

                        // Add Alternatives
                        if (Element.IsEstimated == true && Element.AnalysisResults != null)
                            AlternativeSelector.AddAllChecked();

                        FrequencyPlot.InvalidatePlot(true);
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }
                }
            }
            Element?.RebuildSeriesAndAnnotationBridges(FrequencyPlot);
        }

        /// <summary>
        /// Handles the AnalysisAdded event from the AlternativeSelector. Adds series for the alternative analysis to the frequency plot.
        /// </summary>
        /// <param name="analysisItem">The analysis alternative item containing the alternative distribution to add.</param>
        private void AlternativeSelector_AnalysisAdded(AnalysisAlternativeItem analysisItem)
        {
            // Remove series
            FrequencyPlot.Series.Remove(analysisItem.CredibleIntervals);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorPredictive);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorMode);

            var alternative = analysisItem.Alternative;
            if (alternative.IsEstimated == true && alternative.AnalysisResults != null)
            {
                // Get unique color
                var index = FrequencyPlot.Series.Count;
                Color lineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[index]);
                Color fillColor = Color.FromArgb(100, lineColor.R, lineColor.G, lineColor.B);

                for (int i = 4; i < _colorHexCodes.Length; i++)
                {
                    var templineColor = (Color)ColorConverter.ConvertFromString(_colorHexCodes[i]);
                    var tempfillColor = Color.FromArgb(100, templineColor.R, templineColor.G, templineColor.B);
                    bool colorExists = false;
                    for (int j = 0; j < FrequencyPlot.Series.Count; j++)
                    {
                        if (FrequencyPlot.Series[j].Name.Contains("Mode") && FrequencyPlot.Series[j].Color == templineColor)
                        {
                            colorExists = true;
                            break;
                        }
                    }
                    if (colorExists == false || i == _colorHexCodes.Length - 1)
                    {
                        lineColor = templineColor;
                        fillColor = tempfillColor;
                        break;
                    }
                }

                var ciPoints = new List<Point3D>();
                var prdPoints = new List<OxyPlot.DataPoint>();
                var mdPoints = new List<OxyPlot.DataPoint>();
                for (int i = 0; i < ((ModelAnalyses.IProbabilityOrdinates)alternative).ProbabilityOrdinates.Count; i++)
                {
                    var p = ((ModelAnalyses.IProbabilityOrdinates)alternative).ProbabilityOrdinates[i];
                    var up = alternative.AnalysisResults.ConfidenceIntervals[i, 1];
                    var lo = alternative.AnalysisResults.ConfidenceIntervals[i, 0];
                    var prd = alternative.AnalysisResults.MeanCurve[i];
                    var md = alternative.AnalysisResults.ModeCurve[i];
                    ciPoints.Add(new Point3D(p, lo, up));
                    prdPoints.Add(new OxyPlot.DataPoint(p, prd));
                    mdPoints.Add(new OxyPlot.DataPoint(p, md));
                }

                // Series labels
                double ciWidth = alternative.BayesianAnalysis.CredibleIntervalWidth;
                string ciLabel = "% Credible Intervals";
                string predLabel = " - Posterior Predictive";
                string pointLabel = " - " + (alternative.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode");
                if (alternative.GetType() == typeof(B17CAnalysis))
                {
                    ciLabel = "% Confidence Intervals";
                    predLabel = " - Expected Probability";
                    pointLabel = " - " + (alternative.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Mean Parameters" : "Computed");
                }

                // Credible Intervals
                analysisItem.CredibleIntervals.Name = "CredibleIntervals_" + index;
                analysisItem.CredibleIntervals.Title = alternative.Name + " - " + (ciWidth * 100).ToString("F0") + ciLabel;
                analysisItem.CredibleIntervals.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                analysisItem.CredibleIntervals.ItemsSource = ciPoints;
                analysisItem.CredibleIntervals.DataFieldX = "X";
                analysisItem.CredibleIntervals.DataFieldY = "Y";
                analysisItem.CredibleIntervals.DataFieldX2 = "X";
                analysisItem.CredibleIntervals.DataFieldY2 = "Z";
                analysisItem.CredibleIntervals.Decimator = OxyPlot.Decimator.Decimate;
                analysisItem.CredibleIntervals.Fill = fillColor;
                analysisItem.CredibleIntervals.Color = Colors.Transparent;
                FrequencyPlot.Series.Add(analysisItem.CredibleIntervals);

                // Predictive
                analysisItem.PosteriorPredictive.Name = "PosteriorPredictive_" + index;
                analysisItem.PosteriorPredictive.Title = alternative.Name + predLabel;
                analysisItem.PosteriorPredictive.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                analysisItem.PosteriorPredictive.ItemsSource = prdPoints;
                analysisItem.PosteriorPredictive.Decimator = OxyPlot.Decimator.Decimate;
                analysisItem.PosteriorPredictive.MinimumSegmentLength = 4.0;
                analysisItem.PosteriorPredictive.Color = lineColor;
                FrequencyPlot.Series.Add(analysisItem.PosteriorPredictive);

                // Point Estimator
                analysisItem.PosteriorMode.Name = "PosteriorMode_" + index;
                analysisItem.PosteriorMode.Title = alternative.Name + pointLabel;
                analysisItem.PosteriorMode.TrackerFormatString = "{0}" + Environment.NewLine + "{1}: {2}" + Environment.NewLine + "{3}: {4:" + UserSettings.ValueStringFormat + "}";
                analysisItem.PosteriorMode.ItemsSource = mdPoints;
                analysisItem.PosteriorMode.Decimator = OxyPlot.Decimator.Decimate;
                analysisItem.PosteriorMode.MinimumSegmentLength = 4.0;
                analysisItem.PosteriorMode.Color = lineColor;
                FrequencyPlot.Series.Add(analysisItem.PosteriorMode);

                FrequencyPlot.InvalidatePlot(true);

            }

        }

        /// <summary>
        /// Handles the AnalysisRemoved event from the AlternativeSelector. Removes series for the alternative analysis from the frequency plot.
        /// </summary>
        /// <param name="analysisItem">The analysis alternative item containing the alternative distribution to remove.</param>
        private void AlternativeSelector_AnalysisRemoved(AnalysisAlternativeItem analysisItem)
        {
            FrequencyPlot.Series.Remove(analysisItem.CredibleIntervals);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorPredictive);
            FrequencyPlot.Series.Remove(analysisItem.PosteriorMode);
            FrequencyPlot.InvalidatePlot(true);
        }

        /// <summary>
        /// Handles the MouseLeftButtonUp event of a TextBlock. Toggles the visibility of the filter options.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowFilterToggleButton.IsChecked = !ShowFilterToggleButton.IsChecked;
        }

        /// <summary>
        /// Binds the frequency curve data to the data grid, populating it with probability ordinates,
        /// confidence intervals, predictive values, and mode/mean estimates.
        /// </summary>
        private void BindFrequencyCurveDataGrid()
        {
            FrequencyCurveTable.ItemsSource = null;

            if (Element?.BayesianAnalysis == null)
                return;

            ModeColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

            if (Element.IsEstimated != true || Element.AnalysisResults == null)
                return;

            var curvePoints = new List<FrequencyCurvePoint>();
            for (int i = 0; i < Element.ProbabilityOrdinates.Count; i++)
            {
                var p = Element.ProbabilityOrdinates[i];
                var up = Element.AnalysisResults.ConfidenceIntervals[i, 1];
                var lo = Element.AnalysisResults.ConfidenceIntervals[i, 0];
                var prd = Element.AnalysisResults.MeanCurve[i];
                var md = Element.AnalysisResults.ModeCurve[i];
                curvePoints.Add(new FrequencyCurvePoint(p, up, lo, prd, md));
            }
            FrequencyCurveTable.ItemsSource = curvePoints;
            FrequencyCurveTable.Items.Refresh();
        }

        /// <summary>
        /// Sets the frequency curve table column headers based on the credible interval width.
        /// </summary>
        private void SetFrequencyCurveTableColumnHeaders()
        {
            if (Element?.BayesianAnalysis == null) return;
            double alpha = (1 - Element.BayesianAnalysis.CredibleIntervalWidth) / 2;
            UpperColumn.Header = ((1 - alpha) * 100).ToString("F1") + "% CI";
            LowerColumn.Header = (alpha * 100).ToString("F1") + "% CI";
        }

        /// <summary>
        /// Sets the string format for the frequency curve table columns based on user settings.
        /// </summary>
        private void SetColumnStringFormats()
        {
            // Update the column binding string format
            UpperColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            LowerColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            PredictiveColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
            ModeColumn.Binding.StringFormat = "{0:" + FrameworkUI.UserSettings.ValueStringFormat + "}";
        }

        /// <summary>
        /// Binds the summary statistics data to the data grid, populating it with distribution properties
        /// such as minimum, maximum, mean, standard deviation, skewness, and kurtosis.
        /// </summary>
        private void BindSummaryStatisticsDataGrid()
        {
            SummaryStatisticsTable.ItemsSource = null;

            if (Element == null) return;

            StatValueColumn.Header = Element.BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode";

            var summaryStats = new List<SummaryStatistic>();
            if (Element.IsEstimated == false || Element.AnalysisResults == null)
            {
                summaryStats.Add(new SummaryStatistic("Minimum", double.NaN));
                summaryStats.Add(new SummaryStatistic("Maximum", double.NaN));
                summaryStats.Add(new SummaryStatistic("Mean", double.NaN));
                summaryStats.Add(new SummaryStatistic("Std Dev", double.NaN));
                summaryStats.Add(new SummaryStatistic("Skewness", double.NaN));
                summaryStats.Add(new SummaryStatistic("Kurtosis", double.NaN));
            }
            else
            {
                // Use GetPointEstimateDistribution() instead of AnalysisResults.ParentDistribution
                // because the ParentDistribution is an empirical-CDF Mixture/CompetingRisks whose
                // moment properties (StandardDeviation, Skewness, Kurtosis) return NaN.
                var pointDist = Element.GetPointEstimateDistribution();
                if (pointDist != null)
                {
                    var min = pointDist.Minimum;
                    var max = pointDist.Maximum;
                    summaryStats.Add(new SummaryStatistic("Minimum", min < -1E12 ? double.NegativeInfinity : min));
                    summaryStats.Add(new SummaryStatistic("Maximum", max > 1E12 ? double.PositiveInfinity : max));
                    summaryStats.Add(new SummaryStatistic("Mean", pointDist.Mean));
                    summaryStats.Add(new SummaryStatistic("Std Dev", pointDist.StandardDeviation));
                    summaryStats.Add(new SummaryStatistic("Skewness", pointDist.Skewness));
                    summaryStats.Add(new SummaryStatistic("Kurtosis", pointDist.Kurtosis));
                }
                else
                {
                    summaryStats.Add(new SummaryStatistic("Minimum", double.NaN));
                    summaryStats.Add(new SummaryStatistic("Maximum", double.NaN));
                    summaryStats.Add(new SummaryStatistic("Mean", double.NaN));
                    summaryStats.Add(new SummaryStatistic("Std Dev", double.NaN));
                    summaryStats.Add(new SummaryStatistic("Skewness", double.NaN));
                    summaryStats.Add(new SummaryStatistic("Kurtosis", double.NaN));
                }
            }

            SummaryStatisticsTable.ItemsSource = summaryStats;
            SummaryStatisticsTable.Items.Refresh();
        }

        /// <summary>
        /// Handles the LoadingRow event of the SummaryStatisticsTable. Currently not performing any row customization.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataGridRowEventArgs"/> instance containing the event data.</param>
        private void SummaryStatisticsTable_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            //if (((SummaryStatistic)e.Row.DataContext).Name == "Minimum")
            //{
            //    e.Row.BorderThickness = new Thickness(0, 2, 0, 0);
            //    e.Row.BorderBrush = new SolidColorBrush(Colors.Black);
            //}
            //else
            //{
                //e.Row.BorderThickness = new Thickness(0, 0, 0, 0);
            //}

        }

    }
}
