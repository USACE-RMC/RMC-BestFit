using RMC.BestFit.Analyses;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item that encapsulates Bulletin 17C uncertainty option selection for the user interface.
    /// This class provides a display-friendly representation of different uncertainty quantification methods used in Bulletin 17C flood frequency analysis.
    /// </summary>
    public class B17CUncertaintyOptionItem
    {
        /// <summary>
        /// Gets the human-readable name displayed to users in the combo box.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the underlying Bulletin 17C uncertainty option enumeration value associated with this item.
        /// </summary>
        public UncertaintyMethod Value { get; }

        /// <summary>
        /// Gets the tooltip text that provides additional information about the uncertainty option when users hover over the item.
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="B17CUncertaintyOptionItem"/> class with the specified display name, uncertainty option, and optional tooltip.
        /// </summary>
        /// <param name="displayName">The human-readable name to display in the combo box.</param>
        /// <param name="value">The Bulletin 17C uncertainty option enumeration value this item represents.</param>
        /// <param name="tooltip">Optional tooltip text providing additional information about this uncertainty option. Defaults to an empty string if not provided.</param>
        public B17CUncertaintyOptionItem(string displayName, UncertaintyMethod value, string tooltip = "")
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = tooltip;
        }
    }
}
