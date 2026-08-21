using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Models;
using ModelTransform = RMC.BestFit.Models.Transform;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Tests for the conditional evaluation order of the time-series models: the likelihood,
/// residuals, transform Jacobian window and prediction seeding all start at the same model step,
/// degenerate training windows are rejected, and property changes keep the evaluation state
/// consistent.
/// </summary>
[TestClass]
public class TimeSeriesConditioningOrderTests
{
    private const double Tolerance = 1E-12;
    private static readonly DateTime s_startDate = new(2002, 3, 4);

    /// <summary>
    /// Verifies an ARIMAX distributed-lag regression (p = q = 0, b = 2) conditions on the first
    /// b model steps, so every evaluated step includes all lagged covariate terms.
    /// </summary>
    [TestMethod]
    public void ArimaxDistributedLag_ConditionsOnCovariateLagOrder()
    {
        double[] y = { 3.0, 4.5, 2.5, 5.0, 4.0, 6.5, 5.5, 7.0 };
        double[] x = { 1.0, 2.0, 0.5, 1.5, 3.0, 2.5, 1.0, 2.0 };
        const double mu = 1.0, beta0 = 0.8, beta1 = 0.3, beta2 = -0.2, sigma = 1.5;
        double[] parameters = { mu, beta0, beta1, beta2, sigma };
        ARIMAX model = CreateArimax(y, x, differenceOrder: 0, covariateLagOrder: 2, ModelTransform.None, includeIntercept: true);

        double expected = 0.0;
        var expectedPointwise = new List<double>();
        for (int t = 2; t < y.Length; t++)
        {
            double residual = y[t] - (mu + beta0 * x[t] + beta1 * x[t - 1] + beta2 * x[t - 2]);
            double term = NormalLogDensity(residual, sigma);
            expected += term;
            expectedPointwise.Add(term);
        }

        double[] residuals = model.Residuals(parameters);
        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);

        Assert.AreEqual(expected, model.DataLogLikelihood(parameters), Tolerance, "conditional log-likelihood");
        Assert.AreEqual(y.Length, residuals.Length, "residual length");
        Assert.AreEqual(0.0, residuals[0], 0.0, "conditioning step 0 residual");
        Assert.AreEqual(0.0, residuals[1], 0.0, "conditioning step 1 residual");
        CollectionAssert.AreEqual(
            expectedPointwise.Select(value => Math.Round(value, 10)).ToArray(),
            pointwise.Select(value => Math.Round(value, 10)).ToArray(),
            "pointwise log-likelihood");
        Assert.AreEqual(expected, pointwise.Sum(), Tolerance, "pointwise sum");
        Assert.AreEqual(expectedPointwise.Count, components.Count, "component count");
        Assert.AreEqual(expected, components.Sum(component => component.LogLikelihood), Tolerance, "component sum");
    }

    /// <summary>
    /// Verifies the transform Jacobian window of an ARIMAX distributed-lag regression starts at
    /// the covariate lag order, matching the conditional likelihood.
    /// </summary>
    [TestMethod]
    public void ArimaxDistributedLag_LogJacobianStartsAtCovariateLagOrder()
    {
        double[] y = { 2.0, 4.0, 8.0, 4.0, 2.0, 6.0 };
        double[] x = { 1.0, 3.0, 2.0, 5.0, 4.0, 2.0 };
        const double mu = 0.5, beta0 = 0.1, beta1 = 0.2, sigma = 0.7;
        double[] parameters = { mu, beta0, beta1, sigma };
        ARIMAX model = CreateArimax(y, x, differenceOrder: 0, covariateLagOrder: 1, ModelTransform.Logarithmic, includeIntercept: true);

        double expected = 0.0;
        for (int t = 1; t < y.Length; t++)
        {
            double residual = Math.Log(y[t]) - (mu + beta0 * x[t] + beta1 * x[t - 1]);
            expected += NormalLogDensity(residual, sigma) - Math.Log(y[t]);
        }

        Assert.AreEqual(expected, model.DataLogLikelihood(parameters), Tolerance, "log-scale conditional log-likelihood");
        Assert.AreEqual(expected, model.PointwiseDataLogLikelihood(parameters).Sum(), 1E-10, "pointwise sum");
    }

    /// <summary>
    /// Verifies first-difference ARIMAX predictions seed the first b model steps from observed
    /// differences and apply all lagged covariate terms to every predicted step.
    /// </summary>
    [TestMethod]
    public void ArimaxDistributedLag_PredictSeedsCovariateLagStepsForDifferencedModels()
    {
        double[] raw = { 10.0, 14.0, 15.0, 19.0, 999.0 };
        double[] covariate = { 1.0, 2.0, 3.0, 4.0, 5.0 };
        ARIMAX model = CreateArimax(raw, covariate, differenceOrder: 1, covariateLagOrder: 1, ModelTransform.None, includeIntercept: true, trainingSteps: 4);

        var result = model.Predict(new[] { 0.5, 0.2, 0.1, 1.0 }, 1, -1);

        // Model step 0 is an observed difference (4); steps 1-2 add c + 0.2 x_t + 0.1 x_{t-1} to
        // the observed level; the forecast step adds the same mean to the final training level.
        AssertArrayEqual(new[] { 10.0, 14.0, 15.3, 16.6, 20.9 }, result.Y, "d=1, b=1 conditional levels");
        Assert.AreEqual(0.8, result.CovariatePart[2], Tolerance, "covariate part at raw slot 2");
        Assert.AreEqual(1.1, result.CovariatePart[3], Tolerance, "covariate part at raw slot 3");
        Assert.AreEqual(1.4, result.CovariatePart[4], Tolerance, "covariate part at raw slot 4");
    }

    /// <summary>
    /// Verifies undifferenced ARIMAX predictions seed the first b model steps from observations.
    /// </summary>
    [TestMethod]
    public void ArimaxDistributedLag_PredictSeedsCovariateLagStepsForUndifferencedModels()
    {
        double[] raw = { 10.0, 14.0, 15.0, 19.0 };
        double[] covariate = { 1.0, 2.0, 3.0, 4.0 };
        ARIMAX model = CreateArimax(raw, covariate, differenceOrder: 0, covariateLagOrder: 1, ModelTransform.None, includeIntercept: true);

        var result = model.Predict(new[] { 0.5, 0.2, 0.1, 1.0 }, 0, -1);

        AssertArrayEqual(new[] { 10.0, 1.0, 1.3, 1.6 }, result.Y, "d=0, b=1 conditional values");
    }

    /// <summary>
    /// Verifies changing the AR or MA order of an ARIMA model refreshes the transform Jacobian
    /// window, so the data log-likelihood equals that of a freshly constructed model.
    /// </summary>
    [TestMethod]
    public void ArimaOrderChange_RefreshesTransformJacobianWindow()
    {
        double[] raw = { 12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5 };
        double[] parameters = { 0.1, 0.4, -0.2, 0.3 };

        var changed = new ARIMA(CreateSeries(raw), 1, 0, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        changed.TrainingTimeSteps = raw.Length;
        changed.POrder = 2;

        var fresh = new ARIMA(CreateSeries(raw), 2, 0, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        fresh.TrainingTimeSteps = raw.Length;

        Assert.AreEqual(fresh.DataLogLikelihood(parameters), changed.DataLogLikelihood(parameters), Tolerance, "AR order change");

        var changedMa = new ARIMA(CreateSeries(raw), 0, 0, 1, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        changedMa.TrainingTimeSteps = raw.Length;
        changedMa.QOrder = 2;

        var freshMa = new ARIMA(CreateSeries(raw), 0, 0, 2, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        freshMa.TrainingTimeSteps = raw.Length;

        Assert.AreEqual(freshMa.DataLogLikelihood(parameters), changedMa.DataLogLikelihood(parameters), Tolerance, "MA order change");
    }

    /// <summary>
    /// Verifies changing the order of an autoregressive model refreshes the transform Jacobian
    /// window, so the data log-likelihood equals that of a freshly constructed model.
    /// </summary>
    [TestMethod]
    public void AutoRegressiveOrderChange_RefreshesTransformJacobianWindow()
    {
        double[] raw = { 12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5 };
        double[] parameters = { 0.1, 0.4, -0.2, 0.3 };

        var changed = new AutoRegressive(CreateSeries(raw), order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        changed.TrainingTimeSteps = raw.Length;
        changed.Order = 2;

        var fresh = new AutoRegressive(CreateSeries(raw), order: 2, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        fresh.TrainingTimeSteps = raw.Length;

        Assert.AreEqual(fresh.DataLogLikelihood(parameters), changed.DataLogLikelihood(parameters), Tolerance);
    }

    /// <summary>
    /// Verifies a training window that leaves no evaluated model step yields negative infinity
    /// and a validation error instead of a perfect-fit log-likelihood.
    /// </summary>
    [TestMethod]
    public void EmptyConditionalSum_IsNegativeInfinityAndInvalid()
    {
        double[] raw = { 12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5 };

        var arima = new ARIMA(CreateSeries(raw), 1, 1, 0, true) { UseDefaultTrainingSteps = false };
        arima.TrainingTimeSteps = 2;
        Assert.AreEqual(double.NegativeInfinity, arima.DataLogLikelihood(new[] { 0.1, 0.4, 0.3 }), "ARIMA");
        (bool isValid, List<string> messages) = arima.Validate();
        Assert.IsFalse(isValid, "ARIMA validity");
        Assert.IsTrue(messages.Any(message => message.Contains("differenced model steps")), string.Join(" | ", messages));

        var arimax = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
        };
        arimax.TimeSeries = CreateSeries(raw);
        arimax.TrainingTimeSteps = 2;
        Assert.AreEqual(double.NegativeInfinity, arimax.DataLogLikelihood(new[] { 0.1, 0.4, 0.3 }), "ARIMAX");
    }

    /// <summary>
    /// Verifies residuals requested before any data are attached return an empty array.
    /// </summary>
    [TestMethod]
    public void ArimaResiduals_WithoutData_ReturnsEmpty()
    {
        var model = new ARIMA { POrder = 1, IncludeIntercept = true };

        double[] residuals = model.Residuals(new[] { 0.1, 0.4, 0.3 });

        Assert.AreEqual(0, residuals.Length);
    }

    /// <summary>
    /// Verifies a transform change rebuilds default priors only when default flat priors are in
    /// use, matching the transform-parameter setter.
    /// </summary>
    [TestMethod]
    public void TransformTypeChange_PreservesCustomPriors()
    {
        double[] raw = { 12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5 };

        foreach (ModelBase model in CreateModels(raw))
        {
            model.UseDefaultFlatPriors = false;
            var customPrior = new Uniform(0.001, 5.0);
            ModelParameter scale = model.Parameters[^1];
            scale.PriorDistribution = customPrior;

            SetTransformType(model, ModelTransform.Logarithmic);

            Assert.AreSame(scale, model.Parameters[^1], $"{model.GetType().Name} keeps its parameter objects");
            Assert.AreSame(customPrior, model.Parameters[^1].PriorDistribution, $"{model.GetType().Name} keeps its custom prior");

            model.UseDefaultFlatPriors = true;
            SetTransformType(model, ModelTransform.None);

            Assert.AreNotSame(customPrior, model.Parameters[^1].PriorDistribution, $"{model.GetType().Name} restores default priors");
        }
    }

    /// <summary>
    /// Verifies differenced ARIMA and ARIMAX stochastic predictions keep a roughly constant
    /// credible-band width through the training period instead of a random-walk cone.
    /// </summary>
    [TestMethod]
    public void DifferencedPredictions_TrainingBandWidthIsBoundedBySigma()
    {
        double[] raw = CreateRandomWalk(60, 1234);
        double[] parameters = { 0.5, 0.3, -0.2, 30.0 };
        double sigma = parameters[^1];
        const int realizations = 200;

        var arima = new ARIMA(CreateSeries(raw), 1, 1, 1, true) { UseDefaultTrainingSteps = false };
        arima.TrainingTimeSteps = raw.Length;
        AssertBoundedTrainingWidth(seed => arima.Predict(parameters, 0, seed).Y, raw.Length, realizations, sigma, "ARIMA");

        var arimax = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 1,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
        };
        arimax.TimeSeries = CreateSeries(raw);
        arimax.TrainingTimeSteps = raw.Length;
        AssertBoundedTrainingWidth(seed => arimax.Predict(parameters, 0, seed).Y, raw.Length, realizations, sigma, "ARIMAX");
    }

    /// <summary>
    /// Asserts the empirical 95 percent band of stochastic in-sample predictions stays bounded
    /// late in the training period and is non-degenerate early in the training period.
    /// </summary>
    /// <param name="predict">Produces one stochastic prediction path for a seed.</param>
    /// <param name="length">The prediction path length.</param>
    /// <param name="realizations">The number of seeds.</param>
    /// <param name="sigma">The innovation standard deviation.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertBoundedTrainingWidth(Func<int, double[]> predict, int length, int realizations, double sigma, string context)
    {
        var samples = new double[length, realizations];
        for (int realization = 0; realization < realizations; realization++)
        {
            double[] path = predict(realization + 1);
            for (int t = 0; t < length; t++)
                samples[t, realization] = path[t];
        }

        double WidthAt(int t)
        {
            var row = new double[realizations];
            for (int realization = 0; realization < realizations; realization++)
                row[realization] = samples[t, realization];
            Array.Sort(row);
            return row[(int)(0.975 * realizations)] - row[(int)(0.025 * realizations)];
        }

        double earlyWidth = WidthAt(5);
        double lateWidth = WidthAt(length - 5);

        // A random-walk cone over ~50 steps would be roughly 2 * 1.96 * sqrt(50) * sigma; the
        // conditional band is roughly 2 * 1.96 * sigma.
        Assert.IsTrue(lateWidth < 6.0 * sigma * Math.Sqrt(2), $"{context}: late training width {lateWidth:F1} exceeds the bound {6.0 * sigma * Math.Sqrt(2):F1}.");
        Assert.IsTrue(earlyWidth > 0.1 * sigma, $"{context}: early training width {earlyWidth:F1} is degenerate.");
    }

    /// <summary>
    /// Creates the four time-series models on the same raw data with a default transform.
    /// </summary>
    /// <param name="raw">The raw observations.</param>
    /// <returns>AR, MA, ARIMA and ARIMAX models.</returns>
    private static IEnumerable<ModelBase> CreateModels(double[] raw)
    {
        var ar = new AutoRegressive(CreateSeries(raw), order: 1, includeIntercept: true) { UseDefaultTrainingSteps = false };
        ar.TrainingTimeSteps = raw.Length;
        yield return ar;

        var ma = new MovingAverage(CreateSeries(raw), order: 1, includeIntercept: true) { UseDefaultTrainingSteps = false };
        ma.TrainingTimeSteps = raw.Length;
        yield return ma;

        var arima = new ARIMA(CreateSeries(raw), 1, 0, 1, true) { UseDefaultTrainingSteps = false };
        arima.TrainingTimeSteps = raw.Length;
        yield return arima;

        var arimax = new ARIMAX { IncludeIntercept = true, AROrderP = 1, DiffOrderD = 0, MAOrderQ = 1, XOrderB = 0, UseDefaultTrainingSteps = false };
        arimax.TimeSeries = CreateSeries(raw);
        arimax.TrainingTimeSteps = raw.Length;
        yield return arimax;
    }

    /// <summary>
    /// Sets the transform type of a time-series model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="transform">The transform.</param>
    private static void SetTransformType(ModelBase model, ModelTransform transform)
    {
        switch (model)
        {
            case AutoRegressive ar:
                ar.TransformType = transform;
                break;
            case MovingAverage ma:
                ma.TransformType = transform;
                break;
            case ARIMA arima:
                arima.TransformType = transform;
                break;
            case ARIMAX arimax:
                arimax.TransformType = transform;
                break;
            default:
                throw new ArgumentException("Unexpected model type.", nameof(model));
        }
    }

    /// <summary>
    /// Creates an ARIMAX model with one exact-date covariate.
    /// </summary>
    /// <param name="raw">The raw response values.</param>
    /// <param name="covariate">The covariate values on the response dates.</param>
    /// <param name="differenceOrder">The differencing order.</param>
    /// <param name="covariateLagOrder">The covariate lag order.</param>
    /// <param name="transform">The transform.</param>
    /// <param name="includeIntercept">Whether an intercept is included.</param>
    /// <param name="trainingSteps">The raw training steps; null uses the full series.</param>
    /// <returns>The configured model.</returns>
    private static ARIMAX CreateArimax(
        double[] raw,
        double[] covariate,
        int differenceOrder,
        int covariateLagOrder,
        ModelTransform transform,
        bool includeIntercept,
        int? trainingSteps = null)
    {
        var model = new ARIMAX
        {
            IncludeIntercept = includeIntercept,
            AROrderP = 0,
            DiffOrderD = differenceOrder,
            MAOrderQ = 0,
            XOrderB = covariateLagOrder,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
            UseDefaultTrainingSteps = false,
            TransformType = transform,
        };
        model.TimeSeries = CreateSeries(raw);
        model.TrainingTimeSteps = trainingSteps ?? raw.Length;
        model.SetCovariates(new List<NumericTimeSeries> { CreateSeries(covariate) });
        return model;
    }

    /// <summary>
    /// Creates a daily series.
    /// </summary>
    /// <param name="values">The ordinate values.</param>
    /// <returns>The series.</returns>
    private static NumericTimeSeries CreateSeries(double[] values) =>
        new(TimeInterval.OneDay, s_startDate, values);

    /// <summary>
    /// Creates a deterministic positive random-walk fixture.
    /// </summary>
    /// <param name="length">The series length.</param>
    /// <param name="seed">The generator seed.</param>
    /// <returns>The fixture values.</returns>
    private static double[] CreateRandomWalk(int length, int seed)
    {
        var random = new Random(seed);
        var values = new double[length];
        double level = 500.0;
        for (int i = 0; i < length; i++)
        {
            level += 40.0 * (random.NextDouble() - 0.5);
            values[i] = level;
        }

        return values;
    }

    /// <summary>
    /// Evaluates the Gaussian log density of a residual.
    /// </summary>
    /// <param name="residual">The residual.</param>
    /// <param name="sigma">The standard deviation.</param>
    /// <returns>The log density.</returns>
    private static double NormalLogDensity(double residual, double sigma) =>
        -0.5 * Math.Log(2.0 * Math.PI * sigma * sigma) - residual * residual / (2.0 * sigma * sigma);

    /// <summary>
    /// Asserts two arrays agree element by element within the test tolerance.
    /// </summary>
    /// <param name="expected">The expected values.</param>
    /// <param name="actual">The actual values.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertArrayEqual(double[] expected, double[] actual, string context)
    {
        Assert.AreEqual(expected.Length, actual.Length, $"{context}: length");
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], Tolerance, $"{context}: index {i}");
    }
}
