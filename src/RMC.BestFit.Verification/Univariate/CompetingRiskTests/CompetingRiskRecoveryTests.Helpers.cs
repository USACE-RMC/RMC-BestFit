using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.Univariate.CompetingRiskTests;

/// <summary>
/// Provides the pinned fixtures and independent acceptance helpers for
/// <see cref="CompetingRiskRecoveryTests"/>.
/// </summary>
public partial class CompetingRiskRecoveryTests
{
    /// <summary>The common two-component sample size.</summary>
    private const int TwoComponentSampleSize = 1000;

    /// <summary>The common three-component sample size.</summary>
    private const int ThreeComponentSampleSize = 1500;

    /// <summary>The deterministic synthetic-data seed inherited from the Numerics fixtures.</summary>
    private const int FixtureSeed = 12345;

    /// <summary>The absolute true-parameter data-likelihood parity tolerance.</summary>
    private const double LikelihoodTolerance = 1E-10;

    /// <summary>The relative parameter tolerance used only for approved identifiable parameters.</summary>
    private const double ParameterRelativeTolerance = 0.25d;

    /// <summary>The maximum split R-hat accepted for each Bayesian parameter.</summary>
    private const double MaximumRhat = 1.1d;

    /// <summary>The minimum effective sample size accepted for each Bayesian parameter.</summary>
    private const double MinimumEss = 100d;

    /// <summary>
    /// Runs one fixture through the production maximum-likelihood estimator and verifies
    /// its parent likelihood, optimizer outcome, combined CDF, and approved parameter gates.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    private static void VerifyMaximumLikelihoodRecovery(RecoveryFixture fixture)
    {
        (double[] sample, CompetingRisksModel fittedModel) = PrepareRecovery(fixture);
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
        CompetingRisks fittedDistribution = CreateFittedDistribution(fixture, fittedParameters);
        AssertCombinedCdfRecovery(fixture, sample, fittedDistribution, "MLE");
        AssertApprovedParameterRecovery(fixture, fittedParameters, "MLE");
    }

    /// <summary>
    /// Runs one fixture through <see cref="CompetingRiskAnalysis"/> while leaving the
    /// full DEMCzs simulation and output configuration at production defaults.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <returns>A task that completes after the Bayesian recovery assertions.</returns>
    private static async Task VerifyBayesianRecoveryAsync(RecoveryFixture fixture)
    {
        (double[] sample, CompetingRisksModel fittedModel) = PrepareRecovery(fixture);
        var analysis = new CompetingRiskAnalysis(fittedModel);
        AssertDefaultDemczsConfiguration(fixture, analysis.BayesianAnalysis, fittedModel.NumberOfParameters);

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
        AssertBayesianCombinedCdfRecovery(fixture, sample, analysis);
        AssertApprovedBayesianParameterRecovery(fixture, analysis);
        AssertBayesianDiagnostics(fixture, fittedModel, analysis);
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
    /// Verifies the fitted parent CDF against the known generating parent at 99 fixed
    /// empirical-quantile locations.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="sample">The generated synthetic sample.</param>
    /// <param name="fitted">The fitted competing-risk distribution.</param>
    /// <param name="estimator">The estimator label.</param>
    private static void AssertCombinedCdfRecovery(
        RecoveryFixture fixture,
        double[] sample,
        CompetingRisks fitted,
        string estimator)
    {
        double[] sortedSample = sample.OrderBy(value => value).ToArray();
        double maximumError = 0d;
        double maximumErrorProbability = double.NaN;
        for (int percentile = 1; percentile <= 99; percentile++)
        {
            double probability = percentile / 100d;
            double x = Statistics.Percentile(sortedSample, probability, true);
            double error = Math.Abs(fixture.Parent.CDF(x) - fitted.CDF(x));
            if (error > maximumError)
            {
                maximumError = error;
                maximumErrorProbability = probability;
            }
        }

        Assert.IsTrue(
            double.IsFinite(maximumError) && maximumError <= fixture.CdfTolerance,
            $"{fixture.Label}: {estimator} maximum parent-CDF error {maximumError:G6} at empirical " +
            $"probability {maximumErrorProbability:G2} exceeded {fixture.CdfTolerance:G2}.");
    }

    /// <summary>
    /// Verifies the label-invariant posterior mean combined CDF against the generating parent at
    /// 99 fixed empirical-quantile locations. Averaging combined CDFs, rather than component
    /// parameters, preserves the identifiable posterior target when same-family labels switch.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="sample">The generated synthetic sample.</param>
    /// <param name="analysis">The completed Bayesian competing-risk analysis.</param>
    private static void AssertBayesianCombinedCdfRecovery(
        RecoveryFixture fixture,
        double[] sample,
        CompetingRiskAnalysis analysis)
    {
        double[] sortedSample = sample.OrderBy(value => value).ToArray();
        var locations = new double[99];
        for (int percentile = 1; percentile <= 99; percentile++)
            locations[percentile - 1] = Statistics.Percentile(sortedSample, percentile / 100d, true);

        var cdfSums = new double[locations.Length];
        var output = analysis.BayesianAnalysis.Results!.Output;
        foreach (ParameterSet parameterSet in output)
        {
            var posteriorDistribution = (CompetingRisks)fixture.Parent.Clone();
            posteriorDistribution.SetParameters(parameterSet.Values);
            for (int locationIndex = 0; locationIndex < locations.Length; locationIndex++)
                cdfSums[locationIndex] += posteriorDistribution.CDF(locations[locationIndex]);
        }

        double maximumError = 0d;
        double maximumErrorProbability = double.NaN;
        for (int locationIndex = 0; locationIndex < locations.Length; locationIndex++)
        {
            double posteriorMeanCdf = cdfSums[locationIndex] / output.Count;
            double error = Math.Abs(fixture.Parent.CDF(locations[locationIndex]) - posteriorMeanCdf);
            if (error > maximumError)
            {
                maximumError = error;
                maximumErrorProbability = (locationIndex + 1) / 100d;
            }
        }

        Assert.IsTrue(
            double.IsFinite(maximumError) && maximumError <= fixture.CdfTolerance,
            $"{fixture.Label}: Bayesian maximum posterior-mean combined-CDF error " +
            $"{maximumError:G6} at empirical probability {maximumErrorProbability:G2} exceeded " +
            $"{fixture.CdfTolerance:G2}.");
    }

    /// <summary>
    /// Applies only the approved hybrid component gates: contrasting two-Weibull shapes
    /// and separated two-Normal means, both with label switching resolved.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="parameters">The fitted flattened parameter vector.</param>
    /// <param name="estimator">The estimator label.</param>
    private static void AssertApprovedParameterRecovery(
        RecoveryFixture fixture,
        double[] parameters,
        string estimator)
    {
        if (fixture.ParameterGate == ParameterGate.WeibullShapes)
        {
            double[] expected = [0.8d, 3d];
            double[] actual = [parameters[1], parameters[3]];
            Array.Sort(actual);
            for (int index = 0; index < expected.Length; index++)
            {
                AssertRelativeDifference(
                    fixture,
                    estimator,
                    $"sorted Weibull shape {index + 1}",
                    expected[index],
                    actual[index],
                    ParameterRelativeTolerance);
            }
        }
        else if (fixture.ParameterGate == ParameterGate.NormalMeans)
        {
            double[] expected = [50d, 85d];
            double[] actual = [parameters[0], parameters[2]];
            Array.Sort(actual);
            for (int index = 0; index < expected.Length; index++)
            {
                AssertRelativeDifference(
                    fixture,
                    estimator,
                    $"sorted Normal mean {index + 1}",
                    expected[index],
                    actual[index],
                    0.30d);
            }
        }
    }

    /// <summary>
    /// Verifies the approved identifiable Bayesian component parameters after ordering the
    /// exchangeable component pair within each retained draw.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="analysis">The completed competing-risk analysis.</param>
    private static void AssertApprovedBayesianParameterRecovery(
        RecoveryFixture fixture,
        CompetingRiskAnalysis analysis)
    {
        if (fixture.ParameterGate == ParameterGate.None)
            return;

        int firstParameterIndex = fixture.ParameterGate == ParameterGate.WeibullShapes ? 1 : 0;
        int secondParameterIndex = fixture.ParameterGate == ParameterGate.WeibullShapes ? 3 : 2;
        double[] expected = fixture.ParameterGate == ParameterGate.WeibullShapes
            ? [0.8d, 3d]
            : [50d, 85d];
        double relativeTolerance = fixture.ParameterGate == ParameterGate.WeibullShapes
            ? ParameterRelativeTolerance
            : 0.30d;
        string parameterLabel = fixture.ParameterGate == ParameterGate.WeibullShapes
            ? "Weibull shape"
            : "Normal mean";

        var output = analysis.BayesianAnalysis.Results!.Output;
        double lowerSum = 0d;
        double upperSum = 0d;
        foreach (ParameterSet parameterSet in output)
        {
            double first = parameterSet.Values[firstParameterIndex];
            double second = parameterSet.Values[secondParameterIndex];
            lowerSum += Math.Min(first, second);
            upperSum += Math.Max(first, second);
        }

        double[] actual = [lowerSum / output.Count, upperSum / output.Count];
        for (int index = 0; index < expected.Length; index++)
        {
            AssertRelativeDifference(
                fixture,
                "Bayesian",
                $"draw-ordered posterior-mean {parameterLabel} {index + 1}",
                expected[index],
                actual[index],
                relativeTolerance);
        }
    }

    /// <summary>
    /// Verifies one fitted parameter against a declared relative tolerance.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="estimator">The estimator label.</param>
    /// <param name="parameterLabel">The parameter label.</param>
    /// <param name="expected">The generating value.</param>
    /// <param name="actual">The fitted value.</param>
    /// <param name="relativeTolerance">The maximum relative difference.</param>
    private static void AssertRelativeDifference(
        RecoveryFixture fixture,
        string estimator,
        string parameterLabel,
        double expected,
        double actual,
        double relativeTolerance)
    {
        double relativeDifference = Math.Abs(actual - expected) / Math.Abs(expected);
        Assert.IsTrue(
            double.IsFinite(relativeDifference) && relativeDifference <= relativeTolerance,
            $"{fixture.Label}: {estimator} {parameterLabel} relative difference " +
            $"{relativeDifference:P2} exceeded {relativeTolerance:P0}.");
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
                double.IsFinite(parameters[index]),
                $"{fixture.Label}: {estimator} parameter {index + 1} is not finite.");
        }
    }

    /// <summary>
    /// Verifies split R-hat and conservative effective sample size for every Bayesian parameter.
    /// </summary>
    /// <param name="fixture">The immutable recovery fixture.</param>
    /// <param name="model">The fitted BestFit model.</param>
    /// <param name="analysis">The completed competing-risk analysis.</param>
    private static void AssertBayesianDiagnostics(
        RecoveryFixture fixture,
        CompetingRisksModel model,
        CompetingRiskAnalysis analysis)
    {
        var parameterResults = analysis.BayesianAnalysis.Results!.ParameterResults;
        Assert.AreEqual(
            model.NumberOfParameters,
            parameterResults.Length,
            $"{fixture.Label}: diagnostic parameter count mismatch.");
        for (int index = 0; index < parameterResults.Length; index++)
        {
            double rhat = parameterResults[index].SummaryStatistics.Rhat;
            double ess = parameterResults[index].SummaryStatistics.ESS;
            string parameterName = model.Parameters[index].Name;
            Assert.IsTrue(
                double.IsFinite(rhat) && rhat < MaximumRhat,
                $"{fixture.Label}: {parameterName} R-hat {rhat:G6} is not finite and below {MaximumRhat:G2}.");
            Assert.IsTrue(
                double.IsFinite(ess) && ess > MinimumEss,
                $"{fixture.Label}: {parameterName} ESS {ess:G6} is not finite and above {MinimumEss:G0}.");
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
    /// Creates the independent minimum of Weibull(50, 1) and Weibull(80, 3).
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMinimumTwoWeibullConstantIncreasing()
    {
        return CreateFixture(
            "minimum Weibull(50,1) + Weibull(80,3)",
            true,
            [new Weibull(50d, 1d), new Weibull(80d, 3d)],
            TwoComponentSampleSize,
            0.05d);
    }

    /// <summary>
    /// Creates the identifiable independent minimum of Weibull(30, 0.8) and Weibull(100, 3).
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMinimumTwoWeibullContrastingShapes()
    {
        return CreateFixture(
            "minimum Weibull(30,0.8) + Weibull(100,3)",
            true,
            [new Weibull(30d, 0.8d), new Weibull(100d, 3d)],
            TwoComponentSampleSize,
            0.05d,
            parameterGate: ParameterGate.WeibullShapes);
    }

    /// <summary>
    /// Creates the independent three-Weibull bathtub minimum.
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMinimumThreeWeibullBathtub()
    {
        return CreateFixture(
            "minimum three-Weibull bathtub",
            true,
            [new Weibull(20d, 0.7d), new Weibull(200d, 1d), new Weibull(150d, 4d)],
            ThreeComponentSampleSize,
            0.06d);
    }

    /// <summary>
    /// Creates the independent minimum of three Weibulls with separated shapes.
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMinimumThreeWeibullSeparatedShapes()
    {
        return CreateFixture(
            "minimum Weibull(15,0.5) + Weibull(60,1.5) + Weibull(120,4)",
            true,
            [new Weibull(15d, 0.5d), new Weibull(60d, 1.5d), new Weibull(120d, 4d)],
            ThreeComponentSampleSize,
            0.06d);
    }

    /// <summary>
    /// Creates the identifiable independent maximum of two separated Normal distributions.
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMaximumTwoSeparatedNormals()
    {
        return CreateFixture(
            "maximum Normal(50,8) + Normal(85,12)",
            false,
            [new Normal(50d, 8d), new Normal(85d, 12d)],
            TwoComponentSampleSize,
            0.05d,
            parameterGate: ParameterGate.NormalMeans);
    }

    /// <summary>
    /// Creates the independent maximum of Weibull(50, 2) and Gumbel(70, 15).
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMaximumWeibullAndGumbel()
    {
        return CreateFixture(
            "maximum Weibull(50,2) + Gumbel(70,15)",
            false,
            [new Weibull(50d, 2d), new Gumbel(70d, 15d)],
            TwoComponentSampleSize,
            0.05d);
    }

    /// <summary>
    /// Creates the independent maximum of three separated Normal distributions.
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMaximumThreeSeparatedNormals()
    {
        return CreateFixture(
            "maximum Normal(40,6) + Normal(70,8) + Normal(100,10)",
            false,
            [new Normal(40d, 6d), new Normal(70d, 8d), new Normal(100d, 10d)],
            ThreeComponentSampleSize,
            0.06d);
    }

    /// <summary>
    /// Creates the independent maximum of Exponential, Gamma, and natural-base LogNormal components.
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMaximumThreeDifferentFamilies()
    {
        return CreateFixture(
            "maximum Exponential(0.05) + Gamma(3,15) + natural LogNormal(4.2,0.4)",
            false,
            [
                new Exponential(0.05d),
                new GammaDistribution(3d, 15d),
                new LogNormal(4.2d, 0.4d) { Base = Math.E }
            ],
            ThreeComponentSampleSize,
            0.06d);
    }

    /// <summary>
    /// Creates the correlation-matrix minimum of two Weibulls at latent correlation 0.6.
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMinimumCorrelatedTwoWeibulls()
    {
        return CreateFixture(
            "correlated minimum Weibull(50,1) + Weibull(80,3), rho=0.6",
            true,
            [new Weibull(50d, 1d), new Weibull(80d, 3d)],
            TwoComponentSampleSize,
            0.06d,
            Probability.DependencyType.CorrelationMatrix,
            new[,] { { 1d, 0.6d }, { 0.6d, 1d } });
    }

    /// <summary>
    /// Creates the correlation-matrix maximum of two Normals at latent correlation 0.6.
    /// </summary>
    /// <returns>The recovery fixture.</returns>
    private static RecoveryFixture CreateMaximumCorrelatedTwoNormals()
    {
        return CreateFixture(
            "correlated maximum Normal(50,10) + Normal(65,12), rho=0.6",
            false,
            [new Normal(50d, 10d), new Normal(65d, 12d)],
            TwoComponentSampleSize,
            0.06d,
            Probability.DependencyType.CorrelationMatrix,
            new[,] { { 1d, 0.6d }, { 0.6d, 1d } });
    }

    /// <summary>
    /// Creates a recovery fixture while preserving the selection rule, dependency mode,
    /// distribution base, and fixed correlation matrix on the Numerics parent.
    /// </summary>
    /// <param name="label">The human-readable fixture label.</param>
    /// <param name="isMinimum">Whether the observed response is the component minimum.</param>
    /// <param name="distributions">The generating component distributions.</param>
    /// <param name="sampleSize">The synthetic sample size.</param>
    /// <param name="cdfTolerance">The maximum parent-versus-fitted CDF error.</param>
    /// <param name="dependency">The fixed dependency mode.</param>
    /// <param name="correlationMatrix">The optional fixed latent correlation matrix.</param>
    /// <param name="parameterGate">The optional approved component-parameter gate.</param>
    /// <returns>The immutable recovery fixture.</returns>
    private static RecoveryFixture CreateFixture(
        string label,
        bool isMinimum,
        UnivariateDistributionBase[] distributions,
        int sampleSize,
        double cdfTolerance,
        Probability.DependencyType dependency = Probability.DependencyType.Independent,
        double[,]? correlationMatrix = null,
        ParameterGate parameterGate = ParameterGate.None)
    {
        var parent = new CompetingRisks(distributions)
        {
            MinimumOfRandomVariables = isMinimum,
            Dependency = dependency
        };
        if (correlationMatrix != null)
            parent.CorrelationMatrix = (double[,])correlationMatrix.Clone();

        return new RecoveryFixture(label, parent, sampleSize, cdfTolerance, parameterGate);
    }

    /// <summary>
    /// Identifies the limited component parameters approved for direct recovery checks.
    /// </summary>
    private enum ParameterGate
    {
        /// <summary>No component parameter is independently gated.</summary>
        None,

        /// <summary>The two contrasting Weibull shapes are gated after sorting.</summary>
        WeibullShapes,

        /// <summary>The two separated Normal means are gated after sorting.</summary>
        NormalMeans
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
        /// <param name="sampleSize">The synthetic sample size.</param>
        /// <param name="cdfTolerance">The maximum parent-versus-fitted CDF error.</param>
        /// <param name="parameterGate">The optional approved component-parameter gate.</param>
        public RecoveryFixture(
            string label,
            CompetingRisks parent,
            int sampleSize,
            double cdfTolerance,
            ParameterGate parameterGate)
        {
            Label = label;
            Parent = parent;
            SampleSize = sampleSize;
            CdfTolerance = cdfTolerance;
            ParameterGate = parameterGate;
        }

        /// <summary>Gets the human-readable fixture label.</summary>
        public string Label { get; }

        /// <summary>Gets the known generating distribution.</summary>
        public CompetingRisks Parent { get; }

        /// <summary>Gets the synthetic sample size.</summary>
        public int SampleSize { get; }

        /// <summary>Gets the maximum parent-versus-fitted CDF error.</summary>
        public double CdfTolerance { get; }

        /// <summary>Gets the optional approved component-parameter gate.</summary>
        public ParameterGate ParameterGate { get; }
    }
}
