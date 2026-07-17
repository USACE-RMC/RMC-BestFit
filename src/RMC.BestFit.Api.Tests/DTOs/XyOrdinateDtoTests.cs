using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="XyOrdinateDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class XyOrdinateDtoTests
    {
        /// <summary>
        /// Verifies the defaults are zero.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var point = new XyOrdinateDto();
            Assert.AreEqual(0d, point.X);
            Assert.AreEqual(0d, point.Y);
        }

        /// <summary>
        /// Verifies both coordinates survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var copy = TestJson.Roundtrip(new XyOrdinateDto { X = 350.5, Y = 410.25 });
            Assert.AreEqual(350.5, copy.X);
            Assert.AreEqual(410.25, copy.Y);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new XyOrdinateDto { X = 1d, Y = 2d });
            StringAssert.Contains(json, "\"x\"");
            StringAssert.Contains(json, "\"y\"");
        }
    }
}
