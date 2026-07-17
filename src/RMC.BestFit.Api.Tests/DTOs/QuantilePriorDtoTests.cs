using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="QuantilePriorDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class QuantilePriorDtoTests
    {
        /// <summary>
        /// Verifies the defaults: zero alpha and no distribution.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var prior = new QuantilePriorDto();
            Assert.AreEqual(0d, prior.Alpha);
            Assert.IsNull(prior.Distribution);
        }

        /// <summary>
        /// Verifies the alpha and distribution survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var prior = new QuantilePriorDto
            {
                Alpha = 0.01,
                Distribution = new DistributionSpecDto
                {
                    Type = UnivariateDistributionType.LogNormal,
                    Parameters = new List<double> { 4.7, 0.1 }
                }
            };

            var copy = TestJson.Roundtrip(prior);

            Assert.AreEqual(0.01, copy.Alpha);
            Assert.IsNotNull(copy.Distribution);
            Assert.AreEqual(UnivariateDistributionType.LogNormal, copy.Distribution!.Type);
            CollectionAssert.AreEqual(new List<double> { 4.7, 0.1 }, copy.Distribution.Parameters);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new QuantilePriorDto
            {
                Alpha = 0.002,
                Distribution = new DistributionSpecDto { Parameters = new List<double> { 1d, 2d } }
            });
            StringAssert.Contains(json, "\"alpha\"");
            StringAssert.Contains(json, "\"distribution\"");
        }
    }
}
