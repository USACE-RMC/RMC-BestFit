namespace RMC.BestFit.Tests.InputDataFrame;

/// <summary>
/// Unit tests for the <see cref="IntervalData"/> class.
/// Tests interval-censored data used in flood frequency analysis.
/// </summary>
/// <remarks>
/// IntervalData represents observations with known bounds but uncertain exact value.
/// Common in paleoflood hydrology where slack-water deposits indicate flood levels
/// between a minimum non-inundation surface and maximum inundation elevation.
/// </remarks>
[TestClass]
public class IntervalDataTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default instance.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultInstance()
    {
        var data = new IntervalData();

        Assert.IsNotNull(data);
        Assert.AreEqual(0, data.Index);
        Assert.AreEqual(0.0, data.Value);
        Assert.AreEqual(0.0, data.LowerValue);
        Assert.AreEqual(0.0, data.UpperValue);
        Assert.AreEqual(0.0, data.PlottingPosition);
    }

    /// <summary>Verifies that constructor with parameters sets properties.</summary>
    [TestMethod]
    public void Test_Constructor_WithParameters_SetsProperties()
    {
        var data = new IntervalData(1850, 50000.0, 75000.0, 100000.0);

        Assert.AreEqual(1850, data.Index);
        Assert.AreEqual(50000.0, data.LowerValue);
        Assert.AreEqual(75000.0, data.Value);
        Assert.AreEqual(100000.0, data.UpperValue);
        Assert.AreEqual(0.0, data.PlottingPosition);
    }

    /// <summary>Verifies that constructor with plotting position sets all properties.</summary>
    [TestMethod]
    public void Test_Constructor_WithPlottingPosition_SetsAllProperties()
    {
        var data = new IntervalData(1850, 50000.0, 75000.0, 100000.0, 0.01);

        Assert.AreEqual(1850, data.Index);
        Assert.AreEqual(50000.0, data.LowerValue);
        Assert.AreEqual(75000.0, data.Value);
        Assert.AreEqual(100000.0, data.UpperValue);
        Assert.AreEqual(0.01, data.PlottingPosition);
    }

    /// <summary>Verifies that constructor X element restores all properties.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresAllProperties()
    {
        var original = new IntervalData(1850, 50000.0, 75000.0, 100000.0, 0.01);
        var xElement = original.ToXElement();

        var restored = new IntervalData(xElement);

        Assert.AreEqual(1850, restored.Index);
        Assert.AreEqual(50000.0, restored.LowerValue, 1e-10);
        Assert.AreEqual(75000.0, restored.Value, 1e-10);
        Assert.AreEqual(100000.0, restored.UpperValue, 1e-10);
        Assert.AreEqual(0.01, restored.PlottingPosition, 1e-10);
    }

    #endregion

    #region Property Tests

    /// <summary>Verifies that lower value set and get.</summary>
    [TestMethod]
    public void Test_LowerValue_SetAndGet()
    {
        var data = new IntervalData();
        data.LowerValue = 40000.0;

        Assert.AreEqual(40000.0, data.LowerValue);
    }

    /// <summary>Verifies that upper value set and get.</summary>
    [TestMethod]
    public void Test_UpperValue_SetAndGet()
    {
        var data = new IntervalData();
        data.UpperValue = 120000.0;

        Assert.AreEqual(120000.0, data.UpperValue);
    }

    /// <summary>Verifies that log10 lower value returns correct transform.</summary>
    [TestMethod]
    public void Test_Log10LowerValue_ReturnsCorrectTransform()
    {
        var data = new IntervalData(1850, 100.0, 1000.0, 10000.0);

        Assert.AreEqual(2.0, data.Log10LowerValue, 1e-10);  // log10(100)
    }

    /// <summary>Verifies that log10 upper value returns correct transform.</summary>
    [TestMethod]
    public void Test_Log10UpperValue_ReturnsCorrectTransform()
    {
        var data = new IntervalData(1850, 100.0, 1000.0, 10000.0);

        Assert.AreEqual(4.0, data.Log10UpperValue, 1e-10);  // log10(10000)
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>Verifies that property changed lower value.</summary>
    [TestMethod]
    public void Test_PropertyChanged_LowerValue()
    {
        var data = new IntervalData();
        string? changedProperty = null;
        data.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        data.LowerValue = 50000.0;

        Assert.AreEqual(nameof(data.LowerValue), changedProperty);
    }

    /// <summary>Verifies that property changed upper value.</summary>
    [TestMethod]
    public void Test_PropertyChanged_UpperValue()
    {
        var data = new IntervalData();
        string? changedProperty = null;
        data.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        data.UpperValue = 100000.0;

        Assert.AreEqual(nameof(data.UpperValue), changedProperty);
    }

    /// <summary>Verifies that property changed not fired when unchanged.</summary>
    [TestMethod]
    public void Test_PropertyChanged_NotFiredWhenUnchanged()
    {
        var data = new IntervalData { LowerValue = 50000.0 };
        int fireCount = 0;
        data.PropertyChanged += (s, e) => fireCount++;

        data.LowerValue = 50000.0;  // Same value

        Assert.AreEqual(0, fireCount);
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns true when valid data.</summary>
    [TestMethod]
    public void Test_Validate_ValidData_ReturnsTrue()
    {
        var data = new IntervalData(1850, 50000.0, 75000.0, 100000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate returns false when lower greater than value.</summary>
    [TestMethod]
    public void Test_Validate_LowerGreaterThanValue_ReturnsFalse()
    {
        var data = new IntervalData(1850, 80000.0, 75000.0, 100000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("lower") && m.Contains("less")));
    }

    /// <summary>Verifies that validate returns false when value greater than upper.</summary>
    [TestMethod]
    public void Test_Validate_ValueGreaterThanUpper_ReturnsFalse()
    {
        var data = new IntervalData(1850, 50000.0, 110000.0, 100000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("upper") && m.Contains("greater")));
    }

    /// <summary>Verifies that validate returns false when lower greater than upper.</summary>
    [TestMethod]
    public void Test_Validate_LowerGreaterThanUpper_ReturnsFalse()
    {
        var data = new IntervalData(1850, 100000.0, 75000.0, 50000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns false when na n lower value.</summary>
    [TestMethod]
    public void Test_Validate_NaNLowerValue_ReturnsFalse()
    {
        var data = new IntervalData(1850, double.NaN, 75000.0, 100000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("lower")));
    }

    /// <summary>Verifies that validate returns false when na n value.</summary>
    [TestMethod]
    public void Test_Validate_NaNValue_ReturnsFalse()
    {
        var data = new IntervalData(1850, 50000.0, double.NaN, 100000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("most likely")));
    }

    /// <summary>Verifies that validate returns false when na n upper value.</summary>
    [TestMethod]
    public void Test_Validate_NaNUpperValue_ReturnsFalse()
    {
        var data = new IntervalData(1850, 50000.0, 75000.0, double.NaN);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("upper")));
    }

    /// <summary>Verifies that validate returns false when infinity value.</summary>
    [TestMethod]
    public void Test_Validate_InfinityValue_ReturnsFalse()
    {
        var data = new IntervalData(1850, 50000.0, double.PositiveInfinity, 100000.0);

        var (isValid, _) = data.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns false when index out of range.</summary>
    [TestMethod]
    public void Test_Validate_IndexOutOfRange_ReturnsFalse()
    {
        var data = new IntervalData(-200000, 50000.0, 75000.0, 100000.0);

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("index")));
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new IntervalData(1850, 50000.0, 75000.0, 100000.0, 0.01);

        var clone = original.Clone();

        // Verify values match
        Assert.AreEqual(original.Index, clone.Index);
        Assert.AreEqual(original.LowerValue, clone.LowerValue);
        Assert.AreEqual(original.Value, clone.Value);
        Assert.AreEqual(original.UpperValue, clone.UpperValue);
        Assert.AreEqual(original.PlottingPosition, clone.PlottingPosition);

        // Verify independence
        original.LowerValue = 99999.0;
        Assert.AreEqual(50000.0, clone.LowerValue);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains all attributes.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAllAttributes()
    {
        var data = new IntervalData(1850, 50000.123, 75000.456, 100000.789, 0.01);

        var xElement = data.ToXElement();

        Assert.AreEqual("IntervalData", xElement.Name.LocalName);
        Assert.AreEqual("1850", xElement.Attribute("Index")?.Value);
        Assert.IsNotNull(xElement.Attribute("LowerValue"));
        Assert.IsNotNull(xElement.Attribute("Value"));
        Assert.IsNotNull(xElement.Attribute("UpperValue"));
        Assert.IsNotNull(xElement.Attribute("PlottingPosition"));
    }

    /// <summary>Verifies that round trip preserves high precision for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesHighPrecision()
    {
        var original = new IntervalData(1850, Math.PI * 10000, Math.E * 10000, Math.Sqrt(2) * 100000);

        var xElement = original.ToXElement();
        var restored = new IntervalData(xElement);

        Assert.AreEqual(original.LowerValue, restored.LowerValue, 1e-10);
        Assert.AreEqual(original.Value, restored.Value, 1e-10);
        Assert.AreEqual(original.UpperValue, restored.UpperValue, 1e-10);
    }

    #endregion

    #region Paleoflood Scenarios

    /// <summary>Verifies that slack water deposit.</summary>
    [TestMethod]
    public void Test_SlackWaterDeposit()
    {
        // Slack-water deposit: flood peaked between two elevations
        // Non-inundation surface at 50,000 cfs, deposit at 100,000 cfs
        var data = new IntervalData(1500, 50000.0, 75000.0, 100000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that historical high water mark.</summary>
    [TestMethod]
    public void Test_HistoricalHighWaterMark()
    {
        // Historical flood estimate with uncertainty
        var data = new IntervalData(1889, 300000.0, 350000.0, 400000.0, 0.002);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that rating curve uncertainty.</summary>
    [TestMethod]
    public void Test_RatingCurveUncertainty()
    {
        // Stage-discharge rating curve uncertainty
        var data = new IntervalData(1993, 330000.0, 350000.0, 370000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that non exceedance bound.</summary>
    [TestMethod]
    public void Test_NonExceedanceBound()
    {
        // Non-inundation bound from geomorphic evidence
        // Flood didn't exceed 500,000 cfs in past 1000 years
        var data = new IntervalData(1000, 0.0, 250000.0, 500000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that narrow interval.</summary>
    [TestMethod]
    public void Test_NarrowInterval()
    {
        // Very narrow interval (high confidence estimate)
        var data = new IntervalData(1993, 74000.0, 75000.0, 76000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that wide interval.</summary>
    [TestMethod]
    public void Test_WideInterval()
    {
        // Very wide interval (high uncertainty)
        var data = new IntervalData(1850, 10000.0, 100000.0, 1000000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that negative values temperature.</summary>
    [TestMethod]
    public void Test_NegativeValues_Temperature()
    {
        // Negative values (e.g., temperature extremes)
        var data = new IntervalData(1985, -50.0, -30.0, -10.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that asymmetric interval.</summary>
    [TestMethod]
    public void Test_AsymmetricInterval()
    {
        // Asymmetric interval (more uncertainty on one side)
        var data = new IntervalData(1850, 60000.0, 75000.0, 150000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that negative index ancient flood.</summary>
    [TestMethod]
    public void Test_NegativeIndex_AncientFlood()
    {
        // Ancient paleoflood (500 BCE)
        var data = new IntervalData(-500, 100000.0, 200000.0, 300000.0);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    #endregion
}
