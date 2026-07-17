using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="FrequencyResultsResponse"/>: defaults, JSON round-trip including
    /// the inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class FrequencyResultsResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with no results sections and an empty
        /// parameter-summary list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new FrequencyResultsResponse();
            Assert.IsTrue(response.Success);
            Assert.AreEqual(Guid.Empty, response.AnalysisId);
            Assert.IsNull(response.Kind);
            Assert.IsNull(response.FittedDistribution);
            Assert.IsNull(response.FrequencyCurve);
            Assert.IsNotNull(response.ParameterSummaries);
            Assert.AreEqual(0, response.ParameterSummaries.Count);
            Assert.IsNull(response.InformationCriteria);
            Assert.IsNull(response.Diagnostics);
            Assert.IsNull(response.Bulletin17C);
        }

        /// <summary>
        /// Verifies every property, including all nested result sections and the inherited
        /// response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new FrequencyResultsResponse
            {
                AnalysisId = Guid.NewGuid(),
                Kind = "bulletin17C",
                FittedDistribution = new FittedDistributionDto
                {
                    Type = "logPearsonTypeIII",
                    Parameters = new List<ParameterValueDto>
                    {
                        new ParameterValueDto { Name = "Mu", Value = 3.52 }
                    }
                },
                FrequencyCurve = new FrequencyCurveDto
                {
                    Probabilities = new List<double> { 0.5, 0.01 },
                    ModeCurve = new List<double> { 3300.0, 15400.0 },
                    CredibleIntervalWidth = 0.9
                },
                ParameterSummaries = new List<ParameterSummaryDto>
                {
                    new ParameterSummaryDto { Name = "Mu", Mean = 3.52, Rhat = 1.001 }
                },
                InformationCriteria = new InformationCriteriaDto { Aic = 512.4, Erl = 96.5 },
                Diagnostics = new DiagnosticsDto { Sampler = "demCzs", ElapsedMs = 4210 },
                Bulletin17C = new Bulletin17CInfoDto { UncertaintyMethod = "bootstrap", GmmElapsedMs = 84 },
                Success = false,
                ErrorMessage = "results stale",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 11,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.frequencyCurve.ciUpper[1]" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(response.AnalysisId, copy.AnalysisId);
            Assert.AreEqual("bulletin17C", copy.Kind);
            Assert.IsNotNull(copy.FittedDistribution);
            Assert.AreEqual("logPearsonTypeIII", copy.FittedDistribution.Type);
            Assert.AreEqual(1, copy.FittedDistribution.Parameters.Count);
            Assert.AreEqual(3.52, copy.FittedDistribution.Parameters[0].Value);
            Assert.IsNotNull(copy.FrequencyCurve);
            CollectionAssert.AreEqual(response.FrequencyCurve.Probabilities, copy.FrequencyCurve.Probabilities);
            CollectionAssert.AreEqual(response.FrequencyCurve.ModeCurve, copy.FrequencyCurve.ModeCurve);
            Assert.AreEqual(0.9, copy.FrequencyCurve.CredibleIntervalWidth);
            Assert.AreEqual(1, copy.ParameterSummaries.Count);
            Assert.AreEqual("Mu", copy.ParameterSummaries[0].Name);
            Assert.AreEqual(1.001, copy.ParameterSummaries[0].Rhat);
            Assert.IsNotNull(copy.InformationCriteria);
            Assert.AreEqual(512.4, copy.InformationCriteria.Aic);
            Assert.AreEqual(96.5, copy.InformationCriteria.Erl);
            Assert.IsNotNull(copy.Diagnostics);
            Assert.AreEqual("demCzs", copy.Diagnostics.Sampler);
            Assert.AreEqual(4210L, copy.Diagnostics.ElapsedMs);
            Assert.IsNotNull(copy.Bulletin17C);
            Assert.AreEqual("bootstrap", copy.Bulletin17C.UncertaintyMethod);
            Assert.AreEqual(84L, copy.Bulletin17C.GmmElapsedMs);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("results stale", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(11L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null result sections.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new FrequencyResultsResponse
            {
                Kind = "univariate",
                FrequencyCurve = new FrequencyCurveDto()
            });
            StringAssert.Contains(json, "\"analysisId\"");
            StringAssert.Contains(json, "\"frequencyCurve\"");
            StringAssert.Contains(json, "\"parameterSummaries\"");
            StringAssert.Contains(json, "\"success\"");
            Assert.IsFalse(json.Contains("\"bulletin17C\""), "Null bulletin17C should be omitted.");
            Assert.IsFalse(json.Contains("\"fittedDistribution\""), "Null fittedDistribution should be omitted.");
        }
    }
}
