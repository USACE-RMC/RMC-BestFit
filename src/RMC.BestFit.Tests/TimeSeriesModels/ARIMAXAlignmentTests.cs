using Numerics.Data;
using Numerics.Data.Statistics;
using RMC.BestFit.Models;
using ModelTransform = RMC.BestFit.Models.Transform;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Regression tests for ARIMAX raw/model index and exact-date level-covariate alignment.
/// </summary>
[TestClass]
public class ARIMAXAlignmentTests
{
    private const double Tolerance = 1E-12;
    private static readonly DateTime s_startDate = new(2001, 2, 3);
    private static readonly double[] s_raw = { 1.2, 1.9, 3.1, 4.8, 7.4, 10.9, 15.7, 22.0, 30.2, 41.0 };
    private static readonly double[] s_covariate = { 2.0, -1.0, 0.5, 3.0, -2.0, 1.5, 4.0, -0.5, 8.0, -7.0 };

    /// <summary>
    /// The exact migration hint <see cref="ARIMAX"/> appends once, after every per-covariate
    /// alignment message, when at least one covariate is missing a required timestamp.
    /// </summary>
    private const string CovariateDateAlignmentHint =
        "Warning: Covariates are paired with the response by date. RMC-BestFit 2.0.0 paired them by position, so a covariate that worked there can fail this check; give each covariate a value at every response date in the training window, plus the b preceding dates when the exogenous lag order b is above 0.";

    /// <summary>
    /// Verifies model step <c>k</c> maps to raw index <c>k+d</c>, with exactly
    /// <c>T-d</c> training differences whose timestamps come from the later raw values.
    /// </summary>
    [TestMethod]
    public void Differencing_PreservesLaterRawDatesAndTrainingBoundary()
    {
        for (int order = 0; order <= 2; order++)
        {
            ARIMAX model = CreateModel(order, CreateSeries(s_raw), CreateSeries(s_covariate), ModelTransform.BoxCox);
            double[] expectedFull = Difference(s_raw.Select(value => BoxCox.Transform(value, 0.4)).ToArray(), order);

            Assert.AreEqual(s_raw.Length - order, model.DifferencedSeries.Count, $"d={order} full count");
            Assert.AreEqual(8 - order, model.TrainingTimeSeries.Count, $"d={order} training count");
            Assert.AreEqual(s_startDate.AddDays(order), model.DifferencedSeries[0].Index, $"d={order} first date");
            Assert.AreEqual(s_startDate.AddDays(7), model.TrainingTimeSeries[^1].Index, $"d={order} last training date");
            AssertArrayEqual(expectedFull, model.DifferencedSeries.ValuesToArray(), $"d={order} full values");
            AssertArrayEqual(expectedFull.Take(8 - order).ToArray(), model.TrainingTimeSeries.ValuesToArray(), $"d={order} training values");
        }
    }

    /// <summary>
    /// Verifies response and covariate holdout mutations cannot enter transformed/differenced
    /// training state, default parameters, residuals, or likelihood.
    /// </summary>
    [TestMethod]
    public void TrainingState_IsolatedFromResponseAndCovariateHoldout()
    {
        double[] alternateRaw = s_raw.Take(8).Concat(new[] { 3002.0, 0.041 }).ToArray();
        double[] alternateCovariate = s_covariate.Take(8).Concat(new[] { -900.0, 1200.0 }).ToArray();
        ARIMAX baseline = CreateModel(1, CreateSeries(s_raw), CreateSeries(s_covariate), ModelTransform.BoxCox);
        ARIMAX mutated = CreateModel(1, CreateSeries(alternateRaw), CreateSeries(alternateCovariate), ModelTransform.BoxCox);
        double[] parameters = CreateParameters();

        AssertArrayEqual(baseline.TrainingTimeSeries.ValuesToArray(), mutated.TrainingTimeSeries.ValuesToArray(), "training values");
        AssertArrayEqual(
            baseline.Parameters.Select(parameter => parameter.Value).ToArray(),
            mutated.Parameters.Select(parameter => parameter.Value).ToArray(),
            "default parameters");
        AssertArrayEqual(baseline.Residuals(parameters), mutated.Residuals(parameters), "residuals");
        Assert.AreEqual(baseline.DataLogLikelihood(parameters), mutated.DataLogLikelihood(parameters), Tolerance);
    }

    /// <summary>
    /// Verifies a same-length positional covariate shift is rejected by exact timestamps and every
    /// numerical likelihood decomposition preserves its documented shape while returning negative
    /// infinity.
    /// </summary>
    [TestMethod]
    public void ShiftedCovariate_IsRejectedWithoutPositionalFallback()
    {
        NumericTimeSeries shifted = CreateSeries(s_covariate, s_startDate.AddDays(1));
        ARIMAX model = CreateModel(0, CreateSeries(s_raw), shifted, ModelTransform.None);
        double[] parameters = CreateParameters();
        var validation = model.Validate();

        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.Contains("missing required timestamp", StringComparison.Ordinal)));
        Assert.AreEqual(double.NegativeInfinity, model.DataLogLikelihood(parameters));

        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        Assert.AreEqual(7, pointwise.Length);
        Assert.IsTrue(pointwise.All(value => value == double.NegativeInfinity));
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);
        Assert.AreEqual(pointwise.Length, components.Count);
        Assert.IsTrue(components.All(component => component.LogLikelihood == double.NegativeInfinity));
    }

    /// <summary>
    /// Verifies duplicate required timestamps are rejected while extra dates outside the required
    /// training window are harmless.
    /// </summary>
    [TestMethod]
    public void CovariateValidation_RejectsRequiredDuplicatesAndAllowsExtraDates()
    {
        NumericTimeSeries duplicate = CreateSeries(s_covariate);
        duplicate[1].Index = duplicate[0].Index;
        ARIMAX duplicateModel = CreateModel(0, CreateSeries(s_raw), duplicate, ModelTransform.None);
        var duplicateValidation = duplicateModel.Validate();
        Assert.IsFalse(duplicateValidation.IsValid);
        Assert.IsTrue(duplicateValidation.ValidationMessages.Any(message => message.Contains("duplicate required timestamp", StringComparison.Ordinal)));

        double[] extendedValues = { 99.0, 2.0, -1.0, 0.5, 3.0, -2.0, 1.5, 4.0, -0.5, 8.0, -7.0, 101.0 };
        NumericTimeSeries extended = CreateSeries(extendedValues, s_startDate.AddDays(-1));
        ARIMAX extendedModel = CreateModel(0, CreateSeries(s_raw), extended, ModelTransform.None);
        var extendedValidation = extendedModel.Validate();
        Assert.IsTrue(extendedValidation.IsValid, string.Join(Environment.NewLine, extendedValidation.ValidationMessages));
        Assert.IsTrue(double.IsFinite(extendedModel.DataLogLikelihood(CreateParameters())));
    }

    /// <summary>
    /// Verifies a same-length positional covariate shift produces the date-pairing migration hint
    /// exactly once, positioned after every missing-timestamp error it accompanies.
    /// </summary>
    [TestMethod]
    public void ShiftedCovariate_ValidationHint_AppearsOnceAfterMissingTimestampErrors()
    {
        NumericTimeSeries shifted = CreateSeries(s_covariate, s_startDate.AddDays(1));
        ARIMAX model = CreateModel(0, CreateSeries(s_raw), shifted, ModelTransform.None);
        var validation = model.Validate();

        Assert.IsFalse(validation.IsValid);
        int lastMissingIndex = -1;
        int hintIndex = -1;
        for (int i = 0; i < validation.ValidationMessages.Count; i++)
        {
            if (validation.ValidationMessages[i].Contains("missing required timestamp", StringComparison.Ordinal))
                lastMissingIndex = i;
            if (validation.ValidationMessages[i] == CovariateDateAlignmentHint)
                hintIndex = i;
        }

        Assert.AreNotEqual(-1, lastMissingIndex, "Expected at least one missing-timestamp error.");
        Assert.AreNotEqual(-1, hintIndex, "Expected the date-pairing migration hint.");
        Assert.IsTrue(hintIndex > lastMissingIndex, "Expected the hint after every missing-timestamp error.");
        Assert.AreEqual(1, validation.ValidationMessages.Count(message => message == CovariateDateAlignmentHint), "Expected the hint exactly once.");
    }

    /// <summary>
    /// Verifies two covariates that each miss required timestamps still produce the migration
    /// hint exactly once, not once per covariate.
    /// </summary>
    [TestMethod]
    public void TwoCovariatesMissingDates_ValidationHint_AppearsExactlyOnce()
    {
        NumericTimeSeries shiftedA = CreateSeries(s_covariate, s_startDate.AddDays(1));
        NumericTimeSeries shiftedB = CreateSeries(s_covariate, s_startDate.AddDays(2));
        ARIMAX model = CreateModel(0, CreateSeries(s_raw), shiftedA, ModelTransform.None);
        model.SetCovariates(new List<NumericTimeSeries> { shiftedA, shiftedB });
        var validation = model.Validate();

        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.Contains("Covariate 1 is missing required timestamp", StringComparison.Ordinal)));
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.Contains("Covariate 2 is missing required timestamp", StringComparison.Ordinal)));
        Assert.AreEqual(1, validation.ValidationMessages.Count(message => message == CovariateDateAlignmentHint), "Expected the hint exactly once across both covariates.");
    }

    /// <summary>
    /// Verifies a duplicated required timestamp with no missing date produces no migration hint —
    /// the hint is specific to a covariate that cannot supply a required date at all.
    /// </summary>
    [TestMethod]
    public void DuplicateTimestampWithoutMissingDate_NoValidationHint()
    {
        NumericTimeSeries covariate = CreateSeries(s_covariate);
        covariate[9].Index = covariate[0].Index; // Duplicates day 0 using the "extra" day-9 date; every required date (days 0-7) stays covered.
        ARIMAX model = CreateModel(0, CreateSeries(s_raw), covariate, ModelTransform.None);
        var validation = model.Validate();

        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.Contains("duplicate required timestamp", StringComparison.Ordinal)));
        Assert.IsFalse(validation.ValidationMessages.Any(message => message.Contains("missing required timestamp", StringComparison.Ordinal)));
        Assert.IsFalse(validation.ValidationMessages.Contains(CovariateDateAlignmentHint), "Did not expect the hint when only a duplicate timestamp is present.");
    }

    /// <summary>
    /// Verifies a fully aligned covariate validates cleanly with no migration hint.
    /// </summary>
    [TestMethod]
    public void AlignedCovariate_NoValidationHint()
    {
        ARIMAX model = CreateModel(0, CreateSeries(s_raw), CreateSeries(s_covariate), ModelTransform.None);
        var validation = model.Validate();

        Assert.IsTrue(validation.IsValid, string.Join(Environment.NewLine, validation.ValidationMessages));
        Assert.IsFalse(validation.ValidationMessages.Contains(CovariateDateAlignmentHint), "Did not expect the hint for a clean alignment.");
    }

    /// <summary>
    /// Verifies direct timestamp edits invalidate and then restore the cached exact-date map
    /// before numerical evaluation, without requiring a separate validation call.
    /// </summary>
    [TestMethod]
    public void CovariateTimestampMutation_AtomicallyRefreshesNumericalAlignment()
    {
        ARIMAX model = CreateModel(0, CreateSeries(s_raw), CreateSeries(s_covariate), ModelTransform.None);
        double[] parameters = CreateParameters();
        DateTime originalDate = model.Covariates[0][0].Index;

        Assert.IsTrue(double.IsFinite(model.DataLogLikelihood(parameters)));
        model.Covariates[0][0].Index = originalDate.AddHours(1);
        Assert.AreEqual(double.NegativeInfinity, model.DataLogLikelihood(parameters));

        model.Covariates[0][0].Index = originalDate;
        Assert.IsTrue(double.IsFinite(model.DataLogLikelihood(parameters)));
    }

    /// <summary>
    /// Verifies changing either conditional AR/MA order after data attachment rebuilds the
    /// aligned transformation Jacobian for the new conditioning range.
    /// </summary>
    [TestMethod]
    public void ConditionalOrderChanges_RebuildAlignedJacobian()
    {
        ARIMAX expectedAr = CreateModel(1, CreateSeries(s_raw), CreateSeries(s_covariate), ModelTransform.BoxCox, 2, 1);
        ARIMAX changedAr = CreateModel(1, CreateSeries(s_raw), CreateSeries(s_covariate), ModelTransform.BoxCox);
        changedAr.AROrderP = 2;
        double[] arParameters = { 0.25, 1.1, 0.3, -0.1, -0.2, 0.75 };
        Assert.AreEqual(expectedAr.DataLogLikelihood(arParameters), changedAr.DataLogLikelihood(arParameters), Tolerance);

        ARIMAX expectedMa = CreateModel(1, CreateSeries(s_raw), CreateSeries(s_covariate), ModelTransform.BoxCox, 1, 2);
        ARIMAX changedMa = CreateModel(1, CreateSeries(s_raw), CreateSeries(s_covariate), ModelTransform.BoxCox);
        changedMa.MAOrderQ = 2;
        double[] maParameters = { 0.25, 1.1, 0.3, -0.2, 0.15, 0.75 };
        Assert.AreEqual(expectedMa.DataLogLikelihood(maParameters), changedMa.DataLogLikelihood(maParameters), Tolerance);
    }

    /// <summary>
    /// Verifies a differenced response uses the level covariate at the corresponding later raw
    /// response date and that scalar, pointwise, and component likelihoods agree.
    /// </summary>
    [TestMethod]
    public void DifferencedLikelihood_UsesDateIndexedLevelCovariateAndAlignedJacobian()
    {
        ARIMAX model = CreateModel(1, CreateSeries(s_raw), CreateSeries(s_covariate), ModelTransform.BoxCox);
        double[] parameters = CreateParameters();
        double[] residuals = model.Residuals(parameters);

        double[] transformed = s_raw.Select(value => BoxCox.Transform(value, 0.4)).ToArray();
        double[] differences = Difference(transformed, 1);
        double mean0 = 0.25 + 1.1 * s_covariate[1];
        double mean1 = 0.25 + 1.1 * s_covariate[2];
        double expectedSecondResidual = differences[1] - (mean1 + 0.3 * (differences[0] - mean0));
        Assert.AreEqual(0.0, residuals[0], Tolerance);
        Assert.AreEqual(expectedSecondResidual, residuals[1], Tolerance);

        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);
        Assert.AreEqual(6, pointwise.Length);
        Assert.AreEqual(pointwise.Length, components.Count);
        Assert.AreEqual(pointwise.Sum(), model.DataLogLikelihood(parameters), Tolerance);
        Assert.AreEqual(pointwise.Sum(), components.Sum(component => component.LogLikelihood), Tolerance);
        Assert.AreEqual(model.TrainingTimeSeries[1].Value, components[0].Value, Tolerance);
    }

    /// <summary>
    /// Creates the common ARIMAX(1,d,1,0) regression fixture without fitting a transform.
    /// </summary>
    /// <param name="differencingOrder">The differencing order.</param>
    /// <param name="response">The response series.</param>
    /// <param name="covariate">The level covariate series.</param>
    /// <param name="transform">The configured transform.</param>
    /// <param name="arOrder">The autoregressive order.</param>
    /// <param name="maOrder">The moving-average order.</param>
    /// <returns>The configured model.</returns>
    private static ARIMAX CreateModel(
        int differencingOrder,
        NumericTimeSeries response,
        NumericTimeSeries covariate,
        ModelTransform transform,
        int arOrder = 1,
        int maOrder = 1)
    {
        var model = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = arOrder,
            DiffOrderD = differencingOrder,
            MAOrderQ = maOrder,
            XOrderB = 0,
        };
        model.TransformType = transform;
        if (transform == ModelTransform.BoxCox)
            model.SetTransformParameters(0.4, double.NaN);
        model.TimeSeries = response;
        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = 8;
        model.SetCovariates(new List<NumericTimeSeries> { covariate });
        return model;
    }

    /// <summary>
    /// Creates a daily series.
    /// </summary>
    /// <param name="values">The ordinate values.</param>
    /// <param name="startDate">The optional first timestamp.</param>
    /// <returns>The time series.</returns>
    private static NumericTimeSeries CreateSeries(double[] values, DateTime? startDate = null) =>
        new(TimeInterval.OneDay, startDate ?? s_startDate, values);

    /// <summary>
    /// Returns the fixed intercept, level-covariate, AR, MA, and innovation-scale parameters.
    /// </summary>
    /// <returns>The parameter vector.</returns>
    private static double[] CreateParameters() => new[] { 0.25, 1.1, 0.3, -0.2, 0.75 };

    /// <summary>
    /// Independently computes successive first differences.
    /// </summary>
    /// <param name="values">The input values.</param>
    /// <param name="order">The difference order.</param>
    /// <returns>The differenced values.</returns>
    private static double[] Difference(double[] values, int order)
    {
        double[] result = values;
        for (int difference = 0; difference < order; difference++)
        {
            var next = new double[result.Length - 1];
            for (int i = 0; i < next.Length; i++)
                next[i] = result[i + 1] - result[i];
            result = next;
        }
        return result;
    }

    /// <summary>
    /// Compares deterministic numeric vectors.
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
}
