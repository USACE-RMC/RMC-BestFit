using Numerics;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Sampling;

namespace RMC.BestFit.Verification.Datasets;

/// <summary>
/// Provides synthetic data for testing spatial GEV models and estimation procedures.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
///     This class generates synthetic at-site data for regional frequency analysis with known
///     true parameter values. The test scenarios build up progressively in complexity following
///     Renard's Bayesian Hierarchical Model (BHM) framework.
/// </para>
/// <para>
///     <b>Progressive Test Hierarchy:</b>
/// </para>
/// <list type="number">
///     <item><description>Basic: Constant parameters, no spatial structure (identifiability baseline)</description></item>
///     <item><description>Homogeneous: Constant parameters over 100×100 km grid</description></item>
///     <item><description>Copula dependence: Gaussian copula with various correlation functions</description></item>
///     <item><description>Spatial regressions: X,Y covariates on GEV parameters (various combinations)</description></item>
///     <item><description>Spatial errors: Full BHM with spatially correlated regression errors</description></item>
/// </list>
/// <para>
///     <b>BHM Framework (Renard et al. 2006, 2011):</b>
/// </para>
/// <list type="bullet">
///     <item><description>Level 1 (Data): Y_ij | θ_j ~ GEV(ξ_j, α_j, κ_j)</description></item>
///     <item><description>Level 2 (Process): θ_j = g(β'X_j + ε_j) where ε ~ MVN(0, Σ)</description></item>
///     <item><description>Level 3 (Priors): Priors on β, σ, correlation parameters</description></item>
/// </list>
/// <para>
///     <b>References:</b>
/// </para>
/// <list type="bullet">
///     <item>Renard, B., et al. (2006). Use of a Gaussian copula for multivariate extreme value analysis.
///           Journal of Hydrology, 315(1-4), 203-215.</item>
///     <item>Renard, B. (2011). A Bayesian hierarchical approach to regional frequency analysis.
///           Water Resources Research, 47, W11513.</item>
/// </list>
/// </remarks>
public static class SyntheticSpatialGEVData
{
    #region 1. Basic Tests (No Spatial Structure)

    /// <summary>
    /// Generates basic regional flood data with constant GEV parameters across all sites.
    /// This is the baseline test for parameter identifiability with 10 sites × 50 observations = 500 total.
    /// </summary>
    /// <param name="nObs">Number of observations (years) per site. Default 50.</param>
    /// <param name="nSites">Number of sites. Default 10.</param>
    /// <param name="location">True location parameter. Default 10000.</param>
    /// <param name="scale">True scale parameter. Default 3000.</param>
    /// <param name="shape">True shape parameter. Default -0.1.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>Tuple of (AtSiteData, Coordinates, TrueParameters).</returns>
    /// <remarks>
    /// <para>
    /// This generates independent GEV samples at each site with identical parameters.
    /// With 500 total observations (10 sites × 50 years), parameters should be highly identifiable.
    /// </para>
    /// <para>
    /// Sites are placed on a 100×100 km grid for spatial reference.
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetBasicIdentifiableData(int nObs = 50, int nSites = 10,
            double location = 10000, double scale = 3000, double shape = -0.1,
            int seed = 12345)
    {
        var rng = new MersenneTwister(seed);
        var data = new double[nObs, nSites];
        var coords = new double[nSites, 2];

        var locations = new double[nSites];
        var scales = new double[nSites];
        var shapes = new double[nSites];

        var gev = new GeneralizedExtremeValue(location, scale, shape);

        // Place sites on approximately 100×100 km grid
        int gridSize = (int)Math.Ceiling(Math.Sqrt(nSites));
        double spacing = 100.0 / gridSize; // km

        for (int j = 0; j < nSites; j++)
        {
            int row = j / gridSize;
            int col = j % gridSize;
            coords[j, 0] = col * spacing; // X in km
            coords[j, 1] = row * spacing; // Y in km

            locations[j] = location;
            scales[j] = scale;
            shapes[j] = shape;

            for (int i = 0; i < nObs; i++)
            {
                data[i, j] = gev.InverseCDF(rng.NextDouble());
            }
        }

        var trueParams = new SpatialGEVTrueParameters
        {
            Locations = locations,
            Scales = scales,
            Shapes = shapes,
            LocationIntercept = Math.Log(location), // log-link
            ScaleIntercept = Math.Log(scale),       // log-link
            ShapeIntercept = shape,                 // identity link
            HasCopula = false,
            HasSpatialErrors = false,
            HasSpatialRegression = false
        };

        return (data, coords, trueParams);
    }

    #endregion

    #region 2. Homogeneous Region Tests (100×100 km Grid)

    /// <summary>
    /// Generates regional data with homogeneous region (same parameters everywhere) on a 100×100 km grid.
    /// </summary>
    /// <param name="nObs">Number of observations per site. Default 50.</param>
    /// <param name="nSites">Number of sites. Default 10.</param>
    /// <param name="location">True location parameter.</param>
    /// <param name="scale">True scale parameter.</param>
    /// <param name="shape">True shape parameter.</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// This test validates that the model correctly identifies constant parameters
    /// when there is no spatial variation. The grid is exactly 100×100 km.
    /// </para>
    /// <para>
    /// Use this to test that spatial regression coefficients should be estimated near zero
    /// when there is no true spatial trend.
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetHomogeneousGridData(int nObs = 50, int nSites = 10,
            double location = 10000, double scale = 3000, double shape = -0.1,
            int seed = 22222)
    {
        var rng = new MersenneTwister(seed);
        var data = new double[nObs, nSites];
        var coords = new double[nSites, 2];

        var locations = new double[nSites];
        var scales = new double[nSites];
        var shapes = new double[nSites];

        var gev = new GeneralizedExtremeValue(location, scale, shape);

        // Exactly 100×100 km grid
        int gridSize = (int)Math.Ceiling(Math.Sqrt(nSites));
        double spacing = 100.0 / Math.Max(gridSize - 1, 1);

        for (int j = 0; j < nSites; j++)
        {
            int row = j / gridSize;
            int col = j % gridSize;
            coords[j, 0] = col * spacing; // X: 0 to 100 km
            coords[j, 1] = row * spacing; // Y: 0 to 100 km

            locations[j] = location;
            scales[j] = scale;
            shapes[j] = shape;

            for (int i = 0; i < nObs; i++)
            {
                data[i, j] = gev.InverseCDF(rng.NextDouble());
            }
        }

        var trueParams = new SpatialGEVTrueParameters
        {
            Locations = locations,
            Scales = scales,
            Shapes = shapes,
            LocationIntercept = Math.Log(location),
            ScaleIntercept = Math.Log(scale),
            ShapeIntercept = shape,
            HasCopula = false,
            HasSpatialErrors = false,
            HasSpatialRegression = false
        };

        return (data, coords, trueParams);
    }

    #endregion

    #region 3. Gaussian Copula Tests (Spatial Correlation Structures)

    /// <summary>
    /// Generates spatially correlated data using a Gaussian copula with basic exponential correlation.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="location">True location parameter.</param>
    /// <param name="scale">True scale parameter.</param>
    /// <param name="shape">True shape parameter.</param>
    /// <param name="range">Correlation range parameter (km). Default 30 km.</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Uses basic exponential correlation: ρ(h) = exp(-h/range)
    /// </para>
    /// <para>
    /// This test validates that the copula correctly captures spatial dependence
    /// in the data while maintaining correct marginal GEV distributions.
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetCopulaDataBasicExponential(int nObs = 50, int nSites = 10,
            double location = 10000, double scale = 3000, double shape = -0.1,
            double range = 30.0, int seed = 33333)
    {
        return GenerateCopulaData(nObs, nSites, location, scale, shape,
            CorrelationType.BasicExponential, range, 1.0, seed);
    }

    /// <summary>
    /// Generates spatially correlated data using a Gaussian copula with powered exponential correlation.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="location">True location parameter.</param>
    /// <param name="scale">True scale parameter.</param>
    /// <param name="shape">True shape parameter.</param>
    /// <param name="range">Correlation range parameter (km). Default 30 km.</param>
    /// <param name="power">Power parameter (0 &lt; p ≤ 2). Default 1.5.</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Uses powered exponential correlation: ρ(h) = exp(-(h/range)^p)
    /// </para>
    /// <para>
    /// When p = 1, this reduces to basic exponential. When p = 2, this is Gaussian.
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetCopulaDataPoweredExponential(int nObs = 50, int nSites = 10,
            double location = 10000, double scale = 3000, double shape = -0.1,
            double range = 30.0, double power = 1.5, int seed = 44444)
    {
        return GenerateCopulaData(nObs, nSites, location, scale, shape,
            CorrelationType.PoweredExponential, range, power, seed);
    }

    /// <summary>
    /// Generates spatially correlated data using a Gaussian copula with spherical correlation.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="location">True location parameter.</param>
    /// <param name="scale">True scale parameter.</param>
    /// <param name="shape">True shape parameter.</param>
    /// <param name="range">Correlation range parameter (km). Default 50 km.</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Uses spherical correlation: ρ(h) = 1 - 1.5(h/range) + 0.5(h/range)³ for h &lt; range, 0 otherwise.
    /// </para>
    /// <para>
    /// The spherical model has compact support (exactly zero correlation beyond the range).
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetCopulaDataSpherical(int nObs = 50, int nSites = 10,
            double location = 10000, double scale = 3000, double shape = -0.1,
            double range = 50.0, int seed = 55555)
    {
        return GenerateCopulaData(nObs, nSites, location, scale, shape,
            CorrelationType.Spherical, range, 1.0, seed);
    }

    /// <summary>
    /// Internal method to generate copula data with specified correlation structure.
    /// </summary>
    private static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GenerateCopulaData(int nObs, int nSites, double location, double scale, double shape,
            CorrelationType correlationType, double range, double power, int seed)
    {
        var rng = new MersenneTwister(seed);
        var data = new double[nObs, nSites];
        var coords = new double[nSites, 2];

        var locations = new double[nSites];
        var scales = new double[nSites];
        var shapes = new double[nSites];

        // Place sites on 100×100 km grid
        int gridSize = (int)Math.Ceiling(Math.Sqrt(nSites));
        double spacing = 100.0 / Math.Max(gridSize - 1, 1);

        for (int j = 0; j < nSites; j++)
        {
            int row = j / gridSize;
            int col = j % gridSize;
            coords[j, 0] = col * spacing;
            coords[j, 1] = row * spacing;

            locations[j] = location;
            scales[j] = scale;
            shapes[j] = shape;
        }

        // Build correlation matrix
        var correlationMatrix = BuildCorrelationMatrix(coords, correlationType, range, power);

        // Generate correlated normal samples via Cholesky decomposition
        var chol = new CholeskyDecomposition(new Matrix(correlationMatrix));
        var L = chol.L;

        var gev = new GeneralizedExtremeValue(location, scale, shape);
        var stdNormal = new Normal(0, 1);

        for (int i = 0; i < nObs; i++)
        {
            // Generate independent standard normal
            var z = new double[nSites];
            for (int j = 0; j < nSites; j++)
            {
                z[j] = stdNormal.InverseCDF(rng.NextDouble());
            }

            // Transform to correlated normals: w = L * z
            var w = new double[nSites];
            for (int j = 0; j < nSites; j++)
            {
                w[j] = 0;
                for (int k = 0; k <= j; k++)
                {
                    w[j] += L[j, k] * z[k];
                }
            }

            // Transform to uniform via standard normal CDF, then to GEV
            for (int j = 0; j < nSites; j++)
            {
                double u = stdNormal.CDF(w[j]);
                data[i, j] = gev.InverseCDF(u);
            }
        }

        var trueParams = new SpatialGEVTrueParameters
        {
            Locations = locations,
            Scales = scales,
            Shapes = shapes,
            LocationIntercept = Math.Log(location),
            ScaleIntercept = Math.Log(scale),
            ShapeIntercept = shape,
            HasCopula = true,
            CopulaRange = range,
            CopulaPower = power,
            CopulaCorrelationType = correlationType,
            HasSpatialErrors = false,
            HasSpatialRegression = false
        };

        return (data, coords, trueParams);
    }

    #endregion

    #region 4. Spatial Regression Tests (X, Y Covariates)

    /// <summary>
    /// Generates data with spatial regression on location parameter only.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="locBeta0">Location intercept (log-space). Default log(8000).</param>
    /// <param name="locBetaX">Location coefficient for X coordinate. Default 0.005.</param>
    /// <param name="locBetaY">Location coefficient for Y coordinate. Default 0.008.</param>
    /// <param name="scale">True scale parameter (constant). Default 3000.</param>
    /// <param name="shape">True shape parameter (constant). Default -0.1.</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Location varies spatially: log(ξ_j) = β₀ + β₁*X_j + β₂*Y_j
    /// Scale and shape are constant across the region.
    /// </para>
    /// <para>
    /// For a 100×100 km grid with these coefficients:
    /// - At (0,0): ξ ≈ 8000
    /// - At (100,100): ξ ≈ 8000 * exp(0.5 + 0.8) ≈ 29500
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetRegressionLocationOnly(int nObs = 50, int nSites = 10,
            double locBeta0 = 8.987, double locBetaX = 0.005, double locBetaY = 0.008,
            double scale = 3000, double shape = -0.1, int seed = 66666)
    {
        return GenerateRegressionData(nObs, nSites,
            locBeta0, locBetaX, locBetaY,       // Location regression
            Math.Log(scale), 0, 0,               // Scale constant
            shape, 0, 0,                         // Shape constant
            seed);
    }

    /// <summary>
    /// Generates data with spatial regression on scale parameter only.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="location">True location parameter (constant). Default 10000.</param>
    /// <param name="sclBeta0">Scale intercept (log-space). Default log(2000).</param>
    /// <param name="sclBetaX">Scale coefficient for X coordinate. Default 0.003.</param>
    /// <param name="sclBetaY">Scale coefficient for Y coordinate. Default 0.005.</param>
    /// <param name="shape">True shape parameter (constant). Default -0.1.</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Scale varies spatially: log(α_j) = β₀ + β₁*X_j + β₂*Y_j
    /// Location and shape are constant across the region.
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetRegressionScaleOnly(int nObs = 50, int nSites = 10,
            double location = 10000,
            double sclBeta0 = 7.601, double sclBetaX = 0.003, double sclBetaY = 0.005,
            double shape = -0.1, int seed = 77777)
    {
        return GenerateRegressionData(nObs, nSites,
            Math.Log(location), 0, 0,           // Location constant
            sclBeta0, sclBetaX, sclBetaY,       // Scale regression
            shape, 0, 0,                        // Shape constant
            seed);
    }

    /// <summary>
    /// Generates data with spatial regression on shape parameter only.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="location">True location parameter (constant). Default 10000.</param>
    /// <param name="scale">True scale parameter (constant). Default 3000.</param>
    /// <param name="shpBeta0">Shape intercept. Default -0.15.</param>
    /// <param name="shpBetaX">Shape coefficient for X coordinate. Default 0.0005.</param>
    /// <param name="shpBetaY">Shape coefficient for Y coordinate. Default 0.0008.</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Shape varies spatially: κ_j = β₀ + β₁*X_j + β₂*Y_j (identity link)
    /// Location and scale are constant across the region.
    /// </para>
    /// <para>
    /// Note: Shape uses identity link (not log), so coefficients are smaller.
    /// For a 100×100 km grid: shape ranges from -0.15 to -0.15 + 0.05 + 0.08 = -0.02
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetRegressionShapeOnly(int nObs = 50, int nSites = 10,
            double location = 10000, double scale = 3000,
            double shpBeta0 = -0.15, double shpBetaX = 0.0005, double shpBetaY = 0.0008,
            int seed = 88888)
    {
        return GenerateRegressionData(nObs, nSites,
            Math.Log(location), 0, 0,           // Location constant
            Math.Log(scale), 0, 0,              // Scale constant
            shpBeta0, shpBetaX, shpBetaY,       // Shape regression
            seed);
    }

    /// <summary>
    /// Generates data with spatial regression on both location and scale parameters.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="locBeta0">Location intercept (log-space).</param>
    /// <param name="locBetaX">Location coefficient for X.</param>
    /// <param name="locBetaY">Location coefficient for Y.</param>
    /// <param name="sclBeta0">Scale intercept (log-space).</param>
    /// <param name="sclBetaX">Scale coefficient for X.</param>
    /// <param name="sclBetaY">Scale coefficient for Y.</param>
    /// <param name="shape">True shape parameter (constant).</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Both location and scale vary spatially while shape remains constant.
    /// This is a common real-world scenario where flood magnitude and variability
    /// both increase with drainage area (proxy for X coordinate).
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetRegressionLocationScale(int nObs = 50, int nSites = 10,
            double locBeta0 = 8.987, double locBetaX = 0.005, double locBetaY = 0.008,
            double sclBeta0 = 7.601, double sclBetaX = 0.003, double sclBetaY = 0.005,
            double shape = -0.1, int seed = 99999)
    {
        return GenerateRegressionData(nObs, nSites,
            locBeta0, locBetaX, locBetaY,       // Location regression
            sclBeta0, sclBetaX, sclBetaY,       // Scale regression
            shape, 0, 0,                        // Shape constant
            seed);
    }

    /// <summary>
    /// Generates data with spatial regression on all three GEV parameters.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="locBeta0">Location intercept (log-space).</param>
    /// <param name="locBetaX">Location coefficient for X.</param>
    /// <param name="locBetaY">Location coefficient for Y.</param>
    /// <param name="sclBeta0">Scale intercept (log-space).</param>
    /// <param name="sclBetaX">Scale coefficient for X.</param>
    /// <param name="sclBetaY">Scale coefficient for Y.</param>
    /// <param name="shpBeta0">Shape intercept.</param>
    /// <param name="shpBetaX">Shape coefficient for X.</param>
    /// <param name="shpBetaY">Shape coefficient for Y.</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Full spatial regression on all GEV parameters:
    /// - log(ξ_j) = β₀_loc + β₁_loc*X_j + β₂_loc*Y_j
    /// - log(α_j) = β₀_scl + β₁_scl*X_j + β₂_scl*Y_j
    /// - κ_j = β₀_shp + β₁_shp*X_j + β₂_shp*Y_j
    /// </para>
    /// <para>
    /// This is the full Level 2 model without spatial errors.
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetRegressionLocationScaleShape(int nObs = 50, int nSites = 10,
            double locBeta0 = 8.987, double locBetaX = 0.005, double locBetaY = 0.008,
            double sclBeta0 = 7.601, double sclBetaX = 0.003, double sclBetaY = 0.005,
            double shpBeta0 = -0.15, double shpBetaX = 0.0005, double shpBetaY = 0.0008,
            int seed = 111111)
    {
        return GenerateRegressionData(nObs, nSites,
            locBeta0, locBetaX, locBetaY,       // Location regression
            sclBeta0, sclBetaX, sclBetaY,       // Scale regression
            shpBeta0, shpBetaX, shpBetaY,       // Shape regression
            seed);
    }

    /// <summary>
    /// Internal method to generate regression data.
    /// </summary>
    private static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GenerateRegressionData(int nObs, int nSites,
            double locBeta0, double locBetaX, double locBetaY,
            double sclBeta0, double sclBetaX, double sclBetaY,
            double shpBeta0, double shpBetaX, double shpBetaY,
            int seed)
    {
        var rng = new MersenneTwister(seed);
        var data = new double[nObs, nSites];
        var coords = new double[nSites, 2];

        var locations = new double[nSites];
        var scales = new double[nSites];
        var shapes = new double[nSites];

        // Place sites on 100×100 km grid
        int gridSize = (int)Math.Ceiling(Math.Sqrt(nSites));
        double spacing = 100.0 / Math.Max(gridSize - 1, 1);

        for (int j = 0; j < nSites; j++)
        {
            int row = j / gridSize;
            int col = j % gridSize;
            double x = col * spacing;
            double y = row * spacing;
            coords[j, 0] = x;
            coords[j, 1] = y;

            // Compute GEV parameters from regression
            // Location: log-link
            double logLoc = locBeta0 + locBetaX * x + locBetaY * y;
            locations[j] = Math.Exp(logLoc);

            // Scale: log-link
            double logScl = sclBeta0 + sclBetaX * x + sclBetaY * y;
            scales[j] = Math.Exp(logScl);

            // Shape: identity link
            shapes[j] = shpBeta0 + shpBetaX * x + shpBetaY * y;

            var gev = new GeneralizedExtremeValue(locations[j], scales[j], shapes[j]);
            for (int i = 0; i < nObs; i++)
            {
                data[i, j] = gev.InverseCDF(rng.NextDouble());
            }
        }

        var trueParams = new SpatialGEVTrueParameters
        {
            Locations = locations,
            Scales = scales,
            Shapes = shapes,
            LocationIntercept = locBeta0,
            LocationBetaX = locBetaX,
            LocationBetaY = locBetaY,
            ScaleIntercept = sclBeta0,
            ScaleBetaX = sclBetaX,
            ScaleBetaY = sclBetaY,
            ShapeIntercept = shpBeta0,
            ShapeBetaX = shpBetaX,
            ShapeBetaY = shpBetaY,
            HasCopula = false,
            HasSpatialErrors = false,
            HasSpatialRegression = locBetaX != 0 || locBetaY != 0 ||
                                   sclBetaX != 0 || sclBetaY != 0 ||
                                   shpBetaX != 0 || shpBetaY != 0
        };

        return (data, coords, trueParams);
    }

    #endregion

    #region 5. Spatially Correlated Regression Errors (Full BHM)

    /// <summary>
    /// Generates data with spatially correlated regression errors on location parameter.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="locBeta0">Location intercept (log-space).</param>
    /// <param name="locBetaX">Location coefficient for X.</param>
    /// <param name="locBetaY">Location coefficient for Y.</param>
    /// <param name="locSigma">Standard deviation of spatial errors for location.</param>
    /// <param name="locRange">Correlation range for location errors (km).</param>
    /// <param name="scale">True scale parameter (constant).</param>
    /// <param name="shape">True shape parameter (constant).</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Full BHM for location: log(ξ_j) = β₀ + β₁*X_j + β₂*Y_j + ε_j
    /// where ε ~ MVN(0, σ²Σ) with Σ_ij = ρ(h_ij)
    /// </para>
    /// <para>
    /// The spatial errors capture local deviations from the regional trend
    /// that are spatially correlated (nearby sites have similar deviations).
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetSpatialErrorsLocationOnly(int nObs = 50, int nSites = 10,
            double locBeta0 = 8.987, double locBetaX = 0.005, double locBetaY = 0.008,
            double locSigma = 0.15, double locRange = 30.0,
            double scale = 3000, double shape = -0.1, int seed = 222222)
    {
        return GenerateSpatialErrorData(nObs, nSites,
            locBeta0, locBetaX, locBetaY, locSigma, locRange,           // Location with errors
            Math.Log(scale), 0, 0, 0, 0,                                 // Scale constant
            shape, 0, 0, 0, 0,                                           // Shape constant
            seed);
    }

    /// <summary>
    /// Generates data with spatially correlated regression errors on both location and scale.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="locBeta0">Location intercept (log-space).</param>
    /// <param name="locBetaX">Location coefficient for X.</param>
    /// <param name="locBetaY">Location coefficient for Y.</param>
    /// <param name="locSigma">Standard deviation of spatial errors for location.</param>
    /// <param name="locRange">Correlation range for location errors (km).</param>
    /// <param name="sclBeta0">Scale intercept (log-space).</param>
    /// <param name="sclBetaX">Scale coefficient for X.</param>
    /// <param name="sclBetaY">Scale coefficient for Y.</param>
    /// <param name="sclSigma">Standard deviation of spatial errors for scale.</param>
    /// <param name="sclRange">Correlation range for scale errors (km).</param>
    /// <param name="shape">True shape parameter (constant).</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Full BHM for location and scale with independent error processes:
    /// - log(ξ_j) = β₀_loc + β₁_loc*X_j + β₂_loc*Y_j + ε_loc_j
    /// - log(α_j) = β₀_scl + β₁_scl*X_j + β₂_scl*Y_j + ε_scl_j
    /// where ε_loc and ε_scl are independent Gaussian processes.
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetSpatialErrorsLocationScale(int nObs = 50, int nSites = 10,
            double locBeta0 = 8.987, double locBetaX = 0.005, double locBetaY = 0.008,
            double locSigma = 0.15, double locRange = 30.0,
            double sclBeta0 = 7.601, double sclBetaX = 0.003, double sclBetaY = 0.005,
            double sclSigma = 0.10, double sclRange = 40.0,
            double shape = -0.1, int seed = 333333)
    {
        return GenerateSpatialErrorData(nObs, nSites,
            locBeta0, locBetaX, locBetaY, locSigma, locRange,           // Location with errors
            sclBeta0, sclBetaX, sclBetaY, sclSigma, sclRange,           // Scale with errors
            shape, 0, 0, 0, 0,                                           // Shape constant
            seed);
    }

    /// <summary>
    /// Generates data with the full BHM: spatial regression + correlated errors on all parameters.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="locBeta0">Location intercept (log-space).</param>
    /// <param name="locBetaX">Location coefficient for X.</param>
    /// <param name="locBetaY">Location coefficient for Y.</param>
    /// <param name="locSigma">Standard deviation of spatial errors for location.</param>
    /// <param name="locRange">Correlation range for location errors (km).</param>
    /// <param name="sclBeta0">Scale intercept (log-space).</param>
    /// <param name="sclBetaX">Scale coefficient for X.</param>
    /// <param name="sclBetaY">Scale coefficient for Y.</param>
    /// <param name="sclSigma">Standard deviation of spatial errors for scale.</param>
    /// <param name="sclRange">Correlation range for scale errors (km).</param>
    /// <param name="shpBeta0">Shape intercept.</param>
    /// <param name="shpBetaX">Shape coefficient for X.</param>
    /// <param name="shpBetaY">Shape coefficient for Y.</param>
    /// <param name="shpSigma">Standard deviation of spatial errors for shape.</param>
    /// <param name="shpRange">Correlation range for shape errors (km).</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Full Renard BHM with all components:
    /// - log(ξ_j) = β₀_loc + β₁_loc*X_j + β₂_loc*Y_j + ε_loc_j
    /// - log(α_j) = β₀_scl + β₁_scl*X_j + β₂_scl*Y_j + ε_scl_j
    /// - κ_j = β₀_shp + β₁_shp*X_j + β₂_shp*Y_j + ε_shp_j
    /// where ε_loc, ε_scl, ε_shp are independent Gaussian processes with
    /// MVN(0, σ²Σ) distributions and exponential correlation.
    /// </para>
    /// <para>
    /// This is the most complex test case, representing the full hierarchical model.
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetFullBHMData(int nObs = 50, int nSites = 10,
            double locBeta0 = 8.987, double locBetaX = 0.005, double locBetaY = 0.008,
            double locSigma = 0.15, double locRange = 30.0,
            double sclBeta0 = 7.601, double sclBetaX = 0.003, double sclBetaY = 0.005,
            double sclSigma = 0.10, double sclRange = 40.0,
            double shpBeta0 = -0.15, double shpBetaX = 0.0005, double shpBetaY = 0.0008,
            double shpSigma = 0.03, double shpRange = 50.0,
            int seed = 444444)
    {
        return GenerateSpatialErrorData(nObs, nSites,
            locBeta0, locBetaX, locBetaY, locSigma, locRange,
            sclBeta0, sclBetaX, sclBetaY, sclSigma, sclRange,
            shpBeta0, shpBetaX, shpBetaY, shpSigma, shpRange,
            seed);
    }

    /// <summary>
    /// Internal method to generate data with spatial errors.
    /// </summary>
    private static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GenerateSpatialErrorData(int nObs, int nSites,
            double locBeta0, double locBetaX, double locBetaY, double locSigma, double locRange,
            double sclBeta0, double sclBetaX, double sclBetaY, double sclSigma, double sclRange,
            double shpBeta0, double shpBetaX, double shpBetaY, double shpSigma, double shpRange,
            int seed)
    {
        var rng = new MersenneTwister(seed);
        var data = new double[nObs, nSites];
        var coords = new double[nSites, 2];

        var locations = new double[nSites];
        var scales = new double[nSites];
        var shapes = new double[nSites];
        var locErrors = new double[nSites];
        var sclErrors = new double[nSites];
        var shpErrors = new double[nSites];

        // Place sites on 100×100 km grid
        int gridSize = (int)Math.Ceiling(Math.Sqrt(nSites));
        double spacing = 100.0 / Math.Max(gridSize - 1, 1);

        for (int j = 0; j < nSites; j++)
        {
            int row = j / gridSize;
            int col = j % gridSize;
            coords[j, 0] = col * spacing;
            coords[j, 1] = row * spacing;
        }

        // Generate spatially correlated errors for each parameter
        if (locSigma > 0)
        {
            locErrors = GenerateSpatialErrors(coords, locSigma, locRange, rng);
        }
        if (sclSigma > 0)
        {
            sclErrors = GenerateSpatialErrors(coords, sclSigma, sclRange, rng);
        }
        if (shpSigma > 0)
        {
            shpErrors = GenerateSpatialErrors(coords, shpSigma, shpRange, rng);
        }

        // Compute GEV parameters and generate data
        for (int j = 0; j < nSites; j++)
        {
            double x = coords[j, 0];
            double y = coords[j, 1];

            // Location: log-link with spatial error
            double logLoc = locBeta0 + locBetaX * x + locBetaY * y + locErrors[j];
            locations[j] = Math.Exp(logLoc);

            // Scale: log-link with spatial error
            double logScl = sclBeta0 + sclBetaX * x + sclBetaY * y + sclErrors[j];
            scales[j] = Math.Exp(logScl);

            // Shape: identity link with spatial error
            shapes[j] = shpBeta0 + shpBetaX * x + shpBetaY * y + shpErrors[j];

            var gev = new GeneralizedExtremeValue(locations[j], scales[j], shapes[j]);
            for (int i = 0; i < nObs; i++)
            {
                data[i, j] = gev.InverseCDF(rng.NextDouble());
            }
        }

        var trueParams = new SpatialGEVTrueParameters
        {
            Locations = locations,
            Scales = scales,
            Shapes = shapes,
            LocationIntercept = locBeta0,
            LocationBetaX = locBetaX,
            LocationBetaY = locBetaY,
            LocationErrorSigma = locSigma,
            LocationErrorRange = locRange,
            LocationErrors = locErrors,
            ScaleIntercept = sclBeta0,
            ScaleBetaX = sclBetaX,
            ScaleBetaY = sclBetaY,
            ScaleErrorSigma = sclSigma,
            ScaleErrorRange = sclRange,
            ScaleErrors = sclErrors,
            ShapeIntercept = shpBeta0,
            ShapeBetaX = shpBetaX,
            ShapeBetaY = shpBetaY,
            ShapeErrorSigma = shpSigma,
            ShapeErrorRange = shpRange,
            ShapeErrors = shpErrors,
            HasCopula = false,
            HasSpatialErrors = locSigma > 0 || sclSigma > 0 || shpSigma > 0,
            HasSpatialRegression = locBetaX != 0 || locBetaY != 0 ||
                                   sclBetaX != 0 || sclBetaY != 0 ||
                                   shpBetaX != 0 || shpBetaY != 0
        };

        return (data, coords, trueParams);
    }

    /// <summary>
    /// Generates spatially correlated errors from a Gaussian process.
    /// </summary>
    private static double[] GenerateSpatialErrors(double[,] coords, double sigma, double range, MersenneTwister rng)
    {
        int n = coords.GetLength(0);

        // Build covariance matrix: Σ_ij = σ² * exp(-h_ij / range)
        var covMatrix = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double dx = coords[i, 0] - coords[j, 0];
                double dy = coords[i, 1] - coords[j, 1];
                double h = Math.Sqrt(dx * dx + dy * dy);
                covMatrix[i, j] = sigma * sigma * Math.Exp(-h / range);
            }
        }

        // Cholesky decomposition
        var chol = new CholeskyDecomposition(new Matrix(covMatrix));
        var L = chol.L;

        // Generate independent standard normals
        var stdNormal = new Normal(0, 1);
        var z = new double[n];
        for (int i = 0; i < n; i++)
        {
            z[i] = stdNormal.InverseCDF(rng.NextDouble());
        }

        // Transform: ε = L * z
        var errors = new double[n];
        for (int i = 0; i < n; i++)
        {
            errors[i] = 0;
            for (int j = 0; j <= i; j++)
            {
                errors[i] += L[i, j] * z[j];
            }
        }

        return errors;
    }

    #endregion

    #region 6. Combined Copula + Regression + Errors (Most Complex)

    /// <summary>
    /// Generates data with Gaussian copula dependence AND spatial regression on parameters.
    /// </summary>
    /// <param name="nObs">Number of observations per site.</param>
    /// <param name="nSites">Number of sites.</param>
    /// <param name="locBeta0">Location intercept (log-space).</param>
    /// <param name="locBetaX">Location coefficient for X.</param>
    /// <param name="locBetaY">Location coefficient for Y.</param>
    /// <param name="sclBeta0">Scale intercept (log-space).</param>
    /// <param name="sclBetaX">Scale coefficient for X.</param>
    /// <param name="sclBetaY">Scale coefficient for Y.</param>
    /// <param name="shape">True shape parameter (constant).</param>
    /// <param name="copulaRange">Correlation range for copula (km).</param>
    /// <param name="seed">Random seed.</param>
    /// <remarks>
    /// <para>
    /// Combines spatial regression on parameters with Gaussian copula dependence
    /// in the data (temporal dependence within years across sites).
    /// </para>
    /// </remarks>
    public static (double[,] Data, double[,] Coordinates, SpatialGEVTrueParameters TrueParams)
        GetCopulaWithRegressionData(int nObs = 50, int nSites = 10,
            double locBeta0 = 8.987, double locBetaX = 0.005, double locBetaY = 0.008,
            double sclBeta0 = 7.601, double sclBetaX = 0.003, double sclBetaY = 0.005,
            double shape = -0.1, double copulaRange = 30.0, int seed = 555555)
    {
        var rng = new MersenneTwister(seed);
        var data = new double[nObs, nSites];
        var coords = new double[nSites, 2];

        var locations = new double[nSites];
        var scales = new double[nSites];
        var shapes = new double[nSites];

        // Place sites on 100×100 km grid
        int gridSize = (int)Math.Ceiling(Math.Sqrt(nSites));
        double spacing = 100.0 / Math.Max(gridSize - 1, 1);

        for (int j = 0; j < nSites; j++)
        {
            int row = j / gridSize;
            int col = j % gridSize;
            double x = col * spacing;
            double y = row * spacing;
            coords[j, 0] = x;
            coords[j, 1] = y;

            // Compute GEV parameters from regression
            double logLoc = locBeta0 + locBetaX * x + locBetaY * y;
            locations[j] = Math.Exp(logLoc);

            double logScl = sclBeta0 + sclBetaX * x + sclBetaY * y;
            scales[j] = Math.Exp(logScl);

            shapes[j] = shape;
        }

        // Build copula correlation matrix
        var correlationMatrix = BuildCorrelationMatrix(coords, CorrelationType.BasicExponential, copulaRange, 1.0);

        // Cholesky decomposition
        var chol = new CholeskyDecomposition(new Matrix(correlationMatrix));
        var L = chol.L;

        var stdNormal = new Normal(0, 1);

        for (int i = 0; i < nObs; i++)
        {
            // Generate independent standard normal
            var z = new double[nSites];
            for (int j = 0; j < nSites; j++)
            {
                z[j] = stdNormal.InverseCDF(rng.NextDouble());
            }

            // Transform to correlated normals: w = L * z
            var w = new double[nSites];
            for (int j = 0; j < nSites; j++)
            {
                w[j] = 0;
                for (int k = 0; k <= j; k++)
                {
                    w[j] += L[j, k] * z[k];
                }
            }

            // Transform to site-specific GEV
            for (int j = 0; j < nSites; j++)
            {
                double u = stdNormal.CDF(w[j]);
                var gev = new GeneralizedExtremeValue(locations[j], scales[j], shapes[j]);
                data[i, j] = gev.InverseCDF(u);
            }
        }

        var trueParams = new SpatialGEVTrueParameters
        {
            Locations = locations,
            Scales = scales,
            Shapes = shapes,
            LocationIntercept = locBeta0,
            LocationBetaX = locBetaX,
            LocationBetaY = locBetaY,
            ScaleIntercept = sclBeta0,
            ScaleBetaX = sclBetaX,
            ScaleBetaY = sclBetaY,
            ShapeIntercept = shape,
            HasCopula = true,
            CopulaRange = copulaRange,
            CopulaCorrelationType = CorrelationType.BasicExponential,
            HasSpatialErrors = false,
            HasSpatialRegression = true
        };

        return (data, coords, trueParams);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Builds a correlation matrix from coordinates using specified correlation function.
    /// </summary>
    private static double[,] BuildCorrelationMatrix(double[,] coords, CorrelationType type, double range, double power)
    {
        int n = coords.GetLength(0);
        var matrix = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double dx = coords[i, 0] - coords[j, 0];
                double dy = coords[i, 1] - coords[j, 1];
                double h = Math.Sqrt(dx * dx + dy * dy);

                matrix[i, j] = ComputeCorrelation(h, type, range, power);
            }
        }

        return matrix;
    }

    /// <summary>
    /// Computes correlation value for given distance and correlation model.
    /// </summary>
    private static double ComputeCorrelation(double h, CorrelationType type, double range, double power)
    {
        if (h < 1e-10) return 1.0; // Diagonal

        switch (type)
        {
            case CorrelationType.BasicExponential:
                return Math.Exp(-h / range);

            case CorrelationType.PoweredExponential:
                return Math.Exp(-Math.Pow(h / range, power));

            case CorrelationType.Spherical:
                if (h >= range) return 0.0;
                double ratio = h / range;
                return 1.0 - 1.5 * ratio + 0.5 * Math.Pow(ratio, 3);

            default:
                return Math.Exp(-h / range);
        }
    }

    /// <summary>
    /// Gets standard return period probabilities for flood frequency analysis.
    /// </summary>
    /// <returns>Array of exceedance probabilities for T = 2, 5, 10, 25, 50, 100, 200, 500 years.</returns>
    public static double[] GetStandardReturnPeriodProbabilities()
    {
        return new double[] { 0.5, 0.2, 0.1, 0.04, 0.02, 0.01, 0.005, 0.002 };
    }

    /// <summary>
    /// Computes true quantiles at specified probabilities for given GEV parameters.
    /// </summary>
    /// <param name="location">GEV location parameter.</param>
    /// <param name="scale">GEV scale parameter.</param>
    /// <param name="shape">GEV shape parameter.</param>
    /// <param name="probabilities">Exceedance probabilities.</param>
    /// <returns>Array of true quantile values.</returns>
    public static double[] ComputeTrueQuantiles(double location, double scale, double shape, double[] probabilities)
    {
        var gev = new GeneralizedExtremeValue(location, scale, shape);
        var quantiles = new double[probabilities.Length];
        for (int i = 0; i < probabilities.Length; i++)
        {
            quantiles[i] = gev.InverseCDF(1 - probabilities[i]);
        }
        return quantiles;
    }

    /// <summary>
    /// Computes the Euclidean distance matrix between sites.
    /// </summary>
    /// <param name="coords">Site coordinates [nSites × 2].</param>
    /// <returns>Distance matrix [nSites × nSites].</returns>
    public static double[,] ComputeDistanceMatrix(double[,] coords)
    {
        int n = coords.GetLength(0);
        var dist = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double dx = coords[i, 0] - coords[j, 0];
                double dy = coords[i, 1] - coords[j, 1];
                dist[i, j] = Math.Sqrt(dx * dx + dy * dy);
            }
        }

        return dist;
    }

    #endregion
}

/// <summary>
/// Enumeration of correlation function types for spatial models.
/// </summary>
public enum CorrelationType
{
    /// <summary>
    /// Basic exponential: ρ(h) = exp(-h/range)
    /// </summary>
    BasicExponential,

    /// <summary>
    /// Powered exponential: ρ(h) = exp(-(h/range)^p)
    /// </summary>
    PoweredExponential,

    /// <summary>
    /// Spherical: ρ(h) = 1 - 1.5(h/r) + 0.5(h/r)³ for h &lt; range, 0 otherwise
    /// </summary>
    Spherical
}

/// <summary>
/// Stores true parameter values used to generate synthetic spatial GEV data.
/// </summary>
/// <remarks>
/// <para>
/// This class stores all components of the BHM framework:
/// </para>
/// <list type="bullet">
///     <item><description>Site-specific GEV parameters (resulting from regression + errors)</description></item>
///     <item><description>Regression coefficients for each parameter</description></item>
///     <item><description>Spatial error variances and ranges</description></item>
///     <item><description>Copula parameters</description></item>
/// </list>
/// </remarks>
public class SpatialGEVTrueParameters
{
    #region Site-Specific GEV Parameters

    /// <summary>
    /// True location parameters for each site (after applying link function and errors).
    /// </summary>
    public double[] Locations { get; set; } = Array.Empty<double>();

    /// <summary>
    /// True scale parameters for each site (after applying link function and errors).
    /// </summary>
    public double[] Scales { get; set; } = Array.Empty<double>();

    /// <summary>
    /// True shape parameters for each site (after applying link function and errors).
    /// </summary>
    public double[] Shapes { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Gets the number of sites.
    /// </summary>
    public int NumberOfSites => Locations.Length;

    #endregion

    #region Location Regression Coefficients

    /// <summary>
    /// Location intercept β₀ (in log-space if using log-link).
    /// </summary>
    public double LocationIntercept { get; set; }

    /// <summary>
    /// Location coefficient for X coordinate β₁.
    /// </summary>
    public double LocationBetaX { get; set; }

    /// <summary>
    /// Location coefficient for Y coordinate β₂.
    /// </summary>
    public double LocationBetaY { get; set; }

    /// <summary>
    /// Standard deviation of spatial errors for location (σ_loc).
    /// </summary>
    public double LocationErrorSigma { get; set; }

    /// <summary>
    /// Correlation range for location errors (km).
    /// </summary>
    public double LocationErrorRange { get; set; }

    /// <summary>
    /// Site-specific spatial errors for location (ε_loc_j).
    /// </summary>
    public double[] LocationErrors { get; set; } = Array.Empty<double>();

    #endregion

    #region Scale Regression Coefficients

    /// <summary>
    /// Scale intercept β₀ (in log-space if using log-link).
    /// </summary>
    public double ScaleIntercept { get; set; }

    /// <summary>
    /// Scale coefficient for X coordinate β₁.
    /// </summary>
    public double ScaleBetaX { get; set; }

    /// <summary>
    /// Scale coefficient for Y coordinate β₂.
    /// </summary>
    public double ScaleBetaY { get; set; }

    /// <summary>
    /// Standard deviation of spatial errors for scale (σ_scl).
    /// </summary>
    public double ScaleErrorSigma { get; set; }

    /// <summary>
    /// Correlation range for scale errors (km).
    /// </summary>
    public double ScaleErrorRange { get; set; }

    /// <summary>
    /// Site-specific spatial errors for scale (ε_scl_j).
    /// </summary>
    public double[] ScaleErrors { get; set; } = Array.Empty<double>();

    #endregion

    #region Shape Regression Coefficients

    /// <summary>
    /// Shape intercept β₀ (identity link).
    /// </summary>
    public double ShapeIntercept { get; set; }

    /// <summary>
    /// Shape coefficient for X coordinate β₁.
    /// </summary>
    public double ShapeBetaX { get; set; }

    /// <summary>
    /// Shape coefficient for Y coordinate β₂.
    /// </summary>
    public double ShapeBetaY { get; set; }

    /// <summary>
    /// Standard deviation of spatial errors for shape (σ_shp).
    /// </summary>
    public double ShapeErrorSigma { get; set; }

    /// <summary>
    /// Correlation range for shape errors (km).
    /// </summary>
    public double ShapeErrorRange { get; set; }

    /// <summary>
    /// Site-specific spatial errors for shape (ε_shp_j).
    /// </summary>
    public double[] ShapeErrors { get; set; } = Array.Empty<double>();

    #endregion

    #region Copula Parameters

    /// <summary>
    /// Whether the data was generated with Gaussian copula dependence.
    /// </summary>
    public bool HasCopula { get; set; }

    /// <summary>
    /// Copula correlation range parameter (km).
    /// </summary>
    public double CopulaRange { get; set; }

    /// <summary>
    /// Copula power parameter (for powered exponential).
    /// </summary>
    public double CopulaPower { get; set; } = 1.0;

    /// <summary>
    /// Type of correlation function used for copula.
    /// </summary>
    public CorrelationType CopulaCorrelationType { get; set; }

    #endregion

    #region Model Flags

    /// <summary>
    /// Whether the data was generated with spatial regression errors.
    /// </summary>
    public bool HasSpatialErrors { get; set; }

    /// <summary>
    /// Whether the data was generated with spatial regression (non-zero coefficients).
    /// </summary>
    public bool HasSpatialRegression { get; set; }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the true GEV parameters for a specific site.
    /// </summary>
    /// <param name="siteIndex">The site index (0-based).</param>
    /// <returns>Tuple of (location, scale, shape).</returns>
    public (double Location, double Scale, double Shape) GetSiteParameters(int siteIndex)
    {
        if (siteIndex < 0 || siteIndex >= NumberOfSites)
            throw new ArgumentOutOfRangeException(nameof(siteIndex));

        return (Locations[siteIndex], Scales[siteIndex], Shapes[siteIndex]);
    }

    /// <summary>
    /// Computes the true quantiles at a specific site.
    /// </summary>
    /// <param name="siteIndex">The site index (0-based).</param>
    /// <param name="probabilities">Non-exceedance probabilities.</param>
    /// <returns>Array of quantile values.</returns>
    public double[] ComputeSiteQuantiles(int siteIndex, double[] probabilities)
    {
        var (loc, scl, shp) = GetSiteParameters(siteIndex);
        var gev = new GeneralizedExtremeValue(loc, scl, shp);
        var quantiles = new double[probabilities.Length];
        for (int i = 0; i < probabilities.Length; i++)
        {
            quantiles[i] = gev.InverseCDF(probabilities[i]);
        }
        return quantiles;
    }

    /// <summary>
    /// Gets a summary string of the true parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Spatial GEV True Parameters ===");
        sb.AppendLine($"Number of sites: {NumberOfSites}");
        sb.AppendLine();

        sb.AppendLine("Location regression:");
        sb.AppendLine($"  β₀ = {LocationIntercept:F4}");
        sb.AppendLine($"  βX = {LocationBetaX:F6}");
        sb.AppendLine($"  βY = {LocationBetaY:F6}");
        if (LocationErrorSigma > 0)
            sb.AppendLine($"  σ = {LocationErrorSigma:F4}, range = {LocationErrorRange:F1} km");
        sb.AppendLine();

        sb.AppendLine("Scale regression:");
        sb.AppendLine($"  β₀ = {ScaleIntercept:F4}");
        sb.AppendLine($"  βX = {ScaleBetaX:F6}");
        sb.AppendLine($"  βY = {ScaleBetaY:F6}");
        if (ScaleErrorSigma > 0)
            sb.AppendLine($"  σ = {ScaleErrorSigma:F4}, range = {ScaleErrorRange:F1} km");
        sb.AppendLine();

        sb.AppendLine("Shape regression:");
        sb.AppendLine($"  β₀ = {ShapeIntercept:F4}");
        sb.AppendLine($"  βX = {ShapeBetaX:F6}");
        sb.AppendLine($"  βY = {ShapeBetaY:F6}");
        if (ShapeErrorSigma > 0)
            sb.AppendLine($"  σ = {ShapeErrorSigma:F4}, range = {ShapeErrorRange:F1} km");
        sb.AppendLine();

        if (HasCopula)
        {
            sb.AppendLine($"Copula: {CopulaCorrelationType}, range = {CopulaRange:F1} km");
        }

        return sb.ToString();
    }

    #endregion
}
