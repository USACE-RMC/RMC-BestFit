using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Unit tests for the <c>BootstrapDiagnostics</c> class — the diagnostic
/// counters collected during parametric / pivot bootstrap runs.
/// </summary>
[TestClass]
public class BootstrapDiagnosticsTests
{
    #region Default state

    /// <summary>Verifies that defaults all counters zero.</summary>
    [TestMethod]
    public void Test_Defaults_AllCountersZero()
    {
        var d = new BootstrapDiagnostics();

        Assert.AreEqual(0, d.TotalReplicates);
        Assert.AreEqual(0, d.FailedReplicates);
        Assert.AreEqual(0, d.ValidReplicates);
        Assert.AreEqual(0, d.TotalRetries);
        Assert.AreEqual(0, d.TotalFunctionEvaluations);
        Assert.AreEqual(0, d.PivotRejections);
        Assert.AreEqual(0, d.MahalanobisRejections);
    }

    /// <summary>Verifies that defaults rates return zero when no replicates.</summary>
    [TestMethod]
    public void Test_Defaults_RatesReturnZeroWhenNoReplicates()
    {
        var d = new BootstrapDiagnostics();

        Assert.AreEqual(0.0, d.FailureRate);
        Assert.AreEqual(0.0, d.AverageRetries);
        Assert.AreEqual(0.0, d.AverageFunctionEvaluations);
        Assert.AreEqual(0.0, d.PivotRejectionRate);
        Assert.AreEqual(0.0, d.MahalanobisRejectionRate);
    }

    #endregion

    #region Counter behavior

    /// <summary>Verifies that valid replicates equals total minus failed.</summary>
    [TestMethod]
    public void Test_ValidReplicates_Equals_Total_Minus_Failed()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 100 };
        for (int i = 0; i < 7; i++) d.IncrementFailed();

        Assert.AreEqual(93, d.ValidReplicates);
    }

    /// <summary>Verifies that failure rate computed correctly.</summary>
    [TestMethod]
    public void Test_FailureRate_ComputedCorrectly()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 200 };
        for (int i = 0; i < 50; i++) d.IncrementFailed();

        Assert.AreEqual(0.25, d.FailureRate, 1e-12);
    }

    /// <summary>Verifies that add retries accumulates.</summary>
    [TestMethod]
    public void Test_AddRetries_Accumulates()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 10 };
        d.AddRetries(3);
        d.AddRetries(7);

        Assert.AreEqual(10, d.TotalRetries);
        Assert.AreEqual(1.0, d.AverageRetries, 1e-12);
    }

    /// <summary>Verifies that add function evaluations accumulates.</summary>
    [TestMethod]
    public void Test_AddFunctionEvaluations_Accumulates()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 4 };
        d.AddFunctionEvaluations(20);
        d.AddFunctionEvaluations(20);

        Assert.AreEqual(40, d.TotalFunctionEvaluations);
        Assert.AreEqual(10.0, d.AverageFunctionEvaluations, 1e-12);
    }

    /// <summary>Verifies that increment pivot rejection increments.</summary>
    [TestMethod]
    public void Test_IncrementPivotRejection_Increments()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 100 };
        d.IncrementPivotRejection();
        d.IncrementPivotRejection();

        Assert.AreEqual(2, d.PivotRejections);
        Assert.AreEqual(0.02, d.PivotRejectionRate, 1e-12);
    }

    /// <summary>Verifies that increment mahalanobis rejection increments.</summary>
    [TestMethod]
    public void Test_IncrementMahalanobisRejection_Increments()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 50 };
        d.IncrementMahalanobisRejection();

        Assert.AreEqual(1, d.MahalanobisRejections);
        Assert.AreEqual(0.02, d.MahalanobisRejectionRate, 1e-12);
    }

    /// <summary>Verifies that increments are thread safe.</summary>
    [TestMethod]
    public void Test_Increments_AreThreadSafe()
    {
        var d = new BootstrapDiagnostics { TotalReplicates = 1000 };

        Parallel.For(0, 1000, i =>
        {
            d.IncrementFailed();
            d.AddRetries(1);
            d.AddFunctionEvaluations(5);
            d.IncrementPivotRejection();
            d.IncrementMahalanobisRejection();
        });

        Assert.AreEqual(1000, d.FailedReplicates, "Failed counter must use Interlocked.");
        Assert.AreEqual(1000, d.TotalRetries);
        Assert.AreEqual(5000, d.TotalFunctionEvaluations);
        Assert.AreEqual(1000, d.PivotRejections);
        Assert.AreEqual(1000, d.MahalanobisRejections);
    }

    #endregion

    #region Phase timing

    /// <summary>Verifies that phase times round trip via properties for .</summary>
    [TestMethod]
    public void Test_PhaseTimes_RoundTripViaProperties()
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

    /// <summary>Verifies that to X element round trips all counters for from X element.</summary>
    [TestMethod]
    public void Test_ToXElement_FromXElement_RoundTripsAllCounters()
    {
        var original = new BootstrapDiagnostics { TotalReplicates = 100 };
        for (int i = 0; i < 5; i++) original.IncrementFailed();
        original.AddRetries(15);
        original.AddFunctionEvaluations(2000);
        for (int i = 0; i < 3; i++) original.IncrementPivotRejection();
        for (int i = 0; i < 2; i++) original.IncrementMahalanobisRejection();
        original.Phase1Time = TimeSpan.FromMilliseconds(123);
        original.Phase2Time = TimeSpan.FromMilliseconds(456);
        original.Phase3Time = TimeSpan.FromMilliseconds(789);

        var xml = original.ToXElement();
        var restored = BootstrapDiagnostics.FromXElement(xml);

        Assert.IsNotNull(restored);
        Assert.AreEqual(100, restored!.TotalReplicates);
        Assert.AreEqual(5, restored.FailedReplicates);
        Assert.AreEqual(15, restored.TotalRetries);
        Assert.AreEqual(2000, restored.TotalFunctionEvaluations);
        Assert.AreEqual(3, restored.PivotRejections);
        Assert.AreEqual(2, restored.MahalanobisRejections);
        Assert.AreEqual(TimeSpan.FromMilliseconds(123), restored.Phase1Time);
        Assert.AreEqual(TimeSpan.FromMilliseconds(456), restored.Phase2Time);
        Assert.AreEqual(TimeSpan.FromMilliseconds(789), restored.Phase3Time);
    }

    /// <summary>Verifies that from X element returns null when null element.</summary>
    [TestMethod]
    public void Test_FromXElement_NullElement_ReturnsNull()
    {
        var restored = BootstrapDiagnostics.FromXElement(null);

        Assert.IsNull(restored);
    }

    #endregion
}
