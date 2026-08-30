using Numerics.Data;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.PointProcessTests;

/// <summary>
/// Verifies automatic seasonal changepoint-prior placement against an independently calculated
/// occurrence-histogram oracle on realistic PERT event timing.
/// </summary>
/// <remarks>
/// Each cell contains 4,000 generated dated POT occurrences. The oracle independently rotates and
/// smooths the monthly occurrence counts, selects the unique separated peak pair, locates the two
/// intervening valleys, and constructs the documented five-month supports. These are prior-placement
/// claims, not parameter-estimator recovery claims; no optimizer or MCMC sampler is invoked.
/// </remarks>
[TestClass]
public class PointProcessPriorTests
{
    /// <summary>
    /// Verifies calendar-year automatic priors against the independent occurrence-histogram oracle.
    /// </summary>
    [TestMethod]
    public void Test_CalendarYearPertHistogram_MatchesIndependentPriorPlacementOracle()
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

        AssertMatchesIndependentOracle(frame, model, 1, PointProcessSeasonalFixture.CalendarK1, PointProcessSeasonalFixture.CalendarK2, "calendar-year");
    }

    /// <summary>
    /// Verifies water-year automatic priors against the independently rotated occurrence histogram.
    /// </summary>
    [TestMethod]
    public void Test_WaterYearPertHistogram_MatchesIndependentPriorPlacementOracle()
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

        AssertMatchesIndependentOracle(frame, model, 10, PointProcessSeasonalFixture.WaterYearK1, PointProcessSeasonalFixture.WaterYearK2, "water-year");
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
    /// Compares both production priors with an independently calculated monthly-histogram placement.
    /// </summary>
    /// <param name="frame">The PERT-timed dated occurrence sample.</param>
    /// <param name="model">The production model containing automatic priors.</param>
    /// <param name="firstMonth">The first calendar month in block-year order.</param>
    /// <param name="trueK1">The generating first effective changepoint.</param>
    /// <param name="trueK2">The generating second effective changepoint.</param>
    /// <param name="label">The assertion label.</param>
    private static void AssertMatchesIndependentOracle(
        DataFrame frame,
        PointProcessModel model,
        int firstMonth,
        int trueK1,
        int trueK2,
        string label)
    {
        double[] monthlyCounts = IndependentMonthlyCounts(frame, firstMonth);
        (int firstValley, int secondValley) = IndependentValleys(monthlyCounts);
        int[] monthStarts = IndependentMonthStarts(firstMonth);
        (double value, double lower, double upper) expectedK1 =
            IndependentSupport(firstValley, monthStarts, 1.0, 251.0);
        (double value, double lower, double upper) expectedK2 =
            IndependentSupport(secondValley, monthStarts, 200.0, 367.0);

        AssertPrior(model.Parameters[0], expectedK1, $"{label} K1");
        AssertPrior(model.Parameters[1], expectedK2, $"{label} K2");
        Assert.IsTrue(trueK1 >= expectedK1.lower && trueK1 <= expectedK1.upper,
            $"The independent {label} K1 support did not contain generating day {trueK1}.");
        Assert.IsTrue(trueK2 >= expectedK2.lower && trueK2 <= expectedK2.upper,
            $"The independent {label} K2 support did not contain generating day {trueK2}.");
    }

    /// <summary>Counts dated occurrences by calendar month after independent block-year rotation.</summary>
    /// <param name="frame">The exact dated occurrence sample.</param>
    /// <param name="firstMonth">The first calendar month in block-year order.</param>
    /// <returns>Twelve counts in block-year order.</returns>
    private static double[] IndependentMonthlyCounts(DataFrame frame, int firstMonth)
    {
        var counts = new double[12];
        foreach (ExactData observation in frame.ExactSeries)
        {
            int blockMonth = (observation.DateTime.Month - firstMonth + 12) % 12;
            counts[blockMonth]++;
        }

        return counts;
    }

    /// <summary>Locates valleys with an independent implementation of the documented histogram rule.</summary>
    /// <param name="counts">Monthly occurrence counts in block-year order.</param>
    /// <returns>The ordered zero-based valley months.</returns>
    private static (int First, int Second) IndependentValleys(double[] counts)
    {
        double[] smoothed = Enumerable.Range(0, 12)
            .Select(index => (counts[(index + 11) % 12] + 2.0 * counts[index] + counts[(index + 1) % 12]) / 4.0)
            .ToArray();
        double mean = smoothed.Average();
        int[] peaks = Enumerable.Range(0, 12)
            .Where(index =>
                smoothed[index] >= smoothed[(index + 11) % 12] &&
                smoothed[index] >= smoothed[(index + 1) % 12] &&
                (smoothed[index] > smoothed[(index + 11) % 12] || smoothed[index] > smoothed[(index + 1) % 12]))
            .ToArray();
        var candidatePairs = (
            from leftIndex in Enumerable.Range(0, peaks.Length)
            from rightIndex in Enumerable.Range(leftIndex + 1, peaks.Length - leftIndex - 1)
            let left = peaks[leftIndex]
            let right = peaks[rightIndex]
            let separation = (right - left + 12) % 12
            where separation >= 3 && separation <= 9 && smoothed[left] > mean && smoothed[right] > mean
            select (Left: left, Right: right, Score: smoothed[left] + smoothed[right]))
            .ToArray();
        Assert.IsTrue(candidatePairs.Length > 0, "The independent histogram oracle found no separated seasonal peak pair.");
        double maximumScore = candidatePairs.Max(pair => pair.Score);
        var bestPairs = candidatePairs.Where(pair => pair.Score == maximumScore).ToArray();
        Assert.AreEqual(1, bestPairs.Length, "The independent histogram oracle did not identify one unique peak pair.");

        int valleyA = IndependentCircularValley(smoothed, bestPairs[0].Left, bestPairs[0].Right);
        int valleyB = IndependentCircularValley(smoothed, bestPairs[0].Right, bestPairs[0].Left);
        return (Math.Min(valleyA, valleyB), Math.Max(valleyA, valleyB));
    }

    /// <summary>Finds the middle lowest cell on one open circular arc.</summary>
    /// <param name="values">Smoothed monthly counts.</param>
    /// <param name="start">Excluded starting peak.</param>
    /// <param name="end">Excluded ending peak.</param>
    /// <returns>The selected valley month.</returns>
    private static int IndependentCircularValley(double[] values, int start, int end)
    {
        var arc = new List<int>();
        for (int index = (start + 1) % 12; index != end; index = (index + 1) % 12)
            arc.Add(index);
        double minimum = arc.Min(index => values[index]);
        int[] tied = arc.Where(index => values[index] == minimum).ToArray();
        return tied[(tied.Length - 1) / 2];
    }

    /// <summary>Builds canonical leap-year month boundaries independently.</summary>
    /// <param name="firstMonth">The first calendar month in block-year order.</param>
    /// <returns>Thirteen one-based boundaries spanning 366 days.</returns>
    private static int[] IndependentMonthStarts(int firstMonth)
    {
        var starts = new int[13];
        starts[0] = 1;
        for (int index = 0; index < 12; index++)
        {
            int month = (firstMonth - 1 + index) % 12 + 1;
            starts[index + 1] = starts[index] + DateTime.DaysInMonth(2000, month);
        }

        return starts;
    }

    /// <summary>Constructs the independently expected five-month support.</summary>
    /// <param name="valley">The zero-based valley month.</param>
    /// <param name="monthStarts">Canonical block-month boundaries.</param>
    /// <param name="baseLower">Base inclusive lower limit.</param>
    /// <param name="baseUpperExclusive">Base exclusive upper limit.</param>
    /// <returns>The expected initial value and inclusive floating-point bounds.</returns>
    private static (double Value, double Lower, double Upper) IndependentSupport(
        int valley,
        int[] monthStarts,
        double baseLower,
        double baseUpperExclusive)
    {
        double value = 0.5 * (monthStarts[valley] + monthStarts[valley + 1]);
        double lower = Math.Max(baseLower, monthStarts[Math.Max(0, valley - 2)]);
        double upperExclusive = Math.Min(baseUpperExclusive, monthStarts[Math.Min(12, valley + 3)]);
        return (value, lower, Math.BitDecrement(upperExclusive));
    }

    /// <summary>Compares one production prior with its independent oracle tuple.</summary>
    /// <param name="parameter">Production changepoint parameter.</param>
    /// <param name="expected">Independent initial value and bounds.</param>
    /// <param name="label">Assertion label.</param>
    private static void AssertPrior(
        ModelParameter parameter,
        (double Value, double Lower, double Upper) expected,
        string label)
    {
        Assert.AreEqual(expected.Value, parameter.Value, 0.0, $"{label} initial value differed from the independent oracle.");
        Assert.AreEqual(expected.Lower, parameter.LowerBound, 0.0, $"{label} lower bound differed from the independent oracle.");
        Assert.AreEqual(expected.Upper, parameter.UpperBound, 0.0, $"{label} upper bound differed from the independent oracle.");
    }
}
