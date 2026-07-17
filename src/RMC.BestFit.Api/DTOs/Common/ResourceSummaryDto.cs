using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A one-line summary of any server-side resource, used by the cross-cutting resources
    /// overview so agents can re-orient themselves mid-session.
    /// </summary>
    public class ResourceSummaryDto
    {
        /// <summary>
        /// The resource id.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// The resource type ("timeSeries", "inputData", or "analysis").
        /// </summary>
        [JsonPropertyName("resourceType")]
        public string? ResourceType { get; set; }

        /// <summary>
        /// The resource display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// UTC timestamp at which the resource was created.
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// A short type-specific detail line (e.g., point count for a time series, record length
        /// for input data, kind and run state for an analysis).
        /// </summary>
        [JsonPropertyName("detail")]
        public string? Detail { get; set; }
    }
}
