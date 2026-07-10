using RMC.BestFit.Analyses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RMC_BestFit
{
    /// <summary>
    /// Coalesces batch progress updates before applying them to WPF row view models.
    /// </summary>
    /// <remarks>
    /// Parallel analyses can report progress from several worker threads at once. This helper
    /// keeps only the latest progress value for each analysis and applies those values on the
    /// dispatcher at a modest cadence so rendering and input are not starved by progress chatter.
    /// </remarks>
    internal sealed class BatchProgressUpdateDispatcher : IDisposable
    {
        /// <summary>
        /// Minimum time between queued progress flushes.
        /// </summary>
        private const int MinimumFlushIntervalMilliseconds = 50;

        /// <summary>
        /// Dispatcher that owns the row view models.
        /// </summary>
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// Analysis-to-row lookup updated by this dispatcher.
        /// </summary>
        private readonly Dictionary<IAnalysis, BatchRunItemViewModel> _analysisToViewModel;

        /// <summary>
        /// Synchronizes pending progress and completion state.
        /// </summary>
        private readonly object _gate = new object();

        /// <summary>
        /// Latest progress value by analysis.
        /// </summary>
        private readonly Dictionary<IAnalysis, double> _pendingProgress = new Dictionary<IAnalysis, double>();

        /// <summary>
        /// Analyses whose terminal state has already been received.
        /// </summary>
        private readonly HashSet<IAnalysis> _completedAnalyses = new HashSet<IAnalysis>();

        /// <summary>
        /// Tracks elapsed time since the last progress flush.
        /// </summary>
        private readonly Stopwatch _flushStopwatch = Stopwatch.StartNew();

        /// <summary>
        /// Indicates whether a background flush has already been queued.
        /// </summary>
        private bool _flushQueued;

        /// <summary>
        /// Indicates whether this dispatcher has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchProgressUpdateDispatcher"/> class.
        /// </summary>
        /// <param name="dispatcher">The WPF dispatcher that owns the row view models.</param>
        /// <param name="analysisToViewModel">The analysis-to-row lookup to update.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        internal BatchProgressUpdateDispatcher(
            Dispatcher dispatcher,
            Dictionary<IAnalysis, BatchRunItemViewModel> analysisToViewModel)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _analysisToViewModel = analysisToViewModel ?? throw new ArgumentNullException(nameof(analysisToViewModel));
        }

        /// <summary>
        /// Marks an analysis as running on the dispatcher.
        /// </summary>
        /// <param name="analysis">The analysis that is starting.</param>
        internal void MarkStarting(IAnalysis analysis)
        {
            if (analysis == null) return;

            lock (_gate)
            {
                _completedAnalyses.Remove(analysis);
                _pendingProgress.Remove(analysis);
            }

            BeginInvoke(DispatcherPriority.Normal, () =>
            {
                BatchRunCoordinator.MarkStarting(_analysisToViewModel, analysis);
            });
        }

        /// <summary>
        /// Applies a completed result and prevents later stale progress for the same analysis.
        /// </summary>
        /// <param name="result">The completed batch result.</param>
        internal void ApplyResult(BatchAnalysisResult result)
        {
            if (result == null) return;

            lock (_gate)
            {
                _completedAnalyses.Add(result.Analysis);
                _pendingProgress.Remove(result.Analysis);
            }

            BeginInvoke(DispatcherPriority.Normal, () =>
            {
                BatchRunCoordinator.ApplyResult(_analysisToViewModel, result);
            });
        }

        /// <summary>
        /// Records the latest progress value for an analysis and queues a coalesced flush.
        /// </summary>
        /// <param name="analysis">The analysis whose progress changed.</param>
        /// <param name="progress">The latest progress percentage.</param>
        internal void PostProgress(IAnalysis analysis, double progress)
        {
            if (analysis == null) return;

            int delayMilliseconds;
            lock (_gate)
            {
                if (_disposed || _completedAnalyses.Contains(analysis)) return;

                _pendingProgress[analysis] = progress;
                if (_flushQueued) return;

                _flushQueued = true;
                long remaining = MinimumFlushIntervalMilliseconds - _flushStopwatch.ElapsedMilliseconds;
                delayMilliseconds = (int)Math.Max(0, remaining);
            }

            _ = Task.Delay(delayMilliseconds).ContinueWith(delayTask =>
            {
                if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return;
                _ = _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Flush));
            }, TaskScheduler.Default);
        }

        /// <summary>
        /// Flushes any pending progress updates on the dispatcher.
        /// </summary>
        /// <returns>A task that completes after the flush has run.</returns>
        internal Task FlushAsync()
        {
            if (_dispatcher.CheckAccess())
            {
                Flush();
                return Task.CompletedTask;
            }

            return _dispatcher.InvokeAsync(Flush, DispatcherPriority.Background).Task;
        }

        /// <summary>
        /// Prevents future progress updates from being queued.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _pendingProgress.Clear();
                _completedAnalyses.Clear();
            }
        }

        /// <summary>
        /// Queues an action on the dispatcher if the window is still alive.
        /// </summary>
        /// <param name="priority">The dispatcher priority to use.</param>
        /// <param name="action">The action to invoke.</param>
        private void BeginInvoke(DispatcherPriority priority, Action action)
        {
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return;
            _ = _dispatcher.BeginInvoke(priority, action);
        }

        /// <summary>
        /// Applies the latest pending progress values to the row view models.
        /// </summary>
        private void Flush()
        {
            KeyValuePair<IAnalysis, double>[] updates;
            lock (_gate)
            {
                if (_disposed)
                {
                    _flushQueued = false;
                    return;
                }

                updates = _pendingProgress
                    .Where(pair => !_completedAnalyses.Contains(pair.Key))
                    .ToArray();
                _pendingProgress.Clear();
                _flushQueued = false;
                _flushStopwatch.Restart();
            }

            foreach (KeyValuePair<IAnalysis, double> update in updates)
            {
                BatchRunCoordinator.ApplyProgress(_analysisToViewModel, update.Key, update.Value);
            }
        }
    }
}
