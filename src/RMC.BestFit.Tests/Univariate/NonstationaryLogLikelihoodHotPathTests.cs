using System.Diagnostics;
using System.Reflection;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;
using DataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Tests for the nonstationary <see cref="UnivariateDistribution.NonstationaryData_LogLikelihood"/>
/// hot path — both correctness (the value is unchanged after the FullTimeSeries-caching
/// optimization) and a coarse performance smoke test.
/// </summary>
/// <remarks>
/// <para>
/// Pre-fix, every iteration of the inner loop accessed
/// <see cref="DataFrame.FullTimeSeries"/> twice (once for the loop condition, once for
/// the indexer). The property getter performs a <c>Volatile.Read</c> plus a call to
/// <see cref="DataFrame.TotalRecordLength"/> on each access, producing 2N+1 getter calls
/// per MCMC iteration in addition to N array allocations for the per-time-step
/// <c>values</c> buffer. The fix caches the reference once outside the loop and reuses
/// a single <c>values</c> buffer.
/// </para>
/// </remarks>
[TestClass]
public class NonstationaryLogLikelihoodHotPathTests
{
    private const int FixtureSize = 60;
    private static readonly double[] InlineFloodData = new Normal(15000.0, 5000.0)
        .GenerateRandomValues(FixtureSize, 42);

    private static DataFrame CreateDataFrame()
    {
        var df = new DataFrame();
        for (int i = 0; i < InlineFloodData.Length; i++)
            df.ExactSeries.Add(new ExactData(1960 + i, InlineFloodData[i]));
        return df;
    }

    /// <summary>
    /// Builds a Normal NS distribution with a Linear trend on the location parameter.
    /// FullTimeSeries is pre-built so the hot path doesn't have to (matches what
    /// <c>UnivariateAnalysis.RunAsync</c> does at line 493 before invoking MCMC).
    /// </summary>
    private static UnivariateDistribution CreateNormalLinearMu()
    {
        var df = CreateDataFrame();
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        dist.IsNonstationary = true;
        dist.SetTrendModel(0, TrendModelType.Linear);  // Linear on mu
        df.CreateFullTimeSeries();
        return dist;
    }

    private static double CallNonstationaryDataLogLikelihood(UnivariateDistribution dist, double[] parameters)
    {
        // The NS data log-likelihood is public; call it via the same path BayesianAnalysis uses.
        var workingModel = dist.Distribution.Clone();
        return dist.NonstationaryData_LogLikelihood(workingModel, parameters);
    }

    /// <summary>
    /// Sanity: NS data log-likelihood at a known parameter vector is finite and
    /// matches the sum of pointwise components. The pointwise array goes through
    /// the same caching path, so this test catches any regression where caching
    /// the reference accidentally changed iteration order or values.
    /// </summary>
    [TestMethod]
    public void NonstationaryDataLogLikelihood_MatchesSumOfPointwise()
    {
        var dist = CreateNormalLinearMu();
        // [mu_intercept, mu_slope, sigma]
        double[] parameters = { 15000.0, 50.0, 5000.0 };

        double total = CallNonstationaryDataLogLikelihood(dist, parameters);
        var pointwise = dist.PointwiseDataLogLikelihood(parameters);

        Assert.IsTrue(double.IsFinite(total), "NS data log-likelihood must be finite at sane parameters.");
        Assert.AreEqual(FixtureSize, pointwise.Length,
            "Pointwise array length must match the FullTimeSeries length.");
        double sum = 0;
        for (int i = 0; i < pointwise.Length; i++) sum += pointwise[i];
        Assert.AreEqual(total, sum, 1e-6,
            "Pointwise components should sum to the total NS data log-likelihood.");
    }

    /// <summary>
    /// Repeated calls to <see cref="UnivariateDistribution.NonstationaryData_LogLikelihood"/>
    /// at the same parameters return the same value bit-for-bit. The reusable values
    /// buffer introduced by Fix B must not leak state across calls.
    /// </summary>
    [TestMethod]
    public void NonstationaryDataLogLikelihood_RepeatedCallsReturnSameValue()
    {
        var dist = CreateNormalLinearMu();
        double[] parameters = { 15000.0, 50.0, 5000.0 };

        double a = CallNonstationaryDataLogLikelihood(dist, parameters);
        double b = CallNonstationaryDataLogLikelihood(dist, parameters);
        double c = CallNonstationaryDataLogLikelihood(dist, parameters);

        Assert.AreEqual(a, b, 0.0,
            "Successive identical calls should return bit-identical results — buffer reuse must not introduce drift.");
        Assert.AreEqual(b, c, 0.0);
    }

    /// <summary>
    /// Calling the NS log-likelihood with different parameter vectors interleaved with
    /// the same-vector calls returns deterministic values for each vector. This catches
    /// the failure mode where the reused <c>values</c> buffer carries over partial state
    /// between calls with different parameter counts (which, while not currently
    /// reachable, is the hazard the buffer-reuse optimization could introduce).
    /// </summary>
    [TestMethod]
    public void NonstationaryDataLogLikelihood_DifferentParametersAreIndependent()
    {
        var dist = CreateNormalLinearMu();
        double[] paramsA = { 15000.0, 50.0, 5000.0 };
        double[] paramsB = { 14000.0, 80.0, 6000.0 };

        double a1 = CallNonstationaryDataLogLikelihood(dist, paramsA);
        double b  = CallNonstationaryDataLogLikelihood(dist, paramsB);
        double a2 = CallNonstationaryDataLogLikelihood(dist, paramsA);

        Assert.AreEqual(a1, a2, 0.0,
            "Same parameters before and after an interleaved different-parameter call must give identical results.");
        Assert.AreNotEqual(a1, b,
            "Different parameter vectors should produce different log-likelihoods.");
    }

    /// <summary>
    /// Coarse perf smoke test: 10,000 NS data log-likelihood calls on a 60-point fixture
    /// must complete in well under 10 seconds in Debug. Pre-fix the per-call overhead
    /// included 2N+1 = 121 FullTimeSeries getter calls and N = 60 array allocations,
    /// driving the per-call latency way above the post-fix target. The 10-second
    /// budget is intentionally generous so this test does NOT become flaky on slow
    /// CI runners; the goal is to catch a 50× regression, not to pin a precise number.
    /// </summary>
    [TestMethod]
    public void NonstationaryDataLogLikelihood_HotLoopSmokeTest()
    {
        var dist = CreateNormalLinearMu();
        double[] parameters = { 15000.0, 50.0, 5000.0 };
        // Warm-up — first call may JIT.
        CallNonstationaryDataLogLikelihood(dist, parameters);

        const int Iterations = 10_000;
        var sw = Stopwatch.StartNew();
        double totalSink = 0;
        for (int i = 0; i < Iterations; i++)
        {
            // Vary one parameter very slightly to simulate the proposal step of an MCMC
            // sampler (defeats any constant-folding the JIT might attempt).
            parameters[0] = 15000.0 + (i % 7);
            totalSink += CallNonstationaryDataLogLikelihood(dist, parameters);
        }
        sw.Stop();
        Assert.IsTrue(double.IsFinite(totalSink), "Sanity: result is finite.");
        Assert.IsTrue(sw.Elapsed.TotalSeconds < 10.0,
            $"NS hot loop ({Iterations} calls × {FixtureSize} points) took {sw.Elapsed.TotalSeconds:F2}s; " +
            "budget is 10s. Investigate whether DataFrame.FullTimeSeries / TotalRecordLength is being " +
            "re-evaluated inside the inner loop (Fix B regression).");
    }

    /// <summary>
    /// Pointwise NS log-likelihood (used by the predictive-checks / WAIC path) goes
    /// through the same FullTimeSeries-caching code path; verify it returns the same
    /// value as the public sum and that the array is the right length.
    /// </summary>
    [TestMethod]
    public void NonstationaryPointwise_LengthAndSum_AreCorrect()
    {
        var dist = CreateNormalLinearMu();
        double[] parameters = { 15000.0, 50.0, 5000.0 };

        var pointwise = dist.PointwiseDataLogLikelihood(parameters);
        double sum = 0;
        for (int i = 0; i < pointwise.Length; i++) sum += pointwise[i];

        var workingModel = dist.Distribution.Clone();
        double total = dist.NonstationaryData_LogLikelihood(workingModel, parameters);

        Assert.AreEqual(dist.DataFrame.FullTimeSeries.Count, pointwise.Length);
        Assert.AreEqual(total, sum, 1e-6);
    }
}
