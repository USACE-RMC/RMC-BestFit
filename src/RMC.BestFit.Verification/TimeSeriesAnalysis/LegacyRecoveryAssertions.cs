using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Applies the central-90% parameter-inclusion rule used by historical Bayesian time-series calculations.
/// </summary>
/// <remarks>
/// Every generating coordinate must lie within the completed analysis's central 90% posterior interval,
/// and every R-hat must be finite and below 1.10. This helper does not require an effective sample size
/// or apply a relative point-estimate tolerance. The calculations that retain this helper are not
/// currently discovered as Verification tests; current recovery evidence uses its separately declared
/// central-95% and diagnostic acceptance rules.
/// </remarks>
internal static class LegacyRecoveryAssertions
{
    /// <summary>
    /// The R-hat convergence limit.
    /// </summary>
    private const double RhatLimit = 1.1;

    /// <summary>
    /// Asserts that every generating parameter lies inside the production central 90% credible
    /// interval of the completed analysis and that every R-hat is finite and below the limit.
    /// </summary>
    /// <param name="label">The cell label used in assertion messages.</param>
    /// <param name="model">The fitted model.</param>
    /// <param name="analysis">The completed Bayesian analysis.</param>
    /// <param name="truth">The generating parameter values.</param>
    internal static void AssertCredibleIntervalRecovery(
        string label,
        ModelBase model,
        BayesianAnalysis analysis,
        double[] truth)
    {
        Assert.IsNotNull(analysis.Results, $"{label}: Bayesian results were not published.");
        Assert.AreEqual(0.90, analysis.CredibleIntervalWidth, 1e-12,
            $"{label}: the central 90% credible interval is the production default.");
        Assert.AreEqual(truth.Length, analysis.Results.MAP.Values.Length, $"{label}: MAP parameter count.");
        Assert.AreEqual(truth.Length, analysis.Results.ParameterResults.Length, $"{label}: summary count.");

        for (int index = 0; index < truth.Length; index++)
        {
            var summary = analysis.Results.ParameterResults[index].SummaryStatistics;
            string parameterName = index < model.Parameters.Count ? model.Parameters[index].Name : $"Parameter[{index}]";
            Assert.IsTrue(
                truth[index] >= summary.LowerCI && truth[index] <= summary.UpperCI,
                $"{label}: {parameterName} generating value {truth[index]:G8} is outside the central 90% credible interval " +
                $"[{summary.LowerCI:G8}, {summary.UpperCI:G8}] (MAP {analysis.Results.MAP.Values[index]:G8}).");
            Assert.IsTrue(
                double.IsFinite(summary.Rhat) && summary.Rhat < RhatLimit,
                $"{label}: {parameterName} R-hat {summary.Rhat:G8} is not below {RhatLimit}.");
        }
    }
}
