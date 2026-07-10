using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating an input-data resource by extracting independent
    /// peaks-over-threshold (POT) events from an existing time-series resource.
    /// </summary>
    public class CreatePotInputDataRequest
    {
        /// <summary>
        /// The id of the time-series resource to extract peaks from (create it first via
        /// POST api/timeseries/usgs or api/timeseries/manual).
        /// </summary>
        [Required]
        [JsonPropertyName("timeSeriesId")]
        public Guid TimeSeriesId { get; set; }

        /// <summary>
        /// Optional display name for the resource. Defaults to "POT of {source name}".
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description stored with the resource.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// The threshold magnitude; peaks above this value are recorded as events. Choose it so
        /// the average number of events per year (λ, reported on the created resource) is roughly
        /// 1-3 for typical POT analyses.
        /// </summary>
        [Required]
        [JsonPropertyName("threshold")]
        public double Threshold { get; set; }

        /// <summary>
        /// The minimum number of time steps between recorded peaks, used to enforce event
        /// independence (e.g., 7 for daily data requires a week between peaks). Default 1.
        /// </summary>
        [Range(1, int.MaxValue)]
        [JsonPropertyName("minStepsBetweenPeaks")]
        public int MinStepsBetweenPeaks { get; set; } = 1;

        /// <summary>
        /// Optional smoothing applied to the source series before peak extraction. Default "none".
        /// </summary>
        [JsonPropertyName("smoothingFunction")]
        public SmoothingFunctionType SmoothingFunction { get; set; } = SmoothingFunctionType.None;

        /// <summary>
        /// The smoothing period in time steps. Only used when smoothingFunction is not "none". Default 1.
        /// </summary>
        [Range(1, int.MaxValue)]
        [JsonPropertyName("period")]
        public int Period { get; set; } = 1;

        /// <summary>
        /// Optional explicit average number of events per year (λ). When omitted, the model layer
        /// computes λ from the extracted peaks (events divided by the span of peak years). Set this
        /// to events divided by the full record length when peak-less years at the record edges
        /// should count toward the rate.
        /// </summary>
        [JsonPropertyName("lambda")]
        public double? Lambda { get; set; }
    }
}
