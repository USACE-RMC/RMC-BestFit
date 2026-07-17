using Numerics.Functions;
using RMC.BestFit.Models.LinkFunctions;

namespace RMC.BestFit.Tests.LinkFunctions;

/// <summary>
/// Unit tests for the <c>CenteredLink</c> class.
/// Verifies affine centering/scaling of inner link functions, round-trip consistency,
/// derivative chain rule correctness, and constructor validation.
/// </summary>
[TestClass]
public class CenteredLinkTests
{
    /// <summary>
    /// Finite-difference step size for derivative verification.
    /// </summary>
    private const double DeltaH = 1e-7;

    /// <summary>
    /// Tolerance for round-trip identity tests.
    /// </summary>
    private const double RoundTripTol = 1e-10;

    /// <summary>
    /// Tolerance for derivative (finite-difference vs analytic) tests.
    /// </summary>
    private const double DerivativeTol = 1e-4;

    #region Constructor Tests

    /// <summary>Verifies that constructor stores properties.</summary>
    [TestMethod]
    public void Test_Constructor_StoresProperties()
    {
        var inner = new IdentityLink();
        var link = new CenteredLink(inner, mu0: 100.0, scale: 5.0);
        Assert.AreSame(inner, link.Inner);
        Assert.AreEqual(100.0, link.Mu0);
        Assert.AreEqual(5.0, link.Scale);
    }

    /// <summary>Verifies that constructor throws when null inner.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullInner_Throws()
    {
        new CenteredLink(null!, mu0: 0.0);
    }

    /// <summary>Verifies that constructor negative scale clamped to minimum.</summary>
    [TestMethod]
    public void Test_Constructor_NegativeScale_ClampedToMinimum()
    {
        var link = new CenteredLink(new IdentityLink(), mu0: 0.0, scale: -5.0);
        Assert.IsTrue(link.Scale > 0, "Scale should be clamped to positive");
    }

    /// <summary>Verifies that constructor zero scale clamped to minimum.</summary>
    [TestMethod]
    public void Test_Constructor_ZeroScale_ClampedToMinimum()
    {
        var link = new CenteredLink(new IdentityLink(), mu0: 0.0, scale: 0.0);
        Assert.IsTrue(link.Scale > 0, "Scale should be clamped to positive");
    }

    /// <summary>Verifies that constructor is one when default scale.</summary>
    [TestMethod]
    public void Test_Constructor_DefaultScale_IsOne()
    {
        var link = new CenteredLink(new IdentityLink(), mu0: 50.0);
        Assert.AreEqual(1.0, link.Scale);
    }

    #endregion

    #region Round-Trip Tests with IdentityLink

    /// <summary>Verifies that round trip identity inner.</summary>
    [TestMethod]
    public void Test_RoundTrip_IdentityInner()
    {
        // With identity inner: Link(x) = (x - mu0)/s, InverseLink(eta) = mu0 + s*eta
        var link = new CenteredLink(new IdentityLink(), mu0: 100.0, scale: 20.0);
        double[] values = { 50.0, 80.0, 100.0, 120.0, 200.0 };
        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, RoundTripTol,
                $"Round-trip failed for x={x}");
        }
    }

    /// <summary>Verifies that link identity inner known values.</summary>
    [TestMethod]
    public void Test_Link_IdentityInner_KnownValues()
    {
        var link = new CenteredLink(new IdentityLink(), mu0: 100.0, scale: 20.0);
        // Link(100) = (100-100)/20 = 0
        Assert.AreEqual(0.0, link.Link(100.0), 1e-12);
        // Link(120) = (120-100)/20 = 1
        Assert.AreEqual(1.0, link.Link(120.0), 1e-12);
        // Link(60) = (60-100)/20 = -2
        Assert.AreEqual(-2.0, link.Link(60.0), 1e-12);
    }

    /// <summary>Verifies that inverse link identity inner known values.</summary>
    [TestMethod]
    public void Test_InverseLink_IdentityInner_KnownValues()
    {
        var link = new CenteredLink(new IdentityLink(), mu0: 100.0, scale: 20.0);
        // InverseLink(0) = 100 + 20*0 = 100
        Assert.AreEqual(100.0, link.InverseLink(0.0), 1e-12);
        // InverseLink(1) = 100 + 20*1 = 120
        Assert.AreEqual(120.0, link.InverseLink(1.0), 1e-12);
        // InverseLink(-2) = 100 + 20*(-2) = 60
        Assert.AreEqual(60.0, link.InverseLink(-2.0), 1e-12);
    }

    #endregion

    #region Round-Trip Tests with SESLink

    /// <summary>Verifies that round trip SES inner.</summary>
    [TestMethod]
    public void Test_RoundTrip_SESInner()
    {
        var inner = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.3 };
        var link = new CenteredLink(inner, mu0: 500.0, scale: 50.0);
        double[] values = { 300.0, 450.0, 500.0, 550.0, 700.0 };
        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, Math.Max(RoundTripTol, Math.Abs(x) * 1e-8),
                $"Round-trip failed for x={x} with SES inner");
        }
    }

    /// <summary>Verifies that round trip log link inner.</summary>
    [TestMethod]
    public void Test_RoundTrip_LogLinkInner()
    {
        // Inner = LogLink, so domain of z must be positive
        // z = (x - mu0)/scale > 0 => x > mu0 when scale > 0
        var link = new CenteredLink(new LogLink(), mu0: 0.0, scale: 1.0);
        double[] values = { 0.1, 1.0, 5.0, 100.0 };
        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, Math.Max(RoundTripTol, x * 1e-10),
                $"Round-trip failed for x={x} with Log inner");
        }
    }

    #endregion

    #region Derivative Tests

    /// <summary>Verifies that d link identity inner known value.</summary>
    [TestMethod]
    public void Test_DLink_IdentityInner_KnownValue()
    {
        // dh/dx = Inner.DLink(z)/s. For identity inner, DLink=1 always, so dh/dx = 1/s
        var link = new CenteredLink(new IdentityLink(), mu0: 100.0, scale: 20.0);
        Assert.AreEqual(1.0 / 20.0, link.DLink(100.0), 1e-12);
        Assert.AreEqual(1.0 / 20.0, link.DLink(200.0), 1e-12);
    }

    /// <summary>Verifies that d link finite difference identity inner.</summary>
    [TestMethod]
    public void Test_DLink_FiniteDifference_IdentityInner()
    {
        var link = new CenteredLink(new IdentityLink(), mu0: 100.0, scale: 20.0);
        double[] testPoints = { 50.0, 100.0, 150.0 };
        foreach (double x in testPoints)
        {
            double finiteDiff = (link.Link(x + DeltaH) - link.Link(x - DeltaH)) / (2 * DeltaH);
            Assert.AreEqual(finiteDiff, link.DLink(x), DerivativeTol,
                $"Derivative mismatch at x={x}");
        }
    }

    /// <summary>Verifies that d link finite difference SES inner.</summary>
    [TestMethod]
    public void Test_DLink_FiniteDifference_SESInner()
    {
        var inner = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.0 };
        var link = new CenteredLink(inner, mu0: 50.0, scale: 10.0);
        double[] testPoints = { 20.0, 50.0, 80.0 };
        foreach (double x in testPoints)
        {
            double finiteDiff = (link.Link(x + DeltaH) - link.Link(x - DeltaH)) / (2 * DeltaH);
            Assert.AreEqual(finiteDiff, link.DLink(x), DerivativeTol,
                $"Derivative mismatch at x={x} with SES inner");
        }
    }

    /// <summary>Verifies that d link chain rule log inner.</summary>
    [TestMethod]
    public void Test_DLink_ChainRule_LogInner()
    {
        // For LogLink inner: DLink(z) = 1/z
        // CenteredLink DLink(x) = Inner.DLink(z)/Scale = (1/z)/Scale = 1/(z*Scale)
        // where z = (x - mu0)/Scale
        var link = new CenteredLink(new LogLink(), mu0: 0.0, scale: 2.0);
        double x = 6.0;
        double z = (x - 0.0) / 2.0; // z = 3
        double expected = (1.0 / z) / 2.0; // = 1/(3*2) = 1/6
        Assert.AreEqual(expected, link.DLink(x), 1e-12);
    }

    /// <summary>Verifies that d link always positive.</summary>
    [TestMethod]
    public void Test_DLink_AlwaysPositive()
    {
        var inner = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.0 };
        var link = new CenteredLink(inner, mu0: 50.0, scale: 10.0);
        double[] testPoints = { 0.0, 25.0, 50.0, 75.0, 100.0 };
        foreach (double x in testPoints)
        {
            Assert.IsTrue(link.DLink(x) > 0,
                $"Derivative should be positive at x={x}");
        }
    }

    #endregion

    #region Centering Behavior

    /// <summary>Verifies that link at mu0 maps to inner link of zero.</summary>
    [TestMethod]
    public void Test_Link_AtMu0_MapsToInnerLinkOfZero()
    {
        var inner = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.0 };
        var link = new CenteredLink(inner, mu0: 200.0, scale: 30.0);
        // At x=mu0: z = (200-200)/30 = 0, so Link(200) = inner.Link(0)
        double expected = inner.Link(0.0);
        Assert.AreEqual(expected, link.Link(200.0), 1e-12);
    }

    /// <summary>Verifies that inverse link at zero maps through inner.</summary>
    [TestMethod]
    public void Test_InverseLink_AtZero_MapsThroughInner()
    {
        var inner = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.0 };
        var link = new CenteredLink(inner, mu0: 200.0, scale: 30.0);
        // InverseLink(0) = mu0 + scale * inner.InverseLink(0)
        // inner.InverseLink(0) = 0 for SESLink
        Assert.AreEqual(200.0, link.InverseLink(0.0), 1e-10);
    }

    /// <summary>Verifies that scale affects spread.</summary>
    [TestMethod]
    public void Test_ScaleAffectsSpread()
    {
        var inner = new IdentityLink();
        var linkNarrow = new CenteredLink(inner, mu0: 100.0, scale: 1.0);
        var linkWide = new CenteredLink(inner, mu0: 100.0, scale: 10.0);
        // Same eta should produce more spread with larger scale
        double narrow = linkNarrow.InverseLink(2.0);
        double wide = linkWide.InverseLink(2.0);
        Assert.AreEqual(102.0, narrow, 1e-12);
        Assert.AreEqual(120.0, wide, 1e-12);
    }

    #endregion
}
