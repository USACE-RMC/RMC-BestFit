using Numerics.Distributions;
using RMC.BestFit.Models;
using System.ComponentModel;

namespace RMC.BestFit.Tests.CoreInfrastructure;

/// <summary>
/// Unit tests for the <c>ModelParameter</c> class.
/// Tests construction, validation, serialization, and edge cases.
/// </summary>
[TestClass]
public class ModelParameterTests
{
    #region Constructor Tests

    /// <summary>Verifies that empty constructor sets defaults.</summary>
    [TestMethod]
    public void Test_EmptyConstructor_SetsDefaults()
    {
        // Arrange & Act
        var param = new ModelParameter();

        // Assert
        Assert.AreEqual("", param.OwnerName);
        Assert.AreEqual("Parameter", param.Name);
        Assert.AreEqual(0.0, param.Value);
        Assert.AreEqual(double.MinValue, param.LowerBound);
        Assert.AreEqual(double.MaxValue, param.UpperBound);
        Assert.IsFalse(param.IsPositive);
        Assert.IsFalse(param.IsFixed);
        Assert.IsInstanceOfType(param.PriorDistribution, typeof(Uniform));
    }

    /// <summary>Verifies that full constructor sets all properties.</summary>
    [TestMethod]
    public void Test_FullConstructor_SetsAllProperties()
    {
        // Arrange
        var prior = new Normal(100.0, 10.0);

        // Act
        var param = new ModelParameter("GEV", "Location", 50.0, 0.0, 200.0, prior, false, false);

        // Assert
        Assert.AreEqual("GEV", param.OwnerName);
        Assert.AreEqual("Location", param.Name);
        Assert.AreEqual(50.0, param.Value);
        Assert.AreEqual(0.0, param.LowerBound);
        Assert.AreEqual(200.0, param.UpperBound);
        Assert.IsFalse(param.IsPositive);
        Assert.IsFalse(param.IsFixed);
        Assert.AreSame(prior, param.PriorDistribution);
    }

    /// <summary>Verifies that full constructor positive parameter.</summary>
    [TestMethod]
    public void Test_FullConstructor_PositiveParameter()
    {
        // Arrange
        var prior = new Exponential(0, 10.0);

        // Act
        var param = new ModelParameter("GEV", "Scale", 15.0, 0.001, 1000.0, prior, isPositive: true);

        // Assert
        Assert.IsTrue(param.IsPositive);
    }

    /// <summary>Verifies that full constructor fixed parameter.</summary>
    [TestMethod]
    public void Test_FullConstructor_FixedParameter()
    {
        // Arrange
        var prior = new Uniform(0, 1);

        // Act
        var param = new ModelParameter("GEV", "Shape", 0.0, -0.5, 0.5, prior, isFixed: true);

        // Assert
        Assert.IsTrue(param.IsFixed);
    }

    #endregion

    #region DisplayName Tests

    /// <summary>Verifies that display name with owner.</summary>
    [TestMethod]
    public void Test_DisplayName_WithOwner()
    {
        var param = new ModelParameter("GEV", "Location", 50.0, 0, 200, new Uniform(0, 200));
        Assert.AreEqual("GEV Location", param.DisplayName);
    }

    /// <summary>Verifies that display name without owner.</summary>
    [TestMethod]
    public void Test_DisplayName_WithoutOwner()
    {
        var param = new ModelParameter("", "Location", 50.0, 0, 200, new Uniform(0, 200));
        Assert.AreEqual("Location", param.DisplayName);
    }

    #endregion

    #region Property Change Notification Tests

    /// <summary>Verifies that property changed value.</summary>
    [TestMethod]
    public void Test_PropertyChanged_Value()
    {
        // Arrange
        var param = new ModelParameter();
        string? changedProperty = null;
        param.PropertyChanged += (sender, e) => changedProperty = e.PropertyName;

        // Act
        param.Value = 100.0;

        // Assert
        Assert.AreEqual(nameof(ModelParameter.Value), changedProperty);
    }

    /// <summary>Verifies that property changed name.</summary>
    [TestMethod]
    public void Test_PropertyChanged_Name()
    {
        var param = new ModelParameter();
        string? changedProperty = null;
        param.PropertyChanged += (sender, e) => changedProperty = e.PropertyName;

        param.Name = "NewName";

        Assert.AreEqual(nameof(ModelParameter.Name), changedProperty);
    }

    /// <summary>Verifies that property changed only fires on change.</summary>
    [TestMethod]
    public void Test_PropertyChanged_OnlyFiresOnChange()
    {
        var param = new ModelParameter();
        param.Value = 50.0;
        int fireCount = 0;
        param.PropertyChanged += (sender, e) => fireCount++;

        // Set to same value
        param.Value = 50.0;

        Assert.AreEqual(0, fireCount);
    }

    /// <summary>Verifies that property changed lower bound.</summary>
    [TestMethod]
    public void Test_PropertyChanged_LowerBound()
    {
        var param = new ModelParameter();
        string? changedProperty = null;
        param.PropertyChanged += (sender, e) => changedProperty = e.PropertyName;

        param.LowerBound = 10.0;

        Assert.AreEqual(nameof(ModelParameter.LowerBound), changedProperty);
    }

    /// <summary>Verifies that property changed upper bound.</summary>
    [TestMethod]
    public void Test_PropertyChanged_UpperBound()
    {
        var param = new ModelParameter();
        string? changedProperty = null;
        param.PropertyChanged += (sender, e) => changedProperty = e.PropertyName;

        param.UpperBound = 100.0;

        Assert.AreEqual(nameof(ModelParameter.UpperBound), changedProperty);
    }

    /// <summary>Verifies that property changed is fixed.</summary>
    [TestMethod]
    public void Test_PropertyChanged_IsFixed()
    {
        var param = new ModelParameter();
        string? changedProperty = null;
        param.PropertyChanged += (sender, e) => changedProperty = e.PropertyName;

        param.IsFixed = true;

        Assert.AreEqual(nameof(ModelParameter.IsFixed), changedProperty);
    }

    /// <summary>Verifies that property changed is positive.</summary>
    [TestMethod]
    public void Test_PropertyChanged_IsPositive()
    {
        var param = new ModelParameter();
        string? changedProperty = null;
        param.PropertyChanged += (sender, e) => changedProperty = e.PropertyName;

        param.IsPositive = true;

        Assert.AreEqual(nameof(ModelParameter.IsPositive), changedProperty);
    }

    /// <summary>Verifies that property changed prior distribution.</summary>
    [TestMethod]
    public void Test_PropertyChanged_PriorDistribution()
    {
        var param = new ModelParameter();
        string? changedProperty = null;
        param.PropertyChanged += (sender, e) => changedProperty = e.PropertyName;

        param.PriorDistribution = new Normal(0, 1);

        Assert.AreEqual(nameof(ModelParameter.PriorDistribution), changedProperty);
    }

    /// <summary>Verifies that property changed owner name.</summary>
    [TestMethod]
    public void Test_PropertyChanged_OwnerName()
    {
        var param = new ModelParameter();
        string? changedProperty = null;
        param.PropertyChanged += (sender, e) => changedProperty = e.PropertyName;

        param.OwnerName = "NewOwner";

        Assert.AreEqual(nameof(ModelParameter.OwnerName), changedProperty);
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns true when valid parameter.</summary>
    [TestMethod]
    public void Test_Validate_ValidParameter_ReturnsTrue()
    {
        var param = new ModelParameter("GEV", "Location", 50.0, 0.0, 100.0, new Normal(50, 10));

        var (isValid, messages) = param.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate returns false when lower bound greater than upper bound.</summary>
    [TestMethod]
    public void Test_Validate_LowerBoundGreaterThanUpperBound_ReturnsFalse()
    {
        var param = new ModelParameter();
        param.LowerBound = 100.0;
        param.UpperBound = 0.0;

        var (isValid, messages) = param.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("lower bound")));
    }

    /// <summary>Verifies that validate returns false when positive parameter with invalid prior.</summary>
    [TestMethod]
    public void Test_Validate_PositiveParameter_WithInvalidPrior_ReturnsFalse()
    {
        // Create a parameter that must be positive but has prior that includes non-positive values
        var param = new ModelParameter("GEV", "Scale", 15.0, 0.001, 1000.0, new Normal(0, 10), isPositive: true);

        var (isValid, messages) = param.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("minimum value")));
    }

    /// <summary>Verifies that validate returns true when positive parameter with valid prior.</summary>
    [TestMethod]
    public void Test_Validate_PositiveParameter_WithValidPrior_ReturnsTrue()
    {
        // Create a parameter that must be positive with appropriate prior
        var param = new ModelParameter("GEV", "Scale", 15.0, 0.001, 1000.0, new Exponential(0.001, 20.0), isPositive: true);

        var (isValid, messages) = param.Validate();

        Assert.IsTrue(isValid);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains all attributes.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAllAttributes()
    {
        var param = new ModelParameter("GEV", "Location", 50.0, 0.0, 100.0, new Normal(50, 10), false, true);

        var xElement = param.ToXElement();

        Assert.AreEqual("ModelParameter", xElement.Name.LocalName);
        Assert.AreEqual("GEV", xElement.Attribute("OwnerName")?.Value);
        Assert.AreEqual("Location", xElement.Attribute("Name")?.Value);
        Assert.IsNotNull(xElement.Attribute("Value"));
        Assert.IsNotNull(xElement.Attribute("LowerBound"));
        Assert.IsNotNull(xElement.Attribute("UpperBound"));
        Assert.AreEqual("False", xElement.Attribute("IsPositive")?.Value);
        Assert.AreEqual("True", xElement.Attribute("IsFixed")?.Value);
        Assert.IsNotNull(xElement.Element("Distribution"));
    }

    /// <summary>Verifies that from X element restores all properties.</summary>
    [TestMethod]
    public void Test_FromXElement_RestoresAllProperties()
    {
        // Arrange
        var original = new ModelParameter("GEV", "Location", 50.0, 0.0, 100.0, new Normal(50, 10), false, true);
        var xElement = original.ToXElement();

        // Act
        var restored = new ModelParameter(xElement);

        // Assert
        Assert.AreEqual(original.OwnerName, restored.OwnerName);
        Assert.AreEqual(original.Name, restored.Name);
        Assert.AreEqual(original.Value, restored.Value, 1e-10);
        Assert.AreEqual(original.LowerBound, restored.LowerBound, 1e-10);
        Assert.AreEqual(original.UpperBound, restored.UpperBound, 1e-10);
        Assert.AreEqual(original.IsPositive, restored.IsPositive);
        Assert.AreEqual(original.IsFixed, restored.IsFixed);
        Assert.IsInstanceOfType(restored.PriorDistribution, typeof(Normal));
    }

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new ModelParameter("GEV", "Location", 50.0, 0.0, 100.0, new Normal(50, 10));
        var clone = original.Clone();

        // Modify original
        original.Value = 75.0;
        original.Name = "Modified";

        // Clone should be unchanged
        Assert.AreEqual(50.0, clone.Value);
        Assert.AreEqual("Location", clone.Name);
    }

    /// <summary>Verifies that round trip preserves high precision for serialization.</summary>
    [TestMethod]
    public void Test_RoundTrip_Serialization_PreservesHighPrecision()
    {
        var original = new ModelParameter("Test", "Param", Math.PI, -100.0, 100.0, new Normal(0, 1));
        var xElement = original.ToXElement();
        var restored = new ModelParameter(xElement);

        Assert.AreEqual(original.Value, restored.Value, 1e-15);
    }

    #endregion

    #region Edge Case Tests

    /// <summary>Verifies that extreme values large bounds.</summary>
    [TestMethod]
    public void Test_ExtremeValues_LargeBounds()
    {
        var param = new ModelParameter("Test", "Param", 0.0, -1e300, 1e300, new Uniform(-1e300, 1e300));

        var (isValid, _) = param.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that extreme values small range.</summary>
    [TestMethod]
    public void Test_ExtremeValues_SmallRange()
    {
        var param = new ModelParameter("Test", "Param", 0.0, -1e-10, 1e-10, new Uniform(-1e-10, 1e-10));

        Assert.AreEqual(-1e-10, param.LowerBound);
        Assert.AreEqual(1e-10, param.UpperBound);
    }

    /// <summary>Verifies that null prior distribution from X element.</summary>
    [TestMethod]
    public void Test_NullPriorDistribution_FromXElement()
    {
        // Create XElement without Distribution element
        var xElement = new System.Xml.Linq.XElement("ModelParameter",
            new System.Xml.Linq.XAttribute("OwnerName", "Test"),
            new System.Xml.Linq.XAttribute("Name", "Param"),
            new System.Xml.Linq.XAttribute("Value", "50"),
            new System.Xml.Linq.XAttribute("LowerBound", "0"),
            new System.Xml.Linq.XAttribute("UpperBound", "100"));

        var param = new ModelParameter(xElement);

        // Prior distribution should be null if not provided in XML
        Assert.IsNull(param.PriorDistribution);
    }

    /// <summary>Verifies that special double values.</summary>
    [TestMethod]
    public void Test_SpecialDoubleValues()
    {
        var param = new ModelParameter();

        // Test that special values can be set (even if not recommended)
        param.Value = double.NaN;
        Assert.IsTrue(double.IsNaN(param.Value));

        param.Value = double.PositiveInfinity;
        Assert.IsTrue(double.IsPositiveInfinity(param.Value));
    }

    #endregion
}
