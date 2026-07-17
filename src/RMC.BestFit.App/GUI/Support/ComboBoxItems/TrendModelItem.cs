using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item that encapsulates trend model type selection for the user interface.
    /// This class provides a display-friendly representation of different statistical trend models available for time series analysis.
    /// </summary>
    public class TrendModelItem
    {
        /// <summary>
        /// Gets or sets the human-readable name displayed to users in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying trend model type enumeration value associated with this item.
        /// </summary>
        public TrendModelType Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendModelItem"/> class with the specified display name and trend model type.
        /// </summary>
        /// <param name="displayName">The human-readable name to display in the combo box.</param>
        /// <param name="value">The trend model type enumeration value this item represents.</param>
        public TrendModelItem(string displayName, TrendModelType value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
