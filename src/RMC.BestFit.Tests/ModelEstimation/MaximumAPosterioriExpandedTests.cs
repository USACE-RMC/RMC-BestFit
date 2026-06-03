using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Estimation;

/// <summary>
/// Expanded programmatic unit tests for <see cref="MaximumAPosteriori"/>.
/// Exercises pre-estimation state, ClearResults, and the documented exception
/// contracts on methods that require an estimated fit.
/// </summary>
/// <remarks>
/// These tests do NOT run the optimizer. Computational MAP convergence /
/// recovery tests live in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class MaximumAPosterioriExpandedTests
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

    #region Property / pre-estimation state

    /// <summary>
    /// Constructor produces non-null Optimizer instance.
    /// </summary>
    [TestMethod]
    public void Constructor_OptimizerIsNonNull()
    {
        // Act
        var map = new MaximumAPosteriori(MakeNormalModel());

        // Assert
        Assert.IsNotNull(map.Optimizer);
    }

    /// <summary>
    /// Status is OptimizationStatus.None on a fresh MAP instance.
    /// </summary>
    [TestMethod]
    public void Status_BeforeEstimation_IsNone()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel());

        // Assert
        Assert.AreEqual(OptimizationStatus.None, map.Status);
    }

    /// <summary>
    /// TotalFunctionEvaluations is zero before estimation runs.
    /// </summary>
    [TestMethod]
    public void TotalFunctionEvaluations_BeforeEstimation_IsZero()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel());

        // Assert
        Assert.AreEqual(0, map.TotalFunctionEvaluations);
    }

    /// <summary>
    /// Constructor populates parameter bounds arrays with model parameter counts.
    /// </summary>
    [TestMethod]
    public void Constructor_PopulatesBounds()
    {
        // Arrange
        var model = MakeNormalModel();

        // Act
        var map = new MaximumAPosteriori(model);

        // Assert
        Assert.AreEqual(model.Parameters.Count, map.InitialValues.Length);
        Assert.AreEqual(model.Parameters.Count, map.LowerBounds.Length);
        Assert.AreEqual(model.Parameters.Count, map.UpperBounds.Length);
    }

    /// <summary>
    /// BestParameterSet starts as a deterministic empty <see cref="ParameterSet"/>,
    /// not null — protects callers that bypass the IsEstimated guard.
    /// </summary>
    [TestMethod]
    public void BestParameterSet_BeforeEstimation_IsNotNull()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel());

        // Assert
        Assert.IsNotNull(map.BestParameterSet);
    }

    /// <summary>
    /// IsEstimated is false on a fresh MAP instance.
    /// </summary>
    [TestMethod]
    public void IsEstimated_BeforeEstimation_IsFalse()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel());

        // Assert
        Assert.IsFalse(map.IsEstimated);
    }

    #endregion

    #region OptimizerMethod round-trip

    /// <summary>
    /// OptimizerMethod setter rebuilds the Optimizer.
    /// </summary>
    [TestMethod]
    public void OptimizerMethod_Setter_RebuildsOptimizer()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel(), OptimizationMethod.NelderMead);
        var optimizerBefore = map.Optimizer;

        // Act
        map.OptimizerMethod = OptimizationMethod.MultilevelSingleLinkage;

        // Assert: same value would be a no-op; different value must rebuild.
        Assert.AreNotSame(optimizerBefore, map.Optimizer,
            "Setter should rebuild Optimizer when method changes.");
    }

    /// <summary>
    /// OptimizerMethod set to same value is a no-op (Optimizer instance preserved).
    /// </summary>
    [TestMethod]
    public void OptimizerMethod_SameValue_PreservesOptimizer()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel(), OptimizationMethod.NelderMead);
        var optimizerBefore = map.Optimizer;

        // Act
        map.OptimizerMethod = OptimizationMethod.NelderMead;

        // Assert
        Assert.AreSame(optimizerBefore, map.Optimizer);
    }

    /// <summary>
    /// OptimizerMethod set to BFGS, Powell, MultilevelSingleLinkage all produce non-null Optimizer.
    /// </summary>
    [TestMethod]
    public void OptimizerMethod_VariousValues_AllProduceOptimizer()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel());

        foreach (var method in new[]
        {
            OptimizationMethod.BFGS,
            OptimizationMethod.Powell,
            OptimizationMethod.NelderMead,
            OptimizationMethod.MultilevelSingleLinkage,
            OptimizationMethod.DifferentialEvolution
        })
        {
            // Act
            map.OptimizerMethod = method;

            // Assert
            Assert.AreEqual(method, map.OptimizerMethod);
            Assert.IsNotNull(map.Optimizer);
        }
    }

    #endregion

    #region ClearResults

    /// <summary>
    /// ClearResults restores Status to None, IsEstimated to false, and a fresh BestParameterSet.
    /// </summary>
    [TestMethod]
    public void ClearResults_RestoresPreEstimationState()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel());

        // Act
        map.ClearResults();

        // Assert
        Assert.IsFalse(map.IsEstimated);
        Assert.AreEqual(OptimizationStatus.None, map.Status);
        Assert.AreEqual(0, map.TotalFunctionEvaluations);
        Assert.IsNotNull(map.BestParameterSet);
    }

    #endregion

    #region Documented exception contracts

    /// <summary>
    /// ProfileLikelihood throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void ProfileLikelihood_BeforeEstimation_Throws()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel());

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() => map.ProfileLikelihood());
    }

    /// <summary>
    /// ParameterConfidenceIntervals throws InvalidOperationException before estimation.
    /// </summary>
    [TestMethod]
    public void ParameterConfidenceIntervals_BeforeEstimation_Throws()
    {
        // Arrange
        var map = new MaximumAPosteriori(MakeNormalModel());

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(
            () => map.ParameterConfidenceIntervals());
    }

    #endregion
}
