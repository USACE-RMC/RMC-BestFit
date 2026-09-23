using Numerics.Data.Statistics;
using Numerics.Mathematics.Optimization;
using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.DistributionFitting;

/// <summary>
/// Verifies base-10 Log-Normal fitting and probability calculations against closed-form results.
/// </summary>
/// <remarks>
/// The oracle is derived in base-10 log space and includes the original-measure Jacobian
/// <c>1 / (y ln(10))</c>. It does not call a second optimizer or reuse fitted parameters as truth.
/// </remarks>
[TestClass]
public class Log10NormalFittingVerificationTests
{
    /// <summary>Central-Normal multiplier for covariance-based coordinate and response recovery.</summary>
    private const double Central95NormalMultiplier = 1.959963984540054d;

    /// <summary>Joint 95% chi-square cutoff for the two fitted Log10-Normal coordinates.</summary>
    private const double Joint95LikelihoodRatioCutoff = 5.991464547107979d;

    /// <summary>
    /// Verifies closed-form MLE parameters, data log likelihood, CDF, and quantile calculations.
    /// </summary>
    [TestMethod]
    public void ClosedFormMle_LikelihoodCdfAndQuantileMatchAnalyticalOracle()
    {
        double[] log10Values = [1.1d, 1.4d, 1.7d, 2.0d, 2.3d, 2.6d, 2.9d];
        double[] observedValues = log10Values.Select(value => Math.Pow(10d, value)).ToArray();
        const double expectedMu = 2.0d;
        const double expectedSigma = 0.6d;
        const double standardNormalQuantile90 = 1.2815515655446004d;
        double sumSquaredLogResiduals = log10Values.Sum(value => Math.Pow(value - expectedMu, 2d));
        double jacobianLogSum = observedValues.Sum(value => Math.Log(value * Math.Log(10d)));
        double expectedDataLogLikelihood =
            -log10Values.Length * Math.Log(expectedSigma * Math.Sqrt(2d * Math.PI))
            -sumSquaredLogResiduals / (2d * expectedSigma * expectedSigma)
            -jacobianLogSum;

        var dataFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(observedValues)
        };
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.LogNormal);
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution)
        {
            ComputeHessian = false,
            ReportFailure = true
        };

        bool estimated = mle.Estimate();

        Assert.IsTrue(estimated, "The deterministic Log10-Normal MLE must converge.");
        double muStandardError = expectedSigma / Math.Sqrt(log10Values.Length);
        double sigmaStandardError = expectedSigma / Math.Sqrt(2d * log10Values.Length);
        Assert.AreEqual(
            expectedMu,
            mle.BestParameterSet.Values[0],
            Central95NormalMultiplier * muStandardError,
            "Log10-Normal mu must lie in its analytical central-95% MLE covariance interval.");
        Assert.AreEqual(
            expectedSigma,
            mle.BestParameterSet.Values[1],
            Central95NormalMultiplier * sigmaStandardError,
            "Log10-Normal sigma must lie in its analytical central-95% MLE covariance interval.");
        Assert.AreEqual(expectedDataLogLikelihood, model.DataLogLikelihood([expectedMu, expectedSigma]), 1E-10d);
        Assert.AreEqual(
            expectedDataLogLikelihood,
            model.PointwiseDataLogLikelihood([expectedMu, expectedSigma]).Sum(),
            1E-10d);
        double likelihoodRatioStatistic =
            2d * Math.Abs(expectedDataLogLikelihood - mle.MaximumLogLikelihood);
        Assert.IsTrue(
            likelihoodRatioStatistic <= Joint95LikelihoodRatioCutoff,
            $"Log10-Normal 2*|delta log L|={likelihoodRatioStatistic:G17} exceeds the " +
            $"joint 95% chi-square(2) cutoff {Joint95LikelihoodRatioCutoff:G17}.");

        double expectedQuantile90 = Math.Pow(10d, expectedMu + expectedSigma * standardNormalQuantile90);
        var analyticalDistribution = new LogNormal(expectedMu, expectedSigma);
        Assert.AreEqual(0.5d, analyticalDistribution.CDF(Math.Pow(10d, expectedMu)), 1E-12d);
        Assert.AreEqual(expectedQuantile90, analyticalDistribution.InverseCDF(0.9d), 1E-9d);

        var fittedDistribution = new LogNormal(
            mle.BestParameterSet.Values[0],
            mle.BestParameterSet.Values[1]);
        double log10QuantileStandardError = expectedSigma / Math.Sqrt(log10Values.Length) *
            Math.Sqrt(1d + standardNormalQuantile90 * standardNormalQuantile90 / 2d);
        double quantileStandardError =
            Math.Log(10d) * expectedQuantile90 * log10QuantileStandardError;
        Assert.AreEqual(
            expectedQuantile90,
            fittedDistribution.InverseCDF(0.9d),
            Central95NormalMultiplier * quantileStandardError,
            "The fitted 0.9 quantile must lie in its covariance-based central-95% delta-method interval.");
    }
}
