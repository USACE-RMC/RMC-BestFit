using Numerics.Distributions;
using RMC.BestFit.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC.BestFit.Verification.Datasets.UnivariateData
{

    /// <summary>
    /// Provides datasets and test configurations for verification against the Flike software from Australian Rainfall and Runoff (ARR).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>Background:</b>
    ///     In the RMC-BestFit verification report, a comparison was made with Flike (Kuczera, 1999),
    ///     a Bayesian flood frequency analysis software developed by Professor George Kuczera
    ///     from the School of Civil Engineering at the University of Newcastle, Australia.
    ///     Flike is compliant with the recent major revision of Australian industry guidelines
    ///     for flood estimation, documented in the update of Australian Rainfall and Runoff (ARR).
    /// </para>
    /// <para>
    ///     <b>Flike Methodology:</b>
    ///     Flike uses a novel importance sampling approach for estimating the posterior rather than Bayesian MCMC.
    ///     In addition, Flike samples prior distributions using a multivariate Normal distribution,
    ///     with the default priors set to have very large variances in order to make them uninformative.
    /// </para>
    /// <para>
    ///     <b>Self-Training Examples:</b>
    ///     There are a number of self-training examples on the Flike website (https://flike.tuflow.com).
    ///     The examples most comparable with RMC-BestFit are examples 3 through 6:
    ///     <list type="bullet">
    ///         <item><description><b>Example 3:</b> Basic LPIII flood frequency analysis following ARR Book 3 procedures.</description></item>
    ///         <item><description><b>Example 4:</b> Incorporation of binomial censored historical flood information.</description></item>
    ///         <item><description><b>Example 5:</b> Use of regional skew information as an informative prior.</description></item>
    ///         <item><description><b>Example 6:</b> Low outlier censoring using the MGBT (Multiple Grubbs-Beck Test).</description></item>
    ///     </list>
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     <list type="bullet">
    ///         <item><description>Kuczera, G. (1999). Comprehensive at-site flood frequency analysis using Monte Carlo Bayesian inference. Water Resources Research, 35(5), 1551-1557.</description></item>
    ///         <item><description>Ball, J., et al. (2019). Australian Rainfall and Runoff: A Guide to Flood Estimation. Commonwealth of Australia.</description></item>
    ///         <item><description>ARR Book 3: Peak Discharge Estimation. Available at: http://arr.ga.gov.au/arr-guideline</description></item>
    ///     </list>
    /// </para>
    /// </remarks>
    public static class ArrFlikeData
    {
        /// <summary>
        /// Gets the annual maximum series for the Hunter River at Singleton, NSW, Australia from 1938 to 1968.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This 31-year record contains annual maximum discharge values in cubic meters per second (m³/s).
        /// The dataset includes notable flood events such as the 1955 flood (12,525.66 m³/s), which was
        /// the largest on record. This dataset is used in Flike Examples 3, 4, and 5.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 31 annual maximum flood observations for the Hunter River at Singleton.</returns>
        public static ExactSeries GetHunterRiverData()
        {
            var years = new int[]{ 1938, 1939, 1940, 1941, 1942, 1943, 1944, 1945,
                1946, 1947, 1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955,
                1956, 1957, 1958, 1959, 1960, 1961, 1962, 1963, 1964, 1965,
                1966, 1967, 1968
            };
            var data = new double[] { 76.26, 171.87, 218.21, 668.79, 1374.42, 124.12,
                276.3, 895.5, 1374.42, 280.18, 202.62, 4052.42, 2323.77, 2536.31, 3315.62,
                1232.73, 1391.43, 12525.66, 1099.54, 447.75, 478.92, 180.52, 164.36,
                229.54, 2125.4, 966.35, 2751.68, 49.03, 76.51, 912.5, 926.67 };

            var exact = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exact.Add(new ExactData(years[i], data[i]));
            }
            return exact;
        }

        /// <summary>
        /// Gets the annual maximum series for the Wimmera River at Glynwylln, Victoria, Australia from 1960 to 2015.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This 56-year record contains annual maximum discharge values in cubic meters per second (m³/s),
        /// sorted in descending order as provided in ARR documentation. The dataset exhibits a high degree
        /// of skewness with many low flow years, making it suitable for demonstrating low outlier detection
        /// and censoring using the Multiple Grubbs-Beck Test (MGBT).
        /// </para>
        /// <para>
        /// <b>Note:</b> The source data table from ARR provides flows in descending order without years.
        /// The years shown are the reported record period (1960-2015) assigned sequentially.
        /// </para>
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 56 annual maximum flood observations for the Wimmera River at Glynwylln.</returns>
        public static ExactSeries GetWimerraRiverData()
        {
            var years = new int[]{ 1960, 1961, 1962, 1963, 1964, 1965, 1966, 1967,
                1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975, 1976, 1977,
                1978, 1979, 1980, 1981, 1982, 1983, 1984, 1985, 1986, 1987,
                1988, 1989, 1990, 1991, 1992, 1993, 1994, 1995, 1996, 1997,
                1998, 1999, 2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007,
                2008, 2009, 2010, 2011, 2012, 2013, 2014, 2015
            };
            var data = new double[] { 464.35, 395.65, 285.92, 278.01, 235.22, 211.91,
                173.79, 170.13, 167.72, 155.22, 147, 143.99, 143.62, 142.66, 134.36,
                123.8, 119.63, 110.56, 102.62, 97.32, 96.78, 87.98, 79.15, 77.03, 71.4,
                69.67, 67.49, 61.64, 54.4, 38.62, 36.62, 34.07, 32.18, 25.91, 24.83, 23.95,
                22.76, 19.04, 17.37, 14.87, 14.16, 12.64, 11.9, 11.79, 11.41, 10.8, 10.31,
                10.08, 8.52, 3.22, 2.28, 2.13, 1.9, 1.43, 1.16, 0.01 };

            var exact = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exact.Add(new ExactData(years[i], data[i]));
            }
            return exact;
        }


        /// <summary>
        /// Gets the historical threshold (perception) data used with Example 4 for the Hunter River.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This threshold indicates that during the period 1820-1937 (117 years prior to the systematic record),
        /// one flood exceeded the threshold of 12,525 m³/s. This perception threshold allows incorporation
        /// of binomial censored historical flood information into the likelihood function, effectively
        /// constraining the upper tail of the distribution.
        /// </para>
        /// </remarks>
        /// <returns>A <see cref="ThresholdSeries"/> containing the historical perception threshold for the Hunter River.</returns>
        public static ThresholdSeries GetThresholdData()
        {
            var thresholds = new ThresholdSeries
            {
                new ThresholdData(1820, 1937, 12525) { NumberAbove = 1 }
            };
            return thresholds;
        }

        /// <summary>
        /// Gets the regional skew prior distribution used with Example 5 for the Hunter River.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This prior represents regional skew information derived from a regional skew analysis.
        /// The regional skew was estimated to be 0.00 with a mean square error (MSE) of 0.09,
        /// corresponding to a standard deviation of 0.30. This information is incorporated
        /// into the Bayesian analysis by setting the prior for the skew parameter of the
        /// Log-Pearson Type III distribution to be Normally distributed with mean 0.00
        /// and standard deviation 0.30.
        /// </para>
        /// </remarks>
        /// <returns>A <see cref="Normal"/> distribution representing the regional skew prior N(0.0, 0.3).</returns>
        public static Normal GetRegionalSkewPrior()
        {
            return new Normal(0.0, 0.3);
        }


        /// <summary>
        /// Example 3: Basic LPIII flood frequency analysis following ARR Book 3 procedures.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This example demonstrates a flood frequency analysis using the procedures described
        /// in Australian Rainfall and Runoff Book 3: Peak Discharge Estimation. Specifically,
        /// this example covers fitting a Log-Pearson Type III (LPIII) distribution to the
        /// annual maximum series for the Hunter River at Singleton using Bayesian inference
        /// with uninformative (flat) priors on all parameters.
        /// </para>
        /// <para>
        /// Expected results from Flike for selected AEPs:
        /// </para>
        /// <list type="bullet">
        ///     <item><description>0.2% AEP (500-yr): Mean=47,034 m³/s (90% CI: 11,507-570,635)</description></item>
        ///     <item><description>1% AEP (100-yr): Mean=19,572 m³/s (90% CI: 7,188-107,122)</description></item>
        ///     <item><description>2% AEP (50-yr): Mean=12,786 m³/s (90% CI: 5,502-51,010)</description></item>
        ///     <item><description>10% AEP (10-yr): Mean=3,929 m³/s (90% CI: 2,229-8,408)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Hunter River exact series.</description></item>
        ///     <item><description><b>AEPValues:</b> Array of annual exceedance probabilities [0.002, 0.01, 0.02, 0.1].</description></item>
        ///     <item><description><b>TruePosteriorMean:</b> Expected posterior mean discharge at each AEP.</description></item>
        ///     <item><description><b>TrueLowerCI:</b> Expected lower 90% credible interval at each AEP.</description></item>
        ///     <item><description><b>TrueUpperCI:</b> Expected upper 90% credible interval at each AEP.</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double[] AEPValues,
            double[] TruePosteriorMean,
            double[] TrueLowerCI,
            double[] TrueUpperCI)
            Example3()
        {
            var df = new DataFrame() { ExactSeries = GetHunterRiverData() };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var aepValues = new double[] { 0.002, 0.01, 0.02, 0.1 };
            var truePosteriorMean = new double[] { 47034, 19572, 12786, 3929 };
            var trueLowerCI = new double[] { 11507, 7188, 5502, 2229 };
            var trueUpperCI = new double[] { 570635, 107122, 51010, 8408 };
            return (df, aepValues, truePosteriorMean, trueLowerCI, trueUpperCI);
        }

        /// <summary>
        /// Example 4: LPIII flood frequency analysis with binomial censored historical flood information.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This example is a continuation of Example 3 and examines the benefit of using binomial censored
        /// historical flood information. The historical information indicates that during the 117-year period
        /// prior to systematic records (1820-1937), only one flood exceeded the threshold of 12,525 m³/s.
        /// This additional information significantly reduces uncertainty in the upper tail estimates.
        /// </para>
        /// <para>
        /// Expected results from Flike for selected AEPs:
        /// </para>
        /// <list type="bullet">
        ///     <item><description>0.2% AEP (500-yr): Mean=28,542 m³/s (90% CI: 12,966-85,583)</description></item>
        ///     <item><description>1% AEP (100-yr): Mean=13,511 m³/s (90% CI: 7,785-27,687)</description></item>
        ///     <item><description>2% AEP (50-yr): Mean=9,350 m³/s (90% CI: 5,778-16,511)</description></item>
        ///     <item><description>10% AEP (10-yr): Mean=3,294 m³/s (90% CI: 2,181-4,947)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Hunter River exact and threshold series.</description></item>
        ///     <item><description><b>AEPValues:</b> Array of annual exceedance probabilities [0.002, 0.01, 0.02, 0.1].</description></item>
        ///     <item><description><b>TruePosteriorMean:</b> Expected posterior mean discharge at each AEP.</description></item>
        ///     <item><description><b>TrueLowerCI:</b> Expected lower 90% credible interval at each AEP.</description></item>
        ///     <item><description><b>TrueUpperCI:</b> Expected upper 90% credible interval at each AEP.</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double[] AEPValues,
                    double[] TruePosteriorMean,
                    double[] TrueLowerCI,
                    double[] TrueUpperCI)
                    Example4()
        {
            var df = new DataFrame() { ExactSeries = GetHunterRiverData(), ThresholdSeries = GetThresholdData() };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var aepValues = new double[] { 0.002, 0.01, 0.02, 0.1 };
            var truePosteriorMean = new double[] { 28542, 13511, 9350, 3294 };
            var trueLowerCI = new double[] { 12966, 7785, 5778, 2181 };
            var trueUpperCI = new double[] { 85583, 27687, 16511, 4947 };
            return (df, aepValues, truePosteriorMean, trueLowerCI, trueUpperCI);
        }

        /// <summary>
        /// Example 5: LPIII flood frequency analysis with regional skew information.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This example examines the use of regional information, building on Example 3. A regional skew
        /// analysis was performed and the regional skew was estimated to be 0.00 with a mean square error
        /// (MSE) of 0.09. This information is incorporated into the Bayesian analysis by setting the prior
        /// for the skew parameter of the LPIII distribution to be Normally distributed with mean 0.00
        /// and standard deviation 0.30. The use of regional skew information helps stabilize the
        /// skew parameter estimate, particularly for shorter records.
        /// </para>
        /// <para>
        /// Expected results from Flike for selected AEPs:
        /// </para>
        /// <list type="bullet">
        ///     <item><description>0.2% AEP (500-yr): Mean=33,365 m³/s (90% CI: 12,244-134,107)</description></item>
        ///     <item><description>1% AEP (100-yr): Mean=15,413 m³/s (90% CI: 7,093-45,087)</description></item>
        ///     <item><description>2% AEP (50-yr): Mean=10,535 m³/s (90% CI: 5,310-26,633)</description></item>
        ///     <item><description>10% AEP (10-yr): Mean=3,598 m³/s (90% CI: 2,172-6,702)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Hunter River exact series.</description></item>
        ///     <item><description><b>RegionalSkewPrior:</b> The Normal(0.0, 0.3) prior for the skew parameter.</description></item>
        ///     <item><description><b>AEPValues:</b> Array of annual exceedance probabilities [0.002, 0.01, 0.02, 0.1].</description></item>
        ///     <item><description><b>TruePosteriorMean:</b> Expected posterior mean discharge at each AEP.</description></item>
        ///     <item><description><b>TrueLowerCI:</b> Expected lower 90% credible interval at each AEP.</description></item>
        ///     <item><description><b>TrueUpperCI:</b> Expected upper 90% credible interval at each AEP.</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, Normal RegionalSkewPrior, double[] AEPValues,
                   double[] TruePosteriorMean,
                   double[] TrueLowerCI,
                   double[] TrueUpperCI)
                   Example5()
        {
            var df = new DataFrame() { ExactSeries = GetHunterRiverData() };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var aepValues = new double[] { 0.002, 0.01, 0.02, 0.1 };
            var truePosteriorMean = new double[] { 33365, 15413, 10535, 3598 };
            var trueLowerCI = new double[] { 12244, 7093, 5310, 2172 };
            var trueUpperCI = new double[] { 134107, 45087, 26633, 6702 };
            return (df, GetRegionalSkewPrior(), aepValues, truePosteriorMean, trueLowerCI, trueUpperCI);
        }

        /// <summary>
        /// Example 6a: GEV flood frequency analysis without low outlier removal.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the first part of Example 6, which demonstrates low outlier censoring using the
        /// Multiple Grubbs-Beck Test (MGBT). In Example 6a, the Generalized Extreme Value (GEV) distribution
        /// is fit to the Wimmera River data without removal of low outliers. The dataset exhibits
        /// significant positive skewness with many near-zero values, leading to very wide credible intervals
        /// when low outliers are not censored.
        /// </para>
        /// <para>
        /// Expected results from Flike for selected AEPs (note the very wide credible intervals):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>0.2% AEP (500-yr): Mean=10,696 m³/s (90% CI: 1,802-101,034)</description></item>
        ///     <item><description>1% AEP (100-yr): Mean=2,481 m³/s (90% CI: 737-12,398)</description></item>
        ///     <item><description>2% AEP (50-yr): Mean=1,315 m³/s (90% CI: 493-4,975)</description></item>
        ///     <item><description>10% AEP (10-yr): Mean=286 m³/s (90% CI: 172-578)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Wimmera River exact series (no censoring).</description></item>
        ///     <item><description><b>AEPValues:</b> Array of annual exceedance probabilities [0.002, 0.01, 0.02, 0.1].</description></item>
        ///     <item><description><b>TruePosteriorMean:</b> Expected posterior mean discharge at each AEP.</description></item>
        ///     <item><description><b>TrueLowerCI:</b> Expected lower 90% credible interval at each AEP.</description></item>
        ///     <item><description><b>TrueUpperCI:</b> Expected upper 90% credible interval at each AEP.</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double[] AEPValues,
            double[] TruePosteriorMean,
            double[] TrueLowerCI,
            double[] TrueUpperCI)
            Example6a()
        {
            var df = new DataFrame() { ExactSeries = GetWimerraRiverData() };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var aepValues = new double[] { 0.002, 0.01, 0.02, 0.1 };
            var truePosteriorMean = new double[] { 10696, 2481, 1315, 286 };
            var trueLowerCI = new double[] { 1802, 737, 493, 172 };
            var trueUpperCI = new double[] { 101034, 12398, 4975, 578 };
            return (df, aepValues, truePosteriorMean, trueLowerCI, trueUpperCI);
        }

        /// <summary>
        /// Example 6b: GEV flood frequency analysis with low outliers censored using MGBT.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the second part of Example 6, where the Multiple Grubbs-Beck Test (MGBT) is used
        /// to identify and censor low outliers, which are then replaced with a left-censored threshold.
        /// The GEV distribution is then fit to the data with low outliers removed. This approach is
        /// consistent with Bulletin 17C (U.S. Geological Survey, 2018) and is implemented in both
        /// HEC-SSP and RMC-BestFit. Censoring the low outliers dramatically reduces the credible
        /// interval widths compared to Example 6a.
        /// </para>
        /// <para>
        /// Expected results from Flike for selected AEPs (note the much narrower credible intervals):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>0.2% AEP (500-yr): Mean=789 m³/s (90% CI: 448-2,813)</description></item>
        ///     <item><description>1% AEP (100-yr): Mean=521 m³/s (90% CI: 354-1,145)</description></item>
        ///     <item><description>2% AEP (50-yr): Mean=423 m³/s (90% CI: 304-785)</description></item>
        ///     <item><description>10% AEP (10-yr): Mean=227 m³/s (90% CI: 177-311)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with Wimmera River data after MGBT low outlier censoring.</description></item>
        ///     <item><description><b>AEPValues:</b> Array of annual exceedance probabilities [0.002, 0.01, 0.02, 0.1].</description></item>
        ///     <item><description><b>TruePosteriorMean:</b> Expected posterior mean discharge at each AEP.</description></item>
        ///     <item><description><b>TrueLowerCI:</b> Expected lower 90% credible interval at each AEP.</description></item>
        ///     <item><description><b>TrueUpperCI:</b> Expected upper 90% credible interval at each AEP.</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame, double[] AEPValues,
            double[] TruePosteriorMean,
            double[] TrueLowerCI,
            double[] TrueUpperCI)
            Example6b()
        {
            var df = new DataFrame() { ExactSeries = GetWimerraRiverData() };
            df.SetLowOutliersFromMGBT();
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var aepValues = new double[] { 0.002, 0.01, 0.02, 0.1 };
            var truePosteriorMean = new double[] { 789, 521, 423, 227 };
            var trueLowerCI = new double[] { 448, 354, 304, 177 };
            var trueUpperCI = new double[] { 2813, 1145, 785, 311 };
            return (df, aepValues, truePosteriorMean, trueLowerCI, trueUpperCI);
        }

    }
}
