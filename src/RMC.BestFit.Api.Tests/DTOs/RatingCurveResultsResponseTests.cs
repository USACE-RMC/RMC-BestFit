using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="RatingCurveResultsResponse"/>: defaults, JSON round-trip
    /// including the inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class RatingCurveResultsResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with no curve, zero counts, and empty
        /// parameter lists.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new RatingCurveResultsResponse();
            Assert.IsTrue(response.Success);
            Assert.AreEqual(Guid.Empty, response.AnalysisId);
            Assert.IsNull(response.Kind);
            Assert.IsNull(response.RatingCurve);
            Assert.AreEqual(0, response.NumberOfSegments);
            Assert.AreEqual(0, response.AlignedObservationCount);
            Assert.IsNotNull(response.Parameters);
            Assert.AreEqual(0, response.Parameters.Count);
            Assert.IsNotNull(response.ParameterSummaries);
            Assert.AreEqual(0, response.ParameterSummaries.Count);
            Assert.IsNull(response.InformationCriteria);
            Assert.IsNull(response.Diagnostics);
        }

        /// <summary>
        /// Verifies every property, including all nested result sections and the inherited
        /// response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new RatingCurveResultsResponse
            {
                AnalysisId = Guid.NewGuid(),
                Kind = "ratingCurve",
                RatingCurve = new RatingCurveDto
                {
                    Stages = new List<double> { 2.0, 10.0 },
                    ModeCurve = new List<double> { 150.0, 9800.0 },
                    CredibleIntervalWidth = 0.9
                },
                NumberOfSegments = 2,
                AlignedObservationCount = 48,
                Parameters = new List<ParameterValueDto>
                {
                    new ParameterValueDto { Name = "Xi", Value = 1.2 }
                },
                ParameterSummaries = new List<ParameterSummaryDto>
                {
                    new ParameterSummaryDto { Name = "Xi", Mean = 1.19, Rhat = 1.004 }
                },
                InformationCriteria = new InformationCriteriaDto { Dic = 210.4 },
                Diagnostics = new DiagnosticsDto { Sampler = "demCzs", NumberOfChains = 4 },
                Success = false,
                ErrorMessage = "results stale",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 13,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.ratingCurve.ciUpper[0]" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(response.AnalysisId, copy.AnalysisId);
            Assert.AreEqual("ratingCurve", copy.Kind);
            Assert.IsNotNull(copy.RatingCurve);
            CollectionAssert.AreEqual(response.RatingCurve.Stages, copy.RatingCurve.Stages);
            CollectionAssert.AreEqual(response.RatingCurve.ModeCurve, copy.RatingCurve.ModeCurve);
            Assert.AreEqual(0.9, copy.RatingCurve.CredibleIntervalWidth);
            Assert.AreEqual(2, copy.NumberOfSegments);
            Assert.AreEqual(48, copy.AlignedObservationCount);
            Assert.AreEqual(1, copy.Parameters.Count);
            Assert.AreEqual("Xi", copy.Parameters[0].Name);
            Assert.AreEqual(1.2, copy.Parameters[0].Value);
            Assert.AreEqual(1, copy.ParameterSummaries.Count);
            Assert.AreEqual(1.19, copy.ParameterSummaries[0].Mean);
            Assert.AreEqual(1.004, copy.ParameterSummaries[0].Rhat);
            Assert.IsNotNull(copy.InformationCriteria);
            Assert.AreEqual(210.4, copy.InformationCriteria.Dic);
            Assert.IsNotNull(copy.Diagnostics);
            Assert.AreEqual("demCzs", copy.Diagnostics.Sampler);
            Assert.AreEqual(4, copy.Diagnostics.NumberOfChains);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("results stale", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(13L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null result sections.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new RatingCurveResultsResponse
            {
                Kind = "ratingCurve",
                RatingCurve = new RatingCurveDto()
            });
            StringAssert.Contains(json, "\"analysisId\"");
            StringAssert.Contains(json, "\"ratingCurve\"");
            StringAssert.Contains(json, "\"alignedObservationCount\"");
            StringAssert.Contains(json, "\"success\"");

            string defaultJson = TestJson.Serialize(new RatingCurveResultsResponse());
            Assert.IsFalse(defaultJson.Contains("\"ratingCurve\""), "Null ratingCurve should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"diagnostics\""), "Null diagnostics should be omitted.");
        }
    }
}
