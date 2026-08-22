using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Pins the scale invariance of the GMM covariance conditioning (TR-085): the moment covariance is
/// conditioned only by the symmetric positive-definite floor, so for an exactly identified real-space
/// three-parameter family the sandwich reproduces the closed-form variances of the mean and of the
/// standard deviation no matter how widely the moment-covariance eigenvalues are spread.
/// </summary>
/// <remarks>
/// Until 22 August 2026 the covariance path capped the eigenvalues of the moment covariance at fifty
/// times their median. For a real-space Pearson Type III sample the three eigenvalues scale like
/// sigma^2, sigma^4, and sigma^6, so the cap rewrote the matrix and the variance of the mean came out
/// 62% too large at n = 25 and 22% too small at n = 100. No optimizer runs here: the covariance is
/// evaluated at the closed-form product-moment estimate through <c>TryGetCovariance</c>, and the
/// contract is held to 1e-3 relative because the production Jacobian is numerical.
/// </remarks>
[TestClass]
public class GeneralizedMethodOfMomentsCovarianceScaleTests
{
    /// <summary>
    /// Builds a deterministic real-space Pearson Type III sample and its product-moment parameter estimate.
    /// </summary>
    /// <param name="n">The sample size.</param>
    /// <returns>The data frame and the product-moment parameter vector (mu, sigma, gamma).</returns>
    private static (BestFitDataFrame Frame, double[] Theta) CreatePearsonIIIFixture(int n)
    {
        double[] values = new PearsonTypeIII(100.0, 20.0, 0.5).GenerateRandomValues(n, 12345);
        var frame = new BestFitDataFrame { ExactSeries = new ExactSeries(values) };
        var dist = new PearsonTypeIII();
        double[] theta = dist.ParametersFromMoments(Statistics.ProductMoments(values));
        return (frame, theta);
    }

    /// <summary>
    /// The exactly identified sandwich equals D^-1 S D^-T / n with a lower-triangular Jacobian, so the
    /// variance of the mean is S00 / n and the variance of sigma is S11 / (4 sigma^2 n); the
    /// positive-definite moment covariance must not be reported as regularized.
    /// </summary>
    [TestMethod]
    public void TryGetCovariance_RealSpacePearsonIII_MatchesClosedFormMeanAndScaleVariances()
    {
        foreach (int n in new[] { 25, 100 })
        {
            var (frame, theta) = CreatePearsonIIIFixture(n);
            var model = new Bulletin17CDistribution(frame, UnivariateDistributionType.PearsonTypeIII);
            var gmm = new GeneralizedMethodOfMoments(model);
            Matrix s = model.MomentConditions(theta).S;

            Assert.IsTrue(gmm.TryGetCovariance(theta, sandwich: true, out Matrix covariance),
                $"n={n}: covariance unavailable: {gmm.CovarianceDiagnostic}");
            Assert.AreEqual(CovarianceComputationStatus.Available, gmm.CovarianceStatus,
                $"n={n}: a positive-definite moment covariance must not be reported as regularized.");

            double sigma = theta[1];
            double expectedMeanVariance = s[0, 0] / n;
            double expectedSigmaVariance = s[1, 1] / (4.0 * sigma * sigma * n);
            // The production Jacobian is numerical; its ~1e-9 noise in the analytically zero entries is
            // amplified by the sigma^6-scale moment covariance to roughly 1e-4 relative, far below the
            // 62% (n = 25) and 22% (n = 100) distortions the former eigenvalue cap produced.
            Assert.AreEqual(expectedMeanVariance, covariance[0, 0], 1e-3 * expectedMeanVariance,
                $"n={n}: Var(mean) must equal S00/n.");
            Assert.AreEqual(expectedSigmaVariance, covariance[1, 1], 1e-3 * expectedSigmaVariance,
                $"n={n}: Var(sigma) must equal S11/(4 sigma^2 n).");
        }
    }

    /// <summary>
    /// Guards the fixture: the eigenvalue spread that triggered the former cap is present, so the
    /// closed-form contract above is a meaningful regression.
    /// </summary>
    [TestMethod]
    public void PearsonIIIFixture_MomentCovarianceEigenvaluesSpanMoreThanFiftyTimesTheMedian()
    {
        var (frame, theta) = CreatePearsonIIIFixture(25);
        var model = new Bulletin17CDistribution(frame, UnivariateDistributionType.PearsonTypeIII);
        Matrix s = model.MomentConditions(theta).S;
        var eig = new EigenValueDecomposition(s);
        double[] eigenvalues = Enumerable.Range(0, 3).Select(i => (double)eig.EigenValues[i]).OrderBy(v => v).ToArray();

        Assert.IsTrue(eigenvalues[2] > 50.0 * eigenvalues[1],
            $"Largest eigenvalue {eigenvalues[2]:G4} must exceed fifty times the median {eigenvalues[1]:G4} for the regression to be meaningful.");
    }
}
