using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Numerics;
using Numerics.Distributions;
using Numerics.Mathematics;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using Numerics.Mathematics.RootFinding;
using Numerics.Sampling;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;

namespace RMC.BestFit.Estimation
{

    /// <summary>
    /// An optional function that computes the Jacobian matrix of the mean moment conditions with respect to the parameters.
    /// </summary>
    /// <param name="parameters">The list of parameters to evaluate.</param>
    /// <returns>The Jacobian matrix of the mean moment conditions.</returns>
    [Serializable]
    public delegate double[,] JacobianFunction(double[] parameters);

    /// <summary>
    /// An optional penalty function.
    /// </summary>
    /// <param name="parameters">The list of parameters to evaluate.</param>
    /// <returns>The penalty given the parameter set.</returns>
    /// <remarks>
    /// If there is no penalty, return 0.
    /// </remarks>
    [Serializable]
    public delegate double PenaltyFunction(double[] parameters);

    /// <summary>
    /// The moment condition function.
    /// </summary>
    /// <param name="parameters">A list of model parameters to evaluate.</param>
    /// <returns>
    /// A tuple containing the following information:
    /// <list type="bullet">
    /// <item><c>G</c>: The mean error vector.</item>
    /// <item><c>S</c>: The covariance matrix of the moment conditions.</item>
    /// </list>
    /// </returns>
    public delegate (Vector G, Matrix S) MomentConditionFunction(double[] parameters);

    /// <summary>
    /// An optional function that returns per-observation moment condition vectors for influence diagnostics.
    /// </summary>
    /// <param name="parameters">The list of parameters to evaluate.</param>
    /// <returns>
    /// A matrix of dimension [n × q] where n is the sample size and q is the number of moment conditions.
    /// Row i contains observation i's contribution to the moment conditions: g_i(θ).
    /// </returns>
    public delegate double[,] PointwiseMomentConditionFunction(double[] parameters);

    /// <summary>
    /// A class for performing model estimation with the Generalized Method of Moments (GMM).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Description:</b>
    ///     This class implements GMM parameter estimation by minimizing the quadratic form
    ///     Q(θ) = g(θ)' W g(θ), where g(θ) is the sample mean of moment conditions and W is a
    ///     weighting matrix. Supports one-step, two-step, and iterative estimation strategies.
    /// </para>
    /// <para>
    ///     Two constructors are provided: a model-based constructor that accepts an <see cref="IGMMModel"/>
    ///     (mirroring <see cref="MaximumLikelihood"/>'s use of <see cref="IModel"/>), and a delegate-based
    ///     constructor for advanced users who supply moment condition functions directly.
    /// </para>
    /// <para>
    ///     The default optimizer is BFGS with automatic NelderMead fallback if BFGS fails. This can
    ///     be controlled via <see cref="OptimizerMethod"/> and <see cref="UseFallbackOptimizer"/>.
    /// </para>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     Hansen, L.P. (1982). Large Sample Properties of Generalized Method of Moments Estimators.
    ///     Econometrica, 50(4), 1029-1054.
    /// </para>
    /// </remarks>
    [Serializable]
    public class GeneralizedMethodOfMoments : INotifyPropertyChanged
    {

        #region Construction

        /// <summary>
        /// Constructs a new GMM estimation class from an <see cref="IGMMModel"/>.
        /// </summary>
        /// <param name="model">The GMM model to estimate. Provides parameters, bounds, moment conditions, and sample size.</param>
        /// <param name="method">Optional. The optimization method. Default = BFGS.</param>
        /// <exception cref="ArgumentNullException">Thrown when model is null.</exception>
        /// <remarks>
        /// This constructor mirrors the <see cref="MaximumLikelihood(IModel, OptimizationMethod)"/> pattern.
        /// All configuration is read from the model: parameter values become initial values, parameter bounds
        /// become optimizer bounds, and the moment condition function and optional delegates are read directly.
        /// </remarks>
        public GeneralizedMethodOfMoments(IGMMModel model, OptimizationMethod method = OptimizationMethod.BFGS)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model), "Model cannot be null.");

            MomentConditionFunction = model.MomentConditionFunction ?? throw new ArgumentException("The model's MomentConditionFunction cannot be null.", nameof(model));
            NumberOfParameters = model.NumberOfParameters;
            NumberOfMomentConditions = model.NumberOfMomentConditions;
            SampleSize = model.SampleSize;

            InitialValues = model.Parameters.Select(p => p.Value).ToArray();
            LowerBounds = model.Parameters.Select(p => p.LowerBound).ToArray();
            UpperBounds = model.Parameters.Select(p => p.UpperBound).ToArray();

            JacobianFunction = model.JacobianFunction;
            PenaltyFunction = model.PenaltyFunction;
            PointwiseMomentConditions = model.PointwiseMomentConditions;

            ComputeIdentificationStatus();
            _optimizerMethod = method;
        }

        /// <summary>
        /// Constructs a new GMM estimation class from raw delegate functions.
        /// </summary>
        /// <param name="momentConditionFunction">The moment condition function returning (G, S).</param>
        /// <param name="numberOfParameters">The number of model parameters (p).</param>
        /// <param name="numberOfMomentConditions">The number of moment conditions (q).</param>
        /// <param name="sampleSize">The total sample size.</param>
        /// <param name="initialValues">Initial parameter values for the optimizer.</param>
        /// <param name="lowerBounds">Lower bounds for each parameter.</param>
        /// <param name="upperBounds">Upper bounds for each parameter.</param>
        /// <param name="initialW">Optional initial weighting matrix. Default = identity.</param>
        /// <param name="jacobianFunction">Optional analytical Jacobian function.</param>
        /// <param name="penaltyFunction">Optional penalty function for regularization.</param>
        /// <param name="pointwiseMomentConditions">Optional per-observation moment condition function for influence diagnostics.</param>
        /// <exception cref="ArgumentNullException">Thrown when momentConditionFunction is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when bounds are inconsistent or initial values are out of bounds.</exception>
        public GeneralizedMethodOfMoments(
            MomentConditionFunction momentConditionFunction,
            int numberOfParameters,
            int numberOfMomentConditions,
            int sampleSize,
            IList<double> initialValues,
            IList<double> lowerBounds,
            IList<double> upperBounds,
            Matrix? initialW = null,
            JacobianFunction? jacobianFunction = null,
            PenaltyFunction? penaltyFunction = null,
            PointwiseMomentConditionFunction? pointwiseMomentConditions = null)
        {
            if (momentConditionFunction == null)
                throw new ArgumentNullException(nameof(momentConditionFunction), "The moment condition function cannot be null.");
            if (initialValues.Count != numberOfParameters || lowerBounds.Count != numberOfParameters || upperBounds.Count != numberOfParameters)
                throw new ArgumentOutOfRangeException(nameof(numberOfParameters), "The initial values and lower and upper bounds must be the same length as the number of parameters.");

            for (int j = 0; j < initialValues.Count; j++)
            {
                if (upperBounds[j] < lowerBounds[j])
                    throw new ArgumentOutOfRangeException(nameof(upperBounds), "The upper bound cannot be less than the lower bound.");
                if (initialValues[j] < lowerBounds[j] || initialValues[j] > upperBounds[j])
                    throw new ArgumentOutOfRangeException(nameof(initialValues), "The initial values must be between the upper and lower bounds.");
            }

            MomentConditionFunction = momentConditionFunction;
            NumberOfParameters = numberOfParameters;
            NumberOfMomentConditions = numberOfMomentConditions;
            SampleSize = sampleSize;
            InitialValues = initialValues.ToArray();
            LowerBounds = lowerBounds.ToArray();
            UpperBounds = upperBounds.ToArray();
            W = initialW;
            JacobianFunction = jacobianFunction;
            PenaltyFunction = penaltyFunction;
            PointwiseMomentConditions = pointwiseMomentConditions;

            ComputeIdentificationStatus();
        }

        /// <summary>
        /// Computes the identification status from parameter and moment condition counts.
        /// </summary>
        private void ComputeIdentificationStatus()
        {
            if (NumberOfParameters > NumberOfMomentConditions)
                IdentificationStatus = GMMIdentificationStatus.UnderIdentified;
            else if (NumberOfParameters == NumberOfMomentConditions)
                IdentificationStatus = GMMIdentificationStatus.JustIdentified;
            else
                IdentificationStatus = GMMIdentificationStatus.OverIdentified;
        }

        #endregion

        #region Members

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        private OptimizationMethod _optimizerMethod = OptimizationMethod.BFGS;
        private GMMEstimationStrategy _estimationStrategy = GMMEstimationStrategy.Iterative;
        private int _maxGMMIterations = 100;
        private bool _convergedWithinTolerance;
        private double _absoluteTolerance = 1E-8;
        private double _relativeTolerance = 1E-8;
        private int _maxFunctionEvaluations = 2000;
        private bool _useFallbackOptimizer = true;
        private bool _useNelderMeadForRemainingIterations;
        private int _optimizerFallbackCount;
        /// <summary>
        /// Stores the unpenalized moment objective evaluated with the weighting matrix
        /// selected by the completed estimation strategy.
        /// </summary>
        /// <remarks>
        /// Covariance post-processing may replace <see cref="W"/>. Preserving this scalar
        /// ensures Hansen's J statistic continues to use the fit's selected weight.
        /// </remarks>
        private double _selectedWeightMomentObjective = double.NaN;

        /// <summary>
        /// Gets the number of optimizer fallback transitions recorded by the current estimate.
        /// </summary>
        /// <remarks>
        /// The counter increments when a BFGS pass fails and the configured fallback optimizer is invoked.
        /// Subsequent passes in the same estimate use Nelder-Mead directly.
        /// </remarks>
        internal int OptimizerFallbackCount => _optimizerFallbackCount;

        /// <summary>
        /// Identification status for a GMM specification based on the counts of moment conditions (q) and parameters (p).
        /// </summary>
        public enum GMMIdentificationStatus
        {
            /// <summary>More parameters than moment conditions (p > q). Requires a penalty function.</summary>
            UnderIdentified,
            /// <summary>Equal parameters and moment conditions (p = q). Exactly identified.</summary>
            JustIdentified,
            /// <summary>Fewer parameters than moment conditions (p &lt; q). Can test specification.</summary>
            OverIdentified
        }

        /// <summary>
        /// Enumeration of GMM estimation strategies.
        /// </summary>
        public enum GMMEstimationStrategy
        {
            /// <summary>Single-step using the initial weighting matrix (identity or user-supplied).</summary>
            OneStep,
            /// <summary>Two passes: first with initial W, second with W = S⁻¹.</summary>
            TwoStep,
            /// <summary>Iterative refinement of W = S⁻¹ until parameter convergence.</summary>
            Iterative
        }

        /// <summary>
        /// Gets the IGMMModel used for estimation, if constructed with one. Null for delegate-based construction.
        /// </summary>
        public IGMMModel? Model { get; private set; }

        /// <summary>
        /// Gets the moment condition function.
        /// </summary>
        public MomentConditionFunction MomentConditionFunction { get; private set; }

        /// <summary>
        /// Gets the optional analytical Jacobian function.
        /// </summary>
        public JacobianFunction? JacobianFunction { get; private set; }

        /// <summary>
        /// Gets the optional penalty function.
        /// </summary>
        public PenaltyFunction? PenaltyFunction { get; private set; }

        /// <summary>
        /// Gets the optional pointwise moment condition function for influence diagnostics.
        /// </summary>
        public PointwiseMomentConditionFunction? PointwiseMomentConditions { get; private set; }

        /// <summary>
        /// Gets the number of model parameters.
        /// </summary>
        public int NumberOfParameters { get; private set; }

        /// <summary>
        /// Gets the number of moment conditions.
        /// </summary>
        public int NumberOfMomentConditions { get; private set; }

        /// <summary>
        /// Gets the identification status.
        /// </summary>
        public GMMIdentificationStatus IdentificationStatus { get; private set; }

        /// <summary>
        /// An array of initial values to evaluate.
        /// </summary>
        public double[] InitialValues { get; private set; }

        /// <summary>
        /// An array of lower bounds (inclusive) of the interval containing the optimal point.
        /// </summary>
        public double[] LowerBounds { get; private set; }

        /// <summary>
        /// An array of upper bounds (inclusive) of the interval containing the optimal point.
        /// </summary>
        public double[] UpperBounds { get; private set; }

        /// <summary>
        /// Gets the optimization method to use for estimating the model parameters. Default = BFGS.
        /// </summary>
        public OptimizationMethod OptimizerMethod
        {
            get { return _optimizerMethod; }
            set
            {
                if (_optimizerMethod != value)
                {
                    _optimizerMethod = value;
                    RaisePropertyChange(nameof(OptimizerMethod));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to automatically fall back to NelderMead when BFGS fails.
        /// Default = true. Only applies when <see cref="OptimizerMethod"/> is BFGS.
        /// </summary>
        public bool UseFallbackOptimizer
        {
            get { return _useFallbackOptimizer; }
            set
            {
                if (_useFallbackOptimizer != value)
                {
                    _useFallbackOptimizer = value;
                    RaisePropertyChange(nameof(UseFallbackOptimizer));
                }
            }
        }

        /// <summary>
        /// Gets or sets the GMM estimation strategy. Default = Iterative.
        /// </summary>
        public GMMEstimationStrategy EstimationStrategy
        {
            get { return _estimationStrategy; }
            set
            {
                if (_estimationStrategy != value)
                {
                    _estimationStrategy = value;
                    RaisePropertyChange(nameof(EstimationStrategy));
                }
            }
        }

        /// <summary>
        /// Gets the total sample size used in GMM estimation.
        /// </summary>
        public int SampleSize { get; private set; }

        /// <summary>
        /// Gets or sets the maximum number of iterations for iterative GMM.
        /// </summary>
        public int MaxGMMIterations
        {
            get { return _maxGMMIterations; }
            set
            {
                if (_maxGMMIterations != value)
                {
                    _maxGMMIterations = value;
                    RaisePropertyChange(nameof(MaxGMMIterations));
                }
            }
        }

        /// <summary>
        /// The maximum function evaluations allowed within each GMM iteration.
        /// </summary>
        public int MaxFunctionEvaluations
        {
            get => _maxFunctionEvaluations;
            set
            {
                if (_maxFunctionEvaluations != value)
                {
                    _maxFunctionEvaluations = value;
                    RaisePropertyChange(nameof(MaxFunctionEvaluations));
                }
            }
        }

        /// <summary>
        /// Gets the absolute tolerance for convergence.
        /// </summary>
        public double AbsoluteTolerance
        {
            get { return _absoluteTolerance; }
            set
            {
                if (_absoluteTolerance != value)
                {
                    _absoluteTolerance = value;
                    RaisePropertyChange(nameof(AbsoluteTolerance));
                }
            }
        }

        /// <summary>
        /// Gets the relative tolerance for convergence.
        /// </summary>
        public double RelativeTolerance
        {
            get { return _relativeTolerance; }
            set
            {
                if (_relativeTolerance != value)
                {
                    _relativeTolerance = value;
                    RaisePropertyChange(nameof(RelativeTolerance));
                }
            }
        }

        /// <summary>
        /// Gets the optimizer used to minimize the objective function.
        /// </summary>
        /// <remarks>
        /// This is a transient computation object — it is rebuilt each time
        /// <c>Estimate</c> runs and is <c>null</c> after <see cref="RestoreFromXElement"/>
        /// (which restores only the persistent output state). Read <see cref="Status"/> instead
        /// of <c>Optimizer.Status</c> for any post-estimation logic that must survive Save/Open.
        /// </remarks>
        public Optimizer Optimizer { get; private set; } = null!;

        /// <summary>
        /// Gets the final <see cref="OptimizationStatus"/> from the most recent estimation run.
        /// </summary>
        /// <remarks>
        /// Mirrors the inner <see cref="Optimizer"/>.Status, captured at the end of
        /// <see cref="MinimizeWithFallback"/> so it remains valid after the transient optimizer is
        /// discarded — including after Open() via <see cref="RestoreFromXElement"/>. Returns
        /// <see cref="OptimizationStatus.None"/> before any estimation has run, and after
        /// <see cref="ClearResults"/>. This status always describes the optimization pass that
        /// produced <see cref="BestParameterSet"/>. A usable best point is retained for
        /// non-failure terminations such as maximum function evaluations or maximum iterations;
        /// use <see cref="ConvergedWithinTolerance"/> to distinguish tolerance-confirmed
        /// convergence from a best-effort optimizer result.
        /// </remarks>
        public OptimizationStatus Status { get; private set; } = OptimizationStatus.None;

        /// <summary>
        /// Gets a value indicating whether estimation produced a usable best parameter set.
        /// </summary>
        /// <remarks>
        /// This can be <c>true</c> for non-failure optimizer terminations that do not meet strict
        /// convergence tolerance. Inspect <see cref="Status"/> and <see cref="ConvergedWithinTolerance"/>
        /// when strict optimizer convergence matters.
        /// </remarks>
        public bool IsEstimated { get; private set; }

        /// <summary>
        /// Gets the degrees of freedom for the J-statistic test.
        /// For over-identified models: q - p (number of moment conditions minus parameters).
        /// Returns 0 for just-identified or under-identified models.
        /// </summary>
        public int DegreeOfFreedom => Math.Max(0, NumberOfMomentConditions - NumberOfParameters);

        /// <summary>
        /// Gets the moment condition covariance matrix.
        /// </summary>
        public Matrix? S { get; private set; }

        /// <summary>
        /// Gets the current GMM weighting matrix.
        /// </summary>
        public Matrix? W { get; private set; }

        /// <summary>
        /// Gets the estimated parameter covariance matrix.
        /// </summary>
        /// <remarks>
        /// Valid only after <see cref="PostProcess"/> completes. Returns <c>null</c>
        /// while <c>Estimate</c> is mid-iteration or before it is called;
        /// callers examining <c>Sigma</c> between <c>Estimate()</c> and
        /// <c>PostProcess()</c> see the previous run's value (or null).
        /// </remarks>
        public Matrix? Sigma { get; private set; }
        /// <summary>
        /// Gets the outcome of the most recent covariance computation.
        /// </summary>
        /// <remarks>
        /// The value resets to <see cref="CovarianceComputationStatus.NotComputed"/> whenever
        /// estimation results are cleared or a new estimation run begins.
        /// </remarks>
        public CovarianceComputationStatus CovarianceStatus { get; private set; } = CovarianceComputationStatus.NotComputed;

        /// <summary>
        /// Gets a diagnostic message for the most recent covariance computation.
        /// </summary>
        /// <remarks>
        /// This is <c>null</c> for an unregularized successful computation and before any
        /// covariance attempt. Failed and regularized computations provide concise details.
        /// </remarks>
        public string? CovarianceDiagnostic { get; private set; }

        /// <summary>
        /// Gets the best parameter set found during estimation. Initialized to an empty
        /// <see cref="ParameterSet"/> so consumers that bypass <c>IsEstimated</c>
        /// get a deterministic empty parameter set rather than an
        /// <see cref="NullReferenceException"/>.
        /// </summary>
        public ParameterSet BestParameterSet { get; private set; } = new ParameterSet();

        /// <summary>
        /// Gets the J-statistic value for model fit.
        /// </summary>
        public double JStat { get; private set; } = double.NaN;

        /// <summary>
        /// Gets the p-value for the J-statistic.
        /// </summary>
        public double JStatPval { get; private set; } = double.NaN;

        /// <summary>
        /// Gets the number of iterations used for iterative estimation.
        /// </summary>
        public int GMMIterations { get; private set; }

        /// <summary>
        /// Returns <c>true</c> only when the most recent iterative <c>Estimate</c> run
        /// reached a convergence criterion.
        /// </summary>
        /// <remarks>
        /// Useful for callers that want to gate downstream reporting on
        /// tolerance-convergence rather than best-effort termination. Convergence on the final
        /// permitted pass is reported as confirmed. Exhaustion, optimizer failure, one-step and
        /// two-step strategies, and iterative runs without a comparison pass are reported as not
        /// confirmed converged.
        /// </remarks>
        public bool ConvergedWithinTolerance => IsEstimated && _convergedWithinTolerance;

        /// <summary>
        /// Gets the total number of function evaluations required to estimate the model.
        /// </summary>
        public int TotalFunctionEvaluations { get; private set; }

        /// <summary>
        /// Gets the objective function value Q(θ̂) at the estimated parameters.
        /// Returns NaN if the model has not been estimated.
        /// </summary>
        public double ObjectiveFunctionValue => IsEstimated ? Q(BestParameterSet.Values) : double.NaN;

        /// <summary>
        /// Gets the history of objective function values across GMM iterations (iterative strategy only).
        /// </summary>
        public List<double> ConvergenceHistory { get; private set; } = new();

        /// <summary>
        /// Gets or sets whether the penalty target parameters are random variables. Default = true.
        /// </summary>
        /// <remarks>
        /// <para>Controls whether the penalty Hessian H = P/n appears in the sandwich meat.</para>
        /// <para>
        /// <b>True (default):</b> The penalty target θ₀ is a random variable with its own
        /// sampling variance MSE (e.g., B17C regional skewness estimate). Both data and regional
        /// variability contribute to the meat: Meat = D'WSWD + P/n. When W = S⁻¹, this collapses
        /// to the Bayesian-correct weighted MSE: Σ = (1/MSE_atsite + 1/MSE_regional)⁻¹.
        /// </para>
        /// <para>
        /// <b>False:</b> The penalty target θ₀ is a deterministic constant (ridge-type regularization).
        /// Only data variability contributes: Meat = D'WSWD. Standard penalized GMM.
        /// </para>
        /// </remarks>
        public bool PenaltyIsRandom { get; set; } = true;

        #endregion

        #region Moment Condition Methods

        /// <summary>
        /// Computes the sample mean vector of the moment conditions at the given parameters.
        /// </summary>
        /// <param name="parameters">The parameter values to evaluate.</param>
        /// <returns>The vector G of dimension q×1, where q is the number of moment conditions.</returns>
        /// <remarks>
        /// This method delegates to the MomentConditionFunction and extracts only the mean vector G.
        /// </remarks>
        public Vector GetG(double[] parameters)
        {
            return MomentConditionFunction(parameters).G;
        }

        /// <summary>
        /// Computes the GMM objective function Q(θ).
        /// </summary>
        /// <param name="parameters">The parameter values θ to evaluate.</param>
        /// <returns>The scalar objective function value. Returns double.MaxValue if non-finite.</returns>
        /// <remarks>
        /// <para>Without penalties: Q(θ) = g(θ)'W g(θ), the standard GMM quadratic form.</para>
        /// <para>With penalties: Q(θ) = (1/2)g(θ)'W g(θ) + penalty(θ), where penalty uses the
        /// half-quadratic convention penalty = (1/2)(θ−θ₀)²/(MSE·n). The 1/2 factor on both
        /// the quadratic form and the penalty ensures ∇Q = D'Wg + ∇penalty is exact for BFGS,
        /// and the Hessian ∂²Q ≈ D'WD + P/n requires no Lambda correction.
        /// The argmin is invariant to this positive scaling.</para>
        /// <para>For just-identified models (q=p), minimizing Q leads to g(θ)=0.</para>
        /// <para>For over-identified models (q&gt;p), W is typically S⁻¹ in efficient GMM.</para>
        /// </remarks>
        public double Q(double[] parameters)
        {
            if (W == null)
                throw new InvalidOperationException("Weighting matrix W must be set before computing Q. Call Estimate() or set W directly.");

            double q = GetUnpenalizedMomentObjective(parameters, W);

            if (PenaltyFunction != null)
            {
                // Half-quadratic convention: Q = (1/2)g'Wg + penalty.
                // The penalty already includes its own 1/2 factor, so the 1/2 here
                // applies only to the moment quadratic form to maintain consistent
                // gradient and Hessian scaling for BFGS optimization.
                q *= 0.5;
                q += PenaltyFunction(parameters);
            }

            return Tools.IsFinite(q) ? q : double.MaxValue;
        }

        /// <summary>
        /// Evaluates the unpenalized GMM moment quadratic with an explicit weighting matrix.
        /// </summary>
        /// <param name="parameters">Parameter values at which to evaluate the mean moments.</param>
        /// <param name="weightingMatrix">Weighting matrix defining the quadratic form.</param>
        /// <returns>The scalar value <c>g(theta)' W g(theta)</c>.</returns>
        /// <remarks>
        /// This method intentionally excludes <see cref="PenaltyFunction"/> so model-specification
        /// diagnostics are never contaminated by parameter regularization terms.
        /// </remarks>
        private double GetUnpenalizedMomentObjective(double[] parameters, Matrix weightingMatrix)
        {
            Vector meanMoments = GetG(parameters);
            return meanMoments.Multiply(weightingMatrix).Multiply(meanMoments).Sum();
        }

        /// <summary>
        /// Computes the sample covariance matrix S of the moment conditions.
        /// </summary>
        /// <param name="parameters">The parameter values to evaluate.</param>
        /// <returns>The q×q covariance matrix S, regularized to be symmetric positive definite.</returns>
        /// <remarks>
        /// <para>S estimates the asymptotic variance of √n g_n(θ).</para>
        /// <para>In two-step/iterative GMM, the optimal weighting matrix is W = S⁻¹.</para>
        /// </remarks>
        public Matrix GetS(double[] parameters)
        {
            var s = MomentConditionFunction(parameters).S;
            s = MatrixRegularization.MakeSymmetricPositiveDefinite(s);
            return s;
        }

        /// <summary>
        /// Computes the Jacobian matrix D = ∂g/∂θ of the moment conditions with respect to parameters.
        /// </summary>
        /// <param name="parameters">The parameter values to evaluate.</param>
        /// <returns>A q×p matrix where entry (i,j) is ∂g_i/∂θ_j.</returns>
        /// <remarks>
        /// <para>If JacobianFunction is provided, uses analytic derivatives; otherwise uses numerical differentiation.</para>
        /// <para>The Jacobian is used in computing covariance matrices and gradients.</para>
        /// </remarks>
        public Matrix GetJacobian(double[] parameters)
        {
            if (JacobianFunction == null)
            {
                // Use adaptive step sizes with boundary handling.
                // Bounds prevent σ from going negative and keep γ within feasible range.
                var g0 = GetG(parameters);
                return new Matrix(NumericalDiff.ComputeJacobian(
                    x => { return GetG(x).Array; },
                    parameters.ToArray(),
                    g0.Length,
                    LowerBounds,
                    UpperBounds));
            }
            else
            {
                return new Matrix(JacobianFunction(parameters));
            }
        }

        /// <summary>
        /// Computes the gradient vector of the penalty function with respect to parameters.
        /// </summary>
        /// <param name="parameters">The parameter values to evaluate.</param>
        /// <returns>A p×1 vector of partial derivatives ∂penalty/∂θ. Returns zero vector if no penalty.</returns>
        private Vector GetPenaltyGradient(double[] parameters)
        {
            if (PenaltyFunction == null)
                return new Vector(parameters.Length);

            var gradient = NumericalDiff.ComputeGradient(
                x => { return PenaltyFunction(x); },
                parameters,
                LowerBounds,
                UpperBounds);
            return new Vector(gradient);
        }

        /// <summary>
        /// Computes the Hessian matrix of the penalty function with respect to parameters.
        /// </summary>
        /// <param name="parameters">The parameter values to evaluate.</param>
        /// <returns>A p×p matrix of second derivatives ∂²penalty/∂θ_i∂θ_j. Returns zero matrix if no penalty.</returns>
        public Matrix GetPenaltyHessian(double[] parameters)
        {
            if (PenaltyFunction == null)
                return new Matrix(parameters.Length);

            return NumericalDiff.ComputeHessian(
                x => { return PenaltyFunction(x); },
                parameters,
                parameters.Length,
                LowerBounds,
                UpperBounds);
        }

        /// <summary>
        /// Computes the gradient of the GMM objective function Q(θ) with respect to parameters.
        /// </summary>
        /// <param name="parameters">The parameter values to evaluate.</param>
        /// <returns>A p×1 gradient vector ∇Q(θ).</returns>
        /// <remarks>
        /// <para>With penalties (half-quadratic convention):
        /// ∇Q = D'Wg + ∇penalty, the exact gradient of Q = (1/2)g'Wg + penalty.
        /// Both the penalty function and the 1/2 on g'Wg are designed so that the gradient
        /// and Hessian are directly consistent with BFGS requirements.</para>
        /// <para>Without penalties: ∇Q = D'Wg, which is proportional to the true gradient
        /// 2D'Wg of g'Wg. The constant factor does not affect the BFGS optimum.</para>
        /// </remarks>
        public Vector GetGradient(double[] parameters)
        {
            var gtMean = GetG(parameters);
            var J = GetJacobian(parameters);
            var JT = J.Transpose();
            var grad = JT * (W! * gtMean) + GetPenaltyGradient(parameters);
            return grad;
        }

        #endregion

        #region Covariance Methods

        /// <summary>
        /// Computes the asymptotic covariance matrix of the estimated parameters.
        /// </summary>
        /// <param name="parameters">The parameter values to evaluate.</param>
        /// <param name="sandwich">If true, uses robust sandwich estimator; if false, uses inverse bread only.</param>
        /// <returns>The p×p covariance matrix of parameter estimates.</returns>
        /// <remarks>
        /// <para>For efficient two-step GMM with W = S⁻¹:</para>
        /// <para>Σ = n⁻¹(D'S⁻¹D)⁻¹ where D = ∂g/∂θ.</para>
        /// <para>Sandwich estimator (robust): Σ = n⁻¹ Bread⁻¹ Meat Bread⁻¹.</para>
        /// <para>For fixed-weight one-step GMM, the configured weight is retained in both bread and meat.</para>
        /// <para>For two-step and iterative GMM, covariance uses S⁻¹ evaluated at the fitted parameters.</para>
        /// <para>
        /// Penalty Hessian H = P/n always appears in the bread: Bread = D'WD + H.
        /// Whether H appears in the meat depends on <see cref="PenaltyIsRandom"/>:
        /// </para>
        /// <para>
        /// <b>Random penalty</b> (<see cref="PenaltyIsRandom"/> = true, default):
        /// The penalty target θ₀ is a random variable (e.g., a regional estimate with its own MSE).
        /// Both data (g_n) and regional (θ₀) variability contribute to the meat:
        /// Meat = D'WSWD + H. This gives the Bayesian-correct weighted MSE when W = S⁻¹.
        /// </para>
        /// <para>
        /// <b>Fixed penalty</b> (<see cref="PenaltyIsRandom"/> = false):
        /// The penalty target θ₀ is a deterministic constant (ridge-type regularization).
        /// Only data variability contributes: Meat = D'WSWD. Standard penalized GMM from
        /// the econometrics literature.
        /// </para>
        /// <para>
        /// With the half-quadratic convention, the penalty Hessian ∂²[(1/2)(θ−θ₀)²/(MSE·n)]
        /// = 1/(MSE·n) = P/n is used directly with no Lambda correction.
        /// </para>
        /// </remarks>
        public Matrix GetCovariance(double[] parameters, bool sandwich = true)
        {
            if (!TryGetCovariance(parameters, sandwich, out Matrix covariance))
            {
                throw new InvalidOperationException(
                    CovarianceDiagnostic ?? "The GMM covariance matrix is unavailable.");
            }

            return covariance;
        }

        /// <summary>
        /// Attempts to compute a finite, non-degenerate parameter covariance matrix.
        /// </summary>
        /// <param name="parameters">Parameter values at which to evaluate covariance.</param>
        /// <param name="sandwich">Whether to use the robust sandwich estimator.</param>
        /// <param name="covariance">
        /// The computed covariance matrix when usable; otherwise, a zero matrix that must
        /// not be interpreted as estimated uncertainty.
        /// </param>
        /// <returns><see langword="true"/> when covariance construction produces a usable finite matrix.</returns>
        /// <remarks>
        /// Inspect <see cref="CovarianceStatus"/> and <see cref="CovarianceDiagnostic"/> after
        /// this method returns. Regularization of moment, bread, or covariance matrices is
        /// reported explicitly.
        /// </remarks>
        public bool TryGetCovariance(double[] parameters, bool sandwich, out Matrix covariance)
        {
            covariance = new Matrix(NumberOfParameters, NumberOfParameters);
            try
            {
                covariance = ComputeCovariance(parameters, sandwich, out bool wasRegularized);
                if (!MatrixIsFinite(covariance) || !HasPositiveFiniteDiagonal(covariance))
                {
                    covariance = new Matrix(NumberOfParameters, NumberOfParameters);
                    return SetCovarianceFailure(
                        "GMM covariance is non-finite or has a non-positive diagonal variance.");
                }

                CovarianceStatus = wasRegularized
                    ? CovarianceComputationStatus.Regularized
                    : CovarianceComputationStatus.Available;
                CovarianceDiagnostic = wasRegularized
                    ? "GMM covariance computation required positive-definite regularization."
                    : null;
                if (CovarianceDiagnostic != null)
                    Debug.WriteLine(CovarianceDiagnostic);
                return true;
            }
            catch (Exception ex)
            {
                covariance = new Matrix(NumberOfParameters, NumberOfParameters);
                Debug.WriteLine($"Failed to compute GMM covariance matrix: {ex.Message}");
                return SetCovarianceFailure($"GMM covariance computation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Computes GMM covariance and records whether any matrix regularization changed an input.
        /// </summary>
        /// <param name="parameters">Parameter values at which to evaluate covariance.</param>
        /// <param name="sandwich">Whether to use the robust sandwich estimator.</param>
        /// <param name="wasRegularized">
        /// Set to <see langword="true"/> when moment, bread, or covariance regularization
        /// changed at least one matrix element.
        /// </param>
        /// <returns>The computed covariance matrix.</returns>
        private Matrix ComputeCovariance(
            double[] parameters,
            bool sandwich,
            out bool wasRegularized)
        {
            wasRegularized = false;

            Matrix rawMomentCovariance = MomentConditionFunction(parameters).S;
            Matrix positiveDefiniteS = MatrixRegularization.MakeSymmetricPositiveDefinite(rawMomentCovariance);
            wasRegularized |= MatricesDifferMaterially(rawMomentCovariance, positiveDefiniteS);
            Matrix regularizedS = MatrixRegularization.Regularize(positiveDefiniteS);
            wasRegularized |= MatricesDifferMaterially(positiveDefiniteS, regularizedS);
            S = regularizedS;

            Matrix covarianceWeight;
            if (EstimationStrategy == GMMEstimationStrategy.OneStep)
            {
                covarianceWeight = W ?? throw new InvalidOperationException(
                    "The fixed one-step weighting matrix is unavailable for covariance computation.");
            }
            else
            {
                covarianceWeight = S.Inverse();
                W = covarianceWeight;
            }

            Matrix jacobian = GetJacobian(parameters);
            Matrix jacobianTranspose = jacobian.Transpose();
            Matrix penaltyHessian = GetPenaltyHessian(parameters);

            Matrix rawBread = jacobianTranspose * covarianceWeight * jacobian + penaltyHessian;
            Matrix bread = MatrixRegularization.MakeSymmetricPositiveDefinite(rawBread);
            wasRegularized |= MatricesDifferMaterially(rawBread, bread);
            Matrix breadInverse = bread.Inverse();

            if (!sandwich)
            {
                breadInverse /= (double)SampleSize;
                Matrix regularizedCovariance =
                    MatrixRegularization.MakeSymmetricPositiveDefinite(breadInverse);
                wasRegularized |= MatricesDifferMaterially(breadInverse, regularizedCovariance);
                return regularizedCovariance;
            }

            Matrix meat = jacobianTranspose * covarianceWeight * S * covarianceWeight * jacobian;
            if (PenaltyIsRandom)
                meat += penaltyHessian;

            Matrix rawCovariance = breadInverse * meat * breadInverse;
            rawCovariance /= (double)SampleSize;
            Matrix covariance = MatrixRegularization.MakeSymmetricPositiveDefinite(rawCovariance);
            wasRegularized |= MatricesDifferMaterially(rawCovariance, covariance);
            return covariance;
        }

        /// <summary>
        /// Records an unavailable covariance result.
        /// </summary>
        /// <param name="diagnostic">Diagnostic explaining the failure.</param>
        /// <returns><see langword="false"/> for direct use by Try methods.</returns>
        private bool SetCovarianceFailure(string diagnostic)
        {
            CovarianceStatus = CovarianceComputationStatus.Failed;
            CovarianceDiagnostic = diagnostic;
            return false;
        }

        /// <summary>
        /// Resets covariance status for a new or cleared estimator state.
        /// </summary>
        private void ResetCovarianceStatus()
        {
            CovarianceStatus = CovarianceComputationStatus.NotComputed;
            CovarianceDiagnostic = null;
        }

        /// <summary>
        /// Computes the maximum absolute elementwise difference between two matrices.
        /// </summary>
        /// <param name="left">First matrix.</param>
        /// <param name="right">Second matrix.</param>
        /// <returns>The maximum absolute elementwise difference.</returns>
        private static double MaximumAbsoluteDifference(Matrix left, Matrix right)
        {
            double maximum = 0.0;
            for (int row = 0; row < left.NumberOfRows; row++)
            {
                for (int column = 0; column < left.NumberOfColumns; column++)
                    maximum = Math.Max(maximum, Math.Abs(left[row, column] - right[row, column]));
            }

            return maximum;
        }

        /// <summary>
        /// Determines whether regularization changed a matrix beyond numerical reconstruction noise.
        /// </summary>
        /// <param name="left">Matrix before regularization.</param>
        /// <param name="right">Matrix after regularization.</param>
        /// <returns><see langword="true"/> when the maximum adjustment is material relative to matrix scale.</returns>
        private static bool MatricesDifferMaterially(Matrix left, Matrix right)
        {
            double scale = 0.0;
            for (int row = 0; row < left.NumberOfRows; row++)
            {
                for (int column = 0; column < left.NumberOfColumns; column++)
                    scale = Math.Max(scale, Math.Abs(left[row, column]));
            }

            double tolerance = 1e-8 * Math.Max(scale, 1e-12);
            return MaximumAbsoluteDifference(left, right) > tolerance;
        }

        /// <summary>
        /// Determines whether every matrix entry is finite.
        /// </summary>
        /// <param name="matrix">Matrix to inspect.</param>
        /// <returns><see langword="true"/> when every entry is finite.</returns>
        private static bool MatrixIsFinite(Matrix matrix)
        {
            for (int i = 0; i < matrix.NumberOfRows; i++)
            {
                for (int j = 0; j < matrix.NumberOfColumns; j++)
                {
                    if (!Tools.IsFinite(matrix[i, j]))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Determines whether a covariance matrix has positive finite diagonal entries.
        /// </summary>
        /// <param name="matrix">Covariance matrix to inspect.</param>
        /// <returns><see langword="true"/> when every diagonal variance is finite and positive.</returns>
        private static bool HasPositiveFiniteDiagonal(Matrix matrix)
        {
            int count = Math.Min(matrix.NumberOfRows, matrix.NumberOfColumns);
            if (count == 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (!Tools.IsFinite(matrix[i, i]) || matrix[i, i] <= 0.0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns the parameter covariance matrix (sandwich estimator by default).
        /// </summary>
        /// <param name="sandwich">If true, uses robust sandwich estimator. Default = true.</param>
        /// <returns>The p×p parameter covariance matrix.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        public Matrix GetCovarianceMatrix(bool sandwich = true)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");

            if (Sigma != null)
            {
                if (!MatrixIsFinite(Sigma) || !HasPositiveFiniteDiagonal(Sigma))
                {
                    SetCovarianceFailure(
                        "The stored GMM covariance is non-finite or has a non-positive diagonal variance.");
                    throw new InvalidOperationException(CovarianceDiagnostic);
                }

                if (CovarianceStatus == CovarianceComputationStatus.NotComputed)
                    CovarianceStatus = CovarianceComputationStatus.Available;
                return Sigma;
            }

            return GetCovariance(BestParameterSet.Values, sandwich);
        }

        /// <summary>
        /// Returns the standard errors of the parameter estimates from the diagonal of the covariance matrix.
        /// </summary>
        /// <returns>An array of standard errors for each parameter.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        public double[] GetStandardErrors()
        {
            var cov = GetCovarianceMatrix();
            var se = new double[NumberOfParameters];
            for (int i = 0; i < NumberOfParameters; i++)
                se[i] = Math.Sqrt(Math.Max(0, cov[i, i]));
            return se;
        }

        /// <summary>
        /// Returns the correlation matrix from the covariance matrix.
        /// </summary>
        /// <returns>The parameter correlation matrix.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        public Matrix GetCorrelationMatrix()
        {
            var cov = GetCovarianceMatrix();
            var corr = new Matrix(NumberOfParameters, NumberOfParameters);
            for (int i = 0; i < NumberOfParameters; i++)
            {
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double denom = Math.Sqrt(cov[i, i] * cov[j, j]);
                    corr[i, j] = denom > 0 ? cov[i, j] / denom : 0;
                }
            }
            return corr;
        }

        /// <summary>
        /// Returns the sandwich (robust) covariance matrix.
        /// </summary>
        /// <returns>The robust covariance matrix using the sandwich estimator.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        public Matrix GetSandwichCovarianceMatrix()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            return GetCovariance(BestParameterSet.Values, sandwich: true);
        }

        /// <summary>
        /// Returns the robust standard errors from the sandwich covariance matrix.
        /// </summary>
        /// <returns>An array of robust standard errors for each parameter.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        public double[] GetRobustStandardErrors()
        {
            var sandwich = GetSandwichCovarianceMatrix();
            var se = new double[NumberOfParameters];
            for (int i = 0; i < NumberOfParameters; i++)
                se[i] = Math.Sqrt(Math.Max(0, sandwich[i, i]));
            return se;
        }

        #endregion

        #region Profile Methods

        /// <summary>
        /// Returns the profile Q objective for each model parameter.
        /// </summary>
        /// <param name="bins">The number of grid points in each profile. Default = 100.</param>
        /// <param name="trueProfile">
        /// If true, re-optimizes nuisance parameters at each grid point (true profile).
        /// If false, fixes nuisance parameters at their estimated values (conditional profile).
        /// Default = true.
        /// </param>
        /// <returns>A list of arrays where each array contains [parameter value, Q value] pairs for one parameter.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when bins is less than 2.</exception>
        /// <remarks>
        /// <para>
        /// For each parameter θ_i, the profile Q is:
        /// Q_profile(θ_i) = min_{θ_{-i}} Q(θ_i, θ_{-i}) for true profiling, or
        /// Q(θ_i, θ̂_{-i}) for conditional profiling.
        /// </para>
        /// <para>
        /// Under efficient GMM (W = S⁻¹), the profile Q difference n·[Q_profile(θ_i) − Q(θ̂)]
        /// is asymptotically χ²(1), analogous to profile likelihood for MLE.
        /// </para>
        /// <para>
        /// Reference: Newey, W.K. and McFadden, D. (1994). Large Sample Estimation and Hypothesis
        /// Testing. Handbook of Econometrics, Vol. 4, Ch. 36.
        /// </para>
        /// </remarks>
        public List<double[,]> ProfileQ(int bins = 100, bool trueProfile = true)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (bins < 2)
                throw new ArgumentOutOfRangeException(nameof(bins), "Number of bins must be at least 2.");

            var list = new List<double[,]>();

            for (int i = 0; i < NumberOfParameters; i++)
            {
                var seq = Stratify.XValues(new StratificationOptions(LowerBounds[i], UpperBounds[i], bins));
                var profile = new double[bins, 2];

                for (int j = 0; j < bins; j++)
                {
                    double gridValue = seq[j].Midpoint;
                    profile[j, 0] = gridValue;
                    profile[j, 1] = trueProfile
                        ? ProfileQTrueAtPoint(i, gridValue)
                        : ProfileQConditionalAtPoint(i, gridValue);
                }

                list.Add(profile);
            }

            return list;
        }

        /// <summary>
        /// Returns parameter confidence intervals based on the profile Q statistic using the chi-squared threshold.
        /// </summary>
        /// <param name="alpha">The significance level. Default = 0.1 (90% confidence). Must be between 0 and 1.</param>
        /// <param name="trueProfile">If true, re-optimizes nuisance parameters. Default = true.</param>
        /// <returns>A matrix where each row contains [lower bound, upper bound] for each parameter.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when alpha is not between 0 and 1.</exception>
        /// <remarks>
        /// <para>
        /// For efficient GMM (W = S⁻¹), the profile Q difference is asymptotically χ²(1):
        /// CI = {θ_i : n·[Q_profile(θ_i) − Q(θ̂)] &lt; χ²₁(1−α)}.
        /// </para>
        /// <para>
        /// The threshold in Q-space is: Q(θ̂) + χ²₁(1−α) / (2n), using the half-quadratic convention
        /// where Q = (1/2)g'Wg + penalty.
        /// </para>
        /// </remarks>
        public double[,] ProfileConfidenceIntervals(double alpha = 0.1, bool trueProfile = true)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (alpha <= 0 || alpha >= 1)
                throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be between 0 and 1.");

            double qHat = Q(BestParameterSet.Values);
            double threshold = qHat + ProfileQThresholdIncrement(alpha);
            var CIs = new double[NumberOfParameters, 2];

            for (int i = 0; i < NumberOfParameters; i++)
            {
                CIs[i, 0] = ProfileQFindBound(i, threshold, lower: true, trueProfile);
                CIs[i, 1] = ProfileQFindBound(i, threshold, lower: false, trueProfile);
            }

            return CIs;
        }

        /// <summary>
        /// Returns parameter percentiles from the profile Q surface by converting to an approximate
        /// marginal density via exp(−n·ΔQ) and numerically inverting the CDF.
        /// </summary>
        /// <param name="percentiles">
        /// Percentiles to compute, each in (0, 1). Default = {0.05, 0.25, 0.50, 0.75, 0.95}.
        /// </param>
        /// <param name="trueProfile">If true, re-optimizes nuisance parameters. Default = true.</param>
        /// <param name="bins">Number of grid points for the profile Q curve. Default = 200.</param>
        /// <returns>
        /// A matrix of dimension [p × len(percentiles)] where element [i,j] is the value of parameter i
        /// at percentile j.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <remarks>
        /// <para>
        /// The profile Q curve is converted to an approximate marginal density for each parameter:
        /// p(θ_i) ∝ exp(−n · [Q_profile(θ_i) − Q(θ̂)]).
        /// This is the Laplace approximation to the marginal posterior under a flat prior.
        /// </para>
        /// <para>
        /// The chi-squared threshold method (used in <see cref="ProfileConfidenceIntervals"/>)
        /// applies symmetric ΔQ thresholds on both sides of θ̂, which captures CI width but not
        /// asymmetry correctly when parameters are correlated (e.g., LP3 μ, σ, γ coupling through
        /// higher-moment conditions). The density-based approach preserves the full Q-curve shape:
        /// where Q rises steeply, exp(−n·Q) drops steeply, producing a lighter tail — correctly
        /// mapping objective-function curvature to sampling-distribution spread.
        /// </para>
        /// <para>
        /// Key diagnostics derivable from the output:
        /// <list type="bullet">
        /// <item>Skewness: Bowley = (P75 + P25 − 2·P50) / (P75 − P25)</item>
        /// <item>Bias/centering shift: P50 − θ̂ (profile median vs point estimate)</item>
        /// <item>Asymmetry ratio R: (P95 − P50) / (P50 − P05)</item>
        /// <item>Tail weight: (P95 − P05) / (P75 − P25)</item>
        /// </list>
        /// </para>
        /// </remarks>
        public double[,] ProfilePercentiles(double[]? percentiles = null, bool trueProfile = true, int bins = 200)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (bins < 10)
                throw new ArgumentOutOfRangeException(nameof(bins), "Number of bins must be at least 10.");

            percentiles ??= new[] { 0.05, 0.25, 0.50, 0.75, 0.95 };

            for (int k = 0; k < percentiles.Length; k++)
            {
                if (percentiles[k] <= 0 || percentiles[k] >= 1)
                    throw new ArgumentOutOfRangeException(nameof(percentiles), "Each percentile must be in (0, 1).");
            }

            // Compute profile Q on a fine grid for each parameter
            var profiles = ProfileQ(bins, trueProfile);
            double qHat = Q(BestParameterSet.Values);
            var result = new double[NumberOfParameters, percentiles.Length];

            // Scale factor: n for half-quadratic Q = (1/2)g'Wg + penalty (n*Q ≈ -ℓ),
            // n/2 for standard Q = g'Wg (n*Q ≈ -2ℓ). Use n/2 as conservative default
            // that works for both conventions (the shape is what matters, not absolute scale).
            // The sandwich SE calibration below corrects any residual scale mismatch.
            double scale = 0.5 * SampleSize;

            for (int i = 0; i < NumberOfParameters; i++)
            {
                var profile = profiles[i];
                int nBins = profile.GetLength(0);

                // Step 1: Compute unnormalized density weights w[j] = exp(-scale * ΔQ[j])
                // Subtract qHat for numerical stability (avoids exp overflow/underflow)
                var theta = new double[nBins];
                var logw = new double[nBins];

                for (int j = 0; j < nBins; j++)
                {
                    theta[j] = profile[j, 0];
                    logw[j] = -scale * (profile[j, 1] - qHat);
                }

                // Step 2: Calibrate scale so that the implied variance matches the sandwich SE.
                // The profile density variance should equal Σ[i,i] from the GMM covariance.
                // This ensures the percentile spread is consistent with the sandwich estimator
                // while the shape (skewness, kurtosis) comes from the profile Q curve.
                double targetVar = (Sigma != null) ? Math.Max(Sigma[i, i], 1e-30) : 0;
                if (targetVar > 0)
                {
                    double impliedVar = ComputeImpliedVariance(theta, logw);
                    if (impliedVar > 1e-30)
                    {
                        // Adjust scale: Var ∝ 1/scale, so new_scale = old_scale * impliedVar / targetVar
                        double scaleAdj = impliedVar / targetVar;
                        for (int j = 0; j < nBins; j++)
                            logw[j] *= scaleAdj;
                    }
                }

                // Step 3: Convert log-weights to normalized CDF via trapezoidal rule
                // Shift log-weights so max is 0 (numerical stability)
                double maxLogW = double.NegativeInfinity;
                for (int j = 0; j < nBins; j++)
                    if (logw[j] > maxLogW) maxLogW = logw[j];

                var w = new double[nBins];
                for (int j = 0; j < nBins; j++)
                    w[j] = Math.Exp(logw[j] - maxLogW);

                // Trapezoidal CDF
                var cdf = new double[nBins];
                cdf[0] = 0;
                for (int j = 1; j < nBins; j++)
                {
                    double dx = theta[j] - theta[j - 1];
                    cdf[j] = cdf[j - 1] + 0.5 * (w[j - 1] + w[j]) * dx;
                }

                // Normalize to [0, 1]
                double total = cdf[nBins - 1];
                if (total > 0)
                {
                    for (int j = 0; j < nBins; j++)
                        cdf[j] /= total;
                }

                // Step 4: Invert CDF at each requested percentile via linear interpolation
                for (int k = 0; k < percentiles.Length; k++)
                {
                    double p = percentiles[k];
                    result[i, k] = InterpolateCDFInverse(theta, cdf, p, BestParameterSet.Values[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Computes the variance implied by a log-weighted density on a grid.
        /// </summary>
        /// <param name="theta">Grid of parameter values.</param>
        /// <param name="logw">Log-weights (unnormalized log-density).</param>
        /// <returns>The variance of the implied density.</returns>
        private static double ComputeImpliedVariance(double[] theta, double[] logw)
        {
            int n = theta.Length;

            // Shift for numerical stability
            double maxLW = double.NegativeInfinity;
            for (int j = 0; j < n; j++)
                if (logw[j] > maxLW) maxLW = logw[j];

            // Compute mean and variance via trapezoidal rule
            double sumW = 0, sumWx = 0, sumWx2 = 0;
            for (int j = 1; j < n; j++)
            {
                double dx = theta[j] - theta[j - 1];
                double w0 = Math.Exp(logw[j - 1] - maxLW);
                double w1 = Math.Exp(logw[j] - maxLW);
                double wAvg = 0.5 * (w0 + w1);
                double xAvg = 0.5 * (theta[j - 1] + theta[j]);
                double x2Avg = 0.5 * (theta[j - 1] * theta[j - 1] + theta[j] * theta[j]);

                sumW += wAvg * dx;
                sumWx += wAvg * xAvg * dx;
                sumWx2 += wAvg * x2Avg * dx;
            }

            if (sumW <= 0) return 0;
            double mean = sumWx / sumW;
            double var = sumWx2 / sumW - mean * mean;
            return Math.Max(var, 0);
        }

        /// <summary>
        /// Linearly interpolates the inverse CDF at a given probability level.
        /// </summary>
        /// <param name="theta">Grid of parameter values (sorted ascending).</param>
        /// <param name="cdf">Cumulative density values at each grid point.</param>
        /// <param name="p">The probability level to invert.</param>
        /// <param name="fallback">Fallback value if interpolation fails.</param>
        /// <returns>The parameter value at the given CDF level.</returns>
        private static double InterpolateCDFInverse(double[] theta, double[] cdf, double p, double fallback)
        {
            int n = theta.Length;

            // If p is outside the CDF range, return the boundary
            if (p <= cdf[0]) return theta[0];
            if (p >= cdf[n - 1]) return theta[n - 1];

            // Find bracketing interval and interpolate
            for (int j = 1; j < n; j++)
            {
                if (cdf[j] >= p)
                {
                    double dCdf = cdf[j] - cdf[j - 1];
                    if (dCdf < 1e-30) return theta[j];
                    double t = (p - cdf[j - 1]) / dCdf;
                    return theta[j - 1] + t * (theta[j] - theta[j - 1]);
                }
            }

            return fallback;
        }

        /// <summary>
        /// Computes the Q-space threshold increment for a given significance level.
        /// </summary>
        /// <param name="alpha">The significance level.</param>
        /// <returns>The increment ΔQ above Q(θ̂) that defines the confidence region boundary.</returns>
        /// <remarks>
        /// <para>
        /// Under efficient GMM with half-quadratic convention Q = (1/2)g'Wg + penalty:
        /// n · 2 · ΔQ ~ χ²(1), so ΔQ = χ²₁(1−α) / (2n).
        /// </para>
        /// </remarks>
        private double ProfileQThresholdIncrement(double alpha)
        {
            var chiSquared = new ChiSquared(1);
            return chiSquared.InverseCDF(1 - alpha) / (2.0 * SampleSize);
        }

        /// <summary>
        /// Evaluates the true profile Q at a fixed value of parameter i by re-optimizing nuisance parameters.
        /// </summary>
        /// <param name="paramIndex">The index of the parameter to fix.</param>
        /// <param name="fixedValue">The fixed value for the parameter.</param>
        /// <returns>The minimized Q value with parameter i fixed at the given value.</returns>
        private double ProfileQTrueAtPoint(int paramIndex, double fixedValue)
        {
            int p = NumberOfParameters;

            if (p == 1)
                return Q(new[] { fixedValue });

            // Build reduced-dimension objective: optimize over θ_{-i}
            int pReduced = p - 1;
            var initReduced = new double[pReduced];
            var lbReduced = new double[pReduced];
            var ubReduced = new double[pReduced];

            int r = 0;
            for (int j = 0; j < p; j++)
            {
                if (j == paramIndex) continue;
                initReduced[r] = BestParameterSet.Values[j];
                lbReduced[r] = LowerBounds[j];
                ubReduced[r] = UpperBounds[j];
                r++;
            }

            // Reduced objective: pack fixedValue into the full parameter vector
            double reducedQ(double[] reducedParams)
            {
                var fullParams = new double[p];
                int ri = 0;
                for (int j = 0; j < p; j++)
                {
                    if (j == paramIndex)
                        fullParams[j] = fixedValue;
                    else
                        fullParams[j] = reducedParams[ri++];
                }
                return Q(fullParams);
            }

            if (pReduced == 1)
            {
                // Use Brent for 1D optimization
                var brent = new BrentSearch(x => reducedQ(new[] { x }), lbReduced[0], ubReduced[0])
                {
                    AbsoluteTolerance = AbsoluteTolerance,
                    RelativeTolerance = RelativeTolerance,
                    ReportFailure = false
                };

                try
                {
                    brent.Minimize();
                    if (brent.Status == OptimizationStatus.Success)
                        return brent.BestParameterSet.Fitness;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Profile Q Brent optimization failed at param {paramIndex}={fixedValue}: {ex.Message}");
                }

                // Fallback: evaluate at current nuisance values
                return reducedQ(initReduced);
            }
            else
            {
                // Use NelderMead for multi-dimensional optimization
                var nm = new NelderMead(reducedQ, pReduced, initReduced, lbReduced, ubReduced)
                {
                    MaxFunctionEvaluations = Math.Max(500, MaxFunctionEvaluations / 2),
                    AbsoluteTolerance = AbsoluteTolerance,
                    RelativeTolerance = RelativeTolerance,
                    ReportFailure = false,
                    RecordTraces = false,
                    ComputeHessian = false
                };

                try
                {
                    nm.Minimize();
                    if (nm.Status == OptimizationStatus.Success)
                        return nm.BestParameterSet.Fitness;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Profile Q NelderMead optimization failed at param {paramIndex}={fixedValue}: {ex.Message}");
                }

                // Fallback: evaluate at current nuisance values
                return reducedQ(initReduced);
            }
        }

        /// <summary>
        /// Evaluates the conditional profile Q at a fixed value of parameter i,
        /// keeping all other parameters at their estimated values.
        /// </summary>
        /// <param name="paramIndex">The index of the parameter to vary.</param>
        /// <param name="fixedValue">The value to set for the parameter.</param>
        /// <returns>The Q value with parameter i set to the given value and all others at θ̂.</returns>
        private double ProfileQConditionalAtPoint(int paramIndex, double fixedValue)
        {
            var parms = new double[NumberOfParameters];
            BestParameterSet.Values.CopyTo(parms, 0);
            parms[paramIndex] = fixedValue;
            return Q(parms);
        }

        /// <summary>
        /// Finds where the profile Q crosses a threshold on one side of the estimate,
        /// using Brent root-finding.
        /// </summary>
        /// <param name="paramIndex">The parameter index.</param>
        /// <param name="threshold">The Q threshold to find.</param>
        /// <param name="lower">If true, searches below θ̂_i; if false, searches above.</param>
        /// <param name="trueProfile">If true, uses true profiling; if false, uses conditional profiling.</param>
        /// <returns>The parameter value where the profile Q equals the threshold.</returns>
        private double ProfileQFindBound(int paramIndex, double threshold, bool lower, bool trueProfile)
        {
            double thetaHat = BestParameterSet.Values[paramIndex];
            double lb = LowerBounds[paramIndex];
            double ub = UpperBounds[paramIndex];

            double searchLo, searchHi;
            if (lower)
            {
                searchLo = lb;
                searchHi = thetaHat;
            }
            else
            {
                searchLo = thetaHat;
                searchHi = ub;
            }

            // Check that the bound actually exceeds the threshold; if not, the CI extends to the bound
            double qAtBound = trueProfile
                ? ProfileQTrueAtPoint(paramIndex, lower ? searchLo : searchHi)
                : ProfileQConditionalAtPoint(paramIndex, lower ? searchLo : searchHi);

            if (qAtBound < threshold)
            {
                return lower ? lb : ub;
            }

            try
            {
                return Brent.Solve(x =>
                {
                    double qVal = trueProfile
                        ? ProfileQTrueAtPoint(paramIndex, x)
                        : ProfileQConditionalAtPoint(paramIndex, x);
                    return qVal - threshold;
                }, searchLo, searchHi, tolerance: 1e-6, reportFailure: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Profile Q Brent root-finding failed for param {paramIndex} ({(lower ? "lower" : "upper")}): {ex.Message}");
                return lower ? lb : ub;
            }
        }

        #endregion

        #region Influence Diagnostics

        /// <summary>
        /// Returns the pointwise influence of each observation on parameter estimates (DFBETAS-like diagnostic).
        /// </summary>
        /// <returns>
        /// A matrix where element [i,j] represents the scaled influence of observation i on parameter j.
        /// Values greater than 2/√n are often considered influential.
        /// </returns>
        /// <remarks>
        /// <para>
        /// For each observation i, the influence on parameter j is computed as:
        /// IF_ij = (Σ · D'W g_i)_j / SE_j
        /// </para>
        /// <para>
        /// where g_i is observation i's moment condition vector, D is the Jacobian, W is the weighting matrix,
        /// and Σ is the parameter covariance matrix.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated or pointwise moment conditions are not available.</exception>
        public double[,] GetObservationInfluence()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (PointwiseMomentConditions == null)
                throw new InvalidOperationException("Observation influence requires pointwise moment conditions. Provide a PointwiseMomentConditionFunction.");

            // Get per-observation moment conditions [n x q]
            double[,] gi = PointwiseMomentConditions(BestParameterSet.Values);
            int n = gi.GetLength(0);
            int q = gi.GetLength(1);

            // Compute D'W and penalized bread inverse: B⁻¹ = (D'WD + H)⁻¹
            var D = GetJacobian(BestParameterSet.Values);
            var DT = D.Transpose();
            var DtW = DT * W!;
            var bread = DtW * D + GetPenaltyHessian(BestParameterSet.Values);
            Matrix breadInv;
            try
            {
                breadInv = bread.Inverse();
            }
            catch
            {
                bread = Numerics.Mathematics.LinearAlgebra.MatrixRegularization.MakeSymmetricPositiveDefinite(bread);
                breadInv = bread.Inverse();
            }

            // Get covariance and standard errors
            var sigma = Sigma ?? GetCovariance(BestParameterSet.Values, true);
            var se = new double[NumberOfParameters];
            for (int j = 0; j < NumberOfParameters; j++)
                se[j] = Math.Sqrt(Math.Max(0, sigma[j, j]));

            var influence = new double[n, NumberOfParameters];

            for (int i = 0; i < n; i++)
            {
                // DtWg_i = D'W g_i [p x 1]
                var DtWg = new double[NumberOfParameters];
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < q; k++)
                        sum += DtW[j, k] * gi[i, k];
                    DtWg[j] = sum;
                }

                // psi_i = B⁻¹ · D'W g_i [p x 1] — includes penalty Hessian in bread
                var psi = new double[NumberOfParameters];
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < NumberOfParameters; k++)
                        sum += breadInv[j, k] * DtWg[k];
                    psi[j] = sum;
                }

                // IF_i = Sigma * psi_i, scaled by SE
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double inflJ = 0;
                    for (int k = 0; k < NumberOfParameters; k++)
                        inflJ += sigma[j, k] * psi[k];
                    influence[i, j] = se[j] > 0 ? inflJ / se[j] : 0;
                }
            }

            return influence;
        }

        /// <summary>
        /// Returns Cook's distance-like measure for each observation.
        /// </summary>
        /// <returns>
        /// An array of influence measures, one per observation.
        /// Larger values indicate more influential observations.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Cook's distance for GMM: D_i = ψ_i' Σ ψ_i / p, where ψ_i = D'W g_i(θ̂).
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated or pointwise moment conditions are not available.</exception>
        public double[] GetCooksDistance()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (PointwiseMomentConditions == null)
                throw new InvalidOperationException("Cook's distance requires pointwise moment conditions. Provide a PointwiseMomentConditionFunction.");

            double[,] gi = PointwiseMomentConditions(BestParameterSet.Values);
            int n = gi.GetLength(0);
            int q = gi.GetLength(1);

            // Compute D'W and penalized bread inverse: B⁻¹ = (D'WD + H)⁻¹
            var D = GetJacobian(BestParameterSet.Values);
            var DT = D.Transpose();
            var DtW = DT * W!;
            var bread = DtW * D + GetPenaltyHessian(BestParameterSet.Values);
            Matrix breadInv;
            try
            {
                breadInv = bread.Inverse();
            }
            catch
            {
                bread = Numerics.Mathematics.LinearAlgebra.MatrixRegularization.MakeSymmetricPositiveDefinite(bread);
                breadInv = bread.Inverse();
            }

            var sigma = Sigma ?? GetCovariance(BestParameterSet.Values, true);

            var cooksD = new double[n];

            for (int i = 0; i < n; i++)
            {
                // DtWg_i = D'W g_i [p x 1]
                var DtWg = new double[NumberOfParameters];
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < q; k++)
                        sum += DtW[j, k] * gi[i, k];
                    DtWg[j] = sum;
                }

                // psi_i = B⁻¹ · D'W g_i [p x 1] — includes penalty Hessian in bread
                var psi = new double[NumberOfParameters];
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < NumberOfParameters; k++)
                        sum += breadInv[j, k] * DtWg[k];
                    psi[j] = sum;
                }

                // D_i = psi_i' Sigma psi_i / p
                double quadForm = 0;
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double tmp = 0;
                    for (int k = 0; k < NumberOfParameters; k++)
                        tmp += sigma[j, k] * psi[k];
                    quadForm += psi[j] * tmp;
                }
                cooksD[i] = quadForm / NumberOfParameters;
            }

            return cooksD;
        }

        /// <summary>
        /// Returns the legacy PSIS-shaped compatibility representation of GMM Cook-like influence.
        /// </summary>
        /// <returns>
        /// A legacy <see cref="InfluenceDiagnostics"/> object with Cook-like values mapped to its
        /// Pareto-k field. New code must not interpret its categories or summaries as PSIS diagnostics.
        /// </returns>
        /// <remarks>
        /// This method is retained for source and binary compatibility. Use
        /// <see cref="GetLeverageDiagnostics"/> for labeled GMM diagnostics or
        /// <see cref="GetCooksDistance"/> for the raw Cook-like values.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated or pointwise moment conditions are not available.</exception>
        [Obsolete("GMM Cook-like influence is not a Pareto-k diagnostic. Use GetLeverageDiagnostics() for labeled diagnostics or GetCooksDistance() for raw Cook-like values.")]
        public InfluenceDiagnostics GetInfluenceDiagnostics()
        {
            // Try to get aggregated data component metadata and row mapping
            List<DataComponent>? dataComponents = null;
            int[]? rowMap = null;
            if (Model is Bulletin17CDistribution b17c && b17c.DataFrame != null)
            {
                dataComponents = BuildDataComponents(b17c.DataFrame);
                var nRows = b17c.DataFrame.TotalRecordLength();
                rowMap = BuildRowToComponentMapping(b17c.DataFrame, nRows);
            }

            if (dataComponents != null && rowMap != null)
            {
                // Compute aggregated Cook's distance by summing psi vectors per component
                // BEFORE computing the quadratic form (quadratic scaling with count).
                if (!IsEstimated || PointwiseMomentConditions == null)
                    throw new InvalidOperationException("The model has not been estimated.");

                double[,] gi = PointwiseMomentConditions(BestParameterSet.Values);
                int n = gi.GetLength(0);
                int qm = gi.GetLength(1);
                int pm = NumberOfParameters;
                int nComponents = dataComponents.Count;

                var D = GetJacobian(BestParameterSet.Values);
                var DtW = D.Transpose() * W!;
                var bread = DtW * D + GetPenaltyHessian(BestParameterSet.Values);
                Matrix breadInv;
                try { breadInv = bread.Inverse(); }
                catch
                {
                    bread = Numerics.Mathematics.LinearAlgebra.MatrixRegularization.MakeSymmetricPositiveDefinite(bread);
                    breadInv = bread.Inverse();
                }
                var sigma = Sigma ?? GetCovariance(BestParameterSet.Values, true);

                // Sum psi vectors per component (psi_i = B⁻¹ D'W g_i)
                var aggPsi = new double[nComponents][];
                for (int ci = 0; ci < nComponents; ci++)
                    aggPsi[ci] = new double[pm];

                for (int i = 0; i < n; i++)
                {
                    var DtWg = new double[pm];
                    for (int j = 0; j < pm; j++)
                        for (int k = 0; k < qm; k++)
                            DtWg[j] += DtW[j, k] * gi[i, k];

                    var psi = new double[pm];
                    for (int j = 0; j < pm; j++)
                        for (int k = 0; k < pm; k++)
                            psi[j] += breadInv[j, k] * DtWg[k];

                    int ci = rowMap[i];
                    for (int j = 0; j < pm; j++)
                        aggPsi[ci][j] += psi[j];
                }

                // Compute Cook's D on summed psi vectors
                var observations = new ObservationInfluence[nComponents];
                for (int ci = 0; ci < nComponents; ci++)
                {
                    var v = aggPsi[ci];
                    double quadForm = 0;
                    for (int j = 0; j < pm; j++)
                    {
                        double tmp = 0;
                        for (int k = 0; k < pm; k++)
                            tmp += sigma[j, k] * v[k];
                        quadForm += v[j] * tmp;
                    }
                    double cookD = quadForm / pm;

                    var dc = dataComponents[ci];
                    observations[ci] = new ObservationInfluence(
                        index: ci,
                        paretoK: cookD,
                        elpdLoo: double.NaN,
                        value: dc.Value,
                        dataType: dc.Type,
                        count: dc.Count,
                        name: dc.Name
                    );
                }
                return new InfluenceDiagnostics(observations);
            }

            // Fallback: no component metadata, use raw per-row Cook's distance
            var cooksD = GetCooksDistance();
            var rawObservations = new ObservationInfluence[cooksD.Length];
            for (int i = 0; i < cooksD.Length; i++)
            {
                rawObservations[i] = new ObservationInfluence(
                    index: i,
                    paretoK: cooksD[i],
                    elpdLoo: double.NaN
                );
            }
            return new InfluenceDiagnostics(rawObservations);
        }

        /// <summary>
        /// Builds aggregated data component metadata from a DataFrame for influence diagnostic labels.
        /// Mirrors the observation ordering used in the moment condition function
        /// (low outliers, non-outlier exact, uncertain, interval, threshold) but returns
        /// ONE entry per threshold group (aggregated, like the Bayesian path).
        /// Use <see cref="BuildRowToComponentMapping"/> to map expanded gi rows to these entries.
        /// </summary>
        /// <param name="dataFrame">The DataFrame containing the observation series.</param>
        /// <returns>A list of aggregated <see cref="DataComponent"/> entries for display.</returns>
        private static List<DataComponent> BuildDataComponents(DataFrame dataFrame)
        {
            var result = new List<DataComponent>();
            int idx = 0;

            // 1. Low outliers — PointwiseMomentConditionsImpl groups them first
            foreach (ExactData data in dataFrame.ExactSeries)
            {
                if (data.IsLowOutlier)
                    result.Add(new DataComponent(idx++, 0, data.Value, DataComponentType.LeftCensored, 1, data.Index.ToString()));
            }

            // 2. Non-outlier exact data
            foreach (ExactData data in dataFrame.ExactSeries)
            {
                if (!data.IsLowOutlier)
                    result.Add(new DataComponent(idx++, 0, data.Value, DataComponentType.Exact, 1, data.Index.ToString()));
            }

            // 3. Uncertain data
            for (int i = 0; i < dataFrame.UncertainSeries.Count; i++)
            {
                var data = dataFrame.UncertainSeries[i];
                result.Add(new DataComponent(idx++, 0, data.Value, DataComponentType.Uncertain, 1, data.Index.ToString()));
            }

            // 4. Interval data
            for (int i = 0; i < dataFrame.IntervalSeries.Count; i++)
            {
                var data = (IntervalData)dataFrame.IntervalSeries[i];
                result.Add(new DataComponent(idx++, 0, data.Value, DataComponentType.Interval, 1, data.Index.ToString()));
            }

            // 5. Threshold data — ONE aggregated entry per group (mirrors Bayesian path)
            foreach (ThresholdData data in dataFrame.ThresholdSeries)
            {
                int totalCount = data.NumberBelow + data.NumberAbove;
                result.Add(new DataComponent(idx++, 0, data.Value, DataComponentType.LeftCensored,
                    totalCount, $"{data.StartIndex}-{data.EndIndex}"));
            }

            return result;
        }

        /// <summary>
        /// Builds a mapping from expanded gi matrix rows to aggregated component indices.
        /// Mirrors the exact row ordering of <see cref="Bulletin17CDistribution.PointwiseMomentConditionsImpl"/>:
        /// low outliers (1:1), non-outlier exact (1:1), uncertain (1:1), interval (1:1),
        /// threshold (NumberBelow+NumberAbove rows → single component index per group).
        /// </summary>
        /// <param name="dataFrame">The DataFrame containing the observation series.</param>
        /// <param name="nRows">The total number of rows in the gi matrix (TotalRecordLength).</param>
        /// <returns>An array of length <paramref name="nRows"/> mapping each row to its component index.</returns>
        private static int[] BuildRowToComponentMapping(DataFrame dataFrame, int nRows)
        {
            var rowMap = new int[nRows];
            int row = 0;
            int componentIdx = 0;

            // 1. Low outliers — one row per outlier, 1:1 mapping
            foreach (ExactData data in dataFrame.ExactSeries)
            {
                if (data.IsLowOutlier)
                    rowMap[row++] = componentIdx++;
            }

            // 2. Non-outlier exact data — 1:1
            foreach (ExactData data in dataFrame.ExactSeries)
            {
                if (!data.IsLowOutlier)
                    rowMap[row++] = componentIdx++;
            }

            // 3. Uncertain data — 1:1
            for (int i = 0; i < dataFrame.UncertainSeries.Count; i++)
                rowMap[row++] = componentIdx++;

            // 4. Interval data — 1:1
            for (int i = 0; i < dataFrame.IntervalSeries.Count; i++)
                rowMap[row++] = componentIdx++;

            // 5. Threshold data — all rows for one group map to same component index
            foreach (ThresholdData data in dataFrame.ThresholdSeries)
            {
                for (int j = 0; j < data.NumberBelow + data.NumberAbove; j++)
                    rowMap[row++] = componentIdx;
                componentIdx++;
            }

            return rowMap;
        }

        /// <summary>
        /// Returns the legacy PSIS-shaped compatibility representation of GMM Cook-like influence with labels.
        /// </summary>
        /// <param name="dataComponents">Data component metadata for observation labels (type, name, value).</param>
        /// <returns>
        /// A legacy <see cref="InfluenceDiagnostics"/> object with Cook-like values mapped to its
        /// Pareto-k field. New code must not interpret its categories or summaries as PSIS diagnostics.
        /// </returns>
        /// <remarks>
        /// This overload is retained for source and binary compatibility. Use
        /// <see cref="GetLeverageDiagnostics"/> for labeled GMM diagnostics or
        /// <see cref="GetCooksDistance"/> for the raw Cook-like values.
        /// </remarks>
        [Obsolete("GMM Cook-like influence is not a Pareto-k diagnostic. Use GetLeverageDiagnostics() for labeled diagnostics or GetCooksDistance() for raw Cook-like values.")]
        public InfluenceDiagnostics GetInfluenceDiagnostics(IList<DataComponent> dataComponents)
        {
            var cooksD = GetCooksDistance();
            int n = Math.Min(cooksD.Length, dataComponents.Count);
            var observations = new ObservationInfluence[n];
            for (int i = 0; i < n; i++)
            {
                var dc = dataComponents[i];
                observations[i] = new ObservationInfluence(
                    index: i,
                    paretoK: cooksD[i],
                    elpdLoo: double.NaN,
                    value: dc.Value,
                    dataType: dc.Type,
                    count: dc.Count,
                    name: dc.Name
                );
            }
            return new InfluenceDiagnostics(observations);
        }

        /// <summary>
        /// Computes leverage diagnostics for the GMM estimator, analogous to the Bayesian leverage
        /// computation in <see cref="Diagnostics.LeverageDiagnostics"/>. Returns the same
        /// <see cref="LeverageDiagnostics"/> type so the UI can display combined fit and variance
        /// influence consistently across both Bayesian and GMM analyses.
        /// </summary>
        /// <returns>A <see cref="LeverageDiagnostics"/> object with per-observation leverages and percentages.</returns>
        /// <remarks>
        /// <para>
        /// Leverage is decomposed into two components: ℓ_i = FitInfluence_i + VarianceInfluence_i.
        /// </para>
        /// <para>
        /// <b>Fit Influence</b> (Cook's Distance) measures how much removing observation i shifts θ̂:
        ///   FitInfluence_i = DtWg_i' · fullSigma · DtWg_i / (n²·p)
        /// where DtWg_i = D'W g_i, fullSigma = [∂²Q/∂θ²]⁻¹, and the n² denominator arises because
        /// each observation's score is D'Wg_i/n (g is a sample mean, so each obs contributes 1/n).
        /// </para>
        /// <para>
        /// <b>Variance Influence</b> measures how much precision observation i provides:
        ///   VarianceInfluence_i = |DtWg_i' · breadInv · DtWg_i| / (n·p)
        /// where breadInv = (D'WD + H)⁻¹. The n·p denominator normalizes so that total observation
        /// variance influence ≈ (p − tr(breadInv·H))/p, the data information fraction.
        /// </para>
        /// <para>
        /// Penalty influences use the generalized variance (determinant) method for variance
        /// and grad' · fullSigma · grad / p for fit (no n scaling — penalties enter Q directly).
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the model has not been estimated or pointwise moment conditions are not available.
        /// </exception>
        public LeverageDiagnostics GetLeverageDiagnostics()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (PointwiseMomentConditions == null)
                throw new InvalidOperationException("Leverage diagnostics require pointwise moment conditions.");

            int p = NumberOfParameters;
            int q = NumberOfMomentConditions;

            // 1. Get per-observation moment conditions g_i [n x q]
            double[,] gi = PointwiseMomentConditions(BestParameterSet.Values);
            int n = gi.GetLength(0);

            // 2. Compute penalized bread: B = D'WD + H
            var D = GetJacobian(BestParameterSet.Values);
            var DT = D.Transpose();
            var DtW = DT * W!;
            var bread = DtW * D + GetPenaltyHessian(BestParameterSet.Values);

            Matrix breadInv;
            try
            {
                breadInv = bread.Inverse();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bread inversion failed, attempting regularization: {ex.Message}");
                try
                {
                    bread = Numerics.Mathematics.LinearAlgebra.MatrixRegularization.MakeSymmetricPositiveDefinite(bread);
                    breadInv = bread.Inverse();
                }
                catch (Exception regEx)
                {
                    Debug.WriteLine($"Bread regularization also failed in GetLeverageDiagnostics: {regEx.Message}");
                    return new LeverageDiagnostics();
                }
            }

            // 3. Compute the Hessian inverse for Cook's Distance using one consistent
            //    half-quadratic diagnostic objective: (1/2)g'Wg + penalty.
            //    Q intentionally retains the conventional unhalved g'Wg form when no
            //    penalty is present. The diagnostic score and bread equations use the
            //    half-quadratic convention in both cases, so their numerical Hessian must
            //    not change scale merely because a penalty was enabled.
            Func<double[], double> negDiagnosticObjective = parameters =>
            {
                var meanMoments = GetG(parameters);
                double diagnosticObjective = 0.5d * meanMoments.Multiply(W!).Multiply(meanMoments).Sum();
                if (PenaltyFunction != null)
                    diagnosticObjective += PenaltyFunction(parameters);

                return Tools.IsFinite(diagnosticObjective)
                    ? -diagnosticObjective
                    : double.MinValue;
            };
            var fullHess = LeverageDiagnostics.ComputeNumericalHessianPublic(
                negDiagnosticObjective, BestParameterSet.Values, p);
            Matrix fullNegHess = fullHess * -1d;
            Matrix fullSigma;
            try
            {
                fullSigma = fullNegHess.Inverse();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Full Hessian inversion failed, attempting regularization: {ex.Message}");
                fullSigma = MatrixRegularization.MakeSymmetricPositiveDefinite(fullNegHess).Inverse();
            }

            // 4. Get aggregated data component metadata and row-to-component mapping.
            //    BuildDataComponents returns ONE entry per threshold group (aggregated).
            //    BuildRowToComponentMapping maps each expanded gi row to its component index.
            List<DataComponent>? dataComponents = null;
            int[]? rowMap = null;
            if (Model is Bulletin17CDistribution b17c && b17c.DataFrame != null)
            {
                dataComponents = BuildDataComponents(b17c.DataFrame);
                rowMap = BuildRowToComponentMapping(b17c.DataFrame, n);
            }

            // 5. Compute per-component Cook's Distance and variance influence.
            //    For threshold data, multiple expanded rows in gi share the same component.
            //    We SUM the DtWg vectors per component FIRST, then compute the quadratic forms
            //    on the summed vectors. This gives the correct group-removal Cook's distance:
            //
            //    For a threshold with 50 identical rows, the combined score is 50×DtWg.
            //    Cook's D = (50·DtWg)' Σ (50·DtWg) / (nComp²·p) = 2500 × DtWg'ΣDtWg / (nComp²·p)
            //
            //    Summing per-row scalars would only give 50 × DtWg'ΣDtWg (linear, not quadratic).
            //    The quadratic scaling matches the Bayesian path where the threshold group's
            //    log-likelihood ℓ = 50·log F(t) + 10·log(1-F(t)) yields a score ∝ count.
            int nComponents = dataComponents?.Count ?? n;
            var aggFitInfluence = new double[nComponents];
            var aggVarianceInfluence = new double[nComponents];

            // Phase 1: Accumulate DtWg vectors per component
            var aggDtWg = new double[nComponents][];
            for (int ci = 0; ci < nComponents; ci++)
                aggDtWg[ci] = new double[p];

            for (int i = 0; i < n; i++)
            {
                // DtWg_i = D'W · g_i  [p x 1]
                double[] DtWg = new double[p];
                for (int j = 0; j < p; j++)
                    for (int k = 0; k < q; k++)
                        DtWg[j] += DtW[j, k] * gi[i, k];

                int ci = rowMap != null ? rowMap[i] : i;
                for (int j = 0; j < p; j++)
                    aggDtWg[ci][j] += DtWg[j];
            }

            // Phase 2: Compute quadratic forms on the SUMMED DtWg vectors
            for (int ci = 0; ci < nComponents; ci++)
            {
                var v = aggDtWg[ci];

                // Variance influence: |v' breadInv v| / (n · p)
                // Uses n (expanded row count) to keep observations on the same scale as penalties.
                // Threshold amplification comes from the quadratic sum: (50×DtWg)² = 2500× single.
                double rawLeverage = 0;
                for (int j = 0; j < p; j++)
                {
                    double tmp = 0;
                    for (int k = 0; k < p; k++)
                        tmp += breadInv[j, k] * v[k];
                    rawLeverage += v[j] * tmp;
                }
                aggVarianceInfluence[ci] = Math.Abs(rawLeverage) / ((double)n * p);

                // Fit influence (Cook's D): |v' fullSigma v| / (n² · p)
                double fitInfluence = 0;
                for (int j = 0; j < p; j++)
                {
                    double tmp = 0;
                    for (int k = 0; k < p; k++)
                        tmp += fullSigma[j, k] * v[k];
                    fitInfluence += v[j] * tmp;
                }
                aggFitInfluence[ci] = Math.Abs(fitInfluence) / ((double)n * n * p);
            }

            // Build final ObservationLeverage array from aggregated values
            var observations = new LeverageDiagnostics.ObservationLeverage[nComponents];
            for (int i = 0; i < nComponents; i++)
            {
                double leverage = aggFitInfluence[i] + aggVarianceInfluence[i];
                double value = 0;
                var dataType = DataComponentType.Exact;
                int count = 1;
                string? name = null;
                if (dataComponents != null && i < dataComponents.Count)
                {
                    value = dataComponents[i].Value;
                    dataType = dataComponents[i].Type;
                    count = dataComponents[i].Count;
                    name = dataComponents[i].Name;
                }
                observations[i] = new LeverageDiagnostics.ObservationLeverage(
                    i, leverage, 0, aggFitInfluence[i], aggVarianceInfluence[i], 0, 0, value, dataType, count, name);
            }

            // 6. Compute per-penalty variance influence via generalized variance (determinant).
            //    Uses -Q(θ) as the log-likelihood equivalent (Q is minimized, so -Q is maximized).
            //    For "without penalty k": -Q(θ) + penalty_k(θ) = -(Q - penalty_k).
            //    Fit influence uses score vector quadratic form with fullSigma (Hessian inverse).
            var penaltyComponents = new List<LeverageDiagnostics.PriorComponentLeverage>();

            if (PenaltyFunction != null && Model is Bulletin17CDistribution b17cModel)
            {
                double[] theta = BestParameterSet.Values;
                int nt = b17cModel.DataFrame?.TotalRecordLength() ?? n;

                // Per-parameter penalties
                for (int pi = 0; pi < b17cModel.ParameterPenalties.Count; pi++)
                {
                    var penalty = b17cModel.ParameterPenalties[pi];
                    if (!penalty.Enabled) continue;

                    int capturedPi = pi;
                    Func<double[], double> penaltyFunc = parms =>
                    {
                        var realParams = b17cModel.LinkController.InverseLink(parms);
                        return penalty.Function(realParams[capturedPi], nt);
                    };

                    // GenVar: -Q without this penalty = -(Q - penalty_k) = -Q + penalty_k
                    Func<double[], double> negQWithout = parameters =>
                        negDiagnosticObjective(parameters) + penaltyFunc(parameters);
                    double varInfl = LeverageDiagnostics.ComputeGenVarPublic(negQWithout, fullSigma, theta, p);

                    // Fit (Cook's D): grad' * fullSigma * grad / p
                    // fullSigma = [Hessian(Q)]^{-1} is the GMM analog of J_post^{-1}.
                    var grad = NumericalDiff.ComputeGradient(penaltyFunc, theta, LowerBounds, UpperBounds);
                    double fitInfl = 0;
                    for (int j = 0; j < p; j++)
                    {
                        double tmp = 0;
                        for (int k = 0; k < p; k++)
                            tmp += fullSigma[j, k] * grad[k];
                        fitInfl += grad[j] * tmp;
                    }
                    fitInfl = Math.Abs(fitInfl) / p;

                    penaltyComponents.Add(new LeverageDiagnostics.PriorComponentLeverage(
                        penalty.Name + " Penalty", PriorComponentType.ParameterPrior,
                        fitInfl + varInfl, 0, fitInfl, varInfl, 0, 0));
                }

                // Per-quantile penalties
                for (int qi = 0; qi < b17cModel.QuantilePenalties.Count; qi++)
                {
                    var penalty = b17cModel.QuantilePenalties[qi];
                    if (!penalty.Enabled) continue;

                    Func<double[], double> penaltyFunc = parms =>
                    {
                        var realParams = b17cModel.LinkController.InverseLink(parms);
                        var dist = b17cModel.Distribution.Clone();
                        dist.SetParameters(realParams);
                        return penalty.Function(dist.InverseCDF(1 - penalty.AEP), nt);
                    };

                    Func<double[], double> negQWithout = parameters =>
                        negDiagnosticObjective(parameters) + penaltyFunc(parameters);
                    double varInfl = LeverageDiagnostics.ComputeGenVarPublic(negQWithout, fullSigma, theta, p);

                    // Fit (Cook's D): grad' * fullSigma * grad / p
                    var grad = NumericalDiff.ComputeGradient(penaltyFunc, theta, LowerBounds, UpperBounds);
                    double fitInfl = 0;
                    for (int j = 0; j < p; j++)
                    {
                        double tmp = 0;
                        for (int k = 0; k < p; k++)
                            tmp += fullSigma[j, k] * grad[k];
                        fitInfl += grad[j] * tmp;
                    }
                    fitInfl = Math.Abs(fitInfl) / p;

                    penaltyComponents.Add(new LeverageDiagnostics.PriorComponentLeverage(
                        $"Quantile Penalty (AEP={penalty.AEP})", PriorComponentType.QuantilePrior,
                        fitInfl + varInfl, 0, fitInfl, varInfl, 0, 0));
                }
            }

            // 7. Build result
            var result = new LeverageDiagnostics(
                observations, penaltyComponents.ToArray(), p);

            return result;
        }

        #endregion

        #region Optimization

        /// <summary>
        /// Configures the optimizer based on the selected optimization method.
        /// </summary>
        /// <param name="optimizerMethod">The optimizer method to configure for this pass.</param>
        private void SetUpOptimizer(OptimizationMethod optimizerMethod)
        {
            if (optimizerMethod == OptimizationMethod.Brent)
            {
                Optimizer = new BrentSearch(x => { return Q(new[] { x }); }, LowerBounds[0], UpperBounds[0]);
            }
            else if (optimizerMethod == OptimizationMethod.BFGS)
            {
                Optimizer = new BFGS(Q, NumberOfParameters, InitialValues, LowerBounds, UpperBounds, x => { return GetGradient(x).Array; });
            }
            else if (optimizerMethod == OptimizationMethod.NelderMead)
            {
                Optimizer = new NelderMead(Q, NumberOfParameters, InitialValues, LowerBounds, UpperBounds) { EnableStartPointProbe = true };
            }
            else if (optimizerMethod == OptimizationMethod.Powell)
            {
                Optimizer = new Powell(Q, NumberOfParameters, InitialValues, LowerBounds, UpperBounds);
            }
            else if (optimizerMethod == OptimizationMethod.DifferentialEvolution)
            {
                Optimizer = new DifferentialEvolution(Q, NumberOfParameters, LowerBounds, UpperBounds);
            }
            else if (optimizerMethod == OptimizationMethod.MultilevelSingleLinkage)
            {
                Optimizer = new MLSL(Q, NumberOfParameters, InitialValues, LowerBounds, UpperBounds, LocalMethod.NelderMead);
            }

            Optimizer.MaxFunctionEvaluations = MaxFunctionEvaluations;
            Optimizer.AbsoluteTolerance = AbsoluteTolerance;
            Optimizer.RelativeTolerance = RelativeTolerance;
            Optimizer.ReportFailure = false;
            Optimizer.RecordTraces = false;
            Optimizer.ComputeHessian = false;
        }

        /// <summary>
        /// Runs the primary optimizer with optional BFGS-to-NelderMead fallback.
        /// </summary>
        /// <param name="enableStartPointProbe">Whether to enable NelderMead start-point probing. Default = true for first iteration.</param>
        /// <returns>
        /// <c>true</c> when the optimizer produces a usable best parameter set without reporting
        /// <see cref="OptimizationStatus.Failure"/>; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// GMM keeps the best finite optimizer point from every pass. Hitting maximum function
        /// evaluations or maximum iterations is not treated as a failed estimate when a finite
        /// best point is available; callers can inspect <see cref="Status"/> and
        /// <see cref="ConvergedWithinTolerance"/> when they need strict convergence.
        /// </remarks>
        private bool MinimizeWithFallback(bool enableStartPointProbe = true)
        {
            var primaryMethod = _useNelderMeadForRemainingIterations
                ? OptimizationMethod.NelderMead
                : OptimizerMethod;

            // Run primary optimizer.
            SetUpOptimizer(primaryMethod);
            if (Optimizer is NelderMead nm)
                nm.EnableStartPointProbe = enableStartPointProbe;

            try
            {
                Optimizer.Minimize();
                Status = Optimizer.Status;
            }
            catch (Exception ex)
            {
                Status = OptimizationStatus.Failure;
                Debug.WriteLine($"Primary optimizer ({primaryMethod}) threw exception: {ex.Message}");
            }

            TotalFunctionEvaluations += Optimizer.FunctionEvaluations;

            if (TryCaptureBestParameterSet(Optimizer) && IsNonFailureTermination(Status))
                return true;

            // Fallback only if enabled and primary was BFGS.
            if (!UseFallbackOptimizer || primaryMethod != OptimizationMethod.BFGS)
                return false;

            Debug.WriteLine("BFGS failed, falling back to NelderMead with start-point probe.");
            _useNelderMeadForRemainingIterations = true;
            _optimizerFallbackCount++;

            // Use the BFGS best point if it found a finite candidate, otherwise use InitialValues.
            var fallbackInitials = HasUsableBestParameterSet(Optimizer)
                ? Optimizer.BestParameterSet.Values.ToArray()
                : InitialValues.ToArray();

            var fallback = new NelderMead(Q, NumberOfParameters, fallbackInitials, LowerBounds, UpperBounds)
            {
                EnableStartPointProbe = enableStartPointProbe,
                MaxFunctionEvaluations = MaxFunctionEvaluations,
                AbsoluteTolerance = AbsoluteTolerance,
                RelativeTolerance = RelativeTolerance,
                ReportFailure = false,
                RecordTraces = false,
                ComputeHessian = false
            };

            try
            {
                fallback.Minimize();
                Status = fallback.Status;
            }
            catch (Exception ex)
            {
                Status = OptimizationStatus.Failure;
                Debug.WriteLine($"NelderMead fallback threw exception: {ex.Message}");
                return false;
            }

            TotalFunctionEvaluations += fallback.FunctionEvaluations;
            Optimizer = fallback;

            return TryCaptureBestParameterSet(fallback) && IsNonFailureTermination(Status);
        }

        /// <summary>
        /// Copies an optimizer's best finite parameter set onto the GMM result state.
        /// </summary>
        /// <param name="optimizer">The optimizer whose best parameter set should be captured.</param>
        /// <returns><c>true</c> when a finite, correctly sized parameter set was captured; otherwise, <c>false</c>.</returns>
        private bool TryCaptureBestParameterSet(Optimizer optimizer)
        {
            if (!HasUsableBestParameterSet(optimizer))
                return false;

            BestParameterSet = optimizer.BestParameterSet.Clone();
            return true;
        }

        /// <summary>
        /// Determines whether an optimizer has a finite best parameter set that matches this GMM problem.
        /// </summary>
        /// <param name="optimizer">The optimizer to inspect.</param>
        /// <returns><c>true</c> when the optimizer has usable parameter values and fitness; otherwise, <c>false</c>.</returns>
        private bool HasUsableBestParameterSet(Optimizer optimizer)
        {
            if (optimizer?.BestParameterSet.Values == null || optimizer.BestParameterSet.Values.Length != NumberOfParameters)
                return false;

            if (!Tools.IsFinite(optimizer.BestParameterSet.Fitness) || optimizer.BestParameterSet.Fitness >= double.MaxValue)
                return false;

            foreach (double value in optimizer.BestParameterSet.Values)
            {
                if (!Tools.IsFinite(value))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether an optimizer termination status can supply a usable best-effort result.
        /// </summary>
        /// <param name="status">The optimizer status to inspect.</param>
        /// <returns><c>true</c> when the status is neither <see cref="OptimizationStatus.None"/> nor <see cref="OptimizationStatus.Failure"/>; otherwise, <c>false</c>.</returns>
        private static bool IsNonFailureTermination(OptimizationStatus status)
        {
            return status != OptimizationStatus.None && status != OptimizationStatus.Failure;
        }

        /// <summary>
        /// Set default simulation options.
        /// </summary>
        public void SetDefaultOptions()
        {
            OptimizerMethod = OptimizationMethod.BFGS;
            UseFallbackOptimizer = true;
            EstimationStrategy = GMMEstimationStrategy.Iterative;
            MaxGMMIterations = 100;
            MaxFunctionEvaluations = 2000;
            AbsoluteTolerance = 1E-8;
            RelativeTolerance = 1E-8;
        }

        #endregion

        #region Estimation

        /// <summary>
        /// Perform the One-Step optimization.
        /// </summary>
        private void EstimateOneStep()
        {
            GMMIterations = 1;

            if (!MinimizeWithFallback())
                return;

            BestParameterSet = Optimizer.BestParameterSet.Clone();
        }

        /// <summary>
        /// Perform the Two-Step optimization using the updated weighting matrix.
        /// </summary>
        /// <remarks>
        /// The first optimizer pass records <see cref="GMMIterations"/> as 1 and the second pass
        /// records it as 2. Non-failure optimizer terminations retain the best finite parameter
        /// set even when strict convergence tolerance was not reached.
        /// </remarks>
        private void EstimateTwoStep()
        {
            TotalFunctionEvaluations = 0;

            // Perform the 1st optimization step.
            GMMIterations = 1;
            if (!MinimizeWithFallback())
                return;

            // Update results.
            BestParameterSet = Optimizer.BestParameterSet.Clone();
            S = GetS(BestParameterSet.Values);
            W = S.Inverse();

            // Perform the 2nd optimization step.
            GMMIterations = 2;
            InitialValues = Optimizer.BestParameterSet.Values;
            if (!MinimizeWithFallback())
                return;

            BestParameterSet = Optimizer.BestParameterSet.Clone();
        }

        /// <summary>
        /// Perform the 'iterative method' that iteratively improves the weighting matrix until convergence.
        /// </summary>
        /// <remarks>
        /// <see cref="GMMIterations"/> records the optimizer pass currently being attempted.
        /// Non-failure optimizer terminations retain their best finite parameter set even when
        /// strict convergence tolerance was not reached; <see cref="ConvergedWithinTolerance"/>
        /// preserves the distinction between best-effort termination and tolerance-confirmed convergence.
        /// </remarks>
        private void EstimateIterative()
        {
            ConvergenceHistory.Clear();
            GMMIterations = 1;

            // Perform the 1st optimization step
            if (!MinimizeWithFallback(enableStartPointProbe: true))
                return;

            // Update results
            BestParameterSet = Optimizer.BestParameterSet.Clone();
            S = GetS(BestParameterSet.Values);
            W = S.Inverse();

            InitialValues = BestParameterSet.Values.ToArray();
            double[] oldValues = BestParameterSet.Values.ToArray();
            double oldQ = Q(oldValues);
            ConvergenceHistory.Add(oldQ);

            for (GMMIterations = 2; GMMIterations <= MaxGMMIterations; GMMIterations++)
            {
                // Subsequent iterations: no start-point probe needed.
                if (!MinimizeWithFallback(enableStartPointProbe: false))
                    break;

                // Update results
                BestParameterSet = Optimizer.BestParameterSet.Clone();
                double[] newValues = BestParameterSet.Values.ToArray();
                S = GetS(BestParameterSet.Values);
                W = S.Inverse();

                double newQ = Q(newValues);
                ConvergenceHistory.Add(newQ);

                // Check convergence: absolute parameter distance OR relative objective change
                double distance = Tools.Distance(newValues, oldValues);
                double relChange = Math.Abs(newQ - oldQ) / (Math.Abs(oldQ) + 1e-15);

                if (distance < AbsoluteTolerance || relChange < RelativeTolerance)
                {
                    _convergedWithinTolerance = true;
                    BestParameterSet = Optimizer.BestParameterSet.Clone();
                    return;
                }

                InitialValues = newValues.ToArray();
                oldValues = newValues.ToArray();
                oldQ = newQ;
            }

            if (GMMIterations > MaxGMMIterations)
                GMMIterations = MaxGMMIterations;
        }

        /// <summary>
        /// Estimates the model parameters using the configured GMM estimation strategy.
        /// </summary>
        /// <returns>
        /// <c>true</c> if estimation produced a usable best parameter set; otherwise, <c>false</c>.
        /// A <c>true</c> return does not necessarily imply strict optimizer convergence.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the GMM problem is under-identified without a penalty function.
        /// </exception>
        public bool Estimate()
        {
            IsEstimated = false;
            ResetCovarianceStatus();
            _selectedWeightMomentObjective = double.NaN;
            JStat = double.NaN;
            JStatPval = double.NaN;

            _convergedWithinTolerance = false;
            _useNelderMeadForRemainingIterations = false;
            _optimizerFallbackCount = 0;
            // Reset prior outputs so a failed run can never return a stale solution
            // from an earlier Estimate() call on the same instance.
            Status = OptimizationStatus.None;
            BestParameterSet = new ParameterSet();
            // Validation
            if (IdentificationStatus == GMMIdentificationStatus.UnderIdentified && PenaltyFunction == null)
                throw new InvalidOperationException("The GMM problem is under-identified and cannot be estimated without a penalty function.");

            GMMIterations = 0;
            TotalFunctionEvaluations = 0;
            ConvergenceHistory.Clear();

            // Set up the weighting matrix
            if (W == null)
                W = Matrix.Identity(NumberOfMomentConditions);

            if (EstimationStrategy == GMMEstimationStrategy.OneStep)
                EstimateOneStep();
            else if (EstimationStrategy == GMMEstimationStrategy.TwoStep)
                EstimateTwoStep();
            else if (EstimationStrategy == GMMEstimationStrategy.Iterative)
                EstimateIterative();

            // Check if estimation succeeded
            if (BestParameterSet.Values != null && BestParameterSet.Values.Length > 0)
            {
                double momentObjective = GetUnpenalizedMomentObjective(BestParameterSet.Values, W!);
                _selectedWeightMomentObjective = Tools.IsFinite(momentObjective) && momentObjective >= 0d
                    ? momentObjective
                    : double.NaN;
                IsEstimated = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Post-process estimation results to compute model covariance and optional J-statistic.
        /// </summary>
        /// <param name="useSandwich">If true, uses robust sandwich estimator for covariance. Default = true.</param>
        /// <param name="computeJstat">
        /// If true, computes Hansen's J-statistic for an unpenalized overidentified two-step
        /// or iterative fit. Default = false.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the selected-weight moment objective is unavailable or non-finite.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The statistic is <c>J = n * g(thetaHat)' W g(thetaHat)</c>, using the weighting
        /// matrix selected by the completed fit, and is compared with <c>chi-square(q-p)</c>.
        /// </para>
        /// <para>
        /// A generic fixed-weight one-step fit and a penalized fit do not automatically have
        /// the efficient-weight Hansen chi-square interpretation. Their J-statistic fields
        /// remain <see cref="double.NaN"/>.
        /// </para>
        /// </remarks>
        public void PostProcess(bool useSandwich = true, bool computeJstat = false)
        {
            // Compute covariance
            Sigma = GetCovariance(BestParameterSet.Values, useSandwich);
            if (!computeJstat)
                return;

            JStat = double.NaN;
            JStatPval = double.NaN;
            if (DegreeOfFreedom <= 0 ||
                EstimationStrategy == GMMEstimationStrategy.OneStep ||
                PenaltyFunction != null)
                return;

            if (!Tools.IsFinite(_selectedWeightMomentObjective) || _selectedWeightMomentObjective < 0d)
                throw new InvalidOperationException(
                    "Hansen's J statistic is unavailable because the selected-weight moment objective was not preserved by a completed estimate.");

            JStat = SampleSize * _selectedWeightMomentObjective;
            var chiSquared = new ChiSquared(DegreeOfFreedom);
            JStatPval = 1.0 - chiSquared.CDF(JStat);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Raise property changed event.
        /// </summary>
        /// <param name="propertyName">Name of property that changed.</param>
        protected virtual void RaisePropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Clear results.
        /// </summary>
        public void ClearResults()
        {
            IsEstimated = false;
            Status = OptimizationStatus.None;
            GMMIterations = 0;
            TotalFunctionEvaluations = 0;
            _convergedWithinTolerance = false;
            _useNelderMeadForRemainingIterations = false;
            _optimizerFallbackCount = 0;
            JStat = double.NaN;
            JStatPval = double.NaN;
            _selectedWeightMomentObjective = double.NaN;
            W = null;
            S = null;
            Sigma = null;
            ResetCovarianceStatus();
            BestParameterSet = new ParameterSet();
            ConvergenceHistory.Clear();
        }

        /// <summary>
        /// Validates whether the GMM inputs are valid for estimation.
        /// </summary>
        /// <param name="errors">Output list of error messages.</param>
        /// <returns>True if all validation checks pass; false otherwise.</returns>
        public bool IsValid(out List<string> errors)
        {
            var valid = true;
            errors = new List<string>();

            if (IdentificationStatus == GMMIdentificationStatus.UnderIdentified && PenaltyFunction == null)
            {
                valid = false;
                errors.Add("The GMM problem is under-identified and cannot be estimated.");
            }
            if (MaxGMMIterations < 1 || MaxGMMIterations > 1000)
            {
                valid = false;
                errors.Add("The GMM max iterations must be between 1 and 1,000.");
            }
            if (AbsoluteTolerance < 1E-15)
            {
                valid = false;
                errors.Add("The absolute tolerance must be greater than 1E-15.");
            }
            if (RelativeTolerance < 1E-15)
            {
                valid = false;
                errors.Add("The relative tolerance must be greater than 1E-15.");
            }
            return valid;
        }

        /// <summary>
        /// Clone the GMM class.
        /// </summary>
        /// <returns>A new GeneralizedMethodOfMoments instance with the same configuration.</returns>
        public GeneralizedMethodOfMoments Clone()
        {
            return new GeneralizedMethodOfMoments(MomentConditionFunction,
                                                NumberOfParameters,
                                                NumberOfMomentConditions,
                                                SampleSize,
                                                InitialValues,
                                                LowerBounds,
                                                UpperBounds,
                                                W,
                                                JacobianFunction,
                                                PenaltyFunction,
                                                PointwiseMomentConditions);
        }

        /// <summary>
        /// Returns the GMM estimation as XElement.
        /// </summary>
        /// <returns>An XML element containing the serialized GMM configuration and results.</returns>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(GeneralizedMethodOfMoments));
            // Input
            result.SetAttributeValue(nameof(EstimationStrategy), EstimationStrategy.ToString());
            result.SetAttributeValue(nameof(OptimizerMethod), OptimizerMethod.ToString());
            result.SetAttributeValue(nameof(SampleSize), SampleSize.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(MaxGMMIterations), MaxGMMIterations.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(AbsoluteTolerance), AbsoluteTolerance.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(RelativeTolerance), RelativeTolerance.ToString("G17", CultureInfo.InvariantCulture));

            // Output
            result.SetAttributeValue(nameof(GMMIterations), GMMIterations.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(JStat), JStat.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(ConvergedWithinTolerance), ConvergedWithinTolerance.ToString());
            result.SetAttributeValue(nameof(JStatPval), JStatPval.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Status), Status.ToString());

            // Best parameter set
            var parm = new XElement(nameof(BestParameterSet));
            parm.Add(BestParameterSet.ToXElement());
            result.Add(parm);

            // Output matrices
            if (S != null)
            {
                var s = new XElement(nameof(S));
                s.Add(S.ToXElement());
                result.Add(s);
            }
            if (W != null)
            {
                var w = new XElement(nameof(W));
                w.Add(W.ToXElement());
                result.Add(w);
            }
            if (Sigma != null)
            {
                var covar = new XElement(nameof(Sigma));
                covar.Add(Sigma.ToXElement());
                result.Add(covar);
            }

            return result;
        }

        /// <summary>
        /// Restores estimated results from a previously serialized <see cref="XElement"/>.
        /// The GMM object must already be constructed from a model (providing delegates, bounds, etc.).
        /// This method restores only the output state: BestParameterSet, matrices, and diagnostic scalars.
        /// </summary>
        /// <param name="xElement">The XElement produced by <see cref="ToXElement"/>.</param>
        /// <remarks>
        /// <para>
        /// Call this after constructing the GMM from an <see cref="IGMMModel"/> to restore
        /// persisted estimation results without re-running the optimizer.
        /// </para>
        /// <para>
        /// The model's parameter values are set to the restored BestParameterSet values,
        /// and <c>IsEstimated</c> is set to <c>true</c>.
        /// </para>
        /// <para>
        /// The optional <see cref="ConvergedWithinTolerance"/> attribute restores confirmed
        /// convergence only for iterative runs with at least one comparison pass. Legacy XML
        /// without the attribute is restored conservatively as not confirmed converged.
        /// </para>
        /// </remarks>
        public void RestoreFromXElement(XElement xElement)
        {
            if (xElement == null) return;

            _convergedWithinTolerance = false;
            _selectedWeightMomentObjective = double.NaN;
            JStat = double.NaN;
            JStatPval = double.NaN;
            // Restore configuration attributes
            var stratAttr = xElement.Attribute(nameof(EstimationStrategy));
            if (stratAttr != null && Enum.TryParse(stratAttr.Value, out GMMEstimationStrategy strat))
                EstimationStrategy = strat;

            var optAttr = xElement.Attribute(nameof(OptimizerMethod));
            if (optAttr != null && Enum.TryParse(optAttr.Value, out OptimizationMethod opt))
                OptimizerMethod = opt;

            var maxIterAttr = xElement.Attribute(nameof(MaxGMMIterations));
            if (maxIterAttr != null && int.TryParse(maxIterAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int maxIter))
                MaxGMMIterations = maxIter;

            var absTolAttr = xElement.Attribute(nameof(AbsoluteTolerance));
            if (absTolAttr != null && double.TryParse(absTolAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double absTol))
                AbsoluteTolerance = absTol;

            var relTolAttr = xElement.Attribute(nameof(RelativeTolerance));
            if (relTolAttr != null && double.TryParse(relTolAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double relTol))
                RelativeTolerance = relTol;

            // Restore output scalars
            var gmmIterAttr = xElement.Attribute(nameof(GMMIterations));
            if (gmmIterAttr != null && int.TryParse(gmmIterAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int gmmIter))
                GMMIterations = gmmIter;

            var convergedAttr = xElement.Attribute(nameof(ConvergedWithinTolerance));
            if (convergedAttr != null && bool.TryParse(convergedAttr.Value, out bool converged))
                _convergedWithinTolerance = converged && GMMIterations >= 2 &&
                    EstimationStrategy == GMMEstimationStrategy.Iterative;
            var jStatAttr = xElement.Attribute(nameof(JStat));
            if (jStatAttr != null && double.TryParse(jStatAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double jStat))
                JStat = jStat;

            var jPvalAttr = xElement.Attribute(nameof(JStatPval));
            if (jPvalAttr != null && double.TryParse(jPvalAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double jPval))
                JStatPval = jPval;

            var statusAttr = xElement.Attribute(nameof(Status));
            if (statusAttr != null && Enum.TryParse(statusAttr.Value, out OptimizationStatus status))
                Status = status;

            // Restore BestParameterSet
            var bpsElement = xElement.Element(nameof(BestParameterSet));
            if (bpsElement != null)
            {
                var psElement = bpsElement.Element(nameof(ParameterSet));
                if (psElement != null)
                {
                    BestParameterSet = DeserializeParameterSet(psElement);
                }
            }

            // Restore matrices
            var sElement = xElement.Element(nameof(S));
            if (sElement != null) S = DeserializeMatrix(sElement.Element(nameof(Matrix)));

            var wElement = xElement.Element(nameof(W));
            if (wElement != null) W = DeserializeMatrix(wElement.Element(nameof(Matrix)));

            var sigmaElement = xElement.Element(nameof(Sigma));
            if (sigmaElement != null) Sigma = DeserializeMatrix(sigmaElement.Element(nameof(Matrix)));

            ResetCovarianceStatus();
            if (Sigma != null)
            {
                if (MatrixIsFinite(Sigma) && HasPositiveFiniteDiagonal(Sigma))
                    CovarianceStatus = CovarianceComputationStatus.Available;
                else
                    SetCovarianceFailure("The restored GMM covariance is non-finite or degenerate.");
            }

            // Set model parameter values from best parameter set
            if (BestParameterSet.Values != null && Model != null)
                Model.SetParameterValues(BestParameterSet.Values);

            IsEstimated = true;
        }

        /// <summary>
        /// Deserializes a <see cref="ParameterSet"/> from an XElement.
        /// </summary>
        private static ParameterSet DeserializeParameterSet(XElement xElement)
        {
            var ps = new ParameterSet();
            var valuesAttr = xElement.Attribute(nameof(ParameterSet.Values));
            if (valuesAttr != null)
            {
                ps.Values = valuesAttr.Value.Split('|')
                    .Select(s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0.0)
                    .ToArray();
            }
            var fitnessAttr = xElement.Attribute(nameof(ParameterSet.Fitness));
            if (fitnessAttr != null && double.TryParse(fitnessAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double fit))
                ps.Fitness = fit;
            var weightAttr = xElement.Attribute(nameof(ParameterSet.Weight));
            if (weightAttr != null && double.TryParse(weightAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double wt))
                ps.Weight = wt;
            return ps;
        }

        /// <summary>
        /// Deserializes a <see cref="Matrix"/> from an XElement.
        /// </summary>
        private static Matrix? DeserializeMatrix(XElement? xElement)
        {
            if (xElement == null) return null;
            var rowsAttr = xElement.Attribute(nameof(Matrix.NumberOfRows));
            var colsAttr = xElement.Attribute(nameof(Matrix.NumberOfColumns));
            if (rowsAttr == null || colsAttr == null) return null;
            if (!int.TryParse(rowsAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int rows)) return null;
            if (!int.TryParse(colsAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int cols)) return null;
            var matrix = new Matrix(rows, cols);
            var rowElements = xElement.Elements("row").ToList();
            for (int i = 0; i < Math.Min(rows, rowElements.Count); i++)
            {
                var values = rowElements[i].Value.Split('|');
                for (int j = 0; j < Math.Min(cols, values.Length); j++)
                {
                    if (double.TryParse(values[j], NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                        matrix[i, j] = val;
                }
            }
            return matrix;
        }

        #endregion

    }

}
