using Numerics.Data;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
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

    #region Model Averaging criterion availability for Bulletin17C children

    /// <summary>
    /// Helper: assemble a CompositeAnalysis configured for ModelAverage with the supplied
    /// averaging method. Mixes one UnivariateAnalysis and one Bulletin17CAnalysis child;
    /// both children receive deterministic fitted results so the validation exercises
    /// criterion availability rather than the upstream unestimated-child check.
    /// </summary>
    private static CompositeAnalysis CreateModelAverageWithB17CChild(AverageMethod method)
    {
        var ua = CreateUnivariateAnalysis();
        var b17c = CreateB17CAnalysis();
        SetAscendingProbabilityOrdinates(ua.ProbabilityOrdinates);
        SetAscendingProbabilityOrdinates(b17c.ProbabilityOrdinates);

        // Supply deterministic fitted-result containers so validation and weighting
        // exercise criterion handling rather than an unestimated-child guard.
        double[] uaParameters = ua.UnivariateDistribution.GetParameterValues(
            ua.UnivariateDistribution.DataFrame.FullTimeSeries.Last().Index);
        double[] b17cParameters = b17c.Bulletin17CDistribution.Distribution.GetParameters;
        SetCustomResults(ua.BayesianAnalysis, uaParameters);
        SetCustomResults(b17c.BayesianAnalysis, b17cParameters);
        SetPrivateCriterion(ua.BayesianAnalysis, nameof(BayesianAnalysis.DIC), 100d);
        SetPrivateCriterion(ua.BayesianAnalysis, nameof(BayesianAnalysis.WAIC), 100d);
        SetPrivateCriterion(ua.BayesianAnalysis, nameof(BayesianAnalysis.LOOIC), 100d);
        SetPrivateCriterion(b17c.BayesianAnalysis, nameof(BayesianAnalysis.DIC), 50d);
        SetPrivateCriterion(b17c.BayesianAnalysis, nameof(BayesianAnalysis.WAIC), 50d);
        SetPrivateCriterion(b17c.BayesianAnalysis, nameof(BayesianAnalysis.LOOIC), 50d);

        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(isEstField);
        isEstField!.SetValue(ua, true);
        isEstField.SetValue(b17c, true);
        SetAnalysisResults(ua, 100d, 1d);
        SetAnalysisResults(b17c, 102d, 2d);

        var outer = new CompositeAnalysis();
        outer.CompositeDistributionType = CompositeType.ModelAverage;
        outer.ModelAverageMethod = method;
        outer.ProbabilityOrdinates.Clear();
        outer.ProbabilityOrdinates.Add(0.01);
        outer.ProbabilityOrdinates.Add(0.5);
        outer.ProbabilityOrdinates.Add(0.99);
        outer.Analyses.Add(new WeightedUnivariateAnalysis(ua, weight: 0.5));
        outer.Analyses.Add(new WeightedUnivariateAnalysis(b17c, weight: 0.5));
        return outer;
    }

    /// <summary>
    /// Replaces default ordinates with a compact ascending validation fixture.
    /// </summary>
    /// <param name="ordinates">The probability-ordinate collection.</param>
    private static void SetAscendingProbabilityOrdinates(ProbabilityOrdinates ordinates)
    {
        ordinates.Clear();
        ordinates.Add(0.01d);
        ordinates.Add(0.5d);
        ordinates.Add(0.99d);
    }

    /// <summary>
    /// Populates the compatibility result container with deterministic parameter sets.
    /// </summary>
    /// <param name="analysis">The Bayesian compatibility container.</param>
    /// <param name="parameters">The fitted parameter values.</param>
    private static void SetCustomResults(BayesianAnalysis analysis, double[] parameters)
    {
        var output = new List<ParameterSet>();
        for (int index = 0; index < 100; index++)
            output.Add(new ParameterSet((double[])parameters.Clone(), 0d));

        analysis.OutputLength = output.Count;
        analysis.SetCustomMCMCResults(
            new MCMCResults(new ParameterSet((double[])parameters.Clone(), 0d), output, 0.1d),
            skipInformationCriteria: true);
    }

    /// <summary>
    /// Assigns a private-set comparison criterion for a deterministic test fixture.
    /// </summary>
    /// <param name="analysis">The Bayesian compatibility container.</param>
    /// <param name="propertyName">The criterion property name.</param>
    /// <param name="value">The criterion value.</param>
    private static void SetPrivateCriterion(BayesianAnalysis analysis, string propertyName, double value)
    {
        analysis.GetType()
            .GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)!
            .GetSetMethod(true)!
            .Invoke(analysis, new object[] { value });
    }

    /// <summary>
    /// Assigns deterministic frequency-analysis criteria to a fitted child.
    /// </summary>
    /// <param name="analysis">The fitted child analysis.</param>
    /// <param name="informationCriterion">The AIC and BIC value.</param>
    /// <param name="rmse">The RMSE value.</param>
    private static void SetAnalysisResults(
        IUnivariateAnalysis analysis,
        double informationCriterion,
        double rmse)
    {
        analysis.GetType()
            .GetProperty(nameof(IBayesianAnalysis.AnalysisResults),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)!
            .SetValue(analysis, new UncertaintyAnalysisResults
            {
                AIC = informationCriterion,
                BIC = informationCriterion,
                RMSE = rmse
            });
    }

    /// <summary>
    /// Model Averaging weighted by DIC must not rely on the removed type-specific B17C
    /// rejection rule. Criterion availability is handled by the general invalid-value policy.
    /// </summary>
    [TestMethod]
    public void ModelAverage_DIC_KeepsCompositeValidAndGivesB17CZeroWeight()
    {
        var outer = CreateModelAverageWithB17CChild(AverageMethod.DIC);

        outer.EstimateModelWeights();
        var (isValid, messages) = outer.Validate();

        Assert.IsTrue(isValid, string.Join("; ", messages));
        Assert.AreEqual(1d, outer.Analyses[0].Weight, 0d);
        Assert.AreEqual(0d, outer.Analyses[1].Weight, 0d);
        Assert.IsTrue(messages.Exists(m =>
            m.StartsWith("Warning: Sub-analysis 2 (Bulletin17CAnalysis)", StringComparison.Ordinal) &&
            m.Contains("assigned zero weight", StringComparison.Ordinal)));
    }

    /// <summary>WAIC validation uses the general invalid-criterion policy rather than a type-specific rejection.</summary>
    [TestMethod]
    public void ModelAverage_WAIC_KeepsCompositeValidAndGivesB17CZeroWeight()
    {
        var outer = CreateModelAverageWithB17CChild(AverageMethod.WAIC);

        outer.EstimateModelWeights();
        var (isValid, messages) = outer.Validate();

        Assert.IsTrue(isValid, string.Join("; ", messages));
        Assert.AreEqual(1d, outer.Analyses[0].Weight, 0d);
        Assert.AreEqual(0d, outer.Analyses[1].Weight, 0d);
        Assert.IsTrue(messages.Exists(m => m.Contains("Bulletin17CAnalysis", StringComparison.Ordinal) &&
            m.Contains("assigned zero weight", StringComparison.Ordinal)));
    }

    /// <summary>LOOIC validation uses the general invalid-criterion policy rather than a type-specific rejection.</summary>
    [TestMethod]
    public void ModelAverage_LOOIC_KeepsCompositeValidAndGivesB17CZeroWeight()
    {
        var outer = CreateModelAverageWithB17CChild(AverageMethod.LOOIC);

        outer.EstimateModelWeights();
        var (isValid, messages) = outer.Validate();

        Assert.IsTrue(isValid, string.Join("; ", messages));
        Assert.AreEqual(1d, outer.Analyses[0].Weight, 0d);
        Assert.AreEqual(0d, outer.Analyses[1].Weight, 0d);
        Assert.IsTrue(messages.Exists(m => m.Contains("Bulletin17CAnalysis", StringComparison.Ordinal) &&
            m.Contains("assigned zero weight", StringComparison.Ordinal)));
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

        outer.EstimateModelWeights();
        var (isValid, messages) = outer.Validate();

        Assert.IsTrue(isValid, string.Join("; ", messages));
        Assert.IsTrue(outer.Analyses[1].Weight > 0d,
            $"Method {method} is supported for B17C children and must give it positive weight.");
        Assert.IsFalse(messages.Exists(m => m.Contains("Bulletin17C", StringComparison.Ordinal)),
            $"Method {method} is supported for B17C children and must not emit a B17C diagnostic.");
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
            "No Bulletin17C diagnostic should appear when no child is a Bulletin17CAnalysis.");
    }

    /// <summary>
    /// Mixture and CompetingRisks composite types do not use the averaging method, so
    /// posterior-criterion availability is irrelevant even when ModelAverageMethod is DIC
    /// and a B17C child is present.
    /// </summary>
    [TestMethod]
    public void NonModelAverageCompositeType_DoesNotApplyB17CPosteriorCriterionPolicy()
    {
        var outer = CreateModelAverageWithB17CChild(AverageMethod.DIC);
        outer.CompositeDistributionType = CompositeType.Mixture;

        var (_, messages) = outer.Validate();

        Assert.IsFalse(messages.Exists(m => m.Contains("DIC") && m.Contains("Bulletin17C")),
            "Mixture composites do not use the averaging method, so B17C-DIC availability is irrelevant.");
    }

    #endregion
}
