using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// An informative prior on a single model parameter for Bayesian estimation. Parameters not
    /// named in the request keep the model's default flat (uniform) prior, so partial
    /// specification is supported.
    /// </summary>
    /// <remarks>
    /// Supplying any parameter prior switches the model off its automatic flat-prior defaults for
    /// the whole analysis (the named parameters get the supplied priors; the rest keep the flat
    /// priors already in place). Prior densities are evaluated in real parameter space.
    /// </remarks>
    public class ParameterPriorDto
    {
        /// <summary>
        /// The name of the parameter the prior applies to, matched case-insensitively against the
        /// model's parameter names. The canonical names per distribution are listed by
        /// GET api/metadata/distributions (e.g., Log-Pearson Type III: "Mean (of log)",
        /// "Std Dev (of log)", "Skew (of log)").
        /// </summary>
        [Required]
        [JsonPropertyName("parameterName")]
        public string ParameterName { get; set; } = string.Empty;

        /// <summary>
        /// The prior distribution for the parameter (e.g., a normal prior on a regional skew).
        /// </summary>
        [Required]
        [JsonPropertyName("distribution")]
        public DistributionSpecDto? Distribution { get; set; }

        /// <summary>
        /// True to hold the parameter fixed at its current value during estimation (it is not
        /// sampled). Leave null or false to estimate the parameter. Default null (estimated).
        /// </summary>
        [JsonPropertyName("isFixed")]
        public bool? IsFixed { get; set; }
    }
}
