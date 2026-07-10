using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating an input-data resource by downloading the annual peak-flow file
    /// directly from the USGS (no intermediate time-series resource required).
    /// </summary>
    public class CreateUsgsPeaksInputDataRequest
    {
        /// <summary>
        /// The 8-digit USGS surface-water site number (e.g., "01646500").
        /// </summary>
        [Required]
        [JsonPropertyName("siteNumber")]
        public string SiteNumber { get; set; } = string.Empty;

        /// <summary>
        /// The peak series to download: "peakDischarge" (default) or "peakStage". Other series
        /// types are rejected — use POST api/timeseries/usgs followed by api/inputdata/block-max
        /// for daily records.
        /// </summary>
        [JsonPropertyName("seriesType")]
        public TimeSeriesDownload.TimeSeriesType SeriesType { get; set; } = TimeSeriesDownload.TimeSeriesType.PeakDischarge;

        /// <summary>
        /// Optional display name for the resource. Defaults to "USGS {siteNumber} peaks".
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description stored with the resource.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
