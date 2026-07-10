using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Summary of an input-data resource: identity, provenance, record composition, and the
    /// settings that shape the likelihood (lambda, plotting parameter, low-outlier threshold).
    /// </summary>
    public class InputDataSummaryDto
    {
        /// <summary>
        /// The resource id used to reference this input data when creating analyses.
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
        /// How the exact series was created ("manual", "blockMaxima", "peaksOverThreshold",
        /// "usgsPeakDischarge", "usgsPeakStage").
        /// </summary>
        [JsonPropertyName("method")]
        public string? Method { get; set; }

        /// <summary>
        /// The id of the source time-series resource, when the data was derived from one.
        /// May reference a since-deleted resource.
        /// </summary>
        [JsonPropertyName("sourceTimeSeriesId")]
        public Guid? SourceTimeSeriesId { get; set; }

        /// <summary>
        /// The USGS site number, when the data was downloaded directly from the USGS peak-flow file.
        /// </summary>
        [JsonPropertyName("usgsSiteNumber")]
        public string? UsgsSiteNumber { get; set; }

        /// <summary>
        /// The total record length in time indices (systematic record plus censored periods).
        /// </summary>
        [JsonPropertyName("recordLength")]
        public int RecordLength { get; set; }

        /// <summary>
        /// The number of exact observations.
        /// </summary>
        [JsonPropertyName("exactCount")]
        public int ExactCount { get; set; }

        /// <summary>
        /// The number of uncertain observations (magnitudes described by measurement-error
        /// distributions).
        /// </summary>
        [JsonPropertyName("uncertainCount")]
        public int UncertainCount { get; set; }

        /// <summary>
        /// The number of interval-censored observations.
        /// </summary>
        [JsonPropertyName("intervalCount")]
        public int IntervalCount { get; set; }

        /// <summary>
        /// The number of perception-threshold records.
        /// </summary>
        [JsonPropertyName("thresholdCount")]
        public int ThresholdCount { get; set; }

        /// <summary>
        /// The average number of events per time index (λ). ≈1 for annual maxima; the events-per-year
        /// rate for peaks-over-threshold data.
        /// </summary>
        [JsonPropertyName("lambda")]
        public double Lambda { get; set; }

        /// <summary>
        /// The plotting-position parameter "a" (0 = Weibull, 0.375 = Blom, 0.44 = Gringorten, ...).
        /// </summary>
        [JsonPropertyName("plottingParameter")]
        public double PlottingParameter { get; set; }

        /// <summary>
        /// The low-outlier threshold, when one is set; observations at or below it are censored.
        /// </summary>
        [JsonPropertyName("lowOutlierThreshold")]
        public double? LowOutlierThreshold { get; set; }

        /// <summary>
        /// The number of exact observations flagged as low outliers.
        /// </summary>
        [JsonPropertyName("lowOutlierCount")]
        public int LowOutlierCount { get; set; }

        /// <summary>
        /// The block-maxima extraction options, when method is "blockMaxima"; otherwise null.
        /// </summary>
        [JsonPropertyName("blockOptions")]
        public BlockOptionsDto? BlockOptions { get; set; }

        /// <summary>
        /// The peaks-over-threshold extraction options, when method is "peaksOverThreshold"; otherwise null.
        /// </summary>
        [JsonPropertyName("potOptions")]
        public PotOptionsDto? PotOptions { get; set; }
    }
}
