using RMC.BestFit.Models;
using RMC.BestFit.UI;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item that encapsulates exact data entry method selection for the user interface.
    /// This class provides a display-friendly representation of different methods for entering exact data values.
    /// </summary>
    public class ExactDataMethodItem
    {
        /// <summary>
        /// Gets or sets the human-readable name displayed to users in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying exact data entry type enumeration value associated with this item.
        /// </summary>
        public InputData.ExactDataEntryType Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExactDataMethodItem"/> class with the specified display name and exact data entry type.
        /// </summary>
        /// <param name="displayName">The human-readable name to display in the combo box.</param>
        /// <param name="value">The exact data entry type enumeration value this item represents.</param>
        public ExactDataMethodItem(string displayName, InputData.ExactDataEntryType value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
