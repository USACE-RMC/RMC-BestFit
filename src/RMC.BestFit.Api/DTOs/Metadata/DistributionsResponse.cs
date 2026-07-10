using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body listing the probability distributions available for fitting.
    /// </summary>
    public class DistributionsResponse : ResponseBase
    {
        /// <summary>
        /// The available distributions with the analysis kinds that support each.
        /// </summary>
        [JsonPropertyName("distributions")]
        public List<DistributionInfoDto> Distributions { get; set; } = new();
    }
}
