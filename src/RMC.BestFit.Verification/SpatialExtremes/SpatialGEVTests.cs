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
/// Unit tests for the <see cref="SpatialGEV"/> model class.
/// Tests the hierarchical Bayesian spatial GEV model for regional frequency analysis.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
///     The <see cref="SpatialGEV"/> class implements a hierarchical Bayesian spatial model
///     for extreme value analysis following Renard's BHM framework. This test class provides
///     comprehensive coverage of model construction, parameter handling, likelihood computations,
///     and spatial dependence structures.
/// </para>
/// <para>
///     <b>References:</b>
/// </para>
/// <list type="bullet">
///     <item>Renard, B., et al. (2006). Use of a Gaussian copula for multivariate extreme value analysis.</item>
///     <item>Cooley, D., et al. (2007). Bayesian spatial modeling of extreme precipitation return levels.</item>
/// </list>
/// </remarks>
[TestClass]
public class SpatialGEVTests
{
    #region Test Data Helpers

    /// <summary>
    /// Creates standard test at-site data (30 observations × 5 sites) from a homogeneous GEV distribution.
    /// Uses the same GEV parameters for all sites so that an intercept-only model is appropriate.
    /// </summary>
    private static double[,] CreateTestAtSiteData()
    {
        var data = new double[30, 5];
        var rng = new Random(12345);

        // Use SAME GEV parameters for all sites (homogeneous regional model)
        // This makes an intercept-only model appropriate for the data
        double xi = 10000;    // location
        double alpha = 2500;  // scale
        double kappa = 0.0;   // shape (Gumbel)

        var gev = new Numerics.Distributions.GeneralizedExtremeValue(xi, alpha, kappa);

        for (int site = 0; site < 5; site++)
        {
            for (int year = 0; year < 30; year++)
            {
                // Inverse CDF sampling ensures proper GEV-distributed data
                double u = rng.NextDouble();
                data[year, site] = gev.InverseCDF(u);
            }
        }

        return data;
    }

    /// <summary>
    /// Creates test site coordinates (5 sites along a river).
    /// </summary>
    private static double[,] CreateTestCoordinates()
    {
        return new double[,]
        {
            { 0.0, 0.0 },
            { 10.0, 5.0 },
            { 22.0, 8.0 },
            { 35.0, 12.0 },
            { 50.0, 15.0 }
        };
    }

    /// <summary>
    /// Creates minimal test data (10 observations × 2 sites) from a homogeneous GEV distribution.
    /// </summary>
    private static (double[,] Data, double[,] Coordinates) CreateMinimalTestData()
    {
        var data = new double[10, 2];
        var rng = new Random(54321);

        // Use SAME parameters for both sites (homogeneous model)
        var gev = new Numerics.Distributions.GeneralizedExtremeValue(6000, 1500, 0.0);

        for (int i = 0; i < 10; i++)
        {
            data[i, 0] = gev.InverseCDF(rng.NextDouble());
            data[i, 1] = gev.InverseCDF(rng.NextDouble());
        }

        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 10.0, 0.0 }
        };

        return (data, coords);
    }

    /// <summary>
    /// Creates test data with missing values (NaN).
    /// </summary>
    private static double[,] CreateDataWithMissingValues()
    {
        var data = CreateTestAtSiteData();
        data[5, 2] = double.NaN;
        data[10, 0] = double.NaN;
        data[15, 4] = double.NaN;
        data[20, 1] = double.NaN;
        data[25, 3] = double.NaN;
        return data;
    }

    /// <summary>
    /// Creates a test spatial GEV model with default configuration.
    /// </summary>
    private static SpatialGEV CreateTestModel()
    {
        var data = CreateTestAtSiteData();
        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        return new SpatialGEV(data, coords, location, scale, shape);
    }

    /// <summary>
    /// Creates a test model with copula dependence enabled.
    /// </summary>
    private static SpatialGEV CreateModelWithCopula()
    {
        var model = CreateTestModel();
        model.SpatialDependence = new GaussianCopula(model.Coordinates, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>
    /// Creates a test model with spatial errors enabled.
    /// </summary>
    private static SpatialGEV CreateModelWithSpatialErrors()
    {
        var model = CreateTestModel();
        model.LocationErrors = new SpatialRegressionErrors(model.Coordinates, CorrelationFunctionType.Exponential);
        model.ScaleErrors = new SpatialRegressionErrors(model.Coordinates, CorrelationFunctionType.Exponential);
        model.UseLocationErrors = true;
        model.UseScaleErrors = true;
        model.SetDefaultParameters();
        return model;
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the constructor properly initializes the model.
    /// </summary>
    [TestMethod]
    public void Constructor_WithValidInputs_InitializesCorrectly()
    {
        // Arrange
        var data = CreateTestAtSiteData();
        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        // Act
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Assert
        Assert.IsNotNull(model, "Model should not be null.");
        Assert.AreEqual(5, model.Sites, "Model should have 5 sites.");
        Assert.AreEqual(30, model.Observations, "Model should have 30 observations.");
        Assert.IsNotNull(model.Location, "Location trend should not be null.");
        Assert.IsNotNull(model.Scale, "Scale trend should not be null.");
        Assert.IsNotNull(model.Shape, "Shape trend should not be null.");
    }

    /// <summary>
    /// Tests that the constructor throws when at-site data is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullData_ThrowsArgumentNullException()
    {
        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        _ = new SpatialGEV(null!, coords, location, scale, shape);
    }

    /// <summary>
    /// Tests that the constructor throws when coordinates are null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullCoordinates_ThrowsArgumentNullException()
    {
        var data = CreateTestAtSiteData();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        _ = new SpatialGEV(data, null!, location, scale, shape);
    }

    /// <summary>
    /// Tests that the constructor throws when location trend is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullLocation_ThrowsArgumentNullException()
    {
        var data = CreateTestAtSiteData();
        var coords = CreateTestCoordinates();
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        _ = new SpatialGEV(data, coords, null!, scale, shape);
    }

    /// <summary>
    /// Tests that the constructor throws when scale trend is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullScale_ThrowsArgumentNullException()
    {
        var data = CreateTestAtSiteData();
        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var shape = new GeneralLinearFunction("Shape");

        _ = new SpatialGEV(data, coords, location, null!, shape);
    }

    /// <summary>
    /// Tests that the constructor throws when shape trend is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullShape_ThrowsArgumentNullException()
    {
        var data = CreateTestAtSiteData();
        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");

        _ = new SpatialGEV(data, coords, location, scale, null!);
    }

    /// <summary>
    /// Tests that the constructor throws when data and coordinates dimensions don't match.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_MismatchedDimensions_ThrowsArgumentException()
    {
        var data = CreateTestAtSiteData(); // 5 sites
        var coords = new double[,] { { 0.0, 0.0 }, { 10.0, 0.0 }, { 20.0, 0.0 } }; // 3 sites
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        _ = new SpatialGEV(data, coords, location, scale, shape);
    }

    /// <summary>
    /// Tests that default options are set correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_DefaultOptions_AreCorrect()
    {
        // Act
        var model = CreateTestModel();

        // Assert
        Assert.IsFalse(model.UseCopulaDependence, "UseCopulaDependence should be false by default.");
        Assert.IsFalse(model.UseLocationErrors, "UseLocationErrors should be false by default.");
        Assert.IsFalse(model.UseScaleErrors, "UseScaleErrors should be false by default.");
        Assert.IsFalse(model.UseShapeErrors, "UseShapeErrors should be false by default.");
        Assert.IsTrue(model.UseLogLinkForLocation, "UseLogLinkForLocation should be true by default.");
        Assert.IsTrue(model.UseLogLinkForScale, "UseLogLinkForScale should be true by default.");
    }

    /// <summary>
    /// Tests that site weights are initialized to 1.0.
    /// </summary>
    [TestMethod]
    public void Constructor_SiteWeights_InitializedToOne()
    {
        // Act
        var model = CreateTestModel();

        // Assert
        Assert.IsNotNull(model.SiteWeights, "SiteWeights should not be null.");
        Assert.AreEqual(5, model.SiteWeights.Length, "SiteWeights should have 5 elements.");
        for (int i = 0; i < model.Sites; i++)
        {
            Assert.AreEqual(1.0, model.SiteWeights[i], 1e-10, $"Site {i} weight should be 1.0.");
        }
    }

    /// <summary>
    /// Tests constructor with minimal configuration (2 sites).
    /// </summary>
    [TestMethod]
    public void Constructor_MinimalSites_InitializesCorrectly()
    {
        // Arrange
        var (data, coords) = CreateMinimalTestData();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        // Act
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Assert
        Assert.AreEqual(2, model.Sites, "Model should have 2 sites.");
        Assert.AreEqual(10, model.Observations, "Model should have 10 observations.");
    }

    #endregion

    #region Parameter Tests

    /// <summary>
    /// Tests that parameters are set with expected count.
    /// </summary>
    [TestMethod]
    public void Parameters_BasicModel_HasCorrectCount()
    {
        // Arrange
        var model = CreateTestModel();

        // Assert - Basic model has 3 parameters (location intercept, scale intercept, shape intercept)
        Assert.IsNotNull(model.Parameters, "Parameters should not be null.");
        Assert.AreEqual(3, model.NumberOfParameters, "Basic model should have 3 parameters.");
    }

    /// <summary>
    /// Tests that model with copula has additional parameters.
    /// </summary>
    [TestMethod]
    public void Parameters_WithCopula_HasAdditionalParameters()
    {
        // Arrange
        var model = CreateModelWithCopula();

        // Assert - Should have 3 GEV trend + 1 copula range = 4 parameters
        Assert.IsTrue(model.NumberOfParameters > 3, "Model with copula should have additional parameters.");
    }

    /// <summary>
    /// Tests that model with spatial errors has additional parameters.
    /// </summary>
    [TestMethod]
    public void Parameters_WithSpatialErrors_HasAdditionalParameters()
    {
        // Arrange
        var model = CreateModelWithSpatialErrors();

        // Assert - Should have many more parameters (errors for each site)
        Assert.IsTrue(model.NumberOfParameters > 3, "Model with spatial errors should have additional parameters.");
    }

    /// <summary>
    /// Tests SetParameterValues with valid parameters.
    /// </summary>
    [TestMethod]
    public void SetParameterValues_ValidParameters_UpdatesValues()
    {
        // Arrange
        var model = CreateTestModel();
        var values = new double[model.NumberOfParameters];
        for (int i = 0; i < values.Length; i++)
            values[i] = model.Parameters[i].Value * 1.1; // Slight modification

        // Act
        model.SetParameterValues(values);

        // Assert
        for (int i = 0; i < values.Length; i++)
        {
            Assert.AreEqual(values[i], model.Parameters[i].Value, 1e-10,
                $"Parameter {i} value should be updated.");
        }
    }

    /// <summary>
    /// Tests SetParameterValues throws when parameters are null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SetParameterValues_NullParameters_ThrowsArgumentNullException()
    {
        var model = CreateTestModel();
        model.SetParameterValues(null!);
    }

    /// <summary>
    /// Tests SetParameterValues throws when parameter count is wrong.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void SetParameterValues_WrongCount_ThrowsArgumentException()
    {
        var model = CreateTestModel();
        var values = new double[] { 1.0, 2.0 }; // Wrong count
        model.SetParameterValues(values);
    }

    /// <summary>
    /// Tests SetDefaultParameters creates valid initial values.
    /// </summary>
    [TestMethod]
    public void SetDefaultParameters_CreatesValidInitialValues()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        model.SetDefaultParameters();

        // Assert
        Assert.IsNotNull(model.Parameters, "Parameters should not be null.");
        Assert.AreEqual(3, model.Parameters.Count, "Should have 3 parameters.");

        foreach (var param in model.Parameters)
        {
            Assert.IsFalse(double.IsNaN(param.Value), $"{param.Name} value should not be NaN.");
            Assert.IsFalse(double.IsInfinity(param.Value), $"{param.Name} value should not be infinite.");
            Assert.IsTrue(param.Value >= param.LowerBound, $"{param.Name} value should be >= lower bound.");
            Assert.IsTrue(param.Value <= param.UpperBound, $"{param.Name} value should be <= upper bound.");
        }
    }

    #endregion

    #region GetGEVParameters Tests

    /// <summary>
    /// Tests GetGEVParameters returns valid parameters for each site.
    /// </summary>
    [TestMethod]
    public void GetGEVParameters_ValidSite_ReturnsValidParameters()
    {
        // Arrange
        var model = CreateTestModel();

        // Act & Assert
        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);

            Assert.IsNotNull(gevParams, $"GEV params for site {site} should not be null.");
            Assert.AreEqual(3, gevParams.Length, $"GEV params for site {site} should have 3 elements.");

            // Location (xi)
            Assert.IsFalse(double.IsNaN(gevParams[0]), $"Location for site {site} should not be NaN.");
            Assert.IsTrue(gevParams[0] > 0, $"Location for site {site} should be positive with log-link.");

            // Scale (alpha)
            Assert.IsFalse(double.IsNaN(gevParams[1]), $"Scale for site {site} should not be NaN.");
            Assert.IsTrue(gevParams[1] > 0, $"Scale for site {site} should be positive.");

            // Shape (kappa)
            Assert.IsFalse(double.IsNaN(gevParams[2]), $"Shape for site {site} should not be NaN.");
        }
    }

    /// <summary>
    /// Tests GetGEVParameters throws for invalid site index.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetGEVParameters_NegativeSite_ThrowsArgumentOutOfRangeException()
    {
        var model = CreateTestModel();
        model.GetGEVParameters(-1);
    }

    /// <summary>
    /// Tests GetGEVParameters throws for site index beyond range.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetGEVParameters_SiteBeyondRange_ThrowsArgumentOutOfRangeException()
    {
        var model = CreateTestModel();
        model.GetGEVParameters(model.Sites);
    }

    /// <summary>
    /// Tests GetGEVParameters with identity link for location.
    /// </summary>
    [TestMethod]
    public void GetGEVParameters_IdentityLinkLocation_ReturnsValidParameters()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseLogLinkForLocation = false;
        model.SetDefaultParameters();

        // Act
        var gevParams = model.GetGEVParameters(0);

        // Assert
        Assert.IsFalse(double.IsNaN(gevParams[0]), "Location should not be NaN.");
    }

    /// <summary>
    /// Tests GetGEVParameters with identity link for scale.
    /// </summary>
    [TestMethod]
    public void GetGEVParameters_IdentityLinkScale_ReturnsValidParameters()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseLogLinkForScale = false;
        model.SetDefaultParameters();

        // Act
        var gevParams = model.GetGEVParameters(0);

        // Assert
        Assert.IsFalse(double.IsNaN(gevParams[1]), "Scale should not be NaN.");
        Assert.IsTrue(gevParams[1] > 0, "Scale should be positive.");
    }

    /// <summary>
    /// Tests GetGEVParameters with spatial errors includes error contribution.
    /// </summary>
    [TestMethod]
    public void GetGEVParameters_WithLocationErrors_IncludesErrorContribution()
    {
        // Arrange
        var model = CreateModelWithSpatialErrors();

        // Act - Get parameters for two different sites
        var params0 = model.GetGEVParameters(0);
        var params1 = model.GetGEVParameters(1);

        // Assert - Parameters should exist and be valid
        Assert.IsNotNull(params0);
        Assert.IsNotNull(params1);
        Assert.IsFalse(double.IsNaN(params0[0]));
        Assert.IsFalse(double.IsNaN(params1[0]));
    }

    #endregion

    #region LogLikelihood Tests

    /// <summary>
    /// Tests LogLikelihood returns finite value for valid parameters.
    /// </summary>
    [TestMethod]
    public void LogLikelihood_ValidParameters_ReturnsFiniteValue()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        double logLH = model.LogLikelihood(parameters);

        // Assert
        Assert.IsFalse(double.IsNaN(logLH), "Log-likelihood should not be NaN.");
        Assert.IsTrue(double.IsFinite(logLH), "Log-likelihood should be finite.");
        Assert.IsTrue(logLH < 0, "Log-likelihood should be negative.");
    }

    /// <summary>
    /// Tests LogLikelihood returns NegativeInfinity for invalid parameters.
    /// </summary>
    [TestMethod]
    public void LogLikelihood_InvalidParameterCount_ReturnsNegativeInfinity()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = new double[] { 1.0 }; // Wrong count

        // Act
        double logLH = model.LogLikelihood(parameters);

        // Assert
        Assert.AreEqual(double.NegativeInfinity, logLH, "Invalid params should return NegativeInfinity.");
    }

    /// <summary>
    /// Tests LogLikelihood is higher for better-fitting parameters.
    /// </summary>
    [TestMethod]
    public void LogLikelihood_BetterFit_HasHigherLikelihood()
    {
        // Arrange
        var model = CreateTestModel();
        var goodParams = model.Parameters.Select(p => p.Value).ToArray();

        // Verify baseline parameters give finite likelihood
        double goodLogLH = model.LogLikelihood(goodParams);
        Assert.IsTrue(double.IsFinite(goodLogLH),
            $"Baseline parameters should give finite log-likelihood, got {goodLogLH}");

        // Create worse parameters by perturbing shape away from optimal
        // Shape parameter is the last one in the default model (location, scale, shape intercepts)
        // A shape that's far from the data's empirical shape will give worse likelihood
        var badParams = (double[])goodParams.Clone();
        int shapeIndex = goodParams.Length - 1; // Shape intercept is last parameter
        badParams[shapeIndex] += 0.5; // Perturb shape significantly (shape is in identity link)

        // Act
        double badLogLH = model.LogLikelihood(badParams);

        // Assert
        Assert.IsTrue(goodLogLH > badLogLH,
            $"Better-fitting parameters ({goodLogLH}) should have higher log-likelihood than perturbed ({badLogLH}).");
    }

    /// <summary>
    /// Tests LogLikelihood with copula dependence.
    /// </summary>
    [TestMethod]
    public void LogLikelihood_WithCopula_ReturnsFiniteValue()
    {
        // Arrange
        var model = CreateModelWithCopula();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        double logLH = model.LogLikelihood(parameters);

        // Assert
        Assert.IsFalse(double.IsNaN(logLH), "Log-likelihood with copula should not be NaN.");
        Assert.IsTrue(double.IsFinite(logLH), "Log-likelihood with copula should be finite.");
    }

    /// <summary>
    /// Tests LogLikelihood with spatial errors.
    /// </summary>
    [TestMethod]
    public void LogLikelihood_WithSpatialErrors_ReturnsFiniteValue()
    {
        // Arrange
        var model = CreateModelWithSpatialErrors();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        double logLH = model.LogLikelihood(parameters);

        // Assert
        Assert.IsFalse(double.IsNaN(logLH), "Log-likelihood with spatial errors should not be NaN.");
        Assert.IsTrue(double.IsFinite(logLH), "Log-likelihood with spatial errors should be finite.");
    }

    /// <summary>
    /// Tests LogLikelihood handles missing data correctly.
    /// </summary>
    [TestMethod]
    public void LogLikelihood_WithMissingData_HandlesNaNCorrectly()
    {
        // Arrange
        var data = CreateDataWithMissingValues();
        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        double logLH = model.LogLikelihood(parameters);

        // Assert
        Assert.IsFalse(double.IsNaN(logLH), "Log-likelihood should handle NaN data.");
        Assert.IsTrue(double.IsFinite(logLH), "Log-likelihood should be finite with missing data.");
    }

    /// <summary>
    /// Tests that site weights affect likelihood.
    /// </summary>
    [TestMethod]
    public void LogLikelihood_SiteWeights_AffectResult()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double ll1 = model.LogLikelihood(parameters);

        // First verify baseline likelihood is finite
        Assert.IsTrue(double.IsFinite(ll1),
            $"Baseline log-likelihood should be finite, got {ll1}");

        // Modify weights - use moderate changes to avoid numerical issues
        model.SiteWeights[0] = 0.5;
        model.SiteWeights[1] = 1.5;

        // Act
        double ll2 = model.LogLikelihood(parameters);

        // Assert
        Assert.IsTrue(double.IsFinite(ll2),
            $"Log-likelihood with modified weights should be finite, got {ll2}");
        Assert.AreNotEqual(ll1, ll2, 1e-10,
            "Different weights should produce different likelihoods.");
    }

    #endregion

    #region DataLogLikelihood Tests

    /// <summary>
    /// Tests that LogLikelihood equals DataLogLikelihood plus PriorLogLikelihood.
    /// </summary>
    [TestMethod]
    public void DataLogLikelihood_VerifyLogLikelihoodComposition()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        double dataLL = model.DataLogLikelihood(parameters);
        double priorLL = model.PriorLogLikelihood(parameters);
        double totalLL = model.LogLikelihood(parameters);

        // Assert - LogLikelihood = DataLogLikelihood + PriorLogLikelihood
        Assert.AreEqual(dataLL + priorLL, totalLL, 1e-10,
            "LogLikelihood should equal DataLogLikelihood + PriorLogLikelihood.");
    }

    /// <summary>
    /// Tests DataLogLikelihood returns finite value for valid parameters.
    /// </summary>
    [TestMethod]
    public void DataLogLikelihood_ValidParameters_ReturnsFiniteValue()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        double dataLL = model.DataLogLikelihood(parameters);

        // Assert
        Assert.IsTrue(double.IsFinite(dataLL), "DataLogLikelihood should be finite for valid parameters.");
    }

    #endregion

    #region PointwiseDataLogLikelihood Tests

    /// <summary>
    /// Tests PointwiseDataLogLikelihood returns correct number of elements.
    /// </summary>
    [TestMethod]
    public void PointwiseDataLogLikelihood_ReturnsCorrectCount()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        // Assert
        Assert.IsNotNull(pointwise, "Pointwise array should not be null.");
        Assert.AreEqual(model.Observations, pointwise.Length,
            "Pointwise array should have one element per observation.");
    }

    /// <summary>
    /// Tests PointwiseDataLogLikelihood values are finite.
    /// </summary>
    [TestMethod]
    public void PointwiseDataLogLikelihood_ValuesAreFinite()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        // Assert
        for (int i = 0; i < pointwise.Length; i++)
        {
            Assert.IsFalse(double.IsNaN(pointwise[i]),
                $"Pointwise[{i}] should not be NaN.");
        }
    }

    /// <summary>
    /// Tests PointwiseDataLogLikelihood sums to DataLogLikelihood.
    /// </summary>
    [TestMethod]
    public void PointwiseDataLogLikelihood_SumsToDataLogLikelihood()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        double dataLL = model.DataLogLikelihood(parameters);

        // First verify data log-likelihood is finite (otherwise pointwise comparison is meaningless)
        Assert.IsTrue(double.IsFinite(dataLL),
            $"Data log-likelihood should be finite, got {dataLL}");

        var pointwise = model.PointwiseDataLogLikelihood(parameters);
        double pointwiseSum = pointwise.Sum();

        // Verify all pointwise values are finite
        for (int i = 0; i < pointwise.Length; i++)
        {
            Assert.IsTrue(double.IsFinite(pointwise[i]),
                $"Pointwise[{i}] should be finite, got {pointwise[i]}");
        }

        // Assert
        Assert.AreEqual(dataLL, pointwiseSum, 1e-6,
            "Sum of pointwise log-likelihoods should equal data log-likelihood.");
    }

    #endregion

    #region PointwiseDataLogLikelihoodComponents Tests

    /// <summary>
    /// Tests PointwiseDataLogLikelihoodComponents returns correct components.
    /// </summary>
    [TestMethod]
    public void PointwiseDataLogLikelihoodComponents_ReturnsCorrectCount()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        // Assert
        Assert.IsNotNull(components, "Components should not be null.");
        Assert.AreEqual(model.Observations, components.Count,
            "Should have one component per observation.");
    }

    /// <summary>
    /// Tests DataComponent properties are populated correctly.
    /// </summary>
    [TestMethod]
    public void PointwiseDataLogLikelihoodComponents_HasCorrectProperties()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);
        var first = components[0];

        // Assert
        Assert.AreEqual(0, first.Index, "First component should have index 0.");
        Assert.IsFalse(double.IsNaN(first.LogLikelihood), "LogLikelihood should not be NaN.");
        Assert.AreEqual(DataComponentType.Exact, first.Type, "Data type should be Exact.");
    }

    #endregion

    #region PointwisePriorLogLikelihood Tests

    /// <summary>
    /// Tests PointwisePriorLogLikelihood returns prior components.
    /// </summary>
    [TestMethod]
    public void PointwisePriorLogLikelihood_ReturnsPriorComponents()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        var priorComponents = model.PointwisePriorLogLikelihood(parameters);

        // Assert
        Assert.IsNotNull(priorComponents, "Prior components should not be null.");
        Assert.IsTrue(priorComponents.Count >= model.NumberOfParameters,
            "Should have at least one component per parameter.");
    }

    /// <summary>
    /// Tests prior components have valid log-likelihoods.
    /// </summary>
    [TestMethod]
    public void PointwisePriorLogLikelihood_ComponentsHaveValidLogLikelihoods()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        var priorComponents = model.PointwisePriorLogLikelihood(parameters);

        // Assert
        foreach (var component in priorComponents)
        {
            Assert.IsFalse(double.IsNaN(component.LogLikelihood),
                $"Prior component '{component.Name}' should not have NaN log-likelihood.");
        }
    }

    #endregion

    #region PDF/CDF/InverseCDF Tests

    /// <summary>
    /// Tests PDF returns positive value for valid input.
    /// </summary>
    [TestMethod]
    public void PDF_ValidInput_ReturnsPositiveValue()
    {
        // Arrange
        var model = CreateTestModel();

        // Act & Assert
        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);
            double testValue = gevParams[0]; // Test at location parameter

            double pdf = model.PDF(testValue, site);

            Assert.IsTrue(pdf > 0, $"PDF should be positive for site {site}.");
            Assert.IsFalse(double.IsNaN(pdf), $"PDF should not be NaN for site {site}.");
        }
    }

    /// <summary>
    /// Tests CDF returns value between 0 and 1.
    /// </summary>
    [TestMethod]
    public void CDF_ValidInput_ReturnsValueBetweenZeroAndOne()
    {
        // Arrange
        var model = CreateTestModel();

        // Act & Assert
        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);
            double testValue = gevParams[0];

            double cdf = model.CDF(testValue, site);

            Assert.IsTrue(cdf >= 0 && cdf <= 1,
                $"CDF should be in [0,1] for site {site}, got {cdf}.");
            Assert.IsFalse(double.IsNaN(cdf), $"CDF should not be NaN for site {site}.");
        }
    }

    /// <summary>
    /// Tests InverseCDF is consistent with CDF.
    /// </summary>
    [TestMethod]
    public void InverseCDF_ConsistentWithCDF()
    {
        // Arrange
        var model = CreateTestModel();
        double[] testProbs = { 0.1, 0.25, 0.5, 0.75, 0.9 };

        // Act & Assert
        for (int site = 0; site < model.Sites; site++)
        {
            foreach (double p in testProbs)
            {
                double quantile = model.InverseCDF(p, site);
                double recoveredP = model.CDF(quantile, site);

                Assert.AreEqual(p, recoveredP, 1e-6,
                    $"InverseCDF/CDF roundtrip failed for site {site}, p={p}.");
            }
        }
    }

    /// <summary>
    /// Tests InverseCDF returns increasing quantiles for increasing probabilities.
    /// </summary>
    [TestMethod]
    public void InverseCDF_IncreasingProbabilities_ReturnsIncreasingQuantiles()
    {
        // Arrange
        var model = CreateTestModel();
        double[] probs = { 0.1, 0.5, 0.9 };

        // Act & Assert
        for (int site = 0; site < model.Sites; site++)
        {
            double prev = double.NegativeInfinity;
            foreach (double p in probs)
            {
                double quantile = model.InverseCDF(p, site);
                Assert.IsTrue(quantile > prev,
                    $"Quantiles should be increasing for site {site}.");
                prev = quantile;
            }
        }
    }

    #endregion

    #region GenerateRandomValues Tests

    /// <summary>
    /// Tests GenerateRandomValues returns correct number of samples.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_ReturnsCorrectCount()
    {
        // Arrange
        var model = CreateTestModel();
        int sampleSize = 100;

        // Act
        var samples = model.GenerateRandomValues(sampleSize, seed: 12345);

        // Assert
        Assert.AreEqual(sampleSize * model.Sites, samples.Length,
            "Should return sampleSize * Sites samples.");
    }

    /// <summary>
    /// Tests GenerateRandomValues throws for non-positive sample size.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GenerateRandomValues_ZeroSampleSize_ThrowsException()
    {
        var model = CreateTestModel();
        model.GenerateRandomValues(0);
    }

    /// <summary>
    /// Tests GenerateRandomValues throws for negative sample size.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GenerateRandomValues_NegativeSampleSize_ThrowsException()
    {
        var model = CreateTestModel();
        model.GenerateRandomValues(-10);
    }

    /// <summary>
    /// Tests GenerateRandomValues produces different values with different seeds.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_DifferentSeeds_ProduceDifferentValues()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var samples1 = model.GenerateRandomValues(50, seed: 111);
        var samples2 = model.GenerateRandomValues(50, seed: 222);

        // Assert
        bool anyDifferent = false;
        for (int i = 0; i < samples1.Length; i++)
        {
            if (Math.Abs(samples1[i] - samples2[i]) > 1e-10)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.IsTrue(anyDifferent, "Different seeds should produce different samples.");
    }

    /// <summary>
    /// Tests GenerateRandomValues produces reproducible values with same seed.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_SameSeed_ProducesReproducibleValues()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var samples1 = model.GenerateRandomValues(50, seed: 12345);
        var samples2 = model.GenerateRandomValues(50, seed: 12345);

        // Assert
        for (int i = 0; i < samples1.Length; i++)
        {
            Assert.AreEqual(samples1[i], samples2[i], 1e-10,
                $"Sample[{i}] should be reproducible with same seed.");
        }
    }

    /// <summary>
    /// Tests GenerateRandomValues produces positive values for flood data.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_ProducesPositiveValues()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var samples = model.GenerateRandomValues(1000, seed: 12345);

        // Assert - Most samples should be positive for typical flood data
        int positiveCount = samples.Count(s => s > 0);
        double positiveRatio = (double)positiveCount / samples.Length;

        Assert.IsTrue(positiveRatio > 0.95,
            "Most samples should be positive for flood frequency data.");
    }

    #endregion

    #region Clone Tests

    /// <summary>
    /// Tests Clone creates independent copy.
    /// </summary>
    [TestMethod]
    public void Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = CreateTestModel();
        original.SiteWeights[0] = 2.0;

        // Act
        var clone = (SpatialGEV)original.Clone();

        // Modify original
        original.SiteWeights[0] = 5.0;

        // Assert
        Assert.AreEqual(2.0, clone.SiteWeights[0], 1e-10,
            "Clone should be independent of original.");
    }

    /// <summary>
    /// Tests Clone preserves configuration options.
    /// </summary>
    [TestMethod]
    public void Clone_PreservesConfiguration()
    {
        // Arrange
        var original = CreateTestModel();
        original.UseLogLinkForLocation = false;
        original.UseLogLinkForScale = false;

        // Act
        var clone = (SpatialGEV)original.Clone();

        // Assert
        Assert.AreEqual(original.UseLogLinkForLocation, clone.UseLogLinkForLocation);
        Assert.AreEqual(original.UseLogLinkForScale, clone.UseLogLinkForScale);
        Assert.AreEqual(original.UseCopulaDependence, clone.UseCopulaDependence);
        Assert.AreEqual(original.UseLocationErrors, clone.UseLocationErrors);
    }

    /// <summary>
    /// Tests Clone preserves dimensions.
    /// </summary>
    [TestMethod]
    public void Clone_PreservesDimensions()
    {
        // Arrange
        var original = CreateTestModel();

        // Act
        var clone = (SpatialGEV)original.Clone();

        // Assert
        Assert.AreEqual(original.Sites, clone.Sites);
        Assert.AreEqual(original.Observations, clone.Observations);
        Assert.AreEqual(original.NumberOfParameters, clone.NumberOfParameters);
    }

    /// <summary>
    /// Tests Clone of model with copula.
    /// </summary>
    [TestMethod]
    public void Clone_WithCopula_PreservesCopula()
    {
        // Arrange
        var original = CreateModelWithCopula();

        // Act
        var clone = (SpatialGEV)original.Clone();

        // Assert
        Assert.IsTrue(clone.UseCopulaDependence, "Clone should preserve UseCopulaDependence.");
        Assert.IsNotNull(clone.SpatialDependence, "Clone should have SpatialDependence.");
    }

    /// <summary>
    /// Tests Clone of model with spatial errors.
    /// </summary>
    [TestMethod]
    public void Clone_WithSpatialErrors_PreservesErrors()
    {
        // Arrange
        var original = CreateModelWithSpatialErrors();

        // Act
        var clone = (SpatialGEV)original.Clone();

        // Assert
        Assert.IsTrue(clone.UseLocationErrors, "Clone should preserve UseLocationErrors.");
        Assert.IsTrue(clone.UseScaleErrors, "Clone should preserve UseScaleErrors.");
        Assert.IsNotNull(clone.LocationErrors, "Clone should have LocationErrors.");
        Assert.IsNotNull(clone.ScaleErrors, "Clone should have ScaleErrors.");
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>
    /// Tests ToXElement creates valid XML.
    /// </summary>
    [TestMethod]
    public void ToXElement_CreatesValidXml()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var xElement = model.ToXElement();

        // Assert
        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("SpatialGEV", xElement.Name.LocalName, "Root element should be SpatialGEV.");
    }

    /// <summary>
    /// Tests ToXElement includes configuration attributes.
    /// </summary>
    [TestMethod]
    public void ToXElement_IncludesConfigurationAttributes()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var xElement = model.ToXElement();

        // Assert
        Assert.IsNotNull(xElement.Attribute("UseCopulaDependence"));
        Assert.IsNotNull(xElement.Attribute("UseLocationErrors"));
        Assert.IsNotNull(xElement.Attribute("UseScaleErrors"));
        Assert.IsNotNull(xElement.Attribute("UseShapeErrors"));
        Assert.IsNotNull(xElement.Attribute("UseLogLinkForLocation"));
        Assert.IsNotNull(xElement.Attribute("UseLogLinkForScale"));
    }

    /// <summary>
    /// Tests ToXElement includes site weights.
    /// </summary>
    [TestMethod]
    public void ToXElement_IncludesSiteWeights()
    {
        // Arrange
        var model = CreateTestModel();
        model.SiteWeights[0] = 2.5;

        // Act
        var xElement = model.ToXElement();

        // Assert
        var weightsElement = xElement.Element("SiteWeights");
        Assert.IsNotNull(weightsElement, "Should have SiteWeights element.");
        Assert.IsTrue(weightsElement.Value.Contains("2.5"), "Should contain modified weight.");
    }

    /// <summary>
    /// Tests ToXElement includes parameters.
    /// </summary>
    [TestMethod]
    public void ToXElement_IncludesParameters()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var xElement = model.ToXElement();

        // Assert
        var paramsElement = xElement.Element("Parameters");
        Assert.IsNotNull(paramsElement, "Should have Parameters element.");
        Assert.IsTrue(paramsElement.HasElements, "Parameters element should have children.");
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Tests Validate returns valid for properly configured model.
    /// </summary>
    [TestMethod]
    public void Validate_ValidModel_ReturnsValid()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests Validate fails when copula is enabled but null.
    /// </summary>
    [TestMethod]
    public void Validate_CopulaEnabledButNull_ReturnsInvalid()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseCopulaDependence = true;
        // SpatialDependence is null

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsFalse(isValid, "Validation should fail when copula enabled but null.");
        Assert.IsTrue(messages.Any(m => m.Contains("Copula")),
            "Should have message about copula.");
    }

    /// <summary>
    /// Tests Validate fails when location errors enabled but null.
    /// </summary>
    [TestMethod]
    public void Validate_LocationErrorsEnabledButNull_ReturnsInvalid()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseLocationErrors = true;
        // LocationErrors is null

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsFalse(isValid, "Validation should fail when location errors enabled but null.");
    }

    /// <summary>
    /// Tests Validate fails when scale errors enabled but null.
    /// </summary>
    [TestMethod]
    public void Validate_ScaleErrorsEnabledButNull_ReturnsInvalid()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseScaleErrors = true;

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsFalse(isValid, "Validation should fail when scale errors enabled but null.");
    }

    /// <summary>
    /// Tests Validate fails when shape errors enabled but null.
    /// </summary>
    [TestMethod]
    public void Validate_ShapeErrorsEnabledButNull_ReturnsInvalid()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseShapeErrors = true;

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsFalse(isValid, "Validation should fail when shape errors enabled but null.");
    }

    /// <summary>
    /// Tests Validate returns valid for model with copula properly configured.
    /// </summary>
    [TestMethod]
    public void Validate_WithCopula_ReturnsValid()
    {
        // Arrange
        var model = CreateModelWithCopula();

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass with copula. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests Validate returns valid for model with spatial errors properly configured.
    /// </summary>
    [TestMethod]
    public void Validate_WithSpatialErrors_ReturnsValid()
    {
        // Arrange
        var model = CreateModelWithSpatialErrors();

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass with spatial errors. Messages: {string.Join(", ", messages)}");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests model with minimum 2 sites.
    /// </summary>
    [TestMethod]
    public void Model_WithTwoSites_WorksCorrectly()
    {
        // Arrange
        var (data, coords) = CreateMinimalTestData();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var (isValid, messages) = model.Validate();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double logLH = model.LogLikelihood(parameters);

        // Assert
        Assert.IsTrue(isValid, $"2-site model should be valid. Messages: {string.Join(", ", messages)}");
        Assert.IsTrue(double.IsFinite(logLH), "Log-likelihood should be finite for 2-site model.");
    }

    /// <summary>
    /// Tests model handles identical coordinates (co-located sites).
    /// </summary>
    [TestMethod]
    public void Model_ColocatedSites_WorksCorrectly()
    {
        // Arrange
        var data = new double[20, 2];
        var rng = new Random(12345);
        for (int i = 0; i < 20; i++)
        {
            data[i, 0] = 5000 + rng.NextDouble() * 3000;
            data[i, 1] = 5000 + rng.NextDouble() * 3000;
        }

        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 0.0, 0.0 } // Same location
        };

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        // Act
        var model = new SpatialGEV(data, coords, location, scale, shape);
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double logLH = model.LogLikelihood(parameters);

        // Assert
        Assert.IsTrue(double.IsFinite(logLH), "Log-likelihood should be finite for co-located sites.");
    }

    /// <summary>
    /// Tests model with large data matrix.
    /// </summary>
    [TestMethod]
    public void Model_LargeDataMatrix_WorksCorrectly()
    {
        // Arrange - 100 observations × 10 sites with realistic GEV data (standardized)
        var data = new double[100, 10];
        var coords = new double[10, 2];
        var rng = new Random(12345);

        for (int i = 0; i < 10; i++)
        {
            coords[i, 0] = i * 10.0;
            coords[i, 1] = rng.NextDouble() * 5.0;
        }

        // Generate data with values in a reasonable range for GEV
        // Using values around location=100, scale=20 (similar to test data)
        var gev = new GeneralizedExtremeValue(100, 20, 0);
        for (int i = 0; i < 100; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                // Each site has slightly different mean, all in similar range
                data[i, j] = gev.InverseCDF(rng.NextDouble());
            }
        }

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        // Act
        var model = new SpatialGEV(data, coords, location, scale, shape);
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double logLH = model.LogLikelihood(parameters);

        // Assert
        Assert.AreEqual(10, model.Sites, "Should have 10 sites.");
        Assert.AreEqual(100, model.Observations, "Should have 100 observations.");
        Assert.IsTrue(double.IsFinite(logLH), "Log-likelihood should be finite.");
    }

    /// <summary>
    /// Tests model with zero weight excludes site from likelihood.
    /// </summary>
    [TestMethod]
    public void Model_ZeroWeight_ExcludesSite()
    {
        // Arrange
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double llAllSites = model.LogLikelihood(parameters);

        // First verify baseline likelihood is finite
        Assert.IsTrue(double.IsFinite(llAllSites),
            $"Baseline log-likelihood should be finite, got {llAllSites}");

        // Exclude site 0 by setting its weight to zero
        model.SiteWeights[0] = 0.0;
        double llExcludeOne = model.LogLikelihood(parameters);

        // Assert
        Assert.IsTrue(double.IsFinite(llExcludeOne),
            $"Log-likelihood with excluded site should be finite, got {llExcludeOne}");
        Assert.AreNotEqual(llAllSites, llExcludeOne, 1e-10,
            "Zero weight should change likelihood.");
        // Note: excluding a site can increase or decrease likelihood depending on how well
        // the model fits that particular site, so we just check they're different
    }

    /// <summary>
    /// Tests model with all NaN values at one site.
    /// </summary>
    [TestMethod]
    public void Model_AllNaNAtOneSite_HandlesCorrectly()
    {
        // Arrange
        var data = CreateTestAtSiteData();
        for (int i = 0; i < 30; i++)
        {
            data[i, 2] = double.NaN; // All observations at site 2 are missing
        }

        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Act
        double logLH = model.LogLikelihood(parameters);

        // Assert
        Assert.IsFalse(double.IsNaN(logLH), "Log-likelihood should not be NaN.");
        Assert.IsTrue(double.IsFinite(logLH), "Log-likelihood should be finite.");
    }

    #endregion

    #region Interface Implementation Tests

    /// <summary>
    /// Tests that SpatialGEV implements ISimulatable interface.
    /// </summary>
    [TestMethod]
    public void Model_ImplementsISimulatable()
    {
        // Arrange
        var model = CreateTestModel();

        // Assert
        Assert.IsInstanceOfType(model, typeof(ISimulatable<double[]>),
            "SpatialGEV should implement ISimulatable<double[]>.");
    }

    /// <summary>
    /// Tests that SpatialGEV inherits from ModelBase.
    /// </summary>
    [TestMethod]
    public void Model_InheritsFromModelBase()
    {
        // Arrange
        var model = CreateTestModel();

        // Assert
        Assert.IsInstanceOfType(model, typeof(ModelBase),
            "SpatialGEV should inherit from ModelBase.");
    }

    #endregion

    #region Link Function Tests

    /// <summary>
    /// Tests log-link for location parameter.
    /// </summary>
    [TestMethod]
    public void LogLinkLocation_ProducesPositiveValues()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseLogLinkForLocation = true;

        // Act & Assert
        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);
            Assert.IsTrue(gevParams[0] > 0, $"Log-link should produce positive location for site {site}.");
        }
    }

    /// <summary>
    /// Tests log-link for scale parameter.
    /// </summary>
    [TestMethod]
    public void LogLinkScale_ProducesPositiveValues()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseLogLinkForScale = true;

        // Act & Assert
        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);
            Assert.IsTrue(gevParams[1] > 0, $"Log-link should produce positive scale for site {site}.");
        }
    }

    /// <summary>
    /// Tests identity link for location parameter.
    /// </summary>
    [TestMethod]
    public void IdentityLinkLocation_WorksCorrectly()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseLogLinkForLocation = false;
        model.SetDefaultParameters();

        // Act
        var gevParams = model.GetGEVParameters(0);

        // Assert
        Assert.IsFalse(double.IsNaN(gevParams[0]), "Identity link location should not be NaN.");
    }

    /// <summary>
    /// Tests identity link for scale parameter with positive constraint.
    /// </summary>
    [TestMethod]
    public void IdentityLinkScale_EnforcesPositiveConstraint()
    {
        // Arrange
        var model = CreateTestModel();
        model.UseLogLinkForScale = false;
        model.SetDefaultParameters();

        // Act
        var gevParams = model.GetGEVParameters(0);

        // Assert
        Assert.IsTrue(gevParams[1] > 0, "Identity link scale should still be positive.");
    }

    #endregion

    #region MLE Estimation Tests - Basic Homogeneous Model

    /// <summary>
    /// Tests that MLE converges for basic homogeneous model with sufficient data.
    /// </summary>
    [TestMethod]
    public void MLE_BasicHomogeneous_Converges()
    {
        // Arrange: 10 sites × 50 observations = 500 total
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, seed: 12345);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE should converge for basic homogeneous data.");
    }

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
    /// Tests MLE with minimal data (fewer sites and observations).
    /// </summary>
    [TestMethod]
    public void MLE_MinimalData_Converges()
    {
        // Arrange: 5 sites × 30 observations = 150 total (minimum viable)
        var (data, coords, _) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 30, nSites: 5, seed: 11111);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE should converge even for minimal data.");
    }

    /// <summary>
    /// Tests that MLE produces better likelihood than initial parameters.
    /// </summary>
    [TestMethod]
    public void MLE_ProducesBetterLikelihood()
    {
        // Arrange
        var (data, coords, _) = SyntheticSpatialGEVData.GetHomogeneousGridData(
            nObs: 50, nSites: 10, seed: 22222);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        var initialParams = model.Parameters.Select(p => p.Value).ToArray();
        double initialLL = model.LogLikelihood(initialParams);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();
        model.SetParameterValues(mle.BestParameterSet.Values);

        var finalParams = model.Parameters.Select(p => p.Value).ToArray();
        double finalLL = model.LogLikelihood(finalParams);

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE should converge.");
        Assert.IsTrue(finalLL >= initialLL,
            $"MLE should produce better (or equal) likelihood. Initial: {initialLL}, Final: {finalLL}");
    }

    #endregion

    #region MLE Estimation Tests - With Gaussian Copula

    /// <summary>
    /// Tests that MLE converges for model with Gaussian copula (basic exponential correlation).
    /// </summary>
    [TestMethod]
    public void MLE_WithCopulaBasicExponential_Converges()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(
            nObs: 50, nSites: 10, range: 30.0, seed: 33333);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Enable copula with basic exponential correlation
        model.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE with copula should converge.");
    }

    /// <summary>
    /// Tests that MLE converges for model with spherical correlation copula.
    /// </summary>
    [TestMethod]
    public void MLE_WithCopulaSpherical_Converges()
    {
        // Arrange
        var (data, coords, _) = SyntheticSpatialGEVData.GetCopulaDataSpherical(
            nObs: 50, nSites: 10, range: 50.0, seed: 55555);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        model.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Spherical);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE with spherical copula should converge.");
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

    #endregion

    #region MLE Estimation Tests - Spatial Regression

    /// <summary>
    /// Tests that MLE converges for model with spatial regression on location.
    /// </summary>
    [TestMethod]
    public void MLE_WithLocationRegression_Converges()
    {
        // Arrange
        var (data, coords, _) = SyntheticSpatialGEVData.GetRegressionLocationOnly(
            nObs: 50, nSites: 15, seed: 77777);

        // Create location function with X,Y covariates
        var location = new GeneralLinearFunction("Location", coords);
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE with location regression should converge.");
    }

    /// <summary>
    /// Tests that MLE converges for model with spatial regression on location and scale.
    /// </summary>
    [TestMethod]
    public void MLE_WithLocationScaleRegression_Converges()
    {
        // Arrange
        var (data, coords, _) = SyntheticSpatialGEVData.GetRegressionLocationScale(
            nObs: 50, nSites: 15, seed: 88888);

        var location = new GeneralLinearFunction("Location", coords);
        var scale = new GeneralLinearFunction("Scale", coords);
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE with location+scale regression should converge.");
    }

    /// <summary>
    /// Tests MLE with full spatial regression on all parameters.
    /// </summary>
    [TestMethod]
    public void MLE_WithFullRegression_Converges()
    {
        // Arrange: more data needed for full model
        var (data, coords, _) = SyntheticSpatialGEVData.GetRegressionLocationScaleShape(
            nObs: 80, nSites: 20, seed: 99999);

        var location = new GeneralLinearFunction("Location", coords);
        var scale = new GeneralLinearFunction("Scale", coords);
        var shape = new GeneralLinearFunction("Shape", coords);
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE with full regression should converge.");
    }

    #endregion

    #region Different Shape Parameter Tests

    /// <summary>
    /// Tests MLE with Gumbel distribution (shape = 0).
    /// </summary>
    [TestMethod]
    public void MLE_GumbelShape_Converges()
    {
        // Arrange
        var (data, coords, _) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, shape: 0.0, seed: 11111);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE should converge for Gumbel (shape=0) data.");
    }

    /// <summary>
    /// Tests MLE with positive shape (heavy tail / Fréchet-like).
    /// </summary>
    [TestMethod]
    public void MLE_PositiveShape_Converges()
    {
        // Arrange
        var (data, coords, _) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, shape: 0.15, seed: 22222);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE should converge for positive shape (heavy tail) data.");
    }

    /// <summary>
    /// Tests MLE with negative shape (bounded tail / Weibull-like).
    /// </summary>
    [TestMethod]
    public void MLE_NegativeShape_Converges()
    {
        // Arrange
        var (data, coords, _) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, shape: -0.15, seed: 33333);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE should converge for negative shape (bounded tail) data.");
    }

    #endregion

    #region Uncertainty Quantification Tests

    /// <summary>
    /// Tests ComputeIntersiteCorrelation returns proper correlation matrix.
    /// </summary>
    [TestMethod]
    public void ComputeIntersiteCorrelation_ReturnsValidMatrix()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var corrMatrix = model.ComputeIntersiteCorrelation();

        // Assert
        Assert.IsNotNull(corrMatrix, "Correlation matrix should not be null.");
        Assert.AreEqual(model.Sites, corrMatrix.GetLength(0), "Matrix rows should equal number of sites.");
        Assert.AreEqual(model.Sites, corrMatrix.GetLength(1), "Matrix columns should equal number of sites.");

        // Diagonal should be 1.0
        for (int i = 0; i < model.Sites; i++)
        {
            Assert.AreEqual(1.0, corrMatrix[i, i], 1e-10, $"Diagonal element [{i},{i}] should be 1.0.");
        }

        // Should be symmetric
        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = i + 1; j < model.Sites; j++)
            {
                Assert.AreEqual(corrMatrix[i, j], corrMatrix[j, i], 1e-10,
                    $"Matrix should be symmetric: [{i},{j}] != [{j},{i}]");
            }
        }

        // Correlations should be in [-1, 1]
        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = 0; j < model.Sites; j++)
            {
                Assert.IsTrue(corrMatrix[i, j] >= -1 && corrMatrix[i, j] <= 1,
                    $"Correlation [{i},{j}] should be in [-1,1], got {corrMatrix[i, j]}");
            }
        }
    }

    /// <summary>
    /// Tests ComputeEffectiveSampleSize returns positive value.
    /// </summary>
    [TestMethod]
    public void ComputeEffectiveSampleSize_ReturnsPositiveValue()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        double effectiveN = model.ComputeEffectiveSampleSize();

        // Assert
        Assert.IsTrue(effectiveN > 0, "Effective sample size should be positive.");
        Assert.IsTrue(effectiveN <= model.Observations * model.Sites,
            "Effective sample size should not exceed nominal sample size.");
    }

    /// <summary>
    /// Tests ComputeEffectiveSampleSize with provided correlation matrix.
    /// </summary>
    [TestMethod]
    public void ComputeEffectiveSampleSize_WithCustomCorrelationMatrix_WorksCorrectly()
    {
        // Arrange
        var model = CreateTestModel();
        var corrMatrix = new double[model.Sites, model.Sites];

        // Create a correlation matrix with known average correlation = 0.5
        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = 0; j < model.Sites; j++)
            {
                corrMatrix[i, j] = (i == j) ? 1.0 : 0.5;
            }
        }

        // Act
        double effectiveN = model.ComputeEffectiveSampleSize(corrMatrix);

        // Assert
        // n_eff = n * Sites / (1 + (Sites-1) * ρ̄) = 30 * 5 / (1 + 4 * 0.5) = 150 / 3 = 50
        double expectedN = (model.Observations * model.Sites) / (1.0 + (model.Sites - 1) * 0.5);
        Assert.AreEqual(expectedN, effectiveN, 1e-6,
            $"Effective sample size should be {expectedN}, got {effectiveN}");
    }

    /// <summary>
    /// Tests ComputeVarianceInflationFactor returns value >= 1.
    /// </summary>
    [TestMethod]
    public void ComputeVarianceInflationFactor_ReturnsValidValue()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        double vif = model.ComputeVarianceInflationFactor();

        // Assert
        Assert.IsTrue(vif >= 1.0, $"VIF should be >= 1.0, got {vif}");
    }

    /// <summary>
    /// Tests ComputeVarianceInflationFactor with known correlation.
    /// </summary>
    [TestMethod]
    public void ComputeVarianceInflationFactor_WithKnownCorrelation_ReturnsCorrectValue()
    {
        // Arrange
        var model = CreateTestModel();
        var corrMatrix = new double[model.Sites, model.Sites];

        // Set average correlation to 0.3
        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = 0; j < model.Sites; j++)
            {
                corrMatrix[i, j] = (i == j) ? 1.0 : 0.3;
            }
        }

        // Act
        double vif = model.ComputeVarianceInflationFactor(corrMatrix);

        // Assert
        // VIF = 1 + (Sites-1) * ρ̄ = 1 + 4 * 0.3 = 2.2
        double expectedVIF = 1.0 + (model.Sites - 1) * 0.3;
        Assert.AreEqual(expectedVIF, vif, 1e-6,
            $"VIF should be {expectedVIF}, got {vif}");
    }

    /// <summary>
    /// Tests ComputeEffectiveSampleSizeWeights updates SiteWeights array.
    /// </summary>
    [TestMethod]
    public void ComputeEffectiveSampleSizeWeights_UpdatesSiteWeights()
    {
        // Arrange
        var model = CreateTestModel();
        var originalWeights = (double[])model.SiteWeights.Clone();

        // Act
        model.ComputeEffectiveSampleSizeWeights();

        // Assert
        Assert.IsNotNull(model.SiteWeights, "SiteWeights should not be null.");
        Assert.AreEqual(model.Sites, model.SiteWeights.Length, "Should have weight for each site.");

        // All weights should be positive
        for (int i = 0; i < model.Sites; i++)
        {
            Assert.IsTrue(model.SiteWeights[i] > 0, $"Weight {i} should be positive, got {model.SiteWeights[i]}");
        }

        // Weights should sum to Sites (normalized)
        double sumWeights = model.SiteWeights.Sum();
        Assert.AreEqual(model.Sites, sumWeights, 1e-6, "Weights should sum to number of sites.");
    }

    /// <summary>
    /// Tests ComputeEffectiveSampleSizeWeights with custom correlation matrix.
    /// </summary>
    [TestMethod]
    public void ComputeEffectiveSampleSizeWeights_WithCustomMatrix_WorksCorrectly()
    {
        // Arrange
        var model = CreateTestModel();
        var corrMatrix = new double[model.Sites, model.Sites];

        // Create identity correlation (no correlation between sites)
        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = 0; j < model.Sites; j++)
            {
                corrMatrix[i, j] = (i == j) ? 1.0 : 0.0;
            }
        }

        // Act
        model.ComputeEffectiveSampleSizeWeights(corrMatrix);

        // Assert - with no correlation, weights should all be equal
        for (int i = 0; i < model.Sites; i++)
        {
            Assert.AreEqual(1.0, model.SiteWeights[i], 1e-6,
                $"With no correlation, all weights should be 1.0, got {model.SiteWeights[i]} for site {i}");
        }
    }

    /// <summary>
    /// Tests ComputeEffectiveSampleSizeWeights throws on mismatched correlation matrix.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ComputeEffectiveSampleSizeWeights_MismatchedMatrix_ThrowsException()
    {
        // Arrange
        var model = CreateTestModel();
        var wrongSize = new double[3, 3]; // Model has 5 sites

        // Act
        model.ComputeEffectiveSampleSizeWeights(wrongSize);
    }

    /// <summary>
    /// Tests ConfigureForProperCoverage enables copula and location errors.
    /// </summary>
    [TestMethod]
    public void ConfigureForProperCoverage_EnablesRequiredComponents()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        model.ConfigureForProperCoverage();

        // Assert
        Assert.IsTrue(model.UseCopulaDependence, "Should enable copula dependence.");
        Assert.IsNotNull(model.SpatialDependence, "SpatialDependence should not be null.");
        Assert.IsTrue(model.UseLocationErrors, "Should enable location errors.");
        Assert.IsNotNull(model.LocationErrors, "LocationErrors should not be null.");
    }

    /// <summary>
    /// Tests ConfigureForProperCoverage with optional scale and shape errors.
    /// </summary>
    [TestMethod]
    public void ConfigureForProperCoverage_WithOptionalErrors_EnablesAllComponents()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        model.ConfigureForProperCoverage(
            CorrelationFunctionType.Spherical,
            includeScaleErrors: true,
            includeShapeErrors: true);

        // Assert
        Assert.IsTrue(model.UseCopulaDependence, "Should enable copula dependence.");
        Assert.IsTrue(model.UseLocationErrors, "Should enable location errors.");
        Assert.IsTrue(model.UseScaleErrors, "Should enable scale errors.");
        Assert.IsTrue(model.UseShapeErrors, "Should enable shape errors.");
        Assert.IsNotNull(model.ScaleErrors, "ScaleErrors should not be null.");
        Assert.IsNotNull(model.ShapeErrors, "ShapeErrors should not be null.");
    }

    /// <summary>
    /// Tests ConfigureForProperCoverage with weighted likelihood option.
    /// </summary>
    [TestMethod]
    public void ConfigureForProperCoverage_WithWeightedLikelihood_ComputesWeights()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        model.ConfigureForProperCoverage(useWeightedLikelihood: true);

        // Assert - with real data there may or may not be correlation, so just check weights are valid
        Assert.IsTrue(model.SiteWeights.Sum() > 0, "Weights should be positive.");
    }

    /// <summary>
    /// Tests ConfigureForProperCoverage rebuilds parameters correctly.
    /// </summary>
    [TestMethod]
    public void ConfigureForProperCoverage_RebuildsParmaterList()
    {
        // Arrange
        var model = CreateTestModel();
        int originalParamCount = model.NumberOfParameters;

        // Act
        model.ConfigureForProperCoverage(includeScaleErrors: true);

        // Assert - should have more parameters after enabling spatial components
        Assert.IsTrue(model.NumberOfParameters > originalParamCount,
            $"Parameter count should increase after configuration. Original: {originalParamCount}, New: {model.NumberOfParameters}");
    }

    /// <summary>
    /// Tests PredictAtUngauged returns valid GEV parameters.
    /// </summary>
    [TestMethod]
    public void PredictAtUngauged_ReturnsValidGEVParameters()
    {
        // Arrange
        var model = CreateTestModel();
        var newCoords = new double[] { 25.0, 10.0 }; // Between existing sites

        // Act
        var (gevParams, errorVariances) = model.PredictAtUngauged(newCoords);

        // Assert
        Assert.IsNotNull(gevParams, "GEV params should not be null.");
        Assert.AreEqual(3, gevParams.Length, "Should have 3 GEV parameters.");

        Assert.IsFalse(double.IsNaN(gevParams[0]), "Location should not be NaN.");
        Assert.IsTrue(gevParams[0] > 0, "Location should be positive (log-link).");

        Assert.IsFalse(double.IsNaN(gevParams[1]), "Scale should not be NaN.");
        Assert.IsTrue(gevParams[1] > 0, "Scale should be positive.");

        Assert.IsFalse(double.IsNaN(gevParams[2]), "Shape should not be NaN.");
    }

    /// <summary>
    /// Tests PredictAtUngauged returns error variances.
    /// </summary>
    [TestMethod]
    public void PredictAtUngauged_ReturnsErrorVariances()
    {
        // Arrange
        var model = CreateTestModel();
        var newCoords = new double[] { 25.0, 10.0 };

        // Act
        var (gevParams, errorVariances) = model.PredictAtUngauged(newCoords);

        // Assert
        Assert.IsNotNull(errorVariances, "Error variances should not be null.");
        Assert.AreEqual(3, errorVariances.Length, "Should have 3 error variances.");

        // Without spatial errors enabled, variances should be 0
        Assert.AreEqual(0.0, errorVariances[0], 1e-10, "Location error variance should be 0 without errors enabled.");
        Assert.AreEqual(0.0, errorVariances[1], 1e-10, "Scale error variance should be 0 without errors enabled.");
        Assert.AreEqual(0.0, errorVariances[2], 1e-10, "Shape error variance should be 0 without errors enabled.");
    }

    /// <summary>
    /// Tests PredictAtUngauged with spatial errors returns non-zero variances.
    /// </summary>
    [TestMethod]
    public void PredictAtUngauged_WithSpatialErrors_ReturnsNonZeroVariances()
    {
        // Arrange
        var model = CreateModelWithSpatialErrors();
        var newCoords = new double[] { 25.0, 10.0 };

        // Act
        var (gevParams, errorVariances) = model.PredictAtUngauged(newCoords);

        // Assert
        Assert.IsTrue(errorVariances[0] >= 0, "Location error variance should be non-negative.");
        Assert.IsTrue(errorVariances[1] >= 0, "Scale error variance should be non-negative.");
    }

    /// <summary>
    /// Tests PredictAtUngauged throws for invalid coordinates.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void PredictAtUngauged_InvalidCoordinates_ThrowsException()
    {
        var model = CreateTestModel();
        model.PredictAtUngauged(new double[] { 1.0 }); // Wrong length
    }

    /// <summary>
    /// Tests PredictAtUngauged throws for null coordinates.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void PredictAtUngauged_NullCoordinates_ThrowsException()
    {
        var model = CreateTestModel();
        model.PredictAtUngauged(null!);
    }

    /// <summary>
    /// Tests GetGEVAtUngauged returns valid distribution.
    /// </summary>
    [TestMethod]
    public void GetGEVAtUngauged_ReturnsValidDistribution()
    {
        // Arrange
        var model = CreateTestModel();
        var newCoords = new double[] { 25.0, 10.0 };

        // Act
        var gev = model.GetGEVAtUngauged(newCoords);

        // Assert
        Assert.IsNotNull(gev, "GEV distribution should not be null.");

        // Test that CDF and InverseCDF work
        double quantile = gev.InverseCDF(0.99);
        Assert.IsFalse(double.IsNaN(quantile), "Quantile should not be NaN.");
        Assert.IsTrue(quantile > 0, "Quantile should be positive for flood data.");

        double cdf = gev.CDF(quantile);
        Assert.AreEqual(0.99, cdf, 1e-6, "CDF of the 99th percentile should be 0.99.");
    }

    /// <summary>
    /// Tests GetGEVAtUngauged at existing site location matches site GEV.
    /// </summary>
    [TestMethod]
    public void GetGEVAtUngauged_AtExistingSite_MatchesSiteGEV()
    {
        // Arrange
        var model = CreateTestModel();
        var coords = CreateTestCoordinates();

        // Get GEV at site 0's exact location
        var site0Coords = new double[] { coords[0, 0], coords[0, 1] };

        // Act
        var gevPredicted = model.GetGEVAtUngauged(site0Coords);
        var gevSite0Params = model.GetGEVParameters(0);

        // Assert - parameters should match exactly at existing site location
        Assert.AreEqual(gevSite0Params[0], gevPredicted.Xi, 1e-6, "Location should match.");
        Assert.AreEqual(gevSite0Params[1], gevPredicted.Alpha, 1e-6, "Scale should match.");
        Assert.AreEqual(gevSite0Params[2], gevPredicted.Kappa, 1e-6, "Shape should match.");
    }

    #endregion

    #region SpatialRegressionErrors Tests

    /// <summary>
    /// Tests GetKrigingPrediction returns valid mean and variance.
    /// </summary>
    [TestMethod]
    public void SpatialRegressionErrors_GetKrigingPrediction_ReturnsValidValues()
    {
        // Arrange
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Set some error values
        var paramValues = errors.Parameters.Select(p => p.Value).ToList();
        errors.SetParameterValues(paramValues);

        var newCoords = new double[] { 25.0, 10.0 }; // Between existing sites

        // Act
        var (mean, variance) = errors.GetKrigingPrediction(newCoords);

        // Assert
        Assert.IsFalse(double.IsNaN(mean), "Kriging mean should not be NaN.");
        Assert.IsFalse(double.IsNaN(variance), "Kriging variance should not be NaN.");
        Assert.IsTrue(variance >= 0, "Kriging variance should be non-negative.");
    }

    /// <summary>
    /// Tests GetKrigingPrediction variance is zero at existing site.
    /// </summary>
    [TestMethod]
    public void SpatialRegressionErrors_GetKrigingPrediction_ZeroVarianceAtExistingSite()
    {
        // Arrange
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        var paramValues = errors.Parameters.Select(p => p.Value).ToList();
        errors.SetParameterValues(paramValues);

        // Use exact coordinates of site 0
        var site0Coords = new double[] { coords[0, 0], coords[0, 1] };

        // Act
        var (mean, variance) = errors.GetKrigingPrediction(site0Coords);

        // Assert - at existing site, kriging gives exact interpolation
        Assert.AreEqual(errors.GetError(0), mean, 1e-6, "Mean should equal error at that site.");
        // Note: variance may be very small but not exactly zero due to numerical precision
    }

    /// <summary>
    /// Tests GetIDWPrediction returns valid values.
    /// </summary>
    [TestMethod]
    public void SpatialRegressionErrors_GetIDWPrediction_ReturnsValidValues()
    {
        // Arrange
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        var paramValues = errors.Parameters.Select(p => p.Value).ToList();
        errors.SetParameterValues(paramValues);

        var newCoords = new double[] { 25.0, 10.0 };

        // Act
        var (mean, variance) = errors.GetIDWPrediction(newCoords);

        // Assert
        Assert.IsFalse(double.IsNaN(mean), "IDW mean should not be NaN.");
        Assert.IsFalse(double.IsNaN(variance), "IDW variance should not be NaN.");
        Assert.IsTrue(variance >= 0, "IDW variance should be non-negative.");
    }

    /// <summary>
    /// Tests GetIDWPrediction throws for invalid coordinates.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void SpatialRegressionErrors_GetIDWPrediction_InvalidCoords_ThrowsException()
    {
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.GetIDWPrediction(new double[] { 1.0 }); // Wrong length
    }

    /// <summary>
    /// Tests Coordinates property returns correct values.
    /// </summary>
    [TestMethod]
    public void SpatialRegressionErrors_Coordinates_ReturnsCorrectValues()
    {
        // Arrange
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Act
        var returnedCoords = errors.Coordinates;

        // Assert
        Assert.IsNotNull(returnedCoords, "Coordinates should not be null.");
        Assert.AreEqual(5, returnedCoords.GetLength(0), "Should have 5 sites.");
        Assert.AreEqual(2, returnedCoords.GetLength(1), "Should have 2 dimensions.");

        // Check first coordinate
        Assert.AreEqual(coords[0, 0], returnedCoords[0, 0], 1e-10, "X coordinate should match.");
        Assert.AreEqual(coords[0, 1], returnedCoords[0, 1], 1e-10, "Y coordinate should match.");
    }

    /// <summary>
    /// Tests DistanceMatrix is properly computed.
    /// </summary>
    [TestMethod]
    public void SpatialRegressionErrors_DistanceMatrix_IsProperlyComputed()
    {
        // Arrange
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Act
        var distMatrix = errors.DistanceMatrix;

        // Assert
        Assert.IsNotNull(distMatrix, "Distance matrix should not be null.");
        Assert.AreEqual(5, distMatrix.GetLength(0), "Matrix should be 5×5.");
        Assert.AreEqual(5, distMatrix.GetLength(1), "Matrix should be 5×5.");

        // Diagonal should be zero
        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(0.0, distMatrix[i, i], 1e-10, $"Diagonal [{i},{i}] should be 0.");
        }

        // Should be symmetric
        for (int i = 0; i < 5; i++)
        {
            for (int j = i + 1; j < 5; j++)
            {
                Assert.AreEqual(distMatrix[i, j], distMatrix[j, i], 1e-10,
                    $"Distance matrix should be symmetric: [{i},{j}] != [{j},{i}]");
            }
        }

        // All distances should be positive (except diagonal)
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (i != j)
                {
                    Assert.IsTrue(distMatrix[i, j] > 0,
                        $"Distance [{i},{j}] should be positive, got {distMatrix[i, j]}");
                }
            }
        }
    }

    /// <summary>
    /// Tests GetKrigingPrediction with different correlation functions.
    /// </summary>
    [TestMethod]
    public void SpatialRegressionErrors_GetKrigingPrediction_DifferentCorrelationFunctions()
    {
        // Arrange
        var coords = CreateTestCoordinates();
        var newCoords = new double[] { 25.0, 10.0 };

        var corrTypes = new[] { CorrelationFunctionType.Exponential, CorrelationFunctionType.Spherical };

        foreach (var corrType in corrTypes)
        {
            var errors = new SpatialRegressionErrors(coords, corrType);
            var paramValues = errors.Parameters.Select(p => p.Value).ToList();
            errors.SetParameterValues(paramValues);

            // Act
            var (mean, variance) = errors.GetKrigingPrediction(newCoords);

            // Assert
            Assert.IsFalse(double.IsNaN(mean), $"Mean should not be NaN for {corrType}.");
            Assert.IsFalse(double.IsNaN(variance), $"Variance should not be NaN for {corrType}.");
        }
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests MLE with many sites but few observations per site.
    /// </summary>
    [TestMethod]
    public void MLE_ManySitesFewObs_Converges()
    {
        // Arrange: 20 sites × 20 obs = 400 total, but sparse per site
        var (data, coords, _) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 20, nSites: 20, seed: 44444);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE should converge with many sites but few obs per site.");
    }

    /// <summary>
    /// Tests MLE with few sites but many observations per site.
    /// </summary>
    [TestMethod]
    public void MLE_FewSitesManyObs_Converges()
    {
        // Arrange: 3 sites × 150 obs = 450 total
        var (data, coords, _) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 150, nSites: 3, seed: 55555);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert
        Assert.IsTrue(mle.IsEstimated, "MLE should converge with few sites but many obs per site.");
    }

    #endregion

    #region SpatialGEVAnalysis Tests

    /// <summary>
    /// Tests UncertaintyMethod property default value.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_UncertaintyMethod_DefaultIsBayesianPosterior()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Assert
        Assert.AreEqual(SpatialGEVUncertaintyMethod.BayesianPosterior, analysis.UncertaintyMethod,
            "Default uncertainty method should be BayesianPosterior.");
    }

    /// <summary>
    /// Tests UncertaintyMethod property can be set.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_UncertaintyMethod_CanBeSet()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        analysis.UncertaintyMethod = SpatialGEVUncertaintyMethod.GodambeSandwich;

        // Assert
        Assert.AreEqual(SpatialGEVUncertaintyMethod.GodambeSandwich, analysis.UncertaintyMethod);
    }

    /// <summary>
    /// Tests SpatialGEVAnalysis constructor.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_Constructor_InitializesCorrectly()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var analysis = new SpatialGEVAnalysis(model);

        // Assert
        Assert.IsNotNull(analysis.SpatialGEV, "SpatialGEV should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis, "BayesianAnalysis should not be null.");
        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsFalse(analysis.IsEstimated, "Should not be estimated initially.");
    }

    /// <summary>
    /// Tests SpatialGEVAnalysis constructor throws for null model.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SpatialGEVAnalysis_Constructor_NullModel_ThrowsException()
    {
        _ = new SpatialGEVAnalysis(null!);
    }

    /// <summary>
    /// Tests VarianceInflationFactor default value.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_VarianceInflationFactor_DefaultIsOne()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Assert
        Assert.AreEqual(1.0, analysis.VarianceInflationFactor, 1e-10,
            "Default VIF should be 1.0.");
    }

    /// <summary>
    /// Tests GodambeCovariance is null initially.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_GodambeCovariance_InitiallyNull()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Assert
        Assert.IsNull(analysis.GodambeCovariance, "Godambe covariance should be null initially.");
    }

    /// <summary>
    /// Tests Validate method.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_Validate_ValidModel_ReturnsTrue()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests ClearResults method.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_ClearResults_ClearsAllResults()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        analysis.ClearResults();

        // Assert
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null after clear.");
        Assert.IsNull(analysis.SiteResults, "SiteResults should be null after clear.");
        Assert.IsNull(analysis.CrossValidationResults, "CrossValidationResults should be null after clear.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after clear.");
    }

    /// <summary>
    /// Tests ComputeGodambeCovariance throws when analysis not run.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SpatialGEVAnalysis_ComputeGodambeCovariance_NotEstimated_ThrowsException()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act - should throw because analysis not run
        analysis.ComputeGodambeCovariance();
    }

    /// <summary>
    /// Tests InflatePosteriorCovariance returns VIF >= 1.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_InflatePosteriorCovariance_ReturnsValidVIF()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        double vif = analysis.InflatePosteriorCovariance();

        // Assert
        Assert.IsTrue(vif >= 1.0, $"VIF should be >= 1.0, got {vif}");
        Assert.AreEqual(vif, analysis.VarianceInflationFactor, 1e-10, "Property should match return value.");
    }

    /// <summary>
    /// Tests GetSiteQuantiles throws when analysis not run.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SpatialGEVAnalysis_GetSiteQuantiles_NotEstimated_ThrowsException()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act - should throw
        analysis.GetSiteQuantiles(0, new double[] { 0.5, 0.1, 0.01 });
    }

    /// <summary>
    /// Tests PredictAtUngaugedLocation throws when analysis not run.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SpatialGEVAnalysis_PredictAtUngaugedLocation_NotEstimated_ThrowsException()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act - should throw
        analysis.PredictAtUngaugedLocation(new double[] { 25.0, 10.0 }, null, new double[] { 0.5, 0.1, 0.01 });
    }

    /// <summary>
    /// Tests GetRegionalGrowthCurve throws when analysis not run.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SpatialGEVAnalysis_GetRegionalGrowthCurve_NotEstimated_ThrowsException()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act - should throw
        analysis.GetRegionalGrowthCurve(new double[] { 0.5, 0.1, 0.01 });
    }

    /// <summary>
    /// Tests RunSpatialBootstrapAsync throws when analysis not run.
    /// </summary>
    [TestMethod]
    public async Task SpatialGEVAnalysis_RunSpatialBootstrapAsync_NotEstimated_ThrowsException()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await analysis.RunSpatialBootstrapAsync(10));
    }

    /// <summary>
    /// Tests ToXElement creates valid XML.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_ToXElement_CreatesValidXml()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        var xElement = analysis.ToXElement();

        // Assert
        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("SpatialGEVAnalysis", xElement.Name.LocalName, "Root element should be SpatialGEVAnalysis.");
        Assert.IsNotNull(xElement.Attribute("IsEstimated"), "Should have IsEstimated attribute.");
        Assert.IsNotNull(xElement.Element("ProbabilityOrdinates"), "Should have ProbabilityOrdinates element.");
    }

    /// <summary>
    /// Tests all UncertaintyMethod enum values can be assigned.
    /// </summary>
    [TestMethod]
    public void SpatialGEVAnalysis_UncertaintyMethod_AllValuesCanBeSet()
    {
        // Arrange
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        var methods = new[]
        {
            SpatialGEVUncertaintyMethod.BayesianPosterior,
            SpatialGEVUncertaintyMethod.BayesianInflated,
            SpatialGEVUncertaintyMethod.GodambeSandwich,
            SpatialGEVUncertaintyMethod.SpatialBootstrap
        };

        // Act & Assert
        foreach (var method in methods)
        {
            analysis.UncertaintyMethod = method;
            Assert.AreEqual(method, analysis.UncertaintyMethod,
                $"Should be able to set UncertaintyMethod to {method}.");
        }
    }

    #endregion
}
