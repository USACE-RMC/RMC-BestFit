using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{

    /// <summary>
    /// Performs Bayesian MCMC estimation for a univariate distribution given an input <see cref="DataFrame"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This analysis fits a specified <c>UnivariateDistribution</c> using Bayesian MCMC methods.
    /// It produces uncertainty quantification for both frequency analysis (quantiles at specified
    /// probabilities) and chronology analysis (time-varying quantiles for nonstationary models).
    /// </para>
    /// <para>
    /// The class implements <see cref="IAnalysis"/> and <see cref="IUnivariateAnalysis"/>.
    /// </para>
    /// </remarks>
    public class UnivariateAnalysis : AnalysisBase, IUnivariateAnalysis
    {

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="UnivariateAnalysis"/> class
        /// for the specified univariate distribution.
        /// </summary>
        /// <param name="univariateDistribution">
        /// The <c>UnivariateDistribution</c> to be estimated.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="univariateDistribution"/> is <c>null</c>.
        /// </exception>
        public UnivariateAnalysis(UnivariateDistribution univariateDistribution)
        {
            UnivariateDistribution = univariateDistribution ?? throw new ArgumentNullException(nameof(univariateDistribution));
            BayesianAnalysis = new BayesianAnalysis(univariateDistribution);
            ProbabilityOrdinates = new ProbabilityOrdinates();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnivariateAnalysis"/> class
        /// by deserializing from an <see cref="XElement"/>, with optional restoration of
        /// MCMC results and analysis results from a previous estimation.
        /// </summary>
        /// <param name="univariateDistribution">
        /// The <c>UnivariateDistribution</c> associated with this analysis.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration (BayesianAnalysis settings,
        /// ProbabilityOrdinates, and IsEstimated flag). Produced by <see cref="ToXElement"/>.
        /// </param>
        /// <param name="mcmcResults">
        /// Optional MCMC results to restore from a previous estimation.
        /// When provided, results are set via <c>BayesianAnalysis.SetCustomMCMCResults</c>
        /// with <c>skipInformationCriteria: true</c> since DIC/WAIC/LOO-CV are already in the BayesianAnalysis XElement.
        /// </param>
        /// <param name="analysisResults">
        /// Optional frequency analysis results to restore from a previous estimation.
        /// </param>
        /// <param name="chronologyAnalysisResults">
        /// Optional chronology analysis results for nonstationary models to restore from a previous estimation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="univariateDistribution"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public UnivariateAnalysis(UnivariateDistribution univariateDistribution, XElement xElement,
                                  MCMCResults? mcmcResults = null,
                                  UncertaintyAnalysisResults? analysisResults = null,
                                  UncertaintyAnalysisResults? chronologyAnalysisResults = null)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            UnivariateDistribution = univariateDistribution ?? throw new ArgumentNullException(nameof(univariateDistribution));

            // Probability ordinates
            ProbabilityOrdinates = new ProbabilityOrdinates();
            var probElement = xElement.Element("ProbabilityOrdinates");
            if (probElement != null)
            {
                var probText = (string)probElement;
                ProbabilityOrdinates.FromDelimitedString(probText, ProbabilityOrdinates.DefaultDelimiter);
            }

            // Bayesian analysis
            var bayesElement = xElement.Element("BayesianAnalysis");
            if (bayesElement != null)
            {
                BayesianAnalysis = new BayesianAnalysis(univariateDistribution, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis(univariateDistribution);
            }

            // Check if estimated
            var isEstimatedAttr = xElement.Attribute("IsEstimated");
            if (isEstimatedAttr != null && bool.TryParse(isEstimatedAttr.Value, out bool isEst))
            {
                _isEstimated = isEst;
            }

            // Restore MCMC results if provided
            if (mcmcResults != null)
                _bayesianAnalysis.SetCustomMCMCResults(mcmcResults, skipInformationCriteria: true);

            // Restore analysis results
            AnalysisResults = analysisResults;
            ChronologyAnalysisResults = chronologyAnalysisResults;
            NormalizeRestoredEstimatedState();
        }

        #endregion

        #region Members

        private UnivariateDistribution _univariateDistribution = new UnivariateDistribution();
        private BayesianAnalysis _bayesianAnalysis = null!;
        private ProbabilityOrdinates _probabilityOrdinates = new ProbabilityOrdinates();


        /// <summary>
        /// Gets or sets the univariate distribution model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the distribution changes, the analysis subscribes to its
        /// <c>PropertyChanged</c> event and updates
        /// the associated <see cref="BayesianAnalysis"/> model reference.
        /// </para>
        /// </remarks>
        public UnivariateDistribution UnivariateDistribution
        {
            get { return _univariateDistribution; }
            private set
            {
                if (_univariateDistribution != null)
                {
                    _univariateDistribution.PropertyChanged -= Model_PropertyChanged;
                }

                _univariateDistribution = value;

                if (_univariateDistribution != null)
                {
                    if (_bayesianAnalysis != null)
                        _bayesianAnalysis.Model = _univariateDistribution;

                    _univariateDistribution.PropertyChanged += Model_PropertyChanged;

                    // Seed the previous-time-index tracker used by Model_PropertyChanged to
                    // decide whether a ParameterTimeIndex change crosses the period-of-record
                    // boundary (and thus changes the Chronology forecast extent).
                    _previousTimeIndex = _univariateDistribution.ParameterTimeIndex;
                }

                RaisePropertyChange(nameof(UnivariateDistribution));
            }
        }

        /// <summary>
        /// Last observed value of <see cref="UnivariateDistribution.ParameterTimeIndex"/>,
        /// captured before the current PropertyChanged invocation. Used to detect crossings
        /// of the period-of-record boundary when deciding whether a TimeIndex change requires
        /// re-processing the Chronology results (vs. just the Frequency results).
        /// </summary>
        private int _previousTimeIndex;

        /// <inheritdoc/>
        public ProbabilityOrdinates ProbabilityOrdinates
        {
            get => _probabilityOrdinates;
            set
            {
                if (_probabilityOrdinates != null)
                    _probabilityOrdinates.CollectionChanged -= ProbabilityOrdinates_CollectionChanged;

                _probabilityOrdinates = value ?? new ProbabilityOrdinates();
                _probabilityOrdinates.CollectionChanged += ProbabilityOrdinates_CollectionChanged;

                RaisePropertyChange(nameof(ProbabilityOrdinates));
            }
        }

        /// <summary>
        /// Gets the Bayesian MCMC analysis object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="BayesianAnalysis"/> handles all MCMC simulation, convergence diagnostics,
        /// and posterior sampling for the <c>UnivariateDistribution</c>.
        /// </para>
        /// </remarks>
        public BayesianAnalysis BayesianAnalysis
        {
            get { return _bayesianAnalysis; }
            private set
            {
                if (_bayesianAnalysis != null)
                {
                    _bayesianAnalysis.PropertyChanged -= BayesianAnalysis_PropertyChanged;
                }

                _bayesianAnalysis = value;

                if (_bayesianAnalysis != null)
                {
                    _bayesianAnalysis.PropertyChanged += BayesianAnalysis_PropertyChanged;
                }

                RaisePropertyChange(nameof(BayesianAnalysis));
            }
        }

        /// <summary>
        /// Gets the frequency analysis results containing uncertainty quantification
        /// for quantiles at specified probability ordinates.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> until <see cref="RunAsync"/> completes successfully.
        /// It contains posterior distributions of quantiles, credible intervals, and goodness-of-fit metrics.
        /// </para>
        /// </remarks>
        public UncertaintyAnalysisResults? AnalysisResults { get; private set; }

        /// <summary>
        /// Gets the chronology results for nonstationary models, showing time-varying quantiles.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> for stationary models or until <see cref="RunAsync"/> completes.
        /// For nonstationary distributions, it contains uncertainty bounds on return levels over time.
        /// </para>
        /// </remarks>
        public UncertaintyAnalysisResults? ChronologyAnalysisResults { get; private set; }


        #endregion

        #region Methods

        /// <summary>
        /// Repairs the persisted estimated flag when older saves contain complete Bayesian artifacts
        /// but the outer analysis-level flag was left false.
        /// </summary>
        /// <remarks>
        /// Point-estimate-only setting changes are routed through <see cref="AnalysisBase.ReprocessIfEstimated"/>.
        /// Some persisted projects can have an estimated <see cref="BayesianAnalysis"/>, serialized
        /// <see cref="MCMCResults"/>, and serialized <see cref="AnalysisResults"/> while the outer
        /// <see cref="AnalysisBase.IsEstimated"/> flag is false. Treating those artifacts as authoritative
        /// restores the intended post-processing path without rerunning MCMC.
        /// </remarks>
        private void NormalizeRestoredEstimatedState()
        {
            if (_isEstimated)
            {
                return;
            }

            if (BayesianAnalysis?.IsEstimated == true &&
                BayesianAnalysis.Results != null &&
                AnalysisResults != null)
            {
                _isEstimated = true;
            }
        }

        /// <summary>
        /// Handles changes to the <see cref="ProbabilityOrdinates"/> collection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Probability ordinates drive only <see cref="AnalysisResults"/> (the frequency/quantile
        /// output). They do not affect the Bayesian MCMC fit, <see cref="ChronologyAnalysisResults"/>,
        /// or <c>IsEstimated</c>. So:
        /// </para>
        /// <list type="bullet">
        /// <item><description>If not estimated, no-op.</description></item>
        /// <item><description>If estimated and ordinates are valid, re-run the idempotent
        /// <see cref="CreateFrequencyAnalysisResultsAsync"/> to rebuild <see cref="AnalysisResults"/>
        /// from the existing MCMC output.</description></item>
        /// <item><description>If estimated but ordinates are invalid, clear only
        /// <see cref="AnalysisResults"/> via <see cref="ClearFrequencyAnalysisResults"/>; the fit survives.</description></item>
        /// </list>
        /// </remarks>
        private void ProbabilityOrdinates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChange(nameof(ProbabilityOrdinates));

            if (!IsEstimated) return;

            if (ProbabilityOrdinates.Validate().IsValid)
                ReprocessIfEstimated(CreateFrequencyAnalysisResultsAsync);
            else
                ClearFrequencyAnalysisResults();
        }

        /// <summary>
        /// Handles property changes on the <c>UnivariateDistribution</c> model.
        /// Routes each notification to one of three branches per the canonical
        /// property-change classification:
        /// <list type="bullet">
        /// <item><description><b>Clear results</b> — structurally destructive changes
        /// (data, distribution, parameters, trend models, prior toggles) call
        /// <see cref="ClearResults"/> to invalidate the fit.</description></item>
        /// <item><description><b>Re-process if estimated</b> — <c>ParameterTimeIndex</c>
        /// re-runs <see cref="CreateFrequencyAnalysisResultsAsync"/> against the existing
        /// MCMC fit; <c>Alpha</c> re-runs <see cref="CreateChronologyResultsAsync"/>.
        /// Both are post-fit selectors of which time-slice / exceedance probability the
        /// displayed curves are evaluated at — cheap to recompute, no MCMC re-run needed.</description></item>
        /// <item><description><b>Propagate only</b> — every other notification flows through
        /// <see cref="ModelBase.RaisePropertyChange"/> for UI binding.</description></item>
        /// </list>
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UnivariateDistribution.Parameters) ||
                e.PropertyName == nameof(UnivariateDistribution.SetDefaultParameters))
            {
                if (BayesianAnalysis.UseSimulationDefaults)
                    BayesianAnalysis.SetDefaultSimulationOptions();

                if (BayesianAnalysis.UseAdvancedSimulationDefaults)
                    BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

                ClearResults();
            }
            else if (e.PropertyName == nameof(UnivariateDistribution.DataFrame) ||
                     e.PropertyName == nameof(UnivariateDistribution.Distribution) ||
                     e.PropertyName == nameof(UnivariateDistribution.DistributionType) ||
                     e.PropertyName == nameof(UnivariateDistribution.IsNonstationary) ||
                     e.PropertyName == nameof(UnivariateDistribution.TrendModels) ||
                     e.PropertyName == nameof(UnivariateDistribution.SetDefaultQuantilePriors) ||
                     e.PropertyName == nameof(UnivariateDistribution.UseDefaultFlatPriors) ||
                     e.PropertyName == nameof(UnivariateDistribution.UseJeffreysRuleForScale) ||
                     e.PropertyName == nameof(UnivariateDistribution.QuantilePriors) ||
                     e.PropertyName == nameof(UnivariateDistribution.EnableQuantilePriors) ||
                     e.PropertyName == nameof(UnivariateDistribution.UseSingleQuantile))
            {
                ClearResults();
            }
            else if (e.PropertyName == nameof(UnivariateDistribution.ParameterTimeIndex))
            {
                int newIndex = _univariateDistribution.ParameterTimeIndex;
                int oldIndex = _previousTimeIndex;
                _previousTimeIndex = newIndex;

                if (_univariateDistribution.IsNonstationary)
                {
                    // Time-index change re-evaluates the nonstationary frequency curve at the
                    // new time slice from the existing posterior; no MCMC re-run required.
                    ReprocessIfEstimated(CreateFrequencyAnalysisResultsAsync);

                    // Chronology forecast extent depends on whether the time-index sits inside
                    // or past the data's last index. The Chronology plot always covers the full
                    // period of record; when TimeIndex > maxIndex it extends a forecast tail to
                    // the new index. So re-process Chronology only when at least one of (old, new)
                    // is past the data's last index — i.e. the forecast tail just appeared,
                    // disappeared, or changed extent. When both old and new are inside the POR,
                    // the chronology output is unchanged and a re-process would be wasted work.
                    int? maxIndex = TryGetMaxDataIndex();
                    if (maxIndex == null || oldIndex > maxIndex.Value || newIndex > maxIndex.Value)
                        ReprocessIfEstimated(CreateChronologyResultsAsync);
                }
            }
            else if (e.PropertyName == nameof(UnivariateDistribution.Alpha))
            {
                // Alpha (exceedance probability) change re-evaluates the nonstationary
                // chronology at the new probability from the existing posterior; no MCMC
                // re-run required.
                if (_univariateDistribution.IsNonstationary)
                    ReprocessIfEstimated(CreateChronologyResultsAsync);
            }

            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Handles property changes on the <see cref="BayesianAnalysis"/> object.
        /// Loss of estimated state clears results; PointEstimator changes update the
        /// point-estimate output without re-running the chain; CredibleIntervalWidth
        /// changes reprocess derived frequency results without re-running the chain
        /// (the MCMC output is independent of the CI level), and additionally reprocess
        /// chronology results on nonstationary fits since the chronology CI bands depend
        /// on CredibleIntervalWidth. All other notifications propagate for UI binding.
        /// </summary>
        private void BayesianAnalysis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BayesianAnalysis.IsEstimated))
            {
                if (BayesianAnalysis.IsEstimated == false)
                {
                    ClearResults();
                }
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(BayesianAnalysis.PointEstimator))
            {
                ReprocessIfEstimated(UpdatePointEstimateResultsAsync);
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(BayesianAnalysis.CredibleIntervalWidth))
            {
                ReprocessIfEstimated(CreateFrequencyAnalysisResultsAsync);
                // Chronology CI bands are also derived from CredibleIntervalWidth
                // (see CreateChronologyResultsAsync's alpha calculation).
                if (_univariateDistribution.IsNonstationary)
                    ReprocessIfEstimated(CreateChronologyResultsAsync);
                RaisePropertyChange(e.PropertyName);
            }
            else
            {
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Returns the maximum (last) index of the data frame's full time series, or
        /// <c>null</c> when the data frame has not been initialized yet, has no full
        /// time series, or contains no observations. Used by <see cref="Model_PropertyChanged"/>
        /// to decide whether a <see cref="UnivariateDistribution.ParameterTimeIndex"/> change
        /// crosses the period-of-record boundary and therefore changes the Chronology forecast extent.
        /// </summary>
        /// <returns>The last data-frame index, or <c>null</c> when unavailable.</returns>
        private int? TryGetMaxDataIndex()
        {
            var df = _univariateDistribution?.DataFrame;
            if (df?.FullTimeSeries == null || df.FullTimeSeries.Count == 0)
                return null;
            return df.FullTimeSeries.Last().Index;
        }

        /// <summary>
        /// Clears all analysis results and resets the <c>IsEstimated</c> flag.
        /// </summary>
        public void ClearResults()
        {
            BayesianAnalysis?.ClearResults();
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
            ChronologyAnalysisResults = null;
            RaisePropertyChange(nameof(ChronologyAnalysisResults));
            IsEstimated = false;
        }

        /// <summary>
        /// Clears <see cref="AnalysisResults"/> only — the frequency/quantile output whose
        /// evaluation grid is <see cref="ProbabilityOrdinates"/>.
        /// </summary>
        /// <remarks>
        /// Leaves the Bayesian MCMC output (<see cref="BayesianAnalysis"/>.Results),
        /// <see cref="ChronologyAnalysisResults"/>, and <c>IsEstimated</c> intact.
        /// Called when ordinates become invalid — the fit survives and can be reused once
        /// valid ordinates are restored.
        /// </remarks>
        public void ClearFrequencyAnalysisResults()
        {
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <inheritdoc/>
        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            if (Validate().IsValid == false)
                throw new InvalidOperationException("Analysis is not valid. Please check the configuration before running the analysis.");

            // Preview event, allow GUI to cancel before we start
            var previewArgs = new CancelEventArgs();
            OnAnalysisStarting(previewArgs);
            if (previewArgs.Cancel)
            {
                OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(
                    wasCanceled: true,
                    succeeded: false,
                    error: null));
                return;
            }

            // Set up cancellation token
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Wait for any in-flight reprocess to finish before clearing results and
            // starting a new MCMC run. Without this gate, a fire-and-forget reprocess
            // (triggered by a prior property change via ReprocessIfEstimated) can be
            // inside its parallel loop when ClearResults() nulls AnalysisResults —
            // producing an NRE on the next AnalysisResults dereference inside the loop body.
            await _reprocessGate.WaitAsync();
            try
            {
                ClearResults();
                progressReporter?.IndicateTaskStart();
                AnalysisProgress.ReportStarting(progressReporter);

                bool wasCanceled = false;
                Exception? error = null;

                try
                {
                    // Prepare input data
                    UnivariateDistribution.DataFrame.ProcessThresholdSeries();
                    if (UnivariateDistribution.IsNonstationary == true)
                        UnivariateDistribution.DataFrame.CreateFullTimeSeries();
                    UnivariateDistribution.ProcessQuantilePriors();

                    // Run Bayesian analysis
                    await BayesianAnalysis.RunAsync(AnalysisProgress.CreateEstimatorReporter(progressReporter, nameof(BayesianAnalysis)), false);

                    // Post-process
                    if (BayesianAnalysis.IsEstimated == true)
                    {
                        AnalysisProgress.ReportProcessingResults(progressReporter);
                        await CreateFrequencyAnalysisResultsAsync();
                        await CreateChronologyResultsAsync();
                    }

                    // Mirror the inner Bayesian fit's success state so a silently-failed
                    // MCMC (e.g., sampler returned without setting IsEstimated due to
                    // a soft failure) is reported correctly to AnalysisCompleted.
                    IsEstimated = BayesianAnalysis.IsEstimated;
                    if (IsEstimated)
                    {
                        AnalysisProgress.ReportComplete(progressReporter);
                    }
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

                    bool succeeded = IsEstimated && !wasCanceled && error == null;

                    OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(
                        wasCanceled: wasCanceled,
                        succeeded: succeeded,
                        error: error));
                }
            }
            finally
            {
                _reprocessGate.Release();
            }
        }

        /// <inheritdoc/>
        public override void CancelAnalysis()
        {
            base.CancelAnalysis();
            BayesianAnalysis.CancelSimulation();
        }

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetDistribution(int index)
        {

            UnivariateDistributionBase? result = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
                return result;

            if (UnivariateDistribution.IsNonstationary == true)
            {
                var dist = (UnivariateDistribution)UnivariateDistribution.Clone();
                dist.SetParameterValues(BayesianAnalysis.Results.Output[index].Values);
                result = dist.Distribution.Clone();
            }
            else
            {
                var dist = UnivariateDistribution.Distribution.Clone();
                dist.SetParameters(BayesianAnalysis.Results.Output[index].Values);
                result = dist;
            }
            return result;
        }

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetPointEstimateDistribution()
            => GetPointEstimateDistribution(BayesianAnalysis.PointEstimator);

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetPointEstimateDistribution(
            BayesianAnalysis.PointEstimateType pointEstimator)
        {
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
                return null;

            var parms = pointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean
                ? BayesianAnalysis.Results.PosteriorMean.Values
                : BayesianAnalysis.Results.MAP.Values;

            if (UnivariateDistribution.IsNonstationary == true)
            {
                var dist = (UnivariateDistribution)UnivariateDistribution.Clone();
                dist.SetParameterValues(parms);
                return dist.Distribution.Clone();
            }
            else
            {
                var dist = UnivariateDistribution.Distribution.Clone();
                dist.SetParameters(parms);
                return dist;
            }
        }


        /// <summary>
        /// Updates the point estimate results using the current <see cref="BayesianAnalysis.PointEstimator"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method recalculates the mode curve and goodness-of-fit metrics (AIC, BIC, DIC, RMSE)
        /// based on either the posterior mean or MAP estimate.
        /// </para>
        /// </remarks>
        public async Task UpdatePointEstimateResultsAsync()
        {
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || 
                BayesianAnalysis.Results == null || AnalysisResults == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                // Set the point estimator
                if (BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean)
                {
                    UnivariateDistribution.SetParameterValues(BayesianAnalysis.Results.PosteriorMean.Values);
                }
                else
                {
                    UnivariateDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);
                }

                // Update mode curve
                AnalysisResults.ModeCurve = new double[ProbabilityOrdinates.Count];
                for (int i = 0; i < ProbabilityOrdinates.Count; i++)
                    AnalysisResults.ModeCurve[i] = UnivariateDistribution.Distribution.InverseCDF(1 - ProbabilityOrdinates[i]);

                // Information criteria. AIC/BIC are computed at the MAP estimate
                // using the full log-likelihood (data + prior) — with uniform priors
                // this matches the conventional MLE-based AIC/BIC; with informative
                // priors the metric reflects the prior contribution as well, which
                // is intentional in a Bayesian-first framework where model
                // comparison includes the priors.
                var logL = UnivariateDistribution.LogLikelihood(BayesianAnalysis.Results.MAP.Values);
                var k = UnivariateDistribution.NumberOfParameters;
                var n = UnivariateDistribution.DataFrame.TotalRecordLength();
                var aic = GoodnessOfFit.AIC(k, logL);
                var bic = GoodnessOfFit.BIC(n, k, logL);
                var dic = BayesianAnalysis.DIC;

                // RMSE
                var values = UnivariateDistribution.DataFrame.ExactSeries.ValuesToList();
                values.AddRange(UnivariateDistribution.DataFrame.UncertainSeries.ValuesToList());
                values.AddRange(UnivariateDistribution.DataFrame.IntervalSeries.ValuesToList());
                var probs = UnivariateDistribution.DataFrame.ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
                probs.AddRange(UnivariateDistribution.DataFrame.UncertainSeries.Select(x => x.PlottingPositionComplement));
                probs.AddRange(UnivariateDistribution.DataFrame.IntervalSeries.Select(x => x.PlottingPositionComplement));
                var rmse = GoodnessOfFit.RMSE(values, probs, UnivariateDistribution.Distribution);

                AnalysisResults.AIC = aic;
                AnalysisResults.BIC = bic;
                AnalysisResults.DIC = dic;
                AnalysisResults.RMSE = rmse;
            });

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Creates the frequency analysis results from the Bayesian posterior samples.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method computes credible intervals and summary statistics for quantiles
        /// at each probability ordinate using the MCMC posterior samples.
        /// </para>
        /// </remarks>
        public async Task CreateFrequencyAnalysisResultsAsync()
        {
            AnalysisResults = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }

            await Task.Run(() =>
            {
                UnivariateDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);

                // Get sampled distributions for each MCMC output
                int B = BayesianAnalysis.OutputLength;
                var sampledDistributions = new UnivariateDistributionBase[B];
                if (UnivariateDistribution.IsNonstationary == true)
                {
                    Parallel.For(0, B, AnalysisProgress.CreateParallelOptions(), idx =>
                    {
                        var ud = (UnivariateDistribution)UnivariateDistribution.Clone();
                        ud.SetParameterValues(BayesianAnalysis.Results.Output[idx].Values);
                        sampledDistributions[idx] = ud.Distribution.Clone();
                    });
                }
                else
                {
                    Parallel.For(0, B, AnalysisProgress.CreateParallelOptions(), idx =>
                    {
                        var d = UnivariateDistribution.Distribution.Clone();
                        d.SetParameters(BayesianAnalysis.Results.Output[idx].Values);
                        sampledDistributions[idx] = d;
                    });
                }

                // Create uncertainty analysis results
                AnalysisResults = new UncertaintyAnalysisResults(UnivariateDistribution.Distribution!, 
                                                                sampledDistributions,
                                                                ProbabilityOrdinates.Select(p => 1.0 - p).ToArray(), 
                                                                1 - BayesianAnalysis.CredibleIntervalWidth);

            });

            await UpdatePointEstimateResultsAsync();
        }

        /// <summary>
        /// Creates the chronology results for nonstationary models showing time-varying return levels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is only applicable for nonstationary distributions. It computes credible intervals
        /// for return levels at each time step in the full time series.
        /// </para>
        /// </remarks>
        public async Task CreateChronologyResultsAsync()
        {
            ChronologyAnalysisResults = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || 
                BayesianAnalysis.Results == null || UnivariateDistribution.IsNonstationary == false)
            {
                RaisePropertyChange(nameof(ChronologyAnalysisResults));
                return;
            }

            await Task.Run(() =>
            {
                // Set the point estimator
                if (BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean)
                {
                    UnivariateDistribution.SetParameterValues(BayesianAnalysis.Results.PosteriorMean.Values);
                }
                else
                {
                    UnivariateDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);
                }

                ChronologyAnalysisResults = new UncertaintyAnalysisResults();
                ChronologyAnalysisResults.ParentDistribution = UnivariateDistribution.Distribution.Clone();
                ChronologyAnalysisResults.ModeCurve = UnivariateDistribution.GetNonstationaryReturnLevel()!;

                int realz = BayesianAnalysis.OutputLength;
                int length = ChronologyAnalysisResults.ModeCurve.Length;
                var ts = new double[realz, length];

                Parallel.For(0, realz, AnalysisProgress.CreateParallelOptions(), idx =>
                {
                    var ud = (UnivariateDistribution)UnivariateDistribution.Clone();
                    ud.SetParameterValues(BayesianAnalysis.Results!.Output[idx].Values);
                    ts.SetRow(idx, ud.GetNonstationaryReturnLevel()!);
                });

                var mean = new double[length];
                var ci = new double[length, 2];
                double a = (1 - BayesianAnalysis.CredibleIntervalWidth) / 2;

                Parallel.For(0, ts.GetLength(1), AnalysisProgress.CreateParallelOptions(), idx =>
                {
                    var data = ts.GetColumn(idx);
                    Array.Sort(data);
                    mean[idx] = Statistics.ParallelMean(data);
                    ci[idx, 0] = Statistics.Percentile(data, a, true);
                    ci[idx, 1] = Statistics.Percentile(data, 1 - a, true);
                });

                ChronologyAnalysisResults.MeanCurve = mean;
                ChronologyAnalysisResults.ConfidenceIntervals = ci;

            });

            RaisePropertyChange(nameof(ChronologyAnalysisResults));
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            // Validate univariate distribution
            var distValid = UnivariateDistribution.Validate();
            if (!distValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(distValid.ValidationMessages);
            }

            // Validate probability ordinates
            var probOrdValid = ProbabilityOrdinates.Validate();
            if (!probOrdValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(probOrdValid.ValidationMessages);
            }

            // Validate Bayesian analysis
            var bayesValid = BayesianAnalysis.Validate();
            if (!bayesValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(bayesValid.ValidationMessages);
            }

            return (isValid, messageList);
        }

        /// <summary>
        /// Serializes the analysis configuration and results to an XML element.
        /// </summary>
        /// <returns>
        /// An <see cref="XElement"/> representing the current analysis configuration
        /// and, if available, the Bayesian analysis results.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The XML representation does not include the underlying <c>UnivariateDistribution</c>
        /// or computed results (<see cref="AnalysisResults"/>, <see cref="ChronologyAnalysisResults"/>).
        /// It stores:
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>ProbabilityOrdinates</c> as a delimited string.</description></item>
        /// <item><description><c>BayesianAnalysis</c> configuration and MCMC results.</description></item>
        /// </list>
        /// </remarks>
        public XElement ToXElement()
        {
            var root = new XElement("UnivariateAnalysis",
                new XAttribute("IsEstimated", IsEstimated),
                new XElement("ProbabilityOrdinates",
                    ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter)));

            // Bayesian analysis
            if (BayesianAnalysis != null)
            {
                root.Add(BayesianAnalysis.ToXElement());
            }

            return root;
        }

        #endregion
    }
}
