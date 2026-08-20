using System.Text.Json;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Independent numerical verification methods for the Phase 5 time-series findings.
/// </summary>
[TestClass]
public class Phase5TimeSeriesVerificationTests
{
    /// <summary>
    /// Verifies the four time-series Jeffreys scale components against the analytical
    /// log-density oracle <c>log(1 / sigma) = -log(sigma)</c> at fixed positive scales.
    /// </summary>
    /// <remarks>
    /// The oracle is evaluated directly from the mathematical Jeffreys density and does not
    /// call either production scalar-prior implementation. The absolute acceptance tolerance
    /// is fixed at 1E-12.
    /// </remarks>
    [TestMethod]
    public void JeffreysScaleMetadataMatchesIndependentPriorOracle()
    {
        JsonElement oracle = LoadOracle("phase5-jeffreys-prior-oracle.json");
        double tolerance = oracle.GetProperty("absolute_tolerance").GetDouble();

        foreach (JsonElement testCase in oracle.GetProperty("cases").EnumerateArray())
        {
            string modelName = testCase.GetProperty("model").GetString()!;
            double sigma = testCase.GetProperty("sigma").GetDouble();
            double expected = testCase.GetProperty("expected_log_density").GetDouble();
            VerifyJeffreysComponent(CreateModel(modelName), sigma, expected, tolerance);
        }
    }

    /// <summary>
    /// Verifies valid Gaussian data/prior decomposition and invalid scale rejection against an
    /// independently tabulated finite-positive-scale oracle.
    /// </summary>
    /// <remarks>
    /// The Gaussian log density is calculated directly as
    /// <c>-0.5*log(2*pi)-log(sigma)-e^2/(2*sigma^2)</c>. Uniform marginal-prior normalization and
    /// the Jeffreys contribution are calculated independently. Valid values use 1E-12 absolute
    /// tolerance; invalid values must equal negative infinity exactly.
    /// </remarks>
    [TestMethod]
    public void InvalidScaleBehaviorMatchesScalarAndPointwiseOracle()
    {
        JsonElement oracle = LoadOracle("phase5-invalid-scale-oracle.json");
        double[] response = ReadDoubleArray(oracle.GetProperty("response"));
        double sigma = oracle.GetProperty("sigma").GetDouble();
        double tolerance = oracle.GetProperty("absolute_tolerance").GetDouble();
        double expectedPriorFromFormula = ReadDoubleArray(oracle.GetProperty("parameter_prior_widths"))
            .Sum(width => -Math.Log(width)) - Math.Log(sigma);

        foreach (JsonElement testCase in oracle.GetProperty("cases").EnumerateArray())
        {
            string modelName = testCase.GetProperty("model").GetString()!;
            ModelBase model = CreateModel(modelName, response);
            SetJeffreysRule(model);
            double[] parameters = new double[model.Parameters.Count];
            parameters[^1] = sigma;
            double[] residuals = ReadDoubleArray(testCase.GetProperty("residuals"));
            double[] expectedPointwise = residuals
                .Select(residual => IndependentGaussianLogDensity(residual, sigma))
                .ToArray();
            double expectedData = testCase.GetProperty("expected_data_log_likelihood").GetDouble();
            double expectedPrior = testCase.GetProperty("expected_prior_log_likelihood").GetDouble();

            AssertArrayEqual(expectedPointwise, model.PointwiseDataLogLikelihood(parameters), tolerance, modelName);
            Assert.AreEqual(expectedData, expectedPointwise.Sum(), tolerance, $"{modelName} oracle total");
            Assert.AreEqual(expectedData, model.DataLogLikelihood(parameters), tolerance, modelName);
            Assert.AreEqual(
                expectedData,
                model.PointwiseDataLogLikelihoodComponents(parameters).Sum(component => component.LogLikelihood),
                tolerance,
                modelName);
            Assert.AreEqual(expectedPrior, model.PriorLogLikelihood(parameters), tolerance, modelName);
            Assert.AreEqual(expectedPriorFromFormula, expectedPrior, tolerance, $"{modelName} prior oracle");
            Assert.AreEqual(
                expectedPrior,
                model.PointwisePriorLogLikelihood(parameters).Sum(component => component.LogLikelihood),
                tolerance,
                modelName);

            foreach (double invalidScale in ReadInvalidScales(oracle.GetProperty("invalid_scales")))
            {
                double[] invalid = (double[])parameters.Clone();
                invalid[^1] = invalidScale;
                Assert.AreEqual(double.NegativeInfinity, model.DataLogLikelihood(invalid), modelName);
                Assert.AreEqual(double.NegativeInfinity, model.PriorLogLikelihood(invalid), modelName);
                Assert.IsTrue(model.PointwiseDataLogLikelihood(invalid)
                    .All(value => value == double.NegativeInfinity), modelName);
                Assert.IsTrue(model.PointwiseDataLogLikelihoodComponents(invalid)
                    .All(component => component.LogLikelihood == double.NegativeInfinity), modelName);
                Assert.AreEqual(
                    double.NegativeInfinity,
                    model.PointwisePriorLogLikelihood(invalid).Sum(component => component.LogLikelihood),
                    modelName);
            }
        }
    }

    /// <summary>
    /// Compares one model's pointwise Jeffreys component with the analytical oracle.
    /// </summary>
    /// <param name="model">The time-series model under verification.</param>
    /// <param name="sigma">The fixed positive innovation scale.</param>
    /// <param name="expected">The independently calculated log density.</param>
    /// <param name="tolerance">The fixed absolute acceptance tolerance.</param>
    private static void VerifyJeffreysComponent(
        ModelBase model,
        double sigma,
        double expected,
        double tolerance)
    {
        SetJeffreysRule(model);
        double[] parameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
        parameters[^1] = sigma;
        PriorComponent component = model.PointwisePriorLogLikelihood(parameters)
            .Single(item => item.Type == PriorComponentType.JeffreysScalePrior);

        Assert.AreEqual(expected, component.LogLikelihood, tolerance, model.GetType().Name);
        Assert.AreEqual(-Math.Log(sigma), expected, tolerance, $"Oracle drift for {model.GetType().Name}.");
    }

    /// <summary>
    /// Creates the model named by one committed oracle case.
    /// </summary>
    /// <param name="modelName">The exact model type name stored in the oracle.</param>
    /// <returns>A default time-series model instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unknown oracle model name.</exception>
    private static ModelBase CreateModel(string modelName)
    {
        return modelName switch
        {
            nameof(AutoRegressive) => new AutoRegressive(),
            nameof(MovingAverage) => new MovingAverage(),
            nameof(ARIMA) => new ARIMA(),
            nameof(ARIMAX) => new ARIMAX(),
            _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, "Unknown oracle model."),
        };
    }

    /// <summary>
    /// Loads a committed Phase 5 analytical oracle.
    /// </summary>
    /// <param name="fileName">The oracle file name under the verification-data output folder.</param>
    /// <returns>The root JSON element.</returns>
    private static JsonElement LoadOracle(string fileName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Creates a configured common-data model named by the invalid-scale oracle.
    /// </summary>
    /// <param name="modelName">The exact model type name.</param>
    /// <param name="values">The common raw response values.</param>
    /// <returns>The configured model.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unknown oracle model name.</exception>
    private static ModelBase CreateModel(string modelName, double[] values)
    {
        return modelName switch
        {
            nameof(AutoRegressive) => new AutoRegressive(CreateSeries(values), 1, false),
            nameof(MovingAverage) => new MovingAverage(CreateSeries(values), 1, false),
            nameof(ARIMA) => new ARIMA(CreateSeries(values), 1, 0, 0, false),
            nameof(ARIMAX) => CreateArimax(values),
            _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, "Unknown oracle model."),
        };
    }

    /// <summary>
    /// Creates the configured ARIMAX oracle model.
    /// </summary>
    /// <param name="values">The common response values.</param>
    /// <returns>An ARIMAX(1,0,0,0) model without an intercept.</returns>
    private static ARIMAX CreateArimax(double[] values)
    {
        return new ARIMAX(CreateSeries(values))
        {
            IncludeIntercept = false,
            AROrderP = 1,
            DiffOrderD = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            TransformType = Transform.None,
        };
    }

    /// <summary>
    /// Creates an annual time series containing the supplied values.
    /// </summary>
    /// <param name="values">The response values.</param>
    /// <returns>The constructed time series.</returns>
    private static Numerics.Data.TimeSeries CreateSeries(double[] values)
    {
        var series = new Numerics.Data.TimeSeries(
            Numerics.Data.TimeInterval.OneYear,
            new DateTime(2000, 1, 1),
            new DateTime(2000 + values.Length - 1, 1, 1));
        for (int i = 0; i < values.Length; i++)
            series[i].Value = values[i];
        return series;
    }

    /// <summary>
    /// Evaluates the Gaussian log-density formula independently of Numerics distributions.
    /// </summary>
    /// <param name="residual">The model residual.</param>
    /// <param name="sigma">The positive innovation scale.</param>
    /// <returns>The analytical Gaussian log density.</returns>
    private static double IndependentGaussianLogDensity(double residual, double sigma)
    {
        return -0.5 * Math.Log(2 * Math.PI)
            - Math.Log(sigma)
            - residual * residual / (2 * sigma * sigma);
    }

    /// <summary>
    /// Reads a JSON array of doubles.
    /// </summary>
    /// <param name="element">The JSON array.</param>
    /// <returns>The numeric values.</returns>
    private static double[] ReadDoubleArray(JsonElement element)
    {
        return element.EnumerateArray().Select(item => item.GetDouble()).ToArray();
    }

    /// <summary>
    /// Reads numeric and named non-finite scale cases from the committed oracle.
    /// </summary>
    /// <param name="element">The invalid-scale JSON array.</param>
    /// <returns>The invalid scale values.</returns>
    /// <exception cref="InvalidDataException">Thrown for an unknown named scale.</exception>
    private static double[] ReadInvalidScales(JsonElement element)
    {
        return element.EnumerateArray().Select(item => item.ValueKind switch
        {
            JsonValueKind.Number => item.GetDouble(),
            JsonValueKind.String when item.GetString() == "NaN" => double.NaN,
            JsonValueKind.String when item.GetString() == "PositiveInfinity" => double.PositiveInfinity,
            JsonValueKind.String when item.GetString() == "NegativeInfinity" => double.NegativeInfinity,
            _ => throw new InvalidDataException($"Unknown invalid-scale case: {item}."),
        }).ToArray();
    }

    /// <summary>
    /// Compares two numeric arrays using one absolute tolerance.
    /// </summary>
    /// <param name="expected">The independent expected values.</param>
    /// <param name="actual">The production values.</param>
    /// <param name="tolerance">The absolute tolerance.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertArrayEqual(double[] expected, double[] actual, double tolerance, string context)
    {
        Assert.AreEqual(expected.Length, actual.Length, context);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], tolerance, $"{context}, index {i}");
    }

    /// <summary>
    /// Enables the Jeffreys scale-prior contribution for a supported time-series model.
    /// </summary>
    /// <param name="model">The time-series model under verification.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unsupported model type.</exception>
    private static void SetJeffreysRule(ModelBase model)
    {
        switch (model)
        {
            case AutoRegressive autoRegressive:
                autoRegressive.UseJeffreysRuleForScale = true;
                break;
            case MovingAverage movingAverage:
                movingAverage.UseJeffreysRuleForScale = true;
                break;
            case ARIMA arima:
                arima.UseJeffreysRuleForScale = true;
                break;
            case ARIMAX arimax:
                arimax.UseJeffreysRuleForScale = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported time-series model.");
        }
    }
}
