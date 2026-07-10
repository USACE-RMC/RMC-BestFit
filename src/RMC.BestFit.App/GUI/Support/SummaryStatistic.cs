namespace RMC_BestFit
{
    /// <summary>
    /// Represents a summary statistic with an optional secondary value and significance indicator.
    /// </summary>
    /// <remarks>
    /// This class encapsulates a named statistical measure with one or two numerical values and an optional
    /// significance indicator. It is commonly used to display descriptive statistics such as mean, median,
    /// standard deviation, or other statistical measures in the user interface.
    /// </remarks>
    public class SummaryStatistic
    {
        /// <summary>
        /// Gets or sets the name of the summary statistic.
        /// </summary>
        /// <remarks>
        /// Examples include "Mean", "Median", "Standard Deviation", "Minimum", "Maximum", etc.
        /// </remarks>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the primary numerical value of the statistic.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets an optional secondary numerical value for the statistic.
        /// </summary>
        /// <remarks>
        /// This property defaults to double.NaN when not specified. It can be used for statistics
        /// that require two values, such as confidence intervals or paired measurements.
        /// </remarks>
        public double Value2 { get; set; }

        /// <summary>
        /// Gets or sets an optional significance indicator or additional context for the statistic.
        /// </summary>
        /// <remarks>
        /// This property can be used to indicate statistical significance, provide units,
        /// or offer other contextual information about the statistic.
        /// </remarks>
        public string Significance { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SummaryStatistic"/> class.
        /// </summary>
        /// <param name="name">The name of the summary statistic.</param>
        /// <param name="value">The primary numerical value of the statistic.</param>
        /// <param name="value2">An optional secondary numerical value (defaults to double.NaN).</param>
        /// <param name="significance">An optional significance indicator or context (defaults to empty string).</param>
        public SummaryStatistic(string name, double value, double value2 = double.NaN, string significance = "")
        {
            Name = name;
            Value = value;
            Value2 = value2;
            Significance = significance;
        }
    }
}
