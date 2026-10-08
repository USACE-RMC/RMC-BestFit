using System.Xml.Linq;
using Numerics.Data;
using Numerics.Data.Statistics;
using RMC.BestFit.Models;
using BestFitTransform = RMC.BestFit.Models.Transform;

namespace RMC.BestFit.UI.Tests.Elements.TimeSeriesAnalysis;

/// <summary>
/// Protects the legacy time-series model XML consumed by UI project persistence.
/// </summary>
[TestClass]
public class TimeSeriesModelSerializationCompatibilityTests
{
    /// <summary>
    /// Verifies that the legacy AR XML shape restores and re-emits its established settings.
    /// </summary>
    [TestMethod]
    public void AutoRegressive_LegacyXml_PreservesSemanticContract()
    {
        XElement xml = XElement.Parse(
            "<AutoRegressive Order=\"2\" IncludeIntercept=\"False\" UseDefaultFlatPriors=\"False\" " +
            "UseJeffreysRuleForScale=\"True\" TransformType=\"Logarithmic\" TrainingTimeSteps=\"12\" " +
            "UseDefaultTrainingSteps=\"False\" FutureOptional=\"ignored\"><Parameters /></AutoRegressive>");

        var model = new AutoRegressive(CreatePositiveSeries(), xml);

        Assert.AreEqual(2, model.Order);
        Assert.IsFalse(model.IncludeIntercept);
        Assert.IsFalse(model.UseDefaultFlatPriors);
        Assert.IsTrue(model.UseJeffreysRuleForScale);
        Assert.AreEqual(BestFitTransform.Logarithmic, model.TransformType);
        Assert.AreEqual(0.0, model.TransformLambda, 0.0);
        Assert.AreEqual(12, model.TrainingTimeSteps);
        Assert.IsFalse(model.UseDefaultTrainingSteps);
        AssertEstablishedAttributes(model.ToXElement(), "AutoRegressive", "Order", "2");
    }

    /// <summary>
    /// Verifies that the legacy MA XML shape restores and re-emits its established settings.
    /// </summary>
    [TestMethod]
    public void MovingAverage_LegacyXml_PreservesSemanticContract()
    {
        XElement xml = XElement.Parse(
            "<MovingAverage Order=\"3\" IncludeIntercept=\"True\" UseDefaultFlatPriors=\"False\" " +
            "UseJeffreysRuleForScale=\"True\" TransformType=\"YeoJohnson\" TrainingTimeSteps=\"13\" " +
            "UseDefaultTrainingSteps=\"False\" FutureOptional=\"ignored\"><Parameters /></MovingAverage>");

        var model = new MovingAverage(CreatePositiveSeries(), xml);

        Assert.AreEqual(3, model.Order);
        Assert.IsTrue(model.IncludeIntercept);
        Assert.IsFalse(model.UseDefaultFlatPriors);
        Assert.IsTrue(model.UseJeffreysRuleForScale);
        Assert.AreEqual(BestFitTransform.YeoJohnson, model.TransformType);
        Assert.AreEqual(ExpectedYeoJohnsonLambda(13), model.TransformLambda, 1E-12);
        Assert.AreEqual(13, model.TrainingTimeSteps);
        Assert.IsFalse(model.UseDefaultTrainingSteps);
        AssertEstablishedAttributes(model.ToXElement(), "MovingAverage", "Order", "3");
    }

    /// <summary>
    /// Verifies that the legacy ARIMA XML shape restores and re-emits its established settings.
    /// </summary>
    [TestMethod]
    public void Arima_LegacyXml_PreservesSemanticContract()
    {
        XElement xml = XElement.Parse(
            "<ARIMA POrder=\"2\" DOrder=\"1\" QOrder=\"1\" IncludeIntercept=\"False\" " +
            "UseDefaultFlatPriors=\"False\" UseJeffreysRuleForScale=\"True\" TransformType=\"Logarithmic\" " +
            "TrainingTimeSteps=\"14\" UseDefaultTrainingSteps=\"False\" FutureOptional=\"ignored\"><Parameters /></ARIMA>");

        var model = new ARIMA(CreatePositiveSeries(), xml);

        Assert.AreEqual(2, model.POrder);
        Assert.AreEqual(1, model.DOrder);
        Assert.AreEqual(1, model.QOrder);
        Assert.IsFalse(model.IncludeIntercept);
        Assert.IsFalse(model.UseDefaultFlatPriors);
        Assert.IsTrue(model.UseJeffreysRuleForScale);
        Assert.AreEqual(BestFitTransform.Logarithmic, model.TransformType);
        Assert.AreEqual(0.0, model.TransformLambda, 0.0);
        Assert.AreEqual(14, model.TrainingTimeSteps);
        Assert.IsFalse(model.UseDefaultTrainingSteps);
        AssertEstablishedAttributes(model.ToXElement(), "ARIMA", "DOrder", "1");
    }

    /// <summary>
    /// Verifies that the legacy ARIMAX XML shape restores and re-emits its established settings.
    /// </summary>
    [TestMethod]
    public void Arimax_LegacyXml_PreservesSemanticContract()
    {
        XElement xml = XElement.Parse(
            "<ARIMAX TransformType=\"YeoJohnson\" CovariateExtension=\"BlockBootstrap\" IncludeIntercept=\"True\" " +
            "IncludeSeasonality=\"True\" TrendType=\"Linear\" AROrderP=\"2\" DiffOrderD=\"1\" MAOrderQ=\"1\" " +
            "XOrderB=\"0\" TrainingTimeSteps=\"15\" UseDefaultTrainingSteps=\"False\" UseDefaultFlatPriors=\"False\" " +
            "UseJeffreysRuleForScale=\"True\" FutureOptional=\"ignored\"><Parameters /></ARIMAX>");

        var model = new ARIMAX(CreatePositiveSeries(), xml);

        Assert.AreEqual(BestFitTransform.YeoJohnson, model.TransformType);
        Assert.AreEqual(ExpectedYeoJohnsonLambda(15), model.TransformLambda, 1E-12);
        Assert.AreEqual(ARIMAX.CovariateExtensionMethod.BlockBootstrap, model.CovariateExtension);
        Assert.IsTrue(model.IncludeIntercept);
        Assert.IsTrue(model.IncludeSeasonality);
        Assert.AreEqual(ARIMAX.Trend.Linear, model.TrendType);
        Assert.AreEqual(2, model.AROrderP);
        Assert.AreEqual(1, model.DiffOrderD);
        Assert.AreEqual(1, model.MAOrderQ);
        Assert.AreEqual(0, model.XOrderB);
        Assert.AreEqual(15, model.TrainingTimeSteps);
        Assert.IsFalse(model.UseDefaultTrainingSteps);
        Assert.IsFalse(model.UseDefaultFlatPriors);
        Assert.IsTrue(model.UseJeffreysRuleForScale);
        AssertEstablishedAttributes(model.ToXElement(), "ARIMAX", "DiffOrderD", "1");
    }

    /// <summary>
    /// Verifies the additive manual-lambda attributes restore and re-emit exactly for all four
    /// time-series model types without changing established XML names.
    /// </summary>
    [TestMethod]
    public void ManualTransformLambda_NewXml_RoundTripsAllModelTypes()
    {
        TimeSeries series = CreatePositiveSeries();
        var models = new ModelBase[]
        {
            new AutoRegressive(series.Clone(), XElement.Parse(
                "<AutoRegressive Order=\"1\" IncludeIntercept=\"False\" TransformType=\"BoxCox\" TrainingTimeSteps=\"12\" UseDefaultTrainingSteps=\"False\" TransformLambda=\"-0.35\" TransformLambdaIsManual=\"True\"><Parameters /></AutoRegressive>")),
            new MovingAverage(series.Clone(), XElement.Parse(
                "<MovingAverage Order=\"1\" IncludeIntercept=\"False\" TransformType=\"BoxCox\" TrainingTimeSteps=\"12\" UseDefaultTrainingSteps=\"False\" TransformLambda=\"-0.35\" TransformLambdaIsManual=\"True\"><Parameters /></MovingAverage>")),
            new ARIMA(series.Clone(), XElement.Parse(
                "<ARIMA POrder=\"1\" DOrder=\"1\" QOrder=\"0\" IncludeIntercept=\"False\" TransformType=\"BoxCox\" TrainingTimeSteps=\"12\" UseDefaultTrainingSteps=\"False\" TransformLambda=\"-0.35\" TransformLambdaIsManual=\"True\"><Parameters /></ARIMA>")),
            new ARIMAX(series.Clone(), XElement.Parse(
                "<ARIMAX AROrderP=\"1\" DiffOrderD=\"1\" MAOrderQ=\"0\" XOrderB=\"0\" IncludeIntercept=\"False\" TransformType=\"BoxCox\" TrainingTimeSteps=\"12\" UseDefaultTrainingSteps=\"False\" TransformLambda=\"-0.35\" TransformLambdaIsManual=\"True\"><Parameters /></ARIMAX>")),
        };

        foreach (ModelBase model in models)
        {
            double lambda = model switch
            {
                AutoRegressive ar => ar.TransformLambda,
                MovingAverage ma => ma.TransformLambda,
                ARIMA arima => arima.TransformLambda,
                ARIMAX arimax => arimax.TransformLambda,
                _ => double.NaN,
            };
            XElement saved = model.ToXElement();
            Assert.AreEqual(-0.35, lambda, 1E-12, model.GetType().Name);
            Assert.AreEqual("-0.35", saved.Attribute("TransformLambda")?.Value, model.GetType().Name);
            Assert.AreEqual("True", saved.Attribute("TransformLambdaIsManual")?.Value, model.GetType().Name);
        }
    }

    /// <summary>
    /// Computes the Yeo-Johnson exponent a model refits from its training window when the legacy
    /// XML carries no exponent.
    /// </summary>
    /// <param name="trainingSteps">The number of leading raw observations in the training window.</param>
    /// <returns>The fitted exponent.</returns>
    private static double ExpectedYeoJohnsonLambda(int trainingSteps)
    {
        double[] values = CreatePositiveSeries().ValuesToArray().Take(trainingSteps).ToArray();
        YeoJohnson.FitLambda(values, out double lambda);
        return lambda;
    }

    /// <summary>
    /// Creates a deterministic positive series suitable for every supported transform.
    /// </summary>
    /// <returns>A 20-observation annual time series.</returns>
    private static TimeSeries CreatePositiveSeries()
    {
        var series = new TimeSeries(TimeInterval.OneYear);
        DateTime date = new(2000, 1, 1);
        for (int i = 0; i < 20; i++)
        {
            series.Add(new SeriesOrdinate<DateTime, double>(date, 10.0 + i + 0.05 * i * i));
            date = TimeSeries.AddTimeInterval(date, TimeInterval.OneYear);
        }

        return series;
    }

    /// <summary>
    /// Verifies the element name, a model-specific attribute, and all shared persisted attributes.
    /// </summary>
    /// <param name="saved">Serialized model element.</param>
    /// <param name="elementName">Expected element name.</param>
    /// <param name="modelAttribute">Model-specific attribute name.</param>
    /// <param name="modelAttributeValue">Expected model-specific attribute value.</param>
    private static void AssertEstablishedAttributes(
        XElement saved,
        string elementName,
        string modelAttribute,
        string modelAttributeValue)
    {
        Assert.AreEqual(elementName, saved.Name.LocalName);
        Assert.AreEqual(modelAttributeValue, saved.Attribute(modelAttribute)?.Value);
        Assert.IsNotNull(saved.Attribute(nameof(AutoRegressive.IncludeIntercept)));
        Assert.IsNotNull(saved.Attribute(nameof(AutoRegressive.UseDefaultFlatPriors)));
        Assert.IsNotNull(saved.Attribute(nameof(AutoRegressive.UseJeffreysRuleForScale)));
        Assert.IsNotNull(saved.Attribute(nameof(AutoRegressive.TransformType)));
        Assert.IsNotNull(saved.Attribute(nameof(AutoRegressive.TrainingTimeSteps)));
        Assert.IsNotNull(saved.Attribute(nameof(AutoRegressive.UseDefaultTrainingSteps)));
        Assert.IsNotNull(saved.Element(nameof(AutoRegressive.Parameters)));
    }
}
