using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.TrendFunctions;

/// <summary>
/// Unit tests for the <see cref="ConstantTrend"/> class.
/// Tests the constant trend model y(t) = α.
/// </summary>
[TestClass]
public class ConstantTrendTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        // Act
        var model = new ConstantTrend();

        // Assert
        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.Constant, model.Type);
        Assert.AreEqual(1, model.NumberOfParameters);
        Assert.AreEqual("(α)", model.Parameters[0].Name);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        // Arrange
        var original = new ConstantTrend
        {
            OwnerName = "Location",
            StartIndex = 1950
        };
        original.Parameters[0].Value = 100.0;
        var xElement = original.ToXElement();

        // Act
        var restored = new ConstantTrend(xElement);

        // Assert
        Assert.AreEqual("Location", restored.OwnerName);
        Assert.AreEqual(1950, restored.StartIndex);
        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns constant.</summary>
    [TestMethod]
    public void Test_Type_ReturnsConstant()
    {
        var model = new ConstantTrend();
        Assert.AreEqual(TrendModelType.Constant, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters creates one parameter.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CreatesOneParameter()
    {
        var model = new ConstantTrend();

        Assert.AreEqual(1, model.Parameters.Count);
        Assert.AreEqual("(α)", model.Parameters[0].Name);
    }

    /// <summary>Verifies that set default parameters parameter value is zero.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_ParameterValueIsZero()
    {
        var model = new ConstantTrend();

        Assert.AreEqual(0.0, model.Parameters[0].Value);
    }

    /// <summary>Verifies that set default parameters owner name is propagated.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_OwnerNameIsPropagated()
    {
        var model = new ConstantTrend { OwnerName = "Scale" };
        model.SetDefaultParameters();

        Assert.AreEqual("Scale", model.Parameters[0].OwnerName);
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns constant value.</summary>
    [TestMethod]
    public void Test_Predict_ReturnsConstantValue()
    {
        var model = new ConstantTrend();
        model.Parameters[0].Value = 50.0;

        // Predict should return same value regardless of index
        Assert.AreEqual(50.0, model.Predict(0));
        Assert.AreEqual(50.0, model.Predict(100));
        Assert.AreEqual(50.0, model.Predict(-50));
        Assert.AreEqual(50.0, model.Predict(int.MaxValue));
    }

    /// <summary>Verifies that predict with start index still returns constant.</summary>
    [TestMethod]
    public void Test_Predict_WithStartIndex_StillReturnsConstant()
    {
        var model = new ConstantTrend
        {
            StartIndex = 1950
        };
        model.Parameters[0].Value = 75.5;

        // StartIndex should not affect constant trend
        Assert.AreEqual(75.5, model.Predict(1950));
        Assert.AreEqual(75.5, model.Predict(1900));
        Assert.AreEqual(75.5, model.Predict(2000));
    }

    /// <summary>Verifies that predict negative value.</summary>
    [TestMethod]
    public void Test_Predict_NegativeValue()
    {
        var model = new ConstantTrend();
        model.Parameters[0].Value = -25.5;

        Assert.AreEqual(-25.5, model.Predict(0));
    }

    /// <summary>Verifies that predict zero value.</summary>
    [TestMethod]
    public void Test_Predict_ZeroValue()
    {
        var model = new ConstantTrend();
        model.Parameters[0].Value = 0.0;

        Assert.AreEqual(0.0, model.Predict(0));
    }

    /// <summary>Verifies that predict large value.</summary>
    [TestMethod]
    public void Test_Predict_LargeValue()
    {
        var model = new ConstantTrend();
        model.Parameters[0].Value = 1e100;

        Assert.AreEqual(1e100, model.Predict(0));
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new ConstantTrend
        {
            OwnerName = "Location",
            StartIndex = 1980
        };
        original.Parameters[0].Value = 123.456;

        // Act
        var clone = (ConstantTrend)original.Clone();

        // Assert - clone has same values
        Assert.AreEqual(original.OwnerName, clone.OwnerName);
        Assert.AreEqual(original.StartIndex, clone.StartIndex);
        Assert.AreEqual(original.Parameters[0].Value, clone.Parameters[0].Value);

        // Assert - clone is independent
        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(123.456, clone.Parameters[0].Value);
    }

    /// <summary>Verifies that clone copies use default flat priors.</summary>
    [TestMethod]
    public void Test_Clone_CopiesUseDefaultFlatPriors()
    {
        var original = new ConstantTrend
        {
            UseDefaultFlatPriors = false
        };

        var clone = (ConstantTrend)original.Clone();

        Assert.AreEqual(original.UseDefaultFlatPriors, clone.UseDefaultFlatPriors);
    }

    /// <summary>Verifies that clone clones parameter properties.</summary>
    [TestMethod]
    public void Test_Clone_ClonesParameterProperties()
    {
        var original = new ConstantTrend();
        original.Parameters[0].Value = 50.0;
        original.Parameters[0].LowerBound = 0.0;
        original.Parameters[0].UpperBound = 100.0;

        var clone = (ConstantTrend)original.Clone();

        Assert.AreEqual(50.0, clone.Parameters[0].Value);
        Assert.AreEqual(0.0, clone.Parameters[0].LowerBound);
        Assert.AreEqual(100.0, clone.Parameters[0].UpperBound);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new ConstantTrend();

        var xElement = model.ToXElement();

        Assert.AreEqual("Constant", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new ConstantTrend
        {
            OwnerName = "Shape",
            StartIndex = 1960,
            UseDefaultFlatPriors = false
        };
        original.Parameters[0].Value = Math.E;

        // Act
        var xElement = original.ToXElement();
        var restored = new ConstantTrend(xElement);

        // Assert
        Assert.AreEqual(original.OwnerName, restored.OwnerName);
        Assert.AreEqual(original.StartIndex, restored.StartIndex);
        Assert.AreEqual(original.UseDefaultFlatPriors, restored.UseDefaultFlatPriors);
        Assert.AreEqual(original.Parameters[0].Value, restored.Parameters[0].Value, 1e-15);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that predict returns na n when with na n.</summary>
    [TestMethod]
    public void Test_Predict_WithNaN_ReturnsNaN()
    {
        var model = new ConstantTrend();
        model.Parameters[0].Value = double.NaN;

        Assert.IsTrue(double.IsNaN(model.Predict(0)));
    }

    /// <summary>Verifies that predict returns infinity when with infinity.</summary>
    [TestMethod]
    public void Test_Predict_WithInfinity_ReturnsInfinity()
    {
        var model = new ConstantTrend();
        model.Parameters[0].Value = double.PositiveInfinity;

        Assert.IsTrue(double.IsPositiveInfinity(model.Predict(0)));
    }

    /// <summary>Verifies that set parameter values updates prediction.</summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesPrediction()
    {
        var model = new ConstantTrend();
        model.SetParameterValues(new double[] { 42.0 });

        Assert.AreEqual(42.0, model.Predict(0));
    }

    #endregion
}
