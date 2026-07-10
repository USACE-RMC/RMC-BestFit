using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Distributions;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a Bayesian MCMC competing risks frequency analysis linked to an
    /// input-data resource. Competing risks model each annual maximum as the maximum of 1-3
    /// independent flood-generating processes (e.g., rainfall and snowmelt), each described by
    /// its own component distribution.
    /// </summary>
    public class CreateCompetingRisksAnalysisRequest
    {
        /// <summary>
        /// The id of the input-data resource to fit (create it first via the api/inputdata endpoints).
        /// </summary>
        [Required]
        [JsonPropertyName("inputDataId")]
        public Guid InputDataId { get; set; }

        /// <summary>
        /// The 1-3 component distribution types, one per competing process (e.g., ["gumbel",
        /// "generalizedExtremeValue"]). All 15 supported distributions are accepted; see
        /// GET api/metadata/distributions.
        /// </summary>
        [Required]
        [MinLength(1)]
        [MaxLength(3)]
        [JsonPropertyName("distributions")]
        public List<UnivariateDistributionType> Distributions { get; set; } = new();

        /// <summary>
        /// Optional annual exceedance probabilities (AEP) at which the frequency curve is
        /// evaluated. Each value must be strictly between 0 and 1. Leave null for the 25 defaults.
        /// </summary>
        [JsonPropertyName("probabilityOrdinates")]
        public List<double>? ProbabilityOrdinates { get; set; }

        /// <summary>
        /// Optional Bayesian MCMC settings; omitted fields keep the model defaults.
        /// </summary>
        [JsonPropertyName("bayesianOptions")]
        public BayesianOptionsDto? BayesianOptions { get; set; }

        /// <summary>
        /// Optional informative priors on individual component parameters, matched by parameter
        /// name. Unnamed parameters keep the default flat priors.
        /// </summary>
        [JsonPropertyName("parameterPriors")]
        public List<ParameterPriorDto>? ParameterPriors { get; set; }

        /// <summary>
        /// Optional priors on quantiles of the competing risks distribution — engineering
        /// judgment about flood magnitudes at chosen exceedance probabilities.
        /// </summary>
        [JsonPropertyName("quantilePriors")]
        public List<QuantilePriorDto>? QuantilePriors { get; set; }

        /// <summary>
        /// True to use the single-quantile-prior formulation (Viglione et al., 2013); false for
        /// one prior per parameter. Only meaningful when quantilePriors are supplied. Leave null
        /// for the model default.
        /// </summary>
        [JsonPropertyName("useSingleQuantile")]
        public bool? UseSingleQuantile { get; set; }

        /// <summary>
        /// Optional display name for the analysis. Defaults to "Competing risks analysis of {input data name}".
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description stored with the analysis.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
