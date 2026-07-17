using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="AnalysisResourceResponse"/>: defaults, JSON round-trip including
    /// the inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class AnalysisResourceResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with no analysis summary.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new AnalysisResourceResponse();
            Assert.IsTrue(response.Success);
            Assert.IsNull(response.Analysis);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new AnalysisResourceResponse
            {
                Analysis = new AnalysisSummaryDto
                {
                    Id = Guid.NewGuid(),
                    Name = "LP3 fit",
                    Kind = "univariate",
                    State = "created",
                    IsValid = true
                },
                Success = false,
                ErrorMessage = "analysis stale",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 7,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.q" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.IsNotNull(copy.Analysis);
            Assert.AreEqual(response.Analysis.Id, copy.Analysis.Id);
            Assert.AreEqual("LP3 fit", copy.Analysis.Name);
            Assert.AreEqual("univariate", copy.Analysis.Kind);
            Assert.AreEqual("created", copy.Analysis.State);
            Assert.IsTrue(copy.Analysis.IsValid);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("analysis stale", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(7L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new AnalysisResourceResponse
            {
                Analysis = new AnalysisSummaryDto()
            });
            StringAssert.Contains(json, "\"analysis\"");
            StringAssert.Contains(json, "\"success\"");

            string defaultJson = TestJson.Serialize(new AnalysisResourceResponse());
            Assert.IsFalse(defaultJson.Contains("\"analysis\""), "Null analysis should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
        }
    }
}
