using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item for selecting credible interval confidence levels in the user interface.
    /// </summary>
    /// <remarks>
    /// This class is used to bind credible interval options (e.g., 90%, 95%, 99%) to combo boxes,
    /// providing a user-friendly display name and the corresponding probability value as a decimal.
    /// </remarks>
    public class CredibleIntervalItem
    {
        /// <summary>
        /// Gets or sets the user-friendly name displayed in the combo box for the credible interval.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the credible interval probability value (typically between 0.0 and 1.0).
        /// </summary>
        /// <remarks>
        /// For example, a 95% credible interval would have a value of 0.95.
        /// </remarks>
        public double Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CredibleIntervalItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The credible interval probability value associated with this item.</param>
        public CredibleIntervalItem(string displayName, double value)
        {
            DisplayName = displayName;
            Value = value;
        }

    }
}
