using Numerics.Distributions;
using Numerics.Utilities;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.DistributionFitting;

/// <summary>
/// Verifies the progress reported by <see cref="FittingAnalysis.RunAsync"/>.
/// </summary>
[TestClass]
public class FittingAnalysisProgressTests
{
    /// <summary>
    /// A run in which no candidate distribution fits still finishes, so the progress reporter must
    /// observe completion rather than stall at the estimation stage.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenNoCandidateFits_ReportsCompletion()
    {
        var frame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(new double[] { -5.0, -4.0, -3.0, -2.0, -1.0 })
        };
        var analysis = new FittingAnalysis(frame);
        analysis.DistributionList.Clear();
        analysis.DistributionList.Add(new LogNormal());

        // The reporter posts progress through the synchronization context captured at construction;
        // an inline context lets the test observe the reports without a UI message loop.
        SynchronizationContext? previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
        try
        {
            var reporter = new SafeProgressReporter("fit");
            double lastProgress = double.NaN;
            reporter.ProgressReported += (_, progress, _) => lastProgress = progress;

            await analysis.RunAsync(reporter);

            Assert.IsFalse(analysis.IsEstimated);
            Assert.IsTrue(analysis.FittedDistributions.All(fitted => !fitted.FitSucceeded));
            Assert.AreEqual(AnalysisProgress.Complete, lastProgress, 1e-12);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    /// <summary>
    /// A synchronization context that runs posted callbacks inline on the posting thread.
    /// </summary>
    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        /// <inheritdoc/>
        public override void Post(SendOrPostCallback d, object? state) => d(state);

        /// <inheritdoc/>
        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }
}
