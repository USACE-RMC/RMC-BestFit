using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.Univariate.VerificationReportTests;

/// <summary>
/// Unit tests for verifying RMC-BestFit against the Flike software from Australian Rainfall and Runoff (ARR).
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
///     <b>Purpose:</b>
///     These tests verify that RMC-BestFit produces results consistent with the Flike software
///     developed by Professor George Kuczera at the University of Newcastle, Australia. Flike is
///     compliant with the Australian Rainfall and Runoff (ARR) guidelines for flood frequency analysis.
/// </para>
/// <para>
///     <b>Test Configuration:</b>
///     All tests use Jeffreys' rule for the scale parameter prior (consistent with Flike's default)
///     and the posterior mean as the point estimator with 90% credible intervals. Tests 3-5 use
///     Log-Pearson Type III (LPIII) distribution, while Tests 6a/6b use Generalized Extreme Value (GEV).
/// </para>
/// <para>
///     <b>Acceptance Criteria:</b>
///     Posterior mean quantile estimates and credible intervals are verified to be within 5% of
///     the published Flike results, accounting for differences in MCMC vs. importance sampling methodology.
/// </para>
/// </remarks>
[TestClass]
public class ArrFlikeTests
{
    /// <summary>
    /// Example 3: Tests basic LPIII flood frequency analysis following ARR Book 3 procedures.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test verifies the baseline Bayesian LPIII analysis using the Hunter River at Singleton
    /// dataset with uninformative (flat) priors on all parameters. This corresponds to Example 3
    /// from the Flike self-training examples.
    /// </para>
    /// <para>
    /// Expected results include very wide credible intervals due to the positive skew in the data
    /// and the lack of additional information constraints.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task TestExample3()
    {
        var testData = ArrFlikeData.Example3();

        // Create UnivariateDistribution model with LPIII distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.LogPearsonTypeIII);
        model.UseJeffreysRuleForScale = true; // Flike uses Jeffreys rule for scale parameter prior

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(testData.AEPValues);
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMean;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (LogPearsonTypeIII)model.Distribution;

        // Test frequency curve outputs
        for (int i = 0; i < testData.AEPValues.Length; i++)
        {
            double posteriorMean = analysis.AnalysisResults!.ModeCurve![i];
            double lowerCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            double upperCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            double trueMean = testData.TruePosteriorMean[i];
            double trueLower = testData.TrueLowerCI[i];
            double trueUpper = testData.TrueUpperCI[i];
            Assert.AreEqual(trueMean, posteriorMean, Math.Abs(trueMean * 0.05), $"Posterior mean curve {i} is incorrect.");
            Assert.AreEqual(trueLower, lowerCI, Math.Abs(trueLower * 0.05), $"Lower CI {i} is incorrect.");
            Assert.AreEqual(trueUpper, upperCI, Math.Abs(trueUpper * 0.05), $"Upper CI {i} is incorrect.");
        }

    }

    /// <summary>
    /// Example 4: Tests LPIII analysis with binomial censored historical flood information.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test verifies the incorporation of historical flood information using binomial censoring.
    /// The historical record indicates that during the 117-year period prior to systematic records (1820-1937),
    /// only one flood exceeded the threshold of 12,525 m³/s. This additional information significantly
    /// constrains the upper tail and reduces uncertainty compared to Example 3.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task TestExample4()
    {
        var testData = ArrFlikeData.Example4();

        // Create UnivariateDistribution model with LPIII distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.LogPearsonTypeIII);
        model.UseJeffreysRuleForScale = true; // Flike uses Jeffreys rule for scale parameter prior

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(testData.AEPValues);
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMean;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (LogPearsonTypeIII)model.Distribution;

        // Test frequency curve outputs
        for (int i = 0; i < testData.AEPValues.Length; i++)
        {
            double posteriorMean = analysis.AnalysisResults!.ModeCurve![i];
            double lowerCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            double upperCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            double trueMean = testData.TruePosteriorMean[i];
            double trueLower = testData.TrueLowerCI[i];
            double trueUpper = testData.TrueUpperCI[i];
            Assert.AreEqual(trueMean, posteriorMean, Math.Abs(trueMean * 0.05), $"Posterior mean curve {i} is incorrect.");
            Assert.AreEqual(trueLower, lowerCI, Math.Abs(trueLower * 0.05), $"Lower CI {i} is incorrect.");
            Assert.AreEqual(trueUpper, upperCI, Math.Abs(trueUpper * 0.05), $"Upper CI {i} is incorrect.");
        }

    }

    /// <summary>
    /// Example 5: Tests LPIII analysis with regional skew information as an informative prior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test verifies the use of regional skew information derived from a regional skew analysis.
    /// The regional skew was estimated to be 0.00 with a mean square error (MSE) of 0.09, which is
    /// incorporated into the Bayesian analysis by setting the prior for the skew parameter to be
    /// Normally distributed with mean 0.00 and standard deviation 0.30. This helps stabilize
    /// the skew parameter estimate, particularly for shorter records.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task TestExample5()
    {
        var testData = ArrFlikeData.Example5();

        // Create UnivariateDistribution model with LPIII distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.LogPearsonTypeIII);
        model.UseJeffreysRuleForScale = true; // Flike uses Jeffreys rule for scale parameter prior
        model.Parameters[2].PriorDistribution = testData.RegionalSkewPrior; // Set regional skew prior

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(testData.AEPValues);
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMean;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (LogPearsonTypeIII)model.Distribution;

        // Test frequency curve outputs
        for (int i = 0; i < testData.AEPValues.Length; i++)
        {
            double posteriorMean = analysis.AnalysisResults!.ModeCurve![i];
            double lowerCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            double upperCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            double trueMean = testData.TruePosteriorMean[i];
            double trueLower = testData.TrueLowerCI[i];
            double trueUpper = testData.TrueUpperCI[i];
            Assert.AreEqual(trueMean, posteriorMean, Math.Abs(trueMean * 0.05), $"Posterior mean curve {i} is incorrect.");
            Assert.AreEqual(trueLower, lowerCI, Math.Abs(trueLower * 0.05), $"Lower CI {i} is incorrect.");
            Assert.AreEqual(trueUpper, upperCI, Math.Abs(trueUpper * 0.05), $"Upper CI {i} is incorrect.");
        }

    }

    /// <summary>
    /// Example 6a: Tests GEV flood frequency analysis without low outlier removal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test demonstrates the effect of low outliers on flood frequency analysis using the
    /// Wimmera River at Glynwylln dataset. The dataset exhibits significant positive skewness
    /// with many near-zero values. When low outliers are not censored, the GEV distribution
    /// produces very wide credible intervals due to the influence of these extreme low values.
    /// </para>
    /// <para>
    /// This test serves as a baseline for comparison with Example 6b, which demonstrates
    /// the improvement achieved by censoring low outliers using the Multiple Grubbs-Beck Test (MGBT).
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task TestExample6a()
    {
        var testData = ArrFlikeData.Example6a();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = true; // Flike uses Jeffreys rule for scale parameter prior

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(testData.AEPValues);
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMean;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // Test frequency curve outputs
        for (int i = 0; i < testData.AEPValues.Length; i++)
        {
            double posteriorMean = analysis.AnalysisResults!.ModeCurve![i];
            double lowerCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            double upperCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            double trueMean = testData.TruePosteriorMean[i];
            double trueLower = testData.TrueLowerCI[i];
            double trueUpper = testData.TrueUpperCI[i];
            Assert.AreEqual(trueMean, posteriorMean, Math.Abs(trueMean * 0.05), $"Posterior mean curve {i} is incorrect.");
            Assert.AreEqual(trueLower, lowerCI, Math.Abs(trueLower * 0.05), $"Lower CI {i} is incorrect.");
            Assert.AreEqual(trueUpper, upperCI, Math.Abs(trueUpper * 0.05), $"Upper CI {i} is incorrect.");
        }

    }

    /// <summary>
    /// Example 6b: Tests GEV flood frequency analysis with low outliers censored using MGBT.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test demonstrates the benefit of censoring low outliers using the Multiple Grubbs-Beck Test (MGBT).
    /// The MGBT identifies potentially influential low observations (PILOs) that may unduly influence
    /// the frequency curve shape. These low outliers are replaced with a left-censored threshold,
    /// and the GEV distribution is fit to the modified data.
    /// </para>
    /// <para>
    /// Compared to Example 6a (no censoring), this approach produces dramatically narrower credible
    /// intervals, particularly for extreme quantiles. This approach is consistent with Bulletin 17C
    /// (U.S. Geological Survey, 2018) and is implemented in both HEC-SSP and RMC-BestFit.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [TestMethod]
    public async Task TestExample6b()
    {
        var testData = ArrFlikeData.Example6b();

        // Create UnivariateDistribution model with GEV distribution
        var model = new UnivariateDistribution(testData.DataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        model.UseJeffreysRuleForScale = true; // Flike uses Jeffreys rule for scale parameter prior

        // Create Univariate Analysis
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.AddRange(testData.AEPValues);
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMean;
        await analysis.RunAsync();

        // Assert that the analysis was run successfully
        Assert.AreEqual(true, analysis.IsEstimated, "Analysis failed.");

        // Assert that the MAP parameters are close to the true parameters
        model.SetParameterValues(analysis.BayesianAnalysis.Results!.MAP.Values);
        var dist = (GeneralizedExtremeValue)model.Distribution;

        // Test frequency curve outputs
        for (int i = 0; i < testData.AEPValues.Length; i++)
        {
            double posteriorMean = analysis.AnalysisResults!.ModeCurve![i];
            double lowerCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 0];
            double upperCI = analysis.AnalysisResults!.ConfidenceIntervals![i, 1];
            double trueMean = testData.TruePosteriorMean[i];
            double trueLower = testData.TrueLowerCI[i];
            double trueUpper = testData.TrueUpperCI[i];
            Assert.AreEqual(trueMean, posteriorMean, Math.Abs(trueMean * 0.05), $"Posterior mean curve {i} is incorrect.");
            Assert.AreEqual(trueLower, lowerCI, Math.Abs(trueLower * 0.05), $"Lower CI {i} is incorrect.");
            Assert.AreEqual(trueUpper, upperCI, Math.Abs(trueUpper * 0.05), $"Upper CI {i} is incorrect.");
        }

    }
}
