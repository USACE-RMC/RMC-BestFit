using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateUsgsPeaksInputDataRequest"/>: defaults, JSON round-trip, and
    /// camelCase wire names including string enum values.
    /// </summary>
    [TestClass]
    public class CreateUsgsPeaksInputDataRequestTests
    {
        /// <summary>
        /// Verifies a new request defaults to an empty site number and the peak-discharge series.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateUsgsPeaksInputDataRequest();
            Assert.AreEqual(string.Empty, request.SiteNumber);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.PeakDischarge, request.SeriesType);
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
        }

        /// <summary>
        /// Verifies every property, including the enum-typed series type, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateUsgsPeaksInputDataRequest
            {
                SiteNumber = "01646500",
                SeriesType = TimeSeriesDownload.TimeSeriesType.PeakStage,
                Name = "Little Falls peaks",
                Description = "Annual peak stages."
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual("01646500", copy.SiteNumber);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.PeakStage, copy.SeriesType);
            Assert.AreEqual("Little Falls peaks", copy.Name);
            Assert.AreEqual("Annual peak stages.", copy.Description);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and camelCase enum value strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateUsgsPeaksInputDataRequest { SiteNumber = "01646500" });
            StringAssert.Contains(json, "\"siteNumber\"");
            StringAssert.Contains(json, "\"seriesType\"");
            StringAssert.Contains(json, "\"peakDischarge\"");
            Assert.IsFalse(json.Contains("\"name\""), "Null name should be omitted.");
            Assert.IsFalse(json.Contains("\"description\""), "Null description should be omitted.");
        }
    }
}
