using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="HealthCheckDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class HealthCheckDtoTests
    {
        /// <summary>
        /// Verifies a new health check defaults to a "healthy" status with no version.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new HealthCheckDto();
            Assert.AreEqual("healthy", dto.Status);
            Assert.AreEqual(default, dto.Timestamp);
            Assert.IsNull(dto.Version);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new HealthCheckDto
            {
                Status = "degraded",
                Timestamp = new DateTime(2026, 7, 1, 12, 30, 0, DateTimeKind.Utc),
                Version = "2.0.0.0"
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual("degraded", copy.Status);
            Assert.AreEqual(dto.Timestamp, copy.Timestamp);
            Assert.AreEqual("2.0.0.0", copy.Version);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits the null version.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new HealthCheckDto { Version = "2.0.0.0" });
            StringAssert.Contains(json, "\"status\"");
            StringAssert.Contains(json, "\"timestamp\"");
            StringAssert.Contains(json, "\"version\"");

            string defaultJson = TestJson.Serialize(new HealthCheckDto());
            Assert.IsFalse(defaultJson.Contains("\"version\""), "Null version should be omitted.");
        }
    }
}
