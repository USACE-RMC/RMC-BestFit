using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Echo of the block-maxima extraction options an input-data resource was created with.
    /// </summary>
    public class BlockOptionsDto
    {
        /// <summary>
        /// The block window ("waterYear", "calendarYear", "customYear", "quarter", "month").
        /// </summary>
        [JsonPropertyName("timeBlock")]
        public string? TimeBlock { get; set; }

        /// <summary>
        /// The function computed over each block ("maximum", "minimum", ...).
        /// </summary>
        [JsonPropertyName("blockFunction")]
        public string? BlockFunction { get; set; }

        /// <summary>
        /// The smoothing function applied before extraction ("none", "movingAverage", ...).
        /// </summary>
        [JsonPropertyName("smoothingFunction")]
        public string? SmoothingFunction { get; set; }

        /// <summary>
        /// The starting month of the water/custom year window.
        /// </summary>
        [JsonPropertyName("startMonth")]
        public int? StartMonth { get; set; }

        /// <summary>
        /// The ending month of the custom year window.
        /// </summary>
        [JsonPropertyName("endMonth")]
        public int? EndMonth { get; set; }

        /// <summary>
        /// The smoothing period in time steps.
        /// </summary>
        [JsonPropertyName("period")]
        public int? Period { get; set; }
    }
}
