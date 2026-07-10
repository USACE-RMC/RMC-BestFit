using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A prior distribution on a quantile of the parent distribution at a given annual exceedance
    /// probability — the mechanism for incorporating engineering judgment about flood magnitudes
    /// (e.g., "the 100-year flow is around 50,000 cfs, give or take") into Bayesian estimation.
    /// </summary>
    /// <remarks>
    /// Two formulations are supported via the request-level useSingleQuantile flag: a single
    /// quantile prior (Viglione et al., 2013) or one prior per parent-distribution parameter
    /// (Coles and Tawn, 1996). Supply priors at distinct exceedance probabilities.
    /// </remarks>
    public class QuantilePriorDto
    {
        /// <summary>
        /// The annual exceedance probability of the quantile the prior applies to, strictly
        /// between 0 and 1 (e.g., 0.01 for the 100-year event).
        /// </summary>
        [Range(0d, 1d)]
        [JsonPropertyName("alpha")]
        public double Alpha { get; set; }

        /// <summary>
        /// The prior distribution for the quantile magnitude (e.g., a log-normal prior centered
        /// on the judged 100-year flow).
        /// </summary>
        [Required]
        [JsonPropertyName("distribution")]
        public DistributionSpecDto? Distribution { get; set; }
    }
}
