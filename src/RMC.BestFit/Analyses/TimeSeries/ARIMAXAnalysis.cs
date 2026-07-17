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
    /// Performs Bayesian MCMC estimation for an ARIMAX (Autoregressive Moving Average with eXogenous variables) time series model.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// The ARIMAX model extends ARIMA by incorporating exogenous (external) variables as predictors.
    /// This analysis uses Bayesian Markov Chain Monte Carlo (MCMC) methods to estimate the model parameters
    /// and their uncertainty, producing confidence intervals for both historical fit and forecasts.
    /// </para>
    /// <para>
    /// Model structure: Y(t) = µ + ?(t) + ?(t) + ß*X(t) + f*Y(t-p) + ?*e(t-q) + e(t)
    /// where µ is the intercept, ?(t) is the trend, ?(t) is seasonality, ß*X(t) are exogenous covariates,
    /// f*Y(t-p) is the autoregressive component, ?*e(t-q) is the moving average component, and e(t) is white noise.
    /// </para>
    /// <para>
    /// The class implements <see cref="IAnalysis"/> and extends <see cref="AnalysisBase"/>.
    /// </para>
    /// </remarks>
    public class ARIMAXAnalysis : AnalysisBase
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="ARIMAXAnalysis"/> class
        /// for the specified ARIMAX model.
        /// </summary>
        /// <param name="armax">
        /// The <see cref="ARIMAX"/> model to be estimated.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="armax"/> is <c>null</c>.
        /// </exception>
        public ARIMAXAnalysis(ARIMAX armax)
        {
            ARIMAX = armax ?? throw new ArgumentNullException(nameof(armax));
            BayesianAnalysis = new BayesianAnalysis(armax);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ARIMAXAnalysis"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="armax">
        /// The <see cref="ARIMAX"/> model associated with this analysis.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration and results.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="armax"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        /// <param name="mcmcResults">Optional MCMC results to restore from a previous estimation.</param>
        /// <param name="analysisResults">Optional analysis results to restore from a previous estimation.</param>
        public ARIMAXAnalysis(ARIMAX armax, XElement xElement,
                              MCMCResults? mcmcResults = null,
                              UncertaintyAnalysisResults? analysisResults = null)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            ARIMAX = armax ?? throw new ArgumentNullException(nameof(armax));

            // Forecasting time steps
            if (xElement.Attribute(nameof(ForecastingTimeSteps)) != null)
                int.TryParse(xElement.Attribute(nameof(ForecastingTimeSteps))!.Value, out _forecastingTimeSteps);

            // Bayesian analysis
            var bayesElement = xElement.Element("BayesianAnalysis");
            if (bayesElement != null)
            {
                BayesianAnalysis = new BayesianAnalysis(armax, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis(armax);
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
            NormalizeRestoredEstimatedState();
        }

        #endregion

        #region Members

        private ARIMAX _armax = new ARIMAX();
        private BayesianAnalysis _bayesianAnalysis = null!;
        private int _forecastingTimeSteps = 0;

        /// <summary>
        /// Gets or sets the ARIMAX time series model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the ARIMAX model changes, the analysis subscribes to its
        /// <c>PropertyChanged</c> event and updates
        /// the associated <see cref="BayesianAnalysis"/> model reference.
        /// </para>
        /// </remarks>
        public ARIMAX ARIMAX
        {
            get { return _armax; }
            private set
            {
                if (_armax != null)
                {
                    _armax.PropertyChanged -= Model_PropertyChanged;
                }

                _armax = value;

                if (_armax != null)
                {
                    if (_bayesianAnalysis != null)
                        _bayesianAnalysis.Model = _armax;

                    _armax.PropertyChanged += Model_PropertyChanged;
                }

                RaisePropertyChange(nameof(ARIMAX));
            }
        }

        /// <summary>
        /// Gets the Bayesian MCMC analysis object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="BayesianAnalysis"/> handles all MCMC simulation, convergence diagnostics,
        /// and posterior sampling for the <see cref="ARIMAX"/> model.
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
        /// Gets or sets the number of time steps to forecast beyond the training period.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Number of steps to forecast <b>past the end of the observed series</b>. The
        /// predictive window always covers the full observed range (training + validation)
        /// plus <see cref="ForecastingTimeSteps"/> additional future steps. Must be in
        /// the range <c>[0, 100]</c>.
        /// </para>
        /// </remarks>
        [Category("Output")]
        [DisplayName("Forecasting Time Steps")]
        [Description("Number of steps to forecast past the end of the observed series. Range: 0 (no future forecast) to 100.")]
        [Browsable(true)]
        public int ForecastingTimeSteps
        {
            get { return _forecastingTimeSteps; }
            set
            {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (_forecastingTimeSteps != clamped)
                {
                    _forecastingTimeSteps = clamped;
                    RaisePropertyChange(nameof(ForecastingTimeSteps));
                    ReprocessOrClearForecast();
                }
            }
        }

        /// <summary>
        /// Gets the uncertainty analysis results containing confidence intervals for time series predictions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> until <see cref="RunAsync"/> completes successfully.
        /// It contains posterior distributions of predicted values, credible intervals, and goodness-of-fit metrics.
        /// </para>
        /// </remarks>
        public UncertaintyAnalysisResults? AnalysisResults { get; private set; }

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
        /// Handles property changes on the <see cref="ARIMAX"/> model.
        /// Parameter changes trigger recalculation of default simulation options and clear results.
        /// </summary>
        /// <summary>
        /// Handles property changes on the <see cref="ARIMAX"/> model.
        /// Only structurally destructive changes (parameters, data, order, intercept,
        /// seasonality, trend, transform, training window, exogenous covariates, prior
        /// toggle) clear results. All other notifications are propagated for UI binding
        /// without invalidating the fit.
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ARIMAX.Parameters) ||
                e.PropertyName == nameof(ARIMAX.SetDefaultParameters))
            {
                if (BayesianAnalysis.UseSimulationDefaults)
                    BayesianAnalysis.SetDefaultSimulationOptions();

                if (BayesianAnalysis.UseAdvancedSimulationDefaults)
                    BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

                ClearResults();
            }
            else if (e.PropertyName == nameof(ARIMAX.TimeSeries) ||
                     e.PropertyName == nameof(ARIMAX.AROrderP) ||
                     e.PropertyName == nameof(ARIMAX.DiffOrderD) ||
                     e.PropertyName == nameof(ARIMAX.MAOrderQ) ||
                     e.PropertyName == nameof(ARIMAX.XOrderB) ||
                     e.PropertyName == nameof(ARIMAX.IncludeIntercept) ||
                     e.PropertyName == nameof(ARIMAX.IncludeSeasonality) ||
                     e.PropertyName == nameof(ARIMAX.TrendType) ||
                     e.PropertyName == nameof(ARIMAX.TransformType) ||
                     e.PropertyName == nameof(ARIMAX.TrainingTimeSteps) ||
                     e.PropertyName == nameof(ARIMAX.UseDefaultTrainingSteps) ||
                     e.PropertyName == nameof(ARIMAX.Covariates) ||
                     e.PropertyName == nameof(ARIMAX.UseJeffreysRuleForScale))
            {
                ClearResults();
            }
            else if (e.PropertyName == nameof(ARIMAX.CovariateExtension))
            {
                // CovariateExtension affects only how covariates are extrapolated for the
                // forecast period (block bootstrap, kNN, etc.) — it does not enter the
                // likelihood. Reprocess the forecast at the new method without re-running
                // the chain. (Pre-Phase-6 catch-all cleared instead; this is the corrected
                // post-processing-only treatment.)
                ReprocessIfEstimated(CreateUncertaintyAnalysisResultsAsync);
            }

            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Handles property changes on the <see cref="BayesianAnalysis"/> object.
        /// Updates point estimate results when the point estimator type changes.
        /// CredibleIntervalWidth changes reprocess the forecast CI ribbon without re-running
        /// the chain.
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
        /// Clears all analysis results and resets the <c>IsEstimated</c> flag.
        /// </summary>
        public void ClearResults()
        {
            BayesianAnalysis?.ClearResults();
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
            IsEstimated = false;
        }

        /// <summary>
        /// Clears <see cref="AnalysisResults"/> only — the uncertainty/forecast output
        /// whose horizon is <see cref="ForecastingTimeSteps"/>.
        /// </summary>
        /// <remarks>
        /// Leaves the Bayesian MCMC output (<see cref="BayesianAnalysis"/>.Results) and
        /// <c>IsEstimated</c> intact. Called when the horizon is set to a value
        /// that would produce no derived output (e.g., a defensive fallback) — the fit
        /// survives and reprocesses on the next valid horizon change.
        /// </remarks>
        public void ClearUncertaintyAnalysisResults()
        {
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Reprocess the uncertainty / forecast output in the background after a
        /// <see cref="ForecastingTimeSteps"/> change, or null out
        /// <see cref="AnalysisResults"/> if the new horizon is invalid. Preserves
        /// the underlying MCMC fit in either case.
        /// </summary>
        /// <remarks>
        /// Validity matches the setter clamp (0 = horizon = 100). Reprocess is
        /// fire-and-forget on the default task scheduler — exceptions are logged
        /// via <see cref="Debug"/> and do not propagate to the setter.
        /// </remarks>
        private void ReprocessOrClearForecast()
        {
            if (!IsEstimated) return;

            if (_forecastingTimeSteps >= 0 && _forecastingTimeSteps <= 100)
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
            // (triggered by a prior property change) can be inside its parallel loop
            // when ClearResults() nulls AnalysisResults — producing an NRE on the next
            // AnalysisResults dereference inside the loop body.
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
                    // Run Bayesian analysis
                    await BayesianAnalysis.RunAsync(AnalysisProgress.CreateEstimatorReporter(progressReporter, nameof(BayesianAnalysis)), false);

                    // Post-process. The base-class gate is held throughout RunAsync, so
                    // CreateUncertaintyAnalysisResultsAsync runs without contention here —
                    // its body (and the UpdatePointEstimateResultsAsync it chains to) does
                    // not itself acquire the gate, so there is no re-entrant deadlock.
                    if (BayesianAnalysis.IsEstimated == true)
                    {
                        AnalysisProgress.ReportProcessingResults(progressReporter);
                        await CreateUncertaintyAnalysisResultsAsync();
                    }

                    // Mirror the inner Bayesian fit's success state so a silently-failed
                    // MCMC is reported correctly to AnalysisCompleted.
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
                // ARIMAX.Predict and DataLogLikelihood take the parameter array
                // directly; mutating shared model state from the worker would race
                // with any concurrent UI binding read of ARIMAX.Parameters.
                int dataLength = ARIMAX.TimeSeries.Count;
                int forecastStepsForPredict = (dataLength - ARIMAX.TrainingTimeSteps) + ForecastingTimeSteps;
                AnalysisResults.ModeCurve = ARIMAX.Predict(parameters, forecastStepsForPredict).Y;

                // Get goodness of fit measures
                // RMSE (comparing predicted vs observed for training period only)
                var trueValues = ARIMAX.TimeSeries.ValuesToArray().Subset(0, ARIMAX.TrainingTimeSteps - 1);
                var modelValues = AnalysisResults.ModeCurve.Subset(0, ARIMAX.TrainingTimeSteps - 1);
                var rmse = GoodnessOfFit.RMSE(trueValues, modelValues);

                // AIC/BIC at MAP using full LogLikelihood (data + prior).
                double mapLogLH = ARIMAX.LogLikelihood(BayesianAnalysis.Results.MAP.Values);
                AnalysisResults.AIC = GoodnessOfFit.AIC(ARIMAX.NumberOfParameters, mapLogLH);
                AnalysisResults.BIC = GoodnessOfFit.BIC(ARIMAX.TrainingTimeSteps, ARIMAX.NumberOfParameters, mapLogLH);
                AnalysisResults.DIC = BayesianAnalysis.DIC;
                AnalysisResults.RMSE = rmse;
            });

            // Publish parameters to the shared model on the calling context.
            ARIMAX.SetParameterValues(parameters);

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Creates the uncertainty analysis results from the Bayesian posterior samples.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method computes credible intervals and summary statistics for predicted time series values
        /// using the MCMC posterior samples. Results include both the training period and any forecasting steps.
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

            // Snapshot posterior + MAP references on the calling thread. Reprocess is
            // fire-and-forget; a concurrent BayesianAnalysis.ClearResults() (e.g. from
            // a rapid second property edit, undo replay, or chained property change)
            // sets Results = null, which would NRE the parallel loop below if it
            // dereferenced BayesianAnalysis.Results.* directly inside the closure.
            var posterior = BayesianAnalysis.Results.Output;
            var mapValues = BayesianAnalysis.Results.MAP.Values;
            if (posterior == null || posterior.Count == 0)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }

            await Task.Run(() =>
            {
                // Set the point estimator
                double[] parameters = mapValues;

                // ForecastingTimeSteps = steps past the observed series. Result arrays
                // cover the full observed range plus ForecastingTimeSteps future steps.
                int dataLength = ARIMAX.TimeSeries.Count;
                int forecastStepsForPredict = (dataLength - ARIMAX.TrainingTimeSteps) + ForecastingTimeSteps;
                int n = dataLength + ForecastingTimeSteps;

                AnalysisResults = new UncertaintyAnalysisResults();
                AnalysisResults.ModeCurve = ARIMAX.Predict(parameters, forecastStepsForPredict).Y;
                AnalysisResults.MeanCurve = new double[n];
                AnalysisResults.ConfidenceIntervals = new double[n, 3];

                var prng = new MersenneTwister(BayesianAnalysis.PRNGSeed);
                // Bind realz to the actual posterior length, not the configured
                // OutputLength — guards against a partial run / restore where
                // OutputLength > Output.Count.
                var realz = Math.Min(BayesianAnalysis.OutputLength, posterior.Count);
                double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;
                var seeds = prng.NextIntegers(realz);

                // Generate realizations from posterior
                var series = new double[n, realz];
                Parallel.For(0, realz, AnalysisProgress.CreateParallelOptions(), idx =>
                {
                    var prediction = ARIMAX.Predict(posterior[idx].Values, forecastStepsForPredict, seeds[idx]);
                    series.SetColumn(idx, prediction.Y);
                });

                // Compute summary statistics
                Parallel.For(0, n, AnalysisProgress.CreateParallelOptions(), idx =>
                {
                    var y = series.GetRow(idx);
                    Array.Sort(y);
                    AnalysisResults.MeanCurve[idx] = Statistics.ParallelMean(y);
                    AnalysisResults.ConfidenceIntervals[idx, 0] = idx; // Time index
                    AnalysisResults.ConfidenceIntervals[idx, 1] = Statistics.Percentile(y, alpha / 2d, true);
                    AnalysisResults.ConfidenceIntervals[idx, 2] = Statistics.Percentile(y, 1d - alpha / 2d, true);
                });
            });

            await UpdatePointEstimateResultsAsync();
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            // Validate ARIMAX model
            var modelValid = ARIMAX.Validate();
            if (!modelValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(modelValid.ValidationMessages);
            }

            // Validate forecasting time steps
            if (ForecastingTimeSteps < 0)
            {
                isValid = false;
                messageList.Add("Error: Forecasting time steps must be non-negative.");
            }
            if (ForecastingTimeSteps > 100)
            {
                isValid = false;
                messageList.Add("Error: Forecasting time steps must not exceed 100.");
            }

            // Forecasting with covariates requires a method for generating covariate values
            // beyond the observed series. CovariateExtension = None is valid only when
            // the covariate is already long enough to cover the full horizon
            // (TrainingTimeSteps + validation + ForecastingTimeSteps). Surface the problem
            // here as a validation error so the UI disables Run, instead of throwing at
            // Predict time.
            if (ARIMAX.Covariates != null && ARIMAX.Covariates.Count > 0
                && ARIMAX.CovariateExtension == ARIMAX.CovariateExtensionMethod.None
                && ARIMAX.TimeSeries != null)
            {
                int neededLength = ARIMAX.TimeSeries.Count + ForecastingTimeSteps;
                for (int i = 0; i < ARIMAX.Covariates.Count; i++)
                {
                    if (ARIMAX.Covariates[i].Count < neededLength)
                    {
                        isValid = false;
                        messageList.Add(
                            $"Error: Covariate {i + 1} has {ARIMAX.Covariates[i].Count} observations; " +
                            $"with CovariateExtension = None and ForecastingTimeSteps = {ForecastingTimeSteps}, " +
                            $"{neededLength} are required. Set CovariateExtension to BlockBootstrap or KNN, " +
                            $"extend the covariate, or set ForecastingTimeSteps = 0.");
                        break;
                    }
                }
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
        /// The XML representation does not include the underlying <see cref="ARIMAX"/>
        /// model or computed results (<see cref="AnalysisResults"/>).
        /// It stores:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Forecasting time steps configuration.</description></item>
        /// <item><description><c>BayesianAnalysis</c> configuration and MCMC results.</description></item>
        /// </list>
        /// </remarks>
        public XElement ToXElement()
        {
            var root = new XElement("ARIMAXAnalysis",
                new XAttribute("IsEstimated", IsEstimated),
                new XAttribute(nameof(ForecastingTimeSteps), ForecastingTimeSteps));

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
