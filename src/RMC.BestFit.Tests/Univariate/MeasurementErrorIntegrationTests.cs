using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Unit tests for measurement-error integration conventions in univariate models.
/// </summary>
/// <remarks>
/// These tests stay programmatic: they validate fixed moment and validation behavior without
/// running MLE, GMM, or MCMC estimators.
/// </remarks>
[TestClass]
public class MeasurementErrorIntegrationTests
{
    /// <summary>
    /// Creates a small positive data frame for validation tests.
    /// </summary>
    /// <returns>A data frame with positive exact observations.</returns>
    private static BestFitDataFrame CreatePositiveDataFrame()
    {
        var df = new BestFitDataFrame();
        df.ExactSeries.Add(new ExactData(2000, 90.0));
        df.ExactSeries.Add(new ExactData(2001, 100.0));
        df.ExactSeries.Add(new ExactData(2002, 110.0));
        df.ExactSeries.Add(new ExactData(2003, 120.0));
        df.ExactSeries.Add(new ExactData(2004, 130.0));
        return df;
    }

    /// <summary>
    /// B17C uncertain rows should add measurement-error variance to the hydrologic process variance.
    /// </summary>
    [TestMethod]
    public void Bulletin17C_MomentConditions_UncertainUniform_IncludesMeasurementErrorVariance()
    {
        var df = new BestFitDataFrame();
        df.UncertainSeries.Add(new UncertainData(2000, new Uniform(80.0, 120.0)));
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);

        var (g, s) = model.MomentConditions([100.0, 10.0]);
        double retainedHalfWidth = 20.0 * (1.0 - 2E-8);
        double measurementErrorVariance = retainedHalfWidth * retainedHalfWidth / 3.0;
        double secondMomentMeasurementErrorVariance = 4.0 * Math.Pow(retainedHalfWidth, 4.0) / 45.0;

        Assert.AreEqual(0.0, g[0], 1E-10);
        Assert.AreEqual(measurementErrorVariance - 100.0, g[1], 1E-8);
        Assert.AreEqual(100.0 + measurementErrorVariance, s[0, 0], 1E-8);
        Assert.AreEqual(20000.0 + secondMomentMeasurementErrorVariance, s[1, 1], 1E-6);
    }

    /// <summary>
    /// LP3 validation must inspect retained ME support, not only the uncertain-data mean.
    /// </summary>
    [TestMethod]
    public void Bulletin17C_Validate_LogPearson_UncertainLowerTailCrossesZero_IsInvalid()
    {
        var df = CreatePositiveDataFrame();
        df.UncertainSeries.Add(new UncertainData(2005, new Normal(100.0, 50.0)));
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("non-positive", StringComparison.OrdinalIgnoreCase) ||
                                        m.Contains("1E-8", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Univariate log likelihood models reject uncertain distributions whose retained support crosses zero.
    /// </summary>
    [TestMethod]
    public void UnivariateDistribution_Validate_LogModel_UncertainLowerTailCrossesZero_IsInvalid()
    {
        var df = CreatePositiveDataFrame();
        df.UncertainSeries.Add(new UncertainData(2005, new Normal(100.0, 50.0)));
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LogNormal);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("non positive", StringComparison.OrdinalIgnoreCase) ||
                                        m.Contains("1E-8", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Mixture log components reject uncertain lower support even when zero inflation is enabled.
    /// </summary>
    [TestMethod]
    public void MixtureModel_Validate_LogComponent_UncertainLowerTailCrossesZero_IsInvalid()
    {
        var df = CreatePositiveDataFrame();
        df.UncertainSeries.Add(new UncertainData(2005, new Normal(100.0, 50.0)));
        var model = new MixtureModel(df, [UnivariateDistributionType.LogNormal], isZeroInflated: true);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("retained uncertain support", StringComparison.OrdinalIgnoreCase) ||
                                        m.Contains("1E-8", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Competing-risks log components reject uncertain lower support crossing zero.
    /// </summary>
    [TestMethod]
    public void CompetingRisksModel_Validate_LogComponent_UncertainLowerTailCrossesZero_IsInvalid()
    {
        var df = CreatePositiveDataFrame();
        df.UncertainSeries.Add(new UncertainData(2005, new Normal(100.0, 50.0)));
        var model = new CompetingRisksModel(df, [UnivariateDistributionType.LogNormal]);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("non-positive retained support", StringComparison.OrdinalIgnoreCase) ||
                                        m.Contains("1E-8", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Point-process validation accepts ordinary finite ME integration bounds.
    /// </summary>
    [TestMethod]
    public void PointProcessModel_Validate_ValidUncertainMeasurementErrorBounds_IsValid()
    {
        var df = CreatePositiveDataFrame();
        df.UncertainSeries.Add(new UncertainData(2005, new Normal(110.0, 5.0)));
        var model = new PointProcessModel { DataFrame = df };

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }
}
