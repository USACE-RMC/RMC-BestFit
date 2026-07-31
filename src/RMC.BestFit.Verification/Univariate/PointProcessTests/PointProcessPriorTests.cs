using Numerics.Data;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.PointProcessTests;

/// <summary>
/// Verifies automatic seasonal changepoint-prior placement on realistic PERT event timing.
/// </summary>
[TestClass]
public class PointProcessPriorTests
{
    /// <summary>
    /// Verifies that calendar-year PERT timing places both automatic priors over the parent changepoints.
    /// </summary>
    [TestMethod]
    public void Test_CalendarYearPertHistogram_DefaultPriorsContainBothChangePoints()
    {
        const int sampleSize = 4000;
        DataFrame frame = PointProcessSeasonalFixture.Generate(
            sampleSize,
            45001,
            TimeBlockWindow.CalendarYear,
            1,
            PointProcessSeasonalFixture.CalendarK1,
            PointProcessSeasonalFixture.CalendarK2,
            PointProcessSeasonalFixture.EventTiming.Pert);
        PointProcessModel model = CreateAutomaticPriorModel(frame, TimeBlockWindow.CalendarYear, 1);

        AssertContains(model.Parameters[0], PointProcessSeasonalFixture.CalendarK1, "calendar K1");
        AssertContains(model.Parameters[1], PointProcessSeasonalFixture.CalendarK2, "calendar K2");
    }

    /// <summary>
    /// Verifies that water-year rotation places both automatic priors over the shifted parent changepoints.
    /// </summary>
    [TestMethod]
    public void Test_WaterYearPertHistogram_DefaultPriorsContainShiftedChangePoints()
    {
        const int sampleSize = 4000;
        DataFrame frame = PointProcessSeasonalFixture.Generate(
            sampleSize,
            46001,
            TimeBlockWindow.WaterYear,
            10,
            PointProcessSeasonalFixture.WaterYearK1,
            PointProcessSeasonalFixture.WaterYearK2,
            PointProcessSeasonalFixture.EventTiming.Pert);
        PointProcessModel model = CreateAutomaticPriorModel(frame, TimeBlockWindow.WaterYear, 10);

        AssertContains(model.Parameters[0], PointProcessSeasonalFixture.WaterYearK1, "water-year K1");
        AssertContains(model.Parameters[1], PointProcessSeasonalFixture.WaterYearK2, "water-year K2");
    }

    /// <summary>
    /// Creates a seasonal model that obtains both changepoint priors from the production heuristic.
    /// </summary>
    /// <param name="frame">The PERT-timed exact POT sample.</param>
    /// <param name="timeBlock">The block convention.</param>
    /// <param name="startMonth">The first month in the block.</param>
    /// <returns>A configured model with automatic flat priors.</returns>
    private static PointProcessModel CreateAutomaticPriorModel(
        DataFrame frame,
        TimeBlockWindow timeBlock,
        int startMonth)
    {
        var model = new PointProcessModel
        {
            UseDefaults = false,
            IsSeasonal = true,
            TimeBlock = timeBlock,
            StartMonth = startMonth,
            DataFrame = frame
        };
        model.Threshold = PointProcessSeasonalFixture.Threshold;
        model.TotalYears = frame.PointProcessObservationYears;
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>
    /// Asserts that an automatic continuous support contains an effective integer changepoint.
    /// </summary>
    /// <param name="parameter">The automatically configured changepoint parameter.</param>
    /// <param name="truth">The effective parent day.</param>
    /// <param name="label">The assertion label.</param>
    private static void AssertContains(ModelParameter parameter, int truth, string label)
    {
        Assert.IsTrue(
            truth >= parameter.LowerBound && truth <= parameter.UpperBound,
            $"The automatic {label} support [{parameter.LowerBound:G17}, {parameter.UpperBound:G17}] did not contain {truth}.");
        Assert.IsTrue(
            parameter.Value >= parameter.LowerBound && parameter.Value <= parameter.UpperBound,
            $"The automatic {label} initial value was outside its support.");
    }
}
