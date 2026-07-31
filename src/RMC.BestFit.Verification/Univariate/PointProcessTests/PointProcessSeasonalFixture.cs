using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.PointProcessTests;

/// <summary>
/// Creates independent seasonal Poisson-GPA fixtures with selectable within-season timing.
/// </summary>
/// <remarks>
/// The Poisson count and Hosking-GPA mark generators are coded independently from
/// <see cref="PointProcessModel"/>. PERT timing is used only to test prior placement; uniform
/// timing supplies the posterior-recovery oracle.
/// </remarks>
internal static class PointProcessSeasonalFixture
{
    internal const double Threshold = 80.0;
    internal const double Lambda = 8.0;
    internal const double GpaScaleOne = 12.0;
    internal const double GpaScaleTwo = 40.0;
    internal const double ColesShapeOne = 0.10;
    internal const double ColesShapeTwo = -0.05;
    internal const int CalendarK1 = 170;
    internal const int CalendarK2 = 350;
    internal const int WaterYearK1 = 80;
    internal const int WaterYearK2 = 260;

    /// <summary>
    /// Identifies the verification-only distribution of event days within each seasonal support.
    /// </summary>
    internal enum EventTiming
    {
        /// <summary>Uniform occurrence over each seasonal support.</summary>
        Uniform,

        /// <summary>Symmetric PERT occurrence over each seasonal support.</summary>
        Pert
    }

    /// <summary>
    /// Generates an exact-size seasonal POT sample for the requested block convention.
    /// </summary>
    /// <param name="sampleSize">The exact number of exceedances to retain.</param>
    /// <param name="seed">The fixture random seed.</param>
    /// <param name="timeBlock">The calendar- or water-year block convention.</param>
    /// <param name="startMonth">The first calendar month in each block.</param>
    /// <param name="k1">The first effective block-day changepoint.</param>
    /// <param name="k2">The second effective block-day changepoint.</param>
    /// <param name="timing">The within-season timing distribution.</param>
    /// <returns>A dated exact POT data frame with preserved source exposure.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the sample size or start month is outside its supported range.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the changepoints do not satisfy <c>1 &lt;= K1 &lt; K2 &lt;= 366</c>.
    /// </exception>
    internal static DataFrame Generate(
        int sampleSize,
        int seed,
        TimeBlockWindow timeBlock,
        int startMonth,
        int k1,
        int k2,
        EventTiming timing)
    {
        if (sampleSize < 1)
            throw new ArgumentOutOfRangeException(nameof(sampleSize));
        if (startMonth < 1 || startMonth > 12)
            throw new ArgumentOutOfRangeException(nameof(startMonth));
        if (k1 < 1 || k1 >= k2 || k2 > 366)
            throw new ArgumentException("Changepoints must satisfy 1 <= K1 < K2 <= 366.");

        var random = new Random(seed);
        double seasonOneWeight = (k1 + 366.0 - k2) / 366.0;
        double kappaOne = -ColesShapeOne;
        double kappaTwo = -ColesShapeTwo;
        var events = new List<ExactData>(sampleSize);
        int blockIndex = 0;
        while (events.Count < sampleSize)
        {
            int annualCount = SamplePoisson(Lambda, random);
            int retainedCount = Math.Min(annualCount, sampleSize - events.Count);
            for (int eventIndex = 0; eventIndex < retainedCount; eventIndex++)
            {
                bool seasonOne = NextOpenUnit(random) < seasonOneWeight;
                int blockDay = seasonOne
                    ? SampleWrappedSeasonOneDay(k1, k2, timing, random)
                    : SampleSeasonTwoDay(k1, k2, timing, random);
                double magnitude = seasonOne
                    ? SampleHoskingGpa(Threshold, GpaScaleOne, kappaOne, random)
                    : SampleHoskingGpa(Threshold, GpaScaleTwo, kappaTwo, random);
                events.Add(new ExactData(ToDummyDate(blockIndex, blockDay, timeBlock, startMonth), magnitude));
            }

            blockIndex++;
        }

        events.Sort((left, right) => left.DateTime.CompareTo(right.DateTime));
        return new DataFrame
        {
            ExactSeries = new ExactSeries(events),
            PointProcessObservationYears = sampleSize / Lambda
        };
    }

    /// <summary>
    /// Returns the parent Hosking GEV vector implied by the two verification GPAs.
    /// </summary>
    /// <param name="k1">The first latent changepoint value.</param>
    /// <param name="k2">The second latent changepoint value.</param>
    /// <returns>The two changepoints followed by both three-parameter GEV vectors.</returns>
    internal static double[] ParentParameters(double k1, double k2)
    {
        double kappaOne = -ColesShapeOne;
        double kappaTwo = -ColesShapeTwo;
        double locationOne = Threshold + GpaScaleOne / kappaOne *
            (1.0 - Math.Pow(Lambda, -kappaOne));
        double scaleOne = GpaScaleOne * Math.Pow(Lambda, -kappaOne);
        double locationTwo = Threshold + GpaScaleTwo / kappaTwo *
            (1.0 - Math.Pow(Lambda, -kappaTwo));
        double scaleTwo = GpaScaleTwo * Math.Pow(Lambda, -kappaTwo);
        return new[]
        {
            k1,
            k2,
            locationOne,
            scaleOne,
            kappaOne,
            locationTwo,
            scaleTwo,
            kappaTwo
        };
    }

    /// <summary>
    /// Samples the wrapped first seasonal support on an unwrapped day axis.
    /// </summary>
    /// <param name="k1">The first changepoint.</param>
    /// <param name="k2">The second changepoint.</param>
    /// <param name="timing">The within-season timing distribution.</param>
    /// <param name="random">The fixture random source.</param>
    /// <returns>A block day in <c>[k2,366] ∪ [1,k1)</c>.</returns>
    private static int SampleWrappedSeasonOneDay(
        int k1,
        int k2,
        EventTiming timing,
        Random random)
    {
        int maximumExclusive = 366 + k1;
        double draw = timing == EventTiming.Pert
            ? new Pert(k2, 0.5 * (k2 + maximumExclusive), maximumExclusive).InverseCDF(NextOpenUnit(random))
            : k2 + NextOpenUnit(random) * (maximumExclusive - k2);
        int unwrappedDay = Math.Clamp((int)Math.Floor(draw), k2, maximumExclusive - 1);
        return (unwrappedDay - 1) % 366 + 1;
    }

    /// <summary>
    /// Samples the interior second seasonal support.
    /// </summary>
    /// <param name="k1">The first changepoint.</param>
    /// <param name="k2">The second changepoint.</param>
    /// <param name="timing">The within-season timing distribution.</param>
    /// <param name="random">The fixture random source.</param>
    /// <returns>A block day in <c>[k1,k2)</c>.</returns>
    private static int SampleSeasonTwoDay(int k1, int k2, EventTiming timing, Random random)
    {
        double draw = timing == EventTiming.Pert
            ? new Pert(k1, 0.5 * (k1 + k2), k2).InverseCDF(NextOpenUnit(random))
            : k1 + NextOpenUnit(random) * (k2 - k1);
        return Math.Clamp((int)Math.Floor(draw), k1, k2 - 1);
    }

    /// <summary>
    /// Converts a modeled block day to a leap-containing dummy calendar date.
    /// </summary>
    /// <param name="blockIndex">The zero-based generated block.</param>
    /// <param name="blockDay">The one-based modeled day within the block.</param>
    /// <param name="timeBlock">The block convention.</param>
    /// <param name="startMonth">The configured first month.</param>
    /// <returns>The corresponding dummy date.</returns>
    private static DateTime ToDummyDate(
        int blockIndex,
        int blockDay,
        TimeBlockWindow timeBlock,
        int startMonth)
    {
        bool shiftedYear = timeBlock == TimeBlockWindow.WaterYear || timeBlock == TimeBlockWindow.CustomYear;
        int effectiveStartMonth = shiftedYear ? startMonth : 1;
        int leapYear = 2000 + 4 * blockIndex;
        int startYear = effectiveStartMonth <= 2 ? leapYear : leapYear - 1;
        return new DateTime(startYear, effectiveStartMonth, 1).AddDays(blockDay - 1);
    }

    /// <summary>
    /// Samples a Poisson count through independent exponential inter-arrival times.
    /// </summary>
    /// <param name="mean">The annual Poisson mean.</param>
    /// <param name="random">The fixture random source.</param>
    /// <returns>A nonnegative annual event count.</returns>
    private static int SamplePoisson(double mean, Random random)
    {
        int count = 0;
        double elapsed = 0.0;
        while (true)
        {
            elapsed += -Math.Log(NextOpenUnit(random));
            if (elapsed > mean)
                return count;
            count++;
        }
    }

    /// <summary>
    /// Samples a Hosking-parameterized generalized Pareto magnitude analytically.
    /// </summary>
    /// <param name="threshold">The GPA location and POT threshold.</param>
    /// <param name="scale">The positive GPA scale.</param>
    /// <param name="kappa">The Hosking GPA shape.</param>
    /// <param name="random">The fixture random source.</param>
    /// <returns>A magnitude at or above the threshold.</returns>
    private static double SampleHoskingGpa(
        double threshold,
        double scale,
        double kappa,
        Random random)
    {
        double survival = NextOpenUnit(random);
        double excess = Math.Abs(kappa) < 1E-12
            ? -scale * Math.Log(survival)
            : scale / kappa * (1.0 - Math.Pow(survival, kappa));
        return threshold + excess;
    }

    /// <summary>
    /// Returns a uniform variate strictly inside the unit interval.
    /// </summary>
    /// <param name="random">The fixture random source.</param>
    /// <returns>An open-unit-interval variate.</returns>
    private static double NextOpenUnit(Random random)
    {
        return Math.Clamp(random.NextDouble(), 1E-15, 1.0 - 1E-15);
    }
}
