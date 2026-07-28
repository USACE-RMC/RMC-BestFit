using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>
/// Long-running regression scenarios for conditional stochastic imputation in Bulletin 17C bootstrap fits.
/// </summary>
/// <remarks>
/// These tests are intentionally part of the user-run Verification project. They check finite parameter
/// delivery, fit-failure rates, and bootstrap centering for both narrow and wide uncertainty distributions.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class UncertainDataBootstrapVerificationTests
{
    private const int BootstrapReplicates = 200;

    /// <summary>
    /// Verifies a Normal B17C fit remains finite and centered when narrow and wide uncertain observations are present.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task NormalBootstrap_UncertainObservationsRemainStable()
    {
        var dataFrame = new RMC.BestFit.Models.DataFrame
        {
            ExactSeries = new ExactSeries(TestData.NormalData.Take(80).ToArray())
        };
        dataFrame.UncertainSeries.Add(new UncertainData(80, new TruncatedNormal(100.0, 2.0, 90.0, 110.0)));
        dataFrame.UncertainSeries.Add(new UncertainData(81, new TruncatedNormal(100.0, 15.0, 50.0, 150.0)));

        await VerifyBootstrapStabilityAsync(dataFrame, UnivariateDistributionType.Normal);
    }

    /// <summary>
    /// Verifies an LP3 B17C fit remains finite and centered for MOVE.3-style narrow and wide flow estimates.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogPearsonBootstrap_Move3StyleUncertaintyRemainsStable()
    {
        var dataFrame = new RMC.BestFit.Models.DataFrame
        {
            ExactSeries = new ExactSeries(TestData.LogPearsonTypeIIIData.Take(80).ToArray())
        };

        double median = dataFrame.ExactSeries.Select(x => x.Value).OrderBy(x => x).ElementAt(dataFrame.ExactSeries.Count / 2);
        dataFrame.UncertainSeries.Add(new UncertainData(80, new Triangular(0.90 * median, median, 1.10 * median)));
        dataFrame.UncertainSeries.Add(new UncertainData(81, new Triangular(0.50 * median, median, 1.75 * median)));

        await VerifyBootstrapStabilityAsync(dataFrame, UnivariateDistributionType.LogPearsonTypeIII);
    }

    /// <summary>
    /// Runs a fixed-seed bootstrap and verifies its delivered parameter sample and diagnostics.
    /// </summary>
    /// <param name="dataFrame">The data frame containing exact and uncertain observations.</param>
    /// <param name="distributionType">The B17C distribution family to fit.</param>
    /// <returns>A task representing the asynchronous analysis.</returns>
    /// <remarks>
    /// Parameter means must remain within the greater of five percent of the parent estimate or two
    /// Monte Carlo standard errors. This criterion directly guards against systematic scale or skew growth.
    /// </remarks>
    private static async Task VerifyBootstrapStabilityAsync(
        RMC.BestFit.Models.DataFrame dataFrame,
        UnivariateDistributionType distributionType)
    {
        var model = new Bulletin17CDistribution(dataFrame, distributionType);
        var analysis = new Bulletin17CAnalysis(model)
        {
            UncertaintyMethod = UncertaintyMethod.Bootstrap
        };
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        analysis.BayesianAnalysis.OutputLength = BootstrapReplicates;
        analysis.BayesianAnalysis.PRNGSeed = 24681357;

        await analysis.RunAsync();

        Assert.IsNotNull(analysis.BayesianAnalysis.Results, "Bootstrap results were not published.");
        Assert.IsNotNull(analysis.BootstrapResults, "Bootstrap diagnostics were not published.");

        var output = analysis.BayesianAnalysis.Results!.Output;
        Assert.AreEqual(BootstrapReplicates, output.Count, "Every requested parameter vector must be delivered.");
        Assert.IsTrue(output.All(set => set.Values.All(double.IsFinite)), "Every delivered parameter value must be finite.");

        BootstrapDiagnostics diagnostics = analysis.BootstrapResults!;
        Assert.IsTrue(diagnostics.FailureRate < 0.01,
            $"Bootstrap failure rate {diagnostics.FailureRate:P2} exceeded 1%.");
        double fallbackRate = diagnostics.AttemptedReplicates > 0
            ? (double)diagnostics.OptimizerFallbacks / diagnostics.AttemptedReplicates
            : 0.0;
        Assert.IsTrue(fallbackRate < 0.01,
            $"Optimizer fallback rate {fallbackRate:P2} exceeded 1%.");

        double[] parent = analysis.BayesianAnalysis.Results.MAP.Values;
        for (int parameterIndex = 0; parameterIndex < parent.Length; parameterIndex++)
        {
            double[] values = output.Select(set => set.Values[parameterIndex]).ToArray();
            double mean = values.Average();
            double sumSquared = values.Sum(value => Math.Pow(value - mean, 2.0));
            double standardDeviation = Math.Sqrt(sumSquared / (values.Length - 1));
            double monteCarloStandardError = standardDeviation / Math.Sqrt(values.Length);
            double tolerance = Math.Max(Math.Abs(parent[parameterIndex]) * 0.05, 2.0 * monteCarloStandardError);

            Assert.AreEqual(parent[parameterIndex], mean, tolerance,
                $"Parameter {parameterIndex} bootstrap mean was not centered on the parent estimate.");
        }
    }
}