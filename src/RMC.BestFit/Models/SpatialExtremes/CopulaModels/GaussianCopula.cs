using Numerics;
using Numerics.Distributions;

namespace RMC.BestFit.Models.SpatialExtremes
{
    /// <summary>
    /// Gaussian copula for modeling spatial dependence in extreme values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Gaussian copula models the dependence structure between marginal distributions
    /// by transforming them to standard normal variables and applying a multivariate normal
    /// distribution with correlation matrix R.
    /// </para>
    /// <para>
    /// For observations x = (x_1, ..., x_n) with marginals F_i, the copula density is:
    /// c(u_1, ..., u_n) = |R|^(-1/2) * exp(-0.5 * z' * (R^-1 - I) * z)
    /// where z_i = Φ^(-1)(u_i) and u_i = F_i(x_i).
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     - Renard, B., et al. (2006). Use of a Gaussian copula for multivariate extreme value analysis.
    ///       Journal of Hydrology, 315(1-4), 203-215.
    ///     - Genest, C., and Favre, A.C. (2007). Everything you always wanted to know about copula modeling
    ///       but were afraid to ask. Journal of Hydrologic Engineering, 12(4), 347-368.
    /// </para>
    /// </remarks>
    public class GaussianCopula
    {
        private double[,] _coordinates;
        private CorrelationFunctionType _correlationFunctionType;
        private ICorrelationModel _correlationFunction = null!;
        private CachedMultivariateNormal _mvn;
        private double[,] _distanceMatrix = null!;

        /// <summary>
        /// Creates a new Gaussian copula for spatial dependence modeling.
        /// </summary>
        /// <param name="coordinates">The coordinates (X, Y) or (Lat, Lon) of the sites.</param>
        /// <param name="correlationType">The spatial correlation function type.</param>
        public GaussianCopula(double[,] coordinates, CorrelationFunctionType correlationType)
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
        /// Gets the list of correlation function parameters.
        /// </summary>
        public List<ModelParameter> Parameters => _correlationFunction.Parameters;

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
        /// Sets the correlation function parameter values.
        /// </summary>
        /// <param name="values">The parameter values.</param>
        public void SetParameterValues(IList<double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            _correlationFunction.SetParameterValues(values);

            // Build correlation matrix: R_ij = ρ(h_ij)
            var corrMatrix = new double[Sites, Sites];
            for (int i = 0; i < Sites; i++)
            {
                for (int j = 0; j < Sites; j++)
                {
                    if (i == j)
                    {
                        corrMatrix[i, j] = 1.0;
                    }
                    else
                    {
                        double h = _distanceMatrix[i, j];
                        corrMatrix[i, j] = _correlationFunction.Evaluate(h);
                    }
                }
            }

            var mean = new double[Sites]; // Zero mean
            _mvn.SetMean(mean);
            _mvn.SetCovariance(corrMatrix);
        }

        /// <summary>
        /// Computes the copula probability density function.
        /// </summary>
        /// <param name="z">The vector of standard normal transformed values.</param>
        /// <returns>The copula density c(u), where u_i = Φ(z_i).</returns>
        public double PDF(IList<double> z)
        {
            if (z == null)
                throw new ArgumentNullException(nameof(z));
            if (z.Count != Sites)
                throw new ArgumentException("Input vector dimension mismatch.", nameof(z));

            // Copula PDF: φ_R(z) / ∏φ(z_i)
            double numerator = _mvn.PDF(z.ToArray());
            double denominator = 1.0;
            for (int i = 0; i < z.Count; i++)
                denominator *= Normal.StandardPDF(z[i]);

            if (denominator <= 0)
                return 0.0;

            return numerator / denominator;
        }

        /// <summary>
        /// Computes the log copula probability density function.
        /// </summary>
        /// <param name="z">The vector of standard normal transformed values.</param>
        /// <returns>The log copula density log(c(u)), where u_i = Φ(z_i).</returns>
        public double LogPDF(IList<double> z)
        {
            if (z == null)
                throw new ArgumentNullException(nameof(z));
            if (z.Count != Sites)
                throw new ArgumentException("Input vector dimension mismatch.", nameof(z));

            // Log copula PDF: log φ_R(z) - ∑log φ(z_i)
            double numerator = _mvn.LogPDF(z.ToArray());
            if (numerator == double.NegativeInfinity)
                return double.NegativeInfinity;

            double denominator = 0.0;
            for (int i = 0; i < z.Count; i++)
                denominator += Normal.StandardLogPDF(z[i]);

            return numerator - denominator;
        }

        /// <summary>
        /// Returns a deep copy of the Gaussian copula.
        /// </summary>
        public GaussianCopula Clone()
        {
            var clone = new GaussianCopula(_coordinates, _correlationFunctionType);

            // Copy correlation function parameters
            for (int i = 0; i < Parameters.Count; i++)
            {
                clone.Parameters[i].Value = Parameters[i].Value;
                clone.Parameters[i].LowerBound = Parameters[i].LowerBound;
                clone.Parameters[i].UpperBound = Parameters[i].UpperBound;
                if (Parameters[i].PriorDistribution is not null)
                    clone.Parameters[i].PriorDistribution = Parameters[i].PriorDistribution.Clone();
            }

            // Update MVN with current values
            var values = Parameters.Select(p => p.Value).ToList();
            clone.SetParameterValues(values);

            return clone;
        }
    }
}
