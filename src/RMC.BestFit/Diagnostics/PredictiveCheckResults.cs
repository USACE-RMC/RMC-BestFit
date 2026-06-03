namespace RMC.BestFit.Diagnostics
{
    /// <summary>
    /// Results from posterior predictive checking with common test statistics.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Posterior predictive p-values measure model fit by comparing observed data statistics
    /// to the distribution of those statistics under the posterior predictive distribution.
    /// </para>
    /// <para>
    /// P-values near 0.5 indicate the model adequately captures the corresponding aspect of the data.
    /// P-values near 0 or 1 indicate potential model misspecification for that aspect.
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     Gelman, A., Meng, X.L., and Stern, H. (1996). Posterior predictive assessment
    ///     of model fitness via realized discrepancies. Statistica Sinica, 6, 733-807.
    /// </para>
    /// </remarks>
    public class PredictiveCheckResults
    {
        /// <summary>
        /// Gets or sets the number of replicates used.
        /// </summary>
        public int NumberOfReplicates { get; set; }

        /// <summary>
        /// Gets or sets the p-value for the mean statistic.
        /// </summary>
        /// <remarks>
        /// Values near 0 indicate the model systematically over-predicts the mean.
        /// Values near 1 indicate the model systematically under-predicts the mean.
        /// Values near 0.5 indicate good fit for location.
        /// </remarks>
        public double MeanPValue { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets the p-value for the standard deviation statistic.
        /// </summary>
        /// <remarks>
        /// Values near 0 indicate the model over-predicts variability.
        /// Values near 1 indicate the model under-predicts variability.
        /// </remarks>
        public double SDPValue { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets the p-value for the skewness statistic.
        /// </summary>
        public double SkewnessPValue { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets the p-value for the minimum statistic.
        /// </summary>
        /// <remarks>
        /// Values near 0 indicate observed minimum is unusually low compared to model predictions.
        /// </remarks>
        public double MinPValue { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets the p-value for the maximum statistic.
        /// </summary>
        /// <remarks>
        /// Values near 0 indicate observed maximum is unusually high compared to model predictions.
        /// This is particularly important for extreme value analysis.
        /// </remarks>
        public double MaxPValue { get; set; } = double.NaN;

        /// <summary>
        /// Checks if any p-value indicates potential model misfit.
        /// </summary>
        /// <param name="threshold">
        /// The threshold for flagging extreme p-values. Default is 0.05.
        /// </param>
        /// <returns>
        /// <c>true</c> if any p-value is less than <paramref name="threshold"/>
        /// or greater than (1 - <paramref name="threshold"/>).
        /// </returns>
        public bool HasPotentialMisfit(double threshold = 0.05)
        {
            return MeanPValue < threshold || MeanPValue > (1 - threshold) ||
                   SDPValue < threshold || SDPValue > (1 - threshold) ||
                   SkewnessPValue < threshold || SkewnessPValue > (1 - threshold) ||
                   MinPValue < threshold || MinPValue > (1 - threshold) ||
                   MaxPValue < threshold || MaxPValue > (1 - threshold);
        }
    }
}
