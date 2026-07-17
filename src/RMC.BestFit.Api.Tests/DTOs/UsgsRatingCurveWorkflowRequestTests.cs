using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Serialization tests for <see cref="UsgsRatingCurveWorkflowRequest"/>.
    /// </summary>
    [TestClass]
    public class UsgsRatingCurveWorkflowRequestTests
    {
        /// <summary>
        /// Verifies the documented defaults.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new UsgsRatingCurveWorkflowRequest();
            Assert.AreEqual(string.Empty, request.SiteNumber);
            Assert.AreEqual(1, request.NumberOfSegments);
            Assert.IsNull(request.MinStage);
            Assert.IsNull(request.MaxStage);
            Assert.IsNull(request.StageBins);
        }

        /// <summary>
        /// Verifies every property survives a wire round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new UsgsRatingCurveWorkflowRequest
            {
                SiteNumber = "01646500",
                NumberOfSegments = 2,
                MinStage = 1.5,
                MaxStage = 12d,
                StageBins = 100,
                BayesianOptions = new BayesianOptionsDto { PrngSeed = 3 },
                Name = "n"
            };
            var copy = TestJson.Roundtrip(request);
            Assert.AreEqual(2, copy.NumberOfSegments);
            Assert.AreEqual(1.5, copy.MinStage);
            Assert.AreEqual(12d, copy.MaxStage);
            Assert.AreEqual(100, copy.StageBins);
            Assert.AreEqual(3, copy.BayesianOptions!.PrngSeed);
        }

        /// <summary>
        /// Verifies the camelCase wire names and null omission.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new UsgsRatingCurveWorkflowRequest { SiteNumber = "01646500" });
            StringAssert.Contains(json, "\"siteNumber\"");
            StringAssert.Contains(json, "\"numberOfSegments\"");
            Assert.IsFalse(json.Contains("\"minStage\""));
        }
    }
}
