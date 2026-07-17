using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Serialization tests for <see cref="UsgsRatingCurveWorkflowResponse"/>.
    /// </summary>
    [TestClass]
    public class UsgsRatingCurveWorkflowResponseTests
    {
        /// <summary>
        /// Verifies the documented defaults.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new UsgsRatingCurveWorkflowResponse();
            Assert.IsTrue(response.Success);
            Assert.IsNull(response.FailedStep);
            Assert.IsNull(response.StageTimeSeriesId);
            Assert.IsNull(response.DischargeTimeSeriesId);
            Assert.IsNull(response.AnalysisId);
            Assert.IsNull(response.Results);
        }

        /// <summary>
        /// Verifies every property survives a wire round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new UsgsRatingCurveWorkflowResponse
            {
                Success = false,
                FailedStep = "downloadDischarge",
                StageTimeSeriesId = Guid.NewGuid(),
                DischargeTimeSeriesId = Guid.NewGuid(),
                AnalysisId = Guid.NewGuid(),
                Results = new RatingCurveResultsResponse { Kind = "ratingCurve", NumberOfSegments = 2 }
            };
            var copy = TestJson.Roundtrip(response);
            Assert.AreEqual("downloadDischarge", copy.FailedStep);
            Assert.AreEqual(response.StageTimeSeriesId, copy.StageTimeSeriesId);
            Assert.AreEqual(response.DischargeTimeSeriesId, copy.DischargeTimeSeriesId);
            Assert.AreEqual(2, copy.Results!.NumberOfSegments);
        }

        /// <summary>
        /// Verifies the camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new UsgsRatingCurveWorkflowResponse { StageTimeSeriesId = Guid.NewGuid() });
            StringAssert.Contains(json, "\"stageTimeSeriesId\"");
            StringAssert.Contains(json, "\"success\"");
        }
    }
}
