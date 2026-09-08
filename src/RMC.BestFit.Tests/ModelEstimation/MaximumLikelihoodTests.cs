using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Fast structural unit tests for the <c>MaximumLikelihood</c> class.
/// Covers constructor, configuration round-trip, pre-estimation state, and the
/// <c>MaximumLikelihood.MaximumLogLikelihood</c> sign-convention contract.
/// Real estimation runs live in <c>RMC.BestFit.Verification</c>.
/// </summary>
[TestClass]
public class MaximumLikelihoodTests
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

    /// <summary>Verifies that constructor with model defaults to differential evolution.</summary>
    [TestMethod]
    public void Test_Constructor_WithModel_DefaultsToDifferentialEvolution()
    {
        var model = MakeNormalModel();

        var mle = new MaximumLikelihood(model);

        Assert.AreSame(model, mle.Model);
        Assert.AreEqual(OptimizationMethod.DifferentialEvolution, mle.OptimizerMethod);
        Assert.IsFalse(mle.IsEstimated);
        Assert.AreEqual(OptimizationStatus.None, mle.Status);
    }

    /// <summary>Verifies that optimizer control properties use the intended defaults.</summary>
    [TestMethod]
    public void Test_Constructor_OptimizerControls_DefaultsApplied()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());

        Assert.IsFalse(mle.ReportFailure);
        Assert.IsFalse(mle.Optimizer.ReportFailure);
        Assert.IsTrue(mle.ComputeHessian);
    }

    /// <summary>Verifies that report failure setting updates the current optimizer.</summary>
    [TestMethod]
    public void Test_ReportFailure_Setter_UpdatesOptimizer()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());

        mle.ReportFailure = true;

        Assert.IsTrue(mle.ReportFailure);
        Assert.IsTrue(mle.Optimizer.ReportFailure);
    }

    /// <summary>Verifies that Hessian setting round-trips.</summary>
    [TestMethod]
    public void Test_ComputeHessian_RoundTrip()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());

        mle.ComputeHessian = false;

        Assert.IsFalse(mle.ComputeHessian);
    }

    /// <summary>Verifies that constructor throws when null model.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullModel_Throws()
    {
        _ = new MaximumLikelihood(null!);
    }

    /// <summary>Verifies that constructor populates initial values and bounds.</summary>
    [TestMethod]
    public void Test_Constructor_PopulatesInitialValuesAndBounds()
    {
        var model = MakeNormalModel();
        var mle = new MaximumLikelihood(model);

        Assert.AreEqual(model.Parameters.Count, mle.InitialValues.Length);
        Assert.AreEqual(model.Parameters.Count, mle.LowerBounds.Length);
        Assert.AreEqual(model.Parameters.Count, mle.UpperBounds.Length);
        Assert.AreEqual(model.Parameters.Count, mle.NumberOfParameters);

        for (int i = 0; i < model.Parameters.Count; i++)
        {
            Assert.AreEqual(model.Parameters[i].Value, mle.InitialValues[i]);
            Assert.AreEqual(model.Parameters[i].LowerBound, mle.LowerBounds[i]);
            Assert.AreEqual(model.Parameters[i].UpperBound, mle.UpperBounds[i]);
        }
    }

    /// <summary>Verifies that optimizer method setter rebuilds optimizer.</summary>
    [TestMethod]
    public void Test_OptimizerMethod_Setter_RebuildsOptimizer()
    {
        var mle = new MaximumLikelihood(MakeNormalModel(), OptimizationMethod.NelderMead);
        Assert.AreEqual(OptimizationMethod.NelderMead, mle.OptimizerMethod);

        mle.OptimizerMethod = OptimizationMethod.MultilevelSingleLinkage;

        Assert.AreEqual(OptimizationMethod.MultilevelSingleLinkage, mle.OptimizerMethod);
        Assert.IsFalse(mle.IsEstimated, "Changing optimizer should clear estimation state.");
    }

    /// <summary>Verifies that maximum log likelihood is na n when before estimation.</summary>
    [TestMethod]
    public void Test_MaximumLogLikelihood_BeforeEstimation_IsNaN()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());

        // Sign-convention invariant: MaximumLogLikelihood => -BestParameterSet.Fitness when
        // estimated; NaN before estimation. This protects against the locked C16 finding
        // (a Numerics-side sign flip that would silently invert the LL).
        Assert.IsTrue(double.IsNaN(mle.MaximumLogLikelihood),
            "MaximumLogLikelihood must be NaN before estimation runs.");
    }

    /// <summary>Verifies that total function evaluations is zero when before estimation.</summary>
    [TestMethod]
    public void Test_TotalFunctionEvaluations_BeforeEstimation_IsZero()
    {
        var mle = new MaximumLikelihood(MakeNormalModel());

        Assert.AreEqual(0, mle.TotalFunctionEvaluations);
    }
}
