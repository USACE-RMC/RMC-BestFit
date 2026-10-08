namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// The outcome of one leave-one-site-out cross-validation fold of <see cref="SpatialGEVAnalysis"/>.
    /// </summary>
    /// <remarks>
    /// A fold that did not succeed stores NaN in the per-site error arrays of
    /// <see cref="SpatialGEVCrossValidationResults"/> and is excluded from the aggregate metrics; the
    /// reason is recorded in <see cref="SpatialGEVCrossValidationResults.FoldMessages"/>.
    /// </remarks>
    public enum SpatialGEVCrossValidationFoldStatus
    {
        /// <summary>
        /// The reduced training model was fitted and the held-out site was predicted and scored.
        /// </summary>
        Succeeded = 0,

        /// <summary>
        /// The held-out site has no finite observation, so there is nothing to score.
        /// </summary>
        NoObservations = 1,

        /// <summary>
        /// The reduced training model could not be built, was invalid, or its sampler did not produce an estimate.
        /// </summary>
        FitFailed = 2,

        /// <summary>
        /// The fold was fitted but the held-out prediction or its at-site comparison failed.
        /// </summary>
        PredictionFailed = 3
    }
}
