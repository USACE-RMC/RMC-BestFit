using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying the results of a rating curve analysis: the fitted stage-discharge
    /// curve with uncertainty, the fitted parameters, and run diagnostics.
    /// </summary>
    public class RatingCurveResultsResponse : ResponseBase
    {
        /// <summary>
        /// The id of the analysis the results belong to.
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid AnalysisId { get; set; }

        /// <summary>
        /// The analysis kind (always "ratingCurve").
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// The fitted rating curve with credible intervals over the stage grid.
        /// </summary>
        [JsonPropertyName("ratingCurve")]
        public RatingCurveDto? RatingCurve { get; set; }

        /// <summary>
        /// The number of piecewise power-law segments in the fitted model.
        /// </summary>
        [JsonPropertyName("numberOfSegments")]
        public int NumberOfSegments { get; set; }

        /// <summary>
        /// The number of date-aligned (stage, discharge) observation pairs used in the fit.
        /// </summary>
        [JsonPropertyName("alignedObservationCount")]
        public int AlignedObservationCount { get; set; }

        /// <summary>
        /// The fitted model parameters at the point estimate (offset, log-coefficients,
        /// exponents, breakpoints, error scale).
        /// </summary>
        [JsonPropertyName("parameters")]
        public List<ParameterValueDto> Parameters { get; set; } = new();

        /// <summary>
        /// Posterior summaries per parameter, including convergence diagnostics.
        /// </summary>
        [JsonPropertyName("parameterSummaries")]
        public List<ParameterSummaryDto> ParameterSummaries { get; set; } = new();

        /// <summary>
        /// Model-fit information criteria.
        /// </summary>
        [JsonPropertyName("informationCriteria")]
        public InformationCriteriaDto? InformationCriteria { get; set; }

        /// <summary>
        /// Run diagnostics (sampler settings, acceptance rates, convergence warnings, timing).
        /// </summary>
        [JsonPropertyName("diagnostics")]
        public DiagnosticsDto? Diagnostics { get; set; }
    }
}
