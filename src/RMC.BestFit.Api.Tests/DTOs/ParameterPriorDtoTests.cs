using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ParameterPriorDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class ParameterPriorDtoTests
    {
        /// <summary>
        /// Verifies the defaults: empty name, no distribution, and a null (estimated) fixed flag.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var prior = new ParameterPriorDto();
            Assert.AreEqual(string.Empty, prior.ParameterName);
            Assert.IsNull(prior.Distribution);
            Assert.IsNull(prior.IsFixed);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var prior = new ParameterPriorDto
            {
                ParameterName = "Skew (of log)",
                Distribution = new DistributionSpecDto
                {
                    Type = UnivariateDistributionType.Normal,
                    Parameters = new List<double> { -0.2, 0.3 }
                },
                IsFixed = true
            };

            var copy = TestJson.Roundtrip(prior);

            Assert.AreEqual("Skew (of log)", copy.ParameterName);
            Assert.IsNotNull(copy.Distribution);
            Assert.AreEqual(UnivariateDistributionType.Normal, copy.Distribution!.Type);
            CollectionAssert.AreEqual(new List<double> { -0.2, 0.3 }, copy.Distribution.Parameters);
            Assert.IsTrue(copy.IsFixed);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits the null fixed flag.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ParameterPriorDto
            {
                ParameterName = "Mean (of log)",
                Distribution = new DistributionSpecDto { Parameters = new List<double> { 3d, 0.5 } }
            });
            StringAssert.Contains(json, "\"parameterName\"");
            StringAssert.Contains(json, "\"distribution\"");
            Assert.IsFalse(json.Contains("\"isFixed\""), "Null isFixed should be omitted.");
        }
    }
}
