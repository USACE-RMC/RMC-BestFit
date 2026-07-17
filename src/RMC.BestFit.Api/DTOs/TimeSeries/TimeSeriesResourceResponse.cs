using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying a single time-series resource summary, optionally with a page of points.
    /// </summary>
    public class TimeSeriesResourceResponse : ResponseBase
    {
        /// <summary>
        /// The time-series resource summary.
        /// </summary>
        [JsonPropertyName("timeSeries")]
        public TimeSeriesSummaryDto? TimeSeries { get; set; }

        /// <summary>
        /// A page of ordinates when the request asked for points (includePoints=true); otherwise null.
        /// Use the offset/limit query parameters to page through large series.
        /// </summary>
        [JsonPropertyName("points")]
        public List<TimeSeriesPointDto>? Points { get; set; }

        /// <summary>
        /// The zero-based offset of the first returned point within the full series, when points
        /// are included; otherwise null.
        /// </summary>
        [JsonPropertyName("pointsOffset")]
        public int? PointsOffset { get; set; }
    }
}
