using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Tests the new <c>Status</c> property on <c>GeneralizedMethodOfMoments</c>,
/// <c>MaximumLikelihood</c>, and <c>MaximumAPosteriori</c> — captured from the
/// inner transient <c>Optimizer</c> at the end of estimation so it remains valid after the
/// optimizer is discarded (e.g., after Save/Open).
/// </summary>
[TestClass]
public class EstimationStatusTests
{
    /// <summary>
    /// Creates normal Test Data.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame CreateNormalTestData()
    {
        var values = new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };
        var df = new BestFitDataFrame();
        for (int i = 0; i < values.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        return df;
    }

    #region MaximumLikelihood

    /// <summary>Verifies that maximum likelihood status defaults to none.</summary>
    [TestMethod]
    public void MaximumLikelihood_Status_DefaultsToNone()
    {
        var df = CreateNormalTestData();
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var mle = new MaximumLikelihood(model);

        Assert.AreEqual(OptimizationStatus.None, mle.Status,
            "Status should be None before Estimate runs.");
    }

    /// <summary>Verifies that maximum likelihood estimate sets status from optimizer.</summary>
    [TestMethod]
    public void MaximumLikelihood_Estimate_SetsStatusFromOptimizer()
    {
        var df = CreateNormalTestData();
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);

        mle.Estimate();

        Assert.AreNotEqual(OptimizationStatus.None, mle.Status,
            "Status must be set to a real value after Estimate.");
        Assert.AreEqual(mle.Optimizer.Status, mle.Status,
            "Status must mirror the inner Optimizer.Status.");
    }

    /// <summary>Verifies that maximum likelihood clear results resets status.</summary>
    [TestMethod]
    public void MaximumLikelihood_ClearResults_ResetsStatus()
    {
        var df = CreateNormalTestData();
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        mle.ClearResults();

        Assert.AreEqual(OptimizationStatus.None, mle.Status,
            "ClearResults must reset Status to None.");
    }

    /// <summary>
    /// Pins the MLE Fitness sign-convention invariant: <c>MaximumLogLikelihood</c>
    /// must equal <c>Model.DataLogLikelihood</c> evaluated at <c>BestParameterSet.Values</c>.
    /// The Numerics optimizer minimizes by negating the user-supplied log-likelihood;
    /// <c>BestParameterSet.Fitness</c> stores the negated value, and
    /// <c>MaximumLogLikelihood => -BestParameterSet.Fitness</c> flips it back. If the
    /// Numerics convention ever changes, this test surfaces the regression rather
    /// than letting every reported log-likelihood silently flip sign.
    /// </summary>
    [TestMethod]
    public void MaximumLikelihood_MaximumLogLikelihood_EqualsDataLogLikelihoodAtBestParams()
    {
        var df = CreateNormalTestData();
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);

        mle.Estimate();
        Assert.IsTrue(mle.IsEstimated, "MLE must be estimated before checking the invariant.");

        double expected = model.DataLogLikelihood(mle.BestParameterSet.Values);
        Assert.AreEqual(expected, mle.MaximumLogLikelihood, 1e-9,
            "MaximumLogLikelihood must equal Model.DataLogLikelihood at BestParameterSet.Values.");
    }

    #endregion

    #region MaximumAPosteriori

    /// <summary>Verifies that maximum a posteriori status defaults to none.</summary>
    [TestMethod]
    public void MaximumAPosteriori_Status_DefaultsToNone()
    {
        var df = CreateNormalTestData();
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var map = new MaximumAPosteriori(model);

        Assert.AreEqual(OptimizationStatus.None, map.Status,
            "Status should be None before Estimate runs.");
    }

    /// <summary>Verifies that maximum a posteriori estimate sets status from optimizer.</summary>
    [TestMethod]
    public void MaximumAPosteriori_Estimate_SetsStatusFromOptimizer()
    {
        var df = CreateNormalTestData();
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var map = new MaximumAPosteriori(model, OptimizationMethod.NelderMead);

        map.Estimate();

        Assert.AreNotEqual(OptimizationStatus.None, map.Status,
            "Status must be set to a real value after Estimate.");
        Assert.AreEqual(map.Optimizer.Status, map.Status,
            "Status must mirror the inner Optimizer.Status.");
    }

    /// <summary>Verifies that maximum a posteriori clear results resets status.</summary>
    [TestMethod]
    public void MaximumAPosteriori_ClearResults_ResetsStatus()
    {
        var df = CreateNormalTestData();
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var map = new MaximumAPosteriori(model, OptimizationMethod.NelderMead);
        map.Estimate();

        map.ClearResults();

        Assert.AreEqual(OptimizationStatus.None, map.Status,
            "ClearResults must reset Status to None.");
    }

    /// <summary>
    /// Pins the MAP Fitness sign-convention invariant — same rationale as the
    /// MLE counterpart. <c>MaximumLogLikelihood</c> must equal
    /// <c>Model.LogLikelihood</c> (full posterior LL = data + prior) at
    /// <c>BestParameterSet.Values</c>.
    /// </summary>
    [TestMethod]
    public void MaximumAPosteriori_MaximumLogLikelihood_EqualsLogLikelihoodAtBestParams()
    {
        var df = CreateNormalTestData();
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var map = new MaximumAPosteriori(model, OptimizationMethod.NelderMead);

        map.Estimate();
        Assert.IsTrue(map.IsEstimated, "MAP must be estimated before checking the invariant.");

        double expected = model.LogLikelihood(map.BestParameterSet.Values);
        Assert.AreEqual(expected, map.MaximumLogLikelihood, 1e-9,
            "MaximumLogLikelihood must equal Model.LogLikelihood at BestParameterSet.Values.");
    }

    #endregion

    #region GeneralizedMethodOfMoments

    /// <summary>Verifies that GMM status defaults to none.</summary>
    [TestMethod]
    public void GMM_Status_DefaultsToNone()
    {
        var df = CreateNormalTestData();
        var b17c = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        var gmm = new GeneralizedMethodOfMoments(b17c);

        Assert.AreEqual(OptimizationStatus.None, gmm.Status,
            "Status should be None before Estimate runs.");
    }

    /// <summary>Verifies that GMM estimate sets status.</summary>
    [TestMethod]
    public void GMM_Estimate_SetsStatus()
    {
        var df = CreateNormalTestData();
        var b17c = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        var gmm = new GeneralizedMethodOfMoments(b17c);

        gmm.Estimate();

        Assert.AreNotEqual(OptimizationStatus.None, gmm.Status,
            "Status must be set to a real value after Estimate runs.");
    }

    /// <summary>Verifies that GMM clear results resets status.</summary>
    [TestMethod]
    public void GMM_ClearResults_ResetsStatus()
    {
        var df = CreateNormalTestData();
        var b17c = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        var gmm = new GeneralizedMethodOfMoments(b17c);
        gmm.Estimate();

        gmm.ClearResults();

        Assert.AreEqual(OptimizationStatus.None, gmm.Status,
            "ClearResults must reset Status to None.");
    }

    /// <summary>Verifies that GMM round trips status for to X element restore from X element.</summary>
    [TestMethod]
    public void GMM_ToXElement_RestoreFromXElement_RoundTripsStatus()
    {
        // Arrange — estimate a small GMM problem
        var df = CreateNormalTestData();
        var b17cOriginal = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        var gmmOriginal = new GeneralizedMethodOfMoments(b17cOriginal);
        gmmOriginal.Estimate();
        var originalStatus = gmmOriginal.Status;

        // Act — serialize, then restore into a fresh GMM constructed from the same model.
        // The restored GMM has Optimizer = null (the live computation object isn't persisted)
        // but Status must round-trip via the persisted attribute.
        var xml = gmmOriginal.ToXElement();
        var b17cRestored = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        var gmmRestored = new GeneralizedMethodOfMoments(b17cRestored);
        gmmRestored.RestoreFromXElement(xml);

        // Assert
        Assert.AreEqual(originalStatus, gmmRestored.Status,
            "Status must round-trip through ToXElement / RestoreFromXElement.");
        Assert.IsNull(gmmRestored.Optimizer,
            "After RestoreFromXElement, Optimizer is the transient computation object and must remain null — the whole point of persisting Status is that callers should read it instead.");
    }

    #endregion

    #region Bulletin17CAnalysis deserialize regression

    /// <summary>
    /// Regression for the bug introduced by Phase 1's ordinate reprocess: opening a saved B17C
    /// project and editing a probability ordinate would NRE at line 646 of Bulletin17CAnalysis
    /// because <c>_gmm.Optimizer</c> is null after RestoreFromXElement. The fix replaces all six
    /// <c>.Optimizer.Status</c> references with the persistent <c>.Status</c> property.
    /// </summary>
    [TestMethod]
    public void Bulletin17CAnalysis_AfterRestore_OrdinateChange_DoesNotThrow()
    {
        // Arrange — construct a B17C analysis, serialize it, restore from XElement (Optimizer == null).
        var df = CreateNormalTestData();
        var b17cDist = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        var analysis = new Bulletin17CAnalysis(b17cDist);

        // Manually populate a GMM child so the round-trip carries one. We don't need to actually
        // estimate — the bug only requires _gmm to be non-null with Optimizer == null after
        // restore, which is what RestoreFromXElement leaves us in.
        var seedGmm = new GeneralizedMethodOfMoments(b17cDist);
        seedGmm.Estimate();
        var xml = analysis.ToXElement();
        // The live analysis didn't run; manually splice the seed GMM XML so RestoreFromXElement
        // has something to restore.
        var existingGmm = xml.Element(nameof(GeneralizedMethodOfMoments));
        existingGmm?.Remove();
        xml.Add(seedGmm.ToXElement());

        var b17cDistRestored = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        var restored = new Bulletin17CAnalysis(b17cDistRestored, xml);

        // Sanity: the restored GMM has its Status from disk but Optimizer is null.
        Assert.IsNotNull(restored.GMM, "Restored analysis should have a GMM child.");
        Assert.IsNull(restored.GMM!.Optimizer, "Optimizer is transient and not restored.");

        // Act — toggling ordinates triggers the model-layer ProbabilityOrdinates_CollectionChanged,
        // which (via Phase 1) calls CreateFrequencyAnalysisResultsAsync. That method's first guard
        // dereferences _gmm.Status — must NOT throw.
        restored.ProbabilityOrdinates.Add(0.5);

        // Assert — getting here without an NRE is the test.
        Assert.AreEqual(seedGmm.Status, restored.GMM.Status,
            "Persisted Status must survive round-trip and be available without a live Optimizer.");
    }

    #endregion
}
