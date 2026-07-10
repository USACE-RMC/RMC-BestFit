using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Distributions.Copulas;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a Bayesian MCMC bivariate copula analysis over two FITTED
    /// marginal analyses. The copula parameters are estimated by MCMC; the marginals stay as
    /// their own analyses (live references) and must be run before this analysis runs. The two
    /// marginals' data are paired by shared time index (at least 10 overlapping non-outlier
    /// exact observations are required).
    /// </summary>
    public class CreateBivariateAnalysisRequest
    {
        /// <summary>
        /// The id of the marginal-X analysis. Valid kinds: univariate, bulletin17c, mixture,
        /// pointprocess. A LIVE reference — re-running the marginal refreshes this analysis's
        /// next run.
        /// </summary>
        [Required]
        [JsonPropertyName("marginalXAnalysisId")]
        public Guid MarginalXAnalysisId { get; set; }

        /// <summary>
        /// The id of the marginal-Y analysis (same valid kinds; must differ from marginal X).
        /// </summary>
        [Required]
        [JsonPropertyName("marginalYAnalysisId")]
        public Guid MarginalYAnalysisId { get; set; }

        /// <summary>
        /// The copula family binding the marginals: "normal" (default), "clayton", "frank",
        /// "gumbel", "joe", "aliMikhailHaq", or "studentT". See GET api/metadata/enums.
        /// </summary>
        [JsonPropertyName("copulaType")]
        public CopulaType CopulaType { get; set; } = CopulaType.Normal;

        /// <summary>
        /// How the copula sample is built: "inferenceFromMargins" (default; uses the fitted
        /// marginal CDFs) or "pseudoLikelihood" (uses empirical plotting positions).
        /// </summary>
        [JsonPropertyName("estimationMethod")]
        public CopulaEstimationMethod? EstimationMethod { get; set; }

        /// <summary>
        /// The (x, y) evaluation points of the joint-exceedance grid — the results report
        /// P(X &gt; x AND Y &gt; y) with credible intervals at each point. At least one point is
        /// required; points are sorted by x automatically.
        /// </summary>
        [Required]
        [MinLength(1)]
        [JsonPropertyName("xyOrdinates")]
        public List<XyOrdinateDto> XyOrdinates { get; set; } = new();

        /// <summary>
        /// Optional Bayesian MCMC settings for the copula estimation; omitted fields keep the
        /// model defaults.
        /// </summary>
        [JsonPropertyName("bayesianOptions")]
        public BayesianOptionsDto? BayesianOptions { get; set; }

        /// <summary>
        /// Optional informative priors on the copula parameter(s), matched by parameter name.
        /// </summary>
        [JsonPropertyName("parameterPriors")]
        public List<ParameterPriorDto>? ParameterPriors { get; set; }

        /// <summary>
        /// Optional display name for the analysis. Defaults to "Bivariate: {marginal X} / {marginal Y}".
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
