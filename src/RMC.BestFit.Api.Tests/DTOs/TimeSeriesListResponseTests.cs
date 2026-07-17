using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesListResponse"/>: defaults, JSON round-trip including
    /// the inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class TimeSeriesListResponseTests
    {
        /// <summary>
        /// Verifies a new listing defaults to success with a zero count and an empty summary list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new TimeSeriesListResponse();
            Assert.IsTrue(response.Success);
            Assert.AreEqual(0, response.Count);
            Assert.IsNotNull(response.TimeSeries);
            Assert.AreEqual(0, response.TimeSeries.Count);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new TimeSeriesListResponse
            {
                Count = 1,
                TimeSeries = new List<TimeSeriesSummaryDto>
                {
                    new TimeSeriesSummaryDto { Id = Guid.NewGuid(), Name = "Series A", PointCount = 10 }
                },
                Success = false,
                ErrorMessage = "listing incomplete",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 3,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.z" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(1, copy.Count);
            Assert.AreEqual(1, copy.TimeSeries.Count);
            Assert.AreEqual(response.TimeSeries[0].Id, copy.TimeSeries[0].Id);
            Assert.AreEqual("Series A", copy.TimeSeries[0].Name);
            Assert.AreEqual(10, copy.TimeSeries[0].PointCount);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("listing incomplete", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(3L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new TimeSeriesListResponse());
            StringAssert.Contains(json, "\"count\"");
            StringAssert.Contains(json, "\"timeSeries\"");
            StringAssert.Contains(json, "\"success\"");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
        }
    }
}
