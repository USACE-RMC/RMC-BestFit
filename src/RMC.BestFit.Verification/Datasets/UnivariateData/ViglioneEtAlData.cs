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
    /// Provides the Viglione et al. (2013) dataset and test configurations for verification of Bayesian flood frequency analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>Background:</b>
    ///     Viglione et al. (2013) and Skahill, Viglione, &amp; Byrd (2016) present a Bayesian analysis framework
    ///     for combining and evaluating the worth of different types of additional data (i.e., temporal, spatial, and causal)
    ///     in a flood frequency analysis. This dataset uses the Kamp at Zwettl gauge data from Austria.
    /// </para>
    /// <para>
    ///     <b>Information Expansion Types:</b>
    ///     <list type="bullet">
    ///         <item><description><b>Temporal expansion:</b> Collecting information on flood behavior before or after the systematic data period.</description></item>
    ///         <item><description><b>Spatial expansion:</b> Using flood information from neighboring catchments to improve frequency estimates.</description></item>
    ///         <item><description><b>Causal expansion:</b> Analyzing the generating mechanisms of floods in the catchment of interest.</description></item>
    ///     </list>
    /// </para>
    /// <para>
    ///     <b>Verification Approach:</b>
    ///     Skahill et al. (2016) independently revisited the example originally profiled by Viglione et al. (2013),
    ///     performing eight distinct MCMC simulations using the Kamp at Zwettl dataset. For each simulation, the
    ///     posterior mode (PM) estimate for the GEV parameters and the 100-year and 1,000-year discharges, including
    ///     the 90% credible interval, were provided. The same eight simulations were performed using RMC-BestFit
    ///     and compared with the results from Skahill et al. (2016).
    /// </para>
    /// <para>
    ///     <b>EvdBayes Comparison:</b>
    ///     The evdbayes package is an add-on package for the R programming environment that provides functions for
    ///     Bayesian analysis of extreme value models using MCMC. Test 9 compares RMC-BestFit with evdbayes using
    ///     informative priors on quantiles following the approach of Coles &amp; Tawn (1996).
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     <list type="bullet">
    ///         <item><description>Viglione, A., Merz, R., Salinas, J. L., &amp; Bloschl, G. (2013). Flood frequency hydrology: 3. A Bayesian analysis. Water Resources Research, 49(2), 675-692.</description></item>
    ///         <item><description>Skahill, B. E., Viglione, A., &amp; Byrd, A. R. (2016). A comparison of Bayesian MCMC with alternative methods for flood frequency analysis. Hydrological Sciences Journal.</description></item>
    ///         <item><description>Coles, S. G., &amp; Tawn, J. A. (1996). A Bayesian analysis of extreme rainfall data. Applied Statistics, 45(4), 463-478.</description></item>
    ///         <item><description>Stephenson, A., &amp; Ribatet, M. (2006). evdbayes: Bayesian Analysis in Extreme Value Theory. R package.</description></item>
    ///     </list>
    /// </para>
    /// </remarks>
    public static class ViglioneEtAlData
    {
        /// <summary>
        /// Gets the exact (systematic) annual maximum flood data for the Kamp at Zwettl gauge from 1951 to 2001.
        /// </summary>
        /// <remarks>
        /// This 51-year record represents the shorter systematic period used in the Viglione et al. (2013) analysis.
        /// Values are annual maximum discharge in cubic meters per second (m³/s).
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 51 annual maximum flood observations from 1951 to 2001.</returns>
        public static ExactSeries GetExactData_1951_2001()
        {
            var years = new int[]{
                1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960,
                1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970,
                1971, 1972, 1973, 1974, 1975, 1976, 1977, 1978, 1979, 1980,
                1981, 1982, 1983, 1984, 1985, 1986, 1987, 1988, 1989, 1990,
                1991, 1992, 1993, 1994, 1995, 1996, 1997, 1998, 1999, 2000,
                2001
            };
            var data = new double[] {
                135, 52, 45, 95, 54, 94, 95, 56, 140, 72, 50.9, 54, 105, 53,
                62, 51, 58, 39, 62, 68, 44, 52, 36, 60, 100, 41.2, 60.2, 19.7,
                50.6, 35.6, 46.1, 42, 26, 32.1, 89.1, 29, 58.3, 46.4, 20.9, 17,
                74.4, 22.6, 73.2, 41.8, 34.6, 120, 43, 23.7, 56.9, 30.4, 21.8 };

            var exact = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exact.Add(new ExactData(years[i], data[i]));
            }
            return exact;
        }

        /// <summary>
        /// Gets the exact (systematic) annual maximum flood data for the Kamp at Zwettl gauge from 1951 to 2005.
        /// </summary>
        /// <remarks>
        /// This 55-year record represents the extended systematic period that includes the August 2002 extreme flood event
        /// (459.2 m³/s), which was the largest flood observed in the systematic record and significantly affects
        /// the frequency analysis results.
        /// </remarks>
        /// <returns>An <see cref="ExactSeries"/> containing 55 annual maximum flood observations from 1951 to 2005.</returns>
        public static ExactSeries GetExactData_1951_2005()
        {
            var years = new int[]{
                1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960,
                1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970,
                1971, 1972, 1973, 1974, 1975, 1976, 1977, 1978, 1979, 1980,
                1981, 1982, 1983, 1984, 1985, 1986, 1987, 1988, 1989, 1990,
                1991, 1992, 1993, 1994, 1995, 1996, 1997, 1998, 1999, 2000,
                2001, 2002, 2003, 2004, 2005
            };
            var data = new double[] {
                135, 52, 45, 95, 54, 94, 95, 56, 140, 72, 50.9, 54, 105, 53,
                62, 51, 58, 39, 62, 68, 44, 52, 36, 60, 100, 41.2, 60.2, 19.7,
                50.6, 35.6, 46.1, 42, 26, 32.1, 89.1, 29, 58.3, 46.4, 20.9, 17,
                74.4, 22.6, 73.2, 41.8, 34.6, 120, 43, 23.7, 56.9, 30.4, 21.8,
                459.2, 32.5, 34.1, 95
            };

            var exact = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exact.Add(new ExactData(years[i], data[i]));
            }
            return exact;
        }

        /// <summary>
        /// Gets the historical interval flood data representing uncertain paleofloods for temporal expansion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These three historical floods were documented from historical records and represent interval-censored
        /// observations where the exact magnitude is uncertain but bounded:
        /// </para>
        /// <list type="bullet">
        ///     <item><description>1655 event: Estimated between 180-300 m³/s (most likely 240 m³/s)</description></item>
        ///     <item><description>1803 event: Estimated between 240-400 m³/s (most likely 320 m³/s)</description></item>
        ///     <item><description>1829 event: Estimated between 202.5-337.5 m³/s (most likely 270 m³/s)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>An <see cref="IntervalSeries"/> containing 3 historical interval-censored flood observations.</returns>
        public static IntervalSeries GetIntervalData()
        {
            var intervals = new IntervalSeries
            {
                new IntervalData(1655, 180, 240, 300),
                new IntervalData(1803, 240, 320, 400),
                new IntervalData(1829, 202.5, 270, 337.5)
            };
            return intervals;
        }

        /// <summary>
        /// Gets the threshold (perception) data representing the historical record period for temporal expansion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This threshold indicates that during the period 1600-1950, any flood exceeding 300 m³/s would have been
        /// documented in historical records. This perception threshold allows incorporation of the "non-occurrence"
        /// of large floods into the likelihood function, effectively constraining the upper tail of the distribution.
        /// </para>
        /// </remarks>
        /// <returns>A <see cref="ThresholdSeries"/> containing the historical perception threshold (300 m³/s from 1600-1950).</returns>
        public static ThresholdSeries GetThresholdData()
        {
            var thresholds = new ThresholdSeries
            {
                new ThresholdData(1600, 1950, 300)
            };
            return thresholds;
        }


        /// <summary>
        /// Gets the single quantile prior used for causal information expansion in Tests 5-8.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This prior represents expert knowledge about the 500-year flood (0.2% annual exceedance probability).
        /// The prior is a Normal distribution with mean 480 m³/s and standard deviation 80 m³/s, derived from
        /// analysis of flood generating mechanisms in the Kamp catchment.
        /// </para>
        /// </remarks>
        /// <returns>A <see cref="QuantilePrior"/> for the 0.2% AEP quantile with Normal(480, 80) distribution.</returns>
        public static QuantilePrior GetSinglePrior() => new QuantilePrior(0.002, new Normal(480, 80));

        /// <summary>
        /// Gets the list of three quantile priors used for comparison with the EvdBayes R package in Test 9.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These priors follow the approach used in Coles &amp; Tawn (1996) and are specified on three quantiles:
        /// </para>
        /// <list type="bullet">
        ///     <item><description>10-year flood (10% AEP): Normal(100, 20) m³/s</description></item>
        ///     <item><description>100-year flood (1% AEP): Normal(250, 40) m³/s</description></item>
        ///     <item><description>1000-year flood (0.1% AEP): Normal(500, 60) m³/s</description></item>
        /// </list>
        /// </remarks>
        /// <returns>A <see cref="List{QuantilePrior}"/> containing three quantile priors for multi-prior Bayesian analysis.</returns>
        public static List<QuantilePrior> GetQuantilePriors() => new List<QuantilePrior>
        {
            new QuantilePrior(0.1, new Normal(100, 20)),
            new QuantilePrior(0.01, new Normal(250, 40)),
            new QuantilePrior(0.001, new Normal(500, 60))
        };


        /// <summary>
        /// Test configuration 1: GEV analysis with exact data from 1951-2001 (no information expansion).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the baseline test using only the shorter systematic record without any additional information.
        /// Expected results from Skahill et al. (2016):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>GEV parameters: ξ=42.9, α=20.2, κ=-0.096</description></item>
        ///     <item><description>100-year flood: 160 m³/s (90% CI: 130-288)</description></item>
        ///     <item><description>1000-year flood: 241 m³/s (90% CI: 163-649)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with exact series.</description></item>
        ///     <item><description><b>TrueParameters:</b> Expected GEV parameters [ξ, α, κ].</description></item>
        ///     <item><description><b>True100year:</b> Expected 100-year quantile [mode, lower CI, upper CI].</description></item>
        ///     <item><description><b>True1000year:</b> Expected 1000-year quantile [mode, lower CI, upper CI].</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame,
            double[] TrueParameters,
            double[] True100year,
            double[] True1000year)
            Test1_ExactData_1951_2001()
        {
            var df = new DataFrame() { ExactSeries = GetExactData_1951_2001() };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var trueParameters = new double[] { 42.9, 20.2, -0.096 };
            var true100year = new double[] { 160, 130, 288 };
            var true1000Year = new double[] { 241, 163, 649 };
            return (df, trueParameters, true100year, true1000Year);
        }

        /// <summary>
        /// Test configuration 2: GEV analysis with exact data from 1951-2005 (no information expansion).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This test uses the extended systematic record that includes the extreme 2002 flood event.
        /// The inclusion of this outlier event significantly affects the shape parameter and upper tail estimates.
        /// Expected results from Skahill et al. (2016):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>GEV parameters: ξ=41.7, α=20.7, κ=-0.310</description></item>
        ///     <item><description>100-year flood: 253 m³/s (90% CI: 184-542)</description></item>
        ///     <item><description>1000-year flood: 543 m³/s (90% CI: 317-1853)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with exact series (1951-2005).</description></item>
        ///     <item><description><b>TrueParameters:</b> Expected GEV parameters [ξ, α, κ].</description></item>
        ///     <item><description><b>True100year:</b> Expected 100-year quantile [mode, lower CI, upper CI].</description></item>
        ///     <item><description><b>True1000year:</b> Expected 1000-year quantile [mode, lower CI, upper CI].</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame,
            double[] TrueParameters,
            double[] True100year,
            double[] True1000year)
            Test2_ExactData_1951_2005()
        {
            var df = new DataFrame() { ExactSeries = GetExactData_1951_2005() };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var trueParameters = new double[] { 41.7, 20.7, -0.310 };
            var true100year = new double[] { 253, 184, 542 };
            var true1000Year = new double[] { 543, 317, 1853 };
            return (df, trueParameters, true100year, true1000Year);
        }

        /// <summary>
        /// Test configuration 3: GEV analysis with exact data (1951-2001) plus temporal expansion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This test combines the shorter systematic record with historical flood data (temporal expansion).
        /// The historical information constrains the upper tail and reduces uncertainty.
        /// Expected results from Skahill et al. (2016):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>GEV parameters: ξ=43.4, α=21.7, κ=-0.222</description></item>
        ///     <item><description>100-year flood: 217 m³/s (90% CI: 176-291)</description></item>
        ///     <item><description>1000-year flood: 399 m³/s (90% CI: 278-647)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with exact, interval, and threshold series.</description></item>
        ///     <item><description><b>TrueParameters:</b> Expected GEV parameters [ξ, α, κ].</description></item>
        ///     <item><description><b>True100year:</b> Expected 100-year quantile [mode, lower CI, upper CI].</description></item>
        ///     <item><description><b>True1000year:</b> Expected 1000-year quantile [mode, lower CI, upper CI].</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame,
            double[] TrueParameters,
            double[] True100year,
            double[] True1000year)
            Test3_ExactData_1951_2001_TemporalExpansion()
        {
            var df = new DataFrame() {
                ExactSeries = GetExactData_1951_2001(),
                IntervalSeries = GetIntervalData(),
                ThresholdSeries = GetThresholdData()
            };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var trueParameters = new double[] { 43.4, 21.7, -0.222 };
            var true100year = new double[] { 217, 176, 291 };
            var true1000Year = new double[] { 399, 278, 647 };
            return (df, trueParameters, true100year, true1000Year);
        }

        /// <summary>
        /// Test configuration 4: GEV analysis with exact data (1951-2005) plus temporal expansion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This test combines the extended systematic record (including the 2002 event) with historical data.
        /// Expected results from Skahill et al. (2016):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>GEV parameters: ξ=42.6, α=21.5, κ=-0.281</description></item>
        ///     <item><description>100-year flood: 244 m³/s (90% CI: 197-331)</description></item>
        ///     <item><description>1000-year flood: 497 m³/s (90% CI: 347-818)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with exact (1951-2005), interval, and threshold series.</description></item>
        ///     <item><description><b>TrueParameters:</b> Expected GEV parameters [ξ, α, κ].</description></item>
        ///     <item><description><b>True100year:</b> Expected 100-year quantile [mode, lower CI, upper CI].</description></item>
        ///     <item><description><b>True1000year:</b> Expected 1000-year quantile [mode, lower CI, upper CI].</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame,
            double[] TrueParameters,
            double[] True100year,
            double[] True1000year)
            Test4_ExactData_1951_2005_TemporalExpansion()
        {
            var df = new DataFrame()
            {
                ExactSeries = GetExactData_1951_2005(),
                IntervalSeries = GetIntervalData(),
                ThresholdSeries = GetThresholdData()
            };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var trueParameters = new double[] { 42.6, 21.5, -0.281 };
            var true100year = new double[] { 244, 197, 331 };
            var true1000Year = new double[] { 497, 347, 818 };
            return (df, trueParameters, true100year, true1000Year);
        }

        /// <summary>
        /// Test configuration 5: GEV analysis with exact data (1951-2001) plus causal expansion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This test uses the shorter systematic record with a quantile prior derived from causal analysis
        /// of flood generating mechanisms. The prior constrains the 500-year flood estimate.
        /// Expected results from Skahill et al. (2016):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>GEV parameters: ξ=41.9, α=21.0, κ=-0.313</description></item>
        ///     <item><description>100-year flood: 258 m³/s (90% CI: 193-307)</description></item>
        ///     <item><description>1000-year flood: 557 m³/s (90% CI: 335-702)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with exact series.</description></item>
        ///     <item><description><b>QuantilePrior:</b> The single quantile prior for the 500-year flood.</description></item>
        ///     <item><description><b>TrueParameters:</b> Expected GEV parameters [ξ, α, κ].</description></item>
        ///     <item><description><b>True100year:</b> Expected 100-year quantile [mode, lower CI, upper CI].</description></item>
        ///     <item><description><b>True1000year:</b> Expected 1000-year quantile [mode, lower CI, upper CI].</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame,
            QuantilePrior QuantilePrior,
            double[] TrueParameters,
            double[] True100year,
            double[] True1000year)
            Test5_ExactData_1951_2001_CausalExpansion()
        {
            var df = new DataFrame() { ExactSeries = GetExactData_1951_2001() };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var trueParameters = new double[] { 41.9, 21.0, -0.313 };
            var true100year = new double[] { 258, 193, 307 };
            var true1000Year = new double[] { 557, 335, 702 };
            return (df, GetSinglePrior(), trueParameters, true100year, true1000Year);
        }

        /// <summary>
        /// Test configuration 6: GEV analysis with exact data (1951-2005) plus causal expansion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This test uses the extended systematic record with the quantile prior from causal analysis.
        /// Expected results from Skahill et al. (2016):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>GEV parameters: ξ=41.6, α=20.8, κ=-0.333</description></item>
        ///     <item><description>100-year flood: 269 m³/s (90% CI: 217-317)</description></item>
        ///     <item><description>1000-year flood: 604 m³/s (90% CI: 418-747)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with exact series (1951-2005).</description></item>
        ///     <item><description><b>QuantilePrior:</b> The single quantile prior for the 500-year flood.</description></item>
        ///     <item><description><b>TrueParameters:</b> Expected GEV parameters [ξ, α, κ].</description></item>
        ///     <item><description><b>True100year:</b> Expected 100-year quantile [mode, lower CI, upper CI].</description></item>
        ///     <item><description><b>True1000year:</b> Expected 1000-year quantile [mode, lower CI, upper CI].</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame,
            QuantilePrior QuantilePrior,
            double[] TrueParameters,
            double[] True100year,
            double[] True1000year)
            Test6_ExactData_1951_2005_CausalExpansion()
        {
            var df = new DataFrame() { ExactSeries = GetExactData_1951_2005() };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var trueParameters = new double[] { 41.6, 20.8, -0.333 };
            var true100year = new double[] { 269, 217, 317 };
            var true1000Year = new double[] { 604, 418, 747 };
            return (df, GetSinglePrior(), trueParameters, true100year, true1000Year);
        }

        /// <summary>
        /// Test configuration 7: GEV analysis with exact data (1951-2001) plus temporal and causal expansion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This test combines the shorter systematic record with both historical data and the quantile prior,
        /// representing the most comprehensive information expansion scenario for the 1951-2001 period.
        /// Expected results from Skahill et al. (2016):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>GEV parameters: ξ=42.7, α=21.8, κ=-0.291</description></item>
        ///     <item><description>100-year flood: 253 m³/s (90% CI: 206-299)</description></item>
        ///     <item><description>1000-year flood: 527 m³/s (90% CI: 369-671)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with exact, interval, and threshold series.</description></item>
        ///     <item><description><b>QuantilePrior:</b> The single quantile prior for the 500-year flood.</description></item>
        ///     <item><description><b>TrueParameters:</b> Expected GEV parameters [ξ, α, κ].</description></item>
        ///     <item><description><b>True100year:</b> Expected 100-year quantile [mode, lower CI, upper CI].</description></item>
        ///     <item><description><b>True1000year:</b> Expected 1000-year quantile [mode, lower CI, upper CI].</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame,
            QuantilePrior QuantilePrior,
            double[] TrueParameters,
            double[] True100year,
            double[] True1000year)
            Test7_ExactData_1951_2001_TemporalAndCausalExpansion()
        {
            var df = new DataFrame()
            {
                ExactSeries = GetExactData_1951_2001(),
                IntervalSeries = GetIntervalData(),
                ThresholdSeries = GetThresholdData()
            };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var trueParameters = new double[] { 42.7, 21.8, -0.291 };
            var true100year = new double[] { 253, 206, 299 };
            var true1000Year = new double[] { 527, 369, 671 };
            return (df, GetSinglePrior(), trueParameters, true100year, true1000Year);
        }

        /// <summary>
        /// Test configuration 8: GEV analysis with exact data (1951-2005) plus temporal and causal expansion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This test combines the extended systematic record with both historical data and the quantile prior,
        /// representing the most comprehensive information expansion scenario for the 1951-2005 period.
        /// Expected results from Skahill et al. (2016):
        /// </para>
        /// <list type="bullet">
        ///     <item><description>GEV parameters: ξ=42.5, α=21.5, κ=-0.313</description></item>
        ///     <item><description>100-year flood: 264 m³/s (90% CI: 220-308)</description></item>
        ///     <item><description>1000-year flood: 571 m³/s (90% CI: 418-708)</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with exact (1951-2005), interval, and threshold series.</description></item>
        ///     <item><description><b>QuantilePrior:</b> The single quantile prior for the 500-year flood.</description></item>
        ///     <item><description><b>TrueParameters:</b> Expected GEV parameters [ξ, α, κ].</description></item>
        ///     <item><description><b>True100year:</b> Expected 100-year quantile [mode, lower CI, upper CI].</description></item>
        ///     <item><description><b>True1000year:</b> Expected 1000-year quantile [mode, lower CI, upper CI].</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame,
            QuantilePrior QuantilePrior,
            double[] TrueParameters,
            double[] True100year,
            double[] True1000year)
            Test8_ExactData_1951_2005_TemporalAndCausalExpansion()
        {
            var df = new DataFrame()
            {
                ExactSeries = GetExactData_1951_2005(),
                IntervalSeries = GetIntervalData(),
                ThresholdSeries = GetThresholdData()
            };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var trueParameters = new double[] { 42.5, 21.5, -0.313 };
            var true100year = new double[] { 264, 220, 308 };
            var true1000Year = new double[] { 571, 418, 708 };
            return (df, GetSinglePrior(), trueParameters, true100year, true1000Year);
        }

        /// <summary>
        /// Test configuration 9: GEV analysis with exact data (1951-2001) and three quantile priors for EvdBayes comparison.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This test compares RMC-BestFit with the evdbayes R package using informative priors on three quantiles
        /// following the approach of Coles &amp; Tawn (1996). The expected results are posterior summary statistics
        /// for the GEV parameters computed from the EvdBayes MCMC output.
        /// </para>
        /// <para>
        /// Expected posterior statistics from EvdBayes:
        /// </para>
        /// <list type="bullet">
        ///     <item><description>Location (ξ): Mean=41.43, StdDev=3.09, 2.5%=35.57, Median=41.35, 97.5%=47.74</description></item>
        ///     <item><description>Scale (α): Mean=20.77, StdDev=2.45, 2.5%=16.39, Median=20.62, 97.5%=26.04</description></item>
        ///     <item><description>Shape (κ): Mean=-0.269, StdDev=0.049, 2.5%=-0.361, Median=-0.271, 97.5%=-0.170</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>DataFrame:</b> The configured data frame with exact series (1951-2001).</description></item>
        ///     <item><description><b>QuantilePriors:</b> List of three quantile priors for 10-, 100-, and 1000-year floods.</description></item>
        ///     <item><description><b>TrueMeanParameters:</b> Expected posterior mean [ξ, α, κ].</description></item>
        ///     <item><description><b>TrueStDevParameters:</b> Expected posterior standard deviation [ξ, α, κ].</description></item>
        ///     <item><description><b>TrueLowerParameters:</b> Expected 2.5% posterior quantile [ξ, α, κ].</description></item>
        ///     <item><description><b>TrueMedianParameters:</b> Expected 50% posterior quantile [ξ, α, κ].</description></item>
        ///     <item><description><b>TrueUpperParameters:</b> Expected 97.5% posterior quantile [ξ, α, κ].</description></item>
        /// </list>
        /// </returns>
        public static (DataFrame DataFrame,
            List<QuantilePrior> QuantilePriors,
            double[] TrueMeanParameters,
            double[] TrueStDevParameters,
            double[] TrueLowerParameters,
            double[] TrueMedianParameters,
            double[] TrueUpperParameters)
            Test9_ExactData_1951_2001_CausalExpansion_TheePriors()
        {
            var df = new DataFrame() { ExactSeries = GetExactData_1951_2001() };
            df.ProcessThresholdSeries();
            df.CalculatePlottingPositions();
            var trueMeanParameters = new double[] { 41.4307, 20.7700, -0.2694 }; // Mean of posterior from EvdBayes
            var trueStdDevParameters = new double[] { 3.0918, 2.4520, 0.0488 }; // StdDev of posterior from EvdBayes
            var trueLowerParameters = new double[] { 35.5680, 16.3861, -0.3608 }; // 2.5% quantile of posterior from EvdBayes
            var trueMedianParameters = new double[] { 41.3467, 20.6195, -0.2709 }; // 50% quantile of posterior from EvdBayes
            var trueUpperParameters = new double[] { 47.7391, 26.0375, -0.1699 }; // 97.5% quantile of posterior from EvdBayes
            return (df, GetQuantilePriors(), trueMeanParameters, trueStdDevParameters, trueLowerParameters, trueMedianParameters, trueUpperParameters);
        }

    }
}
