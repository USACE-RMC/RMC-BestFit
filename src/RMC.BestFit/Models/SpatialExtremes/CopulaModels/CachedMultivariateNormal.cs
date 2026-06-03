using Numerics.Mathematics.LinearAlgebra;
using System.Diagnostics;

namespace RMC.BestFit.Models.SpatialExtremes
{
    /// <summary>
    /// Cached multivariate normal distribution for efficient likelihood computations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class caches the Cholesky decomposition and log-determinant of the covariance matrix
    /// to avoid recomputation in repeated likelihood evaluations. The cache is invalidated when
    /// the covariance matrix changes.
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class CachedMultivariateNormal
    {
        private int _dimension;
        private double[] _mean;
        private double[,] _covariance;
        private Matrix _choleskyL = null!;
        private double _logDeterminant;
        private bool _isCacheValid;

        /// <summary>
        /// Constructs a new cached multivariate normal distribution.
        /// </summary>
        /// <param name="dimension">The dimension of the distribution.</param>
        public CachedMultivariateNormal(int dimension)
        {
            _dimension = dimension;
            _mean = new double[dimension];
            _covariance = new double[dimension, dimension];
            _isCacheValid = false;
        }

        /// <summary>
        /// Constructs a new cached multivariate normal distribution.
        /// </summary>
        /// <param name="mean">The mean vector.</param>
        /// <param name="covariance">The covariance matrix.</param>
        public CachedMultivariateNormal(double[] mean, double[,] covariance)
        {
            if (mean == null)
                throw new ArgumentNullException(nameof(mean));
            if (covariance == null)
                throw new ArgumentNullException(nameof(covariance));
            if (mean.Length != covariance.GetLength(0) || covariance.GetLength(0) != covariance.GetLength(1))
                throw new ArgumentException("Dimension mismatch between mean and covariance.");

            _dimension = mean.Length;
            _mean = (double[])mean.Clone();
            _covariance = (double[,])covariance.Clone();
            _isCacheValid = false;
        }

        /// <summary>
        /// Gets the dimension of the distribution.
        /// </summary>
        public int Dimension => _dimension;

        /// <summary>
        /// Sets the mean vector.
        /// </summary>
        /// <param name="mean">The mean vector.</param>
        public void SetMean(double[] mean)
        {
            if (mean == null)
                throw new ArgumentNullException(nameof(mean));
            if (mean.Length != _dimension)
                throw new ArgumentException("Mean vector dimension mismatch.");

            _mean = (double[])mean.Clone();
        }

        /// <summary>
        /// Sets the covariance matrix and invalidates the cache.
        /// </summary>
        /// <param name="covariance">The covariance matrix.</param>
        public void SetCovariance(double[,] covariance)
        {
            if (covariance == null)
                throw new ArgumentNullException(nameof(covariance));
            if (covariance.GetLength(0) != _dimension || covariance.GetLength(1) != _dimension)
                throw new ArgumentException("Covariance matrix dimension mismatch.");

            _covariance = (double[,])covariance.Clone();
            _isCacheValid = false;
        }

        /// <summary>
        /// Updates the Cholesky decomposition and log-determinant cache.
        /// </summary>
        /// <returns>True if decomposition succeeded, false otherwise.</returns>
        private bool UpdateCache()
        {
            try
            {
                var covMatrix = new Matrix(_covariance);
                var chol = new CholeskyDecomposition(covMatrix);

                if (!chol.IsPositiveDefinite)
                {
                    _isCacheValid = false;
                    return false;
                }

                _choleskyL = chol.L;

                // Compute log-determinant from diagonal of Cholesky factor
                // log|Σ| = 2 * sum(log(L_ii))
                _logDeterminant = 0.0;
                for (int i = 0; i < _dimension; i++)
                {
                    double lii = _choleskyL[i, i];
                    if (lii <= 0)
                    {
                        _isCacheValid = false;
                        return false;
                    }
                    _logDeterminant += Math.Log(lii);
                }
                _logDeterminant *= 2.0;

                _isCacheValid = true;
                return true;
            }
            catch (Exception ex)
            {
                // Cholesky decomposition can fail for ill-conditioned or non-positive-definite matrices
                Debug.WriteLine($"CachedMultivariateNormal.UpdateCache failed: {ex.Message}");
                _isCacheValid = false;
                return false;
            }
        }

        /// <summary>
        /// Computes the log probability density function.
        /// </summary>
        /// <param name="x">The point to evaluate.</param>
        /// <returns>The log-PDF value.</returns>
        public double LogPDF(double[] x)
        {
            if (x == null)
                throw new ArgumentNullException(nameof(x));
            if (x.Length != _dimension)
                throw new ArgumentException("Input vector dimension mismatch.");

            // Update cache if needed
            if (!_isCacheValid)
            {
                if (!UpdateCache())
                    return double.NegativeInfinity;
            }

            // Compute (x - μ)
            var centered = new double[_dimension];
            for (int i = 0; i < _dimension; i++)
                centered[i] = x[i] - _mean[i];

            // Solve L * y = (x - μ) for y
            var y = SolveCholesky(centered);
            if (y == null)
                return double.NegativeInfinity;

            // Compute quadratic form: (x-μ)' * Σ^-1 * (x-μ) = y' * y
            double quadForm = 0.0;
            for (int i = 0; i < _dimension; i++)
                quadForm += y[i] * y[i];

            // Log-PDF: -0.5 * [n*log(2π) + log|Σ| + (x-μ)'Σ^-1(x-μ)]
            double logPDF = -0.5 * (_dimension * Math.Log(2.0 * Math.PI) + _logDeterminant + quadForm);

            return logPDF;
        }

        /// <summary>
        /// Computes the probability density function.
        /// </summary>
        /// <param name="x">The point to evaluate.</param>
        /// <returns>The PDF value.</returns>
        public double PDF(double[] x)
        {
            double logPDF = LogPDF(x);
            if (double.IsNegativeInfinity(logPDF))
                return 0.0;
            return Math.Exp(logPDF);
        }

        /// <summary>
        /// Solves the system L * y = b using forward substitution, where L is the Cholesky factor.
        /// </summary>
        /// <param name="b">The right-hand side vector.</param>
        /// <returns>The solution vector y, or null if solution fails.</returns>
        private double[]? SolveCholesky(double[] b)
        {
            var y = new double[_dimension];

            // Forward substitution: L * y = b
            for (int i = 0; i < _dimension; i++)
            {
                double sum = b[i];
                for (int j = 0; j < i; j++)
                    sum -= _choleskyL[i, j] * y[j];

                double lii = _choleskyL[i, i];
                if (Math.Abs(lii) < 1e-15)
                    return null;

                y[i] = sum / lii;
            }

            return y;
        }

        /// <summary>
        /// Gets the log-determinant of the covariance matrix.
        /// </summary>
        /// <returns>The log-determinant, or <see cref="double.NegativeInfinity"/> if invalid.</returns>
        public double GetLogDeterminant()
        {
            if (!_isCacheValid)
            {
                if (!UpdateCache())
                    return double.NegativeInfinity;
            }
            return _logDeterminant;
        }

        /// <summary>
        /// Gets whether the cache is currently valid.
        /// </summary>
        public bool IsCacheValid => _isCacheValid;

        /// <summary>
        /// Invalidates the cache (forces recomputation on next use).
        /// </summary>
        public void InvalidateCache()
        {
            _isCacheValid = false;
        }
    }
}
