using System;

namespace RMC_BestFit
{
    /// <summary>
    /// Maps batch-run statuses to application resource keys.
    /// </summary>
    /// <remarks>
    /// Keeping the mapping separate from the WPF converter lets unit tests assert every
    /// supported status without creating an <see cref="System.Windows.Application"/>.
    /// </remarks>
    internal static class BatchRunStatusIconKeys
    {
        /// <summary>
        /// Gets the icon resource key for a batch-run status.
        /// </summary>
        /// <param name="status">The status to map.</param>
        /// <returns>The application resource key for the status icon.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="status"/> is not supported.</exception>
        internal static string ForStatus(BatchRunStatus status)
        {
            return status switch
            {
                BatchRunStatus.Pending => "StatusPendingIcon",
                BatchRunStatus.Running => "StatusRunningIcon",
                BatchRunStatus.Succeeded => "StatusSucceededIcon",
                BatchRunStatus.Failed => "StatusFailedIcon",
                BatchRunStatus.Canceled => "StatusCanceledIcon",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported batch-run status.")
            };
        }
    }
}
