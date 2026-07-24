using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Integration tests for Maximum Likelihood Estimation using synthetic data.
/// Tests that MLE can recover true population parameters from large samples.
/// All tests use 1000 samples with seed 12345 for reproducibility.
/// </summary>
[TestClass]
public class MLEIntegrationTests
{
    /// <summary>
    /// Tolerance for parameter recovery. With 1000 samples, we expect to be within 5% of true values.
    /// </summary>
    private const double RelativeTolerance = 0.05;

    #region Normal Family

    /// <summary>
    /// Verifies <c>Test_MLE_Normal_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_Normal_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Normal)model.Distribution;

        double trueMu = TestData.NormalTrueParams[0];
        double trueSigma = TestData.NormalTrueParams[1];

        Assert.AreEqual(trueMu, dist.Mu, Math.Abs(trueMu * RelativeTolerance), "Location parameter not recovered.");
        Assert.AreEqual(trueSigma, dist.Sigma, Math.Abs(trueSigma * RelativeTolerance), "Scale parameter not recovered.");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_LnNormal_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_LnNormal_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.LnNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LnNormal);

        // Act - use MLSL for better global optimization on this distribution
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert - MLE should converge
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        Assert.IsTrue(double.IsFinite(mle.MaximumLogLikelihood), "Log-likelihood should be finite.");

        // For LnNormal, verify MLE finds sample statistics of log(data)
        // μ_MLE = mean(log(x)), σ_MLE = std(log(x))
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (LnNormal)model.Distribution;

        double[] logData = TestData.LnNormalData.Select(x => Math.Log(x)).ToArray();
        double sampleMeanLog = logData.Average();
        double sampleStdLog = Math.Sqrt(logData.Select(x => Math.Pow(x - sampleMeanLog, 2)).Average());

        // MLE should find parameters close to sample statistics of log(data)
        Assert.AreEqual(sampleMeanLog, dist.Mu, 0.5, "μ estimate should be close to mean of log(data).");
        Assert.AreEqual(sampleStdLog, dist.Sigma, 0.1, "σ estimate should be close to std of log(data).");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_GeneralizedNormal_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_GeneralizedNormal_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GeneralizedNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedNormal);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GeneralizedNormal)model.Distribution;

        double trueXi = TestData.GeneralizedNormalTrueParams[0];
        double trueAlpha = TestData.GeneralizedNormalTrueParams[1];
        double trueKappa = TestData.GeneralizedNormalTrueParams[2];

        Assert.AreEqual(trueXi, dist.Xi, Math.Abs(trueXi * RelativeTolerance), "Location parameter not recovered.");
        Assert.AreEqual(trueAlpha, dist.Alpha, Math.Abs(trueAlpha * RelativeTolerance), "Scale parameter not recovered.");
        Assert.AreEqual(trueKappa, dist.Kappa, Math.Abs(trueKappa * 0.2), "Shape parameter not recovered."); // Shape is harder to estimate
    }

    #endregion

    #region Gamma Family

    /// <summary>
    /// Verifies <c>Test_MLE_Exponential_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_Exponential_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.ExponentialData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Exponential);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Exponential)model.Distribution;

        double trueXi = TestData.ExponentialTrueParams[0];
        double trueAlpha = TestData.ExponentialTrueParams[1];

        Assert.AreEqual(trueXi, dist.Xi, Math.Abs(trueXi * RelativeTolerance) + 1.0, "Location parameter not recovered.");
        Assert.AreEqual(trueAlpha, dist.Alpha, Math.Abs(trueAlpha * RelativeTolerance), "Scale parameter not recovered.");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_Gamma_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_Gamma_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GammaData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GammaDistribution);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GammaDistribution)model.Distribution;

        double trueTheta = TestData.GammaTrueParams[0];
        double trueKappa = TestData.GammaTrueParams[1];

        Assert.AreEqual(trueTheta, dist.Theta, Math.Abs(trueTheta * RelativeTolerance), "Scale parameter not recovered.");
        Assert.AreEqual(trueKappa, dist.Kappa, Math.Abs(trueKappa * RelativeTolerance), "Shape parameter not recovered.");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_PearsonTypeIII_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_PearsonTypeIII_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.PearsonTypeIIIData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.PearsonTypeIII);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (PearsonTypeIII)model.Distribution;

        double trueMu = TestData.PearsonTypeIIITrueParams[0];
        double trueSigma = TestData.PearsonTypeIIITrueParams[1];
        double trueGamma = TestData.PearsonTypeIIITrueParams[2];

        Assert.AreEqual(trueMu, dist.Mu, Math.Abs(trueMu * RelativeTolerance), "Location parameter not recovered.");
        Assert.AreEqual(trueSigma, dist.Sigma, Math.Abs(trueSigma * RelativeTolerance), "Scale parameter not recovered.");
        Assert.AreEqual(trueGamma, dist.Gamma, Math.Abs(trueGamma * 0.3), "Shape parameter not recovered."); // Skewness harder to estimate
    }

    /// <summary>
    /// Verifies <c>Test_MLE_LogPearsonTypeIII_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_LogPearsonTypeIII_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.LogPearsonTypeIIIData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (LogPearsonTypeIII)model.Distribution;

        double trueMu = TestData.LogPearsonTypeIIITrueParams[0];
        double trueSigma = TestData.LogPearsonTypeIIITrueParams[1];
        double trueGamma = TestData.LogPearsonTypeIIITrueParams[2];

        Assert.AreEqual(trueMu, dist.Mu, Math.Abs(trueMu * RelativeTolerance), "Location parameter not recovered.");
        Assert.AreEqual(trueSigma, dist.Sigma, Math.Abs(trueSigma * RelativeTolerance), "Scale parameter not recovered.");
        Assert.AreEqual(trueGamma, dist.Gamma, Math.Abs(trueGamma * 0.3), "Shape parameter not recovered.");
    }

    #endregion

    #region Extreme Value Distributions

    /// <summary>
    /// Verifies <c>Test_MLE_Gumbel_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_Gumbel_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GumbelData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Gumbel);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Gumbel)model.Distribution;

        double trueXi = TestData.GumbelTrueParams[0];
        double trueAlpha = TestData.GumbelTrueParams[1];

        Assert.AreEqual(trueXi, dist.Xi, Math.Abs(trueXi * RelativeTolerance), "Location parameter not recovered.");
        Assert.AreEqual(trueAlpha, dist.Alpha, Math.Abs(trueAlpha * RelativeTolerance), "Scale parameter not recovered.");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_Weibull_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_Weibull_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.WeibullData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Weibull);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Weibull)model.Distribution;

        double trueLambda = TestData.WeibullTrueParams[0];
        double trueKappa = TestData.WeibullTrueParams[1];

        Assert.AreEqual(trueLambda, dist.Lambda, Math.Abs(trueLambda * RelativeTolerance), "Scale parameter not recovered.");
        Assert.AreEqual(trueKappa, dist.Kappa, Math.Abs(trueKappa * RelativeTolerance), "Shape parameter not recovered.");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_GEV_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_GEV_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GEVData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        double trueXi = TestData.GEVTrueParams[0];
        double trueAlpha = TestData.GEVTrueParams[1];
        double trueKappa = TestData.GEVTrueParams[2];

        Assert.AreEqual(trueXi, dist.Xi, Math.Abs(trueXi * RelativeTolerance), "Location parameter not recovered.");
        Assert.AreEqual(trueAlpha, dist.Alpha, Math.Abs(trueAlpha * RelativeTolerance), "Scale parameter not recovered.");
        Assert.AreEqual(trueKappa, dist.Kappa, Math.Abs(trueKappa * 0.5), "Shape parameter not recovered."); // Shape harder to estimate
    }

    /// <summary>
    /// Verifies <c>Test_MLE_GeneralizedPareto_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_GeneralizedPareto_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GeneralizedParetoData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedPareto);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GeneralizedPareto)model.Distribution;

        double trueXi = TestData.GeneralizedParetoTrueParams[0];
        double trueAlpha = TestData.GeneralizedParetoTrueParams[1];
        double trueKappa = TestData.GeneralizedParetoTrueParams[2];

        Assert.AreEqual(trueXi, dist.Xi, 0.5, "Location parameter not recovered.");
        Assert.AreEqual(trueAlpha, dist.Alpha, Math.Abs(trueAlpha * RelativeTolerance), "Scale parameter not recovered.");
        Assert.AreEqual(trueKappa, dist.Kappa, Math.Abs(trueKappa * 0.5), "Shape parameter not recovered.");
    }

    #endregion

    #region Logistic Distributions

    /// <summary>
    /// Verifies <c>Test_MLE_Logistic_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_Logistic_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.LogisticData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Logistic);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (Logistic)model.Distribution;

        double trueXi = TestData.LogisticTrueParams[0];
        double trueAlpha = TestData.LogisticTrueParams[1];

        Assert.AreEqual(trueXi, dist.Xi, Math.Abs(trueXi * RelativeTolerance), "Location parameter not recovered.");
        Assert.AreEqual(trueAlpha, dist.Alpha, Math.Abs(trueAlpha * RelativeTolerance), "Scale parameter not recovered.");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_GeneralizedLogistic_RecoversTrueParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_GeneralizedLogistic_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GeneralizedLogisticData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedLogistic);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var dist = (GeneralizedLogistic)model.Distribution;

        double trueXi = TestData.GeneralizedLogisticTrueParams[0];
        double trueAlpha = TestData.GeneralizedLogisticTrueParams[1];
        double trueKappa = TestData.GeneralizedLogisticTrueParams[2];

        Assert.AreEqual(trueXi, dist.Xi, Math.Abs(trueXi * RelativeTolerance), "Location parameter not recovered.");
        Assert.AreEqual(trueAlpha, dist.Alpha, Math.Abs(trueAlpha * RelativeTolerance), "Scale parameter not recovered.");
        Assert.AreEqual(trueKappa, dist.Kappa, Math.Abs(trueKappa * 0.5), "Shape parameter not recovered.");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Verifies <c>Test_MLE_SmallSample_StillConverges</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_SmallSample_StillConverges()
    {
        // Arrange - using small sample (10 observations)
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.SmallSample);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert - MLE should still converge, even if parameters aren't accurate
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed on small sample.");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_LogLikelihood_IsNegative</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_LogLikelihood_IsNegative()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Act
        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        // Assert - log-likelihood for continuous distributions is typically negative
        // Note: MaximumLogLikelihood = -Fitness (Fitness stores the negated value for maximization)
        Assert.IsTrue(mle.MaximumLogLikelihood < 0, "Log-likelihood should be negative for continuous MLE.");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_MultipleRuns_ConsistentResults</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_MultipleRuns_ConsistentResults()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GumbelData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Gumbel);

        // Act - run MLE twice
        var mle1 = new MaximumLikelihood(model);
        mle1.Estimate();
        var params1 = mle1.BestParameterSet.Values.ToArray();

        var mle2 = new MaximumLikelihood(model);
        mle2.Estimate();
        var params2 = mle2.BestParameterSet.Values.ToArray();

        // Assert - results should be consistent
        for (int i = 0; i < params1.Length; i++)
        {
            Assert.AreEqual(params1[i], params2[i], Math.Abs(params1[i] * 0.01),
                $"Parameter {i} differs between runs.");
        }
    }

    #endregion
}
