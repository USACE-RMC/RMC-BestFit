using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Regression tests for time-series pointwise prior metadata.
/// </summary>
/// <remarks>
/// These deterministic tests keep the scalar prior value and its decomposed diagnostic
/// representation synchronized without invoking estimation.
/// </remarks>
[TestClass]
public class TimeSeriesPriorMetadataTests
{
    /// <summary>
    /// Verifies that each time-series model exposes exactly one correctly identified Jeffreys
    /// scale component when enabled and none when disabled.
    /// </summary>
    [TestMethod]
    public void PointwisePriorMetadata_ClassifiesExactlyOneJeffreysScaleComponentWhenEnabled()
    {
        foreach (ModelBase model in CreateModels())
        {
            SetJeffreysRule(model, true);
            double[] parameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
            double sigma = parameters[^1];

            List<PriorComponent> components = model.PointwisePriorLogLikelihood(parameters);
            PriorComponent[] jeffreys = components
                .Where(component => component.Type == PriorComponentType.JeffreysScalePrior)
                .ToArray();

            Assert.AreEqual(1, jeffreys.Length, model.GetType().Name);
            StringAssert.Contains(jeffreys[0].Name, "σ", model.GetType().Name);
            Assert.AreEqual(-Math.Log(sigma), jeffreys[0].LogLikelihood, 1E-12, model.GetType().Name);
            Assert.AreEqual(model.Parameters.Count, components
                .Count(component => component.Type == PriorComponentType.ParameterPrior), model.GetType().Name);

            SetJeffreysRule(model, false);
            components = model.PointwisePriorLogLikelihood(parameters);
            Assert.IsFalse(
                components.Any(component => component.Type == PriorComponentType.JeffreysScalePrior),
                model.GetType().Name);
        }
    }

    /// <summary>
    /// Verifies that the decomposed prior contributions sum to the scalar prior likelihood for
    /// valid default parameter vectors in every time-series model.
    /// </summary>
    [TestMethod]
    public void PointwisePriorMetadata_SumsToScalarPriorLikelihood()
    {
        foreach (ModelBase model in CreateModels())
        {
            SetJeffreysRule(model, true);
            double[] parameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
            double pointwise = model.PointwisePriorLogLikelihood(parameters)
                .Sum(component => component.LogLikelihood);

            Assert.AreEqual(model.PriorLogLikelihood(parameters), pointwise, 1E-12, model.GetType().Name);
        }
    }

    /// <summary>
    /// Pins ARIMAX as the established reference implementation for Jeffreys scale metadata.
    /// </summary>
    [TestMethod]
    public void ARIMAX_JeffreysScaleMetadata_RemainsEstablishedReference()
    {
        var model = new ARIMAX { UseJeffreysRuleForScale = true };
        double[] parameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
        PriorComponent component = model.PointwisePriorLogLikelihood(parameters)
            .Single(item => item.Type == PriorComponentType.JeffreysScalePrior);

        Assert.AreEqual("Jeffreys Scale: σ", component.Name);
        Assert.AreEqual(-Math.Log(parameters[^1]), component.LogLikelihood, 1E-12);
    }

    /// <summary>
    /// Creates one default instance of each Phase 5 time-series model.
    /// </summary>
    /// <returns>The four models covered by the Jeffreys metadata contract.</returns>
    private static ModelBase[] CreateModels()
    {
        return
        [
            new AutoRegressive(),
            new MovingAverage(),
            new ARIMA(),
            new ARIMAX(),
        ];
    }

    /// <summary>
    /// Sets the model-specific Jeffreys scale-prior switch.
    /// </summary>
    /// <param name="model">The time-series model.</param>
    /// <param name="enabled">Whether the Jeffreys scale contribution is enabled.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unsupported model type.</exception>
    private static void SetJeffreysRule(ModelBase model, bool enabled)
    {
        switch (model)
        {
            case AutoRegressive autoRegressive:
                autoRegressive.UseJeffreysRuleForScale = enabled;
                break;
            case MovingAverage movingAverage:
                movingAverage.UseJeffreysRuleForScale = enabled;
                break;
            case ARIMA arima:
                arima.UseJeffreysRuleForScale = enabled;
                break;
            case ARIMAX arimax:
                arimax.UseJeffreysRuleForScale = enabled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported time-series model.");
        }
    }
}
