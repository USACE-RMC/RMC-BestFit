using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A frequency curve with uncertainty: quantiles evaluated at each exceedance probability,
    /// with point-estimate, posterior-mean, and credible-interval curves aligned index-by-index
    /// to <see cref="Probabilities"/>.
    /// </summary>
    public class FrequencyCurveDto
    {
        /// <summary>
        /// The annual exceedance probabilities (AEP) the curves are evaluated at (e.g., 0.01 is
        /// the 100-year event).
        /// </summary>
        [JsonPropertyName("probabilities")]
        public List<double> Probabilities { get; set; } = new();

        /// <summary>
        /// Quantiles of the point-estimate (mode/computed) curve, one per probability.
        /// </summary>
        [JsonPropertyName("modeCurve")]
        public List<double>? ModeCurve { get; set; }

        /// <summary>
        /// Quantiles of the posterior-mean (predictive) curve, one per probability.
        /// </summary>
        [JsonPropertyName("meanCurve")]
        public List<double>? MeanCurve { get; set; }

        /// <summary>
        /// Lower credible-interval bound, one per probability.
        /// </summary>
        [JsonPropertyName("ciLower")]
        public List<double>? CiLower { get; set; }

        /// <summary>
        /// Upper credible-interval bound, one per probability.
        /// </summary>
        [JsonPropertyName("ciUpper")]
        public List<double>? CiUpper { get; set; }

        /// <summary>
        /// The width of the reported credible interval (e.g., 0.90 for 90% intervals).
        /// </summary>
        [JsonPropertyName("credibleIntervalWidth")]
        public double CredibleIntervalWidth { get; set; }
    }
}
