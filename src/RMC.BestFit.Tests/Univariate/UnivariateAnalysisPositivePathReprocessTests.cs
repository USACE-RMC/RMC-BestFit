using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.UnivariateAnalyses;

/// <summary>
/// Positive-path tests for the reprocess-don't-clear contract on
/// <see cref="UnivariateAnalysis"/>. These tests inject a synthetic <see cref="MCMCResults"/>
/// via <see cref="BayesianAnalysis.SetCustomMCMCResults"/> to flip the analysis into the
/// estimated state without running an actual MCMC chain (per CLAUDE.md: MCMC-running tests
/// live in the Verification project). They then exercise the actual reprocess code path
/// — the half of the contract that the negative-path preservation tests cannot reach.
/// </summary>
/// <remarks>
/// <para>
/// Without these tests, the contract "estimated analysis preserves <c>Results</c> reference
/// across post-processing property changes" is gated only by the
/// <c>if (!IsEstimated) return;</c> early-return in <c>ReprocessIfEstimated</c>. A regression
/// that re-introduces <c>ClearResults()</c> in the estimated branch would still leave the
/// fresh-analysis tests passing.
/// </para>
/// </remarks>
[TestClass]
public class UnivariateAnalysisPositivePathReprocessTests
{
    private static DataFrame CreateExactDataFrame()
    {
        var values = new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };
        var df = new DataFrame();
        for (int i = 0; i < values.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        return df;
    }

    /// <summary>
    /// Builds synthetic posterior parameter sets for a Normal(mu, sigma) distribution.
    /// Samples are drawn around the input mean/std with deterministic spread so the tests
    /// are reproducible. The MAP point is the data's MLE estimate.
    /// </summary>
    private static MCMCResults BuildSyntheticResults(double dataMean, double dataStd, int sampleSize = 1000)
    {
        var rng = new Random(2026);
        var output = new List<ParameterSet>(sampleSize);
        for (int i = 0; i < sampleSize; i++)
        {
            // Normal posterior: mu jitters around the sample mean, sigma is positive
            double muSample = dataMean + 0.1 * dataStd * (rng.NextDouble() * 2 - 1);
            double sigmaSample = dataStd * (0.9 + 0.2 * rng.NextDouble()); // [0.9, 1.1] * dataStd
            output.Add(new ParameterSet(new[] { muSample, sigmaSample }, 0));
        }
        var map = new ParameterSet(new[] { dataMean, dataStd }, 0);
        return new MCMCResults(map, output, alpha: 0.10);
    }

    /// <summary>
    /// Constructs a fresh UnivariateAnalysis (Normal) and injects synthetic MCMC results
    /// so it appears estimated without actually running a chain.
    /// </summary>
    private static UnivariateAnalysis CreateInjectedAnalysis()
    {
        const int SampleSize = 1000;
        var df = CreateExactDataFrame();
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var values = df.ExactSeries.Select(d => d.Value).ToArray();
        double mean = values.Average();
        double std = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Average());

        // Initialize the model's parameters before injection so subsequent reprocess
        // can clone the distribution shape.
        dist.SetParameterValues(new[] { mean, std });

        var analysis = new UnivariateAnalysis(dist);
        // CreateFrequencyAnalysisResultsAsync reads BayesianAnalysis.OutputLength to size
        // its Parallel.For; must match the synthetic Output count to avoid index errors.
        // Set BEFORE injection — the OutputLength setter calls ClearResults().
        analysis.BayesianAnalysis.OutputLength = SampleSize;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildSyntheticResults(mean, std, SampleSize),
            skipInformationCriteria: true);

        // The protected AnalysisBase._isEstimated backing field is set inside RunAsync()
        // when MCMC completes successfully. For an injection-only test we set it via
        // reflection — the alternative is running an actual chain (slower, requires
        // BayesianAnalysis.RunAsync setup). RunAsync also calls
        // CreateFrequencyAnalysisResultsAsync and CreateChronologyResultsAsync once at
        // the end; we simulate that final step by leaving AnalysisResults null and
        // letting the test trigger reprocess via property changes.
        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        isEstField!.SetValue(analysis, true);
        return analysis;
    }

    /// <summary>
    /// Polls a predicate on a 5-second timeout. Used to wait for the fire-and-forget
    /// reprocess to publish AnalysisResults.
    /// </summary>
    private static async Task<bool> WaitFor(Func<bool> condition, int timeoutMs = 15000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return false;
    }

    /// <summary>Verifies that probability ordinates change preserves results reference for estimated analysis.</summary>
    [TestMethod]
    public async Task ProbabilityOrdinatesChange_EstimatedAnalysis_PreservesResultsReference()
    {
        var analysis = CreateInjectedAnalysis();
        Assert.IsTrue(analysis.BayesianAnalysis.IsEstimated, "Pre-condition: analysis must be estimated.");
        var resultsBefore = analysis.BayesianAnalysis.Results;
        Assert.IsNotNull(resultsBefore);

        // Drive the reprocess synchronously so any exception surfaces in the test.
        // The setter pathway (analysis.ProbabilityOrdinates.Add) goes through
        // ReprocessIfEstimated → fire-and-forget; we directly await the same method
        // to pin down failures and avoid the polling timeout race.
        analysis.ProbabilityOrdinates.Add(0.001);
        await analysis.CreateFrequencyAnalysisResultsAsync();

        Assert.IsNotNull(analysis.AnalysisResults, "AnalysisResults must be populated by reprocess.");
        Assert.AreSame(resultsBefore, analysis.BayesianAnalysis.Results,
            "MCMC Results reference must be preserved across an ordinate change — the chain itself is unchanged.");
        Assert.IsTrue(analysis.BayesianAnalysis.IsEstimated,
            "BayesianAnalysis.IsEstimated must remain true.");
    }

    /// <summary>Verifies that credible interval width change preserves results reference for estimated analysis.</summary>
    [TestMethod]
    public async Task CredibleIntervalWidthChange_EstimatedAnalysis_PreservesResultsReference()
    {
        var analysis = CreateInjectedAnalysis();
        var resultsBefore = analysis.BayesianAnalysis.Results;
        Assert.IsNotNull(resultsBefore);

        // Build initial AnalysisResults at 90% by directly awaiting the reprocess.
        await analysis.CreateFrequencyAnalysisResultsAsync();
        var analysisResultsAt90 = analysis.AnalysisResults;
        Assert.IsNotNull(analysisResultsAt90);

        // CIWidth change triggers two things synchronously+asynchronously:
        //  1. Results.RecomputeParameterResults(1 - 0.95) — synchronous, in-place.
        //  2. Parent's BayesianAnalysis_PropertyChanged fires CIWidth branch →
        //     ReprocessIfEstimated → fire-and-forget CreateFrequencyAnalysisResultsAsync.
        // We test (1) by checking Results reference unchanged, and (2) by awaiting the
        // reprocess directly to avoid timing flakiness.
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95;
        await analysis.CreateFrequencyAnalysisResultsAsync();

        Assert.IsNotNull(analysis.AnalysisResults);
        Assert.AreSame(resultsBefore, analysis.BayesianAnalysis.Results,
            "MCMC Results reference must be preserved across a CredibleIntervalWidth change.");
        Assert.IsTrue(analysis.BayesianAnalysis.IsEstimated,
            "BayesianAnalysis.IsEstimated must remain true.");
    }

    /// <summary>Verifies that credible interval width change preserves parameter diagnostics for .</summary>
    [TestMethod]
    public void CredibleIntervalWidthChange_PreservesParameterDiagnostics()
    {
        var analysis = CreateInjectedAnalysis();

        // Seed alpha-independent diagnostics so we can verify they survive.
        for (int i = 0; i < analysis.BayesianAnalysis.Results!.ParameterResults.Length; i++)
        {
            analysis.BayesianAnalysis.Results.ParameterResults[i].SummaryStatistics.Rhat = 1.005 + 0.001 * i;
            analysis.BayesianAnalysis.Results.ParameterResults[i].SummaryStatistics.ESS = 950.0 - 5.0 * i;
            analysis.BayesianAnalysis.Results.ParameterResults[i].Autocorrelation = new double[1, 3] { { 1.0, 0.4, 0.16 } };
        }
        var rhatsBefore = analysis.BayesianAnalysis.Results.ParameterResults.Select(p => p.SummaryStatistics.Rhat).ToArray();
        var esssBefore = analysis.BayesianAnalysis.Results.ParameterResults.Select(p => p.SummaryStatistics.ESS).ToArray();

        // Trigger CIWidth change — RecomputeParameterResults runs synchronously inside the setter.
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95;

        for (int i = 0; i < analysis.BayesianAnalysis.Results.ParameterResults.Length; i++)
        {
            Assert.AreEqual(rhatsBefore[i], analysis.BayesianAnalysis.Results.ParameterResults[i].SummaryStatistics.Rhat, 1e-12,
                $"Parameter {i}: Rhat must survive CIWidth change.");
            Assert.AreEqual(esssBefore[i], analysis.BayesianAnalysis.Results.ParameterResults[i].SummaryStatistics.ESS, 1e-12,
                $"Parameter {i}: ESS must survive CIWidth change.");
        }
    }

    /// <summary>Verifies that credible interval width change widens CI percentiles.</summary>
    [TestMethod]
    public void CredibleIntervalWidthChange_WidensCIPercentiles()
    {
        var analysis = CreateInjectedAnalysis();
        var lower90 = analysis.BayesianAnalysis.Results!.ParameterResults
            .Select(p => p.SummaryStatistics.LowerCI).ToArray();
        var upper90 = analysis.BayesianAnalysis.Results.ParameterResults
            .Select(p => p.SummaryStatistics.UpperCI).ToArray();

        // 90% → 95% widens the band.
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95;

        for (int i = 0; i < analysis.BayesianAnalysis.Results.ParameterResults.Length; i++)
        {
            double lower95 = analysis.BayesianAnalysis.Results.ParameterResults[i].SummaryStatistics.LowerCI;
            double upper95 = analysis.BayesianAnalysis.Results.ParameterResults[i].SummaryStatistics.UpperCI;
            Assert.IsTrue(lower95 < lower90[i],
                $"Parameter {i}: 95% LowerCI ({lower95}) must be < 90% LowerCI ({lower90[i]}) — band widens.");
            Assert.IsTrue(upper95 > upper90[i],
                $"Parameter {i}: 95% UpperCI ({upper95}) must be > 90% UpperCI ({upper90[i]}) — band widens.");
        }
    }
}
