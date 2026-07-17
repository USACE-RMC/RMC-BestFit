using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Distributions;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a distribution-fitting analysis: a parallel maximum-likelihood
    /// fit of many candidate distributions over one input-data resource, ranked by information
    /// criteria. This is a fast screening tool (seconds, no MCMC) for choosing distributions to
    /// carry into Bayesian analyses.
    /// </summary>
    public class CreateDistributionFittingAnalysisRequest
    {
        /// <summary>
        /// The id of the input-data resource to fit (create it first via the api/inputdata endpoints).
        /// </summary>
        [Required]
        [JsonPropertyName("inputDataId")]
        public Guid InputDataId { get; set; }

        /// <summary>
        /// Optional subset of candidate distributions to fit. Leave null to fit all 15 supported
        /// distributions; see GET api/metadata/distributions.
        /// </summary>
        [JsonPropertyName("distributions")]
        public List<UnivariateDistributionType>? Distributions { get; set; }

        /// <summary>
        /// Optional display name for the analysis. Defaults to "Distribution fitting of {input data name}".
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
