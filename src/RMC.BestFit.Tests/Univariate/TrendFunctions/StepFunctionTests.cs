using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.TrendFunctions;

/// <summary>
/// Unit tests for the <see cref="StepFunction"/> class.
/// Tests the step function model with a single change point.
/// </summary>
[TestClass]
public class StepFunctionTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new StepFunction();

        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.StepFunction, model.Type);
        Assert.AreEqual(3, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var original = new StepFunction { StartIndex = 1900 };
        original.Parameters[0].Value = 50.0;   // μ₁
        original.Parameters[1].Value = 100.0;  // μ₂
        original.Parameters[2].Value = 1950.0; // t_c
        var xElement = original.ToXElement();

        var restored = new StepFunction(xElement);

        Assert.AreEqual(50.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(100.0, restored.Parameters[1].Value, 1e-10);
        Assert.AreEqual(1950.0, restored.Parameters[2].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns step function.</summary>
    [TestMethod]
    public void Test_Type_ReturnsStepFunction()
    {
        var model = new StepFunction();
        Assert.AreEqual(TrendModelType.StepFunction, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters creates three parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CreatesThreeParameters()
    {
        var model = new StepFunction();

        Assert.AreEqual(3, model.Parameters.Count);
        Assert.AreEqual("(μ₁)", model.Parameters[0].Name);
        Assert.AreEqual("(μ₂)", model.Parameters[1].Name);
        Assert.AreEqual("(tₛ)", model.Parameters[2].Name);
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns mu1 when before change point.</summary>
    [TestMethod]
    public void Test_Predict_BeforeChangePoint_ReturnsMu1()
    {
        var model = new StepFunction { StartIndex = 1900 };
        model.Parameters[0].Value = 50.0;   // μ₁
        model.Parameters[1].Value = 100.0;  // μ₂
        model.Parameters[2].Value = 1950.0; // t_c (change point)

        // Before change point
        Assert.AreEqual(50.0, model.Predict(1900), 1e-10);
        Assert.AreEqual(50.0, model.Predict(1949), 1e-10);
        Assert.AreEqual(50.0, model.Predict(1950), 1e-10);  // At change point (≤)
    }

    /// <summary>Verifies that predict returns mu2 when after change point.</summary>
    [TestMethod]
    public void Test_Predict_AfterChangePoint_ReturnsMu2()
    {
        var model = new StepFunction { StartIndex = 1900 };
        model.Parameters[0].Value = 50.0;   // μ₁
        model.Parameters[1].Value = 100.0;  // μ₂
        model.Parameters[2].Value = 1950.0; // t_c

        // After change point
        Assert.AreEqual(100.0, model.Predict(1951), 1e-10);
        Assert.AreEqual(100.0, model.Predict(2000), 1e-10);
    }

    /// <summary>Verifies that predict at exact change point.</summary>
    [TestMethod]
    public void Test_Predict_AtExactChangePoint()
    {
        var model = new StepFunction { StartIndex = 0 };
        model.Parameters[0].Value = 10.0;  // μ₁
        model.Parameters[1].Value = 20.0;  // μ₂
        model.Parameters[2].Value = 50.0;  // t_c

        // At t=50 (t ≤ t_c), should return μ₁
        Assert.AreEqual(10.0, model.Predict(50), 1e-10);
        // Just after
        Assert.AreEqual(20.0, model.Predict(51), 1e-10);
    }

    /// <summary>Verifies that predict decrease after change point.</summary>
    [TestMethod]
    public void Test_Predict_DecreaseAfterChangePoint()
    {
        var model = new StepFunction { StartIndex = 0 };
        model.Parameters[0].Value = 100.0; // μ₁ (higher)
        model.Parameters[1].Value = 50.0;  // μ₂ (lower)
        model.Parameters[2].Value = 25.0;  // t_c

        Assert.AreEqual(100.0, model.Predict(20), 1e-10);
        Assert.AreEqual(50.0, model.Predict(30), 1e-10);
    }

    /// <summary>Verifies that predict same value both sides.</summary>
    [TestMethod]
    public void Test_Predict_SameValueBothSides()
    {
        var model = new StepFunction { StartIndex = 0 };
        model.Parameters[0].Value = 75.0;  // μ₁
        model.Parameters[1].Value = 75.0;  // μ₂ (same)
        model.Parameters[2].Value = 50.0;  // t_c

        // Should return 75 everywhere
        Assert.AreEqual(75.0, model.Predict(0), 1e-10);
        Assert.AreEqual(75.0, model.Predict(50), 1e-10);
        Assert.AreEqual(75.0, model.Predict(100), 1e-10);
    }

    /// <summary>Verifies that predict change point at start.</summary>
    [TestMethod]
    public void Test_Predict_ChangePointAtStart()
    {
        var model = new StepFunction { StartIndex = 1950 };
        model.Parameters[0].Value = 50.0;
        model.Parameters[1].Value = 100.0;
        model.Parameters[2].Value = 1950.0; // Change at start

        // At start: t = t_c, so returns μ₁
        Assert.AreEqual(50.0, model.Predict(1950), 1e-10);
        Assert.AreEqual(100.0, model.Predict(1951), 1e-10);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new StepFunction { StartIndex = 1900 };
        original.Parameters[0].Value = 50.0;
        original.Parameters[1].Value = 100.0;
        original.Parameters[2].Value = 1950.0;

        var clone = (StepFunction)original.Clone();

        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(50.0, clone.Parameters[0].Value);
    }

    /// <summary>Verifies that clone preserves prediction for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesPrediction()
    {
        var original = new StepFunction { StartIndex = 1900 };
        original.Parameters[0].Value = 50.0;
        original.Parameters[1].Value = 100.0;
        original.Parameters[2].Value = 1950.0;

        var clone = (StepFunction)original.Clone();

        Assert.AreEqual(original.Predict(1940), clone.Predict(1940));
        Assert.AreEqual(original.Predict(1960), clone.Predict(1960));
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new StepFunction();
        var xElement = model.ToXElement();

        Assert.AreEqual("StepFunction", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var original = new StepFunction
        {
            OwnerName = "Location",
            StartIndex = 1900
        };
        original.Parameters[0].Value = 50.0;
        original.Parameters[1].Value = 100.0;
        original.Parameters[2].Value = 1950.0;

        var xElement = original.ToXElement();
        var restored = new StepFunction(xElement);

        Assert.AreEqual(original.Predict(1940), restored.Predict(1940), 1e-10);
        Assert.AreEqual(original.Predict(1960), restored.Predict(1960), 1e-10);
    }

    #endregion

    #region Climate Change Scenario Tests

    /// <summary>Verifies that climate shift scenario.</summary>
    [TestMethod]
    public void Test_ClimateShift_Scenario()
    {
        // Model a climate shift in 1970
        var model = new StepFunction { StartIndex = 1950 };
        model.Parameters[0].Value = 1000.0;  // Pre-1970 mean annual flood
        model.Parameters[1].Value = 1200.0;  // Post-1970 mean (20% increase)
        model.Parameters[2].Value = 1970.0;  // Change year

        Assert.AreEqual(1000.0, model.Predict(1960), 1e-10);
        Assert.AreEqual(1000.0, model.Predict(1970), 1e-10);  // At change point
        Assert.AreEqual(1200.0, model.Predict(1980), 1e-10);
        Assert.AreEqual(1200.0, model.Predict(2020), 1e-10);
    }

    /// <summary>Verifies that dam construction scenario.</summary>
    [TestMethod]
    public void Test_DamConstruction_Scenario()
    {
        // Model flood reduction after dam construction in 1960
        var model = new StepFunction { StartIndex = 1900 };
        model.Parameters[0].Value = 50000.0;  // Pre-dam peak flows
        model.Parameters[1].Value = 35000.0;  // Post-dam peak flows (30% reduction)
        model.Parameters[2].Value = 1960.0;

        Assert.AreEqual(50000.0, model.Predict(1950), 1e-10);
        Assert.AreEqual(35000.0, model.Predict(1970), 1e-10);
    }

    #endregion
}
