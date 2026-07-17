using Numerics.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a selectable time block window item for use in combo box controls.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display name and corresponding time block window value,
    /// enabling user-friendly selection of time blocking strategies in the GUI.
    /// </remarks>
    public class TimeBlockItem
    {
        /// <summary>
        /// Gets or sets the display name shown to the user in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying time block window value.
        /// </summary>
        public TimeBlockWindow Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeBlockItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly display name for the time block window.</param>
        /// <param name="value">The underlying time block window value.</param>
        public TimeBlockItem(string displayName, TimeBlockWindow value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
