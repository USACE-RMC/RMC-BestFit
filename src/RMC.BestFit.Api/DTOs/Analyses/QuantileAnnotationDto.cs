using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Model-computed display coordinates for an enabled quantile prior or B17C penalty.
    /// Bounds and mean are in physical units, matching the desktop plot properties.
    /// </summary>
    public class QuantileAnnotationDto
    {
        /// <summary>The annual exceedance probability.</summary>
        [JsonPropertyName("aep")]
        public double Aep { get; set; }

        /// <summary>The mean display magnitude.</summary>
        [JsonPropertyName("value")]
        public double Value { get; set; }

        /// <summary>The model's lower display bound.</summary>
        [JsonPropertyName("lowerBound")]
        public double LowerBound { get; set; }

        /// <summary>The model's upper display bound.</summary>
        [JsonPropertyName("upperBound")]
        public double UpperBound { get; set; }
    }
}
