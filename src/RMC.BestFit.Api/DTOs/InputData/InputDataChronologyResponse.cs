using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>Model-derived observations and inclusive threshold windows for plotting before fitting.</summary>
    /// <remarks>No annual events are manufactured from aggregate censored counts. The client declares the year convention and units.</remarks>
    public class InputDataChronologyResponse : InputDataResourceResponse
    {
        /// <summary>The chronology contract version.</summary>
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;
    }
}
