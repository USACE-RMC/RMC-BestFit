using RMC.BestFit.Models;
using RMC.BestFit.UI;
using ModelAnalyses = RMC.BestFit.Analyses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents an item in a combo box for selecting averaging methods used in composite distribution analysis.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display properties and underlying value for averaging method options
    /// presented to users in the GUI, providing a user-friendly interface for method selection.
    /// </remarks>
    public class AverageMethodItem
    {
        /// <summary>
        /// Gets the user-friendly display name for the averaging method shown in the combo box.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the underlying enumeration value representing the specific averaging method.
        /// </summary>
        public ModelAnalyses.AverageMethod Value { get; }

        /// <summary>
        /// Gets the tooltip text that provides additional information about the averaging method when the user hovers over the item.
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AverageMethodItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The averaging method enumeration value this item represents.</param>
        /// <param name="tooltip">Optional tooltip text providing additional information about the averaging method. Defaults to an empty string.</param>
        public AverageMethodItem(string displayName, ModelAnalyses.AverageMethod value, string tooltip = "")
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = tooltip;
        }
    }
}
