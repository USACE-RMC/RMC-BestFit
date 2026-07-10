using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A fitted stage-discharge rating curve with uncertainty: discharge evaluated over a stage
    /// grid, with point-estimate, posterior-mean, and credible-interval curves aligned
    /// index-by-index to <see cref="Stages"/>.
    /// </summary>
    public class RatingCurveDto
    {
        /// <summary>
        /// The stage grid the discharge curves are evaluated at.
        /// </summary>
        [JsonPropertyName("stages")]
        public List<double> Stages { get; set; } = new();

        /// <summary>
        /// Discharge of the point-estimate curve, one per stage.
        /// </summary>
        [JsonPropertyName("modeCurve")]
        public List<double>? ModeCurve { get; set; }

        /// <summary>
        /// Discharge of the posterior-mean curve, one per stage.
        /// </summary>
        [JsonPropertyName("meanCurve")]
        public List<double>? MeanCurve { get; set; }

        /// <summary>
        /// Lower credible-interval discharge bound, one per stage.
        /// </summary>
        [JsonPropertyName("ciLower")]
        public List<double>? CiLower { get; set; }

        /// <summary>
        /// Upper credible-interval discharge bound, one per stage.
        /// </summary>
        [JsonPropertyName("ciUpper")]
        public List<double>? CiUpper { get; set; }

        /// <summary>
        /// The width of the reported credible interval (e.g., 0.90 for 90% intervals).
        /// </summary>
        [JsonPropertyName("credibleIntervalWidth")]
        public double CredibleIntervalWidth { get; set; }

        /// <summary>
        /// The minimum stage of the output grid.
        /// </summary>
        [JsonPropertyName("minStage")]
        public double? MinStage { get; set; }

        /// <summary>
        /// The maximum stage of the output grid.
        /// </summary>
        [JsonPropertyName("maxStage")]
        public double? MaxStage { get; set; }

        /// <summary>
        /// The number of stage grid points.
        /// </summary>
        [JsonPropertyName("stageBins")]
        public int? StageBins { get; set; }
    }
}
