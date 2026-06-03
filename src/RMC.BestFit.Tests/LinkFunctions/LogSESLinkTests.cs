using RMC.BestFit.Models.LinkFunctions;

namespace RMC.BestFit.Tests.LinkFunctions;

/// <summary>
/// Unit tests for the <see cref="LogSESLink"/> class.
/// Verifies round-trip consistency, derivative correctness, and log-space scale behavior.
/// </summary>
[TestClass]
public class LogSESLinkTests
{
    /// <summary>
    /// Finite-difference step size for derivative verification.
    /// </summary>
    private const double DeltaH = 1e-7;

    /// <summary>
    /// Tolerance for round-trip identity tests.
    /// </summary>
    private const double RoundTripTol = 1e-8;

    /// <summary>
    /// Tolerance for derivative (finite-difference vs analytic) tests.
    /// </summary>
    private const double DerivativeTol = 1e-4;

    #region Constructor Tests

    /// <summary>Verifies that constructor default sets default values.</summary>
    [TestMethod]
    public void Test_Constructor_Default_SetsDefaultValues()
    {
        var link = new LogSESLink();
        Assert.AreEqual(1.0, link.Sigma0);
        Assert.AreEqual(1.0, link.A);
        Assert.AreEqual(0.2, link.Lambda);
        Assert.AreEqual(20, link.MaxIterations);
        Assert.AreEqual(1e-12, link.Tolerance);
    }

    /// <summary>Verifies that constructor custom sets values.</summary>
    [TestMethod]
    public void Test_Constructor_Custom_SetsValues()
    {
        var link = new LogSESLink(sigma0: 10.0, a: 2.0, lambda: 0.5);
        Assert.AreEqual(10.0, link.Sigma0);
        Assert.AreEqual(2.0, link.A);
        Assert.AreEqual(0.5, link.Lambda);
    }

    /// <summary>Verifies that constructor clamps sigma0.</summary>
    [TestMethod]
    public void Test_Constructor_ClampsSigma0()
    {
        // Negative sigma0 gets clamped to 1e-12
        var link = new LogSESLink(sigma0: -5.0);
        Assert.IsTrue(link.Sigma0 > 0);
    }

    /// <summary>Verifies that constructor clamps lambda.</summary>
    [TestMethod]
    public void Test_Constructor_ClampsLambda()
    {
        // Lambda >= 1 should be clamped
        var link = new LogSESLink(sigma0: 1.0, a: 1.0, lambda: 2.0);
        Assert.IsTrue(link.Lambda < 1.0);
    }

    #endregion

    #region Round-Trip Tests

    /// <summary>Verifies that round trip default params.</summary>
    [TestMethod]
    public void Test_RoundTrip_DefaultParams()
    {
        var link = new LogSESLink(sigma0: 10.0);
        double[] sigmas = { 0.1, 1.0, 5.0, 10.0, 20.0, 100.0 };
        foreach (double sigma in sigmas)
        {
            double eta = link.Link(sigma);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(sigma, recovered, Math.Max(RoundTripTol, sigma * 1e-8),
                $"Round-trip failed for sigma={sigma}");
        }
    }

    /// <summary>Verifies that round trip various sigma0.</summary>
    [TestMethod]
    public void Test_RoundTrip_VariousSigma0()
    {
        double[] sigma0Values = { 0.5, 1.0, 10.0, 100.0 };
        foreach (double sigma0 in sigma0Values)
        {
            var link = new LogSESLink(sigma0: sigma0, a: 1.0, lambda: 0.2);
            double[] testSigmas = { sigma0 * 0.1, sigma0 * 0.5, sigma0, sigma0 * 2.0, sigma0 * 10.0 };
            foreach (double sigma in testSigmas)
            {
                double eta = link.Link(sigma);
                double recovered = link.InverseLink(eta);
                Assert.AreEqual(sigma, recovered, Math.Max(RoundTripTol, sigma * 1e-8),
                    $"Round-trip failed for sigma={sigma}, sigma0={sigma0}");
            }
        }
    }

    /// <summary>Verifies that round trip symmetric lambda.</summary>
    [TestMethod]
    public void Test_RoundTrip_SymmetricLambda()
    {
        var link = new LogSESLink(sigma0: 5.0, a: 1.0, lambda: 0.0);
        double[] sigmas = { 0.5, 1.0, 5.0, 25.0 };
        foreach (double sigma in sigmas)
        {
            double eta = link.Link(sigma);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(sigma, recovered, Math.Max(RoundTripTol, sigma * 1e-8),
                $"Round-trip failed for sigma={sigma}, lambda=0");
        }
    }

    #endregion

    #region InverseLink Tests

    /// <summary>Verifies that inverse link returns sigma0 when at zero.</summary>
    [TestMethod]
    public void Test_InverseLink_AtZero_ReturnsSigma0()
    {
        // When eta=0: r(0)=0, so sigma = sigma0 * exp(0) = sigma0
        var link = new LogSESLink(sigma0: 7.5);
        Assert.AreEqual(7.5, link.InverseLink(0.0), 1e-10);
    }

    /// <summary>Verifies that inverse link always positive.</summary>
    [TestMethod]
    public void Test_InverseLink_AlwaysPositive()
    {
        var link = new LogSESLink(sigma0: 5.0);
        // Keep eta in a moderate range to avoid exp() underflow/overflow
        double[] etas = { -5.0, -2.0, -1.0, 0.0, 1.0, 2.0, 5.0 };
        foreach (double eta in etas)
        {
            double sigma = link.InverseLink(eta);
            Assert.IsTrue(sigma > 0, $"InverseLink should always be positive, got {sigma} at eta={eta}");
        }
    }

    /// <summary>Verifies that inverse link monotone.</summary>
    [TestMethod]
    public void Test_InverseLink_Monotone()
    {
        var link = new LogSESLink(sigma0: 5.0);
        // Keep eta in a moderate range to avoid overflow with asymmetric lambda
        double prev = link.InverseLink(-5.0);
        for (double eta = -4.5; eta <= 5.0; eta += 0.5)
        {
            double current = link.InverseLink(eta);
            Assert.IsTrue(current > prev, $"Monotonicity violated at eta={eta}");
            prev = current;
        }
    }

    #endregion

    #region Derivative Tests

    /// <summary>Verifies that d link finite difference.</summary>
    [TestMethod]
    public void Test_DLink_FiniteDifference()
    {
        var link = new LogSESLink(sigma0: 10.0, a: 1.0, lambda: 0.2);
        double[] testSigmas = { 1.0, 5.0, 10.0, 20.0, 50.0 };
        foreach (double sigma in testSigmas)
        {
            double finiteDiff = (link.Link(sigma + DeltaH) - link.Link(sigma - DeltaH)) / (2 * DeltaH);
            Assert.AreEqual(finiteDiff, link.DLink(sigma), DerivativeTol,
                $"Derivative mismatch at sigma={sigma}");
        }
    }

    /// <summary>Verifies that d link always positive.</summary>
    [TestMethod]
    public void Test_DLink_AlwaysPositive()
    {
        var link = new LogSESLink(sigma0: 5.0);
        double[] testSigmas = { 0.01, 0.5, 1.0, 5.0, 25.0, 100.0 };
        foreach (double sigma in testSigmas)
        {
            Assert.IsTrue(link.DLink(sigma) > 0,
                $"Derivative should be positive at sigma={sigma}");
        }
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that link very small sigma.</summary>
    [TestMethod]
    public void Test_Link_VerySmallSigma()
    {
        var link = new LogSESLink(sigma0: 10.0);
        double eta = link.Link(1e-10);
        double recovered = link.InverseLink(eta);
        // Should recover something very small and positive
        Assert.IsTrue(recovered > 0);
        Assert.IsTrue(double.IsFinite(recovered));
    }

    /// <summary>Verifies that link very large sigma.</summary>
    [TestMethod]
    public void Test_Link_VeryLargeSigma()
    {
        var link = new LogSESLink(sigma0: 10.0);
        double eta = link.Link(1e6);
        double recovered = link.InverseLink(eta);
        Assert.AreEqual(1e6, recovered, 1e6 * 1e-6);
        Assert.IsTrue(double.IsFinite(eta));
    }

    #endregion
}
