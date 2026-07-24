using Numerics.Distributions;

namespace RMC.BestFit.Verification.DistributionFitting;

/// <summary>
/// Verifies the analytic zero-primary-shape limits of the Kappa Four distribution.
/// </summary>
/// <remarks>
/// These tests isolate TR-001. They compare the Numerics implementation with the
/// derivative and inverse of its documented zero-shape CDF, without using the
/// implementation's PDF or inverse CDF as an oracle.
/// </remarks>
[TestClass]
public class KappaFourZeroShapeVerificationTests
{
    /// <summary>
    /// Verifies that the zero-primary-shape density is the analytical derivative of the CDF.
    /// </summary>
    [TestMethod]
    public void ZeroShapeNonzeroHondo_PdfMatchesAnalyticalDerivative()
    {
        const double xi = 0.0;
        const double alpha = 1.0;
        const double kappa = 0.0;
        const double hondo = 0.2;
        const double x = 1.0;

        var distribution = new KappaFour(xi, alpha, kappa, hondo);
        double z = (x - xi) / alpha;
        double cdfBase = 1.0 - hondo * Math.Exp(-z);
        double expectedDensity = Math.Exp(-z) / alpha * Math.Pow(cdfBase, 1.0 / hondo - 1.0);

        Assert.AreEqual(
            expectedDensity,
            distribution.PDF(x),
            1e-10,
            "The zero-shape PDF must equal the analytical derivative of the implemented CDF.");
    }

    /// <summary>
    /// Verifies that the analytical zero-primary-shape quantile inverts the CDF.
    /// </summary>
    [TestMethod]
    public void ZeroShapeNonzeroHondo_QuantileInvertsCdf()
    {
        const double xi = 0.0;
        const double alpha = 1.0;
        const double kappa = 0.0;
        const double hondo = 0.2;
        const double x = 1.0;

        var distribution = new KappaFour(xi, alpha, kappa, hondo);
        double probability = distribution.CDF(x);
        double expectedQuantile = xi - alpha * Math.Log((1.0 - Math.Pow(probability, hondo)) / hondo);

        Assert.AreEqual(
            expectedQuantile,
            distribution.InverseCDF(probability),
            1e-10,
            "The zero-shape inverse CDF must implement the algebraic inverse of the CDF.");
        Assert.AreEqual(x, expectedQuantile, 1e-10, "The independent analytical inverse must recover x.");
    }
}
