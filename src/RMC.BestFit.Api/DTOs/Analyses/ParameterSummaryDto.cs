using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Posterior (or sampled) summary of one model parameter, including MCMC convergence
    /// diagnostics where applicable.
    /// </summary>
    public class ParameterSummaryDto
    {
        /// <summary>
        /// The parameter name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The posterior (or sampled) mean.
        /// </summary>
        [JsonPropertyName("mean")]
        public double? Mean { get; set; }

        /// <summary>
        /// The posterior (or sampled) median.
        /// </summary>
        [JsonPropertyName("median")]
        public double? Median { get; set; }

        /// <summary>
        /// The posterior (or sampled) standard deviation.
        /// </summary>
        [JsonPropertyName("standardDeviation")]
        public double? StandardDeviation { get; set; }

        /// <summary>
        /// The lower credible-interval bound of the parameter.
        /// </summary>
        [JsonPropertyName("lowerCI")]
        public double? LowerCI { get; set; }

        /// <summary>
        /// The upper credible-interval bound of the parameter.
        /// </summary>
        [JsonPropertyName("upperCI")]
        public double? UpperCI { get; set; }

        /// <summary>
        /// The Gelman-Rubin convergence diagnostic (values near 1.0 indicate convergence; above
        /// ~1.1 warrants more iterations). Null for non-chain methods (Bulletin 17C).
        /// </summary>
        [JsonPropertyName("rhat")]
        public double? Rhat { get; set; }

        /// <summary>
        /// The effective sample size of the posterior draws. Null for non-chain methods.
        /// </summary>
        [JsonPropertyName("ess")]
        public double? Ess { get; set; }
    }
}
