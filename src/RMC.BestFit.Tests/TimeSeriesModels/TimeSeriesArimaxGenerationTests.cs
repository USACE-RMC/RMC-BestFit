using Numerics.Data;
using RMC.BestFit.Models;
using ModelTransform = RMC.BestFit.Models.Transform;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Regression tests for transformed, differenced, and date-aligned ARIMAX generation.
/// </summary>
[TestClass]
public class TimeSeriesArimaxGenerationTests
{
    private const double Tolerance = 1E-12;
    private static readonly DateTime s_startDate = new(2005, 6, 7);
    private static readonly double[] s_modelScaleGolden =
    {
        2.2620632327954455, -0.6690412732355178, 2.2364682873083486,
        2.718481409956337, -3.097232432613497, 1.0828631290683435,
        3.8879909176213205, 0.179815385903625,
    };

    /// <summary>
    /// Verifies a transformed ARIMAX(1,1) simulation uses level covariates at raw dates, completes
    /// its model-scale recurrence, integrates from the observed anchor, and inverse-transforms once.
    /// </summary>
    [TestMethod]
    public void ArimaxYeoJohnsonD1Generation_UsesDateAlignedModelScaleRecurrence()
    {
        const double lambda = 0.6;
        double[] transformedData = { 2, 3, 4, 5, 6 };
        NumericTimeSeries response = CreateDailySeries(
            transformedData.Select(value => InverseYeoJohnson(value, lambda)).ToArray());
        NumericTimeSeries covariate = CreateDailySeries(new[] { 99.0, 2, -1, 0.5, 3, -2, 1.5, 4.0 });
        ARIMAX model = CreateArimax(response, covariate, 1, 1, ModelTransform.YeoJohnson);
        model.SetTransformParameters(lambda, double.NaN);
        model.SetParameterValues(new[] { 0.25, 1.1, 0.3, -0.2, 0.75 });

        var expectedTransformed = new double[8];
        expectedTransformed[0] = transformedData[0];
        for (int i = 1; i < expectedTransformed.Length; i++)
            expectedTransformed[i] = expectedTransformed[i - 1] + s_modelScaleGolden[i - 1];

        AssertArrayEqual(
            expectedTransformed.Select(value => InverseYeoJohnson(value, lambda)).ToArray(),
            model.GenerateRandomValues(8, 24682),
            "ARIMAX transformed/differenced recurrence");
    }

    /// <summary>
    /// Verifies intercept, trend, seasonality, and level-covariate components are additive on the
    /// configured logarithmic model scale rather than inverse-transformed inside the recursion.
    /// </summary>
    [TestMethod]
    public void ArimaxLogGeneration_DeterministicComponentsRemainOnModelScale()
    {
        double[] responseValues = Enumerable.Repeat(10.0, 8).ToArray();
        double[] covariateValues = { -2, 1, 3, -1, 0.5, 4, -3, 2 };
        NumericTimeSeries response = CreateDailySeries(responseValues);
        NumericTimeSeries covariate = CreateDailySeries(covariateValues);
        ARIMAX baseline = CreateComponentModel(response, covariate);
        ARIMAX shifted = CreateComponentModel(response, covariate);
        baseline.SetParameterValues(new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.4 });
        shifted.SetParameterValues(new[] { 0.2, 0.03, 0.15, -0.08, 0.1, 0.4 });

        double[] baselineValues = baseline.GenerateRandomValues(8, 24683);
        double[] shiftedValues = shifted.GenerateRandomValues(8, 24683);
        for (int t = 0; t < shiftedValues.Length; t++)
        {
            double angle = 2.0 * Math.PI * t / shifted.SeasonalPeriod;
            double expectedShift = 0.2 + 0.03 * t + 0.15 * Math.Sin(angle) -
                0.08 * Math.Cos(angle) + 0.1 * covariateValues[t];
            Assert.AreEqual(
                expectedShift,
                Math.Log(shiftedValues[t]) - Math.Log(baselineValues[t]),
                Tolerance,
                $"Model-scale deterministic shift differs at step {t}.");
        }
    }

    /// <summary>
    /// Verifies attached transformed observations provide integration anchors while an otherwise
    /// identical unattached model uses zero transformed anchors.
    /// </summary>
    [TestMethod]
    public void ArimaxD1Generation_UsesObservedOrZeroAnchors()
    {
        NumericTimeSeries response = CreateDailySeries(new[] { 5.0, 6, 7, 8, 9 });
        NumericTimeSeries covariate = CreateDailySeries(new[] { 1.0, 2, 3, 4, 5, 6, 7, 8 });
        ARIMAX attached = CreateArimax(response, covariate, 0, 0, ModelTransform.None);
        ARIMAX unattached = CreateArimax(null, covariate, 0, 0, ModelTransform.None);
        attached.SetParameterValues(new[] { 0.25, 1.1, 0.75 });
        unattached.SetParameterValues(new[] { 0.25, 1.1, 0.75 });

        double[] observedAnchors = attached.GenerateRandomValues(8, 24684);
        double[] zeroAnchors = unattached.GenerateRandomValues(8, 24684);
        for (int i = 0; i < observedAnchors.Length; i++)
            Assert.AreEqual(5.0, observedAnchors[i] - zeroAnchors[i], Tolerance, $"Anchor shift differs at index {i}.");
    }

    /// <summary>
    /// Verifies requests no longer than the differencing order return only the requested observed
    /// or zero transformed anchors and do not consume model-scale innovations.
    /// </summary>
    [TestMethod]
    public void ArimaxGeneration_SampleSizeAtOrBelowD_ReturnsRequestedAnchors()
    {
        var attached = new ARIMAX(CreateDailySeries(new[] { 7.0, 9, 12 }))
        {
            AROrderP = 0,
            DiffOrderD = 2,
            MAOrderQ = 0,
        };
        var unattached = new ARIMAX
        {
            AROrderP = 0,
            DiffOrderD = 2,
            MAOrderQ = 0,
        };

        CollectionAssert.AreEqual(new[] { 7.0, 9.0 }, attached.GenerateRandomValues(2, 24685));
        CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, unattached.GenerateRandomValues(2, 24685));
    }

    /// <summary>
    /// Verifies explicitly supplied covariates are selected by exact response timestamp even when
    /// their ordinate order differs from response order.
    /// </summary>
    [TestMethod]
    public void ArimaxGeneration_ExplicitCovariatesUseExactResponseDates()
    {
        NumericTimeSeries response = CreateDailySeries(Enumerable.Repeat(10.0, 8).ToArray());
        NumericTimeSeries configured = CreateDailySeries(new[] { 2.0, -1, 0.5, 3, -2, 1.5, 4, -0.5 });
        NumericTimeSeries explicitOrdered = CreateDailySeries(new[] { 3.0, 1, 4, 1.5, -2, 5, 0.25, 2 });
        NumericTimeSeries explicitReversed = ReverseOrdinateOrder(explicitOrdered);
        ARIMAX model = CreateArimax(response, configured, 0, 0, ModelTransform.None, differencingOrder: 0);
        model.SetParameterValues(new[] { 0.25, 1.1, 0.75 });

        double[] ordered = model.GenerateRandomValues(8, 24686, new List<NumericTimeSeries> { explicitOrdered });
        double[] reversed = model.GenerateRandomValues(8, 24686, new List<NumericTimeSeries> { explicitReversed });
        CollectionAssert.AreEqual(ordered, reversed);
    }

    /// <summary>
    /// Verifies generation rejects a covariate whose length is sufficient but which omits a raw
    /// response timestamp required by the differenced model.
    /// </summary>
    [TestMethod]
    public void ArimaxGeneration_MissingRequiredCovariateTimestampThrows()
    {
        NumericTimeSeries response = CreateDailySeries(new[] { 10.0, 11, 12, 13, 14, 15 });
        NumericTimeSeries covariate = CreateDailySeries(new[] { 1.0, 2, 3, 4, 5, 6 });
        covariate[2].Index = s_startDate.AddDays(20);
        ARIMAX model = CreateArimax(response, covariate, 0, 0, ModelTransform.None);
        model.SetParameterValues(new[] { 0.25, 1.1, 0.75 });

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
            () => model.GenerateRandomValues(6, 24687));
        StringAssert.Contains(exception.Message, "missing required timestamp");
        StringAssert.Contains(exception.Message, "raw response index 2");
    }

    /// <summary>
    /// Pins the Transform.None/d=0 fixed-seed sequence bit for bit.
    /// </summary>
    [TestMethod]
    public void ArimaxNoneD0FixedSeedGeneration_RetainsGoldenArrayBitForBit()
    {
        NumericTimeSeries response = CreateDailySeries(new[] { 10.0, 12, 15, 19, 24, 30, 37, 45 });
        NumericTimeSeries covariate = CreateDailySeries(new[] { 2.0, -1, 0.5, 3, -2, 1.5, 4, -0.5 });
        ARIMAX model = CreateArimax(response, covariate, 1, 1, ModelTransform.None, differencingOrder: 0);
        model.SetParameterValues(new[] { 0.25, 1.1, 0.3, -0.2, 0.75 });

        CollectionAssert.AreEqual(s_modelScaleGolden, model.GenerateRandomValues(8, 24682));
    }

    /// <summary>
    /// Creates the standard ARIMAX fixture used by generation contracts.
    /// </summary>
    /// <param name="response">Optional attached response series.</param>
    /// <param name="covariate">The configured level covariate.</param>
    /// <param name="arOrder">The autoregressive order.</param>
    /// <param name="maOrder">The moving-average order.</param>
    /// <param name="transform">The response transform.</param>
    /// <param name="differencingOrder">The response differencing order.</param>
    /// <returns>The configured model with a rebuilt parameter list.</returns>
    private static ARIMAX CreateArimax(
        NumericTimeSeries? response,
        NumericTimeSeries covariate,
        int arOrder,
        int maOrder,
        ModelTransform transform,
        int differencingOrder = 1)
    {
        ARIMAX model = response == null ? new ARIMAX() : new ARIMAX(response);
        model.IncludeIntercept = true;
        model.TrendType = ARIMAX.Trend.None;
        model.IncludeSeasonality = false;
        model.AROrderP = arOrder;
        model.DiffOrderD = differencingOrder;
        model.MAOrderQ = maOrder;
        model.XOrderB = 0;
        model.TransformType = transform;
        model.CovariateExtension = ARIMAX.CovariateExtensionMethod.None;
        model.SetCovariates(new List<NumericTimeSeries> { covariate });
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>
    /// Creates a logarithmic component-isolation model.
    /// </summary>
    /// <param name="response">The attached response dates.</param>
    /// <param name="covariate">The level covariate.</param>
    /// <returns>The configured component model.</returns>
    private static ARIMAX CreateComponentModel(NumericTimeSeries response, NumericTimeSeries covariate)
    {
        var model = new ARIMAX(response)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            IncludeSeasonality = true,
            AROrderP = 0,
            DiffOrderD = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            TransformType = ModelTransform.Logarithmic,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
        };
        model.SetCovariates(new List<NumericTimeSeries> { covariate });
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>
    /// Creates an exact daily series at the shared start date.
    /// </summary>
    /// <param name="values">The ordinates.</param>
    /// <returns>The daily series.</returns>
    private static NumericTimeSeries CreateDailySeries(double[] values)
    {
        return new NumericTimeSeries(TimeInterval.OneDay, s_startDate, values);
    }

    /// <summary>
    /// Copies a series while reversing its ordinate insertion order.
    /// </summary>
    /// <param name="source">The dated source series.</param>
    /// <returns>A series containing the same dated values in reverse order.</returns>
    private static NumericTimeSeries ReverseOrdinateOrder(NumericTimeSeries source)
    {
        var result = new NumericTimeSeries(source.TimeInterval);
        for (int i = source.Count - 1; i >= 0; i--)
            result.Add(source[i].Clone());
        return result;
    }

    /// <summary>
    /// Evaluates the independent Yeo-Johnson inverse formula.
    /// </summary>
    /// <param name="value">The transformed value.</param>
    /// <param name="lambda">The fixed exponent.</param>
    /// <returns>The raw-scale value.</returns>
    private static double InverseYeoJohnson(double value, double lambda)
    {
        if (value >= 0)
            return Math.Abs(lambda) < 1E-14 ? Math.Exp(value) - 1.0 : Math.Pow(lambda * value + 1.0, 1.0 / lambda) - 1.0;
        double complement = 2.0 - lambda;
        return Math.Abs(complement) < 1E-14
            ? 1.0 - Math.Exp(-value)
            : 1.0 - Math.Pow(1.0 - complement * value, 1.0 / complement);
    }

    /// <summary>
    /// Compares two arrays with the package's deterministic tolerance.
    /// </summary>
    /// <param name="expected">The expected values.</param>
    /// <param name="actual">The actual values.</param>
    /// <param name="label">The comparison label.</param>
    private static void AssertArrayEqual(double[] expected, double[] actual, string label)
    {
        Assert.AreEqual(expected.Length, actual.Length, $"{label} length");
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], Tolerance, $"{label} differs at index {i}.");
    }
}
