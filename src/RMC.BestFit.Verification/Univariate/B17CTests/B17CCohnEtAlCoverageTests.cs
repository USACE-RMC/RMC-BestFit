using Numerics;
using Numerics.Distributions;
using Numerics.Sampling;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Recreates Table 3 from Cohn, Lane, and Stedinger (2001) — a systematic Monte Carlo
/// coverage probability study for Log-Pearson Type III confidence intervals across a grid
/// of skewness coefficients, systematic record lengths, and historical record lengths.
/// </summary>
/// <remarks>
/// <para>
/// The test grid covers 30 scenarios:
/// <list type="bullet">
/// <item><description>Skewness (gamma): -1.0, -0.5, 0.0, 0.5, 1.0</description></item>
/// <item><description>Systematic record length (Ns): 25, 100</description></item>
/// <item><description>Historical record length (Nh): 0 (systematic-only), 50, 150</description></item>
/// </list>
/// </para>
/// <para>
/// LP3 parameters are fixed at mu=3.0, sigma=0.5 (log10-space) across all scenarios, with
/// only the skewness coefficient varying. The perception threshold for historical scenarios
/// is set at the 90th percentile of the true distribution.
/// </para>
/// <para>
/// Currently uses <see cref="UncertaintyMethod.MultivariateNormal"/> which is only first-order
/// accurate. Coverage is expected to be below nominal, especially for small samples and extreme
/// quantiles. Assertions verify only that the test pipeline runs successfully; diagnostic output
/// is provided for comparison with the published Table 3 results.
/// </para>
/// <para>
/// <b>Reference:</b> Cohn, T.A., Lane, W.L., Stedinger, J.R. (2001). Confidence intervals for
/// Expected Moments Algorithm flood quantile estimates. Water Resources Research, 37(6):1695-1706.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CCohnEtAlCoverageTests
{
    #region Parameterized Coverage Tests

    /// <summary>
    /// Runs the Cohn et al. LP3 coverage verification scenarios.
    /// </summary>
    /// <param name="gamma">The skew coefficient used by the scenario.</param>
    /// <param name="nSys">The number of systematic observations.</param>
    /// <param name="nHist">The number of historical observations.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// Data rows cover systematic-only and historical-record cases across the LP3 skew values used by the coverage study.
    /// </remarks>
    [DataTestMethod]
    [TestCategory("LongRunning")]
    [DataRow(-1.0, 25, 0, DisplayName = "LP3 γ=-1.0 Ns=25 Nh=0")]
    [DataRow(-1.0, 25, 50, DisplayName = "LP3 γ=-1.0 Ns=25 Nh=50")]
    [DataRow(-1.0, 25, 150, DisplayName = "LP3 γ=-1.0 Ns=25 Nh=150")]
    // gamma = -1.0, Ns = 100
    [DataRow(-1.0, 100, 0, DisplayName = "LP3 γ=-1.0 Ns=100 Nh=0")]
    [DataRow(-1.0, 100, 50, DisplayName = "LP3 γ=-1.0 Ns=100 Nh=50")]
    [DataRow(-1.0, 100, 150, DisplayName = "LP3 γ=-1.0 Ns=100 Nh=150")]
    // gamma = -0.5, Ns = 25
    [DataRow(-0.5, 25, 0, DisplayName = "LP3 γ=-0.5 Ns=25 Nh=0")]
    [DataRow(-0.5, 25, 50, DisplayName = "LP3 γ=-0.5 Ns=25 Nh=50")]
    [DataRow(-0.5, 25, 150, DisplayName = "LP3 γ=-0.5 Ns=25 Nh=150")]
    // gamma = -0.5, Ns = 100
    [DataRow(-0.5, 100, 0, DisplayName = "LP3 γ=-0.5 Ns=100 Nh=0")]
    [DataRow(-0.5, 100, 50, DisplayName = "LP3 γ=-0.5 Ns=100 Nh=50")]
    [DataRow(-0.5, 100, 150, DisplayName = "LP3 γ=-0.5 Ns=100 Nh=150")]
    // gamma = 0.0, Ns = 25
    [DataRow(0.0, 25, 0, DisplayName = "LP3 γ=0.0 Ns=25 Nh=0")]
    [DataRow(0.0, 25, 50, DisplayName = "LP3 γ=0.0 Ns=25 Nh=50")]
    [DataRow(0.0, 25, 150, DisplayName = "LP3 γ=0.0 Ns=25 Nh=150")]
    // gamma = 0.0, Ns = 100
    [DataRow(0.0, 100, 0, DisplayName = "LP3 γ=0.0 Ns=100 Nh=0")]
    [DataRow(0.0, 100, 50, DisplayName = "LP3 γ=0.0 Ns=100 Nh=50")]
    [DataRow(0.0, 100, 150, DisplayName = "LP3 γ=0.0 Ns=100 Nh=150")]
    // gamma = 0.5, Ns = 25
    [DataRow(0.5, 25, 0, DisplayName = "LP3 γ=0.5 Ns=25 Nh=0")]
    [DataRow(0.5, 25, 50, DisplayName = "LP3 γ=0.5 Ns=25 Nh=50")]
    [DataRow(0.5, 25, 150, DisplayName = "LP3 γ=0.5 Ns=25 Nh=150")]
    // gamma = 0.5, Ns = 100
    [DataRow(0.5, 100, 0, DisplayName = "LP3 γ=0.5 Ns=100 Nh=0")]
    [DataRow(0.5, 100, 50, DisplayName = "LP3 γ=0.5 Ns=100 Nh=50")]
    [DataRow(0.5, 100, 150, DisplayName = "LP3 γ=0.5 Ns=100 Nh=150")]
    // gamma = 1.0, Ns = 25
    [DataRow(1.0, 25, 0, DisplayName = "LP3 γ=1.0 Ns=25 Nh=0")]
    [DataRow(1.0, 25, 50, DisplayName = "LP3 γ=1.0 Ns=25 Nh=50")]
    [DataRow(1.0, 25, 150, DisplayName = "LP3 γ=1.0 Ns=25 Nh=150")]
    // gamma = 1.0, Ns = 100
    [DataRow(1.0, 100, 0, DisplayName = "LP3 γ=1.0 Ns=100 Nh=0")]
    [DataRow(1.0, 100, 50, DisplayName = "LP3 γ=1.0 Ns=100 Nh=50")]
    [DataRow(1.0, 100, 150, DisplayName = "LP3 γ=1.0 Ns=100 Nh=150")]
    public async Task CohnEtAl_LP3_Coverage(double gamma, int nSys, int nHist)
    {
        // LP3 parameters: fixed mu and sigma, variable skewness
        double mu = 3.0;
        double sigma = 0.5;

        // Handle gamma=0 case: LP3 with gamma=0 reduces to LogNormal
        // Use a very small gamma to avoid degenerate LP3
        double effectiveGamma = Math.Abs(gamma) < 1e-10 ? 0.001 : gamma;
        var trueDist = new LogPearsonTypeIII(mu, sigma, effectiveGamma);

        // Perception threshold: 90th percentile of the true distribution
        double perceptionThreshold = trueDist.InverseCDF(0.90);

        int B = 1000;
        double nominalCI = 0.90;
        int masterSeed = 12345;

        // Target AEPs for Table 3 output
        double[] targetAEPs = { 0.50, 0.10, 0.04, 0.02, 0.01, 0.005, 0.002 };
        double[] targetProbs = targetAEPs.Select(a => 1.0 - a).ToArray();
        double[] trueQuantiles = trueDist.InverseCDF(targetProbs);

        // Generate seeds
        var masterPRNG = new MersenneTwister(masterSeed);
        var seeds = masterPRNG.NextIntegers(B);

        var coverage = new double[targetProbs.Length];
        var missAbove = new double[targetProbs.Length];
        var missBelow = new double[targetProbs.Length];
        int successCount = 0;

        for (int i = 0; i < B; i++)
        {
            var prng = new MersenneTwister(seeds[i]);

            // Generate data frame based on scenario
            DataFrame df;
            if (nHist == 0)
            {
                df = GenerateSystematicDataFrame(trueDist, nSys, prng);
            }
            else
            {
                df = GenerateHistoricalDataFrame(trueDist, nSys, nHist, perceptionThreshold, prng);
            }

            var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
            var analysis = new Bulletin17CAnalysis(model)
            {
                UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
                // TODO: Add LinkedMultivariateNormal and BiasCorrectedBootstrap once ported
            };
            analysis.BayesianAnalysis.CredibleIntervalWidth = nominalCI;
            analysis.BayesianAnalysis.OutputLength = B;

            // Override probability ordinates to match Table 3 AEPs
            analysis.ProbabilityOrdinates.Clear();
            foreach (double aep in targetAEPs)
                analysis.ProbabilityOrdinates.Add(aep);

            await analysis.RunAsync();

            var CIs = analysis.AnalysisResults?.ConfidenceIntervals;
            if (CIs == null) continue;

            successCount++;
            for (int j = 0; j < targetProbs.Length; j++)
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

        // Normalize
        if (successCount > 0)
        {
            for (int j = 0; j < coverage.Length; j++)
            {
                coverage[j] /= successCount;
                missAbove[j] /= successCount;
                missBelow[j] /= successCount;
            }
        }

        // Output Table 3 row with tail balance
        Debug.WriteLine($"\n=== Cohn et al. Table 3: γ={gamma:F1}, Ns={nSys}, Nh={nHist} " +
                        $"(successes={successCount}/{B}) ===");
        Debug.WriteLine($"{"AEP",10} {"TrueQ",14} {"Coverage",10} {"%Below",8} {"%Above",8}");
        Debug.WriteLine(new string('-', 55));
        for (int j = 0; j < targetAEPs.Length; j++)
        {
            Debug.WriteLine($"{targetAEPs[j],10:F3} {trueQuantiles[j],14:F2} {coverage[j],10:F3} {missBelow[j],8:F3} {missAbove[j],8:F3}");
        }
        double meanCoverage = coverage.Average();
        double meanBelow = missBelow.Average();
        double meanAbove = missAbove.Average();
        Debug.WriteLine($"{"Mean",10} {"",14} {meanCoverage,10:F3} {meanBelow,8:F3} {meanAbove,8:F3}");

        // Assertions — MVN is first-order only, so we only assert pipeline success
        Assert.IsTrue(successCount >= (int)(B * 0.8),
            $"γ={gamma}, Ns={nSys}, Nh={nHist}: Too many failures — " +
            $"{B - successCount}/{B} replicates failed.");

        // TODO: Tighten assertions after LinkedMultivariateNormal and BiasCorrectedBootstrap are ported
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates a DataFrame containing only systematic (exact) data from a random sample.
    /// </summary>
    /// <param name="trueDist">The true distribution to sample from.</param>
    /// <param name="nSys">The systematic record length.</param>
    /// <param name="prng">The pseudo-random number generator.</param>
    /// <returns>A DataFrame with <paramref name="nSys"/> exact observations.</returns>
    private static DataFrame GenerateSystematicDataFrame(
        UnivariateDistributionBase trueDist, int nSys, Random prng)
    {
        var values = trueDist.GenerateRandomValues(nSys, prng.Next());
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(values);
        return df;
    }

    /// <summary>
    /// Generates a DataFrame with systematic data and historical perception threshold data,
    /// following the Cohn et al. (2001) Monte Carlo design for testing EMA confidence intervals.
    /// </summary>
    /// <param name="trueDist">The true distribution to sample from.</param>
    /// <param name="nSys">The systematic record length (recent period).</param>
    /// <param name="nHist">The historical record length (earlier period).</param>
    /// <param name="perceptionThreshold">
    /// The perception threshold value. Floods exceeding this value in the historical period
    /// are assumed to be known (added to ExactSeries); floods below are unobserved and
    /// contribute only to the ThresholdSeries censoring count.
    /// </param>
    /// <param name="prng">The pseudo-random number generator.</param>
    /// <returns>
    /// A DataFrame with:
    /// <list type="bullet">
    /// <item><description>ExactSeries: <paramref name="nSys"/> systematic observations plus any
    /// historical floods exceeding the perception threshold.</description></item>
    /// <item><description>ThresholdSeries: one entry spanning the historical period with
    /// NumberBelow set to the count of non-exceedances.</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// The data layout uses index-based time: the historical period spans indices
    /// [1, nHist], and the systematic period spans indices [nHist+1, nHist+nSys].
    /// This matches the B17C convention where the systematic period is the most recent.
    /// </para>
    /// </remarks>
    private static DataFrame GenerateHistoricalDataFrame(
        UnivariateDistributionBase trueDist, int nSys, int nHist,
        double perceptionThreshold, Random prng)
    {
        var df = new DataFrame();

        // Generate all random values for the combined record
        int nTotal = nSys + nHist;
        var allValues = trueDist.GenerateRandomValues(nTotal, prng.Next());

        // Historical period: indices [1, nHist]
        // Floods exceeding the perception threshold are known (exact data)
        var exactData = new List<ExactData>();
        int numberBelow = 0;

        for (int k = 0; k < nHist; k++)
        {
            int yearIndex = k + 1; // 1-based index
            if (allValues[k] >= perceptionThreshold)
            {
                // Known historical flood — add as exact data
                exactData.Add(new ExactData(yearIndex, allValues[k]));
            }
            else
            {
                // Below perception threshold — censored
                numberBelow++;
            }
        }

        // Systematic period: indices [nHist+1, nHist+nSys]
        for (int k = 0; k < nSys; k++)
        {
            int yearIndex = nHist + k + 1;
            exactData.Add(new ExactData(yearIndex, allValues[nHist + k]));
        }

        df.ExactSeries = new ExactSeries(exactData);

        // Add threshold for the historical period.
        // NumberBelow is auto-derived by DataFrame.ProcessThresholdSeries when the threshold
        // series is assigned: it computes Duration − NumberAbove − (count of exact points in
        // [1, nHist]) = nHist − 0 − (nHist − numberBelow) = numberBelow, matching the counter
        // we'd otherwise assign explicitly.
        if (nHist > 0)
        {
            var threshold = new ThresholdData(1, nHist, perceptionThreshold);
            df.ThresholdSeries = new ThresholdSeries(new List<ThresholdData> { threshold });
        }

        return df;
    }

    #endregion
}
