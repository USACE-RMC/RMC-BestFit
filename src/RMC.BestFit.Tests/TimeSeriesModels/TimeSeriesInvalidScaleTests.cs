using Numerics.Data;
using RMC.BestFit.Models;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Regression tests for invalid innovation-scale handling in time-series models.
/// </summary>
[TestClass]
public class TimeSeriesInvalidScaleTests
{
    /// <summary>
    /// Verifies exact negative-infinity rejection across scalar and decomposed data/prior paths
    /// while preserving pointwise lengths and metadata.
    /// </summary>
    /// <param name="invalidScale">The invalid innovation scale under test.</param>
    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void InvalidInnovationScale_ReturnsNegativeInfinityAcrossAllPaths(double invalidScale)
    {
        foreach (ModelBase model in CreateModels())
        {
            SetJeffreysRule(model, true);
            double[] validParameters = CreateParameterVector(model, 1.75);
            double[] validPointwise = model.PointwiseDataLogLikelihood(validParameters);
            List<DataComponent> validComponents = model.PointwiseDataLogLikelihoodComponents(validParameters);
            List<PriorComponent> validPriorComponents = model.PointwisePriorLogLikelihood(validParameters);
            double[] invalidParameters = (double[])validParameters.Clone();
            invalidParameters[^1] = invalidScale;

            Assert.AreEqual(double.NegativeInfinity, model.DataLogLikelihood(invalidParameters), model.GetType().Name);
            Assert.AreEqual(double.NegativeInfinity, model.PriorLogLikelihood(invalidParameters), model.GetType().Name);
            Assert.AreEqual(double.NegativeInfinity, model.LogLikelihood(invalidParameters), model.GetType().Name);

            double[] invalidPointwise = model.PointwiseDataLogLikelihood(invalidParameters);
            Assert.AreEqual(validPointwise.Length, invalidPointwise.Length, model.GetType().Name);
            Assert.IsTrue(invalidPointwise.All(value => value == double.NegativeInfinity), model.GetType().Name);

            List<DataComponent> invalidComponents = model.PointwiseDataLogLikelihoodComponents(invalidParameters);
            Assert.AreEqual(validComponents.Count, invalidComponents.Count, model.GetType().Name);
            for (int i = 0; i < invalidComponents.Count; i++)
            {
                Assert.AreEqual(double.NegativeInfinity, invalidComponents[i].LogLikelihood, model.GetType().Name);
                Assert.AreEqual(validComponents[i].Index, invalidComponents[i].Index, model.GetType().Name);
                Assert.AreEqual(validComponents[i].Value, invalidComponents[i].Value, model.GetType().Name);
                Assert.AreEqual(validComponents[i].Type, invalidComponents[i].Type, model.GetType().Name);
                Assert.AreEqual(validComponents[i].Count, invalidComponents[i].Count, model.GetType().Name);
                Assert.AreEqual(validComponents[i].Name, invalidComponents[i].Name, model.GetType().Name);
            }

            List<PriorComponent> invalidPriorComponents = model.PointwisePriorLogLikelihood(invalidParameters);
            Assert.AreEqual(validPriorComponents.Count, invalidPriorComponents.Count, model.GetType().Name);
            for (int i = 0; i < invalidPriorComponents.Count; i++)
            {
                Assert.AreEqual(validPriorComponents[i].Name, invalidPriorComponents[i].Name, model.GetType().Name);
                Assert.AreEqual(validPriorComponents[i].Type, invalidPriorComponents[i].Type, model.GetType().Name);
            }
            Assert.AreEqual(
                double.NegativeInfinity,
                invalidPriorComponents.Sum(component => component.LogLikelihood),
                model.GetType().Name);
            Assert.AreEqual(
                double.NegativeInfinity,
                invalidPriorComponents[model.Parameters.Count - 1].LogLikelihood,
                model.GetType().Name);
        }
    }

    /// <summary>
    /// Verifies that a representative finite positive scale remains a valid numerical evaluation rather
    /// than being rejected by the finite-positive guard.
    /// </summary>
    [TestMethod]
    public void FinitePositiveInnovationScale_RetainsValidEvaluation()
    {
        foreach (ModelBase model in CreateModels())
        {
            SetJeffreysRule(model, true);
            double[] parameters = CreateParameterVector(model, 0.1);
            double scalar = model.DataLogLikelihood(parameters);
            double pointwise = model.PointwiseDataLogLikelihood(parameters).Sum();

            Assert.IsFalse(double.IsNaN(scalar) || double.IsInfinity(scalar), model.GetType().Name);
            Assert.AreEqual(scalar, pointwise, Math.Max(1E-12, Math.Abs(scalar) * 1E-12), model.GetType().Name);
            Assert.IsFalse(double.IsNaN(model.PriorLogLikelihood(parameters)), model.GetType().Name);
        }
    }

    /// <summary>
    /// Creates one common-data instance of every Phase 5 time-series model.
    /// </summary>
    /// <returns>The four configured time-series models.</returns>
    private static ModelBase[] CreateModels()
    {
        return
        [
            new AutoRegressive(CreateSeries(), 1, false),
            new MovingAverage(CreateSeries(), 1, false),
            new ARIMA(CreateSeries(), 1, 0, 0, false),
            CreateArimax(),
        ];
    }

    /// <summary>
    /// Creates the configured ARIMAX common-data fixture.
    /// </summary>
    /// <returns>An ARIMAX(1,0,0,0) model without an intercept.</returns>
    private static ARIMAX CreateArimax()
    {
        return new ARIMAX(CreateSeries())
        {
            IncludeIntercept = false,
            AROrderP = 1,
            DiffOrderD = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            TransformType = RMC.BestFit.Models.Transform.None,
        };
    }

    /// <summary>
    /// Creates the five-value annual response fixture.
    /// </summary>
    /// <returns>The deterministic response series.</returns>
    private static NumericsTimeSeries CreateSeries()
    {
        var series = new NumericsTimeSeries(
            TimeInterval.OneYear,
            new DateTime(2000, 1, 1),
            new DateTime(2004, 1, 1));
        double[] values = [1.25, -0.5, 2.0, 0.75, -1.5];
        for (int i = 0; i < values.Length; i++)
            series[i].Value = values[i];
        return series;
    }

    /// <summary>
    /// Creates a valid parameter vector with zero dynamic coefficients and the requested scale.
    /// </summary>
    /// <param name="model">The configured model.</param>
    /// <param name="sigma">The innovation scale.</param>
    /// <returns>The parameter vector.</returns>
    private static double[] CreateParameterVector(ModelBase model, double sigma)
    {
        double[] parameters = new double[model.Parameters.Count];
        parameters[^1] = sigma;
        return parameters;
    }

    /// <summary>
    /// Sets the model-specific Jeffreys scale-prior switch.
    /// </summary>
    /// <param name="model">The time-series model.</param>
    /// <param name="enabled">Whether the Jeffreys contribution is enabled.</param>
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
