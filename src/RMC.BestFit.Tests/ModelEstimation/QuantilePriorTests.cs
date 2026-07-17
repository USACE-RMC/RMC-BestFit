using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Unit tests for the <c>QuantilePrior</c> class.
/// Tests quantile-based prior specification for Bayesian flood frequency analysis.
/// </summary>
/// <remarks>
/// Quantile priors allow engineers to incorporate expert judgment about flood
/// quantiles (e.g., "the 100-year flood is likely between 50,000 and 100,000 cfs")
/// into Bayesian estimation.
/// </remarks>
[TestClass]
public class QuantilePriorTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default instance.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultInstance()
    {
        var prior = new QuantilePrior();

        Assert.IsNotNull(prior);
        Assert.IsNotNull(prior.Distribution);
    }

    /// <summary>Verifies that constructor with parameters sets values.</summary>
    [TestMethod]
    public void Test_Constructor_WithParameters_SetsValues()
    {
        double alpha = 0.01;  // 1% exceedance (100-year flood)
        var distribution = new Normal(75000, 10000);

        var prior = new QuantilePrior(alpha, distribution);

        Assert.AreEqual(0.01, prior.Alpha, 1e-10);
        Assert.IsInstanceOfType(prior.Distribution, typeof(Normal));
    }

    /// <summary>Verifies that constructor X element restores all properties.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresAllProperties()
    {
        var original = new QuantilePrior(0.01, new Normal(75000, 10000));
        var xElement = original.ToXElement();

        var restored = new QuantilePrior(xElement);

        Assert.AreEqual(0.01, restored.Alpha, 1e-10);
        Assert.IsInstanceOfType(restored.Distribution, typeof(Normal));
    }

    #endregion

    #region Property Tests

    /// <summary>Verifies that alpha set and get.</summary>
    [TestMethod]
    public void Test_Alpha_SetAndGet()
    {
        var prior = new QuantilePrior();
        prior.Alpha = 0.02;

        Assert.AreEqual(0.02, prior.Alpha, 1e-10);
    }

    /// <summary>Verifies that distribution set and get.</summary>
    [TestMethod]
    public void Test_Distribution_SetAndGet()
    {
        var prior = new QuantilePrior();
        var dist = new LogNormal(10, 0.5);
        prior.Distribution = dist;

        Assert.AreSame(dist, prior.Distribution);
    }

    /// <summary>Verifies that upper value returns95th percentile.</summary>
    [TestMethod]
    public void Test_UpperValue_Returns95thPercentile()
    {
        var dist = new Normal(100, 10);
        var prior = new QuantilePrior(0.01, dist);

        double expected = dist.InverseCDF(0.95);
        Assert.AreEqual(expected, prior.UpperValue, 1e-10);
    }

    /// <summary>Verifies that lower value returns5th percentile.</summary>
    [TestMethod]
    public void Test_LowerValue_Returns5thPercentile()
    {
        var dist = new Normal(100, 10);
        var prior = new QuantilePrior(0.01, dist);

        double expected = dist.InverseCDF(0.05);
        Assert.AreEqual(expected, prior.LowerValue, 1e-10);
    }

    /// <summary>Verifies that mean value returns mean.</summary>
    [TestMethod]
    public void Test_MeanValue_ReturnsMean()
    {
        var dist = new Normal(100, 10);
        var prior = new QuantilePrior(0.01, dist);

        Assert.AreEqual(100.0, prior.MeanValue, 1e-10);
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>Verifies that property changed alpha.</summary>
    [TestMethod]
    public void Test_PropertyChanged_Alpha()
    {
        var prior = new QuantilePrior();
        string? changedProperty = null;
        prior.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        prior.Alpha = 0.05;

        Assert.AreEqual(nameof(prior.Alpha), changedProperty);
    }

    /// <summary>Verifies that property changed distribution.</summary>
    [TestMethod]
    public void Test_PropertyChanged_Distribution()
    {
        var prior = new QuantilePrior();
        string? changedProperty = null;
        prior.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        prior.Distribution = new Normal(50, 5);

        Assert.AreEqual(nameof(prior.Distribution), changedProperty);
    }

    /// <summary>Verifies that property changed not fired when unchanged.</summary>
    [TestMethod]
    public void Test_PropertyChanged_NotFiredWhenUnchanged()
    {
        var prior = new QuantilePrior { Alpha = 0.01 };
        int fireCount = 0;
        prior.PropertyChanged += (s, e) => fireCount++;

        prior.Alpha = 0.01;  // Same value

        Assert.AreEqual(0, fireCount);
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns true when valid prior.</summary>
    [TestMethod]
    public void Test_Validate_ValidPrior_ReturnsTrue()
    {
        var prior = new QuantilePrior(0.01, new Normal(75000, 10000));

        var (isValid, messages) = prior.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate returns false when alpha zero.</summary>
    [TestMethod]
    public void Test_Validate_AlphaZero_ReturnsFalse()
    {
        var prior = new QuantilePrior(0.0, new Normal(75000, 10000));

        var (isValid, messages) = prior.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("alpha") || m.Contains("exceedance")));
    }

    /// <summary>Verifies that validate returns false when alpha one.</summary>
    [TestMethod]
    public void Test_Validate_AlphaOne_ReturnsFalse()
    {
        var prior = new QuantilePrior(1.0, new Normal(75000, 10000));

        var (isValid, messages) = prior.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns false when alpha negative.</summary>
    [TestMethod]
    public void Test_Validate_AlphaNegative_ReturnsFalse()
    {
        var prior = new QuantilePrior(-0.1, new Normal(75000, 10000));

        var (isValid, messages) = prior.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns false when alpha greater than one.</summary>
    [TestMethod]
    public void Test_Validate_AlphaGreaterThanOne_ReturnsFalse()
    {
        var prior = new QuantilePrior(1.5, new Normal(75000, 10000));

        var (isValid, messages) = prior.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns false when alpha na n.</summary>
    [TestMethod]
    public void Test_Validate_AlphaNaN_ReturnsFalse()
    {
        var prior = new QuantilePrior(double.NaN, new Normal(75000, 10000));

        var (isValid, messages) = prior.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns false when alpha infinity.</summary>
    [TestMethod]
    public void Test_Validate_AlphaInfinity_ReturnsFalse()
    {
        var prior = new QuantilePrior(double.PositiveInfinity, new Normal(75000, 10000));

        var (isValid, messages) = prior.Validate();

        Assert.IsFalse(isValid);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new QuantilePrior(0.01, new Normal(75000, 10000));

        var clone = original.Clone();

        // Verify values match
        Assert.AreEqual(original.Alpha, clone.Alpha);
        Assert.AreEqual(original.MeanValue, clone.MeanValue, 1e-10);

        // Verify independence
        original.Alpha = 0.99;
        Assert.AreEqual(0.01, clone.Alpha);
    }

    /// <summary>Verifies that clone preserves distribution type for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesDistributionType()
    {
        var original = new QuantilePrior(0.01, new LogNormal(10, 0.5));

        var clone = original.Clone();

        Assert.IsInstanceOfType(clone.Distribution, typeof(LogNormal));
    }

    /// <summary>Verifies that clone preserves distribution parameters for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesDistributionParameters()
    {
        var original = new QuantilePrior(0.01, new Normal(100, 15));

        var clone = original.Clone();

        Assert.AreEqual(original.LowerValue, clone.LowerValue, 1e-10);
        Assert.AreEqual(original.UpperValue, clone.UpperValue, 1e-10);
        Assert.AreEqual(original.MeanValue, clone.MeanValue, 1e-10);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains alpha attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAlphaAttribute()
    {
        var prior = new QuantilePrior(0.01, new Normal(75000, 10000));

        var xElement = prior.ToXElement();

        Assert.IsNotNull(xElement.Attribute("Alpha"));
        Assert.AreEqual("QuantilePrior", xElement.Name.LocalName);
    }

    /// <summary>Verifies that to X element contains distribution element.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsDistributionElement()
    {
        var prior = new QuantilePrior(0.01, new Normal(75000, 10000));

        var xElement = prior.ToXElement();

        Assert.IsNotNull(xElement.Element("Distribution"));
    }

    /// <summary>Verifies that round trip preserves high precision for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesHighPrecision()
    {
        var original = new QuantilePrior(Math.PI / 100, new Normal(Math.E * 10000, Math.Sqrt(2) * 1000));

        var xElement = original.ToXElement();
        var restored = new QuantilePrior(xElement);

        Assert.AreEqual(original.Alpha, restored.Alpha, 1e-10);
        Assert.AreEqual(original.MeanValue, restored.MeanValue, 1e-5);
    }

    /// <summary>Verifies that round trip with log normal distribution.</summary>
    [TestMethod]
    public void Test_RoundTrip_WithLogNormalDistribution()
    {
        var original = new QuantilePrior(0.01, new LogNormal(11.5, 0.3));

        var xElement = original.ToXElement();
        var restored = new QuantilePrior(xElement);

        Assert.AreEqual(original.LowerValue, restored.LowerValue, 1e-5);
        Assert.AreEqual(original.UpperValue, restored.UpperValue, 1e-5);
    }

    /// <summary>Verifies that round trip with uniform distribution.</summary>
    [TestMethod]
    public void Test_RoundTrip_WithUniformDistribution()
    {
        var original = new QuantilePrior(0.01, new Uniform(50000, 100000));

        var xElement = original.ToXElement();
        var restored = new QuantilePrior(xElement);

        Assert.AreEqual(original.LowerValue, restored.LowerValue, 1e-5);
        Assert.AreEqual(original.UpperValue, restored.UpperValue, 1e-5);
    }

    #endregion

    #region Engineering Application Tests

    /// <summary>Verifies that quantile prior 100 year flood.</summary>
    [TestMethod]
    public void Test_QuantilePrior_100YearFlood()
    {
        // Expert judgment: 100-year flood is most likely ~75,000 cfs
        // with 90% confidence interval [50,000, 100,000] cfs
        double alpha = 0.01;  // 1% AEP = 100-year
        var prior = new QuantilePrior(alpha, new Normal(75000, 15000));

        // 90% CI should span approximately the stated range
        double lower = prior.LowerValue;  // 5th percentile
        double upper = prior.UpperValue;  // 95th percentile

        Assert.IsTrue(lower > 40000 && lower < 60000);
        Assert.IsTrue(upper > 90000 && upper < 120000);
    }

    /// <summary>Verifies that quantile prior PMF.</summary>
    [TestMethod]
    public void Test_QuantilePrior_PMF()
    {
        // Probable Maximum Flood (PMF) - very rare event
        double alpha = 0.0001;  // 0.01% AEP = 10,000-year event
        var prior = new QuantilePrior(alpha, new LogNormal(12.5, 0.4));

        var (isValid, _) = prior.Validate();
        Assert.IsTrue(isValid);
        Assert.AreEqual(0.0001, prior.Alpha);
    }

    /// <summary>Verifies that quantile prior historical flood.</summary>
    [TestMethod]
    public void Test_QuantilePrior_HistoricalFlood()
    {
        // Historical flood from 1889: estimated ~350,000 cfs
        // with uncertainty represented as Normal prior
        double alpha = 0.001;  // Assumed rarity
        var prior = new QuantilePrior(alpha, new Normal(350000, 50000));

        Assert.IsTrue(prior.MeanValue > 300000);
        Assert.IsTrue(prior.LowerValue > 200000);
        Assert.IsTrue(prior.UpperValue < 500000);
    }

    /// <summary>Verifies that quantile prior regional information.</summary>
    [TestMethod]
    public void Test_QuantilePrior_RegionalInformation()
    {
        // Regional regression: 10-year flood ~ 25,000 cfs
        double alpha = 0.1;  // 10% AEP
        var prior = new QuantilePrior(alpha, new Normal(25000, 5000));

        var (isValid, _) = prior.Validate();
        Assert.IsTrue(isValid);
        Assert.AreEqual(0.1, prior.Alpha);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that quantile prior very small alpha.</summary>
    [TestMethod]
    public void Test_QuantilePrior_VerySmallAlpha()
    {
        // Extreme event: 1 in 1,000,000 year
        double alpha = 1e-6;
        var prior = new QuantilePrior(alpha, new Normal(1000000, 100000));

        var (isValid, _) = prior.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that quantile prior alpha near one.</summary>
    [TestMethod]
    public void Test_QuantilePrior_AlphaNearOne()
    {
        // Very frequent event: 99% AEP
        double alpha = 0.99;
        var prior = new QuantilePrior(alpha, new Normal(1000, 100));

        var (isValid, _) = prior.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that quantile prior wide prior distribution.</summary>
    [TestMethod]
    public void Test_QuantilePrior_WidePriorDistribution()
    {
        // Very uncertain prior
        var prior = new QuantilePrior(0.01, new Uniform(10000, 1000000));

        var (isValid, _) = prior.Validate();
        Assert.IsTrue(isValid);

        // Wide uncertainty should be reflected in LowerValue/UpperValue
        Assert.IsTrue(prior.UpperValue - prior.LowerValue > 800000);
    }

    /// <summary>Verifies that quantile prior narrow prior distribution.</summary>
    [TestMethod]
    public void Test_QuantilePrior_NarrowPriorDistribution()
    {
        // Very confident prior (precise historical measurement)
        var prior = new QuantilePrior(0.01, new Normal(75000, 500));

        // 90% CI should be very narrow
        double ciWidth = prior.UpperValue - prior.LowerValue;
        Assert.IsTrue(ciWidth < 2000);
    }

    #endregion

    #region Multiple Quantile Priors Tests

    /// <summary>Verifies that multiple quantile priors different return periods.</summary>
    [TestMethod]
    public void Test_MultipleQuantilePriors_DifferentReturnPeriods()
    {
        // Multiple priors for different quantiles
        var prior10yr = new QuantilePrior(0.1, new Normal(30000, 5000));
        var prior100yr = new QuantilePrior(0.01, new Normal(75000, 15000));
        var prior500yr = new QuantilePrior(0.002, new Normal(120000, 30000));

        // All should be valid
        Assert.IsTrue(prior10yr.Validate().IsValid);
        Assert.IsTrue(prior100yr.Validate().IsValid);
        Assert.IsTrue(prior500yr.Validate().IsValid);

        // Mean values should increase with decreasing AEP
        Assert.IsTrue(prior10yr.MeanValue < prior100yr.MeanValue);
        Assert.IsTrue(prior100yr.MeanValue < prior500yr.MeanValue);
    }

    #endregion
}
