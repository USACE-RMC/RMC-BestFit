using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateManualTimeSeriesRequest"/>: defaults, JSON round-trip, and
    /// camelCase wire names including string enum values.
    /// </summary>
    [TestClass]
    public class CreateManualTimeSeriesRequestTests
    {
        /// <summary>
        /// Verifies a new request defaults to a daily interval with an empty point list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateManualTimeSeriesRequest();
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
            Assert.AreEqual(TimeInterval.OneDay, request.TimeInterval);
            Assert.IsNotNull(request.Points);
            Assert.AreEqual(0, request.Points.Count);
        }

        /// <summary>
        /// Verifies every property, including the enum-typed interval and points, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateManualTimeSeriesRequest
            {
                Name = "Gage measurements",
                Description = "Discrete field measurements.",
                TimeInterval = TimeInterval.Irregular,
                Points = new List<TimeSeriesPointDto>
                {
                    new TimeSeriesPointDto { DateTime = new DateTime(1996, 10, 1), Value = 15200.5 },
                    new TimeSeriesPointDto { DateTime = new DateTime(1997, 3, 15), Value = 8300.25 }
                }
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual("Gage measurements", copy.Name);
            Assert.AreEqual("Discrete field measurements.", copy.Description);
            Assert.AreEqual(TimeInterval.Irregular, copy.TimeInterval);
            Assert.AreEqual(2, copy.Points.Count);
            Assert.AreEqual(new DateTime(1996, 10, 1), copy.Points[0].DateTime);
            Assert.AreEqual(15200.5, copy.Points[0].Value);
            Assert.AreEqual(8300.25, copy.Points[1].Value);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and camelCase enum value strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateManualTimeSeriesRequest
            {
                TimeInterval = TimeInterval.Irregular
            });
            StringAssert.Contains(json, "\"timeInterval\"");
            StringAssert.Contains(json, "\"points\"");
            StringAssert.Contains(json, "\"irregular\"");
            Assert.IsFalse(json.Contains("\"name\""), "Null name should be omitted.");
            Assert.IsFalse(json.Contains("\"description\""), "Null description should be omitted.");
        }
    }
}
