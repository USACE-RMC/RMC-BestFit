using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Describes one probability distribution available for fitting, including which analysis
    /// kinds support it.
    /// </summary>
    public class DistributionInfoDto
    {
        /// <summary>
        /// The camelCase enum name accepted by create-analysis requests (e.g., "logPearsonTypeIII").
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The human-readable distribution name (e.g., "Log-Pearson Type III").
        /// </summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// The analysis kinds that accept this distribution ("univariate" and/or "bulletin17c").
        /// </summary>
        [JsonPropertyName("supportedBy")]
        public List<string> SupportedBy { get; set; } = new();

        /// <summary>
        /// The canonical parameter names in order (e.g., ["Mean (of log)", "Std Dev (of log)",
        /// "Skew (of log)"]) — the order distribution specs supply parameter values in, and the
        /// names parameter priors and Bulletin 17C parameter penalties are matched against.
        /// </summary>
        [JsonPropertyName("parameterNames")]
        public List<string> ParameterNames { get; set; } = new();
    }
}
