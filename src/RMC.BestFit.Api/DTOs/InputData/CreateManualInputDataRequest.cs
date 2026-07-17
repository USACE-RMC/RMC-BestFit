using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating an input-data resource from client-supplied observations
    /// (systematic record, optionally with uncertain, interval-censored, and perception-threshold
    /// data).
    /// </summary>
    public class CreateManualInputDataRequest
    {
        /// <summary>
        /// Optional display name for the resource. Defaults to "Manual input data".
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description stored with the resource.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// The exact (uncensored) observations, e.g., annual peak discharges by water year.
        /// </summary>
        [Required]
        [MinLength(1)]
        [JsonPropertyName("exactData")]
        public List<ExactObservationDto> ExactData { get; set; } = new();

        /// <summary>
        /// Optional uncertain observations whose magnitudes are known only as measurement-error
        /// distributions (e.g., paleoflood estimates). Bayesian estimation propagates the full
        /// distributions through the likelihood. An uncertain observation may not share a time
        /// index with an exact observation.
        /// </summary>
        [JsonPropertyName("uncertainData")]
        public List<UncertainObservationDto>? UncertainData { get; set; }

        /// <summary>
        /// Optional interval-censored observations (magnitude known only within bounds).
        /// </summary>
        [JsonPropertyName("intervalData")]
        public List<IntervalObservationDto>? IntervalData { get; set; }

        /// <summary>
        /// Optional perception-threshold records describing historical periods when only floods
        /// above a threshold would have been observed.
        /// </summary>
        [JsonPropertyName("thresholdData")]
        public List<ThresholdObservationDto>? ThresholdData { get; set; }

        /// <summary>
        /// The plotting-position parameter "a" in the general formula p = (i - a) / (n + 1 - 2a).
        /// 0 = Weibull (default), 0.375 = Blom, 0.40 = Cunnane, 0.44 = Gringorten, 0.5 = Hazen.
        /// Affects display positions only, not the fit.
        /// </summary>
        [Range(0.0, 0.5)]
        [JsonPropertyName("plottingParameter")]
        public double? PlottingParameter { get; set; }

        /// <summary>
        /// Optional low-outlier threshold: exact observations at or below this magnitude are
        /// flagged as low outliers and censored during fitting.
        /// </summary>
        [JsonPropertyName("lowOutlierThreshold")]
        public double? LowOutlierThreshold { get; set; }

        /// <summary>
        /// The average number of events per time index (λ). Leave null for annual-maximum data
        /// (computed from the record, ≈1); set explicitly for peaks-over-threshold data entered
        /// manually (events per year).
        /// </summary>
        [JsonPropertyName("lambda")]
        public double? Lambda { get; set; }
    }
}
