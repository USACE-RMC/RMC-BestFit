using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Recovery;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.Univariate.CompetingRiskTests;

/// <summary>
/// Provides the pinned fixtures and independent acceptance helpers for
/// <see cref="CompetingRiskRecoveryTests"/>.
/// </summary>
public partial class CompetingRiskRecoveryTests
{
    /// <summary>The deterministic synthetic-data seed inherited from the Numerics fixtures.</summary>
    private const int FixtureSeed = 12345;

    /// <summary>The absolute true-parameter data-likelihood parity tolerance.</summary>
    private const double LikelihoodTolerance = 1E-10;

    /// <summary>The predeclared generating-composite cumulative probabilities.</summary>
    private static readonly double[] ResponseProbabilities = [0.10d, 0.25d, 0.50d, 0.75d, 0.90d];

    /// <summary>
    /// Runs one fixture through the production maximum-likelihood estimator and verifies
    /// its parent likelihood, optimizer outcome, combined CDF, and parameter gates.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    private static void VerifyMaximumLikelihoodRecovery(RecoveryFixture fixture)
    {
        (_, CompetingRisksModel fittedModel) = PrepareRecovery(fixture);
        var maximumLikelihood = new MaximumLikelihood(fittedModel);

        Assert.AreEqual(
            OptimizationMethod.DifferentialEvolution,
            maximumLikelihood.OptimizerMethod,
            $"{fixture.Label}: MLE must use the production Differential Evolution default.");
        Assert.IsTrue(
            maximumLikelihood.Estimate(),
            $"{fixture.Label}: MLE failed with status {maximumLikelihood.Status}.");
        Assert.IsTrue(maximumLikelihood.IsEstimated, $"{fixture.Label}: MLE did not publish an estimate.");

        double[] fittedParameters = maximumLikelihood.BestParameterSet.Values;
        AssertFiniteParameters(fixture, fittedParameters, "MLE");
        Matrix covariance = ComputeObservedInformationCovariance(fixture, fittedModel, fittedParameters);
        (fittedParameters, covariance) = CanonicalizeCoordinates(fixture, fittedParameters, covariance);
        AssertMaximumLikelihoodCoordinateRecovery(fixture, fittedModel, fittedParameters, covariance);
        AssertMaximumLikelihoodResponseRecovery(fixture, fittedModel, fittedParameters, covariance);
    }

    /// <summary>
    /// Runs one fixture through <see cref="CompetingRiskAnalysis"/> while leaving the
    /// full DEMCzs simulation and output configuration at production defaults.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="useJeffreysRuleForScale">
    /// Whether the fitted competing-risk model applies its optional Jeffreys scale prior.
    /// </param>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    private static async Task VerifyBayesianRecoveryAsync(
        RecoveryFixture fixture,
        bool useJeffreysRuleForScale = true)
    {
        (_, CompetingRisksModel fittedModel) = PrepareRecovery(fixture);
        fittedModel.UseJeffreysRuleForScale = useJeffreysRuleForScale;
        var analysis = new CompetingRiskAnalysis(fittedModel);
        AssertDefaultDemczsConfiguration(fixture, analysis.BayesianAnalysis, fittedModel.NumberOfParameters);
        SetPredeclaredPostprocessingGrid(analysis);

        Exception? analysisError = null;
        analysis.AnalysisCompleted += (_, args) => analysisError = args.Error;
        var validation = analysis.Validate();
        Assert.IsTrue(
            validation.IsValid,
            $"{fixture.Label}: {string.Join(Environment.NewLine, validation.ValidationMessages)}");

        await analysis.RunAsync();

        Assert.IsTrue(
            analysis.IsEstimated,
            $"{fixture.Label}: Bayesian recovery failed. {analysisError ?? analysis.BayesianAnalysis.LastError}");
        Assert.IsNotNull(analysis.BayesianAnalysis.Results, $"{fixture.Label}: Bayesian results are null.");
        AssertDefaultDemczsConfiguration(fixture, analysis.BayesianAnalysis, fittedModel.NumberOfParameters);
        Assert.AreEqual(
            MCMCSampler.InitializationType.UserDefined,
            analysis.BayesianAnalysis.Sampler!.Initialize,
            $"{fixture.Label}: competing-risk analysis did not retain MAP-population initialization.");
        Assert.AreEqual(
            analysis.BayesianAnalysis.OutputLength,
            analysis.BayesianAnalysis.Results!.Output.Count,
            $"{fixture.Label}: DEMCzs did not retain the configured default output length.");

        CompetingRisks? fittedDistribution = analysis.GetPointEstimateDistribution() as CompetingRisks;
        Assert.IsNotNull(
            fittedDistribution,
            $"{fixture.Label}: the default posterior-mean competing-risk distribution is null.");
        double[] fittedParameters = fittedDistribution!.GetParameters;
        AssertFiniteParameters(fixture, fittedParameters, "Bayesian");
        AssertBayesianCoordinateRecovery(fixture, fittedModel, analysis);
        AssertBayesianResponseRecovery(fixture, analysis);
    }

    /// <summary>
    /// Generates one synthetic sample through BestFit, constructs a fresh fit model, and
    /// verifies exact-data likelihood parity at the true flattened Numerics parameters.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <returns>The generated sample and fresh model to estimate.</returns>
    private static (double[] Sample, CompetingRisksModel Model) PrepareRecovery(RecoveryFixture fixture)
    {
        var generatingModel = new CompetingRisksModel
        {
            CompetingRisks = (CompetingRisks)fixture.Parent.Clone()
        };
        double[] sample = generatingModel.GenerateRandomValues(fixture.SampleSize, FixtureSeed);
        AssertIdentifiableDesign(fixture, sample);
        var dataFrame = new BestFitDataFrame { ExactSeries = new ExactSeries(sample) };
        var fittedModel = new CompetingRisksModel(dataFrame, fixture.Parent);

        double[] trueParameters = fixture.Parent.GetParameters;
        double expectedLogLikelihood = sample.Sum(fixture.Parent.LogLikelihood);
        double actualLogLikelihood = fittedModel.DataLogLikelihood(trueParameters);
        Assert.AreEqual(
            expectedLogLikelihood,
            actualLogLikelihood,
            LikelihoodTolerance,
            $"{fixture.Label}: BestFit true-parameter DataLogLikelihood does not equal the " +
            "flattened Numerics parent likelihood.");

        AssertRecoveryCrosswalk(fixture, fittedModel, trueParameters, actualLogLikelihood);

        return (sample, fittedModel);
    }

    /// <summary>
    /// Verifies the current production DEMCzs defaults without assigning any simulation,
    /// advanced-sampler, output, point-estimator, or seed property.
    /// </summary>
    /// <param name="fixture">The recovery fixture used in assertion messages.</param>
    /// <param name="analysis">The newly constructed Bayesian analysis.</param>
    /// <param name="parameterCount">The fitted model parameter count.</param>
    private static void AssertDefaultDemczsConfiguration(
        RecoveryFixture fixture,
        BayesianAnalysis analysis,
        int parameterCount)
    {
        int expectedChains = Math.Max(4, Math.Min(20, 2 * parameterCount));
        int expectedThinning = Math.Max(1, Math.Min(100, 10 * parameterCount));
        int expectedInitialIterations = Math.Min(1000, Math.Max(100, parameterCount * 100));
        double expectedJump = 2.38d / Math.Sqrt(2d * parameterCount);

        Assert.AreEqual(BayesianAnalysis.SamplerType.DEMCzs, analysis.Type,
            $"{fixture.Label}: sampler default drifted from DEMCzs.");
        Assert.IsTrue(analysis.UseSimulationDefaults,
            $"{fixture.Label}: simulation defaults must remain enabled.");
        Assert.IsTrue(analysis.UseAdvancedSimulationDefaults,
            $"{fixture.Label}: advanced DEMCzs defaults must remain enabled.");
        Assert.AreEqual(expectedChains, analysis.NumberOfChains,
            $"{fixture.Label}: dimension-scaled chain default changed.");
        Assert.AreEqual(expectedThinning, analysis.ThinningInterval,
            $"{fixture.Label}: dimension-scaled thinning default changed.");
        Assert.AreEqual(3500, analysis.Iterations,
            $"{fixture.Label}: 90% Raftery-Lewis iteration default changed.");
        Assert.AreEqual(1750, analysis.WarmupIterations,
            $"{fixture.Label}: 50% warmup default changed.");
        Assert.AreEqual(expectedInitialIterations, analysis.InitialIterations,
            $"{fixture.Label}: dimension-scaled initialization default changed.");
        Assert.AreEqual(12345, analysis.PRNGSeed,
            $"{fixture.Label}: default DEMCzs seed changed.");
        Assert.AreEqual(10000, analysis.OutputLength,
            $"{fixture.Label}: retained-output default changed.");
        Assert.AreEqual(0.9d, analysis.CredibleIntervalWidth, 0d,
            $"{fixture.Label}: credible-interval default changed.");
        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, analysis.PointEstimator,
            $"{fixture.Label}: point-estimator default changed.");
        Assert.AreEqual(expectedJump, analysis.Jump, 1E-14,
            $"{fixture.Label}: DEMCzs jump default changed.");
        Assert.AreEqual(0.1d, analysis.JumpThreshold, 0d,
            $"{fixture.Label}: DEMCzs jump-threshold default changed.");
        Assert.AreEqual(0.1d, analysis.SnookerThreshold, 0d,
            $"{fixture.Label}: DEMCzs snooker-threshold default changed.");
        Assert.AreEqual(1E-12, analysis.Noise, 0d,
            $"{fixture.Label}: DEMCzs noise default changed.");
    }

    /// <summary>
    /// Replaces the default reporting ordinates with the five predeclared central response
    /// probabilities so posterior fitting is not conflated with extreme-tail inverse-CDF output.
    /// </summary>
    /// <param name="analysis">The newly constructed analysis.</param>
    private static void SetPredeclaredPostprocessingGrid(CompetingRiskAnalysis analysis)
    {
        analysis.ProbabilityOrdinates.Clear();
        for (int probabilityIndex = ResponseProbabilities.Length - 1; probabilityIndex >= 0; probabilityIndex--)
            analysis.ProbabilityOrdinates.Add(1d - ResponseProbabilities[probabilityIndex]);
    }

    /// <summary>
    /// Verifies the fixture-to-model crosswalk, prior support, and true-parent likelihood
    /// discrimination before either estimator is invoked.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="model">The fresh fitted model.</param>
    /// <param name="trueParameters">The flattened generating-parent coordinates.</param>
    /// <param name="trueLogLikelihood">The exact-data likelihood at the generating parent.</param>
    private static void AssertRecoveryCrosswalk(
        RecoveryFixture fixture,
        CompetingRisksModel model,
        double[] trueParameters,
        double trueLogLikelihood)
    {
        Assert.AreEqual(RecoveryDesign.SampleSize, fixture.SampleSize,
            $"{fixture.Label}: recovery must contain exactly 1,000 scalar observations.");
        Assert.AreEqual(RecoverySampleUnit.ScalarObservation, fixture.Design.Unit,
            $"{fixture.Label}: recovery unit must be a scalar composite observation.");

        CompetingRisks fitted = model.CompetingRisks!;
        Assert.AreEqual(fixture.Parent.MinimumOfRandomVariables, fitted.MinimumOfRandomVariables,
            $"{fixture.Label}: minimum/maximum convention changed in the fitted model.");
        Assert.AreEqual(fixture.Parent.Dependency, fitted.Dependency,
            $"{fixture.Label}: dependency convention changed in the fitted model.");
        Assert.AreEqual(fixture.Parent.Distributions.Count, fitted.Distributions.Count,
            $"{fixture.Label}: fitted component count changed.");

        for (int componentIndex = 0; componentIndex < fixture.Parent.Distributions.Count; componentIndex++)
        {
            UnivariateDistributionBase parentComponent = fixture.Parent.Distributions[componentIndex];
            UnivariateDistributionBase fittedComponent = fitted.Distributions[componentIndex];
            Assert.AreEqual(parentComponent.Type, fittedComponent.Type,
                $"{fixture.Label}: fitted component {componentIndex + 1} changed family.");
            if (parentComponent is LogNormal parentLogNormal && fittedComponent is LogNormal fittedLogNormal)
            {
                Assert.AreEqual(parentLogNormal.Base, fittedLogNormal.Base, 0d,
                    $"{fixture.Label}: fitted component {componentIndex + 1} changed logarithm base.");
            }
        }

        if (fixture.Parent.Dependency == Probability.DependencyType.CorrelationMatrix)
        {
            for (int row = 0; row < fixture.Parent.Distributions.Count; row++)
            {
                for (int column = 0; column < fixture.Parent.Distributions.Count; column++)
                {
                    Assert.AreEqual(
                        fixture.Parent.CorrelationMatrix[row, column],
                        fitted.CorrelationMatrix[row, column],
                        0d,
                        $"{fixture.Label}: fixed correlation changed at [{row},{column}].");
                }
            }
        }

        Assert.AreEqual(model.Parameters.Count, trueParameters.Length,
            $"{fixture.Label}: fitted coordinate order does not match the flattened parent.");
        for (int parameterIndex = 0; parameterIndex < trueParameters.Length; parameterIndex++)
        {
            ModelParameter parameter = model.Parameters[parameterIndex];
            double parent = trueParameters[parameterIndex];
            Assert.IsTrue(parameter.LowerBound <= parent && parent <= parameter.UpperBound,
                $"{fixture.Label}: parent {parameter.OwnerName} {parameter.Name}={parent:G17} " +
                $"is outside [{parameter.LowerBound:G17}, {parameter.UpperBound:G17}].");
            Assert.IsTrue(Tools.IsFinite(parameter.PriorDistribution.LogPDF(parent)),
                $"{fixture.Label}: parent {parameter.OwnerName} {parameter.Name} is outside its configured prior.");
        }

        model.ProcessQuantilePriors();
        Assert.IsTrue(Tools.IsFinite(model.PriorLogLikelihood(trueParameters)),
            $"{fixture.Label}: the complete configured prior rejects the generating parent.");

        double[] collapsedParameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
        Assert.IsTrue(
            collapsedParameters.Where((value, index) => value != trueParameters[index]).Any(),
            $"{fixture.Label}: the fresh model did not provide a distinct collapsed/default alternative.");
        double collapsedLogLikelihood = model.DataLogLikelihood(collapsedParameters);
        Assert.IsTrue(
            !double.IsNaN(collapsedLogLikelihood) && trueLogLikelihood > collapsedLogLikelihood,
            $"{fixture.Label}: parent likelihood {trueLogLikelihood:G17} did not beat the " +
            $"collapsed/default alternative {collapsedLogLikelihood:G17}.");
    }

    /// <summary>
    /// Computes the full-likelihood observed-information covariance and requires a full-rank,
    /// positive-definite local parameterization at the fitted optimum.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="model">The fitted BestFit model.</param>
    /// <param name="fittedParameters">The maximum-likelihood coordinates.</param>
    /// <returns>The inverse observed-information covariance.</returns>
    private static Matrix ComputeObservedInformationCovariance(
        RecoveryFixture fixture,
        CompetingRisksModel model,
        double[] fittedParameters)
    {
        Matrix rawInformation = NumericalDiff.ComputeHessian(
            model.DataLogLikelihood,
            fittedParameters,
            fittedParameters.Length,
            model.Parameters.Select(parameter => parameter.LowerBound).ToArray(),
            model.Parameters.Select(parameter => parameter.UpperBound).ToArray()) * -1d;
        var information = new Matrix(fittedParameters.Length, fittedParameters.Length);
        var scaledInformation = new Matrix(fittedParameters.Length, fittedParameters.Length);
        for (int row = 0; row < fittedParameters.Length; row++)
        {
            double rowScale = Math.Max(1d, Math.Abs(fittedParameters[row]));
            for (int column = 0; column < fittedParameters.Length; column++)
            {
                double symmetricValue = 0.5d * (rawInformation[row, column] + rawInformation[column, row]);
                information[row, column] = symmetricValue;
                double columnScale = Math.Max(1d, Math.Abs(fittedParameters[column]));
                scaledInformation[row, column] = symmetricValue * rowScale * columnScale;
            }
        }

        var scaledDecomposition = new SingularValueDecomposition(scaledInformation);
        Assert.AreEqual(
            fittedParameters.Length,
            scaledDecomposition.Rank(),
            $"{fixture.Label}: scale-normalized observed information is rank deficient; " +
            $"inverse condition={scaledDecomposition.InverseCondition:G6}.");
        Assert.IsTrue(
            Tools.IsFinite(scaledDecomposition.InverseCondition) &&
            scaledDecomposition.InverseCondition > 0d,
            $"{fixture.Label}: scale-normalized observed-information condition is unusable.");

        CholeskyDecomposition informationCholesky;
        try
        {
            informationCholesky = new CholeskyDecomposition(information);
        }
        catch (Exception exception)
        {
            Assert.Fail(
                $"{fixture.Label}: full-likelihood observed information is not positive definite: " +
                exception.Message);
            throw;
        }

        Assert.IsTrue(informationCholesky.IsPositiveDefinite,
            $"{fixture.Label}: full-likelihood observed information is not positive definite.");
        Matrix covariance = informationCholesky.InverseA();
        for (int parameterIndex = 0; parameterIndex < fittedParameters.Length; parameterIndex++)
        {
            Assert.IsTrue(
                Tools.IsFinite(covariance[parameterIndex, parameterIndex]) &&
                covariance[parameterIndex, parameterIndex] > 0d,
                $"{fixture.Label}: parameter {parameterIndex + 1} covariance diagonal " +
                $"{covariance[parameterIndex, parameterIndex]:G17} is invalid.");
        }
        return covariance;
    }

    /// <summary>
    /// Requires every deliberately identified MLE coordinate to recover its generating value
    /// within the central 95 percent observed-information interval.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="model">The fitted BestFit model.</param>
    /// <param name="fittedParameters">The maximum-likelihood coordinates.</param>
    /// <param name="covariance">The full-likelihood observed-information covariance.</param>
    private static void AssertMaximumLikelihoodCoordinateRecovery(
        RecoveryFixture fixture,
        CompetingRisksModel model,
        IReadOnlyList<double> fittedParameters,
        Matrix covariance)
    {
        double[] truth = fixture.Parent.GetParameters;
        Assert.AreEqual(truth.Length, fittedParameters.Count,
            $"{fixture.Label}: MLE truth and fitted coordinate counts differ.");
        var coordinateEvidence = new List<string>(truth.Length);
        var standardizedErrors = new double[truth.Length];
        for (int parameterIndex = 0; parameterIndex < truth.Length; parameterIndex++)
        {
            double standardError = Math.Sqrt(covariance[parameterIndex, parameterIndex]);
            standardizedErrors[parameterIndex] =
                Math.Abs(fittedParameters[parameterIndex] - truth[parameterIndex]) / standardError;
            coordinateEvidence.Add(
                $"{model.Parameters[parameterIndex].OwnerName} {model.Parameters[parameterIndex].Name}: " +
                $"fit={fittedParameters[parameterIndex]:G8}, truth={truth[parameterIndex]:G8}, " +
                $"SE={standardError:G8}, |z|={standardizedErrors[parameterIndex]:G6}");
        }
        Assert.IsTrue(
            standardizedErrors.All(value => value <=
                RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff),
            $"{fixture.Label}: one or more identified MLE coordinates missed the central 95% " +
            $"observed-information interval. fitted logL={model.DataLogLikelihood(fittedParameters.ToArray()):G12}, " +
            $"parent logL={model.DataLogLikelihood(truth):G12}. " +
            string.Join("; ", coordinateEvidence));
        for (int parameterIndex = 0; parameterIndex < truth.Length; parameterIndex++)
        {
            double standardError = Math.Sqrt(covariance[parameterIndex, parameterIndex]);
            string coordinate = $"{fixture.Label}: MLE {model.Parameters[parameterIndex].OwnerName} " +
                model.Parameters[parameterIndex].Name;
            RecoveryAcceptance.AssertFrequentistStandardizedError(
                coordinate,
                fittedParameters[parameterIndex],
                truth[parameterIndex],
                standardError);
        }
    }

    /// <summary>
    /// Requires every deliberately identified Bayesian coordinate to contain its generating
    /// value in the central 95 percent posterior interval with acceptable R-hat and ESS.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="model">The fitted BestFit model.</param>
    /// <param name="analysis">The completed competing-risk analysis.</param>
    private static void AssertBayesianCoordinateRecovery(
        RecoveryFixture fixture,
        CompetingRisksModel model,
        CompetingRiskAnalysis analysis)
    {
        Assert.AreEqual(
            Probability.DependencyType.Independent,
            fixture.Parent.Dependency,
            $"{fixture.Label}: Bayesian MCMC is authorized only for independent recovery fixtures.");
        IReadOnlyList<ParameterSet> output = analysis.BayesianAnalysis.Results!.Output;
        double[] truth = fixture.Parent.GetParameters;
        Assert.AreEqual(truth.Length, analysis.BayesianAnalysis.Results.ParameterResults.Length,
            $"{fixture.Label}: Bayesian truth and diagnostic coordinate counts differ.");

        ParameterSet[] orderedOutput = output
            .Select(parameterSet => CanonicalizeParameterSet(fixture, parameterSet))
            .ToArray();
        List<ParameterSet>[] orderedChains = analysis.BayesianAnalysis.Sampler!.MarkovChains
            .Select(chain => chain.Select(parameterSet =>
                CanonicalizeParameterSet(fixture, parameterSet)).ToList())
            .ToArray();
        List<ParameterSet>[] orderedRetainedOutput = analysis.BayesianAnalysis.Sampler.Output
            .Select(chain => chain.Select(parameterSet =>
                CanonicalizeParameterSet(fixture, parameterSet)).ToList())
            .ToArray();
        double[] rhats = MCMCDiagnostics.GelmanRubin(
            orderedChains,
            analysis.BayesianAnalysis.WarmupIterations);
        double[] effectiveSampleSizes = MCMCDiagnostics.EffectiveSampleSize(
            orderedRetainedOutput,
            out _);

        for (int parameterIndex = 0; parameterIndex < truth.Length; parameterIndex++)
        {
            double[] draws = orderedOutput
                .Select(parameterSet => parameterSet.Values[parameterIndex])
                .ToArray();
            Array.Sort(draws);
            double lower = Statistics.Percentile(draws, 0.025d, true);
            double upper = Statistics.Percentile(draws, 0.975d, true);
            string coordinate = $"{fixture.Label}: Bayesian {model.Parameters[parameterIndex].OwnerName} " +
                model.Parameters[parameterIndex].Name;
            RecoveryAcceptance.AssertBayesianRecovery(
                coordinate,
                truth[parameterIndex],
                lower,
                upper,
                rhats[parameterIndex],
                effectiveSampleSizes[parameterIndex]);
        }
    }

    /// <summary>
    /// Applies the predeclared increasing-Weibull-shape label rule to a point and covariance.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="parameters">The raw flattened component coordinates.</param>
    /// <param name="covariance">The raw coordinate covariance.</param>
    /// <returns>The canonically ordered coordinates and covariance.</returns>
    private static (double[] Parameters, Matrix Covariance) CanonicalizeCoordinates(
        RecoveryFixture fixture,
        double[] parameters,
        Matrix covariance)
    {
        int[] coordinateOrder = GetCanonicalCoordinateOrder(fixture, parameters);
        var orderedParameters = new double[parameters.Length];
        var orderedCovariance = new Matrix(parameters.Length, parameters.Length);
        for (int row = 0; row < parameters.Length; row++)
        {
            orderedParameters[row] = parameters[coordinateOrder[row]];
            for (int column = 0; column < parameters.Length; column++)
                orderedCovariance[row, column] = covariance[coordinateOrder[row], coordinateOrder[column]];
        }
        return (orderedParameters, orderedCovariance);
    }

    /// <summary>
    /// Applies the predeclared increasing-Weibull-shape label rule to one posterior state.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="parameterSet">The raw posterior state.</param>
    /// <returns>The canonically ordered posterior state.</returns>
    private static ParameterSet CanonicalizeParameterSet(
        RecoveryFixture fixture,
        ParameterSet parameterSet)
    {
        int[] coordinateOrder = GetCanonicalCoordinateOrder(fixture, parameterSet.Values);
        var orderedValues = new double[parameterSet.Values.Length];
        for (int coordinateIndex = 0; coordinateIndex < orderedValues.Length; coordinateIndex++)
            orderedValues[coordinateIndex] = parameterSet.Values[coordinateOrder[coordinateIndex]];
        return new ParameterSet(orderedValues, parameterSet.Fitness, parameterSet.Weight);
    }

    /// <summary>
    /// Gets the scientifically predeclared component-coordinate order for one flattened state.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="parameters">The raw flattened component coordinates.</param>
    /// <returns>For each canonical coordinate, the corresponding raw coordinate index.</returns>
    private static int[] GetCanonicalCoordinateOrder(
        RecoveryFixture fixture,
        IReadOnlyList<double> parameters)
    {
        bool allWeibull = fixture.Parent.Distributions.All(distribution => distribution is Weibull);
        if (!allWeibull || fixture.Parent.Distributions.Count < 2)
            return Enumerable.Range(0, parameters.Count).ToArray();

        const int WeibullParameterCount = 2;
        Assert.AreEqual(
            WeibullParameterCount * fixture.Parent.Distributions.Count,
            parameters.Count,
            $"{fixture.Label}: Weibull label ordering requires scale/shape coordinate pairs.");
        return Enumerable.Range(0, fixture.Parent.Distributions.Count)
            .OrderBy(componentIndex => parameters[WeibullParameterCount * componentIndex + 1])
            .SelectMany(componentIndex => new[]
            {
                WeibullParameterCount * componentIndex,
                WeibullParameterCount * componentIndex + 1
            })
            .ToArray();
    }

    /// <summary>
    /// Applies the observed-information covariance to the five identified composite CDF
    /// ordinates through an independently implemented finite-difference delta method.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="model">The estimated BestFit model, used only for parameter bounds.</param>
    /// <param name="fittedParameters">The maximum-likelihood coordinates.</param>
    /// <param name="covariance">The full-likelihood observed-information covariance.</param>
    private static void AssertMaximumLikelihoodResponseRecovery(
        RecoveryFixture fixture,
        CompetingRisksModel model,
        double[] fittedParameters,
        Matrix covariance)
    {
        foreach (double probability in ResponseProbabilities)
        {
            double location = fixture.Parent.InverseCDF(probability);
            double parentResponse = fixture.Parent.CDF(location);
            double estimate = EvaluateCompositeCdf(fixture, fittedParameters, location);
            double standardError = ComputeDeltaMethodStandardError(
                fixture,
                model,
                covariance,
                fittedParameters,
                location);
            double lower = Math.Max(0d, estimate -
                RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * standardError);
            double upper = Math.Min(1d, estimate +
                RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * standardError);
            string label = $"{fixture.Label}: MLE composite CDF at parent p={probability:G2}";

            Assert.IsTrue(Tools.IsFinite(location) && Tools.IsFinite(parentResponse),
                $"{label} did not produce a finite parent location and response.");
            RecoveryAcceptance.AssertIdentifiedResponseGrid(label, parentResponse, lower, upper);
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(
                label,
                estimate,
                parentResponse,
                lower,
                upper);
        }
    }

    /// <summary>
    /// Requires each identified generating composite CDF response to lie in its central
    /// posterior 95 percent band over the retained production DEMCzs output.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="analysis">The completed competing-risk analysis.</param>
    private static void AssertBayesianResponseRecovery(
        RecoveryFixture fixture,
        CompetingRiskAnalysis analysis)
    {
        IReadOnlyList<ParameterSet> output = analysis.BayesianAnalysis.Results!.Output;
        foreach (double probability in ResponseProbabilities)
        {
            double location = fixture.Parent.InverseCDF(probability);
            double parentResponse = fixture.Parent.CDF(location);
            var responses = new double[output.Count];
            for (int drawIndex = 0; drawIndex < output.Count; drawIndex++)
                responses[drawIndex] = EvaluateCompositeCdf(fixture, output[drawIndex].Values, location);

            Array.Sort(responses);
            double lower = Statistics.Percentile(responses, 0.025d, true);
            double estimate = Statistics.Percentile(responses, 0.50d, true);
            double upper = Statistics.Percentile(responses, 0.975d, true);
            string label = $"{fixture.Label}: Bayesian composite CDF at parent p={probability:G2}";

            RecoveryAcceptance.AssertIdentifiedResponseGrid(label, parentResponse, lower, upper);
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(
                label,
                estimate,
                parentResponse,
                lower,
                upper);
        }
    }

    /// <summary>
    /// Computes a response-scale standard error from the MLE covariance using a bounded
    /// central finite-difference CDF gradient.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="model">The fitted model providing coordinate bounds.</param>
    /// <param name="covariance">The observed-information covariance.</param>
    /// <param name="parameters">The MLE coordinate vector.</param>
    /// <param name="location">The fixed parent-quantile location.</param>
    /// <returns>The delta-method standard error of the fitted composite CDF.</returns>
    private static double ComputeDeltaMethodStandardError(
        RecoveryFixture fixture,
        CompetingRisksModel model,
        Matrix covariance,
        double[] parameters,
        double location)
    {
        var gradient = new double[parameters.Length];
        for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
        {
            double center = parameters[parameterIndex];
            double step = 1E-5 * Math.Max(1d, Math.Abs(center));
            double lower = Math.Max(model.Parameters[parameterIndex].LowerBound, center - step);
            double upper = Math.Min(model.Parameters[parameterIndex].UpperBound, center + step);
            Assert.IsTrue(upper > lower,
                $"{fixture.Label}: finite-difference interval collapsed for " +
                $"{model.Parameters[parameterIndex].OwnerName} {model.Parameters[parameterIndex].Name}.");

            double[] lowerParameters = parameters.ToArray();
            double[] upperParameters = parameters.ToArray();
            lowerParameters[parameterIndex] = lower;
            upperParameters[parameterIndex] = upper;
            gradient[parameterIndex] =
                (EvaluateCompositeCdf(fixture, upperParameters, location) -
                 EvaluateCompositeCdf(fixture, lowerParameters, location)) /
                (upper - lower);
        }

        double variance = 0d;
        for (int row = 0; row < gradient.Length; row++)
        {
            for (int column = 0; column < gradient.Length; column++)
                variance += gradient[row] * covariance[row, column] * gradient[column];
        }

        Assert.IsTrue(Tools.IsFinite(variance) && variance >= -1E-12,
            $"{fixture.Label}: delta-method response variance {variance:G17} is invalid.");
        return Math.Sqrt(Math.Max(0d, variance));
    }

    /// <summary>
    /// Evaluates the preserved competing-risk definition at one coordinate vector and location.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="parameters">The flattened component coordinates.</param>
    /// <param name="location">The fixed response location.</param>
    /// <returns>The combined CDF value.</returns>
    private static double EvaluateCompositeCdf(
        RecoveryFixture fixture,
        double[] parameters,
        double location)
    {
        CompetingRisks distribution = CreateFittedDistribution(fixture, parameters);
        double response = distribution.CDF(location);
        Assert.IsTrue(Tools.IsFinite(response),
            $"{fixture.Label}: composite CDF is nonfinite at x={location:G17}.");
        return response;
    }

    /// <summary>
    /// Verifies every fitted parameter is finite.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="parameters">The fitted parameter vector.</param>
    /// <param name="estimator">The estimator label.</param>
    private static void AssertFiniteParameters(
        RecoveryFixture fixture,
        IReadOnlyList<double> parameters,
        string estimator)
    {
        Assert.AreEqual(
            fixture.Parent.NumberOfParameters,
            parameters.Count,
            $"{fixture.Label}: {estimator} parameter count mismatch.");
        for (int index = 0; index < parameters.Count; index++)
        {
            Assert.IsTrue(
                Tools.IsFinite(parameters[index]),
                $"{fixture.Label}: {estimator} parameter {index + 1} is not finite.");
        }
    }

    /// <summary>
    /// Clones the generating parent and applies fitted flattened parameters.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="parameters">The fitted flattened parameter vector.</param>
    /// <returns>The fitted competing-risk distribution.</returns>
    private static CompetingRisks CreateFittedDistribution(
        RecoveryFixture fixture,
        double[] parameters)
    {
        var fitted = (CompetingRisks)fixture.Parent.Clone();
        fitted.SetParameters(parameters);
        return fitted;
    }

    /// <summary>
    /// Creates a recovery fixture while preserving the selection rule, dependency mode,
    /// distribution base, and fixed correlation matrix on the Numerics parent.
    /// </summary>
    /// <param name="label">The human-readable fixture label.</param>
    /// <param name="isMinimum">Whether the observed response is the component minimum.</param>
    /// <param name="distributions">The generating component distributions.</param>
    /// <param name="dependency">The fixed dependency mode.</param>
    /// <param name="correlationMatrix">The optional fixed latent correlation matrix.</param>
    /// <returns>The immutable recovery fixture.</returns>
    private static RecoveryFixture CreateFixture(
        string label,
        bool isMinimum,
        UnivariateDistributionBase[] distributions,
        Probability.DependencyType dependency = Probability.DependencyType.Independent,
        double[,]? correlationMatrix = null)
    {
        var parent = new CompetingRisks(distributions)
        {
            MinimumOfRandomVariables = isMinimum,
            Dependency = dependency
        };
        if (correlationMatrix != null)
            parent.CorrelationMatrix = (double[,])correlationMatrix.Clone();

        return new RecoveryFixture(
            label,
            parent,
            RecoveryDesign.ScalarObservations(
                "One observed scalar minimum or maximum from the declared competing-risk parent."));
    }

    /// <summary>
    /// Stores one immutable competing-risk recovery scenario.
    /// </summary>
    private sealed class RecoveryFixture
    {
        /// <summary>
        /// Initializes a competing-risk recovery scenario.
        /// </summary>
        /// <param name="label">The human-readable fixture label.</param>
        /// <param name="parent">The known generating distribution.</param>
        /// <param name="design">The predeclared scalar-observation recovery design.</param>
        public RecoveryFixture(
            string label,
            CompetingRisks parent,
            RecoveryDesign design)
        {
            Label = label;
            Parent = parent;
            Design = design;
        }

        /// <summary>Gets the human-readable fixture label.</summary>
        public string Label { get; }

        /// <summary>Gets the known generating distribution.</summary>
        public CompetingRisks Parent { get; }

        /// <summary>Gets the synthetic sample size.</summary>
        public int SampleSize => RecoveryDesign.SampleSize;

        /// <summary>Gets the predeclared scalar-observation recovery design.</summary>
        public RecoveryDesign Design { get; }

        /// <summary>Gets or sets the measured identification evidence for the fixed realization.</summary>
        public DesignDiagnostics? Diagnostics { get; set; }
    }
}
