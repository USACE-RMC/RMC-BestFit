using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for the one-shot USGS rating curve workflow: download the discrete field
    /// measurements of stage and discharge for a site, create a rating curve analysis over the
    /// date-aligned pairs, run it, and return the fitted curve — all in one call.
    /// </summary>
    public class UsgsRatingCurveWorkflowRequest
    {
        /// <summary>
        /// The 8-digit USGS surface-water site number.
        /// </summary>
        [Required]
        [JsonPropertyName("siteNumber")]
        public string SiteNumber { get; set; } = string.Empty;

        /// <summary>
        /// The number of piecewise power-law segments (1-3). Default 1.
        /// </summary>
        [Range(1, 3)]
        [JsonPropertyName("numberOfSegments")]
        public int NumberOfSegments { get; set; } = 1;

        /// <summary>
        /// Optional minimum stage of the output grid (supply with maxStage).
        /// </summary>
        [JsonPropertyName("minStage")]
        public double? MinStage { get; set; }

        /// <summary>
        /// Optional maximum stage of the output grid (supply with minStage).
        /// </summary>
        [JsonPropertyName("maxStage")]
        public double? MaxStage { get; set; }

        /// <summary>
        /// Optional number of stage grid points (2-1000); null for the model default.
        /// </summary>
        [Range(2, 1000)]
        [JsonPropertyName("stageBins")]
        public int? StageBins { get; set; }

        /// <summary>
        /// Optional Bayesian MCMC settings; omitted fields keep the model defaults.
        /// </summary>
        [JsonPropertyName("bayesianOptions")]
        public BayesianOptionsDto? BayesianOptions { get; set; }

        /// <summary>
        /// Optional base name for the created resources.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
