using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="PotOptionsDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class PotOptionsDtoTests
    {
        /// <summary>
        /// Verifies a new echo defaults to all-null options.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new PotOptionsDto();
            Assert.IsNull(dto.Threshold);
            Assert.IsNull(dto.MinStepsBetweenPeaks);
            Assert.IsNull(dto.SmoothingFunction);
            Assert.IsNull(dto.Period);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new PotOptionsDto
            {
                Threshold = 10000.0,
                MinStepsBetweenPeaks = 7,
                SmoothingFunction = "none",
                Period = 3
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual(10000.0, copy.Threshold);
            Assert.AreEqual(7, copy.MinStepsBetweenPeaks);
            Assert.AreEqual("none", copy.SmoothingFunction);
            Assert.AreEqual(3, copy.Period);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and collapses an all-null echo to
        /// an empty object.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new PotOptionsDto { Threshold = 10000.0, MinStepsBetweenPeaks = 7 });
            StringAssert.Contains(json, "\"threshold\"");
            StringAssert.Contains(json, "\"minStepsBetweenPeaks\"");

            Assert.AreEqual("{}", TestJson.Serialize(new PotOptionsDto()),
                "All-null options should serialize to an empty object.");
        }
    }
}
