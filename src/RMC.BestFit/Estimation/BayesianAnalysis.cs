using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace RMC.BestFit.Estimation
{

    /// <summary>
    /// Performs Bayesian MCMC analysis for a model.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class BayesianAnalysis : INotifyPropertyChanged
    {
        /// <summary>
        /// Constructs a new Bayesian analysis.
        /// </summary>
        public BayesianAnalysis() { }

        /// <summary>
        /// Constructs a new Bayesian analysis.
        /// </summary>
        /// <param name="model">The model to perform Bayesian analysis with.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public BayesianAnalysis(IModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        /// <summary>
        /// Constructs a new Bayesian analysis.
        /// </summary>
        /// <param name="model">The model to perform Bayesian analysis with.</param>
        /// <param name="type">The MCMC sampler type. Default = DEMCzs.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        public BayesianAnalysis(IModel model, SamplerType type = SamplerType.DEMCzs)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Type = type;
        }

        /// <summary>
        /// Constructs a Bayesian analysis from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="model">The model to perform Bayesian analysis with.</param>
        /// <param name="xElement">The <see cref="XElement"/> to deserialize.</param>
        /// <param name="results">Optional MCMC results to attach to the analysis.</param>
        public BayesianAnalysis(IModel? model, XElement xElement, MCMCResults? results = null)
        {
            // Set model and results
            Model = model;
            Results = results;

            DeserializeFromXElement(xElement);
            SetUpSampler();
        }

        /// <summary>
        /// Constructs a Bayesian analysis from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The <see cref="XElement"/> to deserialize.</param>
        public BayesianAnalysis(XElement xElement)
        {
            DeserializeFromXElement(xElement);
            // Set up sampler (Model must be assigned separately for this overload)
            SetUpSampler();
        }

        /// <summary>
        /// Reads all serialized scalar attributes from <paramref name="xElement"/> into
        /// the corresponding backing fields. Extracted from the two paired XElement
        /// constructors so additions/changes to the schema are made in one place.
        /// </summary>
        /// <param name="xElement">The <see cref="XElement"/> to deserialize.</param>
        private void DeserializeFromXElement(XElement xElement)
        {
            var typeAttr = xElement.Attribute(nameof(Type));
            if (typeAttr != null)
            {
                // Backward compatibility: HMC was renamed to NUTS
                string typeValue = typeAttr.Value == "HMC" ? "NUTS" : typeAttr.Value;
                Enum.TryParse(typeValue, out _type);
            }
            var numberOfChainsAttr = xElement.Attribute(nameof(NumberOfChains));
            if (numberOfChainsAttr != null) int.TryParse(numberOfChainsAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _numberOfChains);
            var thinningIntervalAttr = xElement.Attribute(nameof(ThinningInterval));
            if (thinningIntervalAttr != null) int.TryParse(thinningIntervalAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _thinningInterval);
            var warmupIterationsAttr = xElement.Attribute(nameof(WarmupIterations));
            if (warmupIterationsAttr != null) int.TryParse(warmupIterationsAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _warmupIterations);
            var iterationsAttr = xElement.Attribute(nameof(Iterations));
            if (iterationsAttr != null) int.TryParse(iterationsAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _iterations);
            var useSimulationDefaultsAttr = xElement.Attribute(nameof(UseSimulationDefaults));
            if (useSimulationDefaultsAttr != null) bool.TryParse(useSimulationDefaultsAttr.Value, out _useSimulationDefaults);
            var prngSeedAttr = xElement.Attribute(nameof(PRNGSeed));
            if (prngSeedAttr != null) int.TryParse(prngSeedAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _prngSeed);
            var initialIterationsAttr = xElement.Attribute(nameof(InitialIterations));
            if (initialIterationsAttr != null) int.TryParse(initialIterationsAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _initialIterations);
            var jumpAttr = xElement.Attribute(nameof(Jump));
            if (jumpAttr != null) double.TryParse(jumpAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _jump);
            var jumpThresholdAttr = xElement.Attribute(nameof(JumpThreshold));
            if (jumpThresholdAttr != null) double.TryParse(jumpThresholdAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _jumpThreshold);
            var snookerThresholdAttr = xElement.Attribute(nameof(SnookerThreshold));
            if (snookerThresholdAttr != null) double.TryParse(snookerThresholdAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _snookerThreshold);
            var noiseAttr = xElement.Attribute(nameof(Noise));
            if (noiseAttr != null) double.TryParse(noiseAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _noise);
            var scaleAttr = xElement.Attribute(nameof(Scale));
            if (scaleAttr != null) double.TryParse(scaleAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _scale);
            var betaAttr = xElement.Attribute(nameof(Beta));
            if (betaAttr != null) double.TryParse(betaAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _beta);
            var maxTreeDepthAttr = xElement.Attribute(nameof(MaxTreeDepth));
            if (maxTreeDepthAttr != null) int.TryParse(maxTreeDepthAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _maxTreeDepth);
            var useAdvancedSimulationDefaultsAttr = xElement.Attribute(nameof(UseAdvancedSimulationDefaults));
            if (useAdvancedSimulationDefaultsAttr != null) bool.TryParse(useAdvancedSimulationDefaultsAttr.Value, out _useAdvancedSimulationDefaults);
            var credibleIntervalWidthAttr = xElement.Attribute(nameof(CredibleIntervalWidth));
            if (credibleIntervalWidthAttr != null) double.TryParse(credibleIntervalWidthAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _credibleIntervalWidth);
            var outputLengthAttr = xElement.Attribute(nameof(OutputLength));
            if (outputLengthAttr != null) int.TryParse(outputLengthAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _outputLength);
            var pointEstimatorAttr = xElement.Attribute(nameof(PointEstimator));
            if (pointEstimatorAttr != null)
            {
                Enum.TryParse(pointEstimatorAttr.Value, out _pointEstimator);
            }
            else
            {
                // Backward-compat default for legacy projects.
                // Pre-v2 files only emitted the posterior-mode point estimate; the
                // PointEstimator XML attribute did not exist. Defaulting to
                // PosteriorMode here keeps loaded legacy results faithful to what
                // those projects actually contain. New analyses (constructor at
                // line ~217) default to PosteriorMean � the asymmetry is intentional.
                _pointEstimator = PointEstimateType.PosteriorMode;
            }
            var isEstimatedAttr = xElement.Attribute(nameof(IsEstimated));
            if (isEstimatedAttr != null) bool.TryParse(isEstimatedAttr.Value, out _isEstimated);
            var dicAttr = xElement.Attribute(nameof(DIC));
            if (dicAttr != null)
            {
                double.TryParse(dicAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var dic);
                DIC = dic;
            }
            var waicAttr = xElement.Attribute(nameof(WAIC));
            if (waicAttr != null)
            {
                double.TryParse(waicAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var waic);
                WAIC = waic;
            }
            var waicPdAttr = xElement.Attribute(nameof(WAIC_pD));
            if (waicPdAttr != null)
            {
                double.TryParse(waicPdAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var waicPd);
                WAIC_pD = waicPd;
            }
            var looicAttr = xElement.Attribute(nameof(LOOIC));
            if (looicAttr != null)
            {
                double.TryParse(looicAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var looic);
                LOOIC = looic;
            }
            var looPdAttr = xElement.Attribute(nameof(LOO_pD));
            if (looPdAttr != null)
            {
                double.TryParse(looPdAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var looPd);
                LOO_pD = looPd;
            }
            var looicSeAttr = xElement.Attribute(nameof(LOOIC_SE));
            if (looicSeAttr != null)
            {
                double.TryParse(looicSeAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var looicSe);
                LOOIC_SE = looicSe;
            }
            var elapsedTimeAttr = xElement.Attribute(nameof(ElapsedTime));
            if (elapsedTimeAttr != null && long.TryParse(elapsedTimeAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var ticks))
                _elapsedTime = TimeSpan.FromTicks(ticks);
        }

        #region Members

        // General settings
        private SamplerType _type = SamplerType.DEMCzs;
        private int _numberOfChains = 4;
        private int _thinningInterval = 20;
        private int _warmupIterations = 1500;
        private int _iterations = 3000;
        private int _prngSeed = 12345;
        private int _initialIterations = 300;
        private bool _useSimulationDefaults = true;

        // Advanced settings (sampler-specific)
        private bool _useAdvancedSimulationDefaults = true;

        // Parameters for DEMCz and DEMCzs
        private double _jump = 1.0;
        private double _jumpThreshold = 0.1;
        private double _snookerThreshold = 0.1;
        private double _noise = 1E-12;

        // Parameters for ARWMH
        private double _scale = 2.38 * 2.38;
        private double _beta = 0.05;

        // Parameters for NUTS
        private int _maxTreeDepth = 10;

        // Output settings
        private double _credibleIntervalWidth = 0.9;
        private int _outputLength = 10000;
        private PointEstimateType _pointEstimator = PointEstimateType.PosteriorMean;

        private bool _isEstimated = false;
        private IModel? _model = null;
        private IReadOnlyList<string>? _parameterNames = null;
        private MCMCResults? _results = null;
        private TimeSpan? _elapsedTime = null;
        private double[]? _pointwiseElpdLoo = null;
        /// <summary>
        /// Occurs when an analysis property changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool _tokenDisposed = false;

        /// <summary>
        /// Enumeration of MCMC sampler types.
        /// </summary>
        public enum SamplerType
        {
            /// <summary>
            /// Differential Evolution MCMC (DE-MC z).
            /// </summary>
            DEMCz,
            /// <summary>
            /// Differential Evolution MCMC with snooker update (DE-MC zs).
            /// This often has better convergence properties than the basic DEMCz sampler.
            /// </summary>
            DEMCzs,
            /// <summary>
            /// Adaptive Random Walk Metropolis-Hastings.
            /// </summary>
            ARWMH,
            /// <summary>
            /// No-U-Turn Sampler (NUTS).
            /// Automatically adapts trajectory length via binary tree doubling and step size via dual averaging.
            /// </summary>
            NUTS
        }

        /// <summary>
        /// The MCMC sampler type to use for estimating the model parameters.
        /// </summary>
        public SamplerType Type
        {
            get { return _type; }
            set
            {
                if (_type != value)
                {
                    _type = value;
                    ClearResults();
                    RaisePropertyChange(nameof(Type));

                    if (UseSimulationDefaults)
                    {
                        SetDefaultSimulationOptions();
                    }

                    if (UseAdvancedSimulationDefaults)
                    {
                        SetDefaultAdvancedSimulationOptions();
                    }
                }
            }
        }

        /// <summary>
        /// The MCMC sampler instance used by this analysis.
        /// </summary>
        public MCMCSampler? Sampler { get; private set; }

        /// <summary>
        /// Gets or sets the model to estimate.
        /// </summary>
        public IModel? Model
        {
            get { return _model; }
            set
            {
                // Unsubscribe from old model events
                if (_model != null)
                    _model.PropertyChanged -= Model_PropertyChanged;
                
                _model = value;

                // Subscribe to model events
                if (_model != null)
                    _model.PropertyChanged += Model_PropertyChanged;

                ClearResults();
                RaisePropertyChange(nameof(Model));

                if (UseSimulationDefaults)
                    SetDefaultSimulationOptions();

                if (UseAdvancedSimulationDefaults)
                    SetDefaultAdvancedSimulationOptions();
            }
        }

        /// <summary>
        /// Gets the parameter display names for the estimated model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <see cref="Model"/> is set, returns names from <see cref="Model"/>.Parameters.
        /// When Model is null (e.g., GMM-based analyses like Bulletin 17C), returns names
        /// that were explicitly provided via <see cref="SetCustomMCMCResults(MCMCResults, bool, IReadOnlyList{string})"/>.
        /// </para>
        /// </remarks>
        public IReadOnlyList<string>? ParameterNames
        {
            get
            {
                if (Model != null)
                    return Model.Parameters.Select(x => x.DisplayName).ToList();
                return _parameterNames;
            }
        }

        /// <summary>
        /// Gets the number of estimated parameters.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Returns <see cref="Model"/>.NumberOfParameters when Model is set,
        /// otherwise falls back to the <see cref="ParameterNames"/> count.
        /// Returns 0 if neither is available.
        /// </para>
        /// </remarks>
        public int NumberOfEstimatedParameters =>
            Model?.NumberOfParameters ?? _parameterNames?.Count ?? 0;

        /// <summary>
        /// Determines if the Bayesian analysis has been estimated.
        /// </summary>
        public bool IsEstimated
        {
            get { return _isEstimated; }
            set
            {
                if (_isEstimated != value)
                {
                    _isEstimated = value;
                    RaisePropertyChange(nameof(IsEstimated));
                }
            }
        }

        /// <summary>
        /// The MCMC results for this analysis.
        /// </summary>
        public MCMCResults? Results
        {
            get { return _results; }
            private set
            {
                if (ReferenceEquals(_results, value)) return;
                _results = value;
                RaisePropertyChange(nameof(Results));
            }
        }

        /// <summary>
        /// The exception captured by the most recent <see cref="RunAsync"/> call,
        /// or <c>null</c> if the run completed successfully or has not yet been
        /// started. Cleared at the start of every new <c>RunAsync</c>.
        /// </summary>
        /// <remarks>
        /// Pairs with <c>IsEstimated</c>: when <c>IsEstimated == false</c>
        /// and <c>LastError != null</c>, the run failed with that exception. The
        /// UI / wrapper analyses can subscribe to <c>AnalysisCompleted</c> and
        /// surface <c>LastError.Message</c> to the user.
        /// </remarks>
        public Exception? LastError { get; private set; }

        /// <summary>
        /// Gets the elapsed wall-clock time for the most recent MCMC simulation.
        /// </summary>
        /// <remarks>
        /// This is measured from the start of <see cref="MCMCSampler.Sample"/> to its completion,
        /// including all chains and post-processing. Returns null if no simulation has been run.
        /// </remarks>
        public TimeSpan? ElapsedTime
        {
            get { return _elapsedTime; }
            private set
            {
                _elapsedTime = value;
                RaisePropertyChange(nameof(ElapsedTime));
            }
        }

        /// <summary>
        /// Gets the Deviance Information Criterion (DIC) computed from the MCMC results.
        /// </summary>
        public double DIC { get; private set; }

        /// <summary>
        /// Gets the Watanabe-Akaike Information Criterion computed from the MCMC results.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WAIC is <c>-2 * lppd + 2 * p_WAIC</c>, where lppd is the sum of pointwise
        /// log posterior predictive densities and <c>p_WAIC</c> is the effective parameter count.
        /// </para>
        /// <para>
        /// The implementation uses data log likelihoods from
        /// <see cref="IModel.PointwiseDataLogLikelihood(double[])"/>. It evaluates the pointwise
        /// likelihood once per retained posterior draw and shares that transient matrix with PSIS-LOO.
        /// </para>
        /// </remarks>
        public double WAIC { get; private set; }

        /// <summary>
        /// Gets the effective number of parameters computed by the WAIC criterion.
        /// </summary>
        /// <remarks>
        /// This is the sum, over pointwise observation units, of the unbiased sample variance of
        /// their log likelihoods across retained posterior draws.
        /// </remarks>
        public double WAIC_pD { get; private set; }

        /// <summary>
        /// Gets the leave-one-out information criterion computed using Pareto-smoothed importance sampling.
        /// </summary>
        /// <remarks>
        /// <para>
        /// LOOIC is <c>-2 * elpd_loo</c>, where <c>elpd_loo</c> is the estimated
        /// leave-one-out expected log predictive density.
        /// </para>
        /// <para>
        /// PSIS-LOO approximates exact leave-one-out cross-validation from the retained full-posterior
        /// draws and does not refit the model. Pareto-k values must be inspected to assess approximation
        /// reliability.
        /// </para>
        /// </remarks>
        public double LOOIC { get; private set; }

        /// <summary>
        /// Gets the effective number of parameters (p_LOO) as estimated by PSIS-LOO.
        /// </summary>
        /// <remarks>
        /// Computed as the difference between the in-sample log predictive density and the LOO estimate:
        /// p_LOO = lppd - elpd_loo
        /// </remarks>
        public double LOO_pD { get; private set; }

        /// <summary>
        /// Gets the standard error of the LOOIC estimate.
        /// </summary>
        public double LOOIC_SE { get; private set; }

        /// <summary>
        /// Gets one Pareto-k importance-weight-tail diagnostic for each pointwise observation unit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For <c>S</c> retained draws, reliability is assessed against
        /// <c>min(1 - 1 / log10(S), 0.7)</c>. Values at or above that limit require
        /// investigation; values at or above 1.0 lack the usual finite-mean guarantee.
        /// </para>
        /// <para>
        /// A high value diagnoses the PSIS approximation for that pointwise unit. It is not by
        /// itself evidence that the observation is erroneous or should be removed.
        /// </para>
        /// </remarks>
        public double[]? ParetoK { get; private set; }

        #region SimulationOptions

        /// <summary>
        /// Gets or sets the number of Markov chains.
        /// </summary>
        [Category("Simulation Options")]
        [DisplayName("Number of Markov Chains")]
        [Description("Specifies the number of parallel chains for the Bayesian MCMC analysis. A minimum of 4 chains is recommended.")]
        [Browsable(true)]
        public int NumberOfChains
        {
            get { return _numberOfChains; }
            set
            {
                if (_numberOfChains != value)
                {
                    _numberOfChains = value;
                    ClearResults();
                    RaisePropertyChange(nameof(NumberOfChains));
                }
            }
        }

        /// <summary>
        /// Gets or sets the thinning interval. This determines how often the MCMC evolutions are recorded and evaluated.
        /// </summary>
        [Category("Simulation Options")]
        [DisplayName("Thinning Interval")]
        [Description("Specifies the frequency at which MCMC iterations are recorded. Thinning reduces autocorrelation by saving every Nth iteration (for example, an interval of 20 records every 20th iteration).")]
        [Browsable(true)]
        public int ThinningInterval
        {
            get { return _thinningInterval; }
            set
            {
                if (_thinningInterval != value)
                {
                    _thinningInterval = value;
                    ClearResults();
                    RaisePropertyChange(nameof(ThinningInterval));
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of warm up (burn-in) MCMC iterations to discard at the beginning of the simulation.
        /// </summary>
        [Category("Simulation Options")]
        [DisplayName("Warm Up Iterations")]
        [Description("Specifies the number of thinned warm-up iterations to discard at the beginning of the simulation. Typically, the warm-up period is set to half of the total iterations.")]
        [Browsable(true)]
        public int WarmupIterations
        {
            get { return _warmupIterations; }
            set
            {
                if (_warmupIterations != value)
                {
                    _warmupIterations = value;
                    ClearResults();
                    RaisePropertyChange(nameof(WarmupIterations));
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of thinned MCMC iterations to simulate.
        /// </summary>
        [Category("Simulation Options")]
        [DisplayName("Iterations")]
        [Description("Specifies the number of thinned MCMC iterations to simulate. For example, with a thinning interval of 10 and 1,000 iterations, the simulation performs 10,000 total iterations. It is recommended to have at least 3,000 thinned iterations.")]
        [Browsable(true)]
        public int Iterations
        {
            get { return _iterations; }
            set
            {
                if (_iterations != value)
                {
                    _iterations = value;
                    ClearResults();
                    RaisePropertyChange(nameof(Iterations));
                }
            }
        }

        /// <summary>
        /// Determines whether to use default simulation options.
        /// </summary>
        [Category("Simulation Options")]
        [DisplayName("Use Simulation Defaults")]
        [Description("If true, default simulation options (number of chains, thinning, iterations, and warm-up) are applied based on the current model and credible interval width.")]
        [Browsable(true)]
        public bool UseSimulationDefaults
        {
            get { return _useSimulationDefaults; }
            set
            {
                if (_useSimulationDefaults != value)
                {
                    _useSimulationDefaults = value;
                    RaisePropertyChange(nameof(UseSimulationDefaults));
                    if (_useSimulationDefaults)
                        SetDefaultSimulationOptions();
                }
            }
        }

        /// <summary>
        /// Gets or sets the pseudo random number generator (PRNG) seed.
        /// </summary>
        [Category("Simulation Options")]
        [DisplayName("PRNG Seed")]
        [Description("Specifies the pseudo random number generator seed for the MCMC simulation, ensuring repeatability.")]
        [Browsable(true)]
        public int PRNGSeed
        {
            get { return _prngSeed; }
            set
            {
                if (_prngSeed != value)
                {
                    _prngSeed = value;
                    ClearResults();
                    RaisePropertyChange(nameof(PRNGSeed));
                }
            }
        }

        /// <summary>
        /// Gets or sets the length of the initial population matrix.
        /// It is recommended that the initial iterations be at least 100 times the number of parameters.
        /// </summary>
        [Category("Simulation Options")]
        [DisplayName("Initial Iterations")]
        [Description("Specifies the number of iterations used to initialize the chains. It is recommended to use at least 100 times the number of parameters.")]
        [Browsable(true)]
        public int InitialIterations
        {
            get { return _initialIterations; }
            set
            {
                if (_initialIterations != value)
                {
                    _initialIterations = value;
                    ClearResults();
                    RaisePropertyChange(nameof(InitialIterations));
                }
            }
        }

        #endregion

        #region AdvancedOptions

        /// <summary>
        /// Gets or sets the jumping parameter used to jump between mode regions in the target distribution.
        /// </summary>
        [Category("Advanced Options")]
        [DisplayName("Jump Parameter")]
        [Description("Specifies the jump parameter (?) that enables the simulation to move between modes in the target distribution. A recommended setting is ? = 2.38/v(2d), where d is the number of model parameters.")]
        [Browsable(true)]
        public double Jump
        {
            get { return _jump; }
            set
            {
                if (_jump != value)
                {
                    _jump = value;
                    ClearResults();
                    RaisePropertyChange(nameof(Jump));
                }
            }
        }

        /// <summary>
        /// Determines how often the jump parameter switches to 1.0 (for example, 0.10 results in adaptation 10 percent of the time).
        /// </summary>
        [Category("Advanced Options")]
        [DisplayName("Jump Threshold")]
        [Description("Specifies the probability at which the jump parameter (?) is set to 1.0. For example, a value of 0.10 results in a large jump 10 percent of the time.")]
        [Browsable(true)]
        public double JumpThreshold
        {
            get { return _jumpThreshold; }
            set
            {
                if (_jumpThreshold != value)
                {
                    _jumpThreshold = value;
                    ClearResults();
                    RaisePropertyChange(nameof(JumpThreshold));
                }
            }
        }

        /// <summary>
        /// Determines how often the snooker update occurs (for example, 0.10 results in a snooker update 10 percent of the time).
        /// </summary>
        [Category("Advanced Options")]
        [DisplayName("Snooker Threshold")]
        [Description("Specifies the probability that a snooker update occurs. For example, a value of 0.10 means a snooker update is applied 10 percent of the time.")]
        [Browsable(true)]
        public double SnookerThreshold
        {
            get { return _snookerThreshold; }
            set
            {
                if (_snookerThreshold != value)
                {
                    _snookerThreshold = value;
                    ClearResults();
                    RaisePropertyChange(nameof(SnookerThreshold));
                }
            }
        }

        /// <summary>
        /// Gets or sets the noise parameter (b).
        /// </summary>
        [Category("Advanced Options")]
        [DisplayName("Noise Parameter")]
        [Description("Specifies the noise level (b) added to the proposal in the MCMC simulation. The noise is drawn from a Normal distribution N(0, b). A very small value (for example, 1E-8) is recommended.")]
        [Browsable(true)]
        public double Noise
        {
            get { return _noise; }
            set
            {
                if (_noise != value)
                {
                    _noise = value;
                    ClearResults();
                    RaisePropertyChange(nameof(Noise));
                }
            }
        }

        /// <summary>
        /// Gets or sets the scale parameter for the adaptive covariance matrix (ARWMH).
        /// </summary>
        [Category("Advanced Options")]
        [DisplayName("Scale Parameter")]
        [Description("Specifies the scaling factor applied to the adaptive covariance matrix for the ARWMH sampler.")]
        [Browsable(true)]
        public double Scale
        {
            get { return _scale; }
            set
            {
                if (_scale != value)
                {
                    _scale = value;
                    ClearResults();
                    RaisePropertyChange(nameof(Scale));
                }
            }
        }

        /// <summary>
        /// Gets or sets the beta parameter for the ARWMH sampler.
        /// </summary>
        [Category("Advanced Options")]
        [DisplayName("Beta Parameter")]
        [Description("Specifies the probability of sampling from a small identity covariance matrix. For example, a value of 0.05 means this occurs 5 percent of the time.")]
        [Browsable(true)]
        public double Beta
        {
            get { return _beta; }
            set
            {
                if (_beta != value)
                {
                    _beta = value;
                    ClearResults();
                    RaisePropertyChange(nameof(Beta));
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum binary tree depth for the NUTS sampler.
        /// </summary>
        /// <remarks>
        /// Caps the trajectory length at 2^MaxTreeDepth leapfrog steps. Stan and PyMC default to 10.
        /// Values above 10 are seldom useful; increasing beyond 12-15 is not recommended.
        /// </remarks>
        [Category("Advanced Options")]
        [DisplayName("Max Tree Depth")]
        [Description("Maximum binary tree depth for the NUTS sampler. Caps the trajectory at 2^MaxTreeDepth leapfrog steps. Default = 10 (Stan/PyMC convention).")]
        [Browsable(true)]
        public int MaxTreeDepth
        {
            get { return _maxTreeDepth; }
            set
            {
                if (_maxTreeDepth != value)
                {
                    _maxTreeDepth = value;
                    ClearResults();
                    RaisePropertyChange(nameof(MaxTreeDepth));
                }
            }
        }

        /// <summary>
        /// Determines whether to use default advanced simulation options.
        /// </summary>
        [Category("Advanced Options")]
        [DisplayName("Use Advanced Simulation Defaults")]
        [Description("If true, applies default advanced simulation options (PRNG seed, initialization length, and sampler-specific parameters).")]
        [Browsable(true)]
        public bool UseAdvancedSimulationDefaults
        {
            get { return _useAdvancedSimulationDefaults; }
            set
            {
                if (_useAdvancedSimulationDefaults != value)
                {
                    _useAdvancedSimulationDefaults = value;
                    RaisePropertyChange(nameof(UseAdvancedSimulationDefaults));
                    if (_useAdvancedSimulationDefaults)
                        SetDefaultAdvancedSimulationOptions();
                }
            }
        }

        #endregion

        #region OutputOptions

        /// <summary>
        /// Gets or sets the width of the credible interval.
        /// </summary>
        [Category("Output Options")]
        [DisplayName("Credible Interval")]
        [Description("Specifies the credible interval width (for example, 0.90 for a 90 percent credible interval).")]
        [Browsable(true)]
        public double CredibleIntervalWidth
        {
            get { return _credibleIntervalWidth; }
            set
            {
                if (_credibleIntervalWidth != value)
                {
                    _credibleIntervalWidth = value;

                    // Alpha = 1 - CIWidth only affects the LowerCI/UpperCI percentiles in
                    // ParameterResults[i].SummaryStatistics. The MCMC chain itself is
                    // independent of alpha � preserve Results and recompute summaries in place.
                    if (IsEstimated && Results != null)
                    {
                        Results.RecomputeParameterResults(1.0 - value);
                        RaisePropertyChange(nameof(Results));
                    }

                    RaisePropertyChange(nameof(CredibleIntervalWidth));
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of posterior parameter sets to output.
        /// </summary>
        [Category("Output Options")]
        [DisplayName("Output Length")]
        [Description("Specifies the number of posterior parameter sets to output. For example, outputting 10,000 sets is recommended to ensure an accurate 90 percent credible interval.")]
        [Browsable(true)]
        public int OutputLength
        {
            get { return _outputLength; }
            set
            {
                if (_outputLength != value)
                {
                    _outputLength = value;
                    ClearResults();
                    RaisePropertyChange(nameof(OutputLength));
                }
            }
        }

        /// <summary>
        /// Enumeration of point estimate types.
        /// </summary>
        public enum PointEstimateType
        {
            /// <summary>
            /// Posterior mean.
            /// </summary>
            PosteriorMean,
            /// <summary>
            /// Posterior mode (MAP).
            /// </summary>
            PosteriorMode
        }

        /// <summary>
        /// Gets or sets the point estimator used to summarize the posterior.
        /// </summary>
        [Category("Output Options")]
        [DisplayName("Point Estimator")]
        [Description("Specifies the point estimator for summarizing the posterior distribution. Choose 'Posterior Mean' for the average or 'Posterior Mode (MAP)' for the most likely value.")]
        [Browsable(true)]
        public PointEstimateType PointEstimator
        {
            get { return _pointEstimator; }
            set
            {
                if (_pointEstimator != value)
                {
                    _pointEstimator = value;
                    RaisePropertyChange(nameof(PointEstimator));
                }
            }
        }

        /// <summary>
        /// A list of model property names to ignore when the model raises <see cref="INotifyPropertyChanged.PropertyChanged"/> events.
        /// </summary>
        public List<string> ModelPropertiesToIgnore { get; set; } = new List<string>();

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Raises the <c>PropertyChanged</c> event for a given property name.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void RaisePropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// If model parameters change, raise property change for parameters.
        /// </summary>
        /// <param name="sender">The model.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IModel.Parameters) || e.PropertyName == nameof(IModel.SetDefaultParameters))
            {
                RaisePropertyChange(e.PropertyName);
                // ParameterNames is computed from Model.Parameters, so notify subscribers
                // that the parameter list has changed (used by diagnostic plot combo boxes).
                RaisePropertyChange(nameof(ParameterNames));
            }
        }

        /// <summary>
        /// Sets default simulation options based on the current model and credible interval width.
        /// </summary>
        public void SetDefaultSimulationOptions()
        {
            int d = Model?.NumberOfParameters ?? 1;
            if (d <= 0) d = 1;

            // Sampler-specific chain and thinning defaults
            if (Type == SamplerType.DEMCz || Type == SamplerType.DEMCzs)
            {
                // ter Braak & Vrugt (2008): N=4 suffices for DEMCzs, but 2d provides
                // better diversity for differential evolution proposals.
                NumberOfChains = Math.Max(4, Math.Min(20, 2 * d));
                // DE-MC samplers have higher autocorrelation; thinning reduces storage.
                ThinningInterval = Math.Max(1, Math.Min(100, 10 * d));
            }
            else if (Type == SamplerType.ARWMH)
            {
                // Roberts & Rosenthal (2009): 4 chains is standard for R-hat diagnostics.
                NumberOfChains = 4;
                // ARWMH samplers have higher autocorrelation; thinning reduces storage.
                ThinningInterval = Math.Max(1, Math.Min(100, 10 * d));
            }
            else if (Type == SamplerType.NUTS)
            {
                // Stan/PyMC convention: 4 chains for R-hat diagnostics.
                NumberOfChains = 4;
                // Link & Eaton (2012): thinning is statistically wasteful.
                // Stan does not thin by default; NUTS has low autocorrelation.
                ThinningInterval = 1;
            }
            else
            {
                NumberOfChains = 4;
                ThinningInterval = 20;
            }

            double ci = CredibleIntervalWidth;
            if (ci <= 0.0 || ci >= 1.0)
            {
                ci = 0.9;
            }

            double alpha = (1.0 - ci) / 2.0;
            Iterations = MCMCDiagnostics.MinimumSampleSize(ci, 0.01, 1.0 - alpha);
            // 50% warmup is the consensus default (Stan, PyMC, Hoffman & Gelman 2014).
            WarmupIterations = Iterations / 2;

            PRNGSeed = 12345;
            InitialIterations = Math.Min(1000, Model == null ? 100 : Math.Max(100, d * 100));
        }

        /// <summary>
        /// Sets default advanced simulation options based on the current model and sampler type.
        /// </summary>
        public void SetDefaultAdvancedSimulationOptions()
        {
            // DEMCz and DEMCzs samplers
            if (Type == SamplerType.DEMCz || Type == SamplerType.DEMCzs)
            {
                int d = Model?.NumberOfParameters ?? 1;
                if (d <= 0) d = 1;

                Jump = 2.38 / Math.Sqrt(2.0 * d);
                JumpThreshold = 0.1;
                Noise = 1E-12;
            }

            // DEMCzs sampler
            if (Type == SamplerType.DEMCzs)
            {
                SnookerThreshold = 0.1;
            }

            // ARWMH sampler
            if (Type == SamplerType.ARWMH)
            {
                int d = Model?.NumberOfParameters ?? 1;
                if (d <= 0) d = 1;

                Scale = 2.38 * 2.38 / d;
                Beta = 0.05;
            }

            // NUTS sampler
            if (Type == SamplerType.NUTS)
            {
                MaxTreeDepth = 10;
            }
        }

        /// <summary>
        /// Validates the current state of the Bayesian analysis and reports any issues found.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item>
        /// <description><c>IsValid</c>: <c>true</c> if the analysis passes all validation checks; otherwise <c>false</c>.</description>
        /// </item>
        /// <item>
        /// <description><c>ValidationMessages</c>: a list of messages describing any validation errors or warnings. This list is empty when the object is valid.</description>
        /// </item>
        /// </list>
        /// </returns>
        public (bool IsValid, List<string> ValidationMessages) Validate()
        {
            var messages = new List<string>();
            bool isValid = true;

            // Warnings
            if (!IsEstimated)
            {
                messages.Add("Warning: The model has not been estimated.");
            }
            if (WarmupIterations > (int)(0.5 * Iterations))
            {
                messages.Add("Warning: The number of warmup iterations exceeds half of the total iterations.");
            }
            if (Iterations < 1000)
            {
                messages.Add("Warning: The number of iterations is below 1,000, which may reduce the accuracy of the posterior distribution.");
            }
            if (OutputLength < 1000)
            {
                messages.Add("Warning: The output length is below 1,000, which may reduce the accuracy of the posterior distribution.");
            }

            // Errors
            if (NumberOfChains < 4 || NumberOfChains > 20)
            {
                // Vehtari et al. (2021) recommend =4 chains for reliable split-Rhat
                // and bulk/tail-ESS diagnostics. DEMCz/DEMCzs technically work with 3,
                // but the convergence verdict is unreliable below 4.
                messages.Add("Error: The number of Markov chains must be between 4 and 20.");
                isValid = false;
            }
            if (ThinningInterval < 1 || ThinningInterval > 100)
            {
                messages.Add("Error: The thinning interval must be between 1 and 100.");
                isValid = false;
            }
            if (WarmupIterations < 50 || WarmupIterations > 100000)
            {
                messages.Add("Error: The number of warmup iterations must be between 50 and 100,000.");
                isValid = false;
            }
            if (Iterations < 100 || Iterations > 1000000)
            {
                messages.Add("Error: The number of iterations must be between 100 and 1,000,000.");
                isValid = false;
            }
            if (PRNGSeed < 0)
            {
                messages.Add("Error: The PRNG seed cannot be negative.");
                isValid = false;
            }
            if (InitialIterations < NumberOfChains || InitialIterations > 1000)
            {
                messages.Add("Error: The initial iterations must be at least equal to the number of chains and no more than 1,000.");
                isValid = false;
            }
            if (CredibleIntervalWidth <= 0 || CredibleIntervalWidth >= 1)
            {
                messages.Add("Error: The credible interval width must be greater than 0 and less than 1 (for example, 0.90 for a 90 percent interval).");
                isValid = false;
            }
            if (OutputLength < 100 || OutputLength > 1000000)
            {
                messages.Add("Error: The output length must be between 100 and 1,000,000.");
                isValid = false;
            }

            // DEMCz and DEMCzs sampler validation
            if (Type == SamplerType.DEMCz || Type == SamplerType.DEMCzs)
            {
                if (Jump <= 0.0 || Jump >= 2.0)
                {
                    messages.Add("Error: The jump parameter must be greater than 0 and less than 2.");
                    isValid = false;
                }
                if (JumpThreshold < 0.0 || JumpThreshold > 1.0)
                {
                    messages.Add("Error: The jump threshold must be between 0 and 1.");
                    isValid = false;
                }
                if (Noise < 0.0 || Noise > 0.1)
                {
                    messages.Add("Error: The noise parameter should be small and between 0 and 0.1.");
                    isValid = false;
                }
            }

            // DEMCzs sampler validation
            if (Type == SamplerType.DEMCzs)
            {
                if (SnookerThreshold < 0.0 || SnookerThreshold > 0.5)
                {
                    messages.Add("Error: The snooker threshold must be between 0 and 0.5.");
                    isValid = false;
                }
            }

            // ARWMH sampler validation
            if (Type == SamplerType.ARWMH)
            {
                if (Scale <= 0.0)
                {
                    messages.Add("Error: The scale parameter must be greater than 0.");
                    isValid = false;
                }
                if (Beta < 0.0 || Beta > 1.0)
                {
                    messages.Add("Error: The beta parameter must be between 0 and 1.");
                    isValid = false;
                }
            }

            // NUTS sampler validation
            if (Type == SamplerType.NUTS)
            {
                if (MaxTreeDepth < 1 || MaxTreeDepth > 15)
                {
                    messages.Add("Error: The maximum tree depth must be between 1 and 15.");
                    isValid = false;
                }
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Sets up the MCMC sampler based on the current model and sampler type.
        /// </summary>
        public void SetUpSampler()
        {
            if (Model == null || Model.Parameters == null || Model.Parameters.Count == 0)
            {
                Sampler = null;
                return;
            }

            // Mixture models retain all K weights at their public configuration boundary, but
            // sample only the first K-1 weights. The omitted final-weight prior is evaluated
            // after MixtureModel derives the residual inside the posterior target.
            var sampledParameters = Model.Parameters.AsEnumerable();
            LogLikelihood logLikelihood = Model.LogLikelihood;
            if (Model is MixtureModel mixtureModel &&
                mixtureModel.Mixture is not null &&
                mixtureModel.Mixture.Distributions.Length > 1)
            {
                int derivedWeightIndex = mixtureModel.Mixture.Distributions.Length - 1;
                sampledParameters = Model.Parameters.Where((_, index) => index != derivedWeightIndex);
                logLikelihood = mixtureModel.SamplingLogLikelihood;
            }

            var priors = sampledParameters
                .Select(parameter => (IUnivariateDistribution)parameter.PriorDistribution.Clone())
                .ToList();

            // Set up the MCMC sampler
            if (Type == SamplerType.DEMCz)
            {
                Sampler = new DEMCz(priors, logLikelihood)
                {
                    Jump = Jump,
                    JumpThreshold = JumpThreshold,
                    Noise = Noise
                };
            }
            else if (Type == SamplerType.DEMCzs)
            {
                Sampler = new DEMCzs(priors, logLikelihood)
                {
                    Jump = Jump,
                    JumpThreshold = JumpThreshold,
                    SnookerThreshold = SnookerThreshold,
                    Noise = Noise
                };
            }
            else if (Type == SamplerType.ARWMH)
            {
                Sampler = new ARWMH(priors, logLikelihood)
                {
                    Scale = Scale,
                    Beta = Beta
                };
            }
            else if (Type == SamplerType.NUTS)
            {
                Sampler = new NUTS(priors, logLikelihood, maxTreeDepth: MaxTreeDepth);
            }
            else
            {
                Sampler = null;
                return;
            }

            Sampler.NumberOfChains = NumberOfChains;
            Sampler.ThinningInterval = ThinningInterval;
            Sampler.WarmupIterations = WarmupIterations;
            Sampler.Iterations = Iterations;
            Sampler.PRNGSeed = PRNGSeed;
            Sampler.InitialIterations = InitialIterations;
            Sampler.OutputLength = OutputLength;
        }

        /// <summary>
        /// Converts a stored result vector to the parameter vector required by the public model.
        /// </summary>
        /// <param name="storedParameters">The stored MCMC parameter values.</param>
        /// <returns>The full public model vector for a mixture, or the original vector for other models.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a stored mixture vector has an unrecognized or infeasible shape.</exception>
        private double[] GetModelParameterValues(double[] storedParameters)
        {
            if (Model is not MixtureModel mixtureModel)
                return storedParameters;

            if (!mixtureModel.TryGetPhysicalParameters(storedParameters, out double[] physicalParameters))
            {
                throw new InvalidOperationException(
                    "The stored mixture result does not match the K-1 sampled or full-K public parameterization.");
            }

            return physicalParameters;
        }

        /// <summary>
        /// Gets the public model-parameter indexes corresponding to stored MCMC coordinates.
        /// </summary>
        /// <returns>One public model index for each stored parameter result.</returns>
        /// <remarks>
        /// New mixture results omit the derived final weight. Legacy full-K mixture results and
        /// every non-mixture result retain identity indexing.
        /// </remarks>
        private IReadOnlyList<int> GetStoredModelParameterIndexes()
        {
            if (Model is null || Results?.ParameterResults is null)
                return Array.Empty<int>();

            int storedCount = Results.ParameterResults.Length;
            if (Model is MixtureModel mixtureModel &&
                mixtureModel.Mixture is not null &&
                mixtureModel.Mixture.Distributions.Length > 1 &&
                storedCount == Model.NumberOfParameters - 1)
            {
                int derivedWeightIndex = mixtureModel.Mixture.Distributions.Length - 1;
                return Enumerable.Range(0, Model.NumberOfParameters)
                    .Where(index => index != derivedWeightIndex)
                    .ToArray();
            }

            return Enumerable.Range(0, Math.Min(storedCount, Model.NumberOfParameters)).ToArray();
        }

        /// <summary>
        /// Performs Bayesian estimation asynchronously using the configured sampler.
        /// </summary>
        /// <param name="progressReporter">Progress reporter for UI updates.</param>
        /// <param name="reportTaskStartEnd">If true, calls <c>IndicateTaskStart</c> and <c>IndicateTaskEnded</c> on the reporter.</param>
        /// <param name="parallel">Determines if the chains should be sampled in parallel. Default = true.</param>
        public async Task RunAsync(SafeProgressReporter? progressReporter = null, bool reportTaskStartEnd = true, bool parallel = true)
        {
            if (Validate().IsValid == false)
                throw new InvalidOperationException("Bayesian Analysis is not valid. Please check the configuration before running the analysis.");

            // Clear any error captured from a previous run.
            LastError = null;

            // Ensure sampler is ready
            if (Sampler == null)
                SetUpSampler();

            if (Sampler == null)
            {
                // Model was null or had no parameters, or sampler type was invalid for some reason.
                return;
            }

            Sampler.ParallelizeChains = parallel;

            try
            {
                IsEstimated = false;
                Results = null;
                ParetoK = null;
                _pointwiseElpdLoo = null;
                _tokenDisposed = false;

                if (reportTaskStartEnd)
                    progressReporter?.IndicateTaskStart();

                // handle progress update
                Sampler.ProgressChangedRate = 0.01;
                Sampler.ProgressChanged += (double percentComplete, string progressText) =>
                {
                    progressReporter?.ReportProgress(100 * percentComplete);
                };

                // Run the MCMC simulation. Capture results into locals INSIDE the Task.Run
                // worker so the actual property assignments (which fire PropertyChanged)
                // happen on the post-await SynchronizationContext (the dispatcher in WPF).
                // Any WPF binding targeting ElapsedTime / Results / IsEstimated would
                // otherwise fail with InvalidOperationException ("calling thread cannot
                // access this object") because PropertyChanged crosses thread affinity.
                MCMCSampler sampler = Sampler!;
                TimeSpan capturedElapsed = TimeSpan.Zero;
                MCMCResults? capturedResults = null;

                await Task.Run(() =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    sampler.Sample();
                    stopwatch.Stop();
                    capturedElapsed = stopwatch.Elapsed;

                    if (sampler.CancellationTokenSource!.IsCancellationRequested == false)
                    {
                        capturedResults = new MCMCResults(sampler, 1 - CredibleIntervalWidth);
                    }
                });

                // Post-await: now back on the dispatcher (or whichever SynchronizationContext
                // was captured at await). Property setters here fire PropertyChanged on the
                // correct thread. ComputeDIC / WAIC / PSISLOO read this.Results � assign Results
                // first so they see the new chains, and they internally use Parallel.For which
                // dispatches its own worker threads (no dispatcher block on the math itself).
                ElapsedTime = capturedElapsed;
                if (capturedResults != null)
                {
                    Results = capturedResults;
                    await Task.Run(() =>
                    {
                        ComputeDIC();
                        ComputePredictiveInformationCriteria();
                    });
                }

                if (sampler.CancellationTokenSource!.IsCancellationRequested == false)
                {
                    IsEstimated = true;
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is normal � re-throw so wrapper analyses' OperationCanceledException
                // handlers see it as a cancel rather than a generic failure. Without this branch
                // the catch (Exception) below would swallow OCE into LastError and the user would
                // see "TaskCanceledException" reported as a run failure.
                ClearResults();
                throw;
            }
            catch (Exception ex)
            {
                // Log exception and clear results. Surface the error via LastError
                // so the UI / wrapper analyses can render a meaningful message
                // rather than just observing IsEstimated == false silently.
                System.Diagnostics.Debug.WriteLine($"MCMC estimation failed: {ex.Message}");
                LastError = ex;
                ClearResults();
            }
            finally
            {
                if (reportTaskStartEnd)
                    progressReporter?.IndicateTaskEnded();

                if (Sampler?.CancellationTokenSource != null)
                    Sampler.CancellationTokenSource.Dispose();

                _tokenDisposed = true;
            }
        }

        /// <summary>
        /// Cancels the Bayesian analysis if it is currently running.
        /// </summary>
        /// <remarks>
        /// One-shot per <c>RunAsync(SafeProgressReporter?, bool)</c> invocation �
        /// after the run completes (success, fault, or cancellation) the underlying
        /// <see cref="System.Threading.CancellationTokenSource"/> is disposed in the
        /// finally block of <c>RunAsync</c>. Subsequent calls to <c>CancelSimulation</c>
        /// are no-ops (you cannot cancel a completed run). To cancel a future run,
        /// call <c>CancelSimulation</c> while that run is in progress.
        /// </remarks>
        public void CancelSimulation()
        {
            if (Sampler != null && !_tokenDisposed)
            {
                Sampler.CancelSimulation();
            }
        }

        /// <summary>
        /// Clears Bayesian analysis results and resets the sampler.
        /// </summary>
        public void ClearResults()
        {

            Results = null;
            ElapsedTime = null;
            _parameterNames = null;
            DIC = double.NaN;
            WAIC = double.NaN;
            WAIC_pD = double.NaN;
            LOOIC = double.NaN;
            LOO_pD = double.NaN;
            LOOIC_SE = double.NaN;
            ParetoK = null;
            _pointwiseElpdLoo = null;
            IsEstimated = false;
            SetUpSampler();
        }

        /// <summary>
        /// Computes the Deviance Information Criterion (DIC) from the current MCMC results.
        /// </summary>
        private void ComputeDIC()
        {
            if (Results == null || Results.Output == null || Results.Output.Count == 0 || Model == null)
            {
                DIC = double.NaN;
                return;
            }

            int N = Results.Output.Count;
            double dicHat = 0.0;

            Parallel.For(0, N, () => 0d, (j, loop, sum) =>
            {
                double[] parameters = GetModelParameterValues(Results.Output[j].Values);
                sum += -2.0 * Model.DataLogLikelihood(parameters);
                return sum;
            }, z => Tools.ParallelAdd(ref dicHat, z));

            dicHat /= N;
            double[] posteriorMean = GetModelParameterValues(Results.PosteriorMean.Values);
            double dicMu = -2.0 * Model.DataLogLikelihood(posteriorMean);
            DIC = 2.0 * dicHat - dicMu;
        }

        /// <summary>
        /// Computes WAIC and PSIS-LOO from one shared pointwise log-likelihood matrix.
        /// </summary>
        /// <remarks>
        /// The matrix is retained only for this calculation. Pointwise LOO summaries are cached
        /// separately so later influence reporting does not reevaluate the model.
        /// </remarks>
        private void ComputePredictiveInformationCriteria()
        {
            double[,]? pointwiseLogLikelihood = BuildPointwiseLogLikelihoodMatrix();
            if (pointwiseLogLikelihood == null || pointwiseLogLikelihood.GetLength(0) == 0)
            {
                WAIC = double.NaN;
                WAIC_pD = double.NaN;
                LOOIC = double.NaN;
                LOO_pD = double.NaN;
                LOOIC_SE = double.NaN;
                ParetoK = null;
                _pointwiseElpdLoo = null;
                return;
            }

            ComputeWAIC(pointwiseLogLikelihood);
            ComputePSISLOO(pointwiseLogLikelihood);
        }

        /// <summary>
        /// Evaluates the pointwise data log likelihood once for every retained posterior draw.
        /// </summary>
        /// <returns>
        /// An observation-by-draw matrix, or <c>null</c> when posterior results or the model are unavailable.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when pointwise likelihood vectors do not have a consistent observation count.
        /// </exception>
        private double[,]? BuildPointwiseLogLikelihoodMatrix()
        {
            if (Results == null || Results.Output == null || Results.Output.Count == 0 || Model == null)
                return null;

            int drawCount = Results.Output.Count;
            double[] firstParameters = GetModelParameterValues(Results.Output[0].Values);
            double[] firstPointwise = Model.PointwiseDataLogLikelihood(firstParameters);
            int observationCount = firstPointwise.Length;
            var pointwiseLogLikelihood = new double[observationCount, drawCount];

            for (int observationIndex = 0; observationIndex < observationCount; observationIndex++)
                pointwiseLogLikelihood[observationIndex, 0] = firstPointwise[observationIndex];

            Parallel.For(1, drawCount, drawIndex =>
            {
                double[] parameters = GetModelParameterValues(Results.Output[drawIndex].Values);
                double[] values = Model.PointwiseDataLogLikelihood(parameters);
                if (values.Length != observationCount)
                {
                    throw new InvalidOperationException(
                        "Pointwise data log-likelihood length changed across posterior draws.");
                }

                for (int observationIndex = 0; observationIndex < observationCount; observationIndex++)
                    pointwiseLogLikelihood[observationIndex, drawIndex] = values[observationIndex];
            });

            return pointwiseLogLikelihood;
        }

        /// <summary>
        /// Computes the Watanabe-Akaike Information Criterion from a pointwise log-likelihood matrix.
        /// </summary>
        /// <param name="pointwiseLogLikelihood">Observation-by-draw data log likelihoods.</param>
        /// <remarks>
        /// WAIC uses the pointwise log predictive density and the sum of unbiased sample variances
        /// of pointwise log likelihoods. The calculation follows Vehtari, Gelman, and Gabry (2017).
        /// </remarks>
        private void ComputeWAIC(double[,] pointwiseLogLikelihood)
        {
            int observationCount = pointwiseLogLikelihood.GetLength(0);
            int drawCount = pointwiseLogLikelihood.GetLength(1);
            double totalLppd = 0.0;
            double totalPWaic = 0.0;

            Parallel.For(0, observationCount,
                () => (lppd: 0.0, pWaic: 0.0),
                (observationIndex, loop, local) =>
                {
                    double maxLogLikelihood = double.NegativeInfinity;
                    double sumLogLikelihood = 0.0;
                    double sumLogLikelihoodSquared = 0.0;

                    for (int drawIndex = 0; drawIndex < drawCount; drawIndex++)
                    {
                        double value = pointwiseLogLikelihood[observationIndex, drawIndex];
                        sumLogLikelihood += value;
                        sumLogLikelihoodSquared += value * value;
                        if (value > maxLogLikelihood)
                            maxLogLikelihood = value;
                    }

                    double sumExponentials = 0.0;
                    for (int drawIndex = 0; drawIndex < drawCount; drawIndex++)
                    {
                        sumExponentials += Math.Exp(
                            pointwiseLogLikelihood[observationIndex, drawIndex] - maxLogLikelihood);
                    }

                    double pointwiseLppd = maxLogLikelihood
                        + Math.Log(sumExponentials)
                        - Math.Log(drawCount);
                    double meanLogLikelihood = sumLogLikelihood / drawCount;
                    double pointwisePWaic = drawCount > 1
                        ? (sumLogLikelihoodSquared
                            - drawCount * meanLogLikelihood * meanLogLikelihood)
                            / (drawCount - 1)
                        : 0.0;

                    if (pointwisePWaic < 0.0)
                        pointwisePWaic = 0.0;

                    local.lppd += pointwiseLppd;
                    local.pWaic += pointwisePWaic;
                    return local;
                },
                local =>
                {
                    Tools.ParallelAdd(ref totalLppd, local.lppd);
                    Tools.ParallelAdd(ref totalPWaic, local.pWaic);
                });

            WAIC_pD = totalPWaic;
            WAIC = -2.0 * totalLppd + 2.0 * totalPWaic;
        }
        /// <summary>
        /// Computes PSIS-LOO by evaluating a fresh pointwise log-likelihood matrix.
        /// </summary>
        /// <remarks>
        /// This wrapper is used only when PSIS summaries were not produced during normal
        /// Bayesian completion, such as when restored results omit those transient values.
        /// </remarks>
        private void ComputePSISLOO()
        {
            double[,]? pointwiseLogLikelihood = BuildPointwiseLogLikelihoodMatrix();
            if (pointwiseLogLikelihood == null || pointwiseLogLikelihood.GetLength(0) == 0)
            {
                LOOIC = double.NaN;
                LOO_pD = double.NaN;
                LOOIC_SE = double.NaN;
                ParetoK = null;
                _pointwiseElpdLoo = null;
                return;
            }

            ComputePSISLOO(pointwiseLogLikelihood);
        }

        /// <summary>
        /// Computes the Leave-One-Out Information Criterion using Pareto-smoothed importance sampling.
        /// </summary>
        /// <param name="pointwiseLogLikelihood">Observation-by-draw data log likelihoods.</param>
        /// <remarks>
        /// The smoothing and generalized-Pareto fit match R <c>loo</c> 2.10.0 and
        /// <c>posterior::gpdfit</c> 1.7.0 for independent draws (<c>r_eff = 1</c>).
        /// The method is approximate LOO and performs no model refits.
        /// </remarks>
        private void ComputePSISLOO(double[,] pointwiseLogLikelihood)
        {
            int observationCount = pointwiseLogLikelihood.GetLength(0);
            int drawCount = pointwiseLogLikelihood.GetLength(1);
            int tailLength = ComputeParetoTailLength(drawCount);
            var paretoK = new double[observationCount];
            var pointwiseElpdLoo = new double[observationCount];
            var pointwiseLppd = new double[observationCount];

            Parallel.For(0, observationCount, observationIndex =>
            {
                var logLikelihoods = new double[drawCount];
                var logWeights = new double[drawCount];
                for (int drawIndex = 0; drawIndex < drawCount; drawIndex++)
                {
                    double logLikelihood = pointwiseLogLikelihood[observationIndex, drawIndex];
                    logLikelihoods[drawIndex] = logLikelihood;
                    logWeights[drawIndex] = -logLikelihood;
                }

                paretoK[observationIndex] = ParetoSmoothWeights(logWeights, tailLength);
                double logWeightNormalizer = Tools.LogSumExp(logWeights);
                var weightedLogLikelihoods = new double[drawCount];
                for (int drawIndex = 0; drawIndex < drawCount; drawIndex++)
                {
                    weightedLogLikelihoods[drawIndex] = logWeights[drawIndex]
                        - logWeightNormalizer
                        + logLikelihoods[drawIndex];
                }

                pointwiseElpdLoo[observationIndex] = Tools.LogSumExp(weightedLogLikelihoods);
                pointwiseLppd[observationIndex] = Tools.LogSumExp(logLikelihoods) - Math.Log(drawCount);
            });

            double totalElpdLoo = pointwiseElpdLoo.Sum();
            double totalLppd = pointwiseLppd.Sum();
            double standardError = double.NaN;
            if (observationCount > 1)
            {
                double mean = totalElpdLoo / observationCount;
                double sumSquares = pointwiseElpdLoo.Sum(value =>
                {
                    double difference = value - mean;
                    return difference * difference;
                });
                double sampleVariance = sumSquares / (observationCount - 1);
                standardError = 2.0 * Math.Sqrt(observationCount * sampleVariance);
            }

            ParetoK = paretoK;
            _pointwiseElpdLoo = pointwiseElpdLoo;
            LOOIC = -2.0 * totalElpdLoo;
            LOO_pD = totalLppd - totalElpdLoo;
            LOOIC_SE = standardError;
        }

        /// <summary>
        /// Computes the generalized-Pareto tail length used by R <c>loo</c> for independent draws.
        /// </summary>
        /// <param name="drawCount">Number of retained posterior draws.</param>
        /// <returns>The number of upper-tail ratios considered for smoothing.</returns>
        private static int ComputeParetoTailLength(int drawCount)
        {
            if (drawCount <= 1)
                return 0;

            int tailLength = (int)Math.Ceiling(Math.Min(
                0.2 * drawCount,
                3.0 * Math.Sqrt(drawCount)));
            return Math.Min(tailLength, drawCount - 1);
        }

        /// <summary>
        /// Applies Pareto smoothing to log importance ratios in place.
        /// </summary>
        /// <param name="logWeights">Log importance ratios, modified in place.</param>
        /// <param name="tailLength">Number of upper-tail values to smooth.</param>
        /// <returns>The estimated generalized-Pareto shape parameter.</returns>
        /// <remarks>
        /// This is the deterministic <c>loo</c> 2.10.0 algorithm: ratios are shifted by
        /// their maximum, the cutoff precedes the fitted tail, positive excesses are fit
        /// with the bounded fixed-grid estimator, expected order statistics replace the
        /// ordered tail, and final ratios are truncated at the largest raw ratio.
        /// </remarks>
        private static double ParetoSmoothWeights(double[] logWeights, int tailLength)
        {
            int sampleCount = logWeights.Length;
            if (sampleCount == 0)
                return double.NaN;

            double rawMaximum = logWeights.Max();
            for (int index = 0; index < sampleCount; index++)
                logWeights[index] -= rawMaximum;

            double paretoK = double.PositiveInfinity;
            if (tailLength >= 5 && tailLength < sampleCount)
            {
                var orderedLogWeights = (double[])logWeights.Clone();
                int[] orderedIndices = Enumerable.Range(0, sampleCount).ToArray();
                Array.Sort(orderedLogWeights, orderedIndices);
                int tailStart = sampleCount - tailLength;
                double smallestTail = orderedLogWeights[tailStart];
                double largestTail = orderedLogWeights[sampleCount - 1];

                if (Math.Abs(largestTail - smallestTail) >= 2.2204460492503131e-18)
                {
                    double cutoff = orderedLogWeights[tailStart - 1];
                    double exponentialCutoff = Math.Exp(cutoff);
                    var excesses = new double[tailLength];
                    for (int tailIndex = 0; tailIndex < tailLength; tailIndex++)
                    {
                        excesses[tailIndex] = Math.Exp(orderedLogWeights[tailStart + tailIndex])
                            - exponentialCutoff;
                    }

                    (paretoK, double scale) = FitGeneralizedParetoTail(excesses);
                    if (double.IsFinite(paretoK))
                    {
                        for (int tailIndex = 0; tailIndex < tailLength; tailIndex++)
                        {
                            double probability = (tailIndex + 0.5) / tailLength;
                            double quantile = GeneralizedParetoQuantile(probability, scale, paretoK);
                            orderedLogWeights[tailStart + tailIndex] = Math.Log(
                                quantile + exponentialCutoff);
                        }

                        for (int tailIndex = tailStart; tailIndex < sampleCount; tailIndex++)
                            logWeights[orderedIndices[tailIndex]] = orderedLogWeights[tailIndex];
                    }
                }
            }

            for (int index = 0; index < sampleCount; index++)
            {
                if (logWeights[index] > 0.0)
                    logWeights[index] = 0.0;
                logWeights[index] += rawMaximum;
            }

            return paretoK;
        }

        /// <summary>
        /// Fits a zero-location generalized Pareto distribution with the bounded fixed-grid estimator.
        /// </summary>
        /// <param name="orderedExcesses">Ascending positive excesses over the Pareto cutoff.</param>
        /// <returns>The shape and scale estimates.</returns>
        /// <remarks>
        /// The calculation follows <c>posterior::gpdfit</c> 1.7.0 with weakly informative
        /// prior shrinkage enabled. It uses a fixed grid rather than an iterative optimizer,
        /// keeping the per-observation PSIS cost small and deterministic.
        /// </remarks>
        private static (double Shape, double Scale) FitGeneralizedParetoTail(double[] orderedExcesses)
        {
            int sampleCount = orderedExcesses.Length;
            int gridCount = 30 + (int)Math.Floor(Math.Sqrt(sampleCount));
            int quarterIndex = (int)Math.Floor(sampleCount / 4.0 + 0.5) - 1;
            quarterIndex = Math.Clamp(quarterIndex, 0, sampleCount - 1);
            double referenceExcess = orderedExcesses[quarterIndex];
            double maximumExcess = orderedExcesses[sampleCount - 1];

            if (!(referenceExcess > orderedExcesses[0]) || !(maximumExcess > 0.0))
                return (double.NaN, double.NaN);

            const double prior = 3.0;
            var theta = new double[gridCount];
            var logPosterior = new double[gridCount];
            for (int gridIndex = 0; gridIndex < gridCount; gridIndex++)
            {
                double gridPosition = gridIndex + 1.0;
                theta[gridIndex] = 1.0 / maximumExcess
                    + (1.0 - Math.Sqrt(gridCount / (gridPosition - 0.5)))
                    / (prior * referenceExcess);

                double shapeAtGridPoint = 0.0;
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    shapeAtGridPoint += Tools.Log1p(
                        -theta[gridIndex] * orderedExcesses[sampleIndex]);
                }
                shapeAtGridPoint /= sampleCount;

                logPosterior[gridIndex] = sampleCount * (
                    Math.Log(-theta[gridIndex] / shapeAtGridPoint)
                    - shapeAtGridPoint
                    - 1.0);
            }

            double logNormalizer = Tools.LogSumExp(logPosterior);
            double thetaEstimate = 0.0;
            for (int gridIndex = 0; gridIndex < gridCount; gridIndex++)
            {
                thetaEstimate += theta[gridIndex]
                    * Math.Exp(logPosterior[gridIndex] - logNormalizer);
            }

            double shapeEstimate = 0.0;
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                shapeEstimate += Tools.Log1p(
                    -thetaEstimate * orderedExcesses[sampleIndex]);
            }
            shapeEstimate /= sampleCount;
            double scaleEstimate = -shapeEstimate / thetaEstimate;
            shapeEstimate = (shapeEstimate * sampleCount + 5.0) / (sampleCount + 10.0);

            if (double.IsNaN(shapeEstimate))
                return (double.PositiveInfinity, double.NaN);

            return (shapeEstimate, scaleEstimate);
        }

        /// <summary>
        /// Evaluates the zero-location generalized-Pareto quantile function.
        /// </summary>
        /// <param name="probability">Nonexceedance probability strictly between zero and one.</param>
        /// <param name="scale">Positive generalized-Pareto scale.</param>
        /// <param name="shape">Generalized-Pareto shape.</param>
        /// <returns>The requested generalized-Pareto quantile.</returns>
        private static double GeneralizedParetoQuantile(double probability, double scale, double shape)
        {
            if (double.IsNaN(scale) || scale <= 0.0)
                return double.NaN;

            double logSurvival = Tools.Log1p(-probability);
            if (shape == 0.0)
                return -scale * logSurvival;

            return scale * ExponentialMinusOne(-shape * logSurvival) / shape;
        }

        /// <summary>
        /// Evaluates <c>exp(x)-1</c> accurately when <paramref name="value"/> is near zero.
        /// </summary>
        /// <param name="value">Exponent argument.</param>
        /// <returns><c>exp(value)-1</c>.</returns>
        private static double ExponentialMinusOne(double value)
        {
            if (Math.Abs(value) > 1e-5)
                return Math.Exp(value) - 1.0;

            double valueSquared = value * value;
            return value
                + 0.5 * valueSquared
                + valueSquared * value / 6.0
                + valueSquared * valueSquared / 24.0
                + valueSquared * valueSquared * value / 120.0;
        }
        /// <summary>
        /// Sets custom MCMC results that were run externally.
        /// </summary>
        /// <param name="results">The MCMC results.</param>
        public void SetCustomMCMCResults(MCMCResults results)
        {
            SetCustomMCMCResults(results, skipInformationCriteria: false);
        }

        /// <summary>
        /// Sets custom MCMC results that were run externally.
        /// </summary>
        /// <param name="results">The MCMC results.</param>
        /// <param name="skipInformationCriteria">When true, skips recomputation of DIC, WAIC, and LOO-CV.
        /// Use this when restoring persisted results where information criteria have already been
        /// deserialized from the XElement constructor.</param>
        public void SetCustomMCMCResults(MCMCResults results, bool skipInformationCriteria)
        {
            Results = results;
            ParetoK = null;
            _pointwiseElpdLoo = null;
            if (!skipInformationCriteria)
            {
                ComputeDIC();
                ComputePredictiveInformationCriteria();
            }
            IsEstimated = true;
        }

        /// <summary>
        /// Sets custom MCMC results with explicit parameter names for Model-less analyses.
        /// </summary>
        /// <param name="results">The MCMC results.</param>
        /// <param name="skipInformationCriteria">When true, skips recomputation of DIC, WAIC, and LOO-CV.</param>
        /// <param name="parameterNames">Display names for the estimated parameters. Required when
        /// <see cref="Model"/> is null (e.g., GMM-based analyses like Bulletin 17C).</param>
        public void SetCustomMCMCResults(MCMCResults results, bool skipInformationCriteria,
            IReadOnlyList<string> parameterNames)
        {
            _parameterNames = parameterNames;
            SetCustomMCMCResults(results, skipInformationCriteria);
            RaisePropertyChange(nameof(ParameterNames));
        }

        /// <summary>
        /// Computes influence diagnostics for individual observations.
        /// </summary>
        /// <returns>
        /// An <see cref="InfluenceDiagnostics"/> object containing Pareto k values,
        /// ELPD-LOO contributions, and metadata for each observation.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method requires that MCMC estimation has been completed (i.e., <c>Estimate</c>
        /// has been called). The diagnostics are based on PSIS-LOO (Pareto Smoothed Importance
        /// Sampling Leave-One-Out cross-validation).
        /// </para>
        /// <para>
        /// The returned object provides:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Pareto k values indicating observation influence on the posterior</description></item>
        /// <item><description>Pointwise ELPD-LOO contributions for predictive accuracy</description></item>
        /// <item><description>Data metadata (type, value, count, name) for each observation</description></item>
        /// <item><description>Summary statistics and reliability assessment</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when estimation has not been completed.</exception>
        public InfluenceDiagnostics ComputeInfluenceDiagnostics()
        {
            if (!IsEstimated || Results == null || Model == null)
                throw new InvalidOperationException("Estimation must be completed before computing influence diagnostics.");

            // Ensure PSIS-LOO has been computed (may be skipped during deserialization)
            if (ParetoK == null || ParetoK.Length == 0)
                ComputePSISLOO();

            if (ParetoK == null || ParetoK.Length == 0)
                return new InfluenceDiagnostics();

            // Reuse pointwise summaries retained during the default information-criterion calculation.
            var elpdLoo = ComputePointwiseElpdLoo();

            // Get data components for metadata
            List<DataComponent>? dataComponents = null;
            try
            {
                var firstParams = GetModelParameterValues(Results.Output[0].Values);
                dataComponents = Model.PointwiseDataLogLikelihoodComponents(firstParams);
            }
            catch (Exception ex)
            {
                // Fall back to no metadata if method not implemented or fails.
                Debug.WriteLine($"BayesianAnalysis.GetInfluenceDiagnostics: PointwiseDataLogLikelihoodComponents unavailable: {ex.Message}");
            }

            double diagnosticThreshold = ComputeParetoDiagnosticThreshold(Results.Output.Count);
            return new InfluenceDiagnostics(ParetoK, elpdLoo, dataComponents, diagnosticThreshold);
        }

        /// <summary>
        /// Computes prior influence diagnostics.
        /// </summary>
        /// <param name="thinEvery">Thin posterior samples by this factor. Default is 10.</param>
        /// <returns>
        /// A <see cref="PriorInfluenceDiagnostics"/> object containing prior component
        /// summaries and overall prior influence assessment.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method analyzes how different prior components contribute to the posterior
        /// distribution. It helps identify which priors are most constraining and whether
        /// the prior is influential relative to the data.
        /// </para>
        /// <para>
        /// The returned object provides:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Summary statistics for each prior component type</description></item>
        /// <item><description>Prior-to-data log-likelihood ratio</description></item>
        /// <item><description>Assessment of overall prior influence</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when estimation has not been completed.</exception>
        public PriorInfluenceDiagnostics ComputePriorInfluenceDiagnostics(int thinEvery = 10)
        {
            if (!IsEstimated || Results == null || Model == null)
                throw new InvalidOperationException("Estimation must be completed before computing prior influence diagnostics.");

            return new PriorInfluenceDiagnostics(Model, Results, thinEvery);
        }

        /// <summary>
        /// Computes leverage-based influence diagnostics at the MAP estimate using the Hessian of the full posterior.
        /// </summary>
        /// <returns>
        /// A <see cref="LeverageDiagnostics"/> object containing per-observation and per-prior-component
        /// fit influence, variance influence, and their combined ranking at the MAP.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The posterior Hessian is computed numerically via central differences on <see cref="IModel.LogLikelihood"/>
        /// at the MCMC MAP values. No optimization step is performed. The MAP values come directly from the
        /// MCMC posterior samples (<see cref="Numerics.Sampling.MCMC.MCMCResults.MAP"/>).
        /// </para>
        /// <para>
        /// Fit influence is a Cook score quadratic. Observation variance influence uses a local
        /// curvature trace, while prior variance influence uses the finite log generalized-variance
        /// change after removing that prior. Their sum is a combined ranking index; it is not a
        /// hat-matrix diagonal and is not expected to sum to the number of parameters.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when estimation has not been completed.</exception>
        public LeverageDiagnostics ComputeLeverageDiagnostics()
        {
            if (!IsEstimated || Results == null || Model == null)
                throw new InvalidOperationException("Estimation must be completed before computing leverage diagnostics.");

            return new LeverageDiagnostics(Model, Results.MAP.Values);
        }

        /// <summary>
        /// Gets cached pointwise ELPD-LOO values for each observation.
        /// </summary>
        /// <returns>One ELPD-LOO contribution per observation.</returns>
        /// <remarks>
        /// Normal Bayesian completion populates this cache while the shared pointwise matrix is
        /// available. A fresh PSIS calculation occurs only when restored or externally supplied
        /// results do not contain transient pointwise summaries.
        /// </remarks>
        private double[] ComputePointwiseElpdLoo()
        {
            if (_pointwiseElpdLoo == null)
                ComputePSISLOO();

            return _pointwiseElpdLoo == null
                ? Array.Empty<double>()
                : (double[])_pointwiseElpdLoo.Clone();
        }

        /// <summary>
        /// Computes the sample-size-dependent Pareto-k reliability threshold used by R <c>loo</c>.
        /// </summary>
        /// <param name="drawCount">Number of retained posterior draws.</param>
        /// <returns>The diagnostic threshold, capped at 0.7.</returns>
        private static double ComputeParetoDiagnosticThreshold(int drawCount)
        {
            if (drawCount <= 1)
                return double.NegativeInfinity;

            return Math.Min(1.0 - 1.0 / Math.Log10(drawCount), 0.7);
        }
        /// <summary>
        /// Computes the posterior covariance matrix from the MCMC output samples
        /// using <see cref="RunningCovarianceMatrix"/>.
        /// </summary>
        /// <returns>
        /// A p x p sample covariance matrix where p is the number of stored sampler coordinates,
        /// or null if estimation has not been completed.
        /// </returns>
        /// <remarks>
        /// Uses Welford's online algorithm via <see cref="RunningCovarianceMatrix"/> for numerical stability.
        /// The matrix uses Bessel's correction (N-1 denominator).
        /// </remarks>
        public double[,]? GetPosteriorCovarianceMatrix()
        {
            if (!IsEstimated || Results == null || Results.Output == null || Results.Output.Count < 2 || Model == null)
                return null;

            int p = Results.Output[0].Values.Length;
            var rcm = new RunningCovarianceMatrix(p);
            foreach (var ps in Results.Output)
                rcm.Push(ps.Values);

            return rcm.SampleCovariance.ToArray();
        }

        /// <summary>
        /// Computes the posterior correlation matrix from the MCMC output samples
        /// using <see cref="RunningCovarianceMatrix"/>.
        /// </summary>
        /// <returns>
        /// A p x p sample correlation matrix where p is the number of stored sampler coordinates,
        /// or null if estimation has not been completed.
        /// </returns>
        /// <remarks>
        /// Derived from the sample covariance matrix: Corr[i,j] = Cov[i,j] / (SD[i] * SD[j]).
        /// </remarks>
        public double[,]? GetPosteriorCorrelationMatrix()
        {
            if (!IsEstimated || Results == null || Results.Output == null || Results.Output.Count < 2 || Model == null)
                return null;

            int p = Results.Output[0].Values.Length;
            var rcm = new RunningCovarianceMatrix(p);
            foreach (var ps in Results.Output)
                rcm.Push(ps.Values);

            return rcm.SampleCorrelation.ToArray();
        }

        #region Report Generation

        /// <summary>
        /// Generates a comprehensive plain-text MCMC simulation report.
        /// </summary>
        /// <returns>
        /// A formatted string containing simulation configuration, execution performance,
        /// acceptance rates, convergence diagnostics, parameter estimates, covariance/correlation
        /// matrices, information criteria, and prior configuration. Returns an empty string if
        /// the analysis has not been estimated or results are unavailable.
        /// </returns>
        /// <remarks>
        /// This method produces a WPF-independent plain-text report suitable for display in any
        /// text control or for saving to disk. The App layer's MCMCReportControl renders this
        /// text into a FlowDocument for display in a RichTextBox.
        /// </remarks>
        public string GenerateReport()
        {
            if (!IsEstimated || Results == null || Model == null)
                return string.Empty;

            var sb = new StringBuilder();
            IReadOnlyList<int> storedModelIndexes = GetStoredModelParameterIndexes();
            int p = storedModelIndexes.Count;

            // Credible interval percentiles
            double ciWidth = CredibleIntervalWidth;
            double lowerPct = (1.0 - ciWidth) / 2.0 * 100.0;
            double upperPct = (1.0 + ciWidth) / 2.0 * 100.0;

            AppendReportHeader(sb, "MCMC SIMULATION REPORT");

            // Section 1: Simulation Configuration
            AppendReportSectionHeader(sb, "SIMULATION CONFIGURATION");
            int totalRaw = Iterations * ThinningInterval * NumberOfChains;
            const int labelWidth = 24;
            sb.AppendLine($"  {"Sampler:".PadRight(labelWidth)}{FormatSamplerType(Type)}");
            sb.AppendLine($"  {"Number of Chains:".PadRight(labelWidth)}{NumberOfChains:N0}");
            sb.AppendLine($"  {"Iterations (thinned):".PadRight(labelWidth)}{Iterations:N0}");
            sb.AppendLine($"  {"Warmup Iterations:".PadRight(labelWidth)}{WarmupIterations:N0}");
            sb.AppendLine($"  {"Thinning Interval:".PadRight(labelWidth)}{ThinningInterval:N0}");
            sb.AppendLine($"  {"Total Raw Iterations:".PadRight(labelWidth)}{totalRaw:N0}");
            sb.AppendLine($"  {"Initial Iterations:".PadRight(labelWidth)}{InitialIterations:N0}");
            sb.AppendLine($"  {"PRNG Seed:".PadRight(labelWidth)}{PRNGSeed}");
            sb.AppendLine($"  {"Output Length:".PadRight(labelWidth)}{OutputLength:N0}");
            sb.AppendLine($"  {"Credible Interval:".PadRight(labelWidth)}{ciWidth * 100:F0}%");
            sb.AppendLine();

            // Section 2: Execution Performance
            if (ElapsedTime.HasValue)
            {
                AppendReportSectionHeader(sb, "EXECUTION PERFORMANCE");
                var elapsed = ElapsedTime.Value;
                sb.AppendLine($"  {"Elapsed Time:".PadRight(labelWidth)}{elapsed:hh\\:mm\\:ss\\.fff}");
                if (elapsed.TotalSeconds > 0)
                {
                    double itersPerSec = (double)totalRaw / elapsed.TotalSeconds;
                    sb.AppendLine($"  {"Iterations/Second:".PadRight(labelWidth)}{itersPerSec:N0}");
                }
                sb.AppendLine();
            }

            // Section 3: Sampler Diagnostics
            double overallAcceptance = double.NaN;
            bool acceptanceWarning = false;
            if (Type == SamplerType.NUTS &&
                Results.AcceptanceRates != null && Results.AcceptanceRates.Length > 0)
            {
                AppendReportSectionHeader(sb, "NUTS SAMPLER DIAGNOSTICS");
                for (int i = 0; i < Results.AcceptanceRates.Length; i++)
                {
                    sb.AppendLine(
                        $"  Chain {i + 1}:   {Results.AcceptanceRates[i] * 100.0:F1}% Hamiltonian acceptance");
                }

                overallAcceptance = Results.AcceptanceRates.Average();
                double targetAcceptanceRate = Sampler is NUTS nuts
                    ? nuts.TargetAcceptanceRate
                    : 0.80d;
                sb.AppendLine($"  Overall Hamiltonian Acceptance: {overallAcceptance * 100.0:F1}%");

                var acceptance = AssessAcceptanceRate(overallAcceptance, Type);
                bool chainAcceptanceWarning = AppendChainAcceptanceWarnings(sb, Results.AcceptanceRates, Type);
                acceptanceWarning = acceptance.Status == ReportDiagnosticStatus.Warning || chainAcceptanceWarning;
                sb.AppendLine($"  Target:    {targetAcceptanceRate * 100.0:F0}% Hamiltonian acceptance");
                sb.AppendLine($"  Preferred: {acceptance.PreferredRangeLabel}");
                sb.AppendLine($"  Buffer:    {acceptance.AcceptableRangeLabel}");
                sb.AppendLine($"  Status:    {GetStatusLabel(acceptance.Status)} - {acceptance.Message}");
                AppendAcceptanceAdvice(sb, acceptance, Type);
                sb.AppendLine();
            }
            else if (Results.AcceptanceRates != null && Results.AcceptanceRates.Length > 0)
            {
                AppendReportSectionHeader(sb, "ACCEPTANCE RATES");
                for (int i = 0; i < Results.AcceptanceRates.Length; i++)
                    sb.AppendLine($"  Chain {i + 1}:   {Results.AcceptanceRates[i] * 100.0:F1}%");

                overallAcceptance = Results.AcceptanceRates.Average();
                sb.AppendLine($"  Overall:   {overallAcceptance * 100.0:F1}%");

                var acceptance = AssessAcceptanceRate(overallAcceptance, Type);
                bool chainAcceptanceWarning = AppendChainAcceptanceWarnings(sb, Results.AcceptanceRates, Type);
                acceptanceWarning = acceptance.Status == ReportDiagnosticStatus.Warning || chainAcceptanceWarning;
                sb.AppendLine($"  Preferred: {acceptance.PreferredRangeLabel}");
                sb.AppendLine($"  Buffer:    {acceptance.AcceptableRangeLabel}");
                sb.AppendLine($"  Status:    {GetStatusLabel(acceptance.Status)} - {acceptance.Message}");
                AppendAcceptanceAdvice(sb, acceptance, Type);

                sb.AppendLine();
            }

            // Section 4: Convergence Diagnostics
            AppendReportSectionHeader(sb, "CONVERGENCE AND PRECISION DIAGNOSTICS");
            double maxRhat = double.NaN;
            double minESS = double.NaN;
            string worstRhatParam = "";
            string worstESSParam = "";
            if (Results.ParameterResults != null && Results.ParameterResults.Length > 0)
            {
                int parameterCount = Math.Min(p, Results.ParameterResults.Length);
                for (int i = 0; i < parameterCount; i++)
                {
                    double rhat = Results.ParameterResults[i].SummaryStatistics.Rhat;
                    double ess = Results.ParameterResults[i].SummaryStatistics.ESS;
                    int modelIndex = storedModelIndexes[i];
                    if (double.IsNaN(maxRhat) || rhat > maxRhat) { maxRhat = rhat; worstRhatParam = Model.Parameters[modelIndex].DisplayName; }
                    if (double.IsNaN(minESS) || ess < minESS) { minESS = ess; worstESSParam = Model.Parameters[modelIndex].DisplayName; }
                }
            }
            int retainedDrawCount = GetRetainedDrawCount(Results, OutputLength);
            var essAssessment = AssessEffectiveSampleSize(minESS, retainedDrawCount);
            bool rhatOk = !double.IsNaN(maxRhat) && maxRhat < RhatReadinessThreshold;
            bool essOk = essAssessment.Status == ReportDiagnosticStatus.OK;
            bool ready = rhatOk && essOk;

            sb.AppendLine($"  Max R-hat:   {maxRhat:F4}   (target < {RhatReadinessThreshold:F2})   {(rhatOk ? "OK" : $"WARNING ({worstRhatParam})")}");
            sb.AppendLine($"  Min ESS:     {FormatEssSummary(essAssessment)}");
            sb.AppendLine($"  ESS Target:  >= {EssPreferredEfficiency * 100.0:F0}% of retained draws and >= {EssDiagnosticFloor:N0} diagnostic floor");
            sb.AppendLine($"  R-hat Verdict: {GetStatusLabel(rhatOk ? ReportDiagnosticStatus.OK : ReportDiagnosticStatus.Warning)} - {(rhatOk ? "chains mixed across parameters" : $"chain mixing problem detected for {worstRhatParam}")}");
            sb.AppendLine($"  ESS Verdict:   {GetStatusLabel(essAssessment.Status)} - {essAssessment.Message}");
            sb.AppendLine($"  Overall Readiness: {(ready ? "READY" : "NOT READY")}");
            if (!ready)
            {
                sb.AppendLine();
                AppendConvergenceAdvice(sb, maxRhat, rhatOk, essAssessment, worstRhatParam,
                    worstESSParam, acceptanceWarning, overallAcceptance, Type);
            }
            sb.AppendLine();

            // Section 5: Posterior Mode (MAP)
            AppendReportSectionHeader(sb, "POSTERIOR MODE (MAP)");
            int maxNameLen = Model.Parameters.Max(param => param.DisplayName.Length);
            maxNameLen = Math.Max(maxNameLen, 9); // "Parameter" header
            sb.AppendLine($"  {"Parameter".PadRight(maxNameLen)}  MAP Estimate");
            sb.AppendLine($"  {new string('-', maxNameLen)}  {new string('-', 14)}");
            for (int i = 0; i < p; i++)
            {
                string name = Model.Parameters[storedModelIndexes[i]].DisplayName.PadRight(maxNameLen);
                sb.AppendLine($"  {name}  {Results.MAP.Values[i]:G6}");
            }
            sb.AppendLine();
            sb.AppendLine($"  Joint MAP Log-Likelihood: {Results.MAP.Fitness:G6}");
            sb.AppendLine();

            // Section 6: Parameter Summary Statistics
            AppendReportSectionHeader(sb, "PARAMETER SUMMARY STATISTICS");
            string lowerLabel = $"{lowerPct:F0}%".PadLeft(10);
            string upperLabel = $"{upperPct:F0}%".PadLeft(10);
            sb.AppendLine($"  {"Parameter".PadRight(maxNameLen)}  {"Mean",10}  {"Std Dev",10}  {lowerLabel}  {"Median",10}  {upperLabel}  {"R-hat",7}  {"ESS",7}");
            sb.AppendLine($"  {new string('-', maxNameLen)}  {new string('-', 10)}  {new string('-', 10)}  {new string('-', 10)}  {new string('-', 10)}  {new string('-', 10)}  {new string('-', 7)}  {new string('-', 7)}");
            for (int i = 0; i < p; i++)
            {
                var stats = Results.ParameterResults![i].SummaryStatistics;
                string name = Model.Parameters[storedModelIndexes[i]].DisplayName.PadRight(maxNameLen);
                sb.AppendLine($"  {name}  {stats.Mean,10:G6}  {stats.StandardDeviation,10:G6}  {stats.LowerCI,10:G6}  {stats.Median,10:G6}  {stats.UpperCI,10:G6}  {stats.Rhat,7:F4}  {stats.ESS,7:F0}");
            }
            sb.AppendLine();

            // Section 7: Posterior Covariance and Correlation Matrices
            if (p >= 2)
            {
                var covMatrix = GetPosteriorCovarianceMatrix();
                if (covMatrix != null)
                {
                    AppendReportSectionHeader(sb, "POSTERIOR COVARIANCE MATRIX");
                    AppendReportMatrix(sb, covMatrix, storedModelIndexes, maxNameLen, "G4");

                    var corrMatrix = GetPosteriorCorrelationMatrix();
                    if (corrMatrix != null)
                    {
                        AppendReportSectionHeader(sb, "POSTERIOR CORRELATION MATRIX");
                        AppendReportMatrix(sb, corrMatrix, storedModelIndexes, maxNameLen, "F3");
                    }
                }
            }

            // Section 8: Information Criteria
            AppendReportSectionHeader(sb, "INFORMATION CRITERIA");
            sb.AppendLine($"  DIC:         {DIC:F2}");
            sb.AppendLine($"  WAIC:        {WAIC:F2}    (p_D = {WAIC_pD:F2})");
            sb.AppendLine($"  LOOIC:       {LOOIC:F2}    (p_D = {LOO_pD:F2}, SE = {LOOIC_SE:F1})");
            if (ParetoK != null && ParetoK.Length > 0)
            {
                double threshold = ComputeParetoDiagnosticThreshold(Results.Output.Count);
                int unreliableCount = ParetoK.Count(k => k >= threshold);
                sb.AppendLine($"  Pareto k:    {unreliableCount}/{ParetoK.Length} observations with k >= {threshold:F3}");
            }
            sb.AppendLine();

            // Section 9: Prior Configuration
            AppendReportSectionHeader(sb, "PRIOR CONFIGURATION");
            sb.AppendLine($"  {"Parameter".PadRight(maxNameLen)}  {"Prior",-24}  {"Bounds",-20}  Fixed");
            sb.AppendLine($"  {new string('-', maxNameLen)}  {new string('-', 24)}  {new string('-', 20)}  {new string('-', 5)}");
            for (int i = 0; i < Model.Parameters.Count; i++)
            {
                var param = Model.Parameters[i];
                string name = param.DisplayName.PadRight(maxNameLen);
                string prior = param.PriorDistribution is not null ? param.PriorDistribution.DisplayLabel : "None";
                string lo = double.IsNegativeInfinity(param.LowerBound) ? "-Inf" : param.LowerBound.ToString("G6", CultureInfo.InvariantCulture);
                string hi = double.IsPositiveInfinity(param.UpperBound) ? "+Inf" : param.UpperBound.ToString("G6", CultureInfo.InvariantCulture);
                string bounds = $"({lo}, {hi})";
                string isFixed = param.IsFixed ? "Yes" : "No";
                sb.AppendLine($"  {name}  {prior,-24}  {bounds,-20}  {isFixed}");
            }
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Appends a report header with box-drawing decoration.
        /// </summary>
        private static void AppendReportHeader(StringBuilder sb, string title)
        {
            string line = new string('=', 60);
            sb.AppendLine(line);
            int padding = (60 - title.Length) / 2;
            sb.AppendLine(new string(' ', Math.Max(0, padding)) + title);
            sb.AppendLine(line);
            sb.AppendLine();
        }

        /// <summary>
        /// Appends a section header with separator line.
        /// </summary>
        private static void AppendReportSectionHeader(StringBuilder sb, string title)
        {
            sb.AppendLine(title);
            sb.AppendLine(new string('-', 60));
        }

        /// <summary>
        /// Appends a formatted p x p matrix to the report.
        /// </summary>
        /// <param name="sb">The string builder.</param>
        /// <param name="matrix">The matrix to format.</param>
        /// <param name="modelParameterIndexes">The public model indexes corresponding to the matrix coordinates.</param>
        /// <param name="maxNameLen">Maximum parameter name length for alignment.</param>
        /// <param name="format">Numeric format string (e.g., "G4" or "F3").</param>
        private void AppendReportMatrix(
            StringBuilder sb,
            double[,] matrix,
            IReadOnlyList<int> modelParameterIndexes,
            int maxNameLen,
            string format)
        {
            if (Model is null) return;
            int p = modelParameterIndexes.Count;
            int colWidth = Math.Max(12, maxNameLen);

            // Column headers
            sb.Append("  " + new string(' ', maxNameLen));
            for (int j = 0; j < p; j++)
                sb.Append($"  {Model.Parameters[modelParameterIndexes[j]].DisplayName.PadLeft(colWidth)}");
            sb.AppendLine();

            // Rows
            for (int i = 0; i < p; i++)
            {
                string rowName = Model.Parameters[modelParameterIndexes[i]].DisplayName.PadRight(maxNameLen);
                sb.Append($"  {rowName}");
                for (int j = 0; j < p; j++)
                    sb.Append($"  {matrix[i, j].ToString(format, CultureInfo.InvariantCulture).PadLeft(colWidth)}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        /// <summary>
        /// Appends advice text for an assessed acceptance rate.
        /// </summary>
        /// <param name="sb">The report builder.</param>
        /// <param name="assessment">The acceptance-rate diagnostic assessment.</param>
        /// <param name="type">The MCMC sampler type.</param>
        private static void AppendAcceptanceAdvice(StringBuilder sb, AcceptanceRateAssessment assessment, SamplerType type)
        {
            if (assessment.Status == ReportDiagnosticStatus.OK)
                return;

            sb.AppendLine();
            if (assessment.Status == ReportDiagnosticStatus.Note)
            {
                sb.AppendLine("  Note: Acceptance rate is outside the preferred range but within the acceptable buffer.");
                sb.AppendLine("    - Acceptance rate is a sampler-efficiency diagnostic, not a convergence verdict by itself.");
                sb.AppendLine("    - If R-hat, ESS, trace plots, and autocorrelation are acceptable, no immediate rerun is required.");
                sb.AppendLine("    - Tune only if other diagnostics show poor mixing or low effective sample size.");
                return;
            }

            if (assessment.Direction == DiagnosticDirection.Low)
            {
                sb.AppendLine(type == SamplerType.NUTS
                    ? "  Advice: Hamiltonian acceptance is below the acceptable range."
                    : "  Advice: Acceptance rate is too LOW. Proposals are too ambitious.");
                AppendLowAcceptanceTuningAdvice(sb, type);
            }
            else if (assessment.Direction == DiagnosticDirection.High)
            {
                sb.AppendLine(type == SamplerType.NUTS
                    ? "  Advice: Hamiltonian acceptance is above the acceptable range."
                    : "  Advice: Acceptance rate is too HIGH. Proposals are too timid.");
                AppendHighAcceptanceTuningAdvice(sb, type);
            }
        }

        /// <summary>
        /// Appends detailed advice when MCMC convergence or precision diagnostics indicate problems.
        /// </summary>
        /// <param name="sb">The report builder.</param>
        /// <param name="maxRhat">The maximum R-hat across all parameters.</param>
        /// <param name="rhatOk">Whether the R-hat diagnostic passed.</param>
        /// <param name="essAssessment">The effective sample size diagnostic assessment.</param>
        /// <param name="worstRhatParam">Display name of the parameter with the worst R-hat.</param>
        /// <param name="worstESSParam">Display name of the parameter with the worst ESS.</param>
        /// <param name="acceptanceWarning">Whether acceptance-rate diagnostics include a warning.</param>
        /// <param name="overallAcceptance">The overall acceptance rate (0 to 1).</param>
        /// <param name="type">The MCMC sampler type.</param>
        private static void AppendConvergenceAdvice(StringBuilder sb, double maxRhat, bool rhatOk,
            EffectiveSampleSizeAssessment essAssessment, string worstRhatParam, string worstESSParam,
            bool acceptanceWarning, double overallAcceptance, SamplerType type)
        {
            sb.AppendLine("  Recommendations:");

            if (!rhatOk)
            {
                sb.AppendLine();
                sb.AppendLine($"  R-hat ({worstRhatParam} = {maxRhat:F4}):");
                sb.AppendLine("    R-hat > 1.10 indicates chains have not mixed well.");
                if (maxRhat >= 2.0)
                {
                    sb.AppendLine("    R-hat is very large. Chains may be stuck in different modes.");
                    sb.AppendLine("    - Increase warmup iterations substantially (e.g., 2-5x current).");
                    sb.AppendLine("    - Increase the number of chains.");
                    sb.AppendLine("    - Review the model for identifiability issues.");
                }
                else
                {
                    sb.AppendLine("    - Increase warmup iterations to allow chains to converge.");
                    sb.AppendLine("    - Increase total iterations for better mixing.");
                }
                if (acceptanceWarning && !double.IsNaN(overallAcceptance))
                    sb.AppendLine("    - Review the acceptance-rate warning above, then re-run.");
            }

            if (essAssessment.Status != ReportDiagnosticStatus.OK)
            {
                sb.AppendLine();
                sb.AppendLine($"  ESS ({worstESSParam} = {essAssessment.EffectiveSampleSize:N0}):");
                sb.AppendLine("    ESS is the independent-equivalent sample size used to estimate Monte Carlo error.");
                sb.AppendLine("    Low ESS means retained posterior draws are highly autocorrelated.");
                sb.AppendLine("    Posterior summaries and credible intervals may be noisier than the retained draw count suggests.");
                if (rhatOk)
                    sb.AppendLine("    - R-hat is acceptable, so the chains may have mixed, but retained draws are inefficient.");
                if (essAssessment.EffectiveSampleSize <= EssSevereFloor)
                    sb.AppendLine("    - ESS <= 100 is very low; quantile estimates will be noisy.");
                else if (essAssessment.EffectiveSampleSize < EssDiagnosticFloor)
                    sb.AppendLine("    - ESS is below the 400 diagnostic floor used for stable Monte Carlo error estimates.");
                if (essAssessment.Efficiency < EssWarningEfficiency)
                    sb.AppendLine("    - ESS is below 10% of retained draws; improve sampler efficiency before relying on summaries.");
                sb.AppendLine("    - Increase iterations/output length only after inspecting trace and autocorrelation plots.");
                AppendSamplerEfficiencyAdvice(sb, type);
            }
        }

        /// <summary>
        /// Represents the diagnostic status assigned to a report item.
        /// </summary>
        private enum ReportDiagnosticStatus
        {
            /// <summary>
            /// The diagnostic is within the preferred range.
            /// </summary>
            OK,

            /// <summary>
            /// The diagnostic is usable but deserves user review.
            /// </summary>
            Note,

            /// <summary>
            /// The diagnostic is outside the acceptable range.
            /// </summary>
            Warning
        }

        /// <summary>
        /// Represents whether a diagnostic value is low, high, or neither.
        /// </summary>
        private enum DiagnosticDirection
        {
            /// <summary>
            /// The diagnostic has no directional problem.
            /// </summary>
            None,

            /// <summary>
            /// The diagnostic is below its target range.
            /// </summary>
            Low,

            /// <summary>
            /// The diagnostic is above its target range.
            /// </summary>
            High
        }

        /// <summary>
        /// Readiness threshold for rank-normalized split/folded R-hat.
        /// </summary>
        private const double RhatReadinessThreshold = 1.01;

        /// <summary>
        /// The minimum effective sample size used as a diagnostic floor.
        /// </summary>
        private const double EssDiagnosticFloor = 400.0;

        /// <summary>
        /// The severe effective sample size warning threshold.
        /// </summary>
        private const double EssSevereFloor = 100.0;

        /// <summary>
        /// Preferred ESS efficiency as a fraction of retained posterior draws.
        /// </summary>
        private const double EssPreferredEfficiency = 0.25;

        /// <summary>
        /// Warning ESS efficiency as a fraction of retained posterior draws.
        /// </summary>
        private const double EssWarningEfficiency = 0.10;

        /// <summary>
        /// Contains preferred and acceptable acceptance-rate thresholds.
        /// </summary>
        private readonly struct AcceptanceRateThresholds
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="AcceptanceRateThresholds"/> struct.
            /// </summary>
            /// <param name="preferredMin">The lower preferred acceptance rate.</param>
            /// <param name="preferredMax">The upper preferred acceptance rate.</param>
            /// <param name="acceptableMin">The lower acceptable acceptance rate.</param>
            /// <param name="acceptableMax">The upper acceptable acceptance rate.</param>
            public AcceptanceRateThresholds(double preferredMin, double preferredMax,
                double acceptableMin, double acceptableMax)
            {
                PreferredMin = preferredMin;
                PreferredMax = preferredMax;
                AcceptableMin = acceptableMin;
                AcceptableMax = acceptableMax;
            }

            /// <summary>
            /// Gets the lower preferred acceptance rate.
            /// </summary>
            public double PreferredMin { get; }

            /// <summary>
            /// Gets the upper preferred acceptance rate.
            /// </summary>
            public double PreferredMax { get; }

            /// <summary>
            /// Gets the lower acceptable acceptance rate.
            /// </summary>
            public double AcceptableMin { get; }

            /// <summary>
            /// Gets the upper acceptable acceptance rate.
            /// </summary>
            public double AcceptableMax { get; }
        }

        /// <summary>
        /// Contains an acceptance-rate diagnostic assessment.
        /// </summary>
        private readonly struct AcceptanceRateAssessment
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="AcceptanceRateAssessment"/> struct.
            /// </summary>
            /// <param name="status">The diagnostic status.</param>
            /// <param name="direction">Whether the diagnostic is low, high, or neither.</param>
            /// <param name="message">The user-facing diagnostic message.</param>
            /// <param name="preferredRangeLabel">The preferred range label.</param>
            /// <param name="acceptableRangeLabel">The acceptable buffer label.</param>
            public AcceptanceRateAssessment(ReportDiagnosticStatus status, DiagnosticDirection direction,
                string message, string preferredRangeLabel, string acceptableRangeLabel)
            {
                Status = status;
                Direction = direction;
                Message = message;
                PreferredRangeLabel = preferredRangeLabel;
                AcceptableRangeLabel = acceptableRangeLabel;
            }

            /// <summary>
            /// Gets the diagnostic status.
            /// </summary>
            public ReportDiagnosticStatus Status { get; }

            /// <summary>
            /// Gets whether the acceptance rate is low, high, or neither.
            /// </summary>
            public DiagnosticDirection Direction { get; }

            /// <summary>
            /// Gets the user-facing diagnostic message.
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// Gets the preferred range label.
            /// </summary>
            public string PreferredRangeLabel { get; }

            /// <summary>
            /// Gets the acceptable buffer label.
            /// </summary>
            public string AcceptableRangeLabel { get; }
        }

        /// <summary>
        /// Contains an effective sample size diagnostic assessment.
        /// </summary>
        private readonly struct EffectiveSampleSizeAssessment
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="EffectiveSampleSizeAssessment"/> struct.
            /// </summary>
            /// <param name="status">The diagnostic status.</param>
            /// <param name="message">The user-facing diagnostic message.</param>
            /// <param name="effectiveSampleSize">The effective sample size.</param>
            /// <param name="retainedDrawCount">The number of retained posterior draws.</param>
            /// <param name="efficiency">The effective sample size divided by retained draws.</param>
            /// <param name="mcseInflation">The MCSE inflation relative to independent retained draws.</param>
            public EffectiveSampleSizeAssessment(ReportDiagnosticStatus status, string message,
                double effectiveSampleSize, int retainedDrawCount, double efficiency, double mcseInflation)
            {
                Status = status;
                Message = message;
                EffectiveSampleSize = effectiveSampleSize;
                RetainedDrawCount = retainedDrawCount;
                Efficiency = efficiency;
                McseInflation = mcseInflation;
            }

            /// <summary>
            /// Gets the diagnostic status.
            /// </summary>
            public ReportDiagnosticStatus Status { get; }

            /// <summary>
            /// Gets the user-facing diagnostic message.
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// Gets the effective sample size.
            /// </summary>
            public double EffectiveSampleSize { get; }

            /// <summary>
            /// Gets the number of retained posterior draws.
            /// </summary>
            public int RetainedDrawCount { get; }

            /// <summary>
            /// Gets the effective sample size divided by retained draws.
            /// </summary>
            public double Efficiency { get; }

            /// <summary>
            /// Gets the MCSE inflation relative to independent retained draws.
            /// </summary>
            public double McseInflation { get; }
        }

        /// <summary>
        /// Assesses an overall acceptance rate against sampler-specific preferred and acceptable ranges.
        /// </summary>
        /// <param name="rate">The overall acceptance rate.</param>
        /// <param name="type">The sampler type.</param>
        /// <returns>An acceptance-rate assessment for report display.</returns>
        private static AcceptanceRateAssessment AssessAcceptanceRate(double rate, SamplerType type)
        {
            var thresholds = GetAcceptanceRateThresholds(type);
            string preferred = $"{FormatPercentRange(thresholds.PreferredMin, thresholds.PreferredMax)} ({FormatSamplerType(type)})";
            string acceptable = $"{FormatPercentRange(thresholds.AcceptableMin, thresholds.AcceptableMax)} acceptable buffer";

            if (double.IsNaN(rate))
            {
                return new AcceptanceRateAssessment(ReportDiagnosticStatus.Warning, DiagnosticDirection.None,
                    "acceptance rate is unavailable", preferred, acceptable);
            }

            if (rate < thresholds.AcceptableMin)
            {
                return new AcceptanceRateAssessment(ReportDiagnosticStatus.Warning, DiagnosticDirection.Low,
                    "below acceptable buffer", preferred, acceptable);
            }

            if (rate > thresholds.AcceptableMax)
            {
                return new AcceptanceRateAssessment(ReportDiagnosticStatus.Warning, DiagnosticDirection.High,
                    "above acceptable buffer", preferred, acceptable);
            }

            if (rate < thresholds.PreferredMin)
            {
                return new AcceptanceRateAssessment(ReportDiagnosticStatus.Note, DiagnosticDirection.Low,
                    "below preferred range but within acceptable buffer", preferred, acceptable);
            }

            if (rate > thresholds.PreferredMax)
            {
                return new AcceptanceRateAssessment(ReportDiagnosticStatus.Note, DiagnosticDirection.High,
                    "above preferred range but within acceptable buffer", preferred, acceptable);
            }

            return new AcceptanceRateAssessment(ReportDiagnosticStatus.OK, DiagnosticDirection.None,
                "within preferred range", preferred, acceptable);
        }

        /// <summary>
        /// Gets the retained draw count used to judge effective sample size efficiency.
        /// </summary>
        /// <param name="results">The MCMC results.</param>
        /// <param name="configuredOutputLength">The configured output length.</param>
        /// <returns>The retained posterior draw count.</returns>
        private static int GetRetainedDrawCount(MCMCResults results, int configuredOutputLength)
        {
            if (results.Output != null && results.Output.Count > 0)
                return results.Output.Count;
            return Math.Max(0, configuredOutputLength);
        }

        /// <summary>
        /// Assesses effective sample size against absolute and retained-draw-relative thresholds.
        /// </summary>
        /// <param name="ess">The minimum effective sample size.</param>
        /// <param name="retainedDrawCount">The retained posterior draw count.</param>
        /// <returns>An ESS assessment for report display.</returns>
        private static EffectiveSampleSizeAssessment AssessEffectiveSampleSize(double ess, int retainedDrawCount)
        {
            double efficiency = retainedDrawCount > 0 && !double.IsNaN(ess) ? ess / retainedDrawCount : double.NaN;
            double mcseInflation = retainedDrawCount > 0 && ess > 0.0 ? Math.Sqrt(retainedDrawCount / ess) : double.NaN;

            if (double.IsNaN(ess))
            {
                return new EffectiveSampleSizeAssessment(ReportDiagnosticStatus.Warning,
                    "effective sample size is unavailable", ess, retainedDrawCount, efficiency, mcseInflation);
            }

            if (ess <= EssSevereFloor)
            {
                return new EffectiveSampleSizeAssessment(ReportDiagnosticStatus.Warning,
                    "severe Monte Carlo precision warning", ess, retainedDrawCount, efficiency, mcseInflation);
            }

            if (ess < EssDiagnosticFloor)
            {
                return new EffectiveSampleSizeAssessment(ReportDiagnosticStatus.Warning,
                    "below 400 diagnostic floor", ess, retainedDrawCount, efficiency, mcseInflation);
            }

            if (!double.IsNaN(efficiency) && efficiency < EssWarningEfficiency)
            {
                return new EffectiveSampleSizeAssessment(ReportDiagnosticStatus.Warning,
                    "low efficiency relative to retained draw count", ess, retainedDrawCount, efficiency, mcseInflation);
            }

            if (!double.IsNaN(efficiency) && efficiency < EssPreferredEfficiency)
            {
                return new EffectiveSampleSizeAssessment(ReportDiagnosticStatus.Note,
                    "above diagnostic floor but below preferred efficiency", ess, retainedDrawCount, efficiency, mcseInflation);
            }

            return new EffectiveSampleSizeAssessment(ReportDiagnosticStatus.OK,
                "meets preferred effective-sample efficiency", ess, retainedDrawCount, efficiency, mcseInflation);
        }

        /// <summary>
        /// Formats an ESS assessment summary.
        /// </summary>
        /// <param name="assessment">The ESS assessment.</param>
        /// <returns>A formatted ESS summary string.</returns>
        private static string FormatEssSummary(EffectiveSampleSizeAssessment assessment)
        {
            if (assessment.RetainedDrawCount <= 0 || double.IsNaN(assessment.Efficiency))
                return $"{assessment.EffectiveSampleSize:N0} retained-draw baseline unavailable    {GetStatusLabel(assessment.Status)}";

            string inflation = double.IsNaN(assessment.McseInflation)
                ? "MCSE inflation unavailable"
                : $"MCSE {assessment.McseInflation:F1}x independent draws";

            return $"{assessment.EffectiveSampleSize:N0} of {assessment.RetainedDrawCount:N0} retained draws " +
                   $"({assessment.Efficiency * 100.0:F1}% efficiency; {inflation})    {GetStatusLabel(assessment.Status)}";
        }

        /// <summary>
        /// Appends warnings for chains whose acceptance rate is outside the acceptable buffer.
        /// </summary>
        /// <param name="sb">The report builder.</param>
        /// <param name="acceptanceRates">The per-chain acceptance rates.</param>
        /// <param name="type">The sampler type.</param>
        /// <returns><c>true</c> if any chain-level warning was appended; otherwise, <c>false</c>.</returns>
        private static bool AppendChainAcceptanceWarnings(StringBuilder sb, double[] acceptanceRates, SamplerType type)
        {
            var thresholds = GetAcceptanceRateThresholds(type);
            bool hasWarnings = false;
            string bufferLabel = FormatPercentRange(thresholds.AcceptableMin, thresholds.AcceptableMax);
            string statisticLabel = type == SamplerType.NUTS
                ? "Hamiltonian acceptance"
                : "acceptance";

            for (int i = 0; i < acceptanceRates.Length; i++)
            {
                double rate = acceptanceRates[i];
                if (rate < thresholds.AcceptableMin || rate > thresholds.AcceptableMax)
                {
                    if (!hasWarnings)
                    {
                        sb.AppendLine();
                        sb.AppendLine("  Chain-level warnings:");
                    }

                    string direction = rate < thresholds.AcceptableMin ? "below" : "above";
                    sb.AppendLine($"    - Chain {i + 1} {statisticLabel} {rate * 100.0:F1}% is {direction} acceptable buffer {bufferLabel}.");
                    hasWarnings = true;
                }
            }

            return hasWarnings;
        }

        /// <summary>
        /// Gets sampler-specific preferred and acceptable acceptance-rate thresholds.
        /// </summary>
        /// <param name="type">The sampler type.</param>
        /// <returns>The preferred and acceptable acceptance-rate thresholds.</returns>
        private static AcceptanceRateThresholds GetAcceptanceRateThresholds(SamplerType type)
        {
            return type switch
            {
                SamplerType.DEMCz => new AcceptanceRateThresholds(0.23, 0.44, 0.15, 0.50),
                SamplerType.DEMCzs => new AcceptanceRateThresholds(0.23, 0.44, 0.15, 0.50),
                SamplerType.ARWMH => new AcceptanceRateThresholds(0.20, 0.30, 0.15, 0.40),
                SamplerType.NUTS => new AcceptanceRateThresholds(0.65, 0.90, 0.50, 0.95),
                _ => new AcceptanceRateThresholds(0.0, 1.0, 0.0, 1.0)
            };
        }

        /// <summary>
        /// Formats a status label for report output.
        /// </summary>
        /// <param name="status">The diagnostic status.</param>
        /// <returns>A user-facing status label.</returns>
        private static string GetStatusLabel(ReportDiagnosticStatus status)
        {
            return status switch
            {
                ReportDiagnosticStatus.OK => "OK",
                ReportDiagnosticStatus.Note => "NOTE",
                ReportDiagnosticStatus.Warning => "WARNING",
                _ => "UNKNOWN"
            };
        }

        /// <summary>
        /// Formats a rate interval as a percentage range.
        /// </summary>
        /// <param name="minimum">The minimum rate.</param>
        /// <param name="maximum">The maximum rate.</param>
        /// <returns>A formatted percentage range.</returns>
        private static string FormatPercentRange(double minimum, double maximum)
        {
            return $"{minimum * 100.0:F0}-{maximum * 100.0:F0}%";
        }

        /// <summary>
        /// Appends low-acceptance tuning advice for the specified sampler.
        /// </summary>
        /// <param name="sb">The report builder.</param>
        /// <param name="type">The sampler type.</param>
        private static void AppendLowAcceptanceTuningAdvice(StringBuilder sb, SamplerType type)
        {
            if (type == SamplerType.DEMCz || type == SamplerType.DEMCzs)
                sb.AppendLine("    - Reduce the Jump parameter to make smaller differential-evolution proposals.");
            else if (type == SamplerType.ARWMH)
                sb.AppendLine("    - Decrease the Scale parameter to make smaller adaptive random-walk proposals.");
            else if (type == SamplerType.NUTS)
                sb.AppendLine("    - Review posterior geometry; low Hamiltonian acceptance often accompanies inaccurate trajectories.");

            sb.AppendLine("    - Check that priors and parameter bounds are consistent with the data.");
            sb.AppendLine("    - Review highly correlated parameters or poorly identified model structure.");
        }

        /// <summary>
        /// Appends high-acceptance tuning advice for the specified sampler.
        /// </summary>
        /// <param name="sb">The report builder.</param>
        /// <param name="type">The sampler type.</param>
        private static void AppendHighAcceptanceTuningAdvice(StringBuilder sb, SamplerType type)
        {
            if (type == SamplerType.DEMCz || type == SamplerType.DEMCzs)
                sb.AppendLine("    - Increase the Jump parameter so proposals explore the posterior more efficiently.");
            else if (type == SamplerType.ARWMH)
                sb.AppendLine("    - Increase the Scale parameter to make larger adaptive random-walk proposals.");
            else if (type == SamplerType.NUTS)
                sb.AppendLine("    - Review ESS and autocorrelation for inefficient trajectories.");

            sb.AppendLine("    - The chain may be exploring the posterior too slowly.");
            sb.AppendLine("    - Prefer improving proposal efficiency before increasing thinning.");
        }

        /// <summary>
        /// Appends sampler-specific advice for improving ESS efficiency.
        /// </summary>
        /// <param name="sb">The report builder.</param>
        /// <param name="type">The sampler type.</param>
        private static void AppendSamplerEfficiencyAdvice(StringBuilder sb, SamplerType type)
        {
            if (type == SamplerType.DEMCz || type == SamplerType.DEMCzs)
                sb.AppendLine("    - For DE-MC samplers, tune Jump and review correlated parameters before simply drawing more samples.");
            else if (type == SamplerType.ARWMH)
                sb.AppendLine("    - For ARWMH, tune Scale and consider DEMCzs for strongly correlated parameters.");
            else if (type == SamplerType.NUTS)
                sb.AppendLine("    - For NUTS, inspect trace/autocorrelation plots and consider model reparameterization if ESS remains low.");
            else
                sb.AppendLine("    - Consider sampler tuning, sampler changes, or reparameterization if ESS remains low.");
        }

        /// <summary>
        /// Formats the sampler type enum value into a human-readable string.
        /// </summary>
        private static string FormatSamplerType(SamplerType type)
        {
            return type switch
            {
                SamplerType.DEMCz => "DE-MCz",
                SamplerType.DEMCzs => "DE-MCzs",
                SamplerType.ARWMH => "Adaptive Random Walk Metropolis-Hastings",
                SamplerType.NUTS => "No-U-Turn Sampler (NUTS)",
                _ => type.ToString()
            };
        }

        #endregion

        /// <summary>
        /// Clones this Bayesian analysis, including its configuration and results.
        /// </summary>
        /// <remarks>
        /// The <see cref="Model"/> reference is shared by design (consistent with
        /// the rest of the project � the model is the single source of truth and
        /// is not deep-copied). <see cref="Results"/> (the <c>MCMCResults</c>
        /// containing posterior samples) is also shared by reference. Callers who
        /// intend to re-fit the clone should call <c>ClearResults()</c> on it
        /// first; that null's out the shared reference safely.
        /// </remarks>
        public BayesianAnalysis Clone()
        {
            var bayes = new BayesianAnalysis(Model, ToXElement(), Results);
            return bayes;
        }

        /// <summary>
        /// Serializes the Bayesian analysis configuration and state to an <see cref="XElement"/>.
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(BayesianAnalysis));
            result.SetAttributeValue(nameof(Type), Type.ToString());
            result.SetAttributeValue(nameof(NumberOfChains), NumberOfChains.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(ThinningInterval), ThinningInterval.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(WarmupIterations), WarmupIterations.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Iterations), Iterations.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(UseSimulationDefaults), UseSimulationDefaults.ToString());
            result.SetAttributeValue(nameof(PRNGSeed), PRNGSeed.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(InitialIterations), InitialIterations.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Jump), Jump.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(JumpThreshold), JumpThreshold.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(SnookerThreshold), SnookerThreshold.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Noise), Noise.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Scale), Scale.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Beta), Beta.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(MaxTreeDepth), MaxTreeDepth.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(UseAdvancedSimulationDefaults), UseAdvancedSimulationDefaults.ToString());
            result.SetAttributeValue(nameof(CredibleIntervalWidth), CredibleIntervalWidth.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(OutputLength), OutputLength.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(PointEstimator), PointEstimator.ToString());
            result.SetAttributeValue(nameof(DIC), DIC.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(WAIC), WAIC.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(WAIC_pD), WAIC_pD.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(LOOIC), LOOIC.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(LOO_pD), LOO_pD.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(LOOIC_SE), LOOIC_SE.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(IsEstimated), IsEstimated.ToString());
            if (ElapsedTime.HasValue)
                result.SetAttributeValue(nameof(ElapsedTime), ElapsedTime.Value.Ticks.ToString(CultureInfo.InvariantCulture));
            return result;
        }

        #endregion
    }
}

