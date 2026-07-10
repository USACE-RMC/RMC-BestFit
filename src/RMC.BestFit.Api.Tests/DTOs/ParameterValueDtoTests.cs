using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ParameterValueDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class ParameterValueDtoTests
    {
        /// <summary>
        /// Verifies a new parameter value defaults to no name and a zero value.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var parameter = new ParameterValueDto();
            Assert.IsNull(parameter.Name);
            Assert.AreEqual(0.0, parameter.Value);
        }

        /// <summary>
        /// Verifies the name and value survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var parameter = new ParameterValueDto
            {
                Name = "Sigma",
                Value = 0.245
            };

            var copy = TestJson.Roundtrip(parameter);

            Assert.AreEqual("Sigma", copy.Name);
            Assert.AreEqual(0.245, copy.Value);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits the null name.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ParameterValueDto { Name = "Mu", Value = 3.5 });
            StringAssert.Contains(json, "\"name\"");
            StringAssert.Contains(json, "\"value\"");

            string defaultJson = TestJson.Serialize(new ParameterValueDto());
            Assert.IsFalse(defaultJson.Contains("\"name\""), "Null name should be omitted.");
        }
    }
}
