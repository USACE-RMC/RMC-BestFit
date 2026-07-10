using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RMC.BestFit.Models;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a statistical measure for a fitted probability distribution.
    /// </summary>
    /// <remarks>
    /// This class encapsulates a named statistic with one or more numerical values and associated tooltips
    /// for display in the user interface. It is typically used to present goodness-of-fit measures,
    /// parameter estimates, or other distribution-related statistics.
    /// </remarks>
    public class FittedDistributionStatistic
    {
        /// <summary>
        /// Gets the name of the statistic.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the array of numerical values for this statistic.
        /// </summary>
        /// <remarks>
        /// Multiple values may be provided when the statistic represents a collection of related measures,
        /// such as parameter estimates or multiple test results.
        /// </remarks>
        public double[] Value { get; }

        /// <summary>
        /// Gets the array of tooltip strings corresponding to each value.
        /// </summary>
        /// <remarks>
        /// Tooltips provide additional information or context for each statistic value when displayed in the user interface.
        /// </remarks>
        public string[] ToolTip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FittedDistributionStatistic"/> class.
        /// </summary>
        /// <param name="name">The name of the statistic.</param>
        /// <param name="value">An array of numerical values for the statistic.</param>
        /// <param name="toolTip">An array of tooltip strings for each value.</param>
        public FittedDistributionStatistic(string name, double[] value, string[] toolTip)
        {
            Name = name;
            Value = value;
            ToolTip = toolTip;
        }
    }
}
