using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a time-series resource from client-supplied points.
    /// </summary>
    public class CreateManualTimeSeriesRequest
    {
        /// <summary>
        /// Optional display name for the resource. Defaults to "Manual time series".
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description stored with the resource.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// The recording interval of the series. Use "oneDay" for daily records; use "irregular"
        /// for discrete measurements that do not fall on a fixed interval (e.g., field measurements
        /// for a rating curve).
        /// </summary>
        [JsonPropertyName("timeInterval")]
        public TimeInterval TimeInterval { get; set; } = TimeInterval.OneDay;

        /// <summary>
        /// The ordinates of the series. Points are sorted by date-time before the series is built.
        /// Use NaN values to mark missing observations.
        /// </summary>
        [Required]
        [MinLength(1)]
        [JsonPropertyName("points")]
        public List<TimeSeriesPointDto> Points { get; set; } = new();
    }
}
