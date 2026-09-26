using Numerics.Data;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Verifies the average event rate of a peaks-over-threshold data frame, and the retention or
/// clearing of the retained POT source-observation span as the exact-data source changes.
/// </summary>
[TestClass]
public class DataFrameLambdaTests
{
    /// <summary>
    /// Monthly flows from which three peaks exceed 100 with at least two steps between clusters.
    /// </summary>
    private static readonly double[] MonthlyFlows =
    [
        122, 244, 214, 173, 229, 156, 212, 263, 146, 183, 161, 205, 135, 331, 225, 174, 98.8, 149, 238, 262,
        132, 235, 216, 240, 230, 192, 195, 172, 173, 172, 153, 142, 317, 161, 201, 204, 194, 164, 183, 161,
        167, 179, 185, 117, 192, 337, 125, 166, 99.1, 202, 230, 158, 262, 154, 164, 182, 164, 183, 171, 250,
        184, 205, 237, 177, 239, 187, 180, 173, 174
    ];

    /// <summary>
    /// The rate of a peaks-over-threshold frame is events per observed year. Replacing the exact
    /// series (a bootstrap refit, an interactive replace, or an API round trip) must not switch the
    /// rate to events per span of the retained peaks.
    /// </summary>
    [TestMethod]
    public void Lambda_AfterExactSeriesReplacement_KeepsPeaksOverThresholdObservationSpan()
    {
        var series = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(2023, 1, 1), MonthlyFlows);
        var frame = new BestFitDataFrame();
        frame.CreatePeaksOverThresholdSeries(series, 100, 2);

        double observationYears = frame.PointProcessObservationYears;
        double lambda = frame.Lambda;
        Assert.IsTrue(double.IsFinite(observationYears) && observationYears > 0.0);
        Assert.AreEqual(frame.ExactSeries.Count / observationYears, lambda, 1e-12);
        Assert.AreNotEqual(frame.ExactSeries.Count / frame.ExactSeries.IndexSpan(), lambda, 1e-6,
            "The fixture must distinguish the observed span from the span of the retained peaks.");

        frame.ExactSeries = new ExactSeries(frame.ExactSeries.ToList());

        Assert.AreEqual(observationYears, frame.PointProcessObservationYears, 0.0);
        Assert.AreEqual(lambda, frame.Lambda, 1e-12);
    }

    /// <summary>
    /// A block series has no leading or trailing zero-event years to preserve, so any POT source
    /// exposure retained from an earlier peaks-over-threshold extraction over a different period
    /// is stale once the source switches to block maxima. <c>CreateBlockSeries</c> must clear the
    /// retained span so <c>CalculateLambda</c>, and any point-process model that infers its
    /// exposure from the data frame, fall back to the new block series' own index span instead of
    /// silently reusing the earlier extraction's exposure.
    /// </summary>
    [TestMethod]
    public void Lambda_AfterCreateBlockSeries_ClearsPeaksOverThresholdObservationSpan()
    {
        // POT extraction over 1990-2020 (31 years) records a finite source exposure.
        var potValues = Enumerable.Repeat(10.0, 372).ToArray();
        potValues[186] = 500.0;
        var potSeries = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(1990, 1, 1), potValues);
        var frame = new BestFitDataFrame();
        frame.CreatePeaksOverThresholdSeries(potSeries, 100.0, 2);

        Assert.AreEqual(31.0, frame.PointProcessObservationYears, 0.0,
            "Setup sanity check: POT extraction must record the 1990-2020 source exposure before the source changes.");

        // Switching the source to block maxima over the longer 1950-2020 span (71 years) must
        // clear the stale POT exposure rather than let it silently carry over.
        var blockValues = Enumerable.Repeat(50.0, 71 * 12).ToArray();
        var blockSeries = new NumericsTimeSeries(TimeInterval.OneMonth, new DateTime(1950, 1, 1), blockValues);
        frame.CreateBlockSeries(blockSeries, TimeBlockWindow.CalendarYear, BlockFunctionType.Maximum, SmoothingFunctionType.None, 1, 12, 1);

        Assert.IsTrue(double.IsNaN(frame.PointProcessObservationYears),
            "CreateBlockSeries must clear the retained POT exposure; a block series has no zero-event years to preserve.");
        Assert.AreEqual(1.0, frame.Lambda, 0.0);
    }
}
