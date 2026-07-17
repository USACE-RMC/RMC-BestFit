using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body of the frequency workflow endpoints. On step failure, success is false, the
    /// failed step is named, and the ids of resources created by the completed steps are preserved
    /// so the client can inspect intermediates and resume manually.
    /// </summary>
    public class UsgsFrequencyWorkflowResponse : ResponseBase
    {
        /// <summary>
        /// The step that failed ("downloadTimeSeries", "createInputData", "createAnalysis",
        /// "runAnalysis"); null when the workflow succeeded.
        /// </summary>
        [JsonPropertyName("failedStep")]
        public string? FailedStep { get; set; }

        /// <summary>
        /// The id of the created time-series resource (block-maxima workflow only); may be set
        /// even when a later step failed.
        /// </summary>
        [JsonPropertyName("timeSeriesId")]
        public Guid? TimeSeriesId { get; set; }

        /// <summary>
        /// The id of the created input-data resource; may be set even when a later step failed.
        /// </summary>
        [JsonPropertyName("inputDataId")]
        public Guid? InputDataId { get; set; }

        /// <summary>
        /// The id of the created analysis; may be set even when the run failed (the analysis can
        /// be rerun via POST api/analyses/.../{id}/run).
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid? AnalysisId { get; set; }

        /// <summary>
        /// The frequency results when the workflow completed; null on failure.
        /// </summary>
        [JsonPropertyName("results")]
        public FrequencyResultsResponse? Results { get; set; }
    }
}
