using Numerics.Utilities;
using System.ComponentModel;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Interface for all analyses.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface IAnalysis : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs immediately before <see cref="RunAsync(SafeProgressReporter)"/> 
        /// begins executing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Handlers can set <see cref="CancelEventArgs.Cancel"/> to <c>true</c> 
        /// to prevent the analysis from starting. This is the recommended place 
        /// for the GUI to perform last minute checks or to prompt the user.
        /// </para>
        /// </remarks>
        event EventHandler<CancelEventArgs> AnalysisStarting;

        /// <summary>
        /// Occurs after <see cref="RunAsync(SafeProgressReporter)"/> has completed,
        /// either successfully, with an error, or due to cancellation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="AnalysisRunCompletedEventArgs"/> argument indicates whether 
        /// the run completed successfully, was cancelled, or terminated due to an error.
        /// The GUI can use this event to update status indicators and enable or disable 
        /// commands.
        /// </para>
        /// </remarks>
        event EventHandler<AnalysisRunCompletedEventArgs> AnalysisCompleted;

        /// <summary>
        /// Run the analysis.
        /// </summary>
        Task RunAsync(SafeProgressReporter? progressReporter = null);

        /// <summary>
        /// Cancel the analysis.
        /// </summary>
        void CancelAnalysis();

        /// <summary>
        /// Determines whether the analysis has been estimated.
        /// </summary>
        bool IsEstimated { get; }

        /// <summary>
        /// Validates the current state of the object and reports any issues found.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item>
        /// <description><c>IsValid</c>: <c>true</c> if the object passes all validation checks; otherwise <c>false</c>.</description>
        /// </item>
        /// <item>
        /// <description><c>ValidationMessages</c>: a list of messages describing any validation errors or warnings. This list is empty when the object is valid.</description>
        /// </item>
        /// </list>
        /// </returns>
        (bool IsValid, List<string> ValidationMessages) Validate();
    }
}
