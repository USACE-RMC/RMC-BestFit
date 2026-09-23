namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Records how a spatial bootstrap run of <see cref="SpatialGEVAnalysis"/> was performed and how many
    /// replicates it scored.
    /// </summary>
    /// <remarks>
    /// The bootstrap resamples rows (years) with replacement in contiguous blocks while keeping every site,
    /// refits each replicate by maximum a posteriori estimation, and takes percentile intervals over the
    /// successful replicates; replicates whose refit fails are excluded and counted here.
    /// </remarks>
    public class SpatialGEVBootstrapResults
    {
        /// <summary>
        /// Gets or sets the number of replicates requested.
        /// </summary>
        public int RequestedReplicates { get; set; }

        /// <summary>
        /// Gets or sets the number of replicates whose maximum a posteriori refit succeeded with finite results.
        /// </summary>
        public int SuccessfulReplicates { get; set; }

        /// <summary>
        /// Gets the number of replicates that did not produce a usable refit.
        /// </summary>
        public int FailedReplicates => RequestedReplicates - SuccessfulReplicates;

        /// <summary>
        /// Gets or sets the number of consecutive rows (years) per resampled block.
        /// </summary>
        public int BlockSize { get; set; }

        /// <summary>
        /// Gets or sets the pseudo-random seed of the resampling.
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// Gets or sets the minimum fraction of successful replicates required for the run to report intervals.
        /// </summary>
        public double MinimumSuccessFraction { get; set; }

        /// <summary>
        /// Gets or sets a description of the resampling scheme and the per-replicate estimator.
        /// </summary>
        public string Scheme { get; set; } = string.Empty;
    }
}
