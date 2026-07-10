using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesResultsMapper"/>: the run-first and kind guards for
    /// every model family. Curve mapping is exercised end-to-end by user-run smoke tests
    /// (results require MCMC, which unit tests never run).
    /// </summary>
    [TestClass]
    public class TimeSeriesResultsMapperTests
    {
        /// <summary>
        /// Verifies the run-first guard for each model family and the kind guard.
        /// </summary>
        [TestMethod]
        public void ToResults_Guards()
        {
            foreach (var modelType in Enum.GetValues<TimeSeriesModelType>())
            {
                Assert.ThrowsException<InvalidOperationException>(
                    () => TimeSeriesResultsMapper.ToResults(TestAnalyses.CreateTimeSeriesAnalysisResource(modelType)),
                    $"An unrun {modelType} analysis must report run-first.");
            }

            Assert.ThrowsException<ArgumentException>(
                () => TimeSeriesResultsMapper.ToResults(TestAnalyses.CreateUnivariateResource()));
            Assert.ThrowsException<ArgumentNullException>(
                () => TimeSeriesResultsMapper.ToResults(null!));
        }
    }
}
