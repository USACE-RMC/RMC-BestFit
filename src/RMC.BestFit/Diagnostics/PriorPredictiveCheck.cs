using System.Diagnostics;
using Numerics;
using Numerics.Data.Statistics;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling;
using RMC.BestFit.Models;

namespace RMC.BestFit.Diagnostics
{
    /// <summary>
    /// Provides prior predictive checking functionality for Bayesian models.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Prior predictive checks allow assessment of whether the specified priors
    /// produce reasonable predictions before observing any data. This is a fundamental
    /// step in Bayesian workflow to ensure priors are not too vague or too restrictive.
    /// </para>
    /// <para>
    /// The process involves:
    /// </para>
    /// <list type="number">
    /// <item><description>Drawing parameter values from the prior distributions</description></item>
    /// <item><description>Generating simulated data using those parameters</description></item>
    /// <item><description>Assessing whether the simulated data is physically reasonable</description></item>
    /// </list>
    /// <para>
    /// This class works with any model implementing both <see cref="IModel"/> and
    /// <see cref="ISimulatable{T}"/>. Data generation uses the model's
    /// <see cref="ISimulatable{T}.GenerateRandomValues"/> method.
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     Gabry, J., Simpson, D., Vehtari, A., Betancourt, M., and Gelman, A. (2019).
    ///     Visualization in Bayesian workflow. Journal of the Royal Statistical Society:
    ///     Series A, 182(2), 389-402.
    /// </para>
    /// </remarks>
    public class PriorPredictiveCheck
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="PriorPredictiveCheck"/> class.
        /// </summary>
        /// <param name="model">
        /// The Bayesian model to use for prior predictive checking. The model must
        /// implement both <see cref="IModel"/> and <see cref="ISimulatable{T}"/> for
        /// data generation. The model's parameters should have priors defined.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="model"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="model"/> does not implement <see cref="ISimulatable{T}"/> with double[] data.
        /// </exception>
        public PriorPredictiveCheck(IModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));

            if (model is not ISimulatable<double[]>)
                throw new ArgumentException("Model must implement ISimulatable<double[]> for data generation.", nameof(model));
        }

        #endregion

        #region Members

        private readonly IModel _model;
        private int _seed = 12345;
        private int _numberOfDraws = 1000;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the model being checked.
        /// </summary>
        public IModel Model => _model;

        /// <summary>
        /// Gets or sets the random seed for reproducibility.
        /// </summary>
        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }

        /// <summary>
        /// Gets or sets the number of prior draws to generate.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if value is less than 1.
        /// </exception>
        public int NumberOfDraws
        {
            get => _numberOfDraws;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Number of draws must be at least 1.");
                _numberOfDraws = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Samples parameter sets from the prior distributions.
        /// </summary>
        /// <returns>
        /// A list of <see cref="ParameterSet"/> objects, one for each prior draw.
        /// Each ParameterSet.Values has length equal to the number of model parameters.
        /// The <c>Fitness</c> field contains the negative prior log-likelihood for that parameter set.
        /// </returns>
        /// <remarks>
        /// <para>
        /// For each parameter with a defined prior distribution, values are sampled
        /// from that prior. For fixed parameters, the current parameter value is used.
        /// </para>
        /// <para>
        /// Parameters are sampled from their <see cref="ModelParameter.PriorDistribution"/>
        /// using inverse CDF transformation for reproducibility.
        /// </para>
        /// <para>
        /// This method uses parallel processing for improved performance on multi-core systems.
        /// </para>
        /// </remarks>
        public IList<ParameterSet> SampleFromPriors()
        {
            var parameters = _model.Parameters;
            int numParams = parameters.Count;

            // Pre-generate all uniform random values for parallel execution
            var rng = new MersenneTwister(_seed);
            var uniforms = rng.NextDoubles(_numberOfDraws, numParams);

            // Pre-size result array for parallel assignment
            var result = new ParameterSet[_numberOfDraws];

            Parallel.For(0, _numberOfDraws, i =>
            {
                var values = new double[numParams];

                for (int j = 0; j < numParams; j++)
                {
                    var param = parameters[j];

                    if (param.IsFixed)
                    {
                        // Use fixed value
                        values[j] = param.Value;
                    }
                    else if (param.PriorDistribution is not null)
                    {
                        // Sample from prior distribution using pre-generated uniform
                        double u = uniforms[i, j];
                        values[j] = param.PriorDistribution.InverseCDF(u);

                        // Clamp to parameter bounds
                        values[j] = Math.Max(param.LowerBound, Math.Min(param.UpperBound, values[j]));
                    }
                    else
                    {
                        // Fallback: uniform sampling within bounds
                        double lower = param.LowerBound;
                        double upper = param.UpperBound;

                        // When the prior support is unbounded, fall back to a wide range
                        // anchored at the parameter's current value scaled by 1e6 so the
                        // bound is meaningful at the parameter's order of magnitude rather
                        // than truncating physical scales (e.g., flood discharges in cfs
                        // routinely exceed 1e6).
                        double scale = Math.Max(1.0, Math.Abs(param.Value)) * 1e6;
                        if (double.IsNegativeInfinity(lower)) lower = -scale;
                        if (double.IsPositiveInfinity(upper)) upper = scale;

                        values[j] = lower + uniforms[i, j] * (upper - lower);
                    }
                }

                // Compute prior log-likelihood for this parameter set
                double priorLL = _model.PriorLogLikelihood(values);
                result[i] = new ParameterSet(values, -priorLL); // Fitness is negative log-likelihood
            });

            return result;
        }

        /// <summary>
        /// Generates prior predictive datasets.
        /// </summary>
        /// <param name="sampleSize">The number of observations in each simulated dataset.</param>
        /// <returns>
        /// A list of simulated datasets, one for each valid prior draw.
        /// Each dataset contains <paramref name="sampleSize"/> observations.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="sampleSize"/> is less than 1.
        /// </exception>
        /// <remarks>
        /// <para>
        /// For each prior draw, this method:
        /// </para>
        /// <list type="number">
        /// <item><description>Sets the model parameters to the drawn values</description></item>
        /// <item><description>Generates random samples using <see cref="ISimulatable{T}.GenerateRandomValues"/></description></item>
        /// </list>
        /// <para>
        /// The returned datasets can be used to assess whether priors allow
        /// physically reasonable values (e.g., flood peaks between 0 and 10^7 cfs).
        /// </para>
        /// <para>
        /// This method uses parallel processing for improved performance on multi-core systems.
        /// </para>
        /// </remarks>
        public List<double[]> GeneratePriorPredictive(int sampleSize)
        {
            if (sampleSize < 1)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be at least 1.");

            var priorSamples = SampleFromPriors();
            int numSamples = priorSamples.Count;

            // Pre-generate seeds for parallel execution
            var rng = new MersenneTwister(_seed + 1);
            var seeds = rng.NextIntegers(numSamples);

            // Pre-size result array for parallel assignment
            var result = new double[numSamples][];

            Parallel.For(0, numSamples, i =>
            {
                try
                {
                    var priorParams = priorSamples[i];

                    // Check if parameter combination is valid (finite prior log-likelihood)
                    // Fitness is -log-likelihood, so valid if not +Infinity
                    if (double.IsPositiveInfinity(priorParams.Fitness) || double.IsNaN(priorParams.Fitness))
                        return;

                    // Clone model and set parameters
                    var modelClone = _model.Clone();
                    modelClone.SetParameterValues(priorParams.Values);

                    // Generate data using the model's ISimulatable interface
                    var simulatable = (ISimulatable<double[]>)modelClone;
                    var data = simulatable.GenerateRandomValues(sampleSize, seeds[i]);

                    if (data != null && data.Length > 0)
                        result[i] = data;
                }
                catch (Exception ex)
                {
                    // Invalid parameter combination - leave null (will be filtered out).
                    Debug.WriteLine($"PriorPredictiveCheck: invalid parameter draw {i}: {ex.Message}");
                }
            });

            // Filter out null entries (failed draws)
            return result.Where(r => r != null).ToList();
        }

        /// <summary>
        /// Computes summary statistics for the prior predictive distribution.
        /// </summary>
        /// <param name="sampleSize">The number of observations in each simulated dataset.</param>
        /// <returns>
        /// A <see cref="PredictiveSummary"/> containing quantiles and statistics
        /// of the prior predictive distribution.
        /// </returns>
        /// <remarks>
        /// This method generates prior predictive samples and computes summary statistics
        /// useful for assessing prior reasonableness without examining individual datasets.
        /// </remarks>
        public PredictiveSummary ComputeSummary(int sampleSize)
        {
            var predictiveData = GeneratePriorPredictive(sampleSize);
            return ComputeSummaryFromData(predictiveData);
        }

        /// <summary>
        /// Computes summary statistics from a collection of predictive datasets.
        /// </summary>
        /// <param name="predictiveData">The list of simulated datasets.</param>
        /// <returns>A <see cref="PredictiveSummary"/> containing summary statistics.</returns>
        /// <remarks>
        /// This method uses parallel processing for improved performance on multi-core systems.
        /// </remarks>
        private static PredictiveSummary ComputeSummaryFromData(List<double[]> predictiveData)
        {
            if (predictiveData == null || predictiveData.Count == 0)
                return new PredictiveSummary();

            int n = predictiveData.Count;
            var percents = new double[] { 0.025, 0.25, 0.5, 0.75, 0.975 };
            var means = new double[n];
            var sds = new double[n];
            var mins = new double[n];
            var maxs = new double[n];

            Parallel.For(0, n, i =>
            {
                var data = predictiveData[i];
                if (data == null || data.Length == 0)
                {
                    means[i] = double.NaN;
                    sds[i] = double.NaN;
                    mins[i] = double.NaN;
                    maxs[i] = double.NaN;
                    return;
                }

                var validData = data.Where(x => double.IsFinite(x)).ToArray();
                if (validData.Length == 0)
                {
                    means[i] = double.NaN;
                    sds[i] = double.NaN;
                    mins[i] = double.NaN;
                    maxs[i] = double.NaN;
                    return;
                }

                means[i] = Statistics.Mean(validData);
                sds[i] = Statistics.StandardDeviation(validData);
                mins[i] = validData.Min();
                maxs[i] = validData.Max();
            });

            return new PredictiveSummary
            {
                NumberOfValidDraws = predictiveData.Count,
                MeanQuantiles = Statistics.Percentile(means, percents),
                SDQuantiles = Statistics.Percentile(sds, percents),
                MinQuantiles = Statistics.Percentile(mins, percents),
                MaxQuantiles = Statistics.Percentile(maxs, percents)
            };
        }

        #endregion
    }
}
