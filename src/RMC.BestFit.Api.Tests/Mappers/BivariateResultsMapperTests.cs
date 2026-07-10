using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="BivariateResultsMapper"/>: the run-first and kind guards on both
    /// entry points. Curve mapping is exercised end-to-end by user-run smoke tests (results
    /// require MCMC, which unit tests never run).
    /// </summary>
    [TestClass]
    public class BivariateResultsMapperTests
    {
        /// <summary>
        /// Verifies the run-first and kind guards on the bivariate entry point.
        /// </summary>
        [TestMethod]
        public void ToBivariateResults_Guards()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => BivariateResultsMapper.ToBivariateResults(TestAnalyses.CreateBivariateResource()));
            Assert.ThrowsException<ArgumentException>(
                () => BivariateResultsMapper.ToBivariateResults(TestAnalyses.CreateUnivariateResource()));
            Assert.ThrowsException<ArgumentNullException>(
                () => BivariateResultsMapper.ToBivariateResults(null!));
        }

        /// <summary>
        /// Verifies the run-first and kind guards on the coincident frequency entry point.
        /// </summary>
        [TestMethod]
        public void ToCoincidentFrequencyResults_Guards()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => BivariateResultsMapper.ToCoincidentFrequencyResults(TestAnalyses.CreateCoincidentFrequencyResource()));
            Assert.ThrowsException<ArgumentException>(
                () => BivariateResultsMapper.ToCoincidentFrequencyResults(TestAnalyses.CreateBivariateResource()));
            Assert.ThrowsException<ArgumentNullException>(
                () => BivariateResultsMapper.ToCoincidentFrequencyResults(null!));
        }
    }
}
