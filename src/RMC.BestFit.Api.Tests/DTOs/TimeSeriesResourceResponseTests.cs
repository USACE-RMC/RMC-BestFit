using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesResourceResponse"/>: defaults, JSON round-trip including
    /// the inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class TimeSeriesResourceResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with no summary, points, or offset.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new TimeSeriesResourceResponse();
            Assert.IsTrue(response.Success);
            Assert.IsNull(response.TimeSeries);
            Assert.IsNull(response.Points);
            Assert.IsNull(response.PointsOffset);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new TimeSeriesResourceResponse
            {
                TimeSeries = new TimeSeriesSummaryDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Potomac daily",
                    PointCount = 2
                },
                Points = new List<TimeSeriesPointDto>
                {
                    new TimeSeriesPointDto { DateTime = new DateTime(1996, 10, 1), Value = 15200.5 },
                    new TimeSeriesPointDto { DateTime = new DateTime(1996, 10, 2), Value = 14100.0 }
                },
                PointsOffset = 100,
                Success = false,
                ErrorMessage = "partial page",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 9,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.points[0].value" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.IsNotNull(copy.TimeSeries);
            Assert.AreEqual(response.TimeSeries.Id, copy.TimeSeries.Id);
            Assert.AreEqual("Potomac daily", copy.TimeSeries.Name);
            Assert.AreEqual(2, copy.TimeSeries.PointCount);
            Assert.IsNotNull(copy.Points);
            Assert.AreEqual(2, copy.Points.Count);
            Assert.AreEqual(15200.5, copy.Points[0].Value);
            Assert.AreEqual(100, copy.PointsOffset);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("partial page", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(9L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new TimeSeriesResourceResponse
            {
                TimeSeries = new TimeSeriesSummaryDto(),
                PointsOffset = 0
            });
            StringAssert.Contains(json, "\"timeSeries\"");
            StringAssert.Contains(json, "\"pointsOffset\"");
            StringAssert.Contains(json, "\"success\"");

            string defaultJson = TestJson.Serialize(new TimeSeriesResourceResponse());
            Assert.IsFalse(defaultJson.Contains("\"points\""), "Null points should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"timeSeries\""), "Null timeSeries should be omitted.");
        }
    }
}
