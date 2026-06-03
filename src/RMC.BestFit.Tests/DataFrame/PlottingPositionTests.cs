using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.InputDataFrame;

/// <summary>
/// Unit tests for <see cref="DataFrame"/> plotting position calculation methods.
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
    /// Validates <see cref="DataFrame.CalculatePlottingPositions"/> using Bulletin 17C Example 4, which
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
        var df = new DataFrame();

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
        df.ThresholdSeries.Add(new ThresholdData(1165, 1858, 150000));
        df.ThresholdSeries.Add(new ThresholdData(1859, 1892, 40000));
        df.ThresholdSeries.Add(new ThresholdData(1893, 1894, 19900));
        df.ThresholdSeries.Add(new ThresholdData(1977, 2004, 20000));

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
    /// Validates <see cref="DataFrame.CalculatePlottingPositions"/> using Bulletin 17C Example 7, which
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
        var df = new DataFrame();

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
        df.ThresholdSeries.Add(new ThresholdData(1, 1301, 599000));
        df.ThresholdSeries.Add(new ThresholdData(1302, 1847, 399000));
        df.ThresholdSeries.Add(new ThresholdData(1848, 1904, 261000));
        df.ThresholdSeries.Add(new ThresholdData(1910, 1910, 150000));
        df.ThresholdSeries.Add(new ThresholdData(1912, 1913, 150000));
        df.ThresholdSeries.Add(new ThresholdData(1918, 1918, 150000));
        df.ThresholdSeries.Add(new ThresholdData(1929, 1929, 150000));
        df.ThresholdSeries.Add(new ThresholdData(1977, 1977, 150000));
        df.ThresholdSeries.Add(new ThresholdData(1987, 1996, 150000));
        df.ThresholdSeries.Add(new ThresholdData(1998, 2000, 150000));

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
    /// Validates <see cref="DataFrame.CalculatePlottingPositions"/> with Blom parameter (α = 0.375) against
    /// <see cref="PlottingPositions.Blom"/> from the Numerics library. The Blom formula is approximately
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
        var df = new DataFrame();
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
    /// Validates <see cref="DataFrame.CalculatePlottingPositions"/> with Cunnane parameter (α = 0.4) against
    /// <see cref="PlottingPositions.Cunnane"/> from the Numerics library. The Cunnane formula provides
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
        var df = new DataFrame();
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
    /// Validates <see cref="DataFrame.CalculatePlottingPositions"/> with Gringorten parameter (α = 0.44) against
    /// <see cref="PlottingPositions.Gringorten"/> from the Numerics library. The Gringorten formula provides
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
        var df = new DataFrame();
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
    /// Validates <see cref="DataFrame.CalculatePlottingPositions"/> with Hazen parameter (α = 0.5) against
    /// <see cref="PlottingPositions.Hazen"/> from the Numerics library. The Hazen formula provides plotting
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
        var df = new DataFrame();
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
    /// Validates <see cref="DataFrame.CalculatePlottingPositions"/> with median parameter (α = 0.3175) against
    /// <see cref="PlottingPositions.Median"/> from the Numerics library. The median formula, also known as
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
        var df = new DataFrame();
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
    /// Validates <see cref="DataFrame.CalculatePlottingPositions"/> with Weibull parameter (α = 0.0) against
    /// <see cref="PlottingPositions.Weibull"/> from the Numerics library. The Weibull formula provides the
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
        var df = new DataFrame();
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

}
