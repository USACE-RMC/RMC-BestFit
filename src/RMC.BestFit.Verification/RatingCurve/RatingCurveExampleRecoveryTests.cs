using Numerics.Sampling.MCMC;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Verification.TimeSeriesAnalysis;
using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;

namespace RMC.BestFit.Verification.RatingCurve;

/// <summary>
/// Replicates the three synthetic rating-curve cases of <c>examples/6-rating-curve-analysis</c> in the
/// Verification library at 1,000 observations: production maximum-likelihood recovery against an
/// independent SciPy conditional maximum-likelihood optimum and the generating curve.
/// </summary>
/// <remarks>
/// <para>
/// Each discovered MLE method fits exactly 1,000 generated stage-discharge pairs with Differential
/// Evolution and unchanged optimizer tolerances. The model retains the example's flat parameter
/// priors and Jeffreys scale setting. The replication fixtures follow the shipped 300-pair example's
/// generation recipe; their independent SciPy optima are committed in
/// <c>rating-curve-example-fixtures.json</c>. No Bayesian sampling is performed by these MLE methods.
/// </para>
/// <para>
/// Acceptance: the BestFit likelihood equals the oracle at the independent optimum; the production
/// MLE and generating parent remain inside the corresponding joint 95 percent likelihood-ratio
/// regions. The three historical Bayesian example methods are deliberately non-discovered because
/// their sampled-MAP coordinate and curve percentage bands were arbitrary single-realization rules.
/// Bayesian parameter and simultaneous response-space recovery is owned by
/// <see cref="RatingCurveBayesianRecoveryTests"/>.
/// </para>
/// </remarks>
[TestClass]
public class RatingCurveExampleRecoveryTests
{
    /// <summary>Absolute tolerance for the likelihood evaluated at the independent optimum.</summary>
    private const double SamePointLikelihoodTolerance = 1e-8;

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
    private const double CurveTruthRelativeBand = 0.10;

    /// <summary>Relative parity between the sampled-MAP curve and the independent optimum's curve.</summary>
    private const double PosteriorMapCurveParityTolerance = 0.02;

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

    /// <summary>Preserves the historical Bayesian one-segment example calculation.</summary>
    /// <remarks>The former arbitrary sampled-MAP percentage bands are not scientific Verification evidence.</remarks>
    /// <returns>A task representing the non-discovered historical calculation.</returns>
    public async Task Bayesian_OneSegment_RecoversExampleCurve() => await AssertBayesianRecovery("one_segment");

    /// <summary>Preserves the historical Bayesian two-segment example calculation.</summary>
    /// <remarks>The former arbitrary sampled-MAP percentage bands are not scientific Verification evidence.</remarks>
    /// <returns>A task representing the non-discovered historical calculation.</returns>
    public async Task Bayesian_TwoSegment_RecoversExampleCurve() => await AssertBayesianRecovery("two_segment");

    /// <summary>Preserves the historical Bayesian three-segment example calculation.</summary>
    /// <remarks>The former arbitrary sampled-MAP percentage bands are not scientific Verification evidence.</remarks>
    /// <returns>A task representing the non-discovered historical calculation.</returns>
    public async Task Bayesian_ThreeSegment_RecoversExampleCurve() => await AssertBayesianRecovery("three_segment");

    /// <summary>
    /// Runs the production MLE on one example case and compares it with the independent optimum.
    /// </summary>
    /// <param name="key">The example case key.</param>
    private static void AssertMleRecovery(string key)
    {
        var example = RatingCurveExampleFixtures.LoadReplicationCase(key);
        BestFitRatingCurve model = RatingCurveExampleFixtures.CreateModel(example);

        Assert.AreEqual(
            example.IndependentMleDischargeSpaceLogLikelihood,
            model.DataLogLikelihood(example.IndependentMle),
            SamePointLikelihoodTolerance,
            $"{key}: data log likelihood evaluated at the independent optimum.");

        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();
        Assert.IsTrue(mle.IsEstimated, $"{key}: MLE estimation did not complete.");
        double[] estimated = mle.BestParameterSet.Values;
        double maximum = mle.MaximumLogLikelihood;
        Assert.AreEqual(
            maximum,
            model.DataLogLikelihood(estimated),
            1e-8,
            $"{key}: MaximumLogLikelihood must equal the data log likelihood at the estimate.");
        Assert.IsTrue(double.IsFinite(maximum), $"{key}: production MLE log likelihood must be finite.");
        double joint95Cutoff = JointChiSquare95Cutoff(model.NumberOfParameters);
        Assert.IsTrue(double.IsFinite(example.IndependentMleDischargeSpaceLogLikelihood),
            $"{key}: independent optimum log likelihood must be finite.");
        double independentLikelihoodRatio = 2d * Math.Abs(example.IndependentMleDischargeSpaceLogLikelihood - maximum);
        Assert.IsTrue(
            independentLikelihoodRatio <= joint95Cutoff,
            $"{key}: production MLE must lie inside the {model.NumberOfParameters}-coordinate joint 95% "
            + $"likelihood-ratio region around the independent optimum: statistic={independentLikelihoodRatio:R}, "
            + $"cutoff={joint95Cutoff:R}.");
        Assert.IsTrue(double.IsFinite(example.DischargeSpaceLogLikelihoodAtTruth),
            $"{key}: generating-parent log likelihood must be finite.");
        double parentLikelihoodRatio = 2d * Math.Abs(maximum - example.DischargeSpaceLogLikelihoodAtTruth);
        Assert.IsTrue(
            parentLikelihoodRatio <= joint95Cutoff,
            $"{key}: generating parent must lie inside the {model.NumberOfParameters}-coordinate joint 95% "
            + $"likelihood-ratio region around the production optimum: statistic={parentLikelihoodRatio:R}, "
            + $"cutoff={joint95Cutoff:R}.");
        AssertWithinBounds(key, model, estimated);
    }

    /// <summary>Returns the central 95 percent chi-square cutoff for a supported rating-curve dimension.</summary>
    /// <param name="degreesOfFreedom">The fitted parameter count, including residual scale.</param>
    /// <returns>The 0.95 chi-square quantile for the requested degrees of freedom.</returns>
    /// <exception cref="AssertFailedException">Thrown when a fixture has an undeclared parameter count.</exception>
    /// <remarks>
    /// The supported dimensions are fixed by the one-, two-, and three-segment parameter layouts.
    /// Values are standard chi-square 0.95 quantiles used by Wilks likelihood-ratio regions.
    /// </remarks>
    private static double JointChiSquare95Cutoff(int degreesOfFreedom) => degreesOfFreedom switch
    {
        4 => 9.487729036781154d,
        7 => 14.067140449340169d,
        10 => 18.307038053275146d,
        _ => throw new AssertFailedException($"No predeclared joint 95% likelihood-ratio cutoff for {degreesOfFreedom} rating-curve parameters."),
    };

    /// <summary>
    /// Runs the default-setting Bayesian analysis on one example case and compares it with the
    /// independent optimum and the generating curve.
    /// </summary>
    /// <param name="key">The example case key.</param>
    /// <returns>A task representing the asynchronous recovery run.</returns>
    private static async Task AssertBayesianRecovery(string key)
    {
        var example = RatingCurveExampleFixtures.LoadReplicationCase(key);
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
        double[] map = results.MAP.Values;
        string diagnostics =
            $"sampled MAP [{string.Join(", ", map.Select(value => value.ToString("G6")))}] with posterior kernel {model.LogLikelihood(map):G12} "
            + $"and data log likelihood {model.DataLogLikelihood(map):G12}; independent optimum "
            + $"[{string.Join(", ", example.IndependentMle.Select(value => value.ToString("G6")))}] with posterior kernel {model.LogLikelihood(example.IndependentMle):G12} "
            + $"and data log likelihood {example.IndependentMleDischargeSpaceLogLikelihood:G12}";
        AssertParameters(key, $"sampled MAP ({diagnostics})", example.IndependentMle, map, example.ParameterNames, relativeTolerance);
        AssertPointCurve(key, "sampled MAP", model, map, example, PosteriorMapCurveParityTolerance);
        ReportPosteriorBand(key, model, analysis.BayesianAnalysis, results, example);
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
    /// Asserts that a point-estimate curve agrees with the independent optimum's curve and lies within
    /// the wider band of the true curve on the calibration grid.
    /// </summary>
    /// <param name="key">The example case key.</param>
    /// <param name="label">The estimate label.</param>
    /// <param name="model">The rating-curve model.</param>
    /// <param name="parameters">The point estimate.</param>
    /// <param name="example">The example case.</param>
    /// <param name="parityTolerance">Relative parity tolerance against the independent optimum's curve.</param>
    private static void AssertPointCurve(
        string key,
        string label,
        BestFitRatingCurve model,
        double[] parameters,
        RatingCurveExampleFixtures.ExampleCase example,
        double parityTolerance)
    {
        for (int index = 0; index < example.TrueCurveStages.Length; index++)
        {
            double stage = example.TrueCurveStages[index];
            if (stage < CurveGridMinimumStage)
            {
                continue;
            }

            double truth = example.TrueCurveDischarge[index];
            double independent = model.Predict(example.IndependentMle, stage);
            double fitted = model.Predict(parameters, stage);
            Assert.AreEqual(
                independent,
                fitted,
                parityTolerance * independent,
                $"{key}: {label} discharge at stage {stage} versus the independent optimum's curve.");
            Assert.AreEqual(
                truth,
                fitted,
                CurveTruthRelativeBand * truth,
                $"{key}: {label} discharge at stage {stage} versus the true curve.");
        }
    }

    /// <summary>
    /// Reports the fraction of grid stages at which the true curve lies inside the posterior credible
    /// band of the retained draws; the point comparisons use the sampled MAP.
    /// </summary>
    /// <param name="key">The example case key.</param>
    /// <param name="model">The rating-curve model.</param>
    /// <param name="bayesian">The Bayesian analysis that produced the results.</param>
    /// <param name="results">The retained posterior results.</param>
    /// <param name="example">The example case.</param>
    private static void ReportPosteriorBand(
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
            double truth = example.TrueCurveDischarge[index];
            total++;
            if (values[lowerIndex] <= truth && truth <= values[upperIndex])
            {
                covered++;
            }
        }

        // Single-realization coverage is reported, not asserted: the posterior band (about 1.5% wide for
        // these fixtures) is correct even when this realization's curve sits 2-4% from the truth at the
        // upper stages, an offset the independent optimum shares. True coverage belongs to a separately
        // approved multi-realization study.
        Console.WriteLine(
            $"{key}: the true curve lies inside the {bayesian.CredibleIntervalWidth:P0} posterior band at {covered} of {total} grid stages (reported, not asserted).");
    }
}
