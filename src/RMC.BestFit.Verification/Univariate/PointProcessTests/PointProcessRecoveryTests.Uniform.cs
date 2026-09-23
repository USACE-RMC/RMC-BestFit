using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.PointProcessTests;

/// <summary>
/// Contains uniform-timing Bayesian recovery checks for the seasonal point-process model.
/// </summary>
public partial class PointProcessRecoveryTests
{
    /// <summary>
    /// Verifies calendar-year recovery with automatic changepoint priors and uniform within-season timing.
    /// </summary>
    /// <returns>A task representing the asynchronous Bayesian analysis.</returns>
    [TestMethod]
    public async Task Test_CalendarYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints()
    {
        await VerifyUniformRecovery(
            TimeBlockWindow.CalendarYear,
            1,
            PointProcessSeasonalFixture.CalendarK1,
            PointProcessSeasonalFixture.CalendarK2,
            47001,
            "calendar-year");
    }

    /// <summary>
    /// Verifies water-year recovery by changing only the block origin from the calendar-year fixture.
    /// </summary>
    /// <returns>A task representing the asynchronous Bayesian analysis.</returns>
    /// <remarks>
    /// The parent changepoints remain fixed in block-day coordinates. Only the generated dates and
    /// model block convention shift to an October water year.
    /// </remarks>
    [TestMethod]
    public async Task Test_WaterYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints()
    {
        await VerifyUniformRecovery(
            TimeBlockWindow.WaterYear,
            10,
            PointProcessSeasonalFixture.CalendarK1,
            PointProcessSeasonalFixture.CalendarK2,
            47001,
            "water-year");
    }

    /// <summary>
    /// Runs one seasonal recovery cell without modifying the established GEV or sampler settings.
    /// </summary>
    /// <param name="timeBlock">The block convention.</param>
    /// <param name="startMonth">The configured first block month.</param>
    /// <param name="trueK1">The first effective parent changepoint.</param>
    /// <param name="trueK2">The second effective parent changepoint.</param>
    /// <param name="fixtureSeed">The independent fixture seed.</param>
    /// <param name="label">The assertion label.</param>
    /// <returns>A task representing the asynchronous Bayesian analysis.</returns>
    private static async Task VerifyUniformRecovery(
        TimeBlockWindow timeBlock,
        int startMonth,
        int trueK1,
        int trueK2,
        int fixtureSeed,
        string label)
    {
        const int sampleSize = 1000;
        DataFrame frame = PointProcessSeasonalFixture.Generate(
            sampleSize,
            fixtureSeed,
            timeBlock,
            startMonth,
            trueK1,
            trueK2,
            PointProcessSeasonalFixture.EventTiming.Uniform);
        PointProcessModel model = CreateAutomaticUniformModel(frame, timeBlock, startMonth);
        double[] parent = PointProcessSeasonalFixture.ParentParameters(trueK1 + 0.5, trueK2 + 0.5);
        AssertFixtureAndPriorAgreement(model, frame, parent, sampleSize, frame.PointProcessObservationYears, $"{label} uniform timing");
        AssertSeasonalCrosswalkAndAnnualization(
            parent,
            PointProcessSeasonalFixture.Lambda,
            PointProcessSeasonalFixture.Lambda,
            sampleSize / frame.PointProcessObservationYears,
            $"{label} uniform timing");
        AssertTruthBeatsCollapsedAlternative(model, parent, true, $"{label} uniform timing");
        Assert.AreEqual(PointProcessSeasonalFixture.Lambda, model.Lambda, 1E-12, "The parent Poisson rate was not retained.");
        if (timeBlock == TimeBlockWindow.WaterYear)
            AssertCalendarWaterYearBlockOriginParity(frame, model, trueK1, trueK2, fixtureSeed);

        PointProcessAnalysis analysis = ConfigureAnalysis(model);
        var validation = analysis.Validate();
        Assert.IsTrue(validation.IsValid, string.Join(Environment.NewLine, validation.ValidationMessages));

        await analysis.BayesianAnalysis.RunAsync(null, false);

        var results = analysis.BayesianAnalysis.Results;
        Assert.IsNotNull(results, $"The {label} uniform-timing recovery returned no posterior results. {analysis.BayesianAnalysis.LastError}");
        AssertSeasonalPosteriorRecovery(results, parent, sampleSize, trueK1, trueK2, $"{label} uniform timing");
    }

    /// <summary>
    /// Creates a seasonal point-process model using only its automatic changepoint priors.
    /// </summary>
    /// <param name="frame">The uniform-timed POT sample.</param>
    /// <param name="timeBlock">The block convention.</param>
    /// <param name="startMonth">The configured first block month.</param>
    /// <returns>A fully configured seasonal point-process model.</returns>
    private static PointProcessModel CreateAutomaticUniformModel(
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
    /// Verifies that changing only the block origin preserves the generated sample and likelihood.
    /// </summary>
    /// <param name="waterFrame">The October-water-year fixture.</param>
    /// <param name="waterModel">The model configured from the water-year fixture.</param>
    /// <param name="k1">The common first block-day changepoint.</param>
    /// <param name="k2">The common second block-day changepoint.</param>
    /// <param name="fixtureSeed">The common fixture seed.</param>
    private static void AssertCalendarWaterYearBlockOriginParity(
        DataFrame waterFrame,
        PointProcessModel waterModel,
        int k1,
        int k2,
        int fixtureSeed)
    {
        DataFrame calendarFrame = PointProcessSeasonalFixture.Generate(
            waterFrame.ExactSeries.Count,
            fixtureSeed,
            TimeBlockWindow.CalendarYear,
            1,
            k1,
            k2,
            PointProcessSeasonalFixture.EventTiming.Uniform);
        PointProcessModel calendarModel = CreateAutomaticUniformModel(
            calendarFrame,
            TimeBlockWindow.CalendarYear,
            1);

        Assert.AreEqual(calendarFrame.ExactSeries.Count, waterFrame.ExactSeries.Count);
        for (int i = 0; i < calendarFrame.ExactSeries.Count; i++)
        {
            var calendarObservation = (ExactData)calendarFrame.ExactSeries[i];
            var waterObservation = (ExactData)waterFrame.ExactSeries[i];
            Assert.AreEqual(calendarObservation.Value, waterObservation.Value, 0.0,
                $"Magnitude {i} changed when only the block origin shifted.");
            Assert.AreEqual(calendarObservation.DateTime.AddDays(-92), waterObservation.DateTime,
                $"Date {i} was not shifted from January 1 to October 1.");
        }

        CollectionAssert.AreEqual(calendarModel.POTDays, waterModel.POTDays,
            "Calendar and water-year dates did not map to identical block days.");
        double[] parent = PointProcessSeasonalFixture.ParentParameters(k1 + 0.5, k2 + 0.5);
        Assert.AreEqual(calendarModel.DataLogLikelihood(parent), waterModel.DataLogLikelihood(parent), 1E-10,
            "Changing only the block origin changed the parent data log-likelihood.");
    }
}
