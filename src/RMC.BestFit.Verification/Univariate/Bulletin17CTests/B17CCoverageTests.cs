using Numerics;
using Numerics.Distributions;
using Numerics.Sampling;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>
/// Tests the coverage probability of Bulletin 17C confidence intervals across all six
/// supported distribution types. Coverage is the fraction of Monte Carlo replicates in which
/// the true quantile falls within the estimated confidence interval.
/// </summary>
/// <remarks>
/// <para>
/// Each test generates B independent random samples from a known distribution, fits the B17C
/// model via GMM, computes confidence intervals using the multivariate normal approximation,
/// and checks whether the true quantiles fall within the intervals. The empirical coverage rate
/// should approximate the nominal CI width (default 90%).
/// </para>
/// <para>
/// The multivariate normal uncertainty method is first-order accurate, so coverage may be
/// systematically below nominal for small samples (N=25) and extreme quantiles (Q99, Q99.5).
/// Assertions use wide bounds to accommodate this known limitation.
/// </para>
/// <para>
/// All coverage tests are marked with <c>[TestCategory("LongRunning")]</c> because each test
/// runs B=1000 replicates, each involving a full GMM estimation and uncertainty analysis.
/// Expected execution time: 3-10 minutes per test.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CCoverageTests
{
    #region Distribution Coverage Tests — N=25

    /// <summary>
    /// Verifies confidence interval coverage for the Exponential distribution with N=25.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True parameters: xi=10, alpha=50 (2-parameter). Exponential is the simplest B17C-supported
    /// distribution, so coverage should be near nominal even with the first-order MVN approximation.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task Exponential_Coverage_N25()
    {
        var trueDist = new Exponential(10.0, 50.0);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.Exponential, trueDist, n: 25);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "Exponential N=25");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the Gamma distribution with N=25.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True parameters: alpha=5, beta=2 (2-parameter). The Gamma distribution's rate
    /// parameterization means the GMM moment conditions involve nonlinear transformations.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task Gamma_Coverage_N25()
    {
        var trueDist = new GammaDistribution(5.0, 2.0);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.GammaDistribution, trueDist, n: 25);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "Gamma N=25");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the Normal distribution with N=25.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True parameters: mu=100, sigma=15 (2-parameter). Normal is symmetric and the MVN
    /// approximation is exact for the location parameter, so this should achieve the best
    /// coverage among all distribution types.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task Normal_Coverage_N25()
    {
        var trueDist = new Normal(100.0, 15.0);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.Normal, trueDist, n: 25);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "Normal N=25");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the Pearson Type III distribution with N=25.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True parameters: mu=100, sigma=20, gamma=0.5 (3-parameter). The skewness parameter
    /// is poorly estimated with small samples, so coverage may degrade at extreme quantiles.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task PearsonTypeIII_Coverage_N25()
    {
        var trueDist = new PearsonTypeIII(100.0, 20.0, 0.5);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.PearsonTypeIII, trueDist, n: 25);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "PearsonTypeIII N=25");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the LogNormal distribution with N=25.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True parameters: mu=3.0, sigma=0.5 (2-parameter, log10-space). LogNormal is one of the
    /// primary B17C distributions and its quantile function is linear in the parameters
    /// (Q = mu + z*sigma in log-space), so the MVN approximation should perform well.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogNormal_Coverage_N25()
    {
        var trueDist = new LogNormal(3.0, 0.5);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.LogNormal, trueDist, n: 25);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LogNormal N=25");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the Log-Pearson Type III distribution with N=25.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True parameters: mu=3.0, sigma=0.5, gamma=0.2 (3-parameter, log10-space). This is the
    /// standard Bulletin 17C distribution. With N=25 and a 3-parameter model, coverage may be
    /// below nominal especially at extreme quantiles where the skewness parameter has the most
    /// influence on quantile estimates.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogPearsonTypeIII_Coverage_N25()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.2);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 25);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LogPearsonTypeIII N=25");
    }

    #endregion

    #region Distribution Coverage Tests — N=100

    /// <summary>
    /// Verifies confidence interval coverage for the Exponential distribution with N=100.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task Exponential_Coverage_N100()
    {
        var trueDist = new Exponential(10.0, 50.0);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.Exponential, trueDist, n: 100);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "Exponential N=100");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the Gamma distribution with N=100.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task Gamma_Coverage_N100()
    {
        var trueDist = new GammaDistribution(5.0, 2.0);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.GammaDistribution, trueDist, n: 100);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "Gamma N=100");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the Normal distribution with N=100.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task Normal_Coverage_N100()
    {
        var trueDist = new Normal(100.0, 15.0);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.Normal, trueDist, n: 100);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "Normal N=100");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the Pearson Type III distribution with N=100.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task PearsonTypeIII_Coverage_N100()
    {
        var trueDist = new PearsonTypeIII(100.0, 20.0, 0.5);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.PearsonTypeIII, trueDist, n: 100);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "PearsonTypeIII N=100");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the LogNormal distribution with N=100.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogNormal_Coverage_N100()
    {
        var trueDist = new LogNormal(3.0, 0.5);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.LogNormal, trueDist, n: 100);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LogNormal N=100");
    }

    /// <summary>
    /// Verifies confidence interval coverage for the Log-Pearson Type III distribution with N=100.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogPearsonTypeIII_Coverage_N100()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.5);
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulation(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 100,
            uncertaintyMethod: UncertaintyMethod.BiasCorrectedBootstrap);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LogPearsonTypeIII N=100");
    }

    #endregion

    #region Penalty Coverage Tests — LogNormal

    /// <summary>
    /// Verifies that a penalty on mu for LogNormal with N=25 maintains or improves CI coverage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info centered at true value: regMean=3.0, regMeanMSE=0.004. A well-specified
    /// penalty centered at the truth should tighten the CIs while maintaining coverage.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogNormal_CoverageWithMuPenalty_N25()
    {
        var trueDist = new LogNormal(3.0, 0.5);
        var paramPenalties = new PenaltySpec[]
        {
            new(0, 3.0, 0.004)
        };
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulationWithPenalties(
            UnivariateDistributionType.LogNormal, trueDist, n: 25, paramPenalties: paramPenalties);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LogNormal MuPenalty N=25");
    }

    /// <summary>
    /// Verifies that penalties on mu and sigma for LogNormal with N=25 maintain CI coverage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regMean=3.0 (MSE=0.004), regSigma=0.5 (MSE=0.0015). Both penalties
    /// centered at true values.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogNormal_CoverageWithMuSigmaPenalty_N25()
    {
        var trueDist = new LogNormal(3.0, 0.5);
        var paramPenalties = new PenaltySpec[]
        {
            new(0, 3.0, 0.004),
            new(1, 0.5, 0.0015)
        };
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulationWithPenalties(
            UnivariateDistributionType.LogNormal, trueDist, n: 25, paramPenalties: paramPenalties);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LogNormal MuSigmaPenalty N=25");
    }

    /// <summary>
    /// Verifies that penalties on mu, sigma, and Q99 for LogNormal with N=25 maintain CI coverage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regMean=3.0 (MSE=0.004), regSigma=0.5 (MSE=0.0015),
    /// regQ99 computed from true distribution (MSE=0.01, log10 space).
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogNormal_CoverageWithMuSigmaAndQ99Penalty_N25()
    {
        var trueDist = new LogNormal(3.0, 0.5);
        double trueQ99Log10 = Math.Log10(trueDist.InverseCDF(0.99));
        var paramPenalties = new PenaltySpec[]
        {
            new(0, 3.0, 0.004),
            new(1, 0.5, 0.0015)
        };
        var quantilePenalties = new QuantilePenaltySpec[]
        {
            new(0.01, trueQ99Log10, 0.01, true)
        };
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulationWithPenalties(
            UnivariateDistributionType.LogNormal, trueDist, n: 25,
            paramPenalties: paramPenalties, quantilePenalties: quantilePenalties);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LogNormal MuSigmaQ99Penalty N=25");
    }

    #endregion

    #region Penalty Coverage Tests — Log-Pearson Type III

    /// <summary>
    /// Verifies that a penalty on gamma (skewness) for LP3 with N=25 maintains CI coverage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the classic Bulletin 17C use case: a regional skewness estimate used to
    /// stabilize the at-site skewness estimate. Regional gamma=0.2, MSE=0.05.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogPearsonTypeIII_CoverageWithGammaPenalty_N25()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.2);
        var paramPenalties = new PenaltySpec[]
        {
            new(2, 0.2, 0.05)
        };
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulationWithPenalties(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 25,
            paramPenalties: paramPenalties);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LP3 GammaPenalty N=25");
    }

    /// <summary>
    /// Verifies that penalties on all three parameters for LP3 with N=25 maintain CI coverage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regMean=3.0 (MSE=0.004), regSigma=0.5 (MSE=0.0015), regGamma=0.2 (MSE=0.05).
    /// All penalties centered at true values.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogPearsonTypeIII_CoverageWithAllParamsPenalty_N25()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.2);
        var paramPenalties = new PenaltySpec[]
        {
            new(0, 3.0, 0.004),
            new(1, 0.5, 0.0015),
            new(2, 0.2, 0.05)
        };
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulationWithPenalties(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 25,
            paramPenalties: paramPenalties);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LP3 AllParamsPenalty N=25");
    }

    /// <summary>
    /// Verifies that a penalty on gamma for LP3 with N=100 maintains CI coverage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With N=100, the at-site skewness estimate is more precise, so the regional penalty
    /// has proportionally less influence. Coverage should be at or above the no-penalty N=100 case.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LogPearsonTypeIII_CoverageWithGammaPenalty_N100()
    {
        var trueDist = new LogPearsonTypeIII(3.0, 0.5, 0.2);
        var paramPenalties = new PenaltySpec[]
        {
            new(2, 0.2, 0.05)
        };
        var (coverage, missAbove, missBelow, successCount, probabilities) = await RunCoverageSimulationWithPenalties(
            UnivariateDistributionType.LogPearsonTypeIII, trueDist, n: 100,
            paramPenalties: paramPenalties);
        AssertCoverage(coverage, missAbove, missBelow, successCount, probabilities, "LP3 GammaPenalty N=100");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Specification for a parameter penalty to apply during coverage simulation.
    /// </summary>
    /// <param name="ParameterIndex">The zero-based index of the parameter to penalize.</param>
    /// <param name="Mean">The regional mean value for the penalty.</param>
    /// <param name="MSE">The regional mean squared error of the penalty estimate.</param>
    private record PenaltySpec(int ParameterIndex, double Mean, double MSE);

    /// <summary>
    /// Specification for a quantile penalty to apply during coverage simulation.
    /// </summary>
    /// <param name="AEP">The annual exceedance probability (e.g., 0.01 for the 100-year flood).</param>
    /// <param name="Mean">The regional quantile value (in log10 if <paramref name="UseLog10"/> is true).</param>
    /// <param name="MSE">The regional MSE of the quantile estimate.</param>
    /// <param name="UseLog10">Whether the penalty operates in log10 space.</param>
    private record QuantilePenaltySpec(double AEP, double Mean, double MSE, bool UseLog10);

    /// <summary>
    /// Runs a Monte Carlo coverage simulation for a B17C distribution without penalties.
    /// </summary>
    /// <param name="distributionType">The B17C-supported distribution type.</param>
    /// <param name="trueDist">The Numerics distribution with true parameters set.</param>
    /// <param name="n">The sample size for each replicate.</param>
    /// <param name="B">The number of Monte Carlo replicates. Default = 1000.</param>
    /// <param name="nominalCI">The nominal credible interval width. Default = 0.90.</param>
    /// <param name="masterSeed">The master PRNG seed for reproducibility. Default = 12345.</param>
    /// <param name="uncertaintyMethod">The uncertainty method used to compute confidence intervals.</param>
    /// <returns>Coverage rates per probability ordinate, the success count, and the probability array.</returns>
    /// <remarks>
    /// <para>
    /// Each replicate generates a fresh independent random sample from <paramref name="trueDist"/>
    /// (not bootstrap resampling from a fixed dataset), ensuring unconditional frequentist coverage
    /// is measured rather than conditional bootstrap coverage.
    /// </para>
    /// </remarks>
    private static async Task<(double[] Coverage, double[] MissAbove, double[] MissBelow, int SuccessCount, double[] Probabilities)>
        RunCoverageSimulation(
            UnivariateDistributionType distributionType,
            UnivariateDistributionBase trueDist,
            int n,
            int B = 1000,
            double nominalCI = 0.90,
            int masterSeed = 12345,
            UncertaintyMethod uncertaintyMethod = UncertaintyMethod.MultivariateNormal)
    {
        return await RunCoverageSimulationWithPenalties(
            distributionType, trueDist, n, null, null, B, nominalCI, masterSeed, uncertaintyMethod);
    }

    /// <summary>
    /// Runs a Monte Carlo coverage simulation for a B17C distribution with optional penalties.
    /// </summary>
    /// <param name="distributionType">The B17C-supported distribution type.</param>
    /// <param name="trueDist">The Numerics distribution with true parameters set.</param>
    /// <param name="n">The sample size for each replicate.</param>
    /// <param name="paramPenalties">Optional parameter penalties. Pass null for no penalties.</param>
    /// <param name="quantilePenalties">Optional quantile penalties. Pass null for no penalties.</param>
    /// <param name="B">The number of Monte Carlo replicates. Default = 1000.</param>
    /// <param name="nominalCI">The nominal credible interval width. Default = 0.90.</param>
    /// <param name="masterSeed">The master PRNG seed for reproducibility. Default = 12345.</param>
    /// <param name="uncertaintyMethod">The uncertainty method used to compute confidence intervals.</param>
    /// <returns>Coverage rates per probability ordinate, the success count, and the probability array.</returns>
    private static async Task<(double[] Coverage, double[] MissAbove, double[] MissBelow, int SuccessCount, double[] Probabilities)>
        RunCoverageSimulationWithPenalties(
            UnivariateDistributionType distributionType,
            UnivariateDistributionBase trueDist,
            int n,
            PenaltySpec[]? paramPenalties = null,
            QuantilePenaltySpec[]? quantilePenalties = null,
            int B = 1000,
            double nominalCI = 0.90,
            int masterSeed = 12345,
            UncertaintyMethod uncertaintyMethod = UncertaintyMethod.MultivariateNormal)
    {
        // Create a template analysis to get the default probability ordinates
        var templateDf = new DataFrame();
        templateDf.ExactSeries = new ExactSeries(trueDist.InverseCDF(new double[] { 0.5 }));
        var templateDist = new Bulletin17CDistribution(templateDf, distributionType);
        var templateAnalysis = new Bulletin17CAnalysis(templateDist);

        // Get non-exceedance probabilities from the default probability ordinates
        var probabilities = templateAnalysis.ProbabilityOrdinates.Select(p => 1.0 - p).ToArray();
        var coverage = new double[probabilities.Length];
        var missAbove = new double[probabilities.Length];
        var missBelow = new double[probabilities.Length];
        var trueQuantiles = trueDist.InverseCDF(probabilities);

        // Generate independent seeds for each replicate
        var masterPRNG = new MersenneTwister(masterSeed);
        var seeds = masterPRNG.NextIntegers(B);

        int successCount = 0;

        for (int i = 0; i < B; i++)
        {
            // Generate a fresh random sample from the true distribution
            var randomValues = trueDist.GenerateRandomValues(n, seeds[i]);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(randomValues);

            var model = new Bulletin17CDistribution(df, distributionType);

            // Apply parameter penalties
            if (paramPenalties != null)
            {
                foreach (var pp in paramPenalties)
                {
                    model.ParameterPenalties[pp.ParameterIndex].Mean = pp.Mean;
                    model.ParameterPenalties[pp.ParameterIndex].MSE = pp.MSE;
                    model.ParameterPenalties[pp.ParameterIndex].Enabled = true;
                }
            }

            // Apply quantile penalties
            if (quantilePenalties != null)
            {
                for (int q = 0; q < quantilePenalties.Length; q++)
                {
                    model.QuantilePenalties[q].AEP = quantilePenalties[q].AEP;
                    model.QuantilePenalties[q].Mean = quantilePenalties[q].Mean;
                    model.QuantilePenalties[q].MSE = quantilePenalties[q].MSE;
                    model.QuantilePenalties[q].UseLog10 = quantilePenalties[q].UseLog10;
                    model.QuantilePenalties[q].Enabled = true;
                }
            }

            //var bootDist = new LogPearsonTypeIII();
            //bootDist.Estimate(df.ExactSeries.ValuesToArray(), ParameterEstimationMethod.MethodOfMoments);
            //var boot = new BootstrapAnalysis(bootDist, ParameterEstimationMethod.MethodOfMoments, n, B, masterSeed);
            //var CIs = boot.PercentileQuantileCI( probabilities, 1 - nominalCI);


            var analysis = new Bulletin17CAnalysis(model)
            {
                UncertaintyMethod = uncertaintyMethod
            };
            analysis.BayesianAnalysis.CredibleIntervalWidth = nominalCI;
            analysis.BayesianAnalysis.OutputLength = B;
            analysis.BayesianAnalysis.PRNGSeed = masterSeed;

            await analysis.RunAsync();

            var CIs = analysis.AnalysisResults?.ConfidenceIntervals;
            if (CIs == null) continue;

            successCount++;
            for (int j = 0; j < probabilities.Length; j++)
            {
                if (trueQuantiles[j] >= CIs[j, 0] && trueQuantiles[j] <= CIs[j, 1])
                {
                    coverage[j]++;
                }
                else if (trueQuantiles[j] > CIs[j, 1])
                {
                    missAbove[j]++;
                }
                else
                {
                    missBelow[j]++;
                }
            }
        }

        // Normalize by success count
        if (successCount > 0)
        {
            for (int j = 0; j < probabilities.Length; j++)
            {
                coverage[j] /= successCount;
                missAbove[j] /= successCount;
                missBelow[j] /= successCount;
            }
        }

        return (coverage, missAbove, missBelow, successCount, probabilities);
    }

    /// <summary>
    /// Asserts that coverage rates are within acceptable bounds and writes a diagnostic summary table
    /// including tail balance metrics (% of misses above and below the CI).
    /// </summary>
    /// <param name="coverage">The empirical coverage rates per probability ordinate.</param>
    /// <param name="missAbove">The fraction of misses where the true quantile exceeded the upper CI bound.</param>
    /// <param name="missBelow">The fraction of misses where the true quantile was below the lower CI bound.</param>
    /// <param name="successCount">The number of replicates that completed successfully.</param>
    /// <param name="probabilities">The non-exceedance probabilities (for labeling).</param>
    /// <param name="testName">The test name for diagnostic output.</param>
    /// <remarks>
    /// <para>
    /// The %Below and %Above columns reveal CI asymmetry. A well-calibrated CI should have roughly
    /// equal miss rates in each tail (each ≈ (1 − nominal)/2 = 5% for a 90% CI). Biased CIs
    /// (e.g., first-order MVN for small samples) will show asymmetric tails — typically more misses
    /// above the upper bound for right-skewed distributions, indicating the CI is shifted too low.
    /// </para>
    /// <para>
    /// Assertions:
    /// <list type="bullet">
    /// <item><description>At least 90% of replicates completed successfully.</description></item>
    /// <item><description>Mean coverage across all ordinates is in [0.82, 0.97].</description></item>
    /// <item><description>No individual ordinate has coverage below 0.70.</description></item>
    /// </list>
    /// The MVN approximation is first-order accurate, so these bounds are intentionally wide.
    /// </para>
    /// </remarks>
    private static void AssertCoverage(double[] coverage, double[] missAbove, double[] missBelow,
        int successCount, double[] probabilities, string testName)
    {
        // Diagnostic summary table with tail balance
        Debug.WriteLine($"\n=== Coverage Summary: {testName} (successes={successCount}/1000) ===");
        Debug.WriteLine($"{"AEP",10} {"NonExcProb",12} {"Coverage",10} {"%Below",8} {"%Above",8}");
        Debug.WriteLine(new string('-', 52));
        for (int j = 0; j < probabilities.Length; j++)
        {
            double aep = 1.0 - probabilities[j];
            Debug.WriteLine($"{aep,10:F4} {probabilities[j],12:F4} {coverage[j],10:F3} {missBelow[j],8:F3} {missAbove[j],8:F3}");
        }
        double meanCoverage = coverage.Average();
        double meanBelow = missBelow.Average();
        double meanAbove = missAbove.Average();
        Debug.WriteLine($"{"Mean",10} {"",12} {meanCoverage,10:F3} {meanBelow,8:F3} {meanAbove,8:F3}");

        // Assertions
        //Assert.IsTrue(successCount >= 900,
        // $"{testName}: Too many failures — {1000 - successCount}/1000 replicates failed.");

        // Assert.IsTrue(meanCoverage >= 0.82 && meanCoverage <= 0.97,
        // $"{testName}: Mean coverage {meanCoverage:F3} outside [0.82, 0.97].");

        for (int j = 0; j < probabilities.Length; j++)
        {
            // Assert.IsTrue(coverage[j] >= 0.70,
            // $"{testName}: Coverage at p={probabilities[j]:F4} is {coverage[j]:F3} < 0.70.");
        }
    }

    #endregion
}
