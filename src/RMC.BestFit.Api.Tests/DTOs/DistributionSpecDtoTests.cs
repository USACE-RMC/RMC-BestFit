using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="DistributionSpecDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names with string enum values.
    /// </summary>
    [TestClass]
    public class DistributionSpecDtoTests
    {
        /// <summary>
        /// Verifies the defaults: normal type with an empty parameter list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var spec = new DistributionSpecDto();
            Assert.AreEqual(UnivariateDistributionType.Normal, spec.Type);
            Assert.IsNotNull(spec.Parameters);
            Assert.AreEqual(0, spec.Parameters.Count);
        }

        /// <summary>
        /// Verifies the type and parameter values survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var spec = new DistributionSpecDto
            {
                Type = UnivariateDistributionType.Triangular,
                Parameters = new List<double> { 10d, 20d, 45d }
            };

            var copy = TestJson.Roundtrip(spec);

            Assert.AreEqual(UnivariateDistributionType.Triangular, copy.Type);
            CollectionAssert.AreEqual(new List<double> { 10d, 20d, 45d }, copy.Parameters);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and a camelCase enum string.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new DistributionSpecDto
            {
                Type = UnivariateDistributionType.LogNormal,
                Parameters = new List<double> { 3.5, 0.25 }
            });
            StringAssert.Contains(json, "\"type\"");
            StringAssert.Contains(json, "\"logNormal\"");
            StringAssert.Contains(json, "\"parameters\"");
        }
    }
}
