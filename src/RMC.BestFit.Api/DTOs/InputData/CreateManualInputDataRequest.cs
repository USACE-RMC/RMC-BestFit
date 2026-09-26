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
        /// Runs the model's Multiple Grubbs-Beck Test after populating all observations.
        /// Defaults to false. Requires at least ten exact observations and cannot be combined
        /// with an explicit low-outlier threshold or preflagged exact observations.
        /// </summary>
        [JsonPropertyName("useMultipleGrubbsBeckTest")]
        public bool UseMultipleGrubbsBeckTest { get; set; }

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
        /// Optional manual low-outlier threshold, applied with
        /// <see cref="RMC.BestFit.Models.DataFrame.SetLowOutliersFromThreshold"/> after the exact
        /// series is populated: every exact observation strictly below the threshold is flagged as
        /// a low outlier, and every observation at or above it is unflagged, regardless of any
        /// <see cref="ExactObservationDto.IsLowOutlier"/> supplied on it. Requires at least ten
        /// exact observations and a threshold that censors no more than half the record (above the
        /// sorted upper-middle value); violating either is a 400 with the data frame's message and
        /// stores nothing. An observation preflagged <see cref="ExactObservationDto.IsLowOutlier"/>
        /// = true with a value at or above this threshold is rejected as contradictory before
        /// anything is built - a preflag on a value already below the threshold agrees and is
        /// accepted. Cannot be combined with <see cref="UseMultipleGrubbsBeckTest"/>. Omit this to
        /// leave every <see cref="ExactObservationDto.IsLowOutlier"/> value exactly as supplied.
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
