using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>
/// Verification tests for the Bulletin 17C Generalized Method of Moments (GMM) estimation
/// against the seven worked examples published in the B17C guideline.
/// </summary>
/// <remarks>
/// <para>
/// Each test creates a <see cref="Bulletin17CDistribution"/> with a Log-Pearson Type III distribution,
/// fits parameters using <see cref="GeneralizedMethodOfMoments"/>, and verifies that the estimated
/// log-space parameters (mean, standard deviation, skew) match the published values.
/// </para>
/// <para>
/// Reference: England, J.F., Cohn, T.A., Faber, B.A., et al. (2019). Guidelines for Determining
/// Flood Flow Frequency — Bulletin 17C. U.S. Geological Survey Techniques and Methods, Book 4,
/// Chapter B5, 148 p.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CExampleTests
{
    /// <summary>
    /// Example 1: Systematic record — Moose River at Victory, VT (USGS 01134500).
    /// </summary>
    /// <remarks>
    /// 68 years of systematic record (1947–2014) with no historical or censored data.
    /// The true parameters include a fourth value (weighted regional skew = 0.421) that is
    /// not a model parameter; only the first three LP-III parameters are verified.
    /// </remarks>
    [TestMethod]
    public void Test_Example1()
    {
        // Get test configuration
        var (df, trueParameters) = Bulletin17CData.GetExample1();

        // Create B17C model with LP-III distribution
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        // Create GMM estimator and fit the distribution
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Assert that the distribution was fitted successfully
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Example 1 (Moose River).");

        // Assert that the fitted parameters match the published values.
        // Only the first 3 parameters are model parameters; the 4th (weighted skew) is metadata.
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Example 1 (Moose River).");
        }
    }



    /// <summary>
    /// Example 2: Analysis with low outliers — Orestimba Creek near Newman, CA (USGS 11274500).
    /// </summary>
    /// <remarks>
    /// 82 years of systematic record (1932–2013) including many zero-flow years.
    /// Exercises MGBT low-outlier detection and the conditional probability adjustment for zeros.
    /// </remarks>
    [TestMethod]
    public void Test_Example2()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample2();

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Example 2 (Orestimba Creek).");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Example 2 (Orestimba Creek).");
        }
    }

    /// <summary>
    /// Example 3: Broken record — Back Creek near Jones Springs, WV (USGS 01614500).
    /// </summary>
    /// <remarks>
    /// 56 years of broken systematic record with three gap periods treated as threshold-censored
    /// at 21,000 cfs. Manual low-outlier threshold at 2,000 cfs. Exercises broken-record
    /// and perception-threshold handling.
    /// </remarks>
    [TestMethod]
    public void Test_Example3()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample3();

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Example 3 (Back Creek).");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Example 3 (Back Creek).");
        }
    }

    /// <summary>
    /// Example 4: Historical data — Arkansas River at Pueblo, CO (USGS 07099500).
    /// </summary>
    /// <remarks>
    /// 81 years of systematic record (1895–1976) plus 4 historical interval-censored floods
    /// and perception thresholds extending back to 1165 AD. Exercises historical data,
    /// interval-censored observations, and long perception thresholds.
    /// </remarks>
    [TestMethod]
    public void Test_Example4()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample4();

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Example 4 (Arkansas River).");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Example 4 (Arkansas River).");
        }
    }

    /// <summary>
    /// Example 5: Crest stage gage censored data — Bear Creek at Ottumwa, IA (USGS 06903700).
    /// </summary>
    /// <remarks>
    /// 50 years of systematic record (1965–2014) with 9 variable perception thresholds from
    /// crest-stage gage base elevations. Manual low-outlier threshold at 1,200 cfs. Exercises
    /// variable censoring thresholds across the record period.
    /// </remarks>
    [TestMethod]
    public void Test_Example5()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample5();

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Example 5 (Bear Creek).");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Example 5 (Bear Creek).");
        }
    }

    /// <summary>
    /// Example 6: Historic data and low outliers — Santa Cruz River at Lochiel, AZ (USGS 09480500).
    /// </summary>
    /// <remarks>
    /// 65 years of systematic record (1949–2013) with one historical perception threshold
    /// (1927–1948) at 12,000 cfs. Low outliers identified by MGBT. Exercises the combination
    /// of historical information with automatic low-outlier detection.
    /// </remarks>
    [TestMethod]
    public void Test_Example6()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample6();

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Example 6 (Santa Cruz River).");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Example 6 (Santa Cruz River).");
        }
    }

    /// <summary>
    /// Example 7: Paleoflood record — American River at Fair Oaks, CA (USGS 11446500).
    /// </summary>
    /// <remarks>
    /// 77 years of systematic record (1905–1997) plus 5 paleoflood interval-censored observations
    /// and 10 perception thresholds extending back to 1 AD. Exercises the full paleoflood analysis
    /// with long non-exceedance records and interval-censored paleofloods.
    /// </remarks>
    [TestMethod]
    public void Test_Example7()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample7();

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Example 7 (American River).");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Example 7 (American River).");
        }
    }


    /// <summary>
    /// Example 4 with uncertain data instead of intervals.
    /// </summary>
    /// <remarks>
    /// Replacing intervals with uniform distributions should give similar results.
    /// </remarks>
    [TestMethod]
    public void Test_Example4_UncertainData()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample4_Uncertain();

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Example 4 (Arkansas River).");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], Math.Abs(trueParameters[i]) * 0.1,
                $"Parameter[{i}] mismatch for Example 4 (Arkansas River).");
        }
    }

    /// <summary>
    /// Example 7 with uncertain data instead of intervals.
    /// </summary>
    /// <remarks>
    /// Replacing intervals with uniform distributions should give similar results.
    /// </remarks>
    [TestMethod]
    public void Test_Example7_UncertainData()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample7_Uncertain();

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Example 4 (Arkansas River).");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], Math.Abs(trueParameters[i]) * 0.2,
                $"Parameter[{i}] mismatch for Example 4 (Arkansas River).");
        }
    }

    /// <summary>
    /// Verifies the pointwise–aggregate invariant: the column-wise mean of the pointwise
    /// moment conditions matrix must equal the G vector from <see cref="Bulletin17CDistribution.MomentConditions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tests four B17C examples that together exercise all five data types:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Example 1 — exact data only (68 systematic observations).</description></item>
    /// <item><description>Example 3 — exact + low outliers + threshold-censored (broken record).</description></item>
    /// <item><description>Example 4 — exact + interval-censored + threshold-censored (historical floods).</description></item>
    /// <item><description>Example 4 Uncertain — exact + uncertain (Uniform) + threshold-censored.</description></item>
    /// </list>
    /// </remarks>
    [TestMethod]
    public void Test_PointwiseMomentConditions_MeanEqualsG()
    {
        // Each example exercises different data types
        var examples = new (Func<(DataFrame, double[])> getData, string name)[]
        {
            (Bulletin17CData.GetExample1, "Example 1"),
            (Bulletin17CData.GetExample3, "Example 3"),
            (Bulletin17CData.GetExample4, "Example 4"),
            (Bulletin17CData.GetExample4_Uncertain, "Example 4 Uncertain"),
        };

        foreach (var (getData, name) in examples)
        {
            var (df, _) = getData();

            var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
            var gmm = new GeneralizedMethodOfMoments(model);
            gmm.Estimate();
            Assert.IsTrue(gmm.IsEstimated, $"GMM estimation failed for {name}.");

            var parameters = gmm.BestParameterSet.Values;
            int n = df.TotalRecordLength();
            int q = model.NumberOfParameters;

            // Get aggregate G from MomentConditions
            var (G, _) = model.MomentConditions(parameters);

            // Get pointwise matrix [n x q]
            var gi = model.PointwiseMomentConditions!(parameters);
            Assert.AreEqual(n, gi.GetLength(0), $"{name}: row count mismatch.");
            Assert.AreEqual(q, gi.GetLength(1), $"{name}: column count mismatch.");

            // Verify mean of rows equals G
            for (int j = 0; j < q; j++)
            {
                double sum = 0;
                for (int i = 0; i < n; i++)
                    sum += gi[i, j];
                double meanJ = sum / n;
                Assert.AreEqual(G[j], meanJ, 1E-10,
                    $"{name}: Pointwise mean[{j}] = {meanJ:E6} != G[{j}] = {G[j]:E6}");
            }
        }

    }

}

/// <summary>
/// Verifies the Hirsch-Stedinger plotting-position port against peakFQ/Bulletin 17C
/// reference positions for worked Examples 4, 5, and 7.
/// </summary>
/// <remarks>
/// References are exceedance probabilities produced by the peakFQ ARRANGE2, PPLOT2,
/// and PLPOS sequence. These tests deliberately perform no parameter estimation.
/// </remarks>
[TestClass]
public class HirschStedingerPlottingPositionVerificationTests
{
    /// <summary>
    /// Verifies Bulletin 17C Example 4 historical-data plotting positions.
    /// </summary>
    /// <remarks>
    /// Representative systematic ties, record tails, and every historical interval are
    /// pinned to the peakFQ reference output. The fast unit suite separately pins every
    /// systematic ordinate.
    /// </remarks>
    [TestMethod]
    public void Example4_MatchesPeakFQ()
    {
        var (dataFrame, _) = Bulletin17CData.GetExample4();

        Assert.AreEqual(0.690016355873821, dataFrame.ExactSeries[0].PlottingPosition, 1E-12);
        Assert.AreEqual(0.511179638108717, dataFrame.ExactSeries[5].PlottingPosition, 1E-12);
        Assert.AreEqual(0.165428650429517, dataFrame.ExactSeries[6].PlottingPosition, 1E-12);
        Assert.AreEqual(0.153506202578511, dataFrame.ExactSeries[59].PlottingPosition, 1E-12);
        Assert.AreEqual(0.0938939633234761, dataFrame.ExactSeries[^1].PlottingPosition, 1E-12);

        double[] expectedIntervals =
        {
            0.0091324200913242,
            0.0507219548315439,
            0.0211032950758978,
            0.0045662100456621,
        };
        for (int i = 0; i < expectedIntervals.Length; i++)
            Assert.AreEqual(expectedIntervals[i], dataFrame.IntervalSeries[i].PlottingPosition, 1E-12);

        AssertStrict(dataFrame);
    }

    /// <summary>
    /// Verifies Bulletin 17C Example 5 variable crest-stage thresholds.
    /// </summary>
    /// <remarks>
    /// All 50 systematic positions are pinned because this is the negatively skewed,
    /// low-threshold configuration that exposed boundary probabilities in bootstrap samples.
    /// </remarks>
    [TestMethod]
    public void Example5_MatchesPeakFQ()
    {
        var (dataFrame, _) = Bulletin17CData.GetExample5();
        dataFrame.PlottingParameter = 0.4d;

        double[] expected =
        {
            0.031851851851851853, 0.84805555555555556, 0.2508333333333333,
            0.78833333333333322, 0.74851851851851847, 0.19111111111111109,
            0.82814814814814819, 0.64898148148148149, 0.68879629629629624,
            0.4698148148148148, 0.97000000000000008, 0.13138888888888886,
            0.091574074074074058, 0.48972222222222223, 0.58925925925925926,
            0.42999999999999999, 0.2906481481481481, 0.011944444444444443,
            0.44990740740740737, 0.62907407407407412, 0.66888888888888887,
            0.52953703703703703, 0.87124999999999997, 0.89000000000000001,
            0.60916666666666663, 0.21101851851851849, 0.5494444444444444,
            0.56935185185185178, 0.39018518518518519, 0.76842592592592585,
            0.70870370370370361, 0.17120370370370369, 0.93000000000000005,
            0.50962962962962965, 0.27074074074074073, 0.11148148148148147,
            0.37027777777777776, 0.31055555555555553, 0.98999999999999999,
            0.2309259259259259, 0.90874999999999995, 0.95000000000000007,
            0.41009259259259256, 0.15129629629629629, 0.35037037037037039,
            0.07166666666666667, 0.33046296296296296, 0.72861111111111099,
            0.051759259259259262, 0.80824074074074082,
        };

        Assert.AreEqual(expected.Length, dataFrame.ExactSeries.Count);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], dataFrame.ExactSeries[i].PlottingPosition, 1E-12);

        AssertStrict(dataFrame);
    }

    /// <summary>
    /// Verifies Bulletin 17C Example 7 paleoflood plotting positions.
    /// </summary>
    /// <remarks>
    /// The test pins both tied systematic values, both tails, and every paleoflood interval
    /// after applying the Cunnane alpha used by the peakFQ reference calculation.
    /// </remarks>
    [TestMethod]
    public void Example7_MatchesPeakFQ()
    {
        var (dataFrame, _) = Bulletin17CData.GetExample7();
        dataFrame.PlottingParameter = 0.4d;

        Assert.AreEqual(0.739808094808377, dataFrame.ExactSeries[0].PlottingPosition, 1E-12);
        Assert.AreEqual(0.806183580826648, dataFrame.ExactSeries[11].PlottingPosition, 1E-12);
        Assert.AreEqual(0.819458678030302, dataFrame.ExactSeries[34].PlottingPosition, 1E-12);
        Assert.AreEqual(0.00833768638161468, dataFrame.ExactSeries[^1].PlottingPosition, 1E-12);

        double[] expectedIntervals =
        {
            0.00025,
            0.0013043186695279,
            0.00264484978540773,
            0.00398538090128755,
            0.0142509977329467,
        };
        for (int i = 0; i < expectedIntervals.Length; i++)
            Assert.AreEqual(expectedIntervals[i], dataFrame.IntervalSeries[i].PlottingPosition, 1E-12);

        AssertStrict(dataFrame);
    }

    /// <summary>
    /// Asserts that every explicit observation has a finite open-interval plotting position.
    /// </summary>
    /// <param name="dataFrame">Data frame whose exact, uncertain, and interval observations are checked.</param>
    /// <remarks>
    /// Zero and one are invalid because downstream inverse-CDF evaluation would be infinite.
    /// </remarks>
    private static void AssertStrict(DataFrame dataFrame)
    {
        IEnumerable<Data> observations = dataFrame.ExactSeries
            .Concat(dataFrame.UncertainSeries)
            .Concat(dataFrame.IntervalSeries);

        foreach (Data observation in observations)
        {
            Assert.IsTrue(
                double.IsFinite(observation.PlottingPosition) &&
                observation.PlottingPosition > 0d &&
                observation.PlottingPosition < 1d,
                $"Index {observation.Index} has invalid plotting position {observation.PlottingPosition:G17}.");
        }
    }
}
