using Numerics.Distributions.Copulas;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents an item in a combo box for selecting copula parameter estimation methods.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display properties and underlying value for copula estimation method options
    /// presented to users in the GUI. Copulas are used to model the dependence structure between random variables,
    /// and different estimation methods can be employed to fit copula parameters to data.
    /// </remarks>
    public class CopulaEstimationItem
    {
        /// <summary>
        /// Gets the user-friendly display name for the copula estimation method shown in the combo box.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the underlying enumeration value representing the specific copula estimation method.
        /// </summary>
        public CopulaEstimationMethod Value { get; }

        /// <summary>
        /// Gets the tooltip text that provides additional information about the copula estimation method when the user hovers over the item.
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopulaEstimationItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The copula estimation method enumeration value this item represents.</param>
        /// <param name="tooltip">Optional tooltip text providing additional information about the copula estimation method. Defaults to an empty string.</param>
        public CopulaEstimationItem(string displayName, CopulaEstimationMethod value, string tooltip = "")
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = tooltip;
        }
    }
}
