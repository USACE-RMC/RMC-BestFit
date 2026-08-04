using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Verification.Datasets;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies maximum-likelihood recovery of homogeneous spatial GEV parameters and the copula range.
/// </summary>
[TestClass]
public class SpatialGEVMLERecoveryTests
{
    /// <summary>
    /// Tests that MLE recovers approximately correct intercepts for homogeneous model.
    /// </summary>
    [TestMethod]
    public void MLE_BasicHomogeneous_RecoversParameters()
    {
        // Arrange: use larger sample for better parameter recovery
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 100, nSites: 10, location: 10000, scale: 3000, shape: -0.1, seed: 54321);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();
        model.SetParameterValues(mle.BestParameterSet.Values);

        // Extract estimated intercepts (parameters are: loc_β0, scl_β0, shp_β0)
        double estLocIntercept = model.Parameters[0].Value;
        double estSclIntercept = model.Parameters[1].Value;
        double estShpIntercept = model.Parameters[2].Value;

        // Assert: intercepts should be close to true values
        // Location and scale use log-link, so compare in log-space
        Assert.AreEqual(trueParams.LocationIntercept, estLocIntercept, 0.2,
            $"Location intercept not recovered. True: {trueParams.LocationIntercept}, Est: {estLocIntercept}");
        Assert.AreEqual(trueParams.ScaleIntercept, estSclIntercept, 0.2,
            $"Scale intercept not recovered. True: {trueParams.ScaleIntercept}, Est: {estSclIntercept}");
        Assert.AreEqual(trueParams.ShapeIntercept, estShpIntercept, 0.1,
            $"Shape intercept not recovered. True: {trueParams.ShapeIntercept}, Est: {estShpIntercept}");
    }

    /// <summary>
    /// Tests that MLE recovers copula range parameter approximately.
    /// </summary>
    [TestMethod]
    public void MLE_WithCopula_RecoversRangeParameter()
    {
        // Arrange: use large sample for better parameter recovery
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(
            nObs: 100, nSites: 15, range: 40.0, seed: 66666);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        model.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();
        model.SetParameterValues(mle.BestParameterSet.Values);

        // The copula range parameter is in the first position of copula parameters
        double estRange = model.SpatialDependence!.Parameters[0].Value;

        // Assert: range should be within reasonable bounds of true value
        Assert.IsTrue(mle.IsEstimated, "MLE should converge.");
        Assert.AreEqual(trueParams.CopulaRange, estRange, 20.0,
            $"Copula range not recovered well. True: {trueParams.CopulaRange}, Est: {estRange}");
    }
}
