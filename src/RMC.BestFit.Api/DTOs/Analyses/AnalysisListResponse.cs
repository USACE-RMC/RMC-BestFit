using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body listing analysis resources currently held by the server.
    /// </summary>
    public class AnalysisListResponse : ResponseBase
    {
        /// <summary>
        /// The number of listed analyses.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>
        /// Summaries of the listed analyses, ordered by creation time.
        /// </summary>
        [JsonPropertyName("analyses")]
        public List<AnalysisSummaryDto> Analyses { get; set; } = new();
    }
}
