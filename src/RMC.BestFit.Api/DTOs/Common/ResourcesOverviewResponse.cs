using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body of the cross-cutting GET api/resources endpoint listing every resource in the store.
    /// </summary>
    public class ResourcesOverviewResponse : ResponseBase
    {
        /// <summary>
        /// The number of time-series resources currently held.
        /// </summary>
        [JsonPropertyName("timeSeriesCount")]
        public int TimeSeriesCount { get; set; }

        /// <summary>
        /// The number of input-data resources currently held.
        /// </summary>
        [JsonPropertyName("inputDataCount")]
        public int InputDataCount { get; set; }

        /// <summary>
        /// The number of analysis resources currently held.
        /// </summary>
        [JsonPropertyName("analysisCount")]
        public int AnalysisCount { get; set; }

        /// <summary>
        /// One-line summaries of every resource, ordered by creation time.
        /// </summary>
        [JsonPropertyName("resources")]
        public List<ResourceSummaryDto> Resources { get; set; } = new();
    }
}
