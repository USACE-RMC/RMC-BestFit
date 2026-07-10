using Numerics.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item that encapsulates mathematical function type selection for the user interface.
    /// This class provides a display-friendly representation of different mathematical functions available for data transformation and analysis.
    /// </summary>
    public class MathFunctionItem
    {
        /// <summary>
        /// Gets or sets the human-readable name displayed to users in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying mathematical function type enumeration value associated with this item.
        /// </summary>
        public MathFunctionType Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MathFunctionItem"/> class with the specified display name and mathematical function type.
        /// </summary>
        /// <param name="displayName">The human-readable name to display in the combo box.</param>
        /// <param name="value">The mathematical function type enumeration value this item represents.</param>
        public MathFunctionItem(string displayName, MathFunctionType value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
