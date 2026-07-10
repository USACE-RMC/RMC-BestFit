using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesPointDto"/>: defaults, JSON round-trip (including NaN
    /// missing-value markers), and camelCase wire names.
    /// </summary>
    [TestClass]
    public class TimeSeriesPointDtoTests
    {
        /// <summary>
        /// Verifies a new point defaults to the default date-time and a zero value.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new TimeSeriesPointDto();
            Assert.AreEqual(default, dto.DateTime);
            Assert.AreEqual(0.0, dto.Value);
        }

        /// <summary>
        /// Verifies both properties survive a JSON round-trip, including a NaN missing-value marker.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new TimeSeriesPointDto
            {
                DateTime = new DateTime(1996, 10, 1, 6, 0, 0),
                Value = 15200.5
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual(new DateTime(1996, 10, 1, 6, 0, 0), copy.DateTime);
            Assert.AreEqual(15200.5, copy.Value);

            var missing = TestJson.Roundtrip(new TimeSeriesPointDto { Value = double.NaN });
            Assert.IsTrue(double.IsNaN(missing.Value), "NaN missing-value marker should survive the round-trip.");
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and writes NaN as a named literal.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new TimeSeriesPointDto { Value = double.NaN });
            StringAssert.Contains(json, "\"dateTime\"");
            StringAssert.Contains(json, "\"value\"");
            StringAssert.Contains(json, "\"NaN\"");
        }
    }
}
