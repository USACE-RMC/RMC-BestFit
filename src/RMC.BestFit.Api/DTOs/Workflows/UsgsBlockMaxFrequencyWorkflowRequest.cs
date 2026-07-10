using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data;
using Numerics.Distributions;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for the one-shot USGS daily-flow block-maxima frequency workflow: download the
    /// daily record, extract block maxima (water-year annual maxima by default), create a
    /// univariate analysis, run it, and return the frequency results — all in one call.
    /// </summary>
    public class UsgsBlockMaxFrequencyWorkflowRequest
    {
        /// <summary>
        /// The 8-digit USGS surface-water site number.
        /// </summary>
        [Required]
        [JsonPropertyName("siteNumber")]
        public string SiteNumber { get; set; } = string.Empty;

        /// <summary>
        /// The daily series to download: "dailyDischarge" (default) or "dailyStage".
        /// </summary>
        [JsonPropertyName("seriesType")]
        public TimeSeriesDownload.TimeSeriesType SeriesType { get; set; } = TimeSeriesDownload.TimeSeriesType.DailyDischarge;

        /// <summary>
        /// The block window. Default "waterYear".
        /// </summary>
        [JsonPropertyName("timeBlock")]
        public TimeBlockWindow TimeBlock { get; set; } = TimeBlockWindow.WaterYear;

        /// <summary>
        /// The function computed over each block. Default "maximum".
        /// </summary>
        [JsonPropertyName("blockFunction")]
        public BlockFunctionType BlockFunction { get; set; } = BlockFunctionType.Maximum;

        /// <summary>
        /// Optional smoothing applied before extraction (e.g., "movingAverage" with a period for
        /// n-day flows). Default "none".
        /// </summary>
        [JsonPropertyName("smoothingFunction")]
        public SmoothingFunctionType SmoothingFunction { get; set; } = SmoothingFunctionType.None;

        /// <summary>
        /// The starting month of the water/custom year window (1-12). Default 10.
        /// </summary>
        [Range(1, 12)]
        [JsonPropertyName("startMonth")]
        public int StartMonth { get; set; } = 10;

        /// <summary>
        /// The ending month of the custom year window (1-12); only used for "customYear". Default 9.
        /// </summary>
        [Range(1, 12)]
        [JsonPropertyName("endMonth")]
        public int EndMonth { get; set; } = 9;

        /// <summary>
        /// The smoothing period in time steps. Default 1.
        /// </summary>
        [Range(1, int.MaxValue)]
        [JsonPropertyName("period")]
        public int Period { get; set; } = 1;

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
