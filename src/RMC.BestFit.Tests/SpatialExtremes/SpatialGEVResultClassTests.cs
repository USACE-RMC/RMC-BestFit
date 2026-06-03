using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.SpatialExtremes;

/// <summary>
/// Programmatic unit tests for the small DTO-style result classes produced by
/// <see cref="SpatialGEVAnalysis"/>: <see cref="SpatialGEVSiteResults"/> and
/// <see cref="SpatialGEVCrossValidationResults"/>.
/// </summary>
/// <remarks>
/// These classes are pure result-holders. The contract worth pinning is that array
/// properties default to empty (so consumers can iterate without null guards) and
/// that property setters round-trip exactly.
/// </remarks>
[TestClass]
public class SpatialGEVResultClassTests
{
    #region SpatialGEVSiteResults

    /// <summary>
    /// Default-constructed SpatialGEVSiteResults exposes empty arrays (not null) and zero
    /// numerics — so consumers can iterate without null guards.
    /// </summary>
    [TestMethod]
    public void SiteResults_Defaults_ArraysEmpty_NumericsZero()
    {
        var r = new SpatialGEVSiteResults();

        Assert.IsNotNull(r.Coordinate);
        Assert.IsNotNull(r.Probabilities);
        Assert.IsNotNull(r.QuantileMean);
        Assert.IsNotNull(r.QuantileLower);
        Assert.IsNotNull(r.QuantileUpper);
        Assert.IsNotNull(r.QuantileMode);

        Assert.AreEqual(0, r.Coordinate.Length);
        Assert.AreEqual(0, r.Probabilities.Length);
        Assert.AreEqual(0, r.QuantileMean.Length);
        Assert.AreEqual(0, r.QuantileLower.Length);
        Assert.AreEqual(0, r.QuantileUpper.Length);
        Assert.AreEqual(0, r.QuantileMode.Length);

        Assert.AreEqual(0, r.SiteIndex);
        Assert.AreEqual(0.0, r.LocationMean);
        Assert.AreEqual(0.0, r.ScaleMean);
        Assert.AreEqual(0.0, r.ShapeMean);
    }

    /// <summary>
    /// SiteIndex = -1 is the documented sentinel for ungauged (predicted) locations.
    /// </summary>
    [TestMethod]
    public void SiteResults_NegativeIndexIsAllowed_ForUngaugedSites()
    {
        var r = new SpatialGEVSiteResults
        {
            SiteIndex = -1,
            Coordinate = [42.0, -71.0],
        };

        Assert.AreEqual(-1, r.SiteIndex);
        CollectionAssert.AreEqual(new[] { 42.0, -71.0 }, r.Coordinate);
    }

    /// <summary>
    /// Property round-trip: assigning location/scale/shape posterior summaries and the
    /// quantile arrays preserves the exact values supplied.
    /// </summary>
    [TestMethod]
    public void SiteResults_PropertyRoundTrip_PreservesValues()
    {
        double[] probabilities = [0.5, 0.1, 0.01];
        double[] qMean = [10, 20, 40];
        double[] qLower = [8, 16, 32];
        double[] qUpper = [12, 24, 48];
        double[] qMode = [9.5, 19, 39];

        var r = new SpatialGEVSiteResults
        {
            SiteIndex = 3,
            Coordinate = [100.0, 200.0],
            LocationMean = 50.0,
            LocationLower = 45.0,
            LocationUpper = 55.0,
            ScaleMean = 10.0,
            ScaleLower = 8.0,
            ScaleUpper = 12.0,
            ShapeMean = 0.1,
            ShapeLower = -0.05,
            ShapeUpper = 0.25,
            Probabilities = probabilities,
            QuantileMean = qMean,
            QuantileLower = qLower,
            QuantileUpper = qUpper,
            QuantileMode = qMode,
        };

        Assert.AreEqual(3, r.SiteIndex);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0 }, r.Coordinate);
        Assert.AreEqual(50.0, r.LocationMean);
        Assert.AreEqual(45.0, r.LocationLower);
        Assert.AreEqual(55.0, r.LocationUpper);
        Assert.AreEqual(10.0, r.ScaleMean);
        Assert.AreEqual(8.0, r.ScaleLower);
        Assert.AreEqual(12.0, r.ScaleUpper);
        Assert.AreEqual(0.1, r.ShapeMean);
        Assert.AreEqual(-0.05, r.ShapeLower);
        Assert.AreEqual(0.25, r.ShapeUpper);
        CollectionAssert.AreEqual(probabilities, r.Probabilities);
        CollectionAssert.AreEqual(qMean, r.QuantileMean);
        CollectionAssert.AreEqual(qLower, r.QuantileLower);
        CollectionAssert.AreEqual(qUpper, r.QuantileUpper);
        CollectionAssert.AreEqual(qMode, r.QuantileMode);
    }

    #endregion

    #region SpatialGEVCrossValidationResults

    /// <summary>
    /// Default-constructed cross-validation results have empty per-site arrays and zero
    /// aggregates so callers don't need null-checks.
    /// </summary>
    [TestMethod]
    public void CrossValResults_Defaults_ArraysEmpty_AggregatesZero()
    {
        var r = new SpatialGEVCrossValidationResults();

        Assert.IsNotNull(r.SitePredictionErrors);
        Assert.IsNotNull(r.SiteRMSE);
        Assert.IsNotNull(r.SiteBias);
        Assert.IsNotNull(r.SiteCRPS);

        Assert.AreEqual(0, r.SitePredictionErrors.Length);
        Assert.AreEqual(0, r.SiteRMSE.Length);
        Assert.AreEqual(0, r.SiteBias.Length);
        Assert.AreEqual(0, r.SiteCRPS.Length);

        Assert.AreEqual(0.0, r.MeanAbsoluteError);
        Assert.AreEqual(0.0, r.RootMeanSquareError);
        Assert.AreEqual(0.0, r.MeanBias);
    }

    /// <summary>
    /// Property round-trip: assigning per-site arrays and aggregate metrics preserves
    /// values exactly. Pin the contract so a property-setter rewrite cannot silently
    /// drop or reformat data.
    /// </summary>
    [TestMethod]
    public void CrossValResults_PropertyRoundTrip_PreservesValues()
    {
        double[] errors = [-0.1, 0.05, 0.2];
        double[] rmse = [0.5, 0.6, 0.7];
        double[] bias = [-0.01, 0.02, 0.03];
        double[] crps = [0.0, 0.0, 0.0];

        var r = new SpatialGEVCrossValidationResults
        {
            SitePredictionErrors = errors,
            SiteRMSE = rmse,
            SiteBias = bias,
            SiteCRPS = crps,
            MeanAbsoluteError = 0.117,
            RootMeanSquareError = 0.135,
            MeanBias = 0.0133,
        };

        CollectionAssert.AreEqual(errors, r.SitePredictionErrors);
        CollectionAssert.AreEqual(rmse, r.SiteRMSE);
        CollectionAssert.AreEqual(bias, r.SiteBias);
        CollectionAssert.AreEqual(crps, r.SiteCRPS);
        Assert.AreEqual(0.117, r.MeanAbsoluteError);
        Assert.AreEqual(0.135, r.RootMeanSquareError);
        Assert.AreEqual(0.0133, r.MeanBias);
    }

    #endregion
}
