using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body listing the accepted string values for every enum-typed request field, in the
    /// camelCase form the JSON contract expects. Agents should consult this before constructing requests.
    /// </summary>
    public class EnumOptionsResponse : ResponseBase
    {
        /// <summary>
        /// MCMC sampler types accepted by bayesianOptions.sampler.
        /// </summary>
        [JsonPropertyName("samplers")]
        public List<string> Samplers { get; set; } = new();

        /// <summary>
        /// Point estimator types accepted by bayesianOptions.pointEstimator.
        /// </summary>
        [JsonPropertyName("pointEstimators")]
        public List<string> PointEstimators { get; set; } = new();

        /// <summary>
        /// Uncertainty quantification methods accepted by Bulletin 17C create requests.
        /// </summary>
        [JsonPropertyName("uncertaintyMethods")]
        public List<string> UncertaintyMethods { get; set; } = new();

        /// <summary>
        /// Time-block windows accepted by block-maxima input-data requests.
        /// </summary>
        [JsonPropertyName("timeBlockWindows")]
        public List<string> TimeBlockWindows { get; set; } = new();

        /// <summary>
        /// Block functions accepted by block-maxima input-data requests.
        /// </summary>
        [JsonPropertyName("blockFunctions")]
        public List<string> BlockFunctions { get; set; } = new();

        /// <summary>
        /// Smoothing functions accepted by block-maxima and peaks-over-threshold requests.
        /// </summary>
        [JsonPropertyName("smoothingFunctions")]
        public List<string> SmoothingFunctions { get; set; } = new();

        /// <summary>
        /// USGS series types accepted by time-series and USGS-peaks input-data requests.
        /// </summary>
        [JsonPropertyName("usgsSeriesTypes")]
        public List<string> UsgsSeriesTypes { get; set; } = new();

        /// <summary>
        /// Recording intervals accepted by manual time-series requests.
        /// </summary>
        [JsonPropertyName("timeIntervals")]
        public List<string> TimeIntervals { get; set; } = new();

        /// <summary>
        /// The analysis kinds exposed by the API.
        /// </summary>
        [JsonPropertyName("analysisKinds")]
        public List<string> AnalysisKinds { get; set; } = new();

        /// <summary>
        /// The input-data creation methods exposed by the API.
        /// </summary>
        [JsonPropertyName("inputDataMethods")]
        public List<string> InputDataMethods { get; set; } = new();

        /// <summary>
        /// Distribution types accepted in distribution specs (uncertain-observation
        /// measurement-error distributions, parameter priors, and quantile priors) — every type
        /// the distribution factory can construct, a superset of the fitting distributions.
        /// </summary>
        [JsonPropertyName("priorDistributions")]
        public List<string> PriorDistributions { get; set; } = new();

        /// <summary>
        /// Composition methods accepted by composite-analysis create requests.
        /// </summary>
        [JsonPropertyName("compositeTypes")]
        public List<string> CompositeTypes { get; set; } = new();

        /// <summary>
        /// Model-average weighting methods accepted by composite-analysis create requests.
        /// </summary>
        [JsonPropertyName("averageMethods")]
        public List<string> AverageMethods { get; set; } = new();

        /// <summary>
        /// Competing risks dependence assumptions accepted by composite-analysis create requests.
        /// </summary>
        [JsonPropertyName("dependencyTypes")]
        public List<string> DependencyTypes { get; set; } = new();

        /// <summary>
        /// Copula families accepted by bivariate-analysis create requests.
        /// </summary>
        [JsonPropertyName("copulaTypes")]
        public List<string> CopulaTypes { get; set; } = new();

        /// <summary>
        /// Copula estimation methods accepted by bivariate-analysis create requests
        /// ("fullLikelihood" is listed for completeness but rejected by the bivariate analysis).
        /// </summary>
        [JsonPropertyName("copulaEstimationMethods")]
        public List<string> CopulaEstimationMethods { get; set; } = new();

        /// <summary>
        /// Time-series model families accepted by time-series create requests.
        /// </summary>
        [JsonPropertyName("timeSeriesModelTypes")]
        public List<string> TimeSeriesModelTypes { get; set; } = new();

        /// <summary>
        /// Variance-stabilizing transforms accepted by time-series create requests.
        /// </summary>
        [JsonPropertyName("transformTypes")]
        public List<string> TransformTypes { get; set; } = new();

        /// <summary>
        /// Deterministic trend types accepted by ARIMAX create requests.
        /// </summary>
        [JsonPropertyName("trendTypes")]
        public List<string> TrendTypes { get; set; } = new();

        /// <summary>
        /// Covariate extension methods accepted by ARIMAX create requests.
        /// </summary>
        [JsonPropertyName("covariateExtensions")]
        public List<string> CovariateExtensions { get; set; } = new();
    }
}
