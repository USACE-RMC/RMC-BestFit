using RMC.BestFit.Models;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item for selecting data transformation types in the user interface.
    /// </summary>
    /// <remarks>
    /// This class is used to bind transformation type options to combo boxes, providing a user-friendly
    /// display name and the corresponding Transform enumeration value. Transformations are applied to
    /// data before statistical analysis (e.g., logarithmic, Box-Cox, normal score transformations).
    /// </remarks>
    public class TransformTypeItem
    {
        /// <summary>
        /// Gets or sets the user-friendly name displayed in the combo box for the transformation type.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the Transform enumeration value associated with this combo box item.
        /// </summary>
        public Transform Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformTypeItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The Transform enumeration value associated with this item.</param>
        public TransformTypeItem(string displayName, Transform value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
