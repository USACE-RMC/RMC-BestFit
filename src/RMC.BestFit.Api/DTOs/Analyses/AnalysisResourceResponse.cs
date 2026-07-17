using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying a single analysis resource summary.
    /// </summary>
    public class AnalysisResourceResponse : ResponseBase
    {
        /// <summary>
        /// The analysis resource summary.
        /// </summary>
        [JsonPropertyName("analysis")]
        public AnalysisSummaryDto? Analysis { get; set; }
    }
}
