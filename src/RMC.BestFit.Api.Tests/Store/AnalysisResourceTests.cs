using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Store
{
    /// <summary>
    /// Unit tests for <see cref="AnalysisResource"/>: kind dispatch, run-state defaults, and the
    /// single-entry run lock. No estimation is performed — analyses are only constructed.
    /// </summary>
    [TestClass]
    public class AnalysisResourceTests
    {
        /// <summary>
        /// Builds a small data frame with a few exact observations.
        /// </summary>
        /// <returns>The data frame.</returns>
        private static DataFrame CreateDataFrame()
        {
            var dataFrame = new DataFrame();
            dataFrame.ExactSeries.Add(new ExactData(2000, 100d));
            dataFrame.ExactSeries.Add(new ExactData(2001, 150d));
            dataFrame.ExactSeries.Add(new ExactData(2002, 120d));
            return dataFrame;
        }

        /// <summary>
        /// Verifies a univariate resource exposes the wrapped analysis through the kind dispatcher.
        /// </summary>
        [TestMethod]
        public void Analysis_UnivariateKind_ReturnsWrappedAnalysis()
        {
            var analysis = new UnivariateAnalysis(new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal));
            var resource = new AnalysisResource { Name = "a", Kind = AnalysisKind.Univariate, Univariate = analysis };
            Assert.AreSame(analysis, resource.Analysis);
        }

        /// <summary>
        /// Verifies the kind dispatcher throws when the typed field matching the kind is missing.
        /// </summary>
        [TestMethod]
        public void Analysis_KindWithoutInstance_Throws()
        {
            var resource = new AnalysisResource { Name = "a", Kind = AnalysisKind.Univariate };
            Assert.ThrowsException<InvalidOperationException>(() => _ = resource.Analysis);
        }

        /// <summary>
        /// Verifies the kind dispatcher resolves the mixture, point process, and competing risks
        /// arms, and each throws when its typed field is missing.
        /// </summary>
        [TestMethod]
        public void Analysis_Phase4Kinds_Dispatch()
        {
            var mixture = TestAnalyses.CreateMixtureResource();
            Assert.AreSame(mixture.Mixture, mixture.Analysis);

            var pointProcess = TestAnalyses.CreatePointProcessResource();
            Assert.AreSame(pointProcess.PointProcess, pointProcess.Analysis);

            var competingRisks = TestAnalyses.CreateCompetingRisksResource();
            Assert.AreSame(competingRisks.CompetingRisks, competingRisks.Analysis);

            Assert.ThrowsException<InvalidOperationException>(
                () => _ = new AnalysisResource { Name = "a", Kind = AnalysisKind.Mixture }.Analysis);
            Assert.ThrowsException<InvalidOperationException>(
                () => _ = new AnalysisResource { Name = "a", Kind = AnalysisKind.PointProcess }.Analysis);
            Assert.ThrowsException<InvalidOperationException>(
                () => _ = new AnalysisResource { Name = "a", Kind = AnalysisKind.CompetingRisks }.Analysis);

            var composite = TestAnalyses.CreateCompositeResource();
            Assert.AreSame(composite.Composite, composite.Analysis);
            Assert.AreEqual(2, composite.ComponentResources!.Count);

            var fitting = TestAnalyses.CreateDistributionFittingResource();
            Assert.AreSame(fitting.DistributionFitting, fitting.Analysis);

            var bivariate = TestAnalyses.CreateBivariateResource();
            Assert.AreSame(bivariate.Bivariate, bivariate.Analysis);
            Assert.AreEqual(2, bivariate.ComponentResources!.Count);

            var cfa = TestAnalyses.CreateCoincidentFrequencyResource();
            Assert.AreSame(cfa.CoincidentFrequency, cfa.Analysis);
            Assert.AreEqual(3, cfa.ComponentResources!.Count, "CFA locks cover the bivariate plus its transitive marginals.");

            Assert.ThrowsException<InvalidOperationException>(
                () => _ = new AnalysisResource { Name = "a", Kind = AnalysisKind.Composite }.Analysis);
            Assert.ThrowsException<InvalidOperationException>(
                () => _ = new AnalysisResource { Name = "a", Kind = AnalysisKind.DistributionFitting }.Analysis);
            Assert.ThrowsException<InvalidOperationException>(
                () => _ = new AnalysisResource { Name = "a", Kind = AnalysisKind.Bivariate }.Analysis);
            Assert.ThrowsException<InvalidOperationException>(
                () => _ = new AnalysisResource { Name = "a", Kind = AnalysisKind.CoincidentFrequency }.Analysis);

            foreach (var modelType in Enum.GetValues<TimeSeriesModelType>())
            {
                var timeSeries = TestAnalyses.CreateTimeSeriesAnalysisResource(modelType);
                Assert.IsNotNull(timeSeries.Analysis, $"The {modelType} sub-switch must resolve.");
            }
            Assert.ThrowsException<InvalidOperationException>(
                () => _ = new AnalysisResource { Name = "a", Kind = AnalysisKind.TimeSeries, TimeSeriesModel = TimeSeriesModelType.Ar }.Analysis);
            Assert.ThrowsException<InvalidOperationException>(
                () => _ = new AnalysisResource { Name = "a", Kind = AnalysisKind.TimeSeries }.Analysis,
                "A TimeSeries resource without a model discriminator must throw.");
        }

        /// <summary>
        /// Verifies unrun Phase-4 resources throw the run-first message from the results mapper.
        /// </summary>
        [TestMethod]
        public void FrequencyResults_BeforeRun_Throw()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => ResultsMapper.ToFrequencyResults(TestAnalyses.CreateMixtureResource()));
            Assert.ThrowsException<InvalidOperationException>(
                () => ResultsMapper.ToFrequencyResults(TestAnalyses.CreatePointProcessResource()));
            Assert.ThrowsException<InvalidOperationException>(
                () => ResultsMapper.ToFrequencyResults(TestAnalyses.CreateCompetingRisksResource()));
        }

        /// <summary>
        /// Verifies run-state defaults for a freshly created resource.
        /// </summary>
        [TestMethod]
        public void RunState_Defaults()
        {
            var resource = new AnalysisResource { Name = "a", Kind = AnalysisKind.Univariate };
            Assert.AreEqual(AnalysisRunState.Created, resource.State);
            Assert.IsNull(resource.LastRunUtc);
            Assert.IsNull(resource.LastError);
            Assert.IsNull(resource.LastRunMs);
        }

        /// <summary>
        /// Verifies the run lock is single-entry: a second immediate acquisition fails until released.
        /// </summary>
        [TestMethod]
        public void RunLock_IsSingleEntry()
        {
            var resource = new AnalysisResource { Name = "a", Kind = AnalysisKind.Univariate };
            Assert.IsTrue(resource.RunLock.Wait(0));
            try
            {
                Assert.IsFalse(resource.RunLock.Wait(0));
            }
            finally
            {
                resource.RunLock.Release();
            }
            Assert.IsTrue(resource.RunLock.Wait(0));
            resource.RunLock.Release();
        }

        /// <summary>
        /// Verifies provenance ids round-trip.
        /// </summary>
        [TestMethod]
        public void Provenance_RoundTrip()
        {
            var inputDataId = Guid.NewGuid();
            var stageId = Guid.NewGuid();
            var dischargeId = Guid.NewGuid();
            var resource = new AnalysisResource
            {
                Name = "a",
                Kind = AnalysisKind.RatingCurve,
                InputDataId = inputDataId,
                StageTimeSeriesId = stageId,
                DischargeTimeSeriesId = dischargeId
            };
            Assert.AreEqual(inputDataId, resource.InputDataId);
            Assert.AreEqual(stageId, resource.StageTimeSeriesId);
            Assert.AreEqual(dischargeId, resource.DischargeTimeSeriesId);
        }
    }
}
