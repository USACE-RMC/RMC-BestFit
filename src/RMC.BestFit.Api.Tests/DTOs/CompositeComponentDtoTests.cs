using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CompositeComponentDto"/>: defaults, JSON round-trip, and
    /// camelCase wire names.
    /// </summary>
    [TestClass]
    public class CompositeComponentDtoTests
    {
        /// <summary>
        /// Verifies the defaults: empty id and no weight.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var component = new CompositeComponentDto();
            Assert.AreEqual(Guid.Empty, component.AnalysisId);
            Assert.IsNull(component.Weight);
        }

        /// <summary>
        /// Verifies the id and weight survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var component = new CompositeComponentDto { AnalysisId = Guid.NewGuid(), Weight = 0.35 };
            var copy = TestJson.Roundtrip(component);
            Assert.AreEqual(component.AnalysisId, copy.AnalysisId);
            Assert.AreEqual(0.35, copy.Weight);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits the null weight.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CompositeComponentDto { AnalysisId = Guid.NewGuid() });
            StringAssert.Contains(json, "\"analysisId\"");
            Assert.IsFalse(json.Contains("\"weight\""), "Null weight should be omitted.");
        }
    }
}
