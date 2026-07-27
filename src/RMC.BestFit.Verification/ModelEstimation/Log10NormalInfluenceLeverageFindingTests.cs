using Numerics.Distributions;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Uses analytical Log10-Normal curvature and deletion results to evaluate the estimator
/// influence and leverage review findings before any production implementation is changed.
/// </summary>
/// <remarks>
/// These tests distinguish useful ranking information from calibrated diagnostic magnitudes.
/// A passing finding-confirmation test records the current discrepancy against the independent
/// oracle; after an approved production correction, it is replaced by a direct parity assertion.
/// </remarks>
[TestClass]
public class Log10NormalInfluenceLeverageFindingTests
{
    private static readonly double[] SymmetricLog10Values =
        [1.1d, 1.4d, 1.7d, 2.0d, 2.3d, 2.6d, 2.9d];

    /// <summary>
    /// Confirms that the full Log10-Normal observation curvature does not materially
    /// change the current MAP variance-influence magnitudes or leading ranking.
    /// </summary>
    /// <remarks>
    /// A narrow Gaussian prior displaced from the likelihood center makes the posterior
    /// covariance between mu and sigma nonzero. The analytical full contribution is
    /// <c>|tr(Sigma J_i)| / p</c>. Absolute differences from the current diagonal curvature
    /// approximation remain below <c>0.003</c>, and the three leading observations retain
    /// their order. Relative errors are not used because they exaggerate harmless differences
    /// for observations whose variance influence is near zero.
    /// </remarks>
    [TestMethod]
    public void MapVarianceInfluence_FullCurvaturePreservesMaterialMagnitudeAndLeadingRanking()
    {
        const int parameterCount = 2;
        double likelihoodStandardError = 0.6d / Math.Sqrt(SymmetricLog10Values.Length);
        double priorStandardDeviation = 0.1d * likelihoodStandardError;
        double priorMean = 2d + 2d * likelihoodStandardError;
        var fit = FitMap(SymmetricLog10Values, priorMean, priorStandardDeviation);
        double[] parameters = fit.Estimator.BestParameterSet.Values;
        double mu = parameters[0];
        double sigma = parameters[1];

        var posteriorHessian = LeverageDiagnostics.ComputeNumericalHessianPublic(
            fit.Model.LogLikelihood,
            parameters,
            parameterCount);
        var posteriorCovariance = (posteriorHessian * -1d).Inverse();
        var diagnostics = new LeverageDiagnostics(fit.Model, parameters);
        var fullCurvatureInfluence = new double[SymmetricLog10Values.Length];

        double maximumAbsoluteDifference = 0d;
        for (int i = 0; i < SymmetricLog10Values.Length; i++)
        {
            double residual = SymmetricLog10Values[i] - mu;
            double sigmaSquared = sigma * sigma;
            double observationMuMu = 1d / sigmaSquared;
            double observationMuSigma = 2d * residual / (sigmaSquared * sigma);
            double observationSigmaSigma =
                -1d / sigmaSquared + 3d * residual * residual / (sigmaSquared * sigmaSquared);

            fullCurvatureInfluence[i] = Math.Abs(
                posteriorCovariance[0, 0] * observationMuMu +
                posteriorCovariance[0, 1] * observationMuSigma +
                posteriorCovariance[1, 0] * observationMuSigma +
                posteriorCovariance[1, 1] * observationSigmaSigma) / parameterCount;
            maximumAbsoluteDifference = Math.Max(
                maximumAbsoluteDifference,
                Math.Abs(fullCurvatureInfluence[i] - diagnostics.Observations[i].VarianceInfluence));
        }

        Assert.IsTrue(
            maximumAbsoluteDifference < 3E-3d,
            $"Full observation curvature changed variance influence by {maximumAbsoluteDifference:G6}.");

        int[] reportedLeading = diagnostics.Observations
            .OrderByDescending(observation => observation.VarianceInfluence)
            .Take(3)
            .Select(observation => observation.Index)
            .ToArray();
        int[] fullCurvatureLeading = Enumerable.Range(0, fullCurvatureInfluence.Length)
            .OrderByDescending(index => fullCurvatureInfluence[index])
            .Take(3)
            .ToArray();

        CollectionAssert.AreEqual(
            fullCurvatureLeading,
            reportedLeading,
            "Full observation curvature must preserve the leading variance-influence ranking.");
    }
    /// <summary>
    /// Confirms that the current GMM observation influence magnitude is not calibrated to exact
    /// leave-one-out displacement, even though its absolute ranking identifies an injected outlier.
    /// </summary>
    /// <remarks>
    /// For the just-identified Log10-Normal mean moment, deleting observation <c>i</c> changes mu
    /// exactly by <c>(x_i - xBar) / (n - 1)</c>. The diagnostic is compared on its documented
    /// standard-error scale. The clean symmetric fixture isolates magnitude; the injected-high
    /// variant verifies that ranking information remains useful.
    /// </remarks>
    [TestMethod]
    public void GmmObservationInfluence_CurrentMagnitudeDiffersFromExactDeletionScale()
    {
        var clean = FitGmm(SymmetricLog10Values);
        double[,] cleanInfluence = clean.GetObservationInfluence();
        double cleanMu = clean.BestParameterSet.Values[0];
        double cleanMuStandardError = clean.GetStandardErrors()[0];
        int extremeIndex = SymmetricLog10Values.Length - 1;
        double exactCleanDeletion = ExactStandardizedMuDeletion(
            SymmetricLog10Values,
            extremeIndex,
            cleanMu,
            cleanMuStandardError);
        double magnitudeRatio = Math.Abs(cleanInfluence[extremeIndex, 0] / exactCleanDeletion);

        Assert.IsTrue(
            Math.Abs(magnitudeRatio - 1d) > 0.25d,
            "The clean fixture must distinguish the current GMM magnitude from exact deletion scale.");

        double[] withInjectedOutlier = [.. SymmetricLog10Values, 3.5d];
        var outlierFit = FitGmm(withInjectedOutlier);
        double[,] outlierInfluence = outlierFit.GetObservationInfluence();
        int outlierIndex = withInjectedOutlier.Length - 1;
        int reportedMostInfluential = Enumerable.Range(0, withInjectedOutlier.Length)
            .OrderByDescending(index => Math.Abs(outlierInfluence[index, 0]))
            .First();
        int exactMostInfluential = Enumerable.Range(0, withInjectedOutlier.Length)
            .OrderByDescending(index => Math.Abs(ExactStandardizedMuDeletion(
                withInjectedOutlier,
                index,
                outlierFit.BestParameterSet.Values[0],
                outlierFit.GetStandardErrors()[0])))
            .First();

        Assert.AreEqual(outlierIndex, exactMostInfluential, "The analytical deletion oracle must identify the injected outlier.");
        Assert.AreEqual(outlierIndex, reportedMostInfluential, "The current GMM diagnostic must retain the correct influence ranking.");
    }

    /// <summary>
    /// Confirms that the legacy PSIS-shaped GMM adapter is obsolete while preserving compatibility.
    /// </summary>
    [TestMethod]
    public void GmmCookInfluence_LegacyPsisAdapterIsObsoleteCompatibilityOnly()
    {
        var gmm = FitGmm(SymmetricLog10Values);
        double[] cooksDistance = gmm.GetCooksDistance();
        var legacyMethod = typeof(GeneralizedMethodOfMoments).GetMethod(
            "GetInfluenceDiagnostics",
            Type.EmptyTypes);
        Assert.IsNotNull(legacyMethod);
        var obsolete = legacyMethod.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false)
            .Cast<ObsoleteAttribute>()
            .SingleOrDefault();
        Assert.IsNotNull(obsolete, "The misleading legacy adapter must not remain a supported API path.");
        StringAssert.Contains(obsolete.Message, "not a Pareto-k diagnostic");

        var diagnostics = (InfluenceDiagnostics?)legacyMethod.Invoke(gmm, null);
        Assert.IsNotNull(diagnostics, "The obsolete adapter must remain callable for compatibility.");

        Assert.AreEqual(cooksDistance.Length, diagnostics.Count, "GMM diagnostic observation count mismatch.");
        for (int i = 0; i < cooksDistance.Length; i++)
        {
            Assert.AreEqual(
                cooksDistance[i],
                diagnostics.Observations[i].ParetoK,
                1E-12d,
                $"Observation {i} does not reproduce the traced Cook-to-Pareto mapping.");
            Assert.IsTrue(double.IsNaN(diagnostics.Observations[i].ElpdLoo), "GMM does not compute pointwise PSIS ELPD.");
        }

        StringAssert.Contains(
            diagnostics.GetReliabilitySummary(),
            "PSIS-LOO",
            "The retained compatibility object must preserve its historical serialized behavior.");
    }

    /// <summary>
    /// Fits a Log10-Normal MAP model with a Gaussian prior on mu.
    /// </summary>
    /// <param name="log10Values">The observations in base-10 log space.</param>
    /// <param name="priorMean">The Gaussian prior mean for mu.</param>
    /// <param name="priorStandardDeviation">The Gaussian prior standard deviation for mu.</param>
    /// <returns>The fitted estimator and its model.</returns>
    private static (MaximumAPosteriori Estimator, UnivariateDistribution Model) FitMap(
        double[] log10Values,
        double priorMean,
        double priorStandardDeviation)
    {
        var model = new UnivariateDistribution(CreateDataFrame(log10Values), UnivariateDistributionType.LogNormal)
        {
            UseJeffreysRuleForScale = false,
            EnableQuantilePriors = false
        };
        model.Parameters[0].PriorDistribution = new Normal(priorMean, priorStandardDeviation);
        var estimator = new MaximumAPosteriori(model, OptimizationMethod.BFGS)
        {
            ComputeHessian = true,
            ReportFailure = true
        };
        estimator.Optimizer.AbsoluteTolerance = 1E-12d;
        estimator.Optimizer.RelativeTolerance = 1E-12d;

        Assert.IsTrue(estimator.Estimate(), "The deterministic Log10-Normal MAP fit must converge.");
        return (estimator, model);
    }

    /// <summary>
    /// Fits the just-identified Bulletin 17C Log10-Normal GMM model.
    /// </summary>
    /// <param name="log10Values">The observations in base-10 log space.</param>
    /// <returns>The fitted GMM estimator.</returns>
    private static GeneralizedMethodOfMoments FitGmm(double[] log10Values)
    {
        var model = new Bulletin17CDistribution(CreateDataFrame(log10Values), UnivariateDistributionType.LogNormal);
        var estimator = new GeneralizedMethodOfMoments(model, OptimizationMethod.BFGS)
        {
            EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative,
            UseFallbackOptimizer = false,
            MaxFunctionEvaluations = 10000,
            MaxGMMIterations = 100,
            AbsoluteTolerance = 1E-10d,
            RelativeTolerance = 1E-10d
        };

        Assert.IsTrue(estimator.Estimate(), "The deterministic Log10-Normal GMM fit must converge.");
        return estimator;
    }

    /// <summary>
    /// Computes the exact standardized deletion displacement for the sample-mean estimate of mu.
    /// </summary>
    /// <param name="log10Values">The observations in base-10 log space.</param>
    /// <param name="deletedIndex">The deleted observation index.</param>
    /// <param name="fullMu">The full-sample estimate of mu.</param>
    /// <param name="fullStandardError">The full-sample standard error of mu.</param>
    /// <returns><c>(muFull - muWithoutI) / SEFull</c>.</returns>
    private static double ExactStandardizedMuDeletion(
        double[] log10Values,
        int deletedIndex,
        double fullMu,
        double fullStandardError)
    {
        double leaveOneOutMu = (log10Values.Sum() - log10Values[deletedIndex]) / (log10Values.Length - 1d);
        return (fullMu - leaveOneOutMu) / fullStandardError;
    }

    /// <summary>
    /// Creates an exact-data frame from base-10 log observations.
    /// </summary>
    /// <param name="log10Values">The observations in base-10 log space.</param>
    /// <returns>The data frame on the original positive scale.</returns>
    private static DataFrame CreateDataFrame(double[] log10Values)
    {
        return new DataFrame
        {
            ExactSeries = new ExactSeries(log10Values.Select(value => Math.Pow(10d, value)).ToArray())
        };
    }
}
