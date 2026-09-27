using System.ComponentModel;
using System.Xml.Linq;
using Numerics.Data;
using RMC.BestFit.Models;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Verifies the default training window of the AR, MA, ARIMA, and ARIMAX models:
/// <c>max(d + K + k + 10, floor(0.8 * N))</c>.
/// </summary>
/// <remarks>
/// <para>
/// The conditional likelihood scores <c>n = T - d - K</c> steps of a <c>T</c>-step training window,
/// where <c>d</c> is the differencing order and <c>K</c> the conditioning order (AR p; MA 0; ARIMA
/// max(p, q); ARIMAX max(p, q) without covariates and max(q, p + b) with them). With <c>k</c>
/// estimated parameters the residual degrees of freedom are <c>n - k</c>, and the default window
/// leaves at least ten: <c>T &gt;= d + K + k + 10</c>. Longer series keep the 80% training split.
/// The rule was approved by Haden Smith on 26 September 2026.
/// </para>
/// <para>
/// The window is not capped at the series length, so a series too short for the model fails
/// validation with a message that names the minimum window. Manual windows keep their own checks,
/// and a saved window is restored as saved. The fixtures are small deterministic series; no
/// optimizer or sampler runs.
/// </para>
/// </remarks>
[TestClass]
public class DefaultTrainingWindowTests
{
    #region Inline test fixtures

    /// <summary>
    /// Creates an annual series of positive values with visible variation.
    /// </summary>
    /// <param name="count">The number of observations.</param>
    /// <returns>The response series, starting in 1980.</returns>
    private static NumericsTimeSeries CreateSeries(int count)
    {
        var series = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1980, 1, 1), new DateTime(1980 + count - 1, 1, 1));
        for (int i = 0; i < series.Count; i++)
            series[i].Value = 100.0 + 10.0 * Math.Sin(i / 2.0) + i;
        return series;
    }

    /// <summary>
    /// Creates a covariate series on the same dates as <paramref name="response"/>.
    /// </summary>
    /// <param name="response">The response series whose dates the covariate matches.</param>
    /// <returns>A deterministic covariate series with one value per response date.</returns>
    private static NumericsTimeSeries CreateCovariate(NumericsTimeSeries response)
    {
        var covariate = new NumericsTimeSeries(response.TimeInterval, response.First().Index, response.Last().Index);
        for (int i = 0; i < covariate.Count; i++)
            covariate[i].Value = 50.0 + 10.0 * Math.Cos(i / 4.0);
        return covariate;
    }

    /// <summary>
    /// Creates an ARIMAX model on <paramref name="series"/> with the given structure.
    /// </summary>
    /// <param name="series">The response series.</param>
    /// <param name="p">The autoregressive order.</param>
    /// <param name="d">The differencing order.</param>
    /// <param name="q">The moving-average order.</param>
    /// <param name="includeIntercept">Whether the model has an intercept.</param>
    /// <returns>The configured model, without covariates.</returns>
    private static ARIMAX CreateArimax(NumericsTimeSeries series, int p = 1, int d = 0, int q = 0, bool includeIntercept = true)
    {
        return new ARIMAX(series)
        {
            AROrderP = p,
            DiffOrderD = d,
            MAOrderQ = q,
            IncludeIntercept = includeIntercept
        };
    }

    /// <summary>
    /// Records the names of the property changes <paramref name="model"/> raises from now on.
    /// </summary>
    /// <param name="model">The model to observe.</param>
    /// <returns>The live list of raised property names, in order.</returns>
    private static List<string> RecordPropertyChanges(INotifyPropertyChanged model)
    {
        var raised = new List<string>();
        model.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);
        return raised;
    }

    /// <summary>
    /// Reads the training window and the default-rule flag of a time-series model.
    /// </summary>
    /// <param name="model">An AR, MA, ARIMA, or ARIMAX model.</param>
    /// <returns>The model's training window and whether the default rule sets it.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="model"/> is not one of the four time-series models.</exception>
    private static (int TrainingTimeSteps, bool UseDefaultTrainingSteps) GetWindow(ModelBase model) => model switch
    {
        AutoRegressive ar => (ar.TrainingTimeSteps, ar.UseDefaultTrainingSteps),
        MovingAverage ma => (ma.TrainingTimeSteps, ma.UseDefaultTrainingSteps),
        ARIMA arima => (arima.TrainingTimeSteps, arima.UseDefaultTrainingSteps),
        ARIMAX arimax => (arimax.TrainingTimeSteps, arimax.UseDefaultTrainingSteps),
        _ => throw new ArgumentException("Not a time-series model.", nameof(model))
    };

    /// <summary>
    /// Joins validation messages for an assertion message.
    /// </summary>
    /// <param name="messages">The validation messages.</param>
    /// <returns>The messages on separate lines.</returns>
    private static string Describe(IEnumerable<string> messages) => string.Join(Environment.NewLine, messages);

    #endregion

    #region Rule helper

    /// <summary>
    /// Verifies the floor d + K + k + 10 and the choice between the floor and the 80% split.
    /// </summary>
    [TestMethod]
    public void RuleHelper_FloorAndSplit()
    {
        Assert.AreEqual(1 + 2 + 5 + 10, DefaultTrainingWindow.MinimumSteps(1, 2, 5));
        Assert.AreEqual(18, DefaultTrainingWindow.Steps(20, 1, 2, 5), "The floor wins over floor(0.8 * 20) = 16.");
        Assert.AreEqual(80, DefaultTrainingWindow.Steps(100, 1, 2, 5), "The 80% split wins over the floor 18.");
        Assert.AreEqual(19, DefaultTrainingWindow.Steps(12, 2, 3, 4), "The window is not capped at the series length.");
    }

    /// <summary>
    /// Verifies the three validation messages for a training window longer than the series.
    /// </summary>
    [TestMethod]
    public void RuleHelper_ExceedsSeriesMessages()
    {
        const string lengthError = "Error: Training time steps cannot exceed time series length.";

        Assert.AreEqual(lengthError, DefaultTrainingWindow.ExceedsSeriesMessage(false, 12, 0, 1, 3), "Manual window.");

        string stale = DefaultTrainingWindow.ExceedsSeriesMessage(true, 20, 0, 1, 3);
        StringAssert.StartsWith(stale, lengthError, "A default window saved under an earlier rule.");
        StringAssert.Contains(stale, "Turn Use Default Training Steps off and on");

        string tooShort = DefaultTrainingWindow.ExceedsSeriesMessage(true, 12, 1, 2, 4);
        StringAssert.Contains(tooShort, "The time series has 12 observations");
        StringAssert.Contains(tooShort, "needs at least 17 training steps");
        StringAssert.Contains(tooShort, "10 more fitted steps than its 4 parameters");
        StringAssert.Contains(tooShort, "conditioning order (2)");
        StringAssert.Contains(tooShort, "differencing order (1)");
    }

    #endregion

    #region Default rule

    /// <summary>
    /// Verifies that a 20-step series is valid for AR(1) under the default training settings.
    /// </summary>
    /// <remarks>
    /// AR(1) with an intercept has k = 3 and K = 1, so the floor is 14 and the 80% split, 16, wins.
    /// The former rule's 30-step minimum made every series shorter than 30 steps invalid.
    /// </remarks>
    [TestMethod]
    public void ShortSeries_DefaultWindowIsValid()
    {
        var model = new AutoRegressive(CreateSeries(20), 1, true);

        Assert.AreEqual(16, model.TrainingTimeSteps);
        var (isValid, messages) = model.Validate();
        Assert.IsTrue(isValid, Describe(messages));
    }

    /// <summary>
    /// Verifies that the 80% split is kept when it exceeds the degrees-of-freedom floor.
    /// </summary>
    [TestMethod]
    public void DefaultWindow_KeepsTheEightyPercentSplitAboveTheFloor()
    {
        Assert.AreEqual(80, new AutoRegressive(CreateSeries(100), 1, true).TrainingTimeSteps);
        Assert.AreEqual(28, new AutoRegressive(CreateSeries(35), 1, true).TrainingTimeSteps,
            "The former 30-step minimum no longer applies.");
    }

    /// <summary>
    /// Verifies that the default window is <c>d + K + k + 10</c> when the 80% split is shorter,
    /// with <c>k</c> the model's actual parameter count, for every model kind and structural term.
    /// </summary>
    /// <remarks>
    /// On 20 steps the 80% split is 16, below every floor in the list (17 to 20). Comparing with
    /// <see cref="ModelBase.NumberOfParameters"/> ties the rule's parameter count to the layout the
    /// models actually build: intercept, trend, seasonality, covariate lags, AR and MA
    /// coefficients, and the scale.
    /// </remarks>
    [TestMethod]
    public void DefaultWindow_IsTheDegreesOfFreedomFloorWhenTheSplitIsShorter()
    {
        var cases = new List<(string Name, Func<ModelBase> Create, int D, int K)>
        {
            ("AR(3)", () => new AutoRegressive(CreateSeries(20), 3, true), 0, 3),
            ("AR(4) without intercept", () => new AutoRegressive(CreateSeries(20), 4, false), 0, 4),
            ("MA(5)", () => new MovingAverage(CreateSeries(20), 5, true), 0, 0),
            ("MA(6) without intercept", () => new MovingAverage(CreateSeries(20), 6, false), 0, 0),
            ("ARIMA(1,1,2)", () => new ARIMA(CreateSeries(20), 1, 1, 2, true), 1, 2),
            ("ARIMA(2,2,1) without intercept", () => new ARIMA(CreateSeries(20), 2, 2, 1, false), 2, 2),
            ("ARIMAX(2,1,1)", () => CreateArimax(CreateSeries(20), p: 2, d: 1, q: 1), 1, 2),
            ("ARIMAX(1,0,0) linear trend and seasonality", () =>
            {
                var model = CreateArimax(CreateSeries(20));
                model.TrendType = ARIMAX.Trend.Linear;
                model.IncludeSeasonality = true;
                return model;
            }, 0, 1),
            ("ARIMAX(1,0,0) cubic trend", () =>
            {
                var model = CreateArimax(CreateSeries(20));
                model.TrendType = ARIMAX.Trend.Cubic;
                return model;
            }, 0, 1),
            ("ARIMAX(1,0,1) with one covariate, b = 2", () =>
            {
                var series = CreateSeries(20);
                var model = CreateArimax(series, p: 1, q: 1);
                model.XOrderB = 2;
                model.SetCovariates(new List<NumericsTimeSeries> { CreateCovariate(series) });
                return model;
            }, 0, 3),
        };

        foreach (var (name, create, d, k) in cases)
        {
            ModelBase model = create();
            Assert.AreEqual(d + k + model.NumberOfParameters + 10, GetWindow(model).TrainingTimeSteps, name);
        }
    }

    #endregion

    #region Structural changes

    /// <summary>
    /// Verifies that AR, MA, and ARIMA structural setters recompute the default window and raise
    /// <c>TrainingTimeSteps</c>.
    /// </summary>
    [TestMethod]
    public void StructuralChange_RecomputesTheDefaultWindow_ArMaArima()
    {
        var ar = new AutoRegressive(CreateSeries(20), 1, true);
        var arRaised = RecordPropertyChanges(ar);
        ar.Order = 3;
        Assert.AreEqual(18, ar.TrainingTimeSteps, "AR(3): 3 + 5 + 10.");
        CollectionAssert.Contains(arRaised, nameof(AutoRegressive.TrainingTimeSteps));

        var ma = new MovingAverage(CreateSeries(20), 6, false);
        Assert.AreEqual(17, ma.TrainingTimeSteps, "MA(6) without intercept: 0 + 7 + 10.");
        var maRaised = RecordPropertyChanges(ma);
        ma.IncludeIntercept = true;
        Assert.AreEqual(18, ma.TrainingTimeSteps, "MA(6) with intercept: 0 + 8 + 10.");
        CollectionAssert.Contains(maRaised, nameof(MovingAverage.TrainingTimeSteps));

        var arima = new ARIMA(CreateSeries(20), 2, 0, 0, true);
        Assert.AreEqual(16, arima.TrainingTimeSteps, "ARIMA(2,0,0): floor 16 equals the 80% split.");
        var arimaRaised = RecordPropertyChanges(arima);
        arima.DOrder = 2;
        Assert.AreEqual(18, arima.TrainingTimeSteps, "ARIMA(2,2,0): 2 + 2 + 4 + 10.");
        CollectionAssert.Contains(arimaRaised, nameof(ARIMA.TrainingTimeSteps));
    }

    /// <summary>
    /// Verifies that every ARIMAX structural change recomputes the default window, and raises
    /// <c>TrainingTimeSteps</c> after its own notification so that an undo step recorded on the
    /// first notification holds the new window.
    /// </summary>
    [TestMethod]
    public void StructuralChange_RecomputesTheDefaultWindow_Arimax()
    {
        var cases = new List<(string Name, Func<ARIMAX> Create, Action<ARIMAX> Edit, string Property, int Expected)>
        {
            ("p 1 -> 3", () => CreateArimax(CreateSeries(20)), m => m.AROrderP = 3, nameof(ARIMAX.AROrderP), 18),
            ("q 0 -> 3", () => CreateArimax(CreateSeries(20)), m => m.MAOrderQ = 3, nameof(ARIMAX.MAOrderQ), 19),
            ("d 0 -> 2", () => CreateArimax(CreateSeries(20), p: 2), m => m.DiffOrderD = 2, nameof(ARIMAX.DiffOrderD), 18),
            ("intercept off", () => CreateArimax(CreateSeries(20), p: 3), m => m.IncludeIntercept = false, nameof(ARIMAX.IncludeIntercept), 17),
            ("seasonality on", () => CreateArimax(CreateSeries(20), p: 2), m => m.IncludeSeasonality = true, nameof(ARIMAX.IncludeSeasonality), 18),
            ("cubic trend", () => CreateArimax(CreateSeries(20)), m => m.TrendType = ARIMAX.Trend.Cubic, nameof(ARIMAX.TrendType), 17),
            ("b 0 -> 2 with a covariate", () =>
            {
                var series = CreateSeries(20);
                var model = CreateArimax(series);
                model.SetCovariates(new List<NumericsTimeSeries> { CreateCovariate(series) });
                return model;
            }, m => m.XOrderB = 2, nameof(ARIMAX.XOrderB), 19),
        };

        foreach (var (name, create, edit, property, expected) in cases)
        {
            ARIMAX model = create();
            var raised = RecordPropertyChanges(model);
            edit(model);

            Assert.AreEqual(expected, model.TrainingTimeSteps, name);
            int structural = raised.IndexOf(property);
            int window = raised.LastIndexOf(nameof(ARIMAX.TrainingTimeSteps));
            Assert.IsTrue(structural >= 0 && window > structural,
                $"{name}: TrainingTimeSteps must be raised after {property}; raised: {string.Join(", ", raised)}");
        }
    }

    /// <summary>
    /// Verifies the contract for a window assigned while the default rule is on: assigning
    /// <c>TrainingTimeSteps</c> leaves the rule on, so the next structural change recomputes the
    /// window, while a window assigned after the rule is turned off is kept.
    /// </summary>
    /// <remarks>
    /// Haden Smith kept this setter contract on 26 September 2026: library callers that want a
    /// fixed window turn <c>UseDefaultTrainingSteps</c> off after attaching the series (attaching a
    /// series turns it back on), as the desktop application and the REST API do.
    /// </remarks>
    [TestMethod]
    public void WindowAssignedWhileTheDefaultIsOn_IsRecomputedByTheNextStructuralChange()
    {
        var model = new ARIMA(CreateSeries(20), 1, 0, 0, true);
        model.TrainingTimeSteps = 12;
        Assert.IsTrue(model.UseDefaultTrainingSteps);
        model.POrder = 2;
        Assert.AreEqual(16, model.TrainingTimeSteps, "ARIMA(2,0,0): max(0 + 2 + 4 + 10, 16).");

        model.UseDefaultTrainingSteps = false;
        model.TrainingTimeSteps = 12;
        model.POrder = 3;
        Assert.AreEqual(12, model.TrainingTimeSteps, "A manual window is kept.");
    }

    /// <summary>
    /// Verifies that attaching covariates as a user change recomputes the default window, while
    /// reattaching them to restore a saved, copied, or undone model keeps the restored window.
    /// </summary>
    /// <remarks>
    /// With b = 2 one covariate adds three coefficients and raises K from max(p, q) = 1 to
    /// max(q, p + b) = 3, so the floor rises from 13 to 19 on a 20-step series.
    /// </remarks>
    [TestMethod]
    public void SetCovariates_RecomputesForAUserChangeAndKeepsARestoredWindow()
    {
        var series = CreateSeries(20);
        var model = CreateArimax(series);
        model.XOrderB = 2;
        Assert.AreEqual(16, model.TrainingTimeSteps, "Without covariates b has no role: floor 14, split 16.");

        model.SetCovariates(new List<NumericsTimeSeries> { CreateCovariate(series) });
        Assert.AreEqual(19, model.TrainingTimeSteps, "k = 6 and K = 3 give 3 + 6 + 10.");

        XElement saved = model.ToXElement();
        saved.SetAttributeValue(nameof(ARIMAX.TrainingTimeSteps), "17");
        var restored = new ARIMAX(series, saved);
        restored.SetCovariates(new List<NumericsTimeSeries> { CreateCovariate(series) }, resetParameters: false);
        Assert.AreEqual(17, restored.TrainingTimeSteps, "Restoring covariates keeps the saved window.");
        Assert.IsTrue(restored.UseDefaultTrainingSteps);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Verifies that a series too short for the model keeps the full default window and fails
    /// validation with a message that names the minimum window.
    /// </summary>
    [TestMethod]
    public void SeriesTooShortForTheModel_ValidationNamesTheMinimumWindow()
    {
        var cases = new List<(string Name, ModelBase Model, int Expected)>
        {
            ("AR(1)", new AutoRegressive(CreateSeries(12), 1, true), 14),
            ("MA(1)", new MovingAverage(CreateSeries(12), 1, true), 13),
            ("ARIMA(1,0,0)", new ARIMA(CreateSeries(12), 1, 0, 0, true), 14),
            ("ARIMAX(1,0,0)", CreateArimax(CreateSeries(12)), 14),
        };

        foreach (var (name, model, expected) in cases)
        {
            var (isValid, messages) = model.Validate();
            Assert.IsFalse(isValid, name);
            Assert.IsTrue(messages.Any(m => m.Contains($"needs at least {expected} training steps")),
                $"{name}: {Describe(messages)}");
        }
    }

    /// <summary>
    /// Verifies that a manual window below the default floor keeps today's validation.
    /// </summary>
    [TestMethod]
    public void ManualWindowBelowTheFloor_KeepsTheExistingChecks()
    {
        var model = new AutoRegressive(CreateSeries(12), 1, true)
        {
            UseDefaultTrainingSteps = false,
            TrainingTimeSteps = 12
        };

        var (isValid, messages) = model.Validate();
        Assert.IsTrue(isValid, Describe(messages));
    }

    #endregion

    #region Restore

    /// <summary>
    /// Verifies that opening a model restores its saved default window even when the current rule
    /// would choose another one, so saved results stay paired with the window they were fitted on.
    /// </summary>
    /// <remarks>
    /// Thirty steps was the former default for a 35-step series; the current rule gives 28.
    /// </remarks>
    [TestMethod]
    public void Open_RestoresTheSavedDefaultWindow()
    {
        var series = CreateSeries(35);
        var models = new List<(string Name, XElement Saved, Func<XElement, ModelBase> Open)>
        {
            ("AR", new AutoRegressive(series, 1, true).ToXElement(), x => new AutoRegressive(series, x)),
            ("MA", new MovingAverage(series, 1, true).ToXElement(), x => new MovingAverage(series, x)),
            ("ARIMA", new ARIMA(series, 1, 0, 0, true).ToXElement(), x => new ARIMA(series, x)),
            ("ARIMAX", CreateArimax(series).ToXElement(), x => new ARIMAX(series, x)),
        };

        foreach (var (name, saved, open) in models)
        {
            saved.SetAttributeValue("TrainingTimeSteps", "30");
            var (trainingTimeSteps, useDefaultTrainingSteps) = GetWindow(open(saved));
            Assert.AreEqual(30, trainingTimeSteps, name);
            Assert.IsTrue(useDefaultTrainingSteps, name);
        }
    }

    /// <summary>
    /// Verifies that cloning an ARIMAX model with covariates keeps the source's training window.
    /// </summary>
    /// <remarks>
    /// The clone must fit the same window as its source: the sampler and diagnostics evaluate
    /// clones in place of the source model.
    /// </remarks>
    [TestMethod]
    public void ArimaxClone_KeepsTheSourceWindow()
    {
        var series = CreateSeries(35);
        var source = CreateArimax(series);
        source.SetCovariates(new List<NumericsTimeSeries> { CreateCovariate(series) });
        XElement saved = source.ToXElement();
        saved.SetAttributeValue(nameof(ARIMAX.TrainingTimeSteps), "30");
        var restored = new ARIMAX(series, saved);
        restored.SetCovariates(new List<NumericsTimeSeries> { CreateCovariate(series) }, resetParameters: false);

        var clone = (ARIMAX)restored.Clone();

        Assert.AreEqual(30, clone.TrainingTimeSteps);
        Assert.IsTrue(clone.UseDefaultTrainingSteps);
    }

    /// <summary>
    /// Verifies the recovery path for a default window saved by an earlier version that exceeds
    /// the series: validation reports the saved window, and turning the default rule off and on
    /// recomputes a valid window.
    /// </summary>
    /// <remarks>
    /// RMC-BestFit 2.0.0 saved a 30-step default window for any shorter series. The current floor
    /// for AR(1) is 14, so the 20-step series itself is long enough.
    /// </remarks>
    [TestMethod]
    public void SavedDefaultWindowLongerThanTheSeries_RecomputesWhenTheRuleIsReapplied()
    {
        var series = CreateSeries(20);
        XElement saved = new AutoRegressive(series, 1, true).ToXElement();
        saved.SetAttributeValue(nameof(AutoRegressive.TrainingTimeSteps), "30");
        var model = new AutoRegressive(series, saved);

        var (isValid, messages) = model.Validate();
        Assert.IsFalse(isValid);
        Assert.IsFalse(messages.Any(m => m.Contains("needs at least")), Describe(messages));

        model.UseDefaultTrainingSteps = false;
        model.UseDefaultTrainingSteps = true;
        Assert.AreEqual(16, model.TrainingTimeSteps);
        (isValid, messages) = model.Validate();
        Assert.IsTrue(isValid, Describe(messages));
    }

    #endregion
}
