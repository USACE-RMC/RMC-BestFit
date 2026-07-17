using Numerics.Data;
using System;
using System.Collections.Generic;

namespace RMC_BestFit
{
    /// <summary>
    /// Builds DateTime timelines for time-series analysis result curves.
    /// </summary>
    /// <remarks>
    /// Analysis result arrays store values by step index. The App layer owns converting those
    /// steps back to display dates from the active response series and its time interval.
    /// </remarks>
    public static class TimeSeriesResultTimeline
    {
        /// <summary>
        /// Creates a sequence of display dates starting at the first observation in the source series.
        /// </summary>
        /// <param name="source">The time series whose start date and interval define the display timeline.</param>
        /// <param name="count">The number of dates to create.</param>
        /// <returns>A list containing <paramref name="count"/> dates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown when dates are requested from an empty or irregular source series.</exception>
        /// <remarks>
        /// The first result step aligns to the first observed timestamp. Subsequent steps advance by
        /// <see cref="TimeSeries.TimeInterval"/>, which also extends future forecast dates.
        /// </remarks>
        public static List<DateTime> CreateDates(TimeSeries source, int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "The number of dates cannot be negative.");
            if (source.TimeInterval == TimeInterval.Irregular)
                throw new ArgumentException("Time series analysis results require a regular time interval.", nameof(source));
            if (count > 0 && source.Count == 0)
                throw new ArgumentException("The source time series must contain at least one observation.", nameof(source));

            var dates = new List<DateTime>(count);
            if (count == 0)
                return dates;

            DateTime date = source[0].Index;
            for (int i = 0; i < count; i++)
            {
                dates.Add(date);
                date = TimeSeries.AddTimeInterval(date, source.TimeInterval);
            }

            return dates;
        }
    }
}
