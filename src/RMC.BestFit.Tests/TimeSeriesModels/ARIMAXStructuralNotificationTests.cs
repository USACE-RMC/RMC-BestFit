using System.ComponentModel;
using System.Xml.Linq;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitTransform = RMC.BestFit.Models.Transform;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Verifies that the <see cref="ARIMAX"/> structural setters rebuild the training data and the
/// default parameters before they raise their property-change notifications.
/// </summary>
/// <remarks>
/// <para>
/// The time-series analysis records an undo snapshot of the model when a structural notification
/// arrives. A setter that notified before it rebuilt exposed the new structure with the previous
/// parameter vector; that state became the recorded redo target and the baseline of the next
/// recorded edit, so undo and redo restored a structure with the previous structure's default
/// values, bounds, and priors.
/// </para>
/// <para>
/// Every notification an edit raises, including a nested <see cref="ARIMAX.TransformLambda"/>
/// change from refitting the transform exponent, must therefore see the parameters that a fresh
/// <see cref="ARIMAX.SetDefaultParameters"/> builds for the new structure. The fixtures are small
/// deterministic series; no optimizer or sampler runs.
/// </para>
/// </remarks>
[TestClass]
public class ARIMAXStructuralNotificationTests
{
    #region Inline test fixtures

    /// <summary>
    /// Creates a 40-step annual series whose level is near 1000 with visible variation.
    /// </summary>
    /// <returns>The response series, 1980 through 2019.</returns>
    /// <remarks>
    /// The level and first-differenced training series have very different means, so the default
    /// intercept bounds change with the differencing order, and the training mean and standard
    /// deviation change with the training window.
    /// </remarks>
    private static NumericsTimeSeries CreateLevelSeries()
    {
        var series = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1980, 1, 1), new DateTime(2019, 1, 1));
        for (int i = 0; i < series.Count; i++)
            series[i].Value = 1000.0 + 25.0 * Math.Sin(i / 3.0) + i;
        return series;
    }

    /// <summary>
    /// Creates a 40-step annual positive series with multiplicative variation.
    /// </summary>
    /// <returns>The response series, 1980 through 2019.</returns>
    /// <remarks>
    /// The values span roughly 30 to 250, so the Box-Cox exponent fitted from the training prefix
    /// is well defined and changes when the training window changes.
    /// </remarks>
    private static NumericsTimeSeries CreateSkewedSeries()
    {
        var series = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1980, 1, 1), new DateTime(2019, 1, 1));
        for (int i = 0; i < series.Count; i++)
            series[i].Value = Math.Exp(4.0 + 0.6 * Math.Sin(i / 3.0) + 0.02 * i);
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
    /// Creates an ARIMAX(1, 0, 0) model with an intercept and default flat priors on the level series.
    /// </summary>
    /// <param name="withCovariate">Whether to attach one zero-lag covariate.</param>
    /// <returns>
    /// The model: intercept, one AR coefficient, and the scale (plus one covariate coefficient when
    /// requested), with the default training window of 32 of the 40 steps.
    /// </returns>
    private static ARIMAX CreateModel(bool withCovariate)
    {
        NumericsTimeSeries series = CreateLevelSeries();
        var model = new ARIMAX(series);
        if (withCovariate)
            model.SetCovariates(new List<NumericsTimeSeries> { CreateCovariate(series) });
        return model;
    }

    /// <summary>
    /// Gives the first AR coefficient a non-default value, bounds, and a Normal(0.5, 0.1) prior.
    /// </summary>
    /// <param name="model">The model whose AR coefficient is customized.</param>
    private static void CustomizeARCoefficient(ARIMAX model)
    {
        ModelParameter phi = model.Parameters.Single(p => p.Name.StartsWith("AR", StringComparison.Ordinal));
        phi.Value = 0.42;
        phi.LowerBound = -3.0;
        phi.UpperBound = 3.0;
        phi.PriorDistribution = new Normal(0.5, 0.1);
    }

    /// <summary>
    /// Serializes the model's parameters, including each name, value, bound pair, and prior.
    /// </summary>
    /// <param name="model">The model whose parameters are captured.</param>
    /// <returns>An element holding one serialized <see cref="ModelParameter"/> per parameter.</returns>
    /// <remarks>
    /// <see cref="ModelParameter.ToXElement"/> writes values and bounds with round-trip precision
    /// and includes the prior's type and parameters, so equal elements mean identical parameters.
    /// </remarks>
    private static XElement CaptureParameters(ARIMAX model)
    {
        return new XElement(nameof(ARIMAX.Parameters), model.Parameters.Select(parameter => parameter.ToXElement()));
    }

    #endregion

    #region Assertion helpers

    /// <summary>
    /// Applies an edit and verifies that every notification it raises sees the default
    /// parameters built for the edited structure.
    /// </summary>
    /// <param name="model">The model to edit.</param>
    /// <param name="propertyName">The name the edited setter raises.</param>
    /// <param name="edit">The edit to apply.</param>
    /// <param name="expectedParameterCount">The parameter count of the edited structure.</param>
    /// <returns>The names of the notifications the edit raised, in order.</returns>
    /// <remarks>
    /// The reference parameters come from calling <see cref="ARIMAX.SetDefaultParameters"/> after
    /// the edit, which is what the setter must already have built when it notified. The edit has
    /// to change the defaults, so a setter that notified before rebuilding cannot pass.
    /// </remarks>
    private static List<string> AssertEveryNotificationSeesRebuiltDefaults(
        ARIMAX model, string propertyName, Action edit, int expectedParameterCount)
    {
        XElement before = CaptureParameters(model);
        var notifications = new List<(string Name, int ParameterCount, XElement Parameters)>();
        PropertyChangedEventHandler handler = (_, e) =>
            notifications.Add((e.PropertyName ?? string.Empty, model.NumberOfParameters, CaptureParameters(model)));

        model.PropertyChanged += handler;
        try
        {
            edit();
        }
        finally
        {
            model.PropertyChanged -= handler;
        }

        model.SetDefaultParameters();
        XElement expected = CaptureParameters(model);
        Assert.AreEqual(expectedParameterCount, model.NumberOfParameters,
            $"Precondition: the parameter layout after editing {propertyName}.");
        Assert.IsFalse(XNode.DeepEquals(before, expected),
            $"Precondition: editing {propertyName} must change the default parameters.");
        Assert.AreEqual(1, notifications.Count(n => n.Name == propertyName),
            $"{propertyName} must be raised exactly once.");

        foreach (var notification in notifications)
        {
            Assert.AreEqual(expectedParameterCount, notification.ParameterCount,
                $"{notification.Name} was raised before the parameter layout was rebuilt for the new {propertyName}.");
            Assert.IsTrue(XNode.DeepEquals(expected, notification.Parameters),
                $"{notification.Name} was raised before the default parameters were rebuilt for the new {propertyName}." +
                $"{Environment.NewLine}Expected: {expected}{Environment.NewLine}Observed: {notification.Parameters}");
        }

        return notifications.Select(n => n.Name).ToList();
    }

    /// <summary>
    /// Prepares a model from <see cref="CreateModel"/> for a structural edit and returns the edit.
    /// </summary>
    /// <param name="model">The model to prepare.</param>
    /// <param name="propertyName">The structural property to edit.</param>
    /// <returns>The edit, which changes the property away from its arranged value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="propertyName"/> is not one of the structural properties under test.
    /// </exception>
    private static Action ArrangeStructuralEdit(ARIMAX model, string propertyName)
    {
        switch (propertyName)
        {
            case nameof(ARIMAX.AROrderP):
                return () => model.AROrderP = 2;
            case nameof(ARIMAX.MAOrderQ):
                return () => model.MAOrderQ = 1;
            case nameof(ARIMAX.DiffOrderD):
                return () => model.DiffOrderD = 1;
            case nameof(ARIMAX.XOrderB):
                return () => model.XOrderB = 1;
            case nameof(ARIMAX.IncludeIntercept):
                return () => model.IncludeIntercept = false;
            case nameof(ARIMAX.IncludeSeasonality):
                return () => model.IncludeSeasonality = true;
            case nameof(ARIMAX.TrendType):
                return () => model.TrendType = ARIMAX.Trend.Linear;
            case nameof(ARIMAX.TrainingTimeSteps):
                // A manual window, as the analysis sets one: the 80% rule is turned off first.
                model.UseDefaultTrainingSteps = false;
                return () => model.TrainingTimeSteps = 40;
            case nameof(ARIMAX.UseDefaultTrainingSteps):
                // Start from a manual full-length window, so restoring the 80% rule changes it.
                model.UseDefaultTrainingSteps = false;
                model.TrainingTimeSteps = 40;
                return () => model.UseDefaultTrainingSteps = true;
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, "Not a structural ARIMAX property under test.");
        }
    }

    #endregion

    #region Structural setters

    /// <summary>
    /// Verifies that each structural setter raises its own notification, and every other
    /// notification of the same edit, only after the default parameters are rebuilt for the new
    /// structure.
    /// </summary>
    /// <param name="propertyName">The structural property to edit.</param>
    /// <param name="expectedParameterCount">The parameter count of the edited structure.</param>
    [TestMethod]
    [DataRow(nameof(ARIMAX.AROrderP), 4)]
    [DataRow(nameof(ARIMAX.MAOrderQ), 4)]
    [DataRow(nameof(ARIMAX.DiffOrderD), 3)]
    [DataRow(nameof(ARIMAX.XOrderB), 5)]
    [DataRow(nameof(ARIMAX.IncludeIntercept), 2)]
    [DataRow(nameof(ARIMAX.IncludeSeasonality), 5)]
    [DataRow(nameof(ARIMAX.TrendType), 4)]
    [DataRow(nameof(ARIMAX.TrainingTimeSteps), 3)]
    [DataRow(nameof(ARIMAX.UseDefaultTrainingSteps), 3)]
    public void StructuralSetter_NotifiesAfterRebuildingDefaults(string propertyName, int expectedParameterCount)
    {
        ARIMAX model = CreateModel(withCovariate: propertyName == nameof(ARIMAX.XOrderB));
        Action edit = ArrangeStructuralEdit(model, propertyName);

        AssertEveryNotificationSeesRebuiltDefaults(model, propertyName, edit, expectedParameterCount);
    }

    /// <summary>
    /// Verifies that a training-window change that refits the Box-Cox exponent reports the new
    /// exponent, like its own change, only after the default parameters are rebuilt.
    /// </summary>
    /// <remarks>
    /// The exponent is fitted from the training prefix, so a different window refits it from
    /// inside the setter; the analysis records an undo step on either notification.
    /// </remarks>
    [TestMethod]
    public void TrainingTimeSteps_RefittedExponent_NotifiesAfterRebuildingDefaults()
    {
        var model = new ARIMAX(CreateSkewedSeries()) { TransformType = BestFitTransform.BoxCox };
        model.UseDefaultTrainingSteps = false;
        double previousLambda = model.TransformLambda;

        List<string> names = AssertEveryNotificationSeesRebuiltDefaults(
            model, nameof(ARIMAX.TrainingTimeSteps), () => model.TrainingTimeSteps = 40, 3);

        Assert.AreNotEqual(previousLambda, model.TransformLambda, "Precondition: the longer window refits the exponent.");
        Assert.AreEqual(1, names.Count(name => name == nameof(ARIMAX.TransformLambda)),
            "The refitted exponent must be reported exactly once.");
    }

    /// <summary>
    /// Verifies that an order change that refits a restored Box-Cox exponent reports the new
    /// exponent only after the default parameters are rebuilt for the new order.
    /// </summary>
    /// <remarks>
    /// A restored model keeps its saved fitted exponent until the training data is next rebuilt,
    /// so the first structural edit refits it and reports the change from inside the setter.
    /// </remarks>
    [TestMethod]
    public void AROrderP_RefittedRestoredExponent_NotifiesAfterRebuildingDefaults()
    {
        var source = new ARIMAX(CreateSkewedSeries()) { TransformType = BestFitTransform.BoxCox };
        XElement saved = source.ToXElement();
        saved.SetAttributeValue(nameof(ARIMAX.TransformLambda), "0.5");
        var model = new ARIMAX(CreateSkewedSeries(), saved);
        Assert.AreEqual(0.5, model.TransformLambda, 0.0, "Precondition: the restored model keeps the saved exponent.");

        List<string> names = AssertEveryNotificationSeesRebuiltDefaults(
            model, nameof(ARIMAX.AROrderP), () => model.AROrderP = 2, 4);

        Assert.AreNotEqual(0.5, model.TransformLambda, "Precondition: the order change refits the exponent.");
        CollectionAssert.Contains(names, nameof(ARIMAX.TransformLambda),
            "The refitted exponent must be reported.");
    }

    #endregion

    #region Default flat priors

    /// <summary>
    /// Verifies that turning default flat priors on replaces a custom prior with the rebuilt
    /// defaults before the flag's notification is raised.
    /// </summary>
    [TestMethod]
    public void UseDefaultFlatPriors_TurningOn_NotifiesAfterRebuildingDefaults()
    {
        ARIMAX model = CreateModel(withCovariate: false);
        model.UseDefaultFlatPriors = false;
        CustomizeARCoefficient(model);

        AssertEveryNotificationSeesRebuiltDefaults(
            model, nameof(ARIMAX.UseDefaultFlatPriors), () => model.UseDefaultFlatPriors = true, 3);

        Assert.IsTrue(model.UseDefaultFlatPriors);
    }

    /// <summary>
    /// Verifies that turning default flat priors off keeps the current parameters and raises only
    /// the flag's notification, once.
    /// </summary>
    [TestMethod]
    public void UseDefaultFlatPriors_TurningOff_KeepsParametersAndNotifiesOnce()
    {
        ARIMAX model = CreateModel(withCovariate: false);
        CustomizeARCoefficient(model);
        XElement before = CaptureParameters(model);
        var names = new List<string>();
        PropertyChangedEventHandler handler = (_, e) => names.Add(e.PropertyName ?? string.Empty);

        model.PropertyChanged += handler;
        try
        {
            model.UseDefaultFlatPriors = false;
        }
        finally
        {
            model.PropertyChanged -= handler;
        }

        CollectionAssert.AreEqual(new[] { nameof(ARIMAX.UseDefaultFlatPriors) }, names);
        Assert.IsTrue(XNode.DeepEquals(before, CaptureParameters(model)),
            "Turning default flat priors off must keep the current parameters.");
    }

    #endregion
}
