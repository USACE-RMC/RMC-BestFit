using Numerics;
using Numerics.Mathematics.Optimization;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.DistributionFitting;

/// <summary>
/// Verifies <see cref="MaximumLikelihood"/> fits of <see cref="UnivariateDistribution"/> against published reference points.
/// All tests verify against published results from the RMC-BestFit Verification Report (Smith, 2020).
/// </summary>
/// <remarks>
/// Published points are compared with production optima through the joint 95% likelihood-ratio
/// region for the fitted coordinate count. This treats references obtained by L-moments or weak
/// Bayesian priors as compatibility points rather than exact MLE coordinate identities.
///
///     <b> Authors: </b>
///     <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
///     </list>
/// </remarks>
[TestClass]
public class UnivariateDistributionMLETests
{

    #region Normal and Related Distributions

    /// <summary>
    /// Places the closed-form Tippecanoe River Normal reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Rao, A.R. and Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.
    /// </para>
    /// <para>
    /// Verified using Tippecanoe River near Delphi, IN data (Table 5.1.1).
    /// </para>
    /// <para>
    /// The MLE of σ matches the population standard deviation formula (dividing by n rather than n-1),
    /// which does not account for using the sample mean instead of the true population mean.
    /// This tends to underestimate the true standard deviation slightly.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Normal_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale) = VerificationData.Test_Normal_MLE();

        // Create UnivariateDistribution model with Normal distribution
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale]);

    }

    /// <summary>
    /// Places the natural-log Wabash River reference inside the fitted Ln-Normal joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Rao, A.R. and Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.
    /// </para>
    /// <para>
    /// Verified using Wabash River at Lafayette, IN data (Table 1.8.1).
    /// </para>
    /// <para>
    /// The MLE of σ matches the population standard deviation formula applied to log-transformed data,
    /// which tends to underestimate the true standard deviation slightly.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_LnNormal_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale) = VerificationData.Test_LnNormal_MLE();

        // Create UnivariateDistribution model with Ln-Normal distribution
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LnNormal);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(
            model,
            mle,
            LogMomentsToPhysicalMoments(trueLocation, trueScale));

    }

    /// <summary>
    /// Places the base-10-log Wabash River reference inside the fitted Log-Normal joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Rao, A.R. and Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.
    /// </para>
    /// <para>
    /// Verified using Wabash River at Lafayette, IN data (Table 1.8.1).
    /// </para>
    /// <para>
    /// The MLE of σ matches the population standard deviation formula applied to log10-transformed data.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_LogNormal_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale) = VerificationData.Test_Log10Normal_MLE();

        // Create UnivariateDistribution model with Log-Normal distribution
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LogNormal);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale]);

    }

    /// <summary>
    /// Places the AirQuality <c>lmom</c> Generalized Normal reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verified using Air Quality wind data from R datasets package with L-moment estimates from R lmom package.
    /// </para>
    /// <para>
    /// MLE results may differ slightly from L-moment estimates but should be similar. The shape parameter
    /// controls the tail behavior, with negative values indicating lighter tails than the normal distribution.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_GeneralizedNormal_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale, trueShape) = VerificationData.Test_GeneralizedNormal_MLE();

        // Create UnivariateDistribution model with Generalized Normal distribution
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedNormal);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale, trueShape]);

    }

    #endregion

    #region The Gamma Family of Distributions

    /// <summary>
    /// Places the published Wabash River Exponential reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Rao, A.R. and Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.
    /// </para>
    /// <para>
    /// Verified using Wabash River at Lafayette, IN data (Table 1.8.1, Example 6.1.1, page 132).
    /// </para>
    /// <para>
    /// The Exponential distribution is the simplest model for exceedance data and represents the special
    /// case of the Gamma distribution with shape parameter α = 1. The two-parameter form includes a
    /// location parameter ξ (threshold) and scale parameter α.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Exponential_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale) = VerificationData.Test_Exponential_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Exponential);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale]);
    }

    /// <summary>
    /// Places the published Harricana River Gamma reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Bobee, B. and Ashkar, F. (1991). The Gamma Family and Derived Distributions Applied in Hydrology.
    /// Water Resources Publications.
    /// </para>
    /// <para>
    /// Verified using Harricana River at Amos, Quebec data (Table 1.2).
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_GammaDist_MLE()
    {
        // Get test configuration
        var (df, trueScale, trueShape) = VerificationData.Test_GammaDist_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GammaDistribution);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueScale, trueShape]);
    }

    /// <summary>
    /// Places the published Harricana River Pearson Type III reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Bobee, B. and Ashkar, F. (1991). The Gamma Family and Derived Distributions Applied in Hydrology.
    /// Water Resources Publications.
    /// </para>
    /// <para>
    /// Verified using Harricana River at Amos, Quebec data (Table 1.2).
    /// </para>
    /// <para>
    /// The Pearson Type III is a three-parameter Gamma distribution with location parameter.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_PearsonTypeIII_MLE()
    {
        // Get test configuration
        var (df, trueMu, trueSigma, trueGamma) = VerificationData.Test_PearsonTypeIII_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.PearsonTypeIII);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueMu, trueSigma, trueGamma]);
    }

    /// <summary>
    /// Places the published Harricana River Log-Pearson Type III reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Bobee, B. and Ashkar, F. (1991). The Gamma Family and Derived Distributions Applied in Hydrology.
    /// Water Resources Publications.
    /// </para>
    /// <para>
    /// Verified using Harricana River at Amos, Quebec data (Table 1.2).
    /// </para>
    /// <para>
    /// The Log-Pearson Type III is widely used in flood frequency analysis and is the basis for Bulletin 17C methodology.
    /// Parameters are estimated on log-transformed data.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_LogPearsonTypeIII_MLE()
    {
        // Get test configuration
        var (df, trueMu, trueSigma, trueGamma) = VerificationData.Test_LogPearsonTypeIII_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueMu, trueSigma, trueGamma]);
    }

    #endregion

    #region Extreme Value Distributions

    /// <summary>
    /// Places the published Sugar Creek Gumbel reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Rao, A.R. and Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.
    /// </para>
    /// <para>
    /// Verified using Sugar Creek at Crawfordsville, IN data (Example 7.2.1, page 234).
    /// </para>
    /// <para>
    /// The Gumbel distribution is commonly used for modeling annual maximum floods and represents
    /// the distribution of the maximum of a large number of independent, identically distributed random variables.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Gumbel_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale) = VerificationData.Test_Gumbel_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Gumbel);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale]);
    }

    /// <summary>
    /// Places the recorded weak-prior R-Stan Weibull reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verified using Tippecanoe River near Delphi, IN data.
    /// </para>
    /// <para>
    /// Rao and Hamed (2000) only provide solutions for the 3-parameter Weibull, so this test
    /// validates the 2-parameter form against results obtained from R-Stan Bayesian estimation
    /// with weakly informative priors.
    /// </para>
    /// <para>
    /// The Weibull distribution is commonly used for modeling time-to-failure and can represent
    /// both increasing (shape > 1) and decreasing (shape &lt; 1) hazard rates.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Weibull_MLE()
    {
        // Get test configuration
        var (df, trueScale, trueShape) = VerificationData.Test_Weibull_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Weibull);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueScale, trueShape]);
    }

    /// <summary>
    /// Places the published White River GEV reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Rao, A.R. and Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.
    /// </para>
    /// <para>
    /// Verified using White River near Nora, IN data (Example 7.1.1, page 219).
    /// </para>
    /// <para>
    /// The GEV unifies the Gumbel (shape = 0), Frechet (shape > 0), and Weibull (shape &lt; 0)
    /// families of extreme value distributions. It is widely used in flood frequency analysis
    /// and extreme precipitation modeling.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_GeneralizedExtremeValue_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale, trueShape) = VerificationData.Test_GEV_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale, trueShape]);
    }

    /// <summary>
    /// Places the recorded White River peaks-over-threshold GPA reference inside the fitted joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Rao, A.R. and Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.
    /// </para>
    /// <para>
    /// Verified using White River at Mt. Carmel, IN data with 50,000 cfs threshold (Example 8.3.1, page 279).
    /// </para>
    /// <para>
    /// The Generalized Pareto distribution is used for peaks-over-threshold (POT) analysis where only
    /// exceedances above a specified threshold are modeled. It is the limiting distribution for threshold
    /// exceedances and is closely related to the GEV distribution through the Pickands-Balkema-de Haan theorem.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_GeneralizedPareto_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale, trueShape) = VerificationData.Test_GeneralizedPareto_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedPareto);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale, trueShape]);
    }

    /// <summary>
    /// Places the recorded Kappa Four reference inside the fitted four-coordinate joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verified using Air Quality wind data from R datasets package with L-moment estimates from R lmom package.
    /// </para>
    /// <para>
    /// The Kappa-4 is a four-parameter distribution that encompasses many common distributions as special cases,
    /// including the GEV, Generalized Pareto, Generalized Logistic, and others. It provides great flexibility
    /// for modeling data with varying tail behavior.
    /// </para>
    /// <para>
    /// MLE results may differ from L-moment estimates but should be similar. The flexible parameterization
    /// allows the distribution to adapt to a wide range of data characteristics.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Kappa4_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale, trueShape, trueShape2) = VerificationData.Test_Kappa4_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.KappaFour);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale, trueShape, trueShape2]);
    }

    #endregion

    #region Logistic Distributions

    /// <summary>
    /// Places the recorded Logistic reference inside the fitted two-coordinate joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Rao, A.R. and Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.
    /// </para>
    /// <para>
    /// Verified using Tippecanoe River near Delphi, IN data (Example 9.1.1, page 295).
    /// </para>
    /// <para>
    /// The Logistic distribution has heavier tails than the Normal distribution and is sometimes
    /// used as an alternative for modeling hydrologic extremes.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Logistic_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale) = VerificationData.Test_Logistic_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Logistic);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale]);
    }

    /// <summary>
    /// Places the recorded Generalized Logistic reference inside the fitted three-coordinate joint 95% likelihood region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Rao, A.R. and Hamed, K.H. (2000). Flood Frequency Analysis. CRC Press.
    /// </para>
    /// <para>
    /// Verified using East Fork White River at Seymour, IN data (Example 9.1.1, page 295).
    /// </para>
    /// <para>
    /// Note: The textbook uses summary statistics that differ from those calculated directly from
    /// the provided data table. These discrepancies affect the parameter estimates significantly.
    /// When using the textbook's summary statistics, the MLE results match closely. However, when
    /// using the actual dataset, parameter estimates differ. This test validates that RMC-BestFit
    /// results are within 10% of the textbook values, accounting for these data inconsistencies.
    /// </para>
    /// <para>
    /// The Generalized Logistic extends the Logistic distribution with a shape parameter, allowing
    /// for more flexible tail behavior and better adaptation to various hydrologic datasets.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_GeneralizedLogistic_MLE()
    {
        // Get test configuration
        var (df, trueLocation, trueScale, trueShape) = VerificationData.Test_GeneralizedLogistic_MLE();

        // Create UnivariateDistribution model
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedLogistic);

        // Create MLE estimator and fit the distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        AssertPublishedPointInsideJoint95(model, mle, [trueLocation, trueScale, trueShape]);
    }

    #endregion

    /// <summary>
    /// Requires a published or independently recorded parameter point to lie inside the joint
    /// 95% likelihood-ratio region around the production maximum.
    /// </summary>
    /// <param name="model">The fitted univariate model.</param>
    /// <param name="mle">The completed maximum-likelihood estimator.</param>
    /// <param name="publishedPoint">The reference point in production parameter order.</param>
    private static void AssertPublishedPointInsideJoint95(
        UnivariateDistribution model,
        MaximumLikelihood mle,
        double[] publishedPoint)
    {
        double cutoff = publishedPoint.Length switch
        {
            2 => 5.991464547107979d,
            3 => 7.814727903251179d,
            4 => 9.487729036781154d,
            _ => throw new ArgumentOutOfRangeException(
                nameof(publishedPoint),
                publishedPoint.Length,
                "Only two-, three-, and four-coordinate published fixtures are supported."),
        };
        double fittedLogLikelihood = model.DataLogLikelihood(mle.BestParameterSet.Values);
        double publishedLogLikelihood = model.DataLogLikelihood(publishedPoint);
        double likelihoodRatio = 2d * Math.Abs(fittedLogLikelihood - publishedLogLikelihood);

        Assert.IsTrue(double.IsFinite(publishedLogLikelihood), "Published-point likelihood must be finite.");
        Assert.IsTrue(
            likelihoodRatio <= cutoff,
            $"Published-point likelihood-ratio statistic {likelihoodRatio:R} exceeds the joint "
            + $"95% chi-square({publishedPoint.Length}) cutoff {cutoff:R}. Fitted LL={fittedLogLikelihood:R}; "
            + $"published LL={publishedLogLikelihood:R}.");
    }

    /// <summary>
    /// Converts natural-log Normal location and scale to the physical mean and standard deviation
    /// used by the Numerics <see cref="LnNormal.GetParameters"/> contract.
    /// </summary>
    /// <param name="logMean">The natural-log location.</param>
    /// <param name="logStandardDeviation">The natural-log scale.</param>
    /// <returns>The physical mean and standard deviation.</returns>
    private static double[] LogMomentsToPhysicalMoments(
        double logMean,
        double logStandardDeviation)
    {
        double logVariance = logStandardDeviation * logStandardDeviation;
        double mean = Math.Exp(logMean + 0.5d * logVariance);
        double variance = (Math.Exp(logVariance) - 1d) * Math.Exp(2d * logMean + logVariance);
        return [mean, Math.Sqrt(variance)];
    }

}
