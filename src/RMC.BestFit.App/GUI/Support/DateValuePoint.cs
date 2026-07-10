using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a data point that associates a date and time with a numerical value.
    /// </summary>
    /// <remarks>
    /// This class is commonly used for time-series data, where observations are recorded at specific
    /// points in time. It is particularly useful for plotting temporal data or analyzing trends over time.
    /// </remarks>
    public class DateValuePoint
    {
        /// <summary>
        /// Gets or sets the date and time of the observation.
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Gets or sets the numerical value associated with the date and time.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DateValuePoint"/> class.
        /// </summary>
        /// <param name="dateTime">The date and time of the observation.</param>
        /// <param name="value">The numerical value associated with the date and time.</param>
        public DateValuePoint(DateTime dateTime, double value)
        {
            DateTime = dateTime;
            Value = value;
        }
    }
}
