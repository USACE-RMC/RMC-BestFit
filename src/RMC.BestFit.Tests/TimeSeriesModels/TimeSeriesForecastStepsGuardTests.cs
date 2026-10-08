using Numerics.Data;
using RMC.BestFit.Models;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Fast, deterministic tests for the shared forecast-step contract of the four time-series model
/// classes (<see cref="AutoRegressive"/>, <see cref="MovingAverage"/>, <see cref="ARIMA"/>,
/// <see cref="ARIMAX"/>): every public <c>Predict</c> overload rejects a negative
/// <c>forecastSteps</c>, and <c>GenerateRandomSeries</c> clamps the internally derived forecast
/// step count to zero so a requested length shorter than the training window succeeds for every
/// differencing order.
/// </summary>
/// <remarks>
/// With a differenced model (d &gt; 0), <c>GenerateRandomSeries</c>/<c>Predict</c> previously threw
/// an <see cref="ArgumentOutOfRangeException"/> from the internal conditional-level reconstruction
/// when the derived forecast-step count was negative, instead of returning the leading requested
/// values as it did (and continues to do) for undifferenced models. These tests set parameter
/// values directly with <see cref="ModelBase.SetParameterValues"/> and never run an optimizer or
/// MCMC sampler.
/// </remarks>
[TestClass]
public class TimeSeriesForecastStepsGuardTests
{
    /// <summary>
    /// First timestamp of every daily fixture series built by <see cref="CreateSeries"/>.
    /// </summary>
    private static readonly DateTime s_startDate = new(2000, 1, 1);

    /// <summary>
    /// Twelve-step raw fixture shared by every model in this file.
    /// </summary>
    private static readonly double[] s_raw =
    {
        12.0, 15.5, 14.2, 18.9, 21.3, 19.8, 24.6, 23.1, 27.4, 26.0, 30.2, 29.5
    };

    /// <summary>
    /// Intercept, AR/MA coefficient, and innovation scale shared by the fixtures in this file
    /// (mu, phi1/theta1, sigma).
    /// </summary>
    private static readonly double[] s_parameters = { 0.5, 0.3, 1.2 };

    /// <summary>
    /// Verifies that an ARIMA(1,1,0) model -- a differenced model, d &gt; 0 -- accepts a
    /// <c>GenerateRandomSeries</c> request shorter than its training window and returns the
    /// leading values of the zero-forecast-step prediction, instead of throwing. Before the
    /// forecast-step clamp, the negative forecast-step count reached the conditional-level
    /// reconstruction with a reconstructed length shorter than the training window and threw.
    /// </summary>
    [TestMethod]
    public void Arima_GenerateRandomSeriesShorterThanTrainingWindow_MatchesZeroForecastPredict()
    {
        ARIMA model = CreateDifferencedArima();
        model.SetParameterValues(s_parameters);
        const int seed = 4242;
        int timeSteps = model.TrainingTimeSteps - 5;

        double[] expected = model.Predict(s_parameters, 0, seed).Y;
        NumericsTimeSeries generated = model.GenerateRandomSeries(timeSteps, seed);

        AssertLeadingValuesMatch(expected, generated, timeSteps);
    }

    /// <summary>
    /// Verifies that an ARIMAX(1,1,0) model with no covariates -- a differenced model, d &gt; 0 --
    /// accepts a <c>GenerateRandomSeries</c> request shorter than its training window and returns
    /// the leading values of the zero-forecast-step prediction, instead of throwing (the same
    /// differenced-model regression as the ARIMA case).
    /// </summary>
    [TestMethod]
    public void Arimax_GenerateRandomSeriesShorterThanTrainingWindow_MatchesZeroForecastPredict()
    {
        ARIMAX model = CreateDifferencedArimax();
        model.SetParameterValues(s_parameters);
        const int seed = 4242;
        int timeSteps = model.TrainingTimeSteps - 5;

        double[] expected = model.Predict(s_parameters, 0, seed).Y;
        NumericsTimeSeries generated = model.GenerateRandomSeries(timeSteps, seed);

        AssertLeadingValuesMatch(expected, generated, timeSteps);
    }

    /// <summary>
    /// Regression guard: an AutoRegressive(1) model -- an undifferenced model, d = 0, which never
    /// reached the throwing reconstruction path -- keeps returning the same values for a
    /// <c>GenerateRandomSeries</c> request shorter than its training window after the
    /// forecast-step clamp as it did before it. Clamping <c>forecastSteps</c> to zero does not
    /// change any value the caller can observe for this request: the shared recursion is
    /// forward-only, so step <c>t</c> in the clamped (full-training-length) call is identical to
    /// step <c>t</c> in the unclamped (shorter, would-be-negative-forecast) call for every
    /// <c>t &lt; timeSteps</c>.
    /// </summary>
    [TestMethod]
    public void AutoRegressive_GenerateRandomSeriesShorterThanTrainingWindow_MatchesZeroForecastPredict()
    {
        AutoRegressive model = CreateAutoRegressive();
        model.SetParameterValues(s_parameters);
        const int seed = 4242;
        int timeSteps = model.TrainingTimeSteps - 5;

        double[] expected = model.Predict(s_parameters, 0, seed).Y;
        NumericsTimeSeries generated = model.GenerateRandomSeries(timeSteps, seed);

        AssertLeadingValuesMatch(expected, generated, timeSteps);
    }

    /// <summary>
    /// Regression guard: a MovingAverage(1) model -- an undifferenced model, d = 0 -- keeps
    /// returning the same values for a <c>GenerateRandomSeries</c> request shorter than its
    /// training window after the forecast-step clamp as it did before it.
    /// </summary>
    [TestMethod]
    public void MovingAverage_GenerateRandomSeriesShorterThanTrainingWindow_MatchesZeroForecastPredict()
    {
        MovingAverage model = CreateMovingAverage();
        model.SetParameterValues(s_parameters);
        const int seed = 4242;
        int timeSteps = model.TrainingTimeSteps - 5;

        double[] expected = model.Predict(s_parameters, 0, seed).Y;
        NumericsTimeSeries generated = model.GenerateRandomSeries(timeSteps, seed);

        AssertLeadingValuesMatch(expected, generated, timeSteps);
    }

    /// <summary>
    /// Verifies both public <c>Predict</c> overloads of <see cref="AutoRegressive"/> reject a
    /// negative <c>forecastSteps</c> before doing any other work.
    /// </summary>
    [TestMethod]
    public void AutoRegressive_Predict_NegativeForecastSteps_Throws()
    {
        AutoRegressive model = CreateAutoRegressive();

        var arrayOverload = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.Predict(s_parameters, -1, -1));
        Assert.AreEqual("forecastSteps", arrayOverload.ParamName, "double[] overload");

        var scalarOverload = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.Predict(-1));
        Assert.AreEqual("forecastSteps", scalarOverload.ParamName, "int overload");
    }

    /// <summary>
    /// Verifies both public <c>Predict</c> overloads of <see cref="MovingAverage"/> reject a
    /// negative <c>forecastSteps</c> before doing any other work.
    /// </summary>
    [TestMethod]
    public void MovingAverage_Predict_NegativeForecastSteps_Throws()
    {
        MovingAverage model = CreateMovingAverage();

        var arrayOverload = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.Predict(s_parameters, -1, -1));
        Assert.AreEqual("forecastSteps", arrayOverload.ParamName, "double[] overload");

        var scalarOverload = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.Predict(-1));
        Assert.AreEqual("forecastSteps", scalarOverload.ParamName, "int overload");
    }

    /// <summary>
    /// Verifies both public <c>Predict</c> overloads of <see cref="ARIMA"/> reject a negative
    /// <c>forecastSteps</c> before doing any other work.
    /// </summary>
    [TestMethod]
    public void Arima_Predict_NegativeForecastSteps_Throws()
    {
        ARIMA model = CreateDifferencedArima();

        var arrayOverload = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.Predict(s_parameters, -1, -1));
        Assert.AreEqual("forecastSteps", arrayOverload.ParamName, "double[] overload");

        var scalarOverload = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.Predict(-1));
        Assert.AreEqual("forecastSteps", scalarOverload.ParamName, "int overload");
    }

    /// <summary>
    /// Verifies the sole public <c>Predict</c> overload of <see cref="ARIMAX"/> rejects a negative
    /// <c>forecastSteps</c> before doing any other work.
    /// </summary>
    [TestMethod]
    public void Arimax_Predict_NegativeForecastSteps_Throws()
    {
        ARIMAX model = CreateDifferencedArimax();

        var arrayOverload = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.Predict(s_parameters, -1, -1));
        Assert.AreEqual("forecastSteps", arrayOverload.ParamName, "double[] overload");
    }

    /// <summary>
    /// Verifies <c>GenerateRandomSeries</c> of <see cref="AutoRegressive"/> rejects a non-positive
    /// <c>timeSteps</c> before doing any other work, for both a negative value and zero.
    /// </summary>
    [TestMethod]
    public void AutoRegressive_GenerateRandomSeries_NonPositiveTimeSteps_Throws()
    {
        AutoRegressive model = CreateAutoRegressive();

        var negative = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.GenerateRandomSeries(-1));
        Assert.AreEqual("timeSteps", negative.ParamName, "negative timeSteps");

        var zero = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.GenerateRandomSeries(0));
        Assert.AreEqual("timeSteps", zero.ParamName, "zero timeSteps");
    }

    /// <summary>
    /// Verifies <c>GenerateRandomSeries</c> of <see cref="MovingAverage"/> rejects a non-positive
    /// <c>timeSteps</c> before doing any other work, for both a negative value and zero.
    /// </summary>
    [TestMethod]
    public void MovingAverage_GenerateRandomSeries_NonPositiveTimeSteps_Throws()
    {
        MovingAverage model = CreateMovingAverage();

        var negative = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.GenerateRandomSeries(-1));
        Assert.AreEqual("timeSteps", negative.ParamName, "negative timeSteps");

        var zero = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.GenerateRandomSeries(0));
        Assert.AreEqual("timeSteps", zero.ParamName, "zero timeSteps");
    }

    /// <summary>
    /// Verifies <c>GenerateRandomSeries</c> of a differenced (d = 1) <see cref="ARIMA"/> rejects a
    /// non-positive <c>timeSteps</c> before doing any other work, for both a negative value and
    /// zero.
    /// </summary>
    [TestMethod]
    public void Arima_GenerateRandomSeries_NonPositiveTimeSteps_Throws()
    {
        ARIMA model = CreateDifferencedArima();

        var negative = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.GenerateRandomSeries(-1));
        Assert.AreEqual("timeSteps", negative.ParamName, "negative timeSteps");

        var zero = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.GenerateRandomSeries(0));
        Assert.AreEqual("timeSteps", zero.ParamName, "zero timeSteps");
    }

    /// <summary>
    /// Verifies <c>GenerateRandomSeries</c> of a differenced (d = 1) <see cref="ARIMAX"/> rejects a
    /// non-positive <c>timeSteps</c> before doing any other work, for both a negative value and
    /// zero.
    /// </summary>
    [TestMethod]
    public void Arimax_GenerateRandomSeries_NonPositiveTimeSteps_Throws()
    {
        ARIMAX model = CreateDifferencedArimax();

        var negative = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.GenerateRandomSeries(-1));
        Assert.AreEqual("timeSteps", negative.ParamName, "negative timeSteps");

        var zero = Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.GenerateRandomSeries(0));
        Assert.AreEqual("timeSteps", zero.ParamName, "zero timeSteps");
    }

    /// <summary>
    /// Asserts a generated series has the requested length and that every value is bit-identical
    /// to the corresponding leading value of a reference prediction.
    /// </summary>
    /// <param name="expected">The reference prediction values.</param>
    /// <param name="generated">The series returned by <c>GenerateRandomSeries</c>.</param>
    /// <param name="timeSteps">The requested series length.</param>
    private static void AssertLeadingValuesMatch(double[] expected, NumericsTimeSeries generated, int timeSteps)
    {
        Assert.AreEqual(timeSteps, generated.Count, "generated length");
        for (int i = 0; i < timeSteps; i++)
            Assert.AreEqual(expected[i], generated[i].Value, $"step {i}");
    }

    /// <summary>
    /// Creates an ARIMA(1,1,0) model with an intercept, a full-length training window, and
    /// <c>UseDefaultTrainingSteps</c> disabled so the training window is exactly
    /// the fixture length.
    /// </summary>
    /// <returns>The configured model, with default (unset) parameter values.</returns>
    private static ARIMA CreateDifferencedArima()
    {
        var model = new ARIMA(CreateSeries(s_raw), pOrder: 1, dOrder: 1, qOrder: 0, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
        };
        model.TrainingTimeSteps = s_raw.Length;
        return model;
    }

    /// <summary>
    /// Creates an ARIMAX(1,1,0) model with no covariates, an intercept, and a full-length training
    /// window.
    /// </summary>
    /// <returns>The configured model, with default (unset) parameter values.</returns>
    /// <remarks>
    /// Setting <c>TimeSeries</c> after <c>UseDefaultTrainingSteps = false</c> turns
    /// <c>UseDefaultTrainingSteps</c> back on and recomputes a default <c>TrainingTimeSteps</c>
    /// (the model's private <c>ResetDefaultTrainingStepsForNewTimeSeries</c>), so the flag does not
    /// end up disabled here. The explicit <c>TrainingTimeSteps</c> assignment that follows
    /// overwrites that recomputed default directly regardless of the flag's value, so the training
    /// window still ends up exactly the fixture length.
    /// </remarks>
    private static ARIMAX CreateDifferencedArimax()
    {
        var model = new ARIMAX
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
        };
        model.TimeSeries = CreateSeries(s_raw);
        model.TrainingTimeSteps = s_raw.Length;
        return model;
    }

    /// <summary>
    /// Creates an AutoRegressive(1) model with an intercept, a full-length training window, and
    /// <c>UseDefaultTrainingSteps</c> disabled so the training window is exactly
    /// the fixture length.
    /// </summary>
    /// <returns>The configured model, with default (unset) parameter values.</returns>
    private static AutoRegressive CreateAutoRegressive()
    {
        var model = new AutoRegressive(CreateSeries(s_raw), order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
        };
        model.TrainingTimeSteps = s_raw.Length;
        return model;
    }

    /// <summary>
    /// Creates a MovingAverage(1) model with an intercept, a full-length training window, and
    /// <c>UseDefaultTrainingSteps</c> disabled so the training window is exactly
    /// the fixture length.
    /// </summary>
    /// <returns>The configured model, with default (unset) parameter values.</returns>
    private static MovingAverage CreateMovingAverage()
    {
        var model = new MovingAverage(CreateSeries(s_raw), order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
        };
        model.TrainingTimeSteps = s_raw.Length;
        return model;
    }

    /// <summary>
    /// Creates a daily series starting at <see cref="s_startDate"/>.
    /// </summary>
    /// <param name="values">The ordinate values.</param>
    /// <returns>The series.</returns>
    private static NumericsTimeSeries CreateSeries(double[] values) =>
        new(TimeInterval.OneDay, s_startDate, values);
}
