using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A Gaussian penalty on a quantile of the fitted distribution in a Bulletin 17C analysis —
    /// incorporates prior information about a flood magnitude at a specific annual exceedance
    /// probability (e.g., a paleoflood-informed 500-year discharge) into the Expected Moments
    /// Algorithm fit.
    /// </summary>
    /// <remarks>
    /// The penalty adds 0.5 × (quantile − mean)² / (mse × n) to the GMM objective, where n is the
    /// total record length. With useLog10=true (the default, appropriate for flood discharges)
    /// the quantile and mean are compared in log10 space, so mean and mse must be supplied in
    /// log10 units.
    /// </remarks>
    public class QuantilePenaltyDto
    {
        /// <summary>
        /// The annual exceedance probability of the penalized quantile, strictly between 0 and 1
        /// (e.g., 0.002 for the 500-year event).
        /// </summary>
        [Range(0d, 1d)]
        [JsonPropertyName("aep")]
        public double Aep { get; set; }

        /// <summary>
        /// The prior mean for the quantile — in log10 units when useLog10 is true (the default),
        /// otherwise in real units.
        /// </summary>
        [JsonPropertyName("mean")]
        public double Mean { get; set; }

        /// <summary>
        /// The mean squared error (variance) of the prior mean, in the same (log10 or real) units
        /// as the mean. Must be greater than 0; smaller values pull the fitted quantile more
        /// strongly toward the mean.
        /// </summary>
        [JsonPropertyName("mse")]
        public double Mse { get; set; }

        /// <summary>
        /// True (default) to compare the quantile and mean in log10 space — the convention for
        /// flood discharges. Set false to penalize in real space.
        /// </summary>
        [JsonPropertyName("useLog10")]
        public bool UseLog10 { get; set; } = true;
    }
}
