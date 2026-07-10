using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Structural tests for the <see cref="PlotUpdateStatus"/> enum.
/// </summary>
/// <remarks>
/// These tests guard against accidental renaming or removal of enum members that are
/// referenced by name or ordinal position in the UI layer.
/// </remarks>
[TestClass]
public class PlotUpdateStatusTests
{
    /// <summary>
    /// Verifies that the <see cref="PlotUpdateStatus.Success"/> member exists with ordinal 0.
    /// </summary>
    [TestMethod]
    public void Success_HasOrdinalZero()
    {
        Assert.AreEqual(0, (int)PlotUpdateStatus.Success);
    }

    /// <summary>
    /// Verifies that the <see cref="PlotUpdateStatus.InsufficientData"/> member exists with ordinal 1.
    /// </summary>
    [TestMethod]
    public void InsufficientData_HasOrdinalOne()
    {
        Assert.AreEqual(1, (int)PlotUpdateStatus.InsufficientData);
    }

    /// <summary>
    /// Verifies that the <see cref="PlotUpdateStatus.MissingData"/> member exists with ordinal 2.
    /// </summary>
    [TestMethod]
    public void MissingData_HasOrdinalTwo()
    {
        Assert.AreEqual(2, (int)PlotUpdateStatus.MissingData);
    }

    /// <summary>
    /// Verifies that exactly three values are defined in the enum.
    /// </summary>
    [TestMethod]
    public void Enum_HasExactlyThreeMembers()
    {
        var values = Enum.GetValues<PlotUpdateStatus>();
        Assert.AreEqual(3, values.Length);
    }

    /// <summary>
    /// Verifies that switch expressions covering all defined members compile and produce the
    /// correct string — a basic exhaustiveness regression guard.
    /// </summary>
    [TestMethod]
    public void SwitchExpression_CoversAllMembers()
    {
        string Describe(PlotUpdateStatus s) => s switch
        {
            PlotUpdateStatus.Success => "ok",
            PlotUpdateStatus.InsufficientData => "insufficient",
            PlotUpdateStatus.MissingData => "missing",
            _ => "unknown"
        };

        Assert.AreEqual("ok", Describe(PlotUpdateStatus.Success));
        Assert.AreEqual("insufficient", Describe(PlotUpdateStatus.InsufficientData));
        Assert.AreEqual("missing", Describe(PlotUpdateStatus.MissingData));
    }
}
