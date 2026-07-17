using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// An interval-censored observation: the true magnitude is known only to lie between
    /// <see cref="LowerBound"/> and <see cref="UpperBound"/> (e.g., a historical flood bracketed
    /// by high-water marks).
    /// </summary>
    public class IntervalObservationDto
    {
        /// <summary>
        /// The integer time index of the observation, typically the water year.
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; }

        /// <summary>
        /// The lower bound of the interval.
        /// </summary>
        [JsonPropertyName("lowerBound")]
        public double LowerBound { get; set; }

        /// <summary>
        /// The upper bound of the interval.
        /// </summary>
        [JsonPropertyName("upperBound")]
        public double UpperBound { get; set; }

        /// <summary>
        /// The representative value used for plotting. Optional in requests; defaults to the
        /// interval midpoint.
        /// </summary>
        [JsonPropertyName("value")]
        public double? Value { get; set; }

        /// <summary>
        /// The computed plotting position of the observation. Populated in responses; ignored in requests.
        /// </summary>
        [JsonPropertyName("plottingPosition")]
        public double? PlottingPosition { get; set; }
    }
}
