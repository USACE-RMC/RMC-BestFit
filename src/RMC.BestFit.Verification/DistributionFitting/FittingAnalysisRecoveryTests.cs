using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Mathematics.Optimization;
using Numerics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.TestCommon;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.DistributionFitting;

/// <summary>
/// Verifies generated-parent recovery through the default <see cref="FittingAnalysis"/> candidate list.
/// </summary>
/// <remarks>
/// Each cell generates exactly <see cref="RecoveryDesign.SampleSize"/> scalar observations from its
/// declared Numerics parent with seed 12345. The methods exercise the unmodified default candidate
/// list and assess only the generating-family result; they do not require that family to win an
/// information-criterion or RMSE rank.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class FittingAnalysisRecoveryTests
{
    /// <summary>Recovers the Normal generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent Normal(mu=100, sigma=15); fitted coordinates=(mu, sigma). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task Normal_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertCovarianceRecoveryAsync(new Normal(100d, 15d), UnivariateDistributionType.Normal, [100d, 15d], ["mu", "sigma"]);

    /// <summary>Recovers the base-10 LogNormal generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent LogNormal(mu=3, sigma=0.5); fitted coordinates=(mu, sigma) on the base-10 log scale. Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task LogNormal_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertCovarianceRecoveryAsync(new LogNormal(3d, 0.5d), UnivariateDistributionType.LogNormal, [3d, 0.5d], ["mu", "sigma"]);

    /// <summary>Recovers the natural-log LnNormal generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent LnNormal(real-space mean=3.5, real-space standard deviation=0.4). The parent and actual FittingAnalysis distribution are compared in Numerics covariance coordinates=(Mu, Sigma^2). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task LnNormal_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertLnNormalCovarianceRecoveryAsync();

    /// <summary>Recovers the Exponential generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent Exponential(xi=10, alpha=25); fitted coordinates=(xi, alpha). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task Exponential_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertCovarianceRecoveryAsync(new Exponential(10d, 25d), UnivariateDistributionType.Exponential, [10d, 25d], ["xi", "alpha"]);

    /// <summary>Recovers the Gamma generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent Gamma(theta=5, kappa=3); fitted coordinates=(theta, kappa). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task Gamma_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertCovarianceRecoveryAsync(new GammaDistribution(5d, 3d), UnivariateDistributionType.GammaDistribution, [5d, 3d], ["theta", "kappa"]);

    /// <summary>Recovers the Generalized Extreme Value generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent GEV(xi=50, alpha=15, kappa=0.1); fitted coordinates=(xi, alpha, kappa). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task GeneralizedExtremeValue_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertCovarianceRecoveryAsync(new GeneralizedExtremeValue(50d, 15d, 0.1d), UnivariateDistributionType.GeneralizedExtremeValue, [50d, 15d, 0.1d], ["xi", "alpha", "kappa"]);

    /// <summary>Recovers the Generalized Logistic generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent GLO(xi=75, alpha=10, kappa=0.15); fitted coordinates=(xi, alpha, kappa). An auxiliary same-data production MaximumLikelihood fit supplies finite ordered alpha=0.05 profile intervals; each parent and the actual FittingAnalysis coordinate must lie within its corresponding interval, and the secondary 5% rule is conditional on interval width.</remarks>
    [TestMethod]
    public async Task GeneralizedLogistic_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertProfileRecoveryAsync(new GeneralizedLogistic(75d, 10d, 0.15d), UnivariateDistributionType.GeneralizedLogistic, [75d, 10d, 0.15d], ["xi", "alpha", "kappa"]);

    /// <summary>Recovers the Generalized Normal generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent GNO(xi=50, alpha=10, kappa=-0.3); fitted coordinates=(xi, alpha, kappa). An auxiliary same-data production MaximumLikelihood fit supplies finite ordered alpha=0.05 profile intervals; each parent and the actual FittingAnalysis coordinate must lie within its corresponding interval, and the secondary 5% rule is conditional on interval width.</remarks>
    [TestMethod]
    public async Task GeneralizedNormal_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertProfileRecoveryAsync(new GeneralizedNormal(50d, 10d, -0.3d), UnivariateDistributionType.GeneralizedNormal, [50d, 10d, -0.3d], ["xi", "alpha", "kappa"]);

    /// <summary>Recovers the Generalized Pareto generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent GPA(xi=0, alpha=20, kappa=0.15). Fitted alpha and kappa use Numerics parameter covariance; the zero-location claim uses the predeclared Q(0.99) response coordinate and its Numerics maximum-likelihood quantile-variance 95% band. The secondary 5% rule is evaluated in response space only when that band is narrower than 5% of the nonzero parent response.</remarks>
    [TestMethod]
    public async Task GeneralizedPareto_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertGeneralizedParetoRecoveryAsync();

    /// <summary>Recovers the Gumbel generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent Gumbel(xi=50, alpha=15); fitted coordinates=(xi, alpha). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task Gumbel_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertCovarianceRecoveryAsync(new Gumbel(50d, 15d), UnivariateDistributionType.Gumbel, [50d, 15d], ["xi", "alpha"]);

    /// <summary>Recovers the Kappa Four generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent KappaFour(xi=100, alpha=20, kappa=-0.1, hondo=0.1); fitted coordinates=(xi, alpha, kappa, hondo). An auxiliary same-data production MaximumLikelihood fit supplies finite ordered alpha=0.05 profile intervals; each parent and the actual FittingAnalysis coordinate must lie within its corresponding interval, and the secondary 5% rule is conditional on interval width.</remarks>
    [TestMethod]
    public async Task KappaFour_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertProfileRecoveryAsync(new KappaFour(100d, 20d, -0.1d, 0.1d), UnivariateDistributionType.KappaFour, [100d, 20d, -0.1d, 0.1d], ["xi", "alpha", "kappa", "hondo"]);

    /// <summary>Recovers the Logistic generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent Logistic(xi=75, alpha=10); fitted coordinates=(xi, alpha). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task Logistic_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertCovarianceRecoveryAsync(new Logistic(75d, 10d), UnivariateDistributionType.Logistic, [75d, 10d], ["xi", "alpha"]);

    /// <summary>Recovers the Log-Pearson Type III generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent LP3 moment coordinates=(mu=2, sigma=0.3, gamma=0.5). The parent and actual FittingAnalysis distribution are compared in Numerics MLE covariance coordinates=(Mu, 1/Beta, Alpha). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task LogPearsonTypeIII_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertLogPearsonTypeIIICovarianceRecoveryAsync();

    /// <summary>Recovers the Pearson Type III generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent PE3 moment coordinates=(mu=100, sigma=20, gamma=0.8). The parent and actual FittingAnalysis distribution are compared in Numerics MLE covariance coordinates=(Mu, 1/Beta, Alpha). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task PearsonTypeIII_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertPearsonTypeIIICovarianceRecoveryAsync();

    /// <summary>Recovers the Weibull generating family through the default fitting analysis.</summary>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent Weibull(lambda=100, kappa=2.5); fitted coordinates=(lambda, kappa). Numerics parameter covariance supplies the 95% standardized-error bands, and the secondary 5% rule is applied only if a coordinate band is narrower than 5% of its nonzero parent.</remarks>
    [TestMethod]
    public async Task Weibull_N1000_FittingAnalysisRecoversGeneratingFamily()
        => await AssertCovarianceRecoveryAsync(new Weibull(100d, 2.5d), UnivariateDistributionType.Weibull, [100d, 2.5d], ["lambda", "kappa"]);

    /// <summary>Runs default-list fitting and applies covariance-based generated-parent recovery.</summary>
    /// <param name="parentDistribution">Explicit Numerics distribution used to generate the scalar observations.</param>
    /// <param name="generatingFamily">Candidate family that must be located after the completed fitting analysis.</param>
    /// <param name="parents">Generating parameters in Numerics parameter order.</param>
    /// <param name="coordinateNames">Labels for the fitted parameter coordinates.</param>
    /// <returns>A task that completes after default-list fitting and covariance acceptance.</returns>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345. The fitted coordinates use the generating family's Numerics maximum-likelihood parameter covariance. A secondary 5% point rule is checked only when the resulting 95% coordinate band is narrower than 5% of a nonzero parent.</remarks>
    private static async Task AssertCovarianceRecoveryAsync(UnivariateDistributionBase parentDistribution, UnivariateDistributionType generatingFamily, IReadOnlyList<double> parents, IReadOnlyList<string> coordinateNames)
    {
        FittedDistribution fitted = await RunDefaultFittingAsync(parentDistribution, generatingFamily);
        Assert.IsInstanceOfType<IStandardError>(fitted.Distribution, "The recovered family must expose the Numerics parameter-variance API.");
        double[] estimates = fitted.Distribution!.GetParameters;
        Assert.AreEqual(parents.Count, estimates.Length, "The declared parent vector must match the recovered parameter order.");
        Assert.AreEqual(parents.Count, coordinateNames.Count, "Every recovered coordinate must have a declared label.");

        double[,] covariance = ((IStandardError)fitted.Distribution!).ParameterCovariance(
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        AssertCovarianceCoordinateRecovery(
            covariance,
            estimates,
            parents,
            coordinateNames);
    }

    /// <summary>Runs default-list fitting and compares LnNormal distributions in their covariance coordinates.</summary>
    /// <returns>A task that completes after LnNormal covariance-coordinate acceptance.</returns>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent LnNormal(real-space mean=3.5, real-space standard deviation=0.4). The parent and fitted distributions use logarithmic coordinates (Mu, Sigma^2), while Numerics supplies covariance in physical (Mean, StandardDeviation) coordinates. The complete fitted-parameter Jacobian transformation, including cross terms, precedes the unchanged 1.96 and conditional secondary-five-percent rules.</remarks>
    private static async Task AssertLnNormalCovarianceRecoveryAsync()
    {
        var parentDistribution = new LnNormal(3.5d, 0.4d);
        FittedDistribution fitted = await RunDefaultFittingAsync(parentDistribution, UnivariateDistributionType.LnNormal);
        var actualDistribution = (LnNormal)fitted.Distribution!;
        double[,] covariance = actualDistribution.ParameterCovariance(
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        covariance = CovarianceCoordinateTransforms.LnNormalPhysicalToLog(
            covariance,
            actualDistribution.Mean,
            actualDistribution.StandardDeviation);
        AssertCovarianceCoordinateRecovery(
            covariance,
            [actualDistribution.Mu, actualDistribution.Sigma * actualDistribution.Sigma],
            [parentDistribution.Mu, parentDistribution.Sigma * parentDistribution.Sigma],
            ["Mu", "SigmaSquared"]);
    }

    /// <summary>Runs default-list fitting and compares Pearson Type III distributions in their MLE covariance coordinates.</summary>
    /// <returns>A task that completes after Pearson Type III covariance-coordinate acceptance.</returns>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent PearsonTypeIII moment coordinates=(mu=100, sigma=20, gamma=0.8). Numerics supplies covariance in public (Mu, Sigma, Gamma) coordinates; the complete fitted-parameter Jacobian transformation, including cross terms, maps it to (Mu, 1/Beta, Alpha) before the unchanged 1.96 and conditional secondary-five-percent rules.</remarks>
    private static async Task AssertPearsonTypeIIICovarianceRecoveryAsync()
    {
        var parentDistribution = new PearsonTypeIII(100d, 20d, 0.8d);
        FittedDistribution fitted = await RunDefaultFittingAsync(parentDistribution, UnivariateDistributionType.PearsonTypeIII);
        var actualDistribution = (PearsonTypeIII)fitted.Distribution!;
        double[,] covariance = actualDistribution.ParameterCovariance(
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        covariance = CovarianceCoordinateTransforms.PearsonMomentToMle(
            covariance,
            actualDistribution.Sigma,
            actualDistribution.Gamma);
        AssertCovarianceCoordinateRecovery(
            covariance,
            [actualDistribution.Mu, 1d / actualDistribution.Beta, actualDistribution.Alpha],
            [parentDistribution.Mu, 1d / parentDistribution.Beta, parentDistribution.Alpha],
            ["Mu", "OneOverBeta", "Alpha"]);
    }

    /// <summary>Runs default-list fitting and compares Log-Pearson Type III distributions in their MLE covariance coordinates.</summary>
    /// <returns>A task that completes after Log-Pearson Type III covariance-coordinate acceptance.</returns>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent LogPearsonTypeIII moment coordinates=(mu=2, sigma=0.3, gamma=0.5). Numerics supplies covariance in public (Mu, Sigma, Gamma) coordinates; the complete fitted-parameter Jacobian transformation, including cross terms, maps it to (Mu, 1/Beta, Alpha) before the unchanged 1.96 and conditional secondary-five-percent rules.</remarks>
    private static async Task AssertLogPearsonTypeIIICovarianceRecoveryAsync()
    {
        var parentDistribution = new LogPearsonTypeIII(2d, 0.3d, 0.5d);
        FittedDistribution fitted = await RunDefaultFittingAsync(parentDistribution, UnivariateDistributionType.LogPearsonTypeIII);
        var actualDistribution = (LogPearsonTypeIII)fitted.Distribution!;
        double[,] covariance = actualDistribution.ParameterCovariance(
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        covariance = CovarianceCoordinateTransforms.PearsonMomentToMle(
            covariance,
            actualDistribution.Sigma,
            actualDistribution.Gamma);
        AssertCovarianceCoordinateRecovery(
            covariance,
            [actualDistribution.Mu, 1d / actualDistribution.Beta, actualDistribution.Alpha],
            [parentDistribution.Mu, 1d / parentDistribution.Beta, parentDistribution.Alpha],
            ["Mu", "OneOverBeta", "Alpha"]);
    }

    /// <summary>Applies the common covariance-coordinate recovery acceptance rules.</summary>
    /// <param name="covariance">Covariance in the provided coordinate system.</param>
    /// <param name="estimates">Actual FittingAnalysis coordinates in the covariance parameterization.</param>
    /// <param name="parents">Generating-parent coordinates in the same covariance parameterization.</param>
    /// <param name="coordinateNames">Labels for the covariance coordinates.</param>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345. This helper consumes a covariance already expressed in the declared coordinates. It applies absolute standardized parent error no greater than 1.96 and invokes the secondary five-percent rule only when the resulting 95% band is narrower than five percent of a nonzero parent.</remarks>
    private static void AssertCovarianceCoordinateRecovery(double[,] covariance, IReadOnlyList<double> estimates, IReadOnlyList<double> parents, IReadOnlyList<string> coordinateNames)
    {
        Assert.AreEqual(parents.Count, estimates.Count, "The declared parent vector must match the recovered covariance-coordinate order.");
        Assert.AreEqual(parents.Count, coordinateNames.Count, "Every recovered covariance coordinate must have a declared label.");
        Assert.AreEqual(parents.Count, covariance.GetLength(0), "The covariance rows must match the declared coordinate order.");
        Assert.AreEqual(parents.Count, covariance.GetLength(1), "The covariance columns must match the declared coordinate order.");
        for (int index = 0; index < parents.Count; index++)
        {
            double standardError = Math.Sqrt(covariance[index, index]);
            RecoveryAcceptance.AssertFrequentistStandardizedError(coordinateNames[index], estimates[index], parents[index], standardError);
            double halfWidth = RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * standardError;
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(
                coordinateNames[index], estimates[index], parents[index], estimates[index] - halfWidth, estimates[index] + halfWidth);
        }
    }

    /// <summary>Runs default-list fitting and applies the approved true-profile recovery alternative.</summary>
    /// <param name="parentDistribution">Explicit Numerics distribution used to generate the scalar observations.</param>
    /// <param name="generatingFamily">Candidate family that must be located after the completed fitting analysis.</param>
    /// <param name="parents">Generating parameters in Numerics parameter order.</param>
    /// <param name="coordinateNames">Labels for the fitted parameter coordinates.</param>
    /// <returns>A task that completes after fitting and profile-interval acceptance.</returns>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345. The auxiliary same-data MaximumLikelihood run uses unmodified production defaults solely to obtain alpha=0.05 profile intervals. Each generating parent and actual FittingAnalysis coordinate must lie in its finite ordered interval; the secondary 5% rule is conditional on the interval width.</remarks>
    private static async Task AssertProfileRecoveryAsync(UnivariateDistributionBase parentDistribution, UnivariateDistributionType generatingFamily, IReadOnlyList<double> parents, IReadOnlyList<string> coordinateNames)
    {
        DataFrame dataFrame = CreateGeneratedDataFrame(parentDistribution);
        FittedDistribution fitted = await RunDefaultFittingAsync(dataFrame, generatingFamily);
        var auxiliaryModel = new UnivariateDistribution(dataFrame, generatingFamily);
        var auxiliaryMaximumLikelihood = new MaximumLikelihood(auxiliaryModel, OptimizationMethod.DifferentialEvolution);
        Assert.IsTrue(auxiliaryMaximumLikelihood.Estimate(), "The auxiliary maximum-likelihood profile fit failed.");

        double[] estimates = fitted.Distribution!.GetParameters;
        Assert.AreEqual(parents.Count, estimates.Length, "The declared parent vector must match the recovered parameter order.");
        Assert.AreEqual(parents.Count, coordinateNames.Count, "Every recovered coordinate must have a declared label.");
        double[,] intervals = auxiliaryMaximumLikelihood.ParameterConfidenceIntervals(alpha: 0.05d);
        for (int index = 0; index < parents.Count; index++)
        {
            double lower = intervals[index, 0];
            double upper = intervals[index, 1];
            RecoveryAcceptance.AssertFrequentistParentInInterval(coordinateNames[index], parents[index], lower, upper);
            Assert.IsTrue(lower <= estimates[index] && estimates[index] <= upper,
                $"The FittingAnalysis {coordinateNames[index]} coordinate {estimates[index]:G17} must lie inside the auxiliary profile interval [{lower:G17}, {upper:G17}].");
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(coordinateNames[index], estimates[index], parents[index], lower, upper);
        }
    }

    /// <summary>Runs the Generalized-Pareto cell with its identified response-space location check.</summary>
    /// <returns>A task that completes after default-list fitting and the Q(0.99) response-band acceptance.</returns>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345; parent GeneralizedPareto(xi=0, alpha=20, kappa=0.15). Alpha and kappa use parameter covariance; the zero location is represented by Q(0.99) with a Numerics maximum-likelihood quantile-variance 95% band. The secondary rule is conditional in response space.</remarks>
    private static async Task AssertGeneralizedParetoRecoveryAsync()
    {
        var parentDistribution = new GeneralizedPareto(0d, 20d, 0.15d);
        FittedDistribution fitted = await RunDefaultFittingAsync(parentDistribution, UnivariateDistributionType.GeneralizedPareto);
        var fittedDistribution = (GeneralizedPareto)fitted.Distribution!;
        double[] estimates = fittedDistribution.GetParameters;
        double[,] covariance = ((IStandardError)fittedDistribution).ParameterCovariance(
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);

        AssertCovarianceCoordinate("alpha", estimates[1], 20d, covariance[1, 1]);
        AssertCovarianceCoordinate("kappa", estimates[2], 0.15d, covariance[2, 2]);

        const double probability = 0.99d;
        double responseEstimate = fittedDistribution.InverseCDF(probability);
        double responseVariance = fittedDistribution.QuantileVariance(
            probability,
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        Assert.IsTrue(double.IsFinite(responseVariance) && responseVariance > 0d,
            "The Generalized-Pareto Q(0.99) variance must be finite and positive.");
        double halfWidth = RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * Math.Sqrt(responseVariance);
        double parentResponse = parentDistribution.InverseCDF(probability);
        double lower = responseEstimate - halfWidth;
        double upper = responseEstimate + halfWidth;
        RecoveryAcceptance.AssertIdentifiedResponseGrid("Generalized-Pareto Q(0.99)", parentResponse, lower, upper);
        RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved("Generalized-Pareto Q(0.99)", responseEstimate, parentResponse, lower, upper);
    }

    /// <summary>Applies covariance and conditional point recovery to one regular Generalized-Pareto coordinate.</summary>
    /// <param name="coordinate">Declared parameter label.</param>
    /// <param name="estimate">Recovered FittingAnalysis parameter value.</param>
    /// <param name="parent">Generating parent parameter value.</param>
    /// <param name="variance">Numerics maximum-likelihood parameter variance.</param>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345. The resulting 95% coordinate band uses the square root of the supplied Numerics variance, and the secondary 5% rule is conditional on that band's width.</remarks>
    private static void AssertCovarianceCoordinate(string coordinate, double estimate, double parent, double variance)
    {
        double standardError = Math.Sqrt(variance);
        RecoveryAcceptance.AssertFrequentistStandardizedError(coordinate, estimate, parent, standardError);
        double halfWidth = RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * standardError;
        RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(coordinate, estimate, parent, estimate - halfWidth, estimate + halfWidth);
    }

    /// <summary>Generates the predeclared scalar-observation recovery data frame.</summary>
    /// <param name="parentDistribution">Explicit Numerics parent distribution.</param>
    /// <returns>A data frame containing exactly 1,000 generated scalar observations.</returns>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345. This local fitting-specific generator preserves the declared parent parameterization and does not alter shared datasets, production defaults, or estimator configuration.</remarks>
    private static DataFrame CreateGeneratedDataFrame(UnivariateDistributionBase parentDistribution)
    {
        _ = RecoveryDesign.ScalarObservations("Generated univariate scalar observation.");
        return new DataFrame
        {
            ExactSeries = new ExactSeries(parentDistribution.GenerateRandomValues(RecoveryDesign.SampleSize, 12345))
        };
    }

    /// <summary>Runs the unmodified default fitting-analysis candidate list for a generated parent.</summary>
    /// <param name="parentDistribution">Explicit Numerics parent distribution.</param>
    /// <param name="generatingFamily">Candidate family that must have a successful recovered result.</param>
    /// <returns>A task that completes with the successful generating-family fitted distribution.</returns>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345. The default list remains unmodified; overall completion and the generating-family FitSucceeded result are required, while no AIC, BIC, RMSE, or selection rank is asserted.</remarks>
    private static async Task<FittedDistribution> RunDefaultFittingAsync(UnivariateDistributionBase parentDistribution, UnivariateDistributionType generatingFamily)
        => await RunDefaultFittingAsync(CreateGeneratedDataFrame(parentDistribution), generatingFamily);

    /// <summary>Runs the unmodified default fitting-analysis candidate list for an existing generated frame.</summary>
    /// <param name="dataFrame">Generated scalar-observation data frame shared with an auxiliary profile fit when required.</param>
    /// <param name="generatingFamily">Candidate family that must have a successful recovered result.</param>
    /// <returns>A task that completes with the successful generating-family fitted distribution.</returns>
    /// <remarks>Sample unit: scalar observation; N=1000; seed=12345. This helper requires analysis completion and generating-family fit success only; it deliberately does not impose a model-selection rank.</remarks>
    private static async Task<FittedDistribution> RunDefaultFittingAsync(DataFrame dataFrame, UnivariateDistributionType generatingFamily)
    {
        var analysis = new FittingAnalysis(dataFrame);
        await analysis.RunAsync();
        Assert.IsTrue(analysis.IsEstimated, "The default FittingAnalysis candidate run did not complete successfully.");

        FittedDistribution? fitted = analysis.FittedDistributions.FirstOrDefault(
            candidate => candidate.Distribution?.Type == generatingFamily);
        Assert.IsNotNull(fitted, $"The default FittingAnalysis list did not include {generatingFamily}.");
        Assert.IsTrue(fitted.FitSucceeded, $"The generating {generatingFamily} candidate did not fit successfully: {fitted.ErrorMessage}");
        Assert.IsNotNull(fitted.Distribution, $"The generating {generatingFamily} candidate did not retain its fitted distribution.");
        return fitted;
    }
}
