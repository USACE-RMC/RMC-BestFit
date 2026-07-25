using Numerics.Distributions;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies the intended separation of fit influence, variance influence, and combined
/// leverage for Gaussian MAP priors and Gaussian-equivalent GMM penalties on mu.
/// </summary>
/// <remarks>
/// MAP and GMM Cook distances are interpreted only within their own estimator scales.
/// The tests compare the qualitative prior or penalty regimes, not Cook magnitudes across
/// likelihood and moment objectives. Sigma is reestimated jointly in every fit.
/// </remarks>
[TestClass]
public class PriorPenaltyInfluenceVerificationTests
{
    private static readonly double[] SymmetricLog10Values =
        [1.1d, 1.4d, 1.7d, 2.0d, 2.3d, 2.6d, 2.9d];

    /// <summary>
    /// Verifies the wide-centered, narrow-centered, and narrow-shifted Gaussian-prior
    /// regimes for MAP fit and variance influence.
    /// </summary>
    [TestMethod]
    public void MapMuPriorRegimes_SeparateFitAndVarianceInfluence()
    {
        var baseline = FitMap(SymmetricLog10Values, null, null);
        double mu = baseline.Estimator.BestParameterSet.Values[0];
        double standardError = Math.Sqrt(baseline.Estimator.GetCovarianceMatrix()[0, 0]);
        double wideStandardDeviation = 100d * standardError;
        double narrowStandardDeviation = 0.1d * standardError;

        LeverageDiagnostics.PriorComponentLeverage wide = GetMapMuPrior(
            FitMap(SymmetricLog10Values, mu, wideStandardDeviation));
        LeverageDiagnostics.PriorComponentLeverage narrowCentered = GetMapMuPrior(
            FitMap(SymmetricLog10Values, mu, narrowStandardDeviation));
        LeverageDiagnostics.PriorComponentLeverage narrowShifted = GetMapMuPrior(
            FitMap(SymmetricLog10Values, mu + 2d * standardError, narrowStandardDeviation));

        AssertWideCenteredRegime(wide, "MAP prior");
        AssertNarrowCenteredRegime(narrowCentered, wide, "MAP prior");
        AssertNarrowShiftedRegime(narrowShifted, narrowCentered, "MAP prior");
    }

    /// <summary>
    /// Verifies the wide-centered, narrow-centered, and narrow-shifted quadratic-penalty
    /// regimes for GMM fit and variance influence.
    /// </summary>
    [TestMethod]
    public void GmmMuPenaltyRegimes_SeparateFitAndVarianceInfluence()
    {
        GeneralizedMethodOfMoments baseline = FitGmm(SymmetricLog10Values, null, null);
        double mu = baseline.BestParameterSet.Values[0];
        double standardError = Math.Sqrt(baseline.GetCovarianceMatrix()[0, 0]);
        double wideStandardDeviation = 100d * standardError;
        double narrowStandardDeviation = 0.1d * standardError;

        LeverageDiagnostics.PriorComponentLeverage wide = GetGmmMuPenalty(
            FitGmm(SymmetricLog10Values, mu, wideStandardDeviation));
        LeverageDiagnostics.PriorComponentLeverage narrowCentered = GetGmmMuPenalty(
            FitGmm(SymmetricLog10Values, mu, narrowStandardDeviation));
        LeverageDiagnostics.PriorComponentLeverage narrowShifted = GetGmmMuPenalty(
            FitGmm(SymmetricLog10Values, mu + 2d * standardError, narrowStandardDeviation));

        AssertWideCenteredRegime(wide, "GMM penalty");
        AssertNarrowCenteredRegime(narrowCentered, wide, "GMM penalty");
        AssertNarrowShiftedRegime(narrowShifted, narrowCentered, "GMM penalty");
    }

    /// <summary>
    /// Verifies that a fixed centered prior or penalty contributes less variance influence
    /// as the sample grows while retaining negligible fit influence.
    /// </summary>
    [TestMethod]
    public void CenteredPriorAndPenalty_VarianceInfluenceDeclinesWithSampleSize()
    {
        double[] largeSample = Enumerable.Range(0, 10)
            .SelectMany(_ => SymmetricLog10Values)
            .ToArray();

        var mapBaseline = FitMap(SymmetricLog10Values, null, null);
        double mapMu = mapBaseline.Estimator.BestParameterSet.Values[0];
        double mapPriorStandardDeviation =
            0.1d * Math.Sqrt(mapBaseline.Estimator.GetCovarianceMatrix()[0, 0]);
        LeverageDiagnostics.PriorComponentLeverage mapSmall = GetMapMuPrior(
            FitMap(SymmetricLog10Values, mapMu, mapPriorStandardDeviation));
        LeverageDiagnostics.PriorComponentLeverage mapLarge = GetMapMuPrior(
            FitMap(largeSample, mapMu, mapPriorStandardDeviation));

        GeneralizedMethodOfMoments gmmBaseline = FitGmm(SymmetricLog10Values, null, null);
        double gmmMu = gmmBaseline.BestParameterSet.Values[0];
        double gmmPenaltyStandardDeviation =
            0.1d * Math.Sqrt(gmmBaseline.GetCovarianceMatrix()[0, 0]);
        LeverageDiagnostics.PriorComponentLeverage gmmSmall = GetGmmMuPenalty(
            FitGmm(SymmetricLog10Values, gmmMu, gmmPenaltyStandardDeviation));
        LeverageDiagnostics.PriorComponentLeverage gmmLarge = GetGmmMuPenalty(
            FitGmm(largeSample, gmmMu, gmmPenaltyStandardDeviation));

        Assert.IsTrue(mapSmall.FitInfluence < 1E-4d && mapLarge.FitInfluence < 1E-4d,
            "A centered MAP prior must retain negligible fit influence at both sample sizes.");
        Assert.IsTrue(mapLarge.VarianceInfluence < mapSmall.VarianceInfluence,
            "A fixed centered MAP prior must contribute less variance influence as sample size grows.");
        Assert.IsTrue(gmmSmall.FitInfluence < 1E-4d && gmmLarge.FitInfluence < 1E-4d,
            "A centered GMM penalty must retain negligible fit influence at both sample sizes.");
        Assert.IsTrue(gmmLarge.VarianceInfluence < gmmSmall.VarianceInfluence,
            "A fixed centered GMM penalty must contribute less variance influence as sample size grows.");
    }

    /// <summary>
    /// Verifies the wide-centered regime has negligible fit and variance influence.
    /// </summary>
    /// <param name="component">The prior or penalty component.</param>
    /// <param name="label">The estimator-specific assertion label.</param>
    private static void AssertWideCenteredRegime(
        LeverageDiagnostics.PriorComponentLeverage component,
        string label)
    {
        Assert.IsTrue(component.FitInfluence < 1E-4d,
            $"The wide centered {label} must have negligible fit influence.");
        Assert.IsTrue(component.VarianceInfluence < 1E-3d,
            $"The wide centered {label} must have negligible variance influence.");
        AssertCombinedIdentity(component, label + " wide-centered");
    }

    /// <summary>
    /// Verifies the narrow-centered regime has negligible fit influence and strong variance influence.
    /// </summary>
    /// <param name="component">The narrow-centered component.</param>
    /// <param name="wideComponent">The corresponding wide-centered component.</param>
    /// <param name="label">The estimator-specific assertion label.</param>
    private static void AssertNarrowCenteredRegime(
        LeverageDiagnostics.PriorComponentLeverage component,
        LeverageDiagnostics.PriorComponentLeverage wideComponent,
        string label)
    {
        Assert.IsTrue(component.FitInfluence < 1E-4d,
            $"The narrow centered {label} must have negligible fit influence.");
        Assert.IsTrue(component.VarianceInfluence > 100d * wideComponent.VarianceInfluence,
            $"The narrow centered {label} must have much larger variance influence than the wide regime.");
        AssertCombinedIdentity(component, label + " narrow-centered");
    }

    /// <summary>
    /// Verifies the narrow-shifted regime has both fit and variance influence.
    /// </summary>
    /// <param name="component">The narrow-shifted component.</param>
    /// <param name="centeredComponent">The corresponding narrow-centered component.</param>
    /// <param name="label">The estimator-specific assertion label.</param>
    private static void AssertNarrowShiftedRegime(
        LeverageDiagnostics.PriorComponentLeverage component,
        LeverageDiagnostics.PriorComponentLeverage centeredComponent,
        string label)
    {
        Assert.IsTrue(component.FitInfluence > centeredComponent.FitInfluence + 1E-3d,
            $"The narrow shifted {label} must add material fit influence.");
        Assert.IsTrue(component.VarianceInfluence > 0d,
            $"The narrow shifted {label} must retain positive variance influence.");
        AssertCombinedIdentity(component, label + " narrow-shifted");
    }

    /// <summary>
    /// Verifies the combined leverage value is exactly the sum of fit and variance influence.
    /// </summary>
    /// <param name="component">The diagnostic component.</param>
    /// <param name="label">The assertion label.</param>
    private static void AssertCombinedIdentity(
        LeverageDiagnostics.PriorComponentLeverage component,
        string label)
    {
        Assert.AreEqual(
            component.FitInfluence + component.VarianceInfluence,
            component.Leverage,
            1E-12d,
            $"{label} combined leverage must equal fit plus variance influence.");
    }

    /// <summary>
    /// Gets the mu prior component from a fitted MAP model.
    /// </summary>
    /// <param name="fit">The fitted MAP estimator and model.</param>
    /// <returns>The mu prior influence component.</returns>
    private static LeverageDiagnostics.PriorComponentLeverage GetMapMuPrior(
        (MaximumAPosteriori Estimator, UnivariateDistribution Model) fit)
    {
        var diagnostics = new LeverageDiagnostics(fit.Model, fit.Estimator.BestParameterSet.Values);
        Assert.IsTrue(diagnostics.PriorComponents.Length > 0, "MAP mu prior component was not reported.");
        return diagnostics.PriorComponents[0];
    }

    /// <summary>
    /// Gets the single enabled mu penalty component from a fitted GMM model.
    /// </summary>
    /// <param name="estimator">The fitted GMM estimator.</param>
    /// <returns>The mu penalty influence component.</returns>
    private static LeverageDiagnostics.PriorComponentLeverage GetGmmMuPenalty(
        GeneralizedMethodOfMoments estimator)
    {
        LeverageDiagnostics diagnostics = estimator.GetLeverageDiagnostics();
        Assert.AreEqual(1, diagnostics.PriorComponents.Length, "Expected one enabled mu penalty.");
        return diagnostics.PriorComponents[0];
    }

    /// <summary>
    /// Fits a Log10-Normal MAP model with an optional Gaussian prior on mu.
    /// </summary>
    /// <param name="log10Values">The observations in base-10 log space.</param>
    /// <param name="priorMean">The Gaussian prior mean, or <see langword="null"/>.</param>
    /// <param name="priorStandardDeviation">The Gaussian prior standard deviation, or <see langword="null"/>.</param>
    /// <returns>The fitted MAP estimator and model.</returns>
    private static (MaximumAPosteriori Estimator, UnivariateDistribution Model) FitMap(
        double[] log10Values,
        double? priorMean,
        double? priorStandardDeviation)
    {
        var model = new UnivariateDistribution(
            CreateDataFrame(log10Values),
            UnivariateDistributionType.LogNormal)
        {
            UseJeffreysRuleForScale = false,
            EnableQuantilePriors = false
        };
        if (priorMean.HasValue && priorStandardDeviation.HasValue)
        {
            model.Parameters[0].PriorDistribution =
                new Normal(priorMean.Value, priorStandardDeviation.Value);
        }

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
    /// Fits a B17C Log10-Normal GMM model with an optional Gaussian-equivalent penalty on mu.
    /// </summary>
    /// <param name="log10Values">The observations in base-10 log space.</param>
    /// <param name="penaltyMean">The penalty mean, or <see langword="null"/>.</param>
    /// <param name="penaltyStandardDeviation">The Gaussian-equivalent standard deviation, or <see langword="null"/>.</param>
    /// <returns>The fitted GMM estimator.</returns>
    private static GeneralizedMethodOfMoments FitGmm(
        double[] log10Values,
        double? penaltyMean,
        double? penaltyStandardDeviation)
    {
        var model = new Bulletin17CDistribution(
            CreateDataFrame(log10Values),
            UnivariateDistributionType.LogNormal);
        if (penaltyMean.HasValue && penaltyStandardDeviation.HasValue)
        {
            model.ParameterPenalties[0].Enabled = true;
            model.ParameterPenalties[0].Mean = penaltyMean.Value;
            model.ParameterPenalties[0].MSE =
                penaltyStandardDeviation.Value * penaltyStandardDeviation.Value;
            model.ParameterPenalties[0].UseLog = false;
            model.SetPenaltyFunction();
        }

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
