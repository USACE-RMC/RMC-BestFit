using Numerics.Mathematics.Optimization;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.Support;

/// <summary>
/// Programmatic unit tests for the <c>BootstrapDiagnostics</c> counter class.
/// </summary>
/// <remarks>
/// Covers counter increments, the optimizer-status distribution recorder, the retained-count
/// legacy fallback, and XML round-trip including diagnostics saved by earlier versions that
/// lack the newer attributes.
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
        for (int i = 0; i < 7; i++) diag.IncrementFailed();
        diag.AddRetries(15);
        diag.AddFunctionEvaluations(2500);
        for (int i = 0; i < 3; i++) diag.IncrementPivotRejection();
        for (int i = 0; i < 2; i++) diag.IncrementMahalanobisRejection();
        for (int i = 0; i < 4; i++) diag.IncrementTransformFailure();
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

    /// <summary>
    /// A fresh diagnostics object reports zero counts and safe (non-NaN) rates.
    /// </summary>
    [TestMethod]
    public void FreshInstance_AllCountersZero_RatesSafe()
    {
        var diag = new BootstrapDiagnostics();

        Assert.AreEqual(0, diag.TotalReplicates);
        Assert.AreEqual(0, diag.FailedReplicates);
        Assert.AreEqual(0, diag.TransformFailures);
        Assert.AreEqual(0, diag.StatusSuccessCount);
        Assert.AreEqual(0.0, diag.FailureRate);
        Assert.AreEqual(0.0, diag.PivotRejectionRate);
    }

    /// <summary>
    /// When RetainedReplicates was never recorded, the getter falls back to ValidReplicates —
    /// the correct interpretation for diagnostics saved before the retained count existed.
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
    /// RecordGMMStatus routes each terminal optimizer status to its own counter.
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
    /// IncrementTransformFailure accumulates independently of the pivot z-limit counter.
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

    /// <summary>
    /// Every counter, including the new retained/transform/status members, survives an
    /// XML round-trip.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesAllCounters()
    {
        var source = CreatePopulatedDiagnostics();

        var restored = BootstrapDiagnostics.FromXElement(source.ToXElement());

        Assert.IsNotNull(restored);
        Assert.AreEqual(source.TotalReplicates, restored.TotalReplicates);
        Assert.AreEqual(source.FailedReplicates, restored.FailedReplicates);
        Assert.AreEqual(source.TotalRetries, restored.TotalRetries);
        Assert.AreEqual(source.TotalFunctionEvaluations, restored.TotalFunctionEvaluations);
        Assert.AreEqual(source.PivotRejections, restored.PivotRejections);
        Assert.AreEqual(source.MahalanobisRejections, restored.MahalanobisRejections);
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
    /// The unrecorded retained-count sentinel survives a round-trip so restored legacy-style
    /// diagnostics keep falling back to ValidReplicates.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_UnrecordedRetainedCount_KeepsFallbackBehavior()
    {
        var source = new BootstrapDiagnostics { TotalReplicates = 30 };
        for (int i = 0; i < 5; i++) source.IncrementFailed();

        var restored = BootstrapDiagnostics.FromXElement(source.ToXElement());

        Assert.IsNotNull(restored);
        Assert.AreEqual(25, restored.RetainedReplicates, "The -1 sentinel must round-trip so the fallback stays live.");
    }

    /// <summary>
    /// Diagnostics saved by earlier versions (no retained/transform/status attributes)
    /// restore with defaults and the retained-count fallback.
    /// </summary>
    [TestMethod]
    public void FromXElement_LegacyXmlWithoutNewAttributes_RestoresDefaults()
    {
        var source = CreatePopulatedDiagnostics();
        var xml = source.ToXElement();
        xml.Attribute(nameof(BootstrapDiagnostics.RetainedReplicates))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.TransformFailures))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusSuccessCount))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusMaximumIterationsCount))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusMaximumFunctionEvaluationsCount))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusFailureCount))?.Remove();
        xml.Attribute(nameof(BootstrapDiagnostics.StatusNoneCount))?.Remove();

        var restored = BootstrapDiagnostics.FromXElement(xml);

        Assert.IsNotNull(restored);
        Assert.AreEqual(source.TotalReplicates, restored.TotalReplicates);
        Assert.AreEqual(source.FailedReplicates, restored.FailedReplicates);
        Assert.AreEqual(0, restored.TransformFailures);
        Assert.AreEqual(0, restored.StatusSuccessCount);
        Assert.AreEqual(restored.ValidReplicates, restored.RetainedReplicates,
            "Legacy diagnostics must fall back to ValidReplicates for the retained count.");
    }

    /// <summary>
    /// FromXElement returns null for a null element (no diagnostics persisted).
    /// </summary>
    [TestMethod]
    public void FromXElement_NullElement_ReturnsNull()
    {
        Assert.IsNull(BootstrapDiagnostics.FromXElement(null));
    }
}
