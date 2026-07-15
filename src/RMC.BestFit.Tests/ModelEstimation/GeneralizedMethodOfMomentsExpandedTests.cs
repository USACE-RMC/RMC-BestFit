using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Expanded programmatic unit tests for <c>GeneralizedMethodOfMoments</c>.
/// Exercises configuration round-trip on every public property setter and
/// pre-estimation invariants on the read-only outputs.
/// </summary>
/// <remarks>
/// These tests do NOT call <c>Estimate()</c>. Computational GMM verification
/// (parameter recovery, sandwich-variance vs published values) lives in
/// <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class GeneralizedMethodOfMomentsExpandedTests
{
    /// <summary>
    /// Stub moment-condition function returning zero G and zero S of the right shape.
    /// </summary>
    private static (Vector G, Matrix S) StubMomentConditions(double[] parameters)
        => (new Vector(parameters.Length), new Matrix(parameters.Length, parameters.Length));

    /// <summary>
    /// Builds a GMM stub with the requested parameter / moment shape.
    /// </summary>
    private static GeneralizedMethodOfMoments MakeStubGmm(int parameters = 2, int moments = 2)
    {
        var init = Enumerable.Repeat(0.5, parameters).ToArray();
        var lower = Enumerable.Repeat(0.0, parameters).ToArray();
        var upper = Enumerable.Repeat(1.0, parameters).ToArray();

        return new GeneralizedMethodOfMoments(
            momentConditionFunction: StubMomentConditions,
            numberOfParameters: parameters,
            numberOfMomentConditions: moments,
            sampleSize: 100,
            initialValues: init,
            lowerBounds: lower,
            upperBounds: upper);
    }

    #region Property round-trip

    /// <summary>
    /// UseFallbackOptimizer setter round-trips.
    /// </summary>
    [TestMethod]
    public void UseFallbackOptimizer_SetterRoundTrips()
    {
        var gmm = MakeStubGmm();
        gmm.UseFallbackOptimizer = false;
        Assert.IsFalse(gmm.UseFallbackOptimizer);
        gmm.UseFallbackOptimizer = true;
        Assert.IsTrue(gmm.UseFallbackOptimizer);
    }

    /// <summary>
    /// EstimationStrategy setter accepts every value and round-trips.
    /// </summary>
    [TestMethod]
    public void EstimationStrategy_SetterRoundTrips_ForEveryValue()
    {
        var gmm = MakeStubGmm();
        foreach (GeneralizedMethodOfMoments.GMMEstimationStrategy s in
                 Enum.GetValues<GeneralizedMethodOfMoments.GMMEstimationStrategy>())
        {
            gmm.EstimationStrategy = s;
            Assert.AreEqual(s, gmm.EstimationStrategy);
        }
    }

    /// <summary>
    /// MaxFunctionEvaluations setter round-trips.
    /// </summary>
    [TestMethod]
    public void MaxFunctionEvaluations_SetterRoundTrips()
    {
        var gmm = MakeStubGmm();
        gmm.MaxFunctionEvaluations = 5000;
        Assert.AreEqual(5000, gmm.MaxFunctionEvaluations);
    }

    /// <summary>
    /// AbsoluteTolerance setter round-trips.
    /// </summary>
    [TestMethod]
    public void AbsoluteTolerance_SetterRoundTrips()
    {
        var gmm = MakeStubGmm();
        gmm.AbsoluteTolerance = 1e-9;
        Assert.AreEqual(1e-9, gmm.AbsoluteTolerance, 1e-15);
    }

    /// <summary>
    /// RelativeTolerance setter round-trips.
    /// </summary>
    [TestMethod]
    public void RelativeTolerance_SetterRoundTrips()
    {
        var gmm = MakeStubGmm();
        gmm.RelativeTolerance = 1e-7;
        Assert.AreEqual(1e-7, gmm.RelativeTolerance, 1e-15);
    }

    /// <summary>
    /// Setting a property to the same value does not raise PropertyChanged.
    /// </summary>
    [TestMethod]
    public void MaxGMMIterations_SetSameValue_DoesNotRaisePropertyChanged()
    {
        var gmm = MakeStubGmm();
        var current = gmm.MaxGMMIterations;
        bool fired = false;
        gmm.PropertyChanged += (_, _) => fired = true;

        gmm.MaxGMMIterations = current;

        Assert.IsFalse(fired);
    }

    /// <summary>
    /// PropertyChanged fires when the value actually changes.
    /// </summary>
    [TestMethod]
    public void RelativeTolerance_SetNewValue_RaisesPropertyChanged()
    {
        var gmm = MakeStubGmm();
        bool fired = false;
        gmm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GeneralizedMethodOfMoments.RelativeTolerance)) fired = true;
        };

        gmm.RelativeTolerance = gmm.RelativeTolerance * 0.5 + 1e-12;

        Assert.IsTrue(fired);
    }

    #endregion

    #region Pre-estimation invariants

    /// <summary>
    /// IsEstimated is false on a fresh GMM instance.
    /// </summary>
    [TestMethod]
    public void IsEstimated_BeforeEstimation_IsFalse()
    {
        var gmm = MakeStubGmm();
        Assert.IsFalse(gmm.IsEstimated);
    }

    /// <summary>
    /// JStat is NaN before estimation.
    /// </summary>
    [TestMethod]
    public void JStat_BeforeEstimation_IsZero()
    {
        // JStat is initialized to 0 (default double); the contract is that it is meaningful
        // only when IsEstimated == true. Here we simply check the pre-estimation invariant
        // alongside the IsEstimated guard.
        var gmm = MakeStubGmm();
        Assert.IsFalse(gmm.IsEstimated);
        // JStat is a default double (0); the meaningful invariant is on IsEstimated.
        Assert.AreEqual(0.0, gmm.JStat);
    }

    /// <summary>
    /// S, W, and Sigma are null before estimation.
    /// </summary>
    [TestMethod]
    public void S_W_Sigma_BeforeEstimation_AreNull()
    {
        var gmm = MakeStubGmm();
        Assert.IsNull(gmm.S);
        Assert.IsNull(gmm.W);
        Assert.IsNull(gmm.Sigma);
    }

    /// <summary>
    /// TotalFunctionEvaluations is zero before estimation.
    /// </summary>
    [TestMethod]
    public void TotalFunctionEvaluations_BeforeEstimation_IsZero()
    {
        var gmm = MakeStubGmm();
        Assert.AreEqual(0, gmm.TotalFunctionEvaluations);
    }

    /// <summary>
    /// GMMIterations is zero before estimation.
    /// </summary>
    [TestMethod]
    public void GMMIterations_BeforeEstimation_IsZero()
    {
        var gmm = MakeStubGmm();
        Assert.AreEqual(0, gmm.GMMIterations);
    }

    /// <summary>
    /// ConvergedWithinTolerance is false before estimation (because IsEstimated is false).
    /// </summary>
    [TestMethod]
    public void ConvergedWithinTolerance_BeforeEstimation_IsFalse()
    {
        var gmm = MakeStubGmm();
        Assert.IsFalse(gmm.ConvergedWithinTolerance);
    }

    /// <summary>
    /// ConvergenceHistory is non-null and empty before estimation.
    /// </summary>
    [TestMethod]
    public void ConvergenceHistory_BeforeEstimation_IsEmpty()
    {
        var gmm = MakeStubGmm();
        Assert.IsNotNull(gmm.ConvergenceHistory);
        Assert.AreEqual(0, gmm.ConvergenceHistory.Count);
    }

    /// <summary>
    /// BestParameterSet is non-null before estimation (deterministic empty).
    /// </summary>
    [TestMethod]
    public void BestParameterSet_BeforeEstimation_IsNotNull()
    {
        var gmm = MakeStubGmm();
        Assert.IsNotNull(gmm.BestParameterSet);
    }

    /// <summary>
    /// DegreeOfFreedom = max(0, q - p).
    /// </summary>
    [TestMethod]
    public void DegreeOfFreedom_OverIdentified_EqualsMomentsMinusParameters()
    {
        var gmm = MakeStubGmm(parameters: 2, moments: 5);
        Assert.AreEqual(3, gmm.DegreeOfFreedom);
    }

    /// <summary>
    /// DegreeOfFreedom = 0 for just-identified.
    /// </summary>
    [TestMethod]
    public void DegreeOfFreedom_JustIdentified_IsZero()
    {
        var gmm = MakeStubGmm(parameters: 2, moments: 2);
        Assert.AreEqual(0, gmm.DegreeOfFreedom);
    }

    /// <summary>
    /// DegreeOfFreedom = 0 for under-identified (clamped to zero).
    /// </summary>
    [TestMethod]
    public void DegreeOfFreedom_UnderIdentified_IsZero()
    {
        var gmm = MakeStubGmm(parameters: 4, moments: 2);
        Assert.AreEqual(0, gmm.DegreeOfFreedom);
    }

    #endregion

    #region OptimizerMethod transitions

    /// <summary>
    /// OptimizerMethod transitions through every method without throwing.
    /// </summary>
    [TestMethod]
    public void OptimizerMethod_TransitionsThroughEveryValue()
    {
        var gmm = MakeStubGmm();
        foreach (var method in new[]
        {
            OptimizationMethod.BFGS,
            OptimizationMethod.NelderMead,
            OptimizationMethod.Powell,
            OptimizationMethod.MultilevelSingleLinkage,
            OptimizationMethod.DifferentialEvolution
        })
        {
            gmm.OptimizerMethod = method;
            Assert.AreEqual(method, gmm.OptimizerMethod);
        }
    }

    #endregion

    #region Convergence XML state

    /// <summary>
    /// Confirmed iterative convergence survives XML persistence without rerunning estimation.
    /// </summary>
    [TestMethod]
    public void ConvergedWithinTolerance_XmlRoundTrip_PreservesConfirmedState()
    {
        var source = MakeStubGmm();
        var xml = source.ToXElement();
        xml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.GMMIterations), 2);
        xml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.ConvergedWithinTolerance), true);
        source.RestoreFromXElement(xml);
        Assert.IsTrue(source.ConvergedWithinTolerance);

        var restored = MakeStubGmm();
        restored.RestoreFromXElement(source.ToXElement());

        Assert.IsTrue(restored.ConvergedWithinTolerance);
        Assert.AreEqual(2, restored.GMMIterations);
    }

    /// <summary>
    /// Legacy XML and non-iterative strategies restore conservatively as not confirmed converged.
    /// </summary>
    [TestMethod]
    public void ConvergedWithinTolerance_LegacyAndNonIterativeXml_RestoreFalse()
    {
        var legacy = MakeStubGmm();
        var legacyXml = legacy.ToXElement();
        legacyXml.Attribute(nameof(GeneralizedMethodOfMoments.ConvergedWithinTolerance))?.Remove();
        legacyXml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.GMMIterations), 2);
        legacy.RestoreFromXElement(legacyXml);
        Assert.IsFalse(legacy.ConvergedWithinTolerance);

        foreach (GeneralizedMethodOfMoments.GMMEstimationStrategy strategy in new[]
        {
            GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep,
            GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep,
        })
        {
            var nonIterative = MakeStubGmm();
            var xml = nonIterative.ToXElement();
            xml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.EstimationStrategy), strategy);
            xml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.GMMIterations), 2);
            xml.SetAttributeValue(nameof(GeneralizedMethodOfMoments.ConvergedWithinTolerance), true);

            nonIterative.RestoreFromXElement(xml);

            Assert.IsFalse(nonIterative.ConvergedWithinTolerance,
                $"{strategy} must not report iterative convergence.");
        }
    }

    #endregion
}
