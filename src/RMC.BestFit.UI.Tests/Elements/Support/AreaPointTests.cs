using Microsoft.VisualStudio.TestTools.UnitTesting;
using OxyPlot;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for the <see cref="AreaPoint"/> POCO class.
/// </summary>
/// <remarks>
/// <see cref="AreaPoint"/> wraps two <see cref="DataPoint"/> instances to define a rectangular
/// area on an OxyPlot AreaSeries. Tests verify that the constructor correctly maps
/// DataPoint coordinates to X1, X2, Y1, Y2 properties.
/// </remarks>
[TestClass]
public class AreaPointTests
{
    /// <summary>
    /// Verifies that the constructor maps p1.X to X1 and p2.X to X2.
    /// </summary>
    [TestMethod]
    public void Constructor_MapsX1FromP1AndX2FromP2()
    {
        var p1 = new DataPoint(1.0, 3.0);
        var p2 = new DataPoint(2.0, 4.0);

        var area = new AreaPoint(p1, p2);

        Assert.AreEqual(1.0, area.X1, 1e-12);
        Assert.AreEqual(2.0, area.X2, 1e-12);
    }

    /// <summary>
    /// Verifies that the constructor maps p1.Y to Y1 and p2.Y to Y2.
    /// </summary>
    [TestMethod]
    public void Constructor_MapsY1FromP1AndY2FromP2()
    {
        var p1 = new DataPoint(1.0, 3.0);
        var p2 = new DataPoint(2.0, 4.0);

        var area = new AreaPoint(p1, p2);

        Assert.AreEqual(3.0, area.Y1, 1e-12);
        Assert.AreEqual(4.0, area.Y2, 1e-12);
    }

    /// <summary>
    /// Verifies that X1, X2, Y1, Y2 can be overwritten via property setters after construction.
    /// </summary>
    [TestMethod]
    public void PropertySetters_UpdateValues()
    {
        var area = new AreaPoint(new DataPoint(0, 0), new DataPoint(0, 0));

        area.X1 = 10.5;
        area.X2 = 20.5;
        area.Y1 = 30.5;
        area.Y2 = 40.5;

        Assert.AreEqual(10.5, area.X1, 1e-12);
        Assert.AreEqual(20.5, area.X2, 1e-12);
        Assert.AreEqual(30.5, area.Y1, 1e-12);
        Assert.AreEqual(40.5, area.Y2, 1e-12);
    }

    /// <summary>
    /// Verifies that negative coordinate values are stored correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_NegativeCoordinates_StoredCorrectly()
    {
        var p1 = new DataPoint(-5.0, -10.0);
        var p2 = new DataPoint(-3.0, -7.5);

        var area = new AreaPoint(p1, p2);

        Assert.AreEqual(-5.0, area.X1, 1e-12);
        Assert.AreEqual(-3.0, area.X2, 1e-12);
        Assert.AreEqual(-10.0, area.Y1, 1e-12);
        Assert.AreEqual(-7.5, area.Y2, 1e-12);
    }

    /// <summary>
    /// Verifies that zero coordinates are stored and returned as zero, not rounded or normalized.
    /// </summary>
    [TestMethod]
    public void Constructor_ZeroCoordinates_StoredAsZero()
    {
        var area = new AreaPoint(DataPoint.Undefined, DataPoint.Undefined);

        // DataPoint.Undefined has NaN coordinates
        Assert.IsTrue(double.IsNaN(area.X1));
        Assert.IsTrue(double.IsNaN(area.X2));
        Assert.IsTrue(double.IsNaN(area.Y1));
        Assert.IsTrue(double.IsNaN(area.Y2));
    }

    /// <summary>
    /// Verifies that identical p1 and p2 values produce equal X1/X2 and Y1/Y2 pairs
    /// (degenerate zero-area point).
    /// </summary>
    [TestMethod]
    public void Constructor_IdenticalPoints_ProducesZeroArea()
    {
        var pt = new DataPoint(7.0, 8.0);
        var area = new AreaPoint(pt, pt);

        Assert.AreEqual(area.X1, area.X2, 1e-12);
        Assert.AreEqual(area.Y1, area.Y2, 1e-12);
    }

    /// <summary>
    /// Verifies that extreme double values (double.MaxValue) are stored without overflow.
    /// </summary>
    [TestMethod]
    public void Constructor_ExtremeValues_StoredCorrectly()
    {
        var p1 = new DataPoint(double.MaxValue, double.MinValue);
        var p2 = new DataPoint(double.Epsilon, double.NegativeInfinity);

        var area = new AreaPoint(p1, p2);

        Assert.AreEqual(double.MaxValue, area.X1);
        Assert.AreEqual(double.Epsilon, area.X2);
        Assert.AreEqual(double.MinValue, area.Y1);
        Assert.IsTrue(double.IsNegativeInfinity(area.Y2));
    }
}
