using Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a selectable univariate distribution item for use in combo box controls.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display name and corresponding enumeration value for
    /// univariate distribution types, enabling user-friendly selection in the GUI.
    /// </remarks>
    public class UnivariateDistributionItem
    {
        /// <summary>
        /// Gets or sets the display name shown to the user in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying univariate distribution type enumeration value.
        /// </summary>
        public UnivariateDistributionType Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnivariateDistributionItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly display name for the distribution.</param>
        /// <param name="value">The underlying univariate distribution type enumeration value.</param>
        public UnivariateDistributionItem(string displayName, UnivariateDistributionType value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
