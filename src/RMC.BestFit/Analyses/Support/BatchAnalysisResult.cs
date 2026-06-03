namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Contains the outcome of a single <see cref="IAnalysis"/> within a batch run
    /// executed by <see cref="BatchAnalysisRunner"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Each <see cref="BatchAnalysisResult"/> records whether the analysis succeeded,
    /// was canceled, or failed with an exception, along with its wall-clock duration.
    /// A collection of these results is returned by
    /// <see cref="BatchAnalysisRunner.RunAsync"/> and also provided in the
    /// <see cref="BatchAnalysisRunner.BatchCompleted"/> event.
    /// </para>
    /// </remarks>
    public class BatchAnalysisResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BatchAnalysisResult"/> class.
        /// </summary>
        /// <param name="analysis">The analysis that was executed.</param>
        /// <param name="succeeded">Whether the analysis completed successfully.</param>
        /// <param name="wasCanceled">Whether the analysis was canceled.</param>
        /// <param name="error">The exception that caused the failure, or <c>null</c>.</param>
        /// <param name="duration">The wall-clock time the analysis took to execute.</param>
        public BatchAnalysisResult(IAnalysis analysis, bool succeeded, bool wasCanceled,
            Exception? error, TimeSpan duration)
        {
            Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
            Succeeded = succeeded;
            WasCanceled = wasCanceled;
            Error = error;
            Duration = duration;
        }

        /// <summary>
        /// Gets the <see cref="IAnalysis"/> instance that was executed.
        /// </summary>
        public IAnalysis Analysis { get; }

        /// <summary>
        /// Gets a value indicating whether the analysis completed successfully.
        /// </summary>
        /// <value>
        /// <c>true</c> if the analysis ran to completion without errors or cancellation;
        /// otherwise <c>false</c>.
        /// </value>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets a value indicating whether the analysis was canceled before completion.
        /// </summary>
        /// <value>
        /// <c>true</c> if the analysis was canceled via a <see cref="CancellationToken"/>
        /// or by calling <see cref="IAnalysis.CancelAnalysis"/>; otherwise <c>false</c>.
        /// </value>
        public bool WasCanceled { get; }

        /// <summary>
        /// Gets the exception that caused the analysis to fail, or <c>null</c> if
        /// the analysis succeeded or was canceled.
        /// </summary>
        public Exception? Error { get; }

        /// <summary>
        /// Gets the wall-clock duration of the analysis execution.
        /// </summary>
        /// <remarks>
        /// The duration is measured from immediately before <see cref="IAnalysis.RunAsync"/>
        /// is called to immediately after it completes (whether by success, failure, or cancellation).
        /// </remarks>
        public TimeSpan Duration { get; }
    }
}
