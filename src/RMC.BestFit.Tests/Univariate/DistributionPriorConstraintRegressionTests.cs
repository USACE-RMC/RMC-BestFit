using Numerics;
using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions.Support;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Regression tests for Numerics parameter constraints propagated into BestFit priors and trends.
/// </summary>
[TestClass]
public class DistributionPriorConstraintRegressionTests
{
    /// <summary>
    /// Verifies restored legacy scale constraints remain visible in stationary BestFit priors.
    /// </summary>
    /// <param name="distributionType">The BestFit distribution family under test.</param>
    /// <param name="sample">The deterministic inline sample.</param>
    /// <param name="parameterIndex">The scale parameter index.</param>
    /// <param name="expectedUpper">The independently recorded legacy upper bound.</param>
    /// <param name="legacyInteriorValue">A value admitted by the legacy prior envelope.</param>
    [DataTestMethod]
    [DynamicData(nameof(StationaryScaleCases), DynamicDataSourceType.Method)]
    public void StationaryScalePrior_RetainsLegacyConstraintEnvelope(
        UnivariateDistributionType distributionType,
        double[] sample,
        int parameterIndex,
        double expectedUpper,
        double legacyInteriorValue)
    {
        var model = new UnivariateDistribution(CreateDataFrame(sample), distributionType);
        var parameter = model.Parameters[parameterIndex];

        Assert.AreEqual(Tools.DoubleMachineEpsilon, parameter.LowerBound);
        Assert.AreEqual(expectedUpper, parameter.UpperBound);
        Assert.IsTrue(parameter.Value >= parameter.LowerBound && parameter.Value <= parameter.UpperBound);
        Assert.IsTrue(parameter.IsPositive);
        Assert.IsInstanceOfType<Uniform>(parameter.PriorDistribution);
        CollectionAssert.AreEqual(
            new[] { Tools.DoubleMachineEpsilon, expectedUpper },
            parameter.PriorDistribution.GetParameters);
        Assert.IsTrue(double.IsFinite(parameter.PriorDistribution.LogPDF(legacyInteriorValue)));
    }

    /// <summary>
    /// Verifies the restored log-scale upper bound drives the established nonstationary coefficient envelopes.
    /// </summary>
    /// <param name="trendType">The polynomial trend type under test.</param>
    /// <param name="coefficientCount">The number of nonconstant polynomial coefficients.</param>
    [DataTestMethod]
    [DataRow(TrendModelType.Linear, 1)]
    [DataRow(TrendModelType.Quadratic, 2)]
    [DataRow(TrendModelType.Cubic, 3)]
    public void LowDispersionLogScaleTrend_RetainsLegacyCoefficientEnvelope(
        TrendModelType trendType,
        int coefficientCount)
    {
        var model = new UnivariateDistribution(
            CreateDataFrame([1.001d, 1.002d, 1.003d, 1.004d]),
            UnivariateDistributionType.LogNormal)
        {
            IsNonstationary = true
        };

        model.SetTrendModel(1, trendType);

        var trend = model.TrendModels[1];
        Assert.AreEqual(Tools.DoubleMachineEpsilon, trend.Parameters[0].LowerBound);
        Assert.AreEqual(2d, trend.Parameters[0].UpperBound);
        Assert.IsTrue(trend.Parameters[0].IsPositive);
        CollectionAssert.AreEqual(
            new[] { Tools.DoubleMachineEpsilon, 2d },
            trend.Parameters[0].PriorDistribution.GetParameters);

        for (int index = 1; index <= coefficientCount; index++)
        {
            Assert.AreEqual(-1d, trend.Parameters[index].LowerBound);
            Assert.AreEqual(1d, trend.Parameters[index].UpperBound);
            CollectionAssert.AreEqual(
                new[] { -1d, 1d },
                trend.Parameters[index].PriorDistribution.GetParameters);
        }
    }

    /// <summary>
    /// Supplies deterministic distribution cases and independently recorded legacy scale bounds.
    /// </summary>
    /// <returns>The stationary prior regression cases.</returns>
    private static IEnumerable<object[]> StationaryScaleCases()
    {
        yield return
        [
            UnivariateDistributionType.Logistic,
            new[] { 10d, 12d, 14d, 16d },
            1,
            100d,
            75d
        ];
        yield return
        [
            UnivariateDistributionType.LogNormal,
            new[] { 1.001d, 1.002d, 1.003d, 1.004d },
            1,
            2d,
            1d
        ];
        yield return
        [
            UnivariateDistributionType.LogPearsonTypeIII,
            new[] { 1.001d, 1.002d, 1.003d, 1.004d },
            1,
            2d,
            1d
        ];
    }

    /// <summary>
    /// Creates a valid exact-observation data frame from a small inline fixture.
    /// </summary>
    /// <param name="values">The exact observation values.</param>
    /// <returns>A data frame with one exact series.</returns>
    private static BestFitDataFrame CreateDataFrame(double[] values)
    {
        return new BestFitDataFrame { ExactSeries = new ExactSeries(values) };
    }
}
