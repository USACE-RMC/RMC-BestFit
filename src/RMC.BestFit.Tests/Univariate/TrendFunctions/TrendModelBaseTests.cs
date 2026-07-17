using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System.ComponentModel;

namespace RMC.BestFit.Tests.Univariate.TrendFunctions;

/// <summary>
/// Unit tests for the <c>TrendModelBase</c> abstract class.
/// Tests are performed using concrete implementations (ConstantTrend, LinearTrend).
/// </summary>
[TestClass]
public class TrendModelBaseTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor initializes parameters.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_InitializesParameters()
    {
        // Act
        var model = new ConstantTrend();

        // Assert
        Assert.IsNotNull(model.Parameters);
        Assert.AreEqual(1, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        // Arrange
        var original = new LinearTrend
        {
            OwnerName = "Location",
            StartIndex = 1950,
            UseDefaultFlatPriors = false
        };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.5;
        var xElement = original.ToXElement();

        // Act
        var restored = new LinearTrend(xElement);

        // Assert
        Assert.AreEqual("Location", restored.OwnerName);
        Assert.AreEqual(1950, restored.StartIndex);
        Assert.IsFalse(restored.UseDefaultFlatPriors);
        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(0.5, restored.Parameters[1].Value, 1e-10);
    }

    /// <summary>Verifies that constructor X element null throws.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_XElement_NullThrows()
    {
        // Act - should throw
        var model = new ConstantTrend(null!);
    }

    #endregion

    #region Property Tests

    /// <summary>Verifies that owner name set and get.</summary>
    [TestMethod]
    public void Test_OwnerName_SetAndGet()
    {
        var model = new ConstantTrend();
        model.OwnerName = "Scale";

        Assert.AreEqual("Scale", model.OwnerName);
    }

    /// <summary>Verifies that owner name null becomes empty.</summary>
    [TestMethod]
    public void Test_OwnerName_NullBecomesEmpty()
    {
        var model = new ConstantTrend();
        model.OwnerName = null!;

        Assert.AreEqual(string.Empty, model.OwnerName);
    }

    /// <summary>Verifies that start index set and get.</summary>
    [TestMethod]
    public void Test_StartIndex_SetAndGet()
    {
        var model = new LinearTrend();
        model.StartIndex = 2000;

        Assert.AreEqual(2000, model.StartIndex);
    }

    /// <summary>Verifies that use default flat priors set and get.</summary>
    [TestMethod]
    public void Test_UseDefaultFlatPriors_SetAndGet()
    {
        var model = new ConstantTrend();
        model.UseDefaultFlatPriors = false;

        Assert.IsFalse(model.UseDefaultFlatPriors);
    }

    /// <summary>Verifies that use default flat priors true resets parameters.</summary>
    [TestMethod]
    public void Test_UseDefaultFlatPriors_TrueResetsParameters()
    {
        // Arrange
        var model = new LinearTrend();
        model.UseDefaultFlatPriors = false;
        model.Parameters[0].Value = 999.0;
        model.Parameters[1].Value = 0.99;

        // Act - setting to true should call SetDefaultParameters
        model.UseDefaultFlatPriors = true;

        // Assert - default values should be restored
        Assert.AreEqual(0.0, model.Parameters[0].Value);
        Assert.AreEqual(0.0, model.Parameters[1].Value);
    }

    /// <summary>Verifies that number of parameters returns correct count.</summary>
    [TestMethod]
    public void Test_NumberOfParameters_ReturnsCorrectCount()
    {
        var constant = new ConstantTrend();
        var linear = new LinearTrend();

        Assert.AreEqual(1, constant.NumberOfParameters);
        Assert.AreEqual(2, linear.NumberOfParameters);
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>Verifies that property changed owner name.</summary>
    [TestMethod]
    public void Test_PropertyChanged_OwnerName()
    {
        var model = new ConstantTrend();
        string? changedProperty = null;
        model.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        model.OwnerName = "Location";

        Assert.AreEqual(nameof(model.OwnerName), changedProperty);
    }

    /// <summary>Verifies that property changed start index.</summary>
    [TestMethod]
    public void Test_PropertyChanged_StartIndex()
    {
        var model = new LinearTrend();
        string? changedProperty = null;
        model.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        model.StartIndex = 1990;

        Assert.AreEqual(nameof(model.StartIndex), changedProperty);
    }

    /// <summary>Verifies that property changed use default flat priors.</summary>
    [TestMethod]
    public void Test_PropertyChanged_UseDefaultFlatPriors()
    {
        var model = new ConstantTrend();
        string? changedProperty = null;
        model.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        model.UseDefaultFlatPriors = false;

        Assert.AreEqual(nameof(model.UseDefaultFlatPriors), changedProperty);
    }

    /// <summary>Verifies that property changed not fired when value unchanged.</summary>
    [TestMethod]
    public void Test_PropertyChanged_NotFiredWhenValueUnchanged()
    {
        var model = new ConstantTrend();
        model.StartIndex = 2000;
        int fireCount = 0;
        model.PropertyChanged += (s, e) => fireCount++;

        model.StartIndex = 2000; // Same value

        Assert.AreEqual(0, fireCount);
    }

    #endregion

    #region SetParameterValues Tests

    /// <summary>Verifies that set parameter values updates all parameters.</summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesAllParameters()
    {
        var model = new LinearTrend();
        var values = new double[] { 50.0, 0.25 };

        model.SetParameterValues(values);

        Assert.AreEqual(50.0, model.Parameters[0].Value);
        Assert.AreEqual(0.25, model.Parameters[1].Value);
    }

    /// <summary>Verifies that set parameter values null throws.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_SetParameterValues_NullThrows()
    {
        var model = new LinearTrend();
        model.SetParameterValues(null!);
    }

    /// <summary>Verifies that set parameter values wrong length throws.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_SetParameterValues_WrongLengthThrows()
    {
        var model = new LinearTrend();
        var values = new double[] { 50.0 }; // Only 1 value, but model has 2 parameters

        model.SetParameterValues(values);
    }

    /// <summary>Verifies that set parameter values raises property changed.</summary>
    [TestMethod]
    public void Test_SetParameterValues_RaisesPropertyChanged()
    {
        var model = new LinearTrend();
        string? changedProperty = null;
        model.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        model.SetParameterValues(new double[] { 1.0, 0.1 });

        Assert.AreEqual(nameof(model.Parameters), changedProperty);
    }

    #endregion

    #region ToXElement Tests

    /// <summary>Verifies that to X element contains all attributes.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAllAttributes()
    {
        var model = new LinearTrend
        {
            OwnerName = "Location",
            StartIndex = 1980,
            UseDefaultFlatPriors = false
        };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 0.5;

        var xElement = model.ToXElement();

        Assert.AreEqual("ITrendModel", xElement.Name.LocalName);
        Assert.AreEqual("Location", xElement.Attribute("OwnerName")?.Value);
        Assert.AreEqual("False", xElement.Attribute("UseDefaultFlatPriors")?.Value);
        Assert.AreEqual("Linear", xElement.Attribute("Type")?.Value);
        Assert.AreEqual("1980", xElement.Attribute("StartIndex")?.Value);
        Assert.IsNotNull(xElement.Element("Parameters"));
    }

    /// <summary>Verifies that to X element parameters are serialized.</summary>
    [TestMethod]
    public void Test_ToXElement_ParametersAreSerialized()
    {
        var model = new LinearTrend();
        model.Parameters[0].Value = 123.456;
        model.Parameters[1].Value = 0.789;

        var xElement = model.ToXElement();
        var paramsElement = xElement.Element("Parameters");

        Assert.IsNotNull(paramsElement);
        Assert.AreEqual(2, paramsElement.Elements("ModelParameter").Count());
    }

    /// <summary>Verifies that round trip serialization.</summary>
    [TestMethod]
    public void Test_RoundTrip_Serialization()
    {
        // Arrange
        var original = new LinearTrend
        {
            OwnerName = "Scale",
            StartIndex = 1970
        };
        original.Parameters[0].Value = Math.PI;
        original.Parameters[1].Value = 0.123456789;

        // Act
        var xElement = original.ToXElement();
        var restored = new LinearTrend(xElement);

        // Assert
        Assert.AreEqual(original.OwnerName, restored.OwnerName);
        Assert.AreEqual(original.StartIndex, restored.StartIndex);
        Assert.AreEqual(original.Parameters[0].Value, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(original.Parameters[1].Value, restored.Parameters[1].Value, 1e-10);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that negative start index allowed.</summary>
    [TestMethod]
    public void Test_NegativeStartIndex_Allowed()
    {
        var model = new LinearTrend();
        model.StartIndex = -100;

        Assert.AreEqual(-100, model.StartIndex);
    }

    /// <summary>Verifies that large start index allowed.</summary>
    [TestMethod]
    public void Test_LargeStartIndex_Allowed()
    {
        var model = new LinearTrend();
        model.StartIndex = int.MaxValue;

        Assert.AreEqual(int.MaxValue, model.StartIndex);
    }

    /// <summary>Verifies that empty owner name serializes correctly.</summary>
    [TestMethod]
    public void Test_EmptyOwnerName_SerializesCorrectly()
    {
        var model = new ConstantTrend { OwnerName = "" };

        var xElement = model.ToXElement();
        var restored = new ConstantTrend(xElement);

        Assert.AreEqual("", restored.OwnerName);
    }

    #endregion
}
