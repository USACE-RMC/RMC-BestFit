using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.TrendFunctions;

/// <summary>
/// Unit tests for the <see cref="PowerTrend"/> class.
/// Tests the power-law trend model y(t) = α(t - StartIndex)^β.
/// </summary>
[TestClass]
public class PowerTrendTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new PowerTrend();

        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.Power, model.Type);
        Assert.AreEqual(2, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var original = new PowerTrend { StartIndex = 1950 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.5;
        var xElement = original.ToXElement();

        var restored = new PowerTrend(xElement);

        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(0.5, restored.Parameters[1].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns power.</summary>
    [TestMethod]
    public void Test_Type_ReturnsPower()
    {
        var model = new PowerTrend();
        Assert.AreEqual(TrendModelType.Power, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters creates two parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CreatesTwoParameters()
    {
        var model = new PowerTrend();

        Assert.AreEqual(2, model.Parameters.Count);
        Assert.AreEqual("(α)", model.Parameters[0].Name);
        Assert.AreEqual("(β)", model.Parameters[1].Name);
    }

    /// <summary>Verifies that set default parameters exponent has bounds.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_ExponentHasBounds()
    {
        var model = new PowerTrend();

        // β (exponent) should have bounds [-5, 5]
        Assert.AreEqual(-5.0, model.Parameters[1].LowerBound);
        Assert.AreEqual(5.0, model.Parameters[1].UpperBound);
    }

    /// <summary>Verifies that set default parameters exponent has uniform prior.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_ExponentHasUniformPrior()
    {
        var model = new PowerTrend();

        Assert.IsInstanceOfType(model.Parameters[1].PriorDistribution, typeof(Uniform));
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns zero when at start index.</summary>
    [TestMethod]
    public void Test_Predict_AtStartIndex_ReturnsZero()
    {
        var model = new PowerTrend { StartIndex = 1950 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 2.0;

        // At t = StartIndex, (t - StartIndex) = 0, so y = α × 0^β = 0
        Assert.AreEqual(0.0, model.Predict(1950), 1e-10);
    }

    /// <summary>Verifies that predict square law.</summary>
    [TestMethod]
    public void Test_Predict_SquareLaw()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;   // α
        model.Parameters[1].Value = 2.0;   // β (square)

        // y(t) = t²
        Assert.AreEqual(0.0, model.Predict(0), 1e-10);
        Assert.AreEqual(1.0, model.Predict(1), 1e-10);
        Assert.AreEqual(4.0, model.Predict(2), 1e-10);
        Assert.AreEqual(9.0, model.Predict(3), 1e-10);
        Assert.AreEqual(100.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict cube law.</summary>
    [TestMethod]
    public void Test_Predict_CubeLaw()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 2.0;   // α
        model.Parameters[1].Value = 3.0;   // β (cube)

        // y(t) = 2 × t³
        Assert.AreEqual(0.0, model.Predict(0), 1e-10);
        Assert.AreEqual(2.0, model.Predict(1), 1e-10);
        Assert.AreEqual(16.0, model.Predict(2), 1e-10);  // 2 × 8
        Assert.AreEqual(54.0, model.Predict(3), 1e-10);  // 2 × 27
    }

    /// <summary>Verifies that predict square root.</summary>
    [TestMethod]
    public void Test_Predict_SquareRoot()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;   // α
        model.Parameters[1].Value = 0.5;   // β (square root)

        // y(t) = √t
        Assert.AreEqual(0.0, model.Predict(0), 1e-10);
        Assert.AreEqual(1.0, model.Predict(1), 1e-10);
        Assert.AreEqual(Math.Sqrt(2), model.Predict(2), 1e-10);
        Assert.AreEqual(3.0, model.Predict(9), 1e-10);
    }

    /// <summary>Verifies that predict inverse law.</summary>
    [TestMethod]
    public void Test_Predict_InverseLaw()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0; // α
        model.Parameters[1].Value = -1.0;  // β (inverse)

        // y(t) = 100 / t
        Assert.AreEqual(100.0, model.Predict(1), 1e-10);
        Assert.AreEqual(50.0, model.Predict(2), 1e-10);
        Assert.AreEqual(10.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict linear case.</summary>
    [TestMethod]
    public void Test_Predict_LinearCase()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 5.0;   // α
        model.Parameters[1].Value = 1.0;   // β = 1 (linear)

        // y(t) = 5t
        Assert.AreEqual(0.0, model.Predict(0), 1e-10);
        Assert.AreEqual(5.0, model.Predict(1), 1e-10);
        Assert.AreEqual(50.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict constant case.</summary>
    [TestMethod]
    public void Test_Predict_ConstantCase()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 42.0;  // α
        model.Parameters[1].Value = 0.0;   // β = 0, so t^0 = 1

        // y(t) = 42 × t^0 = 42 (for t > 0)
        // Note: 0^0 is undefined but typically treated as 1 in this context
        Assert.AreEqual(42.0, model.Predict(1), 1e-10);
        Assert.AreEqual(42.0, model.Predict(10), 1e-10);
        Assert.AreEqual(42.0, model.Predict(100), 1e-10);
    }

    /// <summary>Verifies that predict with start index.</summary>
    [TestMethod]
    public void Test_Predict_WithStartIndex()
    {
        var model = new PowerTrend { StartIndex = 2000 };
        model.Parameters[0].Value = 1.0;  // α
        model.Parameters[1].Value = 2.0;  // β

        // At t=2010: y = 1 × (10)^2 = 100
        Assert.AreEqual(100.0, model.Predict(2010), 1e-10);
    }

    #endregion

    #region Negative Index Protection Tests

    /// <summary>Verifies that predict negative t clamps to zero.</summary>
    [TestMethod]
    public void Test_Predict_NegativeT_ClampsToZero()
    {
        var model = new PowerTrend { StartIndex = 2000 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 0.5;

        // At t=1990: t - StartIndex = -10, but should be clamped to 0
        // y = 100 × 0^0.5 = 0
        Assert.AreEqual(0.0, model.Predict(1990), 1e-10);
    }

    /// <summary>Verifies that predict negative t avoid complex values.</summary>
    [TestMethod]
    public void Test_Predict_NegativeT_AvoidComplexValues()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;
        model.Parameters[1].Value = 0.5; // Square root of negative would be complex

        // For index < StartIndex, the implementation clamps t to 0
        double result = model.Predict(-5);
        Assert.IsFalse(double.IsNaN(result));
        Assert.AreEqual(0.0, result, 1e-10);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new PowerTrend { StartIndex = 1990 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 1.5;

        var clone = (PowerTrend)original.Clone();

        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(100.0, clone.Parameters[0].Value);
    }

    /// <summary>Verifies that clone preserves prediction for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesPrediction()
    {
        var original = new PowerTrend { StartIndex = 2000 };
        original.Parameters[0].Value = 50.0;
        original.Parameters[1].Value = 0.5;

        var clone = (PowerTrend)original.Clone();

        Assert.AreEqual(original.Predict(2010), clone.Predict(2010), 1e-10);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new PowerTrend();
        var xElement = model.ToXElement();

        Assert.AreEqual("Power", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var original = new PowerTrend { StartIndex = 1980 };
        original.Parameters[0].Value = 50.0;
        original.Parameters[1].Value = 1.5;

        var xElement = original.ToXElement();
        var restored = new PowerTrend(xElement);

        Assert.AreEqual(original.Predict(2000), restored.Predict(2000), 1e-10);
    }

    #endregion

    #region Physical Application Tests

    /// <summary>Verifies that predict sediment transport.</summary>
    [TestMethod]
    public void Test_Predict_SedimentTransport()
    {
        // Sediment transport often follows power law: Q = α × V^β
        // where Q is sediment discharge and V is velocity
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.001;  // α (coefficient)
        model.Parameters[1].Value = 2.5;    // β (typically 2-3)

        // At velocity 10: Q = 0.001 × 10^2.5 ≈ 0.316
        double expected = 0.001 * Math.Pow(10, 2.5);
        Assert.AreEqual(expected, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict area scaling.</summary>
    [TestMethod]
    public void Test_Predict_AreaScaling()
    {
        // Drainage area scaling: Q = α × A^β
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;
        model.Parameters[1].Value = 0.75;  // Regional exponent

        // At area 100: Q = 1.0 × 100^0.75 ≈ 31.62
        double expected = Math.Pow(100, 0.75);
        Assert.AreEqual(expected, model.Predict(100), 1e-10);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that predict zero alpha.</summary>
    [TestMethod]
    public void Test_Predict_ZeroAlpha()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 2.0;

        // 0 × anything = 0
        Assert.AreEqual(0.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict negative alpha.</summary>
    [TestMethod]
    public void Test_Predict_NegativeAlpha()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = -1.0;
        model.Parameters[1].Value = 2.0;

        // y(t) = -1 × t² (negative parabola)
        Assert.AreEqual(-4.0, model.Predict(2), 1e-10);
        Assert.AreEqual(-100.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict large power.</summary>
    [TestMethod]
    public void Test_Predict_LargePower()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;
        model.Parameters[1].Value = 5.0;  // At upper bound

        // y(t) = t^5
        Assert.AreEqual(Math.Pow(2, 5), model.Predict(2), 1e-10);  // 32
    }

    /// <summary>Verifies that predict negative power.</summary>
    [TestMethod]
    public void Test_Predict_NegativePower()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;
        model.Parameters[1].Value = -2.0;

        // y(t) = t^-2 = 1/t²
        Assert.AreEqual(0.25, model.Predict(2), 1e-10);  // 1/4
        Assert.AreEqual(0.01, model.Predict(10), 1e-10); // 1/100
    }

    /// <summary>Verifies that predict very small power.</summary>
    [TestMethod]
    public void Test_Predict_VerySmallPower()
    {
        var model = new PowerTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;
        model.Parameters[1].Value = 0.001;

        // y(t) = t^0.001 ≈ 1 for reasonable t values
        double result = model.Predict(100);
        double expected = Math.Pow(100, 0.001);  // ≈ 1.0046
        Assert.AreEqual(expected, result, 1e-10);
    }

    #endregion
}
