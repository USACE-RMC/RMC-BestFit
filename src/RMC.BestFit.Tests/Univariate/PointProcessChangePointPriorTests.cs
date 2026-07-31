using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Tests the monthly-histogram default priors for seasonal point-process changepoints.
/// </summary>
[TestClass]
public class PointProcessChangePointPriorTests
{
    /// <summary>
    /// Verifies that calendar-month counts rotate into the selected water-year coordinate system.
    /// </summary>
    [TestMethod]
    public void Test_HistogramRotation_CalendarAndWaterYearLocateShiftedValleys()
    {
        BestFitDataFrame dataFrame = CreateMonthlyHistogramDataFrame(SeasonalMonthlyCounts());

        PointProcessModel calendarModel = CreateSeasonalModel(dataFrame, TimeBlockWindow.CalendarYear, 1);
        PointProcessModel waterYearModel = CreateSeasonalModel(dataFrame, TimeBlockWindow.WaterYear, 10);

        Assert.AreEqual(168.0, calendarModel.Parameters[0].Value, 0.0);
        Assert.AreEqual(351.5, calendarModel.Parameters[1].Value, 0.0);
        Assert.AreEqual(77.5, waterYearModel.Parameters[0].Value, 0.0);
        Assert.AreEqual(260.0, waterYearModel.Parameters[1].Value, 0.0);
    }

    /// <summary>
    /// Verifies deterministic circular smoothing, peak selection, and valley selection.
    /// </summary>
    [TestMethod]
    public void Test_HistogramSelection_TwoSeparatedPeaksProduceDeterministicValleys()
    {
        PointProcessModel first = CreateSeasonalModel(
            CreateMonthlyHistogramDataFrame(SeasonalMonthlyCounts()),
            TimeBlockWindow.CalendarYear,
            1);
        PointProcessModel second = CreateSeasonalModel(
            CreateMonthlyHistogramDataFrame(SeasonalMonthlyCounts()),
            TimeBlockWindow.CalendarYear,
            1);

        CollectionAssert.AreEqual(
            first.Parameters.Take(2).Select(parameter => parameter.Value).ToArray(),
            second.Parameters.Take(2).Select(parameter => parameter.Value).ToArray());
        Assert.AreEqual(168, (int)Math.Floor(first.Parameters[0].Value));
        Assert.AreEqual(351, (int)Math.Floor(first.Parameters[1].Value));
    }

    /// <summary>
    /// Verifies five-month flat windows and the half-open latent support convention.
    /// </summary>
    [TestMethod]
    public void Test_HistogramSupports_AreFiveMonthWindowsWithHalfOpenUpperBounds()
    {
        PointProcessModel model = CreateSeasonalModel(
            CreateMonthlyHistogramDataFrame(SeasonalMonthlyCounts()),
            TimeBlockWindow.CalendarYear,
            1);

        AssertPrior(model.Parameters[0], 168.0, 92.0, 245.0);
        AssertPrior(model.Parameters[1], 351.5, 275.0, 367.0);
    }

    /// <summary>
    /// Verifies that a flat histogram retains both approved broad supports.
    /// </summary>
    [TestMethod]
    public void Test_HistogramFallback_FlatHistogramUsesBroadSupports()
    {
        AssertBroadFallback(Enumerable.Repeat(1, 12).ToArray(), dated: true);
    }

    /// <summary>
    /// Verifies that a single seasonal peak retains both approved broad supports.
    /// </summary>
    [TestMethod]
    public void Test_HistogramFallback_UnimodalHistogramUsesBroadSupports()
    {
        AssertBroadFallback(new[] { 0, 0, 20, 4, 1, 0, 0, 0, 0, 0, 0, 0 }, dated: true);
    }

    /// <summary>
    /// Verifies that equally strong competing peak pairs retain both approved broad supports.
    /// </summary>
    [TestMethod]
    public void Test_HistogramFallback_TiedPeakPairsUseBroadSupports()
    {
        AssertBroadFallback(new[] { 12, 0, 0, 0, 12, 0, 0, 0, 12, 0, 0, 0 }, dated: true);
    }

    /// <summary>
    /// Verifies that fewer than ten exact dated events retain both approved broad supports.
    /// </summary>
    [TestMethod]
    public void Test_HistogramFallback_InsufficientDatedEventsUseBroadSupports()
    {
        AssertBroadFallback(new[] { 0, 0, 4, 0, 0, 0, 0, 0, 5, 0, 0, 0 }, dated: true);
    }

    /// <summary>
    /// Verifies that manually entered index-only events retain both approved broad supports.
    /// </summary>
    [TestMethod]
    public void Test_HistogramFallback_UndatedEventsUseBroadSupports()
    {
        AssertBroadFallback(SeasonalMonthlyCounts(), dated: false);
    }

    /// <summary>
    /// Verifies that sampling-scale variation around uniform daily exposure retains broad priors.
    /// </summary>
    [TestMethod]
    public void Test_HistogramFallback_EffectivelyFlatCountsUseBroadSupports()
    {
        AssertBroadFallback(new[] { 68, 54, 65, 61, 58, 59, 66, 57, 69, 58, 59, 58 }, dated: true);
    }

    /// <summary>
    /// Verifies that disabling default priors protects custom changepoint priors from data and block changes.
    /// </summary>
    [TestMethod]
    public void Test_CustomChangePointPriors_ArePreservedWhenDefaultsDisabled()
    {
        BestFitDataFrame originalData = CreateMonthlyHistogramDataFrame(SeasonalMonthlyCounts());
        var model = CreateSeasonalModel(originalData, TimeBlockWindow.CalendarYear, 1);
        model.UseDefaultFlatPriors = false;
        model.Parameters[0].Value = 75.5;
        model.Parameters[0].LowerBound = 20.0;
        model.Parameters[0].UpperBound = 120.0;
        model.Parameters[0].PriorDistribution = new Uniform(20.0, 120.0);
        model.Parameters[1].Value = 280.5;
        model.Parameters[1].LowerBound = 230.0;
        model.Parameters[1].UpperBound = 330.0;
        model.Parameters[1].PriorDistribution = new Uniform(230.0, 330.0);

        model.TimeBlock = TimeBlockWindow.WaterYear;
        model.StartMonth = 10;
        model.DataFrame = CreateMonthlyHistogramDataFrame(SeasonalMonthlyCounts());

        AssertPrior(model.Parameters[0], 75.5, 20.0, Math.BitIncrement(120.0));
        AssertPrior(model.Parameters[1], 280.5, 230.0, Math.BitIncrement(330.0));
    }

    /// <summary>
    /// Verifies that the histogram rule does not change any of the six established GEV defaults.
    /// </summary>
    [TestMethod]
    public void Test_HistogramDefaults_LeaveAllSixGevDefaultsOnEstablishedConstraintPath()
    {
        PointProcessModel model = CreateSeasonalModel(
            CreateMonthlyHistogramDataFrame(SeasonalMonthlyCounts()),
            TimeBlockWindow.CalendarYear,
            1);
        List<double> maxima = model.AMSDataFrame.ExactSeries.Select(observation => observation.Value).ToList();

        for (int component = 0; component < 2; component++)
        {
            var gev = (IMaximumLikelihoodEstimation)model.Distribution!.Distributions[component];
            Tuple<double[], double[], double[]> expected = gev.GetParameterConstraints(maxima);
            for (int parameter = 0; parameter < 3; parameter++)
            {
                ModelParameter actual = model.Parameters[2 + 3 * component + parameter];
                Assert.AreEqual(expected.Item1[parameter], actual.Value, 0.0);
                Assert.AreEqual(expected.Item2[parameter], actual.LowerBound, 0.0);
                Assert.AreEqual(expected.Item3[parameter], actual.UpperBound, 0.0);
            }
        }
    }

    /// <summary>
    /// Verifies that histogram-informed changepoint priors round-trip as ordinary model parameters.
    /// </summary>
    [TestMethod]
    public void Test_HistogramDefaults_SerializeAsOrdinaryModelParameterPriors()
    {
        BestFitDataFrame dataFrame = CreateMonthlyHistogramDataFrame(SeasonalMonthlyCounts());
        PointProcessModel original = CreateSeasonalModel(dataFrame, TimeBlockWindow.CalendarYear, 1);

        var restored = new PointProcessModel(dataFrame, original.ToXElement());

        for (int i = 0; i < 2; i++)
        {
            Assert.AreEqual(original.Parameters[i].Value, restored.Parameters[i].Value, 0.0);
            Assert.AreEqual(original.Parameters[i].LowerBound, restored.Parameters[i].LowerBound, 0.0);
            Assert.AreEqual(original.Parameters[i].UpperBound, restored.Parameters[i].UpperBound, 0.0);
            Assert.IsInstanceOfType<Uniform>(restored.Parameters[i].PriorDistribution);
            Assert.AreEqual(
                ((Uniform)original.Parameters[i].PriorDistribution!).Min,
                ((Uniform)restored.Parameters[i].PriorDistribution!).Min,
                0.0);
            Assert.AreEqual(
                ((Uniform)original.Parameters[i].PriorDistribution!).Max,
                ((Uniform)restored.Parameters[i].PriorDistribution!).Max,
                0.0);
        }
    }

    /// <summary>
    /// Creates a valid seasonal point-process model for a selected block convention.
    /// </summary>
    /// <param name="dataFrame">The exact POT data.</param>
    /// <param name="timeBlock">The calendar or shifted-year block convention.</param>
    /// <param name="startMonth">The first calendar month in the block.</param>
    /// <returns>A seasonal point-process model with default priors.</returns>
    private static PointProcessModel CreateSeasonalModel(
        BestFitDataFrame dataFrame,
        TimeBlockWindow timeBlock,
        int startMonth)
    {
        return new PointProcessModel
        {
            IsSeasonal = true,
            TimeBlock = timeBlock,
            StartMonth = startMonth,
            DataFrame = dataFrame
        };
    }

    /// <summary>
    /// Creates exact POT observations matching a specified calendar-month histogram.
    /// </summary>
    /// <param name="monthlyCounts">The January-through-December occurrence counts.</param>
    /// <returns>A data frame containing dated exact events.</returns>
    private static BestFitDataFrame CreateMonthlyHistogramDataFrame(int[] monthlyCounts)
    {
        var data = new List<ExactData>();
        int sequence = 0;
        for (int month = 1; month <= 12; month++)
        {
            for (int occurrence = 0; occurrence < monthlyCounts[month - 1]; occurrence++)
            {
                int year = 1980 + occurrence;
                int day = occurrence % DateTime.DaysInMonth(year, month) + 1;
                data.Add(new ExactData(new DateTime(year, month, day), 1000.0 + sequence));
                sequence++;
            }
        }

        return new BestFitDataFrame { ExactSeries = new ExactSeries(data) };
    }

    /// <summary>
    /// Creates manually entered exact records that have indexes but no dates.
    /// </summary>
    /// <param name="count">The number of exact records.</param>
    /// <returns>A data frame containing undated exact records.</returns>
    private static BestFitDataFrame CreateUndatedDataFrame(int count)
    {
        var data = new List<ExactData>();
        for (int i = 0; i < count; i++)
            data.Add(new ExactData(1980 + i, 1000.0 + i));
        return new BestFitDataFrame { ExactSeries = new ExactSeries(data) };
    }

    /// <summary>
    /// Returns a deterministic bimodal monthly histogram with broad June and December valleys.
    /// </summary>
    /// <returns>January-through-December counts.</returns>
    private static int[] SeasonalMonthlyCounts()
    {
        return new[] { 1, 4, 12, 5, 1, 0, 1, 4, 10, 4, 1, 0 };
    }

    /// <summary>
    /// Asserts that a histogram retains the approved broad changepoint supports.
    /// </summary>
    /// <param name="monthlyCounts">The occurrence counts used to create the data.</param>
    /// <param name="dated">Whether records have usable occurrence dates.</param>
    private static void AssertBroadFallback(int[] monthlyCounts, bool dated)
    {
        BestFitDataFrame dataFrame = dated
            ? CreateMonthlyHistogramDataFrame(monthlyCounts)
            : CreateUndatedDataFrame(monthlyCounts.Sum());
        PointProcessModel model = CreateSeasonalModel(dataFrame, TimeBlockWindow.CalendarYear, 1);

        AssertPrior(model.Parameters[0], 90.0, 1.0, 251.0);
        AssertPrior(model.Parameters[1], 250.0, 200.0, 367.0);
    }

    /// <summary>
    /// Asserts a model parameter and its uniform prior against a half-open support.
    /// </summary>
    /// <param name="parameter">The parameter under test.</param>
    /// <param name="expectedValue">The expected latent value.</param>
    /// <param name="expectedLower">The inclusive lower endpoint.</param>
    /// <param name="expectedUpperExclusive">The conceptual exclusive upper endpoint.</param>
    private static void AssertPrior(
        ModelParameter parameter,
        double expectedValue,
        double expectedLower,
        double expectedUpperExclusive)
    {
        double expectedUpper = Math.BitDecrement(expectedUpperExclusive);
        Assert.AreEqual(expectedValue, parameter.Value, 0.0);
        Assert.AreEqual(expectedLower, parameter.LowerBound, 0.0);
        Assert.AreEqual(expectedUpper, parameter.UpperBound, 0.0);
        Assert.IsInstanceOfType<Uniform>(parameter.PriorDistribution);
        var prior = (Uniform)parameter.PriorDistribution!;
        Assert.AreEqual(expectedLower, prior.Min, 0.0);
        Assert.AreEqual(expectedUpper, prior.Max, 0.0);
    }
}
