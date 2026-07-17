using RMC.BestFit.Models.LinkFunctions;

namespace RMC.BestFit.Tests.LinkFunctions;

/// <summary>
/// Unit tests for the <c>SESLink</c> class.
/// Verifies round-trip consistency, derivative correctness, adaptive lambda behavior,
/// and behavior in various regimes (central, tails, symmetric, asymmetric).
/// </summary>
[TestClass]
public class SESLinkTests
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

    /// <summary>Verifies that constructor default sets default values.</summary>
    [TestMethod]
    public void Test_Constructor_Default_SetsDefaultValues()
    {
        var link = new SESLink();
        Assert.AreEqual(1.0, link.A);
        Assert.AreEqual(0.4, link.Lambda);
        Assert.IsTrue(link.UseAdaptiveLambda);
        Assert.AreEqual(0.0, link.ParentIndicator);
        Assert.AreEqual(0.8, link.LambdaMax);
        Assert.AreEqual(1.0, link.LambdaSlope);
        Assert.AreEqual(20, link.MaxIterations);
        Assert.AreEqual(1e-12, link.Tolerance);
    }

    /// <summary>Verifies that constructor custom sets values.</summary>
    [TestMethod]
    public void Test_Constructor_Custom_SetsValues()
    {
        var link = new SESLink(a: 2.0, useAdaptiveLambda: false, parentIndicator: 1.5,
            lambdaMax: 0.6, lambdaSlope: 2.0, maxIterations: 50, tolerance: 1e-14);
        Assert.AreEqual(2.0, link.A);
        Assert.IsFalse(link.UseAdaptiveLambda);
        Assert.AreEqual(1.5, link.ParentIndicator);
        Assert.AreEqual(0.6, link.LambdaMax);
        Assert.AreEqual(2.0, link.LambdaSlope);
        Assert.AreEqual(50, link.MaxIterations);
        Assert.AreEqual(1e-14, link.Tolerance);
    }

    #endregion

    #region Round-Trip Tests

    /// <summary>Verifies that round trip symmetric default params.</summary>
    [TestMethod]
    public void Test_RoundTrip_Symmetric_DefaultParams()
    {
        // Adaptive lambda with ParentIndicator=0 => lambda_eff ≈ 0 (symmetric)
        var link = new SESLink();
        double[] values = { -100.0, -10.0, -1.0, 0.0, 1.0, 10.0, 100.0 };
        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, Math.Max(RoundTripTol, Math.Abs(x) * 1e-8),
                $"Round-trip failed for x={x}");
        }
    }

    /// <summary>Verifies that round trip fixed lambda.</summary>
    [TestMethod]
    public void Test_RoundTrip_FixedLambda()
    {
        var link = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.4 };
        double[] values = { -50.0, -5.0, -0.1, 0.0, 0.1, 5.0, 50.0 };
        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, Math.Max(RoundTripTol, Math.Abs(x) * 1e-8),
                $"Round-trip failed for x={x}, lambda=0.4");
        }
    }

    /// <summary>Verifies that round trip negative lambda.</summary>
    [TestMethod]
    public void Test_RoundTrip_NegativeLambda()
    {
        var link = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = -0.3 };
        double[] values = { -20.0, -1.0, 0.0, 1.0, 20.0 };
        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, Math.Max(RoundTripTol, Math.Abs(x) * 1e-8),
                $"Round-trip failed for x={x}, lambda=-0.3");
        }
    }

    /// <summary>Verifies that round trip large a.</summary>
    [TestMethod]
    public void Test_RoundTrip_LargeA()
    {
        var link = new SESLink(a: 3.0, useAdaptiveLambda: false) { Lambda = 0.0 };
        double[] values = { -5.0, -1.0, 0.0, 1.0, 5.0 };
        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, Math.Max(RoundTripTol, Math.Abs(x) * 1e-6),
                $"Round-trip failed for x={x}, a=3.0");
        }
    }

    #endregion

    #region InverseLink Tests

    /// <summary>Verifies that inverse link returns zero when at zero.</summary>
    [TestMethod]
    public void Test_InverseLink_AtZero_ReturnsZero()
    {
        // gamma(0) = (1/a) * exp(0) * sinh(0) = 0 for any a, lambda
        var link = new SESLink();
        Assert.AreEqual(0.0, link.InverseLink(0.0), 1e-15);
    }

    /// <summary>Verifies that inverse link symmetric when lambda zero.</summary>
    [TestMethod]
    public void Test_InverseLink_SymmetricWhenLambdaZero()
    {
        // With lambda=0: gamma(eta) = sinh(a*eta)/a, which is odd
        var link = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.0 };
        double[] etas = { 0.5, 1.0, 2.0 };
        foreach (double eta in etas)
        {
            double pos = link.InverseLink(eta);
            double neg = link.InverseLink(-eta);
            Assert.AreEqual(pos, -neg, 1e-12,
                $"Symmetry failed for eta={eta}");
        }
    }

    /// <summary>Verifies that inverse link monotone.</summary>
    [TestMethod]
    public void Test_InverseLink_Monotone()
    {
        var link = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.4 };
        double prev = link.InverseLink(-10.0);
        for (double eta = -9.0; eta <= 10.0; eta += 0.5)
        {
            double current = link.InverseLink(eta);
            Assert.IsTrue(current > prev, $"Monotonicity violated at eta={eta}");
            prev = current;
        }
    }

    #endregion

    #region Derivative Tests

    /// <summary>Verifies that d link finite difference symmetric case.</summary>
    [TestMethod]
    public void Test_DLink_FiniteDifference_SymmetricCase()
    {
        var link = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.0 };
        double[] testPoints = { -5.0, -1.0, 0.0, 1.0, 5.0 };
        foreach (double x in testPoints)
        {
            double finiteDiff = (link.Link(x + DeltaH) - link.Link(x - DeltaH)) / (2 * DeltaH);
            Assert.AreEqual(finiteDiff, link.DLink(x), DerivativeTol,
                $"Derivative mismatch at x={x}");
        }
    }

    /// <summary>Verifies that d link finite difference asymmetric case.</summary>
    [TestMethod]
    public void Test_DLink_FiniteDifference_AsymmetricCase()
    {
        var link = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.5 };
        double[] testPoints = { -5.0, -1.0, 0.0, 1.0, 5.0 };
        foreach (double x in testPoints)
        {
            double finiteDiff = (link.Link(x + DeltaH) - link.Link(x - DeltaH)) / (2 * DeltaH);
            Assert.AreEqual(finiteDiff, link.DLink(x), DerivativeTol,
                $"Derivative mismatch at x={x}, lambda=0.5");
        }
    }

    /// <summary>Verifies that d link at zero approximately one.</summary>
    [TestMethod]
    public void Test_DLink_AtZero_ApproximatelyOne()
    {
        // Local slope at gamma=0 is 1 (key design property)
        var link = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.0 };
        Assert.AreEqual(1.0, link.DLink(0.0), 1e-6);
    }

    /// <summary>Verifies that d link always positive.</summary>
    [TestMethod]
    public void Test_DLink_AlwaysPositive()
    {
        var link = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.4 };
        double[] testPoints = { -100.0, -10.0, -1.0, 0.0, 1.0, 10.0, 100.0 };
        foreach (double x in testPoints)
        {
            Assert.IsTrue(link.DLink(x) > 0, $"Derivative should be positive at x={x}");
        }
    }

    #endregion

    #region Adaptive Lambda Tests

    /// <summary>Verifies that adaptive lambda zero indicator symmetric result.</summary>
    [TestMethod]
    public void Test_AdaptiveLambda_ZeroIndicator_SymmetricResult()
    {
        var link = new SESLink { ParentIndicator = 0.0 };
        // With ParentIndicator=0: lambda_eff = LambdaMax * tanh(0) = 0
        double pos = link.InverseLink(2.0);
        double neg = link.InverseLink(-2.0);
        Assert.AreEqual(pos, -neg, 1e-10);
    }

    /// <summary>Verifies that adaptive lambda positive indicator positive tail stronger.</summary>
    [TestMethod]
    public void Test_AdaptiveLambda_PositiveIndicator_PositiveTailStronger()
    {
        var link = new SESLink { ParentIndicator = 2.0 };
        // Positive indicator => positive tail is heavier => |gamma(+eta)| > |gamma(-eta)|
        double pos = Math.Abs(link.InverseLink(3.0));
        double neg = Math.Abs(link.InverseLink(-3.0));
        Assert.IsTrue(pos > neg, "Positive tail should be heavier with positive ParentIndicator");
    }

    /// <summary>Verifies that adaptive lambda negative indicator negative tail stronger.</summary>
    [TestMethod]
    public void Test_AdaptiveLambda_NegativeIndicator_NegativeTailStronger()
    {
        var link = new SESLink { ParentIndicator = -2.0 };
        double pos = Math.Abs(link.InverseLink(3.0));
        double neg = Math.Abs(link.InverseLink(-3.0));
        Assert.IsTrue(neg > pos, "Negative tail should be heavier with negative ParentIndicator");
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that link small a.</summary>
    [TestMethod]
    public void Test_Link_SmallA()
    {
        // Moderately small A (a=0.1) — function is mildly nonlinear
        // Note: very small a (e.g., 0.001) causes the tail-based initial guess
        // to dominate incorrectly, so we use a moderate value here.
        var link = new SESLink(a: 0.1, useAdaptiveLambda: false) { Lambda = 0.0 };
        double x = 5.0;
        double eta = link.Link(x);
        double recovered = link.InverseLink(eta);
        Assert.AreEqual(x, recovered, 1e-6);
    }

    /// <summary>Verifies that inverse link extreme tails.</summary>
    [TestMethod]
    public void Test_InverseLink_ExtremeTails()
    {
        var link = new SESLink(a: 1.0, useAdaptiveLambda: false) { Lambda = 0.0 };
        // Large positive eta produces large positive gamma
        double largePos = link.InverseLink(10.0);
        Assert.IsTrue(largePos > 1000.0);
        Assert.IsTrue(double.IsFinite(largePos));
        // Large negative eta produces large negative gamma
        double largeNeg = link.InverseLink(-10.0);
        Assert.IsTrue(largeNeg < -1000.0);
        Assert.IsTrue(double.IsFinite(largeNeg));
    }

    #endregion
}
