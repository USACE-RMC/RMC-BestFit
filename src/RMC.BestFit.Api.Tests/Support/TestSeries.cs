using Numerics.Data;

namespace RMC.BestFit.Api.Tests.Support
{
    /// <summary>
    /// Builders for small synthetic time series with known block maxima and peaks, used to verify
    /// input-data extraction without network access or long computations.
    /// </summary>
    public static class TestSeries
    {
        /// <summary>
        /// The planted water-year maxima of <see cref="DailyThreeWaterYears"/>, keyed by water year.
        /// </summary>
        public static readonly IReadOnlyDictionary<int, double> PlantedMaxima = new Dictionary<int, double>
        {
            [1991] = 500d,
            [1992] = 800d,
            [1993] = 650d
        };

        /// <summary>
        /// Builds a complete daily series spanning water years 1991-1993 (1990-10-01 through
        /// 1993-09-30) with a strictly receding base flow (300 declining by 0.1 per day, always
        /// below 400) and one planted maximum per water year: 500 on 1991-03-15, 800 on
        /// 1992-01-10, and 650 on 1993-06-05.
        /// </summary>
        /// <returns>The synthetic daily series.</returns>
        /// <remarks>
        /// The base flow recedes rather than staying constant because the peaks-over-threshold
        /// clustering in the model layer treats a non-decreasing run as one continuing event; a
        /// flat plateau would merge planted peaks into a single cluster.
        /// </remarks>
        public static TimeSeries DailyThreeWaterYears()
        {
            var series = new TimeSeries(TimeInterval.OneDay);
            var start = new DateTime(1990, 10, 1);
            var end = new DateTime(1993, 9, 30);
            int dayIndex = 0;
            for (var date = start; date <= end; date = date.AddDays(1), dayIndex++)
            {
                double value = 300d - 0.1d * dayIndex;
                if (date == new DateTime(1991, 3, 15)) value = 500d;
                else if (date == new DateTime(1992, 1, 10)) value = 800d;
                else if (date == new DateTime(1993, 6, 5)) value = 650d;
                series.Add(new SeriesOrdinate<DateTime, double>(date, value));
            }
            return series;
        }

        /// <summary>
        /// Builds a complete daily series spanning the requested number of water years starting
        /// 1990-10-01, with a receding base flow (always below 400) and one planted peak per
        /// water year on March 15 (500, 520, 540, ...). Intended for peaks-over-threshold
        /// extraction at threshold 400, which yields exactly one event per water year — enough
        /// exceedances for point process parameter constraints.
        /// </summary>
        /// <param name="waterYears">The number of water years to span. Default 15.</param>
        /// <returns>The synthetic daily series.</returns>
        public static TimeSeries DailyWithAnnualPeaks(int waterYears = 15)
        {
            var series = new TimeSeries(TimeInterval.OneDay);
            var start = new DateTime(1990, 10, 1);
            var end = start.AddYears(waterYears).AddDays(-1);
            int dayIndex = 0;
            for (var date = start; date <= end; date = date.AddDays(1), dayIndex++)
            {
                // Recede slowly and wrap so the base flow stays within (250, 300) — always below
                // the 400 threshold and never a non-decreasing run that would merge peak clusters.
                double value = 300d - 0.01d * (dayIndex % 5000);
                if (date.Month == 3 && date.Day == 15)
                {
                    value = 500d + 20d * (date.Year - 1991);
                }
                series.Add(new SeriesOrdinate<DateTime, double>(date, value));
            }
            return series;
        }

        /// <summary>
        /// Builds an irregular series of discrete measurements (e.g., annual peak values), one per
        /// year from 2000 through 2009, with values 1000, 1100, ..., 1900.
        /// </summary>
        /// <returns>The synthetic irregular series.</returns>
        public static TimeSeries IrregularPeaks()
        {
            var series = new TimeSeries(TimeInterval.Irregular);
            for (int year = 2000; year < 2010; year++)
            {
                series.Add(new SeriesOrdinate<DateTime, double>(new DateTime(year, 5, 1), 1000d + (year - 2000) * 100d));
            }
            return series;
        }
    }
}
