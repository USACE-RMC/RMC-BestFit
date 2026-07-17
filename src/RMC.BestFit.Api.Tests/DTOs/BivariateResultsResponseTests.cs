using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="BivariateResultsResponse"/> and its supporting
    /// <see cref="MarginalInfoDto"/> and <see cref="JointExceedanceCurveDto"/>: defaults, JSON
    /// round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class BivariateResultsResponseTests
    {
        /// <summary>
        /// Verifies the defaults across the response and its supporting DTOs.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new BivariateResultsResponse();
            Assert.AreEqual(Guid.Empty, response.AnalysisId);
            Assert.IsNull(response.Kind);
            Assert.IsNull(response.CopulaType);
            Assert.IsNull(response.MarginalX);
            Assert.IsNull(response.JointExceedance);
            Assert.AreEqual(0, response.Parameters.Count);

            var marginal = new MarginalInfoDto();
            Assert.IsNull(marginal.AnalysisId);
            Assert.IsNull(marginal.DistributionType);

            var curve = new JointExceedanceCurveDto();
            Assert.AreEqual(0, curve.X.Count);
            Assert.IsNull(curve.ModeProbabilities);
            Assert.AreEqual(0d, curve.CredibleIntervalWidth);
        }

        /// <summary>
        /// Verifies the nested structures survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new BivariateResultsResponse
            {
                AnalysisId = Guid.NewGuid(),
                Kind = "bivariate",
                CopulaType = "gumbel",
                EstimationMethod = "inferenceFromMargins",
                MarginalX = new MarginalInfoDto { AnalysisId = Guid.NewGuid(), Name = "x", Kind = "univariate", DistributionType = "gumbel" },
                JointExceedance = new JointExceedanceCurveDto
                {
                    X = new List<double> { 100d },
                    Y = new List<double> { 200d },
                    ModeProbabilities = new List<double> { 0.01 },
                    CiLower = new List<double> { 0.005 },
                    CiUpper = new List<double> { 0.02 },
                    CredibleIntervalWidth = 0.9
                }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual("gumbel", copy.CopulaType);
            Assert.AreEqual("x", copy.MarginalX!.Name);
            Assert.AreEqual(1, copy.JointExceedance!.X.Count);
            Assert.AreEqual(0.01, copy.JointExceedance.ModeProbabilities![0]);
            Assert.AreEqual(0.9, copy.JointExceedance.CredibleIntervalWidth);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new BivariateResultsResponse
            {
                Kind = "bivariate",
                CopulaType = "frank",
                MarginalY = new MarginalInfoDto { Kind = "mixture" },
                JointExceedance = new JointExceedanceCurveDto { X = new List<double> { 1d } }
            });
            StringAssert.Contains(json, "\"copulaType\"");
            StringAssert.Contains(json, "\"marginalY\"");
            StringAssert.Contains(json, "\"jointExceedance\"");
            StringAssert.Contains(json, "\"credibleIntervalWidth\"");
        }
    }
}
