using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ExactObservationDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class ExactObservationDtoTests
    {
        /// <summary>
        /// Verifies a new observation defaults to no index, a zero value, and no plotting position.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new ExactObservationDto();
            Assert.IsNull(dto.Index);
            Assert.IsNull(dto.DateTime);
            Assert.AreEqual(0.0, dto.Value);
            Assert.IsFalse(dto.IsLowOutlier);
            Assert.IsNull(dto.PlottingPosition);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new ExactObservationDto
            {
                Index = 1996,
                DateTime = new DateTime(1996, 1, 19),
                Value = 15200.5,
                IsLowOutlier = true,
                PlottingPosition = 0.25
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual(1996, copy.Index);
            Assert.AreEqual(new DateTime(1996, 1, 19), copy.DateTime);
            Assert.AreEqual(15200.5, copy.Value);
            Assert.IsTrue(copy.IsLowOutlier);
            Assert.AreEqual(0.25, copy.PlottingPosition);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ExactObservationDto { Index = 1996, PlottingPosition = 0.5 });
            StringAssert.Contains(json, "\"index\"");
            StringAssert.Contains(json, "\"value\"");
            StringAssert.Contains(json, "\"isLowOutlier\"");
            StringAssert.Contains(json, "\"plottingPosition\"");

            string defaultJson = TestJson.Serialize(new ExactObservationDto());
            Assert.IsFalse(defaultJson.Contains("\"index\""), "Null index should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"dateTime\""), "Null dateTime should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"plottingPosition\""), "Null plottingPosition should be omitted.");
        }
    }
}
