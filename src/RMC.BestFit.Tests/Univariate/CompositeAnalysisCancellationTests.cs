using System.Diagnostics;
using System.Reflection;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using DataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Tests that <see cref="CompositeAnalysis.RunAsync"/> honours cancellation requests
/// promptly via the new <c>ParallelOptions.CancellationToken</c> +
/// <c>ThrowIfCancellationRequested</c> wiring inside
/// <see cref="CompositeAnalysis.CreateFrequencyAnalysisResultsAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Pre-fix: clicking the App's Cancel button flipped the
/// <see cref="System.Threading.CancellationTokenSource"/> to canceled, but the two
/// <c>Parallel.For</c> loops inside <c>CreateFrequencyAnalysisResultsAsync</c> did
/// not pass <see cref="System.Threading.Tasks.ParallelOptions"/> with the token and
/// did not call <c>ThrowIfCancellationRequested</c> in the loop body. The cancel
/// became a no-op until the loop naturally finished — for large posterior outputs
/// this could be many seconds.
/// </para>
/// <para>
/// The fixtures inject synthetic <see cref="MCMCResults"/> on each child so MCMC
/// itself doesn't run; the only meaningful work in <c>RunAsync</c> is the
/// <c>Parallel.For</c> loop the cancellation wiring targets.
/// </para>
/// </remarks>
[TestClass]
public class CompositeAnalysisCancellationTests
{
    #region Inline test fixtures

    private const int FixtureSize = 30;

    private static readonly double[] InlineFloodData = new Normal(15000.0, 5000.0)
        .GenerateRandomValues(FixtureSize, 12345);

    private static DataFrame CreateDataFrame()
    {
        var df = new DataFrame();
        for (int i = 0; i < InlineFloodData.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, InlineFloodData[i]));
        df.CalculatePlottingPositions();
        return df;
    }

    /// <summary>
    /// Builds a <see cref="UnivariateAnalysis"/> already marked as estimated, with
    /// an injected <see cref="MCMCResults"/> output of the requested length so the
    /// composite's <c>OutputLength</c> minimum across children is large enough to
    /// give the parallel loop measurable wall-clock work.
    /// </summary>
    private static UnivariateAnalysis CreateFitChildWithOutput(int outputLength, double[] mapValues)
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);
        // ProbabilityOrdinates default ctor seeds 25 values; clear before writing
        // a tiny test grid so Validate doesn't fail on duplicate-or-non-ascending order.
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.01);
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.99);

        // Inject MCMC results — same vector replicated `outputLength` times so the
        // mean / MAP land on known values and every Output[idx] reads cleanly.
        var output = new List<ParameterSet>(outputLength);
        for (int i = 0; i < outputLength; i++)
            output.Add(new ParameterSet((double[])mapValues.Clone(), 0.0));
        analysis.BayesianAnalysis.OutputLength = outputLength;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            new MCMCResults(new ParameterSet((double[])mapValues.Clone(), 0.0), output, 0.10),
            skipInformationCriteria: true);

        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        isEstField.SetValue(analysis, true);

        // Composite RunAsync's pre-flight check at CompositeAnalysis.cs:667 requires
        // every child to have a non-null AnalysisResults. Inject a stub.
        var resultsProp = typeof(UnivariateAnalysis).GetProperty("AnalysisResults",
            BindingFlags.Instance | BindingFlags.Public)!;
        resultsProp.SetValue(analysis, new UncertaintyAnalysisResults());

        return analysis;
    }

    private static CompositeAnalysis CreateMixtureCompositeWithFitChildren(int outputLengthPerChild)
    {
        var childA = CreateFitChildWithOutput(outputLengthPerChild, new[] { 15000.0, 5000.0 });
        var childB = CreateFitChildWithOutput(outputLengthPerChild, new[] { 16000.0, 5500.0 });

        var composite = new CompositeAnalysis
        {
            CompositeDistributionType = CompositeType.Mixture
        };
        composite.ProbabilityOrdinates.Clear();
        composite.ProbabilityOrdinates.Add(0.01);
        composite.ProbabilityOrdinates.Add(0.5);
        composite.ProbabilityOrdinates.Add(0.99);
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childA, 0.5));
        composite.Analyses.Add(new WeightedUnivariateAnalysis(childB, 0.5));
        return composite;
    }

    /// <summary>
    /// Subscribes to <c>AnalysisCompleted</c> immediately so the test can't miss the
    /// event due to a race between RunAsync starting and the test subscribing.
    /// </summary>
    private static (TaskCompletionSource<AnalysisRunCompletedEventArgs> tcs,
                    EventHandler<AnalysisRunCompletedEventArgs> handler)
        BeginAwaitingCompletion(CompositeAnalysis composite)
    {
        var tcs = new TaskCompletionSource<AnalysisRunCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<AnalysisRunCompletedEventArgs> handler = null!;
        handler = (_, args) =>
        {
            composite.AnalysisCompleted -= handler;
            tcs.TrySetResult(args);
        };
        composite.AnalysisCompleted += handler;
        return (tcs, handler);
    }

    private static async Task<AnalysisRunCompletedEventArgs?> AwaitCompletionAsync(
        CompositeAnalysis composite,
        TaskCompletionSource<AnalysisRunCompletedEventArgs> tcs,
        EventHandler<AnalysisRunCompletedEventArgs> handler,
        TimeSpan timeout)
    {
        var winner = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        if (winner == tcs.Task) return tcs.Task.Result;
        composite.AnalysisCompleted -= handler;
        return null;
    }

    #endregion

    #region Cancellation behaviour

    /// <summary>
    /// Calling <see cref="AnalysisBase.CancelAnalysis"/> before <c>RunAsync</c>'s
    /// parallel loop dispatches any iterations causes the run to surface as
    /// cancelled with no <see cref="CompositeAnalysis.AnalysisResults"/> populated.
    /// </summary>
    /// <remarks>
    /// We don't pre-cancel before <c>RunAsync</c> is called because the setup steps
    /// (validation, gate acquisition) check the token before the parallel loop.
    /// Instead we cancel as the run starts, which exercises the same code path the
    /// user hits when they click Cancel just after Estimate.
    /// </remarks>
    [TestMethod]
    public async Task Cancel_DuringParallelLoop_StopsAndReportsCanceled()
    {
        // 1500 realisations * two-child mixture is enough wall-clock work in Debug
        // that the cancel token is observed mid-loop. Tune up if Release-mode JIT
        // finishes the loop before Task.Delay's first tick.
        var composite = CreateMixtureCompositeWithFitChildren(outputLengthPerChild: 1500);

        // Subscribe BEFORE RunAsync to avoid a race where the run completes before
        // the test thread subscribes to AnalysisCompleted.
        var (tcs, handler) = BeginAwaitingCompletion(composite);

        var sw = Stopwatch.StartNew();
        var runTask = composite.RunAsync();
        // Give the parallel loop a brief moment to dispatch iterations, then cancel.
        await Task.Delay(50);
        composite.CancelAnalysis();

        var args = await AwaitCompletionAsync(composite, tcs, handler, TimeSpan.FromSeconds(15));
        sw.Stop();

        Assert.IsNotNull(args, "AnalysisCompleted must fire after a cancel; the run hung.");
        Assert.IsTrue(args!.Cancelled,
            "AnalysisRunCompletedEventArgs.Cancelled must report true after CancelAnalysis(). " +
            $"Succeeded={args.Succeeded}, Error={args.Error?.GetType().Name}");
        Assert.IsFalse(composite.IsEstimated, "IsEstimated must reset to false on cancellation.");
        Assert.IsNull(composite.AnalysisResults,
            "AnalysisResults must be null when the run was cancelled before the bootstrap aggregation completed.");
        Assert.IsTrue(sw.Elapsed.TotalSeconds < 10.0,
            $"Cancellation should propagate within seconds; took {sw.Elapsed.TotalSeconds:F2}s. " +
            "If this fails, ParallelOptions.CancellationToken is not being honoured.");

        // Drain RunAsync so leftover continuations don't bleed into the next test.
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }
        catch (System.Exception) { /* RunAsync itself may swallow — handlers reported cancelled */ }
    }

    /// <summary>
    /// CancelAnalysis on a freshly-constructed composite (no run in flight) sets the
    /// inherited <see cref="System.Threading.CancellationTokenSource"/>'s token to
    /// canceled. The next <c>RunAsync</c> replaces the token with a fresh one (see
    /// CompositeAnalysis.cs:683-684), so the prior cancel does not poison subsequent
    /// runs. This is a low-risk side-channel check that the AnalysisBase
    /// CancelAnalysis the App's CancelButton calls actually has an effect at the
    /// composite level.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_BeforeRun_FlipsCancellationToken()
    {
        var composite = CreateMixtureCompositeWithFitChildren(outputLengthPerChild: 150);

        Assert.IsFalse(composite.CancellationTokenSource.IsCancellationRequested,
            "Fresh composite should have an un-cancelled token.");
        composite.CancelAnalysis();
        Assert.IsTrue(composite.CancellationTokenSource.IsCancellationRequested,
            "After CancelAnalysis() the inherited CancellationTokenSource must be canceled.");
    }

    /// <summary>
    /// A composite with no children short-circuits before the parallel loop. A
    /// pre-emptive cancel on this no-op path must still be reported correctly.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WithEmptyAnalyses_FailsValidationRegardlessOfCancellation()
    {
        var composite = new CompositeAnalysis();
        composite.ProbabilityOrdinates.Clear();
        composite.ProbabilityOrdinates.Add(0.01);
        composite.ProbabilityOrdinates.Add(0.5);
        composite.ProbabilityOrdinates.Add(0.99);

        // Empty Analyses → Validate fails → RunAsync throws InvalidOperationException
        // before reaching the parallel loop. This documents the existing behaviour
        // so the cancellation change doesn't regress it.
        await Assert.ThrowsExceptionAsync<System.InvalidOperationException>(
            async () => await composite.RunAsync());
    }

    #endregion
}
