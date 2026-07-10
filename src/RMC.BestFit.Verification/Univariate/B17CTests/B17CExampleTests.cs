using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

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

    /// <summary>
    /// Verifies <c>Test_Example1_Uncertainty</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Example1_Uncertainty()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample1();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 1000;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            var mode = b17CAnalysis.AnalysisResults?.ModeCurve[i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs.UpperCI[i];
            var lower = emaCIs.LowerCI[i];
            Debug.WriteLine(upper + ", " + lower);

        }

    }

    /// <summary>
    /// Verifies <c>Test_Example2_Uncertainty</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Example2_Uncertainty()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample2();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 1000;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            var mode = b17CAnalysis.AnalysisResults?.ModeCurve[i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs.UpperCI[i];
            var lower = emaCIs.LowerCI[i];
            Debug.WriteLine(upper + ", " + lower);

        }
    }

    /// <summary>
    /// Verifies <c>Test_Example3_Uncertainty</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Example3_Uncertainty()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample3();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 1000;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            var mode = b17CAnalysis.AnalysisResults?.ModeCurve[i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs.UpperCI[i];
            var lower = emaCIs.LowerCI[i];
            Debug.WriteLine(upper + ", " + lower);

        }
    }

    /// <summary>
    /// Verifies <c>Test_Example4_Uncertainty</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Example4_Uncertainty()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample4();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap};
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 1000;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            var mode = b17CAnalysis.AnalysisResults?.ModeCurve[i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs.UpperCI[i];
            var lower = emaCIs.LowerCI[i];
            Debug.WriteLine(upper + ", " + lower);

        }
    }

    /// <summary>
    /// Verifies <c>Test_Example5_Uncertainty</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Example5_Uncertainty()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample5();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 1000;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            var mode = b17CAnalysis.AnalysisResults?.ModeCurve[i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs.UpperCI[i];
            var lower = emaCIs.LowerCI[i];
            Debug.WriteLine(upper + ", " + lower);

        }
    }

    /// <summary>
    /// Verifies <c>Test_Example6_Uncertainty</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Example6_Uncertainty()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample6();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 1000;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            var mode = b17CAnalysis.AnalysisResults?.ModeCurve[i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs.UpperCI[i];
            var lower = emaCIs.LowerCI[i];
            Debug.WriteLine(upper + ", " + lower);

        }
    }

    /// <summary>
    /// Verifies <c>Test_Example7_Uncertainty</c>.
    /// </summary>
    [TestMethod]
    public async Task Test_Example7_Uncertainty()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample7();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 1000;
        await b17CAnalysis.RunAsync();

        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            var mode = b17CAnalysis.AnalysisResults?.ModeCurve[i];
            Debug.WriteLine(upper + ", " + lower + ", " + mean + ", " + mode);

        }

        var emaCIs = b17CAnalysis.ComputeCohnStyleConfidenceIntervals();
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = emaCIs.UpperCI[i];
            var lower = emaCIs.LowerCI[i];
            Debug.WriteLine(upper + ", " + lower);

        }
    }

    #region Bootstrap Tests

    /// <summary>
    /// Verifies that Example 5 (crest-stage gage with low perception thresholds and low outlier
    /// threshold at 1200 cfs) produces stable bootstrap results. This example was previously
    /// unstable because Binomial resampling of systematic thresholds created spurious NumberAbove
    /// counts that double-counted observations already in the exact series.
    /// </summary>
    [TestMethod]
    public async Task Test_Example5_Bootstrap()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample5();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.Bootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 100;
        await b17CAnalysis.RunAsync();

        Assert.IsNotNull(b17CAnalysis.AnalysisResults, "Example 5 bootstrap should produce results.");
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            Debug.WriteLine($"Ex5 Bootstrap: p={b17CAnalysis.ProbabilityOrdinates[i]:F6}, lower={lower:F2}, upper={upper:F2}, mean={mean:F2}");
        }
    }

    /// <summary>
    /// Verifies that Example 5 produces stable results using the bias-corrected (pivot) bootstrap.
    /// </summary>
    [TestMethod]
    public async Task Test_Example5_PivotBootstrap()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample5();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 100;
        await b17CAnalysis.RunAsync();

        Assert.IsNotNull(b17CAnalysis.AnalysisResults, "Example 5 pivot bootstrap should produce results.");
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            Debug.WriteLine($"Ex5 Pivot: p={b17CAnalysis.ProbabilityOrdinates[i]:F6}, lower={lower:F2}, upper={upper:F2}, mean={mean:F2}");
        }
    }

    /// <summary>
    /// Verifies that Example 7 (heavy paleoflood censoring with 2000-year threshold spans) remains
    /// stable. Historical thresholds use Binomial count resampling while systematic thresholds
    /// (single-year gaps within the gage record) are cloned.
    /// </summary>
    [TestMethod]
    public async Task Test_Example7_Bootstrap()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample7();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.Bootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 100;
        await b17CAnalysis.RunAsync();

        Assert.IsNotNull(b17CAnalysis.AnalysisResults, "Example 7 bootstrap should produce results.");
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            Debug.WriteLine($"Ex7 Bootstrap: p={b17CAnalysis.ProbabilityOrdinates[i]:F6}, lower={lower:F2}, upper={upper:F2}, mean={mean:F2}");
        }
    }

    /// <summary>
    /// Verifies bootstrap with Example 4 (exact + interval-censored paleofloods + historical thresholds),
    /// which exercises all data type resampling paths.
    /// </summary>
    [TestMethod]
    public async Task Test_Example4_Bootstrap()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample4();

        var b17CDist = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var b17CAnalysis = new Bulletin17CAnalysis(b17CDist) { UncertaintyMethod = UncertaintyMethod.Bootstrap };
        b17CAnalysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        b17CAnalysis.BayesianAnalysis.OutputLength = 100;
        await b17CAnalysis.RunAsync();

        Assert.IsNotNull(b17CAnalysis.AnalysisResults, "Example 4 bootstrap should produce results.");
        for (int i = 0; i < b17CAnalysis.ProbabilityOrdinates.Count; i++)
        {
            var upper = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 1];
            var lower = b17CAnalysis.AnalysisResults?.ConfidenceIntervals[i, 0];
            var mean = b17CAnalysis.AnalysisResults?.MeanCurve[i];
            Debug.WriteLine($"Ex4 Bootstrap: p={b17CAnalysis.ProbabilityOrdinates[i]:F6}, lower={lower:F2}, upper={upper:F2}, mean={mean:F2}");
        }
    }

    #endregion
}
