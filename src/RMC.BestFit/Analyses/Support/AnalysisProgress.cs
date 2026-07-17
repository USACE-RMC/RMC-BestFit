using Numerics.Utilities;
using System.Threading;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Provides shared progress phase constants and ambient parallelism controls for analyses.
    /// </summary>
    /// <remarks>
    /// Batch execution can run several analyses at once. The ambient parallelism limit lets
    /// nested result-building loops reserve CPU headroom during parallel batch runs while
    /// standalone and serial runs keep the normal processor-count behavior.
    /// </remarks>
    internal static class AnalysisProgress
    {
        /// <summary>
        /// Progress value reported when analysis work starts.
        /// </summary>
        internal const double Starting = 0.0;

        /// <summary>
        /// Progress value reached when the main estimator finishes.
        /// </summary>
        internal const double EstimationComplete = 99.0;

        /// <summary>
        /// Progress value reported while final results are being assembled.
        /// </summary>
        internal const double ProcessingResults = 99.0;

        /// <summary>
        /// Progress value reported only after analysis results are fully published.
        /// </summary>
        internal const double Complete = 100.0;

        /// <summary>
        /// Stores the current async-flow-local maximum degree of inner analysis parallelism.
        /// </summary>
        private static readonly AsyncLocal<int?> AmbientMaxDegreeOfParallelism = new AsyncLocal<int?>();

        /// <summary>
        /// Reports that the analysis has started.
        /// </summary>
        /// <param name="progressReporter">The reporter to update, or null when progress is not observed.</param>
        internal static void ReportStarting(SafeProgressReporter? progressReporter)
        {
            progressReporter?.ReportProgress(Starting);
        }

        /// <summary>
        /// Reports that the analysis is assembling final results.
        /// </summary>
        /// <param name="progressReporter">The reporter to update, or null when progress is not observed.</param>
        internal static void ReportProcessingResults(SafeProgressReporter? progressReporter)
        {
            progressReporter?.ReportProgress(ProcessingResults);
        }

        /// <summary>
        /// Reports that the analysis has fully completed.
        /// </summary>
        /// <param name="progressReporter">The reporter to update, or null when progress is not observed.</param>
        internal static void ReportComplete(SafeProgressReporter? progressReporter)
        {
            progressReporter?.ReportProgress(Complete);
        }

        /// <summary>
        /// Creates a child reporter that maps estimator progress onto the main estimator phase.
        /// </summary>
        /// <param name="progressReporter">The parent reporter, or null when progress is not observed.</param>
        /// <param name="taskName">The child task name used by the progress reporter.</param>
        /// <returns>A child reporter that ends at <see cref="EstimationComplete"/>, or null.</returns>
        internal static SafeProgressReporter? CreateEstimatorReporter(SafeProgressReporter? progressReporter, string taskName)
        {
            return progressReporter?.CreateProgressModifier((float)(EstimationComplete / Complete), taskName);
        }

        /// <summary>
        /// Creates a child reporter that maps a custom progress phase onto the parent reporter.
        /// </summary>
        /// <param name="progressReporter">The parent reporter, or null when progress is not observed.</param>
        /// <param name="startProgress">The parent progress value at the start of the phase.</param>
        /// <param name="endProgress">The parent progress value at the end of the phase.</param>
        /// <param name="taskName">The child task name used by the progress reporter.</param>
        /// <returns>A child reporter for the requested progress range, or null.</returns>
        internal static SafeProgressReporter? CreatePhaseReporter(
            SafeProgressReporter? progressReporter,
            double startProgress,
            double endProgress,
            string taskName)
        {
            progressReporter?.ReportProgress(startProgress);
            double fraction = Math.Max(0.0, Math.Min(Complete, endProgress - startProgress)) / Complete;
            return progressReporter?.CreateProgressModifier((float)fraction, taskName);
        }

        /// <summary>
        /// Determines whether a sampling loop should emit a progress report for the given
        /// completed-iteration count.
        /// </summary>
        /// <param name="current">The number of completed iterations (1-based).</param>
        /// <param name="total">The total number of iterations.</param>
        /// <returns>True on the first and last iterations and at every whole-percent boundary; false when <paramref name="total"/> is not positive.</returns>
        /// <remarks>
        /// Reporting at <c>current == 1</c> guarantees visible progress as soon as the first
        /// replicate completes; without it the first tick waits for <c>total / 100</c>
        /// completions, which pins the progress bar at the phase-start value whenever the
        /// replicates are slow (e.g., bootstrap refits that need retries).
        /// </remarks>
        internal static bool ShouldReportLoopProgress(int current, int total)
        {
            if (total <= 0) return false;
            if (current == 1 || current == total) return true;
            return current % Math.Max(1, total / 100) == 0;
        }

        /// <summary>
        /// Applies an ambient maximum degree of parallelism for nested analysis loops.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">The maximum degree of parallelism to use while the returned scope is active.</param>
        /// <returns>A scope that restores the previous ambient value when disposed.</returns>
        internal static IDisposable UseMaxDegreeOfParallelism(int maxDegreeOfParallelism)
        {
            int? previous = AmbientMaxDegreeOfParallelism.Value;
            AmbientMaxDegreeOfParallelism.Value = Math.Max(1, maxDegreeOfParallelism);
            return new AmbientParallelismScope(previous);
        }

        /// <summary>
        /// Creates <see cref="ParallelOptions"/> using the current ambient inner parallelism limit.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to attach to the options.</param>
        /// <returns>Parallel options configured for the current analysis context.</returns>
        internal static ParallelOptions CreateParallelOptions(CancellationToken cancellationToken = default)
        {
            return new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = AmbientMaxDegreeOfParallelism.Value ?? Environment.ProcessorCount
            };
        }

        /// <summary>
        /// Restores the prior ambient parallelism value.
        /// </summary>
        private sealed class AmbientParallelismScope : IDisposable
        {
            /// <summary>
            /// The ambient value that was active before this scope.
            /// </summary>
            private readonly int? _previous;

            /// <summary>
            /// Initializes a new instance of the <see cref="AmbientParallelismScope"/> class.
            /// </summary>
            /// <param name="previous">The value to restore when the scope is disposed.</param>
            internal AmbientParallelismScope(int? previous)
            {
                _previous = previous;
            }

            /// <summary>
            /// Restores the previous ambient maximum degree of parallelism.
            /// </summary>
            public void Dispose()
            {
                AmbientMaxDegreeOfParallelism.Value = _previous;
            }
        }
    }
}
