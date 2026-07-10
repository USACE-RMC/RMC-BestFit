using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body of the rating curve workflow endpoint. On step failure, success is false, the
    /// failed step is named, and the ids of resources created by the completed steps are preserved.
    /// </summary>
    public class UsgsRatingCurveWorkflowResponse : ResponseBase
    {
        /// <summary>
        /// The step that failed ("downloadStage", "downloadDischarge", "createAnalysis",
        /// "runAnalysis"); null when the workflow succeeded.
        /// </summary>
        [JsonPropertyName("failedStep")]
        public string? FailedStep { get; set; }

        /// <summary>
        /// The id of the created stage time-series resource; may be set even when a later step failed.
        /// </summary>
        [JsonPropertyName("stageTimeSeriesId")]
        public Guid? StageTimeSeriesId { get; set; }

        /// <summary>
        /// The id of the created discharge time-series resource; may be set even when a later step failed.
        /// </summary>
        [JsonPropertyName("dischargeTimeSeriesId")]
        public Guid? DischargeTimeSeriesId { get; set; }

        /// <summary>
        /// The id of the created analysis; may be set even when the run failed.
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid? AnalysisId { get; set; }

        /// <summary>
        /// The rating curve results when the workflow completed; null on failure.
        /// </summary>
        [JsonPropertyName("results")]
        public RatingCurveResultsResponse? Results { get; set; }
    }
}
