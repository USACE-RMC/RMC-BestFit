using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Performs Bayesian MCMC estimation for a stage-discharge rating curve.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// A rating curve is a functional relationship between stage (water surface elevation) and discharge (flow rate).
    /// This analysis uses Bayesian Markov Chain Monte Carlo (MCMC) methods to estimate the rating curve parameters
    /// and their uncertainty, producing confidence intervals for predicted discharge values across a range of stages.
    /// </para>
    /// <para>
    /// The class implements <see cref="IAnalysis"/> and extends <see cref="AnalysisBase"/>.
    /// </para>
    /// </remarks>
    public class RatingCurveAnalysis : AnalysisBase
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="RatingCurveAnalysis"/> class
        /// for the specified rating curve model.
        /// </summary>
        /// <param name="ratingCurve">
        /// The <see cref="RatingCurve"/> model to be estimated.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="ratingCurve"/> is <c>null</c>.
        /// </exception>
        public RatingCurveAnalysis(RatingCurve ratingCurve)
        {
            RatingCurve = ratingCurve ?? throw new ArgumentNullException(nameof(ratingCurve));
            BayesianAnalysis = new BayesianAnalysis(ratingCurve);
            SetDefaultStageBins();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RatingCurveAnalysis"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="ratingCurve">
        /// The <see cref="RatingCurve"/> model associated with this analysis.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration and results.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="ratingCurve"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        /// <param name="mcmcResults">Optional MCMC results to restore from a previous estimation.</param>
        /// <param name="analysisResults">Optional analysis results to restore from a previous estimation.</param>
        public RatingCurveAnalysis(RatingCurve ratingCurve, XElement xElement,
                                   MCMCResults? mcmcResults = null,
                                   UncertaintyAnalysisResults? analysisResults = null)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            RatingCurve = ratingCurve ?? throw new ArgumentNullException(nameof(ratingCurve));

            // Stage bins
            if (xElement.Attribute(nameof(MinStage)) != null)
                double.TryParse(xElement.Attribute(nameof(MinStage))!.Value, out _minStage);
            if (xElement.Attribute(nameof(MaxStage)) != null)
                double.TryParse(xElement.Attribute(nameof(MaxStage))!.Value, out _maxStage);
            if (xElement.Attribute(nameof(StageBins)) != null)
                int.TryParse(xElement.Attribute(nameof(StageBins))!.Value, out _stageBins);
            if (xElement.Attribute(nameof(UseDefaultStageBins)) != null)
                bool.TryParse(xElement.Attribute(nameof(UseDefaultStageBins))!.Value, out _useDefaultStageBins);

            // Bayesian analysis
            var bayesElement = xElement.Element("BayesianAnalysis");
            if (bayesElement != null)
            {
                BayesianAnalysis = new BayesianAnalysis(ratingCurve, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis(ratingCurve);
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
        }

        #endregion

        #region Members

        private RatingCurve _ratingCurve = new RatingCurve();
        private BayesianAnalysis _bayesianAnalysis = null!;
        private double _minStage = 0;
        private double _maxStage = 100;
        private int _stageBins = 100;
        private bool _useDefaultStageBins = true;

        /// <summary>
        /// Gets or sets the rating curve model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the rating curve changes, the analysis subscribes to its
        /// <see cref="RatingCurve.PropertyChanged"/> event and updates
        /// the associated <see cref="BayesianAnalysis"/> model reference.
        /// </para>
        /// </remarks>
        public RatingCurve RatingCurve
        {
            get { return _ratingCurve; }
            private set
            {
                if (_ratingCurve != null)
                {
                    _ratingCurve.PropertyChanged -= Model_PropertyChanged;
                }

                _ratingCurve = value;

                if (_ratingCurve != null)
                {
                    if (_bayesianAnalysis != null)
                        _bayesianAnalysis.Model = _ratingCurve;

                    _ratingCurve.PropertyChanged += Model_PropertyChanged;
                }

                RaisePropertyChange(nameof(RatingCurve));
            }
        }

        /// <summary>
        /// Gets the Bayesian MCMC analysis object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="BayesianAnalysis"/> handles all MCMC simulation, convergence diagnostics,
        /// and posterior sampling for the <see cref="RatingCurve"/> model.
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
        /// Gets or sets the minimum stage value to evaluate in the output rating curve.
        /// </summary>
        [Category("Output")]
        [DisplayName("Minimum Stage")]
        [Description("The minimum stage (water level) value to evaluate in the output rating curve.")]
        [Browsable(true)]
        public double MinStage
        {
            get { return _minStage; }
            set
            {
                if (_minStage != value)
                {
                    _minStage = value;
                    RaisePropertyChange(nameof(MinStage));
                    ReprocessOrClearUncertaintyGrid();
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum stage value to evaluate in the output rating curve.
        /// </summary>
        [Category("Output")]
        [DisplayName("Maximum Stage")]
        [Description("The maximum stage (water level) value to evaluate in the output rating curve.")]
        [Browsable(true)]
        public double MaxStage
        {
            get { return _maxStage; }
            set
            {
                if (_maxStage != value)
                {
                    _maxStage = value;
                    RaisePropertyChange(nameof(MaxStage));
                    ReprocessOrClearUncertaintyGrid();
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of stage bins used for constructing the output rating curve.
        /// </summary>
        [Category("Output")]
        [DisplayName("Stage Bins")]
        [Description("The number of stage bins (evaluation points) used for constructing the rating curve output.")]
        [Browsable(true)]
        public int StageBins
        {
            get { return _stageBins; }
            set
            {
                if (_stageBins != value)
                {
                    _stageBins = value;
                    RaisePropertyChange(nameof(StageBins));
                    ReprocessOrClearUncertaintyGrid();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether default stage bins are automatically calculated based on the input data range.
        /// </summary>
        [Category("Output")]
        [DisplayName("Use Default Stage Bins")]
        [Description("Specifies whether default stage bins are automatically calculated from the stage data range with 10% padding.")]
        [Browsable(true)]
        public bool UseDefaultStageBins
        {
            get { return _useDefaultStageBins; }
            set
            {
                if (_useDefaultStageBins != value)
                {
                    _useDefaultStageBins = value;
                    RaisePropertyChange(nameof(UseDefaultStageBins));
                    if (_useDefaultStageBins)
                        SetDefaultStageBins();
                }
            }
        }

        /// <summary>
        /// Gets the uncertainty analysis results containing confidence intervals for predicted discharge values.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> until <see cref="RunAsync"/> completes successfully.
        /// It contains posterior distributions of predicted discharge, credible intervals, and goodness-of-fit metrics.
        /// </para>
        /// </remarks>
        public UncertaintyAnalysisResults? AnalysisResults { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Handles property changes on the <see cref="RatingCurve"/> model.
        /// Only structurally destructive changes (parameters, data, segmentation, prior
        /// toggles) clear results. All other notifications are propagated for UI binding
        /// without invalidating the fit.
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RatingCurve.Parameters) ||
                e.PropertyName == nameof(RatingCurve.SetDefaultParameters))
            {
                if (BayesianAnalysis.UseSimulationDefaults)
                    BayesianAnalysis.SetDefaultSimulationOptions();

                if (BayesianAnalysis.UseAdvancedSimulationDefaults)
                    BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

                ClearResults();
            }
            else if (e.PropertyName == nameof(RatingCurve.StageData))
            {
                // Clear FIRST so BayesianAnalysis.IsEstimated is already false by the
                // time SetDefaultStageBins() raises MinStage/MaxStage/StageBins. Any
                // App-side handler that reacts to those bin changes will see the
                // cleared state and short-circuit instead of racing on stale posterior
                // samples.
                ClearResults();
                if (UseDefaultStageBins)
                    SetDefaultStageBins();
            }
            else if (e.PropertyName == nameof(RatingCurve.DischargeData) ||
                     e.PropertyName == nameof(RatingCurve.NumberOfSegments) ||
                     e.PropertyName == nameof(RatingCurve.UseJeffreysRuleForScale) ||
                     e.PropertyName == nameof(RatingCurve.UseDefaultFlatPriors))
            {
                ClearResults();
            }

            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Handles property changes on the <see cref="BayesianAnalysis"/> object.
        /// Updates point estimate results when the point estimator type changes.
        /// CredibleIntervalWidth changes reprocess the uncertainty grid without re-running
        /// the chain (the rating-curve CI ribbon must rebuild at the new alpha).
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
                ReprocessIfEstimated(CreateUncertaintyAnalysisResultsAsync);
                RaisePropertyChange(e.PropertyName);
            }
            else
            {
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Sets default stage bins based on the range of the stage data with 10% padding on each end.
        /// </summary>
        private void SetDefaultStageBins()
        {
            if (RatingCurve?.StageData != null && RatingCurve.StageData.Count > 0)
            {
                double min = RatingCurve.StageData.MinValue();
                double max = RatingCurve.StageData.MaxValue();
                double range = max - min;
                _minStage = min - 0.1 * range;
                _maxStage = max + 0.1 * range;
            }
            else
            {
                _minStage = 0;
                _maxStage = 100;
            }
            _stageBins = 100;

            RaisePropertyChange(nameof(MinStage));
            RaisePropertyChange(nameof(MaxStage));
            RaisePropertyChange(nameof(StageBins));
        }

        /// <summary>
        /// Clears all analysis results and resets the <see cref="IsEstimated"/> flag.
        /// </summary>
        public void ClearResults()
        {
            BayesianAnalysis?.ClearResults();
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
            IsEstimated = false;
        }

        /// <summary>
        /// Clears <see cref="AnalysisResults"/> only â€” the uncertainty grid output whose
        /// evaluation domain is <see cref="MinStage"/> / <see cref="MaxStage"/> /
        /// <see cref="StageBins"/>.
        /// </summary>
        /// <remarks>
        /// Leaves the Bayesian MCMC output (<see cref="BayesianAnalysis"/>.Results) and
        /// <see cref="IsEstimated"/> intact. Called when the grid becomes invalid â€” the
        /// fit survives and can be reused once a valid grid is restored.
        /// </remarks>
        public void ClearUncertaintyAnalysisResults()
        {
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Reprocess the uncertainty grid in the background after a grid-property change
        /// (<see cref="MinStage"/> / <see cref="MaxStage"/> / <see cref="StageBins"/>),
        /// or null out <see cref="AnalysisResults"/> if the new grid is invalid. Preserves
        /// the underlying MCMC fit in either case.
        /// </summary>
        /// <remarks>
        /// Validity bounds match <see cref="Validate"/>: MinStage and MaxStage finite with
        /// MinStage &lt; MaxStage, and 10 â‰¤ StageBins â‰¤ 1000. Reprocess is fire-and-forget
        /// on the default task scheduler â€” exceptions are logged via <see cref="Debug"/>
        /// and do not propagate to the setter.
        /// </remarks>
        private void ReprocessOrClearUncertaintyGrid()
        {
            if (!IsEstimated) return;

            bool valid = !double.IsNaN(_minStage) && !double.IsNaN(_maxStage)
                      && _minStage < _maxStage
                      && _stageBins >= 10 && _stageBins <= 1000;

            if (valid)
                ReprocessIfEstimated(CreateUncertaintyAnalysisResultsAsync);
            else
                ClearUncertaintyAnalysisResults();
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

                bool wasCanceled = false;
                Exception? error = null;

                try
                {
                    // Run Bayesian analysis
                    await BayesianAnalysis.RunAsync(progressReporter, false);

                    // Post-process
                    if (BayesianAnalysis.IsEstimated == true)
                    {
                        progressReporter?.ReportProgress(100);
                        await CreateUncertaintyAnalysisResultsAsync();
                    }

                    // Mirror the inner Bayesian fit's success state so a silently-failed
                    // MCMC is reported correctly to AnalysisCompleted.
                    IsEstimated = BayesianAnalysis.IsEstimated;
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

            // Pick the point estimator on the calling thread so the parameter
            // array is captured by-value into the worker closure.
            double[] parameters = BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean
                ? BayesianAnalysis.Results.PosteriorMean.Values
                : BayesianAnalysis.Results.MAP.Values;

            await Task.Run(() =>
            {
                // RatingCurve.Predict and DataLogLikelihood take the parameter array
                // directly, so the worker does not need to mutate the shared model.
                // Mutating it here would race with any concurrent UI binding read of
                // RatingCurve.Parameters[i].Value.
                int n = StageBins;
                AnalysisResults.ModeCurve = new double[n];

                // Stratify stage bins
                var xBins = Stratify.XValues(new StratificationOptions(MinStage, MaxStage, n - 1));
                var bins = xBins.Select(x => x.LowerBound).ToList();
                bins.Add(xBins.Last().UpperBound);
                for (int i = 0; i < n; i++)
                {
                    AnalysisResults.ModeCurve[i] = RatingCurve.Predict(parameters, bins[i]);
                }

                // Get goodness of fit measures â€” iterate only the date-aligned
                // observations so (trueValue_i, modelValue_i) correspond to the same
                // record and BIC's n matches the fit's actual sample size.
                var aligned = RatingCurve.GetAlignedObservations();
                var trueValues = new List<double>(aligned.Count);
                var modelValues = new double[aligned.Count];
                for (int i = 0; i < aligned.Count; i++)
                {
                    trueValues.Add(aligned[i].Discharge);
                    modelValues[i] = RatingCurve.Predict(parameters, aligned[i].Stage);
                }
                var rmse = GoodnessOfFit.RMSE(trueValues, modelValues);

                // AIC/BIC at MAP using full LogLikelihood (data + prior). With
                // uniform priors this matches MLE-based AIC/BIC; with informative
                // priors the metric includes the prior contribution.
                double mapLogLH = RatingCurve.LogLikelihood(BayesianAnalysis.Results.MAP.Values);
                AnalysisResults.AIC = GoodnessOfFit.AIC(RatingCurve.NumberOfParameters, mapLogLH);
                AnalysisResults.BIC = GoodnessOfFit.BIC(aligned.Count, RatingCurve.NumberOfParameters, mapLogLH);
                AnalysisResults.DIC = BayesianAnalysis.DIC;
                AnalysisResults.RMSE = rmse;
            });

            // Publish parameters to the shared model on the calling context (typically
            // the UI thread when awaited from one), so any UI binding observing
            // RatingCurve.Parameters sees the point-estimate values without racing
            // against the worker thread.
            RatingCurve.SetParameterValues(parameters);

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Creates the uncertainty analysis results from the Bayesian posterior samples.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method computes credible intervals and summary statistics for predicted discharge
        /// at each stage bin using the MCMC posterior samples.
        /// </para>
        /// </remarks>
        public async Task CreateUncertaintyAnalysisResultsAsync()
        {
            AnalysisResults = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }

            // Snapshot the MCMC results before jumping to the thread pool so a
            // concurrent ClearResults() on the UI thread cannot null the reference
            // out from under Parallel.For. Also publish AnalysisResults atomically
            // at the end so consumers never see a half-populated curve.
            var snapshotResults = BayesianAnalysis.Results;
            var output = snapshotResults.Output;
            if (output == null || output.Count == 0)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }
            double[] parameters = snapshotResults.MAP.Values;
            int prngSeed = BayesianAnalysis.PRNGSeed;
            int realz = Math.Min(BayesianAnalysis.OutputLength, output.Count);
            double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;
            int n = StageBins;
            double minStage = MinStage;
            double maxStage = MaxStage;

            UncertaintyAnalysisResults? built = null;
            await Task.Run(() =>
            {
                var results = new UncertaintyAnalysisResults
                {
                    ModeCurve = new double[n],
                    MeanCurve = new double[n],
                    ConfidenceIntervals = new double[n, 3]
                };

                // Stratify stage bins
                var xBins = Stratify.XValues(new StratificationOptions(minStage, maxStage, n - 1));
                var bins = xBins.Select(x => x.LowerBound).ToList();
                bins.Add(xBins.Last().UpperBound);

                var prng = new MersenneTwister(prngSeed);
                var seeds = prng.NextIntegers(realz);

                for (int i = 0; i < n; i++)
                {
                    results.ModeCurve[i] = RatingCurve.Predict(parameters, bins[i]);

                    var q = new double[realz];
                    Parallel.For(0, realz, idx =>
                    {
                        q[idx] = RatingCurve.Predict(output[idx].Values, bins[i], seeds[idx]);
                    });
                    results.MeanCurve[i] = Statistics.ParallelMean(q);
                    Array.Sort(q);
                    results.ConfidenceIntervals[i, 0] = bins[i];
                    results.ConfidenceIntervals[i, 1] = Statistics.Percentile(q, alpha / 2d, true);
                    results.ConfidenceIntervals[i, 2] = Statistics.Percentile(q, 1d - alpha / 2d, true);
                }

                built = results;
            });

            // If the fit was cleared while we were computing, don't publish stale results.
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
            {
                AnalysisResults = null;
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }

            AnalysisResults = built;
            await UpdatePointEstimateResultsAsync();
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            // Validate rating curve model
            var modelValid = RatingCurve.Validate();
            if (!modelValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(modelValid.ValidationMessages);
            }

            // Validate stage bins
            if (double.IsNaN(MinStage))
            {
                isValid = false;
                messageList.Add("Error: Minimum stage value is invalid (NaN).");
            }
            if (double.IsNaN(MaxStage))
            {
                isValid = false;
                messageList.Add("Error: Maximum stage value is invalid (NaN).");
            }
            if (MinStage >= MaxStage)
            {
                isValid = false;
                messageList.Add("Error: Minimum stage must be less than maximum stage.");
            }
            if (StageBins < 10 || StageBins > 1000)
            {
                isValid = false;
                messageList.Add("Error: Number of stage bins must be between 10 and 1,000.");
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
        /// The XML representation does not include the underlying <see cref="RatingCurve"/>
        /// model or computed results (<see cref="AnalysisResults"/>).
        /// It stores:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Stage bin configuration (MinStage, MaxStage, StageBins, UseDefaultStageBins).</description></item>
        /// <item><description><c>BayesianAnalysis</c> configuration and MCMC results.</description></item>
        /// </list>
        /// </remarks>
        public XElement ToXElement()
        {
            var root = new XElement("RatingCurveAnalysis",
                new XAttribute("IsEstimated", IsEstimated),
                new XAttribute(nameof(MinStage), MinStage),
                new XAttribute(nameof(MaxStage), MaxStage),
                new XAttribute(nameof(StageBins), StageBins),
                new XAttribute(nameof(UseDefaultStageBins), UseDefaultStageBins));

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
