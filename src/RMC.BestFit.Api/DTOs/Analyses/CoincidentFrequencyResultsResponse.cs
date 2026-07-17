using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying coincident frequency results: the annual exceedance probability of
    /// the response magnitude at each output bin, with credible intervals.
    /// </summary>
    public class CoincidentFrequencyResultsResponse : ResponseBase
    {
        /// <summary>
        /// The id of the analysis the results belong to.
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid AnalysisId { get; set; }

        /// <summary>
        /// The analysis kind ("coincidentFrequency").
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// The number of response-magnitude output bins.
        /// </summary>
        [JsonPropertyName("numberOfBins")]
        public int NumberOfBins { get; set; }

        /// <summary>
        /// The response magnitudes (Z) of the output bins.
        /// </summary>
        [JsonPropertyName("zValues")]
        public List<double> ZValues { get; set; } = new();

        /// <summary>
        /// The annual exceedance probability of each bin at the point estimate.
        /// </summary>
        [JsonPropertyName("aepMode")]
        public List<double>? AepMode { get; set; }

        /// <summary>
        /// The posterior-mean annual exceedance probability of each bin.
        /// </summary>
        [JsonPropertyName("aepMean")]
        public List<double>? AepMean { get; set; }

        /// <summary>
        /// The lower credible bound of the annual exceedance probability, per bin.
        /// </summary>
        [JsonPropertyName("ciLower")]
        public List<double>? CiLower { get; set; }

        /// <summary>
        /// The upper credible bound of the annual exceedance probability, per bin.
        /// </summary>
        [JsonPropertyName("ciUpper")]
        public List<double>? CiUpper { get; set; }

        /// <summary>
        /// The credible interval width the bounds correspond to (e.g., 0.90).
        /// </summary>
        [JsonPropertyName("credibleIntervalWidth")]
        public double CredibleIntervalWidth { get; set; }

        /// <summary>
        /// True when the marginal-X posterior chain contributed to the uncertainty bands (the
        /// marginal is a plain univariate analysis with an MCMC posterior); false when only the
        /// copula chain and point-estimate marginals were used.
        /// </summary>
        [JsonPropertyName("marginalXChainUsed")]
        public bool MarginalXChainUsed { get; set; }

        /// <summary>
        /// True when the marginal-Y posterior chain contributed to the uncertainty bands.
        /// </summary>
        [JsonPropertyName("marginalYChainUsed")]
        public bool MarginalYChainUsed { get; set; }
    }
}
