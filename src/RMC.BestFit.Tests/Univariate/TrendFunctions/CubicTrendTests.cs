using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.TrendFunctions;

/// <summary>
/// Unit tests for the <see cref="CubicTrend"/> class.
/// Tests the cubic trend model y(t) = α + β(t-s) + γ(t-s)² + δ(t-s)³.
/// </summary>
[TestClass]
public class CubicTrendTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new CubicTrend();

        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.Cubic, model.Type);
        Assert.AreEqual(4, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var original = new CubicTrend { StartIndex = 1950 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.5;
        original.Parameters[2].Value = 0.01;
        original.Parameters[3].Value = 0.001;
        var xElement = original.ToXElement();

        var restored = new CubicTrend(xElement);

        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(0.5, restored.Parameters[1].Value, 1e-10);
        Assert.AreEqual(0.01, restored.Parameters[2].Value, 1e-10);
        Assert.AreEqual(0.001, restored.Parameters[3].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns cubic.</summary>
    [TestMethod]
    public void Test_Type_ReturnsCubic()
    {
        var model = new CubicTrend();
        Assert.AreEqual(TrendModelType.Cubic, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters creates four parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CreatesFourParameters()
    {
        var model = new CubicTrend();

        Assert.AreEqual(4, model.Parameters.Count);
        Assert.AreEqual("(α)", model.Parameters[0].Name);
        Assert.AreEqual("(β)", model.Parameters[1].Name);
        Assert.AreEqual("(γ)", model.Parameters[2].Name);
        Assert.AreEqual("(δ)", model.Parameters[3].Name);
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns intercept when at start index.</summary>
    [TestMethod]
    public void Test_Predict_AtStartIndex_ReturnsIntercept()
    {
        var model = new CubicTrend { StartIndex = 1950 };
        model.Parameters[0].Value = 100.0;

        Assert.AreEqual(100.0, model.Predict(1950), 1e-10);
    }

    /// <summary>Verifies that predict pure cubic.</summary>
    [TestMethod]
    public void Test_Predict_PureCubic()
    {
        var model = new CubicTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;    // α
        model.Parameters[1].Value = 0.0;    // β
        model.Parameters[2].Value = 0.0;    // γ
        model.Parameters[3].Value = 1.0;    // δ (pure cubic)

        // y(t) = t³
        Assert.AreEqual(0.0, model.Predict(0), 1e-10);
        Assert.AreEqual(1.0, model.Predict(1), 1e-10);
        Assert.AreEqual(8.0, model.Predict(2), 1e-10);
        Assert.AreEqual(27.0, model.Predict(3), 1e-10);
        Assert.AreEqual(-8.0, model.Predict(-2), 1e-10);
    }

    /// <summary>Verifies that predict full cubic.</summary>
    [TestMethod]
    public void Test_Predict_FullCubic()
    {
        var model = new CubicTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;    // α
        model.Parameters[1].Value = 2.0;    // β
        model.Parameters[2].Value = 3.0;    // γ
        model.Parameters[3].Value = 4.0;    // δ

        // y(2) = 1 + 2*2 + 3*4 + 4*8 = 1 + 4 + 12 + 32 = 49
        Assert.AreEqual(49.0, model.Predict(2), 1e-10);
    }

    /// <summary>Verifies that predict inflection point.</summary>
    [TestMethod]
    public void Test_Predict_InflectionPoint()
    {
        // Cubic with inflection at origin
        var model = new CubicTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 0.0;
        model.Parameters[2].Value = 0.0;
        model.Parameters[3].Value = 1.0;

        // Inflection point at t=0: second derivative = 0
        Assert.AreEqual(0.0, model.Predict(0), 1e-10);
        // Odd symmetry through origin
        Assert.AreEqual(-model.Predict(-5), model.Predict(5), 1e-10);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new CubicTrend { StartIndex = 1990 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[3].Value = 0.001;

        var clone = (CubicTrend)original.Clone();

        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(100.0, clone.Parameters[0].Value);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new CubicTrend();
        var xElement = model.ToXElement();

        Assert.AreEqual("Cubic", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var original = new CubicTrend { StartIndex = 1980 };
        original.Parameters[0].Value = 50.0;
        original.Parameters[1].Value = 1.0;
        original.Parameters[2].Value = 0.1;
        original.Parameters[3].Value = 0.001;

        var xElement = original.ToXElement();
        var restored = new CubicTrend(xElement);

        Assert.AreEqual(original.Predict(2000), restored.Predict(2000), 1e-10);
    }

    #endregion
}
