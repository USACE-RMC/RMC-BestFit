using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body of the validate endpoints: the model-layer validation verdict for an
    /// analysis configuration, without running the analysis.
    /// </summary>
    public class ValidationResponse
    {
        /// <summary>
        /// True when the configuration passes model-layer validation and the analysis can run.
        /// </summary>
        [JsonPropertyName("isValid")]
        public bool IsValid { get; set; }

        /// <summary>
        /// The validation error messages when invalid; empty when valid.
        /// </summary>
        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Non-fatal validation warnings, when any.
        /// </summary>
        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new();
    }
}
