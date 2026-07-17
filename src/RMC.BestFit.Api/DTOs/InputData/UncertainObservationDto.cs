using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// An uncertain observation: the magnitude is known only as a probability distribution
    /// describing its measurement error (e.g., a paleoflood estimate expressed as a triangular or
    /// log-normal distribution). Bayesian estimation propagates the full distribution through the
    /// likelihood; the distribution's mean serves as the nominal value for plotting positions and
    /// summary statistics.
    /// </summary>
    /// <remarks>
    /// In requests, supply either <see cref="Index"/> or <see cref="DateTime"/> plus the
    /// measurement-error <see cref="Distribution"/>. An uncertain observation may not share a time
    /// index with an exact observation. In responses, <see cref="Value"/> and
    /// <see cref="PlottingPosition"/> carry the computed nominal value and plotting position.
    /// Note the Bulletin 17C analysis does not use uncertain observations (its Expected Moments
    /// Algorithm has no measurement-error likelihood); use the univariate analysis instead.
    /// </remarks>
    public class UncertainObservationDto
    {
        /// <summary>
        /// The integer time index of the observation, typically the water year (e.g., 1875 for a
        /// historical flood). Required in requests unless <see cref="DateTime"/> is supplied.
        /// </summary>
        [JsonPropertyName("index")]
        public int? Index { get; set; }

        /// <summary>
        /// The date-time of the observation; its year is used as the time index. Alternative to
        /// <see cref="Index"/> in requests.
        /// </summary>
        [JsonPropertyName("dateTime")]
        public DateTime? DateTime { get; set; }

        /// <summary>
        /// The measurement-error distribution of the observed magnitude (e.g., triangular with
        /// [min, most likely, max] from a paleoflood study, or normal with [best estimate,
        /// standard error]).
        /// </summary>
        [Required]
        [JsonPropertyName("distribution")]
        public DistributionSpecDto? Distribution { get; set; }

        /// <summary>
        /// The nominal magnitude (the mean of the measurement-error distribution). Populated in
        /// responses; ignored in requests.
        /// </summary>
        [JsonPropertyName("value")]
        public double? Value { get; set; }

        /// <summary>
        /// The computed plotting position (exceedance probability) of the observation. Populated
        /// in responses; ignored in requests.
        /// </summary>
        [JsonPropertyName("plottingPosition")]
        public double? PlottingPosition { get; set; }
    }
}
