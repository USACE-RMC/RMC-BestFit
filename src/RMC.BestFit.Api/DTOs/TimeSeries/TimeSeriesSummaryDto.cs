using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Summary of a time-series resource: identity, provenance, and extent. Point values are
    /// returned separately (and paged) because series can hold millions of ordinates.
    /// </summary>
    public class TimeSeriesSummaryDto
    {
        /// <summary>
        /// The resource id used to reference this time series in later requests
        /// (e.g., block-maxima input data or rating curve creation).
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// The resource display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The resource description, if any.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// UTC timestamp at which the resource was created.
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// How the resource was created ("usgs" or "manual").
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>
        /// The USGS site number the series was downloaded from, when source is "usgs".
        /// </summary>
        [JsonPropertyName("usgsSiteNumber")]
        public string? UsgsSiteNumber { get; set; }

        /// <summary>
        /// The USGS series type that was downloaded (e.g., "dailyDischarge", "measuredStage"),
        /// when source is "usgs".
        /// </summary>
        [JsonPropertyName("seriesType")]
        public string? SeriesType { get; set; }

        /// <summary>
        /// The recording interval of the series (e.g., "oneDay", "irregular").
        /// </summary>
        [JsonPropertyName("timeInterval")]
        public string? TimeInterval { get; set; }

        /// <summary>
        /// The number of ordinates in the series.
        /// </summary>
        [JsonPropertyName("pointCount")]
        public int PointCount { get; set; }

        /// <summary>
        /// The number of ordinates whose value is NaN (missing observations).
        /// </summary>
        [JsonPropertyName("missingCount")]
        public int MissingCount { get; set; }

        /// <summary>
        /// The date of the first ordinate, or null when the series is empty.
        /// </summary>
        [JsonPropertyName("startDate")]
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// The date of the last ordinate, or null when the series is empty.
        /// </summary>
        [JsonPropertyName("endDate")]
        public DateTime? EndDate { get; set; }
    }
}
