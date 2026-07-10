using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Distributions;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A fully specified univariate probability distribution: a type plus its parameter values in
    /// the distribution's canonical order. Used wherever a request supplies a distribution as an
    /// input — per-observation measurement-error distributions (uncertain data), parameter priors,
    /// and quantile priors.
    /// </summary>
    /// <remarks>
    /// The canonical parameter order and names for each type are listed by
    /// GET api/metadata/distributions (e.g., normal = [mean, standard deviation];
    /// triangular = [min, most likely, max]). Parameter values are validated by the model layer;
    /// invalid values (e.g., a non-positive standard deviation) are rejected with HTTP 400.
    /// </remarks>
    public class DistributionSpecDto
    {
        /// <summary>
        /// The distribution type (camelCase string, e.g., "normal", "logNormal", "triangular",
        /// "uniform", "pertPercentile"). Accepted values are listed by GET api/metadata/enums
        /// under "priorDistributions". Default "normal".
        /// </summary>
        [JsonPropertyName("type")]
        public UnivariateDistributionType Type { get; set; } = UnivariateDistributionType.Normal;

        /// <summary>
        /// The parameter values in the distribution's canonical order (see
        /// GET api/metadata/distributions for the names and order per type). The count must match
        /// the distribution's parameter count exactly.
        /// </summary>
        [Required]
        [JsonPropertyName("parameters")]
        public List<double> Parameters { get; set; } = new();
    }
}
