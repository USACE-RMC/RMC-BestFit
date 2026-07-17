using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data;
using Numerics.Distributions;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for the one-shot USGS peak-flow frequency workflow: download the annual
    /// peak-flow file, build input data, create a univariate analysis, run it, and return the
    /// frequency results — all in one call.
    /// </summary>
    public class UsgsPeakFrequencyWorkflowRequest
    {
        /// <summary>
        /// The 8-digit USGS surface-water site number (e.g., "01646500").
        /// </summary>
        [Required]
        [JsonPropertyName("siteNumber")]
        public string SiteNumber { get; set; } = string.Empty;

        /// <summary>
        /// The peak series to download: "peakDischarge" (default) or "peakStage".
        /// </summary>
        [JsonPropertyName("seriesType")]
        public TimeSeriesDownload.TimeSeriesType SeriesType { get; set; } = TimeSeriesDownload.TimeSeriesType.PeakDischarge;

        /// <summary>
        /// The distribution to fit. Default "logPearsonTypeIII".
        /// </summary>
        [JsonPropertyName("distribution")]
        public UnivariateDistributionType Distribution { get; set; } = UnivariateDistributionType.LogPearsonTypeIII;

        /// <summary>
        /// Optional annual exceedance probabilities for the frequency curve; null for the 25 defaults.
        /// </summary>
        [JsonPropertyName("probabilityOrdinates")]
        public List<double>? ProbabilityOrdinates { get; set; }

        /// <summary>
        /// Optional Bayesian MCMC settings; omitted fields keep the model defaults.
        /// </summary>
        [JsonPropertyName("bayesianOptions")]
        public BayesianOptionsDto? BayesianOptions { get; set; }

        /// <summary>
        /// Optional base name for the created resources.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
