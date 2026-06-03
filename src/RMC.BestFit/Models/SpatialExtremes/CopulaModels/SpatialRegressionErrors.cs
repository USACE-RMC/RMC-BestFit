using Numerics;
using Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RMC.BestFit.Models.SpatialExtremes
{
    /// <summary>
    /// Spatial regression errors using Gaussian Process (GP) framework.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Models spatially correlated errors in regression parameters following Renard's BHM framework.
    /// The errors follow a multivariate normal distribution:
    /// ε ~ MVN(0, Σ) where Σ_ij = σ² * ρ(h_ij)
    /// </para>
    /// <para>
    /// Parameter structure: [σ, correlation_params..., ε_1, ε_2, ..., ε_n]
    /// - σ: Overall error variance (scale parameter)
    /// - correlation_params: Spatial correlation function parameters
    /// - ε_i: Latent spatial error at site i
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     - Renard, B., et al. (2006). Use of a Gaussian copula for multivariate extreme value analysis. 
    ///       Journal of Hydrology, 315(1-4), 203-215.
    ///     - Cooley, D., Naveau, P., and Poncet, P. (2006). Variograms for spatial max-stable random fields.
    /// </para>
    /// </remarks>
    public class SpatialRegressionErrors
    {
        private double[,] _coordinates;
        private CorrelationFunctionType _correlationFunctionType;
        private ICorrelationModel _correlationFunction = null!;
        private CachedMultivariateNormal _mvn;
        private double[,] _distanceMatrix = null!;

        /// <summary>
        /// Creates a new spatial regression errors model.
        /// </summary>
        /// <param name="coordinates">The coordinates (X, Y) or (Lat, Lon) of the sites.</param>
        /// <param name="correlationType">The spatial correlation function type.</param>
        /// <param name="maxError">Maximum error bound for initialization. Default = 10.</param>
        public SpatialRegressionErrors(double[,] coordinates, CorrelationFunctionType correlationType, double maxError = 10)
        {
            if (coordinates == null)
                throw new ArgumentNullException(nameof(coordinates));
            if (coordinates.GetLength(1) != 2)
                throw new ArgumentException("Coordinates must be n×2 array (X,Y) or (Lat,Lon).", nameof(coordinates));

            _coordinates = coordinates;
            _correlationFunctionType = correlationType;

            // Initialize correlation function
            if (_correlationFunctionType == CorrelationFunctionType.Exponential)
                _correlationFunction = new BasicExponential();
            else if (_correlationFunctionType == CorrelationFunctionType.PoweredExponential)
                _correlationFunction = new PoweredExponential();
            else if (_correlationFunctionType == CorrelationFunctionType.Spherical)
                _correlationFunction = new Spherical();

            _mvn = new CachedMultivariateNormal(Sites);

            // Precompute distance matrix
            ComputeDistanceMatrix();

            SetDefaultParameters(maxError);
        }

        /// <summary>
        /// Gets the number of sites in the spatial model.
        /// </summary>
        public int Sites => _coordinates.GetLength(0);

        /// <summary>
        /// Gets the spatial correlation function.
        /// </summary>
        public ICorrelationModel CorrelationFunction => _correlationFunction;

        /// <summary>
        /// Gets the list of all model parameters [σ, corr_params..., ε_1, ..., ε_n].
        /// </summary>
        public List<ModelParameter> Parameters { get; private set; } = null!;

        /// <summary>
        /// Gets the list of error parameters [ε_1, ε_2, ..., ε_n] (view into Parameters).
        /// </summary>
        public List<ModelParameter> ErrorParameters { get; private set; } = null!;

        /// <summary>
        /// Gets the number of model parameters.
        /// </summary>
        public int NumberOfParameters => Parameters.Count;

        /// <summary>
        /// Precomputes the distance matrix between all site pairs.
        /// </summary>
        private void ComputeDistanceMatrix()
        {
            _distanceMatrix = new double[Sites, Sites];
            for (int i = 0; i < Sites; i++)
            {
                for (int j = 0; j < Sites; j++)
                {
                    if (i == j)
                    {
                        _distanceMatrix[i, j] = 0.0;
                    }
                    else
                    {
                        _distanceMatrix[i, j] = Tools.Distance(
                            _coordinates[i, 0], _coordinates[i, 1],
                            _coordinates[j, 0], _coordinates[j, 1]);
                    }
                }
            }
        }

        /// <summary>
        /// Sets default parameters for the spatial error model.
        /// </summary>
        /// <param name="maxError">Maximum error bound.</param>
        public void SetDefaultParameters(double maxError)
        {
            Parameters = new List<ModelParameter>();

            // Scale parameter (σ)
            Parameters.Add(new ModelParameter()
            {
                Name = "Error Scale (σ)",
                Value = maxError / 3.0,
                LowerBound = Tools.DoubleMachineEpsilon,
                UpperBound = maxError,
                IsPositive = true,
                PriorDistribution = new Uniform(Tools.DoubleMachineEpsilon, maxError)
            });

            // Correlation function parameters
            Parameters.AddRange(_correlationFunction.Parameters);

            // Latent error parameters (ε_i for each site)
            ErrorParameters = new List<ModelParameter>();
            for (int i = 0; i < Sites; i++)
            {
                var parm = new ModelParameter()
                {
                    Name = "ε" + SubscriptFormatter.ToSubscript(i + 1),
                    Value = 0.0,
                    LowerBound = -maxError,
                    UpperBound = maxError,
                    PriorDistribution = new Uniform(-maxError, maxError)
                };
                Parameters.Add(parm);
                ErrorParameters.Add(parm);
            }
        }

        /// <summary>
        /// Sets the model parameter values.
        /// </summary>
        /// <param name="values">The parameter values [σ, corr_params..., ε_1, ..., ε_n].</param>
        public void SetParameterValues(IList<double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Count != NumberOfParameters)
                throw new ArgumentException($"Expected {NumberOfParameters} parameters but got {values.Count}.", nameof(values));

            int index = 0;

            // Sigma parameter
            Parameters[0].Value = values[index];
            double sigma = values[index];
            index++;

            // Correlation parameters
            var corrParams = new List<double>();
            for (int i = 0; i < _correlationFunction.Parameters.Count; i++)
            {
                corrParams.Add(values[index]);
                Parameters[index].Value = values[index];
                index++;
            }
            _correlationFunction.SetParameterValues(corrParams);

            // Error parameters
            for (int i = 0; i < Sites; i++)
            {
                Parameters[index].Value = values[index];
                ErrorParameters[i].Value = values[index];
                index++;
            }

            // Update covariance matrix: Σ_ij = σ² * ρ(h_ij)
            var covMatrix = new double[Sites, Sites];
            double sigma2 = sigma * sigma;

            for (int i = 0; i < Sites; i++)
            {
                for (int j = 0; j < Sites; j++)
                {
                    if (i == j)
                    {
                        covMatrix[i, j] = sigma2;
                    }
                    else
                    {
                        double h = _distanceMatrix[i, j];
                        covMatrix[i, j] = sigma2 * _correlationFunction.Evaluate(h);
                    }
                }
            }

            var mean = new double[Sites]; // Zero mean
            _mvn.SetMean(mean);
            _mvn.SetCovariance(covMatrix);
        }

        /// <summary>
        /// Computes the probability density function of the spatial errors.
        /// </summary>
        /// <returns>The PDF value.</returns>
        public double PDF()
        {
            var errors = ErrorParameters.Select(p => p.Value).ToArray();
            return _mvn.PDF(errors);
        }

        /// <summary>
        /// Computes the log probability density function of the spatial errors.
        /// </summary>
        /// <returns>The log-PDF value.</returns>
        public double LogPDF()
        {
            var errors = ErrorParameters.Select(p => p.Value).ToArray();
            return _mvn.LogPDF(errors);
        }

        /// <summary>
        /// Gets the error value for a specific site.
        /// </summary>
        /// <param name="siteIndex">The site index (0-based).</param>
        /// <returns>The error value ε_i.</returns>
        public double GetError(int siteIndex)
        {
            if (siteIndex < 0 || siteIndex >= Sites)
                throw new ArgumentOutOfRangeException(nameof(siteIndex));
            return ErrorParameters[siteIndex].Value;
        }

        /// <summary>
        /// Predicts the error at an ungauged location using simple kriging (conditional Gaussian process).
        /// </summary>
        /// <param name="newCoordinates">The coordinates [X, Y] or [Lat, Lon] of the ungauged location.</param>
        /// <returns>A tuple containing (predicted mean, prediction variance).</returns>
        /// <remarks>
        /// <para>
        /// Uses the conditional distribution of a Gaussian process to predict the error at a new location
        /// given the observed errors at existing sites. This is "simple kriging" with known mean (zero).
        /// </para>
        /// <para>
        /// The prediction is:
        /// ε* | ε ~ N(k*ᵀ K⁻¹ ε, k** - k*ᵀ K⁻¹ k*)
        /// where:
        /// - k* is the covariance between the new location and all existing sites
        /// - K is the covariance matrix of existing sites
        /// - k** is the variance at the new location (σ²)
        /// - ε are the observed errors at existing sites
        /// </para>
        /// <para>
        ///     <b>References:</b>
        ///     - Rasmussen, C.E., and Williams, C.K.I. (2006). Gaussian Processes for Machine Learning. MIT Press.
        ///     - Cressie, N. (1993). Statistics for Spatial Data. Wiley.
        /// </para>
        /// </remarks>
        public (double Mean, double Variance) GetKrigingPrediction(double[] newCoordinates)
        {
            if (newCoordinates == null || newCoordinates.Length != 2)
                throw new ArgumentException("Coordinates must be a 2-element array [X, Y].", nameof(newCoordinates));

            double sigma = Parameters[0].Value;
            double sigma2 = sigma * sigma;

            // Compute distances from new location to all existing sites
            var distToNew = new double[Sites];
            for (int i = 0; i < Sites; i++)
            {
                distToNew[i] = Tools.Distance(
                    newCoordinates[0], newCoordinates[1],
                    _coordinates[i, 0], _coordinates[i, 1]);
            }

            // Compute k* (covariance between new location and existing sites)
            var kStar = new double[Sites];
            for (int i = 0; i < Sites; i++)
            {
                kStar[i] = sigma2 * _correlationFunction.Evaluate(distToNew[i]);
            }

            // Get current covariance matrix from MVN (already computed in SetParameterValues)
            // We need to compute K⁻¹ * ε and K⁻¹ * k*
            var errors = ErrorParameters.Select(p => p.Value).ToArray();

            // Build covariance matrix K
            var K = new double[Sites, Sites];
            for (int i = 0; i < Sites; i++)
            {
                for (int j = 0; j < Sites; j++)
                {
                    if (i == j)
                    {
                        K[i, j] = sigma2;
                    }
                    else
                    {
                        K[i, j] = sigma2 * _correlationFunction.Evaluate(_distanceMatrix[i, j]);
                    }
                }
            }

            // Compute Cholesky decomposition for numerical stability
            // K = L * L^T
            var L = CholeskyDecomposition(K);
            if (L == null)
            {
                // Fallback to IDW if Cholesky fails
                return GetIDWPrediction(newCoordinates);
            }

            // Solve L * y = errors, then L^T * alpha = y to get alpha = K⁻¹ * errors
            var y = ForwardSolve(L, errors);
            var alpha = BackwardSolve(L, y);

            // Solve L * z = kStar, then L^T * v = z to get v = K⁻¹ * k*
            var z = ForwardSolve(L, kStar);
            var v = BackwardSolve(L, z);

            // Predicted mean: k*^T * K⁻¹ * ε = k*^T * alpha
            double predMean = 0;
            for (int i = 0; i < Sites; i++)
            {
                predMean += kStar[i] * alpha[i];
            }

            // Predicted variance: k** - k*^T * K⁻¹ * k* = σ² - k*^T * v
            double kStarStar = sigma2; // Variance at new location
            double quadForm = 0;
            for (int i = 0; i < Sites; i++)
            {
                quadForm += kStar[i] * v[i];
            }
            double predVar = Math.Max(kStarStar - quadForm, 0); // Ensure non-negative

            return (predMean, predVar);
        }

        /// <summary>
        /// Predicts the error at an ungauged location using inverse-distance weighting.
        /// </summary>
        /// <param name="newCoordinates">The coordinates [X, Y] or [Lat, Lon] of the ungauged location.</param>
        /// <returns>The predicted error value.</returns>
        /// <remarks>
        /// This is a simpler alternative to kriging that doesn't require matrix inversion.
        /// Uses power parameter p=2 (inverse squared distance).
        /// </remarks>
        public (double Mean, double Variance) GetIDWPrediction(double[] newCoordinates)
        {
            if (newCoordinates == null || newCoordinates.Length != 2)
                throw new ArgumentException("Coordinates must be a 2-element array [X, Y].", nameof(newCoordinates));

            double sumInvDist = 0;
            double sumWeightedError = 0;
            double sumWeightedVar = 0;
            double sigma2 = Parameters[0].Value * Parameters[0].Value;

            for (int i = 0; i < Sites; i++)
            {
                double dist = Tools.Distance(
                    newCoordinates[0], newCoordinates[1],
                    _coordinates[i, 0], _coordinates[i, 1]);
                dist = Math.Max(dist, 1e-10); // Avoid division by zero

                double weight = 1.0 / (dist * dist); // IDW with p=2
                sumInvDist += weight;
                sumWeightedError += weight * ErrorParameters[i].Value;
                sumWeightedVar += weight * weight * sigma2;
            }

            double predMean = sumWeightedError / sumInvDist;
            double predVar = sumWeightedVar / (sumInvDist * sumInvDist);

            return (predMean, predVar);
        }

        /// <summary>
        /// Performs Cholesky decomposition of a symmetric positive-definite matrix.
        /// If <paramref name="A"/> is numerically indefinite, retries on
        /// <c>A + δI</c> for an increasing diagonal jitter <c>δ ∈ {1e-8, 1e-6, 1e-4, 1e-2}</c>
        /// so the returned <c>L</c> is the true Cholesky factor of the regularized matrix.
        /// Returns <c>null</c> if all jitter levels fail.
        /// </summary>
        /// <param name="A">The input matrix.</param>
        /// <returns>
        /// The lower triangular matrix <c>L</c> such that <c>L Lᵀ = A + δI</c>
        /// (with <c>δ = 0</c> when the input was already positive-definite), or
        /// <c>null</c> if even the largest jitter is insufficient.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The original implementation perturbed only the failing diagonal entry,
        /// producing an <c>L</c> for which <c>L Lᵀ ≠ A</c>. That broke kriging
        /// predictions and variances on near-singular covariance matrices. The
        /// uniform-diagonal jitter implemented here is the standard regularization
        /// (a.k.a. "Cholesky with jitter" in the GP literature) and yields a valid
        /// factorization of <c>A + δI</c> at the cost of a small bias proportional
        /// to <c>δ</c>.
        /// </para>
        /// </remarks>
        private double[,]? CholeskyDecomposition(double[,] A)
        {
            // Try unperturbed first, then escalating jitter on the diagonal.
            double[] jitterLevels = { 0.0, 1e-8, 1e-6, 1e-4, 1e-2 };
            foreach (var jitter in jitterLevels)
            {
                var L = TryCholesky(A, jitter);
                if (L != null) return L;
            }
            return null;
        }

        /// <summary>
        /// Attempts a Cholesky factorization of <c>A + jitter·I</c>. Returns
        /// <c>null</c> if any diagonal element is non-positive (matrix indefinite
        /// after the jitter).
        /// </summary>
        /// <param name="A">The input matrix.</param>
        /// <param name="jitter">Uniform diagonal addition to regularize.</param>
        /// <returns>
        /// The lower triangular Cholesky factor of <c>A + jitter·I</c>, or
        /// <c>null</c> on indefiniteness.
        /// </returns>
        private static double[,]? TryCholesky(double[,] A, double jitter)
        {
            int n = A.GetLength(0);
            var L = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < j; k++)
                    {
                        sum += L[i, k] * L[j, k];
                    }

                    if (i == j)
                    {
                        // Add jitter uniformly to every diagonal so the resulting L
                        // is the true Cholesky factor of A + jitter·I (not a hybrid).
                        double diag = A[i, i] + jitter - sum;
                        if (diag <= 0) return null;
                        L[i, j] = Math.Sqrt(diag);
                    }
                    else
                    {
                        L[i, j] = (A[i, j] - sum) / L[j, j];
                    }
                }
            }

            return L;
        }

        /// <summary>
        /// Solves L * x = b for x using forward substitution.
        /// </summary>
        private double[] ForwardSolve(double[,] L, double[] b)
        {
            int n = b.Length;
            var x = new double[n];

            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int j = 0; j < i; j++)
                {
                    sum += L[i, j] * x[j];
                }
                x[i] = (b[i] - sum) / L[i, i];
            }

            return x;
        }

        /// <summary>
        /// Solves L^T * x = b for x using backward substitution.
        /// </summary>
        private double[] BackwardSolve(double[,] L, double[] b)
        {
            int n = b.Length;
            var x = new double[n];

            for (int i = n - 1; i >= 0; i--)
            {
                double sum = 0;
                for (int j = i + 1; j < n; j++)
                {
                    sum += L[j, i] * x[j]; // L^T[i,j] = L[j,i]
                }
                x[i] = (b[i] - sum) / L[i, i];
            }

            return x;
        }

        /// <summary>
        /// Gets the coordinates of all sites.
        /// </summary>
        public double[,] Coordinates => _coordinates;

        /// <summary>
        /// Gets the precomputed distance matrix.
        /// </summary>
        public double[,] DistanceMatrix => _distanceMatrix;

        /// <summary>
        /// Returns a deep copy of the spatial regression errors model.
        /// </summary>
        public SpatialRegressionErrors Clone()
        {
            var clone = new SpatialRegressionErrors(_coordinates, _correlationFunctionType,
                Parameters[0].UpperBound);

            // Copy all parameter values and bounds
            for (int i = 0; i < Parameters.Count; i++)
            {
                clone.Parameters[i].Value = Parameters[i].Value;
                clone.Parameters[i].LowerBound = Parameters[i].LowerBound;
                clone.Parameters[i].UpperBound = Parameters[i].UpperBound;
                if (Parameters[i].PriorDistribution is not null)
                    clone.Parameters[i].PriorDistribution = Parameters[i].PriorDistribution.Clone();
            }

            // Update the MVN with current values
            var values = Parameters.Select(p => p.Value).ToList();
            clone.SetParameterValues(values);

            return clone;
        }
    }
}
