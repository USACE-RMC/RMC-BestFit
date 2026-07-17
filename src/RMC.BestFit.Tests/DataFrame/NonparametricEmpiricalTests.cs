using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Tests duplicate-value handling for DataFrame operations backed by empirical distributions.
/// </summary>
/// <remarks>
/// Flood and bootstrap samples commonly contain tied values. These tests ensure DataFrame collapses
/// tied empirical ordinates before invoking Numerics while preserving finite consumer results.
/// </remarks>
[TestClass]
public class NonparametricEmpiricalTests
{
    /// <summary>
    /// Verifies standard nonparametric moments remain finite when exact observations contain ties.
    /// </summary>
    /// <remarks>
    /// The fixture contains five distinct values across eight observations so collapsing ties still
    /// leaves a valid empirical distribution.
    /// </remarks>
    [TestMethod]
    public void GetNonparametricMoments_DuplicateValues_ReturnsFiniteMoments()
    {
        var frame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries([100d, 100d, 125d, 125d, 150d, 175d, 200d, 200d])
        };
        frame.CalculatePlottingPositions();

        var moments = frame.GetNonparametricMoments();

        Assert.IsNotNull(moments);
        Assert.IsTrue(moments.All(double.IsFinite));
    }

    /// <summary>
    /// Verifies ROS moments remain finite when low-outlier samples contain repeated values.
    /// </summary>
    /// <remarks>
    /// This is the B17C initialization path exercised by negatively skewed and bootstrap samples.
    /// </remarks>
    [TestMethod]
    public void GetNonparametricMomentsROS_DuplicateValues_ReturnsFiniteMoments()
    {
        var frame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(
            [
                10d,
                100d,
                100d,
                125d,
                125d,
                150d,
                150d,
                200d,
                200d,
                250d
            ])
        };
        frame.LowOutlierThreshold = 50d;
        frame.SetLowOutliersFromThreshold();
        frame.CalculatePlottingPositions();

        var moments = frame.GetNonparametricMomentsROS(useLog10Values: true);

        Assert.IsNotNull(moments);
        Assert.IsTrue(moments.All(double.IsFinite));
    }

    /// <summary>
    /// Verifies summary statistics and standardized values accept repeated empirical X-values.
    /// </summary>
    /// <remarks>
    /// Both raw and log empirical distributions share the same duplicate-collapse behavior.
    /// </remarks>
    [TestMethod]
    public void SummaryAndStandardizedValues_DuplicateValues_ReturnFiniteResults()
    {
        var frame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(
            [
                100d,
                100d,
                125d,
                125d,
                150d,
                150d,
                175d,
                175d,
                200d,
                200d,
                250d,
                300d
            ])
        };
        frame.CalculatePlottingPositions();

        var summary = frame.SummaryStatisticsAllData();
        frame.SetStandardizedValues();

        Assert.IsTrue(double.IsFinite(summary["Mean"]));
        Assert.IsTrue(double.IsFinite(summary["Std Dev"]));
        Assert.IsTrue(double.IsFinite(summary["Mean (of log)"]));
        Assert.IsTrue(double.IsFinite(summary["50%"]));
        Assert.IsTrue(frame.ExactSeries.All(data =>
            double.IsFinite(data.StandardizedValue) && double.IsFinite(data.StandardizedLog10Value)));
    }

    /// <summary>
    /// Verifies an all-identical sample is handled without constructing a one-point empirical distribution.
    /// </summary>
    /// <remarks>
    /// Distribution-derived summaries and standardized values are undefined when only one distinct value
    /// remains, so the DataFrame reports them as unavailable rather than throwing.
    /// </remarks>
    [TestMethod]
    public void EmpiricalConsumers_AllValuesIdentical_ReportUnavailableResults()
    {
        var frame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(Enumerable.Repeat(100d, 10).ToArray())
        };
        frame.CalculatePlottingPositions();

        var moments = frame.GetNonparametricMoments();
        var summary = frame.SummaryStatisticsAllData();
        frame.SetStandardizedValues();

        Assert.IsNull(moments);
        Assert.AreEqual(100d, summary["Minimum"]);
        Assert.AreEqual(100d, summary["Maximum"]);
        Assert.IsTrue(double.IsNaN(summary["Mean"]));
        Assert.IsTrue(double.IsNaN(summary["50%"]));
        Assert.IsTrue(frame.ExactSeries.All(data =>
            double.IsNaN(data.StandardizedValue) && double.IsNaN(data.StandardizedLog10Value)));
    }
}
