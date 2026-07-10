using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// An exact (uncensored) observation. In requests, supply either <see cref="Index"/> (the
    /// integer time index, typically the water year) or <see cref="DateTime"/> (from which the
    /// year is taken). In responses, <see cref="PlottingPosition"/> carries the computed value.
    /// </summary>
    public class ExactObservationDto
    {
        /// <summary>
        /// The integer time index of the observation, typically the water year (e.g., 1996).
        /// Required in requests unless <see cref="DateTime"/> is supplied.
        /// </summary>
        [JsonPropertyName("index")]
        public int? Index { get; set; }

        /// <summary>
        /// The date-time of the observation; its year is used as the time index. Alternative to
        /// <see cref="Index"/> in requests.
        /// </summary>
        [JsonPropertyName("dateTime")]
        public DateTime? DateTime { get; set; }

        /// <summary>
        /// The observed magnitude (e.g., peak discharge in cfs).
        /// </summary>
        [JsonPropertyName("value")]
        public double Value { get; set; }

        /// <summary>
        /// True to mark the observation as a low outlier excluded from fitting (e.g., by the
        /// Multiple Grubbs-Beck test in the desktop application). Default false.
        /// </summary>
        [JsonPropertyName("isLowOutlier")]
        public bool IsLowOutlier { get; set; }

        /// <summary>
        /// The computed plotting position (exceedance probability) of the observation. Populated
        /// in responses; ignored in requests (positions are always recomputed from the data frame's
        /// plotting parameter).
        /// </summary>
        [JsonPropertyName("plottingPosition")]
        public double? PlottingPosition { get; set; }
    }
}
