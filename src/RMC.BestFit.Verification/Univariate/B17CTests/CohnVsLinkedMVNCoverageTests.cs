using Numerics;
using Numerics.Distributions;
using Numerics.Sampling;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Coverage comparison tests: Cohn's adjusted Student-t CIs vs LinkedMVN percentile CIs.
/// </summary>
/// <remarks>
/// <para>
/// For each (n, γ) configuration, generates M=1000 independent LP3 samples, fits B17C via GMM,
/// and checks whether the TRUE quantile falls within each CI type. Reports empirical coverage
/// for both methods at each AEP, plus tail balance (miss above / miss below).
/// </para>
/// <para>
/// True distribution: Log-Pearson Type III with μ=3.0, σ=0.5 in log10-space.
/// Skew values: {-0.5, -0.1, 0.0, 0.1, 0.5}
/// Sample sizes: {25, 100}
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class CohnVsLinkedMVNCoverageTests
{
    private const double Mu = 3.0;
    private const double Sigma = 0.5;
    private const int M = 1000;
    private const double NominalCI = 0.90;
    private const int MasterSeed = 54321;

    #region N=25

    /// <summary>
    /// Verifies <c>LP3_Coverage_N25_Skew_Neg05</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N25_Skew_Neg05()
    {
        await RunDualCoverageTest(25, -0.5);
    }

    /// <summary>
    /// Verifies <c>LP3_Coverage_N25_Skew_Neg01</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N25_Skew_Neg01()
    {
        await RunDualCoverageTest(25, -0.1);
    }

    /// <summary>
    /// Verifies <c>LP3_Coverage_N25_Skew_Zero</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N25_Skew_Zero()
    {
        await RunDualCoverageTest(25, 0.0);
    }

    /// <summary>
    /// Verifies <c>LP3_Coverage_N25_Skew_Pos01</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N25_Skew_Pos01()
    {
        await RunDualCoverageTest(25, 0.1);
    }

    /// <summary>
    /// Verifies <c>LP3_Coverage_N25_Skew_Pos05</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N25_Skew_Pos05()
    {
        await RunDualCoverageTest(25, 0.5);
    }

    #endregion

    #region N=100

    /// <summary>
    /// Verifies <c>LP3_Coverage_N100_Skew_Neg05</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N100_Skew_Neg05()
    {
        await RunDualCoverageTest(100, -0.5);
    }

    /// <summary>
    /// Verifies <c>LP3_Coverage_N100_Skew_Neg01</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N100_Skew_Neg01()
    {
        await RunDualCoverageTest(100, -0.1);
    }

    /// <summary>
    /// Verifies <c>LP3_Coverage_N100_Skew_Zero</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N100_Skew_Zero()
    {
        await RunDualCoverageTest(100, 0.0);
    }

    /// <summary>
    /// Verifies <c>LP3_Coverage_N100_Skew_Pos01</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N100_Skew_Pos01()
    {
        await RunDualCoverageTest(100, 0.1);
    }

    /// <summary>
    /// Verifies <c>LP3_Coverage_N100_Skew_Pos05</c>.
    /// </summary>
    [TestMethod]
    [TestCategory("LongRunning")]
    public async Task LP3_Coverage_N100_Skew_Pos05()
    {
        await RunDualCoverageTest(100, 0.5);
    }

    #endregion

    /// <summary>
    /// Runs a dual coverage simulation comparing Cohn CIs and LinkedMVN CIs.
    /// </summary>
    /// <param name="n">Sample size.</param>
    /// <param name="gamma">True skewness coefficient.</param>
    private async Task RunDualCoverageTest(int n, double gamma)
    {
        string label = $"LP3 (μ={Mu}, σ={Sigma}, γ={gamma}, n={n})";

        // True distribution: LP3 in log10-space → P3 parameters
        // For LP3: parameters are (μ, σ, γ) of the log10-transformed variable
        // The true distribution for generating samples is LP3 with these log-space moments
        double effectiveGamma = Math.Abs(gamma) < 1e-10 ? 0.001 : gamma;
        var trueLogDist = new PearsonTypeIII(Mu, Sigma, effectiveGamma);

        // Get the default probability ordinates from a template analysis
        var templateDf = new DataFrame();
        templateDf.ExactSeries = new ExactSeries(new double[] { 1000.0 });
        var templateModel = new Bulletin17CDistribution(templateDf, UnivariateDistributionType.LogPearsonTypeIII);
        var templateAnalysis = new Bulletin17CAnalysis(templateModel);
        var aeps = templateAnalysis.ProbabilityOrdinates.ToArray();
        int nProb = aeps.Length;

        // True quantiles: LP3 quantile = 10^(P3 quantile in log-space)
        var trueQuantiles = new double[nProb];
        for (int j = 0; j < nProb; j++)
        {
            double nonExceedProb = 1.0 - aeps[j];
            trueQuantiles[j] = Math.Pow(10, trueLogDist.InverseCDF(nonExceedProb));
        }

        // Coverage accumulators
        var covCohn = new double[nProb];
        var covMVN = new double[nProb];
        var missAboveCohn = new double[nProb];
        var missBelowCohn = new double[nProb];
        var missAboveMVN = new double[nProb];
        var missBelowMVN = new double[nProb];
        int successCohn = 0;
        int successMVN = 0;

        // Generate independent seeds
        var masterPRNG = new MersenneTwister(MasterSeed + n * 1000 + (int)(gamma * 100));
        var seeds = masterPRNG.NextIntegers(M);

        for (int i = 0; i < M; i++)
        {
            // Generate LP3 sample: draw from P3 in log-space, exponentiate
            var logValues = trueLogDist.GenerateRandomValues(n, seeds[i]);
            var realValues = new double[n];
            for (int k = 0; k < n; k++)
                realValues[k] = Math.Pow(10, logValues[k]);

            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(realValues);

            var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
            var analysis = new Bulletin17CAnalysis(model)
            {
                UncertaintyMethod = UncertaintyMethod.Bootstrap
            };
            analysis.BayesianAnalysis.CredibleIntervalWidth = NominalCI;
            analysis.BayesianAnalysis.OutputLength = 1000;
            analysis.BayesianAnalysis.PRNGSeed = seeds[i];

            await analysis.RunAsync();

            if (!analysis.IsEstimated) continue;

            // --- LinkedMVN CIs ---
            var mvnCI = analysis.AnalysisResults?.ConfidenceIntervals;
            if (mvnCI != null)
            {
                successMVN++;
                for (int j = 0; j < nProb; j++)
                {
                    if (trueQuantiles[j] >= mvnCI[j, 0] && trueQuantiles[j] <= mvnCI[j, 1])
                        covMVN[j]++;
                    else if (trueQuantiles[j] > mvnCI[j, 1])
                        missAboveMVN[j]++;
                    else
                        missBelowMVN[j]++;
                }
            }

            // --- Cohn CIs ---
            var cohnResult = analysis.ComputeCohnStyleConfidenceIntervals();
            if (cohnResult != null)
            {
                successCohn++;
                for (int j = 0; j < nProb; j++)
                {
                    if (trueQuantiles[j] >= cohnResult.LowerCI[j] && trueQuantiles[j] <= cohnResult.UpperCI[j])
                        covCohn[j]++;
                    else if (trueQuantiles[j] > cohnResult.UpperCI[j])
                        missAboveCohn[j]++;
                    else
                        missBelowCohn[j]++;
                }
            }
        }

        // Normalize
        if (successMVN > 0)
            for (int j = 0; j < nProb; j++) { covMVN[j] /= successMVN; missAboveMVN[j] /= successMVN; missBelowMVN[j] /= successMVN; }
        if (successCohn > 0)
            for (int j = 0; j < nProb; j++) { covCohn[j] /= successCohn; missAboveCohn[j] /= successCohn; missBelowCohn[j] /= successCohn; }

        // Output
        Debug.WriteLine($"\n{"=",-80}");
        Debug.WriteLine($"  {label}: Coverage Comparison (M={M}, {NominalCI * 100}% CI)");
        Debug.WriteLine($"  LinkedMVN successes: {successMVN}/{M}, Cohn successes: {successCohn}/{M}");
        Debug.WriteLine($"{"=",-80}");
        Debug.WriteLine($"{"AEP",10} {"Cohn Cov",10} {"Cohn ↓",8} {"Cohn ↑",8} {"MVN Cov",10} {"MVN ↓",8} {"MVN ↑",8} {"Δ Cov",8}");
        Debug.WriteLine(new string('-', 74));

        for (int j = 0; j < nProb; j++)
        {
            double delta = covCohn[j] - covMVN[j];
            Debug.WriteLine($"{aeps[j],10:G4} {covCohn[j],10:F3} {missBelowCohn[j],8:F3} {missAboveCohn[j],8:F3} " +
                            $"{covMVN[j],10:F3} {missBelowMVN[j],8:F3} {missAboveMVN[j],8:F3} {delta,8:+0.000;-0.000}");
        }

        // --- Summary statistics ---
        // Straight average
        double meanCovCohn = covCohn.Average();
        double meanCovMVN = covMVN.Average();

        // Probability-weighted average: weight by log-spacing between adjacent AEPs.
        // This gives equal weight per decade of return period, preventing the many
        // extreme-AEP points (1e-6 through 1e-3) from dominating the average.
        // Weight_j = |ln(AEP_j+1) - ln(AEP_j-1)| / 2  (trapezoidal in log-space)
        double[] logWeights = new double[nProb];
        for (int j = 0; j < nProb; j++)
        {
            double logLeft = j > 0 ? Math.Log(aeps[j - 1]) : Math.Log(aeps[j]) - 1.0;
            double logRight = j < nProb - 1 ? Math.Log(aeps[j + 1]) : Math.Log(aeps[j]) + 1.0;
            logWeights[j] = Math.Abs(logRight - logLeft) / 2.0;
        }
        double totalWeight = logWeights.Sum();

        double wtCovCohn = 0, wtCovMVN = 0;
        double wtBelowCohn = 0, wtAboveCohn = 0, wtBelowMVN = 0, wtAboveMVN = 0;
        for (int j = 0; j < nProb; j++)
        {
            double w = logWeights[j] / totalWeight;
            wtCovCohn += w * covCohn[j];
            wtCovMVN += w * covMVN[j];
            wtBelowCohn += w * missBelowCohn[j];
            wtAboveCohn += w * missAboveCohn[j];
            wtBelowMVN += w * missBelowMVN[j];
            wtAboveMVN += w * missAboveMVN[j];
        }

        Debug.WriteLine($"\n  Straight avg:  Cohn={meanCovCohn:F3}, MVN={meanCovMVN:F3}, Δ={meanCovCohn - meanCovMVN:+0.000;-0.000}");
        Debug.WriteLine($"  Log-wt avg:    Cohn={wtCovCohn:F3} (↓{wtBelowCohn:F3} ↑{wtAboveCohn:F3}), MVN={wtCovMVN:F3} (↓{wtBelowMVN:F3} ↑{wtAboveMVN:F3}), Δ={wtCovCohn - wtCovMVN:+0.000;-0.000}");
        Debug.WriteLine($"  Cohn balance:  ↓{wtBelowCohn:F3} vs ↑{wtAboveCohn:F3} (ideal: 0.050/0.050)");
        Debug.WriteLine($"  MVN balance:   ↓{wtBelowMVN:F3} vs ↑{wtAboveMVN:F3} (ideal: 0.050/0.050)");

        // Assertions: both methods should have reasonable coverage
        Assert.IsTrue(successCohn >= M * 0.85, $"{label}: Too many Cohn CI failures ({successCohn}/{M}).");
        Assert.IsTrue(successMVN >= M * 0.85, $"{label}: Too many LinkedMVN failures ({successMVN}/{M}).");
    }
}
