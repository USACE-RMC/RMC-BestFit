using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.UI;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item for selecting depth measurement units in the user interface.
    /// </summary>
    /// <remarks>
    /// This class is used to bind depth unit options to combo boxes, providing a user-friendly
    /// display name and the corresponding TimeSeriesDownload.DepthUnit enumeration value.
    /// Depth units are typically used for precipitation or water level measurements.
    /// </remarks>
    public class DepthUnitItem
    {
        /// <summary>
        /// Gets or sets the user-friendly name displayed in the combo box for the depth unit.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the TimeSeriesDownload.DepthUnit enumeration value associated with this combo box item.
        /// </summary>
        public TimeSeriesDownload.DepthUnit Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DepthUnitItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The TimeSeriesDownload.DepthUnit enumeration value associated with this item.</param>
        public DepthUnitItem(string displayName, TimeSeriesDownload.DepthUnit value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
