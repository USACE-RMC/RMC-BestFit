using Numerics.Data;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Store
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesResource"/>: constructor validation and the summary
    /// statistics computed at creation.
    /// </summary>
    [TestClass]
    public class TimeSeriesResourceTests
    {
        /// <summary>
        /// Verifies the constructor rejects a null series.
        /// </summary>
        [TestMethod]
        public void Constructor_NullSeries_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => _ = new TimeSeriesResource(null!) { Name = "x", Source = TimeSeriesSource.Manual });
        }

        /// <summary>
        /// Verifies point count, missing count, and date range are computed from the series.
        /// </summary>
        [TestMethod]
        public void Constructor_ComputesSummary()
        {
            var series = new TimeSeries(TimeInterval.OneDay);
            series.Add(new SeriesOrdinate<DateTime, double>(new DateTime(2020, 1, 1), 1d));
            series.Add(new SeriesOrdinate<DateTime, double>(new DateTime(2020, 1, 2), double.NaN));
            series.Add(new SeriesOrdinate<DateTime, double>(new DateTime(2020, 1, 3), 3d));

            var resource = new TimeSeriesResource(series) { Name = "ts", Source = TimeSeriesSource.Manual };

            Assert.AreEqual(3, resource.PointCount);
            Assert.AreEqual(1, resource.MissingCount);
            Assert.AreEqual(new DateTime(2020, 1, 1), resource.StartDate);
            Assert.AreEqual(new DateTime(2020, 1, 3), resource.EndDate);
        }

        /// <summary>
        /// Verifies an empty series yields null start/end dates and zero counts.
        /// </summary>
        [TestMethod]
        public void Constructor_EmptySeries_HasNullDates()
        {
            var resource = new TimeSeriesResource(new TimeSeries(TimeInterval.Irregular)) { Name = "ts", Source = TimeSeriesSource.Manual };
            Assert.AreEqual(0, resource.PointCount);
            Assert.AreEqual(0, resource.MissingCount);
            Assert.IsNull(resource.StartDate);
            Assert.IsNull(resource.EndDate);
        }

        /// <summary>
        /// Verifies identity and provenance init-properties round-trip and each resource gets a unique id.
        /// </summary>
        [TestMethod]
        public void Properties_RoundTrip_AndIdsAreUnique()
        {
            var a = new TimeSeriesResource(TestSeries.IrregularPeaks())
            {
                Name = "usgs series",
                Description = "desc",
                Source = TimeSeriesSource.Usgs,
                UsgsSiteNumber = "01646500",
                UsgsSeriesType = TimeSeriesDownload.TimeSeriesType.PeakDischarge
            };
            var b = new TimeSeriesResource(TestSeries.IrregularPeaks()) { Name = "b", Source = TimeSeriesSource.Manual };

            Assert.AreEqual("usgs series", a.Name);
            Assert.AreEqual("desc", a.Description);
            Assert.AreEqual(TimeSeriesSource.Usgs, a.Source);
            Assert.AreEqual("01646500", a.UsgsSiteNumber);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.PeakDischarge, a.UsgsSeriesType);
            Assert.AreNotEqual(a.Id, b.Id);
        }
    }
}
