using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using Numerics.Mathematics.RootFinding;
using Numerics.Sampling;
using RMC.BestFit.Models;
using System.Diagnostics;

namespace RMC.BestFit.Estimation
{
    /// <summary>
    /// Estimates model parameters using the method of Maximum Likelihood Estimation (MLE).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>Description:</b>
    ///     This class implements maximum likelihood estimation by maximizing the data log-likelihood function
    ///     using various optimization algorithms. The Hessian matrix is computed for uncertainty quantification.
    /// </para>
    /// </remarks>
    public class MaximumLikelihood
    {
        private OptimizationMethod _optimizerMethod = OptimizationMethod.DifferentialEvolution;
        private bool _reportFailure = false;
        private bool _computeHessian = true;
        private Matrix? _hessian;

        /// <summary>
        /// Constructs a new maximum likelihood estimation (MLE) class. 
        /// </summary>
        /// <param name="model">The model to estimate.</param>
        /// <param name="method">Optional. The optimization method. Default = Differential Evolution.</param>
        /// <exception cref="ArgumentNullException">Thrown when model is null.</exception>
        public MaximumLikelihood(IModel model, OptimizationMethod method = OptimizationMethod.DifferentialEvolution)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model), "Model cannot be null.");
            OptimizerMethod = method;
            SetUpOptimizer();
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
        /// guard (e.g. <see cref="ProfileLikelihood"/>, <see cref="ParameterConfidenceIntervals"/>,
        /// <see cref="GetCovarianceMatrix"/>) get a deterministic empty parameter set rather
        /// than an <see cref="NullReferenceException"/>.
        /// </summary>
        public ParameterSet BestParameterSet { get; private set; } = new ParameterSet();

        /// <summary>
        /// Gets the total number of function evaluations required to estimate the model.
        /// </summary>
        public int TotalFunctionEvaluations { get; private set; }

        /// <summary>
        /// Gets the maximum log-likelihood value at the optimal parameters.
        /// </summary>
        public double MaximumLogLikelihood => IsEstimated ? -BestParameterSet.Fitness : double.NaN;

        #region Methods

        /// <summary>
        /// Sets up the optimizer class based on the selected optimization method.
        /// </summary>
        private void SetUpOptimizer()
        {
            // Store parameter constraints
            InitialValues = Model.Parameters.Select(x => x.Value).ToArray();
            LowerBounds = Model.Parameters.Select(x => x.LowerBound).ToArray();
            UpperBounds = Model.Parameters.Select(x => x.UpperBound).ToArray();

            // Maximum Likelihood is a maximization problem using only the data log-likelihood
            if (OptimizerMethod == OptimizationMethod.Brent)
            {
                if (NumberOfParameters != 1)
                    throw new InvalidOperationException("Brent method requires exactly one parameter.");
                Optimizer = new BrentSearch(x => Model.DataLogLikelihood(new[] { x }), LowerBounds[0], UpperBounds[0]);
            }
            else if (OptimizerMethod == OptimizationMethod.BFGS)
            {
                Optimizer = new BFGS(Model.DataLogLikelihood, NumberOfParameters, InitialValues, LowerBounds, UpperBounds);
            }
            else if (OptimizerMethod == OptimizationMethod.NelderMead)
            {
                Optimizer = new NelderMead(Model.DataLogLikelihood, NumberOfParameters, InitialValues, LowerBounds, UpperBounds);
            }
            else if (OptimizerMethod == OptimizationMethod.Powell)
            {
                Optimizer = new Powell(Model.DataLogLikelihood, NumberOfParameters, InitialValues, LowerBounds, UpperBounds);
            }
            else if (OptimizerMethod == OptimizationMethod.DifferentialEvolution)
            {
                Optimizer = new DifferentialEvolution(Model.DataLogLikelihood, NumberOfParameters, LowerBounds, UpperBounds);
            }
            else if (OptimizerMethod == OptimizationMethod.MultilevelSingleLinkage)
            {
                Optimizer = new MLSL(Model.DataLogLikelihood, NumberOfParameters, InitialValues, LowerBounds, UpperBounds, LocalMethod.NelderMead);
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
        /// Estimates the model parameters that maximize the likelihood function.
        /// </summary>
        /// <returns>True if estimation was successful; otherwise, false.</returns>
        public bool Estimate()
        {
            IsEstimated = false;
            Status = OptimizationStatus.None;
            TotalFunctionEvaluations = 0;
            ResetCovarianceStatus();
            // Reset cached Hessian from any prior successful run so a subsequent failed
            // Estimate() doesn't leave GetCovarianceMatrix returning stale covariance
            // computed from a different parameter vector.
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
                        // Compute Hessian with adaptive step sizes (flat-spot detection)
                        try
                        {
                            // Bounded steps keep the finite differences inside the parameter support,
                            // matching the posterior Hessian used by MaximumAPosteriori.
                            _hessian = NumericalDiff.ComputeHessian(
                                Model.DataLogLikelihood,
                                BestParameterSet.Values,
                                NumberOfParameters,
                                LowerBounds,
                                UpperBounds);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Hessian computation failed: {ex.Message}");
                            _hessian = null;
                            SetCovarianceFailure($"Hessian computation failed: {ex.Message}");
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Status = OptimizationStatus.Failure;
                Debug.WriteLine($"MLE estimation failed: {ex.Message}");
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
        /// Returns the profile likelihood for each model parameter.
        /// </summary>
        /// <param name="bins">The number of bins in each profile. Default = 100.</param>
        /// <returns>
        /// A list of arrays where each array contains [parameter value, log-likelihood] pairs. A grid
        /// point whose nuisance-parameter optimization produces no finite optimum is reported with a
        /// <see cref="double.NaN"/> log-likelihood so the remainder of the profile is preserved.
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
                profile[centerIndex, 1] = centerResult?.LogLikelihood ?? double.NaN;
                double[] centerStart = centerResult?.Parameters ?? BestParameterSet.Values.ToArray();

                double[] lowerStart = centerStart;
                for (int gridIndex = centerIndex - 1; gridIndex >= 0; gridIndex--)
                {
                    var result = TryMaximizeProfileGridPoint(
                        parameterIndex,
                        sequence[gridIndex].Midpoint,
                        lowerStart);
                    profile[gridIndex, 0] = sequence[gridIndex].Midpoint;
                    profile[gridIndex, 1] = result?.LogLikelihood ?? double.NaN;
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
                    profile[gridIndex, 1] = result?.LogLikelihood ?? double.NaN;
                    if (result.HasValue)
                        upperStart = result.Value.Parameters;
                }

                profiles.Add(profile);
            }

            return profiles;
        }

        /// <summary>
        /// Maximizes the profiled data log-likelihood at one grid point, accepting any finite nuisance optimum.
        /// </summary>
        /// <param name="parameterIndex">Index of the parameter held fixed.</param>
        /// <param name="fixedValue">Value assigned to the parameter of interest.</param>
        /// <param name="startingParameters">Full parameter vector used to initialize nuisance optimization.</param>
        /// <returns>The profile result, or null when no finite nuisance optimum exists at the grid point.</returns>
        private (double LogLikelihood, double[] Parameters)? TryMaximizeProfileGridPoint(
            int parameterIndex,
            double fixedValue,
            IReadOnlyList<double> startingParameters)
        {
            try
            {
                return MaximizeProfileDataLogLikelihood(
                    parameterIndex,
                    fixedValue,
                    startingParameters,
                    requireConvergence: false);
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Profile likelihood grid point {fixedValue} for parameter {parameterIndex} has no finite nuisance optimum: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns parameter confidence intervals based on true profile likelihood using the chi-squared threshold.
        /// </summary>
        /// <param name="alpha">The significance level. Default = 0.1 (90% confidence). Must be between 0 and 1.</param>
        /// <returns>A matrix where each row contains [lower bound, upper bound] for each parameter.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the model has not been estimated or nuisance-parameter optimization fails.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when alpha is not between 0 and 1.</exception>
        /// <remarks>
        /// At every candidate value of the parameter of interest, all non-fixed nuisance
        /// parameters are reoptimized against the data log-likelihood.
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

                var lowerResult = MaximizeProfileDataLogLikelihood(
                    parameterIndex,
                    lowerBound,
                    BestParameterSet.Values);
                if (lowerResult.LogLikelihood < threshold)
                {
                    double[] lowerStart = lowerResult.Parameters;
                    confidenceIntervals[parameterIndex, 0] = Brent.Solve(
                        value =>
                        {
                            var result = MaximizeProfileDataLogLikelihood(parameterIndex, value, lowerStart);
                            lowerStart = result.Parameters;
                            return result.LogLikelihood - threshold;
                        },
                        lowerBound,
                        BestParameterSet.Values[parameterIndex]);
                }
                else
                {
                    confidenceIntervals[parameterIndex, 0] = lowerBound;
                }

                var upperResult = MaximizeProfileDataLogLikelihood(
                    parameterIndex,
                    upperBound,
                    BestParameterSet.Values);
                if (upperResult.LogLikelihood < threshold)
                {
                    double[] upperStart = upperResult.Parameters;
                    confidenceIntervals[parameterIndex, 1] = Brent.Solve(
                        value =>
                        {
                            var result = MaximizeProfileDataLogLikelihood(parameterIndex, value, upperStart);
                            upperStart = result.Parameters;
                            return result.LogLikelihood - threshold;
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
        /// Maximizes the data log-likelihood over all free nuisance parameters while fixing one parameter.
        /// </summary>
        /// <param name="parameterIndex">Index of the parameter held fixed.</param>
        /// <param name="fixedValue">Value assigned to the parameter of interest.</param>
        /// <param name="startingParameters">Full parameter vector used to initialize nuisance optimization.</param>
        /// <param name="requireConvergence">
        /// When true, only an optimizer that reports <see cref="OptimizationStatus.Success"/> is
        /// accepted. When false, the best finite optimum found by either optimizer is returned if
        /// neither converges.
        /// </param>
        /// <returns>The profiled data log-likelihood and the optimized full parameter vector.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when both bounded BFGS and bounded Nelder-Mead fail to produce an acceptable nuisance optimum.
        /// </exception>
        /// <remarks>
        /// BFGS supplies the efficient primary solve. Nelder-Mead is a deterministic fallback for
        /// nonsmooth or numerically difficult likelihoods. Fixed model parameters remain fixed.
        /// </remarks>
        private (double LogLikelihood, double[] Parameters) MaximizeProfileDataLogLikelihood(
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
                return (Model.DataLogLikelihood(fullStart), fullStart);

            double[] reducedStart = nuisanceIndices.Select(index => fullStart[index]).ToArray();
            double[] reducedLower = nuisanceIndices.Select(index => Model.Parameters[index].LowerBound).ToArray();
            double[] reducedUpper = nuisanceIndices.Select(index => Model.Parameters[index].UpperBound).ToArray();

            double Objective(double[] nuisanceValues)
            {
                var fullParameters = fullStart.ToArray();
                for (int index = 0; index < nuisanceIndices.Length; index++)
                    fullParameters[nuisanceIndices[index]] = nuisanceValues[index];
                return Model.DataLogLikelihood(fullParameters);
            }

            (double LogLikelihood, double[] Parameters)? fallback = null;

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
                Debug.WriteLine($"Profile-likelihood BFGS nuisance optimization failed: {ex.Message}");
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
                        (!fallback.HasValue || nelderMeadResult.LogLikelihood > fallback.Value.LogLikelihood))
                        fallback = nelderMeadResult;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Profile-likelihood Nelder-Mead nuisance optimization failed: {ex.Message}");
            }

            if (fallback.HasValue)
                return fallback.Value;

            throw new InvalidOperationException(
                $"Unable to profile parameter {parameterIndex}: nuisance-parameter optimization failed.");
        }

        /// <summary>
        /// Converts a finite optimizer result into a full profiled parameter vector.
        /// </summary>
        /// <param name="optimizer">Completed nuisance optimizer.</param>
        /// <param name="nuisanceIndices">Indices represented by the reduced optimizer vector.</param>
        /// <param name="fullStart">Full parameter vector containing fixed values.</param>
        /// <param name="objective">Reduced data log-likelihood objective.</param>
        /// <param name="result">The completed profile result when successful.</param>
        /// <returns>
        /// <see langword="true"/> when the optimizer supplied finite values and likelihood. Whether
        /// the optimizer converged is judged by the caller from its status.
        /// </returns>
        private static bool TryCreateProfileResult(
            Optimizer optimizer,
            IReadOnlyList<int> nuisanceIndices,
            IReadOnlyList<double> fullStart,
            Func<double[], double> objective,
            out (double LogLikelihood, double[] Parameters) result)
        {
            result = default;
            double[] nuisanceValues = optimizer.BestParameterSet.Values;
            if (nuisanceValues == null ||
                nuisanceValues.Length != nuisanceIndices.Count ||
                nuisanceValues.Any(value => !double.IsFinite(value)))
            {
                return false;
            }

            double logLikelihood = objective(nuisanceValues);
            if (!double.IsFinite(logLikelihood))
                return false;

            var fullParameters = fullStart.ToArray();
            for (int index = 0; index < nuisanceIndices.Count; index++)
                fullParameters[nuisanceIndices[index]] = nuisanceValues[index];

            result = (logLikelihood, fullParameters);
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
        /// Attempts to compute a finite parameter covariance matrix.
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
                return SetCovarianceFailure("The Hessian is unavailable. Estimation or Hessian computation may have failed.");

            try
            {
                Matrix fisher = _hessian * -1d;
                Matrix candidate = fisher.Inverse();
                return TryRegularizeAndValidateCovariance(
                    candidate,
                    "Maximum-likelihood covariance",
                    out covariance);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to compute covariance matrix: {ex.Message}");
                return SetCovarianceFailure($"Maximum-likelihood covariance computation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Regularizes and validates a covariance candidate while recording its status.
        /// </summary>
        /// <param name="candidate">Unregularized covariance candidate.</param>
        /// <param name="context">Description used in diagnostic messages.</param>
        /// <param name="covariance">Validated covariance matrix when successful.</param>
        /// <returns><see langword="true"/> when the resulting covariance is finite and non-degenerate.</returns>
        private bool TryRegularizeAndValidateCovariance(
            Matrix candidate,
            string context,
            out Matrix covariance)
        {
            covariance = new Matrix(NumberOfParameters, NumberOfParameters);
            if (MatrixIsUsableCovariance(candidate) && MatrixIsSymmetricPositiveDefinite(candidate))
            {
                covariance = candidate;
                CovarianceStatus = CovarianceComputationStatus.Available;
                CovarianceDiagnostic = null;
                return true;
            }

            Matrix regularized = MatrixRegularization.MakeSymmetricPositiveDefinite(candidate);
            if (!MatrixIsUsableCovariance(regularized))
                return SetCovarianceFailure($"{context} is non-finite or has a non-positive diagonal variance.");

            double maxAbsDelta = MaximumAbsoluteDifference(candidate, regularized);
            covariance = regularized;
            CovarianceStatus = CovarianceComputationStatus.Regularized;
            CovarianceDiagnostic =
                $"{context} was regularized to positive definiteness (maximum absolute adjustment {maxAbsDelta:G6}).";
            Debug.WriteLine(CovarianceDiagnostic);

            return true;
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
        /// Returns the sandwich (robust) covariance matrix for heteroskedasticity-consistent standard errors.
        /// </summary>
        /// <returns>The robust covariance matrix using the sandwich estimator.</returns>
        /// <remarks>
        /// <para>
        /// The sandwich estimator is: Var(?^) = H?� J H?�
        /// </para>
        /// <para>
        /// Where H is the negative Hessian (Fisher Information Matrix) and J is the "meat" matrix:
        /// J = S? ?log p(y?|?) ?log p(y?|?)?
        /// </para>
        /// <para>
        /// This estimator provides consistent standard errors even when the model is misspecified
        /// or when there is heteroskedasticity in the data.
        /// </para>
        /// <para>
        /// <b>References:</b>
        /// White, H. (1980). A Heteroskedasticity-Consistent Covariance Matrix Estimator and a Direct Test
        /// for Heteroskedasticity. Econometrica, 48(4), 817-838.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated or the Hessian is null.</exception>
        public Matrix GetSandwichCovarianceMatrix()
        {
            if (!TryGetSandwichCovarianceMatrix(out Matrix covariance))
            {
                throw new InvalidOperationException(
                    CovarianceDiagnostic ?? "The sandwich covariance matrix is unavailable.");
            }

            return covariance;
        }

        /// <summary>
        /// Attempts to compute the robust sandwich covariance matrix.
        /// </summary>
        /// <param name="covariance">
        /// The robust covariance matrix when successful; otherwise, a zero matrix that must
        /// not be interpreted as estimated uncertainty.
        /// </param>
        /// <returns><see langword="true"/> when a usable robust covariance matrix is available.</returns>
        /// <remarks>
        /// The result updates <see cref="CovarianceStatus"/> and <see cref="CovarianceDiagnostic"/>.
        /// </remarks>
        public bool TryGetSandwichCovarianceMatrix(out Matrix covariance)
        {
            covariance = new Matrix(NumberOfParameters, NumberOfParameters);
            if (!IsEstimated)
                return SetCovarianceFailure("The model has not been estimated.");
            if (_hessian == null)
                return SetCovarianceFailure("The Hessian is unavailable. Estimation or Hessian computation may have failed.");

            try
            {
                Matrix fisher = _hessian * -1d;
                Matrix fisherInverse = fisher.Inverse();
                Matrix meat = ComputeMeatMatrix(BestParameterSet.Values);
                Matrix candidate = fisherInverse * meat * fisherInverse;
                return TryRegularizeAndValidateCovariance(
                    candidate,
                    "Maximum-likelihood sandwich covariance",
                    out covariance);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to compute sandwich covariance matrix: {ex.Message}");
                return SetCovarianceFailure(
                    $"Maximum-likelihood sandwich covariance computation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the robust (sandwich) standard errors of the parameter estimates.
        /// </summary>
        /// <returns>An array of robust standard errors for each parameter.</returns>
        /// <remarks>
        /// These standard errors are consistent even under model misspecification or heteroskedasticity.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated or the Hessian is null.</exception>
        public double[] GetRobustStandardErrors()
        {
            var sandwich = GetSandwichCovarianceMatrix();
            var robustSE = new double[NumberOfParameters];

            for (int i = 0; i < NumberOfParameters; i++)
            {
                robustSE[i] = Math.Sqrt(Math.Max(0, sandwich[i, i])); // Ensure non-negative
            }

            return robustSE;
        }

        /// <summary>
        /// Computes the "meat" matrix J = S? g? g?? for the sandwich estimator.
        /// </summary>
        /// <param name="parameters">The parameter values at which to evaluate gradients.</param>
        /// <returns>The meat matrix.</returns>
        private Matrix ComputeMeatMatrix(double[] parameters)
        {
            // Get pointwise log-likelihoods
            double[] pointwiseLL = Model.PointwiseDataLogLikelihood(parameters);
            int n = pointwiseLL.Length;

            // Compute numerical gradients for each observation
            double[][] gradients = ComputePointwiseGradients(parameters, n);

            // Compute J = S? g? g??
            var meat = new Matrix(NumberOfParameters, NumberOfParameters);

            for (int i = 0; i < n; i++)
            {
                double[] g = gradients[i];
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    for (int k = 0; k < NumberOfParameters; k++)
                    {
                        meat[j, k] += g[j] * g[k];
                    }
                }
            }

            return meat;
        }

        /// <summary>
        /// Computes numerical gradients of pointwise log-likelihoods with respect to parameters
        /// using central differences.
        /// </summary>
        /// <param name="parameters">The parameter values at which to evaluate gradients.</param>
        /// <param name="n">The number of observations (length of pointwise log-likelihood array).</param>
        /// <returns>A jagged array where gradients[i][j] is ?log f(y?|?)/???.</returns>
        /// <remarks>
        /// <para>
        /// Uses the central difference formula: ?f/??? � [f(?+h?e?) - f(?-h?e?)] / (2h?),
        /// where h? = max(|??| � 1e-4, 1e-3). The 1e-3 minimum step size is critical for
        /// distributions that use Normal approximations near zero (e.g., LP3 switches to Normal
        /// when |?| &lt; 1e-4).
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
        /// Computes influence[i,j] = (H?� g?)? / SE?, where:
        /// </para>
        /// <list type="bullet">
        /// <item><description>g? is the score vector (gradient of log f(y?|?)) for observation i</description></item>
        /// <item><description>H?� is the inverse Fisher information matrix (-Hessian)?�, using data-only Hessian</description></item>
        /// <item><description>SE? is the standard error of parameter j</description></item>
        /// </list>
        /// <para>
        /// Unlike <see cref="MaximumAPosteriori.GetObservationInfluence"/>, this uses the data-only
        /// Hessian (no prior contribution), so influence reflects purely data-driven effects.
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
            // Compute influence: I_ij = (H?� g?)_j / SE_j
            var influence = new double[n, NumberOfParameters];
            var se = Enumerable.Range(0, NumberOfParameters)
                .Select(index => Math.Sqrt(fisherInv[index, index]))
                .ToArray();

            for (int i = 0; i < n; i++)
            {
                // Compute H?� g?
                for (int j = 0; j < NumberOfParameters; j++)
                {
                    double inflJ = 0;
                    for (int k = 0; k < NumberOfParameters; k++)
                    {
                        inflJ += fisherInv[j, k] * gradients[i][k];
                    }
                    // Scale by standard error for comparability
                    influence[i, j] = se[j] > 0 ? inflJ / se[j] : 0;
                }
            }

            return influence;
        }

        /// <summary>
        /// Returns Cook's distance-like measure for each observation.
        /// </summary>
        /// <returns>
        /// An array of Cook's distance values, one per observation.
        /// Values greater than 1 indicate highly influential observations; 4/n is a common threshold.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Computes D_i = g?? H?� g? / p, where:
        /// </para>
        /// <list type="bullet">
        /// <item><description>g? is the score vector (gradient of log f(y?|?)) for observation i</description></item>
        /// <item><description>H?� is the inverse Fisher information matrix (-Hessian)?�, using data-only Hessian</description></item>
        /// <item><description>p is the number of parameters</description></item>
        /// </list>
        /// <para>
        /// This uses the data-only Hessian from the MLE optimizer. See
        /// <see cref="MaximumAPosteriori.GetCooksDistance"/> for the posterior Hessian variant.
        /// </para>
        /// <para>
        /// Reference: Cook, R. D. (1977). Detection of influential observation in linear regression.
        /// Technometrics, 19(1), 15-18.
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
                // Compute g?? H?� g?
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
        /// Computes the Akaike Information Criterion (AIC) for model selection.
        /// </summary>
        /// <returns>The AIC value. Lower values indicate better models.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        public double GetAIC()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");

            return GoodnessOfFit.AIC(NumberOfParameters, MaximumLogLikelihood);
        }

        /// <summary>
        /// Computes the Bayesian Information Criterion (BIC) for model selection.
        /// </summary>
        /// <param name="sampleSize">The sample size (number of observations).</param>
        /// <returns>The BIC value. Lower values indicate better models.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when sample size is less than 1.</exception>
        public double GetBIC(int sampleSize)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (sampleSize < 1)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be at least 1.");

            return GoodnessOfFit.BIC(sampleSize, NumberOfParameters, MaximumLogLikelihood);
        }

        #endregion

    }
}
