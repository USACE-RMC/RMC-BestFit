namespace RMC.BestFit.Estimation
{
    /// <summary>
    /// Describes the outcome of the most recent covariance computation.
    /// </summary>
    /// <remarks>
    /// This status distinguishes a usable covariance matrix from numerical failure and
    /// records when positive-definite regularization changed the computed matrix.
    /// </remarks>
    public enum CovarianceComputationStatus
    {
        /// <summary>
        /// No covariance computation has been attempted for the current estimator state.
        /// </summary>
        NotComputed = 0,

        /// <summary>
        /// A finite covariance matrix with positive diagonal variances was computed.
        /// </summary>
        Available = 1,

        /// <summary>
        /// A usable covariance matrix was produced after positive-definite regularization.
        /// </summary>
        Regularized = 2,

        /// <summary>
        /// Covariance computation failed or produced a non-finite or degenerate result.
        /// </summary>
        Failed = 3
    }
}
