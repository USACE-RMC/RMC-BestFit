using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Fast structural unit tests for the <c>MaximumAPosteriori</c> class.
/// Covers constructor, configuration round-trip, and pre-estimation state.
/// Real estimation runs live in <c>RMC.BestFit.Verification</c>.
/// </summary>
[TestClass]
public class MaximumAPosterioriTests
{
    /// <summary>
    /// Creates normal Model.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static UnivariateDistribution MakeNormalModel()
    {
        var df = new BestFitDataFrame { ExactSeries = new ExactSeries(
            new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 }) };
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    /// <summary>Verifies that constructor with model defaults applied.</summary>
    [TestMethod]
    public void Test_Constructor_WithModel_DefaultsApplied()
    {
        var model = MakeNormalModel();

        var map = new MaximumAPosteriori(model);

        Assert.AreSame(model, map.Model);
        Assert.IsFalse(map.IsEstimated);
    }

    /// <summary>Verifies that optimizer control properties use the intended defaults.</summary>
    [TestMethod]
    public void Test_Constructor_OptimizerControls_DefaultsApplied()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());

        Assert.IsFalse(map.ReportFailure);
        Assert.IsFalse(map.Optimizer.ReportFailure);
        Assert.IsTrue(map.ComputeHessian);
    }

    /// <summary>Verifies that report failure setting updates the current optimizer.</summary>
    [TestMethod]
    public void Test_ReportFailure_Setter_UpdatesOptimizer()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());

        map.ReportFailure = true;

        Assert.IsTrue(map.ReportFailure);
        Assert.IsTrue(map.Optimizer.ReportFailure);
    }

    /// <summary>Verifies that Hessian setting round-trips.</summary>
    [TestMethod]
    public void Test_ComputeHessian_RoundTrip()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());

        map.ComputeHessian = false;

        Assert.IsFalse(map.ComputeHessian);
    }

    /// <summary>Verifies that constructor throws when null model.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullModel_Throws()
    {
        _ = new MaximumAPosteriori(null!);
    }

    /// <summary>Verifies that maximum log likelihood is na n when before estimation.</summary>
    [TestMethod]
    public void Test_MaximumLogLikelihood_BeforeEstimation_IsNaN()
    {
        var map = new MaximumAPosteriori(MakeNormalModel());

        // MAP follows the same sign convention as MLE — MaximumLogLikelihood is NaN
        // before estimation and = -BestParameterSet.Fitness afterward.
        Assert.IsTrue(double.IsNaN(map.MaximumLogLikelihood),
            "MaximumLogLikelihood must be NaN before estimation.");
    }

    /// <summary>Verifies that optimizer method round trip for .</summary>
    [TestMethod]
    public void Test_OptimizerMethod_RoundTrip()
    {
        var map = new MaximumAPosteriori(MakeNormalModel(), OptimizationMethod.NelderMead);

        Assert.AreEqual(OptimizationMethod.NelderMead, map.OptimizerMethod);

        map.OptimizerMethod = OptimizationMethod.MultilevelSingleLinkage;

        Assert.AreEqual(OptimizationMethod.MultilevelSingleLinkage, map.OptimizerMethod);
        Assert.IsFalse(map.IsEstimated);
    }

    /// <summary>Verifies that constructor populates parameter shape.</summary>
    [TestMethod]
    public void Test_Constructor_PopulatesParameterShape()
    {
        var model = MakeNormalModel();

        var map = new MaximumAPosteriori(model);

        Assert.AreEqual(model.Parameters.Count, map.NumberOfParameters);
    }
}
