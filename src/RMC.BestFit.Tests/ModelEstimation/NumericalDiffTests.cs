using RMC.BestFit.Estimation;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Fast unit tests for adaptive numerical differentiation safeguards.
/// </summary>
[TestClass]
public class NumericalDiffTests
{
    /// <summary>
    /// Verifies that smooth quadratic curvature is preserved by the adaptive Hessian path.
    /// </summary>
    [TestMethod]
    public void ComputeHessian_SmoothQuadratic_ReturnsExpectedCurvature()
    {
        double[] parameters = { 1.2, -0.7 };

        var hessian = NumericalDiff.ComputeHessian(
            theta => 3.0 * theta[0] * theta[0]
                + 2.0 * theta[1] * theta[1]
                + 5.0 * theta[0] * theta[1],
            parameters,
            parameters.Length);

        Assert.AreEqual(6.0, hessian[0, 0], 1e-6);
        Assert.AreEqual(4.0, hessian[1, 1], 1e-6);
        Assert.AreEqual(5.0, hessian[0, 1], 1e-6);
        Assert.AreEqual(5.0, hessian[1, 0], 1e-6);
    }

    /// <summary>
    /// Verifies that a flat finite step followed by a non-finite larger step exits with the last finite stencil.
    /// </summary>
    [TestMethod]
    public void ComputeHessian_FlatFiniteStepThenNonFiniteLargerStep_DoesNotCycle()
    {
        int evaluations = 0;

        var hessian = NumericalDiff.ComputeHessian(
            theta =>
            {
                evaluations++;
                return Math.Abs(theta[0]) <= 2.5e-4 ? 7.0 : double.NaN;
            },
            new[] { 0.0 },
            1);

        Assert.AreEqual(0.0, hessian[0, 0], 1e-10);
        Assert.IsTrue(evaluations <= 8, "The bounded step search should not revisit the same finite/non-finite cycle.");
    }

    /// <summary>
    /// Verifies that an initially non-finite stencil shrinks to a finite usable step.
    /// </summary>
    [TestMethod]
    public void ComputeHessian_InitialStepNonFinite_ShrinksToFiniteStep()
    {
        var hessian = NumericalDiff.ComputeHessian(
            theta => Math.Abs(theta[0]) <= 7.5e-5 ? theta[0] * theta[0] : double.NaN,
            new[] { 0.0 },
            1);

        Assert.AreEqual(2.0, hessian[0, 0], 1e-8);
    }

    /// <summary>
    /// Verifies that a function with no finite central stencil fails quickly instead of retrying forever.
    /// </summary>
    [TestMethod]
    public void ComputeHessian_NoFiniteCentralStencil_ThrowsInvalidOperationException()
    {
        int evaluations = 0;

        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            NumericalDiff.ComputeHessian(
                theta =>
                {
                    evaluations++;
                    return theta[0] == 0.0 ? 0.0 : double.NaN;
                },
                new[] { 0.0 },
                1));

        StringAssert.Contains(exception.Message, "no finite central stencil");
        Assert.IsTrue(evaluations < 100, "The bounded step search should terminate after a small number of attempts.");
    }
}
