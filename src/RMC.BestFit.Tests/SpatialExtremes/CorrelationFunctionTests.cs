using RMC.BestFit.Models.SpatialExtremes;

namespace RMC.BestFit.Tests.SpatialExtremes;

/// <summary>
/// Unit tests for spatial correlation function classes: <c>BasicExponential</c>,
/// <c>PoweredExponential</c>, and <c>Spherical</c>.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
///     Spatial correlation functions are fundamental to geostatistical models and are used in the
///     Gaussian copula and spatial regression error models within the SpatialGEV framework.
/// </para>
/// <para>
///     <b>Correlation function properties:</b>
/// </para>
/// <list type="bullet">
///     <item><description>ρ(0) = 1 (perfect correlation at zero distance)</description></item>
///     <item><description>ρ(h) ∈ [0, 1] for all h ≥ 0</description></item>
///     <item><description>ρ(h) is monotonically decreasing with distance</description></item>
///     <item><description>ρ(h) → 0 as h → ∞ (for most functions)</description></item>
/// </list>
/// </remarks>
[TestClass]
public class CorrelationFunctionTests
{
    #region BasicExponential Tests

    /// <summary>
    /// Tests BasicExponential constructor initializes correctly.
    /// </summary>
    [TestMethod]
    public void BasicExponential_Constructor_InitializesCorrectly()
    {
        // Act
        var corr = new BasicExponential();

        // Assert
        Assert.IsNotNull(corr.Parameters, "Parameters should not be null.");
        Assert.AreEqual(1, corr.NumberOfParameters, "Should have 1 parameter.");
        Assert.AreEqual("Range", corr.Parameters[0].Name, "Parameter should be Range.");
    }

    /// <summary>
    /// Tests BasicExponential returns 1 at distance 0.
    /// </summary>
    [TestMethod]
    public void BasicExponential_ZeroDistance_ReturnsOne()
    {
        // Arrange
        var corr = new BasicExponential();
        corr.SetParameterValues(new List<double> { 1.0 });

        // Act
        double result = corr.Evaluate(0);

        // Assert
        Assert.AreEqual(1.0, result, 1e-10, "Correlation at distance 0 should be 1.");
    }

    /// <summary>
    /// Tests BasicExponential returns correct value for known distance.
    /// </summary>
    [TestMethod]
    public void BasicExponential_KnownDistance_ReturnsCorrectValue()
    {
        // Arrange - ρ(h) = exp(-h/range)
        var corr = new BasicExponential();
        double range = 2.0;
        corr.SetParameterValues(new List<double> { range });
        double h = 1.0;

        // Act
        double result = corr.Evaluate(h);

        // Expected: exp(-1/2) ≈ 0.6065
        double expected = Math.Exp(-h / range);

        // Assert
        Assert.AreEqual(expected, result, 1e-10, "Should match exp(-h/range).");
    }

    /// <summary>
    /// Tests BasicExponential correlation decreases with distance.
    /// </summary>
    [TestMethod]
    public void BasicExponential_IncreasingDistance_DecreasingCorrelation()
    {
        // Arrange
        var corr = new BasicExponential();
        corr.SetParameterValues(new List<double> { 5.0 });

        // Act
        double r0 = corr.Evaluate(0);
        double r1 = corr.Evaluate(1);
        double r5 = corr.Evaluate(5);
        double r10 = corr.Evaluate(10);

        // Assert
        Assert.IsTrue(r0 > r1, "ρ(0) > ρ(1)");
        Assert.IsTrue(r1 > r5, "ρ(1) > ρ(5)");
        Assert.IsTrue(r5 > r10, "ρ(5) > ρ(10)");
    }

    /// <summary>
    /// Tests BasicExponential returns values in [0, 1].
    /// </summary>
    [TestMethod]
    public void BasicExponential_AllDistances_ReturnsValuesBetweenZeroAndOne()
    {
        // Arrange
        var corr = new BasicExponential();
        corr.SetParameterValues(new List<double> { 3.0 });
        double[] distances = { 0, 0.1, 1, 5, 10, 50, 100 };

        // Act & Assert
        foreach (double h in distances)
        {
            double result = corr.Evaluate(h);
            Assert.IsTrue(result >= 0 && result <= 1,
                $"Correlation at h={h} should be in [0,1], got {result}.");
        }
    }

    /// <summary>
    /// Tests BasicExponential throws for negative distance.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void BasicExponential_NegativeDistance_ThrowsException()
    {
        var corr = new BasicExponential();
        corr.SetParameterValues(new List<double> { 1.0 });
        corr.Evaluate(-1);
    }

    /// <summary>
    /// Tests BasicExponential SetParameterValues works correctly.
    /// </summary>
    [TestMethod]
    public void BasicExponential_SetParameterValues_UpdatesRange()
    {
        // Arrange
        var corr = new BasicExponential();

        // Act
        corr.SetParameterValues(new List<double> { 5.5 });

        // Assert
        Assert.AreEqual(5.5, corr.Parameters[0].Value, 1e-10, "Range should be updated.");
    }

    /// <summary>
    /// Tests BasicExponential SetParameterValues throws for null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void BasicExponential_SetParameterValues_Null_ThrowsException()
    {
        var corr = new BasicExponential();
        corr.SetParameterValues(null!);
    }

    /// <summary>
    /// Tests BasicExponential SetParameterValues throws for wrong count.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void BasicExponential_SetParameterValues_WrongCount_ThrowsException()
    {
        var corr = new BasicExponential();
        corr.SetParameterValues(new List<double> { 1.0, 2.0 }); // Too many
    }

    /// <summary>
    /// Tests BasicExponential Clone creates independent copy.
    /// </summary>
    [TestMethod]
    public void BasicExponential_Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new BasicExponential();
        original.SetParameterValues(new List<double> { 3.0 });

        // Act
        var clone = original.Clone();
        original.SetParameterValues(new List<double> { 10.0 });

        // Assert
        Assert.AreEqual(3.0, clone.Parameters[0].Value, 1e-10,
            "Clone should be independent of original.");
    }

    /// <summary>
    /// Tests BasicExponential Clone preserves bounds.
    /// </summary>
    [TestMethod]
    public void BasicExponential_Clone_PreservesBounds()
    {
        // Arrange
        var original = new BasicExponential();
        original.Parameters[0].LowerBound = 0.5;
        original.Parameters[0].UpperBound = 20.0;

        // Act
        var clone = original.Clone();

        // Assert
        Assert.AreEqual(0.5, clone.Parameters[0].LowerBound, 1e-10);
        Assert.AreEqual(20.0, clone.Parameters[0].UpperBound, 1e-10);
    }

    /// <summary>
    /// Tests BasicExponential practical range (where ρ ≈ 0.05).
    /// </summary>
    [TestMethod]
    public void BasicExponential_PracticalRange_IsApproximately3TimesRange()
    {
        // Arrange - Practical range is approximately 3/θ where ρ(3/θ) = exp(-3) ≈ 0.05
        var corr = new BasicExponential();
        double range = 10.0;
        corr.SetParameterValues(new List<double> { range });

        // Act
        double practicalRange = 3 * range;
        double correlationAtPracticalRange = corr.Evaluate(practicalRange);

        // Assert - Should be approximately exp(-3) ≈ 0.0498
        Assert.AreEqual(Math.Exp(-3), correlationAtPracticalRange, 1e-10,
            "Correlation at practical range should be exp(-3).");
    }

    #endregion

    #region PoweredExponential Tests

    /// <summary>
    /// Tests PoweredExponential constructor initializes correctly.
    /// </summary>
    [TestMethod]
    public void PoweredExponential_Constructor_InitializesCorrectly()
    {
        // Act
        var corr = new PoweredExponential();

        // Assert
        Assert.IsNotNull(corr.Parameters, "Parameters should not be null.");
        Assert.AreEqual(2, corr.NumberOfParameters, "Should have 2 parameters.");
        Assert.AreEqual("Range", corr.Parameters[0].Name, "First parameter should be Range.");
        Assert.AreEqual("Smoothness", corr.Parameters[1].Name, "Second parameter should be Smoothness.");
    }

    /// <summary>
    /// Tests PoweredExponential returns 1 at distance 0.
    /// </summary>
    [TestMethod]
    public void PoweredExponential_ZeroDistance_ReturnsOne()
    {
        // Arrange
        var corr = new PoweredExponential();
        corr.SetParameterValues(new List<double> { 1.0, 1.5 });

        // Act
        double result = corr.Evaluate(0);

        // Assert
        Assert.AreEqual(1.0, result, 1e-10, "Correlation at distance 0 should be 1.");
    }

    /// <summary>
    /// Tests PoweredExponential with smoothness=1 is equivalent to BasicExponential.
    /// </summary>
    [TestMethod]
    public void PoweredExponential_Smoothness1_EqualsBasicExponential()
    {
        // Arrange
        double range = 5.0;
        var powered = new PoweredExponential();
        powered.SetParameterValues(new List<double> { range, 1.0 }); // ν = 1

        var basic = new BasicExponential();
        basic.SetParameterValues(new List<double> { range });

        double[] distances = { 0.5, 1, 2, 5, 10 };

        // Act & Assert
        foreach (double h in distances)
        {
            double rPowered = powered.Evaluate(h);
            double rBasic = basic.Evaluate(h);
            Assert.AreEqual(rBasic, rPowered, 1e-10,
                $"Powered(ν=1) should equal Basic at h={h}.");
        }
    }

    /// <summary>
    /// Tests PoweredExponential with smoothness=2 is Gaussian correlation.
    /// </summary>
    [TestMethod]
    public void PoweredExponential_Smoothness2_IsGaussianCorrelation()
    {
        // Arrange - Gaussian: ρ(h) = exp(-(h/φ)²)
        var corr = new PoweredExponential();
        double range = 3.0;
        corr.SetParameterValues(new List<double> { range, 2.0 }); // ν = 2

        double h = 2.0;

        // Act
        double result = corr.Evaluate(h);
        double expected = Math.Exp(-Math.Pow(h / range, 2));

        // Assert
        Assert.AreEqual(expected, result, 1e-10,
            "Powered(ν=2) should be Gaussian correlation.");
    }

    /// <summary>
    /// Tests PoweredExponential correlation decreases with distance.
    /// </summary>
    [TestMethod]
    public void PoweredExponential_IncreasingDistance_DecreasingCorrelation()
    {
        // Arrange
        var corr = new PoweredExponential();
        corr.SetParameterValues(new List<double> { 5.0, 1.5 });

        // Act
        double r0 = corr.Evaluate(0);
        double r1 = corr.Evaluate(1);
        double r5 = corr.Evaluate(5);

        // Assert
        Assert.IsTrue(r0 > r1, "ρ(0) > ρ(1)");
        Assert.IsTrue(r1 > r5, "ρ(1) > ρ(5)");
    }

    /// <summary>
    /// Tests PoweredExponential returns values in [0, 1].
    /// </summary>
    [TestMethod]
    public void PoweredExponential_AllDistances_ReturnsValuesBetweenZeroAndOne()
    {
        // Arrange
        var corr = new PoweredExponential();
        corr.SetParameterValues(new List<double> { 3.0, 1.8 });
        double[] distances = { 0, 0.1, 1, 5, 10, 50 };

        // Act & Assert
        foreach (double h in distances)
        {
            double result = corr.Evaluate(h);
            Assert.IsTrue(result >= 0 && result <= 1,
                $"Correlation at h={h} should be in [0,1], got {result}.");
        }
    }

    /// <summary>
    /// Tests PoweredExponential throws for negative distance.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void PoweredExponential_NegativeDistance_ThrowsException()
    {
        var corr = new PoweredExponential();
        corr.SetParameterValues(new List<double> { 1.0, 1.5 });
        corr.Evaluate(-1);
    }

    /// <summary>
    /// Tests PoweredExponential SetParameterValues works correctly.
    /// </summary>
    [TestMethod]
    public void PoweredExponential_SetParameterValues_UpdatesBothParameters()
    {
        // Arrange
        var corr = new PoweredExponential();

        // Act
        corr.SetParameterValues(new List<double> { 7.5, 1.8 });

        // Assert
        Assert.AreEqual(7.5, corr.Parameters[0].Value, 1e-10, "Range should be updated.");
        Assert.AreEqual(1.8, corr.Parameters[1].Value, 1e-10, "Smoothness should be updated.");
    }

    /// <summary>
    /// Tests PoweredExponential SetParameterValues throws for wrong count.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void PoweredExponential_SetParameterValues_WrongCount_ThrowsException()
    {
        var corr = new PoweredExponential();
        corr.SetParameterValues(new List<double> { 1.0 }); // Too few
    }

    /// <summary>
    /// Tests PoweredExponential Clone creates independent copy.
    /// </summary>
    [TestMethod]
    public void PoweredExponential_Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new PoweredExponential();
        original.SetParameterValues(new List<double> { 3.0, 1.5 });

        // Act
        var clone = original.Clone();
        original.SetParameterValues(new List<double> { 10.0, 2.0 });

        // Assert
        Assert.AreEqual(3.0, clone.Parameters[0].Value, 1e-10,
            "Clone range should be independent.");
        Assert.AreEqual(1.5, clone.Parameters[1].Value, 1e-10,
            "Clone smoothness should be independent.");
    }

    /// <summary>
    /// Tests PoweredExponential higher smoothness produces smoother decay.
    /// </summary>
    [TestMethod]
    public void PoweredExponential_HigherSmoothness_SlowerNearOriginDecay()
    {
        // Arrange - Higher smoothness means flatter near origin
        var lowSmoothness = new PoweredExponential();
        lowSmoothness.SetParameterValues(new List<double> { 5.0, 1.0 });

        var highSmoothness = new PoweredExponential();
        highSmoothness.SetParameterValues(new List<double> { 5.0, 2.0 });

        // At small distances, higher smoothness decays more slowly
        double h = 0.5;

        // Act
        double rLow = lowSmoothness.Evaluate(h);
        double rHigh = highSmoothness.Evaluate(h);

        // Assert - Higher smoothness should have higher correlation at small distance
        Assert.IsTrue(rHigh > rLow,
            "Higher smoothness should decay more slowly near origin.");
    }

    #endregion

    #region Spherical Tests

    /// <summary>
    /// Tests Spherical constructor initializes correctly.
    /// </summary>
    [TestMethod]
    public void Spherical_Constructor_InitializesCorrectly()
    {
        // Act
        var corr = new Spherical();

        // Assert
        Assert.IsNotNull(corr.Parameters, "Parameters should not be null.");
        Assert.AreEqual(1, corr.NumberOfParameters, "Should have 1 parameter.");
        Assert.AreEqual("Range", corr.Parameters[0].Name, "Parameter should be Range.");
    }

    /// <summary>
    /// Tests Spherical returns 1 at distance 0.
    /// </summary>
    [TestMethod]
    public void Spherical_ZeroDistance_ReturnsOne()
    {
        // Arrange
        var corr = new Spherical();
        corr.SetParameterValues(new List<double> { 5.0 });

        // Act
        double result = corr.Evaluate(0);

        // Assert
        Assert.AreEqual(1.0, result, 1e-10, "Correlation at distance 0 should be 1.");
    }

    /// <summary>
    /// Tests Spherical returns 0 at distance equal to range.
    /// </summary>
    [TestMethod]
    public void Spherical_AtRange_ReturnsZero()
    {
        // Arrange - Spherical has compact support: ρ(φ) = 0
        var corr = new Spherical();
        double range = 5.0;
        corr.SetParameterValues(new List<double> { range });

        // Act
        double result = corr.Evaluate(range);

        // Assert
        Assert.AreEqual(0.0, result, 1e-10,
            "Spherical correlation at range should be exactly 0.");
    }

    /// <summary>
    /// Tests Spherical returns 0 beyond range.
    /// </summary>
    [TestMethod]
    public void Spherical_BeyondRange_ReturnsZero()
    {
        // Arrange
        var corr = new Spherical();
        double range = 5.0;
        corr.SetParameterValues(new List<double> { range });

        // Act
        double result1 = corr.Evaluate(range + 0.1);
        double result2 = corr.Evaluate(range * 2);
        double result3 = corr.Evaluate(range * 10);

        // Assert
        Assert.AreEqual(0.0, result1, 1e-10, "Correlation beyond range should be 0.");
        Assert.AreEqual(0.0, result2, 1e-10, "Correlation far beyond range should be 0.");
        Assert.AreEqual(0.0, result3, 1e-10, "Correlation very far beyond range should be 0.");
    }

    /// <summary>
    /// Tests Spherical returns correct value for known distance.
    /// </summary>
    [TestMethod]
    public void Spherical_KnownDistance_ReturnsCorrectValue()
    {
        // Arrange - ρ(h) = 1 - 1.5(h/φ) + 0.5(h/φ)³ for h < φ
        var corr = new Spherical();
        double range = 10.0;
        corr.SetParameterValues(new List<double> { range });
        double h = 5.0; // h/φ = 0.5

        // Act
        double result = corr.Evaluate(h);

        // Expected: 1 - 1.5(0.5) + 0.5(0.5)³ = 1 - 0.75 + 0.0625 = 0.3125
        double ratio = h / range;
        double expected = 1.0 - 1.5 * ratio + 0.5 * Math.Pow(ratio, 3);

        // Assert
        Assert.AreEqual(expected, result, 1e-10, "Should match spherical formula.");
    }

    /// <summary>
    /// Tests Spherical correlation decreases with distance (within range).
    /// </summary>
    [TestMethod]
    public void Spherical_IncreasingDistanceWithinRange_DecreasingCorrelation()
    {
        // Arrange
        var corr = new Spherical();
        double range = 10.0;
        corr.SetParameterValues(new List<double> { range });

        // Act
        double r0 = corr.Evaluate(0);
        double r2 = corr.Evaluate(2);
        double r5 = corr.Evaluate(5);
        double r8 = corr.Evaluate(8);

        // Assert
        Assert.IsTrue(r0 > r2, "ρ(0) > ρ(2)");
        Assert.IsTrue(r2 > r5, "ρ(2) > ρ(5)");
        Assert.IsTrue(r5 > r8, "ρ(5) > ρ(8)");
    }

    /// <summary>
    /// Tests Spherical returns values in [0, 1].
    /// </summary>
    [TestMethod]
    public void Spherical_AllDistances_ReturnsValuesBetweenZeroAndOne()
    {
        // Arrange
        var corr = new Spherical();
        double range = 10.0;
        corr.SetParameterValues(new List<double> { range });
        double[] distances = { 0, 1, 3, 5, 7, 9, 10, 15, 100 };

        // Act & Assert
        foreach (double h in distances)
        {
            double result = corr.Evaluate(h);
            Assert.IsTrue(result >= 0 && result <= 1,
                $"Correlation at h={h} should be in [0,1], got {result}.");
        }
    }

    /// <summary>
    /// Tests Spherical throws for negative distance.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Spherical_NegativeDistance_ThrowsException()
    {
        var corr = new Spherical();
        corr.SetParameterValues(new List<double> { 5.0 });
        corr.Evaluate(-1);
    }

    /// <summary>
    /// Tests Spherical SetParameterValues works correctly.
    /// </summary>
    [TestMethod]
    public void Spherical_SetParameterValues_UpdatesRange()
    {
        // Arrange
        var corr = new Spherical();

        // Act
        corr.SetParameterValues(new List<double> { 8.5 });

        // Assert
        Assert.AreEqual(8.5, corr.Parameters[0].Value, 1e-10, "Range should be updated.");
    }

    /// <summary>
    /// Tests Spherical Clone creates independent copy.
    /// </summary>
    [TestMethod]
    public void Spherical_Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new Spherical();
        original.SetParameterValues(new List<double> { 7.0 });

        // Act
        var clone = original.Clone();
        original.SetParameterValues(new List<double> { 15.0 });

        // Assert
        Assert.AreEqual(7.0, clone.Parameters[0].Value, 1e-10,
            "Clone should be independent of original.");
    }

    /// <summary>
    /// Tests Spherical midpoint value (h/φ = 0.5).
    /// </summary>
    [TestMethod]
    public void Spherical_Midpoint_ReturnsCorrectValue()
    {
        // Arrange - At midpoint: ρ(φ/2) = 1 - 1.5(0.5) + 0.5(0.5)³ = 0.3125
        var corr = new Spherical();
        double range = 10.0;
        corr.SetParameterValues(new List<double> { range });

        // Act
        double result = corr.Evaluate(range / 2);

        // Assert
        Assert.AreEqual(0.3125, result, 1e-10, "Midpoint correlation should be 0.3125.");
    }

    #endregion

    #region ICorrelationModel Interface Tests

    /// <summary>
    /// Tests all correlation functions implement ICorrelationModel.
    /// </summary>
    [TestMethod]
    public void AllCorrelationFunctions_ImplementICorrelationModel()
    {
        // Act
        var basic = new BasicExponential();
        var powered = new PoweredExponential();
        var spherical = new Spherical();

        // Assert
        Assert.IsInstanceOfType(basic, typeof(ICorrelationModel));
        Assert.IsInstanceOfType(powered, typeof(ICorrelationModel));
        Assert.IsInstanceOfType(spherical, typeof(ICorrelationModel));
    }

    /// <summary>
    /// Tests all correlation functions have required interface members.
    /// </summary>
    [TestMethod]
    public void AllCorrelationFunctions_HaveRequiredMembers()
    {
        // Arrange
        ICorrelationModel[] models = {
            new BasicExponential(),
            new PoweredExponential(),
            new Spherical()
        };

        // Act & Assert
        foreach (var model in models)
        {
            Assert.IsNotNull(model.Parameters, $"{model.GetType().Name}.Parameters should not be null.");
            Assert.IsTrue(model.Parameters.Count > 0, $"{model.GetType().Name}.Parameters should have elements.");

            model.SetParameterValues(model.Parameters.Select(p => p.Value).ToList());
            double result = model.Evaluate(1.0);
            Assert.IsFalse(double.IsNaN(result), $"{model.GetType().Name}.Evaluate should not return NaN.");

            var clone = model.Clone();
            Assert.IsNotNull(clone, $"{model.GetType().Name}.Clone should not return null.");
        }
    }

    #endregion

    #region Comparison Tests

    /// <summary>
    /// Tests all correlation functions return 1 at distance 0.
    /// </summary>
    [TestMethod]
    public void AllCorrelationFunctions_ZeroDistance_ReturnOne()
    {
        // Arrange
        ICorrelationModel[] models = {
            new BasicExponential(),
            new PoweredExponential(),
            new Spherical()
        };

        foreach (var model in models)
        {
            var values = model.Parameters.Select(p => p.Value).ToList();
            model.SetParameterValues(values);
        }

        // Act & Assert
        foreach (var model in models)
        {
            double result = model.Evaluate(0);
            Assert.AreEqual(1.0, result, 1e-10,
                $"{model.GetType().Name} should return 1 at distance 0.");
        }
    }

    /// <summary>
    /// Tests all correlation functions decrease monotonically (within their support).
    /// </summary>
    [TestMethod]
    public void AllCorrelationFunctions_AreMonotonicallyDecreasing()
    {
        // Arrange - Use range that's valid for all functions
        var basic = new BasicExponential();
        basic.SetParameterValues(new List<double> { 5.0 });

        var powered = new PoweredExponential();
        powered.SetParameterValues(new List<double> { 5.0, 1.5 });

        var spherical = new Spherical();
        spherical.SetParameterValues(new List<double> { 10.0 }); // Larger range for testing

        ICorrelationModel[] models = { basic, powered, spherical };
        double[] distances = { 0, 1, 2, 3, 4 }; // Within spherical's support

        // Act & Assert
        foreach (var model in models)
        {
            double prev = double.MaxValue;
            foreach (double h in distances)
            {
                double current = model.Evaluate(h);
                Assert.IsTrue(current <= prev,
                    $"{model.GetType().Name} should be monotonically decreasing.");
                prev = current;
            }
        }
    }

    /// <summary>
    /// Tests Spherical has compact support while others decay asymptotically.
    /// </summary>
    [TestMethod]
    public void CompareSupport_SphericalCompact_OthersAsymptotic()
    {
        // Arrange
        double range = 5.0;

        var basic = new BasicExponential();
        basic.SetParameterValues(new List<double> { range });

        var powered = new PoweredExponential();
        powered.SetParameterValues(new List<double> { range, 1.5 });

        var spherical = new Spherical();
        spherical.SetParameterValues(new List<double> { range });

        double farDistance = range * 2;

        // Act
        double basicFar = basic.Evaluate(farDistance);
        double poweredFar = powered.Evaluate(farDistance);
        double sphericalFar = spherical.Evaluate(farDistance);

        // Assert
        Assert.IsTrue(basicFar > 0, "BasicExponential should be > 0 at far distance.");
        Assert.IsTrue(poweredFar > 0, "PoweredExponential should be > 0 at far distance.");
        Assert.AreEqual(0.0, sphericalFar, 1e-10,
            "Spherical should be exactly 0 beyond range.");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests correlation functions handle very small distances.
    /// </summary>
    [TestMethod]
    public void AllCorrelationFunctions_VerySmallDistance_ReturnsNearOne()
    {
        // Arrange
        ICorrelationModel[] models = {
            new BasicExponential(),
            new PoweredExponential(),
            new Spherical()
        };

        double tinyDistance = 1e-10;

        foreach (var model in models)
        {
            var values = model.Parameters.Select(p => p.Value).ToList();
            model.SetParameterValues(values);

            // Act
            double result = model.Evaluate(tinyDistance);

            // Assert
            Assert.IsTrue(result > 0.99,
                $"{model.GetType().Name} should be near 1 at tiny distance, got {result}.");
        }
    }

    /// <summary>
    /// Tests correlation functions handle very large distances.
    /// </summary>
    [TestMethod]
    public void AllCorrelationFunctions_VeryLargeDistance_ReturnsNearZero()
    {
        // Arrange
        var basic = new BasicExponential();
        basic.SetParameterValues(new List<double> { 1.0 });

        var powered = new PoweredExponential();
        powered.SetParameterValues(new List<double> { 1.0, 1.5 });

        var spherical = new Spherical();
        spherical.SetParameterValues(new List<double> { 1.0 });

        double largeDistance = 1000.0;

        // Act
        double basicResult = basic.Evaluate(largeDistance);
        double poweredResult = powered.Evaluate(largeDistance);
        double sphericalResult = spherical.Evaluate(largeDistance);

        // Assert
        Assert.IsTrue(basicResult < 1e-10, "BasicExponential should be near 0 at large distance.");
        Assert.IsTrue(poweredResult < 1e-10, "PoweredExponential should be near 0 at large distance.");
        Assert.AreEqual(0.0, sphericalResult, 1e-10, "Spherical should be exactly 0 at large distance.");
    }

    /// <summary>
    /// Tests correlation functions with very small range parameter.
    /// </summary>
    [TestMethod]
    public void AllCorrelationFunctions_VerySmallRange_FastDecay()
    {
        // Arrange
        double tinyRange = 0.001;

        var basic = new BasicExponential();
        basic.SetParameterValues(new List<double> { tinyRange });

        var powered = new PoweredExponential();
        powered.SetParameterValues(new List<double> { tinyRange, 1.5 });

        var spherical = new Spherical();
        spherical.SetParameterValues(new List<double> { tinyRange });

        double distance = 1.0;

        // Act
        double basicResult = basic.Evaluate(distance);
        double poweredResult = powered.Evaluate(distance);
        double sphericalResult = spherical.Evaluate(distance);

        // Assert - All should be near 0 at distance 1 with tiny range
        Assert.IsTrue(basicResult < 0.01, "BasicExponential should decay fast with tiny range.");
        Assert.IsTrue(poweredResult < 0.01, "PoweredExponential should decay fast with tiny range.");
        Assert.AreEqual(0.0, sphericalResult, 1e-10, "Spherical should be 0 beyond tiny range.");
    }

    /// <summary>
    /// Tests correlation functions with very large range parameter.
    /// </summary>
    [TestMethod]
    public void AllCorrelationFunctions_VeryLargeRange_SlowDecay()
    {
        // Arrange
        double largeRange = 1000.0;

        var basic = new BasicExponential();
        basic.SetParameterValues(new List<double> { largeRange });

        var powered = new PoweredExponential();
        powered.SetParameterValues(new List<double> { largeRange, 1.5 });

        var spherical = new Spherical();
        spherical.SetParameterValues(new List<double> { largeRange });

        double distance = 1.0;

        // Act
        double basicResult = basic.Evaluate(distance);
        double poweredResult = powered.Evaluate(distance);
        double sphericalResult = spherical.Evaluate(distance);

        // Assert - All should be near 1 at distance 1 with large range
        Assert.IsTrue(basicResult > 0.99, "BasicExponential should decay slowly with large range.");
        Assert.IsTrue(poweredResult > 0.99, "PoweredExponential should decay slowly with large range.");
        Assert.IsTrue(sphericalResult > 0.99, "Spherical should decay slowly with large range.");
    }

    /// <summary>
    /// Tests BasicExponential handles zero/very small range parameter.
    /// </summary>
    [TestMethod]
    public void BasicExponential_ZeroRange_ReturnsZeroCorrelation()
    {
        // Arrange
        var corr = new BasicExponential();
        corr.Parameters[0].Value = 0.0; // Directly set to test edge case

        // Act
        double result = corr.Evaluate(1.0);

        // Assert - Should return 0 to indicate no correlation
        Assert.AreEqual(0.0, result, 1e-10, "Zero range should return 0 correlation.");
    }

    #endregion
}
