using Numerics.Data.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents an item in a combo box for selecting statistical dependency types between variables.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display properties and underlying value for dependency type options
    /// presented to users in the GUI, facilitating the selection of correlation and dependence structures
    /// in statistical analyses.
    /// </remarks>
    public class DependencyItem
    {
        /// <summary>
        /// Gets the user-friendly display name for the dependency type shown in the combo box.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the underlying enumeration value representing the specific dependency type.
        /// </summary>
        public Probability.DependencyType Value { get; }

        /// <summary>
        /// Gets the tooltip text that provides additional information about the dependency type when the user hovers over the item.
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The dependency type enumeration value this item represents.</param>
        /// <param name="tooltip">Optional tooltip text providing additional information about the dependency type. Defaults to an empty string.</param>
        public DependencyItem(string displayName, Probability.DependencyType value, string tooltip = "")
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = tooltip;
        }
    }
}
