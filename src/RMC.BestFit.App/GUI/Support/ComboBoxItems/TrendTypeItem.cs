using RMC.BestFit.Models;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item for selecting ARIMAX trend types in the user interface.
    /// </summary>
    /// <remarks>
    /// This class is used to bind trend type options to combo boxes, providing a user-friendly
    /// display name and the corresponding ARIMAX.Trend enumeration value.
    /// </remarks>
    public class TrendTypeItem
    {
        /// <summary>
        /// Gets or sets the user-friendly name displayed in the combo box for the trend type.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the ARIMAX.Trend enumeration value associated with this combo box item.
        /// </summary>
        public ARIMAX.Trend Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendTypeItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The ARIMAX.Trend enumeration value associated with this item.</param>
        public TrendTypeItem(string displayName, ARIMAX.Trend value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
