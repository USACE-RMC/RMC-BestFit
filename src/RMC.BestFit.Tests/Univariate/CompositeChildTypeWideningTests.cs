using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.LinkFunctions;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Tests for the type-widening of <c>WeightedUnivariateAnalysis.UnivariateAnalysis</c>
/// from concrete <c>UnivariateAnalysis</c> to <c>IUnivariateAnalysis</c>,
/// and the composite-of-composite guards added at the setter, the
/// <c>CompositeAnalysis.Validate</c> entry point, and the XElement deserializer.
/// </summary>
/// <remarks>
/// <para>
/// Issue 3 of the bug-fixes-and-enhancements plan: BMA / Composite analyses must accept
/// any <c>IUnivariateAnalysis</c> sibling — most importantly
/// <c>Bulletin17CAnalysis</c> — but <c>CompositeAnalysis</c> itself is
/// rejected to avoid circular references and undefined nested-weighting behavior.
/// </para>
/// </remarks>
[TestClass]
public class CompositeChildTypeWideningTests
{
    #region Inline test fixtures

    private const int FixtureSize = 30;

    private static readonly double[] InlineFloodData = new Normal(15000.0, 5000.0)
        .GenerateRandomValues(FixtureSize, 12345);

    /// <summary>
    /// Creates data Frame.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame CreateDataFrame()
    {
        var df = new BestFitDataFrame();
        for (int i = 0; i < InlineFloodData.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, InlineFloodData[i]));
        return df;
    }

    /// <summary>
    /// Creates univariate Analysis.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static UnivariateAnalysis CreateUnivariateAnalysis()
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        return new UnivariateAnalysis(dist);
    }

    /// <summary>
    /// Creates b17 C Analysis.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static Bulletin17CAnalysis CreateB17CAnalysis()
    {
        var model = new Bulletin17CDistribution(CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII);
        return new Bulletin17CAnalysis(model);
    }

    #endregion

    #region WeightedUnivariateAnalysis type widening

    /// <summary>
    /// The setter accepts a <c>UnivariateAnalysis</c> instance — the canonical
    /// composite child type that has always been allowed.
    /// </summary>
    [TestMethod]
    public void WeightedUnivariateAnalysis_AcceptsUnivariateAnalysis()
    {
        var ua = CreateUnivariateAnalysis();
        var wua = new WeightedUnivariateAnalysis();

        wua.UnivariateAnalysis = ua;

        Assert.AreSame(ua, wua.UnivariateAnalysis,
            "The widened setter must continue to accept UnivariateAnalysis instances.");
    }

    /// <summary>
    /// The setter accepts a <c>Bulletin17CAnalysis</c> instance — Issue 3's
    /// primary motivation. Previously this would have failed at the cast site or, worse,
    /// thrown <c>System.InvalidCastException</c> on Open.
    /// </summary>
    [TestMethod]
    public void WeightedUnivariateAnalysis_AcceptsBulletin17CAnalysis()
    {
        var b17c = CreateB17CAnalysis();
        var wua = new WeightedUnivariateAnalysis();

        wua.UnivariateAnalysis = b17c;

        Assert.AreSame(b17c, wua.UnivariateAnalysis,
            "Bulletin17CAnalysis is an IUnivariateAnalysis sibling and must be accepted.");
    }

    /// <summary>
    /// The constructor overload also accepts <c>IUnivariateAnalysis</c>.
    /// </summary>
    [TestMethod]
    public void WeightedUnivariateAnalysis_Constructor_AcceptsBulletin17CAnalysis()
    {
        var b17c = CreateB17CAnalysis();

        var wua = new WeightedUnivariateAnalysis(b17c, weight: 0.4);

        Assert.AreSame(b17c, wua.UnivariateAnalysis);
        Assert.AreEqual(0.4, wua.Weight);
    }

    #endregion

    #region Composite-of-composite guards

    /// <summary>
    /// Assigning a <c>CompositeAnalysis</c> to the setter throws
    /// <c>System.ArgumentException</c>. This is the primary defense against
    /// circular references from a composite that ends up containing itself.
    /// </summary>
    [TestMethod]
    public void WeightedUnivariateAnalysis_Setter_RejectsCompositeAnalysis()
    {
        var composite = new CompositeAnalysis();
        var wua = new WeightedUnivariateAnalysis();

        var ex = Assert.ThrowsException<System.ArgumentException>(() =>
        {
            wua.UnivariateAnalysis = composite;
        });

        Assert.IsTrue(ex.Message.Contains("CompositeAnalysis", System.StringComparison.OrdinalIgnoreCase),
            "Exception message should reference CompositeAnalysis so the user knows what was rejected.");
    }

    /// <summary>
    /// The two-arg constructor also rejects a <c>CompositeAnalysis</c> argument,
    /// since it routes through the same setter.
    /// </summary>
    [TestMethod]
    public void WeightedUnivariateAnalysis_Constructor_RejectsCompositeAnalysis()
    {
        var composite = new CompositeAnalysis();

        Assert.ThrowsException<System.ArgumentException>(() =>
        {
            _ = new WeightedUnivariateAnalysis(composite, weight: 0.5);
        });
    }

    /// <summary>
    /// Assigning a non-composite, non-null <c>IUnivariateAnalysis</c> after a
    /// previous composite-rejection still works — the rejection path doesn't leave the
    /// wrapper in a broken state.
    /// </summary>
    [TestMethod]
    public void WeightedUnivariateAnalysis_Setter_RejectionDoesNotCorruptState()
    {
        var composite = new CompositeAnalysis();
        var ua = CreateUnivariateAnalysis();
        var wua = new WeightedUnivariateAnalysis();

        try { wua.UnivariateAnalysis = composite; }
        catch (System.ArgumentException) { /* expected */ }

        wua.UnivariateAnalysis = ua;
        Assert.AreSame(ua, wua.UnivariateAnalysis);
    }

    /// <summary>
    /// <c>CompositeAnalysis.Validate</c> reports an error if a child slips through
    /// (e.g. via reflection / deserialization race) and is itself a CompositeAnalysis.
    /// Force-injects via reflection on the private backing field to bypass the setter.
    /// </summary>
    [TestMethod]
    public void CompositeAnalysis_Validate_RejectsNestedCompositeChild()
    {
        // Build a valid outer composite with one regular child.
        var outer = new CompositeAnalysis();
        outer.ProbabilityOrdinates.Add(0.99);
        outer.ProbabilityOrdinates.Add(0.5);
        outer.ProbabilityOrdinates.Add(0.01);

        var nested = new CompositeAnalysis();

        // Force-inject the nested composite by reflecting on the private backing field —
        // simulates the path a malformed XElement deserializer or reflection-based helper
        // could take if the setter guard were bypassed.
        var wua = new WeightedUnivariateAnalysis { Weight = 1.0 };
        var field = typeof(WeightedUnivariateAnalysis).GetField("_univariateAnalysis",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field, "_univariateAnalysis backing field must exist.");
        field!.SetValue(wua, nested);
        outer.Analyses.Add(wua);

        var (isValid, messages) = outer.Validate();

        Assert.IsFalse(isValid, "Validate must fail when a child is itself a CompositeAnalysis.");
        Assert.IsTrue(messages.Exists(m => m.Contains("CompositeAnalysis", System.StringComparison.OrdinalIgnoreCase)),
            "Validation messages should call out the composite-of-composite issue.");
    }

    #endregion

    #region Validate() with B17C child (regression)

    /// <summary>
    /// A composite with a Bulletin17CAnalysis child does not crash <c>CompositeAnalysis.Validate</c>.
    /// (Both Validate paths through the wrapper and the per-child cast are now interface-typed.)
    /// </summary>
    /// <remarks>
    /// The child is unestimated, so Validate is expected to report an "invalid or requires
    /// estimation" message — but it must report it cleanly via the IUnivariateAnalysis
    /// path, not throw <c>System.InvalidCastException</c>.
    /// </remarks>
    [TestMethod]
    public void CompositeAnalysis_Validate_AcceptsBulletin17CChildWithoutCast()
    {
        var b17c = CreateB17CAnalysis();
        var outer = new CompositeAnalysis();
        outer.ProbabilityOrdinates.Add(0.99);
        outer.ProbabilityOrdinates.Add(0.5);
        outer.ProbabilityOrdinates.Add(0.01);
        outer.Analyses.Add(new WeightedUnivariateAnalysis(b17c, weight: 1.0));

        var (isValid, messages) = outer.Validate();

        // Validate must complete (no InvalidCastException) and report the child as
        // un-estimated rather than blocking the validation pass entirely.
        Assert.IsFalse(isValid, "Unestimated child should be flagged as invalid.");
        Assert.IsTrue(messages.Count > 0);
        Assert.IsFalse(messages.Exists(m => m.Contains("InvalidCast", System.StringComparison.OrdinalIgnoreCase)));
    }

    #endregion

    #region Model Averaging incompatible with Bulletin17C children (DIC/WAIC/LOOIC)

    /// <summary>
    /// Helper: assemble a CompositeAnalysis configured for ModelAverage with the supplied
    /// averaging method. Mixes one UnivariateAnalysis and one Bulletin17CAnalysis child;
    /// both children are force-marked as estimated so the validation we exercise is the
    /// new B17C-incompatibility rule, not the upstream un-estimated-child check.
    /// </summary>
    private static CompositeAnalysis CreateModelAverageWithB17CChild(AverageMethod method)
    {
        var ua = CreateUnivariateAnalysis();
        var b17c = CreateB17CAnalysis();

        // Force-mark both children as estimated so we isolate the rule under test.
        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(isEstField);
        isEstField!.SetValue(ua, true);
        isEstField.SetValue(b17c, true);

        var outer = new CompositeAnalysis();
        outer.CompositeDistributionType = CompositeType.ModelAverage;
        outer.ModelAverageMethod = method;
        outer.ProbabilityOrdinates.Add(0.99);
        outer.ProbabilityOrdinates.Add(0.5);
        outer.ProbabilityOrdinates.Add(0.01);
        outer.Analyses.Add(new WeightedUnivariateAnalysis(ua, weight: 0.5));
        outer.Analyses.Add(new WeightedUnivariateAnalysis(b17c, weight: 0.5));
        return outer;
    }

    /// <summary>
    /// Model Averaging weighted by DIC must fail validation when at least one child is
    /// a Bulletin17CAnalysis. B17C is fit by GMM, not MCMC, so it does not produce a
    /// posterior chain and DIC is undefined for it.
    /// </summary>
    [TestMethod]
    public void ModelAverage_DIC_RejectsBulletin17CChild()
    {
        var outer = CreateModelAverageWithB17CChild(AverageMethod.DIC);

        var (isValid, messages) = outer.Validate();

        Assert.IsFalse(isValid, "DIC averaging must be rejected with a B17C child.");
        Assert.IsTrue(messages.Exists(m => m.Contains("DIC") && m.Contains("Bulletin17C")),
            "Error message should call out the DIC + B17C incompatibility specifically.");
    }

    /// <summary>WAIC averaging must be rejected with a B17C child for the same reason.</summary>
    [TestMethod]
    public void ModelAverage_WAIC_RejectsBulletin17CChild()
    {
        var outer = CreateModelAverageWithB17CChild(AverageMethod.WAIC);

        var (isValid, messages) = outer.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Exists(m => m.Contains("WAIC") && m.Contains("Bulletin17C")));
    }

    /// <summary>LOO-CV (LOOIC) averaging must be rejected with a B17C child for the same reason.</summary>
    [TestMethod]
    public void ModelAverage_LOOIC_RejectsBulletin17CChild()
    {
        var outer = CreateModelAverageWithB17CChild(AverageMethod.LOOIC);

        var (isValid, messages) = outer.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Exists(m => m.Contains("LOOIC") && m.Contains("Bulletin17C")));
    }

    /// <summary>
    /// AIC, BIC, RMSE, and Equal averaging are all defined for B17C (AIC/BIC from the
    /// log-likelihood at the GMM solution; RMSE from observed-vs-fitted residuals;
    /// Equal weighting needs no fit metric). These methods must NOT trigger the B17C
    /// guard.
    /// </summary>
    [DataTestMethod]
    [DataRow(AverageMethod.AIC)]
    [DataRow(AverageMethod.BIC)]
    [DataRow(AverageMethod.RMSE)]
    [DataRow(AverageMethod.Equal)]
    public void ModelAverage_CompatibleMethods_AcceptBulletin17CChild(AverageMethod method)
    {
        var outer = CreateModelAverageWithB17CChild(method);

        var (_, messages) = outer.Validate();

        Assert.IsFalse(messages.Exists(m => m.Contains("Bulletin17C")),
            $"Method {method} is supported for B17C children; the B17C guard must not trigger.");
    }

    /// <summary>
    /// Model Averaging by DIC with all-UnivariateAnalysis children (no B17C) must not
    /// trigger the new guard — DIC is well-defined for MCMC-fit children.
    /// </summary>
    [TestMethod]
    public void ModelAverage_DIC_AcceptsAllUnivariateAnalysisChildren()
    {
        var ua1 = CreateUnivariateAnalysis();
        var ua2 = CreateUnivariateAnalysis();
        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        isEstField!.SetValue(ua1, true);
        isEstField.SetValue(ua2, true);

        var outer = new CompositeAnalysis
        {
            CompositeDistributionType = CompositeType.ModelAverage,
            ModelAverageMethod = AverageMethod.DIC
        };
        outer.ProbabilityOrdinates.Add(0.99);
        outer.ProbabilityOrdinates.Add(0.5);
        outer.ProbabilityOrdinates.Add(0.01);
        outer.Analyses.Add(new WeightedUnivariateAnalysis(ua1, weight: 0.5));
        outer.Analyses.Add(new WeightedUnivariateAnalysis(ua2, weight: 0.5));

        var (_, messages) = outer.Validate();

        Assert.IsFalse(messages.Exists(m => m.Contains("Bulletin17C")),
            "The B17C guard must not trigger when no child is a Bulletin17CAnalysis.");
    }

    /// <summary>
    /// Mixture and CompetingRisks composite types do not use the averaging method, so
    /// the B17C guard must NOT trigger even when ModelAverageMethod is DIC and a B17C
    /// child is present.
    /// </summary>
    [TestMethod]
    public void NonModelAverageCompositeType_DoesNotTriggerB17CGuard()
    {
        var outer = CreateModelAverageWithB17CChild(AverageMethod.DIC);
        outer.CompositeDistributionType = CompositeType.Mixture;

        var (_, messages) = outer.Validate();

        Assert.IsFalse(messages.Exists(m => m.Contains("DIC") && m.Contains("Bulletin17C")),
            "Mixture composites do not use the averaging method, so the B17C-DIC guard must not fire.");
    }

    #endregion
}
