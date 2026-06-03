namespace RMC.BestFit.Tests.InputDataFrame;

/// <summary>
/// Unit tests for the <see cref="ExactData"/> class.
/// Tests exact data observations used in flood frequency analysis.
/// </summary>
/// <remarks>
/// ExactData represents precisely measured observations (systematic records)
/// with known values. These are the most common data type in flood frequency analysis.
/// </remarks>
[TestClass]
public class ExactDataTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default instance.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultInstance()
    {
        var data = new ExactData();

        Assert.IsNotNull(data);
        Assert.AreEqual(0, data.Index);
        Assert.AreEqual(0.0, data.Value);
        Assert.AreEqual(0.0, data.PlottingPosition);
        Assert.IsFalse(data.IsLowOutlier);
    }

    /// <summary>Verifies that constructor with index and value sets properties.</summary>
    [TestMethod]
    public void Test_Constructor_WithIndexAndValue_SetsProperties()
    {
        var data = new ExactData(1985, 75000.0);

        Assert.AreEqual(1985, data.Index);
        Assert.AreEqual(75000.0, data.Value);
        Assert.AreEqual(0.0, data.PlottingPosition);
        Assert.IsFalse(data.IsLowOutlier);
    }

    /// <summary>Verifies that constructor with all parameters sets all properties.</summary>
    [TestMethod]
    public void Test_Constructor_WithAllParameters_SetsAllProperties()
    {
        var data = new ExactData(1985, 75000.0, 0.5, true);

        Assert.AreEqual(1985, data.Index);
        Assert.AreEqual(75000.0, data.Value);
        Assert.AreEqual(0.5, data.PlottingPosition);
        Assert.IsTrue(data.IsLowOutlier);
    }

    /// <summary>Verifies that constructor with date time sets index from year.</summary>
    [TestMethod]
    public void Test_Constructor_WithDateTime_SetsIndexFromYear()
    {
        var dateTime = new DateTime(1985, 6, 15);
        var data = new ExactData(dateTime, 75000.0);

        Assert.AreEqual(1985, data.Index);
        Assert.AreEqual(75000.0, data.Value);
        Assert.AreEqual(dateTime, data.DateTime);
    }

    /// <summary>Verifies that constructor X element restores all properties.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresAllProperties()
    {
        var original = new ExactData(1985, 75000.0, 0.5, true);
        var xElement = original.ToXElement();

        var restored = new ExactData(xElement);

        Assert.AreEqual(1985, restored.Index);
        Assert.AreEqual(75000.0, restored.Value, 1e-10);
        Assert.AreEqual(0.5, restored.PlottingPosition, 1e-10);
        Assert.IsTrue(restored.IsLowOutlier);
    }

    #endregion

    #region Property Tests

    /// <summary>Verifies that index set and get.</summary>
    [TestMethod]
    public void Test_Index_SetAndGet()
    {
        var data = new ExactData();
        data.Index = 2010;

        Assert.AreEqual(2010, data.Index);
    }

    /// <summary>Verifies that value set and get.</summary>
    [TestMethod]
    public void Test_Value_SetAndGet()
    {
        var data = new ExactData();
        data.Value = 50000.0;

        Assert.AreEqual(50000.0, data.Value);
    }

    /// <summary>Verifies that plotting position set and get.</summary>
    [TestMethod]
    public void Test_PlottingPosition_SetAndGet()
    {
        var data = new ExactData();
        data.PlottingPosition = 0.75;

        Assert.AreEqual(0.75, data.PlottingPosition);
    }

    /// <summary>Verifies that is low outlier set and get.</summary>
    [TestMethod]
    public void Test_IsLowOutlier_SetAndGet()
    {
        var data = new ExactData();
        data.IsLowOutlier = true;

        Assert.IsTrue(data.IsLowOutlier);
    }

    /// <summary>Verifies that date time preserved from constructor.</summary>
    [TestMethod]
    public void Test_DateTime_PreservedFromConstructor()
    {
        var dateTime = new DateTime(1997, 3, 2, 14, 30, 0);
        var data = new ExactData(dateTime, 100000.0);

        Assert.AreEqual(dateTime, data.DateTime);
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>Verifies that property changed is low outlier.</summary>
    [TestMethod]
    public void Test_PropertyChanged_IsLowOutlier()
    {
        var data = new ExactData();
        string? changedProperty = null;
        data.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        data.IsLowOutlier = true;

        Assert.AreEqual(nameof(data.IsLowOutlier), changedProperty);
    }

    /// <summary>Verifies that property changed not fired when unchanged.</summary>
    [TestMethod]
    public void Test_PropertyChanged_NotFiredWhenUnchanged()
    {
        var data = new ExactData { IsLowOutlier = true };
        int fireCount = 0;
        data.PropertyChanged += (s, e) => fireCount++;

        data.IsLowOutlier = true;  // Same value

        Assert.AreEqual(0, fireCount);
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns true when valid data.</summary>
    [TestMethod]
    public void Test_Validate_ValidData_ReturnsTrue()
    {
        var data = new ExactData(1985, 75000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate returns false when na n value.</summary>
    [TestMethod]
    public void Test_Validate_NaNValue_ReturnsFalse()
    {
        var data = new ExactData(1985, double.NaN);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("number")));
    }

    /// <summary>Verifies that validate returns false when infinity value.</summary>
    [TestMethod]
    public void Test_Validate_InfinityValue_ReturnsFalse()
    {
        var data = new ExactData(1985, double.PositiveInfinity);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns false when index too low.</summary>
    [TestMethod]
    public void Test_Validate_IndexTooLow_ReturnsFalse()
    {
        var data = new ExactData(-200000, 75000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("index")));
    }

    /// <summary>Verifies that validate returns false when index too high.</summary>
    [TestMethod]
    public void Test_Validate_IndexTooHigh_ReturnsFalse()
    {
        var data = new ExactData(200000, 75000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate is valid when negative value.</summary>
    [TestMethod]
    public void Test_Validate_NegativeValue_IsValid()
    {
        // Negative values are valid (e.g., temperature, elevation)
        var data = new ExactData(1985, -10.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that validate is valid when zero value.</summary>
    [TestMethod]
    public void Test_Validate_ZeroValue_IsValid()
    {
        var data = new ExactData(1985, 0.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new ExactData(1985, 75000.0, 0.5, true);

        var clone = original.Clone();

        // Verify values match
        Assert.AreEqual(original.Index, clone.Index);
        Assert.AreEqual(original.Value, clone.Value);
        Assert.AreEqual(original.PlottingPosition, clone.PlottingPosition);
        Assert.AreEqual(original.IsLowOutlier, clone.IsLowOutlier);

        // Verify independence
        original.IsLowOutlier = false;
        Assert.IsTrue(clone.IsLowOutlier);
    }

    /// <summary>Verifies that clone preserves date time for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesDateTime()
    {
        var dateTime = new DateTime(1997, 3, 2);
        var original = new ExactData(dateTime, 100000.0);

        var clone = original.Clone();

        Assert.AreEqual(dateTime, clone.DateTime);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains all attributes.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAllAttributes()
    {
        var data = new ExactData(1985, 75000.123456789, 0.5, true);

        var xElement = data.ToXElement();

        Assert.AreEqual("ExactData", xElement.Name.LocalName);
        Assert.AreEqual("1985", xElement.Attribute("Index")?.Value);
        Assert.IsNotNull(xElement.Attribute("Value"));
        Assert.IsNotNull(xElement.Attribute("PlottingPosition"));
        Assert.AreEqual("True", xElement.Attribute("IsLowOutlier")?.Value);
    }

    /// <summary>Verifies that round trip preserves high precision for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesHighPrecision()
    {
        var original = new ExactData(1985, Math.PI * 10000, Math.E / 10);

        var xElement = original.ToXElement();
        var restored = new ExactData(xElement);

        Assert.AreEqual(original.Value, restored.Value, 1e-10);
        Assert.AreEqual(original.PlottingPosition, restored.PlottingPosition, 1e-10);
    }

    /// <summary>Verifies that round trip preserves date time for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesDateTime()
    {
        var dateTime = new DateTime(1997, 3, 2, 14, 30, 45);
        var original = new ExactData(dateTime, 100000.0);

        var xElement = original.ToXElement();
        var restored = new ExactData(xElement);

        Assert.AreEqual(dateTime, restored.DateTime);
    }

    #endregion

    #region Flood Frequency Scenarios

    /// <summary>Verifies that flood peak flow.</summary>
    [TestMethod]
    public void Test_FloodPeakFlow()
    {
        // Typical flood peak flow observation
        var data = new ExactData(1993, 350000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(350000.0, data.Value);
    }

    /// <summary>Verifies that low outlier small flood.</summary>
    [TestMethod]
    public void Test_LowOutlier_SmallFlood()
    {
        // Low outlier (censored at perception threshold)
        var data = new ExactData(1988, 5000.0, isLowOutlier: true);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.IsTrue(data.IsLowOutlier);
    }

    /// <summary>Verifies that historical flood.</summary>
    [TestMethod]
    public void Test_HistoricalFlood()
    {
        // Historical flood from 1889
        var data = new ExactData(1889, 500000.0, 0.001);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(1889, data.Index);
    }

    /// <summary>Verifies that systematic record.</summary>
    [TestMethod]
    public void Test_SystematicRecord()
    {
        // Systematic record from modern gage
        var dateTime = new DateTime(2011, 5, 15);
        var data = new ExactData(dateTime, 125000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(2011, data.Index);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that very small value.</summary>
    [TestMethod]
    public void Test_VerySmallValue()
    {
        var data = new ExactData(1985, 1e-10);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(1e-10, data.Value);
    }

    /// <summary>Verifies that very large value.</summary>
    [TestMethod]
    public void Test_VeryLargeValue()
    {
        var data = new ExactData(1985, 1e15);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(1e15, data.Value);
    }

    /// <summary>Verifies that negative index paleoflood.</summary>
    [TestMethod]
    public void Test_NegativeIndex_Paleoflood()
    {
        // Very old paleoflood estimate (500 BCE)
        var data = new ExactData(-500, 200000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(-500, data.Index);
    }

    /// <summary>Verifies that plotting position range.</summary>
    [TestMethod]
    public void Test_PlottingPosition_Range()
    {
        var data = new ExactData(1985, 75000.0, 0.001);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(0.001, data.PlottingPosition);
    }

    #endregion
}
