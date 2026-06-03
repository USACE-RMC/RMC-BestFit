using RMC.BestFit.Models;
using System.ComponentModel;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.Support;

/// <summary>
/// Unit tests for the <see cref="QuantilePenalty"/> class.
/// </summary>
/// <remarks>
/// Covers: default construction, XML round-trip, <c>IsValid</c>, <c>Validate()</c>,
/// <c>Function()</c> in real and log10 space, <c>Clone()</c>, <c>MeanValue</c>/<c>MSEValue</c>,
/// <c>UpperValue</c>/<c>LowerValue</c>, and <see cref="INotifyPropertyChanged"/> event firing.
/// </remarks>
[TestClass]
public class QuantilePenaltyTests
{
    #region Constructor Tests

    /// <summary>
    /// Default constructor sets expected defaults: AEP=0.01, Mean=NaN, MSE=NaN, Enabled=false.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_SetsExpectedDefaults()
    {
        var penalty = new QuantilePenalty();

        Assert.AreEqual(0.01, penalty.AEP, 1e-12);
        Assert.IsTrue(double.IsNaN(penalty.Mean));
        Assert.IsTrue(double.IsNaN(penalty.MSE));
        Assert.IsFalse(penalty.Enabled);
        Assert.IsFalse(penalty.UseLog10);
    }

    /// <summary>
    /// XElement constructor throws ArgumentNullException for null input.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_XElement_Null_ThrowsArgumentNullException()
    {
        _ = new QuantilePenalty((XElement)null!);
    }

    #endregion

    #region IsValid Tests

    /// <summary>
    /// IsValid is false when AEP is 0.
    /// </summary>
    [TestMethod]
    public void IsValid_AEPZero_ReturnsFalse()
    {
        var penalty = new QuantilePenalty { AEP = 0.0, Mean = 100.0, MSE = 25.0 };

        Assert.IsFalse(penalty.IsValid);
    }

    /// <summary>
    /// IsValid is false when AEP is 1.
    /// </summary>
    [TestMethod]
    public void IsValid_AEPOne_ReturnsFalse()
    {
        var penalty = new QuantilePenalty { AEP = 1.0, Mean = 100.0, MSE = 25.0 };

        Assert.IsFalse(penalty.IsValid);
    }

    /// <summary>
    /// IsValid is false when Mean is NaN.
    /// </summary>
    [TestMethod]
    public void IsValid_MeanNaN_ReturnsFalse()
    {
        var penalty = new QuantilePenalty { AEP = 0.01, Mean = double.NaN, MSE = 25.0 };

        Assert.IsFalse(penalty.IsValid);
    }

    /// <summary>
    /// IsValid is false when MSE is zero or negative.
    /// </summary>
    [TestMethod]
    public void IsValid_MSEZeroOrNegative_ReturnsFalse()
    {
        var p0 = new QuantilePenalty { AEP = 0.01, Mean = 100.0, MSE = 0.0 };
        var pNeg = new QuantilePenalty { AEP = 0.01, Mean = 100.0, MSE = -1.0 };

        Assert.IsFalse(p0.IsValid);
        Assert.IsFalse(pNeg.IsValid);
    }

    /// <summary>
    /// IsValid is true when AEP, Mean, and MSE are all valid.
    /// </summary>
    [TestMethod]
    public void IsValid_AllValid_ReturnsTrue()
    {
        var penalty = new QuantilePenalty { AEP = 0.01, Mean = 2.5, MSE = 0.04 };

        Assert.IsTrue(penalty.IsValid);
    }

    #endregion

    #region Validate Tests

    /// <summary>
    /// Validate returns (true, empty) when disabled regardless of other values.
    /// </summary>
    [TestMethod]
    public void Validate_Disabled_AlwaysValid()
    {
        var penalty = new QuantilePenalty { Enabled = false, AEP = -1.0, Mean = double.NaN };

        var (isValid, msg) = penalty.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(string.Empty, msg);
    }

    /// <summary>
    /// Validate returns error when AEP is out of range.
    /// </summary>
    [TestMethod]
    public void Validate_Enabled_BadAEP_ReturnsError()
    {
        var penalty = new QuantilePenalty { Enabled = true, AEP = 1.5, Mean = 100.0, MSE = 25.0 };

        var (isValid, _) = penalty.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Validate returns error when MSE is non-positive.
    /// </summary>
    [TestMethod]
    public void Validate_Enabled_MSEZero_ReturnsError()
    {
        var penalty = new QuantilePenalty { Enabled = true, AEP = 0.01, Mean = 100.0, MSE = 0.0 };

        var (isValid, _) = penalty.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>
    /// Validate returns (true, empty) when all parameters are valid.
    /// </summary>
    [TestMethod]
    public void Validate_ValidParameters_ReturnsTrue()
    {
        var penalty = new QuantilePenalty { Enabled = true, AEP = 0.01, Mean = 2.5, MSE = 0.04 };

        var (isValid, msg) = penalty.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(string.Empty, msg);
    }

    #endregion

    #region Function Tests

    /// <summary>
    /// Function returns 0 when not enabled.
    /// </summary>
    [TestMethod]
    public void Function_Disabled_ReturnsZero()
    {
        var penalty = new QuantilePenalty { Enabled = false, AEP = 0.01, Mean = 2.5, MSE = 0.04 };

        Assert.AreEqual(0.0, penalty.Function(3.0, 50), 1e-15);
    }

    /// <summary>
    /// Function returns 0 when sampleSize is zero.
    /// </summary>
    [TestMethod]
    public void Function_ZeroSampleSize_ReturnsZero()
    {
        var penalty = new QuantilePenalty { Enabled = true, AEP = 0.01, Mean = 2.5, MSE = 0.04 };

        Assert.AreEqual(0.0, penalty.Function(3.0, 0), 1e-15);
    }

    /// <summary>
    /// Function in real space at mean returns zero.
    /// </summary>
    [TestMethod]
    public void Function_RealSpace_AtMean_ReturnsZero()
    {
        var penalty = new QuantilePenalty { Enabled = true, AEP = 0.01, Mean = 100.0, MSE = 25.0 };

        double result = penalty.Function(100.0, 50);

        Assert.AreEqual(0.0, result, 1e-12);
    }

    /// <summary>
    /// Function in real space matches the formula: (1/2)*(q - Mean)² / (MSE * n).
    /// </summary>
    [TestMethod]
    public void Function_RealSpace_MatchesFormula()
    {
        double mean = 2.5;
        double mse = 0.04;
        int n = 30;
        double q = 3.0;
        var penalty = new QuantilePenalty { Enabled = true, AEP = 0.01, Mean = mean, MSE = mse };

        double expected = 0.5 * (q - mean) * (q - mean) / (mse * n);
        double actual = penalty.Function(q, n);

        Assert.AreEqual(expected, actual, 1e-12);
    }

    /// <summary>
    /// Function in log10 space returns zero for non-positive quantile value.
    /// </summary>
    [TestMethod]
    public void Function_Log10Space_NonPositiveQuantile_ReturnsZero()
    {
        var penalty = new QuantilePenalty
        {
            Enabled = true,
            AEP = 0.01,
            Mean = 2.5,
            MSE = 0.04,
            UseLog10 = true
        };

        Assert.AreEqual(0.0, penalty.Function(0.0, 50), 1e-15);
        Assert.AreEqual(0.0, penalty.Function(-10.0, 50), 1e-15);
    }

    /// <summary>
    /// Function in log10 space: penalty at 10^Mean is approximately zero.
    /// </summary>
    [TestMethod]
    public void Function_Log10Space_AtMeanValue_ReturnsNearZero()
    {
        double logMean = 2.5; // means q=316.2 in real space
        var penalty = new QuantilePenalty
        {
            Enabled = true,
            AEP = 0.01,
            Mean = logMean,
            MSE = 0.04,
            UseLog10 = true
        };
        double qAtMean = Math.Pow(10.0, logMean);

        double result = penalty.Function(qAtMean, 50);

        Assert.AreEqual(0.0, result, 1e-10, "Function in log10 space at mean should be zero.");
    }

    #endregion

    #region Clone Tests

    /// <summary>
    /// Clone produces a deep copy with all properties equal.
    /// </summary>
    [TestMethod]
    public void Clone_ProducesDeepCopyWithSameValues()
    {
        var original = new QuantilePenalty
        {
            Enabled = true,
            AEP = 0.01,
            Mean = 2.5,
            MSE = 0.04,
            UseLog10 = true
        };

        var clone = original.Clone();

        Assert.AreNotSame(original, clone);
        Assert.AreEqual(original.Enabled, clone.Enabled);
        Assert.AreEqual(original.AEP, clone.AEP, 1e-15);
        Assert.AreEqual(original.Mean, clone.Mean, 1e-15);
        Assert.AreEqual(original.MSE, clone.MSE, 1e-15);
        Assert.AreEqual(original.UseLog10, clone.UseLog10);
    }

    /// <summary>
    /// Modifying the clone does not affect the original.
    /// </summary>
    [TestMethod]
    public void Clone_Modification_DoesNotAffectOriginal()
    {
        var original = new QuantilePenalty { AEP = 0.01, Mean = 2.5, MSE = 0.04 };
        var clone = original.Clone();
        clone.Mean = 99.0;

        Assert.AreEqual(2.5, original.Mean, 1e-15);
    }

    #endregion

    #region MeanValue / MSEValue Tests

    /// <summary>
    /// MeanValue returns Mean directly when UseLog10 is false.
    /// </summary>
    [TestMethod]
    public void MeanValue_NoLog_EqualsMean()
    {
        var penalty = new QuantilePenalty { Mean = 100.0, MSE = 25.0, UseLog10 = false };

        Assert.AreEqual(100.0, penalty.MeanValue, 1e-10);
    }

    /// <summary>
    /// MSEValue returns MSE directly when UseLog10 is false.
    /// </summary>
    [TestMethod]
    public void MSEValue_NoLog_EqualsMSE()
    {
        var penalty = new QuantilePenalty { Mean = 100.0, MSE = 25.0, UseLog10 = false };

        Assert.AreEqual(25.0, penalty.MSEValue, 1e-10);
    }

    /// <summary>
    /// MeanValue in log10 space is greater than Mean (due to lognormal bias correction).
    /// </summary>
    [TestMethod]
    public void MeanValue_Log10_GreaterThanPowOf10Mean()
    {
        var penalty = new QuantilePenalty { Mean = 2.5, MSE = 0.1, UseLog10 = true };

        // MeanValue (real-space) should be > 10^2.5 = 316.2 (positive bias correction)
        Assert.IsTrue(penalty.MeanValue > Math.Pow(10, 2.5),
            $"MeanValue={penalty.MeanValue} should exceed 10^2.5={Math.Pow(10, 2.5)}");
    }

    #endregion

    #region UpperValue / LowerValue Tests

    /// <summary>
    /// UpperValue is greater than LowerValue for valid parameters.
    /// </summary>
    [TestMethod]
    public void UpperValue_GreaterThan_LowerValue()
    {
        var penalty = new QuantilePenalty { AEP = 0.01, Mean = 2.5, MSE = 0.04 };

        Assert.IsTrue(penalty.UpperValue > penalty.LowerValue,
            $"Upper={penalty.UpperValue}, Lower={penalty.LowerValue}");
    }

    /// <summary>
    /// In real space, UpperValue and LowerValue are symmetric around Mean.
    /// </summary>
    [TestMethod]
    public void UpperValue_LowerValue_RealSpace_SymmetricAroundMean()
    {
        var penalty = new QuantilePenalty { Mean = 2.5, MSE = 0.04 };

        double mid = (penalty.UpperValue + penalty.LowerValue) / 2.0;

        Assert.AreEqual(2.5, mid, 1e-10, "Midpoint of upper/lower should equal Mean.");
    }

    #endregion

    #region XML Round-Trip Tests

    /// <summary>
    /// ToXElement/FromXElement preserves all five properties.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesAllProperties()
    {
        var original = new QuantilePenalty
        {
            Enabled = true,
            AEP = 0.01,
            Mean = 2.75,
            MSE = 0.0625,
            UseLog10 = true
        };

        var xElement = original.ToXElement();
        var restored = new QuantilePenalty(xElement);

        Assert.AreEqual(original.Enabled, restored.Enabled);
        Assert.AreEqual(original.AEP, restored.AEP, 1e-12);
        Assert.AreEqual(original.Mean, restored.Mean, 1e-12);
        Assert.AreEqual(original.MSE, restored.MSE, 1e-12);
        Assert.AreEqual(original.UseLog10, restored.UseLog10);
    }

    /// <summary>
    /// ToXElement element name is "QuantilePenalty".
    /// </summary>
    [TestMethod]
    public void ToXElement_ElementNameIsQuantilePenalty()
    {
        var penalty = new QuantilePenalty();
        var xe = penalty.ToXElement();

        Assert.AreEqual("QuantilePenalty", xe.Name.LocalName);
    }

    #endregion

    #region INotifyPropertyChanged Tests

    /// <summary>
    /// Setting AEP fires PropertyChanged with "AEP".
    /// </summary>
    [TestMethod]
    public void AEP_Changed_FiresPropertyChanged()
    {
        var penalty = new QuantilePenalty { AEP = 0.01 };
        string? changedProp = null;
        penalty.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        penalty.AEP = 0.02;

        Assert.AreEqual(nameof(QuantilePenalty.AEP), changedProp);
    }

    /// <summary>
    /// Setting MSE fires PropertyChanged with "MSE".
    /// </summary>
    [TestMethod]
    public void MSE_Changed_FiresPropertyChanged()
    {
        var penalty = new QuantilePenalty { MSE = 0.01 };
        string? changedProp = null;
        penalty.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        penalty.MSE = 0.05;

        Assert.AreEqual(nameof(QuantilePenalty.MSE), changedProp);
    }

    #endregion
}
