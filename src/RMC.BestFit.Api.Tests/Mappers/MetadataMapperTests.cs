using Numerics.Distributions;
using RMC.BestFit.Api.Mappers;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="MetadataMapper"/>: distribution discovery, enum listings, and
    /// display names.
    /// </summary>
    [TestClass]
    public class MetadataMapperTests
    {
        /// <summary>
        /// Verifies the distribution listing includes the 15 univariate distributions and marks
        /// Log-Pearson Type III as supported by both analysis kinds.
        /// </summary>
        [TestMethod]
        public void ToDistributionsResponse_ListsSupportedDistributions()
        {
            var response = MetadataMapper.ToDistributionsResponse();

            Assert.AreEqual(15, response.Distributions.Count(d => d.SupportedBy.Contains("univariate")));

            var lp3 = response.Distributions.Single(d => d.Name == "logPearsonTypeIII");
            CollectionAssert.Contains(lp3.SupportedBy, "univariate");
            CollectionAssert.Contains(lp3.SupportedBy, "bulletin17c");
            Assert.AreEqual("Log-Pearson Type III", lp3.DisplayName);

            var gev = response.Distributions.Single(d => d.Name == "generalizedExtremeValue");
            CollectionAssert.Contains(gev.SupportedBy, "univariate");
            CollectionAssert.DoesNotContain(gev.SupportedBy, "bulletin17c");
        }

        /// <summary>
        /// Verifies the mixture, point process, and competing risks support flags: mixtures and
        /// competing risks accept the full univariate set while the point process is GEV-only.
        /// </summary>
        [TestMethod]
        public void ToDistributionsResponse_ListsPhase4Support()
        {
            var response = MetadataMapper.ToDistributionsResponse();

            Assert.AreEqual(15, response.Distributions.Count(d => d.SupportedBy.Contains("mixture")));
            Assert.AreEqual(15, response.Distributions.Count(d => d.SupportedBy.Contains("competingrisks")));

            var pointProcess = response.Distributions.Where(d => d.SupportedBy.Contains("pointprocess")).ToList();
            Assert.AreEqual(1, pointProcess.Count, "The point process is GEV-only by construction.");
            Assert.AreEqual("generalizedExtremeValue", pointProcess[0].Name);
        }

        /// <summary>
        /// Verifies each listed distribution carries its canonical parameter names — what
        /// distribution specs, parameter priors, and penalties are matched against.
        /// </summary>
        [TestMethod]
        public void ToDistributionsResponse_ListsParameterNames()
        {
            var response = MetadataMapper.ToDistributionsResponse();

            foreach (var info in response.Distributions)
            {
                Assert.IsTrue(info.ParameterNames.Count > 0, $"'{info.Name}' must list its parameter names.");
            }

            var normal = response.Distributions.Single(d => d.Name == "normal");
            Assert.AreEqual(2, normal.ParameterNames.Count);
        }

        /// <summary>
        /// Verifies the prior-distribution listing contains the common prior/measurement-error
        /// types and every listed name is factory-constructible camelCase.
        /// </summary>
        [TestMethod]
        public void ToEnumOptionsResponse_ListsPriorDistributions()
        {
            var response = MetadataMapper.ToEnumOptionsResponse();

            CollectionAssert.Contains(response.PriorDistributions, "normal");
            CollectionAssert.Contains(response.PriorDistributions, "logNormal");
            CollectionAssert.Contains(response.PriorDistributions, "uniform");
            CollectionAssert.Contains(response.PriorDistributions, "triangular");
            Assert.IsTrue(response.PriorDistributions.Count >= 15, "The spec list must cover at least the fitting distributions.");
            CollectionAssert.DoesNotContain(response.PriorDistributions, "competingRisks");
            CollectionAssert.DoesNotContain(response.PriorDistributions, "mixture");
            CollectionAssert.DoesNotContain(response.PriorDistributions, "userDefined");
        }

        /// <summary>
        /// Verifies the enum listings are populated with the expected camelCase members.
        /// </summary>
        [TestMethod]
        public void ToEnumOptionsResponse_ListsCamelCaseMembers()
        {
            var response = MetadataMapper.ToEnumOptionsResponse();

            CollectionAssert.Contains(response.Samplers, "demCzs");
            CollectionAssert.Contains(response.PointEstimators, "posteriorMean");
            CollectionAssert.Contains(response.PointEstimators, "posteriorMode");
            CollectionAssert.Contains(response.UncertaintyMethods, "multivariateNormal");
            CollectionAssert.Contains(response.TimeBlockWindows, "waterYear");
            CollectionAssert.Contains(response.BlockFunctions, "maximum");
            CollectionAssert.Contains(response.SmoothingFunctions, "none");
            CollectionAssert.Contains(response.UsgsSeriesTypes, "measuredDischarge");
            CollectionAssert.Contains(response.TimeIntervals, "oneDay");
            CollectionAssert.Contains(response.AnalysisKinds, "univariate");
            CollectionAssert.Contains(response.InputDataMethods, "blockMaxima");
        }

        /// <summary>
        /// Verifies display names for the documented distributions.
        /// </summary>
        [TestMethod]
        public void ToDisplayName_MapsDocumentedNames()
        {
            Assert.AreEqual("Gamma", MetadataMapper.ToDisplayName(UnivariateDistributionType.GammaDistribution));
            Assert.AreEqual("Ln-Normal", MetadataMapper.ToDisplayName(UnivariateDistributionType.LnNormal));
            Assert.AreEqual("Generalized Extreme Value", MetadataMapper.ToDisplayName(UnivariateDistributionType.GeneralizedExtremeValue));
        }
    }
}
