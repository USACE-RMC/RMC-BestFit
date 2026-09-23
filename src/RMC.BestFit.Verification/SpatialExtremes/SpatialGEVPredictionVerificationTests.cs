using Numerics.Data.Statistics;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.TimeSeriesAnalysis;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Preserves historical spatial GEV posterior-prediction and regional-aggregation comparisons.
/// </summary>
/// <remarks>
/// These methods are not discovered as Verification tests because they recompute predictions through
/// production paths. The fast SpatialGEV tests cover draw routing and aggregation contracts;
/// <see cref="SpatialGEVKrigingOracleTests"/> and <see cref="SpatialGEVChunk14OracleTests"/> supply
/// independent conditional-Gaussian-process and regional-posterior numerical evidence.
/// </remarks>
[TestClass]
public class SpatialGEVPredictionVerificationTests
{
    /// <summary>Relative tolerance for posterior-mean recomputation over the retained draws.</summary>
    private const double RelativeTolerance = 1e-9;

    /// <summary>
    /// With latent location errors, the ungauged-site location predicted by the analysis equals the
    /// posterior mean over the retained draws of the model-level conditional Gaussian-process prediction
    /// (simple kriging of the sampled latent errors), not an inverse-distance interpolation (TR-054).
    /// </summary>
    public async Task UngaugedPrediction_UsesConditionalGaussianProcessPerDraw()
    {
        var (data, coordinates, _) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(nObs: 40, nSites: 5, range: 30.0, seed: 33333);
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        model.SpatialDependence = new GaussianCopula(coordinates, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.LocationErrors = new SpatialRegressionErrors(coordinates, CorrelationFunctionType.Exponential);
        model.UseLocationErrors = true;
        model.SetDefaultParameters();
        var analysis = new SpatialGEVAnalysis(model);
        TimeSeriesIndependentRecoveryTests.AssertResolvedBayesianDefaults("SpatialGEV location-error network", analysis.BayesianAnalysis, model.NumberOfParameters);

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, $"The location-error network must be estimated under the production defaults. Sampler error: {analysis.BayesianAnalysis.LastError}");
        var results = analysis.BayesianAnalysis.Results!;
        double[] target = { 40.0, 35.0 };
        double[] probabilities = { 0.5, 0.1, 0.01 };

        // Expected: the posterior mean of the model-level conditional prediction over the retained draws.
        int draws = Math.Min(analysis.BayesianAnalysis.OutputLength, results.Output.Count);
        double sumLocation = 0.0;
        double sumScale = 0.0;
        for (int d = 0; d < draws; d++)
        {
            var clone = (SpatialGEV)model.Clone();
            clone.SetParameterValues(results.Output[d].Values);
            var (gevParameters, _) = clone.PredictAtUngauged(target, null);
            sumLocation += gevParameters[0];
            sumScale += gevParameters[1];
        }
        double expectedLocation = sumLocation / draws;
        double expectedScale = sumScale / draws;

        analysis.SampleConditionalResidual = false;
        SpatialGEVSiteResults prediction = analysis.PredictAtUngaugedLocation(target, null, probabilities);
        analysis.SampleConditionalResidual = true;
        SpatialGEVSiteResults sampled = analysis.PredictAtUngaugedLocation(target, null, probabilities);
        SpatialGEVSiteResults sampledAgain = analysis.PredictAtUngaugedLocation(target, null, probabilities);

        Assert.AreEqual(
            expectedLocation,
            prediction.LocationMean,
            RelativeTolerance * Math.Abs(expectedLocation),
            $"Predicted location {prediction.LocationMean:G10} versus the conditional Gaussian-process posterior mean {expectedLocation:G10} (TR-054).");
        Assert.AreEqual(expectedScale, prediction.ScaleMean, RelativeTolerance * Math.Abs(expectedScale), "Scale has no latent error and must agree either way.");
        Assert.AreEqual(sampled.LocationMean, sampledAgain.LocationMean, 0.0, "The conditional-residual draws are seeded and reproducible.");
        Assert.IsTrue(
            sampled.LocationUpper - sampled.LocationLower >= prediction.LocationUpper - prediction.LocationLower,
            $"Sampling the conditional residual widens the predictive interval ({sampled.LocationUpper - sampled.LocationLower:G6} versus {prediction.LocationUpper - prediction.LocationLower:G6}).");
        Console.WriteLine($"Ungauged location: conditional mean {prediction.LocationMean:F3} [{prediction.LocationLower:F3}, {prediction.LocationUpper:F3}]; with residual {sampled.LocationMean:F3} [{sampled.LocationLower:F3}, {sampled.LocationUpper:F3}].");
    }

    /// <summary>
    /// The regional credible bounds are posterior quantiles of the per-draw regional mean quantile,
    /// and the regional mean curve is its posterior mean (TR-058). The network carries a location
    /// regression on the site coordinates so the sites differ and endpoint averages are not posterior
    /// quantiles of the regional mean.
    /// </summary>
    public async Task RegionalCurve_IsPosteriorOfTheRegionalMeanQuantile()
    {
        var (data, coordinates, _) = SyntheticSpatialGEVData.GetRegressionLocationOnly(nObs: 40, nSites: 5, seed: 66666);
        var covariates = new double[5, 2];
        for (int j = 0; j < 5; j++)
        {
            covariates[j, 0] = coordinates[j, 0];
            covariates[j, 1] = coordinates[j, 1];
        }
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location", covariates),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        var analysis = new SpatialGEVAnalysis(model);
        TimeSeriesIndependentRecoveryTests.AssertResolvedBayesianDefaults("SpatialGEV regression network", analysis.BayesianAnalysis, model.NumberOfParameters);

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, $"The regression network must be estimated under the production defaults. Sampler error: {analysis.BayesianAnalysis.LastError}");
        var results = analysis.BayesianAnalysis.Results!;
        Assert.IsNotNull(analysis.AnalysisResults);
        var regionalResults = analysis.AnalysisResults!;
        double[,] intervals = regionalResults.ConfidenceIntervals!;
        double[] meanCurve = regionalResults.MeanCurve!;
        double[] probabilities = analysis.ProbabilityOrdinates.ToArray();
        int draws = Math.Min(analysis.BayesianAnalysis.OutputLength, results.Output.Count);
        double alpha = 1 - analysis.BayesianAnalysis.CredibleIntervalWidth;

        // Per-draw regional mean quantile across the five sites.
        var regional = new double[probabilities.Length, draws];
        for (int d = 0; d < draws; d++)
        {
            var clone = (SpatialGEV)model.Clone();
            clone.SetParameterValues(results.Output[d].Values);
            for (int p = 0; p < probabilities.Length; p++)
            {
                double sum = 0.0;
                for (int j = 0; j < model.Sites; j++)
                    sum += clone.InverseCDF(1 - probabilities[p], j);
                regional[p, d] = sum / model.Sites;
            }
        }

        for (int p = 0; p < probabilities.Length; p++)
        {
            var values = new double[draws];
            for (int d = 0; d < draws; d++)
                values[d] = regional[p, d];
            double mean = values.Average();
            Array.Sort(values);
            double lower = Statistics.Percentile(values, alpha / 2d, true);
            double upper = Statistics.Percentile(values, 1 - alpha / 2d, true);

            Assert.AreEqual(probabilities[p], intervals[p, 0], 0.0);
            Assert.AreEqual(mean, meanCurve[p], RelativeTolerance * Math.Abs(mean), $"Regional mean curve at p = {probabilities[p]}.");
            Assert.AreEqual(lower, intervals[p, 1], RelativeTolerance * Math.Abs(lower), $"Regional lower bound at p = {probabilities[p]} must be the posterior quantile of the regional mean (TR-058).");
            Assert.AreEqual(upper, intervals[p, 2], RelativeTolerance * Math.Abs(upper), $"Regional upper bound at p = {probabilities[p]} must be the posterior quantile of the regional mean (TR-058).");
        }
    }
}
