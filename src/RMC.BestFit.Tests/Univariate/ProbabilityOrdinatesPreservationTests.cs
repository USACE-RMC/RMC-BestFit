using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;

namespace RMC.BestFit.Tests.UnivariateAnalyses;

/// <summary>
/// Phase 1 backfill tests for the Bayesian univariate analyses. Verifies the contract
/// established by the ProbabilityOrdinates reprocess pattern: an ordinate change must
/// not cascade into <see cref="BayesianAnalysis.ClearResults"/> side-effects on the
/// inner MCMC fit. These are programmatic event-wiring tests — no MCMC chain is run.
/// MCMC-running parity tests live in RMC.BestFit.Verification.
/// </summary>
/// <remarks>
/// <para>
/// The contract per analysis: changing <c>ProbabilityOrdinates</c> on a fresh
/// (not-estimated) analysis fires <c>ProbabilityOrdinates</c> PropertyChanged and
/// nothing else. The clear-side-effect signals — <c>AnalysisResults</c>,
/// <c>IsEstimated</c>, and <c>BayesianAnalysis.Results</c> PropertyChanged — must
/// remain silent. Before Phase 1 these would all fire because the model-layer
/// PropertyChanged handler called <c>ClearResults()</c> as a fall-through. After
/// Phase 1's whitelist conversion (and the existing handler at
/// <c>UnivariateAnalysis.ProbabilityOrdinates_CollectionChanged</c>) they no longer do.
/// </para>
/// </remarks>
[TestClass]
public class ProbabilityOrdinatesPreservationTests
{
    #region Test Data

    private static DataFrame CreateExactDataFrame()
    {
        var values = new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };
        var df = new DataFrame();
        for (int i = 0; i < values.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        return df;
    }

    /// <summary>
    /// Subscribes PropertyChanged listeners on both the analysis and its inner
    /// <see cref="BayesianAnalysis"/>, returning out-params that are flipped to
    /// <c>true</c> if a clear-side-effect event is observed.
    /// </summary>
    private static void TrackClearSideEffects(
        AnalysisBase analysis,
        BayesianAnalysis bayes,
        out Action<string> assertNoClear)
    {
        bool analysisResultsChanged = false;
        bool isEstimatedChanged = false;
        bool bayesResultsChanged = false;
        bool bayesIsEstimatedChanged = false;

        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "AnalysisResults") analysisResultsChanged = true;
            if (e.PropertyName == "IsEstimated") isEstimatedChanged = true;
        };
        bayes.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Results)) bayesResultsChanged = true;
            if (e.PropertyName == nameof(BayesianAnalysis.IsEstimated)) bayesIsEstimatedChanged = true;
        };

        assertNoClear = (label) =>
        {
            Assert.IsFalse(analysisResultsChanged, $"{label}: AnalysisResults PropertyChanged should NOT fire — implies ClearResults was called.");
            Assert.IsFalse(isEstimatedChanged, $"{label}: analysis.IsEstimated PropertyChanged should NOT fire.");
            Assert.IsFalse(bayesResultsChanged, $"{label}: BayesianAnalysis.Results PropertyChanged should NOT fire — implies ClearResults was called on the MCMC results.");
            Assert.IsFalse(bayesIsEstimatedChanged, $"{label}: BayesianAnalysis.IsEstimated PropertyChanged should NOT fire.");
        };
    }

    #endregion

    /// <summary>Verifies that univariate analysis ordinate change does not clear mcmc results.</summary>
    [TestMethod]
    public void UnivariateAnalysis_OrdinateChange_DoesNotClearMcmcResults()
    {
        var df = CreateExactDataFrame();
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);

        TrackClearSideEffects(analysis, analysis.BayesianAnalysis, out var assertNoClear);
        bool ordinatesChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UnivariateAnalysis.ProbabilityOrdinates)) ordinatesChanged = true;
        };

        analysis.ProbabilityOrdinates.Add(0.001);

        Assert.IsTrue(ordinatesChanged, "ProbabilityOrdinates PropertyChanged should fire so the App can refresh.");
        assertNoClear("UnivariateAnalysis");
    }

    /// <summary>Verifies that mixture analysis ordinate change does not clear mcmc results.</summary>
    [TestMethod]
    public void MixtureAnalysis_OrdinateChange_DoesNotClearMcmcResults()
    {
        var df = CreateExactDataFrame();
        var mixture = new Mixture(
            new[] { 0.5, 0.5 },
            new UnivariateDistributionBase[] { new Normal(15000, 3000), new Normal(20000, 3000) });
        var model = new MixtureModel(df, mixture);
        var analysis = new MixtureAnalysis(model);

        TrackClearSideEffects(analysis, analysis.BayesianAnalysis, out var assertNoClear);
        bool ordinatesChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MixtureAnalysis.ProbabilityOrdinates)) ordinatesChanged = true;
        };

        analysis.ProbabilityOrdinates.Add(0.001);

        Assert.IsTrue(ordinatesChanged);
        assertNoClear("MixtureAnalysis");
    }

    /// <summary>Verifies that point process analysis ordinate change does not clear mcmc results.</summary>
    [TestMethod]
    public void PointProcessAnalysis_OrdinateChange_DoesNotClearMcmcResults()
    {
        var df = new DataFrame();
        var data = new List<ExactData>
        {
            new ExactData(new DateTime(1990, 3, 15), 1500),
            new ExactData(new DateTime(1991, 4, 10), 2000),
            new ExactData(new DateTime(1992, 6, 5), 2200),
            new ExactData(new DateTime(1993, 4, 1), 2500),
            new ExactData(new DateTime(1994, 5, 15), 1600),
            new ExactData(new DateTime(1995, 3, 30), 3000)
        };
        df.ExactSeries = new ExactSeries(data);
        var dist = new CompetingRisks(new IUnivariateDistribution[]
        {
            new GeneralizedExtremeValue(2000, 500, 0.1)
        });
        var model = new PointProcessModel(df, dist);
        var analysis = new PointProcessAnalysis(model);

        TrackClearSideEffects(analysis, analysis.BayesianAnalysis, out var assertNoClear);
        bool ordinatesChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PointProcessAnalysis.ProbabilityOrdinates)) ordinatesChanged = true;
        };

        analysis.ProbabilityOrdinates.Add(0.001);

        Assert.IsTrue(ordinatesChanged);
        assertNoClear("PointProcessAnalysis");
    }

    /// <summary>Verifies that competing risk analysis ordinate change does not clear mcmc results.</summary>
    [TestMethod]
    public void CompetingRiskAnalysis_OrdinateChange_DoesNotClearMcmcResults()
    {
        var df = CreateExactDataFrame();
        var dist = new CompetingRisks(new UnivariateDistributionBase[]
        {
            new Normal(15000, 3000),
            new Gumbel(15000, 3000)
        });
        var model = new CompetingRisksModel(df, dist);
        var analysis = new CompetingRiskAnalysis(model);

        TrackClearSideEffects(analysis, analysis.BayesianAnalysis, out var assertNoClear);
        bool ordinatesChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CompetingRiskAnalysis.ProbabilityOrdinates)) ordinatesChanged = true;
        };

        analysis.ProbabilityOrdinates.Add(0.001);

        Assert.IsTrue(ordinatesChanged);
        assertNoClear("CompetingRiskAnalysis");
    }

    /// <summary>Verifies that bulletin17 c analysis ordinate change does not clear mcmc results.</summary>
    [TestMethod]
    public void Bulletin17CAnalysis_OrdinateChange_DoesNotClearMcmcResults()
    {
        var df = CreateExactDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var analysis = new Bulletin17CAnalysis(model);

        TrackClearSideEffects(analysis, analysis.BayesianAnalysis, out var assertNoClear);
        bool ordinatesChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Bulletin17CAnalysis.ProbabilityOrdinates)) ordinatesChanged = true;
        };

        analysis.ProbabilityOrdinates.Add(0.001);

        Assert.IsTrue(ordinatesChanged);
        assertNoClear("Bulletin17CAnalysis");
    }

    /// <summary>Verifies that spatial GEV analysis ordinate change does not clear mcmc results.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_OrdinateChange_DoesNotClearMcmcResults()
    {
        // Build a minimal 2-site spatial GEV model. AtSiteData[obs, site] convention.
        var data = new double[10, 2];
        var rng = new Random(2026);
        var gev = new GeneralizedExtremeValue(6000, 1500, 0.0);
        for (int i = 0; i < 10; i++)
        {
            data[i, 0] = gev.InverseCDF(rng.NextDouble());
            data[i, 1] = gev.InverseCDF(rng.NextDouble());
        }
        var coords = new double[,] { { 0.0, 0.0 }, { 10.0, 0.0 } };
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        TrackClearSideEffects(analysis, analysis.BayesianAnalysis, out var assertNoClear);
        bool ordinatesChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpatialGEVAnalysis.ProbabilityOrdinates)) ordinatesChanged = true;
        };

        analysis.ProbabilityOrdinates.Add(0.001);

        Assert.IsTrue(ordinatesChanged);
        assertNoClear("SpatialGEVAnalysis");
    }
}
