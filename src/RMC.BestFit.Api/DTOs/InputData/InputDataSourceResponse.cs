using System.Text.Json;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>Original input request and optional raw USGS payload retained for an input resource.</summary>
    public class InputDataSourceResponse : ResponseBase
    {
        /// <summary>The input resource identifier.</summary>
        [JsonPropertyName("inputDataId")]
        public Guid InputDataId { get; set; }

        /// <summary>The creation time; raw source data was retrieved at or before this time.</summary>
        [JsonPropertyName("capturedUtc")]
        public DateTime CapturedUtc { get; set; }

        /// <summary>An independent snapshot of the original creation request, before model processing.</summary>
        [JsonPropertyName("request")]
        public JsonElement? Request { get; set; }

        /// <summary>The original decoded download, retaining dates, qualifiers and headers.</summary>
        [JsonPropertyName("rawText")]
        public string? RawText { get; set; }

        /// <summary>SHA-256 of the UTF-8 encoded raw text, when available; not a hash of HTTP wire bytes.</summary>
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }
    }
}
