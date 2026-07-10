using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using System;
using System.Collections.Generic;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TimeSeriesResultTimeline"/>.
    /// </summary>
    [TestClass]
    public class TimeSeriesResultTimelineTests
    {
        /// <summary>
        /// Verifies monthly result dates advance by calendar month from the first observation.
        /// </summary>
        [TestMethod]
        public void CreateDates_MonthlySeries_AdvancesByMonth()
        {
            var series = CreateSeries(TimeInterval.OneMonth, new DateTime(2020, 1, 1), 3);

            List<DateTime> dates = RMC_BestFit.TimeSeriesResultTimeline.CreateDates(series, 5);

            CollectionAssert.AreEqual(
                new[]
                {
                    new DateTime(2020, 1, 1),
                    new DateTime(2020, 2, 1),
                    new DateTime(2020, 3, 1),
                    new DateTime(2020, 4, 1),
                    new DateTime(2020, 5, 1),
                },
                dates);
        }

        /// <summary>
        /// Verifies annual result dates advance by calendar year from the first observation.
        /// </summary>
        [TestMethod]
        public void CreateDates_AnnualSeries_AdvancesByYear()
        {
            var series = CreateSeries(TimeInterval.OneYear, new DateTime(1999, 10, 1), 2);

            List<DateTime> dates = RMC_BestFit.TimeSeriesResultTimeline.CreateDates(series, 4);

            CollectionAssert.AreEqual(
                new[]
                {
                    new DateTime(1999, 10, 1),
                    new DateTime(2000, 10, 1),
                    new DateTime(2001, 10, 1),
                    new DateTime(2002, 10, 1),
                },
                dates);
        }

        /// <summary>
        /// Verifies requesting zero dates from an empty source returns an empty list.
        /// </summary>
        [TestMethod]
        public void CreateDates_ZeroCount_AllowsEmptySeries()
        {
            var series = new TimeSeries(TimeInterval.OneMonth);

            List<DateTime> dates = RMC_BestFit.TimeSeriesResultTimeline.CreateDates(series, 0);

            Assert.AreEqual(0, dates.Count);
        }

        /// <summary>
        /// Verifies requesting dates from an empty source is rejected.
        /// </summary>
        [TestMethod]
        public void CreateDates_PositiveCountWithEmptySeries_Throws()
        {
            var series = new TimeSeries(TimeInterval.OneMonth);

            Assert.ThrowsException<ArgumentException>(() => RMC_BestFit.TimeSeriesResultTimeline.CreateDates(series, 1));
        }

        /// <summary>
        /// Verifies irregular source series are rejected because no deterministic display step exists.
        /// </summary>
        [TestMethod]
        public void CreateDates_IrregularSeries_Throws()
        {
            var series = new TimeSeries(TimeInterval.Irregular);
            series.Add(new SeriesOrdinate<DateTime, double>(new DateTime(2020, 1, 1), 1.0));

            Assert.ThrowsException<ArgumentException>(() => RMC_BestFit.TimeSeriesResultTimeline.CreateDates(series, 1));
        }

        /// <summary>
        /// Creates a deterministic time series for timeline tests.
        /// </summary>
        /// <param name="interval">The interval between observations.</param>
        /// <param name="start">The first timestamp.</param>
        /// <param name="count">The number of observations.</param>
        /// <returns>A populated time series.</returns>
        private static TimeSeries CreateSeries(TimeInterval interval, DateTime start, int count)
        {
            var series = new TimeSeries(interval);
            DateTime date = start;
            for (int i = 0; i < count; i++)
            {
                series.Add(new SeriesOrdinate<DateTime, double>(date, i + 1.0));
                date = TimeSeries.AddTimeInterval(date, interval);
            }

            return series;
        }
    }
}
