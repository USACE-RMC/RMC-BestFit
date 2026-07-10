using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body listing all time-series resources currently held by the server.
    /// </summary>
    public class TimeSeriesListResponse : ResponseBase
    {
        /// <summary>
        /// The number of time-series resources.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>
        /// Summaries of every time-series resource, ordered by creation time.
        /// </summary>
        [JsonPropertyName("timeSeries")]
        public List<TimeSeriesSummaryDto> TimeSeries { get; set; } = new();
    }
}
