using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesSummaryDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class TimeSeriesSummaryDtoTests
    {
        /// <summary>
        /// Verifies a new summary defaults to an empty id, zero counts, and null descriptive fields.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new TimeSeriesSummaryDto();
            Assert.AreEqual(Guid.Empty, dto.Id);
            Assert.IsNull(dto.Name);
            Assert.IsNull(dto.Description);
            Assert.AreEqual(default, dto.CreatedUtc);
            Assert.IsNull(dto.Source);
            Assert.IsNull(dto.UsgsSiteNumber);
            Assert.IsNull(dto.SeriesType);
            Assert.IsNull(dto.TimeInterval);
            Assert.AreEqual(0, dto.PointCount);
            Assert.AreEqual(0, dto.MissingCount);
            Assert.IsNull(dto.StartDate);
            Assert.IsNull(dto.EndDate);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new TimeSeriesSummaryDto
            {
                Id = Guid.NewGuid(),
                Name = "Potomac daily flow",
                Description = "USGS download.",
                CreatedUtc = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
                Source = "usgs",
                UsgsSiteNumber = "01646500",
                SeriesType = "dailyDischarge",
                TimeInterval = "oneDay",
                PointCount = 36500,
                MissingCount = 12,
                StartDate = new DateTime(1930, 10, 1),
                EndDate = new DateTime(2026, 6, 30)
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual(dto.Id, copy.Id);
            Assert.AreEqual("Potomac daily flow", copy.Name);
            Assert.AreEqual("USGS download.", copy.Description);
            Assert.AreEqual(dto.CreatedUtc, copy.CreatedUtc);
            Assert.AreEqual("usgs", copy.Source);
            Assert.AreEqual("01646500", copy.UsgsSiteNumber);
            Assert.AreEqual("dailyDischarge", copy.SeriesType);
            Assert.AreEqual("oneDay", copy.TimeInterval);
            Assert.AreEqual(36500, copy.PointCount);
            Assert.AreEqual(12, copy.MissingCount);
            Assert.AreEqual(new DateTime(1930, 10, 1), copy.StartDate);
            Assert.AreEqual(new DateTime(2026, 6, 30), copy.EndDate);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new TimeSeriesSummaryDto { UsgsSiteNumber = "01646500" });
            StringAssert.Contains(json, "\"pointCount\"");
            StringAssert.Contains(json, "\"missingCount\"");
            StringAssert.Contains(json, "\"usgsSiteNumber\"");

            string defaultJson = TestJson.Serialize(new TimeSeriesSummaryDto());
            Assert.IsFalse(defaultJson.Contains("\"startDate\""), "Null startDate should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"endDate\""), "Null endDate should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"usgsSiteNumber\""), "Null usgsSiteNumber should be omitted.");
        }
    }
}
