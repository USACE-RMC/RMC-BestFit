using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body describing the server defaults and limits that shape analysis requests.
    /// </summary>
    public class DefaultsResponse : ResponseBase
    {
        /// <summary>
        /// The default exceedance-probability ordinates (annual exceedance probabilities, AEP) at
        /// which frequency curves are evaluated when a request does not supply its own.
        /// </summary>
        [JsonPropertyName("probabilityOrdinates")]
        public List<double> ProbabilityOrdinates { get; set; } = new();

        /// <summary>
        /// The maximum number of MCMC iterations a run request may specify.
        /// </summary>
        [JsonPropertyName("maxIterations")]
        public int MaxIterations { get; set; }

        /// <summary>
        /// The maximum number of analyses the server will run concurrently; additional run
        /// requests queue until a slot frees up.
        /// </summary>
        [JsonPropertyName("maxConcurrentRuns")]
        public int MaxConcurrentRuns { get; set; }

        /// <summary>
        /// The maximum total number of resources the in-memory store will hold.
        /// </summary>
        [JsonPropertyName("maxResources")]
        public int MaxResources { get; set; }

        /// <summary>
        /// Human-readable notes on conventions (probability convention, run behavior, persistence).
        /// </summary>
        [JsonPropertyName("notes")]
        public List<string> Notes { get; set; } = new();
    }
}
