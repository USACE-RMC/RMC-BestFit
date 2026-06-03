namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Contains the results from leave-one-site-out cross-validation for spatial GEV analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Leave-one-site-out cross-validation (LOOCV) assesses how well the spatial model
    /// can predict at ungauged locations by systematically excluding each site,
    /// re-fitting the model, and comparing predictions to the held-out observations.
    /// </para>
    /// <para>
    /// This provides important diagnostics for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Model predictive skill at ungauged locations</description></item>
    /// <item><description>Identifying sites that are poorly predicted (potential outliers or unique characteristics)</description></item>
    /// <item><description>Assessing spatial model assumptions</description></item>
    /// </list>
    /// <para>
    ///     <b>References:</b>
    ///     Renard, B. (2011). A Bayesian hierarchical approach to regional frequency analysis.
    ///     Water Resources Research, 47, W11513.
    /// </para>
    /// </remarks>
    public class SpatialGEVCrossValidationResults
    {
        /// <summary>
        /// Gets or sets the prediction error (predicted - observed) for each site.
        /// </summary>
        /// <remarks>
        /// Computed as the difference between the predicted T=100 quantile at the
        /// excluded site and the at-site MLE estimate. Positive values indicate
        /// over-prediction; negative values indicate under-prediction.
        /// </remarks>
        public double[] SitePredictionErrors { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the RMSE across quantiles for each site.
        /// </summary>
        /// <remarks>
        /// Root mean square error computed across multiple return periods (T=2, 5, 10, 25, 50, 100).
        /// Provides an overall measure of prediction accuracy at each site.
        /// </remarks>
        public double[] SiteRMSE { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the relative bias for each site.
        /// </summary>
        /// <remarks>
        /// Computed as (predicted - observed) / observed for the T=100 quantile.
        /// Values near zero indicate unbiased predictions; positive values
        /// indicate systematic over-prediction.
        /// </remarks>
        public double[] SiteBias { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the Continuous Ranked Probability Score (CRPS) for each site.
        /// </summary>
        /// <remarks>
        /// CRPS measures both calibration and sharpness of probabilistic predictions.
        /// Lower values indicate better predictive performance. Unlike point-based
        /// metrics, CRPS accounts for the full predictive distribution.
        /// <para>
        /// <b>Not yet implemented in <c>RunCrossValidationAsync</c>.</b> The array is
        /// allocated zero-filled for backward compatibility. Implementation pending
        /// (compute CRPS as a numerical integral of the squared difference between
        /// the predictive CDF and the empirical step at the held-out observation).
        /// </para>
        /// </remarks>
        public double[] SiteCRPS { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the mean absolute error across all sites.
        /// </summary>
        /// <remarks>
        /// The average absolute prediction error, providing an overall measure
        /// of model accuracy that is less sensitive to outliers than RMSE.
        /// </remarks>
        public double MeanAbsoluteError { get; set; }

        /// <summary>
        /// Gets or sets the root mean square error across all sites.
        /// </summary>
        /// <remarks>
        /// The square root of the average squared prediction error. This metric
        /// gives more weight to large errors and is commonly used for model comparison.
        /// </remarks>
        public double RootMeanSquareError { get; set; }

        /// <summary>
        /// Gets or sets the mean relative bias across all sites.
        /// </summary>
        /// <remarks>
        /// The average relative bias, indicating whether the model tends to
        /// systematically over- or under-predict across the region.
        /// </remarks>
        public double MeanBias { get; set; }
    }
}
