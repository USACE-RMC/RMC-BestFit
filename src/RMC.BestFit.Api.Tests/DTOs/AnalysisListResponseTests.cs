using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="AnalysisListResponse"/>: defaults, JSON round-trip including the
    /// inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class AnalysisListResponseTests
    {
        /// <summary>
        /// Verifies a new listing defaults to success with a zero count and an empty summary list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new AnalysisListResponse();
            Assert.IsTrue(response.Success);
            Assert.AreEqual(0, response.Count);
            Assert.IsNotNull(response.Analyses);
            Assert.AreEqual(0, response.Analyses.Count);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new AnalysisListResponse
            {
                Count = 1,
                Analyses = new List<AnalysisSummaryDto>
                {
                    new AnalysisSummaryDto { Id = Guid.NewGuid(), Name = "B17C fit", Kind = "bulletin17C" }
                },
                Success = false,
                ErrorMessage = "listing incomplete",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 4,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.q" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(1, copy.Count);
            Assert.AreEqual(1, copy.Analyses.Count);
            Assert.AreEqual(response.Analyses[0].Id, copy.Analyses[0].Id);
            Assert.AreEqual("B17C fit", copy.Analyses[0].Name);
            Assert.AreEqual("bulletin17C", copy.Analyses[0].Kind);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("listing incomplete", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(4L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new AnalysisListResponse());
            StringAssert.Contains(json, "\"count\"");
            StringAssert.Contains(json, "\"analyses\"");
            StringAssert.Contains(json, "\"success\"");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
        }
    }
}
