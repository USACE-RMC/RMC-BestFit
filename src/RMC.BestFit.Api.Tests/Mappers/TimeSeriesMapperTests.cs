using Numerics.Data;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesMapper"/>: summary fields, point paging, and listing.
    /// </summary>
    [TestClass]
    public class TimeSeriesMapperTests
    {
        /// <summary>
        /// Builds a USGS-sourced resource over the ten-point irregular series.
        /// </summary>
        /// <returns>The resource.</returns>
        private static TimeSeriesResource CreateResource()
        {
            return new TimeSeriesResource(TestSeries.IrregularPeaks())
            {
                Name = "peaks",
                Description = "test series",
                Source = TimeSeriesSource.Usgs,
                UsgsSiteNumber = "01646500",
                UsgsSeriesType = TimeSeriesDownload.TimeSeriesType.PeakDischarge
            };
        }

        /// <summary>
        /// Verifies the summary carries identity, provenance, and camelCase enum names.
        /// </summary>
        [TestMethod]
        public void ToSummary_MapsAllFields()
        {
            var resource = CreateResource();
            var summary = TimeSeriesMapper.ToSummary(resource);

            Assert.AreEqual(resource.Id, summary.Id);
            Assert.AreEqual("peaks", summary.Name);
            Assert.AreEqual("test series", summary.Description);
            Assert.AreEqual("usgs", summary.Source);
            Assert.AreEqual("01646500", summary.UsgsSiteNumber);
            Assert.AreEqual("peakDischarge", summary.SeriesType);
            Assert.AreEqual("irregular", summary.TimeInterval);
            Assert.AreEqual(10, summary.PointCount);
            Assert.AreEqual(0, summary.MissingCount);
            Assert.AreEqual(new DateTime(2000, 5, 1), summary.StartDate);
            Assert.AreEqual(new DateTime(2009, 5, 1), summary.EndDate);
        }

        /// <summary>
        /// Verifies points are excluded by default and included with paging when requested.
        /// </summary>
        [TestMethod]
        public void ToResourceResponse_PagesPoints()
        {
            var resource = CreateResource();

            var withoutPoints = TimeSeriesMapper.ToResourceResponse(resource);
            Assert.IsNull(withoutPoints.Points);
            Assert.IsNull(withoutPoints.PointsOffset);

            var page = TimeSeriesMapper.ToResourceResponse(resource, includePoints: true, offset: 8, limit: 5);
            Assert.IsNotNull(page.Points);
            Assert.AreEqual(2, page.Points.Count);
            Assert.AreEqual(8, page.PointsOffset);
            Assert.AreEqual(1800d, page.Points[0].Value);
        }

        /// <summary>
        /// Verifies negative paging arguments are clamped rather than throwing.
        /// </summary>
        [TestMethod]
        public void ToResourceResponse_NegativePaging_Clamped()
        {
            var resource = CreateResource();
            var page = TimeSeriesMapper.ToResourceResponse(resource, includePoints: true, offset: -5, limit: -1);
            Assert.IsNotNull(page.Points);
            Assert.AreEqual(0, page.Points.Count);
            Assert.AreEqual(0, page.PointsOffset);
        }

        /// <summary>
        /// Verifies the list response carries the count and one summary per resource.
        /// </summary>
        [TestMethod]
        public void ToListResponse_MapsAllResources()
        {
            var resources = new List<TimeSeriesResource> { CreateResource(), CreateResource() };
            var response = TimeSeriesMapper.ToListResponse(resources);
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(2, response.TimeSeries.Count);
        }
    }
}
