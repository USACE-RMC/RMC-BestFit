using Numerics.Data;
using RMC.BestFit.Models;
using ModelTransform = RMC.BestFit.Models.Transform;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Regression tests for time-series prediction conditioning, reintegration, and component alignment.
/// </summary>
[TestClass]
public class TimeSeriesPredictionReintegrationTests
{
    private const double Tolerance = 1E-12;
    private static readonly DateTime s_startDate = new(2002, 3, 4);

    /// <summary>
    /// Verifies AR predictions use observed training lags through the boundary and generated
    /// values only after forecasting has begun.
    /// </summary>
    [TestMethod]
    public void AutoRegressivePrediction_ConditionsOnTrainingAndRecursesAfterBoundary()
    {
        double[] raw = { 10, 14, 15, 999 };
        var model = new AutoRegressive(CreateSeries(raw), order: 1, includeIntercept: false)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.None,
        };
        model.TrainingTimeSteps = 3;

        var result = model.Predict(new[] { 0.5, 1.0 }, 2, -1);

        AssertArrayEqual(new[] { 10.0, 5.0, 7.0, 7.5, 3.75 }, result.Y, "AR conditional prediction");
        AssertArrayEqual(new[] { 0.0, 5.0, 7.0, 7.5, 3.75 }, result.ARPart, "AR component");
    }

    /// <summary>
    /// Verifies MA predictions use observed training residuals through the boundary and let
    /// residual memory expire only after forecasting has begun.
    /// </summary>
    [TestMethod]
    public void MovingAveragePrediction_ConditionsOnTrainingAndRecursesAfterBoundary()
    {
        double[] raw = { 2, 4, 3, 999 };
        var model = new MovingAverage(CreateSeries(raw), order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.None,
        };
        model.TrainingTimeSteps = 3;

        var result = model.Predict(new[] { 1.0, 0.5, 1.0 }, 2, -1);

        AssertArrayEqual(new[] { 1.0, 1.5, 2.25, 1.375, 1.0 }, result.Y, "MA conditional prediction");
        AssertArrayEqual(new[] { 0.0, 0.5, 1.25, 0.375, 0.0 }, result.MAPart, "MA component");
    }

    /// <summary>
    /// Verifies first-difference fitted values are conditioned on the preceding observed level,
    /// the first forecast starts from the final training level, and later forecasts recurse.
    /// </summary>
    [TestMethod]
    public void ArimaD1Prediction_ConditionsOnTrainingAndForecastBoundary()
    {
        double[] raw = { 10, 14, 15, 999 };
        var model = new ARIMA(CreateSeries(raw), 0, 1, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.None,
        };
        model.TrainingTimeSteps = 3;

        var result = model.Predict(new[] { 2.0, 1.0 }, 2, -1);

        AssertArrayEqual(new[] { 10.0, 12.0, 16.0, 17.0, 19.0 }, result.Y, "ARIMA d=1 conditional levels");
        AssertComponentAlignment(result.InterceptPart, 1, 2.0, result.Y.Length, "ARIMA d=1 intercept");
    }

    /// <summary>
    /// Verifies second-difference fitted values use the observed level and first-difference
    /// states, then advance both states recursively after the training boundary.
    /// </summary>
    [TestMethod]
    public void ArimaD2Prediction_ConditionsOnObservedDifferenceStatesAtBoundary()
    {
        double[] raw = { 1, 4, 10, 999 };
        var model = new ARIMA(CreateSeries(raw), 0, 2, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.None,
        };
        model.TrainingTimeSteps = 3;

        var result = model.Predict(new[] { 2.0, 1.0 }, 2, -1);

        AssertArrayEqual(new[] { 1.0, 4.0, 9.0, 18.0, 28.0 }, result.Y, "ARIMA d=2 conditional levels");
        AssertComponentAlignment(result.InterceptPart, 2, 2.0, result.Y.Length, "ARIMA d=2 intercept");
    }

    /// <summary>
    /// Verifies ARIMAX first-difference predictions use the date-aligned level covariate while
    /// conditioning level reconstruction at the training/forecast boundary.
    /// </summary>
    [TestMethod]
    public void ArimaxD1Prediction_ConditionsOnTrainingAndForecastBoundary()
    {
        double[] raw = { 10, 14, 15, 999 };
        double[] covariate = { 0, 1, 1, 1, 1 };
        ARIMAX model = CreateArimax(raw, covariate, 1, ModelTransform.None, trainingSteps: 3);

        var result = model.Predict(new[] { 2.0, 1.0 }, 2, -1);

        AssertArrayEqual(new[] { 10.0, 12.0, 16.0, 17.0, 19.0 }, result.Y, "ARIMAX d=1 conditional levels");
        AssertArrayEqual(new[] { 0.0, 2.0, 2.0, 2.0, 2.0 }, result.CovariatePart, "ARIMAX covariate component");
    }

    /// <summary>
    /// Verifies logarithmic first-difference reconstruction conditions on observed transformed
    /// levels and applies the inverse transform only after boundary-aware reconstruction.
    /// </summary>
    [TestMethod]
    public void LogArimaD1Prediction_ConditionsOnTransformedTrainingBoundary()
    {
        double[] transformed = { 1.0, 1.5, 1.6, 9.0 };
        double[] raw = transformed.Select(Math.Exp).ToArray();
        var model = new ARIMA(CreateSeries(raw), 0, 1, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        model.TrainingTimeSteps = 3;

        var result = model.Predict(new[] { 0.1, 0.25 }, 2, -1);
        double[] expected = new[] { 1.0, 1.1, 1.6, 1.7, 1.8 }.Select(Math.Exp).ToArray();

        AssertArrayEqual(expected, result.Y, "log ARIMA d=1 conditional levels");
    }

    /// <summary>
    /// Verifies transformed AR, MA, ARIMA, and ARIMAX predictions use only model-scale lag
    /// and residual states before applying a single inverse transform to the completed path.
    /// </summary>
    /// <remarks>
    /// The raw observations are exponentials of deliberately small transformed values. Feeding
    /// even one raw observation into an AR or MA recurrence therefore produces a large and
    /// immediately detectable departure from the hand-evaluated transformed-space oracle.
    /// </remarks>
    [TestMethod]
    public void TransformedPredictions_UseOnlyModelScaleLagAndResidualStates()
    {
        double[] transformed = { 1.0, 1.5, 1.6, 9.0 };
        double[] raw = transformed.Select(Math.Exp).ToArray();

        var ar = new AutoRegressive(CreateSeries(raw), order: 1, includeIntercept: false)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        ar.TrainingTimeSteps = 3;
        AssertArrayEqual(
            new[] { 1.0, 0.5, 0.75, 0.8, 0.4 }.Select(Math.Exp).ToArray(),
            ar.Predict(new[] { 0.5, 0.25 }, 2, -1).Y,
            "log AR model-scale lags");

        var ma = new MovingAverage(CreateSeries(raw), order: 1, includeIntercept: false)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        ma.TrainingTimeSteps = 3;
        AssertArrayEqual(
            new[] { 0.0, 0.5, 0.5, 0.55, 0.0 }.Select(Math.Exp).ToArray(),
            ma.Predict(new[] { 0.5, 0.25 }, 2, -1).Y,
            "log MA model-scale residuals");

        var arima = new ARIMA(CreateSeries(raw), 1, 1, 1, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        arima.TrainingTimeSteps = 3;
        double[] expectedIntegrated = new[] { 1.0, 1.5, 1.775, 1.63125, 1.671875 }
            .Select(Math.Exp)
            .ToArray();
        AssertArrayEqual(
            expectedIntegrated,
            arima.Predict(new[] { 0.05, 0.5, 0.25, 0.2 }, 2, -1).Y,
            "log ARIMA model-scale differences");

        var arimax = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 1,
            XOrderB = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        arimax.TimeSeries = CreateSeries(raw);
        arimax.TrainingTimeSteps = 3;
        AssertArrayEqual(
            expectedIntegrated,
            arimax.Predict(new[] { 0.05, 0.5, 0.25, 0.2 }, 2, -1).Y,
            "log ARIMAX model-scale differences");
    }

    /// <summary>
    /// Verifies first-difference predictions reconstruct a linear raw sequence for zero and
    /// positive forecast horizons and map model components to raw slots <c>k+1</c>.
    /// </summary>
    /// <param name="forecastSteps">The forecast horizon.</param>
    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void ArimaD1LinearPrediction_ReintegratesExactLengthAndComponents(int forecastSteps)
    {
        double[] raw = { 10, 12, 14, 16, 18, 20 };
        var model = new ARIMA(CreateSeries(raw), 0, 1, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.None,
        };
        model.TrainingTimeSteps = raw.Length;

        var result = model.Predict(new[] { 2.0, 1.0 }, forecastSteps, -1);
        double[] expected = Enumerable.Range(0, raw.Length + forecastSteps)
            .Select(index => 10.0 + 2.0 * index)
            .ToArray();
        AssertArrayEqual(expected, result.Y, "ARIMA d=1 levels");
        AssertComponentAlignment(result.InterceptPart, 1, 2.0, expected.Length, "ARIMA intercept");
        AssertComponentAlignment(result.ARPart, 1, 0.0, expected.Length, "ARIMA AR");
        AssertComponentAlignment(result.MAPart, 1, 0.0, expected.Length, "ARIMA MA");
    }

    /// <summary>
    /// Verifies second-difference predictions reconstruct a quadratic raw sequence and leave the
    /// first two conditioning component slots at zero.
    /// </summary>
    [TestMethod]
    public void ArimaD2QuadraticPrediction_ReintegratesExactRecurrence()
    {
        double[] raw = { 1, 4, 9, 16, 25, 36 };
        var model = new ARIMA(CreateSeries(raw), 0, 2, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.None,
        };
        model.TrainingTimeSteps = raw.Length;

        var result = model.Predict(new[] { 2.0, 1.0 }, 2, -1);
        double[] expected = Enumerable.Range(1, 8).Select(value => (double)(value * value)).ToArray();
        AssertArrayEqual(expected, result.Y, "ARIMA d=2 levels");
        AssertComponentAlignment(result.InterceptPart, 2, 2.0, expected.Length, "ARIMA d=2 intercept");
    }

    /// <summary>
    /// Verifies ARIMAX uses the level covariate at raw date <c>k+d</c>, reconstructs the complete
    /// level path, and aligns the covariate component with raw output slots.
    /// </summary>
    [TestMethod]
    public void ArimaxD1Prediction_UsesDateIndexedLevelCovariateAndReintegrates()
    {
        double[] raw = { 10, 12, 15, 19, 24, 30 };
        double[] covariate = { 999, 2, 3, 4, 5, 6, 7, 8 };
        ARIMAX model = CreateArimax(raw, covariate, 1, ModelTransform.None);

        var result = model.Predict(new[] { 1.0, 1.0 }, 2, -1);
        double[] expected = { 10, 12, 15, 19, 24, 30, 37, 45 };
        AssertArrayEqual(expected, result.Y, "ARIMAX d=1 levels");
        AssertArrayEqual(new[] { 0.0, 2, 3, 4, 5, 6, 7, 8 }, result.CovariatePart, "ARIMAX covariate part");
        AssertAllComponentsHaveLength(result.Y.Length, result.InterceptPart, result.TrendPart,
            result.SeasonalityPart, result.CovariatePart, result.ARPart, result.MAPart);
    }

    /// <summary>
    /// Verifies logarithmic ARIMA and ARIMAX predictions integrate completely on the transformed
    /// scale and inverse-transform once after reintegration.
    /// </summary>
    [TestMethod]
    public void LogTransformedD1Predictions_ReintegrateBeforeInverseTransform()
    {
        double[] raw = Enumerable.Range(1, 6).Select(value => Math.Exp(value)).ToArray();
        double[] expected = Enumerable.Range(1, 8).Select(value => Math.Exp(value)).ToArray();

        var arima = new ARIMA(CreateSeries(raw), 0, 1, 0, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.Logarithmic,
        };
        arima.TrainingTimeSteps = raw.Length;
        AssertArrayEqual(expected, arima.Predict(new[] { 1.0, 0.25 }, 2, -1).Y, "log ARIMA");

        double[] covariate = { 999, 1, 1, 1, 1, 1, 1, 1 };
        ARIMAX arimax = CreateArimax(raw, covariate, 1, ModelTransform.Logarithmic);
        AssertArrayEqual(expected, arimax.Predict(new[] { 1.0, 0.25 }, 2, -1).Y, "log ARIMAX");
    }

    /// <summary>
    /// Pins the pre-change <c>d=0</c>, untransformed fixed-seed arrays bit for bit for both
    /// prediction implementations and every existing component tuple member.
    /// </summary>
    [TestMethod]
    public void NoneD0FixedSeedPrediction_RetainsGoldenArraysBitForBit()
    {
        double[] raw = { 10, 12, 15, 19, 24, 30, 37, 45 };
        var arima = new ARIMA(CreateSeries(raw), 1, 0, 1, true)
        {
            UseDefaultTrainingSteps = false,
            TransformType = ModelTransform.None,
        };
        arima.TrainingTimeSteps = 6;
        var arimaResult = arima.Predict(new[] { 1.25, 0.35, -0.2, 0.75 }, 2, 24680);
        AssertExact(new[] { 10.0, 3.6344993128015375, 3.2631786252532793, 4.366561412681758, 4.08341894314281, 6.154757011181868, 6.096197809942823, 4.20207300362876 }, arimaResult.Y, "ARIMA Y");
        AssertExact(new[] { 1.25, 1.25, 1.25, 1.25, 1.25, 1.25, 1.25, 1.25 }, arimaResult.InterceptPart, "ARIMA intercept");
        AssertExact(new[] { 0.0, 3.0625, 3.7624999999999997, 4.8125, 6.2124999999999995, 7.9624999999999995, 10.0625, 1.696169233479988 }, arimaResult.ARPart, "ARIMA AR");
        AssertExact(new[] { 0.0, 0.0, -1.5375, -2.305, -3.0485, -3.9172, -4.940940000000001, 0.05507243801143513 }, arimaResult.MAPart, "ARIMA MA");

        double[] covariate = { 2, -1, 0.5, 3, -2, 1.5, 4, -0.5 };
        ARIMAX arimax = CreateArimax(raw, covariate, 0, ModelTransform.None, 6, 1, 1, true);
        var arimaxResult = arimax.Predict(new[] { 0.25, 1.1, 0.3, -0.2, 0.75 }, 2, 24681);
        AssertExact(new[] { 10.0, 1.8200463038354713, 2.8934496296866623, 5.29995609610872, 0.029763592847706077, 4.535582842392279, 7.502413639393079, 0.46333977752210875 }, arimaxResult.Y, "ARIMAX Y");
        AssertExact(new[] { 0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25 }, arimaxResult.InterceptPart, "ARIMAX intercept");
        AssertExact(new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 }, arimaxResult.TrendPart, "ARIMAX trend");
        AssertExact(new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 }, arimaxResult.SeasonalityPart, "ARIMAX seasonality");
        AssertExact(new[] { 2.2, -1.1, 0.55, 3.3000000000000003, -2.2, 1.6500000000000001, 4.4, -0.55 }, arimaxResult.CovariatePart, "ARIMAX covariate");
        AssertExact(new[] { 0.0, 2.2649999999999997, 3.8549999999999995, 4.26, 4.635, 7.784999999999999, 8.43, 0.8557240918179235 }, arimaxResult.ARPart, "ARIMAX AR");
        AssertExact(new[] { 0.0, 0.0, -2.1170000000000004, -2.4924000000000004, -2.7364800000000002, -4.810296000000001, -5.025059200000001, 0.11050543212138404 }, arimaxResult.MAPart, "ARIMAX MA");
    }

    /// <summary>
    /// Creates an exact-date ARIMAX covariate fixture.
    /// </summary>
    /// <param name="raw">The raw response.</param>
    /// <param name="covariate">The level covariate.</param>
    /// <param name="differenceOrder">The response differencing order.</param>
    /// <param name="transform">The response transform.</param>
    /// <param name="trainingSteps">The optional raw training length.</param>
    /// <param name="arOrder">The AR order.</param>
    /// <param name="maOrder">The MA order.</param>
    /// <param name="includeIntercept">Whether to include the intercept before the covariate parameter.</param>
    /// <returns>The configured model.</returns>
    private static ARIMAX CreateArimax(
        double[] raw,
        double[] covariate,
        int differenceOrder,
        ModelTransform transform,
        int? trainingSteps = null,
        int arOrder = 0,
        int maOrder = 0,
        bool includeIntercept = false)
    {
        var model = new ARIMAX
        {
            IncludeIntercept = includeIntercept,
            AROrderP = arOrder,
            DiffOrderD = differenceOrder,
            MAOrderQ = maOrder,
            XOrderB = 0,
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
    /// Verifies one component vector's length, conditioning prefix, and constant model value.
    /// </summary>
    /// <param name="values">The component values.</param>
    /// <param name="conditioningCount">The zero-valued conditioning prefix length.</param>
    /// <param name="modelValue">The expected value after conditioning.</param>
    /// <param name="expectedLength">The expected raw output length.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertComponentAlignment(double[] values, int conditioningCount, double modelValue, int expectedLength, string context)
    {
        Assert.AreEqual(expectedLength, values.Length, context);
        for (int i = 0; i < values.Length; i++)
            Assert.AreEqual(i < conditioningCount ? 0.0 : modelValue, values[i], Tolerance, $"{context}, index {i}");
    }

    /// <summary>
    /// Verifies every component vector has the raw output length.
    /// </summary>
    /// <param name="expectedLength">The expected length.</param>
    /// <param name="components">The component vectors.</param>
    private static void AssertAllComponentsHaveLength(int expectedLength, params double[][] components)
    {
        foreach (double[] component in components)
            Assert.AreEqual(expectedLength, component.Length);
    }

    /// <summary>
    /// Compares deterministic numeric vectors within the recurrence tolerance.
    /// </summary>
    /// <param name="expected">The expected values.</param>
    /// <param name="actual">The actual values.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertArrayEqual(double[] expected, double[] actual, string context)
    {
        Assert.AreEqual(expected.Length, actual.Length, context);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], Tolerance, $"{context}, index {i}");
    }

    /// <summary>
    /// Compares fixed-seed golden arrays exactly.
    /// </summary>
    /// <param name="expected">The captured pre-change values.</param>
    /// <param name="actual">The current values.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertExact(double[] expected, double[] actual, string context)
    {
        Assert.AreEqual(expected.Length, actual.Length, context);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], $"{context}, index {i}");
    }
}
