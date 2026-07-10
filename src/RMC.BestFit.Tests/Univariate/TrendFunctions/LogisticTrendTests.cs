using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.Univariate.TrendFunctions;

/// <summary>
/// Unit tests for the <c>LogisticTrend</c> class.
/// Tests the logistic (sigmoid) trend model y(t) = α / (1 + exp(-β(t - StartIndex))).
/// </summary>
[TestClass]
public class LogisticTrendTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new LogisticTrend();

        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.Logistic, model.Type);
        Assert.AreEqual(2, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var original = new LogisticTrend { StartIndex = 1950 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.1;
        var xElement = original.ToXElement();

        var restored = new LogisticTrend(xElement);

        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(0.1, restored.Parameters[1].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns logistic.</summary>
    [TestMethod]
    public void Test_Type_ReturnsLogistic()
    {
        var model = new LogisticTrend();
        Assert.AreEqual(TrendModelType.Logistic, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters creates two parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CreatesTwoParameters()
    {
        var model = new LogisticTrend();

        Assert.AreEqual(2, model.Parameters.Count);
        Assert.AreEqual("(α)", model.Parameters[0].Name);
        Assert.AreEqual("(β)", model.Parameters[1].Name);
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns half alpha when at start index.</summary>
    [TestMethod]
    public void Test_Predict_AtStartIndex_ReturnsHalfAlpha()
    {
        var model = new LogisticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;  // α
        model.Parameters[1].Value = 0.1;    // β > 0

        // At t=0: y = 100 / (1 + exp(0)) = 100 / 2 = 50
        Assert.AreEqual(50.0, model.Predict(0), 1e-10);
    }

    /// <summary>Verifies that predict large positive t approaches alpha.</summary>
    [TestMethod]
    public void Test_Predict_LargePositiveT_ApproachesAlpha()
    {
        var model = new LogisticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;  // α
        model.Parameters[1].Value = 0.1;    // β > 0

        // For large positive t, exp(-βt) → 0, so y → α
        double yLarge = model.Predict(100);
        Assert.IsTrue(yLarge > 99.0, $"Expected close to 100, got {yLarge}");
    }

    /// <summary>Verifies that predict large negative t approaches zero.</summary>
    [TestMethod]
    public void Test_Predict_LargeNegativeT_ApproachesZero()
    {
        var model = new LogisticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;  // α
        model.Parameters[1].Value = 0.1;    // β > 0

        // For large negative t, exp(-βt) → ∞, so y → 0
        double ySmall = model.Predict(-100);
        Assert.IsTrue(ySmall < 1.0, $"Expected close to 0, got {ySmall}");
    }

    /// <summary>Verifies that predict negative beta reverses direction.</summary>
    [TestMethod]
    public void Test_Predict_NegativeBeta_ReversesDirection()
    {
        var model = new LogisticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = -0.1;  // β < 0 (decreasing)

        // For β < 0, curve is reversed
        double yStart = model.Predict(0);
        double yLarge = model.Predict(100);
        double yNeg = model.Predict(-100);

        Assert.AreEqual(50.0, yStart, 1e-10);
        Assert.IsTrue(yLarge < yStart, "Should decrease with positive t when β < 0");
        Assert.IsTrue(yNeg > yStart, "Should increase with negative t when β < 0");
    }

    /// <summary>Verifies that predict returns constant when zero beta.</summary>
    [TestMethod]
    public void Test_Predict_ZeroBeta_ReturnsConstant()
    {
        var model = new LogisticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 0.0;  // β = 0

        // y = α / (1 + exp(0)) = α / 2 regardless of t
        Assert.AreEqual(50.0, model.Predict(0), 1e-10);
        Assert.AreEqual(50.0, model.Predict(100), 1e-10);
        Assert.AreEqual(50.0, model.Predict(-100), 1e-10);
    }

    /// <summary>Verifies that predict large beta steeper curve.</summary>
    [TestMethod]
    public void Test_Predict_LargeBeta_SteeperCurve()
    {
        var model = new LogisticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 1.0;  // Large β

        // Steeper transition
        Assert.AreEqual(50.0, model.Predict(0), 1e-10);
        Assert.IsTrue(model.Predict(5) > 99.0, "Should be near 100 at t=5 with β=1");
        Assert.IsTrue(model.Predict(-5) < 1.0, "Should be near 0 at t=-5 with β=1");
    }

    #endregion

    #region Overflow Protection Tests

    /// <summary>Verifies that predict extreme positive t no overflow.</summary>
    [TestMethod]
    public void Test_Predict_ExtremePositiveT_NoOverflow()
    {
        var model = new LogisticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 1.0;

        // Very large t should not cause overflow
        double result = model.Predict(10000);
        Assert.IsFalse(double.IsNaN(result));
        Assert.IsFalse(double.IsInfinity(result));
        Assert.IsTrue(result > 0);
    }

    /// <summary>Verifies that predict extreme negative t no underflow.</summary>
    [TestMethod]
    public void Test_Predict_ExtremeNegativeT_NoUnderflow()
    {
        var model = new LogisticTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 1.0;

        // Very large negative t should not cause issues
        double result = model.Predict(-10000);
        Assert.IsFalse(double.IsNaN(result));
        Assert.IsFalse(double.IsInfinity(result));
        Assert.IsTrue(result >= 0);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new LogisticTrend { StartIndex = 1950 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.1;

        var clone = (LogisticTrend)original.Clone();

        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(100.0, clone.Parameters[0].Value);
    }

    /// <summary>Verifies that clone preserves prediction for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesPrediction()
    {
        var original = new LogisticTrend { StartIndex = 1950 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.05;

        var clone = (LogisticTrend)original.Clone();

        Assert.AreEqual(original.Predict(1960), clone.Predict(1960), 1e-10);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new LogisticTrend();
        var xElement = model.ToXElement();

        Assert.AreEqual("Logistic", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var original = new LogisticTrend
        {
            OwnerName = "Location",
            StartIndex = 1970
        };
        original.Parameters[0].Value = 200.0;
        original.Parameters[1].Value = 0.08;

        var xElement = original.ToXElement();
        var restored = new LogisticTrend(xElement);

        Assert.AreEqual(original.Predict(1980), restored.Predict(1980), 1e-10);
        Assert.AreEqual(original.Predict(2020), restored.Predict(2020), 1e-10);
    }

    #endregion

    #region Application Scenarios

    /// <summary>Verifies that urbanization scenario.</summary>
    [TestMethod]
    public void Test_UrbanizationScenario()
    {
        // Model urbanization effect on flooding
        // Gradual transition from rural to urban catchment
        var model = new LogisticTrend { StartIndex = 1960 };
        model.Parameters[0].Value = 5000.0;  // Maximum increase in peak flow
        model.Parameters[1].Value = 0.05;    // Gradual transition

        // Before urbanization: minimal effect.
        // With β=0.05, the curve reaches 10% of asymptote (i.e., value < 500) only at
        // t < −ln(9)/β ≈ −44 years before the inflection (1916). Use 1900 (−60 years),
        // which is unambiguously inside the sub-5% region where "minimal effect" holds:
        // 5000 / (1 + exp(3)) ≈ 237.
        Assert.IsTrue(model.Predict(1900) < 500, "Early years (60 years before inflection) should have minimal effect");

        // Mid transition
        Assert.AreEqual(2500, model.Predict(1960), 1.0); // Half effect at StartIndex

        // After urbanization: near maximum effect
        Assert.IsTrue(model.Predict(2020) > 4500, "Recent years should have near-maximum effect");
    }

    #endregion
}
