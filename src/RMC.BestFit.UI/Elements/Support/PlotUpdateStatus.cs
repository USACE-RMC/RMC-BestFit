namespace RMC.BestFit.UI
{
    /// <summary>
    /// Result status from plot update methods, allowing the caller to handle UI-specific
    /// responses (e.g., toggling warning visibility).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// <b>Caller invariant:</b> when an update method returns <see cref="InsufficientData"/> or
    /// <see cref="MissingData"/>, the plot has already been cleared of stale series. Callers do
    /// not need to clear the plot themselves — only update the warning visibility / message bar
    /// state in response to the returned status.
    /// </para>
    /// </remarks>
    public enum PlotUpdateStatus
    {
        /// <summary>Plot updated successfully with data.</summary>
        Success,

        /// <summary>Insufficient data to compute the plot. The plot has been cleared to empty.</summary>
        InsufficientData,

        /// <summary>Missing values detected; the plot cannot be computed. The plot has been cleared to empty.</summary>
        MissingData
    }
}
