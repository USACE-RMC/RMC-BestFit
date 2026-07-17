using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a combo box item that encapsulates copula type selection for the user interface.
    /// This class provides a display-friendly representation of copula types used in multivariate statistical analysis.
    /// </summary>
    public class CopulaItem
    {
        /// <summary>
        /// Gets or sets the human-readable name displayed to users in the combo box.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the underlying copula type enumeration value associated with this item.
        /// </summary>
        public CopulaType Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopulaItem"/> class with the specified display name and copula type.
        /// </summary>
        /// <param name="displayName">The human-readable name to display in the combo box.</param>
        /// <param name="value">The copula type enumeration value this item represents.</param>
        public CopulaItem(string displayName, CopulaType value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
}
