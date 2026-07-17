using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Echo of the peaks-over-threshold extraction options an input-data resource was created with.
    /// </summary>
    public class PotOptionsDto
    {
        /// <summary>
        /// The threshold magnitude above which peaks were recorded.
        /// </summary>
        [JsonPropertyName("threshold")]
        public double? Threshold { get; set; }

        /// <summary>
        /// The minimum number of time steps enforced between independent peaks.
        /// </summary>
        [JsonPropertyName("minStepsBetweenPeaks")]
        public int? MinStepsBetweenPeaks { get; set; }

        /// <summary>
        /// The smoothing function applied before extraction ("none", "movingAverage", ...).
        /// </summary>
        [JsonPropertyName("smoothingFunction")]
        public string? SmoothingFunction { get; set; }

        /// <summary>
        /// The smoothing period in time steps.
        /// </summary>
        [JsonPropertyName("period")]
        public int? Period { get; set; }
    }
}
