namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item for selecting plotting position formulas in the user interface.
    /// </summary>
    /// <remarks>
    /// This class is used to bind plotting position formula options to combo boxes, providing a user-friendly
    /// display name, the formula's alpha parameter value, and a descriptive tooltip for additional context.
    /// Plotting positions are used to estimate the cumulative probability of data points in statistical analysis.
    /// </remarks>
    public class PlottingPositionItem
    {
        /// <summary>
        /// Gets or sets the user-friendly name displayed in the combo box for the plotting position formula.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the alpha parameter value used in the plotting position formula.
        /// </summary>
        /// <remarks>
        /// The plotting position is typically calculated as (i - alpha) / (n + 1 - 2*alpha),
        /// where i is the rank and n is the sample size. Different formulas use different alpha values
        /// (e.g., Weibull uses 0, Median uses 0.3, Cunnane uses 0.4).
        /// </remarks>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets the tooltip text that provides additional information about the plotting position formula.
        /// </summary>
        public string ToolTip { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlottingPositionItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The alpha parameter value for the plotting position formula.</param>
        /// <param name="tooltip">The tooltip text providing additional information about the formula.</param>
        public PlottingPositionItem(string displayName, double value, string tooltip)
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = tooltip;
        }
    }
}
