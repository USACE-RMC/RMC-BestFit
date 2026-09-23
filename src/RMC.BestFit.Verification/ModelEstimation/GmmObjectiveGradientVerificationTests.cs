using Numerics.Mathematics.LinearAlgebra;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies the GMM objective/gradient scale conventions used by penalized and unpenalized fits.
/// </summary>
/// <remarks>
/// Central differences are implemented directly in this test rather than using the production
/// numerical-differentiation helper. The nonlinear scalar moment makes the constant factor visible
/// away from the optimum. GMM covariance is verified from its estimating-equation bread and meat;
/// it is not inferred from the numerical Hessian of the scalar optimizer objective.
/// </remarks>
[TestClass]
public class GmmObjectiveGradientVerificationTests
{
    /// <summary>
    /// Verifies the current half-quadratic penalized objective against an independent derivative.
    /// </summary>
    [TestMethod]
    public void PenalizedObjectiveGradient_MatchesIndependentCentralDifference()
    {
        PenaltyFunction penalty = parameters => 0.5d * Math.Pow(parameters[0] - 0.5d, 2d) / 0.4d;
        GeneralizedMethodOfMoments gmm = CreateEstimator(penalty);
        double[] parameters = [1.3d];

        double expected = CentralDifference(gmm.Q, parameters[0]);
        double actual = gmm.GetGradient(parameters)[0];

        Assert.AreEqual(expected, actual, 1E-8d, "The penalized GMM objective and gradient scales differ.");
    }

    /// <summary>
    /// Verifies that the unpenalized estimating-equation gradient is one half of the derivative
    /// of the conventional <c>g'Wg</c> reporting objective.
    /// </summary>
    /// <remarks>
    /// Multiplication by this positive constant preserves the stationary point. The reported GMM
    /// covariance uses <c>(D'WD)^-1 / n</c>, not the objective Hessian, so the constant is not
    /// propagated into covariance. When a Gaussian penalty is present, the objective switches to
    /// the half-quadratic form and <see cref="PenalizedObjectiveGradient_MatchesIndependentCentralDifference"/>
    /// proves exact objective/gradient agreement for the combined data and penalty terms.
    /// </remarks>
    [TestMethod]
    public void UnpenalizedEstimatingGradient_IsHalfConventionalObjectiveDerivative()
    {
        GeneralizedMethodOfMoments gmm = CreateEstimator(null);
        double[] parameters = [1.3d];

        double expected = CentralDifference(gmm.Q, parameters[0]);
        double actual = gmm.GetGradient(parameters)[0];

        Assert.AreEqual(0.5d * expected, actual, 1E-8d,
            "The unpenalized estimating-equation gradient must retain the documented half-scale.");
    }

    /// <summary>
    /// Creates a one-parameter GMM estimator with a nonlinear analytical moment and Jacobian.
    /// </summary>
    /// <param name="penalty">An optional half-quadratic penalty function.</param>
    /// <returns>A GMM estimator whose weighting matrix is initialized to identity.</returns>
    private static GeneralizedMethodOfMoments CreateEstimator(PenaltyFunction? penalty)
    {
        MomentConditionFunction moments = parameters =>
        {
            var mean = new Vector([parameters[0] * parameters[0] - 2d]);
            return (mean, Matrix.Identity(1));
        };
        JacobianFunction jacobian = parameters => new double[,] { { 2d * parameters[0] } };

        return new GeneralizedMethodOfMoments(
            moments,
            1,
            1,
            20,
            [1d],
            [-10d],
            [10d],
            Matrix.Identity(1),
            jacobian,
            penalty);
    }

    /// <summary>
    /// Computes an independent central-difference derivative of the GMM objective.
    /// </summary>
    /// <param name="objective">The scalar objective to differentiate.</param>
    /// <param name="parameter">The scalar evaluation point.</param>
    /// <returns>The central-difference derivative using a fixed step of <c>1E-6</c>.</returns>
    private static double CentralDifference(Func<double[], double> objective, double parameter)
    {
        const double step = 1E-6d;
        double upper = objective([parameter + step]);
        double lower = objective([parameter - step]);
        return (upper - lower) / (2d * step);
    }
}
