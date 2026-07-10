using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a Bayesian MCMC peaks-over-threshold point process analysis
    /// linked to an input-data resource — typically one created by
    /// POST api/inputdata/peaks-over-threshold. The point process jointly models event rate and
    /// magnitude above the threshold with a Generalized Extreme Value formulation (the
    /// distribution is fixed by the method; no distribution field).
    /// </summary>
    public class CreatePointProcessAnalysisRequest
    {
        /// <summary>
        /// The id of the input-data resource holding the independent peaks-over-threshold events.
        /// When the resource was created by the POT endpoint, its recorded threshold seeds the
        /// model automatically.
        /// </summary>
        [Required]
        [JsonPropertyName("inputDataId")]
        public Guid InputDataId { get; set; }

        /// <summary>
        /// True to model within-year seasonality of the event rate (seasonal GEV). Default false.
        /// </summary>
        [JsonPropertyName("isSeasonal")]
        public bool IsSeasonal { get; set; }

        /// <summary>
        /// The seasonal time-block window ("waterYear", "calendarYear", ...) used when
        /// isSeasonal is true. Leave null for the model default.
        /// </summary>
        [JsonPropertyName("timeBlock")]
        public TimeBlockWindow? TimeBlock { get; set; }

        /// <summary>
        /// The season start month (1-12) used when isSeasonal is true. Leave null for the model
        /// default.
        /// </summary>
        [Range(1, 12)]
        [JsonPropertyName("startMonth")]
        public int? StartMonth { get; set; }

        /// <summary>
        /// Optional explicit threshold override. Leave null to use the threshold recorded on the
        /// POT input-data resource (or the model's data-derived default).
        /// </summary>
        [JsonPropertyName("threshold")]
        public double? Threshold { get; set; }

        /// <summary>
        /// Optional explicit record span in years, used to compute the event rate λ =
        /// events / totalYears. Must be greater than 0. Leave null for the model's data-derived
        /// value.
        /// </summary>
        [JsonPropertyName("totalYears")]
        public double? TotalYears { get; set; }

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
        /// Optional informative priors on individual model parameters, matched by parameter name.
        /// Unnamed parameters keep the default flat priors.
        /// </summary>
        [JsonPropertyName("parameterPriors")]
        public List<ParameterPriorDto>? ParameterPriors { get; set; }

        /// <summary>
        /// Optional priors on quantiles of the annual-maximum distribution implied by the point
        /// process — engineering judgment about flood magnitudes.
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
        /// Optional display name for the analysis. Defaults to "Point process analysis of {input data name}".
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
