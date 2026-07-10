using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Tests the documented exception contracts of <c>MaximumLikelihood</c> methods
/// that require a successful Estimate() run before returning meaningful values.
/// All tests verify the throw path without ever running the optimizer.
/// </summary>
[TestClass]
public class MaximumLikelihoodExceptionTests
{
    /// <summary>
    /// Builds a small Normal model for the MLE target.
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
    /// Builds a single-parameter Exponential-style model used for the
    /// "fewer than two parameters" exception path.
    /// </summary>
    private static UnivariateDistribution MakeExponentialModel()
    {
        var df = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(
                new double[] { 12.5, 15.3, 8.9, 22.1, 18.7, 14.2, 9.8, 28.5, 17.4, 11.6 })
        };
        return new UnivariateDistribution(df, UnivariateDistributionType.Exponential);
    }

    /// <summary>
    /// GetCovarianceMatrix throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetCovarianceMatrix_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.GetCovarianceMatrix());
    }

    /// <summary>
    /// GetStandardErrors propagates the InvalidOperationException from GetCovarianceMatrix.
    /// </summary>
    [TestMethod]
    public void GetStandardErrors_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.GetStandardErrors());
    }

    /// <summary>
    /// GetCorrelationMatrix propagates the InvalidOperationException from GetCovarianceMatrix.
    /// </summary>
    [TestMethod]
    public void GetCorrelationMatrix_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.GetCorrelationMatrix());
    }

    /// <summary>
    /// GetSandwichCovarianceMatrix throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetSandwichCovarianceMatrix_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.GetSandwichCovarianceMatrix());
    }

    /// <summary>
    /// GetRobustStandardErrors throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetRobustStandardErrors_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.GetRobustStandardErrors());
    }

    /// <summary>
    /// GetObservationInfluence throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetObservationInfluence_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.GetObservationInfluence());
    }

    /// <summary>
    /// GetCooksDistance throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetCooksDistance_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.GetCooksDistance());
    }

    /// <summary>
    /// GetAIC throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetAIC_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.GetAIC());
    }

    /// <summary>
    /// GetBIC throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void GetBIC_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.GetBIC(100));
    }

    /// <summary>
    /// ProfileLikelihood throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void ProfileLikelihood_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(() => mle.ProfileLikelihood());
    }

    /// <summary>
    /// ParameterConfidenceIntervals throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void ParameterConfidenceIntervals_BeforeEstimation_Throws()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        Assert.ThrowsException<InvalidOperationException>(
            () => mle.ParameterConfidenceIntervals());
    }

    /// <summary>
    /// ClearResults restores pre-estimation state.
    /// </summary>
    [TestMethod]
    public void ClearResults_RestoresPreEstimationState()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());
        mle.ClearResults();

        Assert.IsFalse(mle.IsEstimated);
        Assert.AreEqual(OptimizationStatus.None, mle.Status);
        Assert.AreEqual(0, mle.TotalFunctionEvaluations);
        Assert.IsNotNull(mle.BestParameterSet);
    }
}
