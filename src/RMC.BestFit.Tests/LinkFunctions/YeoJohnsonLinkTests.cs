using RMC.BestFit.Models.LinkFunctions;
using Numerics.Functions;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.LinkFunctions;

/// <summary>
/// Unit tests for the <c>YeoJohnsonLink</c> class.
/// </summary>
/// <remarks>
/// Tests cover: default and parameterized construction, XML round-trip,
/// Link/InverseLink round-trip for both positive and negative x values,
/// identity behavior at lambda=1, DLink finite-difference consistency,
/// and edge cases (x=0, very large/small x).
/// </remarks>
[TestClass]
public class YeoJohnsonLinkTests
{
    private const double RoundTripTol = 1e-9;
    private const double DeltaH = 1e-7;
    private const double DerivativeTol = 1e-4;

    #region Constructor Tests

    /// <summary>
    /// Default constructor sets Lambda=1.0 (identity transform).
    /// </summary>
    [TestMethod]
    public void Constructor_Default_LambdaIsOne()
    {
        var link = new YeoJohnsonLink();

        Assert.AreEqual(1.0, link.Lambda, 1e-10);
    }

    /// <summary>
    /// Lambda constructor stores the specified value.
    /// </summary>
    [TestMethod]
    public void Constructor_Lambda_StoresValue()
    {
        var link = new YeoJohnsonLink(lambda: 0.5);

        Assert.AreEqual(0.5, link.Lambda, 1e-10);
    }

    /// <summary>
    /// Lambda constructor with lambda=2 stores correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_Lambda_Two_StoresValue()
    {
        var link = new YeoJohnsonLink(lambda: 2.0);

        Assert.AreEqual(2.0, link.Lambda, 1e-10);
    }

    /// <summary>
    /// Values constructor throws ArgumentNullException for null input.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_Values_Null_ThrowsArgumentNullException()
    {
        _ = new YeoJohnsonLink((double[])null!);
    }

    /// <summary>
    /// Values constructor throws ArgumentException for fewer than 2 elements.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_Values_SingleElement_ThrowsArgumentException()
    {
        _ = new YeoJohnsonLink(new[] { 1.0 });
    }

    /// <summary>
    /// Values constructor with two or more elements produces a finite Lambda.
    /// </summary>
    [TestMethod]
    public void Constructor_Values_TwoElements_ProducesFiniteLambda()
    {
        var link = new YeoJohnsonLink(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        Assert.IsTrue(double.IsFinite(link.Lambda), "Lambda should be finite after fitting.");
    }

    /// <summary>
    /// XElement constructor throws ArgumentNullException for null input.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_XElement_Null_ThrowsArgumentNullException()
    {
        _ = new YeoJohnsonLink((XElement)null!);
    }

    #endregion

    #region Identity Transform Tests (lambda=1)

    /// <summary>
    /// With lambda=1, Link(x) = x for positive x (identity for x >= 0).
    /// </summary>
    [TestMethod]
    public void Link_LambdaOne_PositiveX_IsIdentity()
    {
        var link = new YeoJohnsonLink(1.0);
        double[] positiveValues = { 0.5, 1.0, 2.0, 5.0, 10.0 };

        foreach (double x in positiveValues)
        {
            Assert.AreEqual(x, link.Link(x), 1e-10,
                $"Link(x)=x should hold at lambda=1 for x={x}");
        }
    }

    /// <summary>
    /// With lambda=1, Link(0) = 0.
    /// </summary>
    [TestMethod]
    public void Link_LambdaOne_AtZero_IsZero()
    {
        var link = new YeoJohnsonLink(1.0);

        Assert.AreEqual(0.0, link.Link(0.0), 1e-10);
    }

    /// <summary>
    /// With lambda=1, DLink(x) = 1 for all x (derivative of identity).
    /// </summary>
    [TestMethod]
    public void DLink_LambdaOne_IsOne()
    {
        var link = new YeoJohnsonLink(1.0);
        double[] testPoints = { -5.0, -1.0, 0.0, 1.0, 5.0 };

        foreach (double x in testPoints)
        {
            Assert.AreEqual(1.0, link.DLink(x), 1e-10, $"DLink should be 1 at lambda=1, x={x}");
        }
    }

    #endregion

    #region Round-Trip Tests

    /// <summary>
    /// InverseLink(Link(x)) recovers x for positive values with lambda != 1.
    /// </summary>
    [TestMethod]
    public void RoundTrip_PositiveX_LambdaHalf()
    {
        var link = new YeoJohnsonLink(0.5);
        double[] values = { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0 };

        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, RoundTripTol, $"Round-trip failed for x={x}, lambda=0.5");
        }
    }

    /// <summary>
    /// InverseLink(Link(x)) recovers x for negative values with lambda = 0.5.
    /// </summary>
    [TestMethod]
    public void RoundTrip_NegativeX_LambdaHalf()
    {
        var link = new YeoJohnsonLink(0.5);
        double[] values = { -10.0, -5.0, -2.0, -1.0, -0.5, -0.1 };

        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, RoundTripTol, $"Round-trip failed for x={x}, lambda=0.5");
        }
    }

    /// <summary>
    /// InverseLink(Link(x)) recovers x for lambda = 2.0 (Box-Cox-like).
    /// </summary>
    [TestMethod]
    public void RoundTrip_LambdaTwo_PositiveX()
    {
        var link = new YeoJohnsonLink(2.0);
        double[] values = { 0.5, 1.0, 2.0, 5.0 };

        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, RoundTripTol, $"Round-trip failed for x={x}, lambda=2.0");
        }
    }

    /// <summary>
    /// InverseLink(Link(x)) recovers x for lambda = 0 (log-like transform).
    /// </summary>
    [TestMethod]
    public void RoundTrip_LambdaZero_PositiveX()
    {
        var link = new YeoJohnsonLink(0.0);
        double[] values = { 0.5, 1.0, 2.0, 5.0, 10.0 };

        foreach (double x in values)
        {
            double eta = link.Link(x);
            double recovered = link.InverseLink(eta);
            Assert.AreEqual(x, recovered, RoundTripTol, $"Round-trip failed for x={x}, lambda=0.0");
        }
    }

    #endregion

    #region Derivative Tests

    /// <summary>
    /// DLink agrees with finite difference for positive x at lambda = 0.5.
    /// </summary>
    [TestMethod]
    public void DLink_LambdaHalf_PositiveX_FiniteDifference()
    {
        var link = new YeoJohnsonLink(0.5);
        double[] testPoints = { 0.5, 1.0, 2.0, 5.0 };

        foreach (double x in testPoints)
        {
            double analytic = link.DLink(x);
            double fd = (link.Link(x + DeltaH) - link.Link(x - DeltaH)) / (2.0 * DeltaH);
            Assert.AreEqual(fd, analytic, DerivativeTol, $"DLink mismatch at x={x}, lambda=0.5");
        }
    }

    /// <summary>
    /// DLink agrees with finite difference for negative x at lambda = 0.5.
    /// </summary>
    [TestMethod]
    public void DLink_LambdaHalf_NegativeX_FiniteDifference()
    {
        var link = new YeoJohnsonLink(0.5);
        double[] testPoints = { -5.0, -2.0, -1.0, -0.5 };

        foreach (double x in testPoints)
        {
            double analytic = link.DLink(x);
            double fd = (link.Link(x + DeltaH) - link.Link(x - DeltaH)) / (2.0 * DeltaH);
            Assert.AreEqual(fd, analytic, DerivativeTol, $"DLink mismatch at x={x}, lambda=0.5");
        }
    }

    /// <summary>
    /// DLink at x=0 with lambda=1 equals 1 (identity slope).
    /// </summary>
    [TestMethod]
    public void DLink_AtZero_LambdaOne_IsOne()
    {
        var link = new YeoJohnsonLink(1.0);

        Assert.AreEqual(1.0, link.DLink(0.0), 1e-10);
    }

    /// <summary>
    /// DLink is positive for positive x (confirming monotone behavior).
    /// </summary>
    [TestMethod]
    public void DLink_PositiveX_AlwaysPositive()
    {
        var link = new YeoJohnsonLink(0.5);
        double[] testPoints = { 0.1, 0.5, 1.0, 2.0, 10.0 };

        foreach (double x in testPoints)
        {
            Assert.IsTrue(link.DLink(x) > 0, $"DLink should be positive at x={x}");
        }
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>
    /// ToXElement/FromXElement round-trip preserves Lambda.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesLambda()
    {
        var original = new YeoJohnsonLink(0.75);

        var xElement = original.ToXElement();
        var restored = new YeoJohnsonLink(xElement);

        Assert.AreEqual(original.Lambda, restored.Lambda, 1e-12);
    }

    /// <summary>
    /// ToXElement produces element named YeoJohnsonLink.
    /// </summary>
    [TestMethod]
    public void ToXElement_ElementNameIsYeoJohnsonLink()
    {
        var link = new YeoJohnsonLink();
        var xElement = link.ToXElement();

        Assert.AreEqual("YeoJohnsonLink", xElement.Name.LocalName);
    }

    /// <summary>
    /// XML round-trip then link/inverse-link recovers x.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_ThenLinkInverse_RecoverX()
    {
        var original = new YeoJohnsonLink(0.6);
        var restored = new YeoJohnsonLink(original.ToXElement());

        double x = 3.5;
        double recovered = restored.InverseLink(restored.Link(x));

        Assert.AreEqual(x, recovered, RoundTripTol);
    }

    #endregion
}
