using RMC.BestFit.Estimation;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a selectable Generalized Method of Moments (GMM) estimation strategy item for use in combo box controls.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display name, GMM estimation strategy value, and optional tooltip
    /// for GMM estimation types, enabling user-friendly selection in the GUI.
    /// </remarks>
    public class GMMEstimationTypeItem
    {
        /// <summary>
        /// Gets the display name shown to the user in the combo box.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the underlying GMM estimation strategy enumeration value.
        /// </summary>
        public GeneralizedMethodOfMoments.GMMEstimationStrategy Value { get; }

        /// <summary>
        /// Gets the tooltip text that provides additional information about the estimation strategy.
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GMMEstimationTypeItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly display name for the GMM estimation strategy.</param>
        /// <param name="value">The underlying GMM estimation strategy enumeration value.</param>
        /// <param name="tooltip">Optional tooltip text providing additional information. Defaults to an empty string.</param>
        public GMMEstimationTypeItem(string displayName, GeneralizedMethodOfMoments.GMMEstimationStrategy value, string tooltip = "")
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = tooltip;
        }
    }
}

