using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Applies client-supplied Bayesian MCMC options onto a model-layer
    /// <see cref="BayesianAnalysis"/>. Only non-null fields are assigned, so omitted fields keep
    /// the model defaults; supplying any simulation-shaping field disables the model's automatic
    /// simulation defaults (which would otherwise overwrite the assignments at run time).
    /// </summary>
    public static class BayesianOptionsMapper
    {
        /// <summary>
        /// Applies the options onto the Bayesian analysis.
        /// </summary>
        /// <param name="bayesianAnalysis">The model-layer Bayesian analysis to configure.</param>
        /// <param name="options">The client-supplied options; null applies nothing.</param>
        /// <param name="maxIterations">The server's iteration cap; requests above it are rejected.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bayesianAnalysis"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iterations exceed the server cap or warm-up is not below iterations.</exception>
        public static void Apply(BayesianAnalysis bayesianAnalysis, BayesianOptionsDto? options, int maxIterations)
        {
            ArgumentNullException.ThrowIfNull(bayesianAnalysis);
            if (options == null) return;

            if (options.Iterations.HasValue && options.Iterations.Value > maxIterations)
            {
                throw new ArgumentException(
                    $"bayesianOptions.iterations ({options.Iterations.Value}) exceeds the server cap of {maxIterations}. " +
                    "Run endpoints are synchronous; see GET api/metadata/defaults for the configured limits.");
            }
            if (options.Iterations.HasValue && options.WarmupIterations.HasValue &&
                options.WarmupIterations.Value >= options.Iterations.Value)
            {
                throw new ArgumentException("bayesianOptions.warmupIterations must be less than bayesianOptions.iterations.");
            }

            // Supplying any simulation-shaping field takes the run off the model's automatic
            // defaults; otherwise the model would overwrite these assignments when the run starts.
            bool shapesSimulation = options.Sampler.HasValue || options.Iterations.HasValue ||
                options.WarmupIterations.HasValue || options.ThinningInterval.HasValue ||
                options.NumberOfChains.HasValue || options.OutputLength.HasValue;
            if (shapesSimulation)
            {
                bayesianAnalysis.UseSimulationDefaults = false;
            }

            if (options.Sampler.HasValue) bayesianAnalysis.Type = options.Sampler.Value;
            if (options.Iterations.HasValue) bayesianAnalysis.Iterations = options.Iterations.Value;
            if (options.WarmupIterations.HasValue) bayesianAnalysis.WarmupIterations = options.WarmupIterations.Value;
            if (options.ThinningInterval.HasValue) bayesianAnalysis.ThinningInterval = options.ThinningInterval.Value;
            if (options.NumberOfChains.HasValue) bayesianAnalysis.NumberOfChains = options.NumberOfChains.Value;
            if (options.PrngSeed.HasValue) bayesianAnalysis.PRNGSeed = options.PrngSeed.Value;
            if (options.CredibleIntervalWidth.HasValue) bayesianAnalysis.CredibleIntervalWidth = options.CredibleIntervalWidth.Value;
            if (options.OutputLength.HasValue) bayesianAnalysis.OutputLength = options.OutputLength.Value;
            if (options.PointEstimator.HasValue) bayesianAnalysis.PointEstimator = options.PointEstimator.Value;
        }
    }
}
