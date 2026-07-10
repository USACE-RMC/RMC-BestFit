using RMC.BestFit.Models.SpatialExtremes;

namespace RMC.BestFit.Tests.SpatialExtremes;

/// <summary>
/// Unit tests for the three spatial correlation kernels:
/// <c>BasicExponential</c>, <c>PoweredExponential</c>, and <c>Spherical</c>.
/// Each implements <c>ICorrelationModel</c> with an <c>Evaluate(distance)</c> method
/// returning a correlation value in <c>[0, 1]</c>.
/// </summary>
[TestClass]
public class SpatialCorrelationTests
{
    #region BasicExponential — ρ(h) = exp(-h / range)

    /// <summary>Verifies that basic exponential defaults one parameter correct type.</summary>
    [TestMethod]
    public void BasicExponential_Defaults_OneParameterCorrectType()
    {
        var model = new BasicExponential();

        Assert.AreEqual(1, model.NumberOfParameters);
        Assert.AreEqual("Range", model.Parameters[0].Name);
        Assert.AreEqual(CorrelationFunctionType.Exponential, model.Type);
    }

    /// <summary>Verifies that basic exponential returns one when at zero distance.</summary>
    [TestMethod]
    public void BasicExponential_AtZeroDistance_ReturnsOne()
    {
        var model = new BasicExponential();

        Assert.AreEqual(1.0, model.Evaluate(0.0));
    }

    /// <summary>Verifies that basic exponential returns exp minus one when at range distance.</summary>
    [TestMethod]
    public void BasicExponential_AtRangeDistance_ReturnsExpMinusOne()
    {
        var model = new BasicExponential();
        model.SetParameterValues(new[] { 10.0 });

        // ρ(range) = exp(-1) ≈ 0.3679
        Assert.AreEqual(Math.Exp(-1.0), model.Evaluate(10.0), 1e-12);
    }

    /// <summary>Verifies that basic exponential large distance decays toward zero.</summary>
    [TestMethod]
    public void BasicExponential_LargeDistance_DecaysTowardZero()
    {
        var model = new BasicExponential();
        model.SetParameterValues(new[] { 1.0 });

        Assert.IsTrue(model.Evaluate(100.0) < 1e-30,
            "ρ should decay essentially to zero at 100×range.");
    }

    /// <summary>Verifies that basic exponential throws when evaluate negative distance.</summary>
    [TestMethod]
    public void BasicExponential_Evaluate_NegativeDistance_Throws()
    {
        var model = new BasicExponential();

        Assert.ThrowsException<ArgumentException>(() => model.Evaluate(-1.0));
    }

    /// <summary>Verifies that basic exponential returns zero when evaluate non positive range.</summary>
    [TestMethod]
    public void BasicExponential_Evaluate_NonPositiveRange_ReturnsZero()
    {
        var model = new BasicExponential();
        model.SetParameterValues(new[] { 0.0 });

        Assert.AreEqual(0.0, model.Evaluate(1.0));
    }

    /// <summary>Verifies that basic exponential throws when set parameter values wrong count.</summary>
    [TestMethod]
    public void BasicExponential_SetParameterValues_WrongCount_Throws()
    {
        var model = new BasicExponential();

        Assert.ThrowsException<ArgumentException>(
            () => model.SetParameterValues(new[] { 1.0, 2.0 }));
    }

    /// <summary>Verifies that basic exponential throws when set parameter values null.</summary>
    [TestMethod]
    public void BasicExponential_SetParameterValues_Null_Throws()
    {
        var model = new BasicExponential();

        Assert.ThrowsException<ArgumentNullException>(
            () => model.SetParameterValues(null!));
    }

    /// <summary>Verifies that basic exponential clone independent values.</summary>
    [TestMethod]
    public void BasicExponential_Clone_IndependentValues()
    {
        var original = new BasicExponential();
        original.SetParameterValues(new[] { 25.0 });

        var clone = original.Clone();
        original.SetParameterValues(new[] { 1.0 });

        Assert.AreEqual(25.0, clone.Parameters[0].Value, "Clone must not share parameter state.");
    }

    /// <summary>Verifies that basic exponential to X element contains type attribute.</summary>
    [TestMethod]
    public void BasicExponential_ToXElement_ContainsTypeAttribute()
    {
        var model = new BasicExponential();

        var xml = model.ToXElement();

        Assert.AreEqual(nameof(BasicExponential), xml.Name.LocalName);
        Assert.AreEqual(CorrelationFunctionType.Exponential.ToString(),
            xml.Attribute("Type")?.Value);
    }

    #endregion

    #region PoweredExponential — ρ(h) = exp(-(h/φ)^ν)

    /// <summary>Verifies that powered exponential defaults two parameters correct type.</summary>
    [TestMethod]
    public void PoweredExponential_Defaults_TwoParametersCorrectType()
    {
        var model = new PoweredExponential();

        Assert.AreEqual(2, model.NumberOfParameters);
        Assert.AreEqual("Range", model.Parameters[0].Name);
        Assert.AreEqual("Smoothness", model.Parameters[1].Name);
        Assert.AreEqual(CorrelationFunctionType.PoweredExponential, model.Type);
    }

    /// <summary>Verifies that powered exponential returns one when at zero distance.</summary>
    [TestMethod]
    public void PoweredExponential_AtZeroDistance_ReturnsOne()
    {
        var model = new PoweredExponential();

        Assert.AreEqual(1.0, model.Evaluate(0.0));
    }

    /// <summary>Verifies that powered exponential with smoothness1 reduces to exponential.</summary>
    [TestMethod]
    public void PoweredExponential_WithSmoothness1_ReducesToExponential()
    {
        var model = new PoweredExponential();
        model.SetParameterValues(new[] { 10.0, 1.0 }); // ν = 1 → exp(-h/range)

        var basicExp = new BasicExponential();
        basicExp.SetParameterValues(new[] { 10.0 });

        Assert.AreEqual(basicExp.Evaluate(5.0), model.Evaluate(5.0), 1e-12);
        Assert.AreEqual(basicExp.Evaluate(15.0), model.Evaluate(15.0), 1e-12);
    }

    /// <summary>Verifies that powered exponential is gaussian when with smoothness2.</summary>
    [TestMethod]
    public void PoweredExponential_WithSmoothness2_IsGaussian()
    {
        var model = new PoweredExponential();
        model.SetParameterValues(new[] { 10.0, 2.0 });

        // ρ(h=range) with ν=2 = exp(-1) ≈ 0.3679
        Assert.AreEqual(Math.Exp(-1.0), model.Evaluate(10.0), 1e-12);
    }

    /// <summary>Verifies that powered exponential returns zero when non positive range.</summary>
    [TestMethod]
    public void PoweredExponential_NonPositiveRange_ReturnsZero()
    {
        var model = new PoweredExponential();
        model.SetParameterValues(new[] { 0.0, 1.5 });

        Assert.AreEqual(0.0, model.Evaluate(1.0));
    }

    /// <summary>Verifies that powered exponential throws when negative distance.</summary>
    [TestMethod]
    public void PoweredExponential_NegativeDistance_Throws()
    {
        var model = new PoweredExponential();

        Assert.ThrowsException<ArgumentException>(() => model.Evaluate(-1.0));
    }

    /// <summary>Verifies that powered exponential clone independent values.</summary>
    [TestMethod]
    public void PoweredExponential_Clone_IndependentValues()
    {
        var original = new PoweredExponential();
        original.SetParameterValues(new[] { 25.0, 1.8 });

        var clone = original.Clone();
        original.SetParameterValues(new[] { 1.0, 0.5 });

        Assert.AreEqual(25.0, clone.Parameters[0].Value);
        Assert.AreEqual(1.8, clone.Parameters[1].Value);
    }

    #endregion

    #region Spherical — ρ(h) = 1 − 1.5(h/φ) + 0.5(h/φ)³ for h ≤ φ, 0 otherwise

    /// <summary>Verifies that spherical defaults one parameter correct type.</summary>
    [TestMethod]
    public void Spherical_Defaults_OneParameterCorrectType()
    {
        var model = new Spherical();

        Assert.AreEqual(1, model.NumberOfParameters);
        Assert.AreEqual("Range", model.Parameters[0].Name);
        Assert.AreEqual(CorrelationFunctionType.Spherical, model.Type);
    }

    /// <summary>Verifies that spherical returns one when at zero distance.</summary>
    [TestMethod]
    public void Spherical_AtZeroDistance_ReturnsOne()
    {
        var model = new Spherical();

        Assert.AreEqual(1.0, model.Evaluate(0.0));
    }

    /// <summary>Verifies that spherical returns zero when at or beyond range.</summary>
    [TestMethod]
    public void Spherical_AtOrBeyondRange_ReturnsZero()
    {
        var model = new Spherical();
        model.SetParameterValues(new[] { 10.0 });

        Assert.AreEqual(0.0, model.Evaluate(10.0), "At h = range, correlation is 0.");
        Assert.AreEqual(0.0, model.Evaluate(15.0), "Beyond range, correlation is 0.");
    }

    /// <summary>Verifies that spherical matches closed form for half range.</summary>
    [TestMethod]
    public void Spherical_HalfRange_MatchesClosedForm()
    {
        var model = new Spherical();
        model.SetParameterValues(new[] { 10.0 });

        // ratio = 0.5: ρ = 1 - 1.5*0.5 + 0.5*0.125 = 1 - 0.75 + 0.0625 = 0.3125
        Assert.AreEqual(0.3125, model.Evaluate(5.0), 1e-12);
    }

    /// <summary>Verifies that spherical returns zero when non positive range.</summary>
    [TestMethod]
    public void Spherical_NonPositiveRange_ReturnsZero()
    {
        var model = new Spherical();
        model.SetParameterValues(new[] { 0.0 });

        Assert.AreEqual(0.0, model.Evaluate(1.0));
    }

    /// <summary>Verifies that spherical monotonic decay on sweep.</summary>
    [TestMethod]
    public void Spherical_MonotonicDecay_OnSweep()
    {
        var model = new Spherical();
        model.SetParameterValues(new[] { 10.0 });

        double prev = double.PositiveInfinity;
        for (double h = 0.1; h < 10.0; h += 0.1)
        {
            double rho = model.Evaluate(h);
            Assert.IsTrue(rho < prev, $"ρ should be strictly decreasing on (0, range). prev={prev}, rho={rho} at h={h}.");
            prev = rho;
        }
    }

    /// <summary>Verifies that spherical throws when negative distance.</summary>
    [TestMethod]
    public void Spherical_NegativeDistance_Throws()
    {
        var model = new Spherical();

        Assert.ThrowsException<ArgumentException>(() => model.Evaluate(-1.0));
    }

    /// <summary>Verifies that spherical clone independent values.</summary>
    [TestMethod]
    public void Spherical_Clone_IndependentValues()
    {
        var original = new Spherical();
        original.SetParameterValues(new[] { 25.0 });

        var clone = original.Clone();
        original.SetParameterValues(new[] { 1.0 });

        Assert.AreEqual(25.0, clone.Parameters[0].Value);
    }

    #endregion

    #region Cross-kernel invariants

    /// <summary>Verifies that all kernels output is in unit interval over sweep.</summary>
    [TestMethod]
    public void AllKernels_OutputIsInUnitInterval_OverSweep()
    {
        var kernels = new ICorrelationModel[]
        {
            new BasicExponential(),
            new PoweredExponential(),
            new Spherical()
        };

        foreach (var k in kernels)
        {
            for (double h = 0.0; h <= 50.0; h += 0.5)
            {
                double rho = k.Evaluate(h);
                Assert.IsTrue(rho >= 0.0 && rho <= 1.0,
                    $"{k.GetType().Name}.Evaluate({h}) = {rho} is outside [0, 1].");
            }
        }
    }

    #endregion
}
