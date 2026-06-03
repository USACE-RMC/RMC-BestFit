using System.ComponentModel;
using Numerics.Utilities;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.BatchAnalysis;

/// <summary>
/// Unit tests for the <see cref="BatchAnalysisResult"/> POCO that records
/// the outcome of a single analysis in a batch run.
/// </summary>
[TestClass]
public class BatchAnalysisResultTests
{
    /// <summary>
    /// Minimal IAnalysis stub — just enough to satisfy the constructor's null-check.
    /// </summary>
    private class StubAnalysis : IAnalysis
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CancelEventArgs>? AnalysisStarting;
        public event EventHandler<AnalysisRunCompletedEventArgs>? AnalysisCompleted;
        public bool IsEstimated => false;
        public Task RunAsync(SafeProgressReporter? progressReporter = null) => Task.CompletedTask;
        public void CancelAnalysis() { }
        public (bool IsValid, List<string> ValidationMessages) Validate() => (true, new List<string>());

        // Keep events referenced so they aren't reported as unused.
        internal void RaiseAll()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
            AnalysisStarting?.Invoke(this, new CancelEventArgs());
            AnalysisCompleted?.Invoke(this, new AnalysisRunCompletedEventArgs(false, true, null));
        }
    }

    /// <summary>Verifies that constructor succeeded populates all properties.</summary>
    [TestMethod]
    public void Test_Constructor_Succeeded_PopulatesAllProperties()
    {
        var stub = new StubAnalysis();
        var duration = TimeSpan.FromMilliseconds(123);

        var result = new BatchAnalysisResult(stub, succeeded: true, wasCanceled: false,
            error: null, duration: duration);

        Assert.AreSame(stub, result.Analysis);
        Assert.IsTrue(result.Succeeded);
        Assert.IsFalse(result.WasCanceled);
        Assert.IsNull(result.Error);
        Assert.AreEqual(duration, result.Duration);
    }

    /// <summary>Verifies that constructor throws when failed.</summary>
    [TestMethod]
    public void Test_Constructor_Failed_RecordsException()
    {
        var error = new InvalidOperationException("test");
        var result = new BatchAnalysisResult(new StubAnalysis(), succeeded: false,
            wasCanceled: false, error: error, duration: TimeSpan.Zero);

        Assert.IsFalse(result.Succeeded);
        Assert.AreSame(error, result.Error);
    }

    /// <summary>Verifies that constructor canceled flags cancellation.</summary>
    [TestMethod]
    public void Test_Constructor_Canceled_FlagsCancellation()
    {
        var result = new BatchAnalysisResult(new StubAnalysis(), succeeded: false,
            wasCanceled: true, error: null, duration: TimeSpan.FromSeconds(1));

        Assert.IsTrue(result.WasCanceled);
        Assert.IsFalse(result.Succeeded);
    }

    /// <summary>Verifies that constructor throws when null analysis.</summary>
    [TestMethod]
    public void Test_Constructor_NullAnalysis_ThrowsArgumentNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new BatchAnalysisResult(null!, true, false, null, TimeSpan.Zero));
    }
}
