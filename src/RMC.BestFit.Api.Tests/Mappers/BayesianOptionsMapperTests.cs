using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="BayesianOptionsMapper"/>: null-field passthrough, assignment,
    /// simulation-defaults switching, and the iteration cap.
    /// </summary>
    [TestClass]
    public class BayesianOptionsMapperTests
    {
        /// <summary>
        /// Builds a fresh Bayesian analysis attached to a small univariate model.
        /// </summary>
        /// <returns>The Bayesian analysis.</returns>
        private static BayesianAnalysis CreateBayesianAnalysis()
        {
            return TestAnalyses.CreateUnivariateResource().Univariate!.BayesianAnalysis;
        }

        /// <summary>
        /// Verifies a null options object leaves every setting untouched.
        /// </summary>
        [TestMethod]
        public void Apply_NullOptions_LeavesDefaults()
        {
            var bayesian = CreateBayesianAnalysis();
            bool defaultUseSimulationDefaults = bayesian.UseSimulationDefaults;
            int defaultIterations = bayesian.Iterations;

            BayesianOptionsMapper.Apply(bayesian, null, maxIterations: 500_000);

            Assert.AreEqual(defaultUseSimulationDefaults, bayesian.UseSimulationDefaults);
            Assert.AreEqual(defaultIterations, bayesian.Iterations);
        }

        /// <summary>
        /// Verifies supplied fields are assigned and simulation defaults are switched off so the
        /// run does not overwrite them.
        /// </summary>
        [TestMethod]
        public void Apply_SimulationFields_AssignedAndDefaultsDisabled()
        {
            var bayesian = CreateBayesianAnalysis();
            var options = new BayesianOptionsDto
            {
                Sampler = BayesianAnalysis.SamplerType.DEMCz,
                Iterations = 5000,
                WarmupIterations = 2500,
                ThinningInterval = 5,
                NumberOfChains = 4,
                PrngSeed = 12345,
                CredibleIntervalWidth = 0.95,
                OutputLength = 2000,
                PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean
            };

            BayesianOptionsMapper.Apply(bayesian, options, maxIterations: 500_000);

            Assert.IsFalse(bayesian.UseSimulationDefaults);
            Assert.AreEqual(BayesianAnalysis.SamplerType.DEMCz, bayesian.Type);
            Assert.AreEqual(5000, bayesian.Iterations);
            Assert.AreEqual(2500, bayesian.WarmupIterations);
            Assert.AreEqual(5, bayesian.ThinningInterval);
            Assert.AreEqual(4, bayesian.NumberOfChains);
            Assert.AreEqual(12345, bayesian.PRNGSeed);
            Assert.AreEqual(0.95, bayesian.CredibleIntervalWidth);
            Assert.AreEqual(2000, bayesian.OutputLength);
            Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, bayesian.PointEstimator);
        }

        /// <summary>
        /// Verifies non-simulation fields (point estimator, CI width, seed) do not switch off the
        /// automatic simulation defaults.
        /// </summary>
        [TestMethod]
        public void Apply_NonSimulationFields_KeepSimulationDefaults()
        {
            var bayesian = CreateBayesianAnalysis();
            bool defaultUseSimulationDefaults = bayesian.UseSimulationDefaults;

            BayesianOptionsMapper.Apply(bayesian, new BayesianOptionsDto
            {
                PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean,
                CredibleIntervalWidth = 0.8,
                PrngSeed = 42
            }, maxIterations: 500_000);

            Assert.AreEqual(defaultUseSimulationDefaults, bayesian.UseSimulationDefaults);
            Assert.AreEqual(0.8, bayesian.CredibleIntervalWidth);
        }

        /// <summary>
        /// Verifies iterations above the server cap are rejected.
        /// </summary>
        [TestMethod]
        public void Apply_IterationsOverCap_Throws()
        {
            var bayesian = CreateBayesianAnalysis();
            var ex = Assert.ThrowsException<ArgumentException>(() =>
                BayesianOptionsMapper.Apply(bayesian, new BayesianOptionsDto { Iterations = 1_000_000 }, maxIterations: 500_000));
            StringAssert.Contains(ex.Message, "500000");
        }

        /// <summary>
        /// Verifies warm-up must be below iterations when both are supplied.
        /// </summary>
        [TestMethod]
        public void Apply_WarmupNotBelowIterations_Throws()
        {
            var bayesian = CreateBayesianAnalysis();
            Assert.ThrowsException<ArgumentException>(() =>
                BayesianOptionsMapper.Apply(bayesian, new BayesianOptionsDto { Iterations = 1000, WarmupIterations = 1000 }, maxIterations: 500_000));
        }

        /// <summary>
        /// Verifies the null guard on the target analysis.
        /// </summary>
        [TestMethod]
        public void Apply_NullAnalysis_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => BayesianOptionsMapper.Apply(null!, new BayesianOptionsDto(), 1000));
        }
    }
}
