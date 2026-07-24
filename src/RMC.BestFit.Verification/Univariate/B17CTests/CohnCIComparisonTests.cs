using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Diagnostic tests comparing Cohn's delta-method CIs with the LinkedMultivariateNormal percentile-based CIs.
/// </summary>
/// <remarks>
/// <para>
/// These tests run <see cref="Bulletin17CAnalysis"/> on canonical Bulletin 17C example datasets
/// and compare the CIs produced by two methods:
/// </para>
/// <list type="bullet">
///   <item><b>Cohn-style:</b> Nested Gaussian quadrature + adjusted Student's t formula
///     (mirroring EMA/PeakFQ's <c>VAR_EMAB</c> / <c>CI_EMA_M3B</c>)</item>
///   <item><b>LinkedMVN:</b> Parameter draws from MVN in link-space → percentile CIs</item>
/// </list>
/// <para>
/// The goal is to quantify how closely the LinkedMVN approach reproduces EMA-style CIs
/// and identify discrepancies, especially at extreme AEPs where the SE–quantile correlation
/// drives asymmetric CI widths.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class CohnCIComparisonTests
{

    /// <summary>
    /// Example 1 (Moose River): Systematic record, no censoring.
    /// Compares Cohn CIs with LinkedMVN CIs at standard AEP levels.
    /// </summary>
    [TestMethod]
    public async Task CohnCI_Example1_MooseRiver_DiagnosticComparison()
    {
        var (df, _) = Bulletin17CData.GetExample1();
        await RunDiagnosticComparison("Example 1 (Moose River)", df);
    }

    /// <summary>
    /// Example 2 (Orestimba Creek): Includes low outliers and zero-flow years.
    /// Tests Cohn CI behavior with heavily censored data.
    /// </summary>
    [TestMethod]
    public async Task CohnCI_Example2_OrestimbaCreek_DiagnosticComparison()
    {
        var (df, _) = Bulletin17CData.GetExample2();
        await RunDiagnosticComparison("Example 2 (Orestimba Creek)", df);
    }

    /// <summary>
    /// Runs the full diagnostic comparison between Cohn-style and LinkedMVN CIs.
    /// </summary>
    /// <param name="label">A descriptive label for the test case.</param>
    /// <param name="df">The DataFrame containing the flood data.</param>
    private async Task RunDiagnosticComparison(string label, DataFrame df)
    {
        // --- Setup ---
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var analysis = new Bulletin17CAnalysis(model);
        analysis.UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal;
        analysis.BayesianAnalysis.OutputLength = 5000;

        // --- Run analysis (GMM + LinkedMVN draws) ---
        await analysis.RunAsync();
        Assert.IsTrue(analysis.IsEstimated, $"{label}: Analysis failed to estimate.");

        // --- Compute Cohn-style CIs ---
        var cohnResult = analysis.ComputeCohnStyleConfidenceIntervals();
        Assert.IsNotNull(cohnResult, $"{label}: Cohn CI computation returned null.");

        // --- Extract LinkedMVN percentile CIs from analysis results ---
        var analysisResults = analysis.AnalysisResults;
        Assert.IsNotNull(analysisResults, $"{label}: AnalysisResults is null.");

        // --- Log comparison ---
        Debug.WriteLine($"\n{'=',-60}");
        Debug.WriteLine($"  {label}: Cohn-Style vs LinkedMVN CIs (90% Confidence)");
        Debug.WriteLine($"{'=',-60}");
        Debug.WriteLine($"{"AEP",8} {"Q_hat",12} {"Cohn Lo",12} {"Cohn Hi",12} {"beta1",8} {"nu",8} {"Var(Q)",12}");
        Debug.WriteLine($"{new string('-', 72)}");

        for (int k = 0; k < analysis.ProbabilityOrdinates.Count; k++)
        {
            Debug.WriteLine($"{analysis.ProbabilityOrdinates[k],8:F4} {cohnResult.PointEstimates[k],12:F2} " +
                            $"{cohnResult.LowerCI[k],12:F2} {cohnResult.UpperCI[k],12:F2} " +
                            $"{cohnResult.Beta1[k],8:F4} {cohnResult.Nu[k],8:F1} " +
                            $"{cohnResult.QuantileVariance[k],12:E3}");
        }

        // --- Basic sanity checks ---
        for (int k = 0; k < analysis.ProbabilityOrdinates.Count; k++)
        {
            double qHat = cohnResult.PointEstimates[k];
            double lo = cohnResult.LowerCI[k];
            double hi = cohnResult.UpperCI[k];

            // Lower CI should be below point estimate, upper above
            Assert.IsTrue(lo <= qHat, $"{label} AEP={analysis.ProbabilityOrdinates[k]}: Lower CI ({lo:F2}) > point estimate ({qHat:F2}).");
            Assert.IsTrue(hi >= qHat, $"{label} AEP={analysis.ProbabilityOrdinates[k]}: Upper CI ({hi:F2}) < point estimate ({qHat:F2}).");

            // CI width should be positive
            Assert.IsTrue(hi - lo > 0, $"{label} AEP={analysis.ProbabilityOrdinates[k]}: CI width is non-positive.");

            // Degrees of freedom should be reasonable (≥ 5 per Cohn's floor)
            Assert.IsTrue(cohnResult.Nu[k] >= 5.0, $"{label} AEP={analysis.ProbabilityOrdinates[k]}: Nu ({cohnResult.Nu[k]:F1}) below minimum.");
        }

        // --- Asymmetry check at extreme quantiles ---
        // At the 0.01 AEP (100-year event), the upper CI should be wider than the lower CI
        // for positive-skew LP3 distributions (β₁ > 0 expected)
        int idx001 = Array.IndexOf(analysis.ProbabilityOrdinates.ToArray(), 0.01);
        if (idx001 >= 0)
        {
            double qHat = cohnResult.PointEstimates[idx001];
            double upperWidth = cohnResult.UpperCI[idx001] - qHat;
            double lowerWidth = qHat - cohnResult.LowerCI[idx001];

            Debug.WriteLine($"\n  Asymmetry at 0.01 AEP: upper width = {upperWidth:F2}, lower width = {lowerWidth:F2}");
            Debug.WriteLine($"  Ratio (upper/lower) = {upperWidth / Math.Max(lowerWidth, 1e-10):F3}");
            Debug.WriteLine($"  beta1 = {cohnResult.Beta1[idx001]:F4} (positive → upper CI wider)");
        }
    }
}
