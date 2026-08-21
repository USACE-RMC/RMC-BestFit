using System.Globalization;
using System.Xml.Linq;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.Support;

/// <summary>
/// Programmatic unit tests for the <c>BootstrapDiagnostics</c> counter class.
/// </summary>
/// <remarks>
/// Covers counter increments, per-replicate rates, the optimizer-status distribution recorder,
/// the retained-count legacy fallback, thread safety, and XML round-trip including diagnostics
/// saved by earlier versions that lack the newer attributes or that stored the realization count
/// under the attempted-replicates attribute.
/// </remarks>
[TestClass]
public class BootstrapDiagnosticsTests
{
    /// <summary>
    /// Builds a diagnostics object with every counter populated to a distinct value.
    /// </summary>
    /// <returns>The populated diagnostics.</returns>
    private static BootstrapDiagnostics CreatePopulatedDiagnostics()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 100 };
        for (int i = 0; i < 110; i++) diag.IncrementAttempted();
        for (int i = 0; i < 7; i++) diag.IncrementFailed();
        diag.AddRetries(15);
        diag.AddFunctionEvaluations(2500);
        diag.AddOptimizerFallbacks(9);
        for (int i = 0; i < 3; i++) diag.IncrementPivotRejection();
        for (int i = 0; i < 2; i++) diag.IncrementMahalanobisRejection();
        for (int i = 0; i < 4; i++) diag.IncrementTransformFailure();
        for (int i = 0; i < 11; i++) diag.IncrementBoundRepair();
        for (int i = 0; i < 80; i++) diag.RecordGMMStatus(OptimizationStatus.Success);
        for (int i = 0; i < 20; i++) diag.RecordGMMStatus(OptimizationStatus.MaximumIterationsReached);
        for (int i = 0; i < 6; i++) diag.RecordGMMStatus(OptimizationStatus.MaximumFunctionEvaluationsReached);
        for (int i = 0; i < 5; i++) diag.RecordGMMStatus(OptimizationStatus.Failure);
        diag.RecordGMMStatus(OptimizationStatus.None);
        diag.RetainedReplicates = 86;
        diag.Phase1Time = TimeSpan.FromSeconds(12);
        diag.Phase2Time = TimeSpan.FromSeconds(1);
        diag.Phase3Time = TimeSpan.FromSeconds(3);
        return diag;
    }

    #region Default state

    /// <summary>
    /// A fresh diagnostics object reports zero counts and safe (non-NaN) rates.
    /// </summary>
    [TestMethod]
    public void FreshInstance_AllCountersZero_RatesSafe()
    {
        var diag = new BootstrapDiagnostics();

        Assert.AreEqual(0, diag.TotalReplicates);
        Assert.AreEqual(0, diag.AttemptedReplicates);
        Assert.AreEqual(0, diag.AttemptedRealizations);
        Assert.AreEqual(0, diag.FailedReplicates);
        Assert.AreEqual(0, diag.ValidReplicates);
        Assert.AreEqual(0, diag.TotalRetries);
        Assert.AreEqual(0, diag.TotalFunctionEvaluations);
        Assert.AreEqual(0, diag.TransformFailures);
        Assert.AreEqual(0, diag.BoundRepairs);
        Assert.AreEqual(0, diag.PivotRejections);
        Assert.AreEqual(0, diag.MahalanobisRejections);
        Assert.AreEqual(0, diag.StatusSuccessCount);
        Assert.AreEqual(0.0, diag.FailureRate);
        Assert.AreEqual(0.0, diag.AverageRetries);
        Assert.AreEqual(0.0, diag.AverageFunctionEvaluations);
        Assert.AreEqual(0.0, diag.BoundRepairRate);
        Assert.AreEqual(0.0, diag.PivotRejectionRate);
        Assert.AreEqual(0.0, diag.MahalanobisRejectionRate);
    }

    #endregion

    #region Counter behavior

    /// <summary>
    /// Rates are per requested replicate: a replicate that exhausts ten attempts counts once as
    /// substituted, while every attempt is recorded as a realization.
    /// </summary>
    [TestMethod]
    public void RatesAndCounts_ArePerRequestedReplicate()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 1000 };
        for (int i = 0; i < 1000 + 9 * 100; i++) diag.IncrementAttempted();
        for (int i = 0; i < 100; i++) diag.IncrementFailed();
        diag.AddRetries(9 * 100);

        Assert.AreEqual(1900, diag.AttemptedRealizations);
        Assert.AreEqual(1000, diag.AttemptedReplicates);
        Assert.AreEqual(900, diag.ValidReplicates);
        Assert.AreEqual(0.10, diag.FailureRate, 1e-12);
        Assert.AreEqual(0.9, diag.AverageRetries, 1e-12);
    }

    /// <summary>
    /// Valid replicates are the requested count minus the substituted count.
    /// </summary>
    [TestMethod]
    public void ValidReplicates_Equals_Total_Minus_Failed()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 100 };
        for (int i = 0; i < 7; i++) d.IncrementFailed();

        Assert.AreEqual(93, d.ValidReplicates);
        Assert.AreEqual(0.07, d.FailureRate, 1e-12);
    }

    /// <summary>
    /// Retries accumulate and average over the requested replicates.
    /// </summary>
    [TestMethod]
    public void AddRetries_Accumulates()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 10 };
        d.AddRetries(3);
        d.AddRetries(7);

        Assert.AreEqual(10, d.TotalRetries);
        Assert.AreEqual(1.0, d.AverageRetries, 1e-12);
    }

    /// <summary>
    /// Function evaluations accumulate and average over the requested replicates.
    /// </summary>
    [TestMethod]
    public void AddFunctionEvaluations_Accumulates()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 4 };
        d.AddFunctionEvaluations(20);
        d.AddFunctionEvaluations(20);

        Assert.AreEqual(40, d.TotalFunctionEvaluations);
        Assert.AreEqual(10.0, d.AverageFunctionEvaluations, 1e-12);
    }

    /// <summary>
    /// Pivot z-limit clips, Mahalanobis rejections, and bound repairs increment with per-replicate rates.
    /// </summary>
    [TestMethod]
    public void PivotMahalanobisAndBoundRepairCounters_IncrementWithRates()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 100 };
        d.IncrementPivotRejection();
        d.IncrementPivotRejection();
        d.IncrementMahalanobisRejection();
        for (int i = 0; i < 5; i++) d.IncrementBoundRepair();

        Assert.AreEqual(2, d.PivotRejections);
        Assert.AreEqual(0.02, d.PivotRejectionRate, 1e-12);
        Assert.AreEqual(1, d.MahalanobisRejections);
        Assert.AreEqual(0.01, d.MahalanobisRejectionRate, 1e-12);
        Assert.AreEqual(5, d.BoundRepairs);
        Assert.AreEqual(0.05, d.BoundRepairRate, 1e-12);
    }

    /// <summary>
    /// Every counter uses interlocked increments, so parallel updates are not lost.
    /// </summary>
    [TestMethod]
    public void Increments_AreThreadSafe()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 1000 };
        Parallel.For(0, 1000, i =>
        {
            d.IncrementAttempted();
            d.IncrementFailed();
            d.AddRetries(1);
            d.AddFunctionEvaluations(5);
            d.IncrementPivotRejection();
            d.IncrementMahalanobisRejection();
            d.IncrementBoundRepair();
            d.IncrementTransformFailure();
        });

        Assert.AreEqual(1000, d.AttemptedRealizations);
        Assert.AreEqual(1000, d.FailedReplicates);
        Assert.AreEqual(1000, d.TotalRetries);
        Assert.AreEqual(5000, d.TotalFunctionEvaluations);
        Assert.AreEqual(1000, d.PivotRejections);
        Assert.AreEqual(1000, d.MahalanobisRejections);
        Assert.AreEqual(1000, d.BoundRepairs);
        Assert.AreEqual(1000, d.TransformFailures);
    }

    /// <summary>
    /// An unset retained count falls back to the valid-replicate count; an explicit value wins.
    /// </summary>
    [TestMethod]
    public void RetainedReplicates_NotRecorded_FallsBackToValidReplicates()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 50 };
        for (int i = 0; i < 8; i++) diag.IncrementFailed();

        Assert.AreEqual(42, diag.RetainedReplicates, "Unset retained count must fall back to ValidReplicates.");

        diag.RetainedReplicates = 40;
        Assert.AreEqual(40, diag.RetainedReplicates, "An explicit retained count must win over the fallback.");
    }

    /// <summary>
    /// Each optimizer status routes to its own counter.
    /// </summary>
    [TestMethod]
    public void RecordGMMStatus_RoutesEveryStatusToItsCounter()
    {
        var diag = new BootstrapDiagnostics();
        diag.RecordGMMStatus(OptimizationStatus.Success);
        diag.RecordGMMStatus(OptimizationStatus.MaximumIterationsReached);
        diag.RecordGMMStatus(OptimizationStatus.MaximumIterationsReached);
        diag.RecordGMMStatus(OptimizationStatus.MaximumFunctionEvaluationsReached);
        diag.RecordGMMStatus(OptimizationStatus.Failure);
        diag.RecordGMMStatus(OptimizationStatus.None);

        Assert.AreEqual(1, diag.StatusSuccessCount);
        Assert.AreEqual(2, diag.StatusMaximumIterationsCount);
        Assert.AreEqual(1, diag.StatusMaximumFunctionEvaluationsCount);
        Assert.AreEqual(1, diag.StatusFailureCount);
        Assert.AreEqual(1, diag.StatusNoneCount);
    }

    /// <summary>
    /// Transform failures and pivot clips are independent counters.
    /// </summary>
    [TestMethod]
    public void IncrementTransformFailure_IndependentOfPivotRejections()
    {
        var diag = new BootstrapDiagnostics();
        diag.IncrementTransformFailure();
        diag.IncrementTransformFailure();
        diag.IncrementPivotRejection();

        Assert.AreEqual(2, diag.TransformFailures);
        Assert.AreEqual(1, diag.PivotRejections);
    }

    #endregion

    #region Phase timing

    /// <summary>
    /// Phase durations round-trip through their properties.
    /// </summary>
    [TestMethod]
    public void PhaseTimes_RoundTripViaProperties()
    {
        var d = new BootstrapDiagnostics
        {
            Phase1Time = TimeSpan.FromSeconds(1),
            Phase2Time = TimeSpan.FromSeconds(2),
            Phase3Time = TimeSpan.FromSeconds(3)
        };

        Assert.AreEqual(TimeSpan.FromSeconds(1), d.Phase1Time);
        Assert.AreEqual(TimeSpan.FromSeconds(2), d.Phase2Time);
        Assert.AreEqual(TimeSpan.FromSeconds(3), d.Phase3Time);
    }

    #endregion

    #region Serialization

    /// <summary>
    /// Every counter survives an XML round trip.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesAllCounters()
    {
        var source = CreatePopulatedDiagnostics();

        var restored = BootstrapDiagnostics.FromXElement(source.ToXElement());

        Assert.IsNotNull(restored);
        Assert.AreEqual(source.TotalReplicates, restored!.TotalReplicates);
        Assert.AreEqual(source.AttemptedReplicates, restored.AttemptedReplicates);
        Assert.AreEqual(source.AttemptedRealizations, restored.AttemptedRealizations);
        Assert.AreEqual(source.FailedReplicates, restored.FailedReplicates);
        Assert.AreEqual(source.TotalRetries, restored.TotalRetries);
        Assert.AreEqual(source.TotalFunctionEvaluations, restored.TotalFunctionEvaluations);
        Assert.AreEqual(source.OptimizerFallbacks, restored.OptimizerFallbacks);
        Assert.AreEqual(source.PivotRejections, restored.PivotRejections);
        Assert.AreEqual(source.MahalanobisRejections, restored.MahalanobisRejections);
        Assert.AreEqual(source.BoundRepairs, restored.BoundRepairs);
        Assert.AreEqual(source.RetainedReplicates, restored.RetainedReplicates);
        Assert.AreEqual(source.TransformFailures, restored.TransformFailures);
        Assert.AreEqual(source.StatusSuccessCount, restored.StatusSuccessCount);
        Assert.AreEqual(source.StatusMaximumIterationsCount, restored.StatusMaximumIterationsCount);
        Assert.AreEqual(source.StatusMaximumFunctionEvaluationsCount, restored.StatusMaximumFunctionEvaluationsCount);
        Assert.AreEqual(source.StatusFailureCount, restored.StatusFailureCount);
        Assert.AreEqual(source.StatusNoneCount, restored.StatusNoneCount);
        Assert.AreEqual(source.Phase1Time, restored.Phase1Time);
        Assert.AreEqual(source.Phase2Time, restored.Phase2Time);
        Assert.AreEqual(source.Phase3Time, restored.Phase3Time);
    }

    /// <summary>
    /// The attempted-replicates attribute is written with its per-replicate meaning while the
    /// realization count is written separately.
    /// </summary>
    [TestMethod]
    public void ToXElement_WritesPerReplicateAttemptedCountAndRealizations()
    {
        var source = new BootstrapDiagnostics { TotalReplicates = 5 };
        for (int i = 0; i < 7; i++) source.IncrementAttempted();

        XElement xml = source.ToXElement();

        Assert.AreEqual("5", xml.Attribute(nameof(BootstrapDiagnostics.AttemptedReplicates))?.Value);
        Assert.AreEqual("7", xml.Attribute(nameof(BootstrapDiagnostics.AttemptedRealizations))?.Value);
    }

    /// <summary>
    /// An unrecorded retained count keeps its sentinel through XML so the fallback stays live.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_UnrecordedRetainedCount_KeepsFallbackBehavior()
    {
        var source = new BootstrapDiagnostics { TotalReplicates = 30 };
        for (int i = 0; i < 5; i++) source.IncrementFailed();

        var restored = BootstrapDiagnostics.FromXElement(source.ToXElement());

        Assert.IsNotNull(restored);
        Assert.AreEqual(25, restored!.RetainedReplicates, "The -1 sentinel must round-trip so the fallback stays live.");
    }

    /// <summary>
    /// Diagnostics saved by earlier versions without the newer attributes restore with defaults.
    /// </summary>
    [TestMethod]
    public void FromXElement_LegacyXmlWithoutNewAttributes_RestoresDefaults()
    {
        var source = CreatePopulatedDiagnostics();
        var xml = source.ToXElement();
        xml.Attribute(nameof(BootstrapDiagnostics.RetainedReplicates))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.TransformFailures))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.BoundRepairs))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusSuccessCount))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusMaximumIterationsCount))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusMaximumFunctionEvaluationsCount))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusFailureCount))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusNoneCount))?.Remove();

        var restored = BootstrapDiagnostics.FromXElement(xml);

        Assert.IsNotNull(restored);
        Assert.AreEqual(source.TotalReplicates, restored!.TotalReplicates);
        Assert.AreEqual(source.FailedReplicates, restored.FailedReplicates);
        Assert.AreEqual(0, restored.TransformFailures);
        Assert.AreEqual(0, restored.BoundRepairs);
        Assert.AreEqual(0, restored.StatusSuccessCount);
        Assert.AreEqual(restored.ValidReplicates, restored.RetainedReplicates,
            "Legacy diagnostics must fall back to ValidReplicates for the retained count.");
    }

    /// <summary>
    /// Earlier versions stored the realization count under the attempted-replicates attribute;
    /// it restores as the realization count while rates stay per requested replicate.
    /// </summary>
    [TestMethod]
    public void FromXElement_LegacyAttemptedReplicates_ReadsAsRealizations()
    {
        var legacyXml = new XElement(nameof(BootstrapDiagnostics),
            new XAttribute(nameof(BootstrapDiagnostics.TotalReplicates), "1000"),
            new XAttribute(nameof(BootstrapDiagnostics.AttemptedReplicates), "1900"),
            new XAttribute(nameof(BootstrapDiagnostics.FailedReplicates), "100"),
            new XAttribute(nameof(BootstrapDiagnostics.TotalRetries), "900"));

        var restored = BootstrapDiagnostics.FromXElement(legacyXml);

        Assert.IsNotNull(restored);
        Assert.AreEqual(1900, restored!.AttemptedRealizations);
        Assert.AreEqual(1000, restored.AttemptedReplicates);
        Assert.AreEqual(900, restored.ValidReplicates);
        Assert.AreEqual(0.10, restored.FailureRate, 1e-12);
        Assert.AreEqual(0.9, restored.AverageRetries, 1e-12);
    }

    /// <summary>
    /// Attempts and optimizer fallbacks accumulate and round-trip with per-replicate rates.
    /// </summary>
    [TestMethod]
    public void AttemptsAndOptimizerFallbacks_AccumulateAndRoundTrip()
    {
        var original = new BootstrapDiagnostics { TotalReplicates = 5 };
        for (int i = 0; i < 7; i++) original.IncrementAttempted();
        original.IncrementFailed();
        original.IncrementFailed();
        original.AddOptimizerFallbacks(3);
        original.AddOptimizerFallbacks(2);

        Assert.AreEqual(7, original.AttemptedRealizations);
        Assert.AreEqual(5, original.AttemptedReplicates);
        Assert.AreEqual(3, original.ValidReplicates);
        Assert.AreEqual(2.0 / 5.0, original.FailureRate, 1e-12);
        Assert.AreEqual(5, original.OptimizerFallbacks);

        var restored = BootstrapDiagnostics.FromXElement(original.ToXElement());

        Assert.IsNotNull(restored);
        Assert.AreEqual(7, restored!.AttemptedRealizations);
        Assert.AreEqual(5, restored.AttemptedReplicates);
        Assert.AreEqual(3, restored.ValidReplicates);
        Assert.AreEqual(5, restored.OptimizerFallbacks);
    }

    /// <summary>
    /// Diagnostics saved without any attempt or fallback attributes restore with the requested
    /// count as both the attempted replicates and the realizations.
    /// </summary>
    [TestMethod]
    public void FromLegacyXElement_UsesBackwardCompatibleCounterDefaults()
    {
        var legacyXml = new BootstrapDiagnostics { TotalReplicates = 12 }.ToXElement();
        legacyXml.Attribute(nameof(BootstrapDiagnostics.AttemptedReplicates))?.Remove();
        legacyXml.Attribute(nameof(BootstrapDiagnostics.AttemptedRealizations))?.Remove();
        legacyXml.Attribute(nameof(BootstrapDiagnostics.OptimizerFallbacks))?.Remove();

        var restored = BootstrapDiagnostics.FromXElement(legacyXml);

        Assert.IsNotNull(restored);
        Assert.AreEqual(12, restored!.AttemptedReplicates);
        Assert.AreEqual(12, restored.AttemptedRealizations);
        Assert.AreEqual(0, restored.OptimizerFallbacks);
    }

    /// <summary>
    /// A null element deserializes to null.
    /// </summary>
    [TestMethod]
    public void FromXElement_NullElement_ReturnsNull()
    {
        Assert.IsNull(BootstrapDiagnostics.FromXElement(null));
    }

    #endregion
}
