using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a time-series resource by downloading data from the USGS water services.
    /// </summary>
    public class CreateUsgsTimeSeriesRequest
    {
        /// <summary>
        /// The 8-digit USGS surface-water site number (e.g., "01646500" for the Potomac River at
        /// Little Falls). The first 2 digits are the part number and the following 6 the
        /// downstream-order number.
        /// </summary>
        [Required]
        [JsonPropertyName("siteNumber")]
        public string SiteNumber { get; set; } = string.Empty;

        /// <summary>
        /// The series to download. Use "dailyDischarge"/"dailyStage" for daily mean records (input
        /// to block-maxima extraction), "peakDischarge"/"peakStage" for the annual peak-flow file,
        /// and "measuredDischarge"/"measuredStage" for discrete field measurements (the inputs to a
        /// rating curve analysis).
        /// </summary>
        [JsonPropertyName("seriesType")]
        public TimeSeriesDownload.TimeSeriesType SeriesType { get; set; } = TimeSeriesDownload.TimeSeriesType.DailyDischarge;

        /// <summary>
        /// Optional display name for the resource. Defaults to "USGS {siteNumber} {seriesType}".
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
