namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// The lifecycle state of an analysis resource's most recent (or in-flight) run.
    /// </summary>
    public enum AnalysisRunState
    {
        /// <summary>
        /// The analysis has been created but never run.
        /// </summary>
        Created,

        /// <summary>
        /// A run is currently executing.
        /// </summary>
        Running,

        /// <summary>
        /// The most recent run completed successfully and results are available.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The most recent run failed; see the resource's last error for details.
        /// </summary>
        Failed,

        /// <summary>
        /// The most recent run was cancelled by the client. The analysis can be rerun.
        /// </summary>
        Cancelled
    }
}
