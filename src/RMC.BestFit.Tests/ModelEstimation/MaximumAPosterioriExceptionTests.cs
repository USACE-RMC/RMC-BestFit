using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Tests the documented exception contracts of <c>MaximumAPosteriori</c> methods
/// that require a successful Estimate() run before returning meaningful values.
/// All tests verify the throw path without ever running the optimizer.
/// </summary>
[TestClass]
public class MaximumAPosterioriExceptionTests
{
    /// <summary>
    /// Builds a small Normal model for the MAP target.
    /// </summary>
    private static UnivariateDistribution MakeNormalModel()
    {
        var df = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(
                new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 })
        };
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    /// <summary>
    /// GetCovarianceMatrix throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetCovarianceMatrix_BeforeEstimation_Throws()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => map.GetCovarianceMatrix());
    }

    /// <summary>
    /// GetStandardErrors propagates the InvalidOperationException from GetCovarianceMatrix.
    /// </summary>
    [TestMethod]
    public void GetStandardErrors_BeforeEstimation_Throws()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => map.GetStandardErrors());
    }

    /// <summary>
    /// GetCorrelationMatrix propagates the InvalidOperationException from GetCovarianceMatrix.
    /// </summary>
    [TestMethod]
    public void GetCorrelationMatrix_BeforeEstimation_Throws()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => map.GetCorrelationMatrix());
    }

    /// <summary>
    /// GetAIC throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetAIC_BeforeEstimation_Throws()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => map.GetAIC());
    }

    /// <summary>
    /// GetBIC throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetBIC_BeforeEstimation_Throws()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => map.GetBIC(100));
    }

    /// <summary>
    /// ProfileLikelihood throws ArgumentOutOfRangeException for bins less than 2 (not the
    /// IsEstimated branch — the bins guard fires first when bins is 1).
    /// </summary>
    /// <remarks>
    /// The throw order in the production code is IsEstimated first, then bins; so this
    /// test passes a fresh MAP and checks that the IsEstimated branch fires before bins
    /// validation. Documented contract: InvalidOperationException must surface first.
    /// </remarks>
    [TestMethod]
    public void ProfileLikelihood_BinsOne_BeforeEstimation_ThrowsInvalidOperation()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        // IsEstimated guard fires before the bins guard, so InvalidOperationException
        // is the surfaced exception type.
        Assert.ThrowsException<InvalidOperationException>(() => map.ProfileLikelihood(1));
    }

    /// <summary>
    /// ParameterConfidenceIntervals throws for invalid alpha values when not estimated
    /// (InvalidOperationException fires first in the production guard order).
    /// </summary>
    [TestMethod]
    public void ParameterConfidenceIntervals_InvalidAlpha_BeforeEstimation_ThrowsInvalidOperation()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(
            () => map.ParameterConfidenceIntervals(0.0));
    }

    /// <summary>
    /// GetObservationInfluence throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetObservationInfluence_BeforeEstimation_Throws()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => map.GetObservationInfluence());
    }

    /// <summary>
    /// GetCooksDistance throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetCooksDistance_BeforeEstimation_Throws()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => map.GetCooksDistance());
    }

    /// <summary>
    /// ComputeLeverageDiagnostics throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void ComputeLeverageDiagnostics_BeforeEstimation_Throws()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => map.ComputeLeverageDiagnostics());
    }
}
