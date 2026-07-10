using RMC.BestFit.Models;
using RMC.BestFit.UI;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item that encapsulates time series data entry method selection for the user interface.
    /// This class provides a display-friendly representation of different methods for entering and managing time series data.
    /// </summary>
    public class TimeSeriesEntryMethodItem
    {
        /// <summary>
        /// Gets or sets the human-readable name displayed to users in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying time series entry method enumeration value associated with this item.
        /// </summary>
        public TimeSeriesElement.TimeSeriesEntryMethod Value { get; set; }

        /// <summary>
        /// Gets or sets the tooltip text displayed when the user hovers over this item in the combo box.
        /// </summary>
        public string ToolTip { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSeriesEntryMethodItem"/> class with the specified display name, time series entry method, and optional tooltip.
        /// </summary>
        /// <param name="displayName">The human-readable name to display in the combo box.</param>
        /// <param name="value">The time series entry method enumeration value this item represents.</param>
        /// <param name="toolTip">Optional tooltip text describing the entry method.</param>
        public TimeSeriesEntryMethodItem(string displayName, TimeSeriesElement.TimeSeriesEntryMethod value, string toolTip = "")
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = toolTip;
        }
    }

}
