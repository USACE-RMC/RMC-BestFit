using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Api.Mappers;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="ResultsMapper"/>: both confidence-interval array shapes,
    /// posterior parameter summaries built from synthetic MCMC results (no sampler run), and the
    /// convergence-warning thresholds.
    /// </summary>
    [TestClass]
    public class ResultsMapperTests
    {
        /// <summary>
        /// Builds an uncertainty results object with the univariate/B17C [p, 2] CI shape.
        /// </summary>
        /// <returns>The results object.</returns>
        private static UncertaintyAnalysisResults CreateFrequencyShapedResults()
        {
            return new UncertaintyAnalysisResults
            {
                ModeCurve = new[] { 100d, 200d, 300d },
                MeanCurve = new[] { 110d, 210d, 310d },
                ConfidenceIntervals = new[,] { { 90d, 120d }, { 180d, 230d }, { 270d, 340d } },
                AIC = 10d,
                BIC = 12d,
                DIC = 11d,
                RMSE = 0.5,
                ERL = double.NaN
            };
        }

        /// <summary>
        /// Builds an uncertainty results object with the rating-curve [n, 3] (stage, lower, upper) CI shape.
        /// </summary>
        /// <returns>The results object.</returns>
        private static UncertaintyAnalysisResults CreateRatingShapedResults()
        {
            return new UncertaintyAnalysisResults
            {
                ModeCurve = new[] { 10d, 20d },
                MeanCurve = new[] { 11d, 21d },
                ConfidenceIntervals = new[,] { { 1.0, 8d, 13d }, { 2.0, 16d, 26d } },
                AIC = double.NaN,
                BIC = double.NaN,
                DIC = 5d,
                RMSE = 0.1,
                ERL = double.NaN
            };
        }

        /// <summary>
        /// Builds synthetic MCMC results from deterministic parameter sets (no sampler run).
        /// </summary>
        /// <param name="count">The number of parameter sets.</param>
        /// <returns>The synthetic MCMC results with two parameters centered at 100 and 10.</returns>
        private static MCMCResults CreateSyntheticMcmcResults(int count = 200)
        {
            var sets = new List<ParameterSet>(count);
            for (int i = 0; i < count; i++)
            {
                // Deterministic spread: parameter 0 in [95, 105), parameter 1 in [9.5, 10.5).
                double offset = (i % 100) / 100d;
                sets.Add(new ParameterSet(new[] { 95d + 10d * offset, 9.5 + 1d * offset }, double.NaN));
            }
            var map = new ParameterSet(new[] { 100d, 10d }, double.NaN);
            return new MCMCResults(map, sets, alpha: 0.1);
        }

        /// <summary>
        /// Verifies the [p, 2] CI shape splits into aligned lower/upper lists.
        /// </summary>
        [TestMethod]
        public void BuildFrequencyCurve_SplitsTwoColumnIntervals()
        {
            var probabilities = new List<double> { 0.5, 0.1, 0.01 };
            var curve = ResultsMapper.BuildFrequencyCurve(CreateFrequencyShapedResults(), probabilities, credibleIntervalWidth: 0.9);

            CollectionAssert.AreEqual(probabilities, curve.Probabilities);
            CollectionAssert.AreEqual(new List<double> { 100d, 200d, 300d }, curve.ModeCurve);
            CollectionAssert.AreEqual(new List<double> { 110d, 210d, 310d }, curve.MeanCurve);
            CollectionAssert.AreEqual(new List<double> { 90d, 180d, 270d }, curve.CiLower);
            CollectionAssert.AreEqual(new List<double> { 120d, 230d, 340d }, curve.CiUpper);
            Assert.AreEqual(0.9, curve.CredibleIntervalWidth);
        }

        /// <summary>
        /// Verifies the [n, 3] CI shape splits into stage grid plus lower/upper discharge lists.
        /// </summary>
        [TestMethod]
        public void BuildRatingCurve_SplitsThreeColumnIntervals()
        {
            var curve = ResultsMapper.BuildRatingCurve(CreateRatingShapedResults(), credibleIntervalWidth: 0.9, minStage: 1d, maxStage: 2d, stageBins: 2);

            CollectionAssert.AreEqual(new List<double> { 1d, 2d }, curve.Stages);
            CollectionAssert.AreEqual(new List<double> { 8d, 16d }, curve.CiLower);
            CollectionAssert.AreEqual(new List<double> { 13d, 26d }, curve.CiUpper);
            CollectionAssert.AreEqual(new List<double> { 10d, 20d }, curve.ModeCurve);
            Assert.AreEqual(1d, curve.MinStage);
            Assert.AreEqual(2d, curve.MaxStage);
            Assert.AreEqual(2, curve.StageBins);
        }

        /// <summary>
        /// Verifies missing confidence intervals leave the CI lists null rather than throwing.
        /// </summary>
        [TestMethod]
        public void BuildFrequencyCurve_NoIntervals_LeavesCiNull()
        {
            var results = new UncertaintyAnalysisResults { ModeCurve = new[] { 1d } };
            var curve = ResultsMapper.BuildFrequencyCurve(results, new List<double> { 0.5 }, 0.9);
            Assert.IsNull(curve.CiLower);
            Assert.IsNull(curve.CiUpper);
        }

        /// <summary>
        /// Verifies parameter summaries pair names with synthetic posterior statistics and honor
        /// the chain-diagnostics switch (B17C reports rhat/ess as null).
        /// </summary>
        [TestMethod]
        public void BuildParameterSummaries_FromSyntheticResults()
        {
            var results = CreateSyntheticMcmcResults();
            var names = new List<string> { "Mu", "Sigma" };

            var withChains = ResultsMapper.BuildParameterSummaries(names, results, includeChainDiagnostics: true);
            Assert.AreEqual(2, withChains.Count);
            Assert.AreEqual("Mu", withChains[0].Name);
            Assert.IsNotNull(withChains[0].Mean);
            Assert.AreEqual(100d, withChains[0].Mean!.Value, 1.0);
            Assert.IsNotNull(withChains[1].Mean);
            Assert.AreEqual(10d, withChains[1].Mean!.Value, 0.1);
            Assert.IsNotNull(withChains[0].LowerCI);
            Assert.IsNotNull(withChains[0].UpperCI);
            Assert.IsTrue(withChains[0].LowerCI < withChains[0].UpperCI);

            var withoutChains = ResultsMapper.BuildParameterSummaries(names, results, includeChainDiagnostics: false);
            Assert.IsNull(withoutChains[0].Rhat);
            Assert.IsNull(withoutChains[0].Ess);
        }

        /// <summary>
        /// Verifies null MCMC results yield an empty summary list.
        /// </summary>
        [TestMethod]
        public void BuildParameterSummaries_NullResults_Empty()
        {
            var summaries = ResultsMapper.BuildParameterSummaries(new List<string> { "Mu" }, null, includeChainDiagnostics: true);
            Assert.AreEqual(0, summaries.Count);
        }

        /// <summary>
        /// Verifies the convergence scan flags high R-hat and low effective sample size, and stays
        /// silent for healthy diagnostics.
        /// </summary>
        [TestMethod]
        public void BuildConvergenceWarnings_FlagsThresholds()
        {
            var results = CreateSyntheticMcmcResults();
            var names = new List<string> { "Mu", "Sigma" };

            // Healthy diagnostics → no warnings.
            results.ParameterResults[0].SummaryStatistics.Rhat = 1.01;
            results.ParameterResults[0].SummaryStatistics.ESS = 5000d;
            results.ParameterResults[1].SummaryStatistics.Rhat = 1.02;
            results.ParameterResults[1].SummaryStatistics.ESS = 4000d;
            Assert.AreEqual(0, ResultsMapper.BuildConvergenceWarnings(results, names).Count);

            // High R-hat on Mu, low ESS on Sigma → two warnings naming the parameters.
            results.ParameterResults[0].SummaryStatistics.Rhat = 1.5;
            results.ParameterResults[1].SummaryStatistics.ESS = 50d;
            var warnings = ResultsMapper.BuildConvergenceWarnings(results, names);
            Assert.AreEqual(2, warnings.Count);
            StringAssert.Contains(warnings[0], "Mu");
            StringAssert.Contains(warnings[1], "Sigma");
        }

        /// <summary>
        /// Verifies NaN diagnostics (single-chain runs) produce neither warnings nor summary values.
        /// </summary>
        [TestMethod]
        public void BuildConvergenceWarnings_NaNDiagnostics_Silent()
        {
            var results = CreateSyntheticMcmcResults();
            results.ParameterResults[0].SummaryStatistics.Rhat = double.NaN;
            results.ParameterResults[0].SummaryStatistics.ESS = double.NaN;
            results.ParameterResults[1].SummaryStatistics.Rhat = double.NaN;
            results.ParameterResults[1].SummaryStatistics.ESS = double.NaN;

            Assert.AreEqual(0, ResultsMapper.BuildConvergenceWarnings(results, new List<string> { "Mu", "Sigma" }).Count);

            var summaries = ResultsMapper.BuildParameterSummaries(new List<string> { "Mu", "Sigma" }, results, includeChainDiagnostics: true);
            Assert.IsNull(summaries[0].Rhat);
            Assert.IsNull(summaries[0].Ess);
        }
    }
}
