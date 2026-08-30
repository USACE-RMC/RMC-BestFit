using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>Verifies generated-parent GMM recovery independently of the retained R specification and gradient oracles.</summary>
/// <remarks>
/// The experiment contains 1,000 seeded response/covariate rows, parent theta=2, and two moment
/// conditions E[x-theta]=0 and E[z(x-theta)]=0. Its covariance source is the post-estimate
/// two-step sandwich standard error; the optional 5% point rule is not applicable.
/// </remarks>
[TestClass]
public sealed class GmmGeneratedParentRecoveryTests
{
    /// <summary>Verifies N=1000 two-step GMM parent recovery with the predeclared sandwich standard error.</summary>
    /// <remarks>Sample unit: response/covariate row; N=1000; seed=24680; parent theta=2; fitted coordinate=theta; covariance source=two-step sandwich.</remarks>
    [TestMethod]
    public void TwoStepGmm_RecoversGeneratedParent_WithinStandardizedError()
    {
        _ = RecoveryDesign.ResponseCovariateRows("Independent response/instrument rows.");
        const double parent = 2d;
        (double[] x, double[] z) = CreateData(parent);
        GeneralizedMethodOfMoments estimator = CreateEstimator(x, z);

        Assert.IsTrue(estimator.Estimate(), "The predeclared two-step GMM recovery fit failed.");
        estimator.PostProcess(useSandwich: true, computeJstat: true);
        RecoveryAcceptance.AssertFrequentistStandardizedError("theta", estimator.BestParameterSet.Values[0], parent, estimator.GetStandardErrors()[0]);
    }

    /// <summary>Creates the seeded response and instrument rows.</summary>
    /// <param name="parent">Generating location parameter.</param>
    /// <returns>Response/instrument arrays of exactly 1,000 paired rows.</returns>
    private static (double[] X, double[] Z) CreateData(double parent)
    {
        var random = new Random(24680);
        var x = new double[RecoveryDesign.SampleSize];
        var z = new double[RecoveryDesign.SampleSize];
        for (int index = 0; index < x.Length; index++)
        {
            z[index] = -1d + (2d * random.NextDouble());
            double radius = Math.Sqrt(-2d * Math.Log(random.NextDouble()));
            x[index] = parent + (radius * Math.Cos(2d * Math.PI * random.NextDouble()));
        }
        return (x, z);
    }

    /// <summary>Creates the predeclared two-moment two-step estimator.</summary>
    /// <param name="x">Generated responses.</param>
    /// <param name="z">Generated instrument values.</param>
    /// <returns>The configured estimator.</returns>
    private static GeneralizedMethodOfMoments CreateEstimator(double[] x, double[] z)
    {
        MomentConditionFunction moments = parameters =>
        {
            double theta = parameters[0];
            double first = x.Average(value => value - theta);
            double second = z.Select((instrument, index) => instrument * (x[index] - theta)).Average();
            var covariance = new Matrix(2, 2);
            for (int index = 0; index < x.Length; index++)
            {
                double a = x[index] - theta - first;
                double b = z[index] * (x[index] - theta) - second;
                covariance[0, 0] += a * a; covariance[0, 1] += a * b;
                covariance[1, 0] += b * a; covariance[1, 1] += b * b;
            }
            covariance /= x.Length;
            return (new Vector([first, second]), covariance);
        };
        JacobianFunction jacobian = _ => new double[,] { { -1d }, { -z.Average() } };
        return new GeneralizedMethodOfMoments(moments, 1, 2, x.Length, [0d], [-10d], [10d], Matrix.Identity(2), jacobian)
        {
            EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep,
            OptimizerMethod = OptimizationMethod.BFGS
        };
    }
}
