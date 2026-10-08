using System.Text.Json;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies BestFit DIC and WAIC against committed deterministic results from
/// R <c>BayesianTools</c> and <c>loo</c>.
/// </summary>
/// <remarks>
/// The artifact contains fixed Normal-model observations, fixed posterior draws,
/// and the corresponding pointwise data log-likelihood matrix. R is used only to
/// generate the committed artifact and is not required when these tests execute.
/// </remarks>
[TestClass]
public class InformationCriterionOracleTests
{
    /// <summary>
    /// Verifies the posterior-mean plug-in DIC convention against R
    /// <c>BayesianTools::DIC</c>.
    /// </summary>
    /// <remarks>
    /// The compared convention is
    /// <c>DIC = Dbar + pD = 2*Dbar - Dhat</c>, where
    /// <c>pD = Dbar - Dhat</c> and <c>Dhat</c> is the deviance at the
    /// componentwise posterior mean.
    /// </remarks>
    [TestMethod]
    public void DIC_MatchesRBayesianToolsOracle()
    {
        var fixture = CreateFixture();
        JsonElement expected = fixture.Oracle.GetProperty("dic");
        double tolerance = ReadTolerance(fixture.Oracle);
        double devianceMean = fixture.Draws.Average(
            draw => -2d * fixture.Model.DataLogLikelihood(draw));
        double[] posteriorMean = Enumerable.Range(0, fixture.Draws[0].Length)
            .Select(parameterIndex => fixture.Draws.Average(draw => draw[parameterIndex]))
            .ToArray();
        double devianceAtMean = -2d * fixture.Model.DataLogLikelihood(posteriorMean);
        double effectiveParameterCount = devianceMean - devianceAtMean;
        double dic = devianceMean + effectiveParameterCount;

        Assert.AreEqual(
            expected.GetProperty("Dbar").GetDouble(),
            devianceMean,
            tolerance,
            "Mean posterior deviance differs from R BayesianTools.");
        Assert.AreEqual(
            expected.GetProperty("Dhat").GetDouble(),
            devianceAtMean,
            tolerance,
            "Deviance at the componentwise posterior mean differs from R BayesianTools.");
        Assert.AreEqual(
            expected.GetProperty("pD").GetDouble(),
            effectiveParameterCount,
            tolerance,
            "DIC effective parameter count differs from R BayesianTools.");
        Assert.AreEqual(
            expected.GetProperty("DIC").GetDouble(),
            dic,
            tolerance,
            "Direct DIC reconstruction differs from R BayesianTools.");
        Assert.AreEqual(
            expected.GetProperty("DIC").GetDouble(),
            fixture.Analysis.DIC,
            tolerance,
            "BayesianAnalysis.DIC differs from R BayesianTools.");
    }

    /// <summary>
    /// Verifies pointwise WAIC and its effective parameter count against R
    /// <c>loo::waic</c>.
    /// </summary>
    /// <remarks>
    /// The test first proves that BestFit supplies the same draw-by-observation
    /// data log-likelihood matrix to the criterion, then compares aggregate
    /// <c>lppd</c>, <c>p_WAIC</c>, <c>elpd_WAIC</c>, and WAIC values.
    /// </remarks>
    [TestMethod]
    public void WAIC_MatchesRLooOracle()
    {
        var fixture = CreateFixture();
        JsonElement expected = fixture.Oracle.GetProperty("waic");
        double tolerance = ReadTolerance(fixture.Oracle);
        double[][] expectedPointwise = ReadMatrix(
            fixture.Oracle.GetProperty("pointwise_log_likelihood"));
        double[][] actualPointwise = fixture.Draws
            .Select(fixture.Model.PointwiseDataLogLikelihood)
            .ToArray();

        Assert.AreEqual(expectedPointwise.Length, actualPointwise.Length);
        for (int drawIndex = 0; drawIndex < actualPointwise.Length; drawIndex++)
        {
            Assert.AreEqual(
                expectedPointwise[drawIndex].Length,
                actualPointwise[drawIndex].Length);
            for (int observationIndex = 0;
                observationIndex < actualPointwise[drawIndex].Length;
                observationIndex++)
            {
                Assert.AreEqual(
                    expectedPointwise[drawIndex][observationIndex],
                    actualPointwise[drawIndex][observationIndex],
                    tolerance,
                    $"Pointwise log likelihood differs at draw {drawIndex}, " +
                    $"observation {observationIndex}.");
            }
        }

        double actualLppd = 0d;
        double actualPWaic = 0d;
        for (int observationIndex = 0;
            observationIndex < actualPointwise[0].Length;
            observationIndex++)
        {
            double[] logLikelihoods = actualPointwise
                .Select(row => row[observationIndex])
                .ToArray();
            double maximum = logLikelihoods.Max();
            actualLppd += maximum + Math.Log(
                logLikelihoods.Average(value => Math.Exp(value - maximum)));
            double mean = logLikelihoods.Average();
            actualPWaic += logLikelihoods.Sum(value => Math.Pow(value - mean, 2d)) /
                (logLikelihoods.Length - 1d);
        }

        double actualElpdWaic = actualLppd - actualPWaic;
        double actualWaic = -2d * actualElpdWaic;

        Assert.AreEqual(
            expected.GetProperty("lppd").GetDouble(),
            actualLppd,
            tolerance,
            "Log pointwise predictive density differs from R loo.");
        Assert.AreEqual(
            expected.GetProperty("p_waic").GetDouble(),
            actualPWaic,
            tolerance,
            "Direct WAIC effective parameter count differs from R loo.");
        Assert.AreEqual(
            expected.GetProperty("elpd_waic").GetDouble(),
            actualElpdWaic,
            tolerance,
            "Expected log pointwise predictive density differs from R loo.");
        Assert.AreEqual(
            expected.GetProperty("waic").GetDouble(),
            actualWaic,
            tolerance,
            "Direct WAIC reconstruction differs from R loo.");
        Assert.AreEqual(
            expected.GetProperty("p_waic").GetDouble(),
            fixture.Analysis.WAIC_pD,
            tolerance,
            "BayesianAnalysis.WAIC_pD differs from R loo.");
        Assert.AreEqual(
            expected.GetProperty("waic").GetDouble(),
            fixture.Analysis.WAIC,
            tolerance,
            "BayesianAnalysis.WAIC differs from R loo.");
    }

    /// <summary>
    /// Creates the deterministic BestFit analysis from the committed R oracle.
    /// </summary>
    /// <returns>The oracle, model, populated Bayesian analysis, and posterior draws.</returns>
    private static (
        JsonElement Oracle,
        UnivariateDistribution Model,
        BayesianAnalysis Analysis,
        double[][] Draws) CreateFixture()
    {
        JsonElement oracle = LoadOracle();
        double[] observations = ReadArray(oracle.GetProperty("observations"));
        double[][] draws = ReadMatrix(oracle.GetProperty("posterior_draws"));
        var dataFrame = new DataFrame
        {
            ExactSeries = new ExactSeries(observations)
        };
        var model = new UnivariateDistribution(
            dataFrame,
            UnivariateDistributionType.Normal);
        var output = draws
            .Select(draw => new ParameterSet((double[])draw.Clone(), 0d))
            .ToList();
        var results = new MCMCResults(
            new ParameterSet((double[])draws[0].Clone(), 0d),
            output,
            alpha: 0.10);
        var analysis = new BayesianAnalysis(model);
        analysis.SetCustomMCMCResults(results);

        return (oracle, model, analysis, draws);
    }

    /// <summary>
    /// Loads and clones the committed model-comparison oracle root.
    /// </summary>
    /// <returns>A detached JSON root element.</returns>
    private static JsonElement LoadOracle()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            "model-comparison-oracle.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Reads the absolute comparison tolerance declared by the oracle.
    /// </summary>
    /// <param name="oracle">The oracle root.</param>
    /// <returns>The absolute tolerance.</returns>
    private static double ReadTolerance(JsonElement oracle)
    {
        return oracle.GetProperty("metadata")
            .GetProperty("absolute_tolerance")
            .GetDouble();
    }

    /// <summary>
    /// Reads a JSON numeric array into managed doubles.
    /// </summary>
    /// <param name="element">JSON array element.</param>
    /// <returns>The array values.</returns>
    private static double[] ReadArray(JsonElement element)
    {
        return element.EnumerateArray()
            .Select(value => value.GetDouble())
            .ToArray();
    }

    /// <summary>
    /// Reads a JSON matrix into a jagged managed array.
    /// </summary>
    /// <param name="element">JSON matrix element.</param>
    /// <returns>The matrix rows.</returns>
    private static double[][] ReadMatrix(JsonElement element)
    {
        return element.EnumerateArray()
            .Select(ReadArray)
            .ToArray();
    }
}
