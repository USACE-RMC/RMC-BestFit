using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying the frequency analysis results of a frequency-kind analysis
    /// (univariate, Bulletin 17C, mixture, point process, competing risks, or composite): the
    /// frequency curve with uncertainty, the fitted distribution, parameter summaries,
    /// information criteria, and run diagnostics.
    /// </summary>
    public class FrequencyResultsResponse : ResponseBase
    {
        /// <summary>
        /// The id of the analysis the results belong to.
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid AnalysisId { get; set; }

        /// <summary>
        /// The analysis kind ("univariate" or "bulletin17C").
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// The fitted distribution and its point-estimate parameter values.
        /// </summary>
        [JsonPropertyName("fittedDistribution")]
        public FittedDistributionDto? FittedDistribution { get; set; }

        /// <summary>
        /// The frequency curve with credible intervals, aligned to the configured exceedance
        /// probabilities.
        /// </summary>
        [JsonPropertyName("frequencyCurve")]
        public FrequencyCurveDto? FrequencyCurve { get; set; }

        /// <summary>
        /// Posterior (or sampled) summaries per parameter, including convergence diagnostics
        /// for MCMC fits.
        /// </summary>
        [JsonPropertyName("parameterSummaries")]
        public List<ParameterSummaryDto> ParameterSummaries { get; set; } = new();

        /// <summary>
        /// Model-fit information criteria.
        /// </summary>
        [JsonPropertyName("informationCriteria")]
        public InformationCriteriaDto? InformationCriteria { get; set; }

        /// <summary>
        /// Run diagnostics (sampler settings, acceptance rates, convergence warnings, timing).
        /// </summary>
        [JsonPropertyName("diagnostics")]
        public DiagnosticsDto? Diagnostics { get; set; }

        /// <summary>
        /// Bulletin 17C-specific information; null for other kinds.
        /// </summary>
        [JsonPropertyName("bulletin17C")]
        public Bulletin17CInfoDto? Bulletin17C { get; set; }

        /// <summary>
        /// Composite-specific information (composition settings and per-component weights);
        /// null for other kinds.
        /// </summary>
        [JsonPropertyName("composite")]
        public CompositeInfoDto? Composite { get; set; }
    }
}
