using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling;
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
    /// Performs Bayesian MCMC estimation for a competing risks distribution model given input data.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This analysis fits a <see cref="CompetingRisksModel"/> using Bayesian MCMC methods.
    /// It produces uncertainty quantification for frequency analysis (quantiles at specified
    /// exceedance probabilities).
    /// </para>
    /// <para>
    /// The competing risks distribution combines multiple independent parent distributions where
    /// the observed maximum is the result of competing processes (e.g., different storm types
    /// generating annual maximum floods).
    /// </para>
    /// <para>
    /// The class implements <see cref="IAnalysis"/> and <see cref="IUnivariateAnalysis"/>.
    /// </para>
    /// </remarks>
    public class CompetingRiskAnalysis : AnalysisBase, IUnivariateAnalysis
    {

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="CompetingRiskAnalysis"/> class
        /// for the specified competing risks distribution model.
        /// </summary>
        /// <param name="competingRisksDistribution">
        /// The <see cref="CompetingRisksModel"/> to be estimated.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="competingRisksDistribution"/> is <c>null</c>.
        /// </exception>
        public CompetingRiskAnalysis(CompetingRisksModel competingRisksDistribution)
        {
            CompetingRisksDistribution = competingRisksDistribution ?? throw new ArgumentNullException(nameof(competingRisksDistribution));
            BayesianAnalysis = new BayesianAnalysis(competingRisksDistribution);
            ProbabilityOrdinates = new ProbabilityOrdinates();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompetingRiskAnalysis"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="competingRisksDistribution">
        /// The <see cref="CompetingRisksModel"/> associated with this analysis.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration and results.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="competingRisksDistribution"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public CompetingRiskAnalysis(CompetingRisksModel competingRisksDistribution, XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            CompetingRisksDistribution = competingRisksDistribution ?? throw new ArgumentNullException(nameof(competingRisksDistribution));

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
                BayesianAnalysis = new BayesianAnalysis(competingRisksDistribution, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis(competingRisksDistribution);
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

        private CompetingRisksModel _competingRisksDistribution = null!;
        private BayesianAnalysis _bayesianAnalysis = null!;
        private ProbabilityOrdinates _probabilityOrdinates = new ProbabilityOrdinates();


        /// <summary>
        /// Gets or sets the competing risks distribution model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the distribution changes, the analysis subscribes to its
        /// <c>PropertyChanged</c> event and updates
        /// the associated <see cref="BayesianAnalysis"/> model reference.
        /// </para>
        /// </remarks>
        public CompetingRisksModel CompetingRisksDistribution
        {
            get { return _competingRisksDistribution; }
            private set
            {
                if (_competingRisksDistribution != null)
                {
                    _competingRisksDistribution.PropertyChanged -= Model_PropertyChanged;
                }

                _competingRisksDistribution = value;

                if (_competingRisksDistribution != null)
                {
                    if (_bayesianAnalysis != null)
                        _bayesianAnalysis.Model = _competingRisksDistribution;

                    _competingRisksDistribution.PropertyChanged += Model_PropertyChanged;
                }

                RaisePropertyChange(nameof(CompetingRisksDistribution));
            }
        }

        /// <inheritdoc/>
        public ProbabilityOrdinates ProbabilityOrdinates
        {
            get => _probabilityOrdinates;
            private set
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
        /// and posterior sampling for the <see cref="CompetingRisksDistribution"/>.
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
        /// Handles property changes on the <see cref="CompetingRisksDistribution"/> model.
        /// Structural changes and prior-configuration changes clear results. All other
        /// notifications are propagated for UI binding without invalidating the fit.
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CompetingRisksDistribution.Parameters) ||
                e.PropertyName == nameof(CompetingRisksDistribution.SetDefaultParameters) ||
                e.PropertyName == nameof(CompetingRisksDistribution.DataFrame) ||
                e.PropertyName == nameof(CompetingRisksDistribution.CompetingRisks))
            {
                if (BayesianAnalysis.UseSimulationDefaults)
                    BayesianAnalysis.SetDefaultSimulationOptions();

                if (BayesianAnalysis.UseAdvancedSimulationDefaults)
                    BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

                ClearResults();
            }
            else if (e.PropertyName == nameof(CompetingRisksDistribution.SetDefaultQuantilePriors) ||
                     e.PropertyName == nameof(CompetingRisksDistribution.QuantilePriors) ||
                     e.PropertyName == nameof(CompetingRisksDistribution.EnableQuantilePriors) ||
                     e.PropertyName == nameof(CompetingRisksDistribution.UseSingleQuantile) ||
                     e.PropertyName == nameof(CompetingRisksDistribution.UseJeffreysRuleForScale) ||
                     e.PropertyName == nameof(CompetingRisksDistribution.UseDefaultFlatPriors))
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

            // Wait for any in-flight reprocess to finish before clearing results and
            // starting a new MCMC run. Without this gate, a fire-and-forget reprocess
            // (triggered by a prior property change via ReprocessIfEstimated) can be
            // inside its parallel loop when ClearResults() nulls AnalysisResults �
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
                    CompetingRisksDistribution.DataFrame.ProcessThresholdSeries();
                    CompetingRisksDistribution.ProcessQuantilePriors();

                    // Run Bayesian analysis
                    await BayesianAnalysis.RunAsync(AnalysisProgress.CreateEstimatorReporter(progressReporter, nameof(BayesianAnalysis)), false);

                    // Post-process
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
            UnivariateDistributionBase? result = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
                return result;

            var dist = (CompetingRisks)CompetingRisksDistribution.CompetingRisks!.Clone();
            dist.SetParameters(BayesianAnalysis.Results.Output[index].Values);
            result = dist;

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

            var dist = (CompetingRisks)CompetingRisksDistribution.CompetingRisks!.Clone();
            dist.SetParameters(parms);
            return dist;
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
                    CompetingRisksDistribution.SetParameterValues(BayesianAnalysis.Results.PosteriorMean.Values);
                }
                else
                {
                    CompetingRisksDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);
                }

                // Update mode curve
                AnalysisResults!.ModeCurve = new double[ProbabilityOrdinates.Count];
                for (int i = 0; i < ProbabilityOrdinates.Count; i++)
                    AnalysisResults.ModeCurve[i] = CompetingRisksDistribution.CompetingRisks!.InverseCDF(1 - ProbabilityOrdinates[i]);

                // Information criteria
                // AIC/BIC at MAP using full LogLikelihood (data + prior).
                var logL = CompetingRisksDistribution.LogLikelihood(BayesianAnalysis.Results.MAP.Values);
                var k = CompetingRisksDistribution.NumberOfParameters;
                var n = CompetingRisksDistribution.DataFrame.TotalRecordLength();
                var aic = GoodnessOfFit.AIC(k, logL);
                var bic = GoodnessOfFit.BIC(n, k, logL);
                var dic = BayesianAnalysis.DIC;

                // RMSE
                var values = CompetingRisksDistribution.DataFrame.ExactSeries.ValuesToList();
                values.AddRange(CompetingRisksDistribution.DataFrame.UncertainSeries.ValuesToList());
                values.AddRange(CompetingRisksDistribution.DataFrame.IntervalSeries.ValuesToList());
                var probs = CompetingRisksDistribution.DataFrame.ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
                probs.AddRange(CompetingRisksDistribution.DataFrame.UncertainSeries.Select(x => x.PlottingPositionComplement));
                probs.AddRange(CompetingRisksDistribution.DataFrame.IntervalSeries.Select(x => x.PlottingPositionComplement));
                var rmse = GoodnessOfFit.RMSE(values, probs, CompetingRisksDistribution.CompetingRisks!);

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
                CompetingRisksDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);

                // Get sampled distributions for each MCMC output
                int B = BayesianAnalysis.OutputLength;
                var sampledDistributions = new UnivariateDistributionBase[B];
                Parallel.For(0, B, AnalysisProgress.CreateParallelOptions(), idx =>
                {
                    var d = (CompetingRisks)CompetingRisksDistribution.CompetingRisks!.Clone();
                    d.SetParameters(BayesianAnalysis.Results.Output[idx].Values);
                    sampledDistributions[idx] = d;
                });

                // Create uncertainty analysis results
                AnalysisResults = new UncertaintyAnalysisResults(CompetingRisksDistribution.CompetingRisks!,
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

            // Validate competing risks distribution model
            var distValid = CompetingRisksDistribution.Validate();
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
        /// The XML representation does not include the underlying <see cref="CompetingRisksDistribution"/>
        /// or computed results (<see cref="AnalysisResults"/>).
        /// It stores:
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>ProbabilityOrdinates</c> as a delimited string.</description></item>
        /// <item><description><c>BayesianAnalysis</c> configuration and MCMC results.</description></item>
        /// </list>
        /// </remarks>
        public XElement ToXElement()
        {
            var root = new XElement("CompetingRiskAnalysis",
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
