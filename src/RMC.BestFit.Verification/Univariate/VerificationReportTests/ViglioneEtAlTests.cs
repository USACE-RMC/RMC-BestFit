using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using System.Transactions;

namespace RMC.BestFit.Verification.Univariate.VerificationReportTests;

/// <summary>
/// Unit tests for verifying RMC-BestFit against the Viglione et al. (2013) Bayesian flood frequency analysis framework.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
///     <b>Purpose:</b>
///     These tests verify that RMC-BestFit produces results consistent with the published results from
///     Viglione et al. (2013) and Skahill et al. (2016) for Bayesian flood frequency analysis using
///     the GEV distribution. The tests cover various information expansion scenarios (temporal, spatial,
///     and causal) using the Kamp at Zwettl dataset from Austria.
/// </para>
/// <para>
///     <b>Test Configuration:</b>
///     All tests use the posterior mode (PM) as the point estimator with 90% credible intervals,
///     consistent with the results reported in Skahill et al. (2016). Jeffreys' rule for the scale
///     parameter prior is disabled to match the original methodology.
/// </para>
/// <para>
///     <b>Acceptance Criteria:</b>
///     GEV parameters and quantile estimates are verified to be within 5-10% of the published values,
///     accounting for Monte Carlo variability in the MCMC sampling.
/// </para>
/// </remarks>
[TestClass]
public class ViglioneEtAlTests
{
    /// <summary>
    /// Test 1: Verifies GEV fitting with exact systematic data from 1951-2001 (baseline, no information expansion).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test validates the baseline Bayesian GEV analysis using only the 51-year systematic record.
    /// Expected results: ξ=42.9, α=20.2, κ=-0.096; 100-yr=160 m³/s; 1000-yr=241 m³/s.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task Test1_ExactData_1951_2001()
    {
        var testData = ViglioneEtAlData.Test1_ExactData_1951_2001();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = false; // Viglione et al. do not use Jeffreys rule for scale parameter

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(new double[] { 0.001, 0.01 }); // 1000-year and 100-year return levels)
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // True parameters
        double trueLocation = testData.TrueParameters[0];
        double trueScale = testData.TrueParameters[1];
        double trueShape = testData.TrueParameters[2];
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.1), "Distribution shape parameter is incorrect.");

        // Test 100-year return level
        double mode100year = analysis.AnalysisResults!.ModeCurve[1];
        double lower100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 0];
        double upper100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 1];
        double trueMode100year = testData.True100year[0];
        double trueLower100year = testData.True100year[1];
        double trueUpper100year = testData.True100year[2];
        Assert.AreEqual(trueMode100year, mode100year, Math.Abs(trueMode100year * 0.05), "100-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower100year, lower100year, Math.Abs(trueLower100year * 0.05), "100-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper100year, upper100year, Math.Abs(trueUpper100year * 0.05), "100-year upper CI is incorrect.");

        // Test 1000-year return level
        double mode1000year = analysis.AnalysisResults!.ModeCurve[0];
        double lower1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 0];
        double upper1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 1];
        double trueMode1000year = testData.True1000year[0];
        double trueLower1000year = testData.True1000year[1];
        double trueUpper1000year = testData.True1000year[2];
        Assert.AreEqual(trueMode1000year, mode1000year, Math.Abs(trueMode1000year * 0.05), "1000-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower1000year, lower1000year, Math.Abs(trueLower1000year * 0.05), "1000-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper1000year, upper1000year, Math.Abs(trueUpper1000year * 0.05), "1000-year upper CI is incorrect.");
    }


    /// <summary>
    /// Test 2: Verifies GEV fitting with exact systematic data from 1951-2005 (includes 2002 extreme flood).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test validates the Bayesian GEV analysis using the extended 55-year systematic record
    /// that includes the extreme August 2002 flood event (459.2 m³/s). The inclusion of this outlier
    /// significantly affects the shape parameter estimate.
    /// Expected results: ξ=41.7, α=20.7, κ=-0.310; 100-yr=253 m³/s; 1000-yr=543 m³/s.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task Test2_ExactData_1951_2005()
    {
        var testData = ViglioneEtAlData.Test2_ExactData_1951_2005();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = false; // Viglione et al. do not use Jeffreys rule for scale parameter

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(new double[] { 0.001, 0.01 }); // 1000-year and 100-year return levels)
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // True parameters
        double trueLocation = testData.TrueParameters[0];
        double trueScale = testData.TrueParameters[1];
        double trueShape = testData.TrueParameters[2];
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // Test 100-year return level
        double mode100year = analysis.AnalysisResults!.ModeCurve[1];
        double lower100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 0];
        double upper100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 1];
        double trueMode100year = testData.True100year[0];
        double trueLower100year = testData.True100year[1];
        double trueUpper100year = testData.True100year[2];
        Assert.AreEqual(trueMode100year, mode100year, Math.Abs(trueMode100year * 0.05), "100-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower100year, lower100year, Math.Abs(trueLower100year * 0.05), "100-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper100year, upper100year, Math.Abs(trueUpper100year * 0.05), "100-year upper CI is incorrect.");

        // Test 1000-year return level
        double mode1000year = analysis.AnalysisResults!.ModeCurve[0];
        double lower1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 0];
        double upper1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 1];
        double trueMode1000year = testData.True1000year[0];
        double trueLower1000year = testData.True1000year[1];
        double trueUpper1000year = testData.True1000year[2];
        Assert.AreEqual(trueMode1000year, mode1000year, Math.Abs(trueMode1000year * 0.05), "1000-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower1000year, lower1000year, Math.Abs(trueLower1000year * 0.05), "1000-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper1000year, upper1000year, Math.Abs(trueUpper1000year * 0.05), "1000-year upper CI is incorrect.");
    }

    /// <summary>
    /// Test 3: Verifies GEV fitting with exact data (1951-2001) plus temporal expansion using historical flood data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test validates temporal information expansion by incorporating historical flood data
    /// (interval-censored observations and perception thresholds) along with the systematic record.
    /// The historical information constrains the upper tail and reduces uncertainty.
    /// Expected results: ξ=43.4, α=21.7, κ=-0.222; 100-yr=217 m³/s; 1000-yr=399 m³/s.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task Test3_ExactData_1951_2001_TemporalExpansion()
    {
        var testData = ViglioneEtAlData.Test3_ExactData_1951_2001_TemporalExpansion();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = false; // Viglione et al. do not use Jeffreys rule for scale parameter

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(new double[] { 0.001, 0.01 }); // 1000-year and 100-year return levels)
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // True parameters
        double trueLocation = testData.TrueParameters[0];
        double trueScale = testData.TrueParameters[1];
        double trueShape = testData.TrueParameters[2];
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // Test 100-year return level
        double mode100year = analysis.AnalysisResults!.ModeCurve[1];
        double lower100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 0];
        double upper100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 1];
        double trueMode100year = testData.True100year[0];
        double trueLower100year = testData.True100year[1];
        double trueUpper100year = testData.True100year[2];
        Assert.AreEqual(trueMode100year, mode100year, Math.Abs(trueMode100year * 0.05), "100-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower100year, lower100year, Math.Abs(trueLower100year * 0.05), "100-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper100year, upper100year, Math.Abs(trueUpper100year * 0.05), "100-year upper CI is incorrect.");

        // Test 1000-year return level
        double mode1000year = analysis.AnalysisResults!.ModeCurve[0];
        double lower1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 0];
        double upper1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 1];
        double trueMode1000year = testData.True1000year[0];
        double trueLower1000year = testData.True1000year[1];
        double trueUpper1000year = testData.True1000year[2];
        Assert.AreEqual(trueMode1000year, mode1000year, Math.Abs(trueMode1000year * 0.05), "1000-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower1000year, lower1000year, Math.Abs(trueLower1000year * 0.05), "1000-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper1000year, upper1000year, Math.Abs(trueUpper1000year * 0.05), "1000-year upper CI is incorrect.");
    }

    /// <summary>
    /// Test 4: Verifies GEV fitting with exact data (1951-2005) plus temporal expansion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test validates temporal information expansion using the extended systematic record
    /// (including the 2002 event) combined with historical flood data.
    /// Expected results: ξ=42.6, α=21.5, κ=-0.281; 100-yr=244 m³/s; 1000-yr=497 m³/s.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task Test4_ExactData_1951_2005_TemporalExpansion()
    {
        var testData = ViglioneEtAlData.Test4_ExactData_1951_2005_TemporalExpansion();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = false; // Viglione et al. do not use Jeffreys rule for scale parameter

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(new double[] { 0.001, 0.01 }); // 1000-year and 100-year return levels)
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // True parameters
        double trueLocation = testData.TrueParameters[0];
        double trueScale = testData.TrueParameters[1];
        double trueShape = testData.TrueParameters[2];
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // Test 100-year return level
        double mode100year = analysis.AnalysisResults!.ModeCurve[1];
        double lower100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 0];
        double upper100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 1];
        double trueMode100year = testData.True100year[0];
        double trueLower100year = testData.True100year[1];
        double trueUpper100year = testData.True100year[2];
        Assert.AreEqual(trueMode100year, mode100year, Math.Abs(trueMode100year * 0.05), "100-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower100year, lower100year, Math.Abs(trueLower100year * 0.05), "100-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper100year, upper100year, Math.Abs(trueUpper100year * 0.05), "100-year upper CI is incorrect.");

        // Test 1000-year return level
        double mode1000year = analysis.AnalysisResults!.ModeCurve[0];
        double lower1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 0];
        double upper1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 1];
        double trueMode1000year = testData.True1000year[0];
        double trueLower1000year = testData.True1000year[1];
        double trueUpper1000year = testData.True1000year[2];
        Assert.AreEqual(trueMode1000year, mode1000year, Math.Abs(trueMode1000year * 0.05), "1000-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower1000year, lower1000year, Math.Abs(trueLower1000year * 0.05), "1000-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper1000year, upper1000year, Math.Abs(trueUpper1000year * 0.05), "1000-year upper CI is incorrect.");
    }

    /// <summary>
    /// Test 5: Verifies GEV fitting with exact data (1951-2001) plus causal expansion using a quantile prior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test validates causal information expansion by incorporating expert knowledge about
    /// the 500-year flood as a quantile prior derived from analysis of flood generating mechanisms.
    /// Expected results: ξ=41.9, α=21.0, κ=-0.313; 100-yr=258 m³/s; 1000-yr=557 m³/s.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task Test5_ExactData_1951_2001_CausalExpansion()
    {
        var testData = ViglioneEtAlData.Test5_ExactData_1951_2001_CausalExpansion();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = false; // Viglione et al. do not use Jeffreys rule for scale parameter
        model.UseSingleQuantile = true;
        model.EnableQuantilePriors = true;
        model.QuantilePriors[0] = testData.QuantilePrior;
        model.ProcessQuantilePriors();

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(new double[] { 0.001, 0.01 }); // 1000-year and 100-year return levels)
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // True parameters
        double trueLocation = testData.TrueParameters[0];
        double trueScale = testData.TrueParameters[1];
        double trueShape = testData.TrueParameters[2];
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // Test 100-year return level
        double mode100year = analysis.AnalysisResults!.ModeCurve[1];
        double lower100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 0];
        double upper100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 1];
        double trueMode100year = testData.True100year[0];
        double trueLower100year = testData.True100year[1];
        double trueUpper100year = testData.True100year[2];
        Assert.AreEqual(trueMode100year, mode100year, Math.Abs(trueMode100year * 0.05), "100-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower100year, lower100year, Math.Abs(trueLower100year * 0.05), "100-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper100year, upper100year, Math.Abs(trueUpper100year * 0.05), "100-year upper CI is incorrect.");

        // Test 1000-year return level
        double mode1000year = analysis.AnalysisResults!.ModeCurve[0];
        double lower1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 0];
        double upper1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 1];
        double trueMode1000year = testData.True1000year[0];
        double trueLower1000year = testData.True1000year[1];
        double trueUpper1000year = testData.True1000year[2];
        Assert.AreEqual(trueMode1000year, mode1000year, Math.Abs(trueMode1000year * 0.05), "1000-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower1000year, lower1000year, Math.Abs(trueLower1000year * 0.05), "1000-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper1000year, upper1000year, Math.Abs(trueUpper1000year * 0.05), "1000-year upper CI is incorrect.");
    }

    /// <summary>
    /// Test 6: Verifies GEV fitting with exact data (1951-2005) plus causal expansion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test validates causal information expansion using the extended systematic record
    /// combined with the quantile prior from causal analysis.
    /// Expected results: ξ=41.6, α=20.8, κ=-0.333; 100-yr=269 m³/s; 1000-yr=604 m³/s.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task Test6_ExactData_1951_2005_CausalExpansion()
    {
        var testData = ViglioneEtAlData.Test6_ExactData_1951_2005_CausalExpansion();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = false; // Viglione et al. do not use Jeffreys rule for scale parameter
        model.UseSingleQuantile = true;
        model.EnableQuantilePriors = true;
        model.QuantilePriors[0] = testData.QuantilePrior;
        model.ProcessQuantilePriors();

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(new double[] { 0.001, 0.01 }); // 1000-year and 100-year return levels)
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // True parameters
        double trueLocation = testData.TrueParameters[0];
        double trueScale = testData.TrueParameters[1];
        double trueShape = testData.TrueParameters[2];
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // Test 100-year return level
        double mode100year = analysis.AnalysisResults!.ModeCurve[1];
        double lower100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 0];
        double upper100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 1];
        double trueMode100year = testData.True100year[0];
        double trueLower100year = testData.True100year[1];
        double trueUpper100year = testData.True100year[2];
        Assert.AreEqual(trueMode100year, mode100year, Math.Abs(trueMode100year * 0.05), "100-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower100year, lower100year, Math.Abs(trueLower100year * 0.05), "100-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper100year, upper100year, Math.Abs(trueUpper100year * 0.05), "100-year upper CI is incorrect.");

        // Test 1000-year return level
        double mode1000year = analysis.AnalysisResults!.ModeCurve[0];
        double lower1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 0];
        double upper1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 1];
        double trueMode1000year = testData.True1000year[0];
        double trueLower1000year = testData.True1000year[1];
        double trueUpper1000year = testData.True1000year[2];
        Assert.AreEqual(trueMode1000year, mode1000year, Math.Abs(trueMode1000year * 0.05), "1000-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower1000year, lower1000year, Math.Abs(trueLower1000year * 0.05), "1000-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper1000year, upper1000year, Math.Abs(trueUpper1000year * 0.05), "1000-year upper CI is incorrect.");
    }

    /// <summary>
    /// Test 7: Verifies GEV fitting with exact data (1951-2001) plus combined temporal and causal expansion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test validates the most comprehensive information expansion scenario for the 1951-2001 period,
    /// combining both historical flood data (temporal) and the quantile prior (causal).
    /// Expected results: ξ=42.7, α=21.8, κ=-0.291; 100-yr=253 m³/s; 1000-yr=527 m³/s.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task Test7_ExactData_1951_2001_TemporalAndCausalExpansion()
    {
        var testData = ViglioneEtAlData.Test7_ExactData_1951_2001_TemporalAndCausalExpansion();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = false; // Viglione et al. do not use Jeffreys rule for scale parameter
        model.UseSingleQuantile = true;
        model.EnableQuantilePriors = true;
        model.QuantilePriors[0] = testData.QuantilePrior;
        model.ProcessQuantilePriors();

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(new double[] { 0.001, 0.01 }); // 1000-year and 100-year return levels)
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // True parameters
        double trueLocation = testData.TrueParameters[0];
        double trueScale = testData.TrueParameters[1];
        double trueShape = testData.TrueParameters[2];
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // Test 100-year return level
        double mode100year = analysis.AnalysisResults!.ModeCurve[1];
        double lower100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 0];
        double upper100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 1];
        double trueMode100year = testData.True100year[0];
        double trueLower100year = testData.True100year[1];
        double trueUpper100year = testData.True100year[2];
        Assert.AreEqual(trueMode100year, mode100year, Math.Abs(trueMode100year * 0.05), "100-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower100year, lower100year, Math.Abs(trueLower100year * 0.05), "100-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper100year, upper100year, Math.Abs(trueUpper100year * 0.05), "100-year upper CI is incorrect.");

        // Test 1000-year return level
        double mode1000year = analysis.AnalysisResults!.ModeCurve[0];
        double lower1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 0];
        double upper1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 1];
        double trueMode1000year = testData.True1000year[0];
        double trueLower1000year = testData.True1000year[1];
        double trueUpper1000year = testData.True1000year[2];
        Assert.AreEqual(trueMode1000year, mode1000year, Math.Abs(trueMode1000year * 0.05), "1000-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower1000year, lower1000year, Math.Abs(trueLower1000year * 0.05), "1000-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper1000year, upper1000year, Math.Abs(trueUpper1000year * 0.05), "1000-year upper CI is incorrect.");
    }

    /// <summary>
    /// Test 8: Verifies GEV fitting with exact data (1951-2005) plus combined temporal and causal expansion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test validates the most comprehensive information expansion scenario for the 1951-2005 period,
    /// combining the extended systematic record, historical flood data, and the quantile prior.
    /// Expected results: ξ=42.5, α=21.5, κ=-0.313; 100-yr=264 m³/s; 1000-yr=571 m³/s.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task Test8_ExactData_1951_2005_TemporalAndCausalExpansion()
    {
        var testData = ViglioneEtAlData.Test8_ExactData_1951_2005_TemporalAndCausalExpansion();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = false; // Viglione et al. do not use Jeffreys rule for scale parameter
        model.UseSingleQuantile = true;
        model.EnableQuantilePriors = true;
        model.QuantilePriors[0] = testData.QuantilePrior;
        model.ProcessQuantilePriors();

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(new double[] { 0.001, 0.01 }); // 1000-year and 100-year return levels)
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // True parameters
        double trueLocation = testData.TrueParameters[0];
        double trueScale = testData.TrueParameters[1];
        double trueShape = testData.TrueParameters[2];
        Assert.AreEqual(trueLocation, dist.Xi, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, dist.Alpha, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, dist.Kappa, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // Test 100-year return level
        double mode100year = analysis.AnalysisResults!.ModeCurve[1];
        double lower100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 0];
        double upper100year = analysis.AnalysisResults!.ConfidenceIntervals[1, 1];
        double trueMode100year = testData.True100year[0];
        double trueLower100year = testData.True100year[1];
        double trueUpper100year = testData.True100year[2];
        Assert.AreEqual(trueMode100year, mode100year, Math.Abs(trueMode100year * 0.05), "100-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower100year, lower100year, Math.Abs(trueLower100year * 0.05), "100-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper100year, upper100year, Math.Abs(trueUpper100year * 0.05), "100-year upper CI is incorrect.");

        // Test 1000-year return level
        double mode1000year = analysis.AnalysisResults!.ModeCurve[0];
        double lower1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 0];
        double upper1000year = analysis.AnalysisResults!.ConfidenceIntervals[0, 1];
        double trueMode1000year = testData.True1000year[0];
        double trueLower1000year = testData.True1000year[1];
        double trueUpper1000year = testData.True1000year[2];
        Assert.AreEqual(trueMode1000year, mode1000year, Math.Abs(trueMode1000year * 0.05), "1000-year posterior mode is incorrect.");
        Assert.AreEqual(trueLower1000year, lower1000year, Math.Abs(trueLower1000year * 0.05), "1000-year lower CI is incorrect.");
        Assert.AreEqual(trueUpper1000year, upper1000year, Math.Abs(trueUpper1000year * 0.05), "1000-year upper CI is incorrect.");
    }

    /// <summary>
    /// Test 9: Verifies GEV fitting with three quantile priors against the EvdBayes R package.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test compares RMC-BestFit with the evdbayes R package using informative priors on three quantiles
    /// (10-year, 100-year, and 1000-year floods) following the approach of Coles &amp; Tawn (1996).
    /// The test verifies posterior summary statistics (mean, standard deviation, and credible intervals)
    /// for the GEV parameters against the EvdBayes MCMC output.
    /// </para>
    /// <para>
    /// This test uses the posterior mean as the point estimator with 95% credible intervals,
    /// consistent with the standard Bayesian reporting convention.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task Test9_ExactData_1951_2001_CausalExpansion_TheePriors()
    {
        var testData = ViglioneEtAlData.Test9_ExactData_1951_2001_CausalExpansion_TheePriors();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = false; // Viglione et al. do not use Jeffreys rule for scale parameter
        model.UseSingleQuantile = false;
        model.EnableQuantilePriors = true;
        model.QuantilePriors = testData.QuantilePriors;
        model.ProcessQuantilePriors();

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(new double[] { 0.001, 0.01 }); // 1000-year and 100-year return levels)
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.PosteriorMean.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // True mean parameters
        double location = analysis.BayesianAnalysis.Results.ParameterResults[0].SummaryStatistics.Mean;
        double scale = analysis.BayesianAnalysis.Results.ParameterResults[1].SummaryStatistics.Mean;
        double shape = analysis.BayesianAnalysis.Results.ParameterResults[2].SummaryStatistics.Mean;
        double trueLocation = testData.TrueMeanParameters[0];
        double trueScale = testData.TrueMeanParameters[1];
        double trueShape = testData.TrueMeanParameters[2];
        Assert.AreEqual(trueLocation, location, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, scale, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, shape, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // True stDev parameters
        location = analysis.BayesianAnalysis.Results.ParameterResults[0].SummaryStatistics.StandardDeviation;
        scale = analysis.BayesianAnalysis.Results.ParameterResults[1].SummaryStatistics.StandardDeviation;
        shape = analysis.BayesianAnalysis.Results.ParameterResults[2].SummaryStatistics.StandardDeviation;
        trueLocation = testData.TrueStDevParameters[0];
        trueScale = testData.TrueStDevParameters[1];
        trueShape = testData.TrueStDevParameters[2];
        Assert.AreEqual(trueLocation, location, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, scale, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, shape, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // True lower parameters
        location = analysis.BayesianAnalysis.Results.ParameterResults[0].SummaryStatistics.LowerCI;
        scale = analysis.BayesianAnalysis.Results.ParameterResults[1].SummaryStatistics.LowerCI;
        shape = analysis.BayesianAnalysis.Results.ParameterResults[2].SummaryStatistics.LowerCI;
        trueLocation = testData.TrueLowerParameters[0];
        trueScale = testData.TrueLowerParameters[1];
        trueShape = testData.TrueLowerParameters[2];
        Assert.AreEqual(trueLocation, location, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, scale, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, shape, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // True median parameters
        location = analysis.BayesianAnalysis.Results.ParameterResults[0].SummaryStatistics.Median;
        scale = analysis.BayesianAnalysis.Results.ParameterResults[1].SummaryStatistics.Median;
        shape = analysis.BayesianAnalysis.Results.ParameterResults[2].SummaryStatistics.Median;
        trueLocation = testData.TrueMedianParameters[0];
        trueScale = testData.TrueMedianParameters[1];
        trueShape = testData.TrueMedianParameters[2];
        Assert.AreEqual(trueLocation, location, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, scale, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, shape, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

        // True upper parameters
        location = analysis.BayesianAnalysis.Results.ParameterResults[0].SummaryStatistics.UpperCI;
        scale = analysis.BayesianAnalysis.Results.ParameterResults[1].SummaryStatistics.UpperCI;
        shape = analysis.BayesianAnalysis.Results.ParameterResults[2].SummaryStatistics.UpperCI;
        trueLocation = testData.TrueUpperParameters[0];
        trueScale = testData.TrueUpperParameters[1];
        trueShape = testData.TrueUpperParameters[2];
        Assert.AreEqual(trueLocation, location, Math.Abs(trueLocation * 0.05), "Distribution location parameter is incorrect.");
        Assert.AreEqual(trueScale, scale, Math.Abs(trueScale * 0.05), "Distribution scale parameter is incorrect.");
        Assert.AreEqual(trueShape, shape, Math.Abs(trueShape * 0.05), "Distribution shape parameter is incorrect.");

    }

}
