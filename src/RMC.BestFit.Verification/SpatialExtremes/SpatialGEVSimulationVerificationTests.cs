using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Verification.Datasets;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies the spatially dependent simulation of <see cref="SpatialGEV"/> (TR-061): with copula
/// dependence enabled, a seeded 20,000-row simulation reproduces every intersite normal-score
/// correlation of the configured copula matrix within ±0.02 and checks each site's GEV marginal.
/// </summary>
/// <remarks>
/// The fixture contains five sites, and each of the 20,000 simulated rows is one joint site vector.
/// No estimator is run. Every normal-score correlation must agree with the configured matrix to
/// 0.02 absolute, and empirical site quantiles must agree with configured GEV quantiles to 3% relative
/// at nonexceedance probabilities 0.1, 0.5, and 0.9. These are fixed simulation acceptance bands.
/// </remarks>
[TestClass]
public class SpatialGEVSimulationVerificationTests
{
    /// <summary>Absolute tolerance for the simulated intersite normal-score correlations.</summary>
    private const double CorrelationTolerance = 0.02;

    /// <summary>Relative tolerance for the empirical site quantiles.</summary>
    private const double QuantileRelativeTolerance = 0.03;

    /// <summary>
    /// Every pair of sites of a five-site copula model reproduces the fitted correlation in the simulated
    /// normal scores, and every site keeps its GEV marginal.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_WithCopula_ReproducesTheFittedIntersiteDependence()
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
        model.SetDefaultParameters();
        var values = model.Parameters.Select(p => p.Value).ToArray();
        values[0] = 35.0;
        model.SetParameterValues(values);
        double[,] correlation = model.SpatialDependence.GetCorrelationMatrix()!;
        int sampleSize = 20000;

        double[] samples = model.GenerateRandomValues(sampleSize, seed: 20260822);

        Assert.AreEqual(sampleSize * model.Sites, samples.Length);
        var scores = new double[model.Sites][];
        for (int s = 0; s < model.Sites; s++)
        {
            var gevParams = model.GetGEVParameters(s);
            var gev = new GeneralizedExtremeValue(gevParams[0], gevParams[1], gevParams[2]);
            scores[s] = new double[sampleSize];
            var siteValues = new double[sampleSize];
            for (int i = 0; i < sampleSize; i++)
            {
                siteValues[i] = samples[s * sampleSize + i];
                scores[s][i] = Normal.StandardZ(gev.CDF(siteValues[i]));
            }
            Array.Sort(siteValues);
            foreach (double p in new[] { 0.1, 0.5, 0.9 })
            {
                double expected = gev.InverseCDF(p);
                double empirical = Statistics.Percentile(siteValues, p, true);
                Assert.AreEqual(expected, empirical, QuantileRelativeTolerance * Math.Abs(expected), $"Site {s + 1} quantile at p = {p}.");
            }
        }
        for (int a = 0; a < model.Sites; a++)
        {
            for (int b = a + 1; b < model.Sites; b++)
            {
                double actual = Correlation.Pearson(scores[a], scores[b]);
                Assert.AreEqual(correlation[a, b], actual, CorrelationTolerance, $"Normal-score correlation of sites {a + 1} and {b + 1} (fitted {correlation[a, b]:F3}).");
            }
        }
    }
}
