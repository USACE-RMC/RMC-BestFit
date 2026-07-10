using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ResourceSummaryDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class ResourceSummaryDtoTests
    {
        /// <summary>
        /// Verifies a new summary defaults to an empty id with all descriptive fields null.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new ResourceSummaryDto();
            Assert.AreEqual(Guid.Empty, dto.Id);
            Assert.IsNull(dto.ResourceType);
            Assert.IsNull(dto.Name);
            Assert.AreEqual(default, dto.CreatedUtc);
            Assert.IsNull(dto.Detail);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new ResourceSummaryDto
            {
                Id = Guid.NewGuid(),
                ResourceType = "analysis",
                Name = "LP3 fit",
                CreatedUtc = new DateTime(2026, 7, 1, 8, 15, 0, DateTimeKind.Utc),
                Detail = "univariate, completed"
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual(dto.Id, copy.Id);
            Assert.AreEqual("analysis", copy.ResourceType);
            Assert.AreEqual("LP3 fit", copy.Name);
            Assert.AreEqual(dto.CreatedUtc, copy.CreatedUtc);
            Assert.AreEqual("univariate, completed", copy.Detail);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ResourceSummaryDto { Detail = "5 points" });
            StringAssert.Contains(json, "\"id\"");
            StringAssert.Contains(json, "\"createdUtc\"");
            StringAssert.Contains(json, "\"detail\"");

            string defaultJson = TestJson.Serialize(new ResourceSummaryDto());
            Assert.IsFalse(defaultJson.Contains("\"resourceType\""), "Null resourceType should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"detail\""), "Null detail should be omitted.");
        }
    }
}
