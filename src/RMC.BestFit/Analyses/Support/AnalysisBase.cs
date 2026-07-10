using Numerics.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Provides a common base implementation for analysis classes, including
    /// property change notification, run lifecycle events, cancellation support,
    /// and a standard <c>IsEstimated</c> flag.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Concrete analysis types should derive from <see cref="AnalysisBase"/> and
    /// implement <see cref="RunAsync(SafeProgressReporter)"/> and
    /// <see cref="Validate"/>. The base class:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// Exposes <see cref="AnalysisStarting"/> and <see cref="AnalysisCompleted"/> events to
    /// signal the start and end of an analysis run.
    /// </description></item>
    /// <item><description>
    /// Manages a <see cref="CancellationTokenSource"/> that can be requested via
    /// <see cref="CancelAnalysis"/> and used by derived classes when running
    /// long-running operations.
    /// </description></item>
    /// <item><description>
    /// Provides a common <c>IsEstimated</c> flag indicating whether the
    /// analysis currently has valid results.
    /// </description></item>
    /// </list>
    /// </remarks>
    public abstract class AnalysisBase : IAnalysis
    {
        /// <summary>
        /// Backing field for <c>IsEstimated</c>.
        /// </summary>
        protected bool _isEstimated = false;

        /// <summary>
        /// The cancellation token source used to signal cancellation of the
        /// current analysis run.
        /// </summary>
        protected CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Serializes <see cref="RunAsync"/> against any reprocess scheduled by
        /// <see cref="ReprocessIfEstimated"/>. Without this gate, a fire-and-forget
        /// reprocess body (running on the thread pool inside its own
        /// <c>Parallel.For</c> writing to <c>AnalysisResults.*</c>) can be live when
        /// <see cref="RunAsync"/> calls a derived <c>ClearResults</c> that nulls
        /// <c>AnalysisResults</c> — producing an NRE on the next dereference inside
        /// the loop body. Reprocess scheduling and Run both go through the same
        /// gate, so the new MCMC waits for any in-flight reprocess to complete
        /// before clearing results, and concurrent reprocesses queue rather than
        /// trample each other.
        /// </summary>
        protected readonly SemaphoreSlim _reprocessGate = new SemaphoreSlim(1, 1);

        ///<inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc/>
        public event EventHandler<CancelEventArgs>? AnalysisStarting;

        /// <inheritdoc/>
        public event EventHandler<AnalysisRunCompletedEventArgs>? AnalysisCompleted;

        /// <summary>
        /// Gets a value indicating whether the candidate distributions have been
        /// successfully fitted to the current data.
        /// </summary>
        public bool IsEstimated
        {
            get { return _isEstimated; }
            protected set
            {
                if (_isEstimated != value)
                {
                    _isEstimated = value;
                    RaisePropertyChange(nameof(IsEstimated));
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="CancellationTokenSource"/> used to signal cancellation for ongoing operations.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource => _cancellationTokenSource ??= new CancellationTokenSource();

        /// <summary>
        /// Raises the <c>PropertyChanged</c> event for the specified
        /// property name.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void RaisePropertyChange(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises the <see cref="AnalysisStarting"/> event with the provided
        /// <see cref="CancelEventArgs"/>.
        /// </summary>
        /// <param name="e">
        /// An instance of <see cref="CancelEventArgs"/> that handlers can use
        /// to cancel the run before it starts.
        /// </param>
        protected virtual void OnAnalysisStarting(CancelEventArgs e)
        {
            AnalysisStarting?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="AnalysisCompleted"/> event with the provided
        /// <see cref="AnalysisRunCompletedEventArgs"/>.
        /// </summary>
        /// <param name="e">
        /// An instance of <see cref="AnalysisRunCompletedEventArgs"/> describing
        /// the outcome of the analysis run.
        /// </param>
        protected virtual void OnAnalysisCompleted(AnalysisRunCompletedEventArgs e)
        {
            AnalysisCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes any existing <see cref="CancellationTokenSource"/>, creates
        /// a new one for the next analysis run, and returns its token.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Derived classes should call this method at the beginning of
        /// <see cref="RunAsync(SafeProgressReporter)"/> to obtain a fresh
        /// <see cref="CancellationToken"/> to pass into long-running operations.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A new <see cref="CancellationToken"/> associated with a fresh
        /// <see cref="CancellationTokenSource"/>.
        /// </returns>
        protected CancellationToken ResetCancellationToken()
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            return _cancellationTokenSource.Token;
        }

        /// <inheritdoc/>
        public virtual void CancelAnalysis()
        {
            _cancellationTokenSource?.Cancel();
        }

        ///<inheritdoc/>
        public abstract Task RunAsync(SafeProgressReporter? progressReporter);

        ///<inheritdoc/>
        public abstract (bool IsValid, List<string> ValidationMessages) Validate();

        /// <summary>
        /// Fire-and-forget reprocess of derived analysis results when the analysis is
        /// estimated. Used by post-processing property setters (e.g.,
        /// <c>ProbabilityOrdinates</c>, <c>CredibleIntervalWidth</c>, evaluation-grid
        /// properties) that should rebuild output from an unchanged MCMC chain.
        /// </summary>
        /// <param name="reprocessor">
        /// The async method to invoke (typically <c>CreateFrequencyAnalysisResultsAsync</c>
        /// or <c>CreateUncertaintyAnalysisResultsAsync</c>).
        /// </param>
        /// <param name="callerName">
        /// Captured automatically by the compiler — used to label exceptions in the
        /// background task continuation. Callers should not pass this explicitly.
        /// </param>
        /// <remarks>
        /// <para>
        /// Returns immediately if <c>IsEstimated</c> is <c>false</c> — fresh
        /// (un-fit) analyses have nothing to reprocess. Otherwise schedules
        /// <paramref name="reprocessor"/> on the default task scheduler and logs any
        /// exception via <c>Debug.WriteLine</c> without propagating to the
        /// caller. The continuation runs on the default scheduler so it does not
        /// require a synchronization context.
        /// </para>
        /// </remarks>
        protected void ReprocessIfEstimated(Func<Task> reprocessor, [CallerMemberName] string callerName = "")
        {
            if (!IsEstimated) return;

            // Gate acquisition is wrapped in Task.Run so the calling property setter
            // (typically on the UI thread) returns immediately rather than awaiting
            // the gate synchronously. The gate ensures the reprocess body cannot
            // overlap with another reprocess, or with a user-initiated RunAsync
            // (which acquires the same gate before its synchronous ClearResults).
            _ = Task.Run(async () =>
            {
                await _reprocessGate.WaitAsync();
                try
                {
                    await reprocessor();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Reprocess from {callerName} failed: {ex.InnerException?.Message ?? ex.Message}");
                    // Surface failure by clearing IsEstimated. Without this, stale
                    // results would remain visible to consumers despite the
                    // re-process having silently failed.
                    IsEstimated = false;
                }
                finally
                {
                    _reprocessGate.Release();
                }
            });
        }
    }
}
