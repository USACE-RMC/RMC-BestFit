using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.TimeSeriesAnalysis;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Preserves historical spatial GEV leave-one-site-out comparisons with separately constructed
/// reduced training models.
/// </summary>
/// <remarks>
/// These methods are not discovered as Verification tests: both sides call production fitting and
/// prediction paths. Deterministic reduced-model construction, held-out covariates, unscored folds,
/// and result accounting are covered by the fast SpatialGEV tests. Independent held-out prediction
/// is verified by <see cref="SpatialGEVChunk14OracleTests"/> against committed Python calculations.
/// </remarks>
[TestClass]
public class SpatialGEVCrossValidationVerificationTests
{
    /// <summary>Relative tolerance for the fold-versus-reduced-model prediction error.</summary>
    private const double ParityRelativeTolerance = 1e-6;

    /// <summary>Exceedance probabilities used by the production cross-validation (T = 2, 5, 10, 25, 50, 100).</summary>
    private static readonly double[] CrossValidationProbabilities = { 0.5, 0.2, 0.1, 0.04, 0.02, 0.01 };

    /// <summary>
    /// With Gaussian-copula dependence, leave-one-site-out retains its results after the run and the
    /// fold of site 1 equals the prediction of the independently reduced three-site model fitted with the
    /// same defaults and seed (TR-050, TR-051).
    /// </summary>
    public async Task LeaveOneSiteOut_WithCopula_RetainsResultsAndMatchesReducedModel()
    {
        var (data, coordinates, _) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(nObs: 40, nSites: 4, range: 30.0, seed: 33333);
        SpatialGEV full = BuildCopulaModel(data, coordinates);
        var analysis = new SpatialGEVAnalysis(full);
        TimeSeriesIndependentRecoveryTests.AssertResolvedBayesianDefaults("SpatialGEV cross-validation", analysis.BayesianAnalysis, full.NumberOfParameters);

        await analysis.RunCrossValidationAsync();

        SpatialGEVCrossValidationResults? results = analysis.CrossValidationResults;
        Assert.IsNotNull(results, "The completed cross-validation results must be retained (TR-050).");
        Assert.AreEqual(4, results!.SitePredictionErrors.Length);

        double expected = await ReducedModelPredictionErrorAsync(full, data, coordinates, heldOutSite: 0, covariates: null);
        double actual = results.SitePredictionErrors[0];
        Assert.AreEqual(
            expected,
            actual,
            ParityRelativeTolerance * Math.Abs(expected),
            $"Fold 1 prediction error {actual:G10} versus the reduced three-site model {expected:G10} (TR-051).");
    }

    /// <summary>
    /// With a location regression on site covariates, the fold prediction uses the held-out site's
    /// covariate row: the fold of site 1 equals the reduced-model prediction evaluated at that row (TR-052).
    /// </summary>
    public async Task LeaveOneSiteOut_WithLocationRegression_UsesHeldOutCovariates()
    {
        var (data, coordinates, _) = SyntheticSpatialGEVData.GetRegressionLocationOnly(nObs: 40, nSites: 4, seed: 66666);
        double[,] covariates = CoordinateCovariates(coordinates);
        SpatialGEV full = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location", covariates),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        var analysis = new SpatialGEVAnalysis(full);
        TimeSeriesIndependentRecoveryTests.AssertResolvedBayesianDefaults("SpatialGEV regression cross-validation", analysis.BayesianAnalysis, full.NumberOfParameters);

        await analysis.RunCrossValidationAsync();

        SpatialGEVCrossValidationResults? results = analysis.CrossValidationResults;
        Assert.IsNotNull(results, "The completed cross-validation results must be retained (TR-050).");

        double[] heldOutRow = { covariates[0, 0], covariates[0, 1] };
        double expected = await ReducedModelPredictionErrorAsync(full, data, coordinates, heldOutSite: 0, covariates: heldOutRow, locationCovariates: covariates);
        double actual = results!.SitePredictionErrors[0];
        Assert.AreEqual(
            expected,
            actual,
            ParityRelativeTolerance * Math.Abs(expected),
            $"Fold 1 prediction error {actual:G10} versus the reduced model evaluated at the held-out covariate row {expected:G10} (TR-052).");
    }

    /// <summary>
    /// A site without observations is reported as an unscored fold with NaN metrics, the remaining folds
    /// succeed, and the aggregates average the successful folds only (TR-053).
    /// </summary>
    public async Task LeaveOneSiteOut_SiteWithoutObservations_IsReportedNotScored()
    {
        var (data, coordinates, _) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(nObs: 40, nSites: 4, range: 30.0, seed: 33333);
        for (int i = 0; i < data.GetLength(0); i++)
            data[i, 2] = double.NaN;
        SpatialGEV full = BuildCopulaModel(data, coordinates);
        var analysis = new SpatialGEVAnalysis(full);

        await analysis.RunCrossValidationAsync();

        SpatialGEVCrossValidationResults? results = analysis.CrossValidationResults;
        Assert.IsNotNull(results);
        Assert.AreEqual(4, results!.TotalFolds);
        Assert.AreEqual(3, results.SuccessfulFolds, string.Join(" | ", results.FoldMessages));
        Assert.AreEqual(SpatialGEVCrossValidationFoldStatus.NoObservations, results.FoldStatus[2]);
        Assert.IsTrue(double.IsNaN(results.SitePredictionErrors[2]) && double.IsNaN(results.SiteRMSE[2]) && double.IsNaN(results.SiteBias[2]), "The unscored fold holds NaN metrics.");
        double[] scored = { results.SitePredictionErrors[0], results.SitePredictionErrors[1], results.SitePredictionErrors[3] };
        double[] scoredBias = { results.SiteBias[0], results.SiteBias[1], results.SiteBias[3] };
        Assert.IsTrue(scored.All(double.IsFinite), "The scored folds hold finite errors.");
        Assert.AreEqual(scored.Select(Math.Abs).Average(), results.MeanAbsoluteError, 1e-12, "MAE over the successful folds.");
        Assert.AreEqual(Math.Sqrt(scored.Select(e => e * e).Average()), results.RootMeanSquareError, 1e-12, "RMSE over the successful folds.");
        Assert.AreEqual(scoredBias.Average(), results.MeanBias, 1e-12, "Mean bias over the successful folds.");
        for (int j = 0; j < 4; j++)
        {
            if (j != 2)
                Assert.AreEqual(SpatialGEVCrossValidationFoldStatus.Succeeded, results.FoldStatus[j], results.FoldMessages[j]);
        }
    }

    /// <summary>
    /// Builds a copula model with intercept-only trends on the supplied network.
    /// </summary>
    /// <param name="data">The at-site data.</param>
    /// <param name="coordinates">The site coordinates.</param>
    /// <returns>The configured model.</returns>
    private static SpatialGEV BuildCopulaModel(double[,] data, double[,] coordinates)
    {
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
    /// Uses the site coordinates as the two location covariates.
    /// </summary>
    /// <param name="coordinates">The site coordinates.</param>
    /// <returns>The covariate matrix [sites x 2].</returns>
    private static double[,] CoordinateCovariates(double[,] coordinates)
    {
        int sites = coordinates.GetLength(0);
        var covariates = new double[sites, 2];
        for (int j = 0; j < sites; j++)
        {
            covariates[j, 0] = coordinates[j, 0];
            covariates[j, 1] = coordinates[j, 1];
        }
        return covariates;
    }

    /// <summary>
    /// Fits the network without the held-out site through the production analysis with the default
    /// settings and returns the T = 100 prediction error (predicted posterior-mean quantile at the held-out
    /// coordinates minus the held-out site's at-site maximum-likelihood quantile), the statistic that
    /// <c>RunCrossValidationAsync</c> stores in <c>SitePredictionErrors</c>. The reduced model is built
    /// independently from the data, coordinates, covariates, and copula, and carries the full model's
    /// parameter values, bounds, and priors so that the fold and the reduced fit share one prior specification.
    /// </summary>
    /// <param name="full">The full model whose parameter settings (values, bounds, priors) the reduced model adopts.</param>
    /// <param name="data">The full at-site data.</param>
    /// <param name="coordinates">The full coordinates.</param>
    /// <param name="heldOutSite">The held-out site index.</param>
    /// <param name="covariates">The held-out site's covariate row for the prediction, or null for intercept-only trends.</param>
    /// <param name="locationCovariates">The full location covariate matrix, or null for an intercept-only location trend.</param>
    /// <returns>The prediction error of the held-out site.</returns>
    private static async Task<double> ReducedModelPredictionErrorAsync(SpatialGEV full, double[,] data, double[,] coordinates, int heldOutSite, double[]? covariates, double[,]? locationCovariates = null)
    {
        int observations = data.GetLength(0);
        int sites = data.GetLength(1);
        var reducedData = new double[observations, sites - 1];
        var reducedCoordinates = new double[sites - 1, 2];
        double[,]? reducedLocationCovariates = locationCovariates == null ? null : new double[sites - 1, locationCovariates.GetLength(1)];
        for (int j = 0, r = 0; j < sites; j++)
        {
            if (j == heldOutSite)
                continue;
            for (int i = 0; i < observations; i++)
                reducedData[i, r] = data[i, j];
            reducedCoordinates[r, 0] = coordinates[j, 0];
            reducedCoordinates[r, 1] = coordinates[j, 1];
            if (reducedLocationCovariates != null)
            {
                for (int k = 0; k < reducedLocationCovariates.GetLength(1); k++)
                    reducedLocationCovariates[r, k] = locationCovariates![j, k];
            }
            r++;
        }

        var reduced = new SpatialGEV(
            reducedData,
            reducedCoordinates,
            new GeneralLinearFunction("Location", reducedLocationCovariates),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        if (locationCovariates == null)
        {
            reduced.SpatialDependence = new GaussianCopula(reducedCoordinates, CorrelationFunctionType.Exponential);
            reduced.UseCopulaDependence = true;
            reduced.SetDefaultParameters();
        }

        // Same prior specification as the full model: the trend and copula blocks have the same layout.
        Assert.AreEqual(full.NumberOfParameters, reduced.NumberOfParameters, "Trend and copula blocks share the layout of the full model.");
        for (int i = 0; i < full.NumberOfParameters; i++)
        {
            reduced.Parameters[i].Value = full.Parameters[i].Value;
            reduced.Parameters[i].LowerBound = full.Parameters[i].LowerBound;
            reduced.Parameters[i].UpperBound = full.Parameters[i].UpperBound;
            reduced.Parameters[i].PriorDistribution = full.Parameters[i].PriorDistribution.Clone();
        }
        reduced.SetParameterValues(reduced.Parameters.Select(p => p.Value).ToArray());
        var reducedAnalysis = new SpatialGEVAnalysis(reduced);
        await reducedAnalysis.RunAsync();
        Assert.IsTrue(reducedAnalysis.IsEstimated, "The reduced model must be estimated.");

        double[] heldOutCoordinates = { coordinates[heldOutSite, 0], coordinates[heldOutSite, 1] };
        SpatialGEVSiteResults prediction = reducedAnalysis.PredictAtUngaugedLocation(heldOutCoordinates, covariates, CrossValidationProbabilities);

        var siteData = new List<double>();
        for (int i = 0; i < observations; i++)
        {
            if (!double.IsNaN(data[i, heldOutSite]))
                siteData.Add(data[i, heldOutSite]);
        }
        var gev = new GeneralizedExtremeValue();
        gev.Estimate(siteData, ParameterEstimationMethod.MaximumLikelihood);
        return prediction.QuantileMean[5] - gev.InverseCDF(0.99);
    }
}
