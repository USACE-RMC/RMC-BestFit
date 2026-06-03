using Numerics.Data;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Bivariate;

/// <summary>
/// Phase 5 unit tests for the <see cref="BivariateAnalysis.XYOrdinates"/> setter.
/// Verifies that the joint exceedance evaluation grid can be changed without wiping
/// the MCMC fit. Programmatic event-wiring tests — no MCMC chain is run. Chain-running
/// parity tests live in RMC.BestFit.Verification.
/// </summary>
/// <remarks>
/// <para>
/// The contract: changing <c>XYOrdinates</c> on an estimated analysis preserves
/// <c>BayesianAnalysis.Results</c> (the MCMC chain output) and only reprocesses
/// <c>AnalysisResults</c>. Before Phase 5 the setter called <c>ClearResults()</c>,
/// forcing the user to rerun chains just to widen the grid. After Phase 5 it
/// fires-and-forgets <c>CreateFrequencyAnalysisResultsAsync</c> at the new grid.
/// </para>
/// </remarks>
[TestClass]
public class BivariateAnalysisXYOrdinatesReprocessTests
{
    private static readonly double[] _sampleX =
        { 98.1, 102.7, 115.3, 88.4, 104.9, 92.0, 110.5, 99.2, 107.6, 101.3 };
    private static readonly double[] _sampleY =
        { 75.2, 82.1, 93.6, 68.7, 84.3, 72.5, 90.4, 78.9, 88.5, 81.0 };

    private static UnivariateDistribution MakeMarginal(double[] data, double mu, double sigma)
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(data);
        df.CalculatePlottingPositions();
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        dist.SetParameterValues(new[] { mu, sigma });
        for (int i = 0; i < data.Length; i++)
            ((ExactData)df.ExactSeries[i]).Index = i;
        return dist;
    }

    private static BivariateAnalysis CreateFreshAnalysis()
    {
        var x = MakeMarginal(_sampleX, 100.0, 10.0);
        var y = MakeMarginal(_sampleY, 80.0, 8.0);
        var bd = new BivariateDistribution(x, y, CopulaType.Normal);
        return new BivariateAnalysis(bd);
    }

    /// <summary>
    /// Builds deterministic synthetic posterior draws for a one-parameter Gaussian copula.
    /// </summary>
    /// <param name="sampleSize">Number of posterior draws to create.</param>
    /// <returns>Synthetic MCMC results that fit the configured Normal copula.</returns>
    private static MCMCResults BuildSyntheticCopulaResults(int sampleSize)
    {
        var output = new List<ParameterSet>(sampleSize);
        for (int i = 0; i < sampleSize; i++)
        {
            double rho = 0.25 + 0.05 * Math.Sin(i);
            output.Add(new ParameterSet(new[] { rho }, 0.0));
        }

        return new MCMCResults(new ParameterSet(new[] { 0.25 }, 0.0), output, alpha: 0.10);
    }

    /// <summary>
    /// Creates an estimated bivariate analysis by injecting synthetic MCMC results.
    /// </summary>
    /// <returns>An analysis marked as estimated without running an MCMC chain.</returns>
    private static BivariateAnalysis CreateInjectedAnalysis()
    {
        const int SampleSize = 1000;
        var analysis = CreateFreshAnalysis();
        analysis.BayesianAnalysis.OutputLength = SampleSize;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildSyntheticCopulaResults(SampleSize),
            skipInformationCriteria: true);

        var isEstField = typeof(AnalysisBase).GetField("_isEstimated",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        isEstField!.SetValue(analysis, true);
        return analysis;
    }

    /// <summary>Verifies that XY ordinates change on fresh analysis does not clear mcmc.</summary>
    [TestMethod]
    public void XYOrdinates_ChangeOnFreshAnalysis_DoesNotClearMcmc()
    {
        var analysis = CreateFreshAnalysis();
        bool resultsChanged = false;
        bool isEstimatedChanged = false;
        bool bayesResultsChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BivariateAnalysis.AnalysisResults)) resultsChanged = true;
            if (e.PropertyName == nameof(BivariateAnalysis.IsEstimated)) isEstimatedChanged = true;
        };
        analysis.BayesianAnalysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Results)) bayesResultsChanged = true;
        };
        bool ordinatesChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BivariateAnalysis.XYOrdinates)) ordinatesChanged = true;
        };

        // Replace the ordinate grid on a fresh (not-estimated) analysis.
        analysis.XYOrdinates = new UncertainOrderedPairedData(
            new List<UncertainOrdinate> { new UncertainOrdinate(50, new Deterministic(50)) },
            false, SortOrder.Ascending,
            false, SortOrder.Ascending,
            UnivariateDistributionType.Deterministic);

        Assert.IsTrue(ordinatesChanged, "XYOrdinates PropertyChanged must fire so the App refreshes.");
        Assert.IsFalse(resultsChanged, "AnalysisResults PropertyChanged must NOT fire on a fresh-analysis ordinate edit.");
        Assert.IsFalse(isEstimatedChanged, "IsEstimated PropertyChanged must NOT fire on a fresh-analysis ordinate edit.");
        Assert.IsFalse(bayesResultsChanged, "BayesianAnalysis.Results PropertyChanged must NOT fire — implies ClearResults cascaded into the MCMC.");
    }

    /// <summary>Verifies that clear frequency analysis results preserves mcmc and is estimated for .</summary>
    [TestMethod]
    public void ClearFrequencyAnalysisResults_PreservesMcmcAndIsEstimated()
    {
        var analysis = CreateFreshAnalysis();

        // On a fresh analysis BayesianAnalysis.Results is null and IsEstimated is false;
        // the helper must still be safe to call and must not mutate either.
        analysis.ClearFrequencyAnalysisResults();

        Assert.IsNull(analysis.AnalysisResults);
        Assert.IsFalse(analysis.IsEstimated);
        Assert.IsNull(analysis.BayesianAnalysis.Results);
    }

    /// <summary>Verifies that XY ordinate reprocessing preserves the injected MCMC results.</summary>
    [TestMethod]
    public async Task XYOrdinatesChange_EstimatedAnalysis_PreservesMcmcResults()
    {
        var analysis = CreateInjectedAnalysis();
        Assert.IsTrue(analysis.IsEstimated, "Pre-condition: analysis must be marked estimated.");
        Assert.IsTrue(analysis.BayesianAnalysis.IsEstimated, "Pre-condition: BayesianAnalysis must be estimated.");
        var resultsBefore = analysis.BayesianAnalysis.Results;
        Assert.IsNotNull(resultsBefore);

        analysis.XYOrdinates = new UncertainOrderedPairedData(
            new List<UncertainOrdinate>
            {
                new UncertainOrdinate(90.0, new Deterministic(70.0)),
                new UncertainOrdinate(105.0, new Deterministic(82.0))
            },
            false, SortOrder.Ascending,
            false, SortOrder.Ascending,
            UnivariateDistributionType.Deterministic);
        await analysis.CreateFrequencyAnalysisResultsAsync();

        Assert.IsNotNull(analysis.AnalysisResults, "AnalysisResults must be rebuilt from the preserved chain.");
        Assert.AreSame(resultsBefore, analysis.BayesianAnalysis.Results,
            "XY ordinate edits must not clear or replace the MCMC results.");
        Assert.IsTrue(analysis.IsEstimated, "Analysis.IsEstimated must survive XY ordinate reprocessing.");
        Assert.IsTrue(analysis.BayesianAnalysis.IsEstimated, "BayesianAnalysis.IsEstimated must survive XY ordinate reprocessing.");
    }
}
