using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.DistributionFitting;

/// <summary>
/// Unit tests for the <see cref="UnivariateDistribution"/> model class and <see cref="MaximumLikelihood"/> estimation.
/// All tests verify against published results from the RMC-BestFit Verification Report (Smith, 2020).
/// </summary>
/// <remarks>
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
    /// Tests Maximum Likelihood Estimation for the Normal distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Normal)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Mu, Math.Abs(trueLocation * 0.01), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Sigma, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");

    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Log-Normal (natural log) distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (LnNormal)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Mu, Math.Abs(trueLocation * 0.01), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Sigma, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");

    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Log-Normal (base-10 log) distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (LogNormal)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Mu, Math.Abs(trueLocation * 0.01), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Sigma, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");

    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Generalized Normal distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GeneralizedNormal)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.01), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.1), "Distribution shape parameter is incorrect.");

    }

    #endregion

    #region The Gamma Family of Distributions

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Exponential distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Exponential)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.01), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");
    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Gamma distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GammaDistribution)model.Distribution;
        Assert.AreEqual(trueScale, dist.Theta, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.01), "Distribution shape parameter is incorrect.");
    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Pearson Type III distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (PearsonTypeIII)model.Distribution;
        Assert.AreEqual(trueMu, dist.Mu, Math.Abs(trueMu * 0.01), "Distribution mean parameter is incorrect.");
        Assert.AreEqual(trueSigma, dist.Sigma, Math.Abs(trueSigma * 0.01), "Distribution standard deviation parameter is incorrect.");
        Assert.AreEqual(trueGamma, dist.Gamma, Math.Abs(trueGamma * 0.01), "Distribution skewness parameter is incorrect.");
    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Log-Pearson Type III distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (LogPearsonTypeIII)model.Distribution;
        Assert.AreEqual(trueMu, dist.Mu, Math.Abs(trueMu * 0.01), "Distribution mean parameter is incorrect.");
        Assert.AreEqual(trueSigma, dist.Sigma, Math.Abs(trueSigma * 0.01), "Distribution standard deviation parameter is incorrect.");
        Assert.AreEqual(trueGamma, dist.Gamma, Math.Abs(trueGamma * 0.01), "Distribution skewness parameter is incorrect.");
    }

    #endregion

    #region Extreme Value Distributions

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Gumbel (Extreme Value Type I) distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Gumbel)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.01), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");
    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Weibull distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Weibull)model.Distribution;
        Assert.AreEqual(trueScale, dist.Lambda, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.01), "Distribution shape parameter is incorrect.");
    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Generalized Extreme Value (GEV) distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.01), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, 0.001, "Distribution shape parameter is incorrect.");
    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Generalized Pareto distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GeneralizedPareto)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.01), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.01), "Distribution shape parameter is incorrect.");
    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Kappa-4 distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (KappaFour)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.1), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.1), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, 0.05, "Distribution shape parameter is incorrect.");
        Assert.AreEqual(trueShape2, dist.Hondo, 0.05, "Distribution shape2 parameter is incorrect.");
    }

    #endregion

    #region Logistic Distributions

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Logistic distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Logistic)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.01), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.01), "Distribution scale parameter is incorrect.");
    }

    /// <summary>
    /// Tests Maximum Likelihood Estimation for the Generalized Logistic distribution.
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
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert that the distributions were fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GeneralizedLogistic)model.Distribution;
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.1), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.1), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.1), "Distribution shape parameter is incorrect.");
    }

    #endregion

}

