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
                // line ~217) default to PosteriorMean — the asymmetry is intentional.
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
        private TimeSpan? _elapsedTime = null;
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
        public MCMCResults? Results { get; private set; }

        /// <summary>
        /// The exception captured by the most recent <see cref="RunAsync"/> call,
        /// or <c>null</c> if the run completed successfully or has not yet been
        /// started. Cleared at the start of every new <c>RunAsync</c>.
        /// </summary>
        /// <remarks>
        /// Pairs with <see cref="IsEstimated"/>: when <c>IsEstimated == false</c>
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
        /// Gets the Watanabe-Akaike Information Criterion (WAIC) computed from the MCMC results.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WAIC is computed as -2 × lppd + 2 × p_WAIC, where lppd is the log pointwise predictive density
        /// and p_WAIC is the effective number of parameters.
        /// </para>
        /// <para>
        /// This implementation uses total data log-likelihoods as an approximation. The lppd is approximated
        /// by the mean total log-likelihood across posterior samples, and p_WAIC is approximated by the
        /// variance of total log-likelihoods. This provides a computationally efficient criterion for model
        /// comparison that accounts for posterior uncertainty.
        /// </para>
        /// </remarks>
        public double WAIC { get; private set; }

        /// <summary>
        /// Gets the effective number of parameters (p_WAIC) as computed by the WAIC criterion.
        /// </summary>
        /// <remarks>
        /// This value represents the penalty term in WAIC, computed as the variance of total log-likelihoods
        /// across posterior samples. Larger values indicate more complex models with greater effective
        /// parameter counts.
        /// </remarks>
        public double WAIC_pD { get; private set; }

        /// <summary>
        /// Gets the Leave-One-Out Information Criterion (LOOIC) computed using Pareto Smoothed Importance Sampling.
        /// </summary>
        /// <remarks>
        /// <para>
        /// LOOIC is computed as -2 × elpd_loo, where elpd_loo is the expected log pointwise predictive
        /// density for a new dataset estimated using leave-one-out cross-validation.
        /// </para>
        /// <para>
        /// PSIS-LOO uses importance sampling with Pareto smoothing to efficiently approximate exact LOO-CV
        /// without refitting the model. The Pareto k diagnostic values indicate the reliability of the estimates.
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
        /// Gets the Pareto k diagnostic values for each observation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The Pareto k value indicates the reliability of the importance sampling estimate for each observation:
        /// </para>
        /// <list type="bullet">
        /// <item><description>k &lt; 0.5: Very good, estimates are reliable</description></item>
        /// <item><description>0.5 ≤ k &lt; 0.7: Good, estimates are reasonably reliable</description></item>
        /// <item><description>0.7 ≤ k &lt; 1.0: Problematic, estimates may be biased</description></item>
        /// <item><description>k ≥ 1.0: Very bad, estimates are unreliable</description></item>
        /// </list>
        /// <para>
        /// If many observations have k ≥ 0.7, consider using exact LOO-CV for those points or WAIC instead.
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
        [Description("Specifies the jump parameter (γ) that enables the simulation to move between modes in the target distribution. A recommended setting is γ = 2.38/√(2d), where d is the number of model parameters.")]
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
        [Description("Specifies the probability at which the jump parameter (γ) is set to 1.0. For example, a value of 0.10 results in a large jump 10 percent of the time.")]
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
                    // independent of alpha — preserve Results and recompute summaries in place.
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
        /// Raises the <see cref="PropertyChanged"/> event for a given property name.
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
                // Vehtari et al. (2021) recommend ≥4 chains for reliable split-Rhat
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

            // Get the prior distributions
            var priors = Model.Parameters.Select(x => (IUnivariateDistribution)x.PriorDistribution.Clone()).ToList();

            // Set up the MCMC sampler
            if (Type == SamplerType.DEMCz)
            {
                Sampler = new DEMCz(priors, x => Model.LogLikelihood(x))
                {
                    Jump = Jump,
                    JumpThreshold = JumpThreshold,
                    Noise = Noise
                };
            }
            else if (Type == SamplerType.DEMCzs)
            {
                Sampler = new DEMCzs(priors, x => Model.LogLikelihood(x))
                {
                    Jump = Jump,
                    JumpThreshold = JumpThreshold,
                    SnookerThreshold = SnookerThreshold,
                    Noise = Noise
                };
            }
            else if (Type == SamplerType.ARWMH)
            {
                Sampler = new ARWMH(priors, x => Model.LogLikelihood(x))
                {
                    Scale = Scale,
                    Beta = Beta
                };
            }
            else if (Type == SamplerType.NUTS)
            {
                Sampler = new NUTS(priors, x => Model.LogLikelihood(x), maxTreeDepth: MaxTreeDepth);
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
                // correct thread. ComputeDIC / WAIC / PSISLOO read this.Results — assign Results
                // first so they see the new chains, and they internally use Parallel.For which
                // dispatches its own worker threads (no dispatcher block on the math itself).
                ElapsedTime = capturedElapsed;
                if (capturedResults != null)
                {
                    Results = capturedResults;
                    ComputeDIC();
                    ComputeWAIC();
                    ComputePSISLOO();
                }

                if (sampler.CancellationTokenSource!.IsCancellationRequested == false)
                {
                    IsEstimated = true;
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is normal — re-throw so wrapper analyses' OperationCanceledException
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
        /// One-shot per <see cref="RunAsync(SafeProgressReporter?, bool)"/> invocation —
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
                sum += -2.0 * Model.DataLogLikelihood(Results.Output[j].Values);
                return sum;
            }, z => Tools.ParallelAdd(ref dicHat, z));

            dicHat /= N;
            double dicMu = -2.0 * Model.DataLogLikelihood(Results.PosteriorMean.Values);
            DIC = 2.0 * dicHat - dicMu;
        }

        /// <summary>
        /// Computes the Watanabe-Akaike Information Criterion (WAIC) from the current MCMC results.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WAIC is computed as -2 × lppd + 2 × p_WAIC using the correct pointwise formulation:
        /// </para>
        /// <para>
        /// <b>lppd</b> (log pointwise predictive density):
        /// lppd = Σᵢ log(1/S Σₛ p(yᵢ|θˢ))
        /// </para>
        /// <para>
        /// <b>p_WAIC</b> (effective number of parameters):
        /// p_WAIC = Σᵢ Varₛ[log p(yᵢ|θˢ)]
        /// </para>
        /// <para>
        /// This implementation uses the log-sum-exp trick for numerical stability when computing lppd.
        /// </para>
        /// <para>
        /// <b>References:</b>
        /// Watanabe, S. (2010). Asymptotic equivalence of Bayes cross validation and widely applicable
        /// information criterion in singular learning theory. Journal of Machine Learning Research, 11, 3571-3594.
        /// </para>
        /// </remarks>
        private void ComputeWAIC()
        {
            if (Results == null || Results.Output == null || Results.Output.Count == 0 || Model == null)
            {
                WAIC = double.NaN;
                WAIC_pD = double.NaN;
                return;
            }

            int S = Results.Output.Count;  // Number of posterior samples

            // Get the number of observations from first sample
            double[] firstPointwise = Model.PointwiseDataLogLikelihood(Results.Output[0].Values);
            int n = firstPointwise.Length;  // Number of observations

            if (n == 0)
            {
                WAIC = double.NaN;
                WAIC_pD = double.NaN;
                return;
            }

            // Allocate matrix for pointwise log-likelihoods: logLik[obs, sample]
            var pointwiseLogLik = new double[n, S];

            // Copy first sample
            for (int i = 0; i < n; i++)
                pointwiseLogLik[i, 0] = firstPointwise[i];

            // Compute pointwise log-likelihoods for remaining samples in parallel
            Parallel.For(1, S, s =>
            {
                double[] logLiks = Model.PointwiseDataLogLikelihood(Results.Output[s].Values);
                for (int i = 0; i < n; i++)
                    pointwiseLogLik[i, s] = logLiks[i];
            });

            // Compute lppd and p_WAIC in parallel over observations
            double totalLppd = 0.0;
            double totalPWaic = 0.0;

            Parallel.For(0, n,
                () => (lppd: 0.0, pWaic: 0.0),
                (i, loop, local) =>
                {
                    // Extract log-likelihoods for observation i across all samples
                    // Find max for log-sum-exp stability. Use NegativeInfinity (not MinValue)
                    // per CLAUDE.md "Numerical patterns" — when every per-sample LL is -Inf
                    // (a fully invalid posterior sample for this obs), the log-sum-exp must
                    // collapse to -Inf, not MinValue.
                    double maxLogLik = double.NegativeInfinity;
                    double sumLogLik = 0.0;
                    double sumLogLikSq = 0.0;

                    for (int s = 0; s < S; s++)
                    {
                        double ll = pointwiseLogLik[i, s];
                        sumLogLik += ll;
                        sumLogLikSq += ll * ll;
                        if (ll > maxLogLik) maxLogLik = ll;
                    }

                    // lppd_i = log(1/S Σₛ exp(logLik_is)) using log-sum-exp trick
                    // = log(1/S) + max + log(Σₛ exp(logLik_is - max))
                    double sumExp = 0.0;
                    for (int s = 0; s < S; s++)
                    {
                        sumExp += Math.Exp(pointwiseLogLik[i, s] - maxLogLik);
                    }
                    double lppd_i = maxLogLik + Math.Log(sumExp) - Math.Log(S);

                    // p_WAIC_i = Var_s[logLik_is] using the unbiased sample-variance
                    // estimator (divisor S-1) per Vehtari, Gelman & Gabry (2017) Eq. 12.
                    double meanLogLik = sumLogLik / S;
                    double pWaic_i = S > 1
                        ? (sumLogLikSq - S * meanLogLik * meanLogLik) / (S - 1)
                        : 0.0;

                    // Ensure non-negative variance (can be slightly negative due to floating point)
                    if (pWaic_i < 0.0) pWaic_i = 0.0;

                    local.lppd += lppd_i;
                    local.pWaic += pWaic_i;
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
        /// Computes the Leave-One-Out Information Criterion using Pareto Smoothed Importance Sampling.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PSIS-LOO approximates exact leave-one-out cross-validation using importance sampling.
        /// The importance weights are stabilized using Pareto smoothing, which fits a generalized
        /// Pareto distribution to the tail of the weight distribution and replaces extreme weights
        /// with smoothed values.
        /// </para>
        /// <para>
        /// <b>References:</b>
        /// Vehtari, A., Gelman, A., and Gabry, J. (2017). Practical Bayesian model evaluation using
        /// leave-one-out cross-validation and WAIC. Statistics and Computing, 27(5), 1413-1432.
        /// </para>
        /// </remarks>
        private void ComputePSISLOO()
        {
            if (Results == null || Results.Output == null || Results.Output.Count == 0 || Model == null)
            {
                LOOIC = double.NaN;
                LOO_pD = double.NaN;
                LOOIC_SE = double.NaN;
                ParetoK = null;
                return;
            }

            int S = Results.Output.Count;  // Number of posterior samples

            // Get the number of observations from first sample
            double[] firstPointwise = Model.PointwiseDataLogLikelihood(Results.Output[0].Values);
            int n = firstPointwise.Length;  // Number of observations

            if (n == 0)
            {
                LOOIC = double.NaN;
                LOO_pD = double.NaN;
                LOOIC_SE = double.NaN;
                ParetoK = null;
                return;
            }

            // Allocate matrix for pointwise log-likelihoods: logLik[obs, sample]
            var pointwiseLogLik = new double[n, S];

            // Copy first sample
            for (int i = 0; i < n; i++)
                pointwiseLogLik[i, 0] = firstPointwise[i];

            // Compute pointwise log-likelihoods for remaining samples in parallel
            Parallel.For(1, S, s =>
            {
                double[] logLiks = Model.PointwiseDataLogLikelihood(Results.Output[s].Values);
                for (int i = 0; i < n; i++)
                    pointwiseLogLik[i, s] = logLiks[i];
            });

            // Compute PSIS-LOO for each observation
            ParetoK = new double[n];
            var elpdLoo = new double[n];
            var lppd = new double[n];

            // Number of tail samples for Pareto fitting per Vehtari et al. (2017):
            // M = min(S/5, 3*sqrt(S)). For very small S the formula can drop below 3,
            // so enforce an absolute floor of 3 samples (minimum needed for GPD fit).
            // Drop the legacy floor-of-10 once the Vehtari formula gives a usable
            // value, so the tail size scales naturally with S.
            int M = (int)Math.Min(S / 5.0, 3.0 * Math.Sqrt(S));
            M = Math.Max(M, 3);
            M = Math.Min(M, S - 1);

            Parallel.For(0, n, i =>
            {
                // Extract log-likelihoods for observation i
                var logLiks = new double[S];
                for (int s = 0; s < S; s++)
                    logLiks[s] = pointwiseLogLik[i, s];

                // Compute log importance weights: log r_is = -logLik_is
                // (We want 1/p(y_i|θ^s), and log(1/x) = -log(x))
                var logWeights = new double[S];
                for (int s = 0; s < S; s++)
                    logWeights[s] = -logLiks[s];

                // Normalize log weights to prevent overflow: shift by max
                double maxLogWeight = logWeights[0];
                for (int s = 1; s < S; s++)
                    if (logWeights[s] > maxLogWeight) maxLogWeight = logWeights[s];

                var shiftedLogWeights = new double[S];
                for (int s = 0; s < S; s++)
                    shiftedLogWeights[s] = logWeights[s] - maxLogWeight;

                // Apply Pareto smoothing to the tail
                double paretoK = ParetoSmoothWeights(shiftedLogWeights, M);
                ParetoK[i] = paretoK;

                // Convert back to regular weights (still shifted)
                var weights = new double[S];
                for (int s = 0; s < S; s++)
                    weights[s] = Math.Exp(shiftedLogWeights[s]);

                // Normalize weights to sum to 1
                double sumWeights = 0.0;
                for (int s = 0; s < S; s++)
                    sumWeights += weights[s];

                for (int s = 0; s < S; s++)
                    weights[s] /= sumWeights;

                // Compute LOO predictive density using normalized weights
                // elpd_loo_i = log(Σ_s w_is * exp(logLik_is))
                // Using log-sum-exp trick for stability
                double maxLL = logLiks[0];
                for (int s = 1; s < S; s++)
                    if (logLiks[s] > maxLL) maxLL = logLiks[s];

                double sumWeightedExp = 0.0;
                for (int s = 0; s < S; s++)
                    sumWeightedExp += weights[s] * Math.Exp(logLiks[s] - maxLL);

                // Guard against log(0) when sumWeightedExp is zero or negative
                elpdLoo[i] = sumWeightedExp > 0 ? maxLL + Math.Log(sumWeightedExp) : double.NegativeInfinity;

                // Compute lppd_i for p_LOO calculation (same as WAIC)
                double sumExp = 0.0;
                for (int s = 0; s < S; s++)
                    sumExp += Math.Exp(logLiks[s] - maxLL);
                lppd[i] = maxLL + Math.Log(sumExp) - Math.Log(S);
            });

            // Sum up elpd_loo
            double totalElpdLoo = 0.0;
            double totalLppd = 0.0;
            for (int i = 0; i < n; i++)
            {
                totalElpdLoo += elpdLoo[i];
                totalLppd += lppd[i];
            }

            // Compute standard error using the variance of pointwise elpd_loo
            double meanElpdLoo = totalElpdLoo / n;
            double variance = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = elpdLoo[i] - meanElpdLoo;
                variance += diff * diff;
            }
            variance /= (n - 1);  // Sample variance
            double seElpdLoo = Math.Sqrt(n * variance);  // SE of sum = sqrt(n) * SD

            LOOIC = -2.0 * totalElpdLoo;
            LOO_pD = totalLppd - totalElpdLoo;
            LOOIC_SE = 2.0 * seElpdLoo;  // SE of -2*elpd_loo = 2*SE(elpd_loo)
        }

        /// <summary>
        /// Applies Pareto smoothing to the tail of log importance weights.
        /// </summary>
        /// <param name="logWeights">The log importance weights (modified in place for tail values).</param>
        /// <param name="M">The number of tail samples to smooth.</param>
        /// <returns>The estimated Pareto shape parameter k.</returns>
        private static double ParetoSmoothWeights(double[] logWeights, int M)
        {
            int S = logWeights.Length;

            // Get indices sorted by weight (descending order)
            var indices = Enumerable.Range(0, S).OrderByDescending(s => logWeights[s]).ToArray();

            // Extract the M largest log weights (tail)
            var tailLogWeights = new double[M];
            for (int j = 0; j < M; j++)
                tailLogWeights[j] = logWeights[indices[j]];

            // Find the cutoff (smallest tail weight)
            double cutoff = tailLogWeights[M - 1];

            // Shift tail weights so minimum is 0
            for (int j = 0; j < M; j++)
                tailLogWeights[j] -= cutoff;

            // Convert to linear scale for Pareto fitting
            var tailWeights = new double[M];
            for (int j = 0; j < M; j++)
                tailWeights[j] = Math.Exp(tailLogWeights[j]);

            // Fit Generalized Pareto Distribution by maximum likelihood using the
            // Numerics distribution. This matches the canonical PSIS reference fit
            // (Vehtari, Gelman & Gabry 2017) more closely than method-of-moments.
            //
            // Numerics uses Hosking's parameterization where the shape Kappa has the
            // OPPOSITE sign of the PSIS k convention. If GPD CDF is
            //   F(x) = 1 - (1 + k_psis · x/σ)^(-1/k_psis)
            // then Numerics Kappa = -k_psis. We flip the sign on the way out so the
            // downstream smoothing formulas (which use the PSIS k convention) are
            // unchanged.
            double k;
            double sigma;
            try
            {
                var gpdMle = new Numerics.Distributions.GeneralizedPareto();
                var mleParams = gpdMle.MLE(tailWeights);
                // mleParams: [xi (location, fixed at min), alpha (scale), kappa (Hosking shape)].
                sigma = mleParams[1];
                k = -mleParams[2];  // Convert Hosking κ → PSIS k.
            }
            catch (Exception ex)
            {
                // MLE failed (rare — happens when the tail is degenerate).
                // Fall back to method-of-moments.
                Debug.WriteLine($"BayesianAnalysis.FitGPD: MLE failed, falling back to MOM: {ex.Message}");
                double mean = 0.0;
                for (int j = 0; j < M; j++) mean += tailWeights[j];
                mean /= M;
                double variance = 0.0;
                for (int j = 0; j < M; j++)
                {
                    double diff = tailWeights[j] - mean;
                    variance += diff * diff;
                }
                variance /= (M - 1);
                if (variance > 0 && mean > 0)
                {
                    double cv2 = variance / (mean * mean);
                    k = 0.5 * (cv2 - 1.0) / (cv2 + 1.0);
                    sigma = mean * (1.0 - k);
                    if (sigma <= 0) sigma = mean;
                }
                else
                {
                    k = 0.0;
                    sigma = mean > 0 ? mean : 1.0;
                }
            }

            // Clamp k to a reasonable range and guard sigma > 0.
            k = Math.Max(-0.5, Math.Min(k, 1.5));
            if (sigma <= 0) sigma = double.Epsilon;

            // If k is reasonable, smooth the tail weights
            if (k < 1.0 && k > -0.5)
            {

                // Replace tail weights with expected order statistics from fitted GPD
                // F(x) = 1 - (1 + k*x/σ)^(-1/k) for k ≠ 0
                // Quantile: Q(p) = σ/k * ((1-p)^(-k) - 1) for k ≠ 0
                // Expected order statistic at rank j of M: p_j = (j - 0.5)/M
                for (int j = 0; j < M; j++)
                {
                    double p = (j + 0.5) / M;  // Probability for j-th order statistic (0 = smallest)
                    double quantile;

                    if (Math.Abs(k) < 1e-8)
                    {
                        // k ≈ 0: Exponential distribution, Q(p) = -σ * log(1-p)
                        quantile = -sigma * Math.Log(1.0 - p);
                    }
                    else
                    {
                        // General GPD quantile
                        quantile = sigma / k * (Math.Pow(1.0 - p, -k) - 1.0);
                    }

                    // Ensure quantile is non-negative
                    quantile = Math.Max(0.0, quantile);

                    // Convert back to log scale and un-shift
                    // Use small epsilon to avoid log(0), quantile is guaranteed non-negative by Math.Max above
                    double smoothedLogWeight = quantile > 0 ? Math.Log(quantile) + cutoff : cutoff - 300 * Math.Log(10);

                    // Update the weight at this tail position
                    logWeights[indices[j]] = smoothedLogWeight;
                }
            }

            return k;
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
            if (!skipInformationCriteria)
            {
                ComputeDIC();
                ComputeWAIC();
                ComputePSISLOO();
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
        /// This method requires that MCMC estimation has been completed (i.e., <see cref="Estimate"/>
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

            int n = ParetoK.Length;

            // Compute ELPD-LOO for each observation (recompute to get individual values)
            var elpdLoo = ComputePointwiseElpdLoo();

            // Get data components for metadata
            List<DataComponent>? dataComponents = null;
            try
            {
                var firstParams = Results.Output[0].Values;
                dataComponents = Model.PointwiseDataLogLikelihoodComponents(firstParams);
            }
            catch (Exception ex)
            {
                // Fall back to no metadata if method not implemented or fails.
                Debug.WriteLine($"BayesianAnalysis.GetInfluenceDiagnostics: PointwiseDataLogLikelihoodComponents unavailable: {ex.Message}");
            }

            return new InfluenceDiagnostics(ParetoK, elpdLoo, dataComponents);
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
        /// leverage values that decompose the total information at the MAP.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The posterior Hessian is computed numerically via central differences on <see cref="IModel.LogLikelihood"/>
        /// at the MCMC MAP values. No optimization step is performed. The MAP values come directly from the
        /// MCMC posterior samples (<see cref="Numerics.Sampling.MCMC.MCMCResults.MAP"/>).
        /// </para>
        /// <para>
        /// This is the Bayesian analogue of Cook's distance. The leverage of each component (observation or prior)
        /// measures its share of the total information: ℓᵢ = gᵢᵀ H⁻¹ gᵢ. All leverages sum approximately
        /// to p (the number of parameters), providing a unified ranking across observations and priors.
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
        /// Computes pointwise ELPD-LOO values for each observation.
        /// </summary>
        /// <returns>Array of ELPD-LOO values, one per observation.</returns>
        private double[] ComputePointwiseElpdLoo()
        {
            if (Results == null || Results.Output == null || Results.Output.Count == 0 || Model == null)
                return Array.Empty<double>();

            int S = Results.Output.Count;
            double[] firstPointwise = Model.PointwiseDataLogLikelihood(Results.Output[0].Values);
            int n = firstPointwise.Length;

            if (n == 0) return Array.Empty<double>();

            // Allocate matrix for pointwise log-likelihoods
            var pointwiseLogLik = new double[n, S];

            for (int i = 0; i < n; i++)
                pointwiseLogLik[i, 0] = firstPointwise[i];

            Parallel.For(1, S, s =>
            {
                double[] logLiks = Model.PointwiseDataLogLikelihood(Results.Output[s].Values);
                for (int i = 0; i < n; i++)
                    pointwiseLogLik[i, s] = logLiks[i];
            });

            // PSIS tail size — Vehtari et al. (2017). Match the floor used in
            // ComputePSISLOO so per-observation diagnostics agree across calls.
            int M = (int)Math.Min(S / 5.0, 3.0 * Math.Sqrt(S));
            M = Math.Max(M, 3);
            M = Math.Min(M, S - 1);

            var elpdLoo = new double[n];

            Parallel.For(0, n, i =>
            {
                var logLiks = new double[S];
                for (int s = 0; s < S; s++)
                    logLiks[s] = pointwiseLogLik[i, s];

                var logWeights = new double[S];
                for (int s = 0; s < S; s++)
                    logWeights[s] = -logLiks[s];

                double maxLogWeight = logWeights.Max();
                var shiftedLogWeights = new double[S];
                for (int s = 0; s < S; s++)
                    shiftedLogWeights[s] = logWeights[s] - maxLogWeight;

                ParetoSmoothWeights(shiftedLogWeights, M);

                var weights = new double[S];
                for (int s = 0; s < S; s++)
                    weights[s] = Math.Exp(shiftedLogWeights[s]);

                double sumWeights = weights.Sum();
                for (int s = 0; s < S; s++)
                    weights[s] /= sumWeights;

                double maxLL = logLiks.Max();
                double sumWeightedExp = 0.0;
                for (int s = 0; s < S; s++)
                    sumWeightedExp += weights[s] * Math.Exp(logLiks[s] - maxLL);

                elpdLoo[i] = maxLL + Math.Log(sumWeightedExp);
            });

            return elpdLoo;
        }

        /// <summary>
        /// Computes the posterior covariance matrix from the MCMC output samples
        /// using <see cref="RunningCovarianceMatrix"/>.
        /// </summary>
        /// <returns>
        /// A p x p sample covariance matrix where p is the number of model parameters,
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

            int p = Model.NumberOfParameters;
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
        /// A p x p sample correlation matrix where p is the number of model parameters,
        /// or null if estimation has not been completed.
        /// </returns>
        /// <remarks>
        /// Derived from the sample covariance matrix: Corr[i,j] = Cov[i,j] / (SD[i] * SD[j]).
        /// </remarks>
        public double[,]? GetPosteriorCorrelationMatrix()
        {
            if (!IsEstimated || Results == null || Results.Output == null || Results.Output.Count < 2 || Model == null)
                return null;

            int p = Model.NumberOfParameters;
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
            int p = Model.NumberOfParameters;

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

            // Section 3: Acceptance Rates
            double overallAcceptance = double.NaN;
            bool acceptanceGood = true;
            if (Results.AcceptanceRates != null && Results.AcceptanceRates.Length > 0)
            {
                AppendReportSectionHeader(sb, "ACCEPTANCE RATES");
                for (int i = 0; i < Results.AcceptanceRates.Length; i++)
                    sb.AppendLine($"  Chain {i + 1}:   {Results.AcceptanceRates[i] * 100.0:F1}%");

                overallAcceptance = Results.AcceptanceRates.Average();
                sb.AppendLine($"  Overall:   {overallAcceptance * 100.0:F1}%");

                string targetRange = GetDesirableAcceptanceRange(Type);
                acceptanceGood = IsAcceptanceRateGood(overallAcceptance, Type);
                sb.AppendLine($"  Target:    {targetRange}    {(acceptanceGood ? "OK" : "WARNING: Outside target range")}");
                if (!acceptanceGood)
                    AppendAcceptanceAdvice(sb, overallAcceptance, Type);

                sb.AppendLine();
            }

            // Section 4: Convergence Diagnostics
            AppendReportSectionHeader(sb, "CONVERGENCE DIAGNOSTICS");
            double maxRhat = double.NaN;
            double minESS = double.NaN;
            string worstRhatParam = "";
            string worstESSParam = "";
            if (Results.ParameterResults != null && Results.ParameterResults.Length > 0)
            {
                for (int i = 0; i < p; i++)
                {
                    double rhat = Results.ParameterResults[i].SummaryStatistics.Rhat;
                    double ess = Results.ParameterResults[i].SummaryStatistics.ESS;
                    if (double.IsNaN(maxRhat) || rhat > maxRhat) { maxRhat = rhat; worstRhatParam = Model.Parameters[i].DisplayName; }
                    if (double.IsNaN(minESS) || ess < minESS) { minESS = ess; worstESSParam = Model.Parameters[i].DisplayName; }
                }
            }
            sb.AppendLine($"  Max R-hat:   {maxRhat:F4}   (target < 1.10)   {(maxRhat < 1.1 ? "OK" : $"WARNING ({worstRhatParam})")}");
            sb.AppendLine($"  Min ESS:     {minESS:F0}   (target > 400)    {(minESS > 400 ? "OK" : $"WARNING ({worstESSParam})")}");
            bool converged = maxRhat < 1.1 && minESS > 400;
            sb.AppendLine($"  Verdict:     {(converged ? "CONVERGED" : "NOT CONVERGED")}");
            if (!converged)
            {
                sb.AppendLine();
                AppendConvergenceAdvice(sb, maxRhat, minESS, worstRhatParam, worstESSParam, acceptanceGood, overallAcceptance, Type);
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
                string name = Model.Parameters[i].DisplayName.PadRight(maxNameLen);
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
                string name = Model.Parameters[i].DisplayName.PadRight(maxNameLen);
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
                    AppendReportMatrix(sb, covMatrix, p, maxNameLen, "G4");

                    var corrMatrix = GetPosteriorCorrelationMatrix();
                    if (corrMatrix != null)
                    {
                        AppendReportSectionHeader(sb, "POSTERIOR CORRELATION MATRIX");
                        AppendReportMatrix(sb, corrMatrix, p, maxNameLen, "F3");
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
                int nBad = ParetoK.Count(k => k > 0.7);
                sb.AppendLine($"  Pareto k:    {nBad}/{ParetoK.Length} observations with k > 0.7");
            }
            sb.AppendLine();

            // Section 9: Prior Configuration
            AppendReportSectionHeader(sb, "PRIOR CONFIGURATION");
            sb.AppendLine($"  {"Parameter".PadRight(maxNameLen)}  {"Prior",-24}  {"Bounds",-20}  Fixed");
            sb.AppendLine($"  {new string('-', maxNameLen)}  {new string('-', 24)}  {new string('-', 20)}  {new string('-', 5)}");
            for (int i = 0; i < p; i++)
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
        /// <param name="p">The number of parameters (matrix dimension).</param>
        /// <param name="maxNameLen">Maximum parameter name length for alignment.</param>
        /// <param name="format">Numeric format string (e.g., "G4" or "F3").</param>
        private void AppendReportMatrix(StringBuilder sb, double[,] matrix, int p, int maxNameLen, string format)
        {
            if (Model is null) return;
            int colWidth = Math.Max(12, maxNameLen);

            // Column headers
            sb.Append("  " + new string(' ', maxNameLen));
            for (int j = 0; j < p; j++)
                sb.Append($"  {Model.Parameters[j].DisplayName.PadLeft(colWidth)}");
            sb.AppendLine();

            // Rows
            for (int i = 0; i < p; i++)
            {
                string rowName = Model.Parameters[i].DisplayName.PadRight(maxNameLen);
                sb.Append($"  {rowName}");
                for (int j = 0; j < p; j++)
                    sb.Append($"  {matrix[i, j].ToString(format, CultureInfo.InvariantCulture).PadLeft(colWidth)}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        /// <summary>
        /// Appends advice text when the acceptance rate is outside the desirable range.
        /// </summary>
        private static void AppendAcceptanceAdvice(StringBuilder sb, double rate, SamplerType type)
        {
            bool isTooLow = type switch
            {
                SamplerType.DEMCz => rate < 0.15,
                SamplerType.DEMCzs => rate < 0.15,
                SamplerType.ARWMH => rate < 0.15,
                SamplerType.NUTS => rate < 0.50,
                _ => false
            };
            bool isTooHigh = type switch
            {
                SamplerType.DEMCz => rate > 0.50,
                SamplerType.DEMCzs => rate > 0.50,
                SamplerType.ARWMH => rate > 0.40,
                SamplerType.NUTS => rate > 0.95,
                _ => false
            };

            if (isTooLow)
            {
                sb.AppendLine();
                sb.AppendLine("  Advice: Acceptance rate is too LOW. Proposals are too ambitious.");
                if (type == SamplerType.ARWMH)
                    sb.AppendLine("    - Decrease the jump rate to make smaller proposals.");
                sb.AppendLine("    - Check that priors are consistent with the data.");
                sb.AppendLine("    - Check for highly correlated parameters or a poorly identified model.");
            }
            else if (isTooHigh)
            {
                sb.AppendLine();
                sb.AppendLine("  Advice: Acceptance rate is too HIGH. Proposals are too timid.");
                if (type == SamplerType.ARWMH)
                    sb.AppendLine("    - Increase the jump rate to make larger proposals.");
                sb.AppendLine("    - The chain may be exploring the posterior too slowly.");
                sb.AppendLine("    - Consider increasing the thinning interval to reduce autocorrelation.");
            }
        }

        /// <summary>
        /// Appends detailed advice when MCMC convergence diagnostics indicate problems.
        /// </summary>
        private static void AppendConvergenceAdvice(StringBuilder sb, double maxRhat, double minESS,
            string worstRhatParam, string worstESSParam, bool acceptanceGood, double overallAcceptance,
            SamplerType type)
        {
            sb.AppendLine("  Recommendations:");

            if (maxRhat >= 1.1)
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
                if (!acceptanceGood && !double.IsNaN(overallAcceptance))
                    sb.AppendLine("    - Fix the acceptance rate first (see above), then re-run.");
            }

            if (minESS < 400)
            {
                sb.AppendLine();
                sb.AppendLine($"  ESS ({worstESSParam} = {minESS:F0}):");
                sb.AppendLine("    Low ESS means high autocorrelation between posterior samples.");
                sb.AppendLine("    Posterior summaries and credible intervals may be unreliable.");
                sb.AppendLine("    - Increase the thinning interval to reduce autocorrelation.");
                sb.AppendLine("    - Increase the output length to retain more independent samples.");
                if (minESS < 100)
                {
                    sb.AppendLine("    - ESS < 100 is very low; quantile estimates will be noisy.");
                    sb.AppendLine("    - Consider switching samplers (e.g., DEMCzs handles correlated");
                    sb.AppendLine("      parameters better than ARWMH).");
                }
            }
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

        /// <summary>
        /// Returns the desirable acceptance rate range for the given sampler type.
        /// </summary>
        private static string GetDesirableAcceptanceRange(SamplerType type)
        {
            return type switch
            {
                SamplerType.DEMCz => "23-44% (DE-MCz)",
                SamplerType.DEMCzs => "23-44% (DE-MCzs)",
                SamplerType.ARWMH => "20-30% (ARWMH)",
                SamplerType.NUTS => "65-90% (NUTS)",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Checks whether the overall acceptance rate is within the desirable range.
        /// </summary>
        private static bool IsAcceptanceRateGood(double rate, SamplerType type)
        {
            return type switch
            {
                SamplerType.DEMCz => rate >= 0.15 && rate <= 0.50,
                SamplerType.DEMCzs => rate >= 0.15 && rate <= 0.50,
                SamplerType.ARWMH => rate >= 0.15 && rate <= 0.40,
                SamplerType.NUTS => rate >= 0.50,
                _ => true
            };
        }

        #endregion

        /// <summary>
        /// Clones this Bayesian analysis, including its configuration and results.
        /// </summary>
        /// <remarks>
        /// The <see cref="Model"/> reference is shared by design (consistent with
        /// the rest of the project — the model is the single source of truth and
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

