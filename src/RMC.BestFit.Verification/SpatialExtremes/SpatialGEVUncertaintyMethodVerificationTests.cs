using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.TimeSeriesAnalysis;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies that <see cref="SpatialGEVAnalysis.RunAsync"/> dispatches the selected uncertainty method
/// (TR-062) and that the temporal block bootstrap fits resampled data with explicit replicate accounting
/// (TR-056): each cell runs the production analysis with the untouched DEMCzs defaults on a four-site copula
/// network and checks the applied method, the replicate counts, and the structure of the resulting bounds.
/// </summary>
/// <remarks>
/// Deterministic uncertainty dispatch and selected-method state, Godambe availability and failure state,
/// block-bootstrap row construction, and result/settings DTO state are fast-owned by sampler-free
/// SpatialGEV tests. The remaining independent Godambe H/J, bootstrap-quantile, and VIF evidence is open
/// for Chunk 14B.
/// </remarks>
[TestClass]
public class SpatialGEVUncertaintyMethodVerificationTests
{
    /// <summary>Builds the four-site copula network of the cells.</summary>
    /// <returns>The model.</returns>
    private static SpatialGEV BuildNetwork()
    {
        var (data, coordinates, _) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(nObs: 40, nSites: 4, range: 30.0, seed: 33333);
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        model.SpatialDependence = new GaussianCopula(coordinates, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>
    /// Asserts that every site result carries the method and finite, ordered parameter and quantile bounds.
    /// </summary>
    /// <param name="analysis">The analysis.</param>
    /// <param name="method">The expected method.</param>
    private static void AssertSiteBounds(SpatialGEVAnalysis analysis, SpatialGEVUncertaintyMethod method)
    {
        Assert.AreEqual(method, analysis.AppliedUncertaintyMethod, "Applied method.");
        Assert.IsNotNull(analysis.SiteResults);
        foreach (var site in analysis.SiteResults!)
        {
            Assert.AreEqual(method, site.UncertaintyMethod, $"Site {site.SiteIndex + 1} method.");
            Assert.IsTrue(double.IsFinite(site.LocationLower) && double.IsFinite(site.LocationUpper) && site.LocationLower <= site.LocationUpper, $"Site {site.SiteIndex + 1} location bounds.");
            Assert.IsTrue(double.IsFinite(site.ScaleLower) && double.IsFinite(site.ScaleUpper) && site.ScaleLower <= site.ScaleUpper, $"Site {site.SiteIndex + 1} scale bounds.");
            for (int p = 0; p < site.Probabilities.Length; p++)
            {
                Assert.IsTrue(double.IsFinite(site.QuantileLower[p]) && double.IsFinite(site.QuantileUpper[p]) && site.QuantileLower[p] <= site.QuantileUpper[p], $"Site {site.SiteIndex + 1} quantile bounds at p = {site.Probabilities[p]}.");
            }
        }
        Assert.IsNotNull(analysis.AnalysisResults);
        for (int p = 0; p < analysis.AnalysisResults!.ConfidenceIntervals!.GetLength(0); p++)
        {
            Assert.IsTrue(double.IsFinite(analysis.AnalysisResults.ConfidenceIntervals[p, 1]) && double.IsFinite(analysis.AnalysisResults.ConfidenceIntervals[p, 2]), $"Regional bounds at row {p + 1}.");
            Assert.IsTrue(analysis.AnalysisResults.ConfidenceIntervals[p, 1] <= analysis.AnalysisResults.ConfidenceIntervals[p, 2]);
        }
    }

    /// <summary>
    /// With <see cref="SpatialGEVUncertaintyMethod.SpatialBootstrap"/>, the run refits twenty temporal
    /// block-bootstrap replicates by maximum a posteriori estimation, records the replicate accounting,
    /// and replaces the bounds with percentile intervals; a different seed changes the replicates.
    /// </summary>
    public async Task RunAsync_SpatialBootstrap_FitsResampledReplicatesAndReportsAccounting()
    {
        SpatialGEV model = BuildNetwork();
        var analysis = new SpatialGEVAnalysis(model)
        {
            UncertaintyMethod = SpatialGEVUncertaintyMethod.SpatialBootstrap,
            BootstrapReplicates = 20
        };
        TimeSeriesIndependentRecoveryTests.AssertResolvedBayesianDefaults("SpatialGEV bootstrap network", analysis.BayesianAnalysis, model.NumberOfParameters);

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, $"The bootstrap run must complete. Sampler error: {analysis.BayesianAnalysis.LastError}");
        AssertSiteBounds(analysis, SpatialGEVUncertaintyMethod.SpatialBootstrap);
        SpatialGEVBootstrapResults? bootstrap = analysis.BootstrapResults;
        Assert.IsNotNull(bootstrap);
        Assert.AreEqual(20, bootstrap!.RequestedReplicates);
        Assert.IsTrue(bootstrap.SuccessfulReplicates >= 10, $"At least half of the replicates succeed ({bootstrap.SuccessfulReplicates}).");
        Assert.AreEqual(20 - bootstrap.SuccessfulReplicates, bootstrap.FailedReplicates);
        Assert.AreEqual((int)Math.Ceiling(Math.Pow(40, 1.0 / 3.0)), bootstrap.BlockSize, "Automatic block size is the cube root of the row count, rounded up.");
        Assert.AreEqual(analysis.BayesianAnalysis.PRNGSeed, bootstrap.Seed);
        Assert.AreEqual(0.5, bootstrap.MinimumSuccessFraction, 0.0);
        StringAssert.Contains(bootstrap.Scheme, "block");

        double[] firstLower = analysis.SiteResults!.Select(s => s.QuantileLower[0]).ToArray();
        analysis.BayesianAnalysis.PRNGSeed = 54321;
        await analysis.RunAsync();
        Assert.IsTrue(analysis.IsEstimated);
        double[] secondLower = analysis.SiteResults!.Select(s => s.QuantileLower[0]).ToArray();
        Assert.IsTrue(firstLower.Zip(secondLower, (a, b) => a != b).Any(), "A different seed changes the bootstrap replicates.");
        Console.WriteLine($"Bootstrap: {bootstrap.SuccessfulReplicates}/{bootstrap.RequestedReplicates} replicates, block {bootstrap.BlockSize}.");
    }

    /// <summary>
    /// With <see cref="SpatialGEVUncertaintyMethod.GodambeSandwich"/>, the run builds the site results
    /// from seeded Gaussian draws around the MAP with the Godambe sandwich covariance and records the
    /// method when the covariance is available (homogeneous four-site network), and fails explicitly with
    /// the covariance status when it is not (the copula network, whose sensitivity matrix is singular at
    /// its MAP) instead of silently reporting posterior intervals.
    /// </summary>
    public async Task RunAsync_GodambeSandwich_BuildsResultsFromGaussianDraws()
    {
        // Explicit failure: the copula network's sensitivity matrix is singular at the sampled MAP.
        var copulaAnalysis = new SpatialGEVAnalysis(BuildNetwork()) { UncertaintyMethod = SpatialGEVUncertaintyMethod.GodambeSandwich };
        Exception? copulaError = null;
        copulaAnalysis.AnalysisCompleted += (_, e) => copulaError = e.Error;
        await copulaAnalysis.RunAsync();
        Assert.IsFalse(copulaAnalysis.IsEstimated, "A method that cannot be applied fails the run.");
        Assert.AreEqual(CovarianceComputationStatus.Failed, copulaAnalysis.GodambeCovarianceStatus, copulaAnalysis.GodambeCovarianceDiagnostic);
        Assert.IsNull(copulaAnalysis.AppliedUncertaintyMethod, "No method is recorded as applied.");
        StringAssert.Contains(copulaError?.Message ?? string.Empty, "Godambe sandwich covariance is unavailable");
        Console.WriteLine($"Copula network: {copulaAnalysis.GodambeCovarianceDiagnostic}");

        // Available covariance: the homogeneous network.
        var (data, coordinates, _) = SyntheticSpatialGEVData.GetBasicIdentifiableData(nObs: 40, nSites: 4, seed: 12345);
        var model = new SpatialGEV(data, coordinates, new GeneralLinearFunction("Location"), new GeneralLinearFunction("Scale"), new GeneralLinearFunction("Shape"));
        var analysis = new SpatialGEVAnalysis(model) { UncertaintyMethod = SpatialGEVUncertaintyMethod.GodambeSandwich };
        Exception? completionError = null;
        analysis.AnalysisCompleted += (_, e) => completionError = e.Error;

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, $"The Godambe run must complete. Sampler error: {analysis.BayesianAnalysis.LastError}; completion error: {completionError}; covariance: {analysis.GodambeCovarianceStatus} {analysis.GodambeCovarianceDiagnostic}");
        Assert.AreEqual(CovarianceComputationStatus.Available, analysis.GodambeCovarianceStatus, analysis.GodambeCovarianceDiagnostic);
        AssertSiteBounds(analysis, SpatialGEVUncertaintyMethod.GodambeSandwich);
        double[] map = analysis.BayesianAnalysis.Results!.MAP.Values;
        model.SetParameterValues(map);
        for (int j = 0; j < model.Sites; j++)
        {
            var site = analysis.SiteResults![j];
            double mapQuantile = model.InverseCDF(1 - site.Probabilities[0], j);
            Assert.AreEqual(mapQuantile, site.QuantileMode[0], 1e-9 * mapQuantile, "The mode curve is evaluated at the MAP.");
            Assert.IsTrue(site.QuantileLower[0] <= mapQuantile && mapQuantile <= site.QuantileUpper[0], $"The Gaussian-draw interval of site {j + 1} contains the MAP quantile.");
        }
    }

    /// <summary>
    /// With <see cref="SpatialGEVUncertaintyMethod.BayesianInflated"/>, the run widens the posterior site
    /// and regional intervals by the square root of the variance inflation factor and records the method.
    /// </summary>
    public async Task RunAsync_BayesianInflated_WidensThePosteriorIntervals()
    {
        SpatialGEV model = BuildNetwork();
        var posterior = new SpatialGEVAnalysis(model);
        await posterior.RunAsync();
        Assert.IsTrue(posterior.IsEstimated, $"Posterior run. Sampler error: {posterior.BayesianAnalysis.LastError}");
        double posteriorWidth = posterior.SiteResults![0].QuantileUpper[1] - posterior.SiteResults[0].QuantileLower[1];
        double posteriorRegionalWidth = posterior.AnalysisResults!.ConfidenceIntervals![1, 2] - posterior.AnalysisResults.ConfidenceIntervals[1, 1];

        var inflated = new SpatialGEVAnalysis(BuildNetwork()) { UncertaintyMethod = SpatialGEVUncertaintyMethod.BayesianInflated };
        await inflated.RunAsync();

        Assert.IsTrue(inflated.IsEstimated, $"Inflated run. Sampler error: {inflated.BayesianAnalysis.LastError}");
        AssertSiteBounds(inflated, SpatialGEVUncertaintyMethod.BayesianInflated);
        double sqrtVif = Math.Sqrt(inflated.VarianceInflationFactor);
        Assert.IsTrue(inflated.VarianceInflationFactor >= 1.0, "VIF is at least one.");
        Assert.AreEqual(posteriorWidth * sqrtVif, inflated.SiteResults![0].QuantileUpper[1] - inflated.SiteResults[0].QuantileLower[1], 1e-8 * posteriorWidth, "Site interval widened by sqrt(VIF) (same seed, same posterior).");
        Assert.AreEqual(posteriorRegionalWidth * sqrtVif, inflated.AnalysisResults!.ConfidenceIntervals![1, 2] - inflated.AnalysisResults.ConfidenceIntervals[1, 1], 1e-8 * posteriorRegionalWidth, "Regional interval widened by sqrt(VIF).");
    }
}
