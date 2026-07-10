using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="IntervalObservationDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class IntervalObservationDtoTests
    {
        /// <summary>
        /// Verifies a new observation defaults to zero bounds with no representative value.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new IntervalObservationDto();
            Assert.AreEqual(0, dto.Index);
            Assert.AreEqual(0.0, dto.LowerBound);
            Assert.AreEqual(0.0, dto.UpperBound);
            Assert.IsNull(dto.Value);
            Assert.IsNull(dto.PlottingPosition);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new IntervalObservationDto
            {
                Index = 1889,
                LowerBound = 90000.0,
                UpperBound = 110000.0,
                Value = 100000.0,
                PlottingPosition = 0.01
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual(1889, copy.Index);
            Assert.AreEqual(90000.0, copy.LowerBound);
            Assert.AreEqual(110000.0, copy.UpperBound);
            Assert.AreEqual(100000.0, copy.Value);
            Assert.AreEqual(0.01, copy.PlottingPosition);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new IntervalObservationDto { Value = 100.0 });
            StringAssert.Contains(json, "\"index\"");
            StringAssert.Contains(json, "\"lowerBound\"");
            StringAssert.Contains(json, "\"upperBound\"");
            StringAssert.Contains(json, "\"value\"");

            string defaultJson = TestJson.Serialize(new IntervalObservationDto());
            Assert.IsFalse(defaultJson.Contains("\"value\""), "Null value should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"plottingPosition\""), "Null plottingPosition should be omitted.");
        }
    }
}
