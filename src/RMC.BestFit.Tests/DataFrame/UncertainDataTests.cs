using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Unit tests for the <c>UncertainData</c> class.
/// Tests uncertain data observations represented by probability distributions.
/// </summary>
/// <remarks>
/// UncertainData represents observations with full measurement error distributions,
/// allowing for sophisticated uncertainty propagation in Bayesian analysis.
/// This is particularly valuable for historical floods estimated from high-water marks.
/// </remarks>
[TestClass]
public class UncertainDataTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default instance.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultInstance()
    {
        var data = new UncertainData();

        Assert.IsNotNull(data);
        Assert.AreEqual(0, data.Index);
        Assert.IsNotNull(data.Distribution);
    }

    /// <summary>Verifies that constructor with index and distribution sets properties.</summary>
    [TestMethod]
    public void Test_Constructor_WithIndexAndDistribution_SetsProperties()
    {
        var dist = new Normal(75000, 5000);
        var data = new UncertainData(1985, dist);

        Assert.AreEqual(1985, data.Index);
        Assert.AreEqual(75000, data.Value, 1e-10);  // Mean of distribution
        Assert.AreSame(dist, data.Distribution);
    }

    /// <summary>Verifies that constructor with plotting position sets all properties.</summary>
    [TestMethod]
    public void Test_Constructor_WithPlottingPosition_SetsAllProperties()
    {
        var dist = new Normal(75000, 5000);
        var data = new UncertainData(1985, dist, 0.5);

        Assert.AreEqual(1985, data.Index);
        Assert.AreEqual(0.5, data.PlottingPosition);
    }

    /// <summary>Verifies that constructor X element restores all properties.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresAllProperties()
    {
        var dist = new Normal(75000, 5000);
        var original = new UncertainData(1985, dist, 0.5);
        var xElement = original.ToXElement();

        var restored = new UncertainData(xElement);

        Assert.AreEqual(1985, restored.Index);
        Assert.AreEqual(0.5, restored.PlottingPosition, 1e-10);
        Assert.IsInstanceOfType(restored.Distribution, typeof(Normal));
        Assert.AreEqual(75000, restored.Value, 1e-5);
    }

    #endregion

    #region Property Tests

    /// <summary>Verifies that value returns distribution mean.</summary>
    [TestMethod]
    public void Test_Value_ReturnsDistributionMean()
    {
        var dist = new Normal(100, 10);
        var data = new UncertainData(1985, dist);

        Assert.AreEqual(100, data.Value, 1e-10);
    }

    /// <summary>Verifies that value updates when distribution changes.</summary>
    [TestMethod]
    public void Test_Value_UpdatesWhenDistributionChanges()
    {
        var data = new UncertainData(1985, new Normal(100, 10));
        data.Distribution = new Normal(200, 15);

        Assert.AreEqual(200, data.Value, 1e-10);
    }

    /// <summary>Verifies that upper value returns95th percentile.</summary>
    [TestMethod]
    public void Test_UpperValue_Returns95thPercentile()
    {
        var dist = new Normal(100, 10);
        var data = new UncertainData(1985, dist);

        double expected = dist.InverseCDF(0.95);
        Assert.AreEqual(expected, data.UpperValue, 1e-10);
    }

    /// <summary>Verifies that lower value returns5th percentile.</summary>
    [TestMethod]
    public void Test_LowerValue_Returns5thPercentile()
    {
        var dist = new Normal(100, 10);
        var data = new UncertainData(1985, dist);

        double expected = dist.InverseCDF(0.05);
        Assert.AreEqual(expected, data.LowerValue, 1e-10);
    }

    /// <summary>Verifies that log10 lower value returns correct transform.</summary>
    [TestMethod]
    public void Test_Log10LowerValue_ReturnsCorrectTransform()
    {
        var dist = new Normal(10000, 1000);
        var data = new UncertainData(1985, dist);

        double expected = Math.Log10(data.LowerValue);
        Assert.AreEqual(expected, data.Log10LowerValue, 1e-10);
    }

    /// <summary>Verifies that log10 upper value returns correct transform.</summary>
    [TestMethod]
    public void Test_Log10UpperValue_ReturnsCorrectTransform()
    {
        var dist = new Normal(10000, 1000);
        var data = new UncertainData(1985, dist);

        double expected = Math.Log10(data.UpperValue);
        Assert.AreEqual(expected, data.Log10UpperValue, 1e-10);
    }

    #endregion

    #region Distribution Type Tests

    /// <summary>Verifies that distribution normal.</summary>
    [TestMethod]
    public void Test_Distribution_Normal()
    {
        var dist = new Normal(75000, 5000);
        var data = new UncertainData(1985, dist);

        Assert.IsInstanceOfType(data.Distribution, typeof(Normal));
        Assert.AreEqual(75000, data.Value, 1e-10);
    }

    /// <summary>Verifies that distribution log normal.</summary>
    [TestMethod]
    public void Test_Distribution_LogNormal()
    {
        // Log-Normal often better for flood magnitudes
        var dist = new LogNormal(11.2, 0.3);
        var data = new UncertainData(1985, dist);

        Assert.IsInstanceOfType(data.Distribution, typeof(LogNormal));
        Assert.AreEqual(dist.Mean, data.Value, 1e-5);
    }

    /// <summary>Verifies that distribution uniform.</summary>
    [TestMethod]
    public void Test_Distribution_Uniform()
    {
        var dist = new Uniform(50000, 100000);
        var data = new UncertainData(1985, dist);

        Assert.IsInstanceOfType(data.Distribution, typeof(Uniform));
        Assert.AreEqual(75000, data.Value, 1e-10);  // Uniform mean = (a+b)/2
    }

    /// <summary>Verifies that distribution triangular.</summary>
    [TestMethod]
    public void Test_Distribution_Triangular()
    {
        var dist = new Triangular(50000, 75000, 100000);
        var data = new UncertainData(1985, dist);

        Assert.IsInstanceOfType(data.Distribution, typeof(Triangular));
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>Verifies that property changed distribution.</summary>
    [TestMethod]
    public void Test_PropertyChanged_Distribution()
    {
        var data = new UncertainData(1985, new Normal(100, 10));
        string? changedProperty = null;
        data.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        data.Distribution = new Normal(200, 20);

        Assert.AreEqual(nameof(data.Distribution), changedProperty);
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns true when valid data.</summary>
    [TestMethod]
    public void Test_Validate_ValidData_ReturnsTrue()
    {
        var data = new UncertainData(1985, new Normal(75000, 5000));

        var (isValid, messages) = data.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate returns false when index out of range.</summary>
    [TestMethod]
    public void Test_Validate_IndexOutOfRange_ReturnsFalse()
    {
        var data = new UncertainData(-200000, new Normal(75000, 5000));

        var (isValid, messages) = data.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("index")));
    }

    /// <summary>Verifies that validate is valid when log normal with positive values.</summary>
    [TestMethod]
    public void Test_Validate_LogNormalWithPositiveValues_IsValid()
    {
        var data = new UncertainData(1985, new LogNormal(11, 0.5));

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new UncertainData(1985, new Normal(75000, 5000), 0.5);

        var clone = original.Clone();

        // Verify values match
        Assert.AreEqual(original.Index, clone.Index);
        Assert.AreEqual(original.Value, clone.Value, 1e-10);
        Assert.AreEqual(original.PlottingPosition, clone.PlottingPosition);

        // Verify distribution independence
        original.Distribution = new Normal(999999, 1);
        Assert.AreEqual(75000, clone.Value, 1e-10);
    }

    /// <summary>Verifies that clone preserves distribution type for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesDistributionType()
    {
        var original = new UncertainData(1985, new LogNormal(11.2, 0.3));

        var clone = original.Clone();

        Assert.IsInstanceOfType(clone.Distribution, typeof(LogNormal));
    }

    /// <summary>Verifies that clone preserves distribution parameters for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesDistributionParameters()
    {
        var original = new UncertainData(1985, new Normal(75000, 5000));

        var clone = original.Clone();

        Assert.AreEqual(original.LowerValue, clone.LowerValue, 1e-5);
        Assert.AreEqual(original.UpperValue, clone.UpperValue, 1e-5);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains all attributes.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAllAttributes()
    {
        var data = new UncertainData(1985, new Normal(75000, 5000), 0.5);

        var xElement = data.ToXElement();

        Assert.AreEqual("UncertainData", xElement.Name.LocalName);
        Assert.AreEqual("1985", xElement.Attribute("Index")?.Value);
        Assert.IsNotNull(xElement.Attribute("PlottingPosition"));
        Assert.IsNotNull(xElement.Element("Distribution"));
    }

    /// <summary>Verifies that round trip preserves normal distribution for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesNormalDistribution()
    {
        var original = new UncertainData(1985, new Normal(75000, 5000));

        var xElement = original.ToXElement();
        var restored = new UncertainData(xElement);

        Assert.IsInstanceOfType(restored.Distribution, typeof(Normal));
        Assert.AreEqual(original.Value, restored.Value, 1e-5);
        Assert.AreEqual(original.LowerValue, restored.LowerValue, 1e-3);
        Assert.AreEqual(original.UpperValue, restored.UpperValue, 1e-3);
    }

    /// <summary>Verifies that round trip preserves log normal distribution for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesLogNormalDistribution()
    {
        var original = new UncertainData(1985, new LogNormal(11.2, 0.3));

        var xElement = original.ToXElement();
        var restored = new UncertainData(xElement);

        Assert.IsInstanceOfType(restored.Distribution, typeof(LogNormal));
    }

    /// <summary>Verifies that round trip preserves uniform distribution for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesUniformDistribution()
    {
        var original = new UncertainData(1985, new Uniform(50000, 100000));

        var xElement = original.ToXElement();
        var restored = new UncertainData(xElement);

        Assert.IsInstanceOfType(restored.Distribution, typeof(Uniform));
    }

    #endregion

    #region Flood Frequency Scenarios

    /// <summary>Verifies that historical flood estimate.</summary>
    [TestMethod]
    public void Test_HistoricalFloodEstimate()
    {
        // Historical flood estimate with normal measurement error
        var dist = new Normal(350000, 50000);  // 350,000 ± 50,000 cfs
        var data = new UncertainData(1889, dist);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(350000, data.Value, 1e-10);
    }

    /// <summary>Verifies that rating curve uncertainty.</summary>
    [TestMethod]
    public void Test_RatingCurveUncertainty()
    {
        // Stage-discharge rating curve uncertainty
        // Heteroscedastic: larger flows have larger absolute errors
        var dist = new Normal(100000, 10000);  // 10% CV
        var data = new UncertainData(1993, dist);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that paleoflood estimate.</summary>
    [TestMethod]
    public void Test_PaleofloodEstimate()
    {
        // Paleoflood estimate with log-normal uncertainty
        // Log-normal often appropriate for multiplicative errors
        var dist = new LogNormal(12.5, 0.4);
        var data = new UncertainData(1500, dist);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that high water mark estimate.</summary>
    [TestMethod]
    public void Test_HighWaterMarkEstimate()
    {
        // High-water mark estimate with triangular uncertainty
        var dist = new Triangular(300000, 350000, 420000);
        var data = new UncertainData(1889, dist, 0.002);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that expert judgment estimate.</summary>
    [TestMethod]
    public void Test_ExpertJudgmentEstimate()
    {
        // Expert judgment: uniform over plausible range
        var dist = new Uniform(200000, 500000);
        var data = new UncertainData(1850, dist);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(350000, data.Value, 1e-10);  // Uniform mean
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that narrow uncertainty.</summary>
    [TestMethod]
    public void Test_NarrowUncertainty()
    {
        // Very low uncertainty (precise measurement)
        var dist = new Normal(75000, 100);
        var data = new UncertainData(1985, dist);

        double ciWidth = data.UpperValue - data.LowerValue;
        Assert.IsTrue(ciWidth < 400);  // 90% CI very narrow
    }

    /// <summary>Verifies that wide uncertainty.</summary>
    [TestMethod]
    public void Test_WideUncertainty()
    {
        // High uncertainty (poor estimate)
        var dist = new Normal(75000, 25000);
        var data = new UncertainData(1850, dist);

        double ciWidth = data.UpperValue - data.LowerValue;
        Assert.IsTrue(ciWidth > 80000);  // 90% CI very wide
    }

    /// <summary>Verifies that skewed distribution.</summary>
    [TestMethod]
    public void Test_SkewedDistribution()
    {
        // Skewed uncertainty (log-normal)
        var dist = new LogNormal(11, 0.5);
        var data = new UncertainData(1985, dist);

        // Log-normal: median < mean, upper tail heavier
        Assert.IsTrue(data.UpperValue - data.Value > data.Value - data.LowerValue);
    }

    /// <summary>Verifies that negative index.</summary>
    [TestMethod]
    public void Test_NegativeIndex()
    {
        // Ancient paleoflood
        var dist = new Normal(200000, 50000);
        var data = new UncertainData(-500, dist);

        var (isValid, _) = data.Validate();

        Assert.IsTrue(isValid);
    }

    #endregion
}
