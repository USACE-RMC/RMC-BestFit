using RMC.BestFit.Models;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item for selecting ARIMAX covariate-extension methods in the user interface.
    /// </summary>
    /// <remarks>
    /// This class is used to bind covariate-extension options to combo boxes, providing a user-friendly
    /// display name and the corresponding <see cref="ARIMAX.CovariateExtensionMethod"/> enumeration value.
    /// The extension method controls how covariate time series are lengthened past the observed range when
    /// the ARIMAX model forecasts beyond the training data.
    /// </remarks>
    public class CovariateExtensionItem
    {
        /// <summary>
        /// Gets or sets the user-friendly name displayed in the combo box for the covariate-extension method.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ARIMAX.CovariateExtensionMethod"/> enumeration value associated with this combo box item.
        /// </summary>
        public ARIMAX.CovariateExtensionMethod Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CovariateExtensionItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The <see cref="ARIMAX.CovariateExtensionMethod"/> enumeration value associated with this item.</param>
        public CovariateExtensionItem(string displayName, ARIMAX.CovariateExtensionMethod value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
