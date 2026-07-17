using Numerics.Data;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents an item in a combo box for selecting time series data types for download and analysis.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display properties and underlying value for time series type options
    /// presented to users in the GUI, enabling selection of different types of time series data sources
    /// for statistical analysis.
    /// </remarks>
    public class TimeSeriesTypeItem
    {
        /// <summary>
        /// Gets or sets the user-friendly display name for the time series type shown in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying enumeration value representing the specific time series type.
        /// </summary>
        public TimeSeriesDownload.TimeSeriesType Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSeriesTypeItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The time series type enumeration value this item represents.</param>
        public TimeSeriesTypeItem(string displayName, TimeSeriesDownload.TimeSeriesType value)
        {
            DisplayName = displayName;
            Value = value;

        }
    }
}
