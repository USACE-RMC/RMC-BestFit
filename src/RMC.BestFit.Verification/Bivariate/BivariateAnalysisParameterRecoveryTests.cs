using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.Bivariate;

/// <summary>
/// Verifies N=1000 generated-parent recovery for the seven supported copulas.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
///     <b>Intent:</b> Simulate a paired sample with known parent copula + marginal
///     parameters (via <see cref="SyntheticBivariateData"/>), fit the marginals via MLE,
///     and estimate the conditional copula. The six one-coordinate copulas use the full
///     <see cref="BivariateAnalysis"/> Bayesian MCMC workflow; Student-t uses MLE because
///     repeated beta-function evaluation makes the MCMC realization impractical. Marginal
///     and Student-t MLE coordinates use observed-information uncertainty. Bayesian copula
///     coordinates use central-95% parent inclusion, R-hat below 1.10, and ESS at least 100.
/// </para>
/// <para>
///     <b>Identification:</b> the fitted marginals are fixed during copula sampling, so their
///     MLE uncertainty is not posterior uncertainty. Every parent copula must improve the
///     conditional copula likelihood over its independence limit. Student-t degrees of freedom
///     are evaluated in the identified closed-form symmetric tail-dependence response rather
///     than through a raw-coordinate recovery claim.
/// </para>
/// <para>
///     <b>Runtime:</b> several minutes for the full class. Long enough to belong in
///     <c>RMC.BestFit.Verification</c> rather than a fast test project. Inherits
///     <c>[TestCategory("Verification")]</c> from <c>VerificationAssemblyInfo.cs</c>.
/// </para>
/// </remarks>
[TestClass]
public class BivariateAnalysisParameterRecoveryTests
{
    #region Helpers

    /// <summary>
    /// Fits a Normal univariate distribution via MLE to the supplied DataFrame and returns
    /// a ready-to-use <see cref="UnivariateDistribution"/> with the fitted point estimate.
    /// </summary>
    private static (UnivariateDistribution Distribution, MaximumLikelihood Estimator) FitNormalMarginal(DataFrame dataFrame)
    {
        var dist = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(dist, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();
        Assert.IsTrue(mle.IsEstimated, "Normal marginal MLE did not complete.");
        dist.SetParameterValues(mle.BestParameterSet.Values);
        return (dist, mle);
    }

    /// <summary>
    /// Builds a <see cref="BivariateAnalysis"/> with the default MCMC configuration
    /// (DEMCzs simulation defaults, seed 12345, posterior-mean point estimator, 95% credible interval).
    /// </summary>
    private static BivariateAnalysis BuildAnalysis(BivariateDistribution dist)
    {
        var analysis = new BivariateAnalysis(dist);

        analysis.BayesianAnalysis.PRNGSeed = 12345;
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95;
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;

        return analysis;
    }

    /// <summary>
    /// Applies the predeclared marginal-MLE and copula-posterior recovery rules.
    /// </summary>
    private static void AssertRecovery(
        SyntheticBivariateData.BivariateSample sample,
        UnivariateDistribution marginalX,
        MaximumLikelihood marginalXMle,
        UnivariateDistribution marginalY,
        MaximumLikelihood marginalYMle,
        BivariateAnalysis analysis)
    {
        Assert.IsTrue(analysis.IsEstimated,
            $"Analysis failed to complete for {sample.CopulaType}.");
        Assert.IsNotNull(analysis.BayesianAnalysis.Results,
            $"Bayesian results are null for {sample.CopulaType}.");

        AssertMarginalRecovery("X", sample.TrueMarginalXParameters, marginalX, marginalXMle);
        AssertMarginalRecovery("Y", sample.TrueMarginalYParameters, marginalY, marginalYMle);

        Assert.AreEqual(sample.TrueCopulaParameters.Length, analysis.BayesianAnalysis.Results!.ParameterResults.Length,
            $"{sample.CopulaType}: posterior coordinate order must match the generator.");
        for (int index = 0; index < sample.TrueCopulaParameters.Length; index++)
        {
            var summary = analysis.BayesianAnalysis.Results.ParameterResults[index].SummaryStatistics;
            RecoveryAcceptance.AssertBayesianRecovery(
                $"{sample.CopulaType} copula coordinate {index}",
                sample.TrueCopulaParameters[index],
                summary.LowerCI,
                summary.UpperCI,
                summary.Rhat,
                summary.ESS);
        }

    }

    /// <summary>
    /// Driver for 1-parameter copula recovery tests. Every copula uses the same workflow
    /// and assertions — the only differences are the generator and the parent CopulaType.
    /// </summary>
    private static async Task RunRecoveryAsync(SyntheticBivariateData.BivariateSample sample)
    {
        _ = RecoveryDesign.PairedObservations("One index-matched X/Y observation pair.");
        Assert.AreEqual(RecoveryDesign.SampleSize, sample.DataFrameX.ExactSeries.Count,
            "The X marginal must contain exactly 1,000 generated observations.");
        Assert.AreEqual(RecoveryDesign.SampleSize, sample.DataFrameY.ExactSeries.Count,
            "The Y marginal must contain exactly 1,000 generated observations.");

        var (marginalX, marginalXMle) = FitNormalMarginal(sample.DataFrameX);
        var (marginalY, marginalYMle) = FitNormalMarginal(sample.DataFrameY);

        var dist = new BivariateDistribution(marginalX, marginalY, sample.CopulaType);
        AssertParentInsidePriorSupport(sample, dist);
        AssertParentSeparatedFromIndependence(sample, dist);
        var analysis = BuildAnalysis(dist);

        await analysis.RunAsync();

        AssertRecovery(sample, marginalX, marginalXMle, marginalY, marginalYMle, analysis);
    }

    /// <summary>
    /// Applies Normal observed-information uncertainty to one fixed marginal fit.
    /// </summary>
    /// <param name="label">Marginal label.</param>
    /// <param name="parents">Generating mean and standard deviation.</param>
    /// <param name="distribution">Fitted marginal model.</param>
    /// <param name="estimator">Completed marginal MLE.</param>
    private static void AssertMarginalRecovery(
        string label,
        IReadOnlyList<double> parents,
        UnivariateDistribution distribution,
        MaximumLikelihood estimator)
    {
        Assert.IsInstanceOfType<IStandardError>(distribution.Distribution);
        double[,] covariance = ((IStandardError)distribution.Distribution).ParameterCovariance(
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        for (int index = 0; index < parents.Count; index++)
        {
            RecoveryAcceptance.AssertFrequentistStandardizedError(
                $"marginal {label} {(index == 0 ? "mu" : "sigma")}",
                estimator.BestParameterSet.Values[index],
                parents[index],
                Math.Sqrt(covariance[index, index]));
        }
    }

    /// <summary>
    /// Confirms that every generating copula coordinate lies inside the fitted prior support.
    /// </summary>
    /// <param name="sample">Generated parent and coordinate vector.</param>
    /// <param name="distribution">Conditional copula model.</param>
    private static void AssertParentInsidePriorSupport(
        SyntheticBivariateData.BivariateSample sample,
        BivariateDistribution distribution)
    {
        Assert.AreEqual(sample.TrueCopulaParameters.Length, distribution.Parameters.Count);
        for (int index = 0; index < sample.TrueCopulaParameters.Length; index++)
        {
            Assert.IsTrue(
                distribution.Parameters[index].LowerBound <= sample.TrueCopulaParameters[index] &&
                sample.TrueCopulaParameters[index] <= distribution.Parameters[index].UpperBound,
                $"{sample.CopulaType} parent coordinate {index} is outside prior support.");
        }
    }

    /// <summary>
    /// Requires the generated parent to be distinguished from the applicable independence limit.
    /// </summary>
    /// <param name="sample">Generated parent and copula family.</param>
    /// <param name="distribution">Conditional copula likelihood.</param>
    private static void AssertParentSeparatedFromIndependence(
        SyntheticBivariateData.BivariateSample sample,
        BivariateDistribution distribution)
    {
        double[] independence = sample.CopulaType switch
        {
            CopulaType.AliMikhailHaq => [0d],
            CopulaType.Clayton => [0d],
            CopulaType.Frank => [0.001d],
            CopulaType.Gumbel => [1d],
            CopulaType.Joe => [1d],
            CopulaType.Normal => [0d],
            CopulaType.StudentT => [0d, sample.TrueCopulaParameters[1]],
            _ => throw new AssertFailedException($"No independence crosswalk for {sample.CopulaType}."),
        };
        double parentLogLikelihood = distribution.DataLogLikelihood(sample.TrueCopulaParameters);
        double independenceLogLikelihood = distribution.DataLogLikelihood(independence);
        Assert.IsTrue(parentLogLikelihood > independenceLogLikelihood,
            $"{sample.CopulaType}: parent log likelihood {parentLogLikelihood:G17} must exceed " +
            $"the independence-limit value {independenceLogLikelihood:G17}.");
    }

    /// <summary>
    /// Requires the generating Student-t correlation and tail-dependence response to satisfy
    /// observed-information standardized-error rules.
    /// </summary>
    /// <param name="sample">Student-t generating parent.</param>
    /// <param name="estimator">Completed conditional copula MLE.</param>
    private static void AssertStudentTMleRecovery(
        SyntheticBivariateData.BivariateSample sample,
        MaximumLikelihood estimator)
    {
        Assert.IsTrue(estimator.IsEstimated, "Student-t conditional copula MLE did not complete.");
        Assert.IsTrue(estimator.TryGetCovarianceMatrix(out var covariance),
            estimator.CovarianceDiagnostic ?? "Student-t observed-information covariance is unavailable.");
        Assert.AreEqual(CovarianceComputationStatus.Available, estimator.CovarianceStatus,
            "Student-t recovery requires an unregularized observed-information covariance.");

        double[] estimates = estimator.BestParameterSet.Values;
        RecoveryAcceptance.AssertFrequentistStandardizedError(
            "Student-t copula rho",
            estimates[0],
            sample.TrueCopulaParameters[0],
            Math.Sqrt(covariance[0, 0]));

        double parent = StudentTTailDependence(sample.TrueCopulaParameters[0], sample.TrueCopulaParameters[1]);
        double estimate = StudentTTailDependence(estimates[0], estimates[1]);
        double rhoStep = 1E-4 * Math.Max(1d, Math.Abs(estimates[0]));
        double nuStep = 1E-4 * Math.Max(1d, Math.Abs(estimates[1]));
        double rhoGradient = (
            StudentTTailDependence(estimates[0] + rhoStep, estimates[1]) -
            StudentTTailDependence(estimates[0] - rhoStep, estimates[1])) / (2d * rhoStep);
        double nuGradient = (
            StudentTTailDependence(estimates[0], estimates[1] + nuStep) -
            StudentTTailDependence(estimates[0], estimates[1] - nuStep)) / (2d * nuStep);
        double variance =
            rhoGradient * rhoGradient * covariance[0, 0] +
            2d * rhoGradient * nuGradient * covariance[0, 1] +
            nuGradient * nuGradient * covariance[1, 1];
        Assert.IsTrue(double.IsFinite(variance) && variance > 0d,
            $"Student-t tail-dependence delta-method variance must be positive and finite; observed {variance:G17}.");
        RecoveryAcceptance.AssertFrequentistStandardizedError(
            "Student-t symmetric tail dependence",
            estimate,
            parent,
            Math.Sqrt(variance));
    }

    /// <summary>
    /// Evaluates the closed-form symmetric Student-t copula tail-dependence coefficient.
    /// </summary>
    /// <param name="rho">Copula correlation.</param>
    /// <param name="degreesOfFreedom">Copula degrees of freedom.</param>
    /// <returns>The common lower- and upper-tail dependence coefficient.</returns>
    private static double StudentTTailDependence(double rho, double degreesOfFreedom)
    {
        double argument = -Math.Sqrt((degreesOfFreedom + 1d) * (1d - rho) / (1d + rho));
        return 2d * new StudentT(0d, 1d, degreesOfFreedom + 1d).CDF(argument);
    }

    #endregion

    #region One-Parameter Copula Recovery

    /// <summary>
    /// Recovers rho = 0.8 for a Normal copula from 1,000 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverNormalCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateNormalCopulaData());
    }

    /// <summary>
    /// Recovers theta = 3.0 for a Joe copula from 1,000 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverJoeCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateJoeCopulaData());
    }

    /// <summary>
    /// Recovers theta = 2.0 for a Gumbel copula from 1,000 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverGumbelCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateGumbelCopulaData());
    }

    /// <summary>
    /// Recovers theta = 8.0 for a Frank copula from 1,000 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverFrankCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateFrankCopulaData());
    }

    /// <summary>
    /// Recovers theta = 1.5 for a Clayton copula from 1,000 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverClaytonCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateClaytonCopulaData());
    }

    /// <summary>
    /// Recovers theta = 0.8 for an Ali-Mikhail-Haq copula from 1,000 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverAMHCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateAMHCopulaData());
    }

    #endregion

    #region Two-Parameter Copula Recovery (Student's t)

    /// <summary>
    /// Recovers ρ = 0.8 by conditional copula MLE for a Student's t copula fit from
    /// 1,000 paired observations and checks the symmetric tail-dependence response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifiable rho coordinate uses the observed-information standardized-error rule.
    /// Because nu is weakly identified toward the Gaussian limit, it is not assigned a raw
    /// coordinate recovery claim; instead the generating symmetric tail-dependence coefficient
    /// must be within 1.96 delta-method standard errors of the fitted response.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void RecoverStudentTCopulaParametersWithMaximumLikelihood()
    {
        SyntheticBivariateData.BivariateSample sample = SyntheticBivariateData.GenerateStudentTCopulaData();
        _ = RecoveryDesign.PairedObservations("One index-matched X/Y observation pair.");
        Assert.AreEqual(RecoveryDesign.SampleSize, sample.DataFrameX.ExactSeries.Count,
            "The X marginal must contain exactly 1,000 generated observations.");
        Assert.AreEqual(RecoveryDesign.SampleSize, sample.DataFrameY.ExactSeries.Count,
            "The Y marginal must contain exactly 1,000 generated observations.");

        var (marginalX, marginalXMle) = FitNormalMarginal(sample.DataFrameX);
        var (marginalY, marginalYMle) = FitNormalMarginal(sample.DataFrameY);
        var distribution = new BivariateDistribution(marginalX, marginalY, sample.CopulaType);
        AssertParentInsidePriorSupport(sample, distribution);
        AssertParentSeparatedFromIndependence(sample, distribution);

        var estimator = new MaximumLikelihood(distribution, OptimizationMethod.DifferentialEvolution);
        Assert.IsTrue(estimator.Estimate(), $"Student-t MLE failed with status {estimator.Status}.");

        AssertMarginalRecovery("X", sample.TrueMarginalXParameters, marginalX, marginalXMle);
        AssertMarginalRecovery("Y", sample.TrueMarginalYParameters, marginalY, marginalYMle);
        AssertStudentTMleRecovery(sample, estimator);
    }

    #endregion
}
