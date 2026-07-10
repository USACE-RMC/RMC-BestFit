using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Bulletin 17C-specific run information.
    /// </summary>
    public class Bulletin17CInfoDto
    {
        /// <summary>
        /// The uncertainty quantification method that produced the sampled parameter sets
        /// ("multivariateNormal", "linkedMultivariateNormal", "bootstrap", "biasCorrectedBootstrap").
        /// </summary>
        [JsonPropertyName("uncertaintyMethod")]
        public string? UncertaintyMethod { get; set; }

        /// <summary>
        /// Wall-clock time of the generalized-method-of-moments parameter fit, in milliseconds.
        /// </summary>
        [JsonPropertyName("gmmElapsedMs")]
        public long? GmmElapsedMs { get; set; }

        /// <summary>
        /// Wall-clock time of the uncertainty quantification step, in milliseconds.
        /// </summary>
        [JsonPropertyName("uncertaintyElapsedMs")]
        public long? UncertaintyElapsedMs { get; set; }
    }
}
