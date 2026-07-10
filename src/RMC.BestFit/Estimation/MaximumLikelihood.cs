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
                            _hessian = NumericalDiff.ComputeHessian(Model.DataLogLikelihood, BestParameterSet.Values, NumberOfParameters);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Hessian computation failed: {ex.Message}");
                            _hessian = null;
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
        }

        /// <summary>
        /// Returns the profile likelihood for each model parameter.
        /// </summary>
        /// <param name="bins">The number of bins in each profile. Default = 100.</param>
        /// <returns>A list of arrays where each array contains [parameter value, log-likelihood] pairs.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when bins is less than 2.</exception>
        public List<double[,]> ProfileLikelihood(int bins = 100)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (bins < 2)
                throw new ArgumentOutOfRangeException(nameof(bins), "Number of bins must be at least 2.");

            var list = new List<double[,]>();

            for (int i = 0; i < NumberOfParameters; i++)
            {
                var seq = Stratify.XValues(new StratificationOptions(Model.Parameters[i].LowerBound, Model.Parameters[i].UpperBound, bins));
                var parms = new double[NumberOfParameters];
                BestParameterSet.Values.CopyTo(parms, 0);
                var profile = new double[bins, 2];

                for (int j = 0; j < bins; j++)
                {
                    parms[i] = seq[j].Midpoint;
                    profile[j, 0] = seq[j].Midpoint;
                    profile[j, 1] = Model.DataLogLikelihood(parms);
                }

                list.Add(profile);
            }

            return list;
        }

        /// <summary>
        /// Returns the parameter confidence intervals based on profile likelihood using the chi-squared threshold.
        /// </summary>
        /// <param name="alpha">The significance level. Default = 0.1 (90% confidence). Must be between 0 and 1.</param>
        /// <returns>A matrix where each row contains [lower bound, upper bound] for each parameter.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when alpha is not between 0 and 1.</exception>
        public double[,] ParameterConfidenceIntervals(double alpha = 0.1)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (alpha <= 0 || alpha >= 1)
                throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be between 0 and 1.");

            var chiSquared = new ChiSquared(1);
            double threshold = -BestParameterSet.Fitness - 0.5 * chiSquared.InverseCDF(1 - alpha);
            var CIs = new double[NumberOfParameters, 2];

            for (int i = 0; i < NumberOfParameters; i++)
            {
                var parms = new double[NumberOfParameters];
                BestParameterSet.Values.CopyTo(parms, 0);

                // Lower limit: Check if lower bound exceeds threshold
                parms[i] = Model.Parameters[i].LowerBound;
                var LLH = Model.DataLogLikelihood(parms);
                if (LLH < threshold)
                {
                    CIs[i, 0] = Brent.Solve((x) =>
                    {
                        parms[i] = x;
                        return Model.DataLogLikelihood(parms) - threshold;
                    }, Model.Parameters[i].LowerBound, BestParameterSet.Values[i]);
                }
                else
                {
                    CIs[i, 0] = Model.Parameters[i].LowerBound;
                }

                // Upper limit: Check if upper bound exceeds threshold
                parms[i] = Model.Parameters[i].UpperBound;
                var ULH = Model.DataLogLikelihood(parms);
                if (ULH < threshold)
                {
                    CIs[i, 1] = Brent.Solve((x) =>
                    {
                        parms[i] = x;
                        return Model.DataLogLikelihood(parms) - threshold;
                    }, BestParameterSet.Values[i], Model.Parameters[i].UpperBound);
                }
                else
                {
                    CIs[i, 1] = Model.Parameters[i].UpperBound;
                }
            }

            return CIs;
        }

        /// <summary>
        /// Returns the parameter covariance matrix computed from the inverse of the Fisher Information Matrix (negative Hessian).
        /// </summary>
        /// <returns>The parameter covariance matrix.</returns>
        /// <exception cref="InvalidOperationException">Thrown when there are fewer than two parameters, the model has not been estimated, or the Hessian is null.</exception>
        public Matrix GetCovarianceMatrix()
        {
            if (NumberOfParameters < 2)
                throw new InvalidOperationException("Cannot compute the covariance matrix with fewer than two parameters.");
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (_hessian == null)
                throw new InvalidOperationException("The Hessian is null. Estimation may have failed.");

            try
            {
                // Fisher Information Matrix is the negative Hessian
                Matrix fisher = _hessian * -1d;
                // Invert to get covariance matrix
                var covariance = fisher.Inverse();
                // Regularize to ensure positive definiteness, logging if the regularization
                // produced a non-trivial change so users with crash-dump or trace logs can tell
                // a "real" covariance from a regularized one.
                var regularized = MatrixRegularization.MakeSymmetricPositiveDefinite(covariance);
                double maxAbsDelta = 0.0;
                int n = covariance.NumberOfRows;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        maxAbsDelta = Math.Max(maxAbsDelta, Math.Abs(regularized[i, j] - covariance[i, j]));
                if (maxAbsDelta > 0.0)
                    Debug.WriteLine($"MaximumLikelihood.GetCovarianceMatrix: matrix regularized to ensure positive-definiteness (max |?| = {maxAbsDelta:G6}).");
                return regularized;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to compute covariance matrix: {ex.Message}");
                return new Matrix(NumberOfParameters, NumberOfParameters);
            }
        }

        /// <summary>
        /// Returns the standard errors of the parameter estimates from the diagonal of the covariance matrix.
        /// </summary>
        /// <returns>An array of standard errors for each parameter.</returns>
        /// <exception cref="InvalidOperationException">Thrown when there are fewer than two parameters or the model has not been estimated.</exception>
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
        /// <exception cref="InvalidOperationException">Thrown when there are fewer than two parameters or the model has not been estimated.</exception>
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
        /// The sandwich estimator is: Var(?^) = H?¹ J H?¹
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
        /// <exception cref="InvalidOperationException">Thrown when there are fewer than two parameters, the model has not been estimated, or the Hessian is null.</exception>
        public Matrix GetSandwichCovarianceMatrix()
        {
            if (NumberOfParameters < 2)
                throw new InvalidOperationException("Cannot compute the covariance matrix with fewer than two parameters.");
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (_hessian == null)
                throw new InvalidOperationException("The Hessian is null. Estimation may have failed.");

            try
            {
                // "Bread": H?¹ = inverse of negative Hessian (Fisher Information Matrix)
                Matrix fisher = _hessian * -1d;
                Matrix fisherInv = fisher.Inverse();

                // "Meat": J = S? g? g?? (outer product of gradients)
                Matrix meat = ComputeMeatMatrix(BestParameterSet.Values);

                // Sandwich: H?¹ J H?¹
                Matrix sandwich = fisherInv * meat * fisherInv;

                // Regularize to ensure positive definiteness
                sandwich = MatrixRegularization.MakeSymmetricPositiveDefinite(sandwich);
                return sandwich;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to compute sandwich covariance matrix: {ex.Message}");
                return new Matrix(NumberOfParameters, NumberOfParameters);
            }
        }

        /// <summary>
        /// Returns the robust (sandwich) standard errors of the parameter estimates.
        /// </summary>
        /// <returns>An array of robust standard errors for each parameter.</returns>
        /// <remarks>
        /// These standard errors are consistent even under model misspecification or heteroskedasticity.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when there are fewer than two parameters or the model has not been estimated.</exception>
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
        /// Uses the central difference formula: ?f/??? ˜ [f(?+h?e?) - f(?-h?e?)] / (2h?),
        /// where h? = max(|??| × 1e-4, 1e-3). The 1e-3 minimum step size is critical for
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
        /// Computes influence[i,j] = (H?¹ g?)? / SE?, where:
        /// </para>
        /// <list type="bullet">
        /// <item><description>g? is the score vector (gradient of log f(y?|?)) for observation i</description></item>
        /// <item><description>H?¹ is the inverse Fisher information matrix (-Hessian)?¹, using data-only Hessian</description></item>
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

            // Get inverse Fisher information from adaptive Hessian
            if (_hessian == null)
                return new double[n, NumberOfParameters];
            Matrix fisher = _hessian * -1d;
            Matrix fisherInv;
            try
            {
                fisherInv = fisher.Inverse();
            }
            catch (Exception ex)
            {
                // Fisher matrix inversion failed - likely singular or ill-conditioned matrix
                Debug.WriteLine($"Fisher matrix inversion failed in GetObservationInfluence: {ex.Message}");
                return new double[n, NumberOfParameters];
            }

            // Compute influence: I_ij = (H?¹ g?)_j / SE_j
            var influence = new double[n, NumberOfParameters];
            var se = GetStandardErrors();

            for (int i = 0; i < n; i++)
            {
                // Compute H?¹ g?
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
        /// Computes D_i = g?? H?¹ g? / p, where:
        /// </para>
        /// <list type="bullet">
        /// <item><description>g? is the score vector (gradient of log f(y?|?)) for observation i</description></item>
        /// <item><description>H?¹ is the inverse Fisher information matrix (-Hessian)?¹, using data-only Hessian</description></item>
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

            // Get inverse Fisher information from adaptive Hessian
            if (_hessian == null)
                return new double[n];
            Matrix fisher = _hessian * -1d;
            Matrix fisherInv;
            try
            {
                fisherInv = fisher.Inverse();
            }
            catch (Exception ex)
            {
                // Fisher matrix inversion failed - likely singular or ill-conditioned matrix
                Debug.WriteLine($"Fisher matrix inversion failed in GetCooksDistance: {ex.Message}");
                return new double[n];
            }

            var cooksD = new double[n];

            for (int i = 0; i < n; i++)
            {
                // Compute g?? H?¹ g?
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
