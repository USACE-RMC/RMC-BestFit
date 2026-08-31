using Numerics;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Recovery;
using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;

namespace RMC.BestFit.Verification.RatingCurve;

/// <summary>
/// Implements the predeclared Chunk 12 rating-curve recovery design and acceptance rules.
/// </summary>
/// <remarks>
/// The helper is Verification-only. It preserves every retained fixture's generating parent,
/// seed, estimator, prior, sampler, and convergence configuration. Observed-information
/// parameter uncertainty is used for MLE, while retained posterior draws supply Bayesian parameter
/// uncertainty. Both paths add the declared Normal residual on the model's log10-discharge scale
/// before constructing simultaneous posterior-predictive response bands.
/// </remarks>
internal static class RatingCurveRecoveryAcceptance
{
    /// <summary>
    /// Number of bounded multivariate-Normal draws used to propagate MLE covariance through
    /// the nonlinear rating-curve response.
    /// </summary>
    private const int FrequentistCurveDrawCount = 20_000;

    /// <summary>
    /// Maximum number of deterministic proposals examined to obtain the bounded MLE draws.
    /// </summary>
    private const int MaximumFrequentistCurveProposals = 200_000;

    /// <summary>
    /// Verification-only seed for nonlinear MLE uncertainty propagation; it does not affect
    /// fixture generation or production estimation.
    /// </summary>
    private const int FrequentistCurveSeed = 20_260_831;

    /// <summary>
    /// Verification-only seed for independent log10-scale residuals in MLE predictive draws.
    /// </summary>
    private const int FrequentistResidualSeed = 20_260_901;

    /// <summary>
    /// Verification-only seed for independent log10-scale residuals in Bayesian predictive draws.
    /// </summary>
    private const int BayesianResidualSeed = 20_260_902;

    /// <summary>Posterior or asymptotic curve mass enclosed by the simultaneous band.</summary>
    private const double SimultaneousCurveCoverage = 0.95d;

    /// <summary>
    /// Describes one retained generated-parent rating-curve experiment.
    /// </summary>
    /// <param name="Name">Stable fixture label used in assertion diagnostics.</param>
    /// <param name="StageData">Exactly 1,000 generated stage observations.</param>
    /// <param name="DischargeData">Exactly 1,000 generated discharge observations.</param>
    /// <param name="TrueParameters">Generating physical coordinates in model order.</param>
    /// <param name="Segments">Number of additive controls.</param>
    /// <param name="ResponseStages">Predeclared stages at which curve response is identified.</param>
    internal sealed record Fixture(
        string Name,
        TimeSeries StageData,
        TimeSeries DischargeData,
        double[] TrueParameters,
        int Segments,
        double[] ResponseStages);

    /// <summary>Creates the standard single-control fixture.</summary>
    /// <returns>The N=1000 seed-12345 fixture and its response grid.</returns>
    internal static Fixture SingleSegmentDefault()
    {
        var sample = SyntheticRatingCurveData.GetSingleSegmentData(sampleSize: RecoveryDesign.SampleSize);
        return Create("SingleSegment_Default", sample, 1, [1.25d, 3d, 6d, 9.5d]);
    }

    /// <summary>Creates the low-noise single-control fixture.</summary>
    /// <returns>The N=1000 seed-54321 fixture and its response grid.</returns>
    internal static Fixture SingleSegmentLowNoise()
    {
        var sample = SyntheticRatingCurveData.GetLowNoiseData(sampleSize: RecoveryDesign.SampleSize);
        return Create("SingleSegment_LowNoise", sample, 1, [1.25d, 3d, 7d, 11.5d]);
    }

    /// <summary>Creates the wide-range single-control fixture.</summary>
    /// <returns>The N=1000 seed-99999 fixture and its response grid.</returns>
    internal static Fixture SingleSegmentWideRange()
    {
        var sample = SyntheticRatingCurveData.GetWideRangeData(sampleSize: RecoveryDesign.SampleSize);
        return Create("SingleSegment_WideRange", sample, 1, [1.25d, 5d, 15d, 24.5d]);
    }

    /// <summary>Creates the two-control bankfull-transition fixture.</summary>
    /// <returns>The N=1000 seed-44444 fixture and its response grid.</returns>
    internal static Fixture TwoSegmentBankfullTransition()
    {
        var sample = SyntheticRatingCurveData.GetBankfullTransitionData(sampleSize: RecoveryDesign.SampleSize);
        return Create("TwoSegment_BankfullTransition", sample, 2, [0.75d, 2d, 5.5d, 6.5d, 9d, 11.5d]);
    }

    /// <summary>Creates the three-control multiple-activation fixture.</summary>
    /// <returns>The N=1000 seed-66666 fixture and its response grid.</returns>
    internal static Fixture ThreeSegmentMultipleControl()
    {
        var sample = SyntheticRatingCurveData.GetMultipleControlData(sampleSize: RecoveryDesign.SampleSize);
        return Create(
            "ThreeSegment_MultipleControl",
            sample,
            3,
            [0.75d, 2d, 2.75d, 3.25d, 5.5d, 6.75d, 7.25d, 8.5d, 9.75d]);
    }

    /// <summary>
    /// Runs the unchanged rating-curve MLE and applies observed-information recovery rules.
    /// </summary>
    /// <param name="fixture">Predeclared retained fixture.</param>
    internal static void RunMaximumLikelihood(Fixture fixture)
    {
        ReportStageAllocation(fixture);
        BestFitRatingCurve model = CreateModel(fixture);
        var estimator = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        estimator.Estimate();

        Assert.IsTrue(estimator.IsEstimated, $"{fixture.Name}: MLE fitting failed.");
        Assert.IsTrue(
            estimator.TryGetCovarianceMatrix(out Matrix covariance),
            estimator.CovarianceDiagnostic ?? $"{fixture.Name}: observed-information covariance is unavailable.");
        Assert.AreEqual(
            CovarianceComputationStatus.Available,
            estimator.CovarianceStatus,
            $"{fixture.Name}: recovery requires an unregularized observed-information covariance.");

        double[] estimate = estimator.BestParameterSet.Values;
        Console.WriteLine(
            $"{fixture.Name}: MLE=[{string.Join(", ", estimate.Select(value => value.ToString("G17")))}], " +
            $"parent=[{string.Join(", ", fixture.TrueParameters.Select(value => value.ToString("G17")))}], " +
            $"MLE data log likelihood={model.DataLogLikelihood(estimate):G17}, " +
            $"parent data log likelihood={model.DataLogLikelihood(fixture.TrueParameters):G17}.");
        if (fixture.Segments == 1)
        {
            for (int index = 0; index < estimate.Length; index++)
            {
                RecoveryAcceptance.AssertFrequentistStandardizedError(
                    $"{fixture.Name} {model.Parameters[index].Name}",
                    estimate[index],
                    fixture.TrueParameters[index],
                    Math.Sqrt(covariance[index, index]));
            }
        }
        else
        {
            int sigmaIndex = estimate.Length - 1;
            RecoveryAcceptance.AssertFrequentistStandardizedError(
                $"{fixture.Name} {model.Parameters[sigmaIndex].Name}",
                estimate[sigmaIndex],
                fixture.TrueParameters[sigmaIndex],
                Math.Sqrt(covariance[sigmaIndex, sigmaIndex]));
        }

        AssertFrequentistCurveRecovery(fixture, model, estimate, covariance);
    }

    /// <summary>
    /// Runs the rating-curve Bayesian analysis with the MAP point estimator and applies posterior recovery rules.
    /// </summary>
    /// <param name="fixture">Predeclared retained fixture.</param>
    /// <returns>A task representing the production Bayesian run.</returns>
    internal static async Task RunBayesianAsync(Fixture fixture)
    {
        ReportStageAllocation(fixture);
        BestFitRatingCurve model = CreateModel(fixture);
        AssertParentInsidePriorSupport(fixture, model);
        var analysis = new RatingCurveAnalysis(model);
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95d;
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, $"{fixture.Name}: Bayesian estimation failed.");
        Assert.AreEqual(
            BayesianAnalysis.PointEstimateType.PosteriorMode,
            analysis.BayesianAnalysis.PointEstimator,
            $"{fixture.Name}: Bayesian recovery must publish the MAP point estimate.");
        Assert.IsNotNull(analysis.BayesianAnalysis.Results, $"{fixture.Name}: posterior results are unavailable.");
        MCMCResults results = analysis.BayesianAnalysis.Results!;
        Assert.AreEqual(model.NumberOfParameters, results.ParameterResults.Length,
            $"{fixture.Name}: posterior coordinate order must match the generating vector.");

        for (int index = 0; index < model.NumberOfParameters; index++)
        {
            var summary = results.ParameterResults[index].SummaryStatistics;
            bool identifiedCoordinate = fixture.Segments == 1 || index == model.NumberOfParameters - 1;
            if (identifiedCoordinate)
            {
                RecoveryAcceptance.AssertBayesianRecovery(
                    $"{fixture.Name} {model.Parameters[index].Name}",
                    fixture.TrueParameters[index],
                    summary.LowerCI,
                    summary.UpperCI,
                    summary.Rhat,
                    summary.ESS);
            }
            else
            {
                Assert.IsTrue(double.IsFinite(summary.Rhat) && summary.Rhat < RecoveryAcceptance.MaximumRhat,
                    $"{fixture.Name} {model.Parameters[index].Name} R-hat {summary.Rhat:G17} must be below {RecoveryAcceptance.MaximumRhat:G17}.");
                Assert.IsTrue(double.IsFinite(summary.ESS) && summary.ESS >= RecoveryAcceptance.MinimumEffectiveSampleSize,
                    $"{fixture.Name} {model.Parameters[index].Name} ESS {summary.ESS:G17} must be at least {RecoveryAcceptance.MinimumEffectiveSampleSize:G17}.");
            }
        }

        AssertBayesianCurveRecovery(fixture, model, results);
    }

    /// <summary>
    /// Converts a generated tuple into the common fixture description and enforces N=1000.
    /// </summary>
    /// <param name="name">Fixture label.</param>
    /// <param name="sample">Generated time series and parent coordinates.</param>
    /// <param name="segments">Number of additive controls.</param>
    /// <param name="responseStages">Predeclared curve stages.</param>
    /// <returns>The validated fixture.</returns>
    private static Fixture Create(
        string name,
        (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) sample,
        int segments,
        double[] responseStages)
    {
        _ = RecoveryDesign.PairedObservations("One date-aligned stage-discharge pair.");
        Assert.AreEqual(RecoveryDesign.SampleSize, sample.StageData.Count,
            $"{name}: stage series must contain exactly 1,000 observations.");
        Assert.AreEqual(RecoveryDesign.SampleSize, sample.DischargeData.Count,
            $"{name}: discharge series must contain exactly 1,000 observations.");
        return new Fixture(name, sample.StageData, sample.DischargeData, sample.TrueParameters, segments, responseStages);
    }

    /// <summary>
    /// Builds the fitted model while preserving the legacy recovery prior flags.
    /// </summary>
    /// <param name="fixture">Retained generated-parent fixture.</param>
    /// <returns>A rating-curve model with the production data-derived parameter bounds.</returns>
    private static BestFitRatingCurve CreateModel(Fixture fixture)
    {
        var model = new BestFitRatingCurve(fixture.StageData, fixture.DischargeData, fixture.Segments)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };
        Assert.AreEqual(fixture.TrueParameters.Length, model.NumberOfParameters,
            $"{fixture.Name}: generating and fitted coordinate counts differ.");
        return model;
    }

    /// <summary>
    /// Requires every generating Bayesian coordinate to have finite prior density.
    /// </summary>
    /// <param name="fixture">Retained generated-parent fixture.</param>
    /// <param name="model">Fitted model with resolved priors.</param>
    private static void AssertParentInsidePriorSupport(Fixture fixture, BestFitRatingCurve model)
    {
        for (int index = 0; index < model.NumberOfParameters; index++)
        {
            double logDensity = model.Parameters[index].PriorDistribution.LogPDF(fixture.TrueParameters[index]);
            Assert.IsTrue(double.IsFinite(logDensity),
                $"{fixture.Name}: parent {model.Parameters[index].Name}={fixture.TrueParameters[index]:G17} is outside prior support.");
        }
    }

    /// <summary>
    /// Requires the generating response to lie inside a simultaneous 95% nonlinear MLE predictive band.
    /// </summary>
    /// <param name="fixture">Retained fixture and response stages.</param>
    /// <param name="model">Fitted rating-curve model.</param>
    /// <param name="estimate">MLE parameter vector.</param>
    /// <param name="covariance">Unregularized observed-information covariance.</param>
    private static void AssertFrequentistCurveRecovery(
        Fixture fixture,
        BestFitRatingCurve model,
        double[] estimate,
        Matrix covariance)
    {
        var asymptoticDistribution = new MultivariateNormal(estimate, covariance.ToArray());
        double[,] proposals = asymptoticDistribution.GenerateRandomValues(
            MaximumFrequentistCurveProposals,
            FrequentistCurveSeed);
        var logResponses = new double[FrequentistCurveDrawCount, fixture.ResponseStages.Length];
        var residualGenerator = new MersenneTwister(FrequentistResidualSeed);
        var standardNormal = new Normal(0d, 1d);
        int accepted = 0;
        int proposalsExamined = 0;

        for (int proposal = 0;
             proposal < MaximumFrequentistCurveProposals && accepted < FrequentistCurveDrawCount;
             proposal++)
        {
            proposalsExamined = proposal + 1;
            double[] parameters = GetRow(proposals, proposal);
            if (!TryGetLog10Responses(fixture, model, parameters, out double[] responses))
            {
                continue;
            }

            AddPredictiveResiduals(
                responses,
                parameters[^1],
                standardNormal,
                residualGenerator);
            SetRow(logResponses, accepted, responses);
            accepted++;
        }

        Assert.AreEqual(
            FrequentistCurveDrawCount,
            accepted,
            $"{fixture.Name}: only {accepted:N0} physically admissible nonlinear MLE draws were obtained " +
            $"from {MaximumFrequentistCurveProposals:N0} deterministic proposals.");
        Console.WriteLine(
            $"{fixture.Name}: nonlinear MLE curve propagation retained {accepted:N0} of " +
            $"{proposalsExamined:N0} examined covariance draws (parameter seed {FrequentistCurveSeed}; " +
            $"independent log10-residual seed {FrequentistResidualSeed}).");

        AssertSimultaneousCurveRecovery(
            fixture,
            model,
            estimate,
            logResponses,
            "bounded observed-information plus log10-residual predictive");
    }

    /// <summary>
    /// Requires the generating response to lie inside a MAP-centered simultaneous 95% posterior-predictive band.
    /// </summary>
    /// <param name="fixture">Retained fixture and response stages.</param>
    /// <param name="model">Fitted rating-curve model.</param>
    /// <param name="results">Retained posterior draws.</param>
    private static void AssertBayesianCurveRecovery(Fixture fixture, BestFitRatingCurve model, MCMCResults results)
    {
        int drawCount = results.Output.Count;
        Assert.IsTrue(drawCount >= RecoveryAcceptance.MinimumEffectiveSampleSize,
            $"{fixture.Name}: at least {RecoveryAcceptance.MinimumEffectiveSampleSize} retained draws are required for curve bands.");
        var logResponses = new double[drawCount, fixture.ResponseStages.Length];
        var residualGenerator = new MersenneTwister(BayesianResidualSeed);
        var standardNormal = new Normal(0d, 1d);

        for (int draw = 0; draw < drawCount; draw++)
        {
            Assert.IsTrue(
                TryGetLog10Responses(
                    fixture,
                    model,
                    results.Output[draw].Values,
                out double[] responses),
                $"{fixture.Name}: retained posterior draw {draw + 1} does not define the predeclared rating-curve grid.");
            AddPredictiveResiduals(
                responses,
                results.Output[draw].Values[^1],
                standardNormal,
                residualGenerator);
            SetRow(logResponses, draw, responses);
        }

        Console.WriteLine(
            $"{fixture.Name}: posterior predictive curves add one independent log10 residual per draw and " +
            $"ordinate (seed {BayesianResidualSeed}); the posterior draws separately propagate parameter uncertainty.");

        AssertSimultaneousCurveRecovery(
            fixture,
            model,
            results.MAP.Values,
            logResponses,
            "MAP-centered posterior plus log10-residual predictive");
    }

    /// <summary>
    /// Reports the exact stage allocation that identifies each additive-control regime.
    /// </summary>
    /// <param name="fixture">Retained generated-parent fixture.</param>
    private static void ReportStageAllocation(Fixture fixture)
    {
        double[] activationStages = Enumerable.Range(1, fixture.Segments - 1)
            .Select(segment => fixture.TrueParameters[3 * segment])
            .ToArray();
        var exclusiveCounts = new int[fixture.Segments];
        for (int observation = 0; observation < fixture.StageData.Count; observation++)
        {
            double stage = fixture.StageData[observation].Value;
            int regime = 0;
            while (regime < activationStages.Length && stage >= activationStages[regime])
            {
                regime++;
            }

            exclusiveCounts[regime]++;
        }

        string exclusiveEvidence = string.Join(
            ", ",
            exclusiveCounts.Select((count, regime) => $"regime {regime + 1}={count}"));
        string activeEvidence = string.Join(
            ", ",
            Enumerable.Range(0, fixture.Segments).Select(control =>
                $"control {control + 1} active={exclusiveCounts.Skip(control).Sum()}"));
        Console.WriteLine(
            $"{fixture.Name}: exact generating-stage allocation (not an independently interchangeable effective N): " +
            $"{exclusiveEvidence}; {activeEvidence}.");
    }

    /// <summary>
    /// Builds and evaluates a simultaneous predictive-response band on the log10-discharge scale.
    /// </summary>
    /// <param name="fixture">Retained fixture and predeclared response grid.</param>
    /// <param name="model">Fitted rating-curve model.</param>
    /// <param name="centerParameters">MLE or posterior MAP coordinates defining the center curve.</param>
    /// <param name="logResponses">Uncertainty draws transformed to log10 discharge.</param>
    /// <param name="source">Uncertainty-source label used in diagnostics.</param>
    private static void AssertSimultaneousCurveRecovery(
        Fixture fixture,
        BestFitRatingCurve model,
        double[] centerParameters,
        double[,] logResponses,
        string source)
    {
        Assert.IsTrue(
            TryGetLog10Responses(fixture, model, centerParameters, out double[] center),
            $"{fixture.Name}: the {source} center does not define the predeclared response grid.");
        int drawCount = logResponses.GetLength(0);
        int stageCount = logResponses.GetLength(1);
        Assert.AreEqual(fixture.ResponseStages.Length, stageCount,
            $"{fixture.Name}: simultaneous-band response dimension differs from the predeclared grid.");

        var standardDeviations = new double[stageCount];
        for (int stage = 0; stage < stageCount; stage++)
        {
            double mean = 0d;
            for (int draw = 0; draw < drawCount; draw++)
            {
                mean += logResponses[draw, stage];
            }

            mean /= drawCount;
            double sumSquares = 0d;
            for (int draw = 0; draw < drawCount; draw++)
            {
                double deviation = logResponses[draw, stage] - mean;
                sumSquares += deviation * deviation;
            }

            standardDeviations[stage] = Math.Sqrt(sumSquares / (drawCount - 1d));
            Assert.IsTrue(double.IsFinite(standardDeviations[stage]) && standardDeviations[stage] > 0d,
                $"{fixture.Name}: {source} curve scale at stage {fixture.ResponseStages[stage]:G17} " +
                $"must be finite and positive; observed {standardDeviations[stage]:G17}.");
        }

        var maximumStandardizedDeviations = new double[drawCount];
        for (int draw = 0; draw < drawCount; draw++)
        {
            double maximum = 0d;
            for (int stage = 0; stage < stageCount; stage++)
            {
                double standardized = Math.Abs(logResponses[draw, stage] - center[stage]) /
                    standardDeviations[stage];
                maximum = Math.Max(maximum, standardized);
            }

            maximumStandardizedDeviations[draw] = maximum;
        }

        Array.Sort(maximumStandardizedDeviations);
        int criticalIndex = (int)Math.Ceiling(SimultaneousCurveCoverage * drawCount) - 1;
        double criticalValue = maximumStandardizedDeviations[criticalIndex];
        Assert.IsTrue(double.IsFinite(criticalValue) && criticalValue > 0d,
            $"{fixture.Name}: {source} simultaneous critical value is invalid ({criticalValue:G17}).");
        Console.WriteLine(
            $"{fixture.Name}: {source} simultaneous {SimultaneousCurveCoverage:P0} log10-discharge " +
            $"band uses max-|t| critical {criticalValue:G8} from {drawCount:N0} curve draws.");

        for (int stage = 0; stage < stageCount; stage++)
        {
            double lower = Math.Pow(10d, center[stage] - criticalValue * standardDeviations[stage]);
            double upper = Math.Pow(10d, center[stage] + criticalValue * standardDeviations[stage]);
            double parent = model.Predict(fixture.TrueParameters, fixture.ResponseStages[stage]);
            Console.WriteLine(
                $"{fixture.Name}: stage={fixture.ResponseStages[stage]:G8}, " +
                $"center={Math.Pow(10d, center[stage]):G10}, parent={parent:G10}, " +
                $"simultaneous95=[{lower:G10}, {upper:G10}].");
            RecoveryAcceptance.AssertIdentifiedResponseGrid(
                $"{fixture.Name} simultaneous discharge at stage {fixture.ResponseStages[stage]:G17}",
                parent,
                lower,
                upper);
        }
    }

    /// <summary>
    /// Converts one physically admissible parameter vector to the predeclared log10-response grid.
    /// </summary>
    /// <param name="fixture">Retained fixture and response grid.</param>
    /// <param name="model">Rating-curve model defining parameter bounds and prediction.</param>
    /// <param name="parameters">Candidate coordinates.</param>
    /// <param name="responses">Log10 discharge at every predeclared stage when admissible.</param>
    /// <returns><see langword="true"/> when the coordinates and all responses are physically admissible.</returns>
    private static bool TryGetLog10Responses(
        Fixture fixture,
        BestFitRatingCurve model,
        double[] parameters,
        out double[] responses)
    {
        responses = new double[fixture.ResponseStages.Length];
        if (parameters.Length != model.NumberOfParameters)
        {
            return false;
        }

        for (int index = 0; index < parameters.Length; index++)
        {
            if (!double.IsFinite(parameters[index]) ||
                parameters[index] < model.Parameters[index].LowerBound ||
                parameters[index] > model.Parameters[index].UpperBound)
            {
                return false;
            }
        }

        if ((fixture.Segments >= 2 && parameters[0] >= parameters[3]) ||
            (fixture.Segments >= 3 && parameters[3] >= parameters[6]))
        {
            return false;
        }

        for (int stage = 0; stage < fixture.ResponseStages.Length; stage++)
        {
            double discharge = model.Predict(parameters, fixture.ResponseStages[stage]);
            if (!double.IsFinite(discharge) || discharge <= 0d)
            {
                return false;
            }

            responses[stage] = Math.Log10(discharge);
        }

        return true;
    }

    /// <summary>
    /// Adds independent Normal residuals on the model's log10-discharge observation scale.
    /// </summary>
    /// <param name="logResponses">Mean-curve log10 responses, updated in place.</param>
    /// <param name="sigma">Draw-specific log10 residual standard deviation.</param>
    /// <param name="standardNormal">Standard-Normal quantile implementation.</param>
    /// <param name="generator">Deterministic Verification-only residual generator.</param>
    private static void AddPredictiveResiduals(
        double[] logResponses,
        double sigma,
        Normal standardNormal,
        Random generator)
    {
        Assert.IsTrue(double.IsFinite(sigma) && sigma > 0d,
            $"Predictive log10 residual scale must be finite and positive; observed {sigma:G17}.");
        for (int stage = 0; stage < logResponses.Length; stage++)
        {
            logResponses[stage] += sigma * standardNormal.InverseCDF(generator.NextDouble());
        }
    }

    /// <summary>Copies one row of a rectangular array into a vector.</summary>
    /// <param name="values">Source rectangular array.</param>
    /// <param name="row">Zero-based source row.</param>
    /// <returns>A new vector containing the requested row.</returns>
    private static double[] GetRow(double[,] values, int row)
    {
        var result = new double[values.GetLength(1)];
        for (int column = 0; column < result.Length; column++)
        {
            result[column] = values[row, column];
        }

        return result;
    }

    /// <summary>Copies a vector into one row of a rectangular array.</summary>
    /// <param name="values">Destination rectangular array.</param>
    /// <param name="row">Zero-based destination row.</param>
    /// <param name="source">Vector whose length must equal the column count.</param>
    private static void SetRow(double[,] values, int row, double[] source)
    {
        Assert.AreEqual(values.GetLength(1), source.Length, "Row length differs from the destination width.");
        for (int column = 0; column < source.Length; column++)
        {
            values[row, column] = source[column];
        }
    }
}
