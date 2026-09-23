using System.Text.Json;
using System.Globalization;
using Numerics.Data;
using RMC.BestFit.Models;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Verifies the retained AR, MA, ARIMA, and ARIMAX numerical cases against the
/// independently generated Python artifact <c>chunk13-independent-oracle.json</c>.
/// </summary>
/// <remarks>
/// The artifact implements the conditional recurrences directly. It fixes parameter order,
/// coefficient signs, intercept meaning, innovation scale, burn-in, retained observational units,
/// likelihood contribution counts, date alignment, seasonal period, and optimizer diagnostics
/// before the C# results are evaluated.
/// </remarks>
[TestClass]
public class TimeSeriesChunk13OracleTests
{
    /// <summary>The copied independent artifact.</summary>
    private const string ArtifactFileName = "chunk13-independent-oracle.json";

    /// <summary>
    /// Verifies first-order AR and MA residuals and conditional likelihoods at the generating truth
    /// and independently optimized coordinates. It also proves that the preserved AR(1) default-DE
    /// result is below both the parent and independent optimum rather than a failed generator.
    /// </summary>
    [TestMethod]
    public void FirstOrderConditionalObjectivesMatchIndependentPythonOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement root = document.RootElement;
        double tolerance = root.GetProperty("metadata").GetProperty("absolute_tolerance").GetDouble();

        JsonElement arCase = root.GetProperty("first_order").GetProperty("ar");
        JsonElement arFixture = TimeSeriesIndependentRecoveryTests.LoadFixture("ar");
        var ar = new AutoRegressive(
            TimeSeriesIndependentRecoveryTests.CreateSeries(arFixture, "raw", TimeInterval.OneMonth),
            order: 1,
            includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TrainingTimeSteps = 1000,
        };
        AssertObjective(arCase, ar.Residuals, ar.DataLogLikelihood, tolerance, "AR(1)");

        JsonElement failure = root.GetProperty("known_default_de_failure");
        double[] failedCoordinates = ReadDoubles(failure.GetProperty("parameters"));
        double attained = failure.GetProperty("independent_coordinate_log_likelihood").GetDouble();
        double reportedAttained = failure.GetProperty("reported_attained_log_likelihood").GetDouble();
        double parent = arCase.GetProperty("truth_log_likelihood").GetDouble();
        double optimum = arCase.GetProperty("independent_optimum_log_likelihood").GetDouble();
        Assert.AreEqual(attained, ar.DataLogLikelihood(failedCoordinates), tolerance, "Stored DE-coordinate likelihood.");
        Assert.AreEqual(reportedAttained, attained, 1E-6, "Stored runner likelihood versus independent recurrence.");
        Assert.IsTrue(parent > attained, $"Parent {parent:G12} must dominate the preserved DE result {attained:G12}.");
        Assert.IsTrue(optimum >= parent, $"Independent optimum {optimum:G12} versus parent {parent:G12}.");

        JsonElement maCase = root.GetProperty("first_order").GetProperty("ma");
        JsonElement maFixture = TimeSeriesIndependentRecoveryTests.LoadFixture("ma");
        var ma = new MovingAverage(
            TimeSeriesIndependentRecoveryTests.CreateSeries(maFixture, "raw", TimeInterval.OneMonth),
            order: 1,
            includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TrainingTimeSteps = 1000,
        };
        AssertObjective(maCase, ma.Residuals, ma.DataLogLikelihood, tolerance, "MA(1)");
    }

    /// <summary>
    /// Verifies genuinely distinct AR(2) and MA(2) conditional recurrences on independently generated
    /// 1,000-observation post-burn-in fixtures, including their predeclared residual prefixes,
    /// likelihood contribution counts, and stationary/invertible dynamic-response roots.
    /// </summary>
    [TestMethod]
    public void HigherOrderArAndMaResponsesMatchIndependentPythonOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement root = document.RootElement;
        double tolerance = root.GetProperty("metadata").GetProperty("absolute_tolerance").GetDouble();

        JsonElement arCase = root.GetProperty("higher_order").GetProperty("ar2");
        var ar = new AutoRegressive(CreateSeries(arCase, "values"), order: 2, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TrainingTimeSteps = 1000,
        };
        double[] arParameters = ReadDoubles(arCase.GetProperty("parameters"));
        AssertFixedRecurrence(arCase, ar.Residuals(arParameters), ar.DataLogLikelihood(arParameters), tolerance, "AR(2)");
        Assert.AreEqual(998, ar.PointwiseDataLogLikelihood(arParameters).Length, "AR(2) conditional contribution count.");
        SetParameters(ar, arParameters);
        Assert.IsTrue(ar.IsStationary(), "Production AR(2) stationarity diagnostic.");
        Assert.AreEqual(
            arCase.GetProperty("one_step_conditional_mean").GetDouble(),
            ar.Predict(forecastSteps: 1)[^1],
            tolerance,
            "AR(2) one-step conditional response.");
        AssertRootsOutsideUnitCircle(arCase, "AR(2) stationarity");

        JsonElement maCase = root.GetProperty("higher_order").GetProperty("ma2");
        var ma = new MovingAverage(CreateSeries(maCase, "values"), order: 2, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TrainingTimeSteps = 1000,
        };
        double[] maParameters = ReadDoubles(maCase.GetProperty("parameters"));
        AssertFixedRecurrence(maCase, ma.Residuals(maParameters), ma.DataLogLikelihood(maParameters), tolerance, "MA(2)");
        Assert.AreEqual(1000, ma.PointwiseDataLogLikelihood(maParameters).Length, "MA(2) conditional contribution count.");
        SetParameters(ma, maParameters);
        Assert.IsTrue(ma.IsInvertible(), "Production MA(2) invertibility diagnostic.");
        Assert.AreEqual(
            maCase.GetProperty("one_step_conditional_mean").GetDouble(),
            ma.Predict(forecastSteps: 1)[^1],
            tolerance,
            "MA(2) one-step conditional response.");
        AssertRootsOutsideUnitCircle(maCase, "MA(2) invertibility");
    }

    /// <summary>
    /// Verifies pure AR(2) and pure MA(2) behavior through the ARIMA implementation against
    /// independently evaluated d=0 conditional recurrences, contribution counts, and responses.
    /// </summary>
    [TestMethod]
    public void PureArAndMaThroughArimaMatchIndependentPythonOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement root = document.RootElement;
        double tolerance = root.GetProperty("metadata").GetProperty("absolute_tolerance").GetDouble();

        JsonElement arCase = root.GetProperty("pure_arima").GetProperty("ar2");
        var pureAr = new ARIMA(CreateSeries(arCase, "values"), pOrder: 2, dOrder: 0, qOrder: 0, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TrainingTimeSteps = 1000,
        };
        double[] arParameters = ReadDoubles(arCase.GetProperty("parameters"));
        AssertFixedRecurrence(arCase, pureAr.Residuals(arParameters), pureAr.DataLogLikelihood(arParameters), tolerance, "ARIMA(2,0,0)");
        Assert.AreEqual(998, pureAr.PointwiseDataLogLikelihood(arParameters).Length, "Pure-AR ARIMA contribution count.");
        SetParameters(pureAr, arParameters);
        Assert.IsTrue(pureAr.IsStationary(), "Pure-AR ARIMA stationarity diagnostic.");
        Assert.AreEqual(arCase.GetProperty("one_step_conditional_mean").GetDouble(), pureAr.Predict(forecastSteps: 1)[^1], tolerance, "Pure-AR ARIMA one-step response.");

        JsonElement maCase = root.GetProperty("pure_arima").GetProperty("ma2");
        var pureMa = new ARIMA(CreateSeries(maCase, "values"), pOrder: 0, dOrder: 0, qOrder: 2, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            TrainingTimeSteps = 1000,
        };
        double[] maParameters = ReadDoubles(maCase.GetProperty("parameters"));
        AssertFixedRecurrence(maCase, pureMa.Residuals(maParameters), pureMa.DataLogLikelihood(maParameters), tolerance, "ARIMA(0,0,2)");
        Assert.AreEqual(998, pureMa.PointwiseDataLogLikelihood(maParameters).Length, "Pure-MA ARIMA contribution count.");
        SetParameters(pureMa, maParameters);
        Assert.IsTrue(pureMa.IsInvertible(), "Pure-MA ARIMA invertibility diagnostic.");
        Assert.AreEqual(maCase.GetProperty("one_step_conditional_mean").GetDouble(), pureMa.Predict(forecastSteps: 1)[^1], tolerance, "Pure-MA ARIMA one-step response.");
    }

    /// <summary>
    /// Verifies the consolidated ARIMAX interaction cell: ARMA(1,1), conditional intercept, linear
    /// trend, monthly Fourier seasonality, and two exact-date current level covariates on exactly
    /// 1,000 retained raw observations.
    /// </summary>
    [TestMethod]
    public void ArimaxTrendSeasonalityAndCovariatesMatchIndependentPythonOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement root = document.RootElement;
        JsonElement item = root.GetProperty("arimax_interaction");
        double tolerance = root.GetProperty("metadata").GetProperty("absolute_tolerance").GetDouble();
        NumericTimeSeries response = CreateSeries(item, "values", "response_start_date");
        NumericTimeSeries covariate1 = CreateSeries(item, "covariate_1", "covariate_1_start_date");
        NumericTimeSeries covariate2 = CreateSeries(item, "covariate_2", "covariate_2_start_date");
        var model = new ARIMAX(response)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            IncludeSeasonality = true,
            AROrderP = 1,
            DiffOrderD = 0,
            MAOrderQ = 1,
            XOrderB = 0,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
            UseDefaultTrainingSteps = false,
        };
        model.SetCovariates([covariate1, covariate2]);
        model.TrainingTimeSteps = response.Count;
        model.SetDefaultParameters();
        double[] parameters = ReadDoubles(item.GetProperty("parameters"));

        Assert.AreEqual(12, model.SeasonalPeriod, "Monthly seasonal period.");
        Assert.AreEqual(1000, response.Count, "Retained raw observational units.");
        Assert.AreEqual(1001, covariate1.Count, "First offset covariate storage length.");
        Assert.AreEqual(1002, covariate2.Count, "Second offset covariate storage length.");
        Assert.AreEqual(parameters.Length, model.NumberOfParameters, "Declared parameter order length.");
        AssertFixedRecurrence(item, model.Residuals(parameters), model.DataLogLikelihood(parameters), tolerance, "ARIMAX interaction");
        Assert.IsTrue(
            Math.Abs(item.GetProperty("index_aligned_log_likelihood").GetDouble() - model.DataLogLikelihood(parameters)) > 1.0,
            "The oracle must discriminate exact-date alignment from raw index alignment.");
        Assert.AreEqual(999, model.PointwiseDataLogLikelihood(parameters).Length, "ARIMAX conditional contribution count.");
        Assert.IsTrue(item.GetProperty("ar_root_modulus").GetDouble() > 1.0, "AR root is outside the unit circle.");
        Assert.IsTrue(item.GetProperty("ma_root_modulus").GetDouble() > 1.0, "MA root is outside the unit circle.");
    }

    /// <summary>
    /// Compares an independently tabulated first-order objective at truth and optimum.
    /// </summary>
    /// <param name="item">The oracle case.</param>
    /// <param name="residualFunction">The production residual recurrence.</param>
    /// <param name="likelihoodFunction">The production likelihood.</param>
    /// <param name="tolerance">The absolute tolerance.</param>
    /// <param name="label">The assertion label.</param>
    private static void AssertObjective(
        JsonElement item,
        Func<double[], double[]> residualFunction,
        Func<double[], double> likelihoodFunction,
        double tolerance,
        string label)
    {
        double[] truth = ReadDoubles(item.GetProperty("truth"));
        double[] optimum = ReadDoubles(item.GetProperty("independent_optimum"));
        double[] standardizedErrors = ReadDoubles(item.GetProperty("absolute_standardized_parent_errors"));
        Assert.IsTrue(item.GetProperty("optimizer").GetProperty("success").GetBoolean(), $"{label} independent optimizer.");
        AssertPrefix(item, residualFunction(truth), tolerance, label);
        Assert.AreEqual(item.GetProperty("truth_log_likelihood").GetDouble(), likelihoodFunction(truth), tolerance, $"{label} parent likelihood.");
        Assert.AreEqual(item.GetProperty("independent_optimum_log_likelihood").GetDouble(), likelihoodFunction(optimum), tolerance, $"{label} optimum likelihood.");
        foreach (double standardizedError in standardizedErrors)
            Assert.IsTrue(standardizedError <= 1.96, $"{label} standardized parent error {standardizedError:G12}.");
    }

    /// <summary>
    /// Compares a fixed recurrence's residual prefix and total conditional likelihood.
    /// </summary>
    /// <param name="item">The oracle case.</param>
    /// <param name="residuals">The production residuals.</param>
    /// <param name="likelihood">The production likelihood.</param>
    /// <param name="tolerance">The absolute tolerance.</param>
    /// <param name="label">The assertion label.</param>
    private static void AssertFixedRecurrence(JsonElement item, double[] residuals, double likelihood, double tolerance, string label)
    {
        AssertPrefix(item, residuals, tolerance, label);
        Assert.AreEqual(item.GetProperty("log_likelihood").GetDouble(), likelihood, tolerance, $"{label} likelihood.");
    }

    /// <summary>Compares the predeclared residual prefix.</summary>
    /// <param name="item">The oracle case.</param>
    /// <param name="residuals">The production residuals.</param>
    /// <param name="tolerance">The absolute tolerance.</param>
    /// <param name="label">The assertion label.</param>
    private static void AssertPrefix(JsonElement item, double[] residuals, double tolerance, string label)
    {
        string property = item.TryGetProperty("residual_prefix_at_truth", out _) ? "residual_prefix_at_truth" : "residual_prefix";
        double[] expected = ReadDoubles(item.GetProperty(property));
        for (int index = 0; index < expected.Length; index++)
            Assert.AreEqual(expected[index], residuals[index], tolerance, $"{label} residual {index}.");
    }

    /// <summary>Asserts all independently computed response roots lie outside the unit circle.</summary>
    /// <param name="item">The oracle case.</param>
    /// <param name="label">The assertion label.</param>
    private static void AssertRootsOutsideUnitCircle(JsonElement item, string label)
    {
        foreach (double modulus in ReadDoubles(item.GetProperty("root_moduli")))
            Assert.IsTrue(modulus > 1.0, $"{label}: root modulus {modulus:G12}.");
    }

    /// <summary>Assigns declared physical coordinates to a production model.</summary>
    /// <param name="model">The model to update.</param>
    /// <param name="parameters">The physical coordinates in production order.</param>
    private static void SetParameters(ModelBase model, double[] parameters)
    {
        Assert.AreEqual(model.Parameters.Count, parameters.Length, "Parameter assignment length.");
        for (int index = 0; index < parameters.Length; index++)
            model.Parameters[index].Value = parameters[index];
    }

    /// <summary>Creates a monthly series beginning on the declared fixed epoch.</summary>
    /// <param name="item">The oracle case.</param>
    /// <param name="property">The value-array property.</param>
    /// <param name="startDateProperty">Optional oracle property containing the ISO start date; otherwise 1 January 2000.</param>
    /// <returns>The dated monthly series.</returns>
    private static NumericTimeSeries CreateSeries(JsonElement item, string property, string? startDateProperty = null)
    {
        DateTime startDate = startDateProperty == null
            ? new DateTime(2000, 1, 1)
            : DateTime.ParseExact(item.GetProperty(startDateProperty).GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new NumericTimeSeries(TimeInterval.OneMonth, startDate, ReadDoubles(item.GetProperty(property)));
    }

    /// <summary>Reads a JSON number array.</summary>
    /// <param name="element">The array.</param>
    /// <returns>The values.</returns>
    private static double[] ReadDoubles(JsonElement element) =>
        element.EnumerateArray().Select(value => value.GetDouble()).ToArray();

    /// <summary>Loads the committed artifact from the test output.</summary>
    /// <returns>The parsed document.</returns>
    private static JsonDocument LoadDocument()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "VerificationData", ArtifactFileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
