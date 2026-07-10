using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating an input-data resource by extracting block maxima (e.g., annual
    /// maxima by water year) from an existing time-series resource, typically a USGS daily-flow download.
    /// </summary>
    public class CreateBlockMaxInputDataRequest
    {
        /// <summary>
        /// The id of the time-series resource to extract block maxima from (create it first via
        /// POST api/timeseries/usgs or api/timeseries/manual).
        /// </summary>
        [Required]
        [JsonPropertyName("timeSeriesId")]
        public Guid TimeSeriesId { get; set; }

        /// <summary>
        /// Optional display name for the resource. Defaults to "Block maxima of {source name}".
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description stored with the resource.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// The block window over which the function is computed. "waterYear" (default, Oct-Sep in
        /// the U.S., controlled by startMonth), "calendarYear", "customYear" (startMonth-endMonth),
        /// "quarter", or "month".
        /// </summary>
        [JsonPropertyName("timeBlock")]
        public TimeBlockWindow TimeBlock { get; set; } = TimeBlockWindow.WaterYear;

        /// <summary>
        /// The function computed over each block. "maximum" (default) for flood frequency; other
        /// options include minimum, mean, and sum depending on the application.
        /// </summary>
        [JsonPropertyName("blockFunction")]
        public BlockFunctionType BlockFunction { get; set; } = BlockFunctionType.Maximum;

        /// <summary>
        /// Optional smoothing applied to the source series before block extraction (e.g., a moving
        /// average for n-day flows). Default "none".
        /// </summary>
        [JsonPropertyName("smoothingFunction")]
        public SmoothingFunctionType SmoothingFunction { get; set; } = SmoothingFunctionType.None;

        /// <summary>
        /// The starting month of the water/custom year window (1-12). Default 10 (October, the
        /// U.S. water year convention).
        /// </summary>
        [Range(1, 12)]
        [JsonPropertyName("startMonth")]
        public int StartMonth { get; set; } = 10;

        /// <summary>
        /// The ending month of the custom year window (1-12). Only used when timeBlock is
        /// "customYear". Default 9 (September).
        /// </summary>
        [Range(1, 12)]
        [JsonPropertyName("endMonth")]
        public int EndMonth { get; set; } = 9;

        /// <summary>
        /// The smoothing period in time steps (e.g., 7 for a 7-day moving average of a daily
        /// series). Only used when smoothingFunction is not "none". Default 1.
        /// </summary>
        [Range(1, int.MaxValue)]
        [JsonPropertyName("period")]
        public int Period { get; set; } = 1;
    }
}
