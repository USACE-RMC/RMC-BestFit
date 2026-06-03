using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.TrendFunctions;

/// <summary>
/// Unit tests for the <see cref="SinusoidalTrend"/> class.
/// Tests the sinusoidal trend model y(t) = α + β × sin(2πγ(t - StartIndex) + δ).
/// </summary>
[TestClass]
public class SinusoidalTrendTests
{
    #region Constructor Tests

    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultModel()
    {
        var model = new SinusoidalTrend();

        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.Sinusoidal, model.Type);
        Assert.AreEqual(4, model.NumberOfParameters);
    }

    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var original = new SinusoidalTrend { StartIndex = 1950 };
        original.Parameters[0].Value = 100.0;  // α
        original.Parameters[1].Value = 10.0;   // β
        original.Parameters[2].Value = 0.1;    // γ
        original.Parameters[3].Value = Math.PI / 4; // δ
        var xElement = original.ToXElement();

        var restored = new SinusoidalTrend(xElement);

        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(10.0, restored.Parameters[1].Value, 1e-10);
        Assert.AreEqual(0.1, restored.Parameters[2].Value, 1e-10);
        Assert.AreEqual(Math.PI / 4, restored.Parameters[3].Value, 1e-10);
    }

    #endregion

    #region Type Tests

    [TestMethod]
    public void Test_Type_ReturnsSinusoidal()
    {
        var model = new SinusoidalTrend();
        Assert.AreEqual(TrendModelType.Sinusoidal, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    [TestMethod]
    public void Test_SetDefaultParameters_CreatesFourParameters()
    {
        var model = new SinusoidalTrend();

        Assert.AreEqual(4, model.Parameters.Count);
        Assert.AreEqual("(α)", model.Parameters[0].Name);  // Mean level
        Assert.AreEqual("(β)", model.Parameters[1].Name);  // Amplitude
        Assert.AreEqual("(γ)", model.Parameters[2].Name);  // Frequency
        Assert.AreEqual("(δ)", model.Parameters[3].Name);  // Phase
    }

    [TestMethod]
    public void Test_SetDefaultParameters_AmplitudeHasPositiveBounds()
    {
        var model = new SinusoidalTrend();

        // β (amplitude) should be non-negative [0, 1]
        Assert.AreEqual(0.0, model.Parameters[1].LowerBound);
        Assert.AreEqual(1.0, model.Parameters[1].UpperBound);
    }

    [TestMethod]
    public void Test_SetDefaultParameters_FrequencyHasPositiveBounds()
    {
        var model = new SinusoidalTrend();

        // γ (frequency) should be positive (up to Nyquist = 0.5)
        Assert.IsTrue(model.Parameters[2].LowerBound > 0);
        Assert.AreEqual(0.5, model.Parameters[2].UpperBound);
    }

    [TestMethod]
    public void Test_SetDefaultParameters_PhaseHasPeriodicBounds()
    {
        var model = new SinusoidalTrend();

        // δ (phase) should be [0, 2π]
        Assert.AreEqual(0.0, model.Parameters[3].LowerBound);
        Assert.AreEqual(2 * Math.PI, model.Parameters[3].UpperBound, 1e-10);
    }

    [TestMethod]
    public void Test_SetDefaultParameters_AllHaveUniformPriors()
    {
        var model = new SinusoidalTrend();

        Assert.IsInstanceOfType(model.Parameters[1].PriorDistribution, typeof(Uniform));
        Assert.IsInstanceOfType(model.Parameters[2].PriorDistribution, typeof(Uniform));
        Assert.IsInstanceOfType(model.Parameters[3].PriorDistribution, typeof(Uniform));
    }

    #endregion

    #region Predict Tests

    [TestMethod]
    public void Test_Predict_AtStartIndex_WithZeroPhase()
    {
        var model = new SinusoidalTrend { StartIndex = 1950 };
        model.Parameters[0].Value = 100.0;  // α
        model.Parameters[1].Value = 10.0;   // β
        model.Parameters[2].Value = 0.1;    // γ
        model.Parameters[3].Value = 0.0;    // δ (zero phase)

        // At t = StartIndex: y = α + β × sin(0) = α = 100
        Assert.AreEqual(100.0, model.Predict(1950), 1e-10);
    }

    [TestMethod]
    public void Test_Predict_SimpleSine()
    {
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;    // α (no offset)
        model.Parameters[1].Value = 1.0;    // β (unit amplitude)
        model.Parameters[2].Value = 0.25;   // γ (period = 4 time units)
        model.Parameters[3].Value = 0.0;    // δ (no phase shift)

        // y(t) = sin(2π × 0.25 × t) = sin(πt/2)
        Assert.AreEqual(0.0, model.Predict(0), 1e-10);   // sin(0) = 0
        Assert.AreEqual(1.0, model.Predict(1), 1e-10);   // sin(π/2) = 1
        Assert.AreEqual(0.0, model.Predict(2), 1e-10);   // sin(π) = 0
        Assert.AreEqual(-1.0, model.Predict(3), 1e-10);  // sin(3π/2) = -1
        Assert.AreEqual(0.0, model.Predict(4), 1e-10);   // sin(2π) = 0
    }

    [TestMethod]
    public void Test_Predict_WithMeanLevel()
    {
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 50.0;   // α (mean = 50)
        model.Parameters[1].Value = 10.0;   // β (amplitude = 10)
        model.Parameters[2].Value = 0.25;   // γ (period = 4)
        model.Parameters[3].Value = 0.0;    // δ

        // Oscillates between 40 and 60
        Assert.AreEqual(50.0, model.Predict(0), 1e-10);   // Mean
        Assert.AreEqual(60.0, model.Predict(1), 1e-10);   // Max
        Assert.AreEqual(50.0, model.Predict(2), 1e-10);   // Mean
        Assert.AreEqual(40.0, model.Predict(3), 1e-10);   // Min
    }

    [TestMethod]
    public void Test_Predict_WithPhaseShift()
    {
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 1.0;
        model.Parameters[2].Value = 0.25;
        model.Parameters[3].Value = Math.PI / 2;  // 90° phase shift (cosine)

        // y(t) = sin(πt/2 + π/2) = cos(πt/2)
        Assert.AreEqual(1.0, model.Predict(0), 1e-10);   // cos(0) = 1
        Assert.AreEqual(0.0, model.Predict(1), 1e-10);   // cos(π/2) = 0
        Assert.AreEqual(-1.0, model.Predict(2), 1e-10);  // cos(π) = -1
        Assert.AreEqual(0.0, model.Predict(3), 1e-10);   // cos(3π/2) = 0
    }

    [TestMethod]
    public void Test_Predict_AnnualCycle()
    {
        // Model annual temperature cycle
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 15.0;    // α (mean temp 15°C)
        model.Parameters[1].Value = 10.0;    // β (amplitude 10°C)
        model.Parameters[2].Value = 1.0 / 12; // γ (annual cycle over 12 months)
        model.Parameters[3].Value = -Math.PI / 2;  // δ (minimum at month 0, e.g. January)

        // Check range
        double[] temps = new double[12];
        for (int i = 0; i < 12; i++)
        {
            temps[i] = model.Predict(i);
        }

        double maxTemp = temps.Max();
        double minTemp = temps.Min();

        Assert.IsTrue(maxTemp <= 25.0);  // α + β
        Assert.IsTrue(minTemp >= 5.0);   // α - β
    }

    [TestMethod]
    public void Test_Predict_ZeroAmplitude_ReturnsConstant()
    {
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 42.0;   // α
        model.Parameters[1].Value = 0.0;    // β = 0 (no oscillation)
        model.Parameters[2].Value = 0.1;    // γ
        model.Parameters[3].Value = 1.0;    // δ

        // y(t) = 42 + 0 = 42 (constant)
        Assert.AreEqual(42.0, model.Predict(0), 1e-10);
        Assert.AreEqual(42.0, model.Predict(100), 1e-10);
        Assert.AreEqual(42.0, model.Predict(-50), 1e-10);
    }

    [TestMethod]
    public void Test_Predict_Period()
    {
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 1.0;
        model.Parameters[2].Value = 0.1;    // γ (period = 10 time units)
        model.Parameters[3].Value = 0.0;

        // Period = 1/γ = 10
        // Values should repeat every 10 units
        Assert.AreEqual(model.Predict(0), model.Predict(10), 1e-10);
        Assert.AreEqual(model.Predict(0), model.Predict(20), 1e-10);
        Assert.AreEqual(model.Predict(3), model.Predict(13), 1e-10);
    }

    #endregion

    #region Clone Tests

    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var original = new SinusoidalTrend { StartIndex = 1990 };
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 20.0;
        original.Parameters[2].Value = 0.05;
        original.Parameters[3].Value = Math.PI;

        var clone = (SinusoidalTrend)original.Clone();

        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(100.0, clone.Parameters[0].Value);
    }

    [TestMethod]
    public void Test_Clone_PreservesPrediction()
    {
        var original = new SinusoidalTrend { StartIndex = 2000 };
        original.Parameters[0].Value = 50.0;
        original.Parameters[1].Value = 10.0;
        original.Parameters[2].Value = 0.1;
        original.Parameters[3].Value = 0.5;

        var clone = (SinusoidalTrend)original.Clone();

        Assert.AreEqual(original.Predict(2010), clone.Predict(2010), 1e-10);
    }

    #endregion

    #region Serialization Tests

    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new SinusoidalTrend();
        var xElement = model.ToXElement();

        Assert.AreEqual("Sinusoidal", xElement.Attribute("Type")?.Value);
    }

    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var original = new SinusoidalTrend { StartIndex = 1980 };
        original.Parameters[0].Value = Math.PI;
        original.Parameters[1].Value = Math.E;
        original.Parameters[2].Value = 0.123;
        original.Parameters[3].Value = 1.234;

        var xElement = original.ToXElement();
        var restored = new SinusoidalTrend(xElement);

        Assert.AreEqual(original.Predict(2000), restored.Predict(2000), 1e-10);
    }

    [TestMethod]
    public void Test_RoundTrip_PreservesPhaseParameter()
    {
        var original = new SinusoidalTrend();
        original.Parameters[3].Value = 3.14159;

        var xElement = original.ToXElement();
        var restored = new SinusoidalTrend(xElement);

        Assert.AreEqual(3.14159, restored.Parameters[3].Value, 1e-10);
    }

    #endregion

    #region Physical Application Tests

    [TestMethod]
    public void Test_Predict_TidalPattern()
    {
        // Semi-diurnal tide: period ≈ 12.42 hours
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 5.0;    // Mean sea level
        model.Parameters[1].Value = 2.0;    // Tidal range
        model.Parameters[2].Value = 1.0 / 12.42;  // Frequency
        model.Parameters[3].Value = 0.0;

        // Check that high/low tides occur at expected intervals
        double period = 1.0 / model.Parameters[2].Value;
        double high = model.Predict(0) + model.Parameters[1].Value * (period / 4);

        // Max should be mean + amplitude
        Assert.IsTrue(model.Predict(0) >= model.Parameters[0].Value - model.Parameters[1].Value);
        Assert.IsTrue(model.Predict(0) <= model.Parameters[0].Value + model.Parameters[1].Value);
    }

    [TestMethod]
    public void Test_Predict_SeasonalFloodPattern()
    {
        // Seasonal variation in flood magnitude
        var model = new SinusoidalTrend { StartIndex = 1 };  // Month 1 = January
        model.Parameters[0].Value = 50000.0;  // Mean annual flow
        model.Parameters[1].Value = 30000.0;  // Seasonal amplitude
        model.Parameters[2].Value = 1.0 / 12; // Annual cycle
        model.Parameters[3].Value = Math.PI;  // Peak in summer (month 7)

        // Range should be mean ± amplitude
        for (int month = 1; month <= 12; month++)
        {
            double flow = model.Predict(month);
            Assert.IsTrue(flow >= 20000.0);  // min = 50k - 30k
            Assert.IsTrue(flow <= 80000.0);  // max = 50k + 30k
        }
    }

    [TestMethod]
    public void Test_Predict_MultiDecadalOscillation()
    {
        // Pacific Decadal Oscillation (PDO) ~ 20-30 year cycle
        var model = new SinusoidalTrend { StartIndex = 1950 };
        model.Parameters[0].Value = 0.0;     // Zero mean
        model.Parameters[1].Value = 0.5;     // Normalized index
        model.Parameters[2].Value = 1.0 / 25; // 25-year cycle
        model.Parameters[3].Value = 0.0;

        // Check 25-year periodicity
        Assert.AreEqual(model.Predict(1950), model.Predict(1975), 1e-10);
        Assert.AreEqual(model.Predict(1950), model.Predict(2000), 1e-10);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void Test_Predict_VeryHighFrequency()
    {
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 1.0;
        model.Parameters[2].Value = 0.5;    // Nyquist frequency
        model.Parameters[3].Value = 0.0;

        // At Nyquist, period = 2 time units
        Assert.AreEqual(0.0, model.Predict(0), 1e-10);   // sin(0)
        Assert.AreEqual(0.0, model.Predict(1), 1e-10);   // sin(π)
        Assert.AreEqual(0.0, model.Predict(2), 1e-10);   // sin(2π)
    }

    [TestMethod]
    public void Test_Predict_VeryLowFrequency()
    {
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 1.0;
        model.Parameters[2].Value = 0.001;  // Period = 1000 time units
        model.Parameters[3].Value = 0.0;

        // Very slow oscillation - nearly constant over short periods
        double val0 = model.Predict(0);
        double val10 = model.Predict(10);
        Assert.IsTrue(Math.Abs(val10 - val0) < 0.1);  // Little change
    }

    [TestMethod]
    public void Test_Predict_NegativeTime()
    {
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 1.0;
        model.Parameters[2].Value = 0.25;
        model.Parameters[3].Value = 0.0;

        // sin is odd function, so sin(-x) = -sin(x)
        Assert.AreEqual(-model.Predict(1), model.Predict(-1), 1e-10);
        Assert.AreEqual(-model.Predict(3), model.Predict(-3), 1e-10);
    }

    [TestMethod]
    public void Test_Predict_LargeTime()
    {
        var model = new SinusoidalTrend { StartIndex = 0 };
        model.Parameters[0].Value = 0.0;
        model.Parameters[1].Value = 1.0;
        model.Parameters[2].Value = 0.1;
        model.Parameters[3].Value = 0.0;

        // Even at large times, result should be bounded
        double result = model.Predict(1000000);
        Assert.IsTrue(result >= -1.0);
        Assert.IsTrue(result <= 1.0);
        Assert.IsFalse(double.IsNaN(result));
    }

    [TestMethod]
    public void Test_Predict_PhaseEquivalence()
    {
        var model1 = new SinusoidalTrend { StartIndex = 0 };
        model1.Parameters[0].Value = 0.0;
        model1.Parameters[1].Value = 1.0;
        model1.Parameters[2].Value = 0.1;
        model1.Parameters[3].Value = 0.0;

        var model2 = new SinusoidalTrend { StartIndex = 0 };
        model2.Parameters[0].Value = 0.0;
        model2.Parameters[1].Value = 1.0;
        model2.Parameters[2].Value = 0.1;
        model2.Parameters[3].Value = 2 * Math.PI;  // Full cycle shift

        // Phase shift by 2π should give same result
        Assert.AreEqual(model1.Predict(5), model2.Predict(5), 1e-10);
    }

    #endregion
}
