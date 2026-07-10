using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.Univariate.TrendFunctions;

/// <summary>
/// Unit tests for the <c>ReciprocalTrend</c> class.
/// Tests the reciprocal trend model y(t) = 1 / (α + β(t - StartIndex)).
/// </summary>
[TestClass]
public class ReciprocalTrendTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new ReciprocalTrend();

        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.Reciprocal, model.Type);
        Assert.AreEqual(2, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var original = new ReciprocalTrend { StartIndex = 1950 };
        original.Parameters[0].Value = 1.0;
        original.Parameters[1].Value = 0.01;
        var xElement = original.ToXElement();

        var restored = new ReciprocalTrend(xElement);

        Assert.AreEqual(1.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(0.01, restored.Parameters[1].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns reciprocal.</summary>
    [TestMethod]
    public void Test_Type_ReturnsReciprocal()
    {
        var model = new ReciprocalTrend();
        Assert.AreEqual(TrendModelType.Reciprocal, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters creates two parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CreatesTwoParameters()
    {
        var model = new ReciprocalTrend();

        Assert.AreEqual(2, model.Parameters.Count);
        Assert.AreEqual("(α)", model.Parameters[0].Name);
        Assert.AreEqual("(β)", model.Parameters[1].Name);
    }

    /// <summary>Verifies that set default parameters slope has bounds.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_SlopeHasBounds()
    {
        var model = new ReciprocalTrend();

        // β should have bounds [-1, 1]
        Assert.AreEqual(-1.0, model.Parameters[1].LowerBound);
        Assert.AreEqual(1.0, model.Parameters[1].UpperBound);
    }

    /// <summary>Verifies that set default parameters slope has uniform prior.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_SlopeHasUniformPrior()
    {
        var model = new ReciprocalTrend();

        Assert.IsInstanceOfType(model.Parameters[1].PriorDistribution, typeof(Uniform));
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns one over alpha when at start index.</summary>
    [TestMethod]
    public void Test_Predict_AtStartIndex_ReturnsOneOverAlpha()
    {
        var model = new ReciprocalTrend { StartIndex = 1950 };
        model.Parameters[0].Value = 2.0;   // α
        model.Parameters[1].Value = 0.1;   // β

        // At t = StartIndex, y = 1 / (α + 0) = 1 / α = 0.5
        Assert.AreEqual(0.5, model.Predict(1950), 1e-10);
    }

    /// <summary>Verifies that predict simple reciprocal.</summary>
    [TestMethod]
    public void Test_Predict_SimpleReciprocal()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;   // α
        model.Parameters[1].Value = 1.0;   // β

        // y(t) = 1 / (1 + t)
        Assert.AreEqual(1.0, model.Predict(0), 1e-10);        // 1/1
        Assert.AreEqual(0.5, model.Predict(1), 1e-10);        // 1/2
        Assert.AreEqual(1.0 / 3.0, model.Predict(2), 1e-10);  // 1/3
        Assert.AreEqual(0.1, model.Predict(9), 1e-10);        // 1/10
    }

    /// <summary>Verifies that predict decreasing trend.</summary>
    [TestMethod]
    public void Test_Predict_DecreasingTrend()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 10.0;  // α
        model.Parameters[1].Value = 1.0;   // β

        // y(t) = 1 / (10 + t)
        Assert.AreEqual(0.1, model.Predict(0), 1e-10);        // 1/10
        Assert.AreEqual(1.0 / 20.0, model.Predict(10), 1e-10); // 1/20
        Assert.AreEqual(0.01, model.Predict(90), 1e-10);       // 1/100
    }

    /// <summary>Verifies that predict increasing trend.</summary>
    [TestMethod]
    public void Test_Predict_IncreasingTrend()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 10.0;  // α
        model.Parameters[1].Value = -0.1;  // β (negative)

        // y(t) = 1 / (10 - 0.1t)
        // At t=0: y = 0.1
        // At t=50: y = 1/(10-5) = 0.2
        Assert.AreEqual(0.1, model.Predict(0), 1e-10);
        Assert.AreEqual(0.2, model.Predict(50), 1e-10);
    }

    /// <summary>Verifies that predict with start index.</summary>
    [TestMethod]
    public void Test_Predict_WithStartIndex()
    {
        var model = new ReciprocalTrend { StartIndex = 2000 };
        model.Parameters[0].Value = 4.0;   // α
        model.Parameters[1].Value = 0.1;   // β

        // At t=2010: y = 1 / (4 + 0.1×10) = 1/5 = 0.2
        Assert.AreEqual(0.2, model.Predict(2010), 1e-10);
    }

    /// <summary>Verifies that predict returns constant when zero slope.</summary>
    [TestMethod]
    public void Test_Predict_ZeroSlope_ReturnsConstant()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 5.0;   // α
        model.Parameters[1].Value = 0.0;   // β = 0

        // y(t) = 1 / 5 = 0.2 (constant)
        Assert.AreEqual(0.2, model.Predict(0), 1e-10);
        Assert.AreEqual(0.2, model.Predict(100), 1e-10);
        Assert.AreEqual(0.2, model.Predict(-50), 1e-10);
    }

    #endregion

    #region Division by Zero Protection Tests

    /// <summary>Verifies that predict near zero denominator clamped.</summary>
    [TestMethod]
    public void Test_Predict_NearZeroDenominator_Clamped()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;   // α
        model.Parameters[1].Value = -1.0;  // β

        // At t=1: denominator = 1 - 1 = 0, should be clamped
        double result = model.Predict(1);

        Assert.IsFalse(double.IsInfinity(result));
        Assert.IsFalse(double.IsNaN(result));
    }

    /// <summary>Verifies that predict very small positive denominator.</summary>
    [TestMethod]
    public void Test_Predict_VerySmallPositiveDenominator()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1e-13;
        model.Parameters[1].Value = 0.0;

        double result = model.Predict(0);

        // Should clamp to MinDenominatorMagnitude (1e-12) and return ~1e12
        Assert.IsFalse(double.IsInfinity(result));
        Assert.IsFalse(double.IsNaN(result));
        Assert.IsTrue(result > 0);
    }

    /// <summary>Verifies that predict very small negative denominator.</summary>
    [TestMethod]
    public void Test_Predict_VerySmallNegativeDenominator()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = -1e-13;
        model.Parameters[1].Value = 0.0;

        double result = model.Predict(0);

        // Should clamp to -MinDenominatorMagnitude and return large negative
        Assert.IsFalse(double.IsInfinity(result));
        Assert.IsFalse(double.IsNaN(result));
        Assert.IsTrue(result < 0);
    }

    /// <summary>Verifies that predict exactly zero denominator.</summary>
    [TestMethod]
    public void Test_Predict_ExactlyZeroDenominator()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 0.0;

        double result = model.Predict(0);

        // Both parameters zero means denominator = 0, should be clamped
        Assert.IsFalse(double.IsInfinity(result));
        Assert.IsFalse(double.IsNaN(result));
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new ReciprocalTrend { StartIndex = 1990 };
        original.Parameters[0].Value = 5.0;
        original.Parameters[1].Value = 0.1;

        var clone = (ReciprocalTrend)original.Clone();

        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(5.0, clone.Parameters[0].Value);
    }

    /// <summary>Verifies that clone preserves prediction for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesPrediction()
    {
        var original = new ReciprocalTrend { StartIndex = 2000 };
        original.Parameters[0].Value = 10.0;
        original.Parameters[1].Value = 0.05;

        var clone = (ReciprocalTrend)original.Clone();

        Assert.AreEqual(original.Predict(2010), clone.Predict(2010), 1e-10);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new ReciprocalTrend();
        var xElement = model.ToXElement();

        Assert.AreEqual("Reciprocal", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var original = new ReciprocalTrend { StartIndex = 1980 };
        original.Parameters[0].Value = Math.PI;
        original.Parameters[1].Value = 0.05;

        var xElement = original.ToXElement();
        var restored = new ReciprocalTrend(xElement);

        Assert.AreEqual(original.Predict(2000), restored.Predict(2000), 1e-10);
    }

    #endregion

    #region Physical Application Tests

    /// <summary>Verifies that predict hyperbolic decay.</summary>
    [TestMethod]
    public void Test_Predict_HyperbolicDecay()
    {
        // Many physical processes follow hyperbolic decay: y = 1/(a + bt)
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;
        model.Parameters[1].Value = 0.1;

        // Half-value occurs when denominator doubles
        // 1 + 0.1t = 2 => t = 10
        double initial = model.Predict(0);
        double halfValue = model.Predict(10);
        Assert.AreEqual(initial / 2, halfValue, 1e-10);
    }

    /// <summary>Verifies that predict rating curve approximation.</summary>
    [TestMethod]
    public void Test_Predict_RatingCurveApproximation()
    {
        // Rating curve with asymptotic behavior at high stages
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.1;
        model.Parameters[1].Value = 0.01;

        // y(0) = 1/0.1 = 10
        // y(90) = 1/(0.1 + 0.9) = 1
        Assert.AreEqual(10.0, model.Predict(0), 1e-10);
        Assert.AreEqual(1.0, model.Predict(90), 1e-10);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that predict negative time.</summary>
    [TestMethod]
    public void Test_Predict_NegativeTime()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 10.0;
        model.Parameters[1].Value = 1.0;

        // y(-5) = 1/(10 + 1×(-5)) = 1/5 = 0.2
        Assert.AreEqual(0.2, model.Predict(-5), 1e-10);
    }

    /// <summary>Verifies that predict large time.</summary>
    [TestMethod]
    public void Test_Predict_LargeTime()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 1.0;
        model.Parameters[1].Value = 0.001;

        // y(1000) = 1/(1 + 1) = 0.5
        Assert.AreEqual(0.5, model.Predict(1000), 1e-10);
    }

    /// <summary>Verifies that predict negative intercept.</summary>
    [TestMethod]
    public void Test_Predict_NegativeIntercept()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = -5.0;  // Negative α
        model.Parameters[1].Value = 1.0;

        // y(0) = 1/(-5) = -0.2
        Assert.AreEqual(-0.2, model.Predict(0), 1e-10);
        // y(10) = 1/(-5 + 10) = 1/5 = 0.2
        Assert.AreEqual(0.2, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict asymptote.</summary>
    [TestMethod]
    public void Test_Predict_Asymptote()
    {
        var model = new ReciprocalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 0.01;

        // As t → ∞, y → 0
        double farFuture = model.Predict(100000);
        Assert.IsTrue(Math.Abs(farFuture) < 0.01);
    }

    #endregion
}
