using Numerics;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies Bayesian recovery of identifiable spatial GEV coordinates across independent, dependent,
/// regression, and tail-shape regimes using ten sites with 100 observations each (1,000 scalar values).
/// </summary>
/// <remarks>
/// Every fixture retains the dimension-dependent production DEMCzs defaults. Three-, four-, and
/// five-coordinate models use 6, 8, and 10 chains; thinning 30, 40, and 50; and initial populations
/// 300, 400, and 500, respectively. All use 3,500 iterations, 1,750 warmup iterations, output length
/// 10,000, and estimator seed 12345. Acceptance uses central 95% posterior intervals, R-hat below
/// 1.10, and ESS at least 100 for every fitted coordinate. These models contain no latent
/// spatial-error component, so posterior parameter uncertainty is not mixed with conditional-GP
/// uncertainty.
/// </remarks>
[TestClass]
public class SpatialGEVBayesianRecoveryTests
{
    private const int ObservationCountPerSite = 100;
    private const int SiteCount = 10;
    private const int TotalScalarObservationCount = 1000;
    private const int EstimatorSeed = 12345;

    /// <summary>
    /// Recovers the three homogeneous GEV intercepts for a mildly bounded-tail, independent ten-site network.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fixture seed is 12345. Sites use the partial 25 km Cartesian grid declared by
    /// <see cref="SyntheticSpatialGEVData.GetBasicIdentifiableData"/>; every complete row contributes
    /// ten marginal GEV densities. The fitted order is [log(location), log(scale), shape].
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_BasicHomogeneous_RecoversParameters()
    {
        double trueLocation = 10000;
        double trueScale = 3000;
        double trueShape = -0.1;

        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: ObservationCountPerSite, nSites: SiteCount,
            location: trueLocation, scale: trueScale, shape: trueShape,
            seed: 12345);
        AssertCompleteNetwork(data);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        await AssertPosteriorRecoveryAsync(
            model,
            new[] { trueParams.LocationIntercept, trueParams.ScaleIntercept, trueParams.ShapeIntercept },
            "independent mild-negative-shape network");
    }

    /// <summary>
    /// Recovers the exponential-copula range and the three homogeneous marginal coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fixture contains ten sites on the partial 33.333 km Cartesian grid, 100 observations per
    /// site, generator seed 33333, and parent correlation exp(-h/40). Every complete row contributes ten marginal
    /// densities and one ten-dimensional copula density. The fitted order is
    /// [range, log(location), log(scale), shape].
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithCopula_RecoversParameters()
    {
        double trueRange = 40.0;
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(
            nObs: ObservationCountPerSite, nSites: SiteCount, range: trueRange, seed: 33333);
        AssertCompleteNetwork(data);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        model.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();

        await AssertPosteriorRecoveryAsync(
            model,
            new[]
            {
                trueParams.CopulaRange,
                trueParams.LocationIntercept,
                trueParams.ScaleIntercept,
                trueParams.ShapeIntercept
            },
            "dependent homogeneous network");
    }

    /// <summary>
    /// Recovers all three location-regression coefficients and the constant scale and shape coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fixture seed is 66666. Ten sites use X and Y from the partial 33.333 km Cartesian grid
    /// as covariates in log(location)=beta0+betaX X+betaY Y. Scale uses a log-link intercept and
    /// shape an identity-link intercept. The fitted order is [location beta0, betaX, betaY,
    /// log(scale), shape].
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithLocationRegression_RecoversParameters()
    {
        double locBeta0 = 8.987; // log(8000) approximately
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetRegressionLocationOnly(
            nObs: ObservationCountPerSite, nSites: SiteCount,
            locBeta0: locBeta0, locBetaX: 0.005, locBetaY: 0.008,
            seed: 66666);
        AssertCompleteNetwork(data);

        // Build covariate matrix [sites × 2] with X,Y coordinates
        var locCovariates = new double[10, 2];
        for (int j = 0; j < 10; j++)
        {
            locCovariates[j, 0] = coords[j, 0]; // X coordinate
            locCovariates[j, 1] = coords[j, 1]; // Y coordinate
        }

        var location = new GeneralLinearFunction("Location", locCovariates);
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        await AssertPosteriorRecoveryAsync(
            model,
            new[]
            {
                trueParams.LocationIntercept,
                trueParams.LocationBetaX,
                trueParams.LocationBetaY,
                trueParams.ScaleIntercept,
                trueParams.ShapeIntercept
            },
            "location-regression network");
    }

    /// <summary>
    /// Recovers homogeneous GEV coordinates for a positive-shape heavy-tail parent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The independent ten-site fixture uses generator seed 11111, 100 observations per site, and
    /// shape 0.1. It isolates positive-tail support behavior from the zero- and negative-shape cells.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithPositiveShape_RecoversParameters()
    {
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: ObservationCountPerSite, nSites: SiteCount, shape: 0.1, seed: 11111);
        AssertCompleteNetwork(data);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        await AssertPosteriorRecoveryAsync(
            model,
            new[] { trueParams.LocationIntercept, trueParams.ScaleIntercept, trueParams.ShapeIntercept },
            "positive-shape network");
    }

    /// <summary>
    /// Recovers homogeneous GEV coordinates at the zero-shape Gumbel limit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The independent ten-site fixture uses generator seed 22222, 100 observations per site, and
    /// shape zero. It verifies the continuous Gumbel limit separately from finite-shape support.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithZeroShape_RecoversParameters()
    {
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: ObservationCountPerSite, nSites: SiteCount, shape: 0.0, seed: 22222);
        AssertCompleteNetwork(data);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        await AssertPosteriorRecoveryAsync(
            model,
            new[] { trueParams.LocationIntercept, trueParams.ScaleIntercept, trueParams.ShapeIntercept },
            "zero-shape network");
    }

    /// <summary>
    /// Recovers homogeneous GEV coordinates for a strongly bounded negative-shape parent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The independent ten-site fixture uses generator seed 33333, 100 observations per site, and
    /// shape -0.2. It is more strongly bounded than the mild -0.1 baseline and therefore retains
    /// a distinct support-regime claim.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithNegativeShape_RecoversParameters()
    {
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: ObservationCountPerSite, nSites: SiteCount, shape: -0.2, seed: 33333);
        AssertCompleteNetwork(data);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        await AssertPosteriorRecoveryAsync(
            model,
            new[] { trueParams.LocationIntercept, trueParams.ScaleIntercept, trueParams.ShapeIntercept },
            "negative-shape network");
    }

    /// <summary>
    /// Confirms that a spatial recovery fixture contains ten sites with 100 observations per site,
    /// for total scalar N equal to 1,000.
    /// </summary>
    /// <param name="data">Generated row-by-site matrix.</param>
    private static void AssertCompleteNetwork(double[,] data)
    {
        Assert.AreEqual(ObservationCountPerSite, data.GetLength(0), "Each site must contain 100 observations.");
        Assert.AreEqual(SiteCount, data.GetLength(1), "Every row must contain the ten-site network.");
        Assert.AreEqual(TotalScalarObservationCount, data.Length, "Total scalar recovery N must equal 1,000.");
        Assert.IsTrue(data.Cast<double>().All(double.IsFinite), "Every retained row/year vector must be complete and finite.");
    }

    /// <summary>
    /// Runs one unchanged-default Bayesian spatial analysis and applies the common recovery rule to
    /// every estimator-owned coordinate.
    /// </summary>
    /// <param name="model">Configured spatial model.</param>
    /// <param name="parents">Generating parents in the model's flat parameter order.</param>
    /// <param name="fixture">Fixture label used in assertion diagnostics.</param>
    private static async Task AssertPosteriorRecoveryAsync(
        SpatialGEV model,
        IReadOnlyList<double> parents,
        string fixture)
    {
        var analysis = new SpatialGEVAnalysis(model);
        AssertDefaultSamplerConfiguration(analysis.BayesianAnalysis, parents.Count, fixture);
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95d;

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated,
            $"{fixture}: Bayesian estimation did not complete. Last sampler error: {analysis.BayesianAnalysis.LastError}");
        Assert.AreEqual(model.Parameters.Count, parents.Count,
            $"{fixture}: generating-parent order does not cover every fitted coordinate.");
        MCMCResults results = analysis.BayesianAnalysis.Results!;
        Assert.AreEqual(parents.Count, results.ParameterResults.Count(),
            $"{fixture}: posterior coordinate count differs from the declared parent vector.");

        for (int index = 0; index < parents.Count; index++)
        {
            ModelParameter parameter = model.Parameters[index];
            double parent = parents[index];
            Assert.IsTrue(parent >= parameter.LowerBound && parent <= parameter.UpperBound,
                $"{fixture}.{parameter.Name}: generating parent {parent:G17} is outside [{parameter.LowerBound:G17}, {parameter.UpperBound:G17}].");
            double parentPriorLogDensity = parameter.PriorDistribution.LogPDF(parent);
            Assert.IsTrue(double.IsFinite(parentPriorLogDensity),
                $"{fixture}.{parameter.Name}: generating parent {parent:G17} is outside the unchanged prior support.");

            var summary = results.ParameterResults[index].SummaryStatistics;
            string coordinate = $"{fixture}.{parameter.Name}";
            RecoveryAcceptance.AssertBayesianRecovery(
                coordinate,
                parent,
                summary.LowerCI,
                summary.UpperCI,
                summary.Rhat,
                summary.ESS);
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(
                coordinate,
                results.MAP.Values[index],
                parent,
                summary.LowerCI,
                summary.UpperCI);
        }
    }

    /// <summary>
    /// Requires the dimension-dependent production DEMCzs settings resolved when the model is attached.
    /// </summary>
    /// <param name="analysis">Bayesian analysis configured by <see cref="SpatialGEVAnalysis"/>.</param>
    /// <param name="parameterCount">Number of fitted coordinates.</param>
    /// <param name="fixture">Fixture label used in assertion diagnostics.</param>
    private static void AssertDefaultSamplerConfiguration(
        BayesianAnalysis analysis,
        int parameterCount,
        string fixture)
    {
        Assert.AreEqual(BayesianAnalysis.SamplerType.DEMCzs, analysis.Type, $"{fixture}: sampler changed.");
        Assert.IsTrue(analysis.UseSimulationDefaults, $"{fixture}: simulation defaults were disabled.");
        Assert.IsTrue(analysis.UseAdvancedSimulationDefaults, $"{fixture}: advanced simulation defaults were disabled.");
        Assert.AreEqual(0.90d, analysis.CredibleIntervalWidth, 1e-15, $"{fixture}: pre-run default interval width changed.");
        Assert.AreEqual(Math.Max(4, Math.Min(20, 2 * parameterCount)), analysis.NumberOfChains,
            $"{fixture}: dimension-dependent chain count changed.");
        Assert.AreEqual(Math.Max(1, Math.Min(100, 10 * parameterCount)), analysis.ThinningInterval,
            $"{fixture}: dimension-dependent thinning changed.");
        Assert.AreEqual(3500, analysis.Iterations, $"{fixture}: default iteration count changed.");
        Assert.AreEqual(1750, analysis.WarmupIterations, $"{fixture}: default warmup count changed.");
        Assert.AreEqual(Math.Min(1000, Math.Max(100, parameterCount * 100)), analysis.InitialIterations,
            $"{fixture}: dimension-dependent initial population changed.");
        Assert.AreEqual(10000, analysis.OutputLength, $"{fixture}: default output length changed.");
        Assert.AreEqual(EstimatorSeed, analysis.PRNGSeed, $"{fixture}: estimator seed changed.");
    }
}
