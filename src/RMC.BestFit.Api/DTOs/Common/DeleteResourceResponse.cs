using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body confirming the deletion of a server-side resource.
    /// </summary>
    public class DeleteResourceResponse : ResponseBase
    {
        /// <summary>
        /// The id of the resource that was deleted.
        /// </summary>
        [JsonPropertyName("deletedId")]
        public Guid DeletedId { get; set; }

        /// <summary>
        /// The type of the deleted resource ("timeSeries", "inputData", or "analysis").
        /// </summary>
        [JsonPropertyName("resourceType")]
        public string? ResourceType { get; set; }
    }
}
