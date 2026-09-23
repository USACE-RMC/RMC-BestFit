using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Performs Bayesian MCMC estimation for an AR (Autoregressive) time series model.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// The AR(p) model uses lagged values of the series to predict the current value:
    /// Y(t) = µ + f1*(Y(t-1) - µ) + ... + fp*(Y(t-p) - µ) + e(t)
    /// where e(t) ~ N(0, s²).
    /// </para>
    /// <para>
    /// This analysis uses Bayesian Markov Chain Monte Carlo (MCMC) methods to estimate the model parameters
    /// and their uncertainty, producing confidence intervals for both historical fit and forecasts.
    /// </para>
    /// <para>
    /// The class implements <see cref="IAnalysis"/> and extends <see cref="AnalysisBase"/>.
    /// </para>
    /// </remarks>
    public class ARAnalysis : AnalysisBase
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="ARAnalysis"/> class
        /// for the specified AutoRegressive model.
        /// </summary>
        /// <param name="autoRegressive">
        /// The <see cref="AutoRegressive"/> model to be estimated.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="autoRegressive"/> is <c>null</c>.
        /// </exception>
        public ARAnalysis(AutoRegressive autoRegressive)
        {
            AutoRegressive = autoRegressive ?? throw new ArgumentNullException(nameof(autoRegressive));
            BayesianAnalysis = new BayesianAnalysis(autoRegressive);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ARAnalysis"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="autoRegressive">
        /// The <see cref="AutoRegressive"/> model associated with this analysis.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration and results.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="autoRegressive"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public ARAnalysis(AutoRegressive autoRegressive, XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            AutoRegressive = autoRegressive ?? throw new ArgumentNullException(nameof(autoRegressive));

            // Forecasting time steps
            if (xElement.Attribute(nameof(ForecastingTimeSteps)) != null)
                int.TryParse(xElement.Attribute(nameof(ForecastingTimeSteps))!.Value, out _forecastingTimeSteps);

            // Bayesian analysis
            var bayesElement = xElement.Element("BayesianAnalysis");
            if (bayesElement != null)
            {
                BayesianAnalysis = new BayesianAnalysis(autoRegressive, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis(autoRegressive);
            }

            // Check if estimated
            var isEstimatedAttr = xElement.Attribute("IsEstimated");
            if (isEstimatedAttr != null && bool.TryParse(isEstimatedAttr.Value, out bool isEst))
            {
                _isEstimated = isEst;
            }
        }

        #endregion

        #region Members

        private AutoRegressive _autoRegressive = new AutoRegressive();
        private BayesianAnalysis _bayesianAnalysis = null!;
        private int _forecastingTimeSteps = 0;

        /// <summary>
        /// Gets or sets the AutoRegressive time series model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the AutoRegressive model changes, the analysis subscribes to its
        /// <c>PropertyChanged</c> event and updates
        /// the associated <see cref="BayesianAnalysis"/> model reference.
        /// </para>
        /// </remarks>
        public AutoRegressive AutoRegressive
        {
            get { return _autoRegressive; }
            private set
            {
                if (_autoRegressive != null)
                {
                    _autoRegressive.PropertyChanged -= Model_PropertyChanged;
                }

                _autoRegressive = value;

                if (_autoRegressive != null)
                {
                    if (_bayesianAnalysis != null)
                        _bayesianAnalysis.Model = _autoRegressive;

                    _autoRegressive.PropertyChanged += Model_PropertyChanged;
                }

                RaisePropertyChange(nameof(AutoRegressive));
            }
        }

        /// <summary>
        /// Gets the Bayesian MCMC analysis object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="BayesianAnalysis"/> handles all MCMC simulation, convergence diagnostics,
        /// and posterior sampling for the <see cref="AutoRegressive"/> model.
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
        /// Gets or sets the number of time steps to forecast past the end of the observed series.
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
        /// Handles property changes on the <see cref="AutoRegressive"/> model.
        /// Parameter changes trigger recalculation of default simulation options and clear results.
        /// </summary>
        /// <summary>
        /// Handles property changes on the <see cref="AutoRegressive"/> model.
        /// Only structurally destructive changes (parameters, data, order, intercept toggle,
        /// transform, training window, prior toggle) clear results. All other notifications
        /// are propagated for UI binding without invalidating the fit.
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AutoRegressive.Parameters) ||
                e.PropertyName == nameof(AutoRegressive.SetDefaultParameters))
            {
                if (BayesianAnalysis.UseSimulationDefaults)
                    BayesianAnalysis.SetDefaultSimulationOptions();

                if (BayesianAnalysis.UseAdvancedSimulationDefaults)
                    BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

                ClearResults();
            }
            else if (e.PropertyName == nameof(AutoRegressive.TimeSeries) ||
                     e.PropertyName == nameof(AutoRegressive.Order) ||
                     e.PropertyName == nameof(AutoRegressive.IncludeIntercept) ||
                     e.PropertyName == nameof(AutoRegressive.TransformType) ||
                     e.PropertyName == nameof(AutoRegressive.TrainingTimeSteps) ||
                     e.PropertyName == nameof(AutoRegressive.UseDefaultTrainingSteps) ||
                     e.PropertyName == nameof(AutoRegressive.UseJeffreysRuleForScale))
            {
                ClearResults();
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

            // Clamping in the setter guarantees 0 = _forecastingTimeSteps = 100,
            // which is always a valid evaluation domain. Defensive check kept for
            // future-proofing if the clamp is loosened.
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

            // Pick the point estimator on the calling thread.
            double[] parameters = BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean
                ? BayesianAnalysis.Results.PosteriorMean.Values
                : BayesianAnalysis.Results.MAP.Values;

            await Task.Run(() =>
            {
                // AutoRegressive.Predict() reads parameters from model state, so the
                // worker uses a local clone to avoid mutating the shared instance
                // (which would race with any concurrent UI binding read of
                // AutoRegressive.Parameters).
                var localModel = (AutoRegressive)AutoRegressive.Clone();
                localModel.SetParameterValues(parameters);

                // ForecastingTimeSteps = steps past the observed series. Predict needs
                // (holdout + future) steps beyond the fit window so ModeCurve length
                // matches the full observed range plus the future forecast horizon.
                int dataLength = AutoRegressive.TimeSeries.Count;
                int forecastStepsForPredict = (dataLength - AutoRegressive.TrainingTimeSteps) + ForecastingTimeSteps;

                AnalysisResults.ModeCurve = localModel.Predict(forecastStepsForPredict);

                // Get goodness of fit measures
                // RMSE (comparing predicted vs observed for in-sample period only)
                var trueValues = AutoRegressive.TimeSeries.ValuesToArray();
                var modelValues = AnalysisResults.ModeCurve.Subset(0, dataLength - 1);
                var rmse = GoodnessOfFit.RMSE(trueValues, modelValues);

                // AIC/BIC use the data likelihood at MAP and are comparable with MLE
                // criteria only when all active priors are flat.
                double mapLogLH = AutoRegressive.DataLogLikelihood(BayesianAnalysis.Results.MAP.Values);
                AnalysisResults.AIC = GoodnessOfFit.AIC(AutoRegressive.NumberOfParameters, mapLogLH);
                AnalysisResults.BIC = GoodnessOfFit.BIC(dataLength, AutoRegressive.NumberOfParameters, mapLogLH);
                AnalysisResults.DIC = BayesianAnalysis.DIC;
                AnalysisResults.RMSE = rmse;
            });

            // Publish parameters to the shared model on the calling context.
            AutoRegressive.SetParameterValues(parameters);

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Creates the uncertainty analysis results from the Bayesian posterior samples.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method computes credible intervals and summary statistics for predicted time series values
        /// using the MCMC posterior samples. Results include both the in-sample period and any forecasting steps.
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

                // ForecastingTimeSteps = steps past the observed series. Pass (holdout +
                // future) to Predict so the returned array length matches n.
                int dataLength = AutoRegressive.TimeSeries.Count;
                int forecastStepsForPredict = (dataLength - AutoRegressive.TrainingTimeSteps) + ForecastingTimeSteps;
                int n = dataLength + ForecastingTimeSteps;

                AnalysisResults = new UncertaintyAnalysisResults();
                AnalysisResults.ModeCurve = AutoRegressive.Predict(forecastStepsForPredict);
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
                    // Temporarily set parameters for prediction
                    var tempAR = (AutoRegressive)AutoRegressive.Clone();
                    tempAR.SetParameterValues(posterior[idx].Values);
                    var prediction = tempAR.Predict(forecastStepsForPredict, seeds[idx]);
                    series.SetColumn(idx, prediction);
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

            // Validate AutoRegressive model
            var modelValid = AutoRegressive.Validate();
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
        /// The XML representation does not include the underlying <see cref="AutoRegressive"/>
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
            var root = new XElement("ARAnalysis",
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
