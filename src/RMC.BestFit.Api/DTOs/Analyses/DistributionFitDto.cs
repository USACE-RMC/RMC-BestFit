using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// One candidate distribution's maximum-likelihood fit within a distribution-fitting results
    /// response: parameters, information criteria, and the fit outcome.
    /// </summary>
    public class DistributionFitDto
    {
        /// <summary>
        /// The rank of this fit among the successful fits, 1 = best (lowest AIC). Failed fits are
        /// ranked after all successful ones.
        /// </summary>
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        /// <summary>
        /// The distribution type ("logPearsonTypeIII", "gumbel", ...).
        /// </summary>
        [JsonPropertyName("distribution")]
        public string? Distribution { get; set; }

        /// <summary>
        /// The human-readable distribution name (e.g., "Log-Pearson Type III").
        /// </summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// The maximum-likelihood parameter estimates in canonical order; null when the fit failed.
        /// </summary>
        [JsonPropertyName("parameters")]
        public List<ParameterValueDto>? Parameters { get; set; }

        /// <summary>
        /// The Akaike Information Criterion (lower is better); null when unavailable.
        /// </summary>
        [JsonPropertyName("aic")]
        public double? Aic { get; set; }

        /// <summary>
        /// The Bayesian Information Criterion (lower is better); null when unavailable.
        /// </summary>
        [JsonPropertyName("bic")]
        public double? Bic { get; set; }

        /// <summary>
        /// The root mean square error between plotting positions and the fitted CDF; null when
        /// unavailable.
        /// </summary>
        [JsonPropertyName("rmse")]
        public double? Rmse { get; set; }

        /// <summary>
        /// True when the maximum-likelihood fit converged.
        /// </summary>
        [JsonPropertyName("fitSucceeded")]
        public bool FitSucceeded { get; set; }

        /// <summary>
        /// The failure diagnostic when the fit did not converge; null otherwise.
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }
    }
}
