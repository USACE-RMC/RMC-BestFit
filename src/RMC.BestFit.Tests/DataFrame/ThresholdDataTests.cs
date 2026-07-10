using System.Globalization;
using System.Xml.Linq;
using BestFitThresholdData = RMC.BestFit.Models.ThresholdData;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Unit tests for the <c>ThresholdData</c> class.
/// Tests historical perception thresholds essential for flood frequency analysis.
/// </summary>
/// <remarks>
/// <para>
/// <c>ThresholdData.NumberBelow</c> has an <c>internal</c> setter because it is a
/// derived value managed by <c>DataFrame.ProcessThresholdSeries</c>. Tests that
/// need to construct a <c>ThresholdData</c> with a specific <c>NumberBelow</c>
/// (for round-trip / clone / validation coverage in isolation) use the
/// <c>CreateThresholdDataForTesting</c> helper, which builds an XElement and
/// deserializes through the XML constructor — that path populates the private backing
/// field directly, avoiding the setter.
/// </para>
/// </remarks>
[TestClass]
public class ThresholdDataTests
{
    #region Test Helpers

    /// <summary>
    /// Constructs a <c>ThresholdData</c> with specific count fields by building an
    /// XElement and round-tripping through the XML constructor. Used by tests that
    /// exercise state-space scenarios in isolation, where <c>NumberBelow</c> cannot be
    /// set via the property setter (it is <c>internal</c>).
    /// </summary>
    /// <param name="startIndex">Start index of the threshold window.</param>
    /// <param name="endIndex">End index of the threshold window.</param>
    /// <param name="value">Threshold value.</param>
    /// <param name="numberAbove">Number of observations above the threshold.</param>
    /// <param name="numberBelow">Number of observations below the threshold.</param>
    /// <param name="plottingPosition">Plotting position for the threshold.</param>
    /// <returns>A <c>ThresholdData</c> with the specified fields.</returns>
    private static BestFitThresholdData CreateThresholdDataForTesting(
        int startIndex, int endIndex, double value,
        int numberAbove = 0, int numberBelow = 0, double plottingPosition = 0.0)
    {
        var xElement = new XElement("ThresholdData",
            new XAttribute("StartIndex", startIndex.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("EndIndex", endIndex.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("Value", value.ToString("G17", CultureInfo.InvariantCulture)),
            new XAttribute("NumberBelow", numberBelow.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("NumberAbove", numberAbove.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("PlottingPosition", plottingPosition.ToString("G17", CultureInfo.InvariantCulture)));
        return new BestFitThresholdData(xElement);
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Verifies the parameterless constructor produces a default instance with zeroed fields.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultInstance()
    {
        // Act
        var threshold = new BestFitThresholdData();

        // Assert
        Assert.IsNotNull(threshold);
        Assert.AreEqual(0, threshold.StartIndex);
        Assert.AreEqual(0, threshold.EndIndex);
        Assert.AreEqual(0.0, threshold.Value);
        Assert.AreEqual(0, threshold.NumberBelow);
        Assert.AreEqual(0, threshold.NumberAbove);
    }

    /// <summary>
    /// Verifies the three-argument constructor populates StartIndex, EndIndex, and Value.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithParameters_SetsValues()
    {
        // Act
        var threshold = new BestFitThresholdData(1900, 1950, 100000.0);

        // Assert
        Assert.AreEqual(1900, threshold.StartIndex);
        Assert.AreEqual(1950, threshold.EndIndex);
        Assert.AreEqual(100000.0, threshold.Value);
    }

    /// <summary>
    /// Verifies the XElement constructor restores every persisted property, including the
    /// derived <c>NumberBelow</c> (read directly from the XML attribute into the backing field).
    /// </summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresAllProperties()
    {
        // Arrange — build the XElement directly, bypassing the (internal) NumberBelow setter.
        var xElement = new XElement("ThresholdData",
            new XAttribute("StartIndex", "1900"),
            new XAttribute("EndIndex", "1950"),
            new XAttribute("Value", "75000"),
            new XAttribute("NumberBelow", "45"),
            new XAttribute("NumberAbove", "6"),
            new XAttribute("PlottingPosition", "0.882"));

        // Act
        var restored = new BestFitThresholdData(xElement);

        // Assert
        Assert.AreEqual(1900, restored.StartIndex);
        Assert.AreEqual(1950, restored.EndIndex);
        Assert.AreEqual(75000.0, restored.Value, 1e-10);
        Assert.AreEqual(45, restored.NumberBelow);
        Assert.AreEqual(6, restored.NumberAbove);
        Assert.AreEqual(0.882, restored.PlottingPosition, 1e-10);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Verifies StartIndex setter stores and returns the assigned value.
    /// </summary>
    [TestMethod]
    public void Test_StartIndex_SetAndGet()
    {
        var threshold = new BestFitThresholdData();
        threshold.StartIndex = 1920;

        Assert.AreEqual(1920, threshold.StartIndex);
    }

    /// <summary>
    /// Verifies EndIndex setter stores and returns the assigned value.
    /// </summary>
    [TestMethod]
    public void Test_EndIndex_SetAndGet()
    {
        var threshold = new BestFitThresholdData();
        threshold.EndIndex = 1980;

        Assert.AreEqual(1980, threshold.EndIndex);
    }

    /// <summary>
    /// Verifies Value setter stores and returns the assigned value.
    /// </summary>
    [TestMethod]
    public void Test_Value_SetAndGet()
    {
        var threshold = new BestFitThresholdData();
        threshold.Value = 50000.0;

        Assert.AreEqual(50000.0, threshold.Value);
    }

    /// <summary>
    /// Verifies NumberAbove setter stores and returns the assigned value.
    /// </summary>
    [TestMethod]
    public void Test_NumberAbove_SetAndGet()
    {
        var threshold = new BestFitThresholdData();
        threshold.NumberAbove = 5;

        Assert.AreEqual(5, threshold.NumberAbove);
    }

    /// <summary>
    /// Verifies Duration is computed as EndIndex − StartIndex + 1.
    /// </summary>
    [TestMethod]
    public void Test_Duration_ComputedCorrectly()
    {
        var threshold = new BestFitThresholdData(1900, 1950, 100000.0);

        // Duration = EndIndex - StartIndex + 1 = 1950 - 1900 + 1 = 51
        Assert.AreEqual(51, threshold.Duration);
    }

    /// <summary>
    /// Verifies Duration returns 1 when StartIndex == EndIndex.
    /// </summary>
    [TestMethod]
    public void Test_Duration_SingleYear()
    {
        var threshold = new BestFitThresholdData(1950, 1950, 100000.0);

        Assert.AreEqual(1, threshold.Duration);
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>
    /// Verifies that changing StartIndex raises PropertyChanged for that property name.
    /// </summary>
    [TestMethod]
    public void Test_PropertyChanged_StartIndex()
    {
        var threshold = new BestFitThresholdData();
        string? changedProperty = null;
        threshold.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        threshold.StartIndex = 1900;

        Assert.AreEqual(nameof(threshold.StartIndex), changedProperty);
    }

    /// <summary>
    /// Verifies that changing EndIndex raises PropertyChanged for that property name.
    /// </summary>
    [TestMethod]
    public void Test_PropertyChanged_EndIndex()
    {
        var threshold = new BestFitThresholdData();
        string? changedProperty = null;
        threshold.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        threshold.EndIndex = 1950;

        Assert.AreEqual(nameof(threshold.EndIndex), changedProperty);
    }

    /// <summary>
    /// Verifies that changing NumberAbove raises PropertyChanged for that property name.
    /// </summary>
    [TestMethod]
    public void Test_PropertyChanged_NumberAbove()
    {
        var threshold = new BestFitThresholdData();
        string? changedProperty = null;
        threshold.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        threshold.NumberAbove = 10;

        Assert.AreEqual(nameof(threshold.NumberAbove), changedProperty);
    }

    /// <summary>
    /// Verifies PropertyChanged is suppressed when a property is assigned its current value.
    /// </summary>
    [TestMethod]
    public void Test_PropertyChanged_NotFiredWhenUnchanged()
    {
        var threshold = new BestFitThresholdData(1900, 1950, 100000.0);
        int fireCount = 0;
        threshold.PropertyChanged += (s, e) => fireCount++;

        threshold.StartIndex = 1900; // Same value

        Assert.AreEqual(0, fireCount);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Verifies that a threshold with a user-set NumberAbove within Duration validates as true.
    /// </summary>
    /// <remarks>
    /// NumberBelow defaults to 0 for an isolated BestFitThresholdData; the sum 0 + 6 = 6 is ≤ 51
    /// (the Duration), so the threshold passes all validation rules. In a BestFitDataFrame context,
    /// <c>DataFrame.ProcessThresholdSeries</c> would populate NumberBelow to its derived
    /// value before downstream logic consumed the threshold.
    /// </remarks>
    [TestMethod]
    public void Test_Validate_ValidThreshold_ReturnsTrue()
    {
        var threshold = new BestFitThresholdData(1900, 1950, 100000.0)
        {
            NumberAbove = 6
        };

        var (isValid, messages) = threshold.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>
    /// Verifies that a threshold with StartIndex greater than EndIndex is rejected.
    /// </summary>
    [TestMethod]
    public void Test_Validate_StartAfterEnd_ReturnsFalse()
    {
        var threshold = new BestFitThresholdData(1950, 1900, 100000.0);

        var (isValid, messages) = threshold.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("start index")));
    }

    /// <summary>
    /// Verifies that a threshold with a negative NumberAbove is rejected by validation.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NegativeNumberAbove_ReturnsFalse()
    {
        var threshold = new BestFitThresholdData(1900, 1950, 100000.0)
        {
            NumberAbove = -3
        };

        var (isValid, messages) = threshold.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("number above")));
    }

    /// <summary>
    /// Verifies that a threshold where NumberAbove exceeds Duration is rejected.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NumberAboveExceedsDuration_ReturnsFalse()
    {
        var threshold = new BestFitThresholdData(1900, 1950, 100000.0)
        {
            NumberAbove = 100 // Duration is 51
        };

        var (isValid, messages) = threshold.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("number above")));
    }

    /// <summary>
    /// Verifies that a NaN threshold value is rejected.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NaNValue_ReturnsFalse()
    {
        var threshold = new BestFitThresholdData(1900, 1950, double.NaN);

        var (isValid, messages) = threshold.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("number")));
    }

    /// <summary>
    /// Verifies that an infinite threshold value is rejected.
    /// </summary>
    [TestMethod]
    public void Test_Validate_InfinityValue_ReturnsFalse()
    {
        var threshold = new BestFitThresholdData(1900, 1950, double.PositiveInfinity);

        var (isValid, messages) = threshold.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Verifies that an index outside the valid range is rejected.
    /// </summary>
    [TestMethod]
    public void Test_Validate_IndexOutOfRange_ReturnsFalse()
    {
        var threshold = new BestFitThresholdData(-200000, 1950, 100000.0);

        var (isValid, messages) = threshold.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("index")));
    }

    #endregion

    #region Clone Tests

    /// <summary>
    /// Verifies <c>ThresholdData.Clone</c> copies every field and yields an independent instance.
    /// </summary>
    /// <remarks>
    /// Source is constructed via the XElement helper because NumberBelow has an internal
    /// setter — Clone preserves both the user-set NumberAbove and the derived NumberBelow,
    /// and the two instances must be independent on subsequent mutation.
    /// </remarks>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = CreateThresholdDataForTesting(
            1900, 1950, 75000.0, numberAbove: 6, numberBelow: 45, plottingPosition: 0.882);

        var clone = original.Clone();

        // Verify values match
        Assert.AreEqual(original.StartIndex, clone.StartIndex);
        Assert.AreEqual(original.EndIndex, clone.EndIndex);
        Assert.AreEqual(original.Value, clone.Value);
        Assert.AreEqual(original.NumberBelow, clone.NumberBelow);
        Assert.AreEqual(original.NumberAbove, clone.NumberAbove);
        Assert.AreEqual(original.PlottingPosition, clone.PlottingPosition);

        // Verify independence: mutating the original must not affect the clone.
        original.NumberAbove = 99;
        Assert.AreEqual(6, clone.NumberAbove);
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Verifies <c>ThresholdData.ToXElement</c> writes every persisted field as an attribute.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAllAttributes()
    {
        var threshold = CreateThresholdDataForTesting(
            1900, 1950, 75000.123456789, numberAbove: 6, numberBelow: 45, plottingPosition: 0.882);

        var xElement = threshold.ToXElement();

        Assert.AreEqual("ThresholdData", xElement.Name.LocalName);
        Assert.AreEqual("1900", xElement.Attribute("StartIndex")?.Value);
        Assert.AreEqual("1950", xElement.Attribute("EndIndex")?.Value);
        Assert.IsNotNull(xElement.Attribute("Value"));
        Assert.AreEqual("45", xElement.Attribute("NumberBelow")?.Value);
        Assert.AreEqual("6", xElement.Attribute("NumberAbove")?.Value);
        Assert.IsNotNull(xElement.Attribute("PlottingPosition"));
    }

    /// <summary>
    /// Verifies Value and PlottingPosition survive an XML round-trip without loss of precision.
    /// </summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesHighPrecision()
    {
        var original = new BestFitThresholdData(1900, 1950, Math.PI * 10000);
        original.PlottingPosition = Math.E / 10;

        var xElement = original.ToXElement();
        var restored = new BestFitThresholdData(xElement);

        Assert.AreEqual(original.Value, restored.Value, 1e-10);
        Assert.AreEqual(original.PlottingPosition, restored.PlottingPosition, 1e-10);
    }

    /// <summary>
    /// Verifies round-trip of the default all-zero count state.
    /// </summary>
    [TestMethod]
    public void Test_RoundTrip_AllZeroCounts()
    {
        var original = new BestFitThresholdData(2000, 2020, 50000.0)
        {
            NumberAbove = 0
        };

        var xElement = original.ToXElement();
        var restored = new BestFitThresholdData(xElement);

        Assert.AreEqual(0, restored.NumberBelow);
        Assert.AreEqual(0, restored.NumberAbove);
    }

    #endregion

    #region Historical Flood Analysis Scenarios

    /// <summary>
    /// A typical 90-year historical period with 3 known floods above the perception threshold.
    /// </summary>
    /// <remarks>
    /// Source BestFitThresholdData is constructed via the XElement helper because NumberBelow is
    /// a derived field (the user would only set NumberAbove in normal usage; NumberBelow
    /// would be populated by <c>DataFrame.ProcessThresholdSeries</c>). The scenario
    /// is valid: 87 + 3 = 90 matches Duration exactly.
    /// </remarks>
    [TestMethod]
    public void Test_HistoricalPeriod_TypicalScenario()
    {
        // Typical historical period: 1850-1939, perception threshold 50,000 cfs,
        // 3 known floods exceeded threshold, 87 years below.
        var threshold = CreateThresholdDataForTesting(
            1850, 1939, 50000.0, numberAbove: 3, numberBelow: 87);

        var (isValid, messages) = threshold.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
        Assert.AreEqual(90, threshold.Duration);
    }

    /// <summary>
    /// A historical period where zero floods exceeded the perception threshold.
    /// </summary>
    /// <remarks>
    /// NumberAbove = 0 is explicit; NumberBelow defaults to 0 for an isolated BestFitThresholdData.
    /// The validation only requires that counts are non-negative and their sum does not
    /// exceed Duration — 0 + 0 ≤ 51 passes. BestFitDataFrame would derive NumberBelow = 51 in a
    /// live context.
    /// </remarks>
    [TestMethod]
    public void Test_HistoricalPeriod_NoExceedances()
    {
        var threshold = new BestFitThresholdData(1900, 1950, 100000.0)
        {
            NumberAbove = 0
        };

        var (isValid, _) = threshold.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// A historical period where every year exceeded the threshold (very low threshold).
    /// </summary>
    [TestMethod]
    public void Test_HistoricalPeriod_AllExceedances()
    {
        var threshold = new BestFitThresholdData(1900, 1909, 1000.0)
        {
            NumberAbove = 10
        };

        var (isValid, _) = threshold.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(10, threshold.Duration);
    }

    /// <summary>
    /// A 500-year paleoflood record with 4 paleofloods.
    /// </summary>
    [TestMethod]
    public void Test_HistoricalPeriod_PaleofloodRecord()
    {
        var threshold = CreateThresholdDataForTesting(
            1500, 1999, 200000.0, numberAbove: 4, numberBelow: 496);

        var (isValid, _) = threshold.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(500, threshold.Duration);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Verifies negative indices (e.g., BCE years) are permitted.
    /// </summary>
    [TestMethod]
    public void Test_NegativeIndices_Allowed()
    {
        // Years before common era could use negative indices
        var threshold = new BestFitThresholdData(-500, -400, 100000.0)
        {
            NumberAbove = 2
        };

        var (isValid, _) = threshold.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(101, threshold.Duration);
    }

    /// <summary>
    /// Verifies very large threshold values (1e12) are handled.
    /// </summary>
    [TestMethod]
    public void Test_LargeValue()
    {
        var threshold = new BestFitThresholdData(1900, 1950, 1e12); // 1 trillion

        var (isValid, _) = threshold.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(1e12, threshold.Value);
    }

    /// <summary>
    /// Verifies very small threshold values (0.001) are handled.
    /// </summary>
    [TestMethod]
    public void Test_SmallValue()
    {
        var threshold = new BestFitThresholdData(1900, 1950, 0.001);

        var (isValid, _) = threshold.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(0.001, threshold.Value);
    }

    /// <summary>
    /// Verifies zero threshold values are permitted.
    /// </summary>
    [TestMethod]
    public void Test_ZeroValue()
    {
        var threshold = new BestFitThresholdData(1900, 1950, 0.0);

        var (isValid, _) = threshold.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Verifies negative threshold values (e.g., temperatures, elevations) are permitted.
    /// </summary>
    [TestMethod]
    public void Test_NegativeValue()
    {
        // Negative values could represent temperatures or elevations
        var threshold = new BestFitThresholdData(1900, 1950, -50.0);

        var (isValid, _) = threshold.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(-50.0, threshold.Value);
    }

    #endregion
}
