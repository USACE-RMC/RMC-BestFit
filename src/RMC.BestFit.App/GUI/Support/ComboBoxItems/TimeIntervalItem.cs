using Numerics.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item for selecting time intervals in the user interface.
    /// </summary>
    /// <remarks>
    /// This class is used to bind time interval options to combo boxes, providing a user-friendly
    /// display name and the corresponding TimeInterval enumeration value.
    /// </remarks>
    public class TimeIntervalItem
    {
        /// <summary>
        /// Gets or sets the user-friendly name displayed in the combo box for the time interval.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the TimeInterval enumeration value associated with this combo box item.
        /// </summary>
        public TimeInterval Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeIntervalItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The TimeInterval enumeration value associated with this item.</param>
        public TimeIntervalItem(string displayName, TimeInterval value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
