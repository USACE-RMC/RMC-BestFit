using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ApiInfoDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class ApiInfoDtoTests
    {
        /// <summary>
        /// Verifies a new info body defaults to the service name with an empty feature list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new ApiInfoDto();
            Assert.AreEqual("RMC-BestFit API", dto.Name);
            Assert.IsNull(dto.Version);
            Assert.IsNull(dto.Description);
            Assert.IsNotNull(dto.Features);
            Assert.AreEqual(0, dto.Features.Count);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new ApiInfoDto
            {
                Name = "Test API",
                Version = "1.2.3",
                Description = "A test service.",
                Features = new List<string> { "timeseries", "inputdata" }
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual("Test API", copy.Name);
            Assert.AreEqual("1.2.3", copy.Version);
            Assert.AreEqual("A test service.", copy.Description);
            CollectionAssert.AreEqual(dto.Features, copy.Features);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ApiInfoDto { Description = "desc" });
            StringAssert.Contains(json, "\"name\"");
            StringAssert.Contains(json, "\"features\"");
            StringAssert.Contains(json, "\"description\"");

            string defaultJson = TestJson.Serialize(new ApiInfoDto());
            Assert.IsFalse(defaultJson.Contains("\"version\""), "Null version should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"description\""), "Null description should be omitted.");
        }
    }
}
