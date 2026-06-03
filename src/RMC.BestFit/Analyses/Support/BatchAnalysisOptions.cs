namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Configuration options for batch analysis execution via <see cref="BatchAnalysisRunner"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Use this class to control the concurrency level, error handling behavior,
    /// and execution ordering when running multiple analyses in a batch.
    /// The default configuration runs analyses serially (one at a time) and
    /// continues executing remaining analyses if one fails.
    /// </para>
    /// </remarks>
    public class BatchAnalysisOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of analyses to run concurrently.
        /// </summary>
        /// <value>
        /// A positive integer specifying the concurrency limit.
        /// The default value is <c>1</c>, which runs analyses serially.
        /// Set to a value greater than <c>1</c> for parallel execution.
        /// </value>
        /// <remarks>
        /// <para>
        /// When set to <c>1</c>, analyses execute one at a time in the order determined
        /// by <see cref="OrderByDependency"/>. When greater than <c>1</c>, a
        /// <see cref="System.Threading.SemaphoreSlim"/> is used to limit the number
        /// of concurrently executing analyses to the specified value.
        /// </para>
        /// <para>
        /// A value of <see cref="int.MaxValue"/> effectively removes the concurrency limit.
        /// For CPU-bound MCMC analyses, a reasonable default is
        /// <c>Environment.ProcessorCount</c>.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than or equal to zero.
        /// </exception>
        public int MaxDegreeOfParallelism
        {
            get => _maxDegreeOfParallelism;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "MaxDegreeOfParallelism must be greater than zero.");
                _maxDegreeOfParallelism = value;
            }
        }
        private int _maxDegreeOfParallelism = 1;

        /// <summary>
        /// Gets or sets a value indicating whether the batch runner should continue
        /// executing remaining analyses when one fails.
        /// </summary>
        /// <value>
        /// <c>true</c> to continue executing after a failure (default);
        /// <c>false</c> to stop the batch immediately after the first failure.
        /// </value>
        /// <remarks>
        /// <para>
        /// When <c>true</c>, each analysis that fails is recorded in the results
        /// with its exception, and the runner proceeds to the next analysis.
        /// When <c>false</c>, the runner cancels all remaining analyses and
        /// returns immediately after the first failure.
        /// </para>
        /// </remarks>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether analyses should be reordered
        /// so that dependent (composite) analyses run after independent ones.
        /// </summary>
        /// <value>
        /// <c>true</c> to run non-composite analyses before composite analyses (default);
        /// <c>false</c> to run analyses in the order they are provided.
        /// </value>
        /// <remarks>
        /// <para>
        /// Composite analyses (e.g., model averaging) depend on the results of
        /// individual analyses. When this option is <c>true</c>, the runner
        /// partitions the input list so that all non-composite analyses execute
        /// first, followed by all composite analyses.
        /// </para>
        /// <para>
        /// The relative order of analyses within each partition is preserved.
        /// </para>
        /// </remarks>
        public bool OrderByDependency { get; set; } = true;
    }
}
