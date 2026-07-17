using OxyPlot;
using OxyPlot.Wpf;
using OxyPlotControls;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using BarItem = OxyPlot.Series.BarItem;
using LineAnnotationType = OxyPlot.Annotations.LineAnnotationType;

namespace RMC_BestFit
{
    /// <summary>
    /// Defines the operating mode of the <see cref="InfluenceDiagnosticsControl"/>.
    /// </summary>
    public enum InfluenceControlMode
    {
        /// <summary>
        /// Bayesian mode: shows Leverage (total), Fit Influence, Variance Influence, and Leave-One-Out Diagnostic views.
        /// Requires the <see cref="InfluenceDiagnosticsControl.Analysis"/> property.
        /// </summary>
        Bayesian,

        /// <summary>
        /// GMM mode: shows Leverage (total), Fit Influence, and Variance Influence views.
        /// Requires the <see cref="InfluenceDiagnosticsControl.GMMAnalysis"/> property.
        /// </summary>
        GMM
    }

    /// <summary>
    /// A user control that displays influence diagnostics as a tornado (horizontal bar) plot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In <see cref="InfluenceControlMode.Bayesian"/> mode, supports two views:
    /// <list type="bullet">
    /// <item><description>Influence — Hessian leverage decomposition at the MAP estimate, showing
    /// each observation's and prior component's share of the total information budget (% of total).</description></item>
    /// <item><description>Leave-One-Out Diagnostic — Pareto k values from PSIS-LOO-CV,
    /// color-coded by diagnostic category with reference lines at k = 0.5, 0.7, and 1.0.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// In <see cref="InfluenceControlMode.GMM"/> mode, shows Cook's Distance from the
    /// Generalized Method of Moments estimator. No view combo box is displayed.
    /// The checkbox label changes to "Include Penalties".
    /// </para>
    /// <para>
    /// This control follows the external plot ownership pattern (Pattern B) used by all Bayesian
    /// diagnostic controls. The plot is owned by <see cref="RMC.BestFit.UI.BayesianController"/>
    /// and attached to this control via <see cref="SetPlot"/>. The <see cref="Analysis"/> dependency
    /// property binds to the <see cref="BayesianAnalysis"/> instance.
    /// </para>
    /// <para>
    /// Diagnostics are computed lazily on first view and cached until the analysis re-estimates.
    /// </para>
    /// </remarks>
    public partial class InfluenceDiagnosticsControl : UserControl
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="InfluenceDiagnosticsControl"/> class.
        /// </summary>
        public InfluenceDiagnosticsControl()
        {
            InitializeComponent();
        }

        #endregion

        #region Members

        #region Dependency Properties

        /// <summary>
        /// Identifies the <see cref="Analysis"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty AnalysisProperty = DependencyProperty.Register(
            nameof(Analysis), typeof(BayesianAnalysis), typeof(InfluenceDiagnosticsControl),
            new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis object that provides data for the influence diagnostics.
        /// Used in <see cref="InfluenceControlMode.Bayesian"/> mode.
        /// </summary>
        public BayesianAnalysis Analysis
        {
            get { return (BayesianAnalysis)GetValue(AnalysisProperty); }
            set { SetValue(AnalysisProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="Mode"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
            nameof(Mode), typeof(InfluenceControlMode), typeof(InfluenceDiagnosticsControl),
            new PropertyMetadata(InfluenceControlMode.Bayesian, ModeCallback));

        /// <summary>
        /// Gets or sets the operating mode of this control. Default is <see cref="InfluenceControlMode.Bayesian"/>.
        /// </summary>
        public InfluenceControlMode Mode
        {
            get { return (InfluenceControlMode)GetValue(ModeProperty); }
            set { SetValue(ModeProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="GMMAnalysis"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty GMMAnalysisProperty = DependencyProperty.Register(
            nameof(GMMAnalysis), typeof(GeneralizedMethodOfMoments), typeof(InfluenceDiagnosticsControl),
            new PropertyMetadata(null, GMMAnalysisCallback));

        /// <summary>
        /// Gets or sets the GMM analysis object that provides Cook's distance diagnostics.
        /// Used in <see cref="InfluenceControlMode.GMM"/> mode.
        /// </summary>
        public GeneralizedMethodOfMoments GMMAnalysis
        {
            get { return (GeneralizedMethodOfMoments)GetValue(GMMAnalysisProperty); }
            set { SetValue(GMMAnalysisProperty, value); }
        }

        #endregion

        #region Events

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
        public delegate void PlotPropertiesCalledEventHandler(Plot plot, bool openProperties,
            OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject);

        #endregion

        #region Fields

        /// <summary>
        /// The plot object — always supplied externally by the parent control via <see cref="SetPlot"/>,
        /// using <c>BayesianController.InfluenceDiagnosticsPlot</c>.
        /// </summary>
        /// <remarks>
        /// Both Bayesian and GMM modes share the same UI-layer-owned plot (Convention 1 — plot ownership
        /// belongs to the UI element, not the App control). The plot's static defaults are placeholders;
        /// the App control rewrites the plot title and axis titles dynamically via
        /// <see cref="SetPlotTitle"/> / <see cref="SetAxisTitle"/> in each <see cref="UpdatePlot"/> path,
        /// so the same Plot object serves both "Pareto k" (Bayesian LOO Diagnostic view) and
        /// "% of Total Information" (Bayesian Leverage view + every GMM view).
        /// </remarks>
        private Plot _plot;

        /// <summary>
        /// Tracks whether the control has been loaded and initialized.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// Cached observation influence diagnostics (LOO-CV Pareto k). Cleared when the analysis re-estimates.
        /// </summary>
        private InfluenceDiagnostics _influenceDiagnostics;

        /// <summary>
        /// Cached leverage diagnostics (Hessian leverage at MAP). Cleared when the analysis re-estimates.
        /// </summary>
        private LeverageDiagnostics _leverageDiagnostics;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the plot displayed by this control. Set via <see cref="SetPlot"/> or created automatically.
        /// </summary>
        public Plot Plot => _plot;

        /// <summary>
        /// Gets a value indicating whether the plot area has been clicked by the user.
        /// </summary>
        public bool PlotClicked { get; private set; } = false;

        #endregion

        #endregion

        #region Public Methods

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

        #endregion

        #region Private Methods — Event Handlers

        /// <summary>
        /// Handles property change callbacks for the <see cref="Analysis"/> dependency property.
        /// Subscribes to property change events on the new analysis object.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event data containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d is not InfluenceDiagnosticsControl thisControl) return;

            // Unsubscribe from old Analysis
            if (e.OldValue is BayesianAnalysis oldElement)
            {
                oldElement.PropertyChanged -= thisControl.Analysis_PropertyChanged;
            }

            // Clear cached diagnostics since the Analysis object changed
            thisControl._influenceDiagnostics = null;
            thisControl._leverageDiagnostics = null;

            // Subscribe to new Analysis
            if (e.NewValue is BayesianAnalysis newElement)
            {
                newElement.PropertyChanged += thisControl.Analysis_PropertyChanged;
            }

            // Update the plot for the new Analysis (handles already-estimated analyses)
            if (thisControl._isLoaded)
            {
                thisControl.UpdatePlot();
            }
        }

        /// <summary>
        /// Handles property change callbacks for the <see cref="Mode"/> dependency property.
        /// Updates the UI layout based on the new mode.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event data containing the old and new property values.</param>
        private static void ModeCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not InfluenceDiagnosticsControl thisControl) return;
            if (thisControl._isLoaded)
            {
                thisControl.UpdateControlVisibility();
                thisControl.UpdatePlot();
            }
        }

        /// <summary>
        /// Handles property change callbacks for the <see cref="GMMAnalysis"/> dependency property.
        /// Updates the plot when the GMM analysis changes.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event data containing the old and new property values.</param>
        private static void GMMAnalysisCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not InfluenceDiagnosticsControl thisControl) return;
            if (thisControl._isLoaded)
            {
                thisControl.UpdatePlot();
            }
        }

        /// <summary>
        /// Handles property changes on the Analysis object. Clears cached diagnostics and
        /// updates the plot when the estimation state changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing the name of the property that changed.</param>
        private void Analysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Analysis.IsEstimated))
            {
                // Clear cached diagnostics so they are recomputed on next view
                _influenceDiagnostics = null;
                _leverageDiagnostics = null;
                UpdatePlot();
            }
        }

        /// <summary>
        /// Handles the Loaded event of the UserControl. Initializes the control on first load
        /// by configuring view visibility and performing the initial update.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        /// <remarks>
        /// The Plot must be supplied externally by the parent control via <see cref="SetPlot"/>
        /// before this event fires (typically in the parent's ElementCallback). If <see cref="Plot"/>
        /// is null when Loaded runs, <see cref="UpdatePlot"/> exits early via its null guard
        /// and the control renders empty.
        /// </remarks>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == false)
            {
                // One-time setup: configure view visibility for the current Mode.
                UpdateControlVisibility();
            }
            _isLoaded = true;
            // Always refresh on load so Analysis/GMMAnalysis changes between unload and reload are applied.
            UpdatePlot();
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
        /// Handles the SelectionChanged event of the ViewComboBox.
        /// Updates control visibility and refreshes the plot for the selected view.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void ViewComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded == true)
            {
                UpdateControlVisibility();
                UpdatePlot();
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event of the TopNComboBox.
        /// Refreshes the plot with the new top-N filter.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void TopNComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded == true) UpdatePlot();
        }

        /// <summary>
        /// Handles the Checked/Unchecked events of the IncludePriorsCheckBox.
        /// Refreshes the Influence view to include or exclude prior components.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void IncludePriorsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == true) UpdatePlot();
        }

        /// <summary>
        /// Handles the PropertiesCalled event from the plot toolbar.
        /// Forwards the event to subscribers of the PlotPropertiesCalled event.
        /// </summary>
        /// <param name="targetPlot">The plot whose properties are being accessed.</param>
        /// <param name="openProperties">True if the properties panel should be opened.</param>
        /// <param name="propertyExpander">The property expander control to display.</param>
        /// <param name="selectedObject">The object whose properties are being displayed.</param>
        private void PlotToolbar_PropertiesCalled(Plot targetPlot, bool openProperties,
            OxyPlotPropertiesControl.PropertyEXP? propertyExpander, object selectedObject)
        {
            PlotPropertiesCalled?.Invoke(targetPlot, openProperties, propertyExpander, selectedObject);
        }

        #endregion

        #region Private Methods — Plot Update

        /// <summary>
        /// Main dispatcher that routes to the appropriate view-specific update method
        /// based on the current mode and ViewComboBox selection.
        /// </summary>
        /// <remarks>
        /// View indices for Bayesian mode: 0=Leverage (total), 1=Fit Influence, 2=Variance Influence, 3=LOO Diagnostic.
        /// View indices for GMM mode: 0=Leverage, 1=Fit Influence, 2=Variance Influence.
        /// </remarks>
        private void UpdatePlot()
        {
            if (_plot == null) return;

            if (Mode == InfluenceControlMode.GMM)
            {
                int viewIndex = ViewComboBox.SelectedIndex;
                switch (viewIndex)
                {
                    case 1: UpdateGMMFitInfluencePlot(); break;
                    case 2: UpdateGMMVarianceInfluencePlot(); break;
                    default: UpdateGMMInfluencePlot(); break;
                }
            }
            else
            {
                int viewIndex = ViewComboBox.SelectedIndex;
                switch (viewIndex)
                {
                    case 1: UpdateFitInfluencePlot(); break;
                    case 2: UpdateVarianceInfluencePlot(); break;
                    case 3: UpdateLOODiagnosticPlot(); break;
                    default: UpdateLeveragePlot(); break;
                }
            }

            _plot.InvalidatePlot(true);
        }

        /// <summary>
        /// Updates the plot to display the Influence view (Hessian leverage decomposition at MAP).
        /// Shows each observation's and prior component's share of the total information budget
        /// as a percentage. Observation bars use a single blue series; prior bars are colored by
        /// <see cref="PriorComponentType"/>. The "Include Prior Components" checkbox toggles prior bar visibility.
        /// </summary>
        private void UpdateLeveragePlot()
        {
            ClearPlot();
            SetPlotTitle("Leverage (% of Total Information)");
            SetAxisTitle("YAxis", "");
            SetAxisTitle("XAxis", "% of Total Information");

            if (Analysis == null || Analysis.IsEstimated == false || Analysis.Results == null)
            {
                SummaryText.Text = "";
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var diagnostics = ComputeAndCacheLeverageDiagnostics();
                if (diagnostics == null || diagnostics.Count == 0)
                {
                    SummaryText.Text = "Leverage diagnostics could not be computed.";
                    return;
                }

                bool includePriors = IncludePriorsCheckBox.IsChecked == true
                    && diagnostics.PriorComponents != null
                    && diagnostics.PriorComponents.Length > 0;

                int topN = GetTopN();

                // Build combined list of ALL items (observations + priors) for unified ranking
                var combinedItems = new List<LeverageBarItem>();

                // Add ALL observations (topN applied after combining with priors)
                foreach (var obs in diagnostics.Observations)
                {
                    combinedItems.Add(new LeverageBarItem
                    {
                        Label = FormatDataLabel(obs.DataType, obs.Name, obs.Index, obs.Value),
                        Leverage = obs.Leverage,
                        PercentOfTotal = obs.PercentOfTotal,
                        IsObservation = true,
                        PriorType = PriorComponentType.ParameterPrior
                    });
                }

                // Prior components (if checkbox checked)
                if (includePriors)
                {
                    foreach (var comp in diagnostics.PriorComponents)
                    {
                        combinedItems.Add(new LeverageBarItem
                        {
                            Label = comp.Name,
                            Leverage = comp.Leverage,
                            PercentOfTotal = comp.PercentOfTotal,
                            IsObservation = false,
                            PriorType = comp.Type
                        });
                    }
                }

                // Sort all items together by leverage descending, take top N, then reverse
                // for display (most influential at top of tornado chart).
                // TopN applies to the combined list so priors appear at their true rank.
                var displayOrder = combinedItems
                    .OrderByDescending(c => c.Leverage)
                    .Take(topN)
                    .Reverse()
                    .ToArray();

                // Get the CategoryAxis and clear labels
                var categoryAxis = GetCategoryAxis();
                if (categoryAxis == null) return;
                categoryAxis.ItemsSource = null;

                // Create series: one for observations, one for all priors/penalties
                var obsSeries = CreateBarSeries("Observations", Color.FromArgb(75, 220, 20, 60), Color.FromArgb(255, 255, 0, 0));
                var priorSeries = CreateBarSeries("Priors", Color.FromArgb(125, 104, 140, 175), Color.FromArgb(255, 53, 59, 122));

                for (int i = 0; i < displayOrder.Length; i++)
                {
                    var item = displayOrder[i];
                    categoryAxis.Labels.Add(item.Label);

                    if (item.IsObservation)
                        obsSeries.Items.Add(new BarItem { Value = item.PercentOfTotal, CategoryIndex = i });
                    else
                        priorSeries.Items.Add(new BarItem { Value = item.PercentOfTotal, CategoryIndex = i });
                }

                if (obsSeries.Items.Count > 0)
                    _plot.Series.Add(obsSeries);
                if (includePriors && priorSeries.Items.Count > 0)
                    _plot.Series.Add(priorSeries);

                // Update summary text
                double obsPct = diagnostics.TotalLeverage > 0
                    ? diagnostics.TotalObservationLeverage / diagnostics.TotalLeverage * 100.0
                    : 0;
                double priorPct = diagnostics.TotalLeverage > 0
                    ? diagnostics.TotalPriorLeverage / diagnostics.TotalLeverage * 100.0
                    : 0;
                SummaryText.Text = $"p = {diagnostics.NumberOfParameters}. " +
                    $"Data: {obsPct:F1}% of total information. " +
                    $"Priors: {priorPct:F1}%.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Leverage plot update failed: {ex.Message}");
                SummaryText.Text = "Error computing leverage diagnostics.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Updates the plot to display the Fit Influence view (Cook's Distance) for Bayesian mode.
        /// Bars show each observation's and prior component's fit influence, measuring how much
        /// removing the component shifts the MAP parameters. Sorted by fit influence descending.
        /// </summary>
        private void UpdateFitInfluencePlot()
        {
            UpdateDecomposedView(isFitView: true, isGMM: false);
        }

        /// <summary>
        /// Updates the plot to display the Variance Influence view for Bayesian mode.
        /// Bars show each observation's and prior component's variance influence, measuring how much
        /// the component contributes to posterior precision. Sorted by variance influence descending.
        /// </summary>
        private void UpdateVarianceInfluencePlot()
        {
            UpdateDecomposedView(isFitView: false, isGMM: false);
        }

        /// <summary>
        /// Updates the plot to display the Fit Influence view for GMM mode.
        /// </summary>
        private void UpdateGMMFitInfluencePlot()
        {
            UpdateDecomposedView(isFitView: true, isGMM: true);
        }

        /// <summary>
        /// Updates the plot to display the Variance Influence view for GMM mode.
        /// </summary>
        private void UpdateGMMVarianceInfluencePlot()
        {
            UpdateDecomposedView(isFitView: false, isGMM: true);
        }

        /// <summary>
        /// Shared implementation for Fit Influence and Variance Influence views across both modes.
        /// </summary>
        /// <param name="isFitView">True for Fit Influence (Cook's D), false for Variance Influence.</param>
        /// <param name="isGMM">True for GMM mode, false for Bayesian mode.</param>
        private void UpdateDecomposedView(bool isFitView, bool isGMM)
        {
            ClearPlot();
            string viewName = isFitView ? "Fit Influence" : "Variance Influence";
            SetPlotTitle(isFitView ? "Fit Influence (Cook's Distance)" : "Variance Influence");
            SetAxisTitle("YAxis", "");
            SetAxisTitle("XAxis", viewName);

            LeverageDiagnostics diagnostics;
            if (isGMM)
            {
                if (GMMAnalysis == null || !GMMAnalysis.IsEstimated) { SummaryText.Text = ""; return; }
                diagnostics = GMMAnalysis.GetLeverageDiagnostics();
            }
            else
            {
                if (Analysis == null || !Analysis.IsEstimated || Analysis.Results == null) { SummaryText.Text = ""; return; }
                diagnostics = ComputeAndCacheLeverageDiagnostics();
            }

            if (diagnostics == null || diagnostics.Count == 0)
            {
                SummaryText.Text = $"{viewName} could not be computed.";
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                string checkboxLabel = isGMM ? "Include Penalties" : "Include Prior Components";
                bool includePriors = IncludePriorsCheckBox.IsChecked == true
                    && diagnostics.PriorComponents != null
                    && diagnostics.PriorComponents.Length > 0;
                int topN = GetTopN();

                var combinedItems = new List<LeverageBarItem>();

                foreach (var obs in diagnostics.Observations)
                {
                    double metric = isFitView ? obs.FitInfluence : obs.VarianceInfluence;
                    combinedItems.Add(new LeverageBarItem
                    {
                        Label = FormatDataLabel(obs.DataType, obs.Name, obs.Index, obs.Value),
                        Leverage = metric,
                        PercentOfTotal = isFitView ? obs.PercentFitOfTotal : obs.PercentVarianceOfTotal,
                        IsObservation = true,
                        PriorType = PriorComponentType.ParameterPrior
                    });
                }

                if (includePriors)
                {
                    foreach (var comp in diagnostics.PriorComponents)
                    {
                        double metric = isFitView ? comp.FitInfluence : comp.VarianceInfluence;
                        combinedItems.Add(new LeverageBarItem
                        {
                            Label = comp.Name,
                            Leverage = metric,
                            PercentOfTotal = isFitView ? comp.PercentFitOfTotal : comp.PercentVarianceOfTotal,
                            IsObservation = false,
                            PriorType = comp.Type
                        });
                    }
                }

                var displayOrder = combinedItems
                    .OrderByDescending(c => c.Leverage)
                    .Take(topN)
                    .Reverse()
                    .ToArray();

                var categoryAxis = GetCategoryAxis();
                if (categoryAxis == null) return;
                categoryAxis.ItemsSource = null;

                var obsSeries = CreateBarSeries("Observations", Color.FromArgb(75, 220, 20, 60), Color.FromArgb(255, 255, 0, 0));
                string priorLabel = isGMM ? "Penalties" : "Priors";
                var priorSeries = CreateBarSeries(priorLabel, Color.FromArgb(125, 104, 140, 175), Color.FromArgb(255, 53, 59, 122));

                for (int i = 0; i < displayOrder.Length; i++)
                {
                    var item = displayOrder[i];
                    categoryAxis.Labels.Add(item.Label);

                    if (item.IsObservation)
                        obsSeries.Items.Add(new BarItem { Value = item.Leverage, CategoryIndex = i });
                    else
                        priorSeries.Items.Add(new BarItem { Value = item.Leverage, CategoryIndex = i });
                }

                if (obsSeries.Items.Count > 0)
                    _plot.Series.Add(obsSeries);
                if (includePriors && priorSeries.Items.Count > 0)
                    _plot.Series.Add(priorSeries);

                string description = isFitView
                    ? "Fit influence shows how much each component shifts the best-fit parameters (Cook's Distance)."
                    : "Variance influence shows how much each component contributes to posterior precision.";
                SummaryText.Text = $"p = {diagnostics.NumberOfParameters}. {description}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{viewName} plot update failed: {ex.Message}");
                SummaryText.Text = $"Error computing {viewName.ToLower()}.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Updates the plot to display the Leave-One-Out Diagnostic view.
        /// Bars show the LOO predictive surprise |ELPD-LOO_i| for each observation, sorted by magnitude.
        /// Bars are color-coded by the Pareto k diagnostic category: green (k &lt; 0.5) indicates the
        /// PSIS approximation is reliable, while orange/red indicate potential issues.
        /// </summary>
        /// <remarks>
        /// ELPD-LOO_i is the expected log predictive density contribution for observation i under
        /// leave-one-out cross-validation. More negative values mean the model has more difficulty
        /// predicting that observation — indicating higher influence or potential outliers.
        /// The absolute value is displayed so bars always point right, with larger bars indicating
        /// harder-to-predict observations.
        /// </remarks>
        private void UpdateLOODiagnosticPlot()
        {
            ClearPlot();
            SetPlotTitle("Leave-One-Out Predictive Surprise");
            SetAxisTitle("YAxis", "Observations");
            SetAxisTitle("XAxis", "LOO Predictive Surprise (|\u0112LPD|)");

            if (Analysis == null || Analysis.IsEstimated == false || Analysis.Results == null)
            {
                SummaryText.Text = "";
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var diagnostics = ComputeAndCacheObservationDiagnostics();
                if (diagnostics == null || diagnostics.Count == 0)
                {
                    SummaryText.Text = "Leave-one-out diagnostics could not be computed.";
                    return;
                }

                int topN = GetTopN();

                // Sort by |ELPD-LOO| descending (most surprising first), then take top N
                var sorted = diagnostics.Observations
                    .OrderByDescending(o => Math.Abs(o.ElpdLoo))
                    .Take(Math.Min(topN, diagnostics.Count))
                    .ToArray();

                // Reverse so the most surprising observation appears at the top of the plot
                var displayOrder = sorted.Reverse().ToArray();

                // Get the CategoryAxis and clear labels
                var categoryAxis = GetCategoryAxis();
                if (categoryAxis == null) return;
                categoryAxis.ItemsSource = null;

                // Create one BarSeries per ParetoKCategory for legend support
                var seriesGood = CreateBarSeries("Good (k < 0.5)", GetBarFillForCategory(ParetoKCategory.Good), GetBarStrokeForCategory(ParetoKCategory.Good));
                var seriesOK = CreateBarSeries("OK (0.5 \u2264 k < 0.7)", GetBarFillForCategory(ParetoKCategory.OK), GetBarStrokeForCategory(ParetoKCategory.OK));
                var seriesBad = CreateBarSeries("Bad (0.7 \u2264 k < 1.0)", GetBarFillForCategory(ParetoKCategory.Bad), GetBarStrokeForCategory(ParetoKCategory.Bad));
                var seriesVeryBad = CreateBarSeries("Very Bad (k \u2265 1.0)", GetBarFillForCategory(ParetoKCategory.VeryBad), GetBarStrokeForCategory(ParetoKCategory.VeryBad));

                // Pre-fill all series with zero values, then set the actual value in the correct series
                for (int i = 0; i < displayOrder.Length; i++)
                {
                    var obs = displayOrder[i];
                    categoryAxis.Labels.Add(GetObservationLabel(obs));

                    double absElpd = Math.Abs(obs.ElpdLoo);
                    var emptyItem = new BarItem { Value = 0 };
                    var valueItem = new BarItem { Value = absElpd };

                    switch (obs.Category)
                    {
                        case ParetoKCategory.Good:
                            seriesGood.Items.Add(valueItem);
                            seriesOK.Items.Add(emptyItem);
                            seriesBad.Items.Add(emptyItem);
                            seriesVeryBad.Items.Add(emptyItem);
                            break;
                        case ParetoKCategory.OK:
                            seriesGood.Items.Add(emptyItem);
                            seriesOK.Items.Add(valueItem);
                            seriesBad.Items.Add(emptyItem);
                            seriesVeryBad.Items.Add(emptyItem);
                            break;
                        case ParetoKCategory.Bad:
                            seriesGood.Items.Add(emptyItem);
                            seriesOK.Items.Add(emptyItem);
                            seriesBad.Items.Add(valueItem);
                            seriesVeryBad.Items.Add(emptyItem);
                            break;
                        case ParetoKCategory.VeryBad:
                            seriesGood.Items.Add(emptyItem);
                            seriesOK.Items.Add(emptyItem);
                            seriesBad.Items.Add(emptyItem);
                            seriesVeryBad.Items.Add(valueItem);
                            break;
                    }
                }

                // Only add series that have at least one non-zero value (for a clean legend)
                if (seriesGood.Items.Any(item => item.Value != 0)) _plot.Series.Add(seriesGood);
                if (seriesOK.Items.Any(item => item.Value != 0)) _plot.Series.Add(seriesOK);
                if (seriesBad.Items.Any(item => item.Value != 0)) _plot.Series.Add(seriesBad);
                if (seriesVeryBad.Items.Any(item => item.Value != 0)) _plot.Series.Add(seriesVeryBad);

                // Update summary text with both LOO and Pareto k information
                SummaryText.Text = $"{diagnostics.Count} observations. " +
                    $"Pareto k: {diagnostics.CountParetoKAbove07} with k \u2265 0.7, " +
                    $"{diagnostics.CountParetoKAbove05} with k \u2265 0.5. " +
                    diagnostics.GetReliabilitySummary();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LOO diagnostic plot update failed: {ex.Message}");
                SummaryText.Text = "Error computing leave-one-out diagnostics.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Updates the plot to display GMM Cook's Distance as a tornado chart.
        /// Shows each observation's Cook's Distance sorted descending.
        /// The "Include Penalties" checkbox controls inclusion of penalty components.
        /// </summary>
        private void UpdateGMMInfluencePlot()
        {
            ClearPlot();
            SetPlotTitle("Leverage (% of Total Information)");
            SetAxisTitle("YAxis", "");
            SetAxisTitle("XAxis", "% of Total Information");

            if (GMMAnalysis == null || GMMAnalysis.IsEstimated == false)
            {
                SummaryText.Text = "";
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var diagnostics = GMMAnalysis.GetLeverageDiagnostics();
                if (diagnostics == null || diagnostics.Count == 0)
                {
                    SummaryText.Text = "Leverage diagnostics could not be computed.";
                    return;
                }

                bool includePriors = IncludePriorsCheckBox.IsChecked == true
                    && diagnostics.PriorComponents != null
                    && diagnostics.PriorComponents.Length > 0;

                int topN = GetTopN();

                // Build combined list of ALL items (observations + penalties) for unified ranking
                var combinedItems = new List<LeverageBarItem>();

                foreach (var obs in diagnostics.Observations)
                {
                    combinedItems.Add(new LeverageBarItem
                    {
                        Label = FormatDataLabel(obs.DataType, obs.Name, obs.Index, obs.Value),
                        Leverage = obs.Leverage,
                        PercentOfTotal = obs.PercentOfTotal,
                        IsObservation = true,
                        PriorType = PriorComponentType.ParameterPrior
                    });
                }

                if (includePriors)
                {
                    foreach (var comp in diagnostics.PriorComponents)
                    {
                        combinedItems.Add(new LeverageBarItem
                        {
                            Label = comp.Name,
                            Leverage = comp.Leverage,
                            PercentOfTotal = comp.PercentOfTotal,
                            IsObservation = false,
                            PriorType = comp.Type
                        });
                    }
                }

                // Sort by leverage descending, take top N, reverse for display
                var displayOrder = combinedItems
                    .OrderByDescending(c => c.Leverage)
                    .Take(topN)
                    .Reverse()
                    .ToArray();

                // Get the CategoryAxis and clear labels
                var categoryAxis = GetCategoryAxis();
                if (categoryAxis == null) return;
                categoryAxis.ItemsSource = null;

                // Create series: one for observations, one for all penalties
                var obsSeries = CreateBarSeries("Observations", Color.FromArgb(75, 220, 20, 60), Color.FromArgb(255, 255, 0, 0));
                var priorSeries = CreateBarSeries("Penalties", Color.FromArgb(125, 104, 140, 175), Color.FromArgb(255, 53, 59, 122));

                for (int i = 0; i < displayOrder.Length; i++)
                {
                    var item = displayOrder[i];
                    categoryAxis.Labels.Add(item.Label);

                    if (item.IsObservation)
                        obsSeries.Items.Add(new BarItem { Value = item.PercentOfTotal, CategoryIndex = i });
                    else
                        priorSeries.Items.Add(new BarItem { Value = item.PercentOfTotal, CategoryIndex = i });
                }

                if (obsSeries.Items.Count > 0)
                    _plot.Series.Add(obsSeries);
                if (includePriors && priorSeries.Items.Count > 0)
                    _plot.Series.Add(priorSeries);

                // Summary
                double obsPct = diagnostics.TotalLeverage > 0
                    ? diagnostics.TotalObservationLeverage / diagnostics.TotalLeverage * 100.0
                    : 0;
                double priorPct = diagnostics.TotalLeverage > 0
                    ? diagnostics.TotalPriorLeverage / diagnostics.TotalLeverage * 100.0
                    : 0;
                SummaryText.Text = $"p = {diagnostics.NumberOfParameters}. " +
                    $"Data: {obsPct:F1}% of total information. " +
                    $"Penalties: {priorPct:F1}%.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GMM influence plot update failed: {ex.Message}");
                SummaryText.Text = "Error computing leverage diagnostics.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        #endregion

        #region Private Methods — Helpers

        /// <summary>
        /// Updates the visibility of option bar controls based on the current mode and selected view.
        /// GMM mode shows 3 views (Leverage, Fit, Variance). Bayesian mode shows 4 (+ LOO Diagnostic).
        /// The "Include Prior Components" / "Include Penalties" checkbox is visible for Leverage/Fit/Variance views.
        /// </summary>
        private void UpdateControlVisibility()
        {
            if (Mode == InfluenceControlMode.GMM)
            {
                // GMM mode: show combo with 3 views, hide LOO item
                ViewLabel.Visibility = Visibility.Visible;
                ViewComboBox.Visibility = Visibility.Visible;

                // Hide the LOO Diagnostic item (index 3) if it exists — GMM only shows first 3
                if (ViewComboBox.Items.Count > 3)
                    ((ComboBoxItem)ViewComboBox.Items[3]).Visibility = Visibility.Collapsed;

                TopNLabel.Visibility = Visibility.Visible;
                TopNComboBox.Visibility = Visibility.Visible;

                int viewIndex = ViewComboBox.SelectedIndex;
                IncludePriorsCheckBox.Visibility = (viewIndex <= 2) ? Visibility.Visible : Visibility.Collapsed;
                IncludePriorsCheckBox.Content = "Include Penalties";
            }
            else
            {
                // Bayesian mode: all 4 views visible
                ViewLabel.Visibility = Visibility.Visible;
                ViewComboBox.Visibility = Visibility.Visible;

                if (ViewComboBox.Items.Count > 3)
                    ((ComboBoxItem)ViewComboBox.Items[3]).Visibility = Visibility.Visible;

                int viewIndex = ViewComboBox.SelectedIndex;

                TopNLabel.Visibility = Visibility.Visible;
                TopNComboBox.Visibility = Visibility.Visible;

                // Include Priors checkbox: visible for Leverage (0), Fit (1), Variance (2), hidden for LOO (3)
                IncludePriorsCheckBox.Visibility = (viewIndex <= 2) ? Visibility.Visible : Visibility.Collapsed;
                IncludePriorsCheckBox.Content = "Include Prior Components";
            }
        }

        /// <summary>
        /// Clears all series, annotations, and category axis labels from the plot.
        /// </summary>
        private void ClearPlot()
        {
            _plot.Series.Clear();
            _plot.Annotations.Clear();

            var categoryAxis = GetCategoryAxis();
            if (categoryAxis != null)
            {
                categoryAxis.ItemsSource = null;
                // Assign a fresh Labels list since the default is null
                categoryAxis.Labels = new List<string>();
            }
        }

        /// <summary>
        /// Gets the <see cref="CategoryAxis"/> from the plot's axes collection.
        /// </summary>
        /// <returns>The CategoryAxis, or null if not found.</returns>
        private CategoryAxis GetCategoryAxis()
        {
            return _plot.Axes.OfType<CategoryAxis>().FirstOrDefault();
        }

        /// <summary>
        /// Sets the title of a named axis, using <c>SuppressPropertyChanged</c> to prevent
        /// plot undo tracking from recording this as an undoable
        /// "Change Title" action. Axis titles are driven by the view selection and are not user customizations.
        /// </summary>
        /// <param name="axisName">The Name property of the axis to update.</param>
        /// <param name="title">The new axis title.</param>
        private void SetAxisTitle(string axisName, string title)
        {
            var axis = _plot.Axes.FirstOrDefault(a => a.Name == axisName);
            if (axis != null)
            {
                axis.SuppressPropertyChanged = true;
                axis.Title = title;
                axis.SuppressPropertyChanged = false;
            }
        }

        /// <summary>
        /// Sets the plot title, using <c>SuppressPropertyChanged</c> to prevent
        /// plot undo tracking from recording this as an undoable
        /// action. The plot title is driven by the ViewComboBox selection and is not a user customization.
        /// </summary>
        /// <param name="title">The new plot title.</param>
        private void SetPlotTitle(string title)
        {
            if (_plot == null) return;
            _plot.SuppressPropertyChanged = true;
            _plot.Title = title;
            _plot.SuppressPropertyChanged = false;
        }

        /// <summary>
        /// Lazily computes and caches the observation influence diagnostics (LOO-CV with PSIS).
        /// Returns the cached result if already computed.
        /// </summary>
        /// <returns>The <see cref="InfluenceDiagnostics"/>, or null if computation fails.</returns>
        private InfluenceDiagnostics ComputeAndCacheObservationDiagnostics()
        {
            if (_influenceDiagnostics == null && Analysis?.IsEstimated == true)
            {
                try
                {
                    _influenceDiagnostics = Analysis.ComputeInfluenceDiagnostics();
                }
                catch (Exception ex)
                {
                    // E3: Log full exception (including stack trace) so root cause is visible in debug output.
                    Debug.WriteLine($"Influence diagnostics computation failed ({ex.GetType().Name}): {ex}");
                }
            }
            return _influenceDiagnostics;
        }

        /// <summary>
        /// Lazily computes and caches the leverage diagnostics (Hessian leverage at MAP).
        /// Returns the cached result if already computed.
        /// </summary>
        /// <returns>The <see cref="LeverageDiagnostics"/>, or null if computation fails.</returns>
        private LeverageDiagnostics ComputeAndCacheLeverageDiagnostics()
        {
            if (_leverageDiagnostics == null && Analysis?.IsEstimated == true)
            {
                try
                {
                    _leverageDiagnostics = Analysis.ComputeLeverageDiagnostics();
                }
                catch (Exception ex)
                {
                    // E3: Log full exception (including stack trace) so root cause is visible in debug output.
                    Debug.WriteLine($"Leverage diagnostics computation failed ({ex.GetType().Name}): {ex}");
                }
            }
            return _leverageDiagnostics;
        }

        /// <summary>
        /// Parses the current TopNComboBox selection to get the maximum number of items to display.
        /// </summary>
        /// <returns>The top-N count, or <see cref="int.MaxValue"/> for "All".</returns>
        private int GetTopN()
        {
            if (TopNComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string content = selectedItem.Content?.ToString() ?? "20";
                if (content == "All") return int.MaxValue;
                if (int.TryParse(content, out int n)) return n;
            }
            return 20;
        }

        /// <summary>
        /// Formats a data label with type prefix, index, and value for any observation type.
        /// </summary>
        /// <param name="dataType">The data component type (Exact, Interval, etc.).</param>
        /// <param name="name">The observation name (typically the time index).</param>
        /// <param name="index">The zero-based observation index.</param>
        /// <param name="value">The representative observation value.</param>
        /// <returns>A formatted label like "Exact - 1951 - 135" or "Threshold - 1600 - 300".</returns>
        private static string FormatDataLabel(DataComponentType dataType, string name, int index, double value)
        {
            string typeLabel = dataType switch
            {
                DataComponentType.Exact => "Exact",
                DataComponentType.Uncertain => "Uncertain",
                DataComponentType.Interval => "Interval",
                DataComponentType.LeftCensored => "Threshold",
                DataComponentType.RightCensored => "Threshold",
                _ => "Obs"
            };
            string indexLabel = !string.IsNullOrEmpty(name) ? name : index.ToString();
            return $"{typeLabel} - {indexLabel} - {value:G4}";
        }

        /// <summary>
        /// Formats the Y-axis label for a single observation influence entry.
        /// Delegates to <see cref="FormatDataLabel"/> with the observation's component type, name, index, and value.
        /// </summary>
        /// <param name="obs">The observation influence data to label.</param>
        /// <returns>A formatted label string such as "Exact - 1951 - 135".</returns>
        private static string GetObservationLabel(ObservationInfluence obs)
        {
            return FormatDataLabel(obs.DataType, obs.Name, obs.Index, obs.Value);
        }

        /// <summary>
        /// Creates a new <see cref="BarSeries"/> with the specified title and fill color.
        /// Configures the series for horizontal bar display with the standard tracker format.
        /// </summary>
        /// <param name="title">The legend title for the series.</param>
        /// <param name="fillColor">The WPF fill color for the bars.</param>
        /// <param name="strokeColor">The WPF stroke color for the bar outlines.</param>
        /// <returns>A configured <see cref="BarSeries"/>.</returns>
        private static BarSeries CreateBarSeries(string title, Color fillColor, Color strokeColor)
        {
            return new BarSeries
            {
                Title = title,
                FillColor = fillColor,
                StrokeColor = strokeColor,
                StrokeThickness = 1,
                IsStacked = false,
                TrackerFormatString = "{0}" + Environment.NewLine + "{1}" + Environment.NewLine + "Value: {2:F4}"
            };
        }

        /// <summary>
        /// Adds a vertical dashed reference line annotation to the plot at the specified X value.
        /// </summary>
        /// <param name="x">The X-axis position of the reference line.</param>
        /// <param name="text">The label text for the annotation.</param>
        /// <param name="color">The WPF color of the reference line.</param>
        private void AddVerticalReferenceLine(double x, string text, Color color)
        {
            _plot.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = x,
                LineStyle = LineStyle.Dash,
                Color = color,
                Text = text,
                TextHorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                TextVerticalAlignment = System.Windows.VerticalAlignment.Top,
                FontSize = 10,
                StrokeThickness = 1.5
            });
        }

        /// <summary>
        /// Returns the bar fill color for a given <see cref="ParetoKCategory"/>.
        /// </summary>
        /// <param name="category">The Pareto k diagnostic category.</param>
        /// <returns>The WPF Color for the bar fill.</returns>
        private static Color GetBarFillForCategory(ParetoKCategory category)
        {
            switch (category)
            {
                case ParetoKCategory.Good:    return Color.FromArgb(75, 76, 175, 80);    // Green (alpha=75 matches KDE area fill)
                case ParetoKCategory.OK:      return Color.FromArgb(75, 255, 193, 7);    // Amber
                case ParetoKCategory.Bad:     return Color.FromArgb(75, 255, 152, 0);    // Orange
                case ParetoKCategory.VeryBad: return Color.FromArgb(75, 244, 67, 54);    // Red
                default:                      return Color.FromArgb(75, 158, 158, 158);  // Gray
            }
        }

        /// <summary>
        /// Returns the bar stroke color for a given <see cref="ParetoKCategory"/>.
        /// </summary>
        /// <param name="category">The Pareto k diagnostic category.</param>
        /// <returns>The WPF Color for the bar stroke (opaque, darker shade of the fill).</returns>
        private static Color GetBarStrokeForCategory(ParetoKCategory category)
        {
            switch (category)
            {
                case ParetoKCategory.Good:    return Color.FromArgb(255, 56, 142, 60);   // Dark green
                case ParetoKCategory.OK:      return Color.FromArgb(255, 255, 160, 0);   // Dark amber
                case ParetoKCategory.Bad:     return Color.FromArgb(255, 230, 126, 0);   // Dark orange
                case ParetoKCategory.VeryBad: return Color.FromArgb(255, 211, 47, 47);   // Dark red
                default:                      return Color.FromArgb(255, 117, 117, 117); // Dark gray
            }
        }

        #endregion

        #region Private Types

        /// <summary>
        /// Internal helper for sorting leverage items (observations and priors) together by leverage value.
        /// </summary>
        private class LeverageBarItem
        {
            /// <summary>
            /// Gets or sets the Y-axis label for this item.
            /// </summary>
            public string Label { get; set; }

            /// <summary>
            /// Gets or sets the raw leverage value (gᵀ H⁻¹ g).
            /// </summary>
            public double Leverage { get; set; }

            /// <summary>
            /// Gets or sets the leverage as a percentage of total information.
            /// </summary>
            public double PercentOfTotal { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this item represents an observation (true)
            /// or a prior component (false).
            /// </summary>
            public bool IsObservation { get; set; }

            /// <summary>
            /// Gets or sets the prior component type. Only meaningful when <see cref="IsObservation"/> is false.
            /// </summary>
            public PriorComponentType PriorType { get; set; }
        }

        #endregion
    }
}
