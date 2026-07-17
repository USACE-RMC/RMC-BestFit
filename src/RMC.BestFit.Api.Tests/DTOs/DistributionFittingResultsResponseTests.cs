using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="DistributionFittingResultsResponse"/>: defaults, JSON
    /// round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class DistributionFittingResultsResponseTests
    {
        /// <summary>
        /// Verifies the defaults: empty fits list and inherited response-envelope defaults.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new DistributionFittingResultsResponse();
            Assert.AreEqual(Guid.Empty, response.AnalysisId);
            Assert.IsNull(response.Kind);
            Assert.AreEqual(0, response.Fits.Count);
            Assert.IsTrue(response.Success);
        }

        /// <summary>
        /// Verifies the fits survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new DistributionFittingResultsResponse
            {
                AnalysisId = Guid.NewGuid(),
                Kind = "distributionFitting",
                Fits = new List<DistributionFitDto>
                {
                    new() { Rank = 1, Distribution = "gumbel", FitSucceeded = true, Aic = 100d },
                    new() { Rank = 2, Distribution = "normal", FitSucceeded = true, Aic = 105d }
                }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(response.AnalysisId, copy.AnalysisId);
            Assert.AreEqual("distributionFitting", copy.Kind);
            Assert.AreEqual(2, copy.Fits.Count);
            Assert.AreEqual("gumbel", copy.Fits[0].Distribution);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new DistributionFittingResultsResponse { Kind = "distributionFitting" });
            StringAssert.Contains(json, "\"analysisId\"");
            StringAssert.Contains(json, "\"kind\"");
            StringAssert.Contains(json, "\"fits\"");
        }
    }
}
