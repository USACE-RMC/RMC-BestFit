using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Describes the fitted distribution: its type and point-estimate parameter values.
    /// </summary>
    public class FittedDistributionDto
    {
        /// <summary>
        /// The distribution type (camelCase enum name, e.g., "logPearsonTypeIII").
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// The human-readable distribution name (e.g., "Log-Pearson Type III").
        /// </summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// The point estimator behind the reported values ("posteriorMode" or "posteriorMean").
        /// </summary>
        [JsonPropertyName("pointEstimator")]
        public string? PointEstimator { get; set; }

        /// <summary>
        /// The fitted parameter values at the point estimate.
        /// </summary>
        [JsonPropertyName("parameters")]
        public List<ParameterValueDto> Parameters { get; set; } = new();
    }
}
