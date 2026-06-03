using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling;
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
    /// Performs Bayesian MCMC estimation for a mixture distribution model given input data.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This analysis fits a <see cref="MixtureModel"/> using Bayesian MCMC methods with
    /// Expectation-Maximization (EM) initialization. It produces uncertainty quantification
    /// for frequency analysis (quantiles at specified exceedance probabilities).
    /// </para>
    /// <para>
    /// The mixture distribution combines multiple component distributions with estimated
    /// mixture weights, optionally supporting zero inflation for data with point masses at zero.
    /// </para>
    /// <para>
    /// The class implements <see cref="IAnalysis"/> and <see cref="IUnivariateAnalysis"/>.
    /// </para>
    /// </remarks>
    public class MixtureAnalysis : AnalysisBase, IUnivariateAnalysis
    {

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="MixtureAnalysis"/> class
        /// for the specified mixture distribution model.
        /// </summary>
        /// <param name="mixtureDistribution">
        /// The <see cref="MixtureModel"/> to be estimated.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="mixtureDistribution"/> is <c>null</c>.
        /// </exception>
        public MixtureAnalysis(MixtureModel mixtureDistribution)
        {
            MixtureDistribution = mixtureDistribution ?? throw new ArgumentNullException(nameof(mixtureDistribution));
            BayesianAnalysis = new BayesianAnalysis(mixtureDistribution);
            ProbabilityOrdinates = new ProbabilityOrdinates();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MixtureAnalysis"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="mixtureDistribution">
        /// The <see cref="MixtureModel"/> associated with this analysis.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration and results.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="mixtureDistribution"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public MixtureAnalysis(MixtureModel mixtureDistribution, XElement xElement,
                               MCMCResults? mcmcResults = null,
                               UncertaintyAnalysisResults? analysisResults = null)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            MixtureDistribution = mixtureDistribution ?? throw new ArgumentNullException(nameof(mixtureDistribution));

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
                BayesianAnalysis = new BayesianAnalysis(mixtureDistribution, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis(mixtureDistribution);
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

        private MixtureModel _mixtureDistribution = new MixtureModel();
        private BayesianAnalysis _bayesianAnalysis = null!;
        private ProbabilityOrdinates _probabilityOrdinates = new ProbabilityOrdinates();


        /// <summary>
        /// Gets or sets the mixture distribution model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the distribution changes, the analysis subscribes to its
        /// <see cref="MixtureModel.PropertyChanged"/> event and updates
        /// the associated <see cref="BayesianAnalysis"/> model reference.
        /// </para>
        /// </remarks>
        public MixtureModel MixtureDistribution
        {
            get { return _mixtureDistribution; }
            private set
            {
                if (_mixtureDistribution != null)
                {
                    _mixtureDistribution.PropertyChanged -= Model_PropertyChanged;
                }

                _mixtureDistribution = value;

                if (_mixtureDistribution != null)
                {
                    if (_bayesianAnalysis != null)
                        _bayesianAnalysis.Model = _mixtureDistribution;

                    _mixtureDistribution.PropertyChanged += Model_PropertyChanged;
                }

                RaisePropertyChange(nameof(MixtureDistribution));
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
        /// and posterior sampling for the <see cref="MixtureDistribution"/>.
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
        /// Ordinates drive only <see cref="AnalysisResults"/> â€” not MCMC or <see cref="IsEstimated"/>.
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
        /// Handles property changes on the <see cref="MixtureDistribution"/> model.
        /// Only structurally destructive changes (data, mixture composition, parameters,
        /// zero-inflation toggle) clear results. All other notifications are propagated for
        /// UI binding without invalidating the fit.
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MixtureDistribution.Parameters) ||
                e.PropertyName == nameof(MixtureDistribution.SetDefaultParameters) ||
                e.PropertyName == nameof(MixtureDistribution.DataFrame) ||
                e.PropertyName == nameof(MixtureDistribution.Mixture) ||
                e.PropertyName == nameof(MixtureDistribution.IsZeroInflated))
            {
                if (BayesianAnalysis.UseSimulationDefaults)
                    BayesianAnalysis.SetDefaultSimulationOptions();

                if (BayesianAnalysis.UseAdvancedSimulationDefaults)
                    BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

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
        /// Clears <see cref="AnalysisResults"/> only â€” the frequency/quantile output whose
        /// evaluation grid is <see cref="ProbabilityOrdinates"/>. Leaves MCMC output and
        /// <see cref="IsEstimated"/> intact.
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

                bool wasCanceled = false;
                Exception? error = null;

                try
                {
                    // Prepare input data
                    MixtureDistribution.DataFrame.ProcessThresholdSeries();
                    MixtureDistribution.ProcessQuantilePriors();

                    // Set up sampler with EM initialization
                    BayesianAnalysis.SetUpSampler();
                    var sampler = BayesianAnalysis.Sampler!;
                    sampler.Initialize = MCMCSampler.InitializationType.UserDefined;

                    await Task.Run(() =>
                    {
                        try
                        {
                            // Get initial parameters and covariance from Expectation-Maximization
                            MixtureDistribution.ExpectationMaximization(out var parameters, out var covariance, out var iterations);

                            int K = MixtureDistribution.Mixture!.Distributions.Length;
                            int Np = parameters.Length - K;
                            var prng = new MersenneTwister(sampler.PRNGSeed);
                            var tempPopulation = new List<ParameterSet>();

                            // Build separate proposal distributions for weights and component parameters.
                            // Weights are sampled from a Dirichlet distribution centered on the EM estimates,
                            // which guarantees samples lie on the simplex (positive, sum to 1). This avoids
                            // the near-singular covariance matrix that arises from the multinomial approximation
                            // when all K weights are included in a single multivariate normal.
                            Dirichlet? weightDirichlet = null;
                            if (K > 1)
                            {
                                int N = MixtureDistribution.DataFrame!.TotalRecordLength();
                                // Effective concentration sum: alpha_i = w_i * S gives Dirichlet mean = EM weights.
                                // S = N-1 matches the multinomial variance approximation. Dividing by a deflation
                                // factor widens the distribution, analogous to the covariance inflation for components.
                                double S = Math.Max(N - 1, K + 1);
                                double deflation = 1.5;
                                var alpha = new double[K];
                                for (int j = 0; j < K; j++)
                                    alpha[j] = Math.Max(parameters[j] * S / deflation, 0.1);
                                weightDirichlet = new Dirichlet(alpha);
                            }

                            // Component parameters sampled from a multivariate normal using the
                            // Fisher information sub-block of the EM covariance, inflated by 1.5Ã—.
                            var emComponents = new double[Np];
                            var componentCovar = new double[Np, Np];
                            for (int i = 0; i < Np; i++)
                            {
                                emComponents[i] = parameters[i + K];
                                for (int j = 0; j < Np; j++)
                                    componentCovar[i, j] = covariance[i + K, j + K] * 1.5;
                            }
                            var componentMvn = new MultivariateNormal(emComponents, componentCovar);

                            // Randomly sample from the proposal distributions
                            for (int i = 0; i < sampler.InitialIterations; i++)
                            {
                                // Sample weights from Dirichlet (always valid: positive and sum to 1)
                                double[] weights;
                                if (weightDirichlet != null)
                                {
                                    var wSample = weightDirichlet.GenerateRandomValues(1, prng.Next());
                                    weights = new double[K];
                                    for (int j = 0; j < K; j++)
                                        weights[j] = wSample[0, j];
                                }
                                else
                                {
                                    // Single component â€” weight is always 1.0
                                    weights = new double[] { 1.0 };
                                }

                                // Sample component parameters from MVN (may need retry for invalid params)
                                bool failed = true;
                                int failedCount = 0;
                                double[]? p = null;
                                double lh = double.NegativeInfinity;

                                while (failed)
                                {
                                    try
                                    {
                                        var comp = componentMvn.InverseCDF(prng.NextDoubles(1, Np).GetRow(0));

                                        // Concatenate weights and component parameters
                                        p = new double[K + Np];
                                        Array.Copy(weights, 0, p, 0, K);
                                        Array.Copy(comp, 0, p, K, Np);

                                        lh = MixtureDistribution.LogLikelihood(p);
                                        failed = false;
                                    }
                                    catch (Exception ex)
                                    {
                                        // The sampled component parameters were invalid, try again.
                                        Debug.WriteLine($"MixtureAnalysis.PriorPredictiveCheck: invalid component draw {failedCount + 1}: {ex.Message}");
                                        failedCount++;
                                        if (failedCount > 20)
                                            break;
                                    }
                                }

                                if (failed)
                                    throw new Exception("Bad parameters");

                                sampler.PopulationMatrix.Add(new ParameterSet(p!, lh));
                                tempPopulation.Add(new ParameterSet(p!, lh));
                            }

                            // Sort temp population by log-likelihood in descending order
                            tempPopulation.Sort((x, y) => -1 * x.Fitness.CompareTo(y.Fitness));

                            // Set the initial vectors to the best performing parameter sets
                            for (int i = 0; i < sampler.NumberOfChains; i++)
                            {
                                sampler.MarkovChains[i].Add(tempPopulation[i].Clone());
                            }
                        }
                        catch (Exception ex)
                        {
                            // If there is an error during initialization, fall back to random initialization
                            Debug.WriteLine($"MixtureAnalysis initialization failed, using random initialization: {ex.Message}");
                            sampler.Initialize = MCMCSampler.InitializationType.Randomize;
                        }
                    }, token);

                    // Run Bayesian analysis
                    await BayesianAnalysis.RunAsync(progressReporter, false);

                    // Post-process
                    if (BayesianAnalysis.IsEstimated == true)
                    {
                        progressReporter?.ReportProgress(100);
                        await CreateFrequencyAnalysisResultsAsync();
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

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetDistribution(int index)
        {
            UnivariateDistributionBase? result = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
                return result;

            var dist = MixtureDistribution.Mixture!.Clone();
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

            var dist = MixtureDistribution.Mixture!.Clone();
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
                    MixtureDistribution.SetParameterValues(BayesianAnalysis.Results.PosteriorMean.Values);
                }
                else
                {
                    MixtureDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);
                }

                // Update mode curve
                AnalysisResults!.ModeCurve = new double[ProbabilityOrdinates.Count];
                for (int i = 0; i < ProbabilityOrdinates.Count; i++)
                    AnalysisResults.ModeCurve[i] = MixtureDistribution.Mixture!.InverseCDF(1 - ProbabilityOrdinates[i]);

                // Information criteria
                var logL = MixtureDistribution.LogLikelihood(BayesianAnalysis.Results.MAP.Values);
                var k = MixtureDistribution.NumberOfParameters;
                var n = MixtureDistribution.DataFrame.TotalRecordLength();
                // AIC/BIC at MAP using full LogLikelihood (data + prior); see
                // UnivariateAnalysis for the convention rationale.
                var aic = GoodnessOfFit.AIC(k, logL);
                var bic = GoodnessOfFit.BIC(n, k, logL);
                var dic = BayesianAnalysis.DIC;

                // RMSE
                var values = MixtureDistribution.DataFrame.ExactSeries.ValuesToList();
                values.AddRange(MixtureDistribution.DataFrame.UncertainSeries.ValuesToList());
                values.AddRange(MixtureDistribution.DataFrame.IntervalSeries.ValuesToList());
                var probs = MixtureDistribution.DataFrame.ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
                probs.AddRange(MixtureDistribution.DataFrame.UncertainSeries.Select(x => x.PlottingPositionComplement));
                probs.AddRange(MixtureDistribution.DataFrame.IntervalSeries.Select(x => x.PlottingPositionComplement));
                var rmse = GoodnessOfFit.RMSE(values, probs, MixtureDistribution.Mixture!);

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
                MixtureDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);

                // Get sampled distributions for each MCMC output
                int B = BayesianAnalysis.OutputLength;
                var sampledDistributions = new UnivariateDistributionBase[B];
                Parallel.For(0, B, idx =>
                {
                    var d = (Mixture)MixtureDistribution.Mixture!.Clone();
                    var parms = BayesianAnalysis.Results!.Output[idx].Values;
                    d.SetParameters(ref parms);
                    sampledDistributions[idx] = d;
                });

                // Create uncertainty analysis results
                AnalysisResults = new UncertaintyAnalysisResults(MixtureDistribution.Mixture!,
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

            // Validate mixture distribution model
            var distValid = MixtureDistribution.Validate();
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
        /// The XML representation does not include the underlying <see cref="MixtureDistribution"/>
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
            var root = new XElement("MixtureAnalysis",
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
