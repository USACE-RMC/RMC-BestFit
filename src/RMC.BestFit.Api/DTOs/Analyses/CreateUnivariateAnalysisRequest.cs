using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Distributions;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a Bayesian MCMC univariate frequency analysis linked to an
    /// input-data resource. The analysis clones the input data at creation, so later changes to
    /// the input-data resource cannot affect it.
    /// </summary>
    public class CreateUnivariateAnalysisRequest
    {
        /// <summary>
        /// The id of the input-data resource to fit (create it first via the api/inputdata endpoints).
        /// </summary>
        [Required]
        [JsonPropertyName("inputDataId")]
        public Guid InputDataId { get; set; }

        /// <summary>
        /// The probability distribution to fit. All 15 supported distributions are listed by
        /// GET api/metadata/distributions (e.g., "logPearsonTypeIII", "generalizedExtremeValue",
        /// "gumbel", "lnNormal"). Default "logPearsonTypeIII", the U.S. flood-frequency convention.
        /// </summary>
        [JsonPropertyName("distribution")]
        public UnivariateDistributionType Distribution { get; set; } = UnivariateDistributionType.LogPearsonTypeIII;

        /// <summary>
        /// Optional annual exceedance probabilities (AEP) at which the frequency curve is
        /// evaluated (e.g., 0.01 is the 100-year event). Each value must be strictly between
        /// 0 and 1. Leave null for the 25 default ordinates (see GET api/metadata/defaults).
        /// </summary>
        [JsonPropertyName("probabilityOrdinates")]
        public List<double>? ProbabilityOrdinates { get; set; }

        /// <summary>
        /// Optional Bayesian MCMC settings; omitted fields keep the model defaults.
        /// </summary>
        [JsonPropertyName("bayesianOptions")]
        public BayesianOptionsDto? BayesianOptions { get; set; }

        /// <summary>
        /// Optional informative priors on individual distribution parameters, matched by
        /// parameter name (see GET api/metadata/distributions for the names per distribution).
        /// Unnamed parameters keep the default flat priors.
        /// </summary>
        [JsonPropertyName("parameterPriors")]
        public List<ParameterPriorDto>? ParameterPriors { get; set; }

        /// <summary>
        /// Optional priors on quantiles of the parent distribution — engineering judgment about
        /// flood magnitudes at chosen exceedance probabilities. Supply one prior per distribution
        /// parameter, or a single prior with useSingleQuantile=true.
        /// </summary>
        [JsonPropertyName("quantilePriors")]
        public List<QuantilePriorDto>? QuantilePriors { get; set; }

        /// <summary>
        /// True to use the single-quantile-prior formulation (Viglione et al., 2013); false for
        /// one prior per distribution parameter (Coles and Tawn, 1996). Only meaningful when
        /// quantilePriors are supplied. Leave null for the model default (one per parameter).
        /// </summary>
        [JsonPropertyName("useSingleQuantile")]
        public bool? UseSingleQuantile { get; set; }

        /// <summary>
        /// Optional display name for the analysis. Defaults to "{distribution} analysis of {input data name}".
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
