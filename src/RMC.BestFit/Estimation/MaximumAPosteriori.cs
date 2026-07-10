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
        private void SetUpOptimizer()
        {
            // Store parameter constraints
            InitialValues = Model.Parameters.Select(x => x.Value).ToArray();
            LowerBounds = Model.Parameters.Select(x => x.LowerBound).ToArray();
            UpperBounds = Model.Parameters.Select(x => x.UpperBound).ToArray();

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
        /// Estimates the model parameters that maximize the likelihood function.
        /// </summary>
        /// <returns>True if estimation was successful; otherwise, false.</returns>
        public bool Estimate()
        {
            IsEstimated = false;
            Status = OptimizationStatus.None;
            TotalFunctionEvaluations = 0;
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
                            _hessian = NumericalDiff.ComputeHessian(Model.LogLikelihood, BestParameterSet.Values, NumberOfParameters);
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
                    profile[j, 1] = Model.LogLikelihood(parms);
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
                var LLH = Model.LogLikelihood(parms);
                if (LLH < threshold)
                {
                    CIs[i, 0] = Brent.Solve((x) =>
                    {
                        parms[i] = x;
                        return Model.LogLikelihood(parms) - threshold;
                    }, Model.Parameters[i].LowerBound, BestParameterSet.Values[i]);
                }
                else
                {
                    CIs[i, 0] = Model.Parameters[i].LowerBound;
                }

                // Upper limit: Check if upper bound exceeds threshold
                parms[i] = Model.Parameters[i].UpperBound;
                var ULH = Model.LogLikelihood(parms);
                if (ULH < threshold)
                {
                    CIs[i, 1] = Brent.Solve((x) =>
                    {
                        parms[i] = x;
                        return Model.LogLikelihood(parms) - threshold;
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
                // Regularize to ensure positive definiteness
                covariance = MatrixRegularization.MakeSymmetricPositiveDefinite(covariance);
                return covariance;
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
        /// Computes the Akaike Information Criterion (AIC) for model selection at the
        /// MAP estimate, using the full posterior log-likelihood (data + prior).
        /// </summary>
        /// <returns>The AIC value. Lower values indicate better models.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <remarks>
        /// <para>
        /// Uses <see cref="IModel.LogLikelihood(double[])"/> (data + prior), matching the
        /// convention used elsewhere in the framework (UnivariateAnalysis, BivariateAnalysis,
        /// CompetingRiskAnalysis, MixtureAnalysis, PointProcessAnalysis, RatingCurveAnalysis,
        /// SpatialGEVAnalysis, the time-series analyses, B17C). With **uniform / improper-flat
        /// priors** the prior contribution is constant in ?, and the value reduces to the
        /// conventional MLE-based AIC.
        /// </para>
        /// <para>
        /// **Caveat for informative priors.** When priors are informative, the prior log-density
        /// at the MAP point is parameter-dependent and adds to the AIC value. AIC's effective-
        /// parameter penalty (2k) is not adjusted for prior information, so direct AIC
        /// comparison across analyses with different priors can be misleading. The same caveat
        /// applies more strongly to BIC.
        /// </para>
        /// <para>
        /// **Recommended alternatives with informative priors:** prefer the Deviance Information
        /// Criterion (DIC) or Watanabe-Akaike Information Criterion (WAIC), or LOO-CV with PSIS,
        /// all of which integrate over the posterior and properly account for the effective
        /// number of parameters under the prior. These are produced by <see cref="BayesianAnalysis"/>
        /// after MCMC.
        /// </para>
        /// </remarks>
        public double GetAIC()
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");

            return GoodnessOfFit.AIC(NumberOfParameters, Model.LogLikelihood(BestParameterSet.Values));
        }

        /// <summary>
        /// Computes the Bayesian Information Criterion (BIC) for model selection at the
        /// MAP estimate, using the full posterior log-likelihood (data + prior).
        /// </summary>
        /// <param name="sampleSize">The sample size (number of observations).</param>
        /// <returns>The BIC value. Lower values indicate better models.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the model has not been estimated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when sample size is less than 1.</exception>
        /// <remarks>
        /// <para>
        /// Uses <see cref="IModel.LogLikelihood(double[])"/> (data + prior), matching the
        /// convention used elsewhere in the framework. With **uniform / improper-flat priors**
        /// the prior contribution is constant in ? and the value reduces to the conventional
        /// MLE-based BIC.
        /// </para>
        /// <para>
        /// **Caveat for informative priors.** BIC was derived as a Laplace approximation to
        /// the marginal likelihood under uniform priors, so its `k·ln(n)` penalty does not
        /// adjust for prior information. With informative priors the value still rank-orders
        /// candidate models at fixed prior, but absolute values and cross-prior comparisons
        /// can be misleading.
        /// </para>
        /// <para>
        /// **Recommended alternatives with informative priors:** prefer DIC, WAIC, or LOO-CV
        /// with PSIS — all are produced by <see cref="BayesianAnalysis"/> after MCMC and
        /// properly account for effective parameter count under the prior.
        /// </para>
        /// </remarks>
        public double GetBIC(int sampleSize)
        {
            if (!IsEstimated)
                throw new InvalidOperationException("The model has not been estimated.");
            if (sampleSize < 1)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be at least 1.");

            return GoodnessOfFit.BIC(sampleSize, NumberOfParameters, Model.LogLikelihood(BestParameterSet.Values));
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
        /// Uses the central difference formula: ?f/??? ˜ [f(?+h?e?) - f(?-h?e?)] / (2h?),
        /// where h? = max(|??| × 1e-4, 1e-3). Same step size strategy as
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

            // Get inverse of the negative posterior Hessian (includes prior)
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
                Debug.WriteLine($"Posterior Hessian inversion failed in GetObservationInfluence: {ex.Message}");
                return new double[n, NumberOfParameters];
            }

            // Compute influence: I_ij = (H?¹ g?)_j / SE_j
            var influence = new double[n, NumberOfParameters];
            var se = GetStandardErrors();

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
        /// Cook's D_i = g?? H?¹ g? / p where H is the full posterior Hessian (data + prior)
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

            // Get inverse of the negative posterior Hessian (includes prior)
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
                Debug.WriteLine($"Posterior Hessian inversion failed in GetCooksDistance: {ex.Message}");
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

