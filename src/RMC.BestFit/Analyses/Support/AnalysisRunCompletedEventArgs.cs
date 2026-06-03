using System;
using System.ComponentModel;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Provides data for the <see cref="IAnalysis.AnalysisCompleted"/> event.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public sealed class AnalysisRunCompletedEventArgs : AsyncCompletedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AnalysisRunCompletedEventArgs"/> class.
        /// </summary>
        /// <param name="wasCanceled">
        /// <c>true</c> if the run was canceled; otherwise <c>false</c>.
        /// </param>
        /// <param name="succeeded">
        /// <c>true</c> if the run completed successfully; otherwise <c>false</c>.
        /// </param>
        /// <param name="error">
        /// The exception that caused the run to terminate, or <c>null</c> if the
        /// run did not fault.
        /// </param>
        public AnalysisRunCompletedEventArgs(bool wasCanceled, bool succeeded, Exception? error)
            : base(error, wasCanceled, null)
        {
            Succeeded = succeeded;
        }

        /// <summary>
        /// Gets a value indicating whether the analysis completed successfully.
        /// </summary>
        public bool Succeeded { get; }
    }
}

