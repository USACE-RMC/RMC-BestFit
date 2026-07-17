using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A single time-series ordinate (date-time and value).
    /// </summary>
    public class TimeSeriesPointDto
    {
        /// <summary>
        /// The date-time of the ordinate.
        /// </summary>
        [JsonPropertyName("dateTime")]
        public DateTime DateTime { get; set; }

        /// <summary>
        /// The observed value at the ordinate. NaN marks a missing observation.
        /// </summary>
        [JsonPropertyName("value")]
        public double Value { get; set; }
    }
}
