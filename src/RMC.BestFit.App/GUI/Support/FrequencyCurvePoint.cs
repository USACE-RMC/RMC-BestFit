using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a point on a frequency curve with associated confidence bounds and statistical measures.
    /// </summary>
    /// <remarks>
    /// This class encapsulates data for a single point on a frequency curve, including upper and lower confidence bounds,
    /// predictive intervals, mode values, and optional coordinate or datetime information.
    /// </remarks>
    public class FrequencyCurvePoint
    {
        /// <summary>
        /// Gets or sets the probability value for this frequency curve point.
        /// </summary>
        public double Probability { get; set; }

        /// <summary>
        /// Gets or sets the upper confidence bound value.
        /// </summary>
        public double Upper { get; set; }

        /// <summary>
        /// Gets or sets the lower confidence bound value.
        /// </summary>
        public double Lower { get; set; }

        /// <summary>
        /// Gets or sets the predictive interval value.
        /// </summary>
        public double Predictive { get; set; }

        /// <summary>
        /// Gets or sets the mode value for the distribution.
        /// </summary>
        public double Mode { get; set; }

        /// <summary>
        /// Gets or sets the X-coordinate value for plotting purposes.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Gets or sets the Y-coordinate value for plotting purposes.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Gets or sets the date and time associated with this frequency curve point.
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrequencyCurvePoint"/> class with probability and statistical bounds.
        /// </summary>
        /// <param name="probability">The probability value for this point.</param>
        /// <param name="upper">The upper confidence bound.</param>
        /// <param name="lower">The lower confidence bound.</param>
        /// <param name="predictive">The predictive interval value.</param>
        /// <param name="mode">The mode value of the distribution.</param>
        public FrequencyCurvePoint(double probability, double upper, double lower, double predictive, double mode)
        {
            Probability = probability;
            Upper = upper;
            Lower = lower;
            Predictive = predictive;
            Mode = mode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrequencyCurvePoint"/> class with X-Y coordinates and statistical bounds.
        /// </summary>
        /// <param name="x">The X-coordinate value.</param>
        /// <param name="y">The Y-coordinate value.</param>
        /// <param name="upper">The upper confidence bound.</param>
        /// <param name="lower">The lower confidence bound.</param>
        /// <param name="predictive">The predictive interval value.</param>
        /// <param name="mode">The mode value of the distribution.</param>
        public FrequencyCurvePoint(double x, double y, double upper, double lower, double predictive, double mode)
        {
            X = x;
            Y = y;
            Upper = upper;
            Lower = lower;
            Predictive = predictive;
            Mode = mode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrequencyCurvePoint"/> class with a datetime value and statistical bounds.
        /// </summary>
        /// <param name="dateTime">The date and time associated with this point.</param>
        /// <param name="upper">The upper confidence bound.</param>
        /// <param name="lower">The lower confidence bound.</param>
        /// <param name="predictive">The predictive interval value.</param>
        /// <param name="mode">The mode value of the distribution.</param>
        public FrequencyCurvePoint(DateTime dateTime, double upper, double lower, double predictive, double mode)
        {
            DateTime = dateTime;
            Upper = upper;
            Lower = lower;
            Predictive = predictive;
            Mode = mode;
        }
    }
}
