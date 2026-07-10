using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying bivariate copula results: the fitted copula with posterior
    /// summaries, the two marginal identities, and the joint-exceedance curve over the
    /// configured (x, y) grid.
    /// </summary>
    public class BivariateResultsResponse : ResponseBase
    {
        /// <summary>
        /// The id of the analysis the results belong to.
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid AnalysisId { get; set; }

        /// <summary>
        /// The analysis kind ("bivariate").
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// The copula family ("normal", "gumbel", ...).
        /// </summary>
        [JsonPropertyName("copulaType")]
        public string? CopulaType { get; set; }

        /// <summary>
        /// The copula estimation method ("inferenceFromMargins" or "pseudoLikelihood").
        /// </summary>
        [JsonPropertyName("estimationMethod")]
        public string? EstimationMethod { get; set; }

        /// <summary>
        /// The marginal-X analysis identity.
        /// </summary>
        [JsonPropertyName("marginalX")]
        public MarginalInfoDto? MarginalX { get; set; }

        /// <summary>
        /// The marginal-Y analysis identity.
        /// </summary>
        [JsonPropertyName("marginalY")]
        public MarginalInfoDto? MarginalY { get; set; }

        /// <summary>
        /// The copula parameter point estimates.
        /// </summary>
        [JsonPropertyName("parameters")]
        public List<ParameterValueDto> Parameters { get; set; } = new();

        /// <summary>
        /// Posterior summaries per copula parameter, including convergence diagnostics.
        /// </summary>
        [JsonPropertyName("parameterSummaries")]
        public List<ParameterSummaryDto> ParameterSummaries { get; set; } = new();

        /// <summary>
        /// The joint-exceedance curve over the configured (x, y) grid.
        /// </summary>
        [JsonPropertyName("jointExceedance")]
        public JointExceedanceCurveDto? JointExceedance { get; set; }

        /// <summary>
        /// Model-fit information criteria for the copula fit.
        /// </summary>
        [JsonPropertyName("informationCriteria")]
        public InformationCriteriaDto? InformationCriteria { get; set; }

        /// <summary>
        /// Run diagnostics (sampler settings, acceptance rates, convergence warnings, timing).
        /// </summary>
        [JsonPropertyName("diagnostics")]
        public DiagnosticsDto? Diagnostics { get; set; }
    }

    /// <summary>
    /// The identity of one marginal analysis within a bivariate results response. Kept in this
    /// file as a minor supporting DTO tightly coupled to <see cref="BivariateResultsResponse"/>.
    /// </summary>
    public class MarginalInfoDto
    {
        /// <summary>
        /// The marginal analysis resource id (provenance — the resource may since have been deleted).
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid? AnalysisId { get; set; }

        /// <summary>
        /// The marginal analysis display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The marginal analysis kind ("univariate", "bulletin17C", "mixture", "pointProcess").
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// The marginal's fitted distribution type, when a single parent distribution applies.
        /// </summary>
        [JsonPropertyName("distributionType")]
        public string? DistributionType { get; set; }
    }

    /// <summary>
    /// The joint-exceedance curve of a bivariate results response: for each grid point,
    /// P(X &gt; x AND Y &gt; y) with credible intervals. Kept in this file as a minor supporting
    /// DTO tightly coupled to <see cref="BivariateResultsResponse"/>.
    /// </summary>
    public class JointExceedanceCurveDto
    {
        /// <summary>
        /// The marginal-X magnitudes of the grid points.
        /// </summary>
        [JsonPropertyName("x")]
        public List<double> X { get; set; } = new();

        /// <summary>
        /// The marginal-Y magnitudes of the grid points.
        /// </summary>
        [JsonPropertyName("y")]
        public List<double> Y { get; set; } = new();

        /// <summary>
        /// The joint exceedance probability at the point estimate, per grid point.
        /// </summary>
        [JsonPropertyName("modeProbabilities")]
        public List<double>? ModeProbabilities { get; set; }

        /// <summary>
        /// The posterior-mean joint exceedance probability, per grid point.
        /// </summary>
        [JsonPropertyName("meanProbabilities")]
        public List<double>? MeanProbabilities { get; set; }

        /// <summary>
        /// The lower credible bound of the joint exceedance probability, per grid point.
        /// </summary>
        [JsonPropertyName("ciLower")]
        public List<double>? CiLower { get; set; }

        /// <summary>
        /// The upper credible bound of the joint exceedance probability, per grid point.
        /// </summary>
        [JsonPropertyName("ciUpper")]
        public List<double>? CiUpper { get; set; }

        /// <summary>
        /// The credible interval width the bounds correspond to (e.g., 0.90).
        /// </summary>
        [JsonPropertyName("credibleIntervalWidth")]
        public double CredibleIntervalWidth { get; set; }
    }
}
