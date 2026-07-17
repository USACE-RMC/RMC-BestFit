using Numerics.Data.Statistics;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents an item in a combo box for selecting point estimator types used in Bayesian analysis.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the display properties and underlying value for point estimate type options
    /// presented to users in the GUI. Point estimators provide single-value estimates from posterior distributions
    /// in Bayesian statistical analysis, such as the mean, median, or mode.
    /// </remarks>
    public class PointEstimatorItem
    {
        /// <summary>
        /// Gets the user-friendly display name for the point estimator type shown in the combo box.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the underlying enumeration value representing the specific point estimate type.
        /// </summary>
        public BayesianAnalysis.PointEstimateType Value { get; }

        /// <summary>
        /// Gets the tooltip text that provides additional information about the point estimator type when the user hovers over the item.
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PointEstimatorItem"/> class.
        /// </summary>
        /// <param name="displayName">The user-friendly name to display in the combo box.</param>
        /// <param name="value">The point estimate type enumeration value this item represents.</param>
        /// <param name="tooltip">Optional tooltip text providing additional information about the point estimator type. Defaults to an empty string.</param>
        public PointEstimatorItem(string displayName, BayesianAnalysis.PointEstimateType value, string tooltip = "")
        {
            DisplayName = displayName;
            Value = value;
            ToolTip = tooltip;
        }


    }
}
