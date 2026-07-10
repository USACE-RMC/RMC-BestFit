using RMC.BestFit.Estimation;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents an item in a combo box for selecting MCMC sampler types used in Bayesian analysis.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display properties and underlying value for sampler type options
    /// presented to users in the GUI. Each sampler type has different characteristics for exploring
    /// posterior distributions in Bayesian MCMC analysis.
    /// </remarks>
    public class SamplerTypeItem
    {
        /// <summary>
        /// Gets the user-friendly display name for the sampler type shown in the combo box.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the underlying enumeration value representing the specific sampler type.
        /// </summary>
        public BayesianAnalysis.SamplerType Value { get; }

        /// <summary>
        /// Gets the tooltip text that provides additional information about the sampler type when the user hovers over the item.
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SamplerTypeItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The sampler type enumeration value this item represents.</param>
        /// <param name="tooltip">Optional tooltip text providing additional information about the sampler type. Defaults to an empty string.</param>
        public SamplerTypeItem(string displayName, BayesianAnalysis.SamplerType value, string tooltip = "")
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = tooltip;
        }
    }
}
