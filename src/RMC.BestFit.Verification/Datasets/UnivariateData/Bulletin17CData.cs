using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Datasets.UnivariateData
{
    /// <summary>
    /// Provides test datasets for the seven worked examples in the USGS Bulletin 17C guideline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each example returns a <see cref="DataFrame"/> configured with exact, interval, and/or threshold
    /// series, along with the expected log-space LP-III parameters (mean, standard deviation, skew)
    /// from the published B17C results.
    /// </para>
    /// <para>
    /// Reference: England, J.F., Cohn, T.A., Faber, B.A., et al. (2019). Guidelines for Determining
    /// Flood Flow Frequency — Bulletin 17C. U.S. Geological Survey Techniques and Methods, Book 4,
    /// Chapter B5, 148 p.
    /// </para>
    /// </remarks>
    public static class Bulletin17CData
    {
        /// <summary>
        /// Example 1: Systematic Record — Moose River at Victory, VT (USGS 01134500).
        /// </summary>
        /// <returns>
        /// A tuple of the configured <see cref="DataFrame"/> and expected LP-III parameters
        /// [mean, standard deviation, skew, weighted skew] in log-space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Systematic record of 68 annual peak flows (1947–2014). No historical or censored data.
        /// Low outliers identified by MGBT. The fourth parameter is the weighted regional skew (0.421).
        /// </para>
        /// <para>Reference: B17C Guideline, Example 1, Tables 1–3.</para>
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GetExample1()
        {
            var years = new int[]{
                1947,   1948,   1949,   1950,   1951,   1952,   1953,   1954,   1955,
                1956,   1957,   1958,   1959,   1960,   1961,   1962,   1963,   1964,
                1965,   1966,   1967,   1968,   1969,   1970,   1971,   1972,   1973,
                1974,   1975,   1976,   1977,   1978,   1979,   1980,   1981,   1982,
                1983,   1984,   1985,   1986,   1987,   1988,   1989,   1990,   1991,
                1992,   1993,   1994,   1995,   1996,   1997,   1998,   1999,   2000,
                2001,   2002,   2003,   2004,   2005,   2006,   2007,   2008,   2009,
                2010,   2011,   2012,   2013,   2014
            };
            var data = new double[] {
                2080, 1670, 1480, 2940, 1560, 2380, 2720, 2860, 2620, 1710, 1370, 2180,
                1160, 2780, 1580, 2110, 2160, 2750, 1190, 1560, 1800, 1600, 2400, 3010,
                1490, 2920, 4940, 2550, 1250, 2670, 2020, 1460, 1620, 1460, 1570, 2890,
                1840, 2950, 1380, 2350, 4180, 1700, 2200, 3430, 2270, 2180, 1900, 2760,
                4536, 2160, 1860, 2680, 1540, 2110, 2950, 2410, 2230, 1980, 1610, 2640,
                1930, 1940, 1810, 1900, 3140, 1370, 2180, 4250 };

            var exact = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exact.Add(new ExactData(years[i], data[i]));
            }

            var dataFrame = new DataFrame() { ExactSeries = exact };
            dataFrame.SetLowOutliersFromMGBT();
            dataFrame.ProcessThresholdSeries();
            dataFrame.CalculatePlottingPositions();

            var parameters = new double[] { 3.32862315858514, 0.140287994140121, 0.396626124058735, 0.421 };
            // mean, standard deviation, skew, weighted skew

            return (dataFrame, parameters);
        }

        /// <summary>
        /// Example 2: Analysis with Low Outliers — Orestimba Creek near Newman, CA (USGS 11274500).
        /// </summary>
        /// <returns>
        /// A tuple of the configured <see cref="DataFrame"/> and expected LP-III parameters
        /// [mean, standard deviation, skew] in log-space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Systematic record of 82 annual peak flows (1932–2013) including many zero-flow years.
        /// Low outliers identified by MGBT. Exercises the conditional probability adjustment for
        /// zero flows and the MGBT low-outlier screening procedure.
        /// </para>
        /// <para>Reference: B17C Guideline, Example 2, Tables 4–6.</para>
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GetExample2()
        {
            var years = new int[] {
                1932, 1933, 1934, 1935, 1936, 1937, 1938, 1939, 1940, 1941, 1942, 1943,
                1944, 1945, 1946, 1947, 1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955,
                1956, 1957, 1958, 1959, 1960, 1961, 1962, 1963, 1964, 1965, 1966, 1967,
                1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975, 1976, 1977, 1978, 1979,
                1980, 1981, 1982, 1983, 1984, 1985, 1986, 1987, 1988, 1989, 1990, 1991,
                1992, 1993, 1994, 1995, 1996, 1997, 1998, 1999, 2000, 2001, 2002, 2003,
                2004, 2005, 2006, 2007, 2008, 2009, 2010, 2011, 2012, 2013 };
            var data = new double[] { 4260, 345, 516, 1320, 1200, 2180, 3230, 115, 3440,
                3070, 1880, 6450, 1290, 5970, 782, 0, 0, 335, 175, 2920, 3660, 147, 0, 16,
                5620, 1440, 10200, 5380, 448, 0, 1740, 8300, 156, 560, 128, 4200, 0, 5080,
                1010, 584, 0, 1510, 922, 1010, 0, 0, 4360, 1270, 5210, 1130, 5550, 6360,
                991, 50, 6990, 112, 0, 0, 4, 1260, 888, 4190, 12, 12000, 3130, 3320, 9470,
                833, 2550, 958, 425, 2790, 2990, 1820, 1630, 0, 2110, 310, 4400, 4440,
                0, 6250 };

            var exact = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exact.Add(new ExactData(years[i], data[i]));
            }

            var dataFrame = new DataFrame() { ExactSeries = exact };
            dataFrame.SetLowOutliersFromMGBT();
            dataFrame.ProcessThresholdSeries();
            dataFrame.CalculatePlottingPositions();

            var parameters = new double[] { 3.02266304070359, 0.68208709211999, -0.929108050139471 };
            // mean, standard deviation, skew

            return (dataFrame, parameters);
        }

        /// <summary>
        /// Example 3: Broken Record — Back Creek near Jones Springs, WV (USGS 01614500).
        /// </summary>
        /// <returns>
        /// A tuple of the configured <see cref="DataFrame"/> and expected LP-III parameters
        /// [mean, standard deviation, skew] in log-space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Broken systematic record of 56 annual peak flows with three gap periods (1932–1938,
        /// 1976–1992, 1999–2003) treated as threshold-censored at 21,000 cfs. Manual low-outlier
        /// threshold set at 2,000 cfs. Exercises broken-record and perception-threshold handling.
        /// </para>
        /// <para>Reference: B17C Guideline, Example 3, Tables 7–9.</para>
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GetExample3()
        {
            var years = new int[] {
                1929, 1930, 1931, 1936, 1939, 1940, 1941, 1942, 1943, 1944, 1945, 1946,
                1947, 1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958,
                1959, 1960, 1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970,
                1971, 1972, 1973, 1974, 1975, 1993, 1994, 1995, 1996, 1997, 1998, 2004,
                2005, 2006, 2007, 2008, 2009, 2010, 2011, 2012 };
            var data = new double[] {
                8750, 15500, 4060, 22000, 6300, 3130, 4160, 6700, 22400, 3880, 8050,
                4020, 1600, 4460, 4230, 3010, 9150, 5100, 9820, 6200, 10700, 3880, 3420,
                3240, 6800, 3740, 4700, 4380, 5190, 3960, 5600, 4670, 7080, 4640, 536,
                6680, 8360, 18700, 5210, 4680, 7940, 11800, 8730, 2300, 13900, 4190, 6370,
                9460, 6560, 2000, 5040, 7670, 4830, 9070, 10300, 4650 };

            var exactSeries = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exactSeries.Add(new ExactData(years[i], data[i]));
            }

            var thresholdSeries = new ThresholdSeries
            {
                new ThresholdData(1932, 1938, 21000),
                new ThresholdData(1976, 1992, 21000),
                new ThresholdData(1999, 2003, 21000)
            };

            var dataFrame = new DataFrame()
            {
                ExactSeries = exactSeries,
                ThresholdSeries = thresholdSeries,
                LowOutlierThreshold = 2000
            };
            dataFrame.SetLowOutliersFromThreshold();
            dataFrame.ProcessThresholdSeries();
            dataFrame.CalculatePlottingPositions();

            var parameters = new double[] { 3.75983428546661, 0.243406211005975, 0.144442997114291 };
            // mean, standard deviation, skew

            return (dataFrame, parameters);
        }

        /// <summary>
        /// Example 4: Historical Data — Arkansas River at Pueblo, CO (USGS 07099500).
        /// </summary>
        /// <returns>
        /// A tuple of the configured <see cref="DataFrame"/> and expected LP-III parameters
        /// [mean, standard deviation, skew] in log-space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Systematic record of 81 annual peak flows (1895–1976) plus 4 historical interval-censored
        /// floods (1864, 1893, 1894, 1921) and threshold series extending back to 1165 AD.
        /// Exercises historical data, interval-censored observations, and long perception thresholds.
        /// </para>
        /// <para>Reference: B17C Guideline, Example 4, Tables 10–13.</para>
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GetExample4()
        {
            var years = new int[] {
                1895, 1896, 1897, 1898, 1899, 1900, 1901, 1902, 1903, 1904, 1905, 1906, 1907,
                1908, 1909, 1910, 1911, 1912, 1913, 1914, 1915, 1916, 1917, 1918, 1919, 1920,
                1922, 1923, 1924, 1925, 1926, 1927, 1928, 1929, 1930, 1931, 1932, 1933, 1934,
                1935, 1936, 1937, 1938, 1939, 1940, 1941, 1942, 1943, 1944, 1945, 1946, 1947,
                1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960,
                1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973,
                1974, 1975, 1976 };
            var data = new double[] {
                6100, 16500, 4300, 7500, 8800, 7600, 11100, 30000, 10500, 8500, 8000, 11000,
                6600, 7600, 5800, 8400, 3700, 10500, 7800, 7500, 17000, 8900, 6800, 9600, 6300,
                8500, 8850, 25600, 6510, 4930, 4520, 12400, 7800, 10500, 6050, 3560, 4380, 8630,
                2580, 9880, 11200, 9300, 11200, 2910, 3860, 7560, 10300, 3320, 5980, 9290, 7050,
                7280, 10900, 12800, 8700, 9300, 4740, 6770, 10200, 11100, 8010, 9070, 4540, 2820,
                5260, 5760, 3540, 8360, 2840, 23500, 10600, 5870, 5190, 6620, 6300, 3360, 3360,
                6760, 5440, 10200, 12800 };

            var exactSeries = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exactSeries.Add(new ExactData(years[i], data[i]));
            }

            var intervalSeries = new IntervalSeries
            {
                new IntervalData(1864, 41000, 50500, 60000),
                new IntervalData(1893, 20000, 22500, 25000),
                new IntervalData(1894, 35000, 37500, 40000),
                new IntervalData(1921, 80000, 91500, 103000)
            };

            var thresholdSeries = new ThresholdSeries
            {
                new ThresholdData(1165, 1858, 150000),
                new ThresholdData(1859, 1892, 40000),
                new ThresholdData(1893, 1894, 19900),
                new ThresholdData(1977, 2004, 20000)
            };

            var dataFrame = new DataFrame()
            {
                ExactSeries = exactSeries,
                IntervalSeries = intervalSeries,
                ThresholdSeries = thresholdSeries
            };
            dataFrame.SetLowOutliersFromMGBT();
            dataFrame.ProcessThresholdSeries();
            dataFrame.CalculatePlottingPositions();

            var parameters = new double[] { 3.8857772463796, 0.245920859300769, 0.817849936502285 };
            // mean, standard deviation, skew

            return (dataFrame, parameters);
        }

        /// <summary>
        /// Example 5: Crest Stage Gage Censored Data — Bear Creek at Ottumwa, IA (USGS 06903700).
        /// </summary>
        /// <returns>
        /// A tuple of the configured <see cref="DataFrame"/> and expected LP-III parameters
        /// [mean, standard deviation, skew] in log-space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Systematic record of 50 annual peak flows (1965–2014) with 9 variable perception thresholds
        /// from crest-stage gage base elevations. Manual low-outlier threshold set at 1,200 cfs.
        /// Exercises variable censoring thresholds across the record period.
        /// </para>
        /// <para>Reference: B17C Guideline, Example 5, Tables 14–16.</para>
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GetExample5()
        {
            var years = new int[] {
                1965, 1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975, 1976, 1977,
                1978, 1979, 1980, 1981, 1982, 1983, 1984, 1985, 1986, 1987, 1988, 1989, 1990,
                1991, 1992, 1993, 1994, 1995, 1996, 1997, 1998, 1999, 2000, 2001, 2002, 2003,
                2004, 2005, 2006, 2007, 2008, 2009, 2010, 2011, 2012, 2013, 2014 };
            var data = new double[] {
                4000, 1180, 2880, 1310, 1420, 3130, 1180, 1620, 1570, 2060, 705, 3340, 3530,
                2010, 1830, 2240, 2770, 4030, 2180, 1780, 1610, 1910, 990, 899, 1820, 3120,
                1850, 1840, 2410, 1400, 1560, 3130, 714, 1940, 2840, 3520, 2430, 2670, 560,
                3000, 859, 710, 2390, 3160, 2520, 3750, 2600, 1450, 3850, 1200 };

            var exactSeries = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exactSeries.Add(new ExactData(years[i], data[i]));
            }

            var thresholdSeries = new ThresholdSeries
            {
                new ThresholdData(1965, 1972, 1180),
                new ThresholdData(1973, 1991, 705),
                new ThresholdData(1992, 2001, 714),
                new ThresholdData(2002, 2002, 743),
                new ThresholdData(2003, 2003, 560),
                new ThresholdData(2004, 2005, 700),
                new ThresholdData(2006, 2009, 710),
                new ThresholdData(2010, 2012, 661),
                new ThresholdData(2013, 2014, 700)
            };

            var dataFrame = new DataFrame()
            {
                ExactSeries = exactSeries,
                ThresholdSeries = thresholdSeries,
                LowOutlierThreshold = 1200
            };
            dataFrame.SetLowOutliersFromThreshold();
            dataFrame.ProcessThresholdSeries();
            dataFrame.CalculatePlottingPositions();

            var parameters = new double[] { 3.27868610634695, 0.233135026582499, -0.92540725650213 };
            // mean, standard deviation, skew

            return (dataFrame, parameters);
        }

        /// <summary>
        /// Example 6: Historic Data and Low Outliers — Santa Cruz River at Lochiel, AZ (USGS 09480500).
        /// </summary>
        /// <returns>
        /// A tuple of the configured <see cref="DataFrame"/> and expected LP-III parameters
        /// [mean, standard deviation, skew] in log-space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Systematic record of 65 annual peak flows (1949–2013) with one historical perception
        /// threshold (1927–1948) at 12,000 cfs. Low outliers identified by MGBT. Exercises the
        /// combination of historical information with automatic low-outlier detection.
        /// </para>
        /// <para>Reference: B17C Guideline, Example 6, Tables 17–19.</para>
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GetExample6()
        {
            var years = new int[] {
                1949, 1950, 1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960,
                1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970, 1971, 1972,
                1973, 1974, 1975, 1976, 1977, 1978, 1979, 1980, 1981, 1982, 1983, 1984,
                1985, 1986, 1987, 1988, 1989, 1990, 1991, 1992, 1993, 1994, 1995, 1996,
                1997, 1998, 1999, 2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008,
                2009, 2010, 2011, 2012, 2013 };
            var data = new double[] {
                1650, 4520, 2560, 550, 3320, 1570, 4300, 1360, 688, 380, 243, 625, 1120,
                8, 2390, 2330, 4810, 1780, 1870, 986, 484, 880, 2830, 2070, 1490, 1730,
                3330, 3540, 1130, 12000, 1060, 406, 1110, 2640, 1120, 12000, 850, 4210,
                291, 804, 871, 3510, 17, 483, 4880, 478, 2020, 1860, 2970, 1110, 4870,
                2240, 1080, 2, 22, 256, 73, 5940, 3060, 1180, 1530, 392, 95, 12, 612 };

            var exactSeries = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exactSeries.Add(new ExactData(years[i], data[i]));
            }

            var thresholdSeries = new ThresholdSeries
            {
                new ThresholdData(1927, 1948, 12000)
            };

            var dataFrame = new DataFrame()
            {
                ExactSeries = exactSeries,
                ThresholdSeries = thresholdSeries,
            };
            dataFrame.SetLowOutliersFromMGBT();
            dataFrame.ProcessThresholdSeries();
            dataFrame.CalculatePlottingPositions();

            var parameters = new double[] { 3.06910653275708, 0.489820621740209, -0.462278724495001 };
            // mean, standard deviation, skew

            return (dataFrame, parameters);
        }

        /// <summary>
        /// Example 7: Paleoflood Record — American River at Fair Oaks, CA (USGS 11446500).
        /// </summary>
        /// <returns>
        /// A tuple of the configured <see cref="DataFrame"/> and expected LP-III parameters
        /// [mean, standard deviation, skew] in log-space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Systematic record of 77 annual peak flows (1905–1997) plus 5 paleoflood interval-censored
        /// observations and 10 perception thresholds extending back to 1 AD. Exercises the full
        /// paleoflood analysis with long non-exceedance records and interval-censored paleofloods.
        /// </para>
        /// <para>Reference: B17C Guideline, Example 7, Tables 20–22.</para>
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GetExample7()
        {
            var years = new int[] {
                1905, 1906, 1907, 1908, 1909, 1911, 1914, 1915, 1916, 1917, 1919, 1920, 1921, 1922,
                1923, 1924, 1925, 1926, 1927, 1928, 1930, 1931, 1932, 1933, 1934, 1935, 1936, 1937,
                1938, 1939, 1940, 1941, 1942, 1943, 1944, 1945, 1946, 1947, 1948, 1949, 1950, 1951,
                1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960, 1961, 1962, 1963, 1964, 1965,
                1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975, 1976, 1978, 1979, 1980,
                1981, 1982, 1983, 1984, 1985, 1986, 1997 };
            var data = new double[] {
                24200, 59700, 156000, 10300, 119000, 81300, 74100, 47900, 40700, 42300, 67500, 20100,
                39200, 31600, 39000, 14000, 99500, 27400, 67700, 163000, 24400, 9900, 21100, 16500,
                22600, 60900, 58300, 33000, 114000, 10900, 89200, 38800, 83200, 152000, 20100, 94400,
                42200, 27900, 21000, 37500, 34400, 180000, 37200, 49700, 42600, 10800, 219000, 42000,
                54000, 20000, 75000, 8000, 40000, 240000, 24000, 260000, 6500, 46000, 30000, 120000,
                122000, 48000, 12000, 69000, 55000, 46000, 15000, 40000, 33000, 175000, 20000, 152000,
                93000, 88000, 17000, 259000, 298000 };

            var exactSeries = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exactSeries.Add(new ExactData(years[i], data[i]));
            }

            var intervalSeries = new IntervalSeries
            {
                new IntervalData(650, 600000, 725000, 850000),
                new IntervalData(1437, 400000, 475000, 550000),
                new IntervalData(1574, 400000, 475000, 550000),
                new IntervalData(1711, 400000, 475000, 550000),
                new IntervalData(1862, 262000, 280000, 300000)
            };

            var thresholdSeries = new ThresholdSeries
            {
                new ThresholdData(1, 1301, 599000),
                new ThresholdData(1302, 1847, 399000),
                new ThresholdData(1848, 1904, 261000),
                new ThresholdData(1910, 1910, 150000),
                new ThresholdData(1912, 1913, 150000),
                new ThresholdData(1918, 1918, 150000),
                new ThresholdData(1929, 1929, 150000),
                new ThresholdData(1977, 1977, 150000),
                new ThresholdData(1987, 1996, 150000),
                new ThresholdData(1998, 2000, 150000)
            };

            var dataFrame = new DataFrame()
            {
                ExactSeries = exactSeries,
                IntervalSeries = intervalSeries,
                ThresholdSeries = thresholdSeries
            };
            dataFrame.SetLowOutliersFromMGBT();
            dataFrame.ProcessThresholdSeries();
            dataFrame.CalculatePlottingPositions();

            var parameters = new double[] { 4.653457, 0.376721, -0.101163 };
            // mean, standard deviation, skew

            return (dataFrame, parameters);
        }

        /// <summary>
        /// Example 4 with uncertain data instead of intervals.
        /// </summary>
        /// <returns>
        /// A tuple of the configured <see cref="DataFrame"/> and expected LP-III parameters
        /// [mean, standard deviation, skew] in log-space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Replacing intervals with uniform distributions should give the same result.
        /// </para>
        /// <para>Reference: B17C Guideline, Example 4, Tables 10–13.</para>
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GetExample4_Uncertain()
        {
            var years = new int[] {
                1895, 1896, 1897, 1898, 1899, 1900, 1901, 1902, 1903, 1904, 1905, 1906, 1907,
                1908, 1909, 1910, 1911, 1912, 1913, 1914, 1915, 1916, 1917, 1918, 1919, 1920,
                1922, 1923, 1924, 1925, 1926, 1927, 1928, 1929, 1930, 1931, 1932, 1933, 1934,
                1935, 1936, 1937, 1938, 1939, 1940, 1941, 1942, 1943, 1944, 1945, 1946, 1947,
                1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960,
                1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973,
                1974, 1975, 1976 };
            var data = new double[] {
                6100, 16500, 4300, 7500, 8800, 7600, 11100, 30000, 10500, 8500, 8000, 11000,
                6600, 7600, 5800, 8400, 3700, 10500, 7800, 7500, 17000, 8900, 6800, 9600, 6300,
                8500, 8850, 25600, 6510, 4930, 4520, 12400, 7800, 10500, 6050, 3560, 4380, 8630,
                2580, 9880, 11200, 9300, 11200, 2910, 3860, 7560, 10300, 3320, 5980, 9290, 7050,
                7280, 10900, 12800, 8700, 9300, 4740, 6770, 10200, 11100, 8010, 9070, 4540, 2820,
                5260, 5760, 3540, 8360, 2840, 23500, 10600, 5870, 5190, 6620, 6300, 3360, 3360,
                6760, 5440, 10200, 12800 };

            var exactSeries = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exactSeries.Add(new ExactData(years[i], data[i]));
            }

            var uncertainSeries = new UncertainSeries
            {
                new UncertainData(1864, new Uniform(41000, 60000)),
                new UncertainData(1893, new Uniform(20000, 25000)),
                new UncertainData(1894, new Uniform(35000, 40000)),
                new UncertainData(1921, new Uniform(80000, 103000))
            };

            var thresholdSeries = new ThresholdSeries
            {
                new ThresholdData(1165, 1858, 150000),
                new ThresholdData(1859, 1892, 40000),
                new ThresholdData(1893, 1894, 19900),
                new ThresholdData(1977, 2004, 20000)
            };

            var dataFrame = new DataFrame()
            {
                ExactSeries = exactSeries,
                UncertainSeries = uncertainSeries,
                ThresholdSeries = thresholdSeries
            };
            dataFrame.SetLowOutliersFromMGBT();
            dataFrame.ProcessThresholdSeries();
            dataFrame.CalculatePlottingPositions();

            var parameters = new double[] { 3.8857772463796, 0.245920859300769, 0.817849936502285 };
            // mean, standard deviation, skew

            return (dataFrame, parameters);
        }

        /// <summary>
        /// Example 7 with uncertain data instead of intervals.
        /// </summary>
        /// <returns>
        /// A tuple of the configured <see cref="DataFrame"/> and expected LP-III parameters
        /// [mean, standard deviation, skew] in log-space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Replacing intervals with uniform distributions should give the same result.
        /// </para>
        /// <para>Reference: B17C Guideline, Example 7, Tables 20–22.</para>
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GetExample7_Uncertain()
        {
            var years = new int[] {
                1905, 1906, 1907, 1908, 1909, 1911, 1914, 1915, 1916, 1917, 1919, 1920, 1921, 1922,
                1923, 1924, 1925, 1926, 1927, 1928, 1930, 1931, 1932, 1933, 1934, 1935, 1936, 1937,
                1938, 1939, 1940, 1941, 1942, 1943, 1944, 1945, 1946, 1947, 1948, 1949, 1950, 1951,
                1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960, 1961, 1962, 1963, 1964, 1965,
                1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975, 1976, 1978, 1979, 1980,
                1981, 1982, 1983, 1984, 1985, 1986, 1997 };
            var data = new double[] {
                24200, 59700, 156000, 10300, 119000, 81300, 74100, 47900, 40700, 42300, 67500, 20100,
                39200, 31600, 39000, 14000, 99500, 27400, 67700, 163000, 24400, 9900, 21100, 16500,
                22600, 60900, 58300, 33000, 114000, 10900, 89200, 38800, 83200, 152000, 20100, 94400,
                42200, 27900, 21000, 37500, 34400, 180000, 37200, 49700, 42600, 10800, 219000, 42000,
                54000, 20000, 75000, 8000, 40000, 240000, 24000, 260000, 6500, 46000, 30000, 120000,
                122000, 48000, 12000, 69000, 55000, 46000, 15000, 40000, 33000, 175000, 20000, 152000,
                93000, 88000, 17000, 259000, 298000 };

            var exactSeries = new ExactSeries();
            for (int i = 0; i < years.Length; i++)
            {
                exactSeries.Add(new ExactData(years[i], data[i]));
            }

            var uncertainSeries = new UncertainSeries
            {
                new UncertainData(650, new Uniform(600000, 850000)),
                new UncertainData(1437, new Uniform(400000, 550000)),
                new UncertainData(1574, new Uniform(400000, 550000)),
                new UncertainData(1711, new Uniform(400000, 550000)),
                new UncertainData(1862, new Uniform(262000, 300000))
            };

            var thresholdSeries = new ThresholdSeries
            {
                new ThresholdData(1, 1301, 599000),
                new ThresholdData(1302, 1847, 399000),
                new ThresholdData(1848, 1904, 261000),
                new ThresholdData(1910, 1910, 150000),
                new ThresholdData(1912, 1913, 150000),
                new ThresholdData(1918, 1918, 150000),
                new ThresholdData(1929, 1929, 150000),
                new ThresholdData(1977, 1977, 150000),
                new ThresholdData(1987, 1996, 150000),
                new ThresholdData(1998, 2000, 150000)
            };

            var dataFrame = new DataFrame()
            {
                ExactSeries = exactSeries,
                UncertainSeries = uncertainSeries,
                ThresholdSeries = thresholdSeries
            };
            dataFrame.SetLowOutliersFromMGBT();
            dataFrame.ProcessThresholdSeries();
            dataFrame.CalculatePlottingPositions();

            var parameters = new double[] { 4.653457, 0.376721, -0.101163 };
            // mean, standard deviation, skew

            return (dataFrame, parameters);
        }

    }
}
