using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Serialization tests for <see cref="UsgsFrequencyWorkflowResponse"/>.
    /// </summary>
    [TestClass]
    public class UsgsFrequencyWorkflowResponseTests
    {
        /// <summary>
        /// Verifies the documented defaults (all ids null, success true).
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new UsgsFrequencyWorkflowResponse();
            Assert.IsTrue(response.Success);
            Assert.IsNull(response.FailedStep);
            Assert.IsNull(response.TimeSeriesId);
            Assert.IsNull(response.InputDataId);
            Assert.IsNull(response.AnalysisId);
            Assert.IsNull(response.Results);
        }

        /// <summary>
        /// Verifies every property (including inherited failure fields) survives a wire round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new UsgsFrequencyWorkflowResponse
            {
                Success = false,
                ErrorMessage = "step failed",
                ValidationErrors = new List<string> { "e" },
                FailedStep = "runAnalysis",
                TimeSeriesId = Guid.NewGuid(),
                InputDataId = Guid.NewGuid(),
                AnalysisId = Guid.NewGuid(),
                Results = new FrequencyResultsResponse { AnalysisId = Guid.NewGuid(), Kind = "univariate" }
            };
            var copy = TestJson.Roundtrip(response);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("runAnalysis", copy.FailedStep);
            Assert.AreEqual(response.TimeSeriesId, copy.TimeSeriesId);
            Assert.AreEqual(response.InputDataId, copy.InputDataId);
            Assert.AreEqual(response.AnalysisId, copy.AnalysisId);
            Assert.AreEqual("univariate", copy.Results!.Kind);
        }

        /// <summary>
        /// Verifies the camelCase wire names and null omission.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new UsgsFrequencyWorkflowResponse
            {
                FailedStep = "createInputData",
                InputDataId = Guid.NewGuid()
            });
            StringAssert.Contains(json, "\"failedStep\"");
            StringAssert.Contains(json, "\"inputDataId\"");
            Assert.IsFalse(json.Contains("\"results\""));
        }
    }
}
