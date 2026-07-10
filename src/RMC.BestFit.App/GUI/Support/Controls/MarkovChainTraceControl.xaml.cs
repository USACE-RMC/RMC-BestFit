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
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying and visualizing Markov Chain Monte Carlo (MCMC) trace plots for Bayesian analysis parameters.
    /// This control provides interactive visualization of parameter chains over iterations, with options to filter by parameter and chain.
    /// </summary>
    public partial class MarkovChainTraceControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MarkovChainTraceControl"/> class.
        /// </summary>
        public MarkovChainTraceControl()
        {
            InitializeComponent();
            // Hook IsVisibleChanged so the plot is only (re)built when the trace tab is actually
            // visible. When the tab is hidden, updates are marked dirty and deferred until the user
            // switches back — matching the canonical "lazy tab-gated rendering" pattern in the project coding standards.
            this.IsVisibleChanged += UserControl_IsVisibleChanged;
        }

        /// <summary>
        /// Dependency property for the <see cref="Analysis"/> property.
        /// </summary>
        public static readonly DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(BayesianAnalysis), typeof(MarkovChainTraceControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis instance containing the Markov chain data to be visualized.
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
            if (d as MarkovChainTraceControl == null) return;
            var thisControl = (MarkovChainTraceControl)d;

            // Unsubscribe from old element
            if (e.OldValue is BayesianAnalysis oldElement)
                oldElement.PropertyChanged -= thisControl.Analysis_PropertyChanged;

            // Analysis cleared — invalidate cache so stale references are dropped, mark dirty so a
            // future drain clears the plot.
            if (e.NewValue == null)
            {
                thisControl.InvalidateTraceCache();
                thisControl._isDirty = true;
                thisControl.DrainIfReady();
                return;
            }

            var newElement = e.NewValue as BayesianAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Analysis_PropertyChanged;

            // Initialize combos and series collection without firing SelectionChanged → UpdatePlot
            // on every intermediate SelectedIndex assignment. A single drain at the end performs
            // one UpdatePlot for the coherent new state (gated on visibility by DrainIfReady).
            thisControl._suppressUpdates = true;
            try
            {
                thisControl.LoadParameterComboBox();
                thisControl.LoadChainComboBox();
                thisControl.ConstructDefaultLineSeries();
                thisControl.InvalidateTraceCache();
            }
            finally
            {
                thisControl._suppressUpdates = false;
            }

            thisControl._isDirty = true;
            thisControl.DrainIfReady();
        }

        /// <summary>
        /// Flag indicating whether the control has been loaded.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// The plot object. Injected by the parent control via <see cref="SetPlot"/>
        /// (the parent obtains it from <c>BayesianController.MarkovChainTracePlot</c>).
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
        /// List of line series representing the trace plots for each Markov chain.
        /// </summary>
        private List<LineSeries> _traceSeriesList = new List<LineSeries>();

        /// <summary>
        /// Gate for rapid-fire events during load. While true, <see cref="DrainIfReady"/> is a no-op,
        /// so <see cref="LoadParameterComboBox"/> / <see cref="LoadChainComboBox"/> assigning
        /// <c>SelectedIndex = 0</c> does not trigger redundant UpdatePlot calls.
        /// </summary>
        private bool _suppressUpdates = false;

        /// <summary>
        /// Set to true when some input (Analysis, IsEstimated, NumberOfChains, parameter or chain
        /// selection, warmup toggle) changes. Drained by <see cref="DrainIfReady"/> exactly once per
        /// coherent change batch, provided the control is loaded AND visible. Collapses the cascade
        /// of PropertyChanged events during analysis attach into a single <see cref="UpdatePlot"/> call.
        /// </summary>
        private bool _isDirty = false;

        /// <summary>
        /// Pre-built <c>DataPoint[]</c> per (parameter, chain). Indexed as
        /// <c>_traceCache[parameterIndex][chainIndex]</c>. Built once per (Analysis, WarmUp) state
        /// and reused across parameter/chain-filter switches so switching only costs an
        /// <c>ItemsSource</c> reference swap instead of a full walk of <c>MarkovChains</c>.
        /// </summary>
        private DataPoint[][][] _traceCache;

        /// <summary>
        /// The <c>startE</c> offset (warmup-excluded sample start index) that <see cref="_traceCache"/>
        /// was built for. The cache is stale if the current IncludeWarmUp state produces a different
        /// <c>startE</c>.
        /// </summary>
        private int _cachedStartE = -1;

#if DEBUG
        // Baseline instrumentation — remove once MCMC trace perf work is complete.
        private static int _updatePlotCallCount;
#endif

        /// <summary>
        /// Event handler for when the user control is loaded. Initializes combo boxes and plots.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == false)
            {
                // One-time setup: build series list and combo boxes on first load.
                _suppressUpdates = true;
                try
                {
                    LoadParameterComboBox();
                    LoadChainComboBox();
                    ConstructDefaultLineSeries();
                }
                finally
                {
                    _suppressUpdates = false;
                }
            }
            _isLoaded = true;
            // Always mark dirty so DrainIfReady refreshes on every load,
            // applying Analysis changes that occurred while the control was unloaded.
            _isDirty = true;
            DrainIfReady();
        }

        /// <summary>
        /// Event handler for visibility changes on this control. When the tab hosting the trace
        /// plot becomes visible, any pending update (marked dirty while invisible) is drained.
        /// When the tab becomes invisible, this is a no-op — updates simply accumulate as dirty.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing the new <c>IsVisible</c> value.</param>
        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
#if DEBUG && OXYPLOT_SOURCE_DIAGNOSTICS
            // Each Debug.WriteLine emitted by the OxyPlot wheel-stack trace adds ~0.5–1ms of
            // VS-Output dispatch overhead. With ~50 lines per wheel tick, that masked the real
            // perf and made the plot ~30–50ms slower per interaction during investigation.
            // Re-enable manually (set both flags to true) only when actively diagnosing.
            const bool enableTraceDiagnostics = false;
            if (enableTraceDiagnostics && this.IsVisible)
            {
                OxyPlot.Wpf.PlotViewBase.InvalidatePlotDiagnosticsTitleFilter = "Trace";
                OxyPlot.Wpf.PlotViewBase.InvalidatePlotDiagnosticsEnabled = true;
                OxyPlot.Wpf.Plot.InvalidatePlotPhaseDiagnosticsEnabled = true;
            }
            else
            {
                OxyPlot.Wpf.PlotViewBase.InvalidatePlotDiagnosticsEnabled = false;
                OxyPlot.Wpf.Plot.InvalidatePlotPhaseDiagnosticsEnabled = false;
            }
#endif
            DrainIfReady();
        }

        /// <summary>
        /// Event handler for property changes in the Analysis object. Updates the control when relevant properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing the property name that changed.</param>
        private void Analysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            bool requiresCacheRebuild = false;

            if (e.PropertyName == nameof(Analysis.IsEstimated))
            {
                requiresCacheRebuild = true;
                _isDirty = true;
            }
            if (e.PropertyName == nameof(Analysis.Model) || e.PropertyName == nameof(Analysis.ParameterNames))
            {
                _suppressUpdates = true;
                try { LoadParameterComboBox(); } finally { _suppressUpdates = false; }
                requiresCacheRebuild = true;
                _isDirty = true;
            }
            if (e.PropertyName == nameof(Analysis.NumberOfChains))
            {
                _suppressUpdates = true;
                try
                {
                    LoadChainComboBox();
                    ConstructDefaultLineSeries();
                }
                finally { _suppressUpdates = false; }
                requiresCacheRebuild = true;
                _isDirty = true;
            }

            if (requiresCacheRebuild)
            {
                InvalidateTraceCache();
            }

            DrainIfReady();
        }

        /// <summary>
        /// Populates the parameter combo box with available model parameters from the analysis.
        /// </summary>
        private void LoadParameterComboBox()
        {
            if (Analysis == null || Analysis.ParameterNames == null) return;
            var parms = Analysis.ParameterNames.ToList();
            ParameterComboBox.ItemsSource = null;
            ParameterComboBox.ItemsSource = parms;
            ParameterComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Populates the chain combo box with available Markov chains from the analysis.
        /// </summary>
        private void LoadChainComboBox()
        {
            if (Analysis == null) return;
            List<string> cmbxList = new List<string>();
            cmbxList.Add("All Chains");
            for (int i = 1; i <= Analysis.NumberOfChains; i++)
                cmbxList.Add("Chain " + i.ToString());
            ChainComboBox.ItemsSource = null;
            ChainComboBox.ItemsSource = cmbxList;
            ChainComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Event handler for parameter combo box selection changes. Updates the plot to display the selected parameter.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing selection change information.</param>
        private void ParameterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressUpdates) return;
            if (_isLoaded == false) return;
            // Parameter index changes which series slice is shown but not the underlying cache shape.
            _isDirty = true;
            DrainIfReady();
        }

        /// <summary>
        /// Event handler for chain combo box selection changes. Updates the plot to display the selected chain(s).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing selection change information.</param>
        private void ChainComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressUpdates) return;
            if (_isLoaded == false) return;
            // Chain filter changes which series are added to the plot, but not the cache shape.
            _isDirty = true;
            DrainIfReady();
        }

        /// <summary>
        /// Event handler for when the "Include Warm-up" checkbox is checked. Updates the plot to include warm-up iterations.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void IncludeWarmUpCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressUpdates) return;
            if (_isLoaded == false) return;
            // Warmup toggle changes startE, which changes the DataPoint x-values and count. Cache is stale.
            InvalidateTraceCache();
            _isDirty = true;
            DrainIfReady();
        }

        /// <summary>
        /// Event handler for when the "Include Warm-up" checkbox is unchecked. Updates the plot to exclude warm-up iterations.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void IncludeWarmUpCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressUpdates) return;
            if (_isLoaded == false) return;
            InvalidateTraceCache();
            _isDirty = true;
            DrainIfReady();
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
#if DEBUG
            // Unhook previous plot's model Updated event (if any) so we don't leak.
            if (_plot != null && _plot.ActualModel != null)
            {
#pragma warning disable CS0618 // PlotModel.Updated is obsolete but still fires; used only for perf diagnostics.
                _plot.ActualModel.Updated -= OnPlotModelUpdated_DebugPerf;
#pragma warning restore CS0618
            }
#endif
            _plot = plot;
            PlotHost.Content = _plot;
            PlotToolbar.Plot = _plot;

#if DEBUG
            // Hook model Updated event to measure inter-frame intervals during zoom/pan.
            // UpdatePlot() only runs on parameter/chain changes — zoom/pan bypasses it and
            // re-renders via the WPF composition pipeline. Updated fires per render pass,
            // so logging short intervals reveals interaction frame rate.
            if (_plot != null && _plot.ActualModel != null)
            {
#pragma warning disable CS0618
                _plot.ActualModel.Updated += OnPlotModelUpdated_DebugPerf;
#pragma warning restore CS0618
                _interRenderSw = new Stopwatch();
                _interRenderSw.Start();
            }
#endif
        }

#if DEBUG
        private Stopwatch _interRenderSw;

        /// <summary>
        /// Handles the <c>DebugPerf</c> event for <c>OnPlotModelUpdated</c>.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member is wired by the WPF control or its backing event handlers.
        /// </remarks>
        private void OnPlotModelUpdated_DebugPerf(object sender, EventArgs e)
        {
            if (_interRenderSw == null) return;
            long ms = _interRenderSw.ElapsedMilliseconds;
            _interRenderSw.Restart();
            // Only log rapid successive renders (< 200ms apart) — these indicate active
            // zoom/pan interaction. Single isolated renders (load, parameter switch) are
            // already captured by the UpdatePlot instrumentation.
            if (ms > 0 && ms < 200)
            {
                Debug.WriteLine($"[MCMCTrace RENDER-interval]                        interval={ms,4}ms ({(ms > 0 ? 1000.0 / ms : 0):F1} fps if sustained)");
            }
        }
#endif


        /// <summary>
        /// Returns true if <paramref name="series"/> contains exactly the first <paramref name="count"/>
        /// items of <paramref name="expected"/> in order, by reference equality. Used by
        /// <see cref="UpdatePlot"/> to skip <see cref="OxyPlot.PlotModel.Series"/> mutation when the
        /// visible-series membership hasn't changed (only data has).
        /// </summary>
        private static bool SeriesMembershipMatches(
            System.Collections.ObjectModel.ObservableCollection<OxyPlot.Wpf.Series> series,
            System.Collections.Generic.IList<LineSeries> expected,
            int count)
        {
            if (series == null || expected == null) return false;
            if (series.Count != count) return false;
            for (int i = 0; i < count; i++)
            {
                if (!ReferenceEquals(series[i], expected[i])) return false;
            }
            return true;
        }

        /// <summary>
        /// Constructs default line series for each Markov chain with unique colors and styling.
        /// </summary>
        private void ConstructDefaultLineSeries()
        {
            if (Analysis == null) return;
            _traceSeriesList.Clear();
            var r = new Random(209);
            // TrackerFormatString is static (does not depend on Analysis state), so set it once here
            // rather than on every UpdatePlot assignment.
            const string trackerFormat = "{0}" + "\n" + "{1}: {2:0}" + "\n" + "{3}: {4:0.000000}";
            for (int i = 1; i <= Analysis.NumberOfChains; i++)
            {
                LineSeries newLineSeries = new LineSeries()
                {
                    Name = "MarkovChain_" + i.ToString(),
                    Title = "Chain " + i.ToString(),
                    LineStyle = LineStyle.Solid,
                    Color = Color.FromRgb(System.Convert.ToByte((r.Next(0, 255) + 255) / (double)2), System.Convert.ToByte((r.Next(0, 255) + 255) / (double)2), System.Convert.ToByte((r.Next(0, 255) + 255) / (double)2)),
                    StrokeThickness = 1.5,
                    // Opt this series out of tracker hit-tests entirely. Trace plots show tens of
                    // thousands of MCMC samples per chain; a per-mouse-move O(n) nearest-point
                    // scan across 20 chains saturates the UI thread. IsHitTestEnabled=false short-
                    // circuits before GetNearestPoint is ever called, eliminating the cost.
                    // (wpf-framework OxyPlot Phase 1 item 1.1.)
                    IsHitTestEnabled = false,
                    TrackerFormatString = trackerFormat,
                    // Activates wpf-framework's fused extract-decimate-transform fast path in
                    // OxyPlot.Series.LineSeries.RenderPoints. When the axes are linear (true here)
                    // and X is monotonically increasing (true here — X is iteration index), the
                    // fused path transforms only the 4 surviving points per pixel column (first,
                    // last, min, max) rather than all 1751 points per series per frame. For a 20-
                    // chain trace with a ~1000px plot that drops per-frame transforms from ~35k
                    // to ~4k and makes wheel zoom, pan, and zoom-extents ~100x cheaper.
                    Decimator = OxyPlot.Decimator.Decimate
                };
                _traceSeriesList.Add(newLineSeries);
            }
        }

        /// <summary>
        /// Invalidates the cached trace <c>DataPoint[]</c> arrays. Call when the underlying data or
        /// its sampling window (warmup inclusion) changes — the cache will be rebuilt on the next
        /// <see cref="UpdatePlot"/>.
        /// </summary>
        private void InvalidateTraceCache()
        {
            _traceCache = null;
            _cachedStartE = -1;
        }

        /// <summary>
        /// Returns whether the cached <c>DataPoint[]</c> arrays match the current Analysis +
        /// IncludeWarmUp state (and therefore can be reused).
        /// </summary>
        /// <returns><c>true</c> if <see cref="_traceCache"/> is valid for the current state.</returns>
        private bool IsTraceCacheValid()
        {
            if (_traceCache == null) return false;
            if (Analysis == null || Analysis.Results == null) return false;
            int currentStartE = IncludeWarmUpCheckBox.IsChecked == true ? 0 : Analysis.WarmupIterations - 1;
            return _cachedStartE == currentStartE;
        }

        /// <summary>
        /// Builds <see cref="_traceCache"/> — one <c>DataPoint[]</c> per (parameter, chain) —
        /// by walking <c>Analysis.Results.MarkovChains</c> exactly once for all parameters and chains
        /// at the current warmup state. Subsequent parameter / chain-filter switches then only need
        /// to swap the cached array into <c>LineSeries.ItemsSource</c>.
        /// </summary>
        private void BuildTraceCache()
        {
            if (Analysis == null || Analysis.IsEstimated == false || Analysis.Results == null)
            {
                _traceCache = null;
                return;
            }
            if (Analysis.NumberOfChains != Analysis.Results.MarkovChains.Count())
            {
                _traceCache = null;
                return;
            }

            int chainCount = Analysis.NumberOfChains;
            // Infer parameter count from the first sample of the first chain.
            var firstChain = Analysis.Results.MarkovChains[0];
            if (firstChain == null || firstChain.Count == 0 || firstChain[0].Values == null)
            {
                _traceCache = null;
                return;
            }
            int paramCount = firstChain[0].Values.Length;
            int startE = IncludeWarmUpCheckBox.IsChecked == true ? 0 : Analysis.WarmupIterations - 1;
            int count = Analysis.Iterations - startE;
            if (count <= 0)
            {
                _traceCache = null;
                return;
            }

            var cache = new DataPoint[paramCount][][];
            for (int p = 0; p < paramCount; p++)
            {
                cache[p] = new DataPoint[chainCount][];
            }
            // Chain-major outer loop: each chain's samples are visited once, filling all parameter
            // slots in that chain. This keeps the inner access pattern cache-friendly (one
            // List<ParameterSet> indexer dereference per sample, then write all parameters).
            for (int c = 0; c < chainCount; c++)
            {
                var chain = Analysis.Results.MarkovChains[c];
                // Pre-allocate all parameter arrays for this chain.
                var chainArrays = new DataPoint[paramCount][];
                for (int p = 0; p < paramCount; p++)
                {
                    chainArrays[p] = new DataPoint[count];
                }
                for (int j = 0; j < count; j++)
                {
                    int idx = startE + j;
                    var values = chain[idx].Values;
                    double x = idx + 1;
                    if (values == null)
                    {
                        for (int p = 0; p < paramCount; p++)
                        {
                            chainArrays[p][j] = new DataPoint(x, 0);
                        }
                    }
                    else
                    {
                        for (int p = 0; p < paramCount; p++)
                        {
                            chainArrays[p][j] = new DataPoint(x, values[p]);
                        }
                    }
                }
                for (int p = 0; p < paramCount; p++)
                {
                    cache[p][c] = chainArrays[p];
                }
            }

            _traceCache = cache;
            _cachedStartE = startE;
        }

        /// <summary>
        /// Coalescing drain: if <see cref="_isDirty"/> is set AND the control is loaded AND visible,
        /// clears the dirty flag and calls <see cref="UpdatePlot"/> exactly once. Otherwise a no-op.
        /// This is the single seam through which all change signals flow, ensuring cascading
        /// PropertyChanged / SelectionChanged events collapse into one paint per coherent state.
        /// </summary>
        /// <param name="trigger">Name of the caller (auto-filled by the compiler via
        /// <see cref="CallerMemberNameAttribute"/>). Used only for Debug-only perf logging.</param>
        private void DrainIfReady([CallerMemberName] string trigger = "")
        {
            if (_suppressUpdates) return;
            if (!_isDirty) return;
            if (!_isLoaded) return;
            if (!IsVisible) return;
            _isDirty = false;
            UpdatePlot(trigger);
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
        /// Updates the plot with the latest Markov chain trace data based on current selection criteria.
        /// Displays trace plots for the selected parameter and chain(s), optionally including warm-up iterations.
        /// </summary>
        /// <param name="trigger">Name of the calling method (auto-filled by the compiler via CallerMemberName). Used only for Debug-only perf logging.</param>
        private void UpdatePlot([CallerMemberName] string trigger = "")
        {
            if (_plot == null) return;

#if DEBUG
            int callId = System.Threading.Interlocked.Increment(ref _updatePlotCallCount);
            var totalSw = Stopwatch.StartNew();
            long extractMs = -1;
            long invalidateMs = -1;
            int chainsRendered = 0;
            int totalPoints = 0;
            int pointsPerChain = 0;
            string exitReason = "ok";
#endif

            // Set plot title and axis title to reflect the selected parameter. Suppress
            // PropertyChanged so the PlotUndoManager does not record these as undoable
            // actions — they track combo selection, not user intent.
            string paramName = ParameterComboBox.SelectedValue as string ?? "";
            SetPlotTitle(string.IsNullOrEmpty(paramName) ? "Trace" : $"Trace of {paramName}");
            foreach (var axis in _plot.Axes)
            {
                if (axis.Position == OxyPlot.Axes.AxisPosition.Left)
                {
                    axis.SuppressPropertyChanged = true;
                    axis.Title = paramName;
                    axis.SuppressPropertyChanged = false;
                }
            }

            // Suppress notifications during bulk series update to prevent per-series re-renders
            _plot.SuppressPropertyChanged = true;
            try
            {
                // Note: deliberately do NOT clear _plot.Series here. Clearing and re-adding
                // every parameter switch / warmup toggle triggers WPF logical-tree manipulation
                // for each chain (20 series × 6 inherited DPs = 120 DP updates per click). The
                // series objects in _traceSeriesList are reused across UpdatePlot calls; their
                // ItemsSource is swapped in place below. We only mutate _plot.Series when the
                // chain count or visible-chain selection actually changes.
                _plot.Annotations.Clear();

                if (Analysis == null || Analysis.IsEstimated == false || Analysis.Results == null)
                {
#if DEBUG
                    exitReason = Analysis == null ? "no-analysis"
                        : Analysis.IsEstimated == false ? "not-estimated"
                        : "no-results";
#endif
                    // No data — clear any series that may be present from a prior session.
                    if (_plot.Series.Count > 0)
                    {
                        _plot.Series.Clear();
                    }
                    return;
                }

                if (Analysis.IsEstimated == true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    try
                    {
                    bool chainCountChanged = _traceSeriesList.Count != Analysis.NumberOfChains;
                    if (chainCountChanged)
                    {
                        ConstructDefaultLineSeries();
                        // Chain count changed → cache shape no longer matches; force rebuild below.
                        InvalidateTraceCache();
                    }

                    // FIX this HACK: It shouldn't happen but is
                    if (Analysis.NumberOfChains != Analysis.Results.MarkovChains.Count())
                    {
#if DEBUG
                        exitReason = "chain-count-mismatch";
#endif
                        return;
                    }

#if DEBUG
                    var extractSw = Stopwatch.StartNew();
#endif
                    if (!IsTraceCacheValid())
                    {
                        BuildTraceCache();
                    }
                    if (_traceCache == null)
                    {
#if DEBUG
                        exitReason = "cache-build-failed";
#endif
                        return;
                    }

                    int parameterIndex = Math.Max(0, ParameterComboBox.SelectedIndex);
                    int chainIndex = Math.Max(0, ChainComboBox.SelectedIndex);

                    // Defensive bounds check in case the cache was built for a different param count
                    // (e.g., during an in-flight Analysis.Model switch).
                    if (parameterIndex >= _traceCache.Length)
                    {
#if DEBUG
                        exitReason = "param-index-out-of-range";
#endif
                        return;
                    }

                    if (chainIndex == 0)
                    {
                        var paramCache = _traceCache[parameterIndex];

                        // Update ItemsSource on every persisted series (no Series.Add traffic).
                        for (int i = 0; i < Analysis.NumberOfChains && i < paramCache.Length; i++)
                        {
                            _traceSeriesList[i].ItemsSource = paramCache[i];
#if DEBUG
                            pointsPerChain = paramCache[i]?.Length ?? 0;
                            totalPoints += pointsPerChain;
                            chainsRendered++;
#endif
                        }

                        // Reconcile _plot.Series with _traceSeriesList only when membership
                        // differs (chain count change, or previously single-chain mode).
                        if (!SeriesMembershipMatches(_plot.Series, _traceSeriesList, _traceSeriesList.Count))
                        {
                            _plot.Series.Clear();
                            for (int i = 0; i < _traceSeriesList.Count; i++)
                            {
                                _plot.Series.Add(_traceSeriesList[i]);
                            }
                        }
                    }
                    else
                    {
                        chainIndex = ChainComboBox.SelectedIndex - 1;
                        var paramCache = _traceCache[parameterIndex];
                        if (chainIndex >= 0 && chainIndex < paramCache.Length && chainIndex < _traceSeriesList.Count)
                        {
                            _traceSeriesList[chainIndex].ItemsSource = paramCache[chainIndex];

                            // Single-chain mode: reconcile _plot.Series to hold exactly that one series.
                            var only = _traceSeriesList[chainIndex];
                            if (_plot.Series.Count != 1 || !ReferenceEquals(_plot.Series[0], only))
                            {
                                _plot.Series.Clear();
                                _plot.Series.Add(only);
                            }
#if DEBUG
                            pointsPerChain = paramCache[chainIndex]?.Length ?? 0;
                            totalPoints = pointsPerChain;
                            chainsRendered = 1;
#endif
                        }
                    }

#if DEBUG
                    extractSw.Stop();
                    extractMs = extractSw.ElapsedMilliseconds;
#endif
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }
                }
            }
            finally
            {
                _plot.SuppressPropertyChanged = false;
#if DEBUG
                var invalidateSw = Stopwatch.StartNew();
#endif
                _plot.InvalidatePlot(true);
#if DEBUG
                invalidateSw.Stop();
                invalidateMs = invalidateSw.ElapsedMilliseconds;
                totalSw.Stop();
                long syncTotalMs = totalSw.ElapsedMilliseconds;
                Debug.WriteLine(
                    $"[MCMCTrace #{callId,4} {trigger,-32}] chains={chainsRendered,2} pts/chain={pointsPerChain,6} totalPts={totalPoints,7} " +
                    $"extract={extractMs,5}ms invalidate={invalidateMs,5}ms sync-total={syncTotalMs,5}ms {(exitReason == "ok" ? "" : "[" + exitReason + "]")}");

                // Async paint timing: InvalidatePlot() only QUEUES the render. The actual
                // WPF render pass (where points are transformed to screen coords and
                // StreamGeometry built) happens on the dispatcher at Render priority.
                // A continuation queued at ContextIdle priority fires AFTER the render
                // pass completes, so paintSw.Elapsed captures the queued-render cost.
                int capturedCallId = callId;
                string capturedTrigger = trigger;
                int capturedPoints = totalPoints;
                var paintSw = Stopwatch.StartNew();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    paintSw.Stop();
                    Debug.WriteLine(
                        $"[MCMCTrace #{capturedCallId,4} {capturedTrigger,-32}] async-paint={paintSw.ElapsedMilliseconds,5}ms (pts={capturedPoints})");
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
#endif
            }
        }


    }
}
