using Numerics.Distributions.Copulas;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateBivariateAnalysisRequest"/>: defaults, JSON round-trip,
    /// and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreateBivariateAnalysisRequestTests
    {
        /// <summary>
        /// Verifies the defaults: normal copula, model-default estimation method, empty grid.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateBivariateAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.MarginalXAnalysisId);
            Assert.AreEqual(Guid.Empty, request.MarginalYAnalysisId);
            Assert.AreEqual(CopulaType.Normal, request.CopulaType);
            Assert.IsNull(request.EstimationMethod);
            Assert.AreEqual(0, request.XyOrdinates.Count);
            Assert.IsNull(request.BayesianOptions);
            Assert.IsNull(request.ParameterPriors);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = Guid.NewGuid(),
                MarginalYAnalysisId = Guid.NewGuid(),
                CopulaType = CopulaType.Gumbel,
                EstimationMethod = CopulaEstimationMethod.PseudoLikelihood,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 100d, Y = 200d } },
                Name = "bivariate"
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.MarginalXAnalysisId, copy.MarginalXAnalysisId);
            Assert.AreEqual(request.MarginalYAnalysisId, copy.MarginalYAnalysisId);
            Assert.AreEqual(CopulaType.Gumbel, copy.CopulaType);
            Assert.AreEqual(CopulaEstimationMethod.PseudoLikelihood, copy.EstimationMethod);
            Assert.AreEqual(1, copy.XyOrdinates.Count);
            Assert.AreEqual("bivariate", copy.Name);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and enum strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateBivariateAnalysisRequest
            {
                CopulaType = CopulaType.AliMikhailHaq,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 1d, Y = 2d } }
            });
            StringAssert.Contains(json, "\"marginalXAnalysisId\"");
            StringAssert.Contains(json, "\"copulaType\"");
            StringAssert.Contains(json, "\"aliMikhailHaq\"");
            StringAssert.Contains(json, "\"xyOrdinates\"");
        }
    }
}
