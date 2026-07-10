namespace RMC.BestFit.Api.Configuration
{
    /// <summary>
    /// Configurable limits for the REST API host, bound from the "Api" configuration section.
    /// </summary>
    /// <remarks>
    /// The limits protect the host from unbounded memory growth (the resource store is in-memory)
    /// and CPU oversubscription (each MCMC run parallelizes internally).
    /// </remarks>
    public class ApiOptions
    {
        /// <summary>
        /// The configuration section name the options are bound from.
        /// </summary>
        public const string SectionName = "Api";

        /// <summary>
        /// The maximum total number of resources (time series + input data + analyses) the
        /// in-memory store will hold. Creation requests beyond the cap are rejected with
        /// HTTP 409 so clients holding resource ids never see them evicted. Default = 500.
        /// </summary>
        public int MaxResources { get; set; } = 500;

        /// <summary>
        /// The maximum number of analyses allowed to run concurrently. Each Bayesian MCMC run
        /// parallelizes internally, so this throttle prevents CPU oversubscription when multiple
        /// clients trigger runs at the same time. Default = 2.
        /// </summary>
        public int MaxConcurrentRuns { get; set; } = 2;

        /// <summary>
        /// The maximum number of MCMC iterations a client may request per analysis run. Guards
        /// the synchronous run endpoints against requests that would exceed typical HTTP client
        /// timeouts. Default = 500,000.
        /// </summary>
        public int MaxIterations { get; set; } = 500_000;
    }
}
