using RMC.BestFit.Models.LinkFunctions;

namespace RMC.BestFit.Tests.LinkFunctions;

/// <summary>
/// Unit tests for the <see cref="LogASinHLink"/> class.
/// </summary>
/// <remarks>
/// These tests focus on the support and local-linearization properties needed for linked-MVN
/// uncertainty propagation: positive support, round-trip consistency, monotonicity, derivative
/// correctness, controlled asymmetry, controlled tail thickness, and serialization.
/// </remarks>
[TestClass]
public class LogASinHLinkTests
{
    /// <summary>
    /// Tolerance for round-trip identity tests.
    /// </summary>
    private const double RoundTripTol = 1e-10;

    /// <summary>
    /// Tolerance for derivative tests.
    /// </summary>
    private const double DerivativeTol = 1e-4;

    #region Constructor Tests

    /// <summary>
    /// Verifies the default constructor sets conservative symmetric defaults.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_SetsDefaultValues()
    {
        var link = new LogASinHLink();

        Assert.AreEqual(1.0, link.Sigma0);
        Assert.AreEqual(1.0, link.LogScale);
        Assert.AreEqual(0.0, link.Epsilon);
        Assert.AreEqual(1.0, link.Delta);
        Assert.IsFalse(link.UseAdaptiveEpsilon);
        Assert.AreEqual(0.0, link.ParentIndicator);
        Assert.AreEqual(0.5, link.EpsilonMax);
        Assert.AreEqual(1.0, link.EpsilonSlope);
        Assert.AreEqual(1e-12, link.Eps);
    }

    /// <summary>
    /// Verifies the parameterized constructor stores positive values and fixed asymmetry settings.
    /// </summary>
    [TestMethod]
    public void Constructor_Custom_SetsValues()
    {
        var link = new LogASinHLink(sigma0: 10.0, logScale: 0.25, epsilon: 0.3, delta: 0.9);

        Assert.AreEqual(10.0, link.Sigma0);
        Assert.AreEqual(0.25, link.LogScale);
        Assert.AreEqual(0.3, link.Epsilon);
        Assert.AreEqual(0.9, link.Delta);
    }

    /// <summary>
    /// Verifies invalid positive-support settings are floored instead of producing invalid links.
    /// </summary>
    [TestMethod]
    public void Constructor_InvalidPositiveSettings_AreFloored()
    {
        var link = new LogASinHLink(sigma0: -10.0, logScale: -0.25, epsilon: 0.0, delta: -1.0);

        Assert.IsTrue(link.Sigma0 > 0.0);
        Assert.IsTrue(link.LogScale > 0.0);
        Assert.IsTrue(link.Delta > 0.0);
    }

    #endregion

    #region Centering And Round-Trip Tests

    /// <summary>
    /// Verifies the symmetric baseline is the centered log link.
    /// </summary>
    [TestMethod]
    public void SymmetricBaseline_ReducesToCenteredLogLink()
    {
        var link = new LogASinHLink(sigma0: 10.0, logScale: 0.25);

        double sigma = 15.0;
        double expectedEta = Math.Log(sigma / 10.0) / 0.25;

        Assert.AreEqual(0.0, link.Link(10.0), 1e-15);
        Assert.AreEqual(10.0, link.InverseLink(0.0), 1e-12);
        Assert.AreEqual(expectedEta, link.Link(sigma), 1e-12);
    }

    /// <summary>
    /// Verifies inverse and forward transforms round-trip for practical scale values.
    /// </summary>
    [TestMethod]
    public void RoundTrip_PracticalScaleValues()
    {
        var link = new LogASinHLink(sigma0: 10.0, logScale: 0.30, epsilon: 0.25, delta: 0.85);
        double[] sigmas = { 0.1, 1.0, 5.0, 10.0, 20.0, 100.0 };

        foreach (double sigma in sigmas)
        {
            double eta = link.Link(sigma);
            double recovered = link.InverseLink(eta);

            Assert.AreEqual(sigma, recovered, Math.Max(RoundTripTol, sigma * 1e-10),
                $"Round-trip failed for sigma={sigma}.");
        }
    }

    /// <summary>
    /// Verifies inverse-link draws stay positive across a broad eta range.
    /// </summary>
    [TestMethod]
    public void InverseLink_AlwaysPositive()
    {
        var link = new LogASinHLink(sigma0: 10.0, logScale: 0.25, epsilon: 0.4, delta: 0.9);

        for (double eta = -6.0; eta <= 6.0; eta += 0.5)
        {
            double sigma = link.InverseLink(eta);
            Assert.IsTrue(sigma > 0.0, $"Expected positive sigma for eta={eta}.");
            Assert.IsTrue(double.IsFinite(sigma), $"Expected finite sigma for eta={eta}.");
        }
    }

    /// <summary>
    /// Verifies inverse-link monotonicity.
    /// </summary>
    [TestMethod]
    public void InverseLink_IsMonotoneIncreasing()
    {
        var link = new LogASinHLink(sigma0: 10.0, logScale: 0.25, epsilon: 0.4, delta: 0.9);
        double previous = link.InverseLink(-6.0);

        for (double eta = -5.5; eta <= 6.0; eta += 0.5)
        {
            double current = link.InverseLink(eta);
            Assert.IsTrue(current > previous, $"Monotonicity failed at eta={eta}.");
            previous = current;
        }
    }

    #endregion

    #region Shape Tests

    /// <summary>
    /// Verifies positive epsilon inflates the upper scale tail relative to the lower tail.
    /// </summary>
    [TestMethod]
    public void PositiveEpsilon_InflatesUpperScaleTail()
    {
        var link = new LogASinHLink(sigma0: 10.0, logScale: 0.20, epsilon: 0.4);
        double etaCenter = link.Link(10.0);

        double upperFactor = link.InverseLink(etaCenter + 2.0) / 10.0;
        double lowerFactor = 10.0 / link.InverseLink(etaCenter - 2.0);

        Assert.IsTrue(upperFactor > lowerFactor,
            "Positive epsilon should make the upper multiplicative scale tail heavier than the lower tail.");
    }

    /// <summary>
    /// Verifies delta below one thickens both multiplicative tails symmetrically when epsilon is zero.
    /// </summary>
    [TestMethod]
    public void DeltaBelowOne_ThickensSymmetricMultiplicativeTails()
    {
        var baseline = new LogASinHLink(sigma0: 10.0, logScale: 0.20, epsilon: 0.0, delta: 1.0);
        var heavyTail = new LogASinHLink(sigma0: 10.0, logScale: 0.20, epsilon: 0.0, delta: 0.75);

        Assert.IsTrue(heavyTail.InverseLink(2.0) > baseline.InverseLink(2.0),
            "Delta below one should expand the upper scale tail.");
        Assert.IsTrue(heavyTail.InverseLink(-2.0) < baseline.InverseLink(-2.0),
            "Delta below one should expand the lower multiplicative scale tail.");
    }

    /// <summary>
    /// Verifies adaptive epsilon uses the parent indicator to produce right-tail inflation.
    /// </summary>
    [TestMethod]
    public void AdaptiveEpsilon_UsesParentIndicator()
    {
        var weak = new LogASinHLink(sigma0: 10.0, logScale: 0.20)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = 0.1,
            EpsilonMax = 0.7,
            EpsilonSlope = 1.2
        };
        var strong = new LogASinHLink(sigma0: 10.0, logScale: 0.20)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = 0.9,
            EpsilonMax = 0.7,
            EpsilonSlope = 1.2
        };

        double weakEtaCenter = weak.Link(10.0);
        double strongEtaCenter = strong.Link(10.0);
        double weakUpper = weak.InverseLink(weakEtaCenter + 2.0);
        double strongUpper = strong.InverseLink(strongEtaCenter + 2.0);

        Assert.IsTrue(strongUpper > weakUpper,
            "Larger parent uncertainty should strengthen adaptive upper-tail inflation.");
    }

    #endregion

    #region Derivative Tests

    /// <summary>
    /// Verifies the analytical derivative against a central finite difference.
    /// </summary>
    [TestMethod]
    public void DLink_MatchesFiniteDifference()
    {
        var link = new LogASinHLink(sigma0: 10.0, logScale: 0.25, epsilon: 0.2, delta: 0.9);
        double[] sigmas = { 1.0, 5.0, 10.0, 20.0, 50.0 };

        foreach (double sigma in sigmas)
        {
            double h = Math.Max(1e-6, sigma * 1e-6);
            double finiteDifference = (link.Link(sigma + h) - link.Link(sigma - h)) / (2.0 * h);

            Assert.AreEqual(finiteDifference, link.DLink(sigma), DerivativeTol,
                $"Derivative mismatch at sigma={sigma}.");
            Assert.IsTrue(link.DLink(sigma) > 0.0, $"Derivative should be positive at sigma={sigma}.");
        }
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Verifies XElement serialization preserves all configurable properties.
    /// </summary>
    [TestMethod]
    public void Serialization_RoundTrip_PreservesProperties()
    {
        var original = new LogASinHLink(sigma0: 10.0, logScale: 0.25, epsilon: 0.2, delta: 0.85)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = 0.6,
            EpsilonMax = 0.7,
            EpsilonSlope = 1.4,
            Eps = 1e-10
        };

        var restored = new LogASinHLink(original.ToXElement());

        Assert.AreEqual(original.Sigma0, restored.Sigma0, 1e-15);
        Assert.AreEqual(original.LogScale, restored.LogScale, 1e-15);
        Assert.AreEqual(original.Epsilon, restored.Epsilon, 1e-15);
        Assert.AreEqual(original.Delta, restored.Delta, 1e-15);
        Assert.AreEqual(original.UseAdaptiveEpsilon, restored.UseAdaptiveEpsilon);
        Assert.AreEqual(original.ParentIndicator, restored.ParentIndicator, 1e-15);
        Assert.AreEqual(original.EpsilonMax, restored.EpsilonMax, 1e-15);
        Assert.AreEqual(original.EpsilonSlope, restored.EpsilonSlope, 1e-15);
        Assert.AreEqual(original.Eps, restored.Eps, 1e-15);
        Assert.AreEqual(original.Link(12.0), restored.Link(12.0), 1e-12);
        Assert.AreEqual(original.InverseLink(1.0), restored.InverseLink(1.0), 1e-12);
    }

    /// <summary>
    /// Verifies the BestFit factory can deserialize the link.
    /// </summary>
    [TestMethod]
    public void BestFitFactory_CreatesLogASinHLink()
    {
        var original = new LogASinHLink(sigma0: 10.0, logScale: 0.25, epsilon: 0.2, delta: 0.85);

        var restored = BestFitLinkFunctionFactory.CreateFromXElement(original.ToXElement());

        Assert.IsInstanceOfType(restored, typeof(LogASinHLink));
    }

    #endregion
}
