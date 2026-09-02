using Numerics;
using Numerics.Mathematics.Optimization;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Integration tests for Maximum Likelihood Estimation using synthetic data.
/// The class contains thirteen large-sample family-recovery tests and one separate
/// same-sample analytical comparison for the Ln-Normal maximum-likelihood estimate.
/// Generated-parent recovery cells use 1,000 scalar observations with seed 12345 and Numerics distribution-level maximum-likelihood parameter variances evaluated at the fitted parameters.
/// </summary>
[TestClass]
public class MLEIntegrationTests
{
    #region Normal Family

    /// <summary>
    /// Verifies Normal location and scale recovery on their observed-information scales.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(μ=100, σ=15); fitted
    /// coordinates=(μ, σ). Each 95 percent standardized-error check uses a Numerics distribution-level
    /// maximum-likelihood parameter-variance standard error. No identified response or secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_Normal_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        double trueMu = TestData.NormalTrueParams[0];
        double trueSigma = TestData.NormalTrueParams[1];

        AssertMleRecovery(model, mle, [trueMu, trueSigma], ["mu", "sigma"]);
    }

    /// <summary>
    /// Verifies that the fitted LnNormal parameters match the closed-form MLEs from the
    /// natural-log transformed sample.
    /// </summary>
    /// <remarks>
    /// This is an analytical identity over the seeded scalar Ln-Normal fixture (N=1000; seed=12345;
    /// parent=(μ=3.5, σ=0.4)). It compares fitted (μ, σ) to the sample log-scale MLE coordinates,
    /// not to a parent interval. Coordinate differences are standardized by the known Normal-MLE
    /// covariance at the same-sample optimum, and the fitted objective must remain inside the joint
    /// two-parameter 95 percent likelihood-ratio region. No secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_LnNormal_MatchesClosedFormLogSampleMLE()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.LnNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LnNormal);

        // Act - use the production Differential Evolution MLE policy.
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
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

        double muStandardError = sampleStdLog / Math.Sqrt(logData.Length);
        double sigmaStandardError = sampleStdLog / Math.Sqrt(2d * logData.Length);
        RecoveryAcceptance.AssertFrequentistStandardizedError("mu", dist.Mu, sampleMeanLog, muStandardError);
        RecoveryAcceptance.AssertFrequentistStandardizedError("sigma", dist.Sigma, sampleStdLog, sigmaStandardError);

        double fittedLogLikelihood = LnNormalLogLikelihood(logData, dist.Mu, dist.Sigma);
        double analyticalMaximumLogLikelihood = LnNormalLogLikelihood(logData, sampleMeanLog, sampleStdLog);
        const double chiSquare95TwoDegreesOfFreedom = 5.991464547107979d;
        double likelihoodRatioStatistic = 2d * (analyticalMaximumLogLikelihood - fittedLogLikelihood);
        Assert.IsTrue(
            likelihoodRatioStatistic <= chiSquare95TwoDegreesOfFreedom,
            $"The fitted Ln-Normal objective must lie inside the joint 95% likelihood-ratio region: " +
            $"2*(LL_exact-LL_fit)={likelihoodRatioStatistic:R}, cutoff={chiSquare95TwoDegreesOfFreedom:R}.");
    }

    /// <summary>Verifies Ln-Normal generated-parent recovery separately from the same-sample closed-form identity.</summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(real-space mean=3.5,
    /// real-space standard deviation=0.4). The fitted and parent distributions are compared in the
    /// Numerics maximum-likelihood covariance coordinates (Mu, SigmaSquared), without transforming
    /// the covariance. No
    /// conditional secondary 5% criterion applies because this cell has no identified response grid.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_LnNormal_RecoversGeneratingParameters()
    {
        var df = new Models.DataFrame { ExactSeries = new ExactSeries(TestData.LnNormalData) };
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LnNormal);
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);

        Assert.IsTrue(mle.Estimate(), "Ln-Normal maximum-likelihood estimation failed.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        var actualDistribution = (LnNormal)model.Distribution;
        var parentDistribution = new LnNormal(TestData.LnNormalTrueParams[0], TestData.LnNormalTrueParams[1]);
        AssertMleCovarianceCoordinateRecovery(
            actualDistribution,
            [actualDistribution.Mu, actualDistribution.Sigma * actualDistribution.Sigma],
            [parentDistribution.Mu, parentDistribution.Sigma * parentDistribution.Sigma],
            ["Mu", "SigmaSquared"]);
    }

    /// <summary>
    /// Verifies Generalized Normal location, scale, and shape recovery with true profiling.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(ξ=50, α=10, κ=-0.3); fitted
    /// coordinates=(ξ, α, κ). Haden's approved true-profile alternative supplies the 95 percent
    /// uncertainty intervals through <see cref="MaximumLikelihood.ParameterConfidenceIntervals(double)"/>
    /// with alpha=0.05 after the production MLE succeeds. The conditional secondary 5 percent rule
    /// does not apply.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_GeneralizedNormal_RecoversTrueParameters()
    {
        var df = new Models.DataFrame { ExactSeries = new ExactSeries(TestData.GeneralizedNormalData) };
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedNormal);
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);

        Assert.IsTrue(mle.Estimate(), "Generalized-Normal maximum-likelihood estimation failed.");
        AssertMleTrueProfileRecovery(
            mle,
            TestData.GeneralizedNormalTrueParams,
            ["xi", "alpha", "kappa"]);
    }

    #endregion

    #region Gamma Family

    /// <summary>
    /// Verifies Exponential location and scale recovery on their observed-information scales.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(ξ=10, α=25); fitted
    /// coordinates=(ξ, α). The 95 percent standardized-error checks use Numerics distribution-level
    /// maximum-likelihood parameter-variance standard errors. No identified response or secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_Exponential_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.ExponentialData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Exponential);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        double trueXi = TestData.ExponentialTrueParams[0];
        double trueAlpha = TestData.ExponentialTrueParams[1];

        AssertMleRecovery(model, mle, [trueXi, trueAlpha], ["xi", "alpha"]);
    }

    /// <summary>
    /// Verifies Gamma scale and shape recovery on their observed-information scales.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(θ=5, κ=3); fitted
    /// coordinates=(θ, κ). The 95 percent standardized-error checks use Numerics distribution-level
    /// maximum-likelihood parameter-variance standard errors. No identified response or secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_Gamma_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GammaData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GammaDistribution);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        double trueTheta = TestData.GammaTrueParams[0];
        double trueKappa = TestData.GammaTrueParams[1];

        AssertMleRecovery(model, mle, [trueTheta, trueKappa], ["theta", "kappa"]);
    }

    /// <summary>
    /// Verifies Pearson Type III mean, standard deviation, and skew recovery.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent moment coordinates=(μ=100,
    /// σ=20, γ=0.8). The fitted and parent distributions are compared in Numerics maximum-likelihood
    /// covariance coordinates (Mu, OneOverBeta, Alpha), without transforming the covariance. No
    /// identified response or secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_PearsonTypeIII_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.PearsonTypeIIIData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.PearsonTypeIII);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        model.SetParameterValues(mle.BestParameterSet.Values);
        var actualDistribution = (PearsonTypeIII)model.Distribution;
        var parentDistribution = new PearsonTypeIII(
            TestData.PearsonTypeIIITrueParams[0],
            TestData.PearsonTypeIIITrueParams[1],
            TestData.PearsonTypeIIITrueParams[2]);
        AssertMleCovarianceCoordinateRecovery(
            actualDistribution,
            [actualDistribution.Mu, 1d / actualDistribution.Beta, actualDistribution.Alpha],
            [parentDistribution.Mu, 1d / parentDistribution.Beta, parentDistribution.Alpha],
            ["Mu", "OneOverBeta", "Alpha"]);
    }

    /// <summary>
    /// Verifies Log-Pearson Type III log-mean, log-standard-deviation, and skew recovery.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent moment coordinates=(μ=2,
    /// σ=0.3, γ=0.5). The fitted and parent distributions are compared in Numerics maximum-likelihood
    /// covariance coordinates (Mu, OneOverBeta, Alpha), without transforming the covariance. No
    /// identified response or secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_LogPearsonTypeIII_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.LogPearsonTypeIIIData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        model.SetParameterValues(mle.BestParameterSet.Values);
        var actualDistribution = (LogPearsonTypeIII)model.Distribution;
        var parentDistribution = new LogPearsonTypeIII(
            TestData.LogPearsonTypeIIITrueParams[0],
            TestData.LogPearsonTypeIIITrueParams[1],
            TestData.LogPearsonTypeIIITrueParams[2]);
        AssertMleCovarianceCoordinateRecovery(
            actualDistribution,
            [actualDistribution.Mu, 1d / actualDistribution.Beta, actualDistribution.Alpha],
            [parentDistribution.Mu, 1d / parentDistribution.Beta, parentDistribution.Alpha],
            ["Mu", "OneOverBeta", "Alpha"]);
    }

    #endregion

    #region Extreme Value Distributions

    /// <summary>
    /// Verifies Gumbel location and scale recovery on their observed-information scales.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(ξ=50, α=15); fitted
    /// coordinates=(ξ, α). The 95 percent standardized-error checks use Numerics distribution-level
    /// maximum-likelihood parameter-variance standard errors. No identified response or secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_Gumbel_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GumbelData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Gumbel);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        double trueXi = TestData.GumbelTrueParams[0];
        double trueAlpha = TestData.GumbelTrueParams[1];

        AssertMleRecovery(model, mle, [trueXi, trueAlpha], ["xi", "alpha"]);
    }

    /// <summary>
    /// Verifies Weibull scale and shape recovery on their observed-information scales.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(λ=100, κ=2.5); fitted
    /// coordinates=(λ, κ). The 95 percent standardized-error checks use Numerics distribution-level
    /// maximum-likelihood parameter-variance standard errors. No identified response or secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_Weibull_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.WeibullData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Weibull);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        double trueLambda = TestData.WeibullTrueParams[0];
        double trueKappa = TestData.WeibullTrueParams[1];

        AssertMleRecovery(model, mle, [trueLambda, trueKappa], ["lambda", "kappa"]);
    }

    /// <summary>
    /// Verifies generalized-extreme-value location, scale, and shape recovery.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(ξ=50, α=15, κ=0.1); fitted
    /// coordinates=(ξ, α, κ). The 95 percent standardized-error checks use Numerics distribution-level
    /// maximum-likelihood parameter-variance standard errors. No identified response or secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_GEV_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GEVData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        double trueXi = TestData.GEVTrueParams[0];
        double trueAlpha = TestData.GEVTrueParams[1];
        double trueKappa = TestData.GEVTrueParams[2];

        AssertMleRecovery(model, mle, [trueXi, trueAlpha, trueKappa], ["xi", "alpha", "kappa"]);
    }

    /// <summary>
    /// Verifies generalized-Pareto location, scale, and shape recovery.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(ξ=0, α=20, κ=0.15). The
    /// fitted coordinates α and κ use Numerics distribution-level maximum-likelihood parameter-variance standard errors.
    /// The zero parent location is represented by predeclared Q(0.99), whose 95 percent band uses
    /// <see cref="GeneralizedPareto.QuantileVariance(double, int, ParameterEstimationMethod)"/>.
    /// No conditional secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_GeneralizedPareto_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GeneralizedParetoData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedPareto);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        double trueAlpha = TestData.GeneralizedParetoTrueParams[1];
        double trueKappa = TestData.GeneralizedParetoTrueParams[2];

        model.SetParameterValues(mle.BestParameterSet.Values);
        double[,] parameterCovariance = ((IStandardError)model.Distribution).ParameterCovariance(
            TestData.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        RecoveryAcceptance.AssertFrequentistStandardizedError("alpha", mle.BestParameterSet.Values[1], trueAlpha, Math.Sqrt(parameterCovariance[1, 1]));
        RecoveryAcceptance.AssertFrequentistStandardizedError("kappa", mle.BestParameterSet.Values[2], trueKappa, Math.Sqrt(parameterCovariance[2, 2]));
        AssertGeneralizedParetoQ99Recovery(model, mle);
    }

    #endregion

    #region Logistic Distributions

    /// <summary>
    /// Verifies Logistic location and scale recovery on their observed-information scales.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(ξ=75, α=10); fitted
    /// coordinates=(ξ, α). The 95 percent standardized-error checks use Numerics distribution-level
    /// maximum-likelihood parameter-variance standard errors. No identified response or secondary 5 percent rule applies.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_Logistic_RecoversTrueParameters()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.LogisticData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Logistic);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE estimation failed.");

        double trueXi = TestData.LogisticTrueParams[0];
        double trueAlpha = TestData.LogisticTrueParams[1];

        AssertMleRecovery(model, mle, [trueXi, trueAlpha], ["xi", "alpha"]);
    }

    /// <summary>
    /// Verifies Generalized Logistic location, scale, and shape recovery with true profiling.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(ξ=75, α=10, κ=0.15); fitted
    /// coordinates=(ξ, α, κ). Haden's approved true-profile alternative supplies the 95 percent
    /// uncertainty intervals through <see cref="MaximumLikelihood.ParameterConfidenceIntervals(double)"/>
    /// with alpha=0.05 after the production MLE succeeds. The conditional secondary 5 percent rule
    /// does not apply.
    /// </remarks>
    [TestMethod]
    public void Test_MLE_GeneralizedLogistic_RecoversTrueParameters()
    {
        var df = new Models.DataFrame { ExactSeries = new ExactSeries(TestData.GeneralizedLogisticData) };
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedLogistic);
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);

        Assert.IsTrue(mle.Estimate(), "Generalized-Logistic maximum-likelihood estimation failed.");
        AssertMleTrueProfileRecovery(
            mle,
            TestData.GeneralizedLogisticTrueParams,
            ["xi", "alpha", "kappa"]);
    }

    #endregion

    /// <summary>Applies the common frequentist standardized-error recovery rule to regular fitted coordinates.</summary>
    /// <param name="model">Fitted distribution model whose Numerics distribution parameters are synchronized to MLE values.</param>
    /// <param name="mle">Completed maximum-likelihood estimator supplying fitted parameter values.</param>
    /// <param name="parents">Generating-parent values in model-parameter order.</param>
    /// <param name="coordinateNames">Predeclared coordinate labels in model-parameter order.</param>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345. The caller predeclares parents and
    /// fitted coordinates; this helper uses <see cref="IStandardError.ParameterCovariance(int, ParameterEstimationMethod)"/>
    /// at recovered distribution parameters with <see cref="ParameterEstimationMethod.MaximumLikelihood"/>.
    /// It does not alter estimator defaults or apply an identified-response/secondary-five-percent rule.
    /// </remarks>
    private static void AssertMleRecovery(UnivariateDistribution model, MaximumLikelihood mle, IReadOnlyList<double> parents, IReadOnlyList<string> coordinateNames)
    {
        Assert.AreEqual(parents.Count, mle.BestParameterSet.Values.Length, "Parent vector must match fitted parameter order.");
        Assert.AreEqual(parents.Count, coordinateNames.Count, "Every fitted coordinate must have a predeclared label.");
        model.SetParameterValues(mle.BestParameterSet.Values);
        Assert.IsInstanceOfType<IStandardError>(model.Distribution, "The fitted distribution must expose a Numerics parameter-variance API.");
        double[,] parameterCovariance = ((IStandardError)model.Distribution).ParameterCovariance(
            TestData.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        for (int index = 0; index < parents.Count; index++)
            RecoveryAcceptance.AssertFrequentistStandardizedError(
                coordinateNames[index], mle.BestParameterSet.Values[index], parents[index], Math.Sqrt(parameterCovariance[index, index]));
    }

    /// <summary>Evaluates the independent log-scale Normal likelihood for a Ln-Normal sample.</summary>
    /// <param name="logData">Natural logarithms of the positive observations.</param>
    /// <param name="mu">Candidate Normal location on the log scale.</param>
    /// <param name="sigma">Candidate positive Normal standard deviation on the log scale.</param>
    /// <returns>The log-likelihood up to the data-only Ln-Normal Jacobian, which cancels in likelihood-ratio comparisons.</returns>
    /// <remarks>
    /// The omitted sum of negative log observations is constant in <paramref name="mu"/> and
    /// <paramref name="sigma"/>. Keeping this construction independent of the production model
    /// makes the joint likelihood-ratio acceptance an analytical same-sample oracle.
    /// </remarks>
    private static double LnNormalLogLikelihood(IReadOnlyList<double> logData, double mu, double sigma)
    {
        if (!(sigma > 0d) || !double.IsFinite(sigma))
            return double.NegativeInfinity;

        double sumSquaredResiduals = 0d;
        for (int index = 0; index < logData.Count; index++)
        {
            double residual = logData[index] - mu;
            sumSquaredResiduals += residual * residual;
        }

        return -logData.Count * Math.Log(sigma)
            - 0.5d * logData.Count * Math.Log(2d * Math.PI)
            - sumSquaredResiduals / (2d * sigma * sigma);
    }

    /// <summary>Applies the common MLE recovery rule in a distribution's native covariance coordinates.</summary>
    /// <param name="standardErrorDistribution">Fitted distribution supplying the native-coordinate covariance.</param>
    /// <param name="estimates">Recovered values in the covariance coordinate system.</param>
    /// <param name="parents">Generating-parent values in the same covariance coordinate system.</param>
    /// <param name="coordinateNames">Predeclared labels for the covariance coordinates.</param>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345. The helper consumes the Numerics
    /// maximum-likelihood covariance diagonal without transformation and applies the unchanged
    /// absolute standardized parent error limit of 1.96. It does not apply a secondary 5 percent rule.
    /// </remarks>
    private static void AssertMleCovarianceCoordinateRecovery(
        IStandardError standardErrorDistribution,
        IReadOnlyList<double> estimates,
        IReadOnlyList<double> parents,
        IReadOnlyList<string> coordinateNames)
    {
        Assert.AreEqual(parents.Count, estimates.Count, "Parent and estimate vectors must share the covariance-coordinate order.");
        Assert.AreEqual(parents.Count, coordinateNames.Count, "Every covariance coordinate must have a predeclared label.");
        double[,] covariance = standardErrorDistribution.ParameterCovariance(
            TestData.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        for (int index = 0; index < parents.Count; index++)
        {
            RecoveryAcceptance.AssertFrequentistStandardizedError(
                coordinateNames[index],
                estimates[index],
                parents[index],
                Math.Sqrt(covariance[index, index]));
        }
    }

    /// <summary>Requires all predeclared generating coordinates to lie inside finite ordered true-profile 95% intervals.</summary>
    /// <param name="mle">Completed maximum-likelihood estimator whose true profile likelihood is evaluated.</param>
    /// <param name="parents">Generating-parent values in model-parameter order.</param>
    /// <param name="coordinateNames">Predeclared coordinate labels in model-parameter order.</param>
    /// <remarks>
    /// This helper implements Haden's approved alternative when a distribution's Numerics parameter-
    /// and quantile-variance APIs are unavailable. The caller predeclares scalar-observation N=1000,
    /// seed, parent coordinates, fitted coordinates, and alpha=0.05; this helper invokes the production
    /// true-profile path without changing defaults, tolerances, or optimizer policy. The secondary
    /// five-percent rule is not applied to these profile-interval cells.
    /// </remarks>
    private static void AssertMleTrueProfileRecovery(MaximumLikelihood mle, IReadOnlyList<double> parents, IReadOnlyList<string> coordinateNames)
    {
        Assert.AreEqual(parents.Count, mle.BestParameterSet.Values.Length, "Parent vector must match fitted parameter order.");
        Assert.AreEqual(parents.Count, coordinateNames.Count, "Every fitted coordinate must have a predeclared label.");
        double[,] intervals = mle.ParameterConfidenceIntervals(alpha: 0.05d);
        for (int index = 0; index < parents.Count; index++)
            RecoveryAcceptance.AssertFrequentistParentInInterval(
                coordinateNames[index], parents[index], intervals[index, 0], intervals[index, 1]);
    }

    /// <summary>Requires the predeclared Generalized-Pareto Q(0.99) response band to contain its parent response.</summary>
    /// <param name="model">Fitted Generalized-Pareto model whose distribution is synchronized to MLE parameters.</param>
    /// <param name="mle">Completed maximum-likelihood estimator for the model.</param>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(ξ=0, α=20, κ=0.15); fitted
    /// response=Q(0.99). The 95 percent response band uses the Generalized-Pareto maximum-likelihood
    /// delta-method quantile variance, including its documented asymptotic approximation. It protects
    /// the zero-parent location claim without a relative coordinate assertion; the secondary 5 percent rule does not apply.
    /// </remarks>
    private static void AssertGeneralizedParetoQ99Recovery(UnivariateDistribution model, MaximumLikelihood mle)
    {
        const double probability = 0.99d;
        model.SetParameterValues(mle.BestParameterSet.Values);
        var fittedDistribution = (GeneralizedPareto)model.Distribution;
        double responseEstimate = fittedDistribution.InverseCDF(probability);
        double responseVariance = fittedDistribution.QuantileVariance(
            probability,
            TestData.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        Assert.IsTrue(double.IsFinite(responseVariance) && responseVariance > 0d,
            "The predeclared Generalized-Pareto Q(0.99) delta-method variance must be finite and positive.");

        var parentDistribution = new GeneralizedPareto(0d, 20d, 0.15d);
        double parentResponse = parentDistribution.InverseCDF(probability);
        double responseHalfWidth = RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * Math.Sqrt(responseVariance);
        RecoveryAcceptance.AssertIdentifiedResponseGrid(
            "Generalized-Pareto Q(0.99)", parentResponse, responseEstimate - responseHalfWidth, responseEstimate + responseHalfWidth);
    }

}
