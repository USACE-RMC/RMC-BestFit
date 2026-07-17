using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateUsgsTimeSeriesRequest"/>: defaults, JSON round-trip, and
    /// camelCase wire names including string enum values.
    /// </summary>
    [TestClass]
    public class CreateUsgsTimeSeriesRequestTests
    {
        /// <summary>
        /// Verifies a new request defaults to an empty site number and daily discharge series.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateUsgsTimeSeriesRequest();
            Assert.AreEqual(string.Empty, request.SiteNumber);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.DailyDischarge, request.SeriesType);
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
        }

        /// <summary>
        /// Verifies every property, including the enum-typed series type, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateUsgsTimeSeriesRequest
            {
                SiteNumber = "01646500",
                SeriesType = TimeSeriesDownload.TimeSeriesType.MeasuredStage,
                Name = "Little Falls stage",
                Description = "Field measurements for a rating curve."
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual("01646500", copy.SiteNumber);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.MeasuredStage, copy.SeriesType);
            Assert.AreEqual("Little Falls stage", copy.Name);
            Assert.AreEqual("Field measurements for a rating curve.", copy.Description);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and camelCase enum value strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateUsgsTimeSeriesRequest
            {
                SiteNumber = "01646500",
                SeriesType = TimeSeriesDownload.TimeSeriesType.PeakStage
            });
            StringAssert.Contains(json, "\"siteNumber\"");
            StringAssert.Contains(json, "\"seriesType\"");
            StringAssert.Contains(json, "\"peakStage\"");
            Assert.IsFalse(json.Contains("\"name\""), "Null name should be omitted.");
            Assert.IsFalse(json.Contains("\"description\""), "Null description should be omitted.");
        }
    }
}
