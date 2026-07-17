using Numerics.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item that encapsulates block function type selection for the user interface.
    /// This class provides a display-friendly representation of different block aggregation functions used for statistical analysis of grouped data.
    /// </summary>
    public class BlockFunctionItem
    {
        /// <summary>
        /// Gets or sets the human-readable name displayed to users in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying block function type enumeration value associated with this item.
        /// </summary>
        public BlockFunctionType Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockFunctionItem"/> class with the specified display name and block function type.
        /// </summary>
        /// <param name="displayName">The human-readable name to display in the combo box.</param>
        /// <param name="value">The block function type enumeration value this item represents.</param>
        public BlockFunctionItem(string displayName, BlockFunctionType value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
