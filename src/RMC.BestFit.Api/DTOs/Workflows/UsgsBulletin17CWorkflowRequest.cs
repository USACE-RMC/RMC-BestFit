using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for the one-shot USGS Bulletin 17C workflow: download the annual peak-flow
    /// file, build input data, create a Bulletin 17C analysis, run it, and return the frequency
    /// results — all in one call.
    /// </summary>
    public class UsgsBulletin17CWorkflowRequest
    {
        /// <summary>
        /// The 8-digit USGS surface-water site number.
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
        /// The distribution to fit (Bulletin 17C set). Default "logPearsonTypeIII".
        /// </summary>
        [JsonPropertyName("distribution")]
        public UnivariateDistributionType Distribution { get; set; } = UnivariateDistributionType.LogPearsonTypeIII;

        /// <summary>
        /// The uncertainty method; null for the model default (linkedMultivariateNormal).
        /// </summary>
        [JsonPropertyName("uncertaintyMethod")]
        public UncertaintyMethod? UncertaintyMethod { get; set; }

        /// <summary>
        /// Optional annual exceedance probabilities for the frequency curve; null for the 25 defaults.
        /// </summary>
        [JsonPropertyName("probabilityOrdinates")]
        public List<double>? ProbabilityOrdinates { get; set; }

        /// <summary>
        /// Optional base name for the created resources.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
