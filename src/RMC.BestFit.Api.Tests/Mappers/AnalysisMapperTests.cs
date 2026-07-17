using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="AnalysisMapper"/>: kind-specific summary fields and live validation.
    /// </summary>
    [TestClass]
    public class AnalysisMapperTests
    {
        /// <summary>
        /// Verifies the univariate summary reports kind, distribution, state, and validity.
        /// </summary>
        [TestMethod]
        public void ToSummary_Univariate_MapsKindAndDistribution()
        {
            var resource = TestAnalyses.CreateUnivariateResource();
            var summary = AnalysisMapper.ToSummary(resource);

            Assert.AreEqual(resource.Id, summary.Id);
            Assert.AreEqual("univariate", summary.Kind);
            Assert.AreEqual("logPearsonTypeIII", summary.Distribution);
            Assert.AreEqual("created", summary.State);
            Assert.IsTrue(summary.IsValid);
            Assert.IsNull(summary.ValidationMessages);
            Assert.IsNull(summary.Warnings, "No creation warnings means the field is omitted.");
            Assert.IsNull(summary.UncertaintyMethod);
            Assert.IsNull(summary.NumberOfSegments);
        }

        /// <summary>
        /// Verifies creation warnings recorded on the resource surface on the summary.
        /// </summary>
        [TestMethod]
        public void ToSummary_CreationWarnings_Surfaced()
        {
            var analysis = TestAnalyses.CreateBulletin17CResource();
            var resource = new AnalysisResource
            {
                Name = analysis.Name,
                Kind = analysis.Kind,
                Bulletin17C = analysis.Bulletin17C,
                CreationWarnings = new[] { "uncertain observations are ignored" }
            };

            var summary = AnalysisMapper.ToSummary(resource);

            Assert.IsNotNull(summary.Warnings);
            Assert.AreEqual(1, summary.Warnings.Count);
            StringAssert.Contains(summary.Warnings[0], "ignored");
        }

        /// <summary>
        /// Verifies the Bulletin 17C summary reports its uncertainty method.
        /// </summary>
        [TestMethod]
        public void ToSummary_Bulletin17C_MapsUncertaintyMethod()
        {
            var resource = TestAnalyses.CreateBulletin17CResource();
            var summary = AnalysisMapper.ToSummary(resource);

            Assert.AreEqual("bulletin17C", summary.Kind);
            Assert.AreEqual("logPearsonTypeIII", summary.Distribution);
            // The model's default uncertainty method is LinkedMultivariateNormal; the API leaves
            // it authoritative when the create request omits the field.
            Assert.AreEqual("linkedMultivariateNormal", summary.UncertaintyMethod);
        }

        /// <summary>
        /// Verifies the rating curve summary reports segments and no distribution.
        /// </summary>
        [TestMethod]
        public void ToSummary_RatingCurve_MapsSegments()
        {
            var resource = TestAnalyses.CreateRatingCurveResource();
            var summary = AnalysisMapper.ToSummary(resource);

            Assert.AreEqual("ratingCurve", summary.Kind);
            Assert.IsNull(summary.Distribution);
            Assert.AreEqual(1, summary.NumberOfSegments);
        }

        /// <summary>
        /// Verifies run-state bookkeeping fields flow into the summary.
        /// </summary>
        [TestMethod]
        public void ToSummary_RunState_Mapped()
        {
            var resource = TestAnalyses.CreateUnivariateResource();
            resource.State = AnalysisRunState.Failed;
            resource.LastError = "boom";
            resource.LastRunUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            resource.LastRunMs = 1234;

            var summary = AnalysisMapper.ToSummary(resource);

            Assert.AreEqual("failed", summary.State);
            Assert.AreEqual("boom", summary.LastError);
            Assert.AreEqual(1234, summary.LastRunMs);
            Assert.IsNotNull(summary.LastRunUtc);
        }

        /// <summary>
        /// Verifies the list response carries the count and one summary per resource.
        /// </summary>
        [TestMethod]
        public void ToListResponse_MapsAllResources()
        {
            var resources = new List<AnalysisResource>
            {
                TestAnalyses.CreateUnivariateResource(),
                TestAnalyses.CreateRatingCurveResource()
            };
            var response = AnalysisMapper.ToListResponse(resources);
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(2, response.Analyses.Count);
        }
    }
}
