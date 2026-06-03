using System.ComponentModel;
using System.Diagnostics;
using Numerics.Distributions;
using Numerics.Utilities;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.BatchAnalysis;

/// <summary>
/// Unit tests for the <see cref="BatchAnalysisRunner"/> class.
/// Uses a lightweight <see cref="MockAnalysis"/> to test batch execution
/// behavior without requiring actual MCMC sampling.
/// </summary>
[TestClass]
public class BatchAnalysisRunnerTests
{
    #region Mock Analysis

    /// <summary>
    /// A minimal <see cref="IAnalysis"/> implementation for testing.
    /// Configurable to succeed, fail, or delay to simulate real analyses.
    /// </summary>
    private class MockAnalysis : IAnalysis
    {
        /// <summary>
        /// The delay in milliseconds before the analysis completes.
        /// </summary>
        private readonly int _delayMs;

        /// <summary>
        /// If true, the analysis will throw an exception.
        /// </summary>
        private readonly bool _shouldFail;

        /// <summary>
        /// Cancellation token source for this analysis.
        /// </summary>
        private CancellationTokenSource? _cts;

        /// <summary>
        /// Creates a mock analysis with configurable behavior.
        /// </summary>
        /// <param name="delayMs">How long to simulate running (milliseconds).</param>
        /// <param name="shouldFail">If true, throws an exception during RunAsync.</param>
        public MockAnalysis(int delayMs = 10, bool shouldFail = false)
        {
            _delayMs = delayMs;
            _shouldFail = shouldFail;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event. Reserved for future use.
        /// </summary>
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <inheritdoc/>
        public event EventHandler<CancelEventArgs>? AnalysisStarting;

        /// <inheritdoc/>
        public event EventHandler<AnalysisRunCompletedEventArgs>? AnalysisCompleted;

        /// <inheritdoc/>
        public bool IsEstimated { get; private set; }

        /// <inheritdoc/>
        public async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            _cts = new CancellationTokenSource();

            var previewArgs = new CancelEventArgs();
            AnalysisStarting?.Invoke(this, previewArgs);
            if (previewArgs.Cancel)
            {
                AnalysisCompleted?.Invoke(this, new AnalysisRunCompletedEventArgs(
                    wasCanceled: true, succeeded: false, error: null));
                return;
            }

            progressReporter?.IndicateTaskStart();

            try
            {
                if (_shouldFail)
                    throw new InvalidOperationException("Mock analysis failure.");

                await Task.Delay(_delayMs, _cts.Token);

                progressReporter?.ReportProgress(100);
                IsEstimated = true;

                AnalysisCompleted?.Invoke(this, new AnalysisRunCompletedEventArgs(
                    wasCanceled: false, succeeded: true, error: null));
            }
            finally
            {
                progressReporter?.IndicateTaskEnded();
            }
        }

        /// <inheritdoc/>
        public void CancelAnalysis()
        {
            _cts?.Cancel();
        }

        /// <inheritdoc/>
        public (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }

    #endregion

    #region Single Analysis Tests

    /// <summary>
    /// Verifies that a single successful analysis returns a succeeded result.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_SingleAnalysis_Succeeds()
    {
        var runner = new BatchAnalysisRunner();
        var analysis = new MockAnalysis(delayMs: 10);

        var results = await runner.RunAsync(new List<IAnalysis> { analysis });

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].Succeeded);
        Assert.IsFalse(results[0].WasCanceled);
        Assert.IsNull(results[0].Error);
        Assert.IsTrue(results[0].Duration.TotalMilliseconds >= 0);
    }

    /// <summary>
    /// Verifies that an empty list returns an empty result list.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_EmptyList_ReturnsEmpty()
    {
        var runner = new BatchAnalysisRunner();

        var results = await runner.RunAsync(new List<IAnalysis>());

        Assert.AreEqual(0, results.Count);
    }

    /// <summary>
    /// Verifies that a null analyses list throws ArgumentNullException.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task RunAsync_NullList_ThrowsArgumentNull()
    {
        var runner = new BatchAnalysisRunner();

        await runner.RunAsync(null!);
    }

    #endregion

    #region Serial Execution Tests

    /// <summary>
    /// Verifies that with MaxDegreeOfParallelism = 1, analyses run serially.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_MultipleAnalyses_Serial()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 20),
            new MockAnalysis(delayMs: 20),
            new MockAnalysis(delayMs: 20)
        };

        var options = new BatchAnalysisOptions { MaxDegreeOfParallelism = 1 };
        var sw = Stopwatch.StartNew();
        var results = await runner.RunAsync(analyses, options);
        sw.Stop();

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.All(r => r.Succeeded));

        // Serial execution: total time should be at least sum of delays
        Assert.IsTrue(sw.ElapsedMilliseconds >= 50,
            $"Expected serial execution >= 50ms, got {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Verifies that with MaxDegreeOfParallelism > 1, analyses run concurrently.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_MultipleAnalyses_Parallel()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 100),
            new MockAnalysis(delayMs: 100),
            new MockAnalysis(delayMs: 100)
        };

        var options = new BatchAnalysisOptions { MaxDegreeOfParallelism = 3 };
        var sw = Stopwatch.StartNew();
        var results = await runner.RunAsync(analyses, options);
        sw.Stop();

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.All(r => r.Succeeded));

        // Parallel execution: total time should be significantly less than serial (300ms)
        Assert.IsTrue(sw.ElapsedMilliseconds < 250,
            $"Expected parallel execution < 250ms, got {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Verifies that with ContinueOnError = true, remaining analyses run after a failure.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_ContinueOnError_True()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 10, shouldFail: true),
            new MockAnalysis(delayMs: 10)
        };

        var options = new BatchAnalysisOptions { ContinueOnError = true };
        var results = await runner.RunAsync(analyses, options);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].Succeeded);
        Assert.IsFalse(results[1].Succeeded);
        Assert.IsNotNull(results[1].Error);
        Assert.IsTrue(results[2].Succeeded);
    }

    /// <summary>
    /// Verifies that with ContinueOnError = false, the batch stops after the first failure.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_ContinueOnError_False()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 10, shouldFail: true),
            new MockAnalysis(delayMs: 10)
        };

        var options = new BatchAnalysisOptions { ContinueOnError = false };
        var results = await runner.RunAsync(analyses, options);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].Succeeded);
        Assert.IsFalse(results[1].Succeeded);
        Assert.IsNotNull(results[1].Error);
        // Third analysis should be canceled (not started)
        Assert.IsFalse(results[2].Succeeded);
        Assert.IsTrue(results[2].WasCanceled);
    }

    /// <summary>
    /// Verifies that a failed analysis records the correct exception.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_FailedAnalysis_RecordsException()
    {
        var runner = new BatchAnalysisRunner();
        var analysis = new MockAnalysis(shouldFail: true);

        var results = await runner.RunAsync(new List<IAnalysis> { analysis });

        Assert.AreEqual(1, results.Count);
        Assert.IsFalse(results[0].Succeeded);
        Assert.IsFalse(results[0].WasCanceled);
        Assert.IsNotNull(results[0].Error);
        Assert.IsInstanceOfType(results[0].Error, typeof(InvalidOperationException));
    }

    #endregion

    #region Cancellation Tests

    /// <summary>
    /// Verifies that cancellation via CancellationToken stops remaining analyses.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_Cancellation_StopsRemaining()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 500),
            new MockAnalysis(delayMs: 10)
        };

        using var cts = new CancellationTokenSource();

        // Cancel after the first analysis completes
        runner.AnalysisCompleted += (s, r) =>
        {
            if (r.Succeeded)
                cts.Cancel();
        };

        var options = new BatchAnalysisOptions { MaxDegreeOfParallelism = 1 };
        var results = await runner.RunAsync(analyses, options, cancellationToken: cts.Token);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].Succeeded);
        // Remaining analyses should be canceled
        Assert.IsTrue(results.Skip(1).All(r => r.WasCanceled));
    }

    /// <summary>
    /// Verifies that the Cancel() method stops the batch.
    /// </summary>
    [TestMethod]
    public async Task Cancel_StopsBatch()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 500),
            new MockAnalysis(delayMs: 10)
        };

        // Cancel after first analysis
        runner.AnalysisCompleted += (s, r) =>
        {
            if (r.Succeeded)
                runner.Cancel();
        };

        var results = await runner.RunAsync(analyses);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].Succeeded);
    }

    #endregion

    #region Event Tests

    /// <summary>
    /// Verifies that AnalysisCompleted fires once for each analysis.
    /// </summary>
    [TestMethod]
    public async Task Events_AnalysisCompleted_FiredPerItem()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 10)
        };

        int completedCount = 0;
        runner.AnalysisCompleted += (s, r) => Interlocked.Increment(ref completedCount);

        await runner.RunAsync(analyses);

        Assert.AreEqual(3, completedCount);
    }

    /// <summary>
    /// Verifies that BatchCompleted fires once with the full results list.
    /// </summary>
    [TestMethod]
    public async Task Events_BatchCompleted_FiredOnce()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 10)
        };

        int batchCompletedCount = 0;
        List<BatchAnalysisResult>? batchResults = null;
        runner.BatchCompleted += (s, r) =>
        {
            Interlocked.Increment(ref batchCompletedCount);
            batchResults = r;
        };

        await runner.RunAsync(analyses);

        Assert.AreEqual(1, batchCompletedCount);
        Assert.IsNotNull(batchResults);
        Assert.AreEqual(2, batchResults.Count);
    }

    /// <summary>
    /// Verifies that ProgressChanged reports correct (completed, total) values.
    /// </summary>
    [TestMethod]
    public async Task Events_ProgressChanged_ReportsCorrectly()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 10)
        };

        var progressHistory = new List<(int Completed, int Total)>();
        runner.ProgressChanged += (s, p) => progressHistory.Add(p);

        var options = new BatchAnalysisOptions { MaxDegreeOfParallelism = 1 };
        await runner.RunAsync(analyses, options);

        Assert.AreEqual(3, progressHistory.Count);
        Assert.AreEqual((1, 3), progressHistory[0]);
        Assert.AreEqual((2, 3), progressHistory[1]);
        Assert.AreEqual((3, 3), progressHistory[2]);
    }

    /// <summary>
    /// Verifies that AnalysisStarting fires before each analysis runs.
    /// </summary>
    [TestMethod]
    public async Task Events_AnalysisStarting_FiredPerItem()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 10)
        };

        int startingCount = 0;
        runner.AnalysisStarting += (s, a) => Interlocked.Increment(ref startingCount);

        await runner.RunAsync(analyses);

        Assert.AreEqual(2, startingCount);
    }

    #endregion

    #region Duration Tests

    /// <summary>
    /// Verifies that each result has a reasonable Duration value.
    /// </summary>
    [TestMethod]
    public async Task Duration_TrackedPerAnalysis()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 50),
            new MockAnalysis(delayMs: 50)
        };

        var results = await runner.RunAsync(analyses);

        foreach (var result in results)
        {
            Assert.IsTrue(result.Duration.TotalMilliseconds >= 40,
                $"Expected duration >= 40ms, got {result.Duration.TotalMilliseconds}ms");
        }
    }

    #endregion

    #region Dependency Ordering Tests

    /// <summary>
    /// A lightweight <see cref="CompositeAnalysis"/> subclass for testing dependency ordering.
    /// Passes <c>is CompositeAnalysis</c> checks so the runner places it in the composite phase.
    /// </summary>
    private class MockCompositeAnalysis : CompositeAnalysis
    {
        /// <summary>
        /// The delay in milliseconds before the analysis completes.
        /// </summary>
        private readonly int _delayMs;

        /// <summary>
        /// Records the time this analysis started, for ordering verification.
        /// </summary>
        public DateTime? StartedAt { get; private set; }

        /// <summary>
        /// Creates a mock composite analysis with configurable delay.
        /// </summary>
        /// <param name="delayMs">How long to simulate running (milliseconds).</param>
        public MockCompositeAnalysis(int delayMs = 10) : base()
        {
            _delayMs = delayMs;
        }

        /// <inheritdoc/>
        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            StartedAt = DateTime.UtcNow;
            progressReporter?.IndicateTaskStart();
            try
            {
                await Task.Delay(_delayMs);
                progressReporter?.ReportProgress(100);
                IsEstimated = true;
            }
            finally
            {
                progressReporter?.IndicateTaskEnded();
            }
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }

    /// <summary>
    /// Verifies that in parallel mode, composite analyses only start after all
    /// non-composite analyses have completed.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_Parallel_CompositeRunsAfterNonComposite()
    {
        var runner = new BatchAnalysisRunner();

        // Non-composite analyses take 100ms each
        var nonComposite1 = new MockAnalysis(delayMs: 100);
        var nonComposite2 = new MockAnalysis(delayMs: 100);

        // Composite analysis is fast but must wait for non-composites
        var composite = new MockCompositeAnalysis(delayMs: 10);

        // Put composite FIRST in the list to verify reordering works
        var analyses = new List<IAnalysis> { composite, nonComposite1, nonComposite2 };

        var startTimes = new Dictionary<IAnalysis, DateTime>();
        var completionTimes = new Dictionary<IAnalysis, DateTime>();

        runner.AnalysisStarting += (s, a) =>
        {
            lock (startTimes)
                startTimes[a] = DateTime.UtcNow;
        };

        runner.AnalysisCompleted += (s, r) =>
        {
            lock (completionTimes)
                completionTimes[r.Analysis] = DateTime.UtcNow;
        };

        var options = new BatchAnalysisOptions
        {
            MaxDegreeOfParallelism = 3,
            OrderByDependency = true
        };

        var results = await runner.RunAsync(analyses, options);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.All(r => r.Succeeded));

        // Verify: composite started after BOTH non-composites completed
        Assert.IsTrue(startTimes.ContainsKey(composite),
            "Composite analysis should have started");
        Assert.IsTrue(completionTimes.ContainsKey(nonComposite1),
            "Non-composite 1 should have completed");
        Assert.IsTrue(completionTimes.ContainsKey(nonComposite2),
            "Non-composite 2 should have completed");

        Assert.IsTrue(startTimes[composite] >= completionTimes[nonComposite1],
            "Composite should start after non-composite 1 completes");
        Assert.IsTrue(startTimes[composite] >= completionTimes[nonComposite2],
            "Composite should start after non-composite 2 completes");
    }

    /// <summary>
    /// Verifies that progress events report correctly across both phases.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_Serial_ProgressSpansPhases()
    {
        var runner = new BatchAnalysisRunner();
        var analyses = new List<IAnalysis>
        {
            new MockAnalysis(delayMs: 10),
            new MockCompositeAnalysis(delayMs: 10),
            new MockAnalysis(delayMs: 10)
        };

        var progressHistory = new List<(int Completed, int Total)>();
        runner.ProgressChanged += (s, p) => progressHistory.Add(p);

        var options = new BatchAnalysisOptions
        {
            MaxDegreeOfParallelism = 1,
            OrderByDependency = true
        };
        var results = await runner.RunAsync(analyses, options);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.All(r => r.Succeeded));

        // Total should always be 3 across both phases
        Assert.AreEqual(3, progressHistory.Count);
        Assert.IsTrue(progressHistory.All(p => p.Total == 3));
        Assert.AreEqual((1, 3), progressHistory[0]);
        Assert.AreEqual((2, 3), progressHistory[1]);
        Assert.AreEqual((3, 3), progressHistory[2]);
    }

    /// <summary>
    /// A lightweight <see cref="CoincidentFrequencyAnalysis"/> subclass for testing three-phase
    /// dependency ordering. Passes <c>is CoincidentFrequencyAnalysis</c> checks so the runner
    /// places it in the final CFA phase (after independent and composite).
    /// </summary>
    /// <summary>
    /// Minimal BivariateAnalysis subclass that exposes <see cref="IsEstimated"/> = true at
    /// construction so a mock CFA can satisfy the runner's pre-flight dependency check
    /// without spinning up real MCMC. The protected <c>IsEstimated</c> setter on
    /// <c>AnalysisBase</c> is reachable from this derived class.
    /// </summary>
    private class EstimatedBivariateAnalysis : BivariateAnalysis
    {
        public EstimatedBivariateAnalysis() : base(new BivariateDistribution())
        {
            IsEstimated = true;
        }

        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            await Task.CompletedTask;
            IsEstimated = true;
        }

        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }

    /// <summary>
    /// CFA mock with NO upstream BA set — used to verify the runner's pre-flight
    /// dependency check rejects it without invoking RunAsync.
    /// </summary>
    private class MockCoincidentFrequencyAnalysisWithoutBA : CoincidentFrequencyAnalysis
    {
        public DateTime? StartedAt { get; private set; }

        public MockCoincidentFrequencyAnalysisWithoutBA() : base()
        {
            // Attach an un-estimated BA so the "missing BA" branch isn't hit; the
            // un-estimated branch is what we want to exercise.
            var ba = new BivariateAnalysis(new BivariateDistribution());
            BivariateAnalysis = ba;
            // ba.IsEstimated stays false → runner pre-flight should reject this CFA.
        }

        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            // Recording StartedAt lets the test assert that RunAsync was NEVER called.
            StartedAt = DateTime.UtcNow;
            await Task.CompletedTask;
            IsEstimated = true;
        }

        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }

    /// <summary>
    /// Composite mock with one un-estimated child UnivariateAnalysis — used to verify
    /// the runner's pre-flight dependency check rejects it without invoking RunAsync.
    /// </summary>
    private class MockCompositeAnalysisWithUnestimatedChild : CompositeAnalysis
    {
        public DateTime? StartedAt { get; private set; }

        public MockCompositeAnalysisWithUnestimatedChild() : base()
        {
            // Add a single child whose IsEstimated is false. We construct it via the
            // model-layer UnivariateAnalysis with a default Normal distribution; nothing
            // sets IsEstimated, so the dependency check will reject the parent composite.
            var child = new UnivariateAnalysis(new UnivariateDistribution(new DataFrame(), UnivariateDistributionType.Normal));
            Analyses.Add(new WeightedUnivariateAnalysis(child, 1.0));
        }

        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            StartedAt = DateTime.UtcNow;
            await Task.CompletedTask;
            IsEstimated = true;
        }

        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }

    private class MockCoincidentFrequencyAnalysis : CoincidentFrequencyAnalysis
    {
        private readonly int _delayMs;
        public DateTime? StartedAt { get; private set; }

        public MockCoincidentFrequencyAnalysis(int delayMs = 10) : base()
        {
            _delayMs = delayMs;
            // Provide an upstream BA that satisfies the runner's pre-flight dependency
            // check. The mock's job is exercise phase ordering, not dependency validation;
            // the real dependency-rejection path is covered by a separate dedicated test.
            BivariateAnalysis = new EstimatedBivariateAnalysis();
        }

        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            StartedAt = DateTime.UtcNow;
            progressReporter?.IndicateTaskStart();
            try
            {
                await Task.Delay(_delayMs);
                progressReporter?.ReportProgress(100);
                IsEstimated = true;
            }
            finally
            {
                progressReporter?.IndicateTaskEnded();
            }
        }

        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }

    /// <summary>
    /// Verifies the three-phase dependency order produced by
    /// <see cref="BatchAnalysisRunner"/> when <see cref="BatchAnalysisOptions.OrderByDependency"/>
    /// is true: independent analyses first, then <see cref="CompositeAnalysis"/> instances,
    /// then <see cref="CoincidentFrequencyAnalysis"/> instances. Composite depends on Phase 1
    /// univariate fits and CFA depends on Phase 1 bivariate fits, so both consumers must run
    /// after Phase 1; their relative order (Composite before CFA) is fixed by convention.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_Parallel_CompositeOrderedAfterIndependentBeforeCFA()
    {
        var runner = new BatchAnalysisRunner();

        var independent = new MockAnalysis(delayMs: 100);
        var composite = new MockCompositeAnalysis(delayMs: 50);
        var cfa = new MockCoincidentFrequencyAnalysis(delayMs: 10);

        // Out-of-order list — runner must reorder into independent → composite → CFA phases.
        var analyses = new List<IAnalysis> { cfa, composite, independent };

        var startTimes = new Dictionary<IAnalysis, DateTime>();
        var completionTimes = new Dictionary<IAnalysis, DateTime>();
        runner.AnalysisStarting += (s, a) => { lock (startTimes) startTimes[a] = DateTime.UtcNow; };
        runner.AnalysisCompleted += (s, r) => { lock (completionTimes) completionTimes[r.Analysis] = DateTime.UtcNow; };

        var options = new BatchAnalysisOptions
        {
            MaxDegreeOfParallelism = 3,
            OrderByDependency = true
        };

        var results = await runner.RunAsync(analyses, options);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.All(r => r.Succeeded));

        // Composite started after independent completed.
        Assert.IsTrue(startTimes[composite] >= completionTimes[independent],
            "Composite should start after independent analyses complete.");

        // CFA started after composite completed.
        Assert.IsTrue(startTimes[cfa] >= completionTimes[composite],
            "CFA should start after Composite analyses complete.");
    }

    /// <summary>
    /// Verifies that the runner records a clean dependency failure (without invoking
    /// <c>RunAsync</c>) when a <see cref="CoincidentFrequencyAnalysis"/> reaches its phase
    /// with the upstream <see cref="BivariateAnalysis"/> still un-estimated. The result
    /// must be non-succeeded with a descriptive error message; <c>StartedAt</c> on the
    /// mock CFA must remain null because <c>RunAsync</c> is never reached.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_CFA_UpstreamUnestimated_RecordsFailureWithoutInvokingRunAsync()
    {
        var runner = new BatchAnalysisRunner();

        // Build a CFA whose upstream BA is left un-estimated (no IsEstimated=true setter).
        var cfa = new MockCoincidentFrequencyAnalysisWithoutBA();

        var results = await runner.RunAsync(
            new List<IAnalysis> { cfa },
            new BatchAnalysisOptions { MaxDegreeOfParallelism = 1, OrderByDependency = true });

        Assert.AreEqual(1, results.Count);
        Assert.IsFalse(results[0].Succeeded, "CFA with un-estimated upstream BA must be rejected.");
        Assert.IsNotNull(results[0].Error, "Failure must surface an error.");
        StringAssert.Contains(
            results[0].Error!.Message,
            "BivariateAnalysis",
            "Failure message should identify the missing upstream dependency.");
        Assert.IsNull(cfa.StartedAt, "RunAsync should not be invoked when the dependency check fails.");
    }

    /// <summary>
    /// Symmetric test for <see cref="CompositeAnalysis"/>: an un-estimated child univariate
    /// in <see cref="CompositeAnalysis.Analyses"/> must cause a clean dependency failure
    /// without invoking <c>RunAsync</c>.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_Composite_ChildUnestimated_RecordsFailureWithoutInvokingRunAsync()
    {
        var runner = new BatchAnalysisRunner();

        var composite = new MockCompositeAnalysisWithUnestimatedChild();

        var results = await runner.RunAsync(
            new List<IAnalysis> { composite },
            new BatchAnalysisOptions { MaxDegreeOfParallelism = 1, OrderByDependency = true });

        Assert.AreEqual(1, results.Count);
        Assert.IsFalse(results[0].Succeeded, "Composite with un-estimated child must be rejected.");
        Assert.IsNotNull(results[0].Error, "Failure must surface an error.");
        StringAssert.Contains(
            results[0].Error!.Message,
            "UnivariateAnalysis",
            "Failure message should identify the missing child dependency.");
        Assert.IsNull(composite.StartedAt, "RunAsync should not be invoked when the dependency check fails.");
    }

    /// <summary>
    /// Verifies that when OrderByDependency is false, all analyses run in a single phase
    /// preserving the original order.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_NoOrderByDependency_SinglePhase()
    {
        var runner = new BatchAnalysisRunner();
        var composite = new MockCompositeAnalysis(delayMs: 10);
        var nonComposite = new MockAnalysis(delayMs: 10);

        // Composite first, then non-composite — should stay in this order
        var analyses = new List<IAnalysis> { composite, nonComposite };

        var completionOrder = new List<IAnalysis>();
        runner.AnalysisCompleted += (s, r) =>
        {
            lock (completionOrder)
                completionOrder.Add(r.Analysis);
        };

        var options = new BatchAnalysisOptions
        {
            MaxDegreeOfParallelism = 1,
            OrderByDependency = false
        };

        var results = await runner.RunAsync(analyses, options);

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(r => r.Succeeded));

        // With OrderByDependency=false, composite runs first (original order)
        Assert.AreSame(composite, completionOrder[0]);
        Assert.AreSame(nonComposite, completionOrder[1]);
    }

    #endregion

    #region Options Validation Tests

    /// <summary>
    /// Verifies that MaxDegreeOfParallelism rejects zero or negative values.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void BatchAnalysisOptions_InvalidParallelism_Throws()
    {
        var options = new BatchAnalysisOptions();
        options.MaxDegreeOfParallelism = 0;
    }

    /// <summary>
    /// Verifies that default options have the expected values.
    /// </summary>
    [TestMethod]
    public void BatchAnalysisOptions_Defaults()
    {
        var options = new BatchAnalysisOptions();

        Assert.AreEqual(1, options.MaxDegreeOfParallelism);
        Assert.IsTrue(options.ContinueOnError);
        Assert.IsTrue(options.OrderByDependency);
    }

    #endregion
}
