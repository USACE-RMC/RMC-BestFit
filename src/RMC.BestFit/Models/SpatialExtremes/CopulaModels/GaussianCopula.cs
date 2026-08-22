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
    /// A row with unobserved sites is evaluated with <see cref="LogPDF(IList{double}, IReadOnlyList{int})"/>,
    /// which marginalizes the unobserved coordinates: the marginal of a Gaussian copula over a subset
    /// of its coordinates is the Gaussian copula with the corresponding correlation submatrix.
    /// </para>
    /// <para>
    /// Instances are not thread-safe; <see cref="SpatialGEV"/> clones the copula for every
    /// likelihood evaluation so parallel MCMC chains never share an instance.
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
    ///     - Joe, H. (2014). Dependence Modeling with Copulas. CRC Press. (Closure of the Gaussian
    ///       copula family under marginalization.)
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
        /// The correlation matrix built by the most recent <see cref="SetParameterValues"/> call, or
        /// <c>null</c> before any call. Observed-site submatrices are extracted from it.
        /// </summary>
        private double[,]? _correlationMatrix;

        /// <summary>
        /// Factorized observed-site correlation submatrices keyed by the observed-site pattern.
        /// The cache is cleared whenever the correlation parameters change because every entry
        /// depends on the correlation function.
        /// </summary>
        private readonly Dictionary<string, CachedMultivariateNormal> _observedSubsetCache = new();

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

            // The observed-site factorizations depend on the correlation parameters.
            _correlationMatrix = corrMatrix;
            _observedSubsetCache.Clear();
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
        /// Computes the log copula density of the observed sites of a partially observed row,
        /// marginalizing the unobserved sites.
        /// </summary>
        /// <param name="z">The standard normal transformed values, one entry per site; entries at
        /// unobserved sites are ignored.</param>
        /// <param name="observedSites">The zero-based indices of the observed sites in strictly
        /// increasing order.</param>
        /// <returns>
        /// log φ_{R_O}(z_O) - ∑_{j∈O} log φ(z_j), where R_O is the correlation submatrix of the observed
        /// sites O; zero when fewer than two sites are observed (a single site carries no dependence
        /// term); <see cref="double.NegativeInfinity"/> when the submatrix is not positive definite or
        /// the parameters have not been set.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="z"/> or
        /// <paramref name="observedSites"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="z"/> does not have one entry per
        /// site, or when <paramref name="observedSites"/> contains an index outside the site range or is
        /// not strictly increasing.</exception>
        /// <remarks>
        /// <para>
        /// The observed-data likelihood of a row with missing sites integrates the joint density over the
        /// unobserved coordinates. For a Gaussian copula that integral is the Gaussian copula of the
        /// observed coordinates with the correlation submatrix R_O, so no placeholder score is needed for
        /// a missing site. When every site is observed the result equals <see cref="LogPDF(IList{double})"/>
        /// exactly.
        /// </para>
        /// <para>
        /// The Cholesky factorization of each observed-site pattern is cached on this instance until
        /// <see cref="SetParameterValues"/> changes the correlation parameters, so rows that share a
        /// missingness pattern factor the submatrix once per parameter vector.
        /// </para>
        /// </remarks>
        public double LogPDF(IList<double> z, IReadOnlyList<int> observedSites)
        {
            if (z == null)
                throw new ArgumentNullException(nameof(z));
            if (observedSites == null)
                throw new ArgumentNullException(nameof(observedSites));
            if (z.Count != Sites)
                throw new ArgumentException("Input vector dimension mismatch.", nameof(z));

            int observedCount = observedSites.Count;
            if (observedCount > Sites)
                throw new ArgumentException("More observed sites than sites in the copula.", nameof(observedSites));

            int previous = -1;
            for (int k = 0; k < observedCount; k++)
            {
                int site = observedSites[k];
                if (site < 0 || site >= Sites)
                    throw new ArgumentException($"Observed site index {site} is outside the site range.", nameof(observedSites));
                if (site <= previous)
                    throw new ArgumentException("Observed site indices must be strictly increasing.", nameof(observedSites));
                previous = site;
            }

            // A complete row is the full-dimensional density; a row with fewer than two observed
            // sites has no dependence term because the marginal copula density is one.
            if (observedCount == Sites)
                return LogPDF(z);
            if (observedCount < 2)
                return 0.0;
            if (_correlationMatrix == null)
                return double.NegativeInfinity;

            var observedScores = new double[observedCount];
            for (int k = 0; k < observedCount; k++)
                observedScores[k] = z[observedSites[k]];

            double numerator = GetObservedSubsetDistribution(observedSites).LogPDF(observedScores);
            if (numerator == double.NegativeInfinity)
                return double.NegativeInfinity;

            double denominator = 0.0;
            for (int k = 0; k < observedCount; k++)
                denominator += Normal.StandardLogPDF(observedScores[k]);

            return numerator - denominator;
        }

        /// <summary>
        /// Gets the zero-mean multivariate normal distribution of the observed-site correlation
        /// submatrix, factoring it on first use for the current correlation parameters.
        /// </summary>
        /// <param name="observedSites">The validated observed-site indices.</param>
        /// <returns>The cached distribution for the pattern.</returns>
        private CachedMultivariateNormal GetObservedSubsetDistribution(IReadOnlyList<int> observedSites)
        {
            string key = BuildPatternKey(observedSites);
            if (!_observedSubsetCache.TryGetValue(key, out var distribution))
            {
                int observedCount = observedSites.Count;
                var submatrix = new double[observedCount, observedCount];
                for (int i = 0; i < observedCount; i++)
                {
                    for (int j = 0; j < observedCount; j++)
                        submatrix[i, j] = _correlationMatrix![observedSites[i], observedSites[j]];
                }

                distribution = new CachedMultivariateNormal(new double[observedCount], submatrix);
                _observedSubsetCache[key] = distribution;
            }

            return distribution;
        }

        /// <summary>
        /// Builds the cache key of an observed-site pattern: one character per site, '1' when observed.
        /// </summary>
        /// <param name="observedSites">The validated observed-site indices.</param>
        /// <returns>The pattern key.</returns>
        private string BuildPatternKey(IReadOnlyList<int> observedSites)
        {
            return string.Create(Sites, observedSites, static (span, sites) =>
            {
                span.Fill('0');
                for (int k = 0; k < sites.Count; k++)
                    span[sites[k]] = '1';
            });
        }

        /// <summary>
        /// Gets a copy of the correlation matrix built by the most recent <see cref="SetParameterValues"/>
        /// call, or <c>null</c> before the parameters have been set.
        /// </summary>
        /// <returns>The Sites × Sites correlation matrix, or <c>null</c>.</returns>
        /// <remarks>
        /// Used by <c>SpatialGEV.GenerateRandomValues</c> to simulate spatially dependent rows through the
        /// Cholesky factor of the fitted correlation.
        /// </remarks>
        public double[,]? GetCorrelationMatrix()
        {
            return _correlationMatrix == null ? null : (double[,])_correlationMatrix.Clone();
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
