using Numerics.Distributions;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="DistributionFittingResultsMapper"/>: ranking order, failed-fit
    /// handling, NaN criteria, and the run-first/kind guards. Fits are hand-built — no MLE runs.
    /// </summary>
    [TestClass]
    public class DistributionFittingResultsMapperTests
    {
        /// <summary>
        /// Verifies successful fits rank by AIC ascending with failed fits last, carrying
        /// parameters for successes and error messages for failures.
        /// </summary>
        [TestMethod]
        public void ToRankedFits_OrdersByAic_FailuresLast()
        {
            var gumbel = new Gumbel(100d, 20d);
            var normal = new Normal(120d, 30d);
            var fits = new List<FittedDistribution>
            {
                new(normal, aic: 210d, bic: 214d, rmse: 9d, fitSucceeded: true),
                new(new LogNormal(), fitSucceeded: false) { ErrorMessage = "solver failed" },
                new(gumbel, aic: 205d, bic: 211d, rmse: 8d, fitSucceeded: true)
            };

            var ranked = DistributionFittingResultsMapper.ToRankedFits(fits);

            Assert.AreEqual(3, ranked.Count);
            Assert.AreEqual(1, ranked[0].Rank);
            Assert.AreEqual("gumbel", ranked[0].Distribution);
            Assert.AreEqual(205d, ranked[0].Aic);
            Assert.IsNotNull(ranked[0].Parameters);
            Assert.AreEqual(2, ranked[0].Parameters!.Count);

            Assert.AreEqual("normal", ranked[1].Distribution);

            Assert.AreEqual(3, ranked[2].Rank);
            Assert.IsFalse(ranked[2].FitSucceeded);
            Assert.AreEqual("solver failed", ranked[2].ErrorMessage);
            Assert.IsNull(ranked[2].Parameters, "Failed fits carry no parameter estimates.");
            Assert.IsNull(ranked[2].Aic, "NaN criteria map to null.");
        }

        /// <summary>
        /// Verifies a successful fit with NaN AIC ranks after finite-AIC successes but before
        /// failures.
        /// </summary>
        [TestMethod]
        public void ToRankedFits_NanAic_AfterFiniteSuccesses()
        {
            var fits = new List<FittedDistribution>
            {
                new(new Gumbel(1d, 2d), fitSucceeded: true),
                new(new Normal(0d, 1d), aic: 50d, fitSucceeded: true),
                new(new LogNormal(), fitSucceeded: false)
            };

            var ranked = DistributionFittingResultsMapper.ToRankedFits(fits);

            Assert.AreEqual("normal", ranked[0].Distribution);
            Assert.AreEqual("gumbel", ranked[1].Distribution);
            Assert.IsFalse(ranked[2].FitSucceeded);
        }

        /// <summary>
        /// Verifies the run-first and kind guards on the resource-level entry point.
        /// </summary>
        [TestMethod]
        public void ToResults_Guards()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => DistributionFittingResultsMapper.ToResults(TestAnalyses.CreateDistributionFittingResource()));
            Assert.ThrowsException<ArgumentException>(
                () => DistributionFittingResultsMapper.ToResults(TestAnalyses.CreateUnivariateResource()));
            Assert.ThrowsException<ArgumentNullException>(
                () => DistributionFittingResultsMapper.ToResults(null!));
        }
    }
}
