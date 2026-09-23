using System.Collections.Concurrent;
using Numerics.Utilities;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.Analyses;

/// <summary>
/// Verifies the reprocess failure contract on <see cref="AnalysisBase"/>: a throwing reprocessor
/// must surface through the conventional results notification, not only through IsEstimated.
/// </summary>
/// <remarks>
/// The App controls rebuild their bound views and reset the wait cursor when "AnalysisResults" is
/// raised, and almost none listen to IsEstimated, so a reprocess failure that only cleared
/// IsEstimated left the GUI frozen on stale output with a stuck wait cursor (issue #17). The
/// failure path now raises the conventional name alongside clearing the flag.
/// </remarks>
[TestClass]
public class ReprocessFailureContractTests
{
    /// <summary>
    /// A throwing reprocessor clears IsEstimated and raises the results notification.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task ReprocessIfEstimated_ThrowingReprocessor_RaisesResultsNotification()
    {
        var analysis = new ThrowingReprocessAnalysis();
        Assert.IsTrue(analysis.IsEstimated, "Fixture precondition: the analysis reports estimated.");

        var raisedNames = new ConcurrentBag<string>();
        var resultsRaised = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        analysis.PropertyChanged += (sender, e) =>
        {
            raisedNames.Add(e.PropertyName ?? string.Empty);
            if (e.PropertyName == "AnalysisResults")
                resultsRaised.TrySetResult(true);
        };

        analysis.TriggerFailingReprocess();

        var completed = await Task.WhenAny(resultsRaised.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.AreSame(resultsRaised.Task, completed, "The failure must raise the results notification.");
        Assert.IsFalse(analysis.IsEstimated, "The failure must clear IsEstimated.");
        Assert.IsTrue(raisedNames.Contains(nameof(AnalysisBase.IsEstimated)),
            "The IsEstimated change must also be raised.");
    }

    /// <summary>
    /// Minimal analysis whose reprocessor always throws.
    /// </summary>
    private sealed class ThrowingReprocessAnalysis : AnalysisBase
    {
        /// <summary>
        /// Initializes the stub in the estimated state so the reprocess gate is entered.
        /// </summary>
        public ThrowingReprocessAnalysis()
        {
            IsEstimated = true;
        }

        /// <summary>
        /// Invokes the protected reprocess entry point with a reprocessor that always throws.
        /// </summary>
        public void TriggerFailingReprocess()
        {
            ReprocessIfEstimated(() => throw new InvalidOperationException("Reprocess failure for the contract test."));
        }

        /// <inheritdoc/>
        public override Task RunAsync(SafeProgressReporter? progressReporter)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }
}
