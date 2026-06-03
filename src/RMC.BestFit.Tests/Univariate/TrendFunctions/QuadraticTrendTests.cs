using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.TrendFunctions;

/// <summary>
/// Unit tests for the <see cref="QuadraticTrend"/> class.
/// Tests the quadratic trend model y(t) = α + β(t - StartIndex) + γ(t - StartIndex)².
/// </summary>
[TestClass]
public class QuadraticTrendTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new QuadraticTrend();

        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.Quadratic, model.Type);
        Assert.AreEqual(3, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var original = new QuadraticTrend { StartIndex = 1950 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.5;
        original.Parameters[2].Value = 0.01;
        var xElement = original.ToXElement();

        var restored = new QuadraticTrend(xElement);

        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(0.5, restored.Parameters[1].Value, 1e-10);
        Assert.AreEqual(0.01, restored.Parameters[2].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns quadratic.</summary>
    [TestMethod]
    public void Test_Type_ReturnsQuadratic()
    {
        var model = new QuadraticTrend();
        Assert.AreEqual(TrendModelType.Quadratic, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters creates three parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CreatesThreeParameters()
    {
        var model = new QuadraticTrend();

        Assert.AreEqual(3, model.Parameters.Count);
        Assert.AreEqual("(α)", model.Parameters[0].Name);
        Assert.AreEqual("(β)", model.Parameters[1].Name);
        Assert.AreEqual("(γ)", model.Parameters[2].Name);
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns intercept when at start index.</summary>
    [TestMethod]
    public void Test_Predict_AtStartIndex_ReturnsIntercept()
    {
        var model = new QuadraticTrend { StartIndex = 1950 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 0.5;
        model.Parameters[2].Value = 0.01;

        Assert.AreEqual(100.0, model.Predict(1950), 1e-10);
    }

    /// <summary>Verifies that predict quadratic growth.</summary>
    [TestMethod]
    public void Test_Predict_QuadraticGrowth()
    {
        var model = new QuadraticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;    // α
        model.Parameters[1].Value = 0.0;    // β
        model.Parameters[2].Value = 1.0;    // γ (pure quadratic)

        // y(t) = t²
        Assert.AreEqual(0.0, model.Predict(0), 1e-10);
        Assert.AreEqual(1.0, model.Predict(1), 1e-10);
        Assert.AreEqual(4.0, model.Predict(2), 1e-10);
        Assert.AreEqual(9.0, model.Predict(3), 1e-10);
        Assert.AreEqual(100.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict parabola with vertex.</summary>
    [TestMethod]
    public void Test_Predict_ParabolaWithVertex()
    {
        // y(t) = 100 - 0.1 * t² has vertex at t=0
        var model = new QuadraticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;  // α
        model.Parameters[1].Value = 0.0;    // β
        model.Parameters[2].Value = -0.1;   // γ (downward parabola)

        Assert.AreEqual(100.0, model.Predict(0), 1e-10);   // Vertex
        Assert.AreEqual(99.9, model.Predict(1), 1e-10);
        Assert.AreEqual(99.9, model.Predict(-1), 1e-10);   // Symmetric
        Assert.AreEqual(90.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict full quadratic.</summary>
    [TestMethod]
    public void Test_Predict_FullQuadratic()
    {
        var model = new QuadraticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 10.0;   // α
        model.Parameters[1].Value = 2.0;    // β
        model.Parameters[2].Value = 0.5;    // γ

        // y(5) = 10 + 2*5 + 0.5*25 = 10 + 10 + 12.5 = 32.5
        Assert.AreEqual(32.5, model.Predict(5), 1e-10);
    }

    /// <summary>Verifies that predict with start index.</summary>
    [TestMethod]
    public void Test_Predict_WithStartIndex()
    {
        var model = new QuadraticTrend { StartIndex = 2000 };
        model.Parameters[0].Value = 50.0;   // α
        model.Parameters[1].Value = 1.0;    // β
        model.Parameters[2].Value = 0.01;   // γ

        // At t=2010: y = 50 + 1*10 + 0.01*100 = 50 + 10 + 1 = 61
        Assert.AreEqual(61.0, model.Predict(2010), 1e-10);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new QuadraticTrend { StartIndex = 1990 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.5;
        original.Parameters[2].Value = 0.01;

        var clone = (QuadraticTrend)original.Clone();

        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(100.0, clone.Parameters[0].Value);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new QuadraticTrend();
        var xElement = model.ToXElement();

        Assert.AreEqual("Quadratic", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var original = new QuadraticTrend
        {
            OwnerName = "Location",
            StartIndex = 1970
        };
        original.Parameters[0].Value = Math.PI;
        original.Parameters[1].Value = 0.123;
        original.Parameters[2].Value = 0.00456;

        var xElement = original.ToXElement();
        var restored = new QuadraticTrend(xElement);

        Assert.AreEqual(original.Predict(1980), restored.Predict(1980), 1e-10);
    }

    #endregion
}
