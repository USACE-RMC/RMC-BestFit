using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Run diagnostics: sampler configuration, acceptance rates, convergence warnings, and timing.
    /// </summary>
    public class DiagnosticsDto
    {
        /// <summary>
        /// The MCMC sampler used ("demCzs", ...); null for non-chain methods (Bulletin 17C).
        /// </summary>
        [JsonPropertyName("sampler")]
        public string? Sampler { get; set; }

        /// <summary>
        /// Total MCMC iterations per chain; null for non-chain methods.
        /// </summary>
        [JsonPropertyName("iterations")]
        public int? Iterations { get; set; }

        /// <summary>
        /// Warm-up iterations discarded per chain; null for non-chain methods.
        /// </summary>
        [JsonPropertyName("warmupIterations")]
        public int? WarmupIterations { get; set; }

        /// <summary>
        /// Number of parallel chains; null for non-chain methods.
        /// </summary>
        [JsonPropertyName("numberOfChains")]
        public int? NumberOfChains { get; set; }

        /// <summary>
        /// Per-chain acceptance rates; null for non-chain methods.
        /// </summary>
        [JsonPropertyName("acceptanceRates")]
        public List<double>? AcceptanceRates { get; set; }

        /// <summary>
        /// Convergence warnings (high R-hat, low effective sample size). An empty list means no
        /// convergence concerns were detected; warnings never fail the run.
        /// </summary>
        [JsonPropertyName("convergenceWarnings")]
        public List<string> ConvergenceWarnings { get; set; } = new();

        /// <summary>
        /// Wall-clock estimation time in milliseconds.
        /// </summary>
        [JsonPropertyName("elapsedMs")]
        public long? ElapsedMs { get; set; }
    }
}
