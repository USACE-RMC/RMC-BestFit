using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Optional Bayesian MCMC settings for analysis creation. Every field is nullable: only
    /// supplied values are applied, so omitted fields keep the model library's defaults (which
    /// scale the simulation to the data). Supplying any sampler/iteration field switches the
    /// analysis off its automatic simulation defaults.
    /// </summary>
    public class BayesianOptionsDto
    {
        /// <summary>
        /// The MCMC sampler: "demCz", "demCzs" (default, differential evolution with snooker
        /// update — the recommended workhorse), "arwmh" (adaptive random walk Metropolis-Hastings),
        /// or "nuts" (No-U-Turn Sampler).
        /// </summary>
        [JsonPropertyName("sampler")]
        public BayesianAnalysis.SamplerType? Sampler { get; set; }

        /// <summary>
        /// Total MCMC iterations per chain. Leave null for data-scaled defaults. Larger values
        /// improve convergence at proportional runtime cost; capped by the server's maxIterations
        /// (see GET api/metadata/defaults).
        /// </summary>
        [Range(100, int.MaxValue)]
        [JsonPropertyName("iterations")]
        public int? Iterations { get; set; }

        /// <summary>
        /// Warm-up (burn-in) iterations discarded from the start of each chain. Typically half of
        /// iterations. Leave null for defaults.
        /// </summary>
        [Range(0, int.MaxValue)]
        [JsonPropertyName("warmupIterations")]
        public int? WarmupIterations { get; set; }

        /// <summary>
        /// Keep every n-th post-warm-up sample to reduce autocorrelation. Leave null for defaults.
        /// </summary>
        [Range(1, int.MaxValue)]
        [JsonPropertyName("thinningInterval")]
        public int? ThinningInterval { get; set; }

        /// <summary>
        /// Number of parallel MCMC chains (used for R-hat convergence diagnostics). Leave null for defaults.
        /// </summary>
        [Range(1, 8)]
        [JsonPropertyName("numberOfChains")]
        public int? NumberOfChains { get; set; }

        /// <summary>
        /// Pseudo-random number generator seed for reproducible runs. Leave null for the model default.
        /// </summary>
        [JsonPropertyName("prngSeed")]
        public int? PrngSeed { get; set; }

        /// <summary>
        /// Width of the reported credible intervals (e.g., 0.90 for 90% intervals). Leave null for
        /// the model default of 0.90.
        /// </summary>
        [Range(0.01, 0.999)]
        [JsonPropertyName("credibleIntervalWidth")]
        public double? CredibleIntervalWidth { get; set; }

        /// <summary>
        /// Number of thinned posterior samples retained for post-processing. Leave null for the
        /// model default (10,000).
        /// </summary>
        [Range(100, int.MaxValue)]
        [JsonPropertyName("outputLength")]
        public int? OutputLength { get; set; }

        /// <summary>
        /// The point estimator used for the reported curve: "posteriorMode" (MAP) or
        /// "posteriorMean". Leave null for the model default.
        /// </summary>
        [JsonPropertyName("pointEstimator")]
        public BayesianAnalysis.PointEstimateType? PointEstimator { get; set; }
    }
}
