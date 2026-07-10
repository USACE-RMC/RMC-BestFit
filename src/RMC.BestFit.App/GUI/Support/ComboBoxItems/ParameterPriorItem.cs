using Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a parameter prior distribution item for Bayesian analysis.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the name, distribution, and display label for parameter priors
    /// used in Bayesian statistical analysis, enabling configuration of prior distributions for model parameters.
    /// </remarks>
    public class ParameterPriorItem
    {
        /// <summary>
        /// Gets or sets the name of the parameter.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the prior distribution associated with the parameter.
        /// </summary>
        public UnivariateDistributionBase Distribution { get; set; }

        /// <summary>
        /// Gets or sets the display label for the parameter prior in the user interface.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterPriorItem"/> class.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="distribution">The prior distribution for the parameter.</param>
        /// <param name="label">The display label for the parameter prior.</param>
        public ParameterPriorItem(string name, UnivariateDistributionBase distribution, string label)
        {
            Name = name;
            Distribution = distribution;
            Label = label;
        }
    }
}
