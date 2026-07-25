using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies Log10-Normal MLE, MAP, and Bulletin 17C GMM inference against their respective
/// closed-form sufficient-statistic results in base-10 log space.
/// </summary>
/// <remarks>
/// The deterministic fixture has log10 values symmetric about two. The MLE scale is 0.6 using
/// the finite-sample denominator n; the GMM scale is 0.648074069840786 using the unbiased sample
/// moment denominator n-1. The tests use local optimizers so comparisons are governed by
/// parameter convergence rather than stochastic search.
/// </remarks>
[TestClass]
public class Log10NormalEstimationEquivalenceTests
{
    private const int SampleSize = 7;
    private const double ExpectedMu = 2d;
    private const double ExpectedMleSigma = 0.6d;
    private const double ExpectedMomentSigma = 0.648074069840786d;

    /// <summary>
    /// Verifies that MLE and flat-prior MAP recover the same closed-form Log10-Normal parameters
    /// and data likelihood.
    /// </summary>
    [TestMethod]
    public void FlatPriorMap_MatchesClosedFormLog10NormalMle()
    {
        DataFrame dataFrame = CreateDataFrame();
        var mleModel = new UnivariateDistribution(dataFrame, UnivariateDistributionType.LogNormal);
        var mle = new MaximumLikelihood(mleModel, OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };
        mle.Optimizer.AbsoluteTolerance = 1E-12d;
        mle.Optimizer.RelativeTolerance = 1E-12d;

        bool mleEstimated = mle.Estimate();

        var mapModel = new UnivariateDistribution(dataFrame, UnivariateDistributionType.LogNormal)
        {
            // The production default applies Jeffreys' 1/sigma rule. Disable it explicitly so
            // this method isolates the flat-prior MAP/MLE identity before testing informative priors.
            UseJeffreysRuleForScale = false
        };
        var map = new MaximumAPosteriori(mapModel, OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };
        map.Optimizer.AbsoluteTolerance = 1E-12d;
        map.Optimizer.RelativeTolerance = 1E-12d;

        bool mapEstimated = map.Estimate();

        Assert.IsTrue(mleEstimated, "The deterministic Log10-Normal MLE must converge.");
        Assert.IsTrue(mapEstimated, "The deterministic flat-prior Log10-Normal MAP must converge.");
        Assert.AreEqual(ExpectedMu, mle.BestParameterSet.Values[0], 1E-5d, "MLE mu mismatch.");
        Assert.AreEqual(ExpectedMleSigma, mle.BestParameterSet.Values[1], 1E-5d, "MLE sigma mismatch.");
        Assert.AreEqual(ExpectedMu, map.BestParameterSet.Values[0], 1E-5d, "MAP mu mismatch.");
        Assert.AreEqual(ExpectedMleSigma, map.BestParameterSet.Values[1], 1E-5d, "MAP sigma mismatch.");
        Assert.AreEqual(
            mleModel.DataLogLikelihood(mle.BestParameterSet.Values),
            mapModel.DataLogLikelihood(map.BestParameterSet.Values),
            1E-8d,
            "Flat-prior MAP and MLE data likelihoods must agree.");
    }

    /// <summary>
    /// Verifies that the just-identified Bulletin 17C Log10-Normal GMM estimate matches exact
    /// unbiased sample moments.
    /// </summary>
    /// <remarks>
    /// Nelder-Mead isolates the moment-definition comparison from the objective-gradient scaling
    /// audited separately under TR-033. For a just-identified model, the weighting matrix cannot
    /// change a finite solution of the two moment equations.
    /// </remarks>
    [TestMethod]
    public void UnpenalizedB17CGmm_MatchesExactSampleMoments()
    {
        DataFrame dataFrame = CreateDataFrame();
        var model = new Bulletin17CDistribution(dataFrame, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model, OptimizationMethod.NelderMead)
        {
            EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep,
            UseFallbackOptimizer = false,
            MaxFunctionEvaluations = 10000
        };

        bool estimated = gmm.Estimate();

        Assert.IsTrue(estimated, "The deterministic just-identified Log10-Normal GMM fit must succeed.");
        Assert.AreEqual(ExpectedMu, gmm.BestParameterSet.Values[0], 1E-5d, "GMM mu mismatch.");
        Assert.AreEqual(ExpectedMomentSigma, gmm.BestParameterSet.Values[1], 1E-5d, "GMM sigma mismatch.");
        Assert.AreEqual(0d, gmm.ObjectiveFunctionValue, 1E-10d, "Just-identified GMM objective mismatch.");
    }

    /// <summary>
    /// Verifies the fit and covariance influence of wide-centered, narrow-centered, and
    /// narrow-shifted Gaussian priors on Log10-Normal mu.
    /// </summary>
    /// <remarks>
    /// At a centered prior, the joint MAP remains at the MLE. Its mu variance contracts by
    /// <c>1 / (1 + SE_L^2 / tau^2)</c>, so a prior with <c>tau = 100 SE_L</c> is materially
    /// flat while <c>tau = 0.1 SE_L</c> removes approximately 99 percent of the conditional
    /// variance without shifting the fit. Moving the narrow prior center by <c>2 SE_L</c>
    /// changes both the fitted location and the full-Hessian covariance. Sigma is reoptimized
    /// jointly in every case; it is not held fixed in the analytical comparison.
    /// </remarks>
    [TestMethod]
    public void MapMuPriorRegimes_MatchAnalyticalFitAndVarianceInfluence()
    {
        double likelihoodStandardError = ExpectedMleSigma / Math.Sqrt(SampleSize);
        double widePriorStandardDeviation = 100d * likelihoodStandardError;
        double narrowPriorStandardDeviation = 0.1d * likelihoodStandardError;

        var flat = FitMap(null, null);
        var wideCentered = FitMap(ExpectedMu, widePriorStandardDeviation);
        var narrowCentered = FitMap(ExpectedMu, narrowPriorStandardDeviation);
        var narrowShifted = FitMap(ExpectedMu + 2d * likelihoodStandardError, narrowPriorStandardDeviation);

        AssertMapMatchesAnalyticalHessian(flat, null, null, "flat");
        AssertMapMatchesAnalyticalHessian(wideCentered, ExpectedMu, widePriorStandardDeviation, "wide-centered");
        AssertMapMatchesAnalyticalHessian(narrowCentered, ExpectedMu, narrowPriorStandardDeviation, "narrow-centered");
        AssertMapMatchesAnalyticalHessian(
            narrowShifted,
            ExpectedMu + 2d * likelihoodStandardError,
            narrowPriorStandardDeviation,
            "narrow-shifted");

        Assert.AreEqual(flat.Mu, wideCentered.Mu, 1E-5d, "A wide centered prior must not materially shift mu.");
        Assert.AreEqual(flat.Sigma, wideCentered.Sigma, 1E-5d, "A wide centered prior must not materially shift sigma.");
        Assert.IsTrue(
            wideCentered.MuVariance / flat.MuVariance > 0.999d,
            "A wide centered prior must have negligible variance influence.");

        Assert.AreEqual(flat.Mu, narrowCentered.Mu, 1E-5d, "A narrow centered prior must not shift mu.");
        Assert.AreEqual(flat.Sigma, narrowCentered.Sigma, 1E-5d, "A narrow centered prior must not shift sigma.");
        Assert.IsTrue(
            narrowCentered.MuVariance / flat.MuVariance < 0.011d,
            "A narrow centered prior must contract mu variance by approximately 99 percent.");

        Assert.IsTrue(
            narrowShifted.Mu - flat.Mu > 1.5d * likelihoodStandardError,
            "A narrow shifted prior must materially move the fitted location.");
        Assert.IsTrue(
            narrowShifted.MuVariance < flat.MuVariance,
            "A narrow shifted prior must influence the full-Hessian mu variance.");
    }

    /// <summary>
    /// Verifies the fit and covariance influence of equivalent quadratic B17C penalties on mu.
    /// </summary>
    /// <remarks>
    /// The B17C moment estimator deliberately retains the unbiased scale convention. Therefore,
    /// prior widths are expressed relative to the GMM location standard error
    /// <c>sigma_GMM / sqrt(n)</c>, not the biased MLE scale. Because
    /// <see cref="ParameterPenalty.Function(double, int)"/> already divides by <c>n</c>, a
    /// Gaussian prior variance <c>tau²</c> maps directly to <see cref="ParameterPenalty.MSE"/>.
    /// The non-sandwich covariance is used for posterior-curvature equivalence.
    /// </remarks>
    [TestMethod]
    public void GmmMuPenaltyRegimes_MatchAnalyticalFitAndVarianceInfluence()
    {
        double likelihoodStandardError = ExpectedMomentSigma / Math.Sqrt(SampleSize);
        double widePriorStandardDeviation = 100d * likelihoodStandardError;
        double narrowPriorStandardDeviation = 0.1d * likelihoodStandardError;

        var flat = FitGmm(null, null);
        var wideCentered = FitGmm(ExpectedMu, widePriorStandardDeviation);
        var narrowCentered = FitGmm(ExpectedMu, narrowPriorStandardDeviation);
        var narrowShifted = FitGmm(ExpectedMu + 2d * likelihoodStandardError, narrowPriorStandardDeviation);

        AssertGmmMatchesAnalyticalBread(flat, null, null, "flat");
        AssertGmmMatchesAnalyticalBread(wideCentered, ExpectedMu, widePriorStandardDeviation, "wide-centered");
        AssertGmmMatchesAnalyticalBread(narrowCentered, ExpectedMu, narrowPriorStandardDeviation, "narrow-centered");
        AssertGmmMatchesAnalyticalBread(
            narrowShifted,
            ExpectedMu + 2d * likelihoodStandardError,
            narrowPriorStandardDeviation,
            "narrow-shifted");

        Assert.AreEqual(flat.Mu, wideCentered.Mu, 1E-4d, "A wide centered penalty must not materially shift mu.");
        Assert.AreEqual(flat.Sigma, wideCentered.Sigma, 1E-4d, "A wide centered penalty must not materially shift sigma.");
        Assert.IsTrue(
            wideCentered.MuVariance / flat.MuVariance > 0.999d,
            "A wide centered penalty must have negligible variance influence.");

        Assert.AreEqual(flat.Mu, narrowCentered.Mu, 1E-4d, "A narrow centered penalty must not shift mu.");
        Assert.AreEqual(flat.Sigma, narrowCentered.Sigma, 1E-4d, "A narrow centered penalty must not shift sigma.");
        Assert.IsTrue(
            narrowCentered.MuVariance / flat.MuVariance < 0.011d,
            "A narrow centered penalty must contract mu variance by approximately 99 percent.");

        Assert.IsTrue(
            narrowShifted.Mu - flat.Mu > 1.5d * likelihoodStandardError,
            "A narrow shifted penalty must materially move the fitted location.");
        Assert.IsTrue(
            narrowShifted.MuVariance < flat.MuVariance,
            "A narrow shifted penalty must influence the inverse-bread mu variance.");
    }

    /// <summary>
    /// Verifies that MAP Gaussian priors and B17C quadratic penalties reproduce the
    /// inverse-variance posterior mean and variance for mu.
    /// </summary>
    /// <remarks>
    /// Raw sigma values are intentionally not equated: MAP uses the finite-sample MLE denominator
    /// <c>n</c>, whereas B17C GMM uses the unbiased moment denominator <c>n - 1</c>. Each Gaussian
    /// prior or quadratic penalty is therefore scaled by its estimator's own unpenalized mu
    /// variance. For baseline estimate <c>muL</c>, variance <c>VL</c>, prior mean <c>m</c>, and
    /// variance <c>tauSquared</c>, the required oracle is
    /// <c>Vpost = 1 / (1 / VL + 1 / tauSquared)</c> and
    /// <c>muPost = Vpost * (muL / VL + m / tauSquared)</c>.
    /// Sigma is reestimated jointly in every fit; it is not fixed. Tolerances cover numerical
    /// Hessian evaluation and the iterative update of the GMM weighting matrix, not a different
    /// posterior definition.
    /// </remarks>
    [TestMethod]
    public void MapAndGmmMuPosterior_MatchesInverseVarianceWeighting()
    {
        var mapFlat = FitMap(null, null);
        var gmmFlat = FitGmm(null, null);

        double mapStandardError = Math.Sqrt(mapFlat.MuVariance);
        double gmmStandardError = Math.Sqrt(gmmFlat.MuVariance);
        double mapWideStandardDeviation = 100d * mapStandardError;
        double gmmWideStandardDeviation = 100d * gmmStandardError;
        double mapNarrowStandardDeviation = 0.1d * mapStandardError;
        double gmmNarrowStandardDeviation = 0.1d * gmmStandardError;
        double mapShiftedMean = mapFlat.Mu + 2d * mapStandardError;
        double gmmShiftedMean = gmmFlat.Mu + 2d * gmmStandardError;

        var mapWide = FitMap(mapFlat.Mu, mapWideStandardDeviation);
        var gmmWide = FitGmm(gmmFlat.Mu, gmmWideStandardDeviation);
        var mapNarrow = FitMap(mapFlat.Mu, mapNarrowStandardDeviation);
        var gmmNarrow = FitGmm(gmmFlat.Mu, gmmNarrowStandardDeviation);
        var mapShifted = FitMap(mapShiftedMean, mapNarrowStandardDeviation);
        var gmmShifted = FitGmm(gmmShiftedMean, gmmNarrowStandardDeviation);

        Assert.AreEqual(ExpectedMleSigma, mapFlat.Sigma, 1E-5d, "MAP must retain the MLE scale convention.");
        Assert.AreEqual(ExpectedMomentSigma, gmmFlat.Sigma, 1E-5d, "GMM must retain the unbiased moment scale convention.");

        AssertInverseVariancePosterior(
            mapFlat.Mu, mapFlat.MuVariance, mapFlat.Mu, mapWideStandardDeviation,
            mapWide.Mu, mapWide.MuVariance, 1E-4d, 2E-3d, "MAP wide-centered");
        AssertInverseVariancePosterior(
            gmmFlat.Mu, gmmFlat.MuVariance, gmmFlat.Mu, gmmWideStandardDeviation,
            gmmWide.Mu, gmmWide.MuVariance, 1E-4d, 2E-3d, "GMM wide-centered");
        AssertInverseVariancePosterior(
            mapFlat.Mu, mapFlat.MuVariance, mapFlat.Mu, mapNarrowStandardDeviation,
            mapNarrow.Mu, mapNarrow.MuVariance, 1E-4d, 2E-3d, "MAP narrow-centered");
        AssertInverseVariancePosterior(
            gmmFlat.Mu, gmmFlat.MuVariance, gmmFlat.Mu, gmmNarrowStandardDeviation,
            gmmNarrow.Mu, gmmNarrow.MuVariance, 1E-4d, 2E-3d, "GMM narrow-centered");
        AssertInverseVariancePosterior(
            mapFlat.Mu, mapFlat.MuVariance, mapShiftedMean, mapNarrowStandardDeviation,
            mapShifted.Mu, mapShifted.MuVariance, 0.01d * mapStandardError, 1.5E-2d, "MAP narrow-shifted");
        AssertInverseVariancePosterior(
            gmmFlat.Mu, gmmFlat.MuVariance, gmmShiftedMean, gmmNarrowStandardDeviation,
            gmmShifted.Mu, gmmShifted.MuVariance, 0.01d * gmmStandardError, 5E-3d, "GMM narrow-shifted");

        Assert.AreEqual(
            (mapShifted.Mu - mapFlat.Mu) / mapStandardError,
            (gmmShifted.Mu - gmmFlat.Mu) / gmmStandardError,
            1E-2d,
            "MAP and GMM standardized posterior mu must agree after their finite-sample scale normalization.");
        Assert.AreEqual(
            mapShifted.MuVariance / mapFlat.MuVariance,
            gmmShifted.MuVariance / gmmFlat.MuVariance,
            1.5E-2d,
            "MAP and GMM posterior mu-variance contraction must agree after scale normalization.");
    }

    /// <summary>
    /// Fits a Log10-Normal MAP model with an optional Gaussian prior on mu.
    /// </summary>
    /// <param name="priorMean">The Gaussian prior mean, or <see langword="null"/> for the flat baseline.</param>
    /// <param name="priorStandardDeviation">The Gaussian prior standard deviation, or <see langword="null"/> for the flat baseline.</param>
    /// <returns>The fitted mu, sigma, and full-Hessian variance of mu.</returns>
    private static (double Mu, double Sigma, double MuVariance) FitMap(
        double? priorMean,
        double? priorStandardDeviation)
    {
        var model = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.LogNormal)
        {
            UseJeffreysRuleForScale = false,
            EnableQuantilePriors = false
        };
        if (priorMean.HasValue && priorStandardDeviation.HasValue)
            model.Parameters[0].PriorDistribution = new Normal(priorMean.Value, priorStandardDeviation.Value);

        var map = new MaximumAPosteriori(model, OptimizationMethod.BFGS)
        {
            ComputeHessian = true,
            ReportFailure = true
        };
        map.Optimizer.AbsoluteTolerance = 1E-12d;
        map.Optimizer.RelativeTolerance = 1E-12d;

        bool estimated = map.Estimate();

        Assert.IsTrue(estimated, "The deterministic Log10-Normal MAP fit must converge.");
        var covariance = map.GetCovarianceMatrix();
        return (map.BestParameterSet.Values[0], map.BestParameterSet.Values[1], covariance[0, 0]);
    }

    /// <summary>
    /// Fits a B17C Log10-Normal GMM model with an optional quadratic penalty on mu.
    /// </summary>
    /// <param name="penaltyMean">The penalty mean, or <see langword="null"/> for the unpenalized baseline.</param>
    /// <param name="penaltyStandardDeviation">The equivalent Gaussian standard deviation, or <see langword="null"/> for the baseline.</param>
    /// <returns>The fitted parameters, inverse-bread variance, moments, and objective gradient.</returns>
    private static (
        double Mu,
        double Sigma,
        double MuVariance,
        double FirstMoment,
        double SecondMoment,
        double MuGradient) FitGmm(
            double? penaltyMean,
            double? penaltyStandardDeviation)
    {
        var model = new Bulletin17CDistribution(CreateDataFrame(), UnivariateDistributionType.LogNormal);
        if (penaltyMean.HasValue && penaltyStandardDeviation.HasValue)
        {
            model.ParameterPenalties[0].Enabled = true;
            model.ParameterPenalties[0].Mean = penaltyMean.Value;
            model.ParameterPenalties[0].MSE = penaltyStandardDeviation.Value * penaltyStandardDeviation.Value;
            model.ParameterPenalties[0].UseLog = false;
            model.SetPenaltyFunction();
        }

        var gmm = new GeneralizedMethodOfMoments(model, OptimizationMethod.BFGS)
        {
            EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative,
            UseFallbackOptimizer = false,
            MaxFunctionEvaluations = 10000,
            MaxGMMIterations = 100,
            AbsoluteTolerance = 1E-10d,
            RelativeTolerance = 1E-10d
        };

        bool estimated = gmm.Estimate();

        Assert.IsTrue(estimated, "The deterministic penalized Log10-Normal GMM fit must succeed.");
        double[] parameters = gmm.BestParameterSet.Values;
        // Use the same default sandwich path consumed by Bulletin17CAnalysis. With the Gaussian
        // penalty Hessian included in both bread and meat, efficient W = S^-1 reduces this to
        // the inverse total precision required by the posterior oracle.
        var covariance = gmm.GetCovarianceMatrix();
        var moments = gmm.GetG(parameters);
        var gradient = gmm.GetGradient(parameters);
        return (parameters[0], parameters[1], covariance[0, 0], moments[0], moments[1], gradient[0]);
    }

    /// <summary>
    /// Confirms a fitted GMM result against the exact moment, penalized-score, and bread equations.
    /// </summary>
    /// <param name="fit">The fitted GMM result.</param>
    /// <param name="penaltyMean">The penalty mean, or <see langword="null"/> for no penalty.</param>
    /// <param name="penaltyStandardDeviation">The equivalent Gaussian standard deviation, or <see langword="null"/> for no penalty.</param>
    /// <param name="regime">The regime name used in assertion messages.</param>
    private static void AssertGmmMatchesAnalyticalBread(
        (double Mu, double Sigma, double MuVariance, double FirstMoment, double SecondMoment, double MuGradient) fit,
        double? penaltyMean,
        double? penaltyStandardDeviation,
        string regime)
    {
        double displacement = fit.Mu - ExpectedMu;
        double c2 = SampleSize / (double)(SampleSize - 1);
        double expectedSigmaSquared = ExpectedMomentSigma * ExpectedMomentSigma + c2 * displacement * displacement;
        double priorPrecision = penaltyStandardDeviation.HasValue
            ? 1d / (penaltyStandardDeviation.Value * penaltyStandardDeviation.Value)
            : 0d;
        // B17C forms S = E[g_i g_i'] - gBar gBar'. For a Normal location moment at a
        // penalized solution, S11 is therefore sigma^2 - (mu - xBar)^2 rather than sigma^2.
        double locationMomentVariance = expectedSigmaSquared - displacement * displacement;
        double muScore = SampleSize * displacement / locationMomentVariance;
        if (penaltyMean.HasValue)
            muScore += (fit.Mu - penaltyMean.Value) * priorPrecision;
        double expectedMuVariance = 1d / (SampleSize / locationMomentVariance + priorPrecision);

        Assert.AreEqual(-displacement, fit.FirstMoment, 2E-5d, $"The {regime} first moment mismatch.");
        Assert.AreEqual(0d, fit.SecondMoment, 2E-5d, $"The {regime} variance moment is not solved.");
        Assert.AreEqual(0d, fit.MuGradient, 2E-5d, $"The {regime} objective gradient is not stationary.");
        Assert.AreEqual(0d, muScore, 2E-4d, $"The {regime} penalized score equation is not stationary.");
        Assert.AreEqual(expectedSigmaSquared, fit.Sigma * fit.Sigma, 2E-5d, $"The {regime} sigma moment mismatch.");
        Assert.AreEqual(
            expectedMuVariance,
            fit.MuVariance,
            Math.Max(2E-5d, 2E-3d * expectedMuVariance),
            $"The {regime} inverse-bread mu variance mismatch.");
    }

    /// <summary>
    /// Compares a fitted posterior location and variance with scalar inverse-variance weighting.
    /// </summary>
    /// <param name="baselineMu">The unpenalized location estimate.</param>
    /// <param name="baselineVariance">The unpenalized variance of the location estimate.</param>
    /// <param name="priorMean">The Gaussian prior or quadratic-penalty mean.</param>
    /// <param name="priorStandardDeviation">The Gaussian prior or equivalent penalty standard deviation.</param>
    /// <param name="posteriorMu">The fitted posterior location.</param>
    /// <param name="posteriorVariance">The fitted posterior location variance.</param>
    /// <param name="muTolerance">The absolute tolerance for the posterior location.</param>
    /// <param name="varianceRelativeTolerance">The relative tolerance for posterior variance.</param>
    /// <param name="regime">The estimator and regime label used in assertion messages.</param>
    private static void AssertInverseVariancePosterior(
        double baselineMu,
        double baselineVariance,
        double priorMean,
        double priorStandardDeviation,
        double posteriorMu,
        double posteriorVariance,
        double muTolerance,
        double varianceRelativeTolerance,
        string regime)
    {
        double priorVariance = priorStandardDeviation * priorStandardDeviation;
        double expectedVariance = 1d / (1d / baselineVariance + 1d / priorVariance);
        double expectedMu = expectedVariance * (baselineMu / baselineVariance + priorMean / priorVariance);

        Assert.AreEqual(
            expectedMu,
            posteriorMu,
            muTolerance,
            $"The {regime} posterior mu does not match inverse-variance weighting.");
        Assert.AreEqual(
            expectedVariance,
            posteriorVariance,
            Math.Max(1E-8d, varianceRelativeTolerance * expectedVariance),
            $"The {regime} posterior mu variance does not match inverse-variance weighting.");
    }

    /// <summary>
    /// Confirms a fitted MAP result against the exact score equations and inverse Hessian.
    /// </summary>
    /// <param name="fit">The fitted MAP result.</param>
    /// <param name="priorMean">The Gaussian prior mean, or <see langword="null"/> for a flat prior.</param>
    /// <param name="priorStandardDeviation">The Gaussian prior standard deviation, or <see langword="null"/> for a flat prior.</param>
    /// <param name="regime">The regime name used in assertion messages.</param>
    private static void AssertMapMatchesAnalyticalHessian(
        (double Mu, double Sigma, double MuVariance) fit,
        double? priorMean,
        double? priorStandardDeviation,
        string regime)
    {
        double displacement = fit.Mu - ExpectedMu;
        double expectedSigmaSquared = ExpectedMleSigma * ExpectedMleSigma + displacement * displacement;
        double priorPrecision = priorStandardDeviation.HasValue
            ? 1d / (priorStandardDeviation.Value * priorStandardDeviation.Value)
            : 0d;
        double muScore = SampleSize * displacement / (fit.Sigma * fit.Sigma);
        if (priorMean.HasValue)
            muScore += (fit.Mu - priorMean.Value) * priorPrecision;

        Assert.AreEqual(0d, muScore, 2E-4d, $"The {regime} mu score equation is not stationary.");
        Assert.AreEqual(
            expectedSigmaSquared,
            fit.Sigma * fit.Sigma,
            2E-5d,
            $"The {regime} sigma score equation is not stationary.");

        double jMuMu = SampleSize / expectedSigmaSquared + priorPrecision;
        double jMuSigma = -2d * SampleSize * displacement / Math.Pow(expectedSigmaSquared, 1.5d);
        double jSigmaSigma = 2d * SampleSize / expectedSigmaSquared;
        double expectedMuVariance = jSigmaSigma / (jMuMu * jSigmaSigma - jMuSigma * jMuSigma);

        Assert.AreEqual(
            expectedMuVariance,
            fit.MuVariance,
            Math.Max(2E-5d, 2E-3d * expectedMuVariance),
            $"The {regime} full-Hessian mu variance does not match the analytical inverse Hessian.");
    }

    /// <summary>
    /// Creates the deterministic seven-observation Log10-Normal fixture.
    /// </summary>
    /// <returns>A data frame whose base-10 logarithms are symmetric about two.</returns>
    private static DataFrame CreateDataFrame()
    {
        double[] log10Values = [1.1d, 1.4d, 1.7d, 2.0d, 2.3d, 2.6d, 2.9d];
        double[] observedValues = log10Values.Select(value => Math.Pow(10d, value)).ToArray();
        return new DataFrame
        {
            ExactSeries = new ExactSeries(observedValues)
        };
    }
}
