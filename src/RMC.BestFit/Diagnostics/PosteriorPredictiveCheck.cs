using Numerics;
using Numerics.Data.Statistics;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Models;

namespace RMC.BestFit.Diagnostics
{
    /// <summary>
    /// Provides posterior predictive checking functionality for Bayesian models.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Posterior predictive checks assess model fit by comparing observed data
    /// to data simulated from the posterior predictive distribution. If the model
    /// fits well, observed data should look like a typical sample from the posterior
    /// predictive distribution.
    /// </para>
    /// <para>
    /// Key diagnostics include:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Posterior predictive p-values for test statistics</description></item>
    /// <item><description>Visual comparison of observed vs. replicated data distributions</description></item>
    /// <item><description>Residual analysis</description></item>
    /// </list>
    /// <para>
    /// This class works with any model implementing both <see cref="IModel"/> and
    /// <see cref="ISimulatable{T}"/>. Data generation uses the model's
    /// <see cref="ISimulatable{T}.GenerateRandomValues"/> method.
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     Gelman, A., Meng, X.L., and Stern, H. (1996). Posterior predictive assessment
    ///     of model fitness via realized discrepancies. Statistica Sinica, 6, 733-807.
    /// </para>
    /// </remarks>
    public class PosteriorPredictiveCheck
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="PosteriorPredictiveCheck"/> class.
        /// </summary>
        /// <param name="model">The fitted Bayesian model. Must implement <see cref="ISimulatable{T}"/> with double[].</param>
        /// <param name="posteriorSamples">
        /// MCMC samples from the posterior distribution as a list of <see cref="ParameterSet"/>.
        /// This is the same format as <see cref="MCMCResults.Output"/>.
        /// </param>
        /// <param name="observedData">The observed data used for fitting.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any argument is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="posteriorSamples"/> or <paramref name="observedData"/> is empty,
        /// or if <paramref name="model"/> does not implement <see cref="ISimulatable{T}"/> with double[].
        /// </exception>
        public PosteriorPredictiveCheck(
            IModel model,
            IList<ParameterSet> posteriorSamples,
            double[] observedData)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _posteriorSamples = posteriorSamples ?? throw new ArgumentNullException(nameof(posteriorSamples));
            _observedData = observedData ?? throw new ArgumentNullException(nameof(observedData));

            if (model is not ISimulatable<double[]>)
                throw new ArgumentException("Model must implement ISimulatable<double[]> for data generation.", nameof(model));

            if (_posteriorSamples.Count == 0)
                throw new ArgumentException("Posterior samples cannot be empty.", nameof(posteriorSamples));

            if (_observedData.Length == 0)
                throw new ArgumentException("Observed data cannot be empty.", nameof(observedData));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PosteriorPredictiveCheck"/> class
        /// using MCMC results.
        /// </summary>
        /// <param name="model">The fitted Bayesian model. Must implement <see cref="ISimulatable{T}"/> with double[].</param>
        /// <param name="mcmcResults">The MCMC results containing posterior samples.</param>
        /// <param name="observedData">The observed data used for fitting.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any argument is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="model"/> does not implement <see cref="ISimulatable{T}"/> with double[].
        /// </exception>
        public PosteriorPredictiveCheck(
            IModel model,
            MCMCResults mcmcResults,
            double[] observedData)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _observedData = observedData ?? throw new ArgumentNullException(nameof(observedData));

            if (model is not ISimulatable<double[]>)
                throw new ArgumentException("Model must implement ISimulatable<double[]> for data generation.", nameof(model));

            if (mcmcResults == null)
                throw new ArgumentNullException(nameof(mcmcResults));

            if (mcmcResults.Output == null || mcmcResults.Output.Count == 0)
                throw new ArgumentException("MCMC results contain no samples.", nameof(mcmcResults));

            _posteriorSamples = mcmcResults.Output;
        }

        #endregion

        #region Members

        private readonly IModel _model;
        private readonly IList<ParameterSet> _posteriorSamples;
        private readonly double[] _observedData;
        private int _seed = 12345;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the model being checked.
        /// </summary>
        public IModel Model => _model;

        /// <summary>
        /// Gets the number of posterior samples available.
        /// </summary>
        public int NumberOfPosteriorSamples => _posteriorSamples.Count;

        /// <summary>
        /// Gets the observed data sample size.
        /// </summary>
        public int SampleSize => _observedData.Length;

        /// <summary>
        /// Gets or sets the random seed for reproducibility.
        /// </summary>
        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Generates replicated datasets from the posterior predictive distribution.
        /// </summary>
        /// <param name="numberOfReplicates">
        /// The number of replicate datasets to generate. If greater than the number
        /// of posterior samples, sampling with replacement is used.
        /// </param>
        /// <returns>
        /// A list of replicated datasets, each with the same sample size as the observed data.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="numberOfReplicates"/> is less than 1.
        /// </exception>
        /// <remarks>
        /// For each replicate:
        /// <list type="number">
        /// <item><description>Draw a parameter set from the posterior samples</description></item>
        /// <item><description>Generate a dataset of size n from the model using <see cref="ISimulatable{T}.GenerateRandomValues"/></description></item>
        /// </list>
        /// <para>
        /// This method uses parallel processing for improved performance on multi-core systems.
        /// </para>
        /// </remarks>
        public List<double[]> GenerateReplicates(int numberOfReplicates)
        {
            if (numberOfReplicates < 1)
                throw new ArgumentOutOfRangeException(nameof(numberOfReplicates), "Must generate at least 1 replicate.");

            var rng = new MersenneTwister(_seed);
            int n = _observedData.Length;

            // Pre-generate random indices and seeds for parallel execution
            var indices = rng.NextIntegers(0, _posteriorSamples.Count, numberOfReplicates);
            var seeds = rng.NextIntegers(numberOfReplicates);

            // Pre-size result array for parallel assignment
            var result = new double[numberOfReplicates][];

            Parallel.For(0, numberOfReplicates, rep =>
            {
                try
                {
                    // Get pre-generated index and seed
                    int idx = indices[rep];
                    int dataSeed = seeds[rep];
                    var posteriorParams = _posteriorSamples[idx];

                    // Clone model and set parameters
                    var modelClone = _model.Clone();
                    modelClone.SetParameterValues(posteriorParams.Values);

                    // Generate replicate data using the model's ISimulatable interface
                    var simulatable = (ISimulatable<double[]>)modelClone;
                    var yRep = simulatable.GenerateRandomValues(n, dataSeed);

                    if (yRep != null && yRep.Length > 0)
                        result[rep] = yRep;
                }
                catch (Exception ex)
                {
                    // Invalid parameters - leave null (will be filtered out).
                    // Log so a chain with high failure rate is diagnosable.
                    System.Diagnostics.Debug.WriteLine(
                        $"PosteriorPredictiveCheck.GenerateReplicates: replicate {rep} failed: {ex.Message}");
                }
            });

            // Filter out null entries (failed replicates)
            return result.Where(r => r != null).ToList();
        }

        /// <summary>
        /// Computes a posterior predictive p-value for a test statistic.
        /// </summary>
        /// <param name="testStatistic">
        /// A function that computes a scalar test statistic from a dataset.
        /// </param>
        /// <param name="numberOfReplicates">
        /// The number of replicate datasets to generate for the p-value calculation.
        /// </param>
        /// <returns>
        /// The posterior predictive p-value, defined as the proportion of replicates
        /// where T(y_rep) >= T(y_obs).
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="testStatistic"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A p-value near 0 or 1 indicates the model fails to capture the aspect
        /// of the data measured by the test statistic. P-values near 0.5 indicate
        /// good fit for that aspect.
        /// </para>
        /// <para>
        /// Common test statistics include:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Mean - checks location</description></item>
        /// <item><description>Standard deviation - checks spread</description></item>
        /// <item><description>Skewness - checks asymmetry</description></item>
        /// <item><description>Maximum - checks tail behavior</description></item>
        /// <item><description>Number of exceedances above threshold - checks tail</description></item>
        /// </list>
        /// </remarks>
        public double ComputePValue(Func<double[], double> testStatistic, int numberOfReplicates = 1000)
        {
            if (testStatistic == null)
                throw new ArgumentNullException(nameof(testStatistic));

            // Compute test statistic for observed data
            double tObs = testStatistic(_observedData);

            // Generate replicates and compute test statistics
            var replicates = GenerateReplicates(numberOfReplicates);
            if (replicates.Count == 0)
                return double.NaN;

            int countGreaterOrEqual = 0;
            // Loop bound is replicates.Count, not numberOfReplicates: GenerateReplicates
            // filters out failed parameter draws, so replicates.Count <= numberOfReplicates.
            // Indexing past replicates.Count would throw IndexOutOfRangeException.
            Parallel.For(0, replicates.Count, rep =>
            {
                double tRep = testStatistic(replicates[rep]);
                if (tRep >= tObs)
                {
                    Interlocked.Increment(ref countGreaterOrEqual);
                }
            });

            return (double)countGreaterOrEqual / replicates.Count;
        }

        /// <summary>
        /// Computes posterior predictive p-values for common test statistics.
        /// </summary>
        /// <param name="numberOfReplicates">
        /// The number of replicate datasets to generate.
        /// </param>
        /// <returns>
        /// A <see cref="PredictiveCheckResults"/> object containing p-values for mean, SD,
        /// skewness, min, and max.
        /// </returns>
        public PredictiveCheckResults ComputeCommonPValues(int numberOfReplicates = 1000)
        {
            var replicates = GenerateReplicates(numberOfReplicates);
            if (replicates.Count == 0)
                return new PredictiveCheckResults();

            // Compute observed statistics
            double obsMean = Statistics.Mean(_observedData);
            double obsSD = Statistics.StandardDeviation(_observedData);
            double obsSkew = Statistics.Skewness(_observedData);
            double obsMin = _observedData.Min();
            double obsMax = _observedData.Max();

            // Count exceedances
            int countMean = 0, countSD = 0, countSkew = 0, countMin = 0, countMax = 0;

            Parallel.ForEach(replicates, yRep =>
            {
                if (Statistics.Mean(yRep) >= obsMean) Interlocked.Increment(ref countMean);
                if (Statistics.StandardDeviation(yRep) >= obsSD) Interlocked.Increment(ref countSD);
                if (Statistics.Skewness(yRep) >= obsSkew) Interlocked.Increment(ref countSkew);
                if (yRep.Min() >= obsMin) Interlocked.Increment(ref countMin);
                if (yRep.Max() >= obsMax) Interlocked.Increment(ref countMax);
            });

            int n = replicates.Count;
            return new PredictiveCheckResults
            {
                NumberOfReplicates = n,
                MeanPValue = (double)countMean / n,
                SDPValue = (double)countSD / n,
                SkewnessPValue = (double)countSkew / n,
                MinPValue = (double)countMin / n,
                MaxPValue = (double)countMax / n
            };
        }

        /// <summary>
        /// Computes summary statistics for the posterior predictive distribution.
        /// </summary>
        /// <param name="numberOfReplicates">
        /// The number of replicate datasets to generate.
        /// </param>
        /// <returns>
        /// A <see cref="PredictiveSummary"/> containing quantiles and statistics.
        /// </returns>
        public PredictiveSummary ComputeSummary(int numberOfReplicates = 1000)
        {
            var replicates = GenerateReplicates(numberOfReplicates);
            return ComputeSummaryFromData(replicates);
        }

        /// <summary>
        /// Computes summary statistics from replicate data.
        /// </summary>
        /// <param name="replicates">The list of replicate datasets.</param>
        /// <returns>A <see cref="PredictiveSummary"/> containing summary statistics.</returns>
        private static PredictiveSummary ComputeSummaryFromData(List<double[]> replicates)
        {
            if (replicates == null || replicates.Count == 0)
                return new PredictiveSummary();

            int n = replicates.Count;
            var percents = new double[] { 0.025, 0.25, 0.5, 0.75, 0.975 };
            var means = new double[n];
            var sds = new double[n];
            var mins = new double[n];
            var maxs = new double[n];

            Parallel.For(0, n, i =>
            {
                var data = replicates[i];
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
                NumberOfValidDraws = replicates.Count,
                MeanQuantiles = Statistics.Percentile(means, percents),
                SDQuantiles = Statistics.Percentile(sds, percents),
                MinQuantiles = Statistics.Percentile(mins, percents),
                MaxQuantiles = Statistics.Percentile(maxs, percents)
            };
        }

        #endregion
    }
}
