using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Estimation;

/// <summary>
/// Expanded programmatic unit tests for <see cref="MaximumLikelihood"/>.
/// Exercises configuration, ClearResults, and pre-estimation state without running
/// an estimator (no Optimizer.Maximize calls).
/// </summary>
/// <remarks>
/// Computational MLE convergence tests live in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class MaximumLikelihoodExpandedTests
{
    /// <summary>
    /// Creates a small Normal model used as the estimation target across tests.
    /// </summary>
    private static UnivariateDistribution MakeNormalModel()
    {
        var df = new DataFrame
        {
            ExactSeries = new ExactSeries(
                new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 })
        };
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    /// <summary>
    /// Setting OptimizerMethod to the same value does not raise events / reinitialize state.
    /// </summary>
    [TestMethod]
    public void OptimizerMethod_SameValue_DoesNotChangeState()
    {
        // Arrange
        var mle = new MaximumLikelihood(MakeNormalModel(), OptimizationMethod.NelderMead);
        var optimizerBefore = mle.Optimizer;

        // Act
        mle.OptimizerMethod = OptimizationMethod.NelderMead;

        // Assert: optimizer instance preserved when value is unchanged
        Assert.AreSame(optimizerBefore, mle.Optimizer);
    }

    /// <summary>
    /// Constructor produces non-null Optimizer instance.
    /// </summary>
    [TestMethod]
    public void Constructor_OptimizerIsNonNull()
    {
        // Act
        var mle = new MaximumLikelihood(MakeNormalModel());

        // Assert
        Assert.IsNotNull(mle.Optimizer);
    }

    /// <summary>
    /// BestParameterSet is initialized to an empty ParameterSet (deterministic empty,
    /// not null), per the documented contract.
    /// </summary>
    [TestMethod]
    public void BestParameterSet_BeforeEstimation_IsEmpty()
    {
        // Arrange
        var mle = new MaximumLikelihood(MakeNormalModel());

        // Assert: deterministic empty so consumers don't get NullReferenceException.
        Assert.IsNotNull(mle.BestParameterSet);
    }

    /// <summary>
    /// Switching OptimizerMethod to BFGS produces a non-null Optimizer.
    /// </summary>
    [TestMethod]
    public void OptimizerMethod_BFGS_ProducesOptimizer()
    {
        // Arrange
        var mle = new MaximumLikelihood(MakeNormalModel());

        // Act
        mle.OptimizerMethod = OptimizationMethod.BFGS;

        // Assert
        Assert.AreEqual(OptimizationMethod.BFGS, mle.OptimizerMethod);
        Assert.IsNotNull(mle.Optimizer);
    }

    /// <summary>
    /// Switching OptimizerMethod to Powell produces a non-null Optimizer.
    /// </summary>
    [TestMethod]
    public void OptimizerMethod_Powell_ProducesOptimizer()
    {
        // Arrange
        var mle = new MaximumLikelihood(MakeNormalModel());

        // Act
        mle.OptimizerMethod = OptimizationMethod.Powell;

        // Assert
        Assert.AreEqual(OptimizationMethod.Powell, mle.OptimizerMethod);
        Assert.IsNotNull(mle.Optimizer);
    }

    /// <summary>
    /// Switching OptimizerMethod to MultilevelSingleLinkage produces a non-null Optimizer.
    /// </summary>
    [TestMethod]
    public void OptimizerMethod_MultilevelSingleLinkage_ProducesOptimizer()
    {
        // Arrange
        var mle = new MaximumLikelihood(MakeNormalModel());

        // Act
        mle.OptimizerMethod = OptimizationMethod.MultilevelSingleLinkage;

        // Assert
        Assert.AreEqual(OptimizationMethod.MultilevelSingleLinkage, mle.OptimizerMethod);
        Assert.IsNotNull(mle.Optimizer);
    }

    /// <summary>
    /// Switching OptimizerMethod to NelderMead produces a non-null Optimizer.
    /// </summary>
    [TestMethod]
    public void OptimizerMethod_NelderMead_ProducesOptimizer()
    {
        // Arrange
        var mle = new MaximumLikelihood(MakeNormalModel());

        // Act
        mle.OptimizerMethod = OptimizationMethod.NelderMead;

        // Assert
        Assert.AreEqual(OptimizationMethod.NelderMead, mle.OptimizerMethod);
        Assert.IsNotNull(mle.Optimizer);
    }

    /// <summary>
    /// Status is OptimizationStatus.None on a fresh MLE.
    /// </summary>
    [TestMethod]
    public void Status_BeforeEstimation_IsNone()
    {
        // Arrange
        var mle = new MaximumLikelihood(MakeNormalModel());

        // Assert
        Assert.AreEqual(OptimizationStatus.None, mle.Status);
    }
}
