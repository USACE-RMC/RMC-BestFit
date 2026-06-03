using System.Diagnostics;
using Numerics.Utilities;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Executes a collection of <see cref="IAnalysis"/> instances with configurable
    /// parallelism, error handling, progress reporting, and cancellation support.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// The runner can operate in serial mode (<see cref="BatchAnalysisOptions.MaxDegreeOfParallelism"/> = 1)
    /// or parallel mode (> 1). In parallel mode, a <see cref="SemaphoreSlim"/> gates the number
    /// of concurrently executing analyses.
    /// </para>
    /// <para>
    /// This class resides in <c>RMC.BestFit.dll</c> (the model library) so that it can be
    /// consumed by WPF desktop applications, Python interop (via pythonnet), web APIs,
    /// command-line tools, and unit tests without any UI dependencies.
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> A single <see cref="BatchAnalysisRunner"/> instance must not
    /// be used concurrently from multiple threads. Create separate instances for concurrent
    /// batch executions.
    /// </para>
    /// <para>
    /// <b>Execution ordering:</b> When <see cref="BatchAnalysisOptions.OrderByDependency"/>
    /// is <c>true</c> (the default), analyses are partitioned into three sequential phases:
    /// (1) independent analyses (Univariate, Bivariate, B17C, etc.);
    /// (2) <see cref="CompositeAnalysis"/> instances, which depend on Phase 1 univariate fits;
    /// (3) <see cref="CoincidentFrequencyAnalysis"/> instances, which depend on Phase 1 bivariate fits.
    /// Composite and CFA are in different phases purely by convention â€” they have no
    /// cross-dependency on each other.
    /// </para>
    /// </remarks>
    public class BatchAnalysisRunner
    {
        /// <summary>
        /// The cancellation token source used to cancel the current batch run.
        /// </summary>
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Occurs immediately before an individual analysis begins executing
        /// within the batch.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The event argument is the <see cref="IAnalysis"/> that is about to run.
        /// This event is raised on the thread that starts the analysis, which may
        /// be a thread-pool thread in parallel mode.
        /// </para>
        /// </remarks>
        public event EventHandler<IAnalysis>? AnalysisStarting;

        /// <summary>
        /// Occurs after an individual analysis has finished executing within the batch,
        /// regardless of whether it succeeded, failed, or was canceled.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The event argument is a <see cref="BatchAnalysisResult"/> containing the
        /// outcome, duration, and any exception for the completed analysis.
        /// In parallel mode, this event may be raised from multiple threads.
        /// </para>
        /// </remarks>
        public event EventHandler<BatchAnalysisResult>? AnalysisCompleted;

        /// <summary>
        /// Occurs once after all analyses in the batch have finished executing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The event argument is the complete list of <see cref="BatchAnalysisResult"/>
        /// objects, one for each analysis in the batch, in execution order.
        /// </para>
        /// </remarks>
        public event EventHandler<List<BatchAnalysisResult>>? BatchCompleted;

        /// <summary>
        /// Occurs each time an individual analysis finishes, reporting the aggregate
        /// progress of the batch as a (Completed, Total) tuple.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>Completed</c> is the number of analyses that have finished so far
        /// (regardless of outcome), and <c>Total</c> is the total number of analyses
        /// in the batch. In parallel mode, this event uses
        /// <see cref="Interlocked.Increment(ref int)"/> to ensure thread-safe counting.
        /// </para>
        /// </remarks>
        public event EventHandler<(int Completed, int Total)>? ProgressChanged;

        /// <summary>
        /// Occurs when an individual analysis reports progress during execution.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The event argument is a tuple of the <see cref="IAnalysis"/> that reported
        /// progress and the progress percentage (0â€“100). Each analysis receives its own
        /// <see cref="SafeProgressReporter"/>, so this event correctly identifies which
        /// analysis is reporting even in parallel mode.
        /// </para>
        /// </remarks>
        public event EventHandler<(IAnalysis Analysis, double Progress)>? AnalysisProgressChanged;

        /// <summary>
        /// Executes a collection of analyses as a batch with the specified options.
        /// </summary>
        /// <param name="analyses">
        /// The analyses to execute. Must not be <c>null</c>. An empty list is permitted
        /// and returns an empty result list immediately.
        /// </param>
        /// <param name="options">
        /// Optional configuration for parallelism, error handling, and ordering.
        /// If <c>null</c>, default options are used (serial execution, continue on error,
        /// order by dependency).
        /// </param>
        /// <param name="progressReporter">
        /// Optional progress reporter passed to each <see cref="IAnalysis.RunAsync"/>.
        /// In serial mode, each analysis receives this reporter directly. In parallel mode,
        /// only the aggregate <see cref="ProgressChanged"/> event is meaningful since
        /// multiple analyses may report progress simultaneously.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe for cancellation. When cancellation is requested, the runner
        /// stops starting new analyses and cancels any currently running analyses via
        /// <see cref="IAnalysis.CancelAnalysis"/>.
        /// </param>
        /// <returns>
        /// A list of <see cref="BatchAnalysisResult"/> objects, one for each analysis,
        /// in execution order.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="analyses"/> is <c>null</c>.
        /// </exception>
        public async Task<List<BatchAnalysisResult>> RunAsync(
            IList<IAnalysis> analyses,
            BatchAnalysisOptions? options = null,
            SafeProgressReporter? progressReporter = null,
            CancellationToken cancellationToken = default)
        {
            if (analyses == null)
                throw new ArgumentNullException(nameof(analyses));

            options ??= new BatchAnalysisOptions();
            var results = new List<BatchAnalysisResult>();

            if (analyses.Count == 0)
            {
                BatchCompleted?.Invoke(this, results);
                return results;
            }

            // Create a linked cancellation token source so Cancel() and the external token both work
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var linkedToken = _cancellationTokenSource.Token;

            // Partition analyses into execution phases: independent first, then Composite,
            // then CoincidentFrequencyAnalysis. Each phase completes before the next begins,
            // ensuring upstream fits are available to their dependent consumers.
            var phases = OrderAnalyses(analyses, options.OrderByDependency);
            int total = analyses.Count;
            int completed = 0;

            progressReporter?.IndicateTaskStart();

            try
            {
                foreach (var phase in phases)
                {
                    List<BatchAnalysisResult> phaseResults;

                    if (options.MaxDegreeOfParallelism == 1)
                    {
                        phaseResults = await RunSerialAsync(phase, options,
                            linkedToken, total, completed);
                    }
                    else
                    {
                        phaseResults = await RunParallelAsync(phase, options,
                            linkedToken, total, completed);
                    }

                    results.AddRange(phaseResults);
                    completed += phaseResults.Count;
                }
            }
            finally
            {
                progressReporter?.IndicateTaskEnded();
                BatchCompleted?.Invoke(this, results);
            }

            return results;
        }

        /// <summary>
        /// Cancels the currently executing batch run.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method signals the internal <see cref="CancellationTokenSource"/>,
        /// which causes the runner to stop starting new analyses. Currently running
        /// analyses are also canceled via <see cref="IAnalysis.CancelAnalysis"/>.
        /// </para>
        /// <para>
        /// If no batch is currently running, this method has no effect.
        /// </para>
        /// </remarks>
        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Executes analyses one at a time in sequence.
        /// </summary>
        /// <param name="analyses">The list of analyses in this phase to execute.</param>
        /// <param name="options">The batch options controlling error behavior.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <param name="total">Total number of analyses across all phases.</param>
        /// <param name="completedBefore">Number of analyses completed in prior phases.</param>
        /// <returns>A list of results for analyses executed in this phase.</returns>
        private async Task<List<BatchAnalysisResult>> RunSerialAsync(
            List<IAnalysis> analyses,
            BatchAnalysisOptions options,
            CancellationToken cancellationToken,
            int total,
            int completedBefore)
        {
            var results = new List<BatchAnalysisResult>(analyses.Count);
            int completed = completedBefore;

            foreach (var analysis in analyses)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // Record remaining analyses as canceled
                    results.Add(new BatchAnalysisResult(analysis,
                        succeeded: false, wasCanceled: true, error: null, duration: TimeSpan.Zero));
                    continue;
                }

                var result = await ExecuteSingleAnalysisAsync(analysis, cancellationToken);
                results.Add(result);

                completed++;
                ProgressChanged?.Invoke(this, (completed, total));

                // Stop early if configured and analysis failed
                if (!result.Succeeded && !result.WasCanceled && !options.ContinueOnError)
                {
                    // Cancel the token so remaining analyses are marked as canceled
                    _cancellationTokenSource?.Cancel();
                }
            }

            return results;
        }

        /// <summary>
        /// Executes analyses in a single phase concurrently using a semaphore to limit parallelism.
        /// </summary>
        /// <param name="analyses">The list of analyses in this phase to execute.</param>
        /// <param name="options">The batch options controlling parallelism and error behavior.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <param name="total">Total number of analyses across all phases.</param>
        /// <param name="completedBefore">Number of analyses completed in prior phases.</param>
        /// <returns>A list of results for analyses executed in this phase.</returns>
        /// <remarks>
        /// <para>
        /// Tasks are started without <see cref="Task.Run"/> so that analysis completions
        /// (which fire <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>)
        /// resume on the caller's <see cref="SynchronizationContext"/> (the UI thread in WPF).
        /// The CPU-heavy MCMC work still runs on thread-pool threads because each
        /// <see cref="IAnalysis.RunAsync"/> uses <c>Task.Run</c> internally.
        /// The <see cref="SemaphoreSlim"/> gates how many analyses are in-flight concurrently.
        /// </para>
        /// </remarks>
        private async Task<List<BatchAnalysisResult>> RunParallelAsync(
            List<IAnalysis> analyses,
            BatchAnalysisOptions options,
            CancellationToken cancellationToken,
            int total,
            int completedBefore)
        {
            int phaseCount = analyses.Count;
            var results = new BatchAnalysisResult[phaseCount];
            int completed = completedBefore;

            using var semaphore = new SemaphoreSlim(options.MaxDegreeOfParallelism);
            var tasks = new List<Task>(phaseCount);

            for (int i = 0; i < phaseCount; i++)
            {
                int index = i;

                // Start without Task.Run so completions resume on the calling SynchronizationContext.
                // RunAsync is already async and does its CPU work on pool threads internally.
                tasks.Add(RunSingleWithSemaphoreAsync(index));
            }

            async Task RunSingleWithSemaphoreAsync(int idx)
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var analysis = analyses[idx];

                    if (cancellationToken.IsCancellationRequested)
                    {
                        results[idx] = new BatchAnalysisResult(analysis,
                            succeeded: false, wasCanceled: true, error: null, duration: TimeSpan.Zero);
                        return;
                    }

                    var result = await ExecuteSingleAnalysisAsync(analysis, cancellationToken);
                    results[idx] = result;

                    Interlocked.Increment(ref completed);
                    ProgressChanged?.Invoke(this, (completed, total));

                    // Stop early if configured and analysis failed
                    if (!result.Succeeded && !result.WasCanceled && !options.ContinueOnError)
                    {
                        _cancellationTokenSource?.Cancel();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested; results array has entries for completed analyses
            }

            // Fill in null entries for analyses that never started due to cancellation
            for (int i = 0; i < phaseCount; i++)
            {
                if (results[i] == null)
                {
                    results[i] = new BatchAnalysisResult(analyses[i],
                        succeeded: false, wasCanceled: true, error: null, duration: TimeSpan.Zero);
                }
            }

            return results.ToList();
        }

        /// <summary>
        /// Executes a single analysis with timing, error handling, and event notification.
        /// Each analysis receives its own <see cref="SafeProgressReporter"/> so that
        /// <see cref="AnalysisProgressChanged"/> correctly identifies the source even
        /// when multiple analyses run concurrently.
        /// </summary>
        /// <param name="analysis">The analysis to execute.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A <see cref="BatchAnalysisResult"/> describing the outcome.</returns>
        private async Task<BatchAnalysisResult> ExecuteSingleAnalysisAsync(
            IAnalysis analysis,
            CancellationToken cancellationToken)
        {
            AnalysisStarting?.Invoke(this, analysis);

            var stopwatch = Stopwatch.StartNew();
            bool succeeded = false;
            bool wasCanceled = false;
            Exception? error = null;

            // Create a per-analysis progress reporter so parallel runs don't collide
            var progressReporter = new SafeProgressReporter(analysis.GetType().Name);
            progressReporter.ProgressReported += (reporter, progress, delta) =>
            {
                AnalysisProgressChanged?.Invoke(this, (analysis, progress));
            };

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    analysis.CancelAnalysis();
                    wasCanceled = true;
                }
                else
                {
                    // Phase 2/3 dependents (CompositeAnalysis with un-estimated children,
                    // CoincidentFrequencyAnalysis with un-estimated upstream BivariateAnalysis)
                    // are batch-eligible BEFORE Phase 1 has run their dependencies. Phase
                    // ordering normally fits the dependency first, but if it failed in
                    // Phase 1 we must not let RunAsync throw deeper in the stack — instead
                    // we record a clear, type-specific failure here and skip the run.
                    var dependencyFailure = CheckDependencies(analysis);
                    if (dependencyFailure != null)
                    {
                        error = dependencyFailure;
                        Debug.WriteLine($"BatchAnalysisRunner: dependency failure: {error.Message}");
                    }
                    else
                    {
                        // Register callback so canceling the token mid-flight calls CancelAnalysis
                        using var registration = cancellationToken.Register(() => analysis.CancelAnalysis());
                        await analysis.RunAsync(progressReporter);

                        // If cancellation was requested during RunAsync, treat as canceled
                        // even if the analysis set IsEstimated = true before stopping.
                        if (cancellationToken.IsCancellationRequested)
                            wasCanceled = true;
                        else
                            succeeded = analysis.IsEstimated;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                wasCanceled = true;
            }
            catch (Exception ex)
            {
                error = ex;
                Debug.WriteLine($"BatchAnalysisRunner: Analysis failed with exception: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
            }

            var result = new BatchAnalysisResult(analysis, succeeded, wasCanceled, error, stopwatch.Elapsed);
            AnalysisCompleted?.Invoke(this, result);

            return result;
        }

        /// <summary>
        /// Verifies that the cross-batch dependencies of dependent analyses are satisfied
        /// by the time the dependent's phase runs. Returns null when the analysis is safe
        /// to invoke, or an <see cref="InvalidOperationException"/> describing the missing
        /// dependency when it is not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two dependent analysis types are guarded:
        /// </para>
        /// <list type="bullet">
        /// <item><description><see cref="CompositeAnalysis"/> requires every child
        /// <see cref="WeightedUnivariateAnalysis.UnivariateAnalysis"/> to satisfy
        /// <see cref="UnivariateAnalysis.IsEstimated"/>.</description></item>
        /// <item><description><see cref="CoincidentFrequencyAnalysis"/> requires its
        /// upstream <see cref="CoincidentFrequencyAnalysis.BivariateAnalysis"/> to be
        /// non-null and satisfy <see cref="BivariateAnalysis.IsEstimated"/>.</description></item>
        /// </list>
        /// <para>
        /// Other analysis types pass through unchanged. The check is intentionally narrower
        /// than the analysis's own <c>Validate()</c> — it guards the cross-batch dependency
        /// only; all other failure modes (shape, bin count, etc.) surface from
        /// <c>RunAsync</c>'s own validation as before.
        /// </para>
        /// </remarks>
        private static InvalidOperationException? CheckDependencies(IAnalysis analysis)
        {
            if (analysis is CompositeAnalysis composite)
            {
                if (composite.Analyses == null || composite.Analyses.Count == 0)
                    return null; // Composite's own Validate() will catch this.
                foreach (var wua in composite.Analyses)
                {
                    var ua = wua?.UnivariateAnalysis;
                    if (ua == null || !ua.IsEstimated)
                    {
                        return new InvalidOperationException(
                            "Cannot run CompositeAnalysis: a child UnivariateAnalysis has not been estimated.");
                    }
                }
                return null;
            }
            if (analysis is CoincidentFrequencyAnalysis cfa)
            {
                var ba = cfa.BivariateAnalysis;
                if (ba == null)
                {
                    return new InvalidOperationException(
                        "Cannot run CoincidentFrequencyAnalysis: no upstream BivariateAnalysis selected.");
                }
                if (!ba.IsEstimated)
                {
                    return new InvalidOperationException(
                        "Cannot run CoincidentFrequencyAnalysis: upstream BivariateAnalysis has not been estimated.");
                }
                return null;
            }
            return null;
        }

        /// <summary>
        /// Partitions analyses into execution phases so that dependent analyses run
        /// after the analyses they depend on.
        /// </summary>
        /// <param name="analyses">The input list of analyses.</param>
        /// <param name="orderByDependency">
        /// If <c>true</c>, partitions into phases: non-composite first, then composite.
        /// If <c>false</c>, returns all analyses in a single phase preserving original order.
        /// </param>
        /// <returns>
        /// A list of phases, where each phase is a list of analyses that can run concurrently.
        /// Phases execute sequentially â€” all analyses in a phase must complete before the
        /// next phase begins.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Two analysis types depend on results produced by sibling analyses run in the same batch:
        /// <see cref="CompositeAnalysis"/> aggregates the posterior fits of its child univariate analyses;
        /// <see cref="CoincidentFrequencyAnalysis"/> consumes the upstream <see cref="BivariateAnalysis"/>'s
        /// posterior copula and marginal MCMC chains. This method partitions runs into three
        /// dependency phases so the upstream fits are guaranteed to be complete before the consumers run:
        /// </para>
        /// <list type="number">
        /// <item><description>Phase 1 â€” independent analyses (Univariate, Bivariate, etc.)</description></item>
        /// <item><description>Phase 2 â€” <see cref="CompositeAnalysis"/> instances (depend on Phase 1 univariate fits)</description></item>
        /// <item><description>Phase 3 â€” <see cref="CoincidentFrequencyAnalysis"/> instances (depend on Phase 1 bivariate fits)</description></item>
        /// </list>
        /// <para>
        /// Composite (univariate) and CFA (bivariate) live in different phases by convention only;
        /// they have no actual cross-dependency. Within each phase, runs proceed concurrently up to
        /// <see cref="BatchAnalysisOptions.MaxDegreeOfParallelism"/>, and the relative order within
        /// each phase is preserved.
        /// </para>
        /// </remarks>
        private static List<List<IAnalysis>> OrderAnalyses(IList<IAnalysis> analyses, bool orderByDependency)
        {
            if (!orderByDependency)
                return new List<List<IAnalysis>> { new List<IAnalysis>(analyses) };

            var independent = new List<IAnalysis>();
            var coinFreq = new List<IAnalysis>();
            var composite = new List<IAnalysis>();

            foreach (var analysis in analyses)
            {
                if (analysis is CompositeAnalysis)
                    composite.Add(analysis);
                else if (analysis is CoincidentFrequencyAnalysis)
                    coinFreq.Add(analysis);
                else
                    independent.Add(analysis);
            }

            var phases = new List<List<IAnalysis>>();
            if (independent.Count > 0) phases.Add(independent);
            if (composite.Count > 0) phases.Add(composite);
            if (coinFreq.Count > 0) phases.Add(coinFreq);
            return phases;
        }
    }
}
