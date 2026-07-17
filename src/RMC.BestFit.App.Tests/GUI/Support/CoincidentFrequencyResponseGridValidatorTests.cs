using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC_BestFit;
using System;

namespace RMC.BestFit.App.Tests.GUI.Support;

/// <summary>
/// Unit tests for <see cref="CoincidentFrequencyResponseGridValidator"/>.
/// </summary>
/// <remarks>
/// These tests pin the editable coincident-frequency response-grid validation rules
/// independently from WPF cell realization.
/// </remarks>
[TestClass]
public class CoincidentFrequencyResponseGridValidatorTests
{
    /// <summary>
    /// Verifies a finite, strictly increasing response surface is accepted.
    /// </summary>
    [TestMethod]
    public void ValidateCell_StrictSurface_ReturnsValid()
    {
        double?[,] response =
        {
            { 1.0, 2.0 },
            { 3.0, 4.0 },
        };

        var result = CoincidentFrequencyResponseGridValidator.ValidateCell(response, 0, 0);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(string.Empty, result.ToolTip);
    }

    /// <summary>
    /// Verifies blank cells are invalid rather than silently treated as zero.
    /// </summary>
    [TestMethod]
    public void ValidateCell_BlankCell_ReturnsInvalid()
    {
        double?[,] response =
        {
            { 1.0, 2.0 },
            { null, 4.0 },
        };

        var result = CoincidentFrequencyResponseGridValidator.ValidateCell(response, 1, 0);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.ToolTip, "required");
    }

    /// <summary>
    /// Verifies NaN cells are invalid.
    /// </summary>
    [TestMethod]
    public void ValidateCell_NaNCell_ReturnsInvalid()
    {
        double?[,] response =
        {
            { 1.0, 2.0 },
            { double.NaN, 4.0 },
        };

        var result = CoincidentFrequencyResponseGridValidator.ValidateCell(response, 1, 0);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.ToolTip, "finite");
    }

    /// <summary>
    /// Verifies cells that break strict increase along X are invalid.
    /// </summary>
    [TestMethod]
    public void ValidateCell_XMonotonicityViolation_ReturnsInvalid()
    {
        double?[,] response =
        {
            { 2.0, 3.0 },
            { 1.0, 4.0 },
        };

        var result = CoincidentFrequencyResponseGridValidator.ValidateCell(response, 1, 0);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.ToolTip, "previous X-row");
    }

    /// <summary>
    /// Verifies cells that break strict increase along Y are invalid.
    /// </summary>
    [TestMethod]
    public void ValidateCell_YMonotonicityViolation_ReturnsInvalid()
    {
        double?[,] response =
        {
            { 2.0, 1.0 },
            { 3.0, 4.0 },
        };

        var result = CoincidentFrequencyResponseGridValidator.ValidateCell(response, 0, 1);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.ToolTip, "previous Y-column");
    }

    /// <summary>
    /// Verifies DataTable blank values remain blank for validation.
    /// </summary>
    [TestMethod]
    public void ConvertCellValue_DBNull_ReturnsNull()
    {
        var result = CoincidentFrequencyResponseGridValidator.ConvertCellValue(DBNull.Value);

        Assert.IsNull(result);
    }
}
