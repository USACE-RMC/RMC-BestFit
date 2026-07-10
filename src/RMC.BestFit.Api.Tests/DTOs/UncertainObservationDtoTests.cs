using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="UncertainObservationDto"/>: defaults, JSON round-trip, and
    /// camelCase wire names.
    /// </summary>
    [TestClass]
    public class UncertainObservationDtoTests
    {
        /// <summary>
        /// Verifies the defaults: no index/date, no distribution, and null response-only fields.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var observation = new UncertainObservationDto();
            Assert.IsNull(observation.Index);
            Assert.IsNull(observation.DateTime);
            Assert.IsNull(observation.Distribution);
            Assert.IsNull(observation.Value);
            Assert.IsNull(observation.PlottingPosition);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var observation = new UncertainObservationDto
            {
                Index = 1875,
                Distribution = new DistributionSpecDto
                {
                    Type = UnivariateDistributionType.Triangular,
                    Parameters = new List<double> { 40000d, 55000d, 90000d }
                },
                Value = 61666.7,
                PlottingPosition = 0.02
            };

            var copy = TestJson.Roundtrip(observation);

            Assert.AreEqual(1875, copy.Index);
            Assert.IsNotNull(copy.Distribution);
            Assert.AreEqual(UnivariateDistributionType.Triangular, copy.Distribution!.Type);
            CollectionAssert.AreEqual(new List<double> { 40000d, 55000d, 90000d }, copy.Distribution.Parameters);
            Assert.AreEqual(61666.7, copy.Value);
            Assert.AreEqual(0.02, copy.PlottingPosition);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new UncertainObservationDto
            {
                Index = 1902,
                Distribution = new DistributionSpecDto { Parameters = new List<double> { 100d, 15d } }
            });
            StringAssert.Contains(json, "\"index\"");
            StringAssert.Contains(json, "\"distribution\"");
            Assert.IsFalse(json.Contains("\"plottingPosition\""), "Null plotting position should be omitted.");
            Assert.IsFalse(json.Contains("\"dateTime\""), "Null dateTime should be omitted.");
        }
    }
}
