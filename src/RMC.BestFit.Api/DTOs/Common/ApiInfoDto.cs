using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Body of the GET api/info endpoint describing the service and its capabilities.
    /// </summary>
    public class ApiInfoDto
    {
        /// <summary>
        /// The service name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "RMC-BestFit API";

        /// <summary>
        /// The API assembly version.
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>
        /// A short description of what the service provides.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// The feature areas currently exposed by the service (e.g., "timeseries", "inputdata").
        /// </summary>
        [JsonPropertyName("features")]
        public List<string> Features { get; set; } = new();
    }
}
