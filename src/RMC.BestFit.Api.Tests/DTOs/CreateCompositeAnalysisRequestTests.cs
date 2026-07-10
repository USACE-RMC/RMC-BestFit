using Numerics.Data.Statistics;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateCompositeAnalysisRequest"/>: defaults, JSON round-trip,
    /// and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreateCompositeAnalysisRequestTests
    {
        /// <summary>
        /// Verifies the defaults: competing risks combination as a maximum, with null options.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateCompositeAnalysisRequest();
            Assert.AreEqual(0, request.Components.Count);
            Assert.AreEqual(CompositeType.CompetingRisks, request.CompositeType);
            Assert.IsNull(request.AverageMethod);
            Assert.IsNull(request.Dependency);
            Assert.IsTrue(request.IsMaximum);
            Assert.IsNull(request.ProbabilityOrdinates);
            Assert.IsNull(request.CredibleIntervalWidth);
            Assert.IsNull(request.PointEstimator);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto>
                {
                    new() { AnalysisId = Guid.NewGuid(), Weight = 0.6 },
                    new() { AnalysisId = Guid.NewGuid(), Weight = 0.4 }
                },
                CompositeType = CompositeType.Mixture,
                AverageMethod = AverageMethod.WAIC,
                Dependency = Probability.DependencyType.PerfectlyPositive,
                IsMaximum = false,
                ProbabilityOrdinates = new List<double> { 0.5, 0.01 },
                CredibleIntervalWidth = 0.95,
                PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean,
                Name = "composite"
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(2, copy.Components.Count);
            Assert.AreEqual(0.6, copy.Components[0].Weight);
            Assert.AreEqual(CompositeType.Mixture, copy.CompositeType);
            Assert.AreEqual(AverageMethod.WAIC, copy.AverageMethod);
            Assert.AreEqual(Probability.DependencyType.PerfectlyPositive, copy.Dependency);
            Assert.IsFalse(copy.IsMaximum);
            Assert.AreEqual(0.95, copy.CredibleIntervalWidth);
            Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, copy.PointEstimator);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and enum strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto> { new() { AnalysisId = Guid.NewGuid() } },
                CompositeType = CompositeType.ModelAverage
            });
            StringAssert.Contains(json, "\"components\"");
            StringAssert.Contains(json, "\"analysisId\"");
            StringAssert.Contains(json, "\"compositeType\"");
            StringAssert.Contains(json, "\"modelAverage\"");
            StringAssert.Contains(json, "\"isMaximum\"");
        }
    }
}
