using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Summary of an analysis resource: identity, kind, configuration highlights, provenance,
    /// current validity, and run state.
    /// </summary>
    public class AnalysisSummaryDto
    {
        /// <summary>
        /// The resource id used to run the analysis and fetch its results.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// The resource display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The resource description, if any.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// UTC timestamp at which the resource was created.
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// The analysis kind ("univariate", "bulletin17C", "ratingCurve").
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// The run state ("created", "running", "succeeded", "failed", "cancelled").
        /// </summary>
        [JsonPropertyName("state")]
        public string? State { get; set; }

        /// <summary>
        /// True when the configuration currently passes model-layer validation.
        /// </summary>
        [JsonPropertyName("isValid")]
        public bool IsValid { get; set; }

        /// <summary>
        /// The validation messages when invalid; null when valid.
        /// </summary>
        [JsonPropertyName("validationMessages")]
        public List<string>? ValidationMessages { get; set; }

        /// <summary>
        /// Non-fatal warnings recorded when the analysis was created (e.g., a Bulletin 17C
        /// analysis over input data with uncertain observations, which its algorithm ignores);
        /// null when there are none.
        /// </summary>
        [JsonPropertyName("warnings")]
        public List<string>? Warnings { get; set; }

        /// <summary>
        /// The fitted distribution ("logPearsonTypeIII", ...) for univariate and Bulletin 17C
        /// analyses; null for rating curves.
        /// </summary>
        [JsonPropertyName("distribution")]
        public string? Distribution { get; set; }

        /// <summary>
        /// The uncertainty method for Bulletin 17C analyses; null for other kinds.
        /// </summary>
        [JsonPropertyName("uncertaintyMethod")]
        public string? UncertaintyMethod { get; set; }

        /// <summary>
        /// The number of rating curve segments; null for other kinds.
        /// </summary>
        [JsonPropertyName("numberOfSegments")]
        public int? NumberOfSegments { get; set; }

        /// <summary>
        /// The component distribution types for mixture and competing risks analyses; null for
        /// other kinds.
        /// </summary>
        [JsonPropertyName("componentDistributions")]
        public List<string>? ComponentDistributions { get; set; }

        /// <summary>
        /// True when a mixture analysis models a point mass at zero; null for other kinds.
        /// </summary>
        [JsonPropertyName("isZeroInflated")]
        public bool? IsZeroInflated { get; set; }

        /// <summary>
        /// True when a point process analysis models seasonal event rates; null for other kinds.
        /// </summary>
        [JsonPropertyName("isSeasonal")]
        public bool? IsSeasonal { get; set; }

        /// <summary>
        /// The peaks-over-threshold threshold of a point process analysis; null for other kinds
        /// or when not yet derived.
        /// </summary>
        [JsonPropertyName("threshold")]
        public double? Threshold { get; set; }

        /// <summary>
        /// The record span in years of a point process analysis; null for other kinds or when
        /// not yet derived.
        /// </summary>
        [JsonPropertyName("totalYears")]
        public double? TotalYears { get; set; }

        /// <summary>
        /// The event rate λ (events per year) of a point process analysis; null for other kinds
        /// or when not yet derived.
        /// </summary>
        [JsonPropertyName("lambda")]
        public double? Lambda { get; set; }

        /// <summary>
        /// The composition method of a composite analysis ("competingRisks", "mixture",
        /// "modelAverage"); null for other kinds.
        /// </summary>
        [JsonPropertyName("compositeType")]
        public string? CompositeType { get; set; }

        /// <summary>
        /// The model-average weighting method of a composite analysis; null for other kinds or
        /// composition methods.
        /// </summary>
        [JsonPropertyName("averageMethod")]
        public string? AverageMethod { get; set; }

        /// <summary>
        /// The competing risks dependence assumption of a composite analysis; null for other
        /// kinds or composition methods.
        /// </summary>
        [JsonPropertyName("dependency")]
        public string? Dependency { get; set; }

        /// <summary>
        /// True when a competing risks composite combines as the maximum of its components; null
        /// for other kinds or composition methods.
        /// </summary>
        [JsonPropertyName("isMaximum")]
        public bool? IsMaximum { get; set; }

        /// <summary>
        /// The ids of a composite analysis's component analyses, in composite order (provenance —
        /// a referenced resource may since have been deleted); null for other kinds.
        /// </summary>
        [JsonPropertyName("componentAnalysisIds")]
        public List<Guid>? ComponentAnalysisIds { get; set; }

        /// <summary>
        /// The copula family of a bivariate analysis; null for other kinds.
        /// </summary>
        [JsonPropertyName("copulaType")]
        public string? CopulaType { get; set; }

        /// <summary>
        /// The copula estimation method of a bivariate analysis; null for other kinds.
        /// </summary>
        [JsonPropertyName("copulaEstimationMethod")]
        public string? CopulaEstimationMethod { get; set; }

        /// <summary>
        /// The id of the marginal-X analysis of a bivariate analysis (provenance); null for other kinds.
        /// </summary>
        [JsonPropertyName("marginalXAnalysisId")]
        public Guid? MarginalXAnalysisId { get; set; }

        /// <summary>
        /// The id of the marginal-Y analysis of a bivariate analysis (provenance); null for other kinds.
        /// </summary>
        [JsonPropertyName("marginalYAnalysisId")]
        public Guid? MarginalYAnalysisId { get; set; }

        /// <summary>
        /// The id of the upstream bivariate analysis of a coincident frequency analysis
        /// (provenance); null for other kinds.
        /// </summary>
        [JsonPropertyName("bivariateAnalysisId")]
        public Guid? BivariateAnalysisId { get; set; }

        /// <summary>
        /// The number of response-magnitude output bins of a coincident frequency analysis; null
        /// for other kinds.
        /// </summary>
        [JsonPropertyName("numberOfBins")]
        public int? NumberOfBins { get; set; }

        /// <summary>
        /// The model family of a time-series analysis ("ar", "ma", "arima", "arimax"); null for
        /// other kinds.
        /// </summary>
        [JsonPropertyName("timeSeriesModelType")]
        public string? TimeSeriesModelType { get; set; }

        /// <summary>
        /// The id of the source time-series resource of a time-series analysis (provenance);
        /// null for other kinds.
        /// </summary>
        [JsonPropertyName("timeSeriesId")]
        public Guid? TimeSeriesId { get; set; }

        /// <summary>
        /// The ids of an ARIMAX analysis's covariate time-series resources (provenance); null
        /// for other kinds or model types.
        /// </summary>
        [JsonPropertyName("covariateTimeSeriesIds")]
        public List<Guid>? CovariateTimeSeriesIds { get; set; }

        /// <summary>
        /// The forecast horizon in time steps of a time-series analysis; null for other kinds.
        /// </summary>
        [JsonPropertyName("forecastingTimeSteps")]
        public int? ForecastingTimeSteps { get; set; }

        /// <summary>
        /// The id of the input-data resource the analysis was created from (univariate and
        /// Bulletin 17C). May reference a since-deleted resource.
        /// </summary>
        [JsonPropertyName("inputDataId")]
        public Guid? InputDataId { get; set; }

        /// <summary>
        /// The id of the stage time-series resource (rating curves only).
        /// </summary>
        [JsonPropertyName("stageTimeSeriesId")]
        public Guid? StageTimeSeriesId { get; set; }

        /// <summary>
        /// The id of the discharge time-series resource (rating curves only).
        /// </summary>
        [JsonPropertyName("dischargeTimeSeriesId")]
        public Guid? DischargeTimeSeriesId { get; set; }

        /// <summary>
        /// UTC timestamp of the most recent run, when the analysis has been run.
        /// </summary>
        [JsonPropertyName("lastRunUtc")]
        public DateTime? LastRunUtc { get; set; }

        /// <summary>
        /// Wall-clock duration of the most recent run in milliseconds.
        /// </summary>
        [JsonPropertyName("lastRunMs")]
        public long? LastRunMs { get; set; }

        /// <summary>
        /// The error message of the most recent failed run; null otherwise.
        /// </summary>
        [JsonPropertyName("lastError")]
        public string? LastError { get; set; }
    }
}
