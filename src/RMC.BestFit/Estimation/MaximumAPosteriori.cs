using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using Numerics.Mathematics.RootFinding;
using Numerics.Sampling;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using System.Diagnostics;

namespace RMC.BestFit.Estimation
{
    /// <summary>
    /// Estimates model parameters using the method of Maximum A Posteriori (MAP).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>Description:</b>
    ///     This class implements maximum a posteriori estimation by maximizing the full log-likelihood function
    ///     (data likelihood plus prior) using various optimization algorithms. The Hessian matrix is computed for 
    ///     uncertainty quantification. MAP estimation incorporates prior distributions on parameters, providing 
    ///     a Bayesian point estimate.
    /// </para>
    /// </remarks>
    public class MaximumAPosteriori
    {
        private OptimizationMethod _optimizerMethod = OptimizationMethod.DifferentialEvolution;
        private bool _reportFailure = false;
        private bool _computeHessian = true;
        private Matrix? _hessian;

        /// <summary>
        /// Constructs a new maximum a posteriori (MAP) class. 
        /// </summary>
        /// <param name="model">The model to estimate.</param>
        /// <param name="method">Optional. The optimization method. Default = Differential Evolution.</param>
        /// <exception cref="ArgumentNullException">Thrown when model is null.</exception>
        public MaximumAPosteriori(IModel model, OptimizationMethod method = OptimizationMethod.DifferentialEvolution)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model), "Model cannot be null.");
            OptimizerMethod = method;
            SetUpOptimizer();
            IsEstimated = false;
        }

        /// <summary>
        /// Constructs a MAP estimator with an explicit optimizer starting point.
        /// </summary>
        /// <param name="model">The model to estimate.</param>
        /// <param name="method">The optimization method.</param>
        /// <param name="initialValues">The finite in-bounds parameter vector used to initialize a local optimizer.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> or <paramref name="initialValues"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when the initial vector has the wrong dimension or contains a nonfinite or out-of-bounds value.</exception>
        /// <remarks>
        /// This internal construction path lets model-specific estimators, such as mixture EM,
        /// provide a reliable basin for local full-posterior refinement without changing the
        /// public MAP API or the model's current parameter values.
        /// </remarks>
        internal MaximumAPosteriori(
            IModel model,
            OptimizationMethod method,
            IReadOnlyList<double> initialValues)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model), "Model cannot be null.");
            ArgumentNullException.ThrowIfNull(initialValues);
            _optimizerMethod = method;
            SetUpOptimizer(initialValues);
            IsEstimated = false;
        }

        /// <summary>
        /// Gets the model to estimate.
        /// </summary>
        public IModel Model { get; private set; }

        /// <summary>
        /// Gets or sets the optimization method to use for estimating the model parameters. Default = Differential Evolution.
        /// </summary>
        /// <remarks>
        /// Differential Evolution is a global optimization method that works well for complex surfaces with multiple modes.
        /// Changing this property will clear existing results and reinitialize the optimizer.
        /// </remarks>
        public OptimizationMethod OptimizerMethod
        {
            get { return _optimizerMethod; }
            set
            {
                if (_optimizerMethod != value)
                {
                    _optimizerMethod = value;
                    ClearResults();
                    SetUpOptimizer();
                }
            }
        }

        /// <summary>
        /// Gets the optimizer that finds the maximum likelihood solution.
        /// </summary>
        public Optimizer Optimizer { get; private set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether optimizer failures should be reported through exceptions.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>, so most callers can inspect <see cref="Status"/> and the
        /// <c>Estimate</c> return value. Set to <c>true</c> for callers that need the
        /// optimizer to stop immediately on failure conditions such as maximum function evaluations.
        /// </remarks>
        public bool ReportFailure
        {
            get { return _reportFailure; }
            set
            {
                _reportFailure = value;
                if (Optimizer != null)
                    Optimizer.ReportFailure = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to compute the adaptive Hessian after successful estimation.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>true</c> for callers that need covariance, standard error, and influence diagnostics.
        /// Set to <c>false</c> for screening workflows that only need the fitted parameter set and likelihood.
        /// </remarks>
        public bool ComputeHessian
        {
            get { return _computeHessian; }
            set
            {
                _computeHessian = value;
                if (!value)
                    _hessian = null;
                ResetCovarianceStatus();
            }
        }

        /// <summary>
        /// Gets the final <see cref="OptimizationStatus"/> from the most recent estimation run.
        /// </summary>
        /// <remarks>
        /// Captured from the inner <see cref="Optimizer"/>.Status at the end of <c>Estimate</c>
        /// so it remains valid after the transient optimizer is discarded. Returns
        /// <see cref="OptimizationStatus.None"/> before any estimation has run, and after
        /// <see cref="ClearResults"/>. Set to <see cref="OptimizationStatus.Failure"/> if the
        /// optimizer threw an exception.
        /// </remarks>
        public OptimizationStatus Status { get; private set; } = OptimizationStatus.None;
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
        /// Gets the number of model parameters.
        /// </summary>
        public int NumberOfParameters => Model.Parameters.Count;

        /// <summary>
        /// Gets the array of initial parameter values used by the optimizer.
        /// </summary>
        public double[] InitialValues { get; private set; } = null!;

        /// <summary>
        /// Gets the array of lower bounds (inclusive) for each parameter.
        /// </summary>
        public double[] LowerBounds { get; private set; } = null!;

        /// <summary>
        /// Gets the array of upper bounds (inclusive) for each parameter.
        /// </summary>
        public double[] UpperBounds { get; private set; } = null!;

        /// <summary>
        /// Gets a value indicating whether the model has been successfully estimated.
        /// </summary>
        public bool IsEstimated { get; private set; }

        /// <summary>
        /// Gets the optimal parameter set from the estimation. Initialized to an empty
        /// <see cref="ParameterSet"/> so consumers that bypass the <c>IsEstimated</c>
        /// guard get a deterministic empty parameter set rather than an
        /// <see cref="NullReferenceException"/>.
        /// </summary>
        public ParameterSet BestParameterSet { get; private set; } = new ParameterSet();

        /// <summary>
        /// Gets the total number of function evaluations required to estimate the model.
        /// </summary>
        public int TotalFunctionEvaluations { get; private set; }

        /// <summary>
        /// Gets the maximum log-likelihood value (including prior) at the optimal parameters.
        /// </summary>
        public double MaximumLogLikelihood => IsEstimated ? -BestParameterSet.Fitness : double.NaN;

        #region Methods

        /// <summary>
        /// Sets up the optimizer class based on the selected optimization method.
        /// </summary>
        private void SetUpOptimizer(IReadOnlyList<double>? explicitInitialValues = null)
        {
            // Store parameter constraints
            LowerBounds = Model.Parameters.Select(x => x.LowerBound).ToArray();
            UpperBounds = Model.Parameters.Select(x => x.UpperBound).ToArray();
            InitialValues = explicitInitialValues?.ToArray() ??
                Model.Parameters.Select(x => x.Value).ToArray();
            if (explicitInitialValues != null)
                ValidateInitialValues(InitialValues);

            // MAP estimation maximizes the full log-likelihood(data likelihood + prior)
            if (OptimizerMethod == OptimizationMethod.Brent)
            {
                if (NumberOfParameters != 1)
                    throw new InvalidOperationException("Brent method requires exactly one parameter.");
                Optimizer = new BrentSearch(x => Model.LogLikelihood(new[] { x }), LowerBounds[0], UpperBounds[0]);
            }
            else if (OptimizerMethod == OptimizationMethod.BFGS)
            {
                Optimizer = new BFGS(Model.LogLikelihood, NumberOfParameters, InitialValues, LowerBounds, UpperBounds);
            }
            else if (OptimizerMethod == OptimizationMethod.NelderMead)
            {
                Optimizer = new NelderMead(Model.LogLikelihood, NumberOfParameters, InitialValues, LowerBounds, UpperBounds);
            }
            else if (OptimizerMethod == OptimizationMethod.Powell)
            {
                Optimizer = new Powell(Model.LogLikelihood, NumberOfParameters, InitialValues, LowerBounds, UpperBounds);
            }
            else if (OptimizerMethod == OptimizationMethod.DifferentialEvolution)
            {
                Optimizer = new DifferentialEvolution(Model.LogLikelihood, NumberOfParameters, LowerBounds, UpperBounds);
            }
            else if (OptimizerMethod == OptimizationMethod.MultilevelSingleLinkage)
            {
                Optimizer = new MLSL(Model.LogLikelihood, NumberOfParameters, InitialValues, LowerBounds, UpperBounds, LocalMethod.NelderMead);
            }
            else
            {
                throw new NotSupportedException($"Optimization method '{OptimizerMethod}' is not supported.");
            }

            Optimizer.ReportFailure = ReportFailure;
            Optimizer.RecordTraces = false;
            Optimizer.ComputeHessian = false; // This class computes an adaptive Hessian when requested.
        }

        /// <summary>
        /// Validates an optimizer starting point against the current model parameter space.
        /// </summary>
        /// <param name="initialValues">The proposed optimizer starting point.</param>
        /// <exception cref="ArgumentException">Thrown when the vector has the wrong dimension or contains a nonfinite or out-of-bounds value.</exception>
        private void ValidateInitialValues(IReadOnlyList<double> initialValues)
        {
            if (initialValues.Count != NumberOfParameters)
            {
                throw new ArgumentException(
                    "The initial parameter vector must match the model parameter count.",
                    nameof(initialValues));
            }

            for (int index = 0; index < initialValues.Count; index++)
            {
                double value = initialValues[index];
                if (!double.IsFinite(value) || value < LowerBounds[index] || value > UpperBounds[index])
                {
                    throw new ArgumentException(
                        $"Initial parameter {index} must be finite and within its model bounds.",
                        nameof(initialValues));
                }
            }
        }

        /// <summary>
        /// Estimates the model parameters that maximize the likelihood function.
        /// </summary>
        /// <returns>True if estimation was successful; otherwise, false.</returns>
        public bool Estimate()
        {
            IsEstimated = false;
            Status = OptimizationStatus.None;
            TotalFunctionEvaluations = 0;
            ResetCovarianceStatus();
            _hessian = null;

            try
            {
                Optimizer.Maximize();
                Status = Optimizer.Status;
                if (Optimizer.Status == OptimizationStatus.Success)
                {
                    IsEstimated = true;
                    BestParameterSet = Optimizer.BestParameterSet.Clone();
                    TotalFunctionEvaluations = Optimizer.FunctionEvaluations;

                    if (ComputeHessian)
                    {
                        // Compute Hessian with adaptive step sizes (flat-spot detection).
                        // Uses full log-likelihood (data + prior) for posterior Fisher information.
                        try
                        {
                            _hessian = NumericalDiff.ComputeHessian(
                                Model.LogLikelihood,
                                BestParameterSet.Values,
                                NumberOfParameters,
                                LowerBounds,
                                UpperBounds);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Hessian computation failed: {ex.Message}");
                            _hessian = null;
                            SetCovarianceFailure($"Posterior Hessian computation failed: {ex.Message}");
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Status = OptimizationStatus.Failure;
                Debug.WriteLine($"MAP estimation failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Clears all estimation results.
        /// </summary>
        public void ClearResults()
        {
            IsEstimated = false;
            Status = OptimizationStatus.None;
            TotalFunctionEvaluations = 0;
            BestParameterSet = new ParameterSet();
            _hessian = null;
            ResetCovarianceStatus();
        }

        /// <summary>
        /// Returns the profiled posterior kernel for each model parameter.
        /// </summary>
        /// <param name="bins">The number of bins in each profile. Default = 100.</param>
        /// <returns>
        /// A list of arrays where each array contains [parameter value, log-posterior-kernel] pairs. A
        /// grid point whose nuisance-parameter optimization produces no finite optimum is reported
        /// with a <see cref="double.NaN"/> kernel value so the remainder of the profile is preserved.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when bins is less than 2.</exception>
        /// <remarks>
        /// Each grid point reoptimizes every free nuisance parameter, starting from the neighbouring
        /// solution. A finite nuisance optimum is accepted even when the optimizer does not report
        /// success, because bound-adjacent and flat grid points routinely end on an iteration or
        /// tolerance limit. <see cref="ParameterConfidenceIntervals"/> requires converged solves.
        /// </remarks>
        public List<double[,]> ProfileLikelihood(int bins = 100)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (bins < 2)
                throw new ArgumentOutOfRangeException(nameof(bins), "Number of bins must be at least 2.");

            var profiles = new List<double[,]>();

            for (int parameterIndex = 0; parameterIndex < NumberOfParameters; parameterIndex++)
            {
                var sequence = Stratify.XValues(new StratificationOptions(
                    Model.Parameters[parameterIndex].LowerBound,
                    Model.Parameters[parameterIndex].UpperBound,
                    bins));
                var profile = new double[bins, 2];
                int centerIndex = Enumerable.Range(0, bins)
                    .OrderBy(index => Math.Abs(sequence[index].Midpoint - BestParameterSet.Values[parameterIndex]))
                    .First();

                var centerResult = TryMaximizeProfileGridPoint(
                    parameterIndex,
                    sequence[centerIndex].Midpoint,
                    BestParameterSet.Values);
                profile[centerIndex, 0] = sequence[centerIndex].Midpoint;
                profile[centerIndex, 1] = centerResult?.LogPosterior ?? double.NaN;
                double[] centerStart = centerResult?.Parameters ?? BestParameterSet.Values.ToArray();

                double[] lowerStart = centerStart;
                for (int gridIndex = centerIndex - 1; gridIndex >= 0; gridIndex--)
                {
                    var result = TryMaximizeProfileGridPoint(
                        parameterIndex,
                        sequence[gridIndex].Midpoint,
                        lowerStart);
                    profile[gridIndex, 0] = sequence[gridIndex].Midpoint;
                    profile[gridIndex, 1] = result?.LogPosterior ?? double.NaN;
                    if (result.HasValue)
                        lowerStart = result.Value.Parameters;
                }

                double[] upperStart = centerStart;
                for (int gridIndex = centerIndex + 1; gridIndex < bins; gridIndex++)
                {
                    var result = TryMaximizeProfileGridPoint(
                        parameterIndex,
                        sequence[gridIndex].Midpoint,
                        upperStart);
                    profile[gridIndex, 0] = sequence[gridIndex].Midpoint;
                    profile[gridIndex, 1] = result?.LogPosterior ?? double.NaN;
                    if (result.HasValue)
                        upperStart = result.Value.Parameters;
                }

                profiles.Add(profile);
            }

            return profiles;
        }

        /// <summary>
        /// Returns likelihood-ratio-style intervals from the profiled posterior kernel.
        /// </summary>
        /// <param name="alpha">The significance level. Default = 0.1 (90% confidence). Must be between 0 and 1.</param>
        /// <returns>A matrix where each row contains [lower bound, upper bound] for each parameter.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the model has not been estimated or nuisance-parameter optimization fails.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when alpha is not between 0 and 1.</exception>
        /// <remarks>
        /// At every candidate value of the parameter of interest, all non-fixed nuisance
        /// parameters are reoptimized against the full posterior kernel. These intervals
        /// are not posterior credible intervals; use MCMC marginal quantiles for that purpose.
        /// </remarks>
        public double[,] ParameterConfidenceIntervals(double alpha = 0.1)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (alpha <= 0 || alpha >= 1)
                throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be between 0 and 1.");

            var chiSquared = new ChiSquared(1);
            double threshold = MaximumLogLikelihood - 0.5 * chiSquared.InverseCDF(1 - alpha);
            var confidenceIntervals = new double[NumberOfParameters, 2];

            for (int parameterIndex = 0; parameterIndex < NumberOfParameters; parameterIndex++)
            {
                double lowerBound = Model.Parameters[parameterIndex].LowerBound;
                double upperBound = Model.Parameters[parameterIndex].UpperBound;

                var lowerResult = MaximizeProfileLogPosterior(
                    parameterIndex,
                    lowerBound,
                    BestParameterSet.Values);
                if (lowerResult.LogPosterior < threshold)
                {
                    double[] lowerStart = lowerResult.Parameters;
                    confidenceIntervals[parameterIndex, 0] = Brent.Solve(
                        value =>
                        {
                            var result = MaximizeProfileLogPosterior(parameterIndex, value, lowerStart);
                            lowerStart = result.Parameters;
                            return result.LogPosterior - threshold;
                        },
                        lowerBound,
                        BestParameterSet.Values[parameterIndex]);
                }
                else
                {
                    confidenceIntervals[parameterIndex, 0] = lowerBound;
                }

                var upperResult = MaximizeProfileLogPosterior(
                    parameterIndex,
                    upperBound,
                    BestParameterSet.Values);
                if (upperResult.LogPosterior < threshold)
                {
                    double[] upperStart = upperResult.Parameters;
                    confidenceIntervals[parameterIndex, 1] = Brent.Solve(
                        value =>
                        {
                            var result = MaximizeProfileLogPosterior(parameterIndex, value, upperStart);
                            upperStart = result.Parameters;
                            return result.LogPosterior - threshold;
                        },
                        BestParameterSet.Values[parameterIndex],
                        upperBound);
                }
                else
                {
                    confidenceIntervals[parameterIndex, 1] = upperBound;
                }
            }

            return confidenceIntervals;
        }

        /// <summary>
        /// Maximizes the posterior kernel at one grid point, accepting any finite nuisance optimum.
        /// </summary>
        /// <param name="parameterIndex">Index of the parameter held fixed.</param>
        /// <param name="fixedValue">Value assigned to the parameter of interest.</param>
        /// <param name="startingParameters">Full parameter vector used to initialize nuisance optimization.</param>
        /// <returns>The profile result, or null when no finite nuisance optimum exists at the grid point.</returns>
        private (double LogPosterior, double[] Parameters)? TryMaximizeProfileGridPoint(
            int parameterIndex,
            double fixedValue,
            IReadOnlyList<double> startingParameters)
        {
            try
            {
                return MaximizeProfileLogPosterior(
                    parameterIndex,
                    fixedValue,
                    startingParameters,
                    requireConvergence: false);
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"MAP profile grid point {fixedValue} for parameter {parameterIndex} has no finite nuisance optimum: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Maximizes the posterior kernel over all free nuisance parameters while fixing one parameter.
        /// </summary>
        /// <param name="parameterIndex">Index of the parameter held fixed.</param>
        /// <param name="fixedValue">Value assigned to the parameter of interest.</param>
        /// <param name="startingParameters">Full parameter vector used to initialize nuisance optimization.</param>
        /// <param name="requireConvergence">
        /// When true, only an optimizer that reports <see cref="OptimizationStatus.Success"/> is
        /// accepted. When false, the best finite optimum found by either optimizer is returned if
        /// neither converges.
        /// </param>
        /// <returns>The profiled log-posterior kernel and the optimized full parameter vector.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when both bounded BFGS and bounded Nelder-Mead fail to produce an acceptable nuisance optimum.
        /// </exception>
        /// <remarks>
        /// BFGS supplies the efficient primary solve. Nelder-Mead is a deterministic fallback for
        /// nonsmooth or numerically difficult posterior surfaces. Fixed model parameters remain fixed.
        /// </remarks>
        private (double LogPosterior, double[] Parameters) MaximizeProfileLogPosterior(
            int parameterIndex,
            double fixedValue,
            IReadOnlyList<double> startingParameters,
            bool requireConvergence = true)
        {
            var fullStart = startingParameters.ToArray();
            fullStart[parameterIndex] = fixedValue;
            int[] nuisanceIndices = Enumerable.Range(0, NumberOfParameters)
                .Where(index => index != parameterIndex && !Model.Parameters[index].IsFixed)
                .ToArray();

            if (nuisanceIndices.Length == 0)
                return (Model.LogLikelihood(fullStart), fullStart);

            double[] reducedStart = nuisanceIndices.Select(index => fullStart[index]).ToArray();
            double[] reducedLower = nuisanceIndices.Select(index => Model.Parameters[index].LowerBound).ToArray();
            double[] reducedUpper = nuisanceIndices.Select(index => Model.Parameters[index].UpperBound).ToArray();

            double Objective(double[] nuisanceValues)
            {
                var fullParameters = fullStart.ToArray();
                for (int index = 0; index < nuisanceIndices.Length; index++)
                    fullParameters[nuisanceIndices[index]] = nuisanceValues[index];
                return Model.LogLikelihood(fullParameters);
            }

            (double LogPosterior, double[] Parameters)? fallback = null;

            try
            {
                var bfgs = new BFGS(
                    Objective,
                    nuisanceIndices.Length,
                    reducedStart,
                    reducedLower,
                    reducedUpper)
                {
                    ReportFailure = false,
                    RecordTraces = false,
                    ComputeHessian = false
                };
                bfgs.Maximize();
                if (TryCreateProfileResult(
                    bfgs,
                    nuisanceIndices,
                    fullStart,
                    Objective,
                    out var bfgsResult))
                {
                    if (bfgs.Status == OptimizationStatus.Success)
                        return bfgsResult;
                    if (!requireConvergence)
                        fallback = bfgsResult;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MAP profile BFGS nuisance optimization failed: {ex.Message}");
            }

            try
            {
                var nelderMead = new NelderMead(
                    Objective,
                    nuisanceIndices.Length,
                    reducedStart,
                    reducedLower,
                    reducedUpper)
                {
                    ReportFailure = false,
                    RecordTraces = false,
                    ComputeHessian = false,
                    EnableStartPointProbe = true
                };
                nelderMead.Maximize();
                if (TryCreateProfileResult(
                    nelderMead,
                    nuisanceIndices,
                    fullStart,
                    Objective,
                    out var nelderMeadResult))
                {
                    if (nelderMead.Status == OptimizationStatus.Success)
                        return nelderMeadResult;
                    if (!requireConvergence &&
                        (!fallback.HasValue || nelderMeadResult.LogPosterior > fallback.Value.LogPosterior))
                        fallback = nelderMeadResult;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MAP profile Nelder-Mead nuisance optimization failed: {ex.Message}");
            }

            if (fallback.HasValue)
                return fallback.Value;

            throw new InvalidOperationException(
                $"Unable to profile MAP parameter {parameterIndex}: nuisance-parameter optimization failed.");
        }

        /// <summary>
        /// Converts a finite optimizer result into a full profiled parameter vector.
        /// </summary>
        /// <param name="optimizer">Completed nuisance optimizer.</param>
        /// <param name="nuisanceIndices">Indices represented by the reduced optimizer vector.</param>
        /// <param name="fullStart">Full parameter vector containing fixed values.</param>
        /// <param name="objective">Reduced log-posterior-kernel objective.</param>
        /// <param name="result">The completed profile result when successful.</param>
        /// <returns>
        /// <see langword="true"/> when the optimizer supplied finite values and objective. Whether
        /// the optimizer converged is judged by the caller from its status.
        /// </returns>
        private static bool TryCreateProfileResult(
            Optimizer optimizer,
            IReadOnlyList<int> nuisanceIndices,
            IReadOnlyList<double> fullStart,
            Func<double[], double> objective,
            out (double LogPosterior, double[] Parameters) result)
        {
            result = default;
            double[] nuisanceValues = optimizer.BestParameterSet.Values;
            if (nuisanceValues == null ||
                nuisanceValues.Length != nuisanceIndices.Count ||
                nuisanceValues.Any(value => !double.IsFinite(value)))
            {
                return false;
            }

            double logPosterior = objective(nuisanceValues);
            if (!double.IsFinite(logPosterior))
                return false;

            var fullParameters = fullStart.ToArray();
            for (int index = 0; index < nuisanceIndices.Count; index++)
                fullParameters[nuisanceIndices[index]] = nuisanceValues[index];

            result = (logPosterior, fullParameters);
            return true;
        }

        /// <summary>
        /// Returns the parameter covariance matrix computed from the inverse of the Fisher Information Matrix (negative Hessian).
        /// </summary>
        /// <returns>The parameter covariance matrix.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated or the Hessian is null.</exception>
        public Matrix GetCovarianceMatrix()
        {
            if (!TryGetCovarianceMatrix(out Matrix covariance))
            {
                throw new InvalidOperationException(
                    CovarianceDiagnostic ?? "The covariance matrix is unavailable.");
            }

            return covariance;
        }

        /// <summary>
        /// Attempts to compute a finite posterior covariance matrix.
        /// </summary>
        /// <param name="covariance">
        /// The covariance matrix when successful; otherwise, a zero matrix that must not
        /// be interpreted as estimated uncertainty.
        /// </param>
        /// <returns><see langword="true"/> when a usable covariance matrix is available.</returns>
        /// <remarks>
        /// Inspect <see cref="CovarianceStatus"/> and <see cref="CovarianceDiagnostic"/> after
        /// this method returns. Positive-definite regularization is reported explicitly.
        /// </remarks>
        public bool TryGetCovarianceMatrix(out Matrix covariance)
        {
            covariance = new Matrix(NumberOfParameters, NumberOfParameters);
            if (!IsEstimated)
                return SetCovarianceFailure("The model has not been estimated.");
            if (_hessian == null)
                return SetCovarianceFailure("The posterior Hessian is unavailable. Estimation or Hessian computation may have failed.");

            try
            {
                Matrix fisher = _hessian * -1d;
                Matrix candidate = fisher.Inverse();
                if (MatrixIsUsableCovariance(candidate) && MatrixIsSymmetricPositiveDefinite(candidate))
                {
                    covariance = candidate;
                    CovarianceStatus = CovarianceComputationStatus.Available;
                    CovarianceDiagnostic = null;
                    return true;
                }

                Matrix regularized = MatrixRegularization.MakeSymmetricPositiveDefinite(candidate);
                if (!MatrixIsUsableCovariance(regularized))
                    return SetCovarianceFailure(
                        "Posterior covariance is non-finite or has a non-positive diagonal variance.");

                double maxAbsDelta = MaximumAbsoluteDifference(candidate, regularized);
                covariance = regularized;
                CovarianceStatus = CovarianceComputationStatus.Regularized;
                CovarianceDiagnostic =
                    $"Posterior covariance was regularized to positive definiteness (maximum absolute adjustment {maxAbsDelta:G6}).";
                Debug.WriteLine(CovarianceDiagnostic);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to compute covariance matrix: {ex.Message}");
                return SetCovarianceFailure($"Posterior covariance computation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to obtain a finite local covariance for MAP-centered MCMC initialization.
        /// </summary>
        /// <param name="covariance">The initialization covariance when successful; otherwise, a zero matrix.</param>
        /// <param name="diagnostic">A diagnostic describing any singular-information fallback that was required.</param>
        /// <returns><see langword="true"/> when a finite positive-definite initialization covariance is available.</returns>
        /// <remarks>
        /// The public covariance contract remains governed by <see cref="TryGetCovarianceMatrix(out Matrix)"/>.
        /// When that method rejects a singular posterior information matrix, initialization may
        /// still use its Moore-Penrose inverse. Null-space directions are thereby anchored at the
        /// MAP rather than assigned unbounded variance, after which only the established
        /// positive-definite matrix regularization is applied. This covariance is intended solely
        /// for initial population generation and must not be reported as posterior uncertainty.
        /// </remarks>
        internal bool TryGetInitializationCovarianceMatrix(
            out Matrix covariance,
            out string? diagnostic)
        {
            if (TryGetCovarianceMatrix(out covariance))
            {
                diagnostic = CovarianceDiagnostic;
                return true;
            }

            diagnostic = CovarianceDiagnostic;
            covariance = new Matrix(NumberOfParameters, NumberOfParameters);
            if (!IsEstimated || _hessian == null)
                return false;

            try
            {
                Matrix fisher = _hessian * -1d;
                var decomposition = new SingularValueDecomposition(fisher);
                Matrix pseudoInverse = decomposition.Solve(Matrix.Identity(NumberOfParameters));
                Matrix regularized = MatrixRegularization.MakeSymmetricPositiveDefinite(pseudoInverse);
                if (!MatrixIsUsableCovariance(regularized) ||
                    !MatrixIsSymmetricPositiveDefinite(regularized))
                {
                    diagnostic =
                        "The Moore-Penrose posterior covariance could not be regularized for initialization.";
                    return false;
                }

                covariance = regularized;
                diagnostic =
                    $"Posterior information was rank deficient ({decomposition.Rank()} of " +
                    $"{NumberOfParameters}); MAP initialization uses a regularized Moore-Penrose covariance.";
                Debug.WriteLine(diagnostic);
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = $"Initialization covariance computation failed: {ex.Message}";
                Debug.WriteLine(diagnostic);
                covariance = new Matrix(NumberOfParameters, NumberOfParameters);
                return false;
            }
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
        /// Determines whether a covariance matrix is finite with positive diagonal variances.
        /// </summary>
        /// <param name="matrix">Matrix to validate.</param>
        /// <returns><see langword="true"/> when the covariance matrix is usable.</returns>
        private static bool MatrixIsUsableCovariance(Matrix matrix)
        {
            if (matrix.NumberOfRows == 0 || matrix.NumberOfRows != matrix.NumberOfColumns)
                return false;

            for (int row = 0; row < matrix.NumberOfRows; row++)
            {
                for (int column = 0; column < matrix.NumberOfColumns; column++)
                {
                    if (!double.IsFinite(matrix[row, column]))
                        return false;
                }

                if (matrix[row, row] <= 0.0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a covariance candidate is symmetric positive definite without modifying it.
        /// </summary>
        /// <param name="matrix">Matrix to inspect.</param>
        /// <returns><see langword="true"/> when the matrix is symmetric positive definite.</returns>
        private static bool MatrixIsSymmetricPositiveDefinite(Matrix matrix)
        {
            double scale = 0.0;
            for (int row = 0; row < matrix.NumberOfRows; row++)
            {
                for (int column = 0; column < matrix.NumberOfColumns; column++)
                    scale = Math.Max(scale, Math.Abs(matrix[row, column]));
            }

            double symmetryTolerance = 1e-10 * Math.Max(scale, 1e-12);
            for (int row = 0; row < matrix.NumberOfRows; row++)
            {
                for (int column = row + 1; column < matrix.NumberOfColumns; column++)
                {
                    if (Math.Abs(matrix[row, column] - matrix[column, row]) > symmetryTolerance)
                        return false;
                }
            }

            try
            {
                _ = new CholeskyDecomposition(matrix);
                return true;
            }
            catch
            {
                return false;
            }
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
        /// Returns the standard errors of the parameter estimates from the diagonal of the covariance matrix.
        /// </summary>
        /// <returns>An array of standard errors for each parameter.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated or the Hessian is null.</exception>
        public double[] GetStandardErrors()
        {
            var covariance = GetCovarianceMatrix();
            var standardErrors = new double[NumberOfParameters];

            for (int i = 0; i < NumberOfParameters; i++)
            {
                standardErrors[i] = Math.Sqrt(Math.Max(0, covariance[i, i])); // Ensure non-negative
            }

            return standardErrors;
        }

        /// <summary>
        /// Returns the correlation matrix from the covariance matrix.
        /// </summary>
        /// <returns>The parameter correlation matrix.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated or the Hessian is null.</exception>
        public Matrix GetCorrelationMatrix()
        {
            var covariance = GetCovarianceMatrix();
            var correlation = new Matrix(NumberOfParameters, NumberOfParameters);

            for (int i = 0; i < NumberOfParameters; i++)
            {
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double denom = Math.Sqrt(covariance[i, i] * covariance[j, j]);
                    correlation[i, j] = denom > 0 ? covariance[i, j] / denom : 0;
                }
            }

            return correlation;
        }

        /// <summary>
        /// Computes the Akaike Information Criterion (AIC) from the data log-likelihood
        /// evaluated at the MAP estimate.
        /// </summary>
        /// <returns>The AIC value. Lower values indicate better models.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <remarks>
        /// <para>
        /// Uses <see cref="IModel.DataLogLikelihood(double[])"/> at the posterior mode so
        /// prior-density values and normalization constants are never included in AIC.
        /// When every active prior is constant over the relevant parameter region, the MAP
        /// and constrained MLE coincide and this value is directly comparable with MLE AIC,
        /// subject to numerical accuracy of the MAP estimate.
        /// </para>
        /// <para>
        /// <b>Caveat for informative priors.</b> Informative priors can move the MAP away from
        /// the MLE, while AIC's parameter penalty does not account for that prior information.
        /// Consequently this MAP-evaluated value should not be interpreted as conventional AIC
        /// when informative, Jeffreys, quantile, or other nonconstant priors are active.
        /// </para>
        /// <para>
        /// With informative priors, prefer DIC, WAIC, or LOO-CV with verified PSIS diagnostics.
        /// </para>
        /// </remarks>
        public double GetAIC()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");

            return GoodnessOfFit.AIC(NumberOfParameters, Model.DataLogLikelihood(BestParameterSet.Values));
        }

        /// <summary>
        /// Computes the Bayesian Information Criterion (BIC) from the data log-likelihood
        /// evaluated at the MAP estimate.
        /// </summary>
        /// <param name="sampleSize">The sample size (number of observations).</param>
        /// <returns>The BIC value. Lower values indicate better models.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when sample size is less than 1.</exception>
        /// <remarks>
        /// <para>
        /// Uses <see cref="IModel.DataLogLikelihood(double[])"/> at the posterior mode so
        /// prior-density values and normalization constants are never included in BIC. When
        /// every active prior is constant over the relevant parameter region, the MAP and
        /// constrained MLE coincide and this value is directly comparable with MLE BIC,
        /// subject to numerical accuracy of the MAP estimate.
        /// </para>
        /// <para>
        /// <b>Caveat for informative priors.</b> Informative priors can move the MAP away from
        /// the MLE, while BIC's <c>k*ln(n)</c> penalty does not account for that prior information.
        /// Consequently this MAP-evaluated value should not be interpreted as conventional BIC
        /// when informative, Jeffreys, quantile, or other nonconstant priors are active.
        /// </para>
        /// <para>
        /// With informative priors, prefer DIC, WAIC, or LOO-CV with verified PSIS diagnostics.
        /// </para>
        /// </remarks>
        public double GetBIC(int sampleSize)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (sampleSize < 1)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be at least 1.");

            return GoodnessOfFit.BIC(sampleSize, NumberOfParameters, Model.DataLogLikelihood(BestParameterSet.Values));
        }

        /// <summary>
        /// Computes numerical gradients of pointwise data log-likelihoods with respect to parameters
        /// using central differences.
        /// </summary>
        /// <param name="parameters">The parameter values at which to evaluate gradients.</param>
        /// <param name="n">The number of observations (length of pointwise log-likelihood array).</param>
        /// <returns>A jagged array where gradients[i][j] is ?log f(y?|?)/???.</returns>
        /// <remarks>
        /// <para>
        /// Uses the central difference formula: ∂f/∂θⱼ ≈ [f(θ+hⱼeⱼ) - f(θ-hⱼeⱼ)] / (2hⱼ),
        /// where hⱼ = max(|θⱼ| × 1e-4, 1e-3). Same step size strategy as
        /// <see cref="MaximumLikelihood.GetCooksDistance"/> and
        /// <see cref="LeverageDiagnostics.ComputeNumericalHessianPublic"/>.
        /// </para>
        /// </remarks>
        private double[][] ComputePointwiseGradients(double[] parameters, int n)
        {
            return NumericalDiff.ComputePointwiseGradients(
                Model.PointwiseDataLogLikelihood, parameters, n, NumberOfParameters);
        }

        /// <summary>
        /// Returns the pointwise influence of each observation on parameter estimates (DFBETAS-like diagnostic).
        /// </summary>
        /// <returns>
        /// A matrix where element [i,j] represents the scaled influence of observation i on parameter j.
        /// Values greater than 2/vn are often considered influential.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This computes an approximation to the leave-one-out influence using the score contributions.
        /// Unlike <see cref="MaximumLikelihood.GetObservationInfluence"/>, the Hessian here is the full
        /// posterior Hessian (data + prior), not data-only.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        public double[,] GetObservationInfluence()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");

            // Get pointwise log-likelihoods and gradients
            double[] pointwiseLL = Model.PointwiseDataLogLikelihood(BestParameterSet.Values);
            int n = pointwiseLL.Length;

            double[][] gradients = ComputePointwiseGradients(BestParameterSet.Values, n);

            // Use the validated covariance path so numerical failure is explicit.
            Matrix fisherInv = GetCovarianceMatrix();
            // Compute influence: I_ij = (H⁻¹ gᵢ)_j / SE_j
            var influence = new double[n, NumberOfParameters];
            var se = Enumerable.Range(0, NumberOfParameters)
                .Select(index => Math.Sqrt(fisherInv[index, index]))
                .ToArray();

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double inflJ = 0;
                    for (int k = 0; k < NumberOfParameters; k++)
                    {
                        inflJ += fisherInv[j, k] * gradients[i][k];
                    }
                    influence[i, j] = se[j] > 0 ? inflJ / se[j] : 0;
                }
            }

            return influence;
        }

        /// <summary>
        /// Returns Cook's distance-like measure for each observation using the full posterior Hessian.
        /// </summary>
        /// <returns>
        /// An array of influence measures, one per observation.
        /// Larger values indicate more influential observations.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Cook's D_i = gᵢᵀ H⁻¹ gᵢ / p where H is the full posterior Hessian (data + prior)
        /// and p is the number of parameters.
        /// </para>
        /// <para>
        /// Unlike <see cref="MaximumLikelihood.GetCooksDistance"/> which uses the data-only Hessian,
        /// this method uses the full posterior Hessian from the MAP optimizer, accounting for the
        /// regularizing effect of priors.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        public double[] GetCooksDistance()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");

            // Get pointwise log-likelihoods and gradients
            double[] pointwiseLL = Model.PointwiseDataLogLikelihood(BestParameterSet.Values);
            int n = pointwiseLL.Length;

            double[][] gradients = ComputePointwiseGradients(BestParameterSet.Values, n);

            // Use the validated covariance path so numerical failure is explicit.
            Matrix fisherInv = GetCovarianceMatrix();

            var cooksD = new double[n];

            for (int i = 0; i < n; i++)
            {
                // Compute gᵢᵀ H⁻¹ gᵢ
                double quadForm = 0;
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double tmp = 0;
                    for (int k = 0; k < NumberOfParameters; k++)
                    {
                        tmp += fisherInv[j, k] * gradients[i][k];
                    }
                    quadForm += gradients[i][j] * tmp;
                }
                cooksD[i] = quadForm / NumberOfParameters;
            }

            return cooksD;
        }

        /// <summary>
        /// Computes leverage diagnostics from the optimizer's Hessian, decomposing information
        /// contributions from observations and prior components.
        /// </summary>
        /// <returns>A <see cref="LeverageDiagnostics"/> instance with per-observation and per-prior leverage.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <remarks>
        /// Delegates to the <see cref="LeverageDiagnostics(IModel, double[])"/> constructor which
        /// computes the posterior Hessian numerically via central differences at the MAP values.
        /// This is consistent with the <see cref="BayesianAnalysis"/> path.
        /// </remarks>
        public LeverageDiagnostics ComputeLeverageDiagnostics()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");

            // Use the IModel + MAP values constructor which computes the Hessian numerically.
            // This is simpler and consistent with the BayesianAnalysis path.
            return new LeverageDiagnostics(Model, BestParameterSet.Values);
        }

        #endregion

    }
}

