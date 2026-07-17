using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateBulletin17CAnalysisRequest"/>: defaults, JSON round-trip,
    /// and camelCase wire names including string enum values.
    /// </summary>
    [TestClass]
    public class CreateBulletin17CAnalysisRequestTests
    {
        /// <summary>
        /// Verifies a new request defaults to the Log-Pearson Type III distribution with a null
        /// uncertainty method (model default) and no ordinates, name, or description.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateBulletin17CAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.InputDataId);
            Assert.AreEqual(UnivariateDistributionType.LogPearsonTypeIII, request.Distribution);
            Assert.IsNull(request.UncertaintyMethod);
            Assert.IsNull(request.ProbabilityOrdinates);
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
        }

        /// <summary>
        /// Verifies every property, including the two enum-typed options, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateBulletin17CAnalysisRequest
            {
                InputDataId = Guid.NewGuid(),
                Distribution = UnivariateDistributionType.LogNormal,
                UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap,
                ProbabilityOrdinates = new List<double> { 0.5, 0.01, 0.002 },
                Name = "B17C peaks",
                Description = "Bulletin 17C fit of the annual peak record."
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.InputDataId, copy.InputDataId);
            Assert.AreEqual(UnivariateDistributionType.LogNormal, copy.Distribution);
            Assert.AreEqual(UncertaintyMethod.BiasCorrectedBootstrap, copy.UncertaintyMethod);
            CollectionAssert.AreEqual(request.ProbabilityOrdinates, copy.ProbabilityOrdinates);
            Assert.AreEqual("B17C peaks", copy.Name);
            Assert.AreEqual("Bulletin 17C fit of the annual peak record.", copy.Description);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names, camelCase enum value strings,
        /// and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateBulletin17CAnalysisRequest
            {
                UncertaintyMethod = UncertaintyMethod.Bootstrap
            });
            StringAssert.Contains(json, "\"inputDataId\"");
            StringAssert.Contains(json, "\"distribution\"");
            StringAssert.Contains(json, "\"logPearsonTypeIII\"");
            StringAssert.Contains(json, "\"uncertaintyMethod\"");
            StringAssert.Contains(json, "\"bootstrap\"");

            string defaultJson = TestJson.Serialize(new CreateBulletin17CAnalysisRequest());
            Assert.IsFalse(defaultJson.Contains("\"uncertaintyMethod\""), "Null uncertainty method should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"name\""), "Null name should be omitted.");
        }
    }
}
