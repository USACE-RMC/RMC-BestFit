using System.Linq;
using System.Reflection;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.App.Tests.GUI.Support;

/// <summary>Creates deterministic stored estimation artifacts for App contract tests.</summary>
internal static class BayesianStoredResults
{
    /// <summary>Creates an ensemble whose posterior mean differs from the stored MAP.</summary>
    /// <param name="map">The stored MAP parameter vector.</param>
    /// <param name="mean">The identical parameter vector for each posterior draw.</param>
    /// <param name="count">The number of stored draws.</param>
    /// <returns>The stored posterior artifacts.</returns>
    /// <remarks>No optimizer, random generator, or MCMC sampler is invoked.</remarks>
    internal static MCMCResults Build(double[] map, double[] mean, int count = 100)
        => new MCMCResults(new ParameterSet((double[])map.Clone(), 0d),
            Enumerable.Range(0, count).Select(_ => new ParameterSet((double[])mean.Clone(), 0d)).ToList(), alpha: 0.10);

    /// <summary>Restores the analysis-level estimated flag normally set at the end of estimation.</summary>
    /// <param name="analysis">The analysis containing injected results.</param>
    /// <remarks>Reflection is confined to test setup and does not expand production APIs.</remarks>
    internal static void MarkEstimated(AnalysisBase analysis)
        => typeof(AnalysisBase).GetField("_isEstimated", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(analysis, true);
}
