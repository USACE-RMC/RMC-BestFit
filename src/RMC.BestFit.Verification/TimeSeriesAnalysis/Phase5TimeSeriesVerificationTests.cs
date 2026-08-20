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
        JsonElement oracle = LoadOracle();
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
    /// Loads the committed analytical Jeffreys scale-prior oracle.
    /// </summary>
    /// <returns>The root JSON element.</returns>
    private static JsonElement LoadOracle()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            "phase5-jeffreys-prior-oracle.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
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
