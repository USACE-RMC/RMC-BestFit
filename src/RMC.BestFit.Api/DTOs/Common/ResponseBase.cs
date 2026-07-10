using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Base class for every REST/MCP response body, carrying the shared success/error/diagnostic
    /// contract used across the API (mirrors the Dam Screening Tool API response conventions).
    /// </summary>
    public abstract class ResponseBase
    {
        /// <summary>
        /// True when the operation completed successfully; false when any error occurred.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        /// <summary>
        /// A human-readable description of the failure when <see cref="Success"/> is false; otherwise null.
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Individual validation error messages when the request failed model-layer validation; otherwise null.
        /// </summary>
        [JsonPropertyName("validationErrors")]
        public List<string>? ValidationErrors { get; set; }

        /// <summary>
        /// Non-fatal validation warnings (e.g., convergence diagnostics, partial block years); otherwise null.
        /// </summary>
        [JsonPropertyName("validationWarnings")]
        public List<string>? ValidationWarnings { get; set; }

        /// <summary>
        /// Wall-clock time the server spent handling the request, in milliseconds.
        /// </summary>
        [JsonPropertyName("computationTimeMs")]
        public long? ComputationTimeMs { get; set; }

        /// <summary>
        /// UTC timestamp of the response in ISO 8601 round-trip ("O") format.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        /// <summary>
        /// Paths of ±Infinity values detected in the response by the finite auditor, when the
        /// server rejected its own response as non-finite; otherwise null.
        /// </summary>
        [JsonPropertyName("nonFiniteFindings")]
        public List<string>? NonFiniteFindings { get; set; }
    }
}
