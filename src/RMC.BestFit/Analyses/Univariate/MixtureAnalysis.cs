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
    /// This analysis fits a <see cref="MixtureModel"/> using Bayesian MCMC methods. It uses
    /// Expectation-Maximization to identify a reliable mixture basin and initializes the sampler
    /// from an inflated approximation in the identified K-1 weight coordinates before producing
    /// frequency-analysis uncertainty quantification.
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

        /// <summary>
        /// Fixed covariance multiplier used to overdisperse the local EM approximation.
        /// </summary>
        /// <remarks>
        /// This matches the competing-risk initialization policy.
        /// </remarks>
        private const double InitializationCovarianceInflationFactor = 1.5d;

        /// <summary>
        /// Maximum number of replacement draws attempted after an invalid EM-population draw.
        /// </summary>
        /// <remarks>
        /// This matches the competing-risk feasibility-retry policy.
        /// </remarks>
        private const int MaximumInitializationReplacementDraws = 20;

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
        /// <param name="mcmcResults">Optional persisted MCMC results to restore.</param>
        /// <param name="analysisResults">Optional persisted uncertainty analysis results to restore.</param>
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
            NormalizeRestoredEstimatedState();
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
        /// <c>PropertyChanged</c> event and updates
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
        /// Handles property changes on the <see cref="MixtureDistribution"/> model.
        /// Structural changes and prior-configuration changes clear results. All other
        /// notifications are propagated for UI binding without invalidating the fit.
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
            else if (e.PropertyName == nameof(MixtureDistribution.SetDefaultQuantilePriors) ||
                     e.PropertyName == nameof(MixtureDistribution.QuantilePriors) ||
                     e.PropertyName == nameof(MixtureDistribution.EnableQuantilePriors) ||
                     e.PropertyName == nameof(MixtureDistribution.UseSingleQuantile) ||
                     e.PropertyName == nameof(MixtureDistribution.UseJeffreysRuleForScale) ||
                     e.PropertyName == nameof(MixtureDistribution.UseDefaultFlatPriors))
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
                    // Prepare input data
                    MixtureDistribution.DataFrame.ProcessThresholdSeries();
                    MixtureDistribution.ProcessQuantilePriors();

                    // Set up the default DEMCzs sampler, then replace only its initialization
                    // population with an overdispersed identified EM approximation.
                    BayesianAnalysis.SetUpSampler();
                    var sampler = BayesianAnalysis.Sampler!;
                    await ConfigureEmSeededInitializationAsync(sampler, token);

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

        /// <summary>
        /// Configures a mixture sampler with an identified, EM-seeded population.
        /// </summary>
        /// <param name="sampler">The already configured production MCMC sampler.</param>
        /// <param name="cancellationToken">Token used to cancel initialization before MCMC begins.</param>
        /// <returns>A task that completes when initialization succeeds or the sampler is reset to randomized initialization.</returns>
        /// <remarks>
        /// The public EM method remains an approximate-MLE estimator with full-K output. The
        /// sampler drops the derived final-weight coordinate and uses the matching principal
        /// covariance block. Population fitness still evaluates the complete posterior, including
        /// the configured prior on the derived weight. All DEMCzs settings remain unchanged.
        /// </remarks>
        private async Task ConfigureEmSeededInitializationAsync(
            MCMCSampler sampler,
            CancellationToken cancellationToken)
        {
            sampler.Initialize = MCMCSampler.InitializationType.UserDefined;

            await Task.Run(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    MixtureDistribution.ExpectationMaximization(
                        out double[] emParameters,
                        out double[,] emCovariance,
                        out _);
                    GetIdentifiedEmApproximation(
                        MixtureDistribution,
                        emParameters,
                        emCovariance,
                        out double[] sampledParameters,
                        out double[,] sampledCovariance);
                    PopulateSamplerFromEmApproximation(
                        MixtureDistribution,
                        sampler,
                        sampledParameters,
                        sampledCovariance,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    ResetSamplerToRandomizedInitialization(sampler);
                    Debug.WriteLine(
                        $"MixtureAnalysis EM initialization failed, using random initialization: {ex.Message}");
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Removes the derived final-weight coordinate from a public full-K EM approximation.
        /// </summary>
        /// <param name="model">The mixture model that defines the component count.</param>
        /// <param name="publicParameters">The public full-K EM parameter vector.</param>
        /// <param name="publicCovariance">The public full-K EM covariance matrix.</param>
        /// <param name="sampledParameters">The identified K-1 sampler center.</param>
        /// <param name="sampledCovariance">The identified K-1 sampler covariance.</param>
        /// <exception cref="ArgumentException">Thrown when the public approximation dimensions are inconsistent.</exception>
        private static void GetIdentifiedEmApproximation(
            MixtureModel model,
            double[] publicParameters,
            double[,] publicCovariance,
            out double[] sampledParameters,
            out double[,] sampledCovariance)
        {
            int publicCount = model.NumberOfParameters;
            if (publicParameters.Length != publicCount ||
                publicCovariance.GetLength(0) != publicCount ||
                publicCovariance.GetLength(1) != publicCount)
            {
                throw new ArgumentException(
                    "The public EM center and covariance must match the mixture model dimensions.",
                    nameof(publicCovariance));
            }

            int componentCount = model.Mixture?.Distributions.Length ?? 0;
            if (componentCount <= 1)
            {
                sampledParameters = publicParameters.ToArray();
                sampledCovariance = (double[,])publicCovariance.Clone();
                return;
            }

            int derivedWeightIndex = componentCount - 1;
            int sampledCount = publicCount - 1;
            sampledParameters = new double[sampledCount];
            sampledCovariance = new double[sampledCount, sampledCount];
            int sampledRow = 0;
            for (int publicRow = 0; publicRow < publicCount; publicRow++)
            {
                if (publicRow == derivedWeightIndex) continue;
                sampledParameters[sampledRow] = publicParameters[publicRow];
                int sampledColumn = 0;
                for (int publicColumn = 0; publicColumn < publicCount; publicColumn++)
                {
                    if (publicColumn == derivedWeightIndex) continue;
                    sampledCovariance[sampledRow, sampledColumn++] = publicCovariance[publicRow, publicColumn];
                }
                sampledRow++;
            }
        }

        /// <summary>
        /// Clears a failed custom population and restores randomized sampler initialization.
        /// </summary>
        /// <param name="sampler">The sampler to reset.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sampler"/> is <c>null</c>.</exception>
        /// <remarks>
        /// This seam keeps the final fallback atomic and supports fast state-contract testing
        /// without executing EM, MAP, or MCMC.
        /// </remarks>
        internal static void ResetSamplerToRandomizedInitialization(MCMCSampler sampler)
        {
            ArgumentNullException.ThrowIfNull(sampler);
            sampler.Reset();
            sampler.Initialize = MCMCSampler.InitializationType.Randomize;
        }

        /// <summary>
        /// Populates an MCMC sampler from an inflated identified EM approximation.
        /// </summary>
        /// <param name="model">The mixture model used to evaluate the full posterior.</param>
        /// <param name="sampler">The configured sampler that receives the population and chain states.</param>
        /// <param name="centerParameters">The K-1 EM parameter vector.</param>
        /// <param name="covariance">The K-1 EM covariance.</param>
        /// <param name="cancellationToken">Token used to cancel population generation.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required input is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when the approximation dimensions do not match.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a feasible initial population cannot be generated.</exception>
        /// <remarks>
        /// Population fitness expands each candidate before using <see cref="MixtureModel.LogLikelihood(double[])"/>, so
        /// parameter, Jeffreys-scale, and quantile priors participate in ranking even
        /// when the likelihood-only EM approximation is used as a fallback center.
        /// </remarks>
        internal static void PopulateSamplerFromEmApproximation(
            MixtureModel model,
            MCMCSampler sampler,
            double[] centerParameters,
            double[,] covariance,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(sampler);
            ArgumentNullException.ThrowIfNull(centerParameters);
            ArgumentNullException.ThrowIfNull(covariance);

            int parameterCount = centerParameters.Length;
            if (parameterCount != sampler.NumberOfParameters ||
                covariance.GetLength(0) != parameterCount ||
                covariance.GetLength(1) != parameterCount)
            {
                throw new ArgumentException(
                    "The center parameter and covariance dimensions must match the identified sampler.",
                    nameof(covariance));
            }

            sampler.Reset();
            sampler.Initialize = MCMCSampler.InitializationType.UserDefined;

            var inflatedCovariance = new double[parameterCount, parameterCount];
            for (int row = 0; row < parameterCount; row++)
            {
                for (int column = 0; column < parameterCount; column++)
                {
                    inflatedCovariance[row, column] =
                            covariance[row, column] * InitializationCovarianceInflationFactor;
                }
            }

            var proposal = new MultivariateNormal(centerParameters, inflatedCovariance);
            var prng = new MersenneTwister(sampler.PRNGSeed);
            var population = new List<ParameterSet>(sampler.InitialIterations);

            for (int populationIndex = 0; populationIndex < sampler.InitialIterations; populationIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double[]? proposalParameters = null;
                double logPosterior = double.NegativeInfinity;
                bool isFeasible = false;

                for (int attempt = 0; attempt <= MaximumInitializationReplacementDraws; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        proposalParameters = populationIndex == 0 && attempt == 0
                            ? centerParameters.ToArray()
                            : proposal.InverseCDF(prng.NextDoubles(1, parameterCount).GetRow(0));
                        logPosterior = model.SamplingLogLikelihood(proposalParameters);
                        isFeasible = Tools.IsFinite(logPosterior);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"MixtureAnalysis initialization draw {attempt + 1} was invalid: {ex.Message}");
                        isFeasible = false;
                    }

                    if (isFeasible)
                        break;
                }

                if (!isFeasible || proposalParameters is null)
                {
                    throw new InvalidOperationException(
                        "Unable to generate a feasible mixture EM initialization.");
                }

                var parameterSet = new ParameterSet(proposalParameters, logPosterior);
                sampler.PopulationMatrix.Add(parameterSet.Clone());
                population.Add(parameterSet);
            }

            population.Sort((left, right) => right.Fitness.CompareTo(left.Fitness));
            for (int chainIndex = 0; chainIndex < sampler.NumberOfChains; chainIndex++)
                sampler.MarkovChains[chainIndex].Add(population[chainIndex].Clone());
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

            result = MixtureDistribution.CreateDistribution(
                BayesianAnalysis.Results.Output[index].Values);

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

            return MixtureDistribution.CreateDistribution(parms);
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
                double[] pointEstimateParameters;
                if (BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean)
                {
                    pointEstimateParameters = MixtureDistribution.GetPhysicalParameters(
                        BayesianAnalysis.Results.PosteriorMean.Values);
                }
                else
                {
                    pointEstimateParameters = MixtureDistribution.GetPhysicalParameters(
                        BayesianAnalysis.Results.MAP.Values);
                }
                MixtureDistribution.SetParameterValues(pointEstimateParameters);

                // Update mode curve
                AnalysisResults!.ModeCurve = new double[ProbabilityOrdinates.Count];
                for (int i = 0; i < ProbabilityOrdinates.Count; i++)
                    AnalysisResults.ModeCurve[i] = MixtureDistribution.Mixture!.InverseCDF(1 - ProbabilityOrdinates[i]);

                // Information criteria
                double[] mapParameters = MixtureDistribution.GetPhysicalParameters(
                    BayesianAnalysis.Results.MAP.Values);
                var logL = MixtureDistribution.DataLogLikelihood(mapParameters);
                var k = MixtureDistribution.Mixture!.Distributions.Length > 1
                    ? MixtureDistribution.NumberOfParameters - 1
                    : MixtureDistribution.NumberOfParameters;
                var n = MixtureDistribution.DataFrame.TotalRecordLength();
                // AIC/BIC use the data likelihood at MAP and are comparable with MLE
                // criteria only when all active priors are flat.
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
                // RMSE is undefined when the residual degrees of freedom are not positive.
                var rmse = values.Count > MixtureDistribution.Mixture!.NumberOfParameters
                    ? GoodnessOfFit.RMSE(values, probs, MixtureDistribution.Mixture!)
                    : double.NaN;

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
                double[] mapParameters = MixtureDistribution.GetPhysicalParameters(
                    BayesianAnalysis.Results.MAP.Values);
                MixtureDistribution.SetParameterValues(mapParameters);

                // Get sampled distributions for each MCMC output
                int B = BayesianAnalysis.Results.Output.Count;
                var sampledDistributions = new UnivariateDistributionBase[B];
                Parallel.For(0, B, AnalysisProgress.CreateParallelOptions(), idx =>
                {
                    sampledDistributions[idx] = MixtureDistribution.CreateDistribution(
                        BayesianAnalysis.Results!.Output[idx].Values);
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
