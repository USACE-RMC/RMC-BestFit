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
    /// Represents a selectable composite analysis type item for use in combo box controls.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display name, composite type value, and optional tooltip
    /// for composite analysis types, enabling user-friendly selection in the GUI.
    /// </remarks>
    public class CompositeTypeItem
    {
        /// <summary>
        /// Gets the display name shown to the user in the combo box.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the underlying composite type enumeration value.
        /// </summary>
        public ModelAnalyses.CompositeType Value { get; }

        /// <summary>
        /// Gets the tooltip text that provides additional information about the composite type.
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeTypeItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly display name for the composite type.</param>
        /// <param name="value">The underlying composite type enumeration value.</param>
        /// <param name="tooltip">Optional tooltip text providing additional information. Defaults to an empty string.</param>
        public CompositeTypeItem(string displayName, ModelAnalyses.CompositeType value, string tooltip = "")
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = tooltip;
        }
    }

}

