using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;
using BestFitThresholdData = RMC.BestFit.Models.ThresholdData;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Unit tests for <c>DataFrame</c> plotting position calculation methods.
/// Validates implementations against HEC-SSP software and the Numerics library.
/// </summary>
/// <remarks>
/// <para>
/// Plotting positions estimate the empirical exceedance probability for each observation in a sample,
/// providing the foundation for graphical distribution fitting and goodness-of-fit assessment. Different
/// plotting position formulas are optimal for different distribution families and sample sizes.
/// </para>
///     <b> Authors: </b>
///     <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
///     </list>
/// </remarks>
[TestClass]
public class PlottingPositionTests
{

    /// <summary>
    /// Tests Hirsch-Stedinger plotting positions for complex mixed-data scenario with perception thresholds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <c>DataFrame.CalculatePlottingPositions</c> using Bulletin 17C Example 4, which
    /// demonstrates a real flood frequency analysis with systematic record, historical flood intervals,
    /// and multiple perception thresholds spanning different time periods.
    /// </para>
    /// <para>
    /// The Hirsch-Stedinger method extends traditional plotting positions to handle:
    /// - Exact observations (systematic gage record)
    /// - Interval data (historical floods with uncertain magnitudes)
    /// - Perception thresholds (incomplete record periods with known detection limits)
    /// </para>
    /// <para>
    /// This comprehensive test ensures correct handling of mixed data types, proper threshold processing,
    /// and accurate plotting position computation for both systematic and historical data. The example
    /// includes 81 years of systematic record plus 4 historical intervals with varying perception thresholds
    /// across multiple epochs.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_PlottingPositions_B17C_Ex4()
    {
        var sysYears = new int[] { 1895, 1896, 1897, 1898, 1899, 1900, 1901, 1902, 1903, 1904, 1905, 1906, 1907, 1908, 1909, 1910, 1911, 1912, 1913, 1914, 1915, 1916, 1917, 1918, 1919, 1920, 1922, 1923, 1924, 1925, 1926, 1927, 1928, 1929, 1930, 1931, 1932, 1933, 1934, 1935, 1936, 1937, 1938, 1939, 1940, 1941, 1942, 1943, 1944, 1945, 1946, 1947, 1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960, 1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975, 1976 };
        var sysValues = new double[] { 6100, 16500, 4300, 7500, 8800, 7600, 11100, 30000, 10500, 8500, 8000, 11000, 6600, 7600, 5800, 8400, 3700, 10500, 7800, 7500, 17000, 8900, 6800, 9600, 6300, 8500, 8850, 25600, 6510, 4930, 4520, 12400, 7800, 10500, 6050, 3560, 4380, 8630, 2580, 9880, 11200, 9300, 11200, 2910, 3860, 7560, 10300, 3320, 5980, 9290, 7050, 7280, 10900, 12800, 8700, 9300, 4740, 6770, 10200, 11100, 8010, 9070, 4540, 2820, 5260, 5760, 3540, 8360, 2840, 23500, 10600, 5870, 5190, 6620, 6300, 3360, 3360, 6760, 5440, 10200, 12800 };
        var sysPP = new double[] { 0.690016355873821, 0.0819715154724692, 0.856930625787917, 0.558869429512745, 0.380032711747642, 0.511179638108717, 0.165428650429517, 0.0285079600148093, 0.213118441833545, 0.427722503151669, 0.475412294555697, 0.177351098280524, 0.642326564469793, 0.523102085959724, 0.737706147277848, 0.439644951002676, 0.880775521489931, 0.236963337535559, 0.49925719025771, 0.546946981661738, 0.0700490676214623, 0.356187816045628, 0.594636773065766, 0.296575576790593, 0.666171460171807, 0.415800055300662, 0.368110263896635, 0.0359126249537208, 0.6542490123208, 0.797318386532883, 0.833085730085903, 0.11773885902549, 0.487334742406703, 0.225040889684552, 0.701938803724828, 0.892697969340938, 0.84500817793691, 0.403877607449655, 0.988077552148993, 0.284653128939586, 0.141583754727504, 0.320420472492607, 0.129661306876497, 0.952310208595972, 0.868853073638924, 0.535024533810731, 0.248885785386566, 0.940387760744965, 0.713861251575834, 0.332342920343614, 0.582714325214759, 0.570791877363752, 0.189273546131531, 0.105816411174483, 0.391955159598648, 0.3084980246416, 0.80924083438389, 0.606559220916773, 0.272730681088579, 0.153506202578511, 0.46348984670469, 0.344265368194621, 0.821163282234897, 0.976155104297986, 0.773473490830869, 0.749628595128855, 0.904620417191945, 0.451567398853683, 0.964232656446979, 0.0433172898926324, 0.201195993982538, 0.725783699426841, 0.785395938681876, 0.630404116618786, 0.678093908022814, 0.928465312893959, 0.916542865042952, 0.618481668767779, 0.761551042979862, 0.260808233237573, 0.0938939633234761 };
        var intPP = new double[] { 0.0091324200913242, 0.0507219548315439, 0.0211032950758978, 0.0045662100456621 };

        // Create data frame
        var df = new BestFitDataFrame();

        // Add exact data
        for (int i = 0; i < sysValues.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(sysYears[i], sysValues[i]));
        }

        // Add interval data
        df.IntervalSeries.Add(new IntervalData(1864, 41000, 49598.38707, 60000));
        df.IntervalSeries.Add(new IntervalData(1893, 20000, 22360.67977, 25000));
        df.IntervalSeries.Add(new IntervalData(1894, 35000, 37416.57387, 40000));
        df.IntervalSeries.Add(new IntervalData(1921, 80000, 90774.44574, 103000));

        // Add thresholds
        df.ThresholdSeries.Add(new BestFitThresholdData(1165, 1858, 150000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1859, 1892, 40000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1893, 1894, 19900));
        df.ThresholdSeries.Add(new BestFitThresholdData(1977, 2004, 20000));

        // Process thresholds
        df.ProcessThresholdSeries();

        // Create plotting positions
        df.CalculatePlottingPositions();

        // Test exact data plotting positions
        for (int i = 0; i < sysPP.Length; i++)
        {
            Assert.AreEqual(df.ExactSeries[i].PlottingPosition, sysPP[i], 1E-12);
        }

        // Test interval data plotting positions
        for (int i = 0; i < intPP.Length; i++)
        {
            Assert.AreEqual(df.IntervalSeries[i].PlottingPosition, intPP[i], 1E-12);
        }

    }

    /// <summary>
    /// Tests Hirsch-Stedinger plotting positions with Cunnane parameter for long historical record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <c>DataFrame.CalculatePlottingPositions</c> using Bulletin 17C Example 7, which
    /// demonstrates extreme historical record length with paleoflood data. This example includes a systematic
    /// record from 1905-1997 plus 5 historical interval floods dating back to 605 AD, spanning nearly 1,400 years.
    /// </para>
    /// <para>
    /// Uses the Cunnane plotting parameter (0.4), which is recommended for the Generalized Extreme Value
    /// distribution and is particularly appropriate for datasets with extreme historical information.
    /// The example includes multiple discontinuous perception threshold periods and demonstrates proper
    /// handling of very long record lengths where traditional plotting positions become numerically challenging.
    /// </para>
    /// <para>
    /// This test validates correct computation of very small exceedance probabilities for rare paleoflood
    /// events and ensures numerical stability across widely varying probability scales.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_PlottingPositions_B17C_Ex7()
    {
        var sysYears = new int[] { 1905, 1906, 1907, 1908, 1909, 1911, 1914, 1915, 1916, 1917, 1919, 1920, 1921, 1922, 1923, 1924, 1925, 1926, 1927, 1928, 1930, 1931, 1932, 1933, 1934, 1935, 1936, 1937, 1938, 1939, 1940, 1941, 1942, 1943, 1944, 1945, 1946, 1947, 1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960, 1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975, 1976, 1978, 1979, 1980, 1981, 1982, 1983, 1984, 1985, 1986, 1997 };
        var sysValues = new double[] { 24200, 59700, 156000, 10300, 119000, 81300, 74100, 47900, 40700, 42300, 67500, 20100, 39200, 31600, 39000, 14000, 99500, 27400, 67700, 163000, 24400, 9900, 21100, 16500, 22600, 60900, 58300, 33000, 114000, 10900, 89200, 38800, 83200, 152000, 20100, 94400, 42200, 27900, 21000, 37500, 34400, 180000, 37200, 49700, 42600, 10800, 219000, 42000, 54000, 20000, 75000, 8000, 40000, 240000, 24000, 260000, 6500, 46000, 30000, 120000, 122000, 48000, 12000, 69000, 55000, 46000, 15000, 40000, 33000, 175000, 20000, 152000, 93000, 88000, 17000, 259000, 298000 };
        var sysPP = new double[] { 0.739808094808377, 0.354830275902403, 0.0948343583050208, 0.952209650066845, 0.15570381784759, 0.261904595476824, 0.288454789884132, 0.434480859124329, 0.527406539549909, 0.487581247938946, 0.328280081495095, 0.806183580826648, 0.567231831160871, 0.673432608790105, 0.580506928364526, 0.899109261252228, 0.182254012254898, 0.713257900401068, 0.315004984291441, 0.0846981249153794, 0.726532997604722, 0.965484747270499, 0.779633386419339, 0.872559066844919, 0.766358289215685, 0.341555178698749, 0.368105373106058, 0.646882414382797, 0.168978915051244, 0.925659455659536, 0.222079303865861, 0.59378202556818, 0.248629498273169, 0.104970591694662, 0.819458678030302, 0.195529109458552, 0.5008563451426, 0.699982803197414, 0.792908483622994, 0.607057122771834, 0.633607317179143, 0.0644256581360965, 0.620332219975488, 0.40793066471702, 0.474306150735292, 0.93893455286319, 0.0542894247464551, 0.514131442346254, 0.394655567513366, 0.832733775233956, 0.275179692680478, 0.978759844474153, 0.540681636753563, 0.0441531913568137, 0.753083192012031, 0.0238807245775308, 0.992034941677807, 0.461031053531637, 0.68670770599376, 0.142428720643935, 0.129153623440281, 0.421205761920675, 0.912384358455882, 0.301729887087786, 0.381380470309712, 0.447755956327983, 0.885834164048573, 0.553956733957217, 0.660157511586451, 0.0745618915257379, 0.846008872437611, 0.115106825084304, 0.208804206662207, 0.235354401069515, 0.859283969641265, 0.0340169579671723, 0.00833768638161468 };
        var intPP = new double[] { 0.00025, 0.0013043186695279, 0.00264484978540773, 0.00398538090128755, 0.0142509977329467 };

        // Create data frame
        var df = new BestFitDataFrame();

        // Add exact data
        for (int i = 0; i < sysValues.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(sysYears[i], sysValues[i]));
        }

        // Add interval data
        df.IntervalSeries.Add(new IntervalData(605, 600000, 714142.8429, 850000));
        df.IntervalSeries.Add(new IntervalData(1437, 400000, 469041.576, 550000));
        df.IntervalSeries.Add(new IntervalData(1574, 400000, 469041.576, 550000));
        df.IntervalSeries.Add(new IntervalData(1711, 400000, 469041.576, 550000));
        df.IntervalSeries.Add(new IntervalData(1862, 262000, 280356.9154, 300000));

        // Add thresholds
        df.ThresholdSeries.Add(new BestFitThresholdData(1, 1301, 599000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1302, 1847, 399000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1848, 1904, 261000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1910, 1910, 150000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1912, 1913, 150000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1918, 1918, 150000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1929, 1929, 150000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1977, 1977, 150000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1987, 1996, 150000));
        df.ThresholdSeries.Add(new BestFitThresholdData(1998, 2000, 150000));

        // Process thresholds
        df.ProcessThresholdSeries();

        // Create plotting positions
        df.PlottingParameter = 0.4;
        df.CalculatePlottingPositions();

        // Test exact data plotting positions
        for (int i = 0; i < sysPP.Length; i++)
        {
            Assert.AreEqual(df.ExactSeries[i].PlottingPosition, sysPP[i], 1E-12);
        }

        // Test interval data plotting positions
        for (int i = 0; i < intPP.Length; i++)
        {
            Assert.AreEqual(df.IntervalSeries[i].PlottingPosition, intPP[i], 1E-12);
        }

    }

    /// <summary>
    /// Tests Blom plotting position formula.
    /// </summary>
    /// <remarks>
    /// Validates <c>DataFrame.CalculatePlottingPositions</c> with Blom parameter (α = 0.375) against
    /// <c>PlottingPositions.Blom</c> from the Numerics library. The Blom formula is approximately
    /// unbiased for the Normal distribution and provides plotting positions: p = (i - 0.375)/(n + 0.25).
    /// This formula is widely used and provides reasonable results for most distribution families.
    /// </remarks>
    [TestMethod]
    public void Test_Blom()
    {
        // Create random
        int n = 30;
        var norm = new Normal(100, 15);
        var data = norm.GenerateRandomValues(30, 12345);
        Array.Sort(data);
        Array.Reverse(data);

        // Create data frame and add data
        var df = new BestFitDataFrame();
        for (int i = 0; i < data.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(i, data[i]));
        }

        // Create plotting positions
        df.PlottingParameter = 0.375;
        df.CalculatePlottingPositions();

        // Test exact data plotting positions
        var pp = PlottingPositions.Blom(n);
        for (int i = 0; i < data.Length; i++)
        {
            Assert.AreEqual(pp[i], df.ExactSeries[i].PlottingPosition, 1E-6);
        }
    }

    /// <summary>
    /// Tests Cunnane plotting position formula.
    /// </summary>
    /// <remarks>
    /// Validates <c>DataFrame.CalculatePlottingPositions</c> with Cunnane parameter (α = 0.4) against
    /// <c>PlottingPositions.Cunnane</c> from the Numerics library. The Cunnane formula provides
    /// plotting positions: p = (i - 0.4)/(n + 0.2) and is approximately unbiased for the Gumbel and
    /// Generalized Extreme Value (GEV) distributions. This is the recommended formula in USGS Bulletin 17C
    /// for flood frequency analysis.
    /// </remarks>
    [TestMethod]
    public void Test_Cunnane()
    {
        // Create random
        int n = 30;
        var norm = new Normal(100, 15);
        var data = norm.GenerateRandomValues(30, 12345);
        Array.Sort(data);
        Array.Reverse(data);

        // Create data frame and add data
        var df = new BestFitDataFrame();
        for (int i = 0; i < data.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(i, data[i]));
        }

        // Create plotting positions
        df.PlottingParameter = 0.4;
        df.CalculatePlottingPositions();

        // Test exact data plotting positions
        var pp = PlottingPositions.Cunnane(n);
        for (int i = 0; i < data.Length; i++)
        {
            Assert.AreEqual(pp[i], df.ExactSeries[i].PlottingPosition, 1E-6);
        }
    }

    /// <summary>
    /// Tests Gringorten plotting position formula.
    /// </summary>
    /// <remarks>
    /// Validates <c>DataFrame.CalculatePlottingPositions</c> with Gringorten parameter (α = 0.44) against
    /// <c>PlottingPositions.Gringorten</c> from the Numerics library. The Gringorten formula provides
    /// plotting positions: p = (i - 0.44)/(n + 0.12) and is approximately unbiased for the Weibull distribution.
    /// This formula is commonly used in extreme value analysis and provides slightly more conservative
    /// (higher probability) estimates for extreme events compared to Cunnane.
    /// </remarks>
    [TestMethod]
    public void Test_Gringorten()
    {
        // Create random
        int n = 30;
        var norm = new Normal(100, 15);
        var data = norm.GenerateRandomValues(30, 12345);
        Array.Sort(data);
        Array.Reverse(data);

        // Create data frame and add data
        var df = new BestFitDataFrame();
        for (int i = 0; i < data.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(i, data[i]));
        }

        // Create plotting positions
        df.PlottingParameter = 0.44;
        df.CalculatePlottingPositions();

        // Test exact data plotting positions
        var pp = PlottingPositions.Gringorten(n);
        for (int i = 0; i < data.Length; i++)
        {
            Assert.AreEqual(pp[i], df.ExactSeries[i].PlottingPosition, 1E-6);
        }
    }

    /// <summary>
    /// Tests Hazen plotting position formula.
    /// </summary>
    /// <remarks>
    /// Validates <c>DataFrame.CalculatePlottingPositions</c> with Hazen parameter (α = 0.5) against
    /// <c>PlottingPositions.Hazen</c> from the Numerics library. The Hazen formula provides plotting
    /// positions: p = (i - 0.5)/n and is one of the oldest plotting position formulas. It provides the
    /// median plotting position and is symmetric, making it appropriate when no specific distribution
    /// is assumed. The formula is simple but can be biased for some distributions.
    /// </remarks>
    [TestMethod]
    public void Test_Hazen()
    {
        // Create random
        int n = 30;
        var norm = new Normal(100, 15);
        var data = norm.GenerateRandomValues(30, 12345);
        Array.Sort(data);
        Array.Reverse(data);

        // Create data frame and add data
        var df = new BestFitDataFrame();
        for (int i = 0; i < data.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(i, data[i]));
        }

        // Create plotting positions
        df.PlottingParameter = 0.50;
        df.CalculatePlottingPositions();

        // Test exact data plotting positions
        var pp = PlottingPositions.Hazen(n);
        for (int i = 0; i < data.Length; i++)
        {
            Assert.AreEqual(pp[i], df.ExactSeries[i].PlottingPosition, 1E-6);
        }
    }

    /// <summary>
    /// Tests median (APL) plotting position formula.
    /// </summary>
    /// <remarks>
    /// Validates <c>DataFrame.CalculatePlottingPositions</c> with median parameter (α = 0.3175) against
    /// <c>PlottingPositions.Median</c> from the Numerics library. The median formula, also known as
    /// the APL (Approximate Probability for Large samples) formula, provides plotting positions:
    /// p = (i - 0.3175)/(n + 0.365) and is approximately median-unbiased for a wide range of distributions.
    /// This formula balances performance across multiple distribution families.
    /// </remarks>
    [TestMethod]
    public void Test_Median()
    {
        // Create random
        int n = 30;
        var norm = new Normal(100, 15);
        var data = norm.GenerateRandomValues(30, 12345);
        Array.Sort(data);
        Array.Reverse(data);

        // Create data frame and add data
        var df = new BestFitDataFrame();
        for (int i = 0; i < data.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(i, data[i]));
        }

        // Create plotting positions
        df.PlottingParameter = 0.3175;
        df.CalculatePlottingPositions();

        // Test exact data plotting positions
        var pp = PlottingPositions.Median(n);
        for (int i = 0; i < data.Length; i++)
        {
            Assert.AreEqual(pp[i], df.ExactSeries[i].PlottingPosition, 1E-6);
        }
    }

    /// <summary>
    /// Tests Weibull plotting position formula.
    /// </summary>
    /// <remarks>
    /// Validates <c>DataFrame.CalculatePlottingPositions</c> with Weibull parameter (α = 0.0) against
    /// <c>PlottingPositions.Weibull</c> from the Numerics library. The Weibull formula provides the
    /// simplest plotting positions: p = i/(n + 1) and is also known as the California formula. While simple,
    /// this formula can be biased for most distributions and tends to underestimate extreme probabilities.
    /// It remains popular due to its intuitive interpretation and historical usage.
    /// </remarks>
    [TestMethod]
    public void Test_Weibull()
    {
        // Create random
        int n = 30;
        var norm = new Normal(100, 15);
        var data = norm.GenerateRandomValues(30, 12345);
        Array.Sort(data);
        Array.Reverse(data);

        // Create data frame and add data
        var df = new BestFitDataFrame();
        for (int i = 0; i < data.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(i, data[i]));
        }

        // Create plotting positions
        df.PlottingParameter = 0.0;
        df.CalculatePlottingPositions();

        // Test exact data plotting positions
        var pp = PlottingPositions.Weibull(n);
        for (int i = 0; i < data.Length; i++)
        {
            Assert.AreEqual(pp[i], df.ExactSeries[i].PlottingPosition, 1E-6);
        }
    }

    /// <summary>
    /// Verifies the Example 5 bootstrap edge shape keeps every resampled value unchanged
    /// and assigns only open-interval plotting positions.
    /// </summary>
    /// <remarks>
    /// The arranged counts intentionally reproduce the K=43/K=6 condition that
    /// made the prior recurrence calculate Q=1 at the 743-cfs level. Two observations
    /// fall below their own perception thresholds and are classified as censored only
    /// for plotting; they remain exact observations with their original values.
    /// </remarks>
    [TestMethod]
    public void Test_PlottingPositions_Example5BootstrapEdge_RemainsStrictWithoutChangingSample()
    {
        var values = new double[50];
        for (int i = 0; i < 8; i++)
            values[i] = i == 7 ? 1000d : 2000d + i;
        for (int i = 8; i < 44; i++)
            values[i] = 3000d + i;
        for (int i = 44; i < 49; i++)
            values[i] = 800d + (10d * (i - 44));
        values[49] = 500d;

        var dataFrame = new BestFitDataFrame();
        dataFrame.ExactSeries.SuppressCollectionChanged = true;
        dataFrame.ThresholdSeries.SuppressCollectionChanged = true;

        for (int i = 0; i < values.Length; i++)
            dataFrame.ExactSeries.Add(new ExactData(1965 + i, values[i]));

        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(1965, 1972, 1180));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(1973, 1991, 705));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(1992, 2001, 714));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(2002, 2002, 743));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(2003, 2003, 560));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(2004, 2005, 700));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(2006, 2009, 710));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(2010, 2012, 661));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(2013, 2014, 700));

        double[] originalValues = dataFrame.ExactSeries.Select(data => data.Value).ToArray();
        dataFrame.PlottingParameter = 0.4;
        dataFrame.CalculatePlottingPositions();

        CollectionAssert.AreEqual(originalValues, dataFrame.ExactSeries.Select(data => data.Value).ToArray());
        Assert.AreEqual(50, dataFrame.ExactSeries.Count);
        Assert.IsTrue(dataFrame.ExactSeries.All(
            data => double.IsFinite(data.PlottingPosition) &&
                    data.PlottingPosition > 0d &&
                    data.PlottingPosition < 1d));
        Assert.IsTrue(dataFrame.ExactSeries.Any(data => data.PlottingPosition > 0.98d));
        Assert.IsTrue(dataFrame.ExactSeries.SuppressCollectionChanged,
            "CalculatePlottingPositions must restore the caller's prior suppression state.");
        Assert.IsTrue(dataFrame.ThresholdSeries.SuppressCollectionChanged,
            "CalculatePlottingPositions must restore the caller's prior suppression state.");
    }

    /// <summary>
    /// Verifies a frozen Example 5 bootstrap sample cannot retain duplicate H-S plotting positions.
    /// </summary>
    /// <remarks>
    /// Seed 1 previously assigned the exact same probability to events at indexes 1975 and 1998.
    /// Freezing the generated values keeps this regression isolated from bootstrap mechanics.
    /// </remarks>
    [TestMethod]
    public void Test_PlottingPositions_Example5BootstrapSample_TiesAreSeparated()
    {
        int[] years =
        [
            1965, 1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973, 1974,
            1975, 1976, 1977, 1978, 1979, 1980, 1981, 1982, 1983, 1984,
            1985, 1986, 1987, 1988, 1989, 1990, 1991, 1992, 1993, 1994,
            1995, 1996, 1997, 1998, 1999, 2000, 2001, 2002, 2003, 2004,
            2005, 2006, 2007, 2008, 2009, 2010, 2011, 2012, 2013, 2014
        ];
        double[] values =
        [
            1398.5976334510967, 966.59552180918161, 1974.3223495966256, 2891.8338540508757, 2505.9334402560103,
            1885.0116761582647, 1680.6543191030453, 3847.0027056032231, 930.55140459080849, 2460.2531376046609,
            564.28401727153459, 1396.8182016011826, 1591.5650583425017, 4578.067551996458, 2582.6599531978427,
            2497.8881953825239, 1491.8593917890207, 2380.1796316909777, 2654.2379192321528, 2646.1646459149301,
            3905.9886563585524, 899.41749596844386, 1138.9218977761075, 1753.1788985139399, 2995.6252828849888,
            1166.0571810678148, 2978.705067413357, 1557.4501735296865, 3102.3195776956154, 3424.0748502645079,
            2213.4043771087458, 2695.8435706029095, 2637.9755363911408, 429.59797890129613, 4654.4548499288721,
            3030.215825029497, 3272.014817282608, 763.26237263475105, 2099.7317325275308, 2128.9063276074667,
            1465.0224395732935, 4738.3332968838067, 2611.3950878331016, 1765.2295530597592, 1889.0450114112809,
            2561.4319968329551, 2567.6539372706479, 1989.7098547510266, 1387.028035261912, 1679.0903337032385
        ];

        var source = new BestFitDataFrame
        {
            LowOutlierThreshold = 1200d,
            PlottingParameter = 0.4d
        };
        source.ExactSeries.SuppressCollectionChanged = true;
        source.ThresholdSeries.SuppressCollectionChanged = true;
        for (int i = 0; i < years.Length; i++)
            source.ExactSeries.Add(new ExactData(years[i], values[i]));

        source.ThresholdSeries.Add(new BestFitThresholdData(1965, 1972, 1180));
        source.ThresholdSeries.Add(new BestFitThresholdData(1973, 1991, 705));
        source.ThresholdSeries.Add(new BestFitThresholdData(1992, 2001, 714));
        source.ThresholdSeries.Add(new BestFitThresholdData(2002, 2002, 743));
        source.ThresholdSeries.Add(new BestFitThresholdData(2003, 2003, 560));
        source.ThresholdSeries.Add(new BestFitThresholdData(2004, 2005, 700));
        source.ThresholdSeries.Add(new BestFitThresholdData(2006, 2009, 710));
        source.ThresholdSeries.Add(new BestFitThresholdData(2010, 2012, 661));
        source.ThresholdSeries.Add(new BestFitThresholdData(2013, 2014, 700));
        source.CalculatePlottingPositions();

        double[] positions = source.ExactSeries
            .Select(data => data.PlottingPosition)
            .OrderBy(position => position)
            .ToArray();

        const int higherValueIndex = 1975;
        const int lowerValueIndex = 1998;
        const double expectedCenter = 0.97714285714285709d;
        Data higherValueEvent = source.ExactSeries.Single(data => data.Index == higherValueIndex);
        Data lowerValueEvent = source.ExactSeries.Single(data => data.Index == lowerValueIndex);

        Assert.IsTrue(higherValueEvent.Value > lowerValueEvent.Value);
        Assert.IsTrue(
            higherValueEvent.PlottingPosition < lowerValueEvent.PlottingPosition,
            $"Higher event {higherValueEvent.PlottingPosition:G17}; lower event {lowerValueEvent.PlottingPosition:G17}.");
        Assert.AreEqual(
            expectedCenter,
            (higherValueEvent.PlottingPosition + lowerValueEvent.PlottingPosition) / 2d,
            1E-15,
            "Separating a tie must preserve its original H-S probability center.");

        for (int i = 1; i < positions.Length; i++)
        {
            Assert.IsFalse(
                positions[i - 1].AlmostEquals(positions[i]),
                $"Positions {positions[i - 1]:G17} and {positions[i]:G17} must be distinct.");
        }

        Assert.AreEqual(
            positions.Length,
            positions.Select(position => Math.Round(position, 6)).Distinct().Count(),
            "Plotting positions must remain distinct in the app's six-decimal display.");
    }

    /// <summary>
    /// Verifies explicit values below their own thresholds use the ARRANGE2 censored branch.
    /// </summary>
    /// <remarks>
    /// With one detection and one censored observation at a common threshold, Weibull
    /// plotting positions are 0.25 and 0.75 exceedance probability, respectively.
    /// </remarks>
    [TestMethod]
    public void Test_PlottingPositions_ValueBelowOwnThreshold_UsesCensoredBranch()
    {
        var dataFrame = new BestFitDataFrame();
        dataFrame.ExactSeries.SuppressCollectionChanged = true;
        dataFrame.ThresholdSeries.SuppressCollectionChanged = true;
        dataFrame.ExactSeries.Add(new ExactData(0, 50d));
        dataFrame.ExactSeries.Add(new ExactData(1, 200d));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(0, 1, 100d));

        dataFrame.CalculatePlottingPositions();

        Assert.AreEqual(0.75d, dataFrame.ExactSeries[0].PlottingPosition, 1E-12);
        Assert.AreEqual(0.25d, dataFrame.ExactSeries[1].PlottingPosition, 1E-12);
        Assert.AreEqual(50d, dataFrame.ExactSeries[0].Value);
        Assert.AreEqual(200d, dataFrame.ExactSeries[1].Value);
    }

    /// <summary>
    /// Verifies aggregate below- and above-threshold counts participate in PPLOT2 ranks.
    /// </summary>
    /// <remarks>
    /// Three left-censored placeholders, one finite detection, and one right-censored
    /// placeholder give a detection probability of 2/(2+3). The finite detection is
    /// ordered before the right-censored placeholder.
    /// </remarks>
    [TestMethod]
    public void Test_PlottingPositions_AggregateThresholdCounts_AffectRanks()
    {
        var dataFrame = new BestFitDataFrame();
        dataFrame.ExactSeries.SuppressCollectionChanged = true;
        dataFrame.ThresholdSeries.SuppressCollectionChanged = true;
        dataFrame.ExactSeries.Add(new ExactData(2, 150d));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(0, 4, 100d) { NumberAbove = 1 });

        dataFrame.CalculatePlottingPositions();

        Assert.AreEqual(4d / 15d, dataFrame.ExactSeries[0].PlottingPosition, 1E-12);
        Assert.AreEqual(3, ((BestFitThresholdData)dataFrame.ThresholdSeries[0]).NumberBelow);
        Assert.AreEqual(1, ((BestFitThresholdData)dataFrame.ThresholdSeries[0]).NumberAbove);
    }

    /// <summary>
    /// Verifies observations outside perception windows receive the synthetic unbounded threshold.
    /// </summary>
    /// <remarks>
    /// An outside observation is detected regardless of whether its magnitude is below an
    /// unrelated finite threshold. The two threshold-only years remain left-censored.
    /// </remarks>
    [TestMethod]
    public void Test_PlottingPositions_OutsideThresholdWindow_IsDetected()
    {
        var dataFrame = new BestFitDataFrame();
        dataFrame.ExactSeries.SuppressCollectionChanged = true;
        dataFrame.ThresholdSeries.SuppressCollectionChanged = true;
        dataFrame.ExactSeries.Add(new ExactData(10, 50d));
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(0, 1, 100d));

        dataFrame.CalculatePlottingPositions();

        Assert.AreEqual(0.5d, dataFrame.ExactSeries[0].PlottingPosition, 1E-12);
    }

    /// <summary>
    /// Verifies invalid plotting parameters and impossible processed threshold counts are rejected.
    /// </summary>
    /// <remarks>
    /// Rejecting invalid inputs prevents zero denominators and boundary probabilities; the
    /// routine does not clamp or silently substitute denominators.
    /// </remarks>
    [TestMethod]
    public void Test_PlottingPositions_InvalidInputs_Throw()
    {
        var dataFrame = new BestFitDataFrame();

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => dataFrame.PlottingParameter = double.NaN);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => dataFrame.PlottingParameter = -0.01d);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => dataFrame.PlottingParameter = 1d);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => dataFrame.PlottingParameter = double.PositiveInfinity);

        dataFrame.ThresholdSeries.SuppressCollectionChanged = true;
        dataFrame.ThresholdSeries.Add(new BestFitThresholdData(0, 0, 100d) { NumberAbove = 2 });
        Assert.ThrowsException<InvalidOperationException>(() => dataFrame.CalculatePlottingPositions());

        var overlappingFrame = new BestFitDataFrame();
        overlappingFrame.ThresholdSeries.SuppressCollectionChanged = true;
        overlappingFrame.ThresholdSeries.Add(new BestFitThresholdData(0, 2, 100d));
        overlappingFrame.ThresholdSeries.Add(new BestFitThresholdData(2, 4, 200d));
        Assert.ThrowsException<InvalidOperationException>(
            () => overlappingFrame.CalculatePlottingPositions());
    }

    /// <summary>
    /// Verifies plotting positions remain fast enough for interactive data-entry recalculation.
    /// </summary>
    /// <remarks>
    /// The two-second ceiling for 25,000 observations is intentionally generous to avoid
    /// machine-sensitive microbenchmark failures while guarding against accidental nested
    /// observation-by-threshold scans or other order-of-magnitude regressions.
    /// </remarks>
    [TestMethod]
    public void Test_PlottingPositions_LargeInteractiveFrame_CompletesPromptly()
    {
        const int observationCount = 25000;
        var dataFrame = new BestFitDataFrame();
        dataFrame.ExactSeries.SuppressCollectionChanged = true;
        dataFrame.ThresholdSeries.SuppressCollectionChanged = true;

        for (int i = 0; i < observationCount; i++)
            dataFrame.ExactSeries.Add(new ExactData(i, 1d + ((i * 7919L) % 100003L)));

        for (int i = 0; i < 25; i++)
            dataFrame.ThresholdSeries.Add(
                new BestFitThresholdData(i * 1000, ((i + 1) * 1000) - 1, 100d + i));

        dataFrame.CalculatePlottingPositions();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        dataFrame.CalculatePlottingPositions();
        stopwatch.Stop();

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"Plotting {observationCount:N0} observations took {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
        Assert.IsTrue(dataFrame.ExactSeries.All(
            data => data.PlottingPosition > 0d && data.PlottingPosition < 1d));
    }
}
