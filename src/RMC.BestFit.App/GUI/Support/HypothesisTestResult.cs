using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents the result of a statistical hypothesis test.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the essential components of a hypothesis test result, including
    /// the test name, p-value, significance level, and statistical inference conclusions.
    /// It is typically used for goodness-of-fit tests in distribution fitting analysis.
    /// </remarks>
    public class HypothesisTestResult
    {
        /// <summary>
        /// Gets or sets the name of the hypothesis test.
        /// </summary>
        /// <remarks>
        /// Examples include "Kolmogorov-Smirnov", "Anderson-Darling", or "Chi-Square" tests.
        /// </remarks>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the p-value of the hypothesis test.
        /// </summary>
        /// <remarks>
        /// The p-value represents the probability of obtaining test results at least as extreme as
        /// the observed results, assuming the null hypothesis is true.
        /// </remarks>
        public string PValue { get; set; }

        /// <summary>
        /// Gets or sets the significance level used for the test.
        /// </summary>
        /// <remarks>
        /// Common significance levels include 0.05, 0.01, or other alpha values used to determine
        /// whether to reject the null hypothesis.
        /// </remarks>
        public string Significance { get; set; }

        /// <summary>
        /// Gets or sets the statistical inference or conclusion of the test.
        /// </summary>
        /// <remarks>
        /// This typically indicates whether the null hypothesis is rejected or not rejected,
        /// along with any relevant interpretation of the test results.
        /// </remarks>
        public string Inference { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HypothesisTestResult"/> class.
        /// </summary>
        /// <param name="name">The name of the hypothesis test.</param>
        /// <param name="pValue">The p-value of the test.</param>
        /// <param name="significance">The significance level used (optional, defaults to empty string).</param>
        /// <param name="inference">The statistical inference or conclusion (optional, defaults to empty string).</param>
        public HypothesisTestResult(string name, string pValue, string significance = "", string inference = "")
        {
            Name = name;
            PValue = pValue;
            Significance = significance;
            Inference = inference;
        }

    }
}
