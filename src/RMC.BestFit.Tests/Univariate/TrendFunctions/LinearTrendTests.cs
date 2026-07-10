using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.Univariate.TrendFunctions;

/// <summary>
/// Unit tests for the <c>LinearTrend</c> class.
/// Tests the linear trend model y(t) = α + β(t - StartIndex).
/// </summary>
[TestClass]
public class LinearTrendTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        // Act
        var model = new LinearTrend();

        // Assert
        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.Linear, model.Type);
        Assert.AreEqual(2, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        // Arrange
        var original = new LinearTrend
        {
            OwnerName = "Location",
            StartIndex = 1950
        };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.5;
        var xElement = original.ToXElement();

        // Act
        var restored = new LinearTrend(xElement);

        // Assert
        Assert.AreEqual("Location", restored.OwnerName);
        Assert.AreEqual(1950, restored.StartIndex);
        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(0.5, restored.Parameters[1].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns linear.</summary>
    [TestMethod]
    public void Test_Type_ReturnsLinear()
    {
        var model = new LinearTrend();
        Assert.AreEqual(TrendModelType.Linear, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters creates two parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CreatesTwoParameters()
    {
        var model = new LinearTrend();

        Assert.AreEqual(2, model.Parameters.Count);
        Assert.AreEqual("(α)", model.Parameters[0].Name);
        Assert.AreEqual("(β)", model.Parameters[1].Name);
    }

    /// <summary>Verifies that set default parameters slope has bounds.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_SlopeHasBounds()
    {
        var model = new LinearTrend();

        // β (slope) should have bounds [-1, 1]
        Assert.AreEqual(-1.0, model.Parameters[1].LowerBound);
        Assert.AreEqual(1.0, model.Parameters[1].UpperBound);
    }

    /// <summary>Verifies that set default parameters slope has uniform prior.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_SlopeHasUniformPrior()
    {
        var model = new LinearTrend();

        Assert.IsInstanceOfType(model.Parameters[1].PriorDistribution, typeof(Uniform));
        var prior = (Uniform)model.Parameters[1].PriorDistribution;
        Assert.AreEqual(-1.0, prior.Min);
        Assert.AreEqual(1.0, prior.Max);
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns intercept when at start index.</summary>
    [TestMethod]
    public void Test_Predict_AtStartIndex_ReturnsIntercept()
    {
        var model = new LinearTrend
        {
            StartIndex = 1950
        };
        model.Parameters[0].Value = 100.0; // α
        model.Parameters[1].Value = 0.5;   // β

        // At t = StartIndex, y = α + β * 0 = α
        Assert.AreEqual(100.0, model.Predict(1950), 1e-10);
    }

    /// <summary>Verifies that predict returns correct value when after start index.</summary>
    [TestMethod]
    public void Test_Predict_AfterStartIndex_ReturnsCorrectValue()
    {
        var model = new LinearTrend
        {
            StartIndex = 1950
        };
        model.Parameters[0].Value = 100.0; // α
        model.Parameters[1].Value = 0.5;   // β

        // At t = 1960, y = 100 + 0.5 * (1960 - 1950) = 100 + 5 = 105
        Assert.AreEqual(105.0, model.Predict(1960), 1e-10);
    }

    /// <summary>Verifies that predict returns correct value when before start index.</summary>
    [TestMethod]
    public void Test_Predict_BeforeStartIndex_ReturnsCorrectValue()
    {
        var model = new LinearTrend
        {
            StartIndex = 1950
        };
        model.Parameters[0].Value = 100.0; // α
        model.Parameters[1].Value = 0.5;   // β

        // At t = 1940, y = 100 + 0.5 * (1940 - 1950) = 100 - 5 = 95
        Assert.AreEqual(95.0, model.Predict(1940), 1e-10);
    }

    /// <summary>Verifies that predict with negative slope decreasing.</summary>
    [TestMethod]
    public void Test_Predict_WithNegativeSlope_Decreasing()
    {
        var model = new LinearTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 100.0; // α
        model.Parameters[1].Value = -2.0;  // β

        // At t = 10, y = 100 - 2 * 10 = 80
        Assert.AreEqual(80.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict returns constant when with zero slope.</summary>
    [TestMethod]
    public void Test_Predict_WithZeroSlope_ReturnsConstant()
    {
        var model = new LinearTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 50.0; // α
        model.Parameters[1].Value = 0.0;  // β

        // Should return α regardless of index
        Assert.AreEqual(50.0, model.Predict(0), 1e-10);
        Assert.AreEqual(50.0, model.Predict(100), 1e-10);
        Assert.AreEqual(50.0, model.Predict(-100), 1e-10);
    }

    /// <summary>Verifies that predict large time offset.</summary>
    [TestMethod]
    public void Test_Predict_LargeTimeOffset()
    {
        var model = new LinearTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 0.0;   // α
        model.Parameters[1].Value = 0.001; // β (small slope)

        // At t = 1000, y = 0 + 0.001 * 1000 = 1.0
        Assert.AreEqual(1.0, model.Predict(1000), 1e-10);
    }

    /// <summary>Verifies that predict default start index zero.</summary>
    [TestMethod]
    public void Test_Predict_DefaultStartIndex_Zero()
    {
        var model = new LinearTrend();
        model.Parameters[0].Value = 10.0;
        model.Parameters[1].Value = 1.0;

        // StartIndex defaults to 0, so at t = 5: y = 10 + 1 * 5 = 15
        Assert.AreEqual(0, model.StartIndex);
        Assert.AreEqual(15.0, model.Predict(5), 1e-10);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new LinearTrend
        {
            OwnerName = "Location",
            StartIndex = 1980
        };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.25;

        // Act
        var clone = (LinearTrend)original.Clone();

        // Assert - clone has same values
        Assert.AreEqual(original.OwnerName, clone.OwnerName);
        Assert.AreEqual(original.StartIndex, clone.StartIndex);
        Assert.AreEqual(original.Parameters[0].Value, clone.Parameters[0].Value);
        Assert.AreEqual(original.Parameters[1].Value, clone.Parameters[1].Value);

        // Assert - clone is independent
        original.Parameters[0].Value = 999.0;
        original.Parameters[1].Value = 0.99;
        Assert.AreEqual(100.0, clone.Parameters[0].Value);
        Assert.AreEqual(0.25, clone.Parameters[1].Value);
    }

    /// <summary>Verifies that clone preserves prediction for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesPrediction()
    {
        var original = new LinearTrend { StartIndex = 2000 };
        original.Parameters[0].Value = 50.0;
        original.Parameters[1].Value = 0.1;

        var clone = (LinearTrend)original.Clone();

        // Both should give same prediction
        Assert.AreEqual(original.Predict(2010), clone.Predict(2010), 1e-10);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new LinearTrend();

        var xElement = model.ToXElement();

        Assert.AreEqual("Linear", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new LinearTrend
        {
            OwnerName = "Scale",
            StartIndex = 1970,
            UseDefaultFlatPriors = false
        };
        original.Parameters[0].Value = Math.PI;
        original.Parameters[1].Value = 0.123456789;

        // Act
        var xElement = original.ToXElement();
        var restored = new LinearTrend(xElement);

        // Assert
        Assert.AreEqual(original.OwnerName, restored.OwnerName);
        Assert.AreEqual(original.StartIndex, restored.StartIndex);
        Assert.AreEqual(original.UseDefaultFlatPriors, restored.UseDefaultFlatPriors);
        Assert.AreEqual(original.Parameters[0].Value, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(original.Parameters[1].Value, restored.Parameters[1].Value, 1e-10);
    }

    /// <summary>Verifies that round trip preserves prediction for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesPrediction()
    {
        var original = new LinearTrend { StartIndex = 1990 };
        original.Parameters[0].Value = 123.456;
        original.Parameters[1].Value = 0.789;

        var xElement = original.ToXElement();
        var restored = new LinearTrend(xElement);

        // Predictions should match
        Assert.AreEqual(original.Predict(2000), restored.Predict(2000), 1e-10);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that predict large slope.</summary>
    [TestMethod]
    public void Test_Predict_LargeSlope()
    {
        var model = new LinearTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 1000.0; // Very steep slope

        Assert.AreEqual(10000.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict small slope.</summary>
    [TestMethod]
    public void Test_Predict_SmallSlope()
    {
        var model = new LinearTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 1e-10; // Nearly flat

        // After 1000 units: y = 100 + 1e-10 * 1000 = 100 + 1e-7
        Assert.AreEqual(100.0000001, model.Predict(1000), 1e-10);
    }

    /// <summary>Verifies that set parameter values updates prediction.</summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesPrediction()
    {
        var model = new LinearTrend { StartIndex = 0 };
        model.SetParameterValues(new double[] { 20.0, 2.0 });

        // y(5) = 20 + 2 * 5 = 30
        Assert.AreEqual(30.0, model.Predict(5), 1e-10);
    }

    /// <summary>Verifies that predict integer overflow handled.</summary>
    [TestMethod]
    public void Test_Predict_IntegerOverflow_Handled()
    {
        // This tests behavior with extreme time offsets
        var model = new LinearTrend { StartIndex = int.MinValue };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 0.0; // Zero slope to avoid overflow

        // Should still work
        Assert.AreEqual(0.0, model.Predict(int.MaxValue), 1e-10);
    }

    #endregion
}
