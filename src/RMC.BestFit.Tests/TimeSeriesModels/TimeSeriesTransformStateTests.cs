using System.ComponentModel;
using System.Xml.Linq;
using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using BestFitTransform = RMC.BestFit.Models.Transform;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Regression tests for the atomic time-series transformation-state lifecycle.
/// </summary>
/// <remarks>
/// These tests use restored or manually assigned exponents so they remain deterministic fast
/// contracts. Cross-language fitting and likelihood checks live in the Verification project.
/// </remarks>
[TestClass]
public class TimeSeriesTransformStateTests
{
    private const double StateTolerance = 1E-12;
    private static readonly string[] s_modelNames =
    {
        nameof(AutoRegressive),
        nameof(MovingAverage),
        nameof(ARIMA),
        nameof(ARIMAX),
    };

    /// <summary>
    /// Verifies the getter is read-only and hidden from property-grid discovery.
    /// </summary>
    [TestMethod]
    public void TransformLambda_IsReadOnlyAndNotBrowsable()
    {
        foreach (Type type in new[] { typeof(AutoRegressive), typeof(MovingAverage), typeof(ARIMA), typeof(ARIMAX) })
        {
            var property = type.GetProperty(nameof(AutoRegressive.TransformLambda));
            Assert.IsNotNull(property, type.Name);
            Assert.IsTrue(property.CanRead, type.Name);
            Assert.IsFalse(property.CanWrite, type.Name);
            var browsable = property.GetCustomAttributes(typeof(BrowsableAttribute), true)
                .Cast<BrowsableAttribute>()
                .Single();
            Assert.IsFalse(browsable.Browsable, type.Name);
        }
    }

    /// <summary>
    /// Verifies manual assignment rebuilds all dependent model state, notifies once, preserves
    /// custom parameters, and ignores the compatibility placeholder even when it is non-finite.
    /// </summary>
    [TestMethod]
    public void ManualLambda_RebuildsStateAndIgnoresLambda2()
    {
        double[] raw = { -3.5, -1.7, -0.4, 0.2, 1.1, 2.4, 4.0, 6.2, -30.0, 40.0 };

        foreach (string modelName in s_modelNames)
        {
            ModelBase model = CreateManualModel(modelName, raw, BestFitTransform.YeoJohnson, 0.2, 8);
            double[] before = GetTrainingValues(model);
            int lambdaNotifications = 0;
            model.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AutoRegressive.TransformLambda))
                    lambdaNotifications++;
            };

            SetTransformParameters(model, 0.6, double.NaN);

            Assert.AreEqual(0.6, GetTransformLambda(model), StateTolerance, modelName);
            Assert.AreEqual(1, lambdaNotifications, modelName);
            Assert.IsFalse(before.SequenceEqual(GetTrainingValues(model)), modelName);
            Assert.AreEqual(
                GetTrainingSeries(model).StandardDeviation(),
                model.Parameters[^1].Value,
                StateTolerance,
                $"{modelName} default scale");

            ModelBase comparison = CreateManualModel(modelName, raw, BestFitTransform.YeoJohnson, 0.6, 8, lambda2: double.PositiveInfinity);
            AssertArrayEqual(GetTrainingValues(model), GetTrainingValues(comparison), modelName);
            Assert.AreEqual(
                model.DataLogLikelihood(CreateFixedParameters(model)),
                comparison.DataLogLikelihood(CreateFixedParameters(comparison)),
                StateTolerance,
                modelName);

            model.UseDefaultFlatPriors = false;
            double[] customParameters = CreateFixedParameters(model);
            model.SetParameterValues(customParameters);
            SetTransformParameters(model, 0.8, double.NegativeInfinity);
            AssertArrayEqual(customParameters, model.Parameters.Select(parameter => parameter.Value).ToArray(), $"{modelName} custom parameters");
        }
    }

    /// <summary>
    /// Verifies holdout values cannot change frozen training state for either supported fitted
    /// transform family across all four model types.
    /// </summary>
    [TestMethod]
    public void FrozenLambda_HoldoutMutationCannotChangeTrainingState()
    {
        VerifyHoldoutIsolation(
            BestFitTransform.BoxCox,
            -0.35,
            new[] { 1.1, 1.3, 1.8, 2.7, 5.0, 12.0, 18.0, 31.0, 54.0, 80.0 },
            new[] { 1.1, 1.3, 1.8, 2.7, 5.0, 12.0, 18.0, 31.0, 5400.0, 0.031 });
        VerifyHoldoutIsolation(
            BestFitTransform.YeoJohnson,
            0.6,
            new[] { -4.0, -2.0, -0.5, 0.5, 2.0, 4.0, -3.0, 6.0, -12.0, 20.0 },
            new[] { -4.0, -2.0, -0.5, 0.5, 2.0, 4.0, -3.0, 6.0, -1200.0, 900.0 });
    }

    /// <summary>
    /// Verifies manual state and fitted provenance survive XML and cloning, while manual state
    /// remains fixed when the training window changes.
    /// </summary>
    [TestMethod]
    public void TransformState_XmlCloneAndTrainingWindowRoundTrips()
    {
        double[] raw = { 1.1, 1.3, 1.8, 2.7, 5.0, 12.0, 18.0, 31.0, 54.0, 80.0 };

        foreach (string modelName in s_modelNames)
        {
            ModelBase original = CreateManualModel(modelName, raw, BestFitTransform.BoxCox, -0.35, 8);
            XElement saved = original.ToXElement();
            Assert.AreEqual("-0.35", saved.Attribute(nameof(AutoRegressive.TransformLambda))?.Value, modelName);
            Assert.AreEqual("True", saved.Attribute("TransformLambdaIsManual")?.Value, modelName);

            ModelBase restored = RestoreModel(modelName, CreateSeries(raw), saved);
            Assert.AreEqual(-0.35, GetTransformLambda(restored), StateTolerance, modelName);
            SetTrainingSteps(restored, 7);
            Assert.AreEqual(-0.35, GetTransformLambda(restored), StateTolerance, $"{modelName} manual training change");

            ModelBase clone = (ModelBase)original.Clone();
            Assert.AreEqual(-0.35, GetTransformLambda(clone), StateTolerance, $"{modelName} clone");
            AssertArrayEqual(GetTrainingValues(original), GetTrainingValues(clone), $"{modelName} clone training");

            XElement fittedState = new(saved);
            fittedState.SetAttributeValue("TransformLambdaIsManual", false);
            ModelBase restoredFitted = RestoreModel(modelName, CreateSeries(raw), fittedState);
            Assert.AreEqual(-0.35, GetTransformLambda(restoredFitted), StateTolerance, $"{modelName} restored fitted value");
            Assert.AreEqual("False", restoredFitted.ToXElement().Attribute("TransformLambdaIsManual")?.Value, modelName);
        }
    }

    /// <summary>
    /// Verifies none/log transforms canonicalize lambda, discard manual intent, reject non-finite
    /// primary exponents, and leave the secondary compatibility value uninspected.
    /// </summary>
    [TestMethod]
    public void TransformLambda_CanonicalizationAndValidationAreConsistent()
    {
        foreach (string modelName in s_modelNames)
        {
            ModelBase model = CreateEmptyModel(modelName);
            SetTransformType(model, BestFitTransform.BoxCox);
            SetTransformParameters(model, 0.4, double.NaN);
            Assert.AreEqual(0.4, GetTransformLambda(model), StateTolerance, modelName);

            SetTransformType(model, BestFitTransform.None);
            Assert.AreEqual(0.0, GetTransformLambda(model), StateTolerance, modelName);
            SetTransformParameters(model, 2.5, double.PositiveInfinity);
            Assert.AreEqual(0.0, GetTransformLambda(model), StateTolerance, modelName);

            SetTransformType(model, BestFitTransform.Logarithmic);
            Assert.AreEqual(0.0, GetTransformLambda(model), StateTolerance, modelName);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => SetTransformParameters(model, double.NaN, 0.0), modelName);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => SetTransformParameters(model, double.PositiveInfinity, 0.0), modelName);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => SetTransformParameters(model, double.NegativeInfinity, 0.0), modelName);
        }
    }

    /// <summary>
    /// Verifies differenced series, likelihood/default state, and analysis results are invalidated
    /// when a manual exponent changes.
    /// </summary>
    [TestMethod]
    public void ManualLambda_RebuildsDifferencesAndClearsAnalysisState()
    {
        double[] raw = { -3.5, -1.7, -0.4, 0.2, 1.1, 2.4, 4.0, 6.2, 7.0, 8.0 };

        foreach (string modelName in new[] { nameof(ARIMA), nameof(ARIMAX) })
        {
            ModelBase model = CreateManualModel(modelName, raw, BestFitTransform.YeoJohnson, 0.2, 8, differencingOrder: 1);
            double[] before = GetDifferenceValues(model);
            double likelihoodBefore = model.DataLogLikelihood(CreateFixedParameters(model));
            AnalysisBase analysis = CreateEstimatedAnalysis(model);
            Assert.IsTrue(analysis.IsEstimated, modelName);

            SetTransformParameters(model, 0.6, double.NaN);

            double[] after = GetDifferenceValues(model);
            Assert.IsFalse(before.SequenceEqual(after), modelName);
            double expectedFirst = Numerics.Data.Statistics.YeoJohnson.Transform(raw[1], 0.6)
                - Numerics.Data.Statistics.YeoJohnson.Transform(raw[0], 0.6);
            Assert.AreEqual(expectedFirst, after[0], StateTolerance, modelName);
            Assert.AreNotEqual(likelihoodBefore, model.DataLogLikelihood(CreateFixedParameters(model)), modelName);
            Assert.IsFalse(analysis.IsEstimated, modelName);
        }
    }

    /// <summary>
    /// Runs the holdout-isolation contract for all four models.
    /// </summary>
    /// <param name="transform">The manually frozen transform.</param>
    /// <param name="lambda">The fixed exponent.</param>
    /// <param name="baselineRaw">The baseline response.</param>
    /// <param name="mutatedRaw">The response with an altered holdout tail.</param>
    private static void VerifyHoldoutIsolation(BestFitTransform transform, double lambda, double[] baselineRaw, double[] mutatedRaw)
    {
        foreach (string modelName in s_modelNames)
        {
            ModelBase baseline = CreateManualModel(modelName, baselineRaw, transform, lambda, 8);
            ModelBase mutated = CreateManualModel(modelName, mutatedRaw, transform, lambda, 8, lambda2: double.NaN);
            Assert.AreEqual(lambda, GetTransformLambda(baseline), StateTolerance, modelName);
            Assert.AreEqual(lambda, GetTransformLambda(mutated), StateTolerance, modelName);
            AssertArrayEqual(GetTrainingValues(baseline), GetTrainingValues(mutated), modelName);
            Assert.AreEqual(
                baseline.DataLogLikelihood(CreateFixedParameters(baseline)),
                mutated.DataLogLikelihood(CreateFixedParameters(mutated)),
                StateTolerance,
                modelName);
        }
    }

    /// <summary>
    /// Creates a manually transformed configured model without invoking automatic lambda fitting.
    /// </summary>
    /// <param name="modelName">The model type name.</param>
    /// <param name="raw">The raw response.</param>
    /// <param name="transform">The transform type.</param>
    /// <param name="lambda">The manual exponent.</param>
    /// <param name="trainingSteps">The raw training boundary.</param>
    /// <param name="lambda2">The ignored compatibility value.</param>
    /// <param name="differencingOrder">The ARIMA/ARIMAX differencing order.</param>
    /// <returns>The configured model.</returns>
    private static ModelBase CreateManualModel(
        string modelName,
        double[] raw,
        BestFitTransform transform,
        double lambda,
        int trainingSteps,
        double lambda2 = 0.0,
        int differencingOrder = 0)
    {
        ModelBase model = CreateEmptyModel(modelName, differencingOrder);
        SetTransformType(model, transform);
        SetTransformParameters(model, lambda, lambda2);
        SetTimeSeries(model, CreateSeries(raw));
        SetUseDefaultTrainingSteps(model, false);
        SetTrainingSteps(model, trainingSteps);
        return model;
    }

    /// <summary>
    /// Creates an empty model with a common one-lag, no-intercept parameter layout.
    /// </summary>
    /// <param name="modelName">The model type name.</param>
    /// <param name="differencingOrder">The ARIMA/ARIMAX differencing order.</param>
    /// <returns>The empty model.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model name is unknown.</exception>
    private static ModelBase CreateEmptyModel(string modelName, int differencingOrder = 0)
    {
        return modelName switch
        {
            nameof(AutoRegressive) => new AutoRegressive { Order = 1, IncludeIntercept = false },
            nameof(MovingAverage) => new MovingAverage { Order = 1, IncludeIntercept = false },
            nameof(ARIMA) => new ARIMA { POrder = 1, DOrder = differencingOrder, QOrder = 0, IncludeIntercept = false },
            nameof(ARIMAX) => new ARIMAX
            {
                AROrderP = 1,
                DiffOrderD = differencingOrder,
                MAOrderQ = 0,
                XOrderB = 0,
                IncludeIntercept = false,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, "Unknown model."),
        };
    }

    /// <summary>
    /// Creates an annual series from raw ordinates.
    /// </summary>
    /// <param name="raw">The raw values.</param>
    /// <returns>The time series.</returns>
    private static NumericsTimeSeries CreateSeries(double[] raw)
    {
        return new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(2000, 1, 1), raw);
    }

    /// <summary>
    /// Restores a model from its XML representation.
    /// </summary>
    /// <param name="modelName">The model type name.</param>
    /// <param name="series">The response series.</param>
    /// <param name="xml">The serialized model.</param>
    /// <returns>The restored model.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model name is unknown.</exception>
    private static ModelBase RestoreModel(string modelName, NumericsTimeSeries series, XElement xml)
    {
        return modelName switch
        {
            nameof(AutoRegressive) => new AutoRegressive(series, xml),
            nameof(MovingAverage) => new MovingAverage(series, xml),
            nameof(ARIMA) => new ARIMA(series, xml),
            nameof(ARIMAX) => new ARIMAX(series, xml),
            _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, "Unknown model."),
        };
    }

    /// <summary>
    /// Creates an analysis restored as estimated without running a sampler.
    /// </summary>
    /// <param name="model">The configured model.</param>
    /// <returns>The estimated-state analysis.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static AnalysisBase CreateEstimatedAnalysis(ModelBase model)
    {
        var xml = new XElement("Analysis", new XAttribute("IsEstimated", true));
        return model switch
        {
            AutoRegressive ar => new ARAnalysis(ar, xml),
            MovingAverage ma => new MAAnalysis(ma, xml),
            ARIMA arima => new ARIMAAnalysis(arima, xml),
            ARIMAX arimax => new ARIMAXAnalysis(arimax, xml),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model."),
        };
    }

    /// <summary>
    /// Returns fixed valid parameters for the common one-lag, no-intercept layout.
    /// </summary>
    /// <param name="model">The configured model.</param>
    /// <returns>The parameter vector.</returns>
    private static double[] CreateFixedParameters(ModelBase model)
    {
        var parameters = new double[model.Parameters.Count];
        if (parameters.Length > 1)
            parameters[0] = 0.35;
        parameters[^1] = 0.8;
        return parameters;
    }

    /// <summary>
    /// Gets the model training series.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The training series.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static NumericsTimeSeries GetTrainingSeries(ModelBase model)
    {
        return model switch
        {
            AutoRegressive ar => ar.TrainingTimeSeries,
            MovingAverage ma => ma.TrainingTimeSeries,
            ARIMA arima => arima.TrainingTimeSeries,
            ARIMAX arimax => arimax.TrainingTimeSeries,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model."),
        };
    }

    /// <summary>
    /// Gets the model training values.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The training values.</returns>
    private static double[] GetTrainingValues(ModelBase model) => GetTrainingSeries(model).ValuesToArray();

    /// <summary>
    /// Gets the differenced model values.
    /// </summary>
    /// <param name="model">The ARIMA or ARIMAX model.</param>
    /// <returns>The differenced values.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static double[] GetDifferenceValues(ModelBase model)
    {
        return model switch
        {
            ARIMA arima => arima.DifferencedSeries.ValuesToArray(),
            ARIMAX arimax => arimax.DifferencedSeries.ValuesToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model."),
        };
    }

    /// <summary>
    /// Gets the effective transform exponent.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The exponent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static double GetTransformLambda(ModelBase model)
    {
        return model switch
        {
            AutoRegressive ar => ar.TransformLambda,
            MovingAverage ma => ma.TransformLambda,
            ARIMA arima => arima.TransformLambda,
            ARIMAX arimax => arimax.TransformLambda,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model."),
        };
    }

    /// <summary>
    /// Assigns the transform type.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="transform">The transform.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetTransformType(ModelBase model, BestFitTransform transform)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.TransformType = transform; break;
            case MovingAverage ma: ma.TransformType = transform; break;
            case ARIMA arima: arima.TransformType = transform; break;
            case ARIMAX arimax: arimax.TransformType = transform; break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Assigns manual transform parameters.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="lambda1">The effective exponent.</param>
    /// <param name="lambda2">The ignored compatibility value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetTransformParameters(ModelBase model, double lambda1, double lambda2)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.SetTransformParameters(lambda1, lambda2); break;
            case MovingAverage ma: ma.SetTransformParameters(lambda1, lambda2); break;
            case ARIMA arima: arima.SetTransformParameters(lambda1, lambda2); break;
            case ARIMAX arimax: arimax.SetTransformParameters(lambda1, lambda2); break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Assigns the response series.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="series">The response series.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetTimeSeries(ModelBase model, NumericsTimeSeries series)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.TimeSeries = series; break;
            case MovingAverage ma: ma.TimeSeries = series; break;
            case ARIMA arima: arima.TimeSeries = series; break;
            case ARIMAX arimax: arimax.TimeSeries = series; break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Enables or disables default training-window management.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="value">The requested state.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetUseDefaultTrainingSteps(ModelBase model, bool value)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.UseDefaultTrainingSteps = value; break;
            case MovingAverage ma: ma.UseDefaultTrainingSteps = value; break;
            case ARIMA arima: arima.UseDefaultTrainingSteps = value; break;
            case ARIMAX arimax: arimax.UseDefaultTrainingSteps = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Assigns the raw training boundary.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="value">The training boundary.</param>
    /// <exception cref="ArgumentOutOfRangeException">The model type is unsupported.</exception>
    private static void SetTrainingSteps(ModelBase model, int value)
    {
        switch (model)
        {
            case AutoRegressive ar: ar.TrainingTimeSteps = value; break;
            case MovingAverage ma: ma.TrainingTimeSteps = value; break;
            case ARIMA arima: arima.TrainingTimeSteps = value; break;
            case ARIMAX arimax: arimax.TrainingTimeSteps = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, "Unsupported model.");
        }
    }

    /// <summary>
    /// Compares two deterministic state vectors.
    /// </summary>
    /// <param name="expected">The expected values.</param>
    /// <param name="actual">The actual values.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertArrayEqual(double[] expected, double[] actual, string context)
    {
        Assert.AreEqual(expected.Length, actual.Length, context);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], StateTolerance, $"{context}, index {i}");
    }
}
