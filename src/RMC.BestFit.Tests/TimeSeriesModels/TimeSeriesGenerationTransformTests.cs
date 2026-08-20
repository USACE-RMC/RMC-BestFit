using Numerics.Data;
using RMC.BestFit.Models;
using ModelTransform = RMC.BestFit.Models.Transform;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Regression tests for AR, MA, and ARIMA transformed/differenced generation.
/// </summary>
[TestClass]
public class TimeSeriesGenerationTransformTests
{
    private const double Tolerance = 1E-12;
    private static readonly DateTime s_startDate = new(2004, 5, 6);
    private static readonly double[] s_arModelScale =
    {
        0.5275903734293788, 0.19367432545682162, 0.598464731025482,
        0.8573412905998781, 0.3188682234957716, 0.9177848551790461,
        2.6350006057479343, 2.635215407976874,
    };
    private static readonly double[] s_maModelScale =
    {
        1.0227687685036484, 0.4974530014652585, 1.1363812293685684,
        0.8964366966232473, 1.3833833036998602, 0.6973654912089674,
        0.15873803749631565, 0.2074704533451301,
    };
    private static readonly double[] s_arimaModelScale =
    {
        1.5504888464633453, 2.245464762138672, 0.2062780780166804,
        0.49593324179260784, 0.9224018499700285, 0.8145819937498172,
        2.459490777492791, -0.4843239510513987,
    };

    /// <summary>
    /// Verifies AR generation completes its recursion on logarithmic scale and then applies the
    /// exponential inverse exactly once.
    /// </summary>
    [TestMethod]
    public void ArLogarithmicGeneration_InverseTransformsCompletedModelRecurrence()
    {
        AutoRegressive model = CreateAr(ModelTransform.Logarithmic);
        double[] actual = model.GenerateRandomValues(8, 13579);
        double[] expected = s_arModelScale.Select(value => Math.Exp(value)).ToArray();
        AssertArrayEqual(expected, actual, "AR logarithmic generation");
    }

    /// <summary>
    /// Verifies MA generation completes its recursion on Box-Cox scale and then applies the
    /// algebraic inverse exactly once.
    /// </summary>
    [TestMethod]
    public void MaBoxCoxGeneration_InverseTransformsCompletedModelRecurrence()
    {
        const double lambda = 0.5;
        MovingAverage model = CreateMa(ModelTransform.BoxCox, lambda);
        double[] actual = model.GenerateRandomValues(8, 13580);
        double[] expected = s_maModelScale.Select(value => InverseBoxCox(value, lambda)).ToArray();
        AssertArrayEqual(expected, actual, "MA Box-Cox generation");
    }

    /// <summary>
    /// Verifies ARIMA generation integrates every model-scale difference from the observed
    /// transformed anchor and only then applies the Yeo-Johnson inverse.
    /// </summary>
    [TestMethod]
    public void ArimaYeoJohnsonD1Generation_IntegratesThenInverseTransforms()
    {
        const double lambda = 0.6;
        double[] transformedData = { 2, 3, 4, 5, 6 };
        double[] rawData = transformedData.Select(value => InverseYeoJohnson(value, lambda)).ToArray();
        var model = new ARIMA(CreateSeries(rawData), 1, 1, 1, true)
        {
            TransformType = ModelTransform.YeoJohnson,
        };
        model.SetTransformParameters(lambda, double.NaN);
        model.SetParameterValues(new[] { 1.1, 0.4, -0.25, 0.7 });

        double[] expectedTransformed = new double[8];
        expectedTransformed[0] = transformedData[0];
        for (int i = 1; i < expectedTransformed.Length; i++)
            expectedTransformed[i] = expectedTransformed[i - 1] + s_arimaModelScale[i - 1];
        double[] expected = expectedTransformed
            .Select(value => InverseYeoJohnson(value, lambda))
            .ToArray();

        AssertArrayEqual(expected, model.GenerateRandomValues(8, 13581), "ARIMA Yeo-Johnson d=1");
    }

    /// <summary>
    /// Verifies attached ARIMA generation uses observed transformed anchors while an unattached
    /// model uses zero transformed anchors.
    /// </summary>
    [TestMethod]
    public void ArimaD2Generation_UsesObservedOrZeroTransformedAnchors()
    {
        var attached = new ARIMA(CreateSeries(new[] { 4.0, 7, 12, 19, 28 }), 0, 2, 0, true);
        attached.SetParameterValues(new[] { 0.5, 1.0 });
        var unattached = new ARIMA { POrder = 0, DOrder = 2, QOrder = 0, IncludeIntercept = true };
        unattached.SetParameterValues(new[] { 0.5, 1.0 });

        double[] observedAnchors = attached.GenerateRandomValues(7, 24680);
        double[] zeroAnchors = unattached.GenerateRandomValues(7, 24680);
        Assert.AreEqual(7, observedAnchors.Length);
        Assert.AreEqual(7, zeroAnchors.Length);
        for (int i = 0; i < observedAnchors.Length; i++)
            Assert.AreEqual(4.0 + 3.0 * i, observedAnchors[i] - zeroAnchors[i], Tolerance, $"anchor effect, index {i}");
    }

    /// <summary>
    /// Verifies requests no longer than the differencing order return only the requested observed
    /// or zero transformed anchors after inverse transformation.
    /// </summary>
    [TestMethod]
    public void ArimaGeneration_SampleSizeAtOrBelowD_ReturnsRequestedAnchors()
    {
        double[] raw = { Math.Exp(2), Math.Exp(3), Math.Exp(4) };
        var attached = new ARIMA(CreateSeries(raw), 0, 2, 0, true)
        {
            TransformType = ModelTransform.Logarithmic,
        };
        attached.SetParameterValues(new[] { 0.5, 1.0 });

        var unattached = new ARIMA
        {
            POrder = 0,
            DOrder = 2,
            QOrder = 0,
            IncludeIntercept = true,
            TransformType = ModelTransform.Logarithmic,
        };
        unattached.SetParameterValues(new[] { 0.5, 1.0 });

        AssertArrayEqual(new[] { raw[0] }, attached.GenerateRandomValues(1, 9876), "attached one anchor");
        AssertArrayEqual(new[] { raw[0], raw[1] }, attached.GenerateRandomValues(2, 9876), "attached two anchors");
        AssertArrayEqual(new[] { 1.0 }, unattached.GenerateRandomValues(1, 9876), "unattached one anchor");
        AssertArrayEqual(new[] { 1.0, 1.0 }, unattached.GenerateRandomValues(2, 9876), "unattached two anchors");
    }

    /// <summary>
    /// Pins every pre-change untransformed, undifferenced fixed-seed sequence bit for bit.
    /// </summary>
    [TestMethod]
    public void NoneD0FixedSeedGeneration_RetainsGoldenArraysBitForBit()
    {
        AssertExact(s_arModelScale, CreateAr(ModelTransform.None).GenerateRandomValues(8, 13579), "AR");
        AssertExact(s_maModelScale, CreateMa(ModelTransform.None).GenerateRandomValues(8, 13580), "MA");

        var arima = new ARIMA { POrder = 1, DOrder = 0, QOrder = 1, IncludeIntercept = true };
        arima.SetParameterValues(new[] { 1.1, 0.4, -0.25, 0.7 });
        AssertExact(s_arimaModelScale, arima.GenerateRandomValues(8, 13581), "ARIMA");
    }

    /// <summary>
    /// Creates the fixed AR generator fixture.
    /// </summary>
    /// <param name="transform">The configured transform.</param>
    /// <returns>The configured model.</returns>
    private static AutoRegressive CreateAr(ModelTransform transform)
    {
        var model = new AutoRegressive { Order = 2, IncludeIntercept = true, TransformType = transform };
        model.SetParameterValues(new[] { 1.25, 0.35, -0.15, 0.75 });
        return model;
    }

    /// <summary>
    /// Creates the fixed MA generator fixture.
    /// </summary>
    /// <param name="transform">The configured transform.</param>
    /// <param name="lambda">The optional manual Box-Cox exponent.</param>
    /// <returns>The configured model.</returns>
    private static MovingAverage CreateMa(ModelTransform transform, double? lambda = null)
    {
        var model = new MovingAverage { Order = 2, IncludeIntercept = true, TransformType = transform };
        if (lambda.HasValue)
            model.SetTransformParameters(lambda.Value, double.NaN);
        model.SetParameterValues(new[] { 0.75, -0.2, 0.3, 0.6 });
        return model;
    }

    /// <summary>
    /// Creates a daily response series.
    /// </summary>
    /// <param name="values">The raw response values.</param>
    /// <returns>The response series.</returns>
    private static NumericTimeSeries CreateSeries(double[] values) =>
        new(TimeInterval.OneDay, s_startDate, values);

    /// <summary>
    /// Independently evaluates the positive-branch Box-Cox inverse.
    /// </summary>
    /// <param name="value">The transformed value.</param>
    /// <param name="lambda">The transform exponent.</param>
    /// <returns>The raw positive value.</returns>
    private static double InverseBoxCox(double value, double lambda) =>
        Math.Pow(1.0 + lambda * value, 1.0 / lambda);

    /// <summary>
    /// Independently evaluates the nonnegative-branch Yeo-Johnson inverse.
    /// </summary>
    /// <param name="value">The transformed value.</param>
    /// <param name="lambda">The transform exponent.</param>
    /// <returns>The raw nonnegative value.</returns>
    private static double InverseYeoJohnson(double value, double lambda) =>
        Math.Pow(1.0 + lambda * value, 1.0 / lambda) - 1.0;

    /// <summary>
    /// Compares deterministic numeric vectors within the algebraic tolerance.
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
    /// <param name="expected">The pre-change values.</param>
    /// <param name="actual">The current values.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertExact(double[] expected, double[] actual, string context)
    {
        Assert.AreEqual(expected.Length, actual.Length, context);
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], $"{context}, index {i}");
    }
}
