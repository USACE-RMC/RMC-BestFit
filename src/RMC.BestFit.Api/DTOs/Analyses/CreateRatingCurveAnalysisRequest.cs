using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a Bayesian stage-discharge rating curve analysis linked to a
    /// stage time series and a discharge time series (typically USGS "measuredStage" and
    /// "measuredDischarge" field measurements for the same site). The two series are date-aligned
    /// internally; at least 10 common dates are required.
    /// </summary>
    public class CreateRatingCurveAnalysisRequest
    {
        /// <summary>
        /// The id of the stage (gage height) time-series resource.
        /// </summary>
        [Required]
        [JsonPropertyName("stageTimeSeriesId")]
        public Guid StageTimeSeriesId { get; set; }

        /// <summary>
        /// The id of the discharge time-series resource.
        /// </summary>
        [Required]
        [JsonPropertyName("dischargeTimeSeriesId")]
        public Guid DischargeTimeSeriesId { get; set; }

        /// <summary>
        /// The number of piecewise power-law segments (hydraulic controls), 1-3. Use 1 for a
        /// single channel control; more segments model control changes (e.g., overbank flow). Default 1.
        /// </summary>
        [Range(1, 3)]
        [JsonPropertyName("numberOfSegments")]
        public int NumberOfSegments { get; set; } = 1;

        /// <summary>
        /// Optional minimum stage of the output grid. Supply together with maxStage to override
        /// the automatic grid (data range with padding).
        /// </summary>
        [JsonPropertyName("minStage")]
        public double? MinStage { get; set; }

        /// <summary>
        /// Optional maximum stage of the output grid. Supply together with minStage.
        /// </summary>
        [JsonPropertyName("maxStage")]
        public double? MaxStage { get; set; }

        /// <summary>
        /// Optional number of stage grid points the discharge curves are evaluated at (2-1000).
        /// Leave null for the model default.
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
        /// Optional informative priors on individual rating curve parameters, matched by
        /// parameter name (e.g., a prior on the offset ξ from a surveyed gage datum). Unnamed
        /// parameters keep the default flat priors.
        /// </summary>
        [JsonPropertyName("parameterPriors")]
        public List<ParameterPriorDto>? ParameterPriors { get; set; }

        /// <summary>
        /// Optional display name for the analysis. Defaults to "Rating curve: {stage name} / {discharge name}".
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description stored with the analysis.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
