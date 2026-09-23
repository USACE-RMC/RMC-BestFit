using System.Xml.Linq;
using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Verifies that distribution XML written by the public RMC.Numerics 2.1.4 package loads through
/// the strict distribution factory and through every BestFit element that embeds a distribution.
/// </summary>
/// <remarks>
/// The payloads are the literal <c>ToXElement()</c> output of the 2.1.4 package for each of the
/// fifteen supported distribution families, so a project saved by the public 2.0.0 release is
/// represented by exact strings rather than by the current writer.
/// </remarks>
[TestClass]
public class LegacyDistributionXmlTests
{
    /// <summary>
    /// The 2.1.4 payload, expected type, and expected parameter values for every supported family.
    /// </summary>
    private static readonly (string Xml, UnivariateDistributionType Type, double[] Parameters)[] LegacyPayloads =
    [
        ("<Distribution Type=\"Exponential\" Xi=\"100\" Alpha=\"10\" />", UnivariateDistributionType.Exponential, [100, 10]),
        ("<Distribution Type=\"GammaDistribution\" Theta=\"10\" Kappa=\"2\" />", UnivariateDistributionType.GammaDistribution, [10, 2]),
        ("<Distribution Type=\"GeneralizedExtremeValue\" Xi=\"100\" Alpha=\"10\" Kappa=\"0\" />", UnivariateDistributionType.GeneralizedExtremeValue, [100, 10, 0]),
        ("<Distribution Type=\"GeneralizedLogistic\" Xi=\"100\" Alpha=\"10\" Kappa=\"0\" />", UnivariateDistributionType.GeneralizedLogistic, [100, 10, 0]),
        ("<Distribution Type=\"GeneralizedNormal\" Xi=\"100\" Alpha=\"10\" Kappa=\"0\" />", UnivariateDistributionType.GeneralizedNormal, [100, 10, 0]),
        ("<Distribution Type=\"GeneralizedPareto\" Xi=\"100\" Alpha=\"10\" Kappa=\"0\" />", UnivariateDistributionType.GeneralizedPareto, [100, 10, 0]),
        ("<Distribution Type=\"Gumbel\" Xi=\"100\" Alpha=\"10\" />", UnivariateDistributionType.Gumbel, [100, 10]),
        ("<Distribution Type=\"KappaFour\" Xi=\"100\" Alpha=\"10\" Kappa=\"0\" Hondo=\"0\" />", UnivariateDistributionType.KappaFour, [100, 10, 0, 0]),
        ("<Distribution Type=\"LnNormal\" Mean=\"9.9999999999999982\" StandardDeviation=\"9.9999999999999964\" />", UnivariateDistributionType.LnNormal, [9.9999999999999982, 9.9999999999999964]),
        ("<Distribution Type=\"Logistic\" Xi=\"0\" Alpha=\"0.10000000000000001\" />", UnivariateDistributionType.Logistic, [0, 0.1]),
        ("<Distribution Type=\"LogNormal\" Mu=\"3\" Sigma=\"0.5\" />", UnivariateDistributionType.LogNormal, [3, 0.5]),
        ("<Distribution Type=\"LogPearsonTypeIII\" Mu=\"3\" Sigma=\"0.5\" Gamma=\"0\" />", UnivariateDistributionType.LogPearsonTypeIII, [3, 0.5, 0]),
        ("<Distribution Type=\"Normal\" Mu=\"0\" Sigma=\"1\" />", UnivariateDistributionType.Normal, [0, 1]),
        ("<Distribution Type=\"PearsonTypeIII\" Mu=\"100\" Sigma=\"10\" Gamma=\"0\" />", UnivariateDistributionType.PearsonTypeIII, [100, 10, 0]),
        ("<Distribution Type=\"Weibull\" Lambda=\"10\" Kappa=\"2\" />", UnivariateDistributionType.Weibull, [10, 2]),
    ];

    /// <summary>
    /// Every 2.1.4 payload loads through the strict factory with its exact type and parameter values.
    /// </summary>
    [TestMethod]
    public void Factory_LoadsEveryLegacyPayload_WithExactParameters()
    {
        foreach (var (xml, type, parameters) in LegacyPayloads)
        {
            UnivariateDistributionBase distribution = UnivariateDistributionFactory.CreateDistribution(XElement.Parse(xml));

            Assert.AreEqual(type, distribution.Type, xml);
            CollectionAssert.AreEqual(parameters, distribution.GetParameters, xml);
        }
    }

    /// <summary>
    /// A univariate model saved with an embedded 2.1.4 distribution element restores that distribution.
    /// </summary>
    [TestMethod]
    public void UnivariateDistribution_RestoresEmbeddedLegacyDistribution()
    {
        var frame = new BestFitDataFrame { ExactSeries = new ExactSeries(new double[] { 1200, 1450, 980, 1710, 1330 }) };
        var element = new XElement(nameof(UnivariateDistribution),
            XElement.Parse("<Distribution Type=\"LogPearsonTypeIII\" Mu=\"3\" Sigma=\"0.5\" Gamma=\"0\" />"));

        var model = new UnivariateDistribution(frame, element);

        Assert.AreEqual(UnivariateDistributionType.LogPearsonTypeIII, model.Distribution.Type);
        CollectionAssert.AreEqual(new double[] { 3, 0.5, 0 }, model.Distribution.GetParameters);
    }

    /// <summary>
    /// Uncertain observations, fitted distributions, and parameter priors restore their embedded
    /// 2.1.4 distribution elements.
    /// </summary>
    [TestMethod]
    public void EmbeddingElements_RestoreLegacyDistributions()
    {
        var uncertain = new UncertainData(XElement.Parse(
            "<UncertainData Index=\"1990\" PlottingPosition=\"0.5\"><Distribution Type=\"Normal\" Mu=\"1500\" Sigma=\"120\" /></UncertainData>"));
        Assert.AreEqual(UnivariateDistributionType.Normal, uncertain.Distribution.Type);
        Assert.AreEqual(1500d, uncertain.Value, 0d);

        var fitted = new FittedDistribution(XElement.Parse(
            "<FittedDistribution AIC=\"10\" BIC=\"12\" RMSE=\"0.5\" FitSucceeded=\"True\" ShowResults=\"True\">" +
            "<Distribution Type=\"GeneralizedExtremeValue\" Xi=\"100\" Alpha=\"10\" Kappa=\"0\" /></FittedDistribution>"));
        Assert.IsNotNull(fitted.Distribution);
        Assert.AreEqual(UnivariateDistributionType.GeneralizedExtremeValue, fitted.Distribution!.Type);
        Assert.IsTrue(fitted.FitSucceeded);

        var parameter = new ModelParameter(XElement.Parse(
            "<ModelParameter OwnerName=\"Normal\" Name=\"Mu\" Value=\"1\" LowerBound=\"-10\" UpperBound=\"10\" IsPositive=\"False\" IsFixed=\"False\">" +
            "<Distribution Type=\"Normal\" Mu=\"0\" Sigma=\"1\" /></ModelParameter>"));
        Assert.AreEqual(UnivariateDistributionType.Normal, parameter.PriorDistribution.Type);
        Assert.AreEqual(1d, parameter.Value, 0d);
    }
}
