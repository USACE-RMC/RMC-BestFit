using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying the sample summary statistics of an input-data resource
    /// (record length, mean, standard deviation, skew, and related measures computed by the model layer).
    /// </summary>
    public class SummaryStatisticsResponse : ResponseBase
    {
        /// <summary>
        /// The id of the input-data resource the statistics describe.
        /// </summary>
        [JsonPropertyName("inputDataId")]
        public Guid InputDataId { get; set; }

        /// <summary>
        /// The summary statistics keyed by measure name, as computed by the model layer over all
        /// data (exact, interval, and threshold observations). Values may be NaN when a measure is
        /// undefined for the data set.
        /// </summary>
        [JsonPropertyName("statistics")]
        public Dictionary<string, double> Statistics { get; set; } = new();
    }
}
