using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="BlockOptionsDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class BlockOptionsDtoTests
    {
        /// <summary>
        /// Verifies a new echo defaults to all-null options.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new BlockOptionsDto();
            Assert.IsNull(dto.TimeBlock);
            Assert.IsNull(dto.BlockFunction);
            Assert.IsNull(dto.SmoothingFunction);
            Assert.IsNull(dto.StartMonth);
            Assert.IsNull(dto.EndMonth);
            Assert.IsNull(dto.Period);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new BlockOptionsDto
            {
                TimeBlock = "waterYear",
                BlockFunction = "maximum",
                SmoothingFunction = "movingAverage",
                StartMonth = 10,
                EndMonth = 9,
                Period = 7
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual("waterYear", copy.TimeBlock);
            Assert.AreEqual("maximum", copy.BlockFunction);
            Assert.AreEqual("movingAverage", copy.SmoothingFunction);
            Assert.AreEqual(10, copy.StartMonth);
            Assert.AreEqual(9, copy.EndMonth);
            Assert.AreEqual(7, copy.Period);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and collapses an all-null echo to
        /// an empty object.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new BlockOptionsDto { TimeBlock = "waterYear", BlockFunction = "maximum" });
            StringAssert.Contains(json, "\"timeBlock\"");
            StringAssert.Contains(json, "\"blockFunction\"");
            StringAssert.Contains(json, "\"waterYear\"");

            Assert.AreEqual("{}", TestJson.Serialize(new BlockOptionsDto()),
                "All-null options should serialize to an empty object.");
        }
    }
}
