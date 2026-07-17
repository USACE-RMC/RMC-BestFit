using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ThresholdObservationDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class ThresholdObservationDtoTests
    {
        /// <summary>
        /// Verifies a new record defaults to a zero window and threshold with no computed counts.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new ThresholdObservationDto();
            Assert.AreEqual(0, dto.StartIndex);
            Assert.AreEqual(0, dto.EndIndex);
            Assert.AreEqual(0.0, dto.Value);
            Assert.AreEqual(0, dto.NumberAbove);
            Assert.IsNull(dto.NumberBelow);
            Assert.IsNull(dto.PlottingPosition);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new ThresholdObservationDto
            {
                StartIndex = 1861,
                EndIndex = 1929,
                Value = 85000.0,
                NumberAbove = 2,
                NumberBelow = 67,
                PlottingPosition = 0.05
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual(1861, copy.StartIndex);
            Assert.AreEqual(1929, copy.EndIndex);
            Assert.AreEqual(85000.0, copy.Value);
            Assert.AreEqual(2, copy.NumberAbove);
            Assert.AreEqual(67, copy.NumberBelow);
            Assert.AreEqual(0.05, copy.PlottingPosition);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null computed fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ThresholdObservationDto { NumberBelow = 10 });
            StringAssert.Contains(json, "\"startIndex\"");
            StringAssert.Contains(json, "\"endIndex\"");
            StringAssert.Contains(json, "\"numberAbove\"");
            StringAssert.Contains(json, "\"numberBelow\"");

            string defaultJson = TestJson.Serialize(new ThresholdObservationDto());
            Assert.IsFalse(defaultJson.Contains("\"numberBelow\""), "Null numberBelow should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"plottingPosition\""), "Null plottingPosition should be omitted.");
        }
    }
}
