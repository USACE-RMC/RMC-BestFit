using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateMixtureAnalysisRequest"/>: defaults, JSON round-trip, and
    /// camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreateMixtureAnalysisRequestTests
    {
        /// <summary>
        /// Verifies the defaults: empty components, zero inflation off, and null optional fields.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateMixtureAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.InputDataId);
            Assert.AreEqual(0, request.Distributions.Count);
            Assert.IsFalse(request.IsZeroInflated);
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
            var request = new CreateMixtureAnalysisRequest
            {
                InputDataId = Guid.NewGuid(),
                Distributions = new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Gumbel,
                    UnivariateDistributionType.LogNormal
                },
                IsZeroInflated = true,
                ProbabilityOrdinates = new List<double> { 0.5, 0.01 },
                UseSingleQuantile = true,
                Name = "mixture"
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.InputDataId, copy.InputDataId);
            CollectionAssert.AreEqual(request.Distributions, copy.Distributions);
            Assert.IsTrue(copy.IsZeroInflated);
            CollectionAssert.AreEqual(request.ProbabilityOrdinates, copy.ProbabilityOrdinates);
            Assert.IsTrue(copy.UseSingleQuantile);
            Assert.AreEqual("mixture", copy.Name);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and enum strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateMixtureAnalysisRequest
            {
                Distributions = new List<UnivariateDistributionType> { UnivariateDistributionType.Gumbel }
            });
            StringAssert.Contains(json, "\"inputDataId\"");
            StringAssert.Contains(json, "\"distributions\"");
            StringAssert.Contains(json, "\"gumbel\"");
            StringAssert.Contains(json, "\"isZeroInflated\"");
        }
    }
}
