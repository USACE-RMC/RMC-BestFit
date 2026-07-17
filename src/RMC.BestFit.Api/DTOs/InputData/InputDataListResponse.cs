using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body listing all input-data resources currently held by the server.
    /// </summary>
    public class InputDataListResponse : ResponseBase
    {
        /// <summary>
        /// The number of input-data resources.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>
        /// Summaries of every input-data resource, ordered by creation time.
        /// </summary>
        [JsonPropertyName("inputData")]
        public List<InputDataSummaryDto> InputData { get; set; } = new();
    }
}
