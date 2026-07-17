namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item for selecting months in the user interface.
    /// </summary>
    /// <remarks>
    /// This class is used to bind month options to combo boxes, providing a user-friendly
    /// display name (e.g., "January", "February") and the corresponding numeric month value.
    /// </remarks>
    public class MonthItem
    {
        /// <summary>
        /// Gets or sets the user-friendly name displayed in the combo box for the month.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the numeric value representing the month (typically 1-12, where 1 is January).
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonthItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The numeric month value associated with this item.</param>
        public MonthItem(string displayName, int value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
