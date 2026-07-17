using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="DistributionInfoDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class DistributionInfoDtoTests
    {
        /// <summary>
        /// Verifies a new entry defaults to null names with an empty supported-by list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new DistributionInfoDto();
            Assert.IsNull(dto.Name);
            Assert.IsNull(dto.DisplayName);
            Assert.IsNotNull(dto.SupportedBy);
            Assert.AreEqual(0, dto.SupportedBy.Count);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new DistributionInfoDto
            {
                Name = "logPearsonTypeIII",
                DisplayName = "Log-Pearson Type III",
                SupportedBy = new List<string> { "univariate", "bulletin17c" }
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual("logPearsonTypeIII", copy.Name);
            Assert.AreEqual("Log-Pearson Type III", copy.DisplayName);
            CollectionAssert.AreEqual(dto.SupportedBy, copy.SupportedBy);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new DistributionInfoDto
            {
                Name = "gumbel",
                DisplayName = "Gumbel"
            });
            StringAssert.Contains(json, "\"name\"");
            StringAssert.Contains(json, "\"displayName\"");
            StringAssert.Contains(json, "\"supportedBy\"");

            string defaultJson = TestJson.Serialize(new DistributionInfoDto());
            Assert.IsFalse(defaultJson.Contains("\"displayName\""), "Null displayName should be omitted.");
        }
    }
}
