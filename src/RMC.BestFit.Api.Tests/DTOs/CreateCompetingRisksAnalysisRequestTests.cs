using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateCompetingRisksAnalysisRequest"/>: defaults, JSON
    /// round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreateCompetingRisksAnalysisRequestTests
    {
        /// <summary>
        /// Verifies the defaults: empty components and null optional fields.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateCompetingRisksAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.InputDataId);
            Assert.AreEqual(0, request.Distributions.Count);
            Assert.IsNull(request.ProbabilityOrdinates);
            Assert.IsNull(request.BayesianOptions);
            Assert.IsNull(request.ParameterPriors);
            Assert.IsNull(request.QuantilePriors);
            Assert.IsNull(request.UseSingleQuantile);
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateCompetingRisksAnalysisRequest
            {
                InputDataId = Guid.NewGuid(),
                Distributions = new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Gumbel,
                    UnivariateDistributionType.GeneralizedExtremeValue
                },
                ProbabilityOrdinates = new List<double> { 0.5, 0.01 },
                UseSingleQuantile = false,
                Name = "competing risks"
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.InputDataId, copy.InputDataId);
            CollectionAssert.AreEqual(request.Distributions, copy.Distributions);
            CollectionAssert.AreEqual(request.ProbabilityOrdinates, copy.ProbabilityOrdinates);
            Assert.IsFalse(copy.UseSingleQuantile!.Value);
            Assert.AreEqual("competing risks", copy.Name);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and enum strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateCompetingRisksAnalysisRequest
            {
                Distributions = new List<UnivariateDistributionType> { UnivariateDistributionType.GeneralizedExtremeValue }
            });
            StringAssert.Contains(json, "\"inputDataId\"");
            StringAssert.Contains(json, "\"distributions\"");
            StringAssert.Contains(json, "\"generalizedExtremeValue\"");
        }
    }
}
