using System.Text.Json;
using Numerics.Data;
using Numerics.Data.Statistics;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Verifies Phase 5 time-series parameter recovery from independently generated R fixtures.
/// </summary>
/// <remarks>
/// Every raw fixture contains exactly 1,000 observations retained after a 110-step stationary
/// ARMA initialization period. Bayesian recovery uses the resolved production
/// <see cref="BayesianAnalysis"/> defaults without test-side changes to the sampler or its settings.
/// </remarks>
[TestClass]
public class Phase5TimeSeriesRecoveryTests
{
    private const int MaximumVerificationSteps = 1000;
    private const int RecoveryBurnInSteps = 110;
    private const double PredictionTolerance = 1E-10;

    /// <summary>
    /// Verifies MLE recovery for a logarithmic ARIMA(1,1,1) fixture generated directly in R.
    /// </summary>
    [TestMethod]
    public void MleArima111LogD1RecoversGeneratingParameters()
    {
        JsonElement fixture = LoadFixture("arima");
        ARIMA model = CreateArimaModel(fixture);
        double[] truth = GetArimaTruth(fixture);
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);

        mle.Estimate();

        Assert.IsTrue(mle.IsEstimated, "ARIMA MLE did not complete.");
        AssertMleRecovery("ARIMA", model, truth, mle.BestParameterSet.Values, 0.15, 0.10);
        AssertArimaPrediction(model, truth, fixture);
    }

    /// <summary>
    /// Verifies Bayesian recovery for a logarithmic ARIMA(1,1,1) fixture generated directly in R.
    /// </summary>
    [TestMethod]
    public async Task BayesianArima111LogD1RecoversGeneratingParameters()
    {
        JsonElement fixture = LoadFixture("arima");
        ARIMA model = CreateArimaModel(fixture);
        double[] truth = GetArimaTruth(fixture);
        var analysis = new ARIMAAnalysis(model);
        AssertResolvedBayesianDefaults("ARIMA", analysis.BayesianAnalysis, model.NumberOfParameters);

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, "ARIMA Bayesian estimation did not complete.");
        AssertBayesianRecovery("ARIMA", model, analysis.BayesianAnalysis, truth);
        AssertArimaPrediction(model, truth, fixture);
    }

    /// <summary>
    /// Verifies MLE recovery for a differenced ARIMAX model with an exact-date level covariate.
    /// </summary>
    [TestMethod]
    public void MleArimax10D1LevelCovariateRecoversGeneratingParameters()
    {
        JsonElement fixture = LoadFixture("arimax");
        ARIMAX model = CreateArimaxModel(fixture);
        double[] truth = GetArimaxTruth(fixture);
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);

        mle.Estimate();

        Assert.IsTrue(mle.IsEstimated, "ARIMAX MLE did not complete.");
        AssertMleRecovery("ARIMAX", model, truth, mle.BestParameterSet.Values, 0.15, 0.10);
        AssertArimaxPrediction(model, truth, fixture);
    }

    /// <summary>
    /// Verifies Bayesian recovery for a differenced ARIMAX model with an exact-date level covariate.
    /// </summary>
    [TestMethod]
    public async Task BayesianArimax10D1LevelCovariateRecoversGeneratingParameters()
    {
        JsonElement fixture = LoadFixture("arimax");
        ARIMAX model = CreateArimaxModel(fixture);
        double[] truth = GetArimaxTruth(fixture);
        var analysis = new ARIMAXAnalysis(model);
        AssertResolvedBayesianDefaults("ARIMAX", analysis.BayesianAnalysis, model.NumberOfParameters);

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, "ARIMAX Bayesian estimation did not complete.");
        AssertBayesianRecovery("ARIMAX", model, analysis.BayesianAnalysis, truth);
        AssertArimaxPrediction(model, truth, fixture);
    }

    /// <summary>
    /// Loads one named fixture from the committed Phase 5 recovery artifact.
    /// </summary>
    /// <param name="name">The lower-case fixture name.</param>
    /// <returns>A detached JSON fixture element.</returns>
    internal static JsonElement LoadFixture(string name)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            "phase5-recovery-fixtures.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement metadata = document.RootElement.GetProperty("metadata");
        Assert.AreEqual(MaximumVerificationSteps, metadata.GetProperty("maximum_steps").GetInt32());
        Assert.AreEqual(MaximumVerificationSteps, metadata.GetProperty("retained_sample_size").GetInt32());
        Assert.AreEqual(RecoveryBurnInSteps, metadata.GetProperty("burn_in").GetInt32());
        Assert.AreEqual(
            "discarded stationary ARMA recursion",
            metadata.GetProperty("initialization_policy").GetString());
        JsonElement fixture = document.RootElement.GetProperty(name);
        Assert.AreEqual(MaximumVerificationSteps, fixture.GetProperty("sample_size").GetInt32());
        Assert.AreEqual(RecoveryBurnInSteps, fixture.GetProperty("burn_in").GetInt32());
        int differencingOrder = fixture.TryGetProperty("differencing_order", out JsonElement order)
            ? order.GetInt32()
            : 0;
        int retainedModelSteps = MaximumVerificationSteps - differencingOrder;
        Assert.AreEqual(retainedModelSteps, fixture.GetProperty("retained_model_steps").GetInt32());
        Assert.AreEqual(
            RecoveryBurnInSteps + retainedModelSteps,
            fixture.GetProperty("total_model_steps").GetInt32());
        Assert.AreEqual(MaximumVerificationSteps, fixture.GetProperty("dates").GetArrayLength());
        Assert.AreEqual(MaximumVerificationSteps, fixture.GetProperty("raw").GetArrayLength());
        return fixture.Clone();
    }

    /// <summary>
    /// Creates a regular dated series from a committed fixture array.
    /// </summary>
    /// <param name="fixture">The fixture element.</param>
    /// <param name="valueProperty">The array property containing ordinates.</param>
    /// <param name="interval">The declared regular interval.</param>
    /// <returns>The constructed series.</returns>
    internal static NumericTimeSeries CreateSeries(JsonElement fixture, string valueProperty, TimeInterval interval)
    {
        double[] values = fixture.GetProperty(valueProperty)
            .EnumerateArray()
            .Select(item => item.GetDouble())
            .ToArray();
        DateTime startDate = DateTime.Parse(
            fixture.GetProperty("dates")[0].GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(MaximumVerificationSteps, values.Length);
        return new NumericTimeSeries(interval, startDate, values);
    }

    /// <summary>
    /// Gets the AR(1) generating parameter vector from the committed fixture.
    /// </summary>
    /// <param name="fixture">The AR fixture.</param>
    /// <returns>The vector [mu, phi, sigma].</returns>
    internal static double[] GetArTruth(JsonElement fixture)
    {
        return
        [
            fixture.GetProperty("mu").GetDouble(),
            fixture.GetProperty("phi").GetDouble(),
            fixture.GetProperty("sigma").GetDouble(),
        ];
    }

    /// <summary>
    /// Gets the MA(1) generating parameter vector from the committed fixture.
    /// </summary>
    /// <param name="fixture">The MA fixture.</param>
    /// <returns>The vector [mu, theta, sigma].</returns>
    internal static double[] GetMaTruth(JsonElement fixture)
    {
        return
        [
            fixture.GetProperty("mu").GetDouble(),
            fixture.GetProperty("theta").GetDouble(),
            fixture.GetProperty("sigma").GetDouble(),
        ];
    }

    /// <summary>
    /// Asserts the unchanged resolved production DEMCzs defaults used by a recovery run.
    /// </summary>
    /// <param name="label">The model label.</param>
    /// <param name="analysis">The Bayesian analysis.</param>
    /// <param name="parameterCount">The fitted parameter count.</param>
    internal static void AssertResolvedBayesianDefaults(
        string label,
        BayesianAnalysis analysis,
        int parameterCount)
    {
        int expectedChains = Math.Max(4, Math.Min(20, 2 * parameterCount));
        int expectedThinning = Math.Max(1, Math.Min(100, 10 * parameterCount));
        int expectedInitialIterations = Math.Min(1000, Math.Max(100, parameterCount * 100));
        double expectedJump = 2.38 / Math.Sqrt(2.0 * parameterCount);

        Assert.AreEqual(BayesianAnalysis.SamplerType.DEMCzs, analysis.Type, $"{label} sampler default.");
        Assert.IsTrue(analysis.UseSimulationDefaults, $"{label} simulation-default flag.");
        Assert.IsTrue(analysis.UseAdvancedSimulationDefaults, $"{label} advanced-default flag.");
        Assert.AreEqual(expectedChains, analysis.NumberOfChains, $"{label} chain default.");
        Assert.AreEqual(expectedThinning, analysis.ThinningInterval, $"{label} thinning default.");
        Assert.AreEqual(3500, analysis.Iterations, $"{label} iteration default.");
        Assert.AreEqual(1750, analysis.WarmupIterations, $"{label} warmup default.");
        Assert.AreEqual(expectedInitialIterations, analysis.InitialIterations, $"{label} initialization default.");
        Assert.AreEqual(12345, analysis.PRNGSeed, $"{label} seed default.");
        Assert.AreEqual(10000, analysis.OutputLength, $"{label} output default.");
        Assert.AreEqual(0.9, analysis.CredibleIntervalWidth, 0.0, $"{label} interval default.");
        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, analysis.PointEstimator, $"{label} point-estimator default.");
        Assert.AreEqual(expectedJump, analysis.Jump, 1E-14, $"{label} jump default.");
        Assert.AreEqual(0.1, analysis.JumpThreshold, 0.0, $"{label} jump-threshold default.");
        Assert.AreEqual(0.1, analysis.SnookerThreshold, 0.0, $"{label} snooker default.");
        Assert.AreEqual(1E-12, analysis.Noise, 0.0, $"{label} noise default.");
    }

    /// <summary>
    /// Asserts finite likelihood and parameter recovery for an MLE result.
    /// </summary>
    /// <param name="label">The model label.</param>
    /// <param name="model">The fitted model.</param>
    /// <param name="truth">The generating parameters.</param>
    /// <param name="estimated">The fitted parameters.</param>
    /// <param name="coefficientTolerance">Relative tolerance for non-scale parameters.</param>
    /// <param name="scaleTolerance">Relative tolerance for the final scale parameter.</param>
    internal static void AssertMleRecovery(
        string label,
        ModelBase model,
        double[] truth,
        double[] estimated,
        double coefficientTolerance,
        double scaleTolerance)
    {
        Assert.AreEqual(truth.Length, estimated.Length, $"{label} parameter count.");
        for (int index = 0; index < truth.Length; index++)
        {
            double relativeTolerance = index == truth.Length - 1 ? scaleTolerance : coefficientTolerance;
            Assert.AreEqual(
                truth[index],
                estimated[index],
                Math.Abs(truth[index]) * relativeTolerance,
                $"{label} MLE parameter {model.Parameters[index].Name}.");
        }
        Assert.IsTrue(double.IsFinite(model.DataLogLikelihood(estimated)), $"{label} recovered likelihood.");
        Assert.IsTrue(double.IsFinite(model.PriorLogLikelihood(estimated)), $"{label} recovered prior.");
    }

    /// <summary>
    /// Asserts central-interval, MAP, R-hat, ESS, and likelihood recovery for a Bayesian result.
    /// </summary>
    /// <param name="label">The model label.</param>
    /// <param name="model">The fitted model.</param>
    /// <param name="analysis">The completed Bayesian analysis.</param>
    /// <param name="truth">The generating parameters.</param>
    internal static void AssertBayesianRecovery(
        string label,
        ModelBase model,
        BayesianAnalysis analysis,
        double[] truth)
    {
        Assert.IsNotNull(analysis.Results, $"{label} Bayesian results.");
        AssertResolvedBayesianDefaults(label, analysis, model.NumberOfParameters);
        Assert.AreEqual(analysis.OutputLength, analysis.Results.Output.Count, $"{label} retained output count.");
        double[] map = analysis.Results.MAP.Values;
        Assert.AreEqual(truth.Length, map.Length, $"{label} MAP parameter count.");
        Assert.AreEqual(truth.Length, analysis.Results.ParameterResults.Length, $"{label} summary count.");

        for (int index = 0; index < truth.Length; index++)
        {
            var summary = analysis.Results.ParameterResults[index].SummaryStatistics;
            string parameterName = model.Parameters[index].Name;
            double[] retainedValues = analysis.Results.Output
                .Select(parameterSet => parameterSet.Values[index])
                .OrderBy(value => value)
                .ToArray();
            double lower95 = Statistics.Percentile(retainedValues, 0.025, true);
            double upper95 = Statistics.Percentile(retainedValues, 0.975, true);
            Assert.IsTrue(
                truth[index] >= lower95 && truth[index] <= upper95,
                $"{label} truth for {parameterName} is outside [{lower95:G8}, {upper95:G8}].");
            Assert.AreEqual(
                truth[index],
                map[index],
                Math.Abs(truth[index]) * 0.25,
                $"{label} MAP parameter {parameterName}.");
            Assert.IsTrue(
                double.IsFinite(summary.Rhat) && summary.Rhat < 1.1,
                $"{label} {parameterName} R-hat {summary.Rhat:G8}.");
            Assert.IsTrue(
                double.IsFinite(summary.ESS) && summary.ESS > 100.0,
                $"{label} {parameterName} ESS {summary.ESS:G8}.");
        }

        Assert.IsTrue(double.IsFinite(model.DataLogLikelihood(map)), $"{label} MAP data likelihood.");
        Assert.IsTrue(double.IsFinite(model.PriorLogLikelihood(map)), $"{label} MAP prior.");
    }

    /// <summary>
    /// Asserts the deterministic AR one-step prediction against the committed generating recurrence.
    /// </summary>
    /// <param name="model">The AR model.</param>
    /// <param name="truth">The generating parameters.</param>
    /// <param name="fixture">The AR fixture.</param>
    internal static void AssertArPrediction(AutoRegressive model, double[] truth, JsonElement fixture)
    {
        double actual = model.Predict(truth, forecastSteps: 1).Y[^1];
        AssertPrediction(fixture, actual, "AR");
    }

    /// <summary>
    /// Asserts the deterministic MA one-step prediction against the committed generating recurrence.
    /// </summary>
    /// <param name="model">The MA model.</param>
    /// <param name="truth">The generating parameters.</param>
    /// <param name="fixture">The MA fixture.</param>
    internal static void AssertMaPrediction(MovingAverage model, double[] truth, JsonElement fixture)
    {
        double actual = model.Predict(truth, forecastSteps: 1).Y[^1];
        AssertPrediction(fixture, actual, "MA");
    }

    /// <summary>
    /// Creates the configured logarithmic ARIMA recovery model.
    /// </summary>
    /// <param name="fixture">The ARIMA fixture.</param>
    /// <returns>The configured model.</returns>
    private static ARIMA CreateArimaModel(JsonElement fixture)
    {
        NumericTimeSeries series = CreateSeries(fixture, "raw", TimeInterval.OneDay);
        var model = new ARIMA(series, pOrder: 1, dOrder: 1, qOrder: 1, includeIntercept: false)
        {
            UseDefaultTrainingSteps = false,
            TransformType = RMC.BestFit.Models.Transform.Logarithmic,
        };
        model.TrainingTimeSteps = series.Count;
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>
    /// Creates the configured exact-date level-covariate ARIMAX recovery model.
    /// </summary>
    /// <param name="fixture">The ARIMAX fixture.</param>
    /// <returns>The configured model.</returns>
    private static ARIMAX CreateArimaxModel(JsonElement fixture)
    {
        NumericTimeSeries series = CreateSeries(fixture, "raw", TimeInterval.OneDay);
        NumericTimeSeries covariate = CreateSeries(fixture, "covariate", TimeInterval.OneDay);
        var model = new ARIMAX(series)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.None,
            IncludeSeasonality = false,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None,
        };
        model.SetCovariates([covariate]);
        model.TrainingTimeSteps = series.Count;
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>
    /// Gets the ARIMA generating parameter vector.
    /// </summary>
    /// <param name="fixture">The ARIMA fixture.</param>
    /// <returns>The vector [phi, theta, sigma].</returns>
    private static double[] GetArimaTruth(JsonElement fixture)
    {
        return
        [
            fixture.GetProperty("phi").GetDouble(),
            fixture.GetProperty("theta").GetDouble(),
            fixture.GetProperty("sigma").GetDouble(),
        ];
    }

    /// <summary>
    /// Gets the ARIMAX generating parameter vector.
    /// </summary>
    /// <param name="fixture">The ARIMAX fixture.</param>
    /// <returns>The vector [intercept, beta, phi, sigma].</returns>
    private static double[] GetArimaxTruth(JsonElement fixture)
    {
        return
        [
            fixture.GetProperty("intercept").GetDouble(),
            fixture.GetProperty("beta").GetDouble(),
            fixture.GetProperty("phi").GetDouble(),
            fixture.GetProperty("sigma").GetDouble(),
        ];
    }

    /// <summary>
    /// Asserts the deterministic ARIMA one-step prediction against the committed R recurrence.
    /// </summary>
    /// <param name="model">The ARIMA model.</param>
    /// <param name="truth">The generating parameters.</param>
    /// <param name="fixture">The ARIMA fixture.</param>
    private static void AssertArimaPrediction(ARIMA model, double[] truth, JsonElement fixture)
    {
        double actual = model.Predict(truth, forecastSteps: 1).Y[^1];
        AssertPrediction(fixture, actual, "ARIMA");
    }

    /// <summary>
    /// Asserts the deterministic ARIMAX one-step prediction against the committed R recurrence.
    /// </summary>
    /// <param name="model">The ARIMAX model.</param>
    /// <param name="truth">The generating parameters.</param>
    /// <param name="fixture">The ARIMAX fixture.</param>
    private static void AssertArimaxPrediction(ARIMAX model, double[] truth, JsonElement fixture)
    {
        JsonElement dates = fixture.GetProperty("dates");
        DateTime forecastDate = DateTime.Parse(
            dates[dates.GetArrayLength() - 1].GetString()!,
            System.Globalization.CultureInfo.InvariantCulture).AddDays(1.0);
        double nextCovariate = fixture.GetProperty("next_covariate").GetDouble();
        var tail = new NumericTimeSeries(TimeInterval.OneDay, forecastDate, [nextCovariate]);
        double actual = model.Predict(truth, forecastSteps: 1, forecastCovariates: [tail]).Y[^1];
        AssertPrediction(fixture, actual, "ARIMAX");
    }

    /// <summary>
    /// Compares a deterministic one-step prediction with the committed zero-innovation recurrence.
    /// </summary>
    /// <param name="fixture">The recovery fixture.</param>
    /// <param name="actual">The production prediction.</param>
    /// <param name="label">The model label.</param>
    private static void AssertPrediction(JsonElement fixture, double actual, string label)
    {
        double expected = fixture.GetProperty("next_raw_zero_innovation").GetDouble();
        double tolerance = Math.Max(PredictionTolerance, Math.Abs(expected) * PredictionTolerance);
        Assert.AreEqual(expected, actual, tolerance, $"{label} one-step recurrence.");
    }
}
