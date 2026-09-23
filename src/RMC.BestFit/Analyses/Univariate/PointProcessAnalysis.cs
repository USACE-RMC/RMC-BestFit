using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Utilities;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{

    /// <summary>
    /// Performs Bayesian MCMC estimation for a point process model given peaks-over-threshold (POT) data.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This analysis fits a <see cref="PointProcessModel"/> using Bayesian MCMC methods.
    /// It produces uncertainty quantification for frequency analysis (quantiles at specified
    /// exceedance probabilities). The point process approach models both the arrival rate and
    /// magnitude distribution of threshold exceedances using a competing risks framework.
    /// </para>
    /// <para>
    /// The class implements <see cref="IAnalysis"/> and <see cref="IUnivariateAnalysis"/>.
    /// </para>
    /// </remarks>
    public class PointProcessAnalysis : AnalysisBase, IUnivariateAnalysis
    {

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="PointProcessAnalysis"/> class
        /// for the specified point process model.
        /// </summary>
        /// <param name="pointProcess">
        /// The <see cref="PointProcessModel"/> to be estimated.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="pointProcess"/> is <c>null</c>.
        /// </exception>
        public PointProcessAnalysis(PointProcessModel pointProcess)
        {
            PointProcess = pointProcess ?? throw new ArgumentNullException(nameof(pointProcess));
            BayesianAnalysis = new BayesianAnalysis(pointProcess);
            ProbabilityOrdinates = new ProbabilityOrdinates();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PointProcessAnalysis"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="pointProcess">
        /// The <see cref="PointProcessModel"/> associated with this analysis.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration and results.
        /// </param>
        /// <param name="mcmcResults">Optional persisted MCMC results to restore.</param>
        /// <param name="analysisResults">Optional persisted uncertainty analysis results to restore.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="pointProcess"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public PointProcessAnalysis(PointProcessModel pointProcess, XElement xElement,
                                    MCMCResults? mcmcResults = null,
                                    UncertaintyAnalysisResults? analysisResults = null)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            PointProcess = pointProcess ?? throw new ArgumentNullException(nameof(pointProcess));

            ProbabilityOrdinates = new ProbabilityOrdinates();
            var probElement = xElement.Element("ProbabilityOrdinates");
            if (probElement != null)
            {
                var probText = (string)probElement;
                ProbabilityOrdinates.FromDelimitedString(probText, ProbabilityOrdinates.DefaultDelimiter);
            }

            var bayesElement = xElement.Element("BayesianAnalysis");
            if (bayesElement != null)
            {
                BayesianAnalysis = new BayesianAnalysis(pointProcess, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis(pointProcess);
            }

            var isEstimatedAttr = xElement.Attribute("IsEstimated");
            if (isEstimatedAttr != null && bool.TryParse(isEstimatedAttr.Value, out bool isEst))
            {
                _isEstimated = isEst;
            }

            if (mcmcResults != null)
                _bayesianAnalysis.SetCustomMCMCResults(mcmcResults, skipInformationCriteria: true);

            AnalysisResults = analysisResults;
            NormalizeRestoredEstimatedState();
        }

        #endregion

        #region Members

        private PointProcessModel _pointProcess = new PointProcessModel();
        private BayesianAnalysis _bayesianAnalysis = null!;
        private ProbabilityOrdinates _probabilityOrdinates = new ProbabilityOrdinates();


        /// <summary>
        /// Gets or sets the point process model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the distribution changes, the analysis subscribes to its
        /// <c>PropertyChanged</c> event and updates
        /// the associated <see cref="BayesianAnalysis"/> model reference.
        /// </para>
        /// </remarks>
        public PointProcessModel PointProcess
        {
            get { return _pointProcess; }
            private set
            {
                if (_pointProcess != null)
                {
                    _pointProcess.PropertyChanged -= Model_PropertyChanged;
                }

                _pointProcess = value;

                if (_pointProcess != null)
                {
                    if (_bayesianAnalysis != null)
                        _bayesianAnalysis.Model = _pointProcess;

                    _pointProcess.PropertyChanged += Model_PropertyChanged;
                }

                RaisePropertyChange(nameof(PointProcess));
            }
        }

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
        /// and posterior sampling for the <see cref="PointProcess"/>.
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
        /// Ordinates drive only <see cref="AnalysisResults"/> — not MCMC or <c>IsEstimated</c>.
        /// When estimated and ordinates are valid, reprocess via <see cref="CreateFrequencyAnalysisResultsAsync"/>;
        /// when invalid, clear <see cref="AnalysisResults"/> only; when not estimated, no-op.
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
        /// Handles property changes on the <see cref="PointProcess"/> model.
        /// Structural changes and prior-configuration changes clear results. All other
        /// notifications are propagated for UI binding without invalidating the fit.
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PointProcess.Parameters) ||
                e.PropertyName == nameof(PointProcess.SetDefaultParameters) ||
                e.PropertyName == nameof(PointProcess.DataFrame) ||
                e.PropertyName == nameof(PointProcess.Distribution) ||
                e.PropertyName == nameof(PointProcess.Threshold) ||
                e.PropertyName == nameof(PointProcess.TotalYears) ||
                e.PropertyName == nameof(PointProcess.IsSeasonal) ||
                e.PropertyName == nameof(PointProcess.TimeBlock) ||
                e.PropertyName == nameof(PointProcess.StartMonth))
            {
                if (BayesianAnalysis.UseSimulationDefaults)
                    BayesianAnalysis.SetDefaultSimulationOptions();

                if (BayesianAnalysis.UseAdvancedSimulationDefaults)
                    BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

                ClearResults();
            }
            else if (e.PropertyName == nameof(PointProcess.SetDefaultQuantilePriors) ||
                     e.PropertyName == nameof(PointProcess.QuantilePriors) ||
                     e.PropertyName == nameof(PointProcess.EnableQuantilePriors) ||
                     e.PropertyName == nameof(PointProcess.UseSingleQuantile) ||
                     e.PropertyName == nameof(PointProcess.UseJeffreysRuleForScale) ||
                     e.PropertyName == nameof(PointProcess.UseDefaultFlatPriors))
            {
                ClearResults();
            }

            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Handles property changes on the <see cref="BayesianAnalysis"/> object.
        /// Updates point estimate results when the point estimator type changes.
        /// CredibleIntervalWidth changes reprocess derived frequency results without
        /// re-running the chain (the MCMC output is independent of the CI level).
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
        /// Clears <see cref="AnalysisResults"/> only — the frequency/quantile output whose
        /// evaluation grid is <see cref="ProbabilityOrdinates"/>. Leaves MCMC output and
        /// <c>IsEstimated</c> intact.
        /// </summary>
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
                    PointProcess.DataFrame.ProcessThresholdSeries();
                    PointProcess.ProcessQuantilePriors();

                    await BayesianAnalysis.RunAsync(AnalysisProgress.CreateEstimatorReporter(progressReporter, nameof(BayesianAnalysis)), false);

                    if (BayesianAnalysis.IsEstimated == true)
                    {
                        AnalysisProgress.ReportProcessingResults(progressReporter);
                        await CreateFrequencyAnalysisResultsAsync();
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

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetDistribution(int index)
        {
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
                return null;

            return PointProcess.GetDistribution(BayesianAnalysis.Results.Output[index].Values);
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

            return PointProcess.GetDistribution(parms);
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
                if (BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean)
                {
                    PointProcess.SetParameterValues(BayesianAnalysis.Results.PosteriorMean.Values);
                }
                else
                {
                    PointProcess.SetParameterValues(BayesianAnalysis.Results.MAP.Values);
                }

                AnalysisResults!.ModeCurve = new double[ProbabilityOrdinates.Count];
                for (int i = 0; i < ProbabilityOrdinates.Count; i++)
                    AnalysisResults.ModeCurve[i] = PointProcess.Distribution!.InverseCDF(1 - ProbabilityOrdinates[i]);

                // AIC/BIC use the data likelihood at MAP and are comparable with MLE
                // criteria only when all active priors are flat.
                var logL = PointProcess.DataLogLikelihood(BayesianAnalysis.Results.MAP.Values);
                var k = PointProcess.NumberOfParameters;
                var n = PointProcess.DataFrame.TotalRecordLength();
                var aic = GoodnessOfFit.AIC(k, logL);
                var bic = GoodnessOfFit.BIC(n, k, logL);
                var dic = BayesianAnalysis.DIC;

                var values = PointProcess.DataFrame.ExactSeries.ValuesToList();
                values.AddRange(PointProcess.DataFrame.UncertainSeries.ValuesToList());
                values.AddRange(PointProcess.DataFrame.IntervalSeries.ValuesToList());
                var probs = PointProcess.DataFrame.ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
                probs.AddRange(PointProcess.DataFrame.UncertainSeries.Select(x => x.PlottingPositionComplement));
                probs.AddRange(PointProcess.DataFrame.IntervalSeries.Select(x => x.PlottingPositionComplement));
                var rmse = GoodnessOfFit.RMSE(values, probs, PointProcess.Distribution!);

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
                PointProcess.SetParameterValues(BayesianAnalysis.Results.MAP.Values);

                int B = BayesianAnalysis.OutputLength;
                var sampledDistributions = new UnivariateDistributionBase[B];
                Parallel.For(0, B, AnalysisProgress.CreateParallelOptions(), idx =>
                {
                    var parms = BayesianAnalysis.Results.Output[idx].Values;
                    var d = PointProcess.GetDistribution(parms);
                    sampledDistributions[idx] = d;
                });

                AnalysisResults = new UncertaintyAnalysisResults(PointProcess.Distribution!,
                                                                sampledDistributions,
                                                                ProbabilityOrdinates.Select(p => 1.0 - p).ToArray(),
                                                                1 - BayesianAnalysis.CredibleIntervalWidth);

            });

            await UpdatePointEstimateResultsAsync();
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            var distValid = PointProcess.Validate();
            if (!distValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(distValid.ValidationMessages);
            }

            var probOrdValid = ProbabilityOrdinates.Validate();
            if (!probOrdValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(probOrdValid.ValidationMessages);
            }

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
        /// The XML representation does not include the underlying <see cref="PointProcess"/>,
        /// posterior samples, or computed <see cref="AnalysisResults"/>. It stores:
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>ProbabilityOrdinates</c> as a delimited string.</description></item>
        /// <item><description><c>BayesianAnalysis</c> configuration and scalar summary state.</description></item>
        /// </list>
        /// </remarks>
        public XElement ToXElement()
        {
            var root = new XElement("PointProcessAnalysis",
                new XAttribute("IsEstimated", IsEstimated),
                new XElement("ProbabilityOrdinates",
                    ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter)));

            if (BayesianAnalysis != null)
            {
                root.Add(BayesianAnalysis.ToXElement());
            }

            return root;
        }

        #endregion
    }
}
