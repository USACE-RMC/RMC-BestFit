using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying distribution-fitting results: every candidate distribution's
    /// maximum-likelihood fit, ranked by AIC (successful fits first).
    /// </summary>
    public class DistributionFittingResultsResponse : ResponseBase
    {
        /// <summary>
        /// The analysis resource id.
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid AnalysisId { get; set; }

        /// <summary>
        /// The analysis kind ("distributionFitting").
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// The candidate fits ranked best-first by AIC, with failed fits after all successful ones.
        /// </summary>
        [JsonPropertyName("fits")]
        public List<DistributionFitDto> Fits { get; set; } = new();
    }
}
