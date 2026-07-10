using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying a single input-data resource summary, optionally with the full
    /// observation lists (includeData=true).
    /// </summary>
    public class InputDataResourceResponse : ResponseBase
    {
        /// <summary>
        /// The input-data resource summary.
        /// </summary>
        [JsonPropertyName("inputData")]
        public InputDataSummaryDto? InputData { get; set; }

        /// <summary>
        /// The exact observations with computed plotting positions, when requested; otherwise null.
        /// </summary>
        [JsonPropertyName("exactData")]
        public List<ExactObservationDto>? ExactData { get; set; }

        /// <summary>
        /// The uncertain observations with their measurement-error distributions, when requested
        /// and present; otherwise null.
        /// </summary>
        [JsonPropertyName("uncertainData")]
        public List<UncertainObservationDto>? UncertainData { get; set; }

        /// <summary>
        /// The interval-censored observations, when requested and present; otherwise null.
        /// </summary>
        [JsonPropertyName("intervalData")]
        public List<IntervalObservationDto>? IntervalData { get; set; }

        /// <summary>
        /// The perception-threshold records, when requested and present; otherwise null.
        /// </summary>
        [JsonPropertyName("thresholdData")]
        public List<ThresholdObservationDto>? ThresholdData { get; set; }
    }
}
