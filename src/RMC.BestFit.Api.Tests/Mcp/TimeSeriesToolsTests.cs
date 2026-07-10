using System.Text.Json;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Mcp
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesTools"/>: contract parity with the REST endpoints via
    /// the faked USGS seam.
    /// </summary>
    [TestClass]
    public class TimeSeriesToolsTests
    {
        /// <summary>
        /// The faked USGS seam.
        /// </summary>
        private FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// The tools under test.
        /// </summary>
        private TimeSeriesTools _tools = null!;

        /// <summary>
        /// Creates a fresh tool stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            var store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService { Result = TestSeries.DailyThreeWaterYears() };
            _tools = new TimeSeriesTools(new TimeSeriesService(store, _usgs));
        }

        /// <summary>
        /// Verifies the download tool parses the series type string and returns the resource id.
        /// </summary>
        [TestMethod]
        public async Task UsgsDownloadTimeSeries_ParsesTypeAndReturnsId()
        {
            string json = await _tools.UsgsDownloadTimeSeries("01646500", "peakDischarge");
            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("timeSeries");

            Assert.AreEqual("01646500", summary.GetProperty("usgsSiteNumber").GetString());
            Assert.AreEqual("peakDischarge", summary.GetProperty("seriesType").GetString());
            Assert.AreNotEqual(Guid.Empty, summary.GetProperty("id").GetGuid());
            Assert.AreEqual(Numerics.Data.TimeSeriesDownload.TimeSeriesType.PeakDischarge, _usgs.LastSeriesType);
        }

        /// <summary>
        /// Verifies an invalid series type string fails with the accepted-values message.
        /// </summary>
        [TestMethod]
        public async Task UsgsDownloadTimeSeries_InvalidType_Throws()
        {
            var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _tools.UsgsDownloadTimeSeries("01646500", "bogusType"));
            StringAssert.Contains(ex.Message, "dailyDischarge");
        }

        /// <summary>
        /// Verifies the manual creation and retrieval tools round-trip a series with paging.
        /// </summary>
        [TestMethod]
        public void CreateManual_ThenGet_RoundTrips()
        {
            string createdJson = _tools.CreateManualTimeSeries(new List<TimeSeriesPointDto>
            {
                new() { DateTime = new DateTime(2020, 1, 2), Value = 2d },
                new() { DateTime = new DateTime(2020, 1, 1), Value = 1d }
            }, "irregular", "manual series");

            using var createdDocument = JsonDocument.Parse(createdJson);
            var id = createdDocument.RootElement.GetProperty("timeSeries").GetProperty("id").GetGuid();

            string detailJson = _tools.GetTimeSeries(id, includePoints: true, offset: 0, limit: 10);
            using var detailDocument = JsonDocument.Parse(detailJson);
            var points = detailDocument.RootElement.GetProperty("points");

            Assert.AreEqual(2, points.GetArrayLength());
            Assert.AreEqual(1d, points[0].GetProperty("value").GetDouble(), "Points must be sorted by date.");
        }
    }
}
