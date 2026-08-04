using Numerics;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Verification.Datasets;
using System.Diagnostics;
using System.Xml.Linq;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies Bayesian recovery of spatial GEV parameters across dependence, regression, shape, and sample-size regimes.
/// </summary>
[TestClass]
public class SpatialGEVBayesianRecoveryTests
{
    /// <summary>
    /// Tests Bayesian MCMC parameter recovery for homogeneous spatial GEV model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates that Bayesian estimation recovers the true intercept parameters
    /// within 25% tolerance (appropriate for MCMC variability).
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_BasicHomogeneous_RecoversParameters()
    {
        // Arrange
        double trueLocation = 10000;
        double trueScale = 3000;
        double trueShape = -0.1;

        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10,
            location: trueLocation, scale: trueScale, shape: trueShape,
            seed: 12345);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation should converge.");

        var map = analysis.BayesianAnalysis.Results!.MAP.Values;

        // Parameter layout: [ξ_intercept, α_intercept, κ_intercept]
        // Location: log-link, so estimated intercept should be ≈ log(10000) ≈ 9.21
        double estimatedLocIntercept = map[0];
        double expectedLocIntercept = trueParams.LocationIntercept;
        Assert.AreEqual(expectedLocIntercept, estimatedLocIntercept, Math.Abs(expectedLocIntercept * 0.25),
            "Location intercept should be recovered within 25%.");

        // Scale: log-link, so estimated intercept should be ≈ log(3000) ≈ 8.01
        double estimatedSclIntercept = map[1];
        double expectedSclIntercept = trueParams.ScaleIntercept;
        Assert.AreEqual(expectedSclIntercept, estimatedSclIntercept, Math.Abs(expectedSclIntercept * 0.25),
            "Scale intercept should be recovered within 25%.");

        // Shape: identity link, so estimated intercept should be ≈ -0.1
        double estimatedShpIntercept = map[2];
        double expectedShpIntercept = trueParams.ShapeIntercept;
        // Use absolute tolerance of 0.2 for shape parameter near zero
        Assert.AreEqual(expectedShpIntercept, estimatedShpIntercept, 0.2,
            "Shape intercept should be recovered within 0.2.");
    }

    /// <summary>
    /// Tests Bayesian MCMC recovery of copula range parameter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates that the copula range parameter is recovered within 50% tolerance.
    /// The range parameter controls the spatial extent of dependence.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithCopula_RecoversRangeParameter()
    {
        // Arrange
        double trueRange = 40.0;
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(
            nObs: 50, nSites: 10, range: trueRange, seed: 33333);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        model.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();

        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation with copula should converge.");

        // Find range parameter index (it's the copula correlation function parameter)
        var map = analysis.BayesianAnalysis.Results!.MAP.Values;
        int rangeParamIndex = -1;
        for (int i = 0; i < model.Parameters.Count; i++)
        {
            if (model.Parameters[i].Name == "Range")
            {
                rangeParamIndex = i;
                break;
            }
        }

        Assert.IsTrue(rangeParamIndex >= 0, "Range parameter should exist in model.");
        double estimatedRange = map[rangeParamIndex];
        // Range parameter is difficult to estimate precisely - use 50% tolerance
        Assert.AreEqual(trueRange, estimatedRange, trueRange * 0.50,
            $"Range parameter should be recovered within 50%. Estimated: {estimatedRange:F1}, True: {trueRange:F1}");
    }

    /// <summary>
    /// Tests Bayesian MCMC recovery of location regression intercept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates that the location intercept is recovered within 25% tolerance
    /// when using spatial regression covariates.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithLocationRegression_RecoversIntercept()
    {
        // Arrange
        double locBeta0 = 8.987; // log(8000) approximately
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetRegressionLocationOnly(
            nObs: 50, nSites: 10, locBeta0: locBeta0, locBetaX: 0.005, locBetaY: 0.008,
            seed: 66666);

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
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation should converge.");

        var map = analysis.BayesianAnalysis.Results!.MAP.Values;

        // First parameter is location intercept
        double estimatedIntercept = map[0];
        Assert.AreEqual(locBeta0, estimatedIntercept, Math.Abs(locBeta0 * 0.25),
            $"Location intercept should be recovered within 25%. Estimated: {estimatedIntercept:F3}, True: {locBeta0:F3}");
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation with positive shape parameter (heavy upper tail).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tests with shape κ = 0.1 (Fréchet-type, heavy upper tail).
    /// This is less common in hydrology but important for certain phenomena.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithPositiveShape_Converges()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, shape: 0.1, seed: 11111);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation with positive shape should converge.");

        var map = analysis.BayesianAnalysis.Results!.MAP.Values;
        double estimatedShape = map[2]; // Shape is third parameter
        Assert.AreEqual(0.1, estimatedShape, 0.2,
            $"Shape parameter should be recovered. Estimated: {estimatedShape:F3}, True: 0.1");
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation with zero shape parameter (Gumbel).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tests with shape κ = 0 (Gumbel distribution).
    /// This is common for annual maximum precipitation.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithZeroShape_Converges()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, shape: 0.0, seed: 22222);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation with zero shape should converge.");

        var map = analysis.BayesianAnalysis.Results!.MAP.Values;
        double estimatedShape = map[2]; // Shape is third parameter
        Assert.AreEqual(0.0, estimatedShape, 0.2,
            $"Shape parameter should be near zero. Estimated: {estimatedShape:F3}, True: 0.0");
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation with negative shape parameter (bounded upper tail).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tests with shape κ = -0.2 (Weibull-type, bounded upper tail).
    /// This is common for annual maximum streamflow.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithNegativeShape_Converges()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, shape: -0.2, seed: 33333);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation with negative shape should converge.");

        var map = analysis.BayesianAnalysis.Results!.MAP.Values;
        double estimatedShape = map[2]; // Shape is third parameter
        Assert.AreEqual(-0.2, estimatedShape, 0.2,
            $"Shape parameter should be recovered. Estimated: {estimatedShape:F3}, True: -0.2");
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation with large sample size for improved precision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses 100 observations at 15 sites (1500 total) for tighter parameter recovery.
    /// With more data, we expect closer estimates (15% tolerance).
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_LargeSample_HasTighterEstimates()
    {
        // Arrange
        double trueLocation = 10000;
        double trueScale = 3000;
        double trueShape = -0.1;

        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 100, nSites: 15,
            location: trueLocation, scale: trueScale, shape: trueShape,
            seed: 54321);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation should converge.");

        var map = analysis.BayesianAnalysis.Results!.MAP.Values;

        // With larger sample, use tighter tolerance (15%)
        double expectedLocIntercept = trueParams.LocationIntercept;
        Assert.AreEqual(expectedLocIntercept, map[0], Math.Abs(expectedLocIntercept * 0.15),
            "Location intercept should be recovered within 15% with large sample.");

        double expectedSclIntercept = trueParams.ScaleIntercept;
        Assert.AreEqual(expectedSclIntercept, map[1], Math.Abs(expectedSclIntercept * 0.15),
            "Scale intercept should be recovered within 15% with large sample.");
    }
}
