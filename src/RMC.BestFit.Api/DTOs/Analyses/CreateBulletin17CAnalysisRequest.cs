using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Distributions;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a Bulletin 17C flood frequency analysis (USGS guidelines:
    /// Expected Moments Algorithm via generalized method of moments, with parametric or bootstrap
    /// uncertainty) linked to an input-data resource.
    /// </summary>
    public class CreateBulletin17CAnalysisRequest
    {
        /// <summary>
        /// The id of the input-data resource to fit — typically USGS annual peaks
        /// (POST api/inputdata/usgs-peaks) or manually entered systematic/historical data.
        /// </summary>
        [Required]
        [JsonPropertyName("inputDataId")]
        public Guid InputDataId { get; set; }

        /// <summary>
        /// The distribution to fit. Bulletin 17C supports "logPearsonTypeIII" (default, the
        /// Bulletin's prescribed distribution), "pearsonTypeIII", "logNormal", "normal",
        /// "gammaDistribution", and "exponential".
        /// </summary>
        [JsonPropertyName("distribution")]
        public UnivariateDistributionType Distribution { get; set; } = UnivariateDistributionType.LogPearsonTypeIII;

        /// <summary>
        /// The uncertainty quantification method: "multivariateNormal", "linkedMultivariateNormal",
        /// "bootstrap", or "biasCorrectedBootstrap". Leave null for the model default
        /// (linkedMultivariateNormal).
        /// </summary>
        [JsonPropertyName("uncertaintyMethod")]
        public UncertaintyMethod? UncertaintyMethod { get; set; }

        /// <summary>
        /// Optional annual exceedance probabilities (AEP) at which the frequency curve is
        /// evaluated. Each value must be strictly between 0 and 1. Leave null for the 25 defaults.
        /// </summary>
        [JsonPropertyName("probabilityOrdinates")]
        public List<double>? ProbabilityOrdinates { get; set; }

        /// <summary>
        /// Optional Gaussian penalties on distribution parameters, matched by parameter name —
        /// the Bulletin 17C mechanism for regional information. The canonical use is a regional
        /// skew: penalize "Skew (of log)" toward the regional value with its mean squared error.
        /// </summary>
        [JsonPropertyName("parameterPenalties")]
        public List<ParameterPenaltyDto>? ParameterPenalties { get; set; }

        /// <summary>
        /// Optional Gaussian penalties on quantiles at chosen exceedance probabilities (e.g., a
        /// paleoflood-informed 500-year discharge), compared in log10 space by default.
        /// </summary>
        [JsonPropertyName("quantilePenalties")]
        public List<QuantilePenaltyDto>? QuantilePenalties { get; set; }

        /// <summary>
        /// Optional display name for the analysis. Defaults to "Bulletin 17C analysis of {input data name}".
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
