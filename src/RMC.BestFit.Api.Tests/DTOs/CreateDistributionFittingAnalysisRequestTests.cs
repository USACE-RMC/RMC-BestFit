using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateDistributionFittingAnalysisRequest"/>: defaults, JSON
    /// round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreateDistributionFittingAnalysisRequestTests
    {
        /// <summary>
        /// Verifies the defaults: no candidate subset (all 15 fitted) and null metadata.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateDistributionFittingAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.InputDataId);
            Assert.IsNull(request.Distributions);
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateDistributionFittingAnalysisRequest
            {
                InputDataId = Guid.NewGuid(),
                Distributions = new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.LogPearsonTypeIII,
                    UnivariateDistributionType.Gumbel
                },
                Name = "screen"
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.InputDataId, copy.InputDataId);
            CollectionAssert.AreEqual(request.Distributions, copy.Distributions);
            Assert.AreEqual("screen", copy.Name);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and enum strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateDistributionFittingAnalysisRequest
            {
                Distributions = new List<UnivariateDistributionType> { UnivariateDistributionType.Weibull }
            });
            StringAssert.Contains(json, "\"inputDataId\"");
            StringAssert.Contains(json, "\"distributions\"");
            StringAssert.Contains(json, "\"weibull\"");
        }
    }
}
