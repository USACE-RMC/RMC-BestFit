using Numerics.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a selectable smoothing function item for use in combo box controls.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display name and corresponding enumeration value for
    /// smoothing function types, enabling user-friendly selection in the GUI.
    /// </remarks>
    public class SmoothingFunctionItem
    {
        /// <summary>
        /// Gets or sets the display name shown to the user in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying smoothing function type enumeration value.
        /// </summary>
        public SmoothingFunctionType Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmoothingFunctionItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly display name for the smoothing function.</param>
        /// <param name="value">The underlying smoothing function type enumeration value.</param>
        public SmoothingFunctionItem(string displayName, SmoothingFunctionType value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
