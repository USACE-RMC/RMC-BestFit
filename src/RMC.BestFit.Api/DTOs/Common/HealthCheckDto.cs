using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Body of the detailed health-check endpoint.
    /// </summary>
    public class HealthCheckDto
    {
        /// <summary>
        /// The health status string ("healthy" when the host is serving requests).
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "healthy";

        /// <summary>
        /// UTC timestamp at which the health check was evaluated.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The API assembly version.
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }
}
