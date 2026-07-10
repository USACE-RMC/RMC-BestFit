using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="AnalysisSummaryDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class AnalysisSummaryDtoTests
    {
        /// <summary>
        /// Verifies a new summary defaults to an empty identity with no kind, state, provenance,
        /// or run information.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var summary = new AnalysisSummaryDto();
            Assert.AreEqual(Guid.Empty, summary.Id);
            Assert.IsNull(summary.Name);
            Assert.IsNull(summary.Description);
            Assert.AreEqual(default(DateTime), summary.CreatedUtc);
            Assert.IsNull(summary.Kind);
            Assert.IsNull(summary.State);
            Assert.IsFalse(summary.IsValid);
            Assert.IsNull(summary.ValidationMessages);
            Assert.IsNull(summary.Distribution);
            Assert.IsNull(summary.UncertaintyMethod);
            Assert.IsNull(summary.NumberOfSegments);
            Assert.IsNull(summary.InputDataId);
            Assert.IsNull(summary.StageTimeSeriesId);
            Assert.IsNull(summary.DischargeTimeSeriesId);
            Assert.IsNull(summary.LastRunUtc);
            Assert.IsNull(summary.LastRunMs);
            Assert.IsNull(summary.LastError);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var summary = new AnalysisSummaryDto
            {
                Id = Guid.NewGuid(),
                Name = "LP3 fit",
                Description = "Univariate analysis of the annual peaks.",
                CreatedUtc = new DateTime(2026, 6, 30, 8, 15, 0),
                Kind = "univariate",
                State = "failed",
                IsValid = true,
                ValidationMessages = new List<string> { "The input data has no observations." },
                Distribution = "logPearsonTypeIII",
                UncertaintyMethod = "linkedMultivariateNormal",
                NumberOfSegments = 2,
                InputDataId = Guid.NewGuid(),
                StageTimeSeriesId = Guid.NewGuid(),
                DischargeTimeSeriesId = Guid.NewGuid(),
                LastRunUtc = new DateTime(2026, 7, 1, 9, 30, 0),
                LastRunMs = 5321,
                LastError = "MCMC failed to initialize."
            };

            var copy = TestJson.Roundtrip(summary);

            Assert.AreEqual(summary.Id, copy.Id);
            Assert.AreEqual("LP3 fit", copy.Name);
            Assert.AreEqual("Univariate analysis of the annual peaks.", copy.Description);
            Assert.AreEqual(summary.CreatedUtc, copy.CreatedUtc);
            Assert.AreEqual("univariate", copy.Kind);
            Assert.AreEqual("failed", copy.State);
            Assert.IsTrue(copy.IsValid);
            CollectionAssert.AreEqual(summary.ValidationMessages, copy.ValidationMessages);
            Assert.AreEqual("logPearsonTypeIII", copy.Distribution);
            Assert.AreEqual("linkedMultivariateNormal", copy.UncertaintyMethod);
            Assert.AreEqual(2, copy.NumberOfSegments);
            Assert.AreEqual(summary.InputDataId, copy.InputDataId);
            Assert.AreEqual(summary.StageTimeSeriesId, copy.StageTimeSeriesId);
            Assert.AreEqual(summary.DischargeTimeSeriesId, copy.DischargeTimeSeriesId);
            Assert.AreEqual(summary.LastRunUtc, copy.LastRunUtc);
            Assert.AreEqual(5321L, copy.LastRunMs);
            Assert.AreEqual("MCMC failed to initialize.", copy.LastError);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new AnalysisSummaryDto
            {
                Kind = "ratingCurve",
                State = "succeeded"
            });
            StringAssert.Contains(json, "\"id\"");
            StringAssert.Contains(json, "\"createdUtc\"");
            StringAssert.Contains(json, "\"isValid\"");
            StringAssert.Contains(json, "\"kind\"");
            StringAssert.Contains(json, "\"state\"");
            Assert.IsFalse(json.Contains("\"lastError\""), "Null lastError should be omitted.");
            Assert.IsFalse(json.Contains("\"validationMessages\""), "Null validationMessages should be omitted.");
        }
    }
}
