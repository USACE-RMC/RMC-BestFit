using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A fitted model parameter: its name and point-estimate value.
    /// </summary>
    public class ParameterValueDto
    {
        /// <summary>
        /// The parameter name (e.g., "Mu", "Sigma", or rating-curve names like the offset and exponents).
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The point-estimate value of the parameter (per the configured point estimator).
        /// </summary>
        [JsonPropertyName("value")]
        public double Value { get; set; }
    }
}
