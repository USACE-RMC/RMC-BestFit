using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="Bulletin17CInfoDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class Bulletin17CInfoDtoTests
    {
        /// <summary>
        /// Verifies a new info object defaults to no uncertainty method and no timings.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var info = new Bulletin17CInfoDto();
            Assert.IsNull(info.UncertaintyMethod);
            Assert.IsNull(info.GmmElapsedMs);
            Assert.IsNull(info.UncertaintyElapsedMs);
        }

        /// <summary>
        /// Verifies the uncertainty method and both timings survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var info = new Bulletin17CInfoDto
            {
                UncertaintyMethod = "linkedMultivariateNormal",
                GmmElapsedMs = 84,
                UncertaintyElapsedMs = 1312
            };

            var copy = TestJson.Roundtrip(info);

            Assert.AreEqual("linkedMultivariateNormal", copy.UncertaintyMethod);
            Assert.AreEqual(84L, copy.GmmElapsedMs);
            Assert.AreEqual(1312L, copy.UncertaintyElapsedMs);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new Bulletin17CInfoDto
            {
                UncertaintyMethod = "bootstrap",
                GmmElapsedMs = 12,
                UncertaintyElapsedMs = 30
            });
            StringAssert.Contains(json, "\"uncertaintyMethod\"");
            StringAssert.Contains(json, "\"gmmElapsedMs\"");
            StringAssert.Contains(json, "\"uncertaintyElapsedMs\"");

            string defaultJson = TestJson.Serialize(new Bulletin17CInfoDto());
            Assert.AreEqual("{}", defaultJson, "An all-default info object should serialize empty.");
        }
    }
}
