using Numerics.Data;
using Numerics.Data.Statistics;
using RMC.BestFit.Models;
using ModelTransform = RMC.BestFit.Models.Transform;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Tests that the pointwise data log-likelihood of a transformed time-series model attributes
/// each observation's own change-of-variables term to that observation.
/// </summary>
/// <remarks>
/// Each transformed model is compared with an identical model attached to the already
/// transformed data without a transform. The Gaussian terms of the two models coincide, so the
/// pointwise difference at every evaluated step must equal the log-Jacobian term of the raw
/// observation at that step's raw index, and the pointwise sum must equal the scalar
/// data log-likelihood.
/// </remarks>
[TestClass]
public class TimeSeriesPointwiseJacobianTests
{
    private const double Tolerance = 1E-12;
    private static readonly DateTime s_startDate = new(2002, 3, 4);
    private static readonly double[] s_raw = { 12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5 };
    private static readonly double[] s_covariate = { 0.4, -1.2, 0.8, 1.5, -0.3, 0.9, 2.1, -0.7, 1.1, 0.2, -1.6, 0.6 };

    /// <summary>
    /// Verifies the logarithmic transform attributes -log(y) to each evaluated observation for
    /// AR, MA, ARIMA and ARIMAX models at their raw-index alignment.
    /// </summary>
    [TestMethod]
    public void LogarithmicTransform_AttributesEachObservationsJacobianTerm()
    {
        double[] transformed = s_raw.Select(value => Math.Log(value)).ToArray();

        AssertPointwiseAttribution(
            CreateAutoRegressive(s_raw, ModelTransform.Logarithmic),
            CreateAutoRegressive(transformed, ModelTransform.None),
            new[] { 0.1, 0.4, 0.3 },
            firstRawIndex: 1,
            rawIndex => -Math.Log(s_raw[rawIndex]),
            "AR(1) log");

        AssertPointwiseAttribution(
            CreateMovingAverage(s_raw, ModelTransform.Logarithmic),
            CreateMovingAverage(transformed, ModelTransform.None),
            new[] { 0.1, -0.2, 0.3 },
            firstRawIndex: 0,
            rawIndex => -Math.Log(s_raw[rawIndex]),
            "MA(1) log");

        AssertPointwiseAttribution(
            CreateArima(s_raw, ModelTransform.Logarithmic),
            CreateArima(transformed, ModelTransform.None),
            new[] { 0.05, 0.4, -0.2, 0.3 },
            firstRawIndex: 2,
            rawIndex => -Math.Log(s_raw[rawIndex]),
            "ARIMA(1,1,1) log");

        AssertPointwiseAttribution(
            CreateArimax(s_raw, ModelTransform.Logarithmic),
            CreateArimax(transformed, ModelTransform.None),
            new[] { 0.05, 0.2, 0.4, 0.3 },
            firstRawIndex: 2,
            rawIndex => -Math.Log(s_raw[rawIndex]),
            "ARIMAX(1,1,0) log");
    }

    /// <summary>
    /// Verifies a fixed-exponent Box-Cox transform attributes (lambda - 1) log(y) to each evaluated
    /// observation.
    /// </summary>
    [TestMethod]
    public void BoxCoxTransform_AttributesEachObservationsJacobianTerm()
    {
        const double lambda = 0.5;
        double[] transformed = s_raw.Select(value => BoxCox.Transform(value, lambda)).ToArray();

        ARIMA model = CreateArima(s_raw, ModelTransform.BoxCox);
        model.SetTransformParameters(lambda, double.NaN);

        AssertPointwiseAttribution(
            model,
            CreateArima(transformed, ModelTransform.None),
            new[] { 0.05, 0.4, -0.2, 0.3 },
            firstRawIndex: 2,
            rawIndex => (lambda - 1.0) * Math.Log(s_raw[rawIndex]),
            "ARIMA(1,1,1) Box-Cox");
    }

    /// <summary>
    /// Verifies a fixed-exponent Yeo-Johnson transform attributes log|dT/dy| to each evaluated
    /// observation.
    /// </summary>
    [TestMethod]
    public void YeoJohnsonTransform_AttributesEachObservationsJacobianTerm()
    {
        const double lambda = 0.3;
        double[] transformed = s_raw.Select(value => YeoJohnson.Transform(value, lambda)).ToArray();

        AutoRegressive model = CreateAutoRegressive(s_raw, ModelTransform.YeoJohnson);
        model.SetTransformParameters(lambda, double.NaN);

        AssertPointwiseAttribution(
            model,
            CreateAutoRegressive(transformed, ModelTransform.None),
            new[] { 0.1, 0.4, 0.3 },
            firstRawIndex: 1,
            rawIndex => (lambda - 1.0) * Math.Log(s_raw[rawIndex] + 1.0),
            "AR(1) Yeo-Johnson");
    }

    /// <summary>
    /// Asserts the pointwise terms of a transformed model equal those of the untransformed model
    /// plus each observation's own Jacobian term, and that they sum to the scalar log-likelihood.
    /// </summary>
    /// <param name="transformedModel">The model attached to raw data with a transform.</param>
    /// <param name="plainModel">The identical model attached to the transformed data without a transform.</param>
    /// <param name="parameters">The parameter vector.</param>
    /// <param name="firstRawIndex">The raw index of the first evaluated model step.</param>
    /// <param name="jacobianTerm">The expected log-Jacobian term for a raw index.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertPointwiseAttribution(
        ModelBase transformedModel,
        ModelBase plainModel,
        double[] parameters,
        int firstRawIndex,
        Func<int, double> jacobianTerm,
        string context)
    {
        double[] pointwise = transformedModel.PointwiseDataLogLikelihood(parameters);
        double[] plain = plainModel.PointwiseDataLogLikelihood(parameters);
        List<DataComponent> components = transformedModel.PointwiseDataLogLikelihoodComponents(parameters);

        Assert.AreEqual(plain.Length, pointwise.Length, $"{context}: evaluated step count");
        Assert.AreEqual(s_raw.Length - firstRawIndex, pointwise.Length, $"{context}: evaluated steps cover raw indices {firstRawIndex}..{s_raw.Length - 1}");
        for (int i = 0; i < pointwise.Length; i++)
        {
            double expected = plain[i] + jacobianTerm(firstRawIndex + i);
            Assert.AreEqual(expected, pointwise[i], Tolerance, $"{context}: step {i}");
            Assert.AreEqual(expected, components[i].LogLikelihood, Tolerance, $"{context}: component {i}");
        }

        double scalar = transformedModel.DataLogLikelihood(parameters);
        Assert.IsTrue(double.IsFinite(scalar), $"{context}: scalar log-likelihood is finite");
        Assert.AreEqual(scalar, pointwise.Sum(), 1E-10, $"{context}: pointwise sum");
    }

    /// <summary>
    /// Creates an AR(1) model with intercept using all data for training.
    /// </summary>
    /// <param name="values">The series values.</param>
    /// <param name="transform">The transform.</param>
    /// <returns>The model.</returns>
    private static AutoRegressive CreateAutoRegressive(double[] values, ModelTransform transform)
    {
        var model = new AutoRegressive(CreateSeries(values), order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = transform,
        };
        model.TrainingTimeSteps = values.Length;
        return model;
    }

    /// <summary>
    /// Creates an MA(1) model with intercept using all data for training.
    /// </summary>
    /// <param name="values">The series values.</param>
    /// <param name="transform">The transform.</param>
    /// <returns>The model.</returns>
    private static MovingAverage CreateMovingAverage(double[] values, ModelTransform transform)
    {
        var model = new MovingAverage(CreateSeries(values), order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = transform,
        };
        model.TrainingTimeSteps = values.Length;
        return model;
    }

    /// <summary>
    /// Creates an ARIMA(1,1,1) model with intercept using all data for training.
    /// </summary>
    /// <param name="values">The series values.</param>
    /// <param name="transform">The transform.</param>
    /// <returns>The model.</returns>
    private static ARIMA CreateArima(double[] values, ModelTransform transform)
    {
        var model = new ARIMA(CreateSeries(values), 1, 1, 1, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = transform,
        };
        model.TrainingTimeSteps = values.Length;
        return model;
    }

    /// <summary>
    /// Creates an ARIMAX(1,1,0) model with intercept and one level covariate using all data for training.
    /// </summary>
    /// <param name="values">The series values.</param>
    /// <param name="transform">The transform.</param>
    /// <returns>The model.</returns>
    private static ARIMAX CreateArimax(double[] values, ModelTransform transform)
    {
        var model = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
            UseDefaultTrainingSteps = false,
            TransformType = transform,
        };
        model.TimeSeries = CreateSeries(values);
        model.TrainingTimeSteps = values.Length;
        model.SetCovariates(new List<NumericTimeSeries> { CreateSeries(s_covariate) });
        return model;
    }

    /// <summary>
    /// Creates a daily series.
    /// </summary>
    /// <param name="values">The ordinate values.</param>
    /// <returns>The series.</returns>
    private static NumericTimeSeries CreateSeries(double[] values) =>
        new(TimeInterval.OneDay, s_startDate, values);
}
