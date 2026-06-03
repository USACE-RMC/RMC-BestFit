using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Enumeration of composite distribution types.
    /// </summary>
    public enum CompositeType
    {
        /// <summary>
        /// Competing risks model - combines distributions as the maximum or minimum of random variables.
        /// </summary>
        CompetingRisks,

        /// <summary>
        /// Mixture model - combines distributions using weighted mixture.
        /// </summary>
        Mixture,

        /// <summary>
        /// Model averaging - combines distributions using information criterion-based weights.
        /// </summary>
        ModelAverage
    }

    /// <summary>
    /// Enumeration of model averaging methods.
    /// </summary>
    public enum AverageMethod
    {
        /// <summary>
        /// Akaike Information Criterion weights.
        /// </summary>
        AIC,

        /// <summary>
        /// Bayesian Information Criterion weights.
        /// </summary>
        BIC,

        /// <summary>
        /// Deviance Information Criterion weights.
        /// </summary>
        DIC,

        /// <summary>
        /// Watanabe-Akaike Information Criterion weights.
        /// </summary>
        WAIC,

        /// <summary>
        /// Leave-One-Out Cross-Validation weights via Pareto Smoothed Importance Sampling (PSIS).
        /// </summary>
        LOOIC,

        /// <summary>
        /// Equal weights for all models.
        /// </summary>
        Equal,

        /// <summary>
        /// Root Mean Square Error weights.
        /// </summary>
        RMSE
    }

    /// <summary>
    /// Performs composite distribution analysis by combining multiple univariate analyses
    /// using competing risks, mixture models, or model averaging.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This analysis combines multiple fitted univariate distributions into a single composite
    /// distribution. Three composition methods are supported:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Competing Risks:</b> Computes the distribution of the maximum (or minimum) of
    /// independent random variables, useful for combining flood sources.
    /// </description></item>
    /// <item><description>
    /// <b>Mixture:</b> Combines distributions using specified mixture weights, creating
    /// a weighted average of the component distributions.
    /// </description></item>
    /// <item><description>
    /// <b>Model Averaging:</b> Automatically computes weights based on information criteria
    /// (AIC, BIC, DIC, WAIC) or RMSE to average across model uncertainty.
    /// </description></item>
    /// </list>
    /// <para>
    /// The class propagates uncertainty from each component analysis through the composite
    /// by sampling from each component's posterior distribution.
    /// </para>
    /// </remarks>
    public class CompositeAnalysis : AnalysisBase, IUnivariateAnalysis
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeAnalysis"/> class.
        /// </summary>
        public CompositeAnalysis()
        {
            Analyses = new ObservableCollection<WeightedUnivariateAnalysis>();
            ProbabilityOrdinates = new ProbabilityOrdinates();
            BayesianAnalysis = new BayesianAnalysis();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeAnalysis"/> class
        /// with the specified analyses.
        /// </summary>
        /// <param name="analyses">The collection of weighted univariate analyses.</param>
        public CompositeAnalysis(IEnumerable<WeightedUnivariateAnalysis> analyses)
        {
            Analyses = new ObservableCollection<WeightedUnivariateAnalysis>(analyses);
            ProbabilityOrdinates = new ProbabilityOrdinates();
            BayesianAnalysis = new BayesianAnalysis();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeAnalysis"/> class
        /// from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XML element containing the serialized state.</param>
        /// <param name="analysisResolver">
        /// A function that resolves analysis names to <see cref="IUnivariateAnalysis"/> instances.
        /// Resolved analyses that are themselves <see cref="CompositeAnalysis"/> are skipped to
        /// prevent composite-of-composite nesting.
        /// </param>
        public CompositeAnalysis(XElement xElement, Func<string, IUnivariateAnalysis?>? analysisResolver = null)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            Analyses = new ObservableCollection<WeightedUnivariateAnalysis>();
            ProbabilityOrdinates = new ProbabilityOrdinates();

            // Composite type
            var typeAttr = xElement.Attribute(nameof(CompositeDistributionType));
            if (typeAttr != null && Enum.TryParse(typeAttr.Value, out CompositeType ct))
                _compositeDistributionType = ct;

            // Average method
            var avgAttr = xElement.Attribute(nameof(ModelAverageMethod));
            if (avgAttr != null && Enum.TryParse(avgAttr.Value, out AverageMethod am))
                _modelAverageMethod = am;

            // Dependency
            var depAttr = xElement.Attribute(nameof(Dependency));
            if (depAttr != null && Enum.TryParse(depAttr.Value, out Probability.DependencyType dt))
                _dependency = dt;

            // Is maximum
            var maxAttr = xElement.Attribute(nameof(IsMaximum));
            if (maxAttr != null && bool.TryParse(maxAttr.Value, out bool isMax))
                _isMaximum = isMax;

            // Probability ordinates
            var probElement = xElement.Element("ProbabilityOrdinates");
            if (probElement != null)
            {
                ProbabilityOrdinates.FromDelimitedString((string)probElement, ProbabilityOrdinates.DefaultDelimiter);
            }

            // Bayesian analysis settings
            var bayesElement = xElement.Element("BayesianAnalysis");
            if (bayesElement != null)
            {
                BayesianAnalysis = new BayesianAnalysis(bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis();
            }

            // Analyses
            var analysesElement = xElement.Element(nameof(Analyses));
            if (analysesElement != null && analysisResolver != null)
            {
                foreach (var wuaElement in analysesElement.Elements(nameof(WeightedUnivariateAnalysis)))
                {
                    var nameAttr = wuaElement.Attribute("Name");
                    var weightAttr = wuaElement.Attribute(nameof(WeightedUnivariateAnalysis.Weight));
                    if (nameAttr != null && weightAttr != null)
                    {
                        var analysis = analysisResolver(nameAttr.Value);
                        // Skip composite-of-composite at the deserializer to keep stale or
                        // hand-edited project files loadable rather than throwing on Open.
                        if (analysis is CompositeAnalysis)
                            continue;
                        if (analysis != null && double.TryParse(weightAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double weight))
                        {
                            Analyses.Add(new WeightedUnivariateAnalysis(analysis, weight));
                        }
                    }
                }
            }

            // Is estimated
            var isEstimatedAttr = xElement.Attribute("IsEstimated");
            if (isEstimatedAttr != null && bool.TryParse(isEstimatedAttr.Value, out bool isEst))
            {
                _isEstimated = isEst;
            }
        }

        #endregion

        #region Members

        private ObservableCollection<WeightedUnivariateAnalysis> _analyses = null!;
        private CompositeType _compositeDistributionType = CompositeType.CompetingRisks;
        private AverageMethod _modelAverageMethod = AverageMethod.DIC;
        private Probability.DependencyType _dependency = Probability.DependencyType.Independent;
        private bool _isMaximum = true;
        private ProbabilityOrdinates _probabilityOrdinates = null!;
        private BayesianAnalysis _bayesianAnalysis = null!;

        /// <summary>
        /// True while <see cref="EstimateModelWeights"/> is mutating per-child weights.
        /// The Weight setters fire <c>PropertyChanged("Weight")</c> events that bubble up
        /// to <see cref="WeightedAnalysis_PropertyChanged"/>; when the writes originate
        /// from <see cref="EstimateModelWeights"/> itself, the per-Weight ClearResults
        /// would cascade into N redundant <see cref="ClearResults"/> calls (one per
        /// child) plus the outer caller's single <see cref="ClearResults"/>. Each
        /// <see cref="ClearResults"/> raises <c>AnalysisResults</c> PropertyChanged,
        /// which the App's <c>UpdateFrequencyPlot</c> handler treats as a full plot
        /// rebuild — producing visible flicker and a wait-cursor flash for every child.
        /// This guard collapses the cascade to a single ClearResults at the outer
        /// caller's site.
        /// </summary>
        private bool _isEstimatingWeights;

        /// <summary>
        /// Gets or sets the collection of weighted univariate analyses that make up the composite.
        /// </summary>
        public ObservableCollection<WeightedUnivariateAnalysis> Analyses
        {
            get { return _analyses; }
            set
            {
                if (_analyses != null)
                    _analyses.CollectionChanged -= Analyses_CollectionChanged;

                _analyses = value;

                if (_analyses != null)
                    _analyses.CollectionChanged += Analyses_CollectionChanged;

                ClearResults();
                RaisePropertyChange(nameof(Analyses));
            }
        }

        /// <summary>
        /// Gets or sets how the univariate distributions will be composited.
        /// </summary>
        public CompositeType CompositeDistributionType
        {
            get { return _compositeDistributionType; }
            set
            {
                if (_compositeDistributionType != value)
                {
                    _compositeDistributionType = value;
                    if (_compositeDistributionType == CompositeType.ModelAverage)
                        EstimateModelWeights();
                    ClearResults();
                    RaisePropertyChange(nameof(CompositeDistributionType));
                }
            }
        }

        /// <summary>
        /// Gets or sets the model averaging method used when <see cref="CompositeDistributionType"/>
        /// is <see cref="CompositeType.ModelAverage"/>.
        /// </summary>
        public AverageMethod ModelAverageMethod
        {
            get { return _modelAverageMethod; }
            set
            {
                if (_modelAverageMethod != value)
                {
                    _modelAverageMethod = value;
                    if (_compositeDistributionType == CompositeType.ModelAverage)
                        EstimateModelWeights();
                    ClearResults();
                    RaisePropertyChange(nameof(ModelAverageMethod));
                }
            }
        }

        /// <summary>
        /// Gets or sets the statistical dependency between competing distributions.
        /// </summary>
        /// <remarks>
        /// For maximum values, perfect positive dependency yields the upper bound and
        /// perfect negative dependency the lower bound. The reverse applies for minimum values.
        /// </remarks>
        public Probability.DependencyType Dependency
        {
            get { return _dependency; }
            set
            {
                if (_dependency != value)
                {
                    _dependency = value;
                    ClearResults();
                    RaisePropertyChange(nameof(Dependency));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the distributions are competing
        /// to be the maximum or minimum value.
        /// </summary>
        public bool IsMaximum
        {
            get { return _isMaximum; }
            set
            {
                if (_isMaximum != value)
                {
                    _isMaximum = value;
                    ClearResults();
                    RaisePropertyChange(nameof(IsMaximum));
                }
            }
        }

        /// <inheritdoc/>
        public ProbabilityOrdinates ProbabilityOrdinates
        {
            get { return _probabilityOrdinates; }
            set
            {
                if (_probabilityOrdinates != null)
                    _probabilityOrdinates.CollectionChanged -= ProbabilityOrdinates_CollectionChanged;

                _probabilityOrdinates = value;

                if (_probabilityOrdinates != null)
                    _probabilityOrdinates.CollectionChanged += ProbabilityOrdinates_CollectionChanged;

                ClearResults();
                RaisePropertyChange(nameof(ProbabilityOrdinates));
            }
        }

        /// <inheritdoc/>
        public BayesianAnalysis BayesianAnalysis
        {
            get { return _bayesianAnalysis; }
            private set
            {
                if (_bayesianAnalysis != null)
                    _bayesianAnalysis.PropertyChanged -= BayesianAnalysis_PropertyChanged;

                _bayesianAnalysis = value;

                if (_bayesianAnalysis != null)
                    _bayesianAnalysis.PropertyChanged += BayesianAnalysis_PropertyChanged;

                RaisePropertyChange(nameof(BayesianAnalysis));
            }
        }

        /// <summary>
        /// Gets the uncertainty analysis results.
        /// </summary>
        public UncertaintyAnalysisResults? AnalysisResults { get; private set; }

        #endregion

        #region Methods

        private void Analyses_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (WeightedUnivariateAnalysis oldItem in e.OldItems)
                {
                    oldItem.PropertyChanged -= WeightedAnalysis_PropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (WeightedUnivariateAnalysis newItem in e.NewItems)
                {
                    newItem.PropertyChanged += WeightedAnalysis_PropertyChanged;
                }
            }
            EstimateModelWeights();
            ClearResults();
            RaisePropertyChange(nameof(Analyses));
        }

        private void WeightedAnalysis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Re-fit detection: a child analysis re-running goes through ClearResults (sets
            // AnalysisResults = null with IsEstimated still true), then MCMC, then assigns
            // a fresh AnalysisResults, then finally flips IsEstimated to true (see
            // UnivariateAnalysis.RunAsync line 510). The "AnalysisResults"-only branch fires
            // EstimateModelWeights at every step of that sequence — but at the moment a new
            // AnalysisResults is set, IsEstimated is still false, so the child is filtered out
            // by EstimateModelWeights' "valid sub-analyses" check (CompositeAnalysis.cs:561)
            // and ends up with weight=0. The IsEstimated handler below catches the final
            // transition so the weights resync to the now-fully-fit child without the user
            // having to toggle ModelAverageMethod manually.
            if (e.PropertyName == nameof(WeightedUnivariateAnalysis.UnivariateAnalysis) ||
                e.PropertyName == nameof(UnivariateAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(IsEstimated))
            {
                EstimateModelWeights();
                ClearResults();
            }
            else if (e.PropertyName == nameof(WeightedUnivariateAnalysis.Weight))
            {
                // Skip the per-Weight ClearResults when EstimateModelWeights is the writer —
                // the cascading rebuild fires N AnalysisResults PropertyChanged events in
                // the App, producing flicker and a wait-cursor flash per child. The caller
                // (e.g. ModelAverageMethod setter) invokes ClearResults once at the end.
                if (!_isEstimatingWeights)
                    ClearResults();
            }
            RaisePropertyChange(nameof(Analyses));
        }

        /// <summary>
        /// Handles changes to the <see cref="ProbabilityOrdinates"/> collection.
        /// </summary>
        /// <remarks>
        /// Ordinates drive only <see cref="AnalysisResults"/>. When estimated and valid,
        /// reprocess via <see cref="CreateFrequencyAnalysisResultsAsync"/>; when invalid,
        /// clear <see cref="AnalysisResults"/> only; when not estimated, no-op.
        /// </remarks>
        private void ProbabilityOrdinates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChange(nameof(ProbabilityOrdinates));

            if (!IsEstimated) return;

            if (ProbabilityOrdinates.Validate().IsValid)
                ReprocessIfEstimated(() => CreateFrequencyAnalysisResultsAsync());
            else
                ClearFrequencyAnalysisResults();
        }

        /// <summary>
        /// Handles property changes on the <see cref="BayesianAnalysis"/> object.
        /// Loss of estimated state clears results; PointEstimator changes update the
        /// point-estimate output without rebuilding posterior bands; CredibleIntervalWidth
        /// changes clear derived composite results because the per-realisation matrix of
        /// composite distributions is not cached and the user must rerun explicitly.
        /// All other notifications propagate for UI binding.
        /// </summary>
        private void BayesianAnalysis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BayesianAnalysis.IsEstimated))
            {
                if (BayesianAnalysis.IsEstimated == false)
                    ClearResults();
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(BayesianAnalysis.PointEstimator))
            {
                ReprocessIfEstimated(UpdatePointEstimateResultsAsync);
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(BayesianAnalysis.CredibleIntervalWidth))
            {
                ClearResults();
                RaisePropertyChange(e.PropertyName);
            }
            else
            {
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Clears all analysis results.
        /// </summary>
        public void ClearResults()
        {
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
            IsEstimated = false;
        }

        /// <summary>
        /// Clears <see cref="AnalysisResults"/> only â€” the frequency/quantile output whose
        /// evaluation grid is <see cref="ProbabilityOrdinates"/>. Leaves <see cref="IsEstimated"/>
        /// and the child analyses' fits intact.
        /// </summary>
        public void ClearFrequencyAnalysisResults()
        {
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Updates the point-estimate frequency curve using the current
        /// <see cref="BayesianAnalysis.PointEstimator"/> without rebuilding uncertainty bands.
        /// </summary>
        /// <returns>A task that completes when the point-estimate curve has been refreshed.</returns>
        /// <remarks>
        /// The child analyses are read without mutating their own point-estimator settings, so
        /// a Composite selection changes only the Composite point-estimate presentation.
        /// </remarks>
        public async Task UpdatePointEstimateResultsAsync()
        {
            if (!IsEstimated || AnalysisResults == null)
                return;

            await Task.Run(() =>
            {
                var pointEstimateDistribution = GetPointEstimateDistribution();
                if (pointEstimateDistribution is null)
                    return;

                AnalysisResults.ParentDistribution = pointEstimateDistribution;
                AnalysisResults.ModeCurve = new double[ProbabilityOrdinates.Count];
                for (int i = 0; i < ProbabilityOrdinates.Count; i++)
                    AnalysisResults.ModeCurve[i] = pointEstimateDistribution.InverseCDF(1 - ProbabilityOrdinates[i]);
            });

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Restores previously saved analysis results from deserialization.
        /// Sets <see cref="IsEstimated"/> to true if results are non-null.
        /// </summary>
        /// <param name="results">The deserialized analysis results.</param>
        public void RestoreAnalysisResults(UncertaintyAnalysisResults results)
        {
            AnalysisResults = results;
            IsEstimated = results != null;
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(IsEstimated));
        }

        /// <summary>
        /// Estimates model weights based on the selected averaging method.
        /// </summary>
        public void EstimateModelWeights()
        {
            if (CompositeDistributionType != CompositeType.ModelAverage || Analyses == null || Analyses.Count == 0)
                return;

            // Suppress the per-Weight ClearResults cascade in WeightedAnalysis_PropertyChanged
            // for the duration of this method. The caller (e.g. ModelAverageMethod setter,
            // CompositeDistributionType setter, Analyses_CollectionChanged) is responsible
            // for invoking ClearResults once at the end.
            _isEstimatingWeights = true;
            try
            {

            double sum = 0;
            if (ModelAverageMethod == AverageMethod.Equal)
            {
                double w = 1d / Analyses.Count;
                for (int i = 0; i < Analyses.Count; i++)
                {
                    Analyses[i].Weight = w;
                    sum += w;
                }
            }
            else
            {
                // Build the goodness-of-fit array over only the successfully-fit
                // sub-analyses. Passing default-zero entries for unfit analyses to
                // AICWeights would assign them the BEST weight (since 0 is the
                // minimum AIC across the array), contaminating the composite with
                // phantom contributions.
                var validIndices = new List<int>();
                for (int i = 0; i < Analyses.Count; i++)
                {
                    var ua = Analyses[i].UnivariateAnalysis;
                    if (ua?.AnalysisResults != null && ua.IsEstimated)
                        validIndices.Add(i);
                }

                // Default every weight to 0; only valid sub-analyses contribute.
                for (int i = 0; i < Analyses.Count; i++)
                    Analyses[i].Weight = 0.0;

                if (validIndices.Count == 0)
                    return; // sum stays 0; caller's normalization branch is a no-op

                var gofValid = new double[validIndices.Count];
                for (int j = 0; j < validIndices.Count; j++)
                {
                    int i = validIndices[j];
                    gofValid[j] = ModelAverageMethod switch
                    {
                        AverageMethod.AIC => Analyses[i].UnivariateAnalysis!.AnalysisResults!.AIC,
                        AverageMethod.BIC => Analyses[i].UnivariateAnalysis!.AnalysisResults!.BIC,
                        AverageMethod.DIC => Analyses[i].UnivariateAnalysis!.BayesianAnalysis?.DIC ?? double.NaN,
                        AverageMethod.WAIC => Analyses[i].UnivariateAnalysis!.BayesianAnalysis?.WAIC ?? double.NaN,
                        AverageMethod.LOOIC => Analyses[i].UnivariateAnalysis!.BayesianAnalysis?.LOOIC ?? double.NaN,
                        AverageMethod.RMSE => Analyses[i].UnivariateAnalysis!.AnalysisResults!.RMSE,
                        _ => 0
                    };
                }

                var validWeights = ModelAverageMethod == AverageMethod.RMSE
                    ? GoodnessOfFit.RMSEWeights(gofValid)
                    : GoodnessOfFit.AICWeights(gofValid);

                for (int j = 0; j < validIndices.Count; j++)
                {
                    Analyses[validIndices[j]].Weight = validWeights[j];
                    sum += validWeights[j];
                }
            }

            // Ensure weights sum to 1 by normalizing proportionally
            // This avoids pushing any single weight negative or above 1
            if (Analyses.Count > 0 && Math.Abs(sum - 1.0) > 1e-10 && sum > 0)
            {
                for (int i = 0; i < Analyses.Count; i++)
                {
                    Analyses[i].Weight /= sum;
                }
            }

            }
            finally { _isEstimatingWeights = false; }
        }

        /// <inheritdoc/>
        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            var validation = Validate();
            if (!validation.IsValid)
                throw new InvalidOperationException(string.Join("; ", validation.ValidationMessages));

            // Composite analysis is a weighted aggregation over already-fitted
            // sub-analyses; it has no MCMC chain of its own. Refuse to run unless
            // every sub-analysis has been successfully estimated, otherwise the
            // model-averaged quantiles would silently include zero-information
            // contributions from unfit sub-analyses.
            for (int i = 0; i < Analyses.Count; i++)
            {
                var ua = Analyses[i].UnivariateAnalysis;
                if (ua == null || !ua.IsEstimated || ua.AnalysisResults == null)
                {
                    throw new InvalidOperationException(
                        $"CompositeAnalysis cannot run: sub-analysis #{i} has not been successfully fit. " +
                        "Run all sub-analyses first.");
                }
            }

            var previewArgs = new CancelEventArgs();
            OnAnalysisStarting(previewArgs);
            if (previewArgs.Cancel)
            {
                OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(true, false, null));
                return;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            // Wait for any in-flight reprocess to finish before clearing results and
            // starting a new MCMC run. Without this gate, a fire-and-forget reprocess
            // (triggered by a prior property change via ReprocessIfEstimated) can be
            // inside its parallel loop when ClearResults() nulls AnalysisResults —
            // producing an NRE on the next AnalysisResults dereference inside the loop body.
            await _reprocessGate.WaitAsync();
            try
            {
                ClearResults();
                EstimateModelWeights();
                progressReporter?.IndicateTaskStart();

                bool wasCanceled = false;
                Exception? error = null;

                try
                {
                    // Set IsEstimated BEFORE creating results so that when AnalysisResults
                    // PropertyChanged fires inside CreateFrequencyAnalysisResultsAsync, the
                    // App control's gates (Element.IsEstimated == true && AnalysisResults != null)
                    // see the analysis as estimated and draw the curves. Without this, the batch
                    // path — which calls inner.RunAsync directly without the UI wrapper's
                    // post-await RaisePropertyChange — leaves the plot/grids empty. Mirrors the
                    // B17C pattern (Bulletin17CAnalysis.cs:558).
                    _isEstimated = true;
                    await CreateFrequencyAnalysisResultsAsync(progressReporter);
                    RaisePropertyChange(nameof(IsEstimated));
                }
                catch (OperationCanceledException)
                {
                    wasCanceled = true;
                    IsEstimated = false;
                }
                catch (Exception ex)
                {
                    error = ex;
                    IsEstimated = false;
                }
                finally
                {
                    progressReporter?.IndicateTaskEnded();
                    OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(wasCanceled, IsEstimated && !wasCanceled && error == null, error));
                }
            }
            finally
            {
                _reprocessGate.Release();
            }
        }

        /// <summary>
        /// Creates the frequency analysis results for the composite distribution.
        /// </summary>
        /// <param name="progressReporter">Optional progress reporter.</param>
        public async Task CreateFrequencyAnalysisResultsAsync(SafeProgressReporter? progressReporter = null)
        {
            AnalysisResults = null;
            if (Analyses == null || Analyses.Count == 0)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }

            await Task.Run(() =>
            {
                // Get minimum number of realizations across all analyses
                int realz = Analyses.Min(x => x.UnivariateAnalysis?.BayesianAnalysis?.OutputLength ?? 0);
                if (realz == 0) return;

                // Cancellation wiring (mirrors Bulletin17CAnalysis.cs:789-850 pattern):
                // pass the run's cancellation token via ParallelOptions so the runtime
                // stops dispatching new iterations as soon as it's signaled, and check
                // ThrowIfCancellationRequested at the top of each iteration body so an
                // already-running iteration short-circuits instead of completing. The
                // outer RunAsync already catches OperationCanceledException at line 714
                // and reports it through AnalysisRunCompletedEventArgs.
                var options = new ParallelOptions
                {
                    CancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                var results = new UnivariateDistributionBase[realz];
                UnivariateDistributionBase? mode = null;

                if (CompositeDistributionType == CompositeType.CompetingRisks)
                {
                    // Get mode (point estimate)
                    var modeDists = new UnivariateDistributionBase[Analyses.Count];
                    for (int i = 0; i < Analyses.Count; i++)
                    {
                        modeDists[i] = GetChildPointEstimateDistribution(Analyses[i].UnivariateAnalysis!,
                            BayesianAnalysis.PointEstimator)!;
                    }
                    mode = new CompetingRisks(modeDists)
                    {
                        MinimumOfRandomVariables = !IsMaximum,
                        Dependency = Dependency,
                        XTransform = Transform.Logarithmic,
                        ProbabilityTransform = Transform.NormalZ
                    };
                    ((CompetingRisks)mode).CreateEmpiricalCDF();

                    // Get uncertainty samples
                    int iteration = 0;
                    Parallel.For(0, realz, options, idx =>
                    {
                        // ThrowIfCancellationRequested is the reliable cancel-observation
                        // primitive: it fires IMMEDIATELY when the token is signaled,
                        // regardless of how fast individual iterations are. The
                        // OperationCanceledException it raises pops as a "first-chance
                        // exception" in the Visual Studio debugger when CLR exceptions
                        // are enabled in Debug → Windows → Exception Settings, but the
                        // outer catch in RunAsync (CompositeAnalysis.cs:714) handles it
                        // correctly — the user sees a clean cancel in Release mode and
                        // when running outside the debugger. (Silent-return + phase-
                        // boundary check is theoretically equivalent but only catches
                        // the cancel between Parallel.For dispatches, which can be
                        // unreliable for fast iterations.)
                        options.CancellationToken.ThrowIfCancellationRequested();

                        var uDists = new UnivariateDistributionBase[Analyses.Count];
                        for (int i = 0; i < Analyses.Count; i++)
                        {
                            uDists[i] = Analyses[i].UnivariateAnalysis!.GetDistribution(idx)!;
                        }
                        results[idx] = new CompetingRisks(uDists)
                        {
                            MinimumOfRandomVariables = !IsMaximum,
                            Dependency = Dependency,
                            XTransform = Transform.Logarithmic,
                            ProbabilityTransform = Transform.NormalZ
                        };
                        ((CompetingRisks)results[idx]).CreateEmpiricalCDF();

                        int current = Interlocked.Increment(ref iteration);
                        if (current % Math.Max(1, (int)(realz * 0.01)) == 0)
                            progressReporter?.ReportProgress((int)(99.0 * current / realz));
                    });
                }
                else // Mixture or ModelAverage
                {
                    // Get mode (point estimate)
                    double sum = 0;
                    var weights = new List<double>();
                    var modeDists = new List<UnivariateDistributionBase>();
                    for (int i = 0; i < Analyses.Count; i++)
                    {
                        sum += Analyses[i].Weight;
                        weights.Add(Analyses[i].Weight);
                        modeDists.Add(GetChildPointEstimateDistribution(Analyses[i].UnivariateAnalysis!,
                            BayesianAnalysis.PointEstimator)!);
                    }
                    mode = new Mixture(weights.ToArray(), modeDists.ToArray())
                    {
                        XTransform = Transform.Logarithmic,
                        ProbabilityTransform = Transform.NormalZ
                    };
                    if (sum < 1)
                    {
                        ((Mixture)mode).IsZeroInflated = true;
                        ((Mixture)mode).ZeroWeight = 1 - sum;
                    }
                    ((Mixture)mode).CreateEmpiricalCDF();

                    // Get uncertainty samples
                    int iteration = 0;
                    Parallel.For(0, realz, options, idx =>
                    {
                        // See CompetingRisks branch above for rationale.
                        options.CancellationToken.ThrowIfCancellationRequested();

                        var uDists = new List<UnivariateDistributionBase>();
                        for (int i = 0; i < Analyses.Count; i++)
                        {
                            uDists.Add(Analyses[i].UnivariateAnalysis!.GetDistribution(idx)!);
                        }
                        results[idx] = new Mixture(weights.ToArray(), uDists.ToArray())
                        {
                            XTransform = Transform.Logarithmic,
                            ProbabilityTransform = Transform.NormalZ
                        };
                        if (sum < 1)
                        {
                            ((Mixture)results[idx]).IsZeroInflated = true;
                            ((Mixture)results[idx]).ZeroWeight = 1 - sum;
                        }
                        ((Mixture)results[idx]).CreateEmpiricalCDF();

                        int current = Interlocked.Increment(ref iteration);
                        if (current % Math.Max(1, (int)(realz * 0.01)) == 0)
                            progressReporter?.ReportProgress((int)(99.0 * current / realz));
                    });
                }

                // Phase-boundary check: if the user cancels just as the parallel block
                // is finishing, short-circuit before the (synchronous) bootstrap
                // aggregation rather than making them wait for it to complete.
                options.CancellationToken.ThrowIfCancellationRequested();

                // Convert probability ordinates to non-exceedance
                var probs = new double[ProbabilityOrdinates.Count];
                for (int i = 0; i < probs.Length; i++)
                    probs[i] = 1 - ProbabilityOrdinates[i];

                // Create uncertainty analysis results using bootstrap estimator
                var boot = new BootstrapAnalysis(mode, ParameterEstimationMethod.MaximumLikelihood, 100, realz);
                AnalysisResults = boot.Estimate(probs, 1 - BayesianAnalysis.CredibleIntervalWidth, results, false);

                progressReporter?.ReportProgress(100);
            });

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetDistribution(int index)
        {
            // Composite distributions don't support returning individual distributions
            // because they are constructed from multiple component analyses
            return null;
        }

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetPointEstimateDistribution()
            => GetPointEstimateDistribution(BayesianAnalysis.PointEstimator);

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetPointEstimateDistribution(
            BayesianAnalysis.PointEstimateType pointEstimator)
        {
            if (Analyses == null || Analyses.Count == 0)
                return null;

            if (CompositeDistributionType == CompositeType.CompetingRisks)
            {
                var modeDists = new UnivariateDistributionBase[Analyses.Count];
                for (int i = 0; i < Analyses.Count; i++)
                {
                    var dist = GetChildPointEstimateDistribution(Analyses[i].UnivariateAnalysis, pointEstimator);
                    if (dist is null) return null;
                    modeDists[i] = dist;
                }
                var cr = new CompetingRisks(modeDists)
                {
                    MinimumOfRandomVariables = !IsMaximum,
                    Dependency = Dependency,
                    XTransform = Transform.Logarithmic,
                    ProbabilityTransform = Transform.NormalZ
                };
                cr.CreateEmpiricalCDF();
                return cr;
            }
            else
            {
                double sum = 0;
                var weights = new List<double>();
                var modeDists = new List<UnivariateDistributionBase>();
                for (int i = 0; i < Analyses.Count; i++)
                {
                    var dist = GetChildPointEstimateDistribution(Analyses[i].UnivariateAnalysis, pointEstimator);
                    if (dist is null) return null;
                    sum += Analyses[i].Weight;
                    weights.Add(Analyses[i].Weight);
                    modeDists.Add(dist);
                }
                var mix = new Mixture(weights.ToArray(), modeDists.ToArray())
                {
                    XTransform = Transform.Logarithmic,
                    ProbabilityTransform = Transform.NormalZ
                };
                if (sum < 1)
                {
                    mix.IsZeroInflated = true;
                    mix.ZeroWeight = 1 - sum;
                }
                mix.CreateEmpiricalCDF();
                return mix;
            }
        }

        /// <summary>
        /// Gets a child analysis point-estimate distribution for the specified estimator.
        /// </summary>
        /// <param name="analysis">The child univariate analysis to evaluate.</param>
        /// <param name="pointEstimator">The estimator to use for the child posterior samples.</param>
        /// <returns>The child point-estimate distribution, or <c>null</c> when unavailable.</returns>
        /// <remarks>
        /// Delegates to <see cref="IUnivariateAnalysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType)"/>
        /// so the child handles its own stationary / nonstationary parameter layout. The
        /// composite never directly pokes at the child's <see cref="UnivariateDistribution"/>
        /// or calls <c>SetParameters</c> on a base distribution, which previously broke
        /// nonstationary <see cref="UnivariateAnalysis"/> children whose parameter array
        /// includes trend coefficients.
        /// </remarks>
        private static UnivariateDistributionBase? GetChildPointEstimateDistribution(
            IUnivariateAnalysis? analysis,
            BayesianAnalysis.PointEstimateType pointEstimator)
        {
            if (analysis?.BayesianAnalysis == null ||
                analysis.BayesianAnalysis.IsEstimated == false ||
                analysis.BayesianAnalysis.Results == null)
                return null;

            return analysis.GetPointEstimateDistribution(pointEstimator);
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            if (Analyses == null || Analyses.Count == 0)
            {
                isValid = false;
                messages.Add("Error: No univariate analyses defined for the composite analysis.");
            }
            else
            {
                double sum = 0;
                foreach (var wua in Analyses)
                {
                    // Belt-and-suspenders guard against composite-of-composite. The
                    // WeightedUnivariateAnalysis setter rejects this assignment, but a
                    // legacy project file or a non-public reflection path could still
                    // produce such a configuration.
                    if (wua.UnivariateAnalysis is CompositeAnalysis)
                    {
                        isValid = false;
                        messages.Add(
                            "Error: a child analysis is itself a CompositeAnalysis; " +
                            "nesting composites is not supported.");
                    }

                    var validation = wua.Validate();
                    if (!validation.IsValid)
                    {
                        isValid = false;
                        messages.Add(validation.Message);
                    }

                    if (CompositeDistributionType == CompositeType.Mixture &&
                        (wua.Weight <= 0 || wua.Weight >= 1))
                    {
                        isValid = false;
                        messages.Add("Error: Weights for each univariate analysis must be between 0 and 1.");
                    }
                    sum += wua.Weight;
                }

                if (CompositeDistributionType == CompositeType.Mixture && sum - 1 > Tools.DoubleMachineEpsilon * 2)
                {
                    isValid = false;
                    messages.Add("Error: The sum of the weights is greater than 1.");
                }

                // Bulletin17CAnalysis is fit by GMM, not by an MCMC chain, so it does not
                // produce posterior-likelihood-based information criteria (DIC, WAIC, LOO-CV).
                // Reject Model Averaging weighted by any of those criteria when at least one
                // child is a Bulletin17CAnalysis — silently averaging with NaN / 0 weights
                // would produce a meaningless composite curve.
                if (CompositeDistributionType == CompositeType.ModelAverage &&
                    (ModelAverageMethod == AverageMethod.DIC ||
                     ModelAverageMethod == AverageMethod.WAIC ||
                     ModelAverageMethod == AverageMethod.LOOIC) &&
                    Analyses.Any(wua => wua.UnivariateAnalysis is Bulletin17CAnalysis))
                {
                    isValid = false;
                    messages.Add(
                        $"Error: Model Averaging with {ModelAverageMethod} is not supported when any child " +
                        "is a Bulletin17CAnalysis. B17C uses Generalized Method of Moments rather than an " +
                        "MCMC chain, so DIC, WAIC, and LOO-CV are not defined for it. Choose AIC, BIC, " +
                        "RMSE, or Equal weighting instead.");
                }
            }

            if (ProbabilityOrdinates == null || ProbabilityOrdinates.Count == 0)
            {
                isValid = false;
                messages.Add("Error: At least one probability ordinate is required.");
            }
            else
            {
                for (int i = 0; i < ProbabilityOrdinates.Count; i++)
                {
                    if (ProbabilityOrdinates[i] < 0 || ProbabilityOrdinates[i] > 1)
                    {
                        isValid = false;
                        messages.Add("Error: Probability ordinates must be between 0 and 1.");
                        break;
                    }
                    if (i > 0 && ProbabilityOrdinates[i] <= ProbabilityOrdinates[i - 1])
                    {
                        isValid = false;
                        messages.Add("Error: Probability ordinates must be in ascending order.");
                        break;
                    }
                }
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Serializes this analysis to an XML element.
        /// </summary>
        /// <returns>An <see cref="XElement"/> containing the serialized state.</returns>
        public XElement ToXElement()
        {
            var root = new XElement("CompositeAnalysis",
                new XAttribute("IsEstimated", IsEstimated),
                new XAttribute(nameof(CompositeDistributionType), CompositeDistributionType),
                new XAttribute(nameof(ModelAverageMethod), ModelAverageMethod),
                new XAttribute(nameof(Dependency), Dependency),
                new XAttribute(nameof(IsMaximum), IsMaximum));

            // Probability ordinates
            if (ProbabilityOrdinates != null && ProbabilityOrdinates.Count > 0)
            {
                root.Add(new XElement("ProbabilityOrdinates",
                    ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter)));
            }

            // Bayesian analysis settings
            if (BayesianAnalysis != null)
            {
                root.Add(BayesianAnalysis.ToXElement());
            }

            // Analyses
            if (Analyses != null && Analyses.Count > 0)
            {
                var analysesElement = new XElement(nameof(Analyses));
                foreach (var wua in Analyses)
                {
                    var wuaElement = wua.ToXElement();
                    if (wuaElement != null)
                    {
                        analysesElement.Add(wuaElement);
                    }
                }
                root.Add(analysesElement);
            }

            return root;
        }

        #endregion
    }
}
