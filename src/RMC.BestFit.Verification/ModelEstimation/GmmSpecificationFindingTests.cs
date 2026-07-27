using System.Text.Json;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Evaluates the overidentified GMM fitting and specification-test review findings
/// against a committed independent R <c>gmm</c> oracle.
/// </summary>
/// <remarks>
/// These tests verify fixed-weight one-step estimation and the efficient-weight
/// Hansen J statistic after the approved TR-026/TR-034 corrections. R is not
/// required at test runtime.
/// </remarks>
[TestClass]
public class GmmSpecificationFindingTests
{
    /// <summary>
    /// Verifies the efficient two-step fit and Hansen J statistic against R <c>gmm</c>.
    /// </summary>
    /// <remarks>
    /// The reference statistic is <c>n*gBar'W*gBar</c>, where <c>W</c> is the
    /// weighting matrix selected for the completed second optimization pass.
    /// </remarks>
    [TestMethod]
    public void HansenJ_MatchesRGmmSelectedWeightStatistic()
    {
        JsonElement oracle = LoadOracle();
        JsonElement expected = oracle.GetProperty("two_step");
        JsonElement metadata = oracle.GetProperty("metadata");
        (double[] x, double[] z) = ReadData(oracle);
        GeneralizedMethodOfMoments estimator = CreateEstimator(
            x,
            z,
            GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep);

        Assert.IsTrue(estimator.Estimate(), "The deterministic overidentified two-step fit must converge.");
        Assert.AreEqual(
            expected.GetProperty("parameter").GetDouble(),
            estimator.BestParameterSet.Values[0],
            metadata.GetProperty("parameter_absolute_tolerance").GetDouble(),
            "BestFit's two-step parameter must match the R gmm fit before testing Hansen J.");
        Assert.AreEqual(
            expected.GetProperty("objective").GetDouble(),
            estimator.ObjectiveFunctionValue,
            metadata.GetProperty("objective_absolute_tolerance").GetDouble(),
            "BestFit's selected-weight two-step objective must match R gmm.");

        double expectedJ = expected.GetProperty("j_statistic").GetDouble();
        double expectedPValue = expected.GetProperty("p_value").GetDouble();
        Assert.AreEqual(
            x.Length * expected.GetProperty("objective").GetDouble(),
            expectedJ,
            metadata.GetProperty("j_statistic_absolute_tolerance").GetDouble(),
            "The committed R oracle must satisfy J = nQ.");
        estimator.PostProcess(useSandwich: true, computeJstat: true);

        Assert.AreEqual(
            expectedJ,
            estimator.JStat,
            metadata.GetProperty("j_statistic_absolute_tolerance").GetDouble(),
            "BestFit's selected-weight Hansen J statistic must match R gmm.");
        Assert.AreEqual(
            expectedPValue,
            estimator.JStatPval,
            metadata.GetProperty("p_value_absolute_tolerance").GetDouble(),
            "BestFit's Hansen J p-value must match R gmm.");
    }

    /// <summary>
    /// Verifies overidentified fixed-weight one-step estimation against R <c>gmm</c>.
    /// </summary>
    /// <remarks>
    /// R <c>gmm</c> uses a supplied positive-definite <c>weightsMatrix</c> as a fixed
    /// weight. A generic fixed weight supports estimation but is not automatically
    /// assigned an efficient-weight Hansen chi-square interpretation.
    /// </remarks>
    [TestMethod]
    public void OveridentifiedOneStep_MatchesRGmmFixedWeightOracle()
    {
        JsonElement oracle = LoadOracle();
        JsonElement expected = oracle.GetProperty("fixed_weight");
        JsonElement metadata = oracle.GetProperty("metadata");
        (double[] x, double[] z) = ReadData(oracle);
        GeneralizedMethodOfMoments estimator = CreateEstimator(
            x,
            z,
            GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep);
        double expectedParameter = expected.GetProperty("parameter").GetDouble();

        Assert.AreEqual(
            expected.GetProperty("objective").GetDouble(),
            estimator.Q([expectedParameter]),
            metadata.GetProperty("objective_absolute_tolerance").GetDouble(),
            "BestFit's fixed-weight objective must match R gmm at the external optimum.");

        Assert.IsTrue(
            estimator.IsValid(out List<string> validationErrors),
            $"Overidentified one-step GMM must be valid: {string.Join("; ", validationErrors)}");
        Assert.IsTrue(estimator.Estimate(), "The overidentified fixed-weight fit must converge.");
        Assert.AreEqual(
            expectedParameter,
            estimator.BestParameterSet.Values[0],
            metadata.GetProperty("parameter_absolute_tolerance").GetDouble(),
            "BestFit's fixed-weight one-step parameter must match R gmm.");
        Assert.AreEqual(
            expected.GetProperty("objective").GetDouble(),
            estimator.ObjectiveFunctionValue,
            metadata.GetProperty("objective_absolute_tolerance").GetDouble(),
            "BestFit's fitted fixed-weight objective must match R gmm.");

        estimator.PostProcess(useSandwich: true, computeJstat: true);
        Assert.IsTrue(double.IsNaN(estimator.JStat),
            "A generic fixed-weight one-step fit must not be labeled as efficient-weight Hansen J.");
        Assert.IsTrue(double.IsNaN(estimator.JStatPval),
            "A generic fixed-weight one-step fit must not receive a Hansen chi-square p-value.");
    }

    /// <summary>
    /// Verifies overidentified efficient two-step sandwich covariance against R <c>gmm</c>.
    /// </summary>
    [TestMethod]
    public void OveridentifiedTwoStepSandwichCovariance_MatchesRGmmOracle()
    {
        JsonElement oracle = LoadOracle();
        JsonElement metadata = oracle.GetProperty("metadata");
        (double[] x, double[] z) = ReadData(oracle);
        double tolerance = metadata.GetProperty("covariance_absolute_tolerance").GetDouble();

        AssertCovarianceMatches(
            CreateEstimator(x, z, GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep),
            oracle.GetProperty("two_step"),
            tolerance,
            "efficient two-step");
    }

    /// <summary>
    /// Verifies overidentified fixed-weight one-step sandwich covariance against R <c>gmm</c>.
    /// </summary>
    [TestMethod]
    public void OveridentifiedFixedWeightSandwichCovariance_MatchesRGmmOracle()
    {
        JsonElement oracle = LoadOracle();
        JsonElement metadata = oracle.GetProperty("metadata");
        (double[] x, double[] z) = ReadData(oracle);
        double tolerance = metadata.GetProperty("covariance_absolute_tolerance").GetDouble();

        AssertCovarianceMatches(
            CreateEstimator(x, z, GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep),
            oracle.GetProperty("fixed_weight"),
            tolerance,
            "fixed-weight one-step");
    }

    /// <summary>
    /// Fits one GMM strategy and compares its sandwich variance and standard error with R.
    /// </summary>
    /// <param name="estimator">Configured overidentified estimator.</param>
    /// <param name="expected">Oracle result for the matching R fit.</param>
    /// <param name="tolerance">Absolute covariance tolerance.</param>
    /// <param name="label">Strategy label included in assertion messages.</param>
    private static void AssertCovarianceMatches(
        GeneralizedMethodOfMoments estimator,
        JsonElement expected,
        double tolerance,
        string label)
    {
        Assert.IsTrue(estimator.Estimate(), $"The deterministic {label} fit must converge.");
        estimator.PostProcess(useSandwich: true, computeJstat: false);

        double expectedVariance = expected.GetProperty("covariance")[0][0].GetDouble();
        double actualVariance = estimator.GetCovarianceMatrix()[0, 0];
        Assert.AreEqual(expectedVariance, actualVariance, tolerance, $"{label} sandwich variance mismatch.");
        Assert.AreEqual(
            expected.GetProperty("standard_error").GetDouble(),
            estimator.GetStandardErrors()[0],
            tolerance,
            $"{label} standard error mismatch.");
    }

    /// <summary>
    /// Creates the deterministic one-parameter, two-moment GMM estimator.
    /// </summary>
    /// <param name="x">Primary observations.</param>
    /// <param name="z">Instrument values.</param>
    /// <param name="strategy">GMM estimation strategy.</param>
    /// <returns>The configured estimator with an identity initial weighting matrix.</returns>
    private static GeneralizedMethodOfMoments CreateEstimator(
        double[] x,
        double[] z,
        GeneralizedMethodOfMoments.GMMEstimationStrategy strategy)
    {
        MomentConditionFunction momentFunction = parameters =>
        {
            double theta = parameters[0];
            var pointwise = new double[x.Length, 2];
            double meanFirst = 0d;
            double meanSecond = 0d;
            for (int i = 0; i < x.Length; i++)
            {
                double residual = x[i] - theta;
                pointwise[i, 0] = residual;
                pointwise[i, 1] = z[i] * residual;
                meanFirst += pointwise[i, 0];
                meanSecond += pointwise[i, 1];
            }

            meanFirst /= x.Length;
            meanSecond /= x.Length;
            var covariance = new Matrix(2, 2);
            for (int i = 0; i < x.Length; i++)
            {
                double centeredFirst = pointwise[i, 0] - meanFirst;
                double centeredSecond = pointwise[i, 1] - meanSecond;
                covariance[0, 0] += centeredFirst * centeredFirst;
                covariance[0, 1] += centeredFirst * centeredSecond;
                covariance[1, 0] += centeredSecond * centeredFirst;
                covariance[1, 1] += centeredSecond * centeredSecond;
            }

            covariance /= x.Length;
            return (new Vector([meanFirst, meanSecond]), covariance);
        };

        JacobianFunction jacobianFunction = _ =>
            new double[,] { { -1d }, { -z.Average() } };
        var estimator = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunction,
            numberOfParameters: 1,
            numberOfMomentConditions: 2,
            sampleSize: x.Length,
            initialValues: [1.5d],
            lowerBounds: [-10d],
            upperBounds: [10d],
            initialW: Matrix.Identity(2),
            jacobianFunction: jacobianFunction)
        {
            EstimationStrategy = strategy,
            OptimizerMethod = OptimizationMethod.BFGS,
            UseFallbackOptimizer = false,
            MaxFunctionEvaluations = 10000,
            AbsoluteTolerance = 1E-12d,
            RelativeTolerance = 1E-12d
        };
        return estimator;
    }

    /// <summary>
    /// Loads and clones the committed R <c>gmm</c> oracle root.
    /// </summary>
    /// <returns>A detached JSON root element.</returns>
    private static JsonElement LoadOracle()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            "gmm-specification-oracle.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Reads the deterministic observation and instrument arrays from the oracle.
    /// </summary>
    /// <param name="oracle">Oracle root element.</param>
    /// <returns>The primary observations and instrument values.</returns>
    private static (double[] X, double[] Z) ReadData(JsonElement oracle)
    {
        JsonElement data = oracle.GetProperty("data");
        var x = new double[data.GetArrayLength()];
        var z = new double[data.GetArrayLength()];
        int index = 0;
        foreach (JsonElement observation in data.EnumerateArray())
        {
            x[index] = observation.GetProperty("x").GetDouble();
            z[index] = observation.GetProperty("z").GetDouble();
            index++;
        }

        return (x, z);
    }
}
