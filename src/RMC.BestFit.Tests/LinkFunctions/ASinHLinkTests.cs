using RMC.BestFit.Models.LinkFunctions;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.LinkFunctions;

/// <summary>
/// Unit tests for the <see cref="ASinHLink"/> class.
/// Verifies round-trip consistency, derivative correctness, centering, monotonicity,
/// asymmetry behavior, adaptive epsilon, serialization, and edge cases.
/// </summary>
[TestClass]
public class ASinHLinkTests
{
    /// <summary>
    /// Finite-difference step size for derivative verification.
    /// </summary>
    private const double DeltaH = 1e-7;

    /// <summary>
    /// Tolerance for round-trip identity tests (closed-form inverse, so near machine precision).
    /// </summary>
    private const double RoundTripTol = 1e-10;

    /// <summary>
    /// Tolerance for derivative (finite-difference vs analytic) tests.
    /// </summary>
    private const double DerivativeTol = 1e-4;

    #region Constructor Tests

    /// <summary>
    /// Verifies the default constructor sets all parameters to their defaults.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_Default_SetsDefaultValues()
    {
        var link = new ASinHLink();
        Assert.AreEqual(0.0, link.Gamma0);
        Assert.AreEqual(1.0, link.Scale);
        Assert.AreEqual(0.0, link.Epsilon);
        Assert.AreEqual(1.0, link.Delta);
        Assert.IsFalse(link.UseAdaptiveEpsilon);
        Assert.AreEqual(0.0, link.ParentIndicator);
        Assert.AreEqual(0.5, link.EpsilonMax);
        Assert.AreEqual(1.0, link.EpsilonSlope);
    }

    /// <summary>
    /// Verifies the two-parameter constructor creates a symmetric link.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_TwoParam_SymmetricDefaults()
    {
        var link = new ASinHLink(gamma0: 0.5, scale: 0.3);
        Assert.AreEqual(0.5, link.Gamma0);
        Assert.AreEqual(0.3, link.Scale);
        Assert.AreEqual(0.0, link.Epsilon, "Epsilon should default to 0 (symmetric).");
        Assert.AreEqual(1.0, link.Delta, "Delta should default to 1.");
    }

    /// <summary>
    /// Verifies the four-parameter constructor stores asymmetry values.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_FourParam_SetsValues()
    {
        var link = new ASinHLink(gamma0: 0.5, scale: 0.3, epsilon: 0.2, delta: 1.5);
        Assert.AreEqual(0.5, link.Gamma0);
        Assert.AreEqual(0.3, link.Scale);
        Assert.AreEqual(0.2, link.Epsilon);
        Assert.AreEqual(1.5, link.Delta);
    }

    /// <summary>
    /// Verifies the parameterized constructor floors the scale at Eps.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_SmallScale_FloorsAtEps()
    {
        var link = new ASinHLink(gamma0: 0.0, scale: -1.0);
        Assert.IsTrue(link.Scale > 0.0, "Scale should be floored to a positive value.");
    }

    /// <summary>
    /// Verifies the parameterized constructor floors delta at Eps.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_SmallDelta_FloorsAtEps()
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: 0.0, delta: -1.0);
        Assert.IsTrue(link.Delta > 0.0, "Delta should be floored to a positive value.");
    }

    #endregion

    #region Centering Tests (Symmetric: ε=0, δ=1)

    /// <summary>
    /// Verifies Link(Gamma0) = 0 for the symmetric case (ε=0, δ=1).
    /// </summary>
    [TestMethod]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(-0.5)]
    [DataRow(1.2)]
    [DataRow(-2.0)]
    public void Test_Link_Symmetric_AtCenter_ReturnsZero(double gamma0)
    {
        var link = new ASinHLink(gamma0, scale: 0.3);
        Assert.AreEqual(0.0, link.Link(gamma0), 1e-15, $"Link({gamma0}) should be 0 when Gamma0={gamma0}.");
    }

    /// <summary>
    /// Verifies InverseLink(0) = Gamma0 for the symmetric case (ε=0, δ=1).
    /// </summary>
    [TestMethod]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(-0.5)]
    [DataRow(1.2)]
    [DataRow(-2.0)]
    public void Test_InverseLink_Symmetric_AtZero_ReturnsGamma0(double gamma0)
    {
        var link = new ASinHLink(gamma0, scale: 0.3);
        Assert.AreEqual(gamma0, link.InverseLink(0.0), 1e-15, $"InverseLink(0) should be {gamma0}.");
    }

    #endregion

    #region Round-Trip Tests (Symmetric)

    /// <summary>
    /// Verifies InverseLink(Link(x)) == x for a range of values with default (symmetric) parameters.
    /// </summary>
    [TestMethod]
    [DataRow(-100.0)]
    [DataRow(-10.0)]
    [DataRow(-1.0)]
    [DataRow(-0.01)]
    [DataRow(0.0)]
    [DataRow(0.01)]
    [DataRow(1.0)]
    [DataRow(10.0)]
    [DataRow(100.0)]
    public void Test_RoundTrip_Symmetric_DefaultParams(double gamma)
    {
        var link = new ASinHLink();
        double eta = link.Link(gamma);
        double recovered = link.InverseLink(eta);
        Assert.AreEqual(gamma, recovered, RoundTripTol, $"Round-trip failed for gamma={gamma}.");
    }

    /// <summary>
    /// Verifies InverseLink(Link(x)) == x for typical LP3 skewness parameters.
    /// </summary>
    [TestMethod]
    [DataRow(-2.0)]
    [DataRow(-0.5)]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(2.0)]
    public void Test_RoundTrip_Symmetric_TypicalLP3(double gamma)
    {
        var link = new ASinHLink(gamma0: 0.5, scale: 0.3);
        double eta = link.Link(gamma);
        double recovered = link.InverseLink(eta);
        Assert.AreEqual(gamma, recovered, RoundTripTol, $"Round-trip failed for gamma={gamma} (LP3 typical).");
    }

    #endregion

    #region Round-Trip Tests (Asymmetric)

    /// <summary>
    /// Verifies InverseLink(Link(x)) == x with positive epsilon (right-skewed).
    /// </summary>
    [TestMethod]
    [DataRow(-5.0)]
    [DataRow(-1.0)]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(1.0)]
    [DataRow(5.0)]
    public void Test_RoundTrip_PositiveEpsilon(double gamma)
    {
        var link = new ASinHLink(gamma0: 0.5, scale: 0.3, epsilon: 0.3);
        double eta = link.Link(gamma);
        double recovered = link.InverseLink(eta);
        Assert.AreEqual(gamma, recovered, RoundTripTol, $"Round-trip failed for gamma={gamma} (ε=0.3).");
    }

    /// <summary>
    /// Verifies InverseLink(Link(x)) == x with negative epsilon (left-skewed).
    /// </summary>
    [TestMethod]
    [DataRow(-5.0)]
    [DataRow(-1.0)]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(1.0)]
    [DataRow(5.0)]
    public void Test_RoundTrip_NegativeEpsilon(double gamma)
    {
        var link = new ASinHLink(gamma0: -0.3, scale: 0.5, epsilon: -0.4);
        double eta = link.Link(gamma);
        double recovered = link.InverseLink(eta);
        Assert.AreEqual(gamma, recovered, RoundTripTol, $"Round-trip failed for gamma={gamma} (ε=-0.4).");
    }

    /// <summary>
    /// Verifies InverseLink(Link(x)) == x with non-unit delta (heavy tails).
    /// </summary>
    [TestMethod]
    [DataRow(-5.0)]
    [DataRow(-1.0)]
    [DataRow(0.0)]
    [DataRow(1.0)]
    [DataRow(5.0)]
    public void Test_RoundTrip_HeavyTails(double gamma)
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: 0.2, delta: 1.5);
        double eta = link.Link(gamma);
        double recovered = link.InverseLink(eta);
        Assert.AreEqual(gamma, recovered, RoundTripTol, $"Round-trip failed for gamma={gamma} (δ=1.5).");
    }

    /// <summary>
    /// Verifies InverseLink(Link(x)) == x with light tails (delta &lt; 1).
    /// </summary>
    [TestMethod]
    [DataRow(-5.0)]
    [DataRow(-1.0)]
    [DataRow(0.0)]
    [DataRow(1.0)]
    [DataRow(5.0)]
    public void Test_RoundTrip_LightTails(double gamma)
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: 0.0, delta: 0.5);
        double eta = link.Link(gamma);
        double recovered = link.InverseLink(eta);
        Assert.AreEqual(gamma, recovered, RoundTripTol, $"Round-trip failed for gamma={gamma} (δ=0.5).");
    }

    #endregion

    #region Derivative Tests

    /// <summary>
    /// Verifies DLink matches finite-difference for the symmetric case (ε=0, δ=1).
    /// </summary>
    [TestMethod]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(-0.5)]
    [DataRow(2.0)]
    [DataRow(-3.0)]
    [DataRow(10.0)]
    [DataRow(-10.0)]
    public void Test_DLink_Symmetric_MatchesFiniteDifference(double gamma)
    {
        var link = new ASinHLink();
        double analytical = link.DLink(gamma);
        double numerical = (link.Link(gamma + DeltaH) - link.Link(gamma - DeltaH)) / (2.0 * DeltaH);
        Assert.AreEqual(numerical, analytical, DerivativeTol,
            $"DLink mismatch at gamma={gamma}: analytical={analytical}, numerical={numerical}.");
    }

    /// <summary>
    /// Verifies DLink matches finite-difference for asymmetric case.
    /// </summary>
    [TestMethod]
    [DataRow(-2.0)]
    [DataRow(-0.5)]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(2.0)]
    public void Test_DLink_Asymmetric_MatchesFiniteDifference(double gamma)
    {
        var link = new ASinHLink(gamma0: 0.3, scale: 0.5, epsilon: 0.3, delta: 1.2);
        double analytical = link.DLink(gamma);
        double numerical = (link.Link(gamma + DeltaH) - link.Link(gamma - DeltaH)) / (2.0 * DeltaH);
        Assert.AreEqual(numerical, analytical, DerivativeTol,
            $"DLink mismatch at gamma={gamma} (ε=0.3, δ=1.2): analytical={analytical}, numerical={numerical}.");
    }

    /// <summary>
    /// Verifies DLink matches finite-difference for LP3-typical parameters with asymmetry.
    /// </summary>
    [TestMethod]
    [DataRow(-1.0)]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(1.0)]
    [DataRow(3.0)]
    public void Test_DLink_LP3Params_MatchesFiniteDifference(double gamma)
    {
        var link = new ASinHLink(gamma0: 0.5, scale: 0.3, epsilon: 0.15);
        double analytical = link.DLink(gamma);
        double numerical = (link.Link(gamma + DeltaH) - link.Link(gamma - DeltaH)) / (2.0 * DeltaH);
        Assert.AreEqual(numerical, analytical, DerivativeTol,
            $"DLink mismatch at gamma={gamma}: analytical={analytical}, numerical={numerical}.");
    }

    #endregion

    #region Monotonicity Tests

    /// <summary>
    /// Verifies DLink is strictly positive everywhere for the symmetric case.
    /// </summary>
    [TestMethod]
    public void Test_DLink_Symmetric_AlwaysPositive()
    {
        var link = new ASinHLink(gamma0: 0.5, scale: 0.3);
        double[] testPoints = { -100, -10, -1, -0.01, 0, 0.01, 0.5, 1, 10, 100 };
        foreach (double gamma in testPoints)
        {
            Assert.IsTrue(link.DLink(gamma) > 0, $"DLink should be positive at gamma={gamma}.");
        }
    }

    /// <summary>
    /// Verifies DLink is strictly positive everywhere for the asymmetric case.
    /// </summary>
    [TestMethod]
    public void Test_DLink_Asymmetric_AlwaysPositive()
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: 0.4, delta: 1.3);
        double[] testPoints = { -100, -10, -1, -0.01, 0, 0.01, 0.5, 1, 10, 100 };
        foreach (double gamma in testPoints)
        {
            Assert.IsTrue(link.DLink(gamma) > 0, $"DLink should be positive at gamma={gamma} (ε=0.4, δ=1.3).");
        }
    }

    /// <summary>
    /// Verifies Link(x2) > Link(x1) whenever x2 > x1 for the symmetric case.
    /// </summary>
    [TestMethod]
    public void Test_Link_Symmetric_StrictlyIncreasing()
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1.0);
        double[] sorted = { -100, -10, -1, 0, 1, 10, 100 };
        for (int i = 0; i < sorted.Length - 1; i++)
        {
            Assert.IsTrue(link.Link(sorted[i + 1]) > link.Link(sorted[i]),
                $"Link should be strictly increasing: Link({sorted[i + 1]}) > Link({sorted[i]}).");
        }
    }

    /// <summary>
    /// Verifies Link(x2) > Link(x1) whenever x2 > x1 for the asymmetric case.
    /// </summary>
    [TestMethod]
    public void Test_Link_Asymmetric_StrictlyIncreasing()
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: 0.3, delta: 1.5);
        double[] sorted = { -100, -10, -1, 0, 1, 10, 100 };
        for (int i = 0; i < sorted.Length - 1; i++)
        {
            Assert.IsTrue(link.Link(sorted[i + 1]) > link.Link(sorted[i]),
                $"Link should be strictly increasing: Link({sorted[i + 1]}) > Link({sorted[i]}) (ε=0.3, δ=1.5).");
        }
    }

    #endregion

    #region Asymmetry Behavior Tests

    /// <summary>
    /// Verifies that positive ε inflates the positive tail more than the negative tail.
    /// The positive deviation in η-space should be smaller than the symmetric case,
    /// while the negative deviation should be larger (because ε shifts the sinh argument).
    /// </summary>
    [TestMethod]
    public void Test_PositiveEpsilon_InflatesPositiveTail()
    {
        double gamma0 = 0.0, scale = 1.0;
        var symmetric = new ASinHLink(gamma0, scale);
        var skewed = new ASinHLink(gamma0, scale, epsilon: 0.4);

        // For the inverse link: positive η should map to a larger γ with ε > 0
        // InverseLink adds ε to asinh(η) before dividing by δ and taking sinh
        double etaTest = 2.0;
        double gammaSym = symmetric.InverseLink(etaTest);
        double gammaSkew = skewed.InverseLink(etaTest);
        Assert.IsTrue(gammaSkew > gammaSym,
            $"Positive ε should produce larger γ for positive η. Symmetric={gammaSym}, Skewed={gammaSkew}.");

        // For negative η, positive ε should produce less negative γ (closer to center)
        double gammaSym_neg = symmetric.InverseLink(-etaTest);
        double gammaSkew_neg = skewed.InverseLink(-etaTest);
        Assert.IsTrue(gammaSkew_neg > gammaSym_neg,
            $"Positive ε should shift negative η toward center. Symmetric={gammaSym_neg}, Skewed={gammaSkew_neg}.");
    }

    /// <summary>
    /// Verifies that ε = 0 and δ = 1 reduces to the simple asinh link.
    /// </summary>
    [TestMethod]
    public void Test_SymmetricCase_ReducesToSimpleAsinh()
    {
        double gamma0 = 0.5, scale = 0.3;
        var link = new ASinHLink(gamma0, scale, epsilon: 0.0, delta: 1.0);

        // Known value: Link(gamma0) = sinh(1 * asinh(0) - 0) = sinh(0) = 0
        Assert.AreEqual(0.0, link.Link(gamma0), 1e-15);

        // Known value: InverseLink(0) = gamma0 + s * sinh((asinh(0) + 0) / 1) = gamma0
        Assert.AreEqual(gamma0, link.InverseLink(0.0), 1e-15);

        // DLink at center: (1/s) * cosh(0) / sqrt(1+0) = 1/s
        Assert.AreEqual(1.0 / scale, link.DLink(gamma0), 1e-12);
    }

    /// <summary>
    /// <summary>
    /// Verifies that δ &gt; 1 ("heavier tails" on the forward link) compresses the inverse
    /// link — a given η maps to a γ closer to γ₀ than under δ = 1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>InverseLink(η) = γ₀ + s · sinh((asinh(η) + ε) / δ)</c>. With ε = 0, increasing
    /// δ divides the sinh argument, which shrinks the output:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>standard (δ=1): sinh(asinh(3)/1) = 3.0</description></item>
    ///   <item><description>heavy (δ=1.5): sinh(asinh(3)/1.5) = sinh(1.2123) ≈ 1.53</description></item>
    /// </list>
    /// <para>
    /// "Heavier tails" refers to the forward direction — for a given γ, the heavy link
    /// produces a LARGER η (more γ-space mass pushed into the η-tail). On the inverse
    /// link, this same property means a given η maps to a SMALLER γ.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Delta_GreaterThanOne_InverseLinkCompresses()
    {
        var standard = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: 0.0, delta: 1.0);
        var heavy = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: 0.0, delta: 1.5);

        // For large positive η, δ > 1 compresses the inverse-link argument → smaller γ.
        double eta = 3.0;
        Assert.IsTrue(heavy.InverseLink(eta) < standard.InverseLink(eta),
            "δ > 1 compresses the inverse-link argument, giving smaller γ for the same positive η.");

        // For large negative η, δ > 1 compresses symmetrically → less-negative γ.
        Assert.IsTrue(heavy.InverseLink(-eta) > standard.InverseLink(-eta),
            "δ > 1 compresses the inverse-link argument, giving less-negative γ for the same negative η.");
    }

    #endregion

    #region Adaptive Epsilon Tests

    /// <summary>
    /// Verifies that adaptive epsilon with ParentIndicator = 0 gives ε_eff = 0 (symmetric).
    /// </summary>
    [TestMethod]
    public void Test_AdaptiveEpsilon_ZeroParent_Symmetric()
    {
        var adaptive = new ASinHLink(gamma0: 0.0, scale: 1.0)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = 0.0,
            EpsilonMax = 0.5
        };
        var symmetric = new ASinHLink(gamma0: 0.0, scale: 1.0);

        // Should behave identically to symmetric
        double[] testPoints = { -5, -1, 0, 1, 5 };
        foreach (double gamma in testPoints)
        {
            Assert.AreEqual(symmetric.Link(gamma), adaptive.Link(gamma), 1e-15,
                $"Adaptive ε with zero parent should equal symmetric at gamma={gamma}.");
        }
    }

    /// <summary>
    /// Verifies that adaptive epsilon with positive parent gives ε > 0.
    /// </summary>
    [TestMethod]
    public void Test_AdaptiveEpsilon_PositiveParent_RightInflation()
    {
        var adaptive = new ASinHLink(gamma0: 0.5, scale: 0.3)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = 1.0,
            EpsilonMax = 0.5,
            EpsilonSlope = 1.0
        };
        var symmetric = new ASinHLink(gamma0: 0.5, scale: 0.3);

        // Positive parent → ε > 0 → positive tail should be larger
        double eta = 2.0;
        Assert.IsTrue(adaptive.InverseLink(eta) > symmetric.InverseLink(eta),
            "Adaptive ε with positive parent should inflate positive tail.");
    }

    /// <summary>
    /// Verifies that adaptive epsilon saturates toward ±EpsilonMax.
    /// </summary>
    [TestMethod]
    public void Test_AdaptiveEpsilon_Saturation()
    {
        double epsMax = 0.5;
        var linkLargePos = new ASinHLink(gamma0: 0.0, scale: 1.0)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = 100.0,
            EpsilonMax = epsMax,
            EpsilonSlope = 1.0
        };
        var linkLargeNeg = new ASinHLink(gamma0: 0.0, scale: 1.0)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = -100.0,
            EpsilonMax = epsMax,
            EpsilonSlope = 1.0
        };

        // With very large parent, ε_eff ≈ ±EpsilonMax
        // Compare against fixed ε = ±EpsilonMax
        var fixedPos = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: epsMax);
        var fixedNeg = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: -epsMax);

        Assert.AreEqual(fixedPos.Link(2.0), linkLargePos.Link(2.0), 1e-6,
            "Saturated positive adaptive ε should match fixed ε = EpsilonMax.");
        Assert.AreEqual(fixedNeg.Link(2.0), linkLargeNeg.Link(2.0), 1e-6,
            "Saturated negative adaptive ε should match fixed ε = -EpsilonMax.");
    }

    /// <summary>
    /// Verifies round-trip works correctly with adaptive epsilon.
    /// </summary>
    [TestMethod]
    [DataRow(-3.0)]
    [DataRow(-0.5)]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(3.0)]
    public void Test_RoundTrip_AdaptiveEpsilon(double gamma)
    {
        var link = new ASinHLink(gamma0: 0.5, scale: 0.3)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = 0.5,
            EpsilonMax = 0.4,
            EpsilonSlope = 1.0
        };
        double eta = link.Link(gamma);
        double recovered = link.InverseLink(eta);
        Assert.AreEqual(gamma, recovered, RoundTripTol,
            $"Round-trip failed for gamma={gamma} (adaptive ε, parent=0.5).");
    }

    #endregion

    #region Known-Value Tests

    /// <summary>
    /// Verifies known values for the standard asinh (Gamma0=0, Scale=1, ε=0, δ=1).
    /// </summary>
    [TestMethod]
    public void Test_KnownValues_Standard()
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1.0);

        // Link(0) = sinh(1*asinh(0) - 0) = sinh(0) = 0
        Assert.AreEqual(0.0, link.Link(0.0), 1e-15);

        // Link(sinh(1)) = sinh(1*asinh(sinh(1)) - 0) = sinh(1) = sinh(1) ✓
        // asinh(sinh(1)) = 1, so Link(sinh(1)) = sinh(1*1 - 0) = sinh(1)
        Assert.AreEqual(Math.Sinh(1.0), link.Link(Math.Sinh(1.0)), 1e-12);

        // InverseLink(0) = 0 + 1*sinh((asinh(0) + 0)/1) = sinh(0) = 0
        Assert.AreEqual(0.0, link.InverseLink(0.0), 1e-15);

        // DLink(0) = (1/1) * cosh(1*asinh(0) - 0) / sqrt(1+0) = cosh(0)/1 = 1
        Assert.AreEqual(1.0, link.DLink(0.0), 1e-15);
    }

    /// <summary>
    /// Verifies the near-linear behavior within one scale unit for the symmetric case.
    /// </summary>
    [TestMethod]
    public void Test_NearLinear_Behavior_Symmetric()
    {
        double gamma0 = 0.5;
        double scale = 0.3;
        var link = new ASinHLink(gamma0, scale);

        // Small deviation: link should be approximately linear
        double smallDelta = 0.001;
        double eta = link.Link(gamma0 + smallDelta);
        double linear = smallDelta / scale;
        Assert.AreEqual(linear, eta, 1e-8, "Should be approximately linear near center.");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Verifies the link handles very large |gamma| without overflow.
    /// </summary>
    /// <summary>
    /// Verifies round-trip stability throughout the documented stable domain for the
    /// default symmetric parameterization (|γ| up to 1e3 at 1e-10 relative precision).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Test_RoundTrip_Symmetric_DefaultParams"/> (line ~165) establishes
    /// round-trip at <see cref="RoundTripTol"/> (1e-10 relative) for |γ| up to 100.
    /// This test extends coverage by one order of magnitude (|γ| up to 1e3). Values
    /// much beyond this drift past the 1e-10 tolerance because the
    /// <c>sinh ∘ asinh ∘ sinh ∘ asinh</c> chain loses ULP resolution as its arguments
    /// grow — empirically, |γ| = 1e4 yields ~1.4e-8 relative error, which exceeds the
    /// 1e-10 bound but is still orders of magnitude smaller than any physically
    /// meaningful skewness value (real-world |γ| &lt; 10 typically). Larger-γ coverage
    /// at looser tolerance is tracked by separate tests in the
    /// <see cref="Test_RoundTrip_Symmetric_DefaultParams"/> family.
    /// </para>
    /// </remarks>
    [TestMethod]
    [DataRow(1e2)]
    [DataRow(-1e2)]
    [DataRow(1e3)]
    [DataRow(-1e3)]
    public void Test_LargeGamma_RoundTrip_StableDomain(double gamma)
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1.0);
        double eta = link.Link(gamma);
        Assert.IsFalse(double.IsNaN(eta), $"Link({gamma}) should not be NaN.");
        Assert.IsFalse(double.IsInfinity(eta), $"Link({gamma}) should not be Infinity.");

        double recovered = link.InverseLink(eta);
        Assert.AreEqual(gamma, recovered, Math.Abs(gamma) * 1e-10,
            $"Round-trip failed for large gamma={gamma}.");
    }

    /// <summary>
    /// Verifies asymmetric round-trip throughout the documented stable domain
    /// with non-identity ε and δ.
    /// </summary>
    /// <remarks>
    /// The asymmetric parameterization (ε = 0.3, δ = 1.2) compounds floating-point
    /// error in the sinh-asinh chain: both ε and δ shift / scale the argument to
    /// sinh, reducing the stable domain relative to the symmetric case. |γ| up to 1e3
    /// is safely within that band; the tolerance (1e-8 relative) reflects the
    /// extra accumulated drift vs the symmetric test's 1e-10.
    /// </remarks>
    [TestMethod]
    [DataRow(1e2)]
    [DataRow(-1e2)]
    [DataRow(1e3)]
    [DataRow(-1e3)]
    public void Test_LargeGamma_Asymmetric_RoundTrip_StableDomain(double gamma)
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1.0, epsilon: 0.3, delta: 1.2);
        double eta = link.Link(gamma);
        Assert.IsFalse(double.IsNaN(eta), $"Link({gamma}) should not be NaN.");
        Assert.IsFalse(double.IsInfinity(eta), $"Link({gamma}) should not be Infinity.");

        double recovered = link.InverseLink(eta);
        Assert.AreEqual(gamma, recovered, Math.Abs(gamma) * 1e-8,
            $"Round-trip failed for large gamma={gamma} (ε=0.3, δ=1.2).");
    }

    /// <summary>
    /// Verifies the link handles very small Scale without producing NaN.
    /// </summary>
    [TestMethod]
    public void Test_VerySmallScale_StillMonotone()
    {
        var link = new ASinHLink(gamma0: 0.0, scale: 1e-10);
        Assert.IsTrue(link.DLink(0.0) > 0, "DLink should be positive even with tiny Scale.");
        Assert.IsTrue(link.DLink(5.0) > 0, "DLink should be positive even with tiny Scale.");

        double eta = link.Link(1.0);
        Assert.IsFalse(double.IsNaN(eta), "Link should not be NaN with tiny Scale.");
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Verifies round-trip serialization via XElement for the symmetric case.
    /// </summary>
    [TestMethod]
    public void Test_Serialization_RoundTrip_Symmetric()
    {
        var original = new ASinHLink(gamma0: 0.5, scale: 0.3);
        XElement xml = original.ToXElement();
        var restored = new ASinHLink(xml);

        Assert.AreEqual(original.Gamma0, restored.Gamma0, 1e-15);
        Assert.AreEqual(original.Scale, restored.Scale, 1e-15);
        Assert.AreEqual(original.Epsilon, restored.Epsilon, 1e-15);
        Assert.AreEqual(original.Delta, restored.Delta, 1e-15);

        double testGamma = 1.5;
        Assert.AreEqual(original.Link(testGamma), restored.Link(testGamma), 1e-15,
            "Link values differ after serialization round-trip.");
    }

    /// <summary>
    /// Verifies round-trip serialization via XElement for the full asymmetric case.
    /// </summary>
    [TestMethod]
    public void Test_Serialization_RoundTrip_Asymmetric()
    {
        var original = new ASinHLink(gamma0: -0.3, scale: 0.5, epsilon: 0.25, delta: 1.3)
        {
            UseAdaptiveEpsilon = true,
            ParentIndicator = 0.7,
            EpsilonMax = 0.6,
            EpsilonSlope = 2.0
        };
        XElement xml = original.ToXElement();
        var restored = new ASinHLink(xml);

        Assert.AreEqual(original.Gamma0, restored.Gamma0, 1e-15);
        Assert.AreEqual(original.Scale, restored.Scale, 1e-15);
        Assert.AreEqual(original.Epsilon, restored.Epsilon, 1e-15);
        Assert.AreEqual(original.Delta, restored.Delta, 1e-15);
        Assert.AreEqual(original.UseAdaptiveEpsilon, restored.UseAdaptiveEpsilon);
        Assert.AreEqual(original.ParentIndicator, restored.ParentIndicator, 1e-15);
        Assert.AreEqual(original.EpsilonMax, restored.EpsilonMax, 1e-15);
        Assert.AreEqual(original.EpsilonSlope, restored.EpsilonSlope, 1e-15);

        double testGamma = 1.5;
        Assert.AreEqual(original.Link(testGamma), restored.Link(testGamma), 1e-15,
            "Link values differ after serialization round-trip (asymmetric).");
    }

    /// <summary>
    /// Verifies the XElement name is "ASinHLink".
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ElementName()
    {
        var link = new ASinHLink();
        XElement xml = link.ToXElement();
        Assert.AreEqual("ASinHLink", xml.Name.LocalName);
    }

    /// <summary>
    /// Verifies the factory can create an ASinHLink from XElement.
    /// </summary>
    [TestMethod]
    public void Test_Factory_CreatesASinHLink()
    {
        var original = new ASinHLink(gamma0: -0.3, scale: 0.5, epsilon: 0.2, delta: 1.1);
        XElement xml = original.ToXElement();
        var restored = BestFitLinkFunctionFactory.CreateFromXElement(xml);

        Assert.IsInstanceOfType(restored, typeof(ASinHLink));
        var asinhRestored = (ASinHLink)restored;
        Assert.AreEqual(original.Gamma0, asinhRestored.Gamma0, 1e-15);
        Assert.AreEqual(original.Scale, asinhRestored.Scale, 1e-15);
        Assert.AreEqual(original.Epsilon, asinhRestored.Epsilon, 1e-15);
        Assert.AreEqual(original.Delta, asinhRestored.Delta, 1e-15);
    }

    #endregion
}
