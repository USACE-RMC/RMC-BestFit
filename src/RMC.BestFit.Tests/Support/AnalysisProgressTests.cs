using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.Support;

/// <summary>
/// Programmatic unit tests for the <c>AnalysisProgress</c> sampling-loop helpers.
/// </summary>
/// <remarks>
/// Covers the loop-progress throttle used by the B17C uncertainty samplers. The throttle
/// must emit a report for the first completed iteration so the progress bar moves off the
/// phase-start value immediately, even when individual replicates are slow.
/// </remarks>
[TestClass]
public class AnalysisProgressTests
{
    /// <summary>
    /// A non-positive total never reports.
    /// </summary>
    [TestMethod]
    public void ShouldReportLoopProgress_NonPositiveTotal_IsFalse()
    {
        Assert.IsFalse(AnalysisProgress.ShouldReportLoopProgress(1, 0));
        Assert.IsFalse(AnalysisProgress.ShouldReportLoopProgress(1, -5));
    }

    /// <summary>
    /// The first completed iteration always reports, regardless of the loop size.
    /// This is the guarantee that unpins the progress bar from the phase-start value.
    /// </summary>
    [TestMethod]
    public void ShouldReportLoopProgress_FirstIteration_AlwaysReports()
    {
        Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(1, 10));
        Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(1, 10_000));
        Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(1, 1_000_000));
    }

    /// <summary>
    /// The final iteration always reports so the phase ends at its full progress value.
    /// </summary>
    [TestMethod]
    public void ShouldReportLoopProgress_LastIteration_AlwaysReports()
    {
        Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(10, 10));
        Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(10_000, 10_000));
        // A last iteration that is not on a whole-percent boundary still reports.
        Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(9_999, 9_999));
    }

    /// <summary>
    /// Large loops report at every whole-percent boundary and stay quiet in between.
    /// </summary>
    [TestMethod]
    public void ShouldReportLoopProgress_LargeLoop_ReportsAtPercentBoundaries()
    {
        const int total = 10_000;
        Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(100, total));
        Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(200, total));
        Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(5_000, total));
        Assert.IsFalse(AnalysisProgress.ShouldReportLoopProgress(150, total));
        Assert.IsFalse(AnalysisProgress.ShouldReportLoopProgress(9_999, total));
    }

    /// <summary>
    /// Small loops (fewer than 100 iterations) report on every iteration because the
    /// percent stride clamps to one.
    /// </summary>
    [TestMethod]
    public void ShouldReportLoopProgress_SmallLoop_ReportsEveryIteration()
    {
        const int total = 50;
        for (int current = 1; current <= total; current++)
        {
            Assert.IsTrue(AnalysisProgress.ShouldReportLoopProgress(current, total),
                $"Iteration {current} of {total} must report when the stride clamps to 1.");
        }
    }
}
