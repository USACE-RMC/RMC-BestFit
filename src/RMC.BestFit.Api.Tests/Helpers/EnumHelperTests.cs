using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.Tests.Helpers
{
    /// <summary>
    /// Unit tests for <see cref="EnumHelper"/> camelCase conversions.
    /// </summary>
    [TestClass]
    public class EnumHelperTests
    {
        /// <summary>
        /// Verifies single-name conversion matches the serializer's camelCase policy.
        /// </summary>
        [TestMethod]
        public void ToCamelCase_MatchesJsonPolicy()
        {
            Assert.AreEqual("waterYear", EnumHelper.ToCamelCase("WaterYear"));
            Assert.AreEqual("maximum", EnumHelper.ToCamelCase("Maximum"));
            Assert.AreEqual("posteriorMode", EnumHelper.ToCamelCase("PosteriorMode"));
        }

        /// <summary>
        /// Verifies enum enumeration returns every member in camelCase.
        /// </summary>
        [TestMethod]
        public void CamelCaseNames_ListsAllMembers()
        {
            var kinds = EnumHelper.CamelCaseNames<AnalysisKind>();
            CollectionAssert.AreEqual(
                new List<string>
                {
                    "univariate", "bulletin17C", "ratingCurve", "mixture", "pointProcess",
                    "competingRisks", "composite", "distributionFitting", "bivariate",
                    "coincidentFrequency", "timeSeries"
                },
                kinds);

            var samplers = EnumHelper.CamelCaseNames<BayesianAnalysis.SamplerType>();
            Assert.AreEqual(Enum.GetNames<BayesianAnalysis.SamplerType>().Length, samplers.Count);
        }

        /// <summary>
        /// Verifies MCP-parameter parsing: camelCase and any-case values parse, blank falls back
        /// to the default, and invalid values throw with the accepted list.
        /// </summary>
        [TestMethod]
        public void ParseOrDefault_ParsesCaseInsensitively_AndFallsBack()
        {
            Assert.AreEqual(AnalysisKind.RatingCurve, EnumHelper.ParseOrDefault("ratingCurve", AnalysisKind.Univariate));
            Assert.AreEqual(AnalysisKind.RatingCurve, EnumHelper.ParseOrDefault("RATINGCURVE", AnalysisKind.Univariate));
            Assert.AreEqual(AnalysisKind.Univariate, EnumHelper.ParseOrDefault(null, AnalysisKind.Univariate));
            Assert.AreEqual(AnalysisKind.Univariate, EnumHelper.ParseOrDefault("  ", AnalysisKind.Univariate));

            var ex = Assert.ThrowsException<ArgumentException>(() => EnumHelper.ParseOrDefault("bogus", AnalysisKind.Univariate));
            StringAssert.Contains(ex.Message, "univariate");
        }

        /// <summary>
        /// Verifies nullable MCP-parameter parsing: blank yields null (model default stays
        /// authoritative) and invalid values throw.
        /// </summary>
        [TestMethod]
        public void ParseOrNull_BlankYieldsNull_InvalidThrows()
        {
            Assert.IsNull(EnumHelper.ParseOrNull<AnalysisKind>(null));
            Assert.IsNull(EnumHelper.ParseOrNull<AnalysisKind>(""));
            Assert.AreEqual(AnalysisKind.Bulletin17C, EnumHelper.ParseOrNull<AnalysisKind>("bulletin17C"));
            Assert.ThrowsException<ArgumentException>(() => EnumHelper.ParseOrNull<AnalysisKind>("bogus"));
        }
    }
}
