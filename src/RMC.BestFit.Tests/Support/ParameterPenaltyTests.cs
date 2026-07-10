using RMC.BestFit.Models;
using System.ComponentModel;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.Support;

/// <summary>
/// Unit tests for the <c>ParameterPenalty</c> class.
/// </summary>
/// <remarks>
/// Covers: default construction, XML round-trip, <c>IsValid</c>, <c>Validate()</c>,
/// <c>Function()</c> in real and log space, <c>Clone()</c>, <c>UpperValue</c>/<c>LowerValue</c>,
/// and <c>INotifyPropertyChanged</c> event firing.
/// </remarks>
[TestClass]
public class ParameterPenaltyTests
{
    #region Constructor Tests

    /// <summary>
    /// Default constructor creates a penalty with NaN Mean, NaN MSE, and disabled state.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_SetsExpectedDefaults()
    {
        var penalty = new ParameterPenalty();

        Assert.IsFalse(penalty.Enabled);
        Assert.IsTrue(double.IsNaN(penalty.Mean));
        Assert.IsTrue(double.IsNaN(penalty.MSE));
        Assert.AreEqual(string.Empty, penalty.Name);
        Assert.IsFalse(penalty.UseLog);
    }

    /// <summary>
    /// XElement constructor throws ArgumentNullException for null input.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_XElement_Null_ThrowsArgumentNullException()
    {
        _ = new ParameterPenalty((XElement)null!);
    }

    #endregion

    #region IsValid Tests

    /// <summary>
    /// IsValid is false when Mean is NaN.
    /// </summary>
    [TestMethod]
    public void IsValid_MeanNaN_ReturnsFalse()
    {
        var penalty = new ParameterPenalty { Mean = double.NaN, MSE = 0.1 };

        Assert.IsFalse(penalty.IsValid);
    }

    /// <summary>
    /// IsValid is false when MSE is zero.
    /// </summary>
    [TestMethod]
    public void IsValid_MSEZero_ReturnsFalse()
    {
        var penalty = new ParameterPenalty { Mean = 1.0, MSE = 0.0 };

        Assert.IsFalse(penalty.IsValid);
    }

    /// <summary>
    /// IsValid is false when MSE is negative.
    /// </summary>
    [TestMethod]
    public void IsValid_MSENegative_ReturnsFalse()
    {
        var penalty = new ParameterPenalty { Mean = 1.0, MSE = -0.01 };

        Assert.IsFalse(penalty.IsValid);
    }

    /// <summary>
    /// IsValid is true when Mean is finite and MSE is positive.
    /// </summary>
    [TestMethod]
    public void IsValid_ValidMeanAndMSE_ReturnsTrue()
    {
        var penalty = new ParameterPenalty { Mean = 0.3, MSE = 0.04 };

        Assert.IsTrue(penalty.IsValid);
    }

    /// <summary>
    /// IsValid is false when UseLog is true but Mean is non-positive.
    /// </summary>
    [TestMethod]
    public void IsValid_UseLog_NonPositiveMean_ReturnsFalse()
    {
        var penalty = new ParameterPenalty { Mean = -0.5, MSE = 0.01, UseLog = true };

        Assert.IsFalse(penalty.IsValid);
    }

    /// <summary>
    /// IsValid is true when UseLog is true and Mean is positive.
    /// </summary>
    [TestMethod]
    public void IsValid_UseLog_PositiveMean_ReturnsTrue()
    {
        var penalty = new ParameterPenalty { Mean = 2.5, MSE = 0.1, UseLog = true };

        Assert.IsTrue(penalty.IsValid);
    }

    #endregion

    #region Validate Tests

    /// <summary>
    /// Validate returns (true, empty) when not enabled (regardless of other values).
    /// </summary>
    [TestMethod]
    public void Validate_Disabled_AlwaysValid()
    {
        var penalty = new ParameterPenalty { Enabled = false, Mean = double.NaN, MSE = -1.0 };

        var (isValid, msg) = penalty.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(string.Empty, msg);
    }

    /// <summary>
    /// Validate returns error when enabled and Mean is NaN.
    /// </summary>
    [TestMethod]
    public void Validate_Enabled_MeanNaN_ReturnsError()
    {
        var penalty = new ParameterPenalty { Enabled = true, Mean = double.NaN, MSE = 0.1, Name = "Skew" };

        var (isValid, msg) = penalty.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(msg.Length > 0);
    }

    /// <summary>
    /// Validate returns error when enabled and MSE is non-positive.
    /// </summary>
    [TestMethod]
    public void Validate_Enabled_MSEZero_ReturnsError()
    {
        var penalty = new ParameterPenalty { Enabled = true, Mean = 0.3, MSE = 0.0 };

        var (isValid, msg) = penalty.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(msg.Length > 0);
    }

    /// <summary>
    /// Validate returns (true, empty) when all parameters are valid and enabled.
    /// </summary>
    [TestMethod]
    public void Validate_ValidParameters_ReturnsTrue()
    {
        var penalty = new ParameterPenalty { Enabled = true, Mean = 0.3, MSE = 0.04, Name = "Skew" };

        var (isValid, msg) = penalty.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(string.Empty, msg);
    }

    /// <summary>
    /// Validate with UseLog and non-positive Mean returns error.
    /// </summary>
    [TestMethod]
    public void Validate_UseLog_NonPositiveMean_ReturnsError()
    {
        var penalty = new ParameterPenalty { Enabled = true, Mean = 0.0, MSE = 0.01, UseLog = true };

        var (isValid, _) = penalty.Validate();

        Assert.IsFalse(isValid);
    }

    #endregion

    #region Function Tests

    /// <summary>
    /// Function returns 0 when penalty is not enabled.
    /// </summary>
    [TestMethod]
    public void Function_Disabled_ReturnsZero()
    {
        var penalty = new ParameterPenalty { Enabled = false, Mean = 0.3, MSE = 0.04 };

        double result = penalty.Function(0.5, 50);

        Assert.AreEqual(0.0, result, 1e-15);
    }

    /// <summary>
    /// Function returns 0 when sampleSize is zero or negative.
    /// </summary>
    [TestMethod]
    public void Function_ZeroSampleSize_ReturnsZero()
    {
        var penalty = new ParameterPenalty { Enabled = true, Mean = 0.3, MSE = 0.04 };

        Assert.AreEqual(0.0, penalty.Function(0.5, 0), 1e-15);
        Assert.AreEqual(0.0, penalty.Function(0.5, -5), 1e-15);
    }

    /// <summary>
    /// Function returns zero when parameterValue equals Mean (no deviation from prior).
    /// </summary>
    [TestMethod]
    public void Function_AtMean_ReturnsZero()
    {
        var penalty = new ParameterPenalty { Enabled = true, Mean = 0.3, MSE = 0.04 };

        double result = penalty.Function(0.3, 50);

        Assert.AreEqual(0.0, result, 1e-15);
    }

    /// <summary>
    /// Function in real space: (1/2)*(param - Mean)² / (MSE * n).
    /// </summary>
    [TestMethod]
    public void Function_RealSpace_MatchesFormula()
    {
        double mean = 0.3;
        double mse = 0.04;
        int n = 50;
        double param = 0.5;
        var penalty = new ParameterPenalty { Enabled = true, Mean = mean, MSE = mse };

        double expected = 0.5 * (param - mean) * (param - mean) / (mse * n);
        double actual = penalty.Function(param, n);

        Assert.AreEqual(expected, actual, 1e-12);
    }

    /// <summary>
    /// Function in log space returns zero when parameterValue is non-positive.
    /// </summary>
    [TestMethod]
    public void Function_LogSpace_NonPositiveParam_ReturnsZero()
    {
        var penalty = new ParameterPenalty { Enabled = true, Mean = 2.0, MSE = 0.1, UseLog = true };

        Assert.AreEqual(0.0, penalty.Function(-1.0, 50), 1e-15);
        Assert.AreEqual(0.0, penalty.Function(0.0, 50), 1e-15);
    }

    /// <summary>
    /// Function in log space at mean value returns zero.
    /// </summary>
    [TestMethod]
    public void Function_LogSpace_AtMean_ReturnsZero()
    {
        double mean = 2.0;
        var penalty = new ParameterPenalty { Enabled = true, Mean = mean, MSE = 0.1, UseLog = true };

        double result = penalty.Function(mean, 50);

        Assert.AreEqual(0.0, result, 1e-12, "Function at mean in log space should be zero.");
    }

    /// <summary>
    /// Function in log space increases as parameter deviates further from mean.
    /// </summary>
    [TestMethod]
    public void Function_LogSpace_LargerDeviation_LargerPenalty()
    {
        var penalty = new ParameterPenalty { Enabled = true, Mean = 2.0, MSE = 0.1, UseLog = true };

        double p1 = penalty.Function(2.5, 50);  // Small deviation
        double p2 = penalty.Function(5.0, 50);  // Large deviation

        Assert.IsTrue(p2 > p1, $"Larger deviation should produce larger penalty: p2={p2}, p1={p1}.");
    }

    #endregion

    #region Clone Tests

    /// <summary>
    /// Clone produces an independent copy with all properties equal.
    /// </summary>
    [TestMethod]
    public void Clone_ProducesIndependentCopyWithSameValues()
    {
        var original = new ParameterPenalty
        {
            Enabled = true,
            Name = "Skewness",
            Mean = 0.25,
            MSE = 0.05,
            UseLog = false
        };

        var clone = original.Clone();

        Assert.AreNotSame(original, clone);
        Assert.AreEqual(original.Enabled, clone.Enabled);
        Assert.AreEqual(original.Name, clone.Name);
        Assert.AreEqual(original.Mean, clone.Mean, 1e-15);
        Assert.AreEqual(original.MSE, clone.MSE, 1e-15);
        Assert.AreEqual(original.UseLog, clone.UseLog);
    }

    /// <summary>
    /// Modifying the clone does not affect the original.
    /// </summary>
    [TestMethod]
    public void Clone_Modification_DoesNotAffectOriginal()
    {
        var original = new ParameterPenalty { Mean = 0.3, MSE = 0.04 };
        var clone = original.Clone();
        clone.Mean = 99.0;

        Assert.AreEqual(0.3, original.Mean, 1e-15);
    }

    #endregion

    #region UpperValue / LowerValue Tests

    /// <summary>
    /// UpperValue is greater than LowerValue for valid parameters.
    /// </summary>
    [TestMethod]
    public void UpperValue_GreaterThan_LowerValue()
    {
        var penalty = new ParameterPenalty { Mean = 1.0, MSE = 0.25 };

        Assert.IsTrue(penalty.UpperValue > penalty.LowerValue,
            $"Upper={penalty.UpperValue}, Lower={penalty.LowerValue}");
    }

    /// <summary>
    /// In real space, UpperValue and LowerValue are symmetric around Mean.
    /// </summary>
    [TestMethod]
    public void UpperValue_LowerValue_RealSpace_Symmetric()
    {
        var penalty = new ParameterPenalty { Mean = 1.0, MSE = 0.25 };

        double mid = (penalty.UpperValue + penalty.LowerValue) / 2.0;

        Assert.AreEqual(1.0, mid, 1e-10, "Midpoint of upper/lower should equal Mean.");
    }

    /// <summary>
    /// In log space, UpperValue and LowerValue bracket the mean.
    /// </summary>
    [TestMethod]
    public void UpperValue_LowerValue_LogSpace_BracketMean()
    {
        var penalty = new ParameterPenalty { Mean = 2.0, MSE = 0.1, UseLog = true };

        Assert.IsTrue(penalty.UpperValue > penalty.Mean);
        Assert.IsTrue(penalty.LowerValue < penalty.Mean);
    }

    #endregion

    #region XML Round-Trip Tests

    /// <summary>
    /// ToXElement/FromXElement preserves all five properties.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesAllProperties()
    {
        var original = new ParameterPenalty
        {
            Enabled = true,
            Name = "Regional Skew",
            Mean = 0.35,
            MSE = 0.0225,
            UseLog = false
        };

        var xElement = original.ToXElement();
        var restored = new ParameterPenalty(xElement);

        Assert.AreEqual(original.Enabled, restored.Enabled);
        Assert.AreEqual(original.Name, restored.Name);
        Assert.AreEqual(original.Mean, restored.Mean, 1e-12);
        Assert.AreEqual(original.MSE, restored.MSE, 1e-12);
        Assert.AreEqual(original.UseLog, restored.UseLog);
    }

    /// <summary>
    /// ToXElement element name is "ParameterPenalty".
    /// </summary>
    [TestMethod]
    public void ToXElement_ElementNameIsParameterPenalty()
    {
        var penalty = new ParameterPenalty();
        var xe = penalty.ToXElement();

        Assert.AreEqual("ParameterPenalty", xe.Name.LocalName);
    }

    #endregion

    #region INotifyPropertyChanged Tests

    /// <summary>
    /// Setting Enabled fires PropertyChanged with correct property name.
    /// </summary>
    [TestMethod]
    public void Enabled_Changed_FiresPropertyChanged()
    {
        var penalty = new ParameterPenalty { Enabled = false };
        string? changedProp = null;
        penalty.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        penalty.Enabled = true;

        Assert.AreEqual(nameof(ParameterPenalty.Enabled), changedProp);
    }

    /// <summary>
    /// Setting Mean fires PropertyChanged with correct property name.
    /// </summary>
    [TestMethod]
    public void Mean_Changed_FiresPropertyChanged()
    {
        var penalty = new ParameterPenalty { Mean = 0.0 };
        string? changedProp = null;
        penalty.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        penalty.Mean = 0.5;

        Assert.AreEqual(nameof(ParameterPenalty.Mean), changedProp);
    }

    #endregion
}
