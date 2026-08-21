using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Verification.TimeSeriesAnalysis;
using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;

namespace RMC.BestFit.Verification.RatingCurve;

/// <summary>
/// Replicates the three synthetic rating-curve cases of <c>examples/6-rating-curve-analysis</c> in the
/// Verification library: production maximum-likelihood and Bayesian recovery against an independent
/// SciPy conditional maximum-likelihood optimum and the generating curve.
/// </summary>
/// <remarks>
/// <para>
/// Every cell builds the model exactly as the example project does (default flat priors, Jeffreys'
/// rule for the scale) and assigns no <c>BayesianAnalysis</c> setting; the resolved production
/// DEMCzs defaults are asserted before and after sampling. The fixtures and the independent optimum
/// come from the committed <c>rating-curve-example-fixtures.json</c> artifact.
/// </para>
/// <para>
/// Acceptance: the BestFit likelihood equals the oracle at the independent optimum; the production
/// MLE reaches that optimum's log likelihood and agrees with its parameters; the Bayesian run has
/// R-hat below the limit and ESS above the minimum for every parameter, its sampled MAP agrees with
/// the independent optimum, the median posterior curve lies within the declared band of the true
/// curve on the calibration grid, and the true curve lies inside the credible band at the declared
/// fraction of grid stages. Multi-segment parameters trade off along the curve, so the three-segment
/// parameter tolerances are wider while the curve-level checks are the same for every case.
/// </para>
/// </remarks>
[TestClass]
public class RatingCurveExampleRecoveryTests
{
    /// <summary>Absolute tolerance for the likelihood evaluated at the independent optimum.</summary>
    private const double SamePointLikelihoodTolerance = 1e-8;

    /// <summary>Shortfall allowed between the production MLE log likelihood and the independent optimum.</summary>
    private const double MleOptimalityTolerance = 1e-5;

    /// <summary>Relative MLE parameter tolerance for the one- and two-segment cases.</summary>
    private const double MleRelativeTolerance = 1e-3;

    /// <summary>Relative MLE parameter tolerance for the three-segment case.</summary>
    private const double ThreeSegmentMleRelativeTolerance = 1e-2;

    /// <summary>Absolute floor applied to every relative parameter tolerance.</summary>
    private const double ParameterAbsoluteFloor = 1e-3;

    /// <summary>Relative sampled-MAP tolerance for the one- and two-segment cases.</summary>
    private const double MapRelativeTolerance = 0.05;

    /// <summary>Relative sampled-MAP tolerance for the three-segment case.</summary>
    private const double ThreeSegmentMapRelativeTolerance = 0.10;

    /// <summary>Upper R-hat limit for every parameter.</summary>
    private const double RhatLimit = 1.1;

    /// <summary>Lower ESS limit for every parameter.</summary>
    private const double EssMinimum = 100.0;

    /// <summary>Relative band around the true curve for the point and median curves.</summary>
    private const double CurveRelativeBand = 0.05;

    /// <summary>Minimum fraction of grid stages at which the true curve must lie inside the credible band.</summary>
    private const double CurveCoverageFraction = 0.90;

    /// <summary>Lowest grid stage used for curve checks; below it the curve approaches zero flow.</summary>
    private const double CurveGridMinimumStage = 2.0;

    /// <summary>Production MLE recovery of the one-segment example case.</summary>
    [TestMethod]
    public void Mle_OneSegment_RecoversExampleCurve() => AssertMleRecovery("one_segment");

    /// <summary>Production MLE recovery of the two-segment example case.</summary>
    [TestMethod]
    public void Mle_TwoSegment_RecoversExampleCurve() => AssertMleRecovery("two_segment");

    /// <summary>Production MLE recovery of the three-segment example case.</summary>
    [TestMethod]
    public void Mle_ThreeSegment_RecoversExampleCurve() => AssertMleRecovery("three_segment");

    /// <summary>Default-setting Bayesian recovery of the one-segment example case.</summary>
    [TestMethod]
    public async Task Bayesian_OneSegment_RecoversExampleCurve() => await AssertBayesianRecovery("one_segment");

    /// <summary>Default-setting Bayesian recovery of the two-segment example case.</summary>
    [TestMethod]
    public async Task Bayesian_TwoSegment_RecoversExampleCurve() => await AssertBayesianRecovery("two_segment");

    /// <summary>Default-setting Bayesian recovery of the three-segment example case.</summary>
    [TestMethod]
    public async Task Bayesian_ThreeSegment_RecoversExampleCurve() => await AssertBayesianRecovery("three_segment");

    /// <summary>
    /// Runs the production MLE on one example case and compares it with the independent optimum.
    /// </summary>
    /// <param name="key">The example case key.</param>
    private static void AssertMleRecovery(string key)
    {
        var example = RatingCurveExampleFixtures.LoadCase(key);
        BestFitRatingCurve model = RatingCurveExampleFixtures.CreateModel(example);

        Assert.AreEqual(
            example.IndependentMleDischargeSpaceLogLikelihood,
            model.DataLogLikelihood(example.IndependentMle),
            SamePointLikelihoodTolerance,
            $"{key}: data log likelihood evaluated at the independent optimum.");

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        Assert.IsTrue(mle.IsEstimated, $"{key}: MLE estimation did not complete.");
        double[] estimated = mle.BestParameterSet.Values;
        double maximum = mle.MaximumLogLikelihood;
        Assert.AreEqual(
            maximum,
            model.DataLogLikelihood(estimated),
            1e-8,
            $"{key}: MaximumLogLikelihood must equal the data log likelihood at the estimate.");
        Assert.IsTrue(
            double.IsFinite(maximum) && maximum >= example.IndependentMleDischargeSpaceLogLikelihood - MleOptimalityTolerance,
            $"{key}: production MLE log likelihood {maximum:G17} must reach the independent optimum "
            + $"{example.IndependentMleDischargeSpaceLogLikelihood:G17} within {MleOptimalityTolerance}.");

        double relativeTolerance = example.Segments == 3 ? ThreeSegmentMleRelativeTolerance : MleRelativeTolerance;
        AssertParameters(key, "MLE", example.IndependentMle, estimated, example.ParameterNames, relativeTolerance);
        AssertWithinBounds(key, model, estimated);
        AssertPointCurve(key, "MLE", model, estimated, example);
    }

    /// <summary>
    /// Runs the default-setting Bayesian analysis on one example case and compares it with the
    /// independent optimum and the generating curve.
    /// </summary>
    /// <param name="key">The example case key.</param>
    /// <returns>A task representing the asynchronous recovery run.</returns>
    private static async Task AssertBayesianRecovery(string key)
    {
        var example = RatingCurveExampleFixtures.LoadCase(key);
        BestFitRatingCurve model = RatingCurveExampleFixtures.CreateModel(example);
        var analysis = new RatingCurveAnalysis(model);
        TimeSeriesIndependentRecoveryTests.AssertResolvedBayesianDefaults(key, analysis.BayesianAnalysis, model.NumberOfParameters);

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, $"{key}: Bayesian estimation did not complete.");
        TimeSeriesIndependentRecoveryTests.AssertResolvedBayesianDefaults(key, analysis.BayesianAnalysis, model.NumberOfParameters);
        MCMCResults results = analysis.BayesianAnalysis.Results!;
        Assert.AreEqual(analysis.BayesianAnalysis.OutputLength, results.Output.Count, $"{key}: retained output count.");

        for (int index = 0; index < model.NumberOfParameters; index++)
        {
            var statistics = results.ParameterResults[index].SummaryStatistics;
            Assert.IsTrue(
                double.IsFinite(statistics.Rhat) && statistics.Rhat < RhatLimit,
                $"{key}: {model.Parameters[index].Name} R-hat {statistics.Rhat:G6} must be below {RhatLimit}.");
            Assert.IsTrue(
                double.IsFinite(statistics.ESS) && statistics.ESS > EssMinimum,
                $"{key}: {model.Parameters[index].Name} ESS {statistics.ESS:G6} must exceed {EssMinimum}.");
        }

        double relativeTolerance = example.Segments == 3 ? ThreeSegmentMapRelativeTolerance : MapRelativeTolerance;
        AssertParameters(key, "sampled MAP", example.IndependentMle, results.MAP.Values, example.ParameterNames, relativeTolerance);
        AssertPosteriorCurve(key, model, analysis.BayesianAnalysis, results, example);
    }

    /// <summary>
    /// Asserts parameter agreement with the independent optimum under a relative tolerance with an
    /// absolute floor.
    /// </summary>
    /// <param name="key">The example case key.</param>
    /// <param name="label">The estimate label.</param>
    /// <param name="expected">The independent optimum.</param>
    /// <param name="actual">The BestFit estimate.</param>
    /// <param name="names">The parameter names.</param>
    /// <param name="relativeTolerance">The relative tolerance.</param>
    private static void AssertParameters(
        string key,
        string label,
        double[] expected,
        double[] actual,
        string[] names,
        double relativeTolerance)
    {
        Assert.AreEqual(expected.Length, actual.Length, $"{key}: {label} parameter count.");
        for (int index = 0; index < expected.Length; index++)
        {
            double tolerance = Math.Max(ParameterAbsoluteFloor, Math.Abs(expected[index]) * relativeTolerance);
            Assert.AreEqual(
                expected[index],
                actual[index],
                tolerance,
                $"{key}: {label} {names[index]} versus the independent optimum.");
        }
    }

    /// <summary>
    /// Asserts that an estimate lies inside the model's own parameter bounds.
    /// </summary>
    /// <param name="key">The example case key.</param>
    /// <param name="model">The rating-curve model.</param>
    /// <param name="estimated">The estimate.</param>
    private static void AssertWithinBounds(string key, BestFitRatingCurve model, double[] estimated)
    {
        for (int index = 0; index < model.NumberOfParameters; index++)
        {
            var parameter = model.Parameters[index];
            Assert.IsTrue(
                estimated[index] >= parameter.LowerBound - 1e-12 && estimated[index] <= parameter.UpperBound + 1e-12,
                $"{key}: {parameter.Name} estimate {estimated[index]:G17} must lie within [{parameter.LowerBound:G17}, {parameter.UpperBound:G17}].");
        }
    }

    /// <summary>
    /// Asserts that a point-estimate curve lies within the relative band of the true curve on the
    /// calibration grid.
    /// </summary>
    /// <param name="key">The example case key.</param>
    /// <param name="label">The estimate label.</param>
    /// <param name="model">The rating-curve model.</param>
    /// <param name="parameters">The point estimate.</param>
    /// <param name="example">The example case.</param>
    private static void AssertPointCurve(
        string key,
        string label,
        BestFitRatingCurve model,
        double[] parameters,
        RatingCurveExampleFixtures.ExampleCase example)
    {
        for (int index = 0; index < example.TrueCurveStages.Length; index++)
        {
            double stage = example.TrueCurveStages[index];
            if (stage < CurveGridMinimumStage)
            {
                continue;
            }

            double truth = example.TrueCurveDischarge[index];
            Assert.AreEqual(
                truth,
                model.Predict(parameters, stage),
                CurveRelativeBand * truth,
                $"{key}: {label} discharge at stage {stage} versus the true curve.");
        }
    }

    /// <summary>
    /// Asserts that the median posterior curve lies within the relative band of the true curve and
    /// that the true curve lies inside the posterior credible band at the declared fraction of grid
    /// stages.
    /// </summary>
    /// <param name="key">The example case key.</param>
    /// <param name="model">The rating-curve model.</param>
    /// <param name="bayesian">The Bayesian analysis that produced the results.</param>
    /// <param name="results">The retained posterior results.</param>
    /// <param name="example">The example case.</param>
    private static void AssertPosteriorCurve(
        string key,
        BestFitRatingCurve model,
        BayesianAnalysis bayesian,
        MCMCResults results,
        RatingCurveExampleFixtures.ExampleCase example)
    {
        int draws = results.Output.Count;
        double alpha = (1.0 - bayesian.CredibleIntervalWidth) / 2.0;
        int lowerIndex = (int)Math.Floor(alpha * (draws - 1));
        int upperIndex = (int)Math.Ceiling((1.0 - alpha) * (draws - 1));
        var values = new double[draws];
        int covered = 0;
        int total = 0;

        for (int index = 0; index < example.TrueCurveStages.Length; index++)
        {
            double stage = example.TrueCurveStages[index];
            if (stage < CurveGridMinimumStage)
            {
                continue;
            }

            for (int draw = 0; draw < draws; draw++)
            {
                values[draw] = model.Predict(results.Output[draw].Values, stage);
            }

            Array.Sort(values);
            double median = draws % 2 == 1 ? values[draws / 2] : 0.5 * (values[draws / 2 - 1] + values[draws / 2]);
            double truth = example.TrueCurveDischarge[index];
            Assert.AreEqual(
                truth,
                median,
                CurveRelativeBand * truth,
                $"{key}: median posterior discharge at stage {stage} versus the true curve.");
            total++;
            if (values[lowerIndex] <= truth && truth <= values[upperIndex])
            {
                covered++;
            }
        }

        Assert.IsTrue(
            covered >= CurveCoverageFraction * total,
            $"{key}: the true curve lies inside the {bayesian.CredibleIntervalWidth:P0} posterior band at {covered} of {total} grid stages; "
            + $"at least {CurveCoverageFraction:P0} is required.");
    }
}
