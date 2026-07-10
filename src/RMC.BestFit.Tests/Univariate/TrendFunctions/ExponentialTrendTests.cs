using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.Univariate.TrendFunctions;

/// <summary>
/// Unit tests for the <c>ExponentialTrend</c> class.
/// Tests the exponential trend model y(t) = α × exp(β × (t - StartIndex)).
/// </summary>
[TestClass]
public class ExponentialTrendTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        // Act
        var model = new ExponentialTrend();

        // Assert
        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.Exponential, model.Type);
        Assert.AreEqual(2, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        // Arrange
        var original = new ExponentialTrend
        {
            OwnerName = "Location",
            StartIndex = 1950
        };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.05;
        var xElement = original.ToXElement();

        // Act
        var restored = new ExponentialTrend(xElement);

        // Assert
        Assert.AreEqual("Location", restored.OwnerName);
        Assert.AreEqual(1950, restored.StartIndex);
        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(0.05, restored.Parameters[1].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns exponential.</summary>
    [TestMethod]
    public void Test_Type_ReturnsExponential()
    {
        var model = new ExponentialTrend();
        Assert.AreEqual(TrendModelType.Exponential, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters creates two parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CreatesTwoParameters()
    {
        var model = new ExponentialTrend();

        Assert.AreEqual(2, model.Parameters.Count);
        Assert.AreEqual("(α)", model.Parameters[0].Name);
        Assert.AreEqual("(β)", model.Parameters[1].Name);
    }

    /// <summary>Verifies that set default parameters rate has bounds.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_RateHasBounds()
    {
        var model = new ExponentialTrend();

        // β (rate) should have bounds [-1, 1]
        Assert.AreEqual(-1.0, model.Parameters[1].LowerBound);
        Assert.AreEqual(1.0, model.Parameters[1].UpperBound);
    }

    /// <summary>Verifies that set default parameters rate has uniform prior.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_RateHasUniformPrior()
    {
        var model = new ExponentialTrend();

        Assert.IsInstanceOfType(model.Parameters[1].PriorDistribution, typeof(Uniform));
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns alpha when at start index.</summary>
    [TestMethod]
    public void Test_Predict_AtStartIndex_ReturnsAlpha()
    {
        var model = new ExponentialTrend
        {
            StartIndex = 1950
        };
        model.Parameters[0].Value = 100.0; // α
        model.Parameters[1].Value = 0.05;  // β

        // At t = StartIndex, y = α × exp(0) = α
        Assert.AreEqual(100.0, model.Predict(1950), 1e-10);
    }

    /// <summary>Verifies that predict exponential growth.</summary>
    [TestMethod]
    public void Test_Predict_ExponentialGrowth()
    {
        var model = new ExponentialTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 1.0;  // α
        model.Parameters[1].Value = 1.0;  // β

        // At t = 1, y = 1 × exp(1) = e
        Assert.AreEqual(Math.E, model.Predict(1), 1e-10);
    }

    /// <summary>Verifies that predict exponential decay.</summary>
    [TestMethod]
    public void Test_Predict_ExponentialDecay()
    {
        var model = new ExponentialTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 100.0; // α
        model.Parameters[1].Value = -0.1;  // β (negative = decay)

        // At t = 10, y = 100 × exp(-0.1 × 10) = 100 × exp(-1)
        double expected = 100.0 * Math.Exp(-1);
        Assert.AreEqual(expected, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict returns constant when with zero rate.</summary>
    [TestMethod]
    public void Test_Predict_WithZeroRate_ReturnsConstant()
    {
        var model = new ExponentialTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 50.0; // α
        model.Parameters[1].Value = 0.0;  // β = 0 means exp(0) = 1

        // Should return α regardless of index
        Assert.AreEqual(50.0, model.Predict(0), 1e-10);
        Assert.AreEqual(50.0, model.Predict(100), 1e-10);
        Assert.AreEqual(50.0, model.Predict(-100), 1e-10);
    }

    /// <summary>Verifies that predict doubling time.</summary>
    [TestMethod]
    public void Test_Predict_DoublingTime()
    {
        // Test that doubling time formula works
        double doublingTime = Math.Log(2) / 0.1; // ~6.93 time units
        var model = new ExponentialTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 0.1;

        // After one doubling time, value should be ~200.
        // The continuous doubling time is 6.9315; Math.Round overshoots to t=7,
        // where value = 100·exp(0.7) ≈ 201.375 — an unavoidable ~0.7% overshoot
        // from integer rounding. Tolerance is 2.0 (1% of 200).
        double value = model.Predict((int)Math.Round(doublingTime));
        Assert.AreEqual(200.0, value, 2.0, "Predict at integer-rounded doubling time ≈ 201 (within 1% of 2α).");
    }

    /// <summary>Verifies that predict half life.</summary>
    [TestMethod]
    public void Test_Predict_HalfLife()
    {
        // Test that half-life formula works
        double halfLife = Math.Log(2) / 0.1; // ~6.93 time units
        var model = new ExponentialTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = -0.1; // Decay

        // After one half-life, value should be ~50
        double value = model.Predict((int)Math.Round(halfLife));
        Assert.AreEqual(50.0, value, 1.0); // Allow small error
    }

    #endregion

    #region Overflow Protection Tests

    /// <summary>Verifies that predict large exponent propagates infinity.</summary>
    [TestMethod]
    public void Test_Predict_LargeExponent_PropagatesInfinity()
    {
        var model = new ExponentialTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 1.0;
        model.Parameters[1].Value = 1.0; // β = 1

        // At t = 1000, exponent = 1000 which would overflow Math.Exp.
        // Predict propagates the saturation as +∞ (since a > 0) so downstream
        // log-likelihood guards reject the unphysical extrapolation, rather
        // than treating a · 1e304 as a valid prediction.
        double result = model.Predict(1000);

        Assert.IsTrue(double.IsPositiveInfinity(result),
            "Large positive exponent with positive a should propagate +∞.");
    }

    /// <summary>Verifies that predict large negative exponent clamped.</summary>
    [TestMethod]
    public void Test_Predict_LargeNegativeExponent_Clamped()
    {
        var model = new ExponentialTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 1.0;
        model.Parameters[1].Value = -1.0; // β = -1

        // At t = 1000, exponent = -1000 which underflows to 0
        double result = model.Predict(1000);

        // Should be a very small positive number (or 0)
        Assert.IsTrue(result >= 0.0);
        Assert.IsFalse(double.IsNaN(result));
    }

    /// <summary>Verifies that predict normal range not affected by clamping.</summary>
    [TestMethod]
    public void Test_Predict_NormalRange_NotAffectedByClamping()
    {
        var model = new ExponentialTrend
        {
            StartIndex = 0
        };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 0.01;

        // At t = 50, exponent = 0.5, well within normal range
        double expected = 100.0 * Math.Exp(0.5);
        Assert.AreEqual(expected, model.Predict(50), 1e-10);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new ExponentialTrend
        {
            OwnerName = "Location",
            StartIndex = 1980
        };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.02;

        // Act
        var clone = (ExponentialTrend)original.Clone();

        // Assert - clone has same values
        Assert.AreEqual(original.OwnerName, clone.OwnerName);
        Assert.AreEqual(original.StartIndex, clone.StartIndex);
        Assert.AreEqual(original.Parameters[0].Value, clone.Parameters[0].Value);
        Assert.AreEqual(original.Parameters[1].Value, clone.Parameters[1].Value);

        // Assert - clone is independent
        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(100.0, clone.Parameters[0].Value);
    }

    /// <summary>Verifies that clone preserves prediction for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesPrediction()
    {
        var original = new ExponentialTrend { StartIndex = 2000 };
        original.Parameters[0].Value = 50.0;
        original.Parameters[1].Value = 0.03;

        var clone = (ExponentialTrend)original.Clone();

        Assert.AreEqual(original.Predict(2010), clone.Predict(2010), 1e-10);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new ExponentialTrend();

        var xElement = model.ToXElement();

        Assert.AreEqual("Exponential", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new ExponentialTrend
        {
            OwnerName = "Scale",
            StartIndex = 1970,
            UseDefaultFlatPriors = false
        };
        original.Parameters[0].Value = Math.PI;
        original.Parameters[1].Value = 0.05;

        // Act
        var xElement = original.ToXElement();
        var restored = new ExponentialTrend(xElement);

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
        var original = new ExponentialTrend { StartIndex = 1990 };
        original.Parameters[0].Value = 123.456;
        original.Parameters[1].Value = 0.01;

        var xElement = original.ToXElement();
        var restored = new ExponentialTrend(xElement);

        Assert.AreEqual(original.Predict(2000), restored.Predict(2000), 1e-10);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that predict returns zero when zero alpha.</summary>
    [TestMethod]
    public void Test_Predict_ZeroAlpha_ReturnsZero()
    {
        var model = new ExponentialTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 0.1;

        // 0 × exp(anything) = 0
        Assert.AreEqual(0.0, model.Predict(10), 1e-10);
    }

    /// <summary>Verifies that predict negative alpha.</summary>
    [TestMethod]
    public void Test_Predict_NegativeAlpha()
    {
        var model = new ExponentialTrend { StartIndex = 0 };
        model.Parameters[0].Value = -100.0;
        model.Parameters[1].Value = 0.1;

        // Negative α × exp(positive) = negative
        double result = model.Predict(10);
        Assert.IsTrue(result < 0);
        Assert.AreEqual(-100.0 * Math.Exp(1.0), result, 1e-10);
    }

    /// <summary>Verifies that set parameter values updates prediction.</summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesPrediction()
    {
        var model = new ExponentialTrend { StartIndex = 0 };
        model.SetParameterValues(new double[] { 100.0, 0.1 });

        double expected = 100.0 * Math.Exp(0.5);
        Assert.AreEqual(expected, model.Predict(5), 1e-10);
    }

    /// <summary>Verifies that predict small rate approximately linear.</summary>
    [TestMethod]
    public void Test_Predict_SmallRate_Approximately_Linear()
    {
        // For small β, exp(βt) ≈ 1 + βt
        var model = new ExponentialTrend { StartIndex = 0 };
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 0.001; // Very small rate

        // At t = 10: exp(0.01) ≈ 1.01005
        double exponentialResult = model.Predict(10);
        double linearApprox = 100.0 * (1 + 0.001 * 10); // 101.0

        // Should be close to linear approximation
        Assert.AreEqual(linearApprox, exponentialResult, 0.1);
    }

    #endregion
}
