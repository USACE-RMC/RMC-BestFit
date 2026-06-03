namespace RMC.BestFit.Diagnostics
{
    /// <summary>
    /// Summary statistics for a predictive distribution (prior or posterior).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class contains quantile summaries for key statistics (mean, standard deviation,
    /// minimum, maximum) computed across multiple predictive datasets. Each quantile array
    /// contains the [2.5%, 25%, 50%, 75%, 97.5%] percentiles, providing a compact summary
    /// of the predictive distribution's characteristics.
    /// </para>
    /// <para>
    /// These summaries are useful for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Assessing whether priors produce reasonable predictions (prior predictive)</description></item>
    /// <item><description>Understanding model fit characteristics (posterior predictive)</description></item>
    /// <item><description>Comparing observed data statistics to predictive intervals</description></item>
    /// </list>
    /// </remarks>
    public class PredictiveSummary
    {
        /// <summary>
        /// Gets or sets the number of valid draws used to compute the summary.
        /// </summary>
        public int NumberOfValidDraws { get; set; }

        /// <summary>
        /// Gets or sets the quantiles [2.5%, 25%, 50%, 75%, 97.5%] of the mean statistic.
        /// </summary>
        public double[] MeanQuantiles { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the quantiles [2.5%, 25%, 50%, 75%, 97.5%] of the standard deviation statistic.
        /// </summary>
        public double[] SDQuantiles { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the quantiles [2.5%, 25%, 50%, 75%, 97.5%] of the minimum statistic.
        /// </summary>
        public double[] MinQuantiles { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the quantiles [2.5%, 25%, 50%, 75%, 97.5%] of the maximum statistic.
        /// </summary>
        public double[] MaxQuantiles { get; set; } = Array.Empty<double>();
    }
}
