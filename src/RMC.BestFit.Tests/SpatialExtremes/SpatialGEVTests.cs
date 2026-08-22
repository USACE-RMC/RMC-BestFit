using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;

namespace RMC.BestFit.Tests.SpatialExtremes;

/// <summary>
/// Programmatic unit tests for the <c>SpatialGEV</c> hierarchical Bayesian spatial GEV model
/// and the <c>SpatialGEVAnalysis</c> wrapper.
/// </summary>
/// <remarks>
/// <para>
/// These tests cover constructors, parameter handling, link functions, log-likelihood
/// evaluation at fixed parameters, PDF/CDF/InverseCDF round-trip, random sampling,
/// cloning, XML serialization, validation, ungauged prediction, and the analysis
/// wrapper's POCO surface. Estimation-driven tests (MLE convergence and parameter
/// recovery) live in <c>RMC.BestFit.Verification</c>.
/// </para>
/// <para>
/// Inline deterministic fixtures (small homogeneous spatial GEV samples) are used so
/// this file does not depend on the Verification project's shared
/// <c>SyntheticSpatialGEVData</c> helper. Sample sizes are small (3-5 sites × 30-50
/// years) so the suite stays in the fast PR gate.
/// </para>
/// </remarks>
[TestClass]
public class SpatialGEVTests
{
    #region Inline test fixtures

    /// <summary>
    /// Creates standard inline at-site data (30 observations × 5 sites) drawn from a homogeneous
    /// GEV(10000, 2500, 0) distribution via inverse-CDF sampling with a fixed seed.
    /// </summary>
    /// <remarks>
    /// All sites share the same GEV parameters, so an intercept-only model is appropriate.
    /// </remarks>
    private static double[,] CreateTestAtSiteData()
    {
        var data = new double[30, 5];
        var rng = new Random(12345);
        var gev = new GeneralizedExtremeValue(10000, 2500, 0.0);

        for (int site = 0; site < 5; site++)
        {
            for (int year = 0; year < 30; year++)
            {
                data[year, site] = gev.InverseCDF(rng.NextDouble());
            }
        }

        return data;
    }

    /// <summary>
    /// Creates inline test site coordinates for a 5-site network spread along a roughly
    /// linear river path.
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
    /// Creates a minimal 2-site × 10-year fixture for edge-case testing.
    /// </summary>
    private static (double[,] Data, double[,] Coordinates) CreateMinimalTestData()
    {
        var data = new double[10, 2];
        var rng = new Random(54321);
        var gev = new GeneralizedExtremeValue(6000, 1500, 0.0);

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
    /// Creates the standard fixture and inserts NaN values at five distinct (year, site) cells.
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
    /// Creates a default-configured <c>SpatialGEV</c> with intercept-only trends.
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
    /// Creates a test model with copula spatial dependence enabled.
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
    /// Creates a test model with spatial regression errors enabled on location and scale.
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

    /// <summary>Verifies that constructor with valid inputs initializes correctly.</summary>
    [TestMethod]
    public void Constructor_WithValidInputs_InitializesCorrectly()
    {
        var data = CreateTestAtSiteData();
        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);

        Assert.IsNotNull(model);
        Assert.AreEqual(5, model.Sites);
        Assert.AreEqual(30, model.Observations);
        Assert.IsNotNull(model.Location);
        Assert.IsNotNull(model.Scale);
        Assert.IsNotNull(model.Shape);
    }

    /// <summary>Verifies that constructor throws when with null data.</summary>
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

    /// <summary>Verifies that constructor throws when with null coordinates.</summary>
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

    /// <summary>Verifies that constructor throws when with null location.</summary>
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

    /// <summary>Verifies that constructor throws when with null scale.</summary>
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

    /// <summary>Verifies that constructor throws when with null shape.</summary>
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

    /// <summary>Verifies that constructor throws when mismatched dimensions.</summary>
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

    /// <summary>Verifies that constructor default options are correct.</summary>
    [TestMethod]
    public void Constructor_DefaultOptions_AreCorrect()
    {
        var model = CreateTestModel();

        Assert.IsFalse(model.UseCopulaDependence);
        Assert.IsFalse(model.UseLocationErrors);
        Assert.IsFalse(model.UseScaleErrors);
        Assert.IsFalse(model.UseShapeErrors);
        Assert.IsTrue(model.UseLogLinkForLocation);
        Assert.IsTrue(model.UseLogLinkForScale);
    }

    /// <summary>Verifies that constructor site weights initialized to one.</summary>
    [TestMethod]
    public void Constructor_SiteWeights_InitializedToOne()
    {
        var model = CreateTestModel();

        Assert.IsNotNull(model.SiteWeights);
        Assert.AreEqual(5, model.SiteWeights.Length);
        for (int i = 0; i < model.Sites; i++)
        {
            Assert.AreEqual(1.0, model.SiteWeights[i], 1e-10);
        }
    }

    /// <summary>Verifies that constructor minimal sites initializes correctly.</summary>
    [TestMethod]
    public void Constructor_MinimalSites_InitializesCorrectly()
    {
        var (data, coords) = CreateMinimalTestData();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);

        Assert.AreEqual(2, model.Sites);
        Assert.AreEqual(10, model.Observations);
    }

    #endregion

    #region Parameter Tests

    /// <summary>Verifies that parameters basic model has correct count.</summary>
    [TestMethod]
    public void Parameters_BasicModel_HasCorrectCount()
    {
        var model = CreateTestModel();

        Assert.IsNotNull(model.Parameters);
        Assert.AreEqual(3, model.NumberOfParameters);
    }

    /// <summary>Verifies that parameters with copula has additional parameters.</summary>
    [TestMethod]
    public void Parameters_WithCopula_HasAdditionalParameters()
    {
        var model = CreateModelWithCopula();

        Assert.IsTrue(model.NumberOfParameters > 3);
    }

    /// <summary>Verifies that parameters with spatial errors has additional parameters.</summary>
    [TestMethod]
    public void Parameters_WithSpatialErrors_HasAdditionalParameters()
    {
        var model = CreateModelWithSpatialErrors();

        Assert.IsTrue(model.NumberOfParameters > 3);
    }

    /// <summary>Verifies that set parameter values valid parameters updates values.</summary>
    [TestMethod]
    public void SetParameterValues_ValidParameters_UpdatesValues()
    {
        var model = CreateTestModel();
        var values = new double[model.NumberOfParameters];
        for (int i = 0; i < values.Length; i++)
            values[i] = model.Parameters[i].Value * 1.1;

        model.SetParameterValues(values);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.AreEqual(values[i], model.Parameters[i].Value, 1e-10);
        }
    }

    /// <summary>Verifies that set parameter values throws when null parameters.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SetParameterValues_NullParameters_ThrowsArgumentNullException()
    {
        var model = CreateTestModel();
        model.SetParameterValues(null!);
    }

    /// <summary>Verifies that set parameter values throws when wrong count.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void SetParameterValues_WrongCount_ThrowsArgumentException()
    {
        var model = CreateTestModel();
        var values = new double[] { 1.0, 2.0 };
        model.SetParameterValues(values);
    }

    /// <summary>Verifies that set default parameters creates valid initial values.</summary>
    [TestMethod]
    public void SetDefaultParameters_CreatesValidInitialValues()
    {
        var model = CreateTestModel();

        model.SetDefaultParameters();

        Assert.IsNotNull(model.Parameters);
        Assert.AreEqual(3, model.Parameters.Count);

        foreach (var param in model.Parameters)
        {
            Assert.IsFalse(double.IsNaN(param.Value));
            Assert.IsFalse(double.IsInfinity(param.Value));
            Assert.IsTrue(param.Value >= param.LowerBound);
            Assert.IsTrue(param.Value <= param.UpperBound);
        }
    }

    #endregion

    #region GetGEVParameters Tests

    /// <summary>Verifies that get GEV parameters returns valid parameters when valid site.</summary>
    [TestMethod]
    public void GetGEVParameters_ValidSite_ReturnsValidParameters()
    {
        var model = CreateTestModel();

        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);

            Assert.IsNotNull(gevParams);
            Assert.AreEqual(3, gevParams.Length);
            Assert.IsFalse(double.IsNaN(gevParams[0]));
            Assert.IsTrue(gevParams[0] > 0, "Location should be positive with log-link.");
            Assert.IsFalse(double.IsNaN(gevParams[1]));
            Assert.IsTrue(gevParams[1] > 0, "Scale should be positive.");
            Assert.IsFalse(double.IsNaN(gevParams[2]));
        }
    }

    /// <summary>Verifies that get GEV parameters throws when negative site.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetGEVParameters_NegativeSite_ThrowsArgumentOutOfRangeException()
    {
        var model = CreateTestModel();
        model.GetGEVParameters(-1);
    }

    /// <summary>Verifies that get GEV parameters throws when site beyond range.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetGEVParameters_SiteBeyondRange_ThrowsArgumentOutOfRangeException()
    {
        var model = CreateTestModel();
        model.GetGEVParameters(model.Sites);
    }

    /// <summary>Verifies that get GEV parameters returns valid parameters when identity link location.</summary>
    [TestMethod]
    public void GetGEVParameters_IdentityLinkLocation_ReturnsValidParameters()
    {
        var model = CreateTestModel();
        model.UseLogLinkForLocation = false;
        model.SetDefaultParameters();

        var gevParams = model.GetGEVParameters(0);

        Assert.IsFalse(double.IsNaN(gevParams[0]));
    }

    /// <summary>Verifies that get GEV parameters returns valid parameters when identity link scale.</summary>
    [TestMethod]
    public void GetGEVParameters_IdentityLinkScale_ReturnsValidParameters()
    {
        var model = CreateTestModel();
        model.UseLogLinkForScale = false;
        model.SetDefaultParameters();

        var gevParams = model.GetGEVParameters(0);

        Assert.IsFalse(double.IsNaN(gevParams[1]));
        Assert.IsTrue(gevParams[1] > 0);
    }

    /// <summary>Verifies that get GEV parameters with location errors includes error contribution.</summary>
    [TestMethod]
    public void GetGEVParameters_WithLocationErrors_IncludesErrorContribution()
    {
        var model = CreateModelWithSpatialErrors();

        var params0 = model.GetGEVParameters(0);
        var params1 = model.GetGEVParameters(1);

        Assert.IsNotNull(params0);
        Assert.IsNotNull(params1);
        Assert.IsFalse(double.IsNaN(params0[0]));
        Assert.IsFalse(double.IsNaN(params1[0]));
    }

    #endregion

    #region LogLikelihood Tests

    /// <summary>Verifies that log likelihood returns finite value when valid parameters.</summary>
    [TestMethod]
    public void LogLikelihood_ValidParameters_ReturnsFiniteValue()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        double logLH = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(logLH));
        Assert.IsTrue(double.IsFinite(logLH));
        Assert.IsTrue(logLH < 0);
    }

    /// <summary>Verifies that log likelihood returns negative infinity when invalid parameter count.</summary>
    [TestMethod]
    public void LogLikelihood_InvalidParameterCount_ReturnsNegativeInfinity()
    {
        var model = CreateTestModel();
        var parameters = new double[] { 1.0 };

        double logLH = model.LogLikelihood(parameters);

        Assert.AreEqual(double.NegativeInfinity, logLH);
    }

    /// <summary>Verifies that log likelihood better fit has higher likelihood.</summary>
    [TestMethod]
    public void LogLikelihood_BetterFit_HasHigherLikelihood()
    {
        var model = CreateTestModel();
        var goodParams = model.Parameters.Select(p => p.Value).ToArray();

        double goodLogLH = model.LogLikelihood(goodParams);
        Assert.IsTrue(double.IsFinite(goodLogLH));

        var badParams = (double[])goodParams.Clone();
        int shapeIndex = goodParams.Length - 1;
        badParams[shapeIndex] += 0.5;

        double badLogLH = model.LogLikelihood(badParams);

        Assert.IsTrue(goodLogLH > badLogLH,
            $"Better-fitting parameters ({goodLogLH}) should have higher LL than perturbed ({badLogLH}).");
    }

    /// <summary>Verifies that log likelihood returns finite value when with copula.</summary>
    [TestMethod]
    public void LogLikelihood_WithCopula_ReturnsFiniteValue()
    {
        var model = CreateModelWithCopula();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        double logLH = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(logLH));
        Assert.IsTrue(double.IsFinite(logLH));
    }

    /// <summary>Verifies that log likelihood returns finite value when with spatial errors.</summary>
    [TestMethod]
    public void LogLikelihood_WithSpatialErrors_ReturnsFiniteValue()
    {
        var model = CreateModelWithSpatialErrors();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        double logLH = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(logLH));
        Assert.IsTrue(double.IsFinite(logLH));
    }

    /// <summary>Verifies that log likelihood with missing data handles na n correctly.</summary>
    [TestMethod]
    public void LogLikelihood_WithMissingData_HandlesNaNCorrectly()
    {
        var data = CreateDataWithMissingValues();
        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        double logLH = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(logLH));
        Assert.IsTrue(double.IsFinite(logLH));
    }

    /// <summary>Verifies that log likelihood site weights affect result.</summary>
    [TestMethod]
    public void LogLikelihood_SiteWeights_AffectResult()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double ll1 = model.LogLikelihood(parameters);

        Assert.IsTrue(double.IsFinite(ll1));

        model.SiteWeights[0] = 0.5;
        model.SiteWeights[1] = 1.5;

        double ll2 = model.LogLikelihood(parameters);

        Assert.IsTrue(double.IsFinite(ll2));
        Assert.AreNotEqual(ll1, ll2, 1e-10);
    }

    #endregion

    #region DataLogLikelihood Tests

    /// <summary>Verifies that data log likelihood verify log likelihood composition.</summary>
    [TestMethod]
    public void DataLogLikelihood_VerifyLogLikelihoodComposition()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        double dataLL = model.DataLogLikelihood(parameters);
        double priorLL = model.PriorLogLikelihood(parameters);
        double totalLL = model.LogLikelihood(parameters);

        Assert.AreEqual(dataLL + priorLL, totalLL, 1e-10,
            "LogLikelihood should equal DataLogLikelihood + PriorLogLikelihood.");
    }

    /// <summary>Verifies that data log likelihood returns finite value when valid parameters.</summary>
    [TestMethod]
    public void DataLogLikelihood_ValidParameters_ReturnsFiniteValue()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        double dataLL = model.DataLogLikelihood(parameters);

        Assert.IsTrue(double.IsFinite(dataLL));
    }

    #endregion

    #region PointwiseDataLogLikelihood Tests

    /// <summary>Verifies that pointwise data log likelihood returns correct count.</summary>
    [TestMethod]
    public void PointwiseDataLogLikelihood_ReturnsCorrectCount()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        Assert.IsNotNull(pointwise);
        Assert.AreEqual(model.Observations, pointwise.Length);
    }

    /// <summary>Verifies that pointwise data log likelihood values are finite.</summary>
    [TestMethod]
    public void PointwiseDataLogLikelihood_ValuesAreFinite()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        for (int i = 0; i < pointwise.Length; i++)
        {
            Assert.IsFalse(double.IsNaN(pointwise[i]));
        }
    }

    /// <summary>Verifies that pointwise data log likelihood sums to data log likelihood.</summary>
    [TestMethod]
    public void PointwiseDataLogLikelihood_SumsToDataLogLikelihood()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        double dataLL = model.DataLogLikelihood(parameters);
        Assert.IsTrue(double.IsFinite(dataLL));

        var pointwise = model.PointwiseDataLogLikelihood(parameters);
        double pointwiseSum = pointwise.Sum();

        for (int i = 0; i < pointwise.Length; i++)
        {
            Assert.IsTrue(double.IsFinite(pointwise[i]));
        }

        Assert.AreEqual(dataLL, pointwiseSum, 1e-6);
    }

    #endregion

    #region PointwiseDataLogLikelihoodComponents Tests

    /// <summary>Verifies that pointwise data log likelihood components returns correct count.</summary>
    [TestMethod]
    public void PointwiseDataLogLikelihoodComponents_ReturnsCorrectCount()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        Assert.IsNotNull(components);
        Assert.AreEqual(model.Observations, components.Count);
    }

    /// <summary>Verifies that pointwise data log likelihood components has correct properties.</summary>
    [TestMethod]
    public void PointwiseDataLogLikelihoodComponents_HasCorrectProperties()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var components = model.PointwiseDataLogLikelihoodComponents(parameters);
        var first = components[0];

        Assert.AreEqual(0, first.Index);
        Assert.IsFalse(double.IsNaN(first.LogLikelihood));
        Assert.AreEqual(DataComponentType.Exact, first.Type);
    }

    #endregion

    #region PointwisePriorLogLikelihood Tests

    /// <summary>Verifies that pointwise prior log likelihood returns prior components.</summary>
    [TestMethod]
    public void PointwisePriorLogLikelihood_ReturnsPriorComponents()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var priorComponents = model.PointwisePriorLogLikelihood(parameters);

        Assert.IsNotNull(priorComponents);
        Assert.IsTrue(priorComponents.Count >= model.NumberOfParameters);
    }

    /// <summary>Verifies that pointwise prior log likelihood components have valid log likelihoods.</summary>
    [TestMethod]
    public void PointwisePriorLogLikelihood_ComponentsHaveValidLogLikelihoods()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var priorComponents = model.PointwisePriorLogLikelihood(parameters);

        foreach (var component in priorComponents)
        {
            Assert.IsFalse(double.IsNaN(component.LogLikelihood),
                $"Prior component '{component.Name}' should not have NaN log-likelihood.");
        }
    }

    #endregion

    #region PDF/CDF/InverseCDF Tests

    /// <summary>Verifies that PDF returns positive value when valid input.</summary>
    [TestMethod]
    public void PDF_ValidInput_ReturnsPositiveValue()
    {
        var model = CreateTestModel();

        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);
            double testValue = gevParams[0];

            double pdf = model.PDF(testValue, site);

            Assert.IsTrue(pdf > 0);
            Assert.IsFalse(double.IsNaN(pdf));
        }
    }

    /// <summary>Verifies that CDF returns value between zero and one when valid input.</summary>
    [TestMethod]
    public void CDF_ValidInput_ReturnsValueBetweenZeroAndOne()
    {
        var model = CreateTestModel();

        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);
            double testValue = gevParams[0];

            double cdf = model.CDF(testValue, site);

            Assert.IsTrue(cdf >= 0 && cdf <= 1);
            Assert.IsFalse(double.IsNaN(cdf));
        }
    }

    /// <summary>Verifies that inverse CDF consistent with CDF.</summary>
    [TestMethod]
    public void InverseCDF_ConsistentWithCDF()
    {
        var model = CreateTestModel();
        double[] testProbs = { 0.1, 0.25, 0.5, 0.75, 0.9 };

        for (int site = 0; site < model.Sites; site++)
        {
            foreach (double p in testProbs)
            {
                double quantile = model.InverseCDF(p, site);
                double recoveredP = model.CDF(quantile, site);

                Assert.AreEqual(p, recoveredP, 1e-6);
            }
        }
    }

    /// <summary>Verifies that inverse CDF returns increasing quantiles when increasing probabilities.</summary>
    [TestMethod]
    public void InverseCDF_IncreasingProbabilities_ReturnsIncreasingQuantiles()
    {
        var model = CreateTestModel();
        double[] probs = { 0.1, 0.5, 0.9 };

        for (int site = 0; site < model.Sites; site++)
        {
            double prev = double.NegativeInfinity;
            foreach (double p in probs)
            {
                double quantile = model.InverseCDF(p, site);
                Assert.IsTrue(quantile > prev);
                prev = quantile;
            }
        }
    }

    #endregion

    #region GenerateRandomValues Tests

    /// <summary>Verifies that generate random values returns correct count.</summary>
    [TestMethod]
    public void GenerateRandomValues_ReturnsCorrectCount()
    {
        var model = CreateTestModel();
        int sampleSize = 100;

        var samples = model.GenerateRandomValues(sampleSize, seed: 12345);

        Assert.AreEqual(sampleSize * model.Sites, samples.Length);
    }

    /// <summary>Verifies that generate random values throws when zero sample size.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GenerateRandomValues_ZeroSampleSize_ThrowsException()
    {
        var model = CreateTestModel();
        model.GenerateRandomValues(0);
    }

    /// <summary>Verifies that generate random values throws when negative sample size.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GenerateRandomValues_NegativeSampleSize_ThrowsException()
    {
        var model = CreateTestModel();
        model.GenerateRandomValues(-10);
    }

    /// <summary>Verifies that generate random values different seeds produce different values.</summary>
    [TestMethod]
    public void GenerateRandomValues_DifferentSeeds_ProduceDifferentValues()
    {
        var model = CreateTestModel();

        var samples1 = model.GenerateRandomValues(50, seed: 111);
        var samples2 = model.GenerateRandomValues(50, seed: 222);

        bool anyDifferent = false;
        for (int i = 0; i < samples1.Length; i++)
        {
            if (Math.Abs(samples1[i] - samples2[i]) > 1e-10)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.IsTrue(anyDifferent);
    }

    /// <summary>Verifies that generate random values same seed produces reproducible values.</summary>
    [TestMethod]
    public void GenerateRandomValues_SameSeed_ProducesReproducibleValues()
    {
        var model = CreateTestModel();

        var samples1 = model.GenerateRandomValues(50, seed: 12345);
        var samples2 = model.GenerateRandomValues(50, seed: 12345);

        for (int i = 0; i < samples1.Length; i++)
        {
            Assert.AreEqual(samples1[i], samples2[i], 1e-10);
        }
    }

    /// <summary>Verifies that generate random values produces positive values.</summary>
    [TestMethod]
    public void GenerateRandomValues_ProducesPositiveValues()
    {
        var model = CreateTestModel();

        var samples = model.GenerateRandomValues(1000, seed: 12345);

        int positiveCount = samples.Count(s => s > 0);
        double positiveRatio = (double)positiveCount / samples.Length;

        Assert.IsTrue(positiveRatio > 0.95);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Clone_CreatesIndependentCopy()
    {
        var original = CreateTestModel();
        original.SiteWeights[0] = 2.0;

        var clone = (SpatialGEV)original.Clone();

        original.SiteWeights[0] = 5.0;

        Assert.AreEqual(2.0, clone.SiteWeights[0], 1e-10);
    }

    /// <summary>Verifies that clone preserves configuration for .</summary>
    [TestMethod]
    public void Clone_PreservesConfiguration()
    {
        var original = CreateTestModel();
        original.UseLogLinkForLocation = false;
        original.UseLogLinkForScale = false;

        var clone = (SpatialGEV)original.Clone();

        Assert.AreEqual(original.UseLogLinkForLocation, clone.UseLogLinkForLocation);
        Assert.AreEqual(original.UseLogLinkForScale, clone.UseLogLinkForScale);
        Assert.AreEqual(original.UseCopulaDependence, clone.UseCopulaDependence);
        Assert.AreEqual(original.UseLocationErrors, clone.UseLocationErrors);
    }

    /// <summary>Verifies that clone preserves dimensions for .</summary>
    [TestMethod]
    public void Clone_PreservesDimensions()
    {
        var original = CreateTestModel();

        var clone = (SpatialGEV)original.Clone();

        Assert.AreEqual(original.Sites, clone.Sites);
        Assert.AreEqual(original.Observations, clone.Observations);
        Assert.AreEqual(original.NumberOfParameters, clone.NumberOfParameters);
    }

    /// <summary>Verifies that clone preserves copula for with copula.</summary>
    [TestMethod]
    public void Clone_WithCopula_PreservesCopula()
    {
        var original = CreateModelWithCopula();

        var clone = (SpatialGEV)original.Clone();

        Assert.IsTrue(clone.UseCopulaDependence);
        Assert.IsNotNull(clone.SpatialDependence);
    }

    /// <summary>
    /// Verifies that the clone of a copula model has the source's parameter structure (the copula block
    /// included) and accepts the source's parameter vector.
    /// </summary>
    [TestMethod]
    public void Clone_WithCopula_PreservesParameterStructure()
    {
        var original = CreateModelWithCopula();
        var values = original.Parameters.Select(p => p.Value).ToArray();

        var clone = (SpatialGEV)original.Clone();

        Assert.AreEqual(original.NumberOfParameters, clone.NumberOfParameters, "The clone must carry the copula parameter block.");
        clone.SetParameterValues(values);
        CollectionAssert.AreEqual(values, clone.Parameters.Select(p => p.Value).ToArray());
        Assert.AreEqual(original.DataLogLikelihood(values), clone.DataLogLikelihood(values), 1e-10);
    }

    /// <summary>
    /// Verifies that the clone of a latent-error model has the source's parameter structure (the error
    /// blocks included) and accepts the source's parameter vector.
    /// </summary>
    [TestMethod]
    public void Clone_WithSpatialErrors_PreservesParameterStructure()
    {
        var original = CreateModelWithSpatialErrors();
        var values = original.Parameters.Select(p => p.Value).ToArray();

        var clone = (SpatialGEV)original.Clone();

        Assert.AreEqual(original.NumberOfParameters, clone.NumberOfParameters, "The clone must carry the error parameter blocks.");
        clone.SetParameterValues(values);
        CollectionAssert.AreEqual(values, clone.Parameters.Select(p => p.Value).ToArray());
        Assert.AreEqual(original.LogLikelihood(values), clone.LogLikelihood(values), 1e-10);
    }

    /// <summary>
    /// Verifies that the clone keeps the source's parameter values, bounds, and priors (including the
    /// trend intercepts that the constructor would otherwise reset to data-derived defaults) without a
    /// further <c>SetParameterValues</c> call.
    /// </summary>
    [TestMethod]
    public void Clone_PreservesParameterValuesBoundsAndPriors()
    {
        var original = CreateModelWithCopula();
        original.Parameters[0].Value = 17.5;
        original.Parameters[1].Value = original.Parameters[1].Value + 0.25;
        original.Parameters[1].LowerBound = original.Parameters[1].Value - 2.0;
        original.Parameters[1].UpperBound = original.Parameters[1].Value + 2.0;
        original.Parameters[1].PriorDistribution = new Normal(original.Parameters[1].Value, 0.5);
        original.SetParameterValues(original.Parameters.Select(p => p.Value).ToArray());

        var clone = (SpatialGEV)original.Clone();

        for (int i = 0; i < original.NumberOfParameters; i++)
        {
            Assert.AreEqual(original.Parameters[i].Value, clone.Parameters[i].Value, 0.0, $"Value of parameter {i + 1}.");
            Assert.AreEqual(original.Parameters[i].LowerBound, clone.Parameters[i].LowerBound, 0.0, $"Lower bound of parameter {i + 1}.");
            Assert.AreEqual(original.Parameters[i].UpperBound, clone.Parameters[i].UpperBound, 0.0, $"Upper bound of parameter {i + 1}.");
            Assert.AreNotSame(original.Parameters[i], clone.Parameters[i], "Parameter objects are independent.");
        }
        Assert.IsInstanceOfType(clone.Parameters[1].PriorDistribution, typeof(Normal), "The customized prior is cloned.");
        Assert.AreNotSame(original.Parameters[1].PriorDistribution, clone.Parameters[1].PriorDistribution);
        var values = original.Parameters.Select(p => p.Value).ToArray();
        Assert.AreEqual(original.LogLikelihood(values), clone.LogLikelihood(values), 1e-10, "Identical kernel on the clone.");
        clone.Parameters[0].Value = 99.0;
        Assert.AreEqual(17.5, original.Parameters[0].Value, 0.0, "Editing the clone leaves the source untouched.");
    }

    /// <summary>Verifies that clone preserves errors for with spatial errors.</summary>
    [TestMethod]
    public void Clone_WithSpatialErrors_PreservesErrors()
    {
        var original = CreateModelWithSpatialErrors();

        var clone = (SpatialGEV)original.Clone();

        Assert.IsTrue(clone.UseLocationErrors);
        Assert.IsTrue(clone.UseScaleErrors);
        Assert.IsNotNull(clone.LocationErrors);
        Assert.IsNotNull(clone.ScaleErrors);
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>Verifies that to X element creates valid xml.</summary>
    [TestMethod]
    public void ToXElement_CreatesValidXml()
    {
        var model = CreateTestModel();

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement);
        Assert.AreEqual("SpatialGEV", xElement.Name.LocalName);
    }

    /// <summary>Verifies that to X element includes configuration attributes.</summary>
    [TestMethod]
    public void ToXElement_IncludesConfigurationAttributes()
    {
        var model = CreateTestModel();

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement.Attribute("UseCopulaDependence"));
        Assert.IsNotNull(xElement.Attribute("UseLocationErrors"));
        Assert.IsNotNull(xElement.Attribute("UseScaleErrors"));
        Assert.IsNotNull(xElement.Attribute("UseShapeErrors"));
        Assert.IsNotNull(xElement.Attribute("UseLogLinkForLocation"));
        Assert.IsNotNull(xElement.Attribute("UseLogLinkForScale"));
    }

    /// <summary>Verifies that to X element includes site weights.</summary>
    [TestMethod]
    public void ToXElement_IncludesSiteWeights()
    {
        var model = CreateTestModel();
        model.SiteWeights[0] = 2.5;

        var xElement = model.ToXElement();

        var weightsElement = xElement.Element("SiteWeights");
        Assert.IsNotNull(weightsElement);
        Assert.IsTrue(weightsElement.Value.Contains("2.5"));
    }

    /// <summary>Verifies that to X element includes parameters.</summary>
    [TestMethod]
    public void ToXElement_IncludesParameters()
    {
        var model = CreateTestModel();

        var xElement = model.ToXElement();

        var paramsElement = xElement.Element("Parameters");
        Assert.IsNotNull(paramsElement);
        Assert.IsTrue(paramsElement.HasElements);
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns valid when valid model.</summary>
    [TestMethod]
    public void Validate_ValidModel_ReturnsValid()
    {
        var model = CreateTestModel();

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>Verifies that validate returns invalid when copula enabled but null.</summary>
    [TestMethod]
    public void Validate_CopulaEnabledButNull_ReturnsInvalid()
    {
        var model = CreateTestModel();
        model.UseCopulaDependence = true;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Copula")));
    }

    /// <summary>Verifies that validate returns invalid when location errors enabled but null.</summary>
    [TestMethod]
    public void Validate_LocationErrorsEnabledButNull_ReturnsInvalid()
    {
        var model = CreateTestModel();
        model.UseLocationErrors = true;

        var (isValid, _) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns invalid when scale errors enabled but null.</summary>
    [TestMethod]
    public void Validate_ScaleErrorsEnabledButNull_ReturnsInvalid()
    {
        var model = CreateTestModel();
        model.UseScaleErrors = true;

        var (isValid, _) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns invalid when shape errors enabled but null.</summary>
    [TestMethod]
    public void Validate_ShapeErrorsEnabledButNull_ReturnsInvalid()
    {
        var model = CreateTestModel();
        model.UseShapeErrors = true;

        var (isValid, _) = model.Validate();

        Assert.IsFalse(isValid);
    }

    /// <summary>Verifies that validate returns valid when with copula.</summary>
    [TestMethod]
    public void Validate_WithCopula_ReturnsValid()
    {
        var model = CreateModelWithCopula();

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation should pass with copula. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>Verifies that validate returns valid when with spatial errors.</summary>
    [TestMethod]
    public void Validate_WithSpatialErrors_ReturnsValid()
    {
        var model = CreateModelWithSpatialErrors();

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation should pass with spatial errors. Messages: {string.Join(", ", messages)}");
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that model with two sites works correctly.</summary>
    [TestMethod]
    public void Model_WithTwoSites_WorksCorrectly()
    {
        var (data, coords) = CreateMinimalTestData();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        var (isValid, messages) = model.Validate();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double logLH = model.LogLikelihood(parameters);

        Assert.IsTrue(isValid, $"2-site model should be valid. Messages: {string.Join(", ", messages)}");
        Assert.IsTrue(double.IsFinite(logLH));
    }

    /// <summary>Verifies that model colocated sites works correctly.</summary>
    [TestMethod]
    public void Model_ColocatedSites_WorksCorrectly()
    {
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
            { 0.0, 0.0 }
        };

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double logLH = model.LogLikelihood(parameters);

        Assert.IsTrue(double.IsFinite(logLH));
    }

    /// <summary>Verifies that model zero weight excludes site.</summary>
    [TestMethod]
    public void Model_ZeroWeight_ExcludesSite()
    {
        var model = CreateTestModel();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double llAllSites = model.LogLikelihood(parameters);

        Assert.IsTrue(double.IsFinite(llAllSites));

        model.SiteWeights[0] = 0.0;
        double llExcludeOne = model.LogLikelihood(parameters);

        Assert.IsTrue(double.IsFinite(llExcludeOne));
        Assert.AreNotEqual(llAllSites, llExcludeOne, 1e-10);
    }

    /// <summary>Verifies that model all na n at one site handles correctly.</summary>
    [TestMethod]
    public void Model_AllNaNAtOneSite_HandlesCorrectly()
    {
        var data = CreateTestAtSiteData();
        for (int i = 0; i < 30; i++)
        {
            data[i, 2] = double.NaN;
        }

        var coords = CreateTestCoordinates();
        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        double logLH = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(logLH));
        Assert.IsTrue(double.IsFinite(logLH));
    }

    #endregion

    #region Interface Implementation Tests

    /// <summary>Verifies that model implements i simulatable.</summary>
    [TestMethod]
    public void Model_ImplementsISimulatable()
    {
        var model = CreateTestModel();

        Assert.IsInstanceOfType(model, typeof(ISimulatable<double[]>));
    }

    /// <summary>Verifies that model inherits from model base.</summary>
    [TestMethod]
    public void Model_InheritsFromModelBase()
    {
        var model = CreateTestModel();

        Assert.IsInstanceOfType(model, typeof(ModelBase));
    }

    #endregion

    #region Link Function Tests

    /// <summary>Verifies that log link location produces positive values.</summary>
    [TestMethod]
    public void LogLinkLocation_ProducesPositiveValues()
    {
        var model = CreateTestModel();
        model.UseLogLinkForLocation = true;

        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);
            Assert.IsTrue(gevParams[0] > 0);
        }
    }

    /// <summary>Verifies that log link scale produces positive values.</summary>
    [TestMethod]
    public void LogLinkScale_ProducesPositiveValues()
    {
        var model = CreateTestModel();
        model.UseLogLinkForScale = true;

        for (int site = 0; site < model.Sites; site++)
        {
            var gevParams = model.GetGEVParameters(site);
            Assert.IsTrue(gevParams[1] > 0);
        }
    }

    /// <summary>Verifies that identity link location works correctly.</summary>
    [TestMethod]
    public void IdentityLinkLocation_WorksCorrectly()
    {
        var model = CreateTestModel();
        model.UseLogLinkForLocation = false;
        model.SetDefaultParameters();

        var gevParams = model.GetGEVParameters(0);

        Assert.IsFalse(double.IsNaN(gevParams[0]));
    }

    /// <summary>Verifies that identity link scale enforces positive constraint.</summary>
    [TestMethod]
    public void IdentityLinkScale_EnforcesPositiveConstraint()
    {
        var model = CreateTestModel();
        model.UseLogLinkForScale = false;
        model.SetDefaultParameters();

        var gevParams = model.GetGEVParameters(0);

        Assert.IsTrue(gevParams[1] > 0);
    }

    #endregion

    #region Uncertainty / Effective-Sample-Size Helpers

    /// <summary>Verifies that compute intersite correlation returns valid matrix.</summary>
    [TestMethod]
    public void ComputeIntersiteCorrelation_ReturnsValidMatrix()
    {
        var model = CreateTestModel();

        var corrMatrix = model.ComputeIntersiteCorrelation();

        Assert.IsNotNull(corrMatrix);
        Assert.AreEqual(model.Sites, corrMatrix.GetLength(0));
        Assert.AreEqual(model.Sites, corrMatrix.GetLength(1));

        for (int i = 0; i < model.Sites; i++)
        {
            Assert.AreEqual(1.0, corrMatrix[i, i], 1e-10);
        }

        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = i + 1; j < model.Sites; j++)
            {
                Assert.AreEqual(corrMatrix[i, j], corrMatrix[j, i], 1e-10);
            }
        }

        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = 0; j < model.Sites; j++)
            {
                Assert.IsTrue(corrMatrix[i, j] >= -1 && corrMatrix[i, j] <= 1);
            }
        }
    }

    /// <summary>Verifies that compute effective sample size returns positive value.</summary>
    [TestMethod]
    public void ComputeEffectiveSampleSize_ReturnsPositiveValue()
    {
        var model = CreateTestModel();

        double effectiveN = model.ComputeEffectiveSampleSize();

        Assert.IsTrue(effectiveN > 0);
        Assert.IsTrue(effectiveN <= model.Observations * model.Sites);
    }

    /// <summary>Verifies that compute effective sample size with custom correlation matrix works correctly.</summary>
    [TestMethod]
    public void ComputeEffectiveSampleSize_WithCustomCorrelationMatrix_WorksCorrectly()
    {
        var model = CreateTestModel();
        var corrMatrix = new double[model.Sites, model.Sites];

        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = 0; j < model.Sites; j++)
            {
                corrMatrix[i, j] = (i == j) ? 1.0 : 0.5;
            }
        }

        double effectiveN = model.ComputeEffectiveSampleSize(corrMatrix);

        // n_eff = n_total / (1 + (Sites-1) * ρ̄) = 30*5 / (1 + 4*0.5) = 150 / 3 = 50
        double expectedN = (model.Observations * model.Sites) / (1.0 + (model.Sites - 1) * 0.5);
        Assert.AreEqual(expectedN, effectiveN, 1e-6);
    }

    /// <summary>Verifies that compute variance inflation factor returns valid value.</summary>
    [TestMethod]
    public void ComputeVarianceInflationFactor_ReturnsValidValue()
    {
        var model = CreateTestModel();

        double vif = model.ComputeVarianceInflationFactor();

        Assert.IsTrue(vif >= 1.0);
    }

    /// <summary>Verifies that compute variance inflation factor returns correct value when with known correlation.</summary>
    [TestMethod]
    public void ComputeVarianceInflationFactor_WithKnownCorrelation_ReturnsCorrectValue()
    {
        var model = CreateTestModel();
        var corrMatrix = new double[model.Sites, model.Sites];

        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = 0; j < model.Sites; j++)
            {
                corrMatrix[i, j] = (i == j) ? 1.0 : 0.3;
            }
        }

        double vif = model.ComputeVarianceInflationFactor(corrMatrix);

        // VIF = 1 + (Sites-1) * ρ̄ = 1 + 4*0.3 = 2.2
        double expectedVIF = 1.0 + (model.Sites - 1) * 0.3;
        Assert.AreEqual(expectedVIF, vif, 1e-6);
    }

    /// <summary>Verifies that compute effective sample size weights updates site weights.</summary>
    [TestMethod]
    public void ComputeEffectiveSampleSizeWeights_UpdatesSiteWeights()
    {
        var model = CreateTestModel();

        model.ComputeEffectiveSampleSizeWeights();

        Assert.IsNotNull(model.SiteWeights);
        Assert.AreEqual(model.Sites, model.SiteWeights.Length);

        for (int i = 0; i < model.Sites; i++)
        {
            Assert.IsTrue(model.SiteWeights[i] > 0);
        }

        double sumWeights = model.SiteWeights.Sum();
        Assert.AreEqual(model.Sites, sumWeights, 1e-6);
    }

    /// <summary>Verifies that compute effective sample size weights with custom matrix works correctly.</summary>
    [TestMethod]
    public void ComputeEffectiveSampleSizeWeights_WithCustomMatrix_WorksCorrectly()
    {
        var model = CreateTestModel();
        var corrMatrix = new double[model.Sites, model.Sites];

        for (int i = 0; i < model.Sites; i++)
        {
            for (int j = 0; j < model.Sites; j++)
            {
                corrMatrix[i, j] = (i == j) ? 1.0 : 0.0;
            }
        }

        model.ComputeEffectiveSampleSizeWeights(corrMatrix);

        for (int i = 0; i < model.Sites; i++)
        {
            Assert.AreEqual(1.0, model.SiteWeights[i], 1e-6);
        }
    }

    /// <summary>Verifies that compute effective sample size weights throws when mismatched matrix.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ComputeEffectiveSampleSizeWeights_MismatchedMatrix_ThrowsException()
    {
        var model = CreateTestModel();
        var wrongSize = new double[3, 3];

        model.ComputeEffectiveSampleSizeWeights(wrongSize);
    }

    /// <summary>Verifies that configure for proper coverage enables required components.</summary>
    [TestMethod]
    public void ConfigureForProperCoverage_EnablesRequiredComponents()
    {
        var model = CreateTestModel();

        model.ConfigureForProperCoverage();

        Assert.IsTrue(model.UseCopulaDependence);
        Assert.IsNotNull(model.SpatialDependence);
        Assert.IsTrue(model.UseLocationErrors);
        Assert.IsNotNull(model.LocationErrors);
    }

    /// <summary>Verifies that configure for proper coverage with optional errors enables all components.</summary>
    [TestMethod]
    public void ConfigureForProperCoverage_WithOptionalErrors_EnablesAllComponents()
    {
        var model = CreateTestModel();

        model.ConfigureForProperCoverage(
            CorrelationFunctionType.Spherical,
            includeScaleErrors: true,
            includeShapeErrors: true);

        Assert.IsTrue(model.UseCopulaDependence);
        Assert.IsTrue(model.UseLocationErrors);
        Assert.IsTrue(model.UseScaleErrors);
        Assert.IsTrue(model.UseShapeErrors);
        Assert.IsNotNull(model.ScaleErrors);
        Assert.IsNotNull(model.ShapeErrors);
    }

    /// <summary>Verifies that configure for proper coverage with weighted likelihood computes weights.</summary>
    [TestMethod]
    public void ConfigureForProperCoverage_WithWeightedLikelihood_ComputesWeights()
    {
        var model = CreateTestModel();

        model.ConfigureForProperCoverage(useWeightedLikelihood: true);

        Assert.IsTrue(model.SiteWeights.Sum() > 0);
    }

    /// <summary>Verifies that configure for proper coverage rebuilds parameter list.</summary>
    [TestMethod]
    public void ConfigureForProperCoverage_RebuildsParameterList()
    {
        var model = CreateTestModel();
        int originalParamCount = model.NumberOfParameters;

        model.ConfigureForProperCoverage(includeScaleErrors: true);

        Assert.IsTrue(model.NumberOfParameters > originalParamCount);
    }

    #endregion

    #region PredictAtUngauged / GetGEVAtUngauged

    /// <summary>Verifies that predict at ungauged returns valid GEV parameters.</summary>
    [TestMethod]
    public void PredictAtUngauged_ReturnsValidGEVParameters()
    {
        var model = CreateTestModel();
        var newCoords = new double[] { 25.0, 10.0 };

        var (gevParams, _) = model.PredictAtUngauged(newCoords);

        Assert.IsNotNull(gevParams);
        Assert.AreEqual(3, gevParams.Length);

        Assert.IsFalse(double.IsNaN(gevParams[0]));
        Assert.IsTrue(gevParams[0] > 0);
        Assert.IsFalse(double.IsNaN(gevParams[1]));
        Assert.IsTrue(gevParams[1] > 0);
        Assert.IsFalse(double.IsNaN(gevParams[2]));
    }

    /// <summary>Verifies that predict at ungauged returns error variances.</summary>
    [TestMethod]
    public void PredictAtUngauged_ReturnsErrorVariances()
    {
        var model = CreateTestModel();
        var newCoords = new double[] { 25.0, 10.0 };

        var (_, errorVariances) = model.PredictAtUngauged(newCoords);

        Assert.IsNotNull(errorVariances);
        Assert.AreEqual(3, errorVariances.Length);

        Assert.AreEqual(0.0, errorVariances[0], 1e-10);
        Assert.AreEqual(0.0, errorVariances[1], 1e-10);
        Assert.AreEqual(0.0, errorVariances[2], 1e-10);
    }

    /// <summary>Verifies that predict at ungauged returns non negative variances when with spatial errors.</summary>
    [TestMethod]
    public void PredictAtUngauged_WithSpatialErrors_ReturnsNonNegativeVariances()
    {
        var model = CreateModelWithSpatialErrors();
        var newCoords = new double[] { 25.0, 10.0 };

        var (_, errorVariances) = model.PredictAtUngauged(newCoords);

        Assert.IsTrue(errorVariances[0] >= 0);
        Assert.IsTrue(errorVariances[1] >= 0);
    }

    /// <summary>Verifies that predict at ungauged throws when invalid coordinates.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void PredictAtUngauged_InvalidCoordinates_ThrowsException()
    {
        var model = CreateTestModel();
        model.PredictAtUngauged(new double[] { 1.0 });
    }

    /// <summary>Verifies that predict at ungauged throws when null coordinates.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void PredictAtUngauged_NullCoordinates_ThrowsException()
    {
        var model = CreateTestModel();
        model.PredictAtUngauged(null!);
    }

    /// <summary>Verifies that get GEV at ungauged returns valid distribution.</summary>
    [TestMethod]
    public void GetGEVAtUngauged_ReturnsValidDistribution()
    {
        var model = CreateTestModel();
        var newCoords = new double[] { 25.0, 10.0 };

        var gev = model.GetGEVAtUngauged(newCoords);

        Assert.IsNotNull(gev);

        double quantile = gev.InverseCDF(0.99);
        Assert.IsFalse(double.IsNaN(quantile));
        Assert.IsTrue(quantile > 0);

        double cdf = gev.CDF(quantile);
        Assert.AreEqual(0.99, cdf, 1e-6);
    }

    /// <summary>Verifies that get GEV at ungauged matches site GEV for at existing site.</summary>
    [TestMethod]
    public void GetGEVAtUngauged_AtExistingSite_MatchesSiteGEV()
    {
        var model = CreateTestModel();
        var coords = CreateTestCoordinates();

        var site0Coords = new double[] { coords[0, 0], coords[0, 1] };

        var gevPredicted = model.GetGEVAtUngauged(site0Coords);
        var gevSite0Params = model.GetGEVParameters(0);

        Assert.AreEqual(gevSite0Params[0], gevPredicted.Xi, 1e-6);
        Assert.AreEqual(gevSite0Params[1], gevPredicted.Alpha, 1e-6);
        Assert.AreEqual(gevSite0Params[2], gevPredicted.Kappa, 1e-6);
    }

    #endregion

    #region SpatialRegressionErrors Programmatic Tests

    /// <summary>Verifies that spatial regression errors returns valid values when get kriging prediction.</summary>
    [TestMethod]
    public void SpatialRegressionErrors_GetKrigingPrediction_ReturnsValidValues()
    {
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        var paramValues = errors.Parameters.Select(p => p.Value).ToList();
        errors.SetParameterValues(paramValues);

        var newCoords = new double[] { 25.0, 10.0 };

        var (mean, variance) = errors.GetKrigingPrediction(newCoords);

        Assert.IsFalse(double.IsNaN(mean));
        Assert.IsFalse(double.IsNaN(variance));
        Assert.IsTrue(variance >= 0);
    }

    /// <summary>Verifies that spatial regression errors matches site value for get kriging prediction at existing site.</summary>
    [TestMethod]
    public void SpatialRegressionErrors_GetKrigingPrediction_AtExistingSite_MatchesSiteValue()
    {
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        var paramValues = errors.Parameters.Select(p => p.Value).ToList();
        errors.SetParameterValues(paramValues);

        var site0Coords = new double[] { coords[0, 0], coords[0, 1] };

        var (mean, _) = errors.GetKrigingPrediction(site0Coords);

        Assert.AreEqual(errors.GetError(0), mean, 1e-6);
    }

    /// <summary>Verifies that spatial regression errors returns valid values when get IDW prediction.</summary>
    [TestMethod]
    public void SpatialRegressionErrors_GetIDWPrediction_ReturnsValidValues()
    {
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        var paramValues = errors.Parameters.Select(p => p.Value).ToList();
        errors.SetParameterValues(paramValues);

        var newCoords = new double[] { 25.0, 10.0 };

        var (mean, variance) = errors.GetIDWPrediction(newCoords);

        Assert.IsFalse(double.IsNaN(mean));
        Assert.IsFalse(double.IsNaN(variance));
        Assert.IsTrue(variance >= 0);
    }

    /// <summary>Verifies that spatial regression errors throws when get IDW prediction invalid coords.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void SpatialRegressionErrors_GetIDWPrediction_InvalidCoords_ThrowsException()
    {
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.GetIDWPrediction(new double[] { 1.0 });
    }

    /// <summary>Verifies that spatial regression errors returns correct values when coordinates.</summary>
    [TestMethod]
    public void SpatialRegressionErrors_Coordinates_ReturnsCorrectValues()
    {
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        var returnedCoords = errors.Coordinates;

        Assert.IsNotNull(returnedCoords);
        Assert.AreEqual(5, returnedCoords.GetLength(0));
        Assert.AreEqual(2, returnedCoords.GetLength(1));

        Assert.AreEqual(coords[0, 0], returnedCoords[0, 0], 1e-10);
        Assert.AreEqual(coords[0, 1], returnedCoords[0, 1], 1e-10);
    }

    /// <summary>Verifies that spatial regression errors is properly computed when distance matrix.</summary>
    [TestMethod]
    public void SpatialRegressionErrors_DistanceMatrix_IsProperlyComputed()
    {
        var coords = CreateTestCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        var distMatrix = errors.DistanceMatrix;

        Assert.IsNotNull(distMatrix);
        Assert.AreEqual(5, distMatrix.GetLength(0));
        Assert.AreEqual(5, distMatrix.GetLength(1));

        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(0.0, distMatrix[i, i], 1e-10);
        }

        for (int i = 0; i < 5; i++)
        {
            for (int j = i + 1; j < 5; j++)
            {
                Assert.AreEqual(distMatrix[i, j], distMatrix[j, i], 1e-10);
            }
        }

        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (i != j)
                {
                    Assert.IsTrue(distMatrix[i, j] > 0);
                }
            }
        }
    }

    /// <summary>Verifies that spatial regression errors get kriging prediction different correlation functions.</summary>
    [TestMethod]
    public void SpatialRegressionErrors_GetKrigingPrediction_DifferentCorrelationFunctions()
    {
        var coords = CreateTestCoordinates();
        var newCoords = new double[] { 25.0, 10.0 };

        var corrTypes = new[] { CorrelationFunctionType.Exponential, CorrelationFunctionType.Spherical };

        foreach (var corrType in corrTypes)
        {
            var errors = new SpatialRegressionErrors(coords, corrType);
            var paramValues = errors.Parameters.Select(p => p.Value).ToList();
            errors.SetParameterValues(paramValues);

            var (mean, variance) = errors.GetKrigingPrediction(newCoords);

            Assert.IsFalse(double.IsNaN(mean), $"Mean should not be NaN for {corrType}.");
            Assert.IsFalse(double.IsNaN(variance), $"Variance should not be NaN for {corrType}.");
        }
    }

    #endregion

    #region SpatialGEVAnalysis Programmatic Tests

    /// <summary>Verifies that spatial GEV analysis uncertainty method default is bayesian posterior.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_UncertaintyMethod_DefaultIsBayesianPosterior()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        Assert.AreEqual(SpatialGEVUncertaintyMethod.BayesianPosterior, analysis.UncertaintyMethod);
    }

    /// <summary>Verifies that spatial GEV analysis uncertainty method can be set.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_UncertaintyMethod_CanBeSet()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        analysis.UncertaintyMethod = SpatialGEVUncertaintyMethod.GodambeSandwich;

        Assert.AreEqual(SpatialGEVUncertaintyMethod.GodambeSandwich, analysis.UncertaintyMethod);
    }

    /// <summary>Verifies that spatial GEV analysis constructor initializes correctly.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_Constructor_InitializesCorrectly()
    {
        var model = CreateTestModel();

        var analysis = new SpatialGEVAnalysis(model);

        Assert.IsNotNull(analysis.SpatialGEV);
        Assert.IsNotNull(analysis.BayesianAnalysis);
        Assert.IsNotNull(analysis.ProbabilityOrdinates);
        Assert.IsFalse(analysis.IsEstimated);
    }

    /// <summary>Verifies that spatial GEV analysis throws when constructor null model.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SpatialGEVAnalysis_Constructor_NullModel_ThrowsException()
    {
        _ = new SpatialGEVAnalysis(null!);
    }

    /// <summary>Verifies that spatial GEV analysis variance inflation factor default is one.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_VarianceInflationFactor_DefaultIsOne()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        Assert.AreEqual(1.0, analysis.VarianceInflationFactor, 1e-10);
    }

    /// <summary>Verifies that spatial GEV analysis godambe covariance initially null.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_GodambeCovariance_InitiallyNull()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        Assert.IsNull(analysis.GodambeCovariance);
    }

    /// <summary>Verifies that spatial GEV analysis returns true when validate valid model.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_Validate_ValidModel_ReturnsTrue()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>Verifies that spatial GEV analysis clear results clears all results.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_ClearResults_ClearsAllResults()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        analysis.ClearResults();

        Assert.IsNull(analysis.AnalysisResults);
        Assert.IsNull(analysis.SiteResults);
        Assert.IsNull(analysis.CrossValidationResults);
        Assert.IsFalse(analysis.IsEstimated);
    }

    /// <summary>Verifies that spatial GEV analysis throws when compute godambe covariance not estimated.</summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SpatialGEVAnalysis_ComputeGodambeCovariance_NotEstimated_ThrowsException()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        analysis.ComputeGodambeCovariance();
    }

    /// <summary>Verifies that spatial GEV analysis returns valid VIF when inflate posterior covariance.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_InflatePosteriorCovariance_ReturnsValidVIF()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        double vif = analysis.InflatePosteriorCovariance();

        Assert.IsTrue(vif >= 1.0);
        Assert.AreEqual(vif, analysis.VarianceInflationFactor, 1e-10);
    }

    /// <summary>Verifies that spatial GEV analysis throws when get site quantiles not estimated.</summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SpatialGEVAnalysis_GetSiteQuantiles_NotEstimated_ThrowsException()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        analysis.GetSiteQuantiles(0, new double[] { 0.5, 0.1, 0.01 });
    }

    /// <summary>Verifies that spatial GEV analysis throws when predict at ungauged location not estimated.</summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SpatialGEVAnalysis_PredictAtUngaugedLocation_NotEstimated_ThrowsException()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        analysis.PredictAtUngaugedLocation(new double[] { 25.0, 10.0 }, null, new double[] { 0.5, 0.1, 0.01 });
    }

    /// <summary>Verifies that spatial GEV analysis throws when get regional growth curve not estimated.</summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SpatialGEVAnalysis_GetRegionalGrowthCurve_NotEstimated_ThrowsException()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        analysis.GetRegionalGrowthCurve(new double[] { 0.5, 0.1, 0.01 });
    }

    /// <summary>Verifies that spatial GEV analysis throws when run spatial bootstrap async not estimated.</summary>
    [TestMethod]
    public async Task SpatialGEVAnalysis_RunSpatialBootstrapAsync_NotEstimated_ThrowsException()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await analysis.RunSpatialBootstrapAsync(10));
    }

    /// <summary>Verifies that spatial GEV analysis to X element creates valid xml.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_ToXElement_CreatesValidXml()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        var xElement = analysis.ToXElement();

        Assert.IsNotNull(xElement);
        Assert.AreEqual("SpatialGEVAnalysis", xElement.Name.LocalName);
        Assert.IsNotNull(xElement.Attribute("IsEstimated"));
        Assert.IsNotNull(xElement.Element("ProbabilityOrdinates"));
    }

    /// <summary>Verifies that spatial GEV analysis uncertainty method all values can be set.</summary>
    [TestMethod]
    public void SpatialGEVAnalysis_UncertaintyMethod_AllValuesCanBeSet()
    {
        var model = CreateTestModel();
        var analysis = new SpatialGEVAnalysis(model);

        var methods = new[]
        {
            SpatialGEVUncertaintyMethod.BayesianPosterior,
            SpatialGEVUncertaintyMethod.BayesianInflated,
            SpatialGEVUncertaintyMethod.GodambeSandwich,
            SpatialGEVUncertaintyMethod.SpatialBootstrap
        };

        foreach (var method in methods)
        {
            analysis.UncertaintyMethod = method;
            Assert.AreEqual(method, analysis.UncertaintyMethod);
        }
    }

    /// <summary>
    /// Verifies that a realistically sized 100-by-10 matrix can be evaluated without
    /// dimension truncation or a nonfinite likelihood.
    /// </summary>
    [TestMethod]
    public void Model_LargeDataMatrix_ReturnsFiniteLikelihood()
    {
        var data = new double[100, 10];
        var coordinates = new double[10, 2];
        var random = new Random(12345);
        var distribution = new GeneralizedExtremeValue(100.0, 20.0, 0.0);

        for (int site = 0; site < 10; site++)
        {
            coordinates[site, 0] = site * 10.0;
            coordinates[site, 1] = random.NextDouble() * 5.0;
        }

        for (int observation = 0; observation < 100; observation++)
        {
            for (int site = 0; site < 10; site++)
                data[observation, site] = distribution.InverseCDF(random.NextDouble());
        }

        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));

        double likelihood = model.LogLikelihood(model.Parameters.Select(parameter => parameter.Value).ToArray());

        Assert.AreEqual(10, model.Sites);
        Assert.AreEqual(100, model.Observations);
        Assert.IsTrue(double.IsFinite(likelihood));
    }

    #endregion

    #region Observed-Subset Copula and Prior Decomposition Tests

    /// <summary>
    /// Creates a copula model whose data matrix has a row with one missing site and a row with two
    /// missing sites, with an explicit copula range.
    /// </summary>
    /// <param name="parameters">Receives the parameter vector applied to the model.</param>
    /// <returns>The configured model.</returns>
    private static SpatialGEV CreateCopulaModelWithMissingSites(out double[] parameters)
    {
        var data = CreateTestAtSiteData();
        data[3, 1] = double.NaN;
        data[7, 0] = double.NaN;
        data[7, 4] = double.NaN;
        var coords = CreateTestCoordinates();
        var model = new SpatialGEV(data, coords, new GeneralLinearFunction("Location"), new GeneralLinearFunction("Scale"), new GeneralLinearFunction("Shape"));
        model.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();
        parameters = model.Parameters.Select(p => p.Value).ToArray();
        parameters[0] = 20.0;
        model.SetParameterValues(parameters);
        return model;
    }

    /// <summary>
    /// Computes the expected row/year log likelihood of a copula model by hand: the observed-site GEV log
    /// densities plus the observed-subset copula density when at least two sites are observed.
    /// </summary>
    /// <param name="model">The model with its parameter values applied.</param>
    /// <param name="row">The row index.</param>
    /// <param name="placeholderValue">Receives the value obtained with a zero latent score substituted for every missing site and the full-dimensional copula density (the behavior corrected by TR-048).</param>
    /// <returns>The expected row log likelihood.</returns>
    private static double ExpectedRowLogLikelihood(SpatialGEV model, int row, out double placeholderValue)
    {
        var observed = new List<int>();
        var z = new double[model.Sites];
        double marginals = 0.0;
        for (int j = 0; j < model.Sites; j++)
        {
            double value = model.AtSiteData[row, j];
            if (double.IsNaN(value))
                continue;
            var gev = new GeneralizedExtremeValue();
            gev.SetParameters(model.GetGEVParameters(j));
            marginals += gev.LogPDF(value);
            z[j] = Normal.StandardZ(gev.CDF(value));
            observed.Add(j);
        }

        placeholderValue = observed.Count > 0 ? marginals + model.SpatialDependence.LogPDF(z) : 0.0;
        return observed.Count >= 2 ? marginals + model.SpatialDependence.LogPDF(z, observed) : marginals;
    }

    /// <summary>
    /// Verifies that rows with missing sites contribute their observed-site marginals plus the copula density
    /// over the observed-site correlation submatrix, in the scalar and the pointwise likelihood, and that the
    /// zero-placeholder full-dimensional value is no longer produced (TR-048).
    /// </summary>
    [TestMethod]
    public void DataLogLikelihood_WithCopulaAndMissingSites_UsesObservedSiteCopulaSubmatrix()
    {
        SpatialGEV model = CreateCopulaModelWithMissingSites(out double[] parameters);

        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        double expectedTotal = 0.0;
        for (int i = 0; i < model.Observations; i++)
        {
            double expected = ExpectedRowLogLikelihood(model, i, out double placeholder);
            Assert.AreEqual(expected, pointwise[i], 1e-10, $"Row {i + 1}.");
            if (i == 3 || i == 7)
                Assert.AreNotEqual(placeholder, pointwise[i], 1e-6, $"Row {i + 1} must not use the zero-placeholder full-dimensional density.");
            expectedTotal += expected;
        }

        Assert.AreEqual(expectedTotal, model.DataLogLikelihood(parameters), 1e-8, "Scalar likelihood.");
        Assert.AreEqual(pointwise.Sum(), model.DataLogLikelihood(parameters), 1e-8, "Pointwise sum identity.");
    }

    /// <summary>
    /// Verifies that a row with a single observed site contributes its marginal log density only (TR-048).
    /// </summary>
    [TestMethod]
    public void DataLogLikelihood_WithCopula_SingleObservedSiteRow_HasNoDependenceTerm()
    {
        var data = CreateTestAtSiteData();
        for (int j = 0; j < 5; j++)
        {
            if (j != 2)
                data[5, j] = double.NaN;
        }
        var coords = CreateTestCoordinates();
        var model = new SpatialGEV(data, coords, new GeneralLinearFunction("Location"), new GeneralLinearFunction("Scale"), new GeneralLinearFunction("Shape"));
        model.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        parameters[0] = 20.0;
        model.SetParameterValues(parameters);

        var gev = new GeneralizedExtremeValue();
        gev.SetParameters(model.GetGEVParameters(2));
        double expected = gev.LogPDF(data[5, 2]);

        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        Assert.AreEqual(expected, pointwise[5], 1e-12, "A single observed site has no copula term.");
        Assert.AreEqual(pointwise.Sum(), model.DataLogLikelihood(parameters), 1e-8, "Pointwise sum identity.");
    }

    /// <summary>
    /// Verifies that a fully missing row contributes zero and that removing it leaves the likelihood
    /// unchanged (TR-048).
    /// </summary>
    [TestMethod]
    public void DataLogLikelihood_WithCopula_FullyMissingRow_ContributesNothing()
    {
        var full = CreateTestAtSiteData();
        var withMissingRow = (double[,])full.Clone();
        for (int j = 0; j < 5; j++)
            withMissingRow[9, j] = double.NaN;
        var reduced = new double[29, 5];
        for (int i = 0, r = 0; i < 30; i++)
        {
            if (i == 9)
                continue;
            for (int j = 0; j < 5; j++)
                reduced[r, j] = full[i, j];
            r++;
        }
        var coords = CreateTestCoordinates();

        var modelWithRow = new SpatialGEV(withMissingRow, coords, new GeneralLinearFunction("Location"), new GeneralLinearFunction("Scale"), new GeneralLinearFunction("Shape"));
        modelWithRow.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        modelWithRow.UseCopulaDependence = true;
        modelWithRow.SetDefaultParameters();
        var modelWithoutRow = new SpatialGEV(reduced, coords, new GeneralLinearFunction("Location"), new GeneralLinearFunction("Scale"), new GeneralLinearFunction("Shape"));
        modelWithoutRow.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        modelWithoutRow.UseCopulaDependence = true;
        modelWithoutRow.SetDefaultParameters();

        var parameters = modelWithRow.Parameters.Select(p => p.Value).ToArray();
        parameters[0] = 20.0;

        double[] pointwise = modelWithRow.PointwiseDataLogLikelihood(parameters);
        Assert.AreEqual(30, pointwise.Length, "One pointwise term per row/year.");
        Assert.AreEqual(0.0, pointwise[9], 0.0, "A fully missing row contributes zero.");
        Assert.AreEqual(modelWithoutRow.DataLogLikelihood(parameters), modelWithRow.DataLogLikelihood(parameters), 1e-10, "Removing the empty row leaves the likelihood unchanged.");
    }

    /// <summary>
    /// Verifies the hierarchical decomposition with latent spatial errors: the prior log likelihood is the
    /// sum of the parameter priors and the Gaussian-process densities, the data log likelihood holds the
    /// observation terms only, and the scalar/pointwise identities and the posterior kernel identity hold
    /// (TR-049).
    /// </summary>
    [TestMethod]
    public void PriorLogLikelihood_WithSpatialErrors_HoldsGaussianProcessDensities()
    {
        var model = CreateModelWithSpatialErrors();
        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // Parameter layout: [location, scale, shape] then the location-error block [σ, range, ε₁..ε₅]
        // and the scale-error block [σ, range, ε₁..ε₅].
        int locationBlock = 3;
        int scaleBlock = locationBlock + model.LocationErrors.NumberOfParameters;
        Assert.AreEqual(7, model.LocationErrors.NumberOfParameters);
        parameters[locationBlock] = 0.30;
        parameters[locationBlock + 1] = 30.0;
        double[] locationErrors = { 0.05, -0.04, 0.02, 0.01, -0.03 };
        double[] scaleErrors = { -0.02, 0.03, 0.01, -0.01, 0.02 };
        for (int j = 0; j < 5; j++)
        {
            parameters[locationBlock + 2 + j] = locationErrors[j];
            parameters[scaleBlock + 2 + j] = scaleErrors[j];
        }
        parameters[scaleBlock] = 0.20;
        parameters[scaleBlock + 1] = 15.0;
        model.SetParameterValues(parameters);

        double parameterPriors = 0.0;
        for (int i = 0; i < parameters.Length; i++)
            parameterPriors += model.Parameters[i].PriorDistribution.LogPDF(parameters[i]);
        double processDensity = model.LocationErrors.LogPDF() + model.ScaleErrors.LogPDF();
        Assert.IsTrue(double.IsFinite(processDensity) && processDensity != 0.0, "The latent errors carry a nontrivial process density.");

        double marginals = 0.0;
        for (int j = 0; j < model.Sites; j++)
        {
            var gev = new GeneralizedExtremeValue();
            gev.SetParameters(model.GetGEVParameters(j));
            for (int i = 0; i < model.Observations; i++)
                marginals += gev.LogPDF(model.AtSiteData[i, j]);
        }

        double data = model.DataLogLikelihood(parameters);
        double prior = model.PriorLogLikelihood(parameters);
        var components = model.PointwisePriorLogLikelihood(parameters);

        Assert.AreEqual(marginals, data, 1e-8, "The data log likelihood holds the observation terms only.");
        Assert.AreEqual(model.PointwiseDataLogLikelihood(parameters).Sum(), data, 1e-8, "Data equals the pointwise sum.");
        Assert.AreEqual(parameterPriors + processDensity, prior, 1e-10, "The prior holds the parameter priors and the process densities.");
        Assert.AreEqual(prior, components.Sum(c => c.LogLikelihood), 1e-10, "The prior equals the pointwise prior component sum.");
        Assert.AreEqual(2, components.Count(c => c.Type == PriorComponentType.SpatialError), "One spatial-error component per enabled process.");
        Assert.AreEqual(data + prior, model.LogLikelihood(parameters), 1e-8, "The posterior kernel is data plus prior.");
    }

    /// <summary>
    /// Verifies that the prior evaluation is pure: evaluating another parameter vector leaves the model's
    /// parameter values (including the latent error parameters) untouched (TR-049).
    /// </summary>
    [TestMethod]
    public void PriorLogLikelihood_WithSpatialErrors_DoesNotMutateModelState()
    {
        var model = CreateModelWithSpatialErrors();
        var current = model.Parameters.Select(p => p.Value).ToArray();
        var other = (double[])current.Clone();
        for (int i = 0; i < other.Length; i++)
            other[i] += 0.01 * (i + 1);

        double prior = model.PriorLogLikelihood(other);

        Assert.IsFalse(double.IsNaN(prior));
        CollectionAssert.AreEqual(current, model.Parameters.Select(p => p.Value).ToArray(), "The model state must not change.");
    }

    #endregion

    #region Reduced Training Model Tests

    /// <summary>
    /// Verifies that the reduced model of a copula network drops the held-out site from the data, the
    /// coordinates, the site weights, and the copula dimension while keeping the parameter settings.
    /// </summary>
    [TestMethod]
    public void CreateReducedModel_WithCopula_RemovesTheHeldOutSite()
    {
        var original = CreateModelWithCopula();
        original.SiteWeights = new[] { 1.0, 0.9, 0.8, 0.7, 0.6 };
        original.Parameters[0].Value = 22.0;
        original.Parameters[0].UpperBound = 250.0;
        original.SetParameterValues(original.Parameters.Select(p => p.Value).ToArray());

        SpatialGEV reduced = original.CreateReducedModel(2);

        Assert.AreEqual(4, reduced.Sites);
        Assert.AreEqual(original.Observations, reduced.Observations);
        Assert.AreEqual(4, reduced.SpatialDependence.Sites, "The copula dimension follows the remaining sites.");
        Assert.AreEqual(original.NumberOfParameters, reduced.NumberOfParameters, "Intercept-only trends and one copula range: the parameter count is unchanged.");
        CollectionAssert.AreEqual(new[] { 1.0, 0.9, 0.7, 0.6 }, reduced.SiteWeights);
        int[] kept = { 0, 1, 3, 4 };
        for (int r = 0; r < 4; r++)
        {
            Assert.AreEqual(original.Coordinates[kept[r], 0], reduced.Coordinates[r, 0], 0.0);
            Assert.AreEqual(original.Coordinates[kept[r], 1], reduced.Coordinates[r, 1], 0.0);
            for (int i = 0; i < original.Observations; i++)
                Assert.AreEqual(original.AtSiteData[i, kept[r]], reduced.AtSiteData[i, r], 0.0);
        }
        for (int i = 0; i < original.NumberOfParameters; i++)
        {
            Assert.AreEqual(original.Parameters[i].Value, reduced.Parameters[i].Value, 0.0, $"Value {i + 1}.");
            Assert.AreEqual(original.Parameters[i].LowerBound, reduced.Parameters[i].LowerBound, 0.0, $"Lower bound {i + 1}.");
            Assert.AreEqual(original.Parameters[i].UpperBound, reduced.Parameters[i].UpperBound, 0.0, $"Upper bound {i + 1}.");
        }
        Assert.IsTrue(reduced.UseCopulaDependence && reduced.UseLogLinkForLocation == original.UseLogLinkForLocation && reduced.UseLogLinkForScale == original.UseLogLinkForScale);
        Assert.IsTrue(reduced.Validate().IsValid);
    }

    /// <summary>
    /// Verifies that the reduced model's likelihood does not depend on the held-out site's observations
    /// and equals the likelihood of a network built directly without that site.
    /// </summary>
    [TestMethod]
    public void CreateReducedModel_WithCopula_IsIndependentOfTheHeldOutSite()
    {
        var original = CreateModelWithCopula();
        var values = original.Parameters.Select(p => p.Value).ToArray();
        values[0] = 20.0;
        original.SetParameterValues(values);

        SpatialGEV reduced = original.CreateReducedModel(1);
        double before = reduced.DataLogLikelihood(values);
        for (int i = 0; i < original.Observations; i++)
            original.AtSiteData[i, 1] *= 2.0;
        double after = reduced.DataLogLikelihood(values);

        var data = new double[30, 4];
        var coords = new double[4, 2];
        int[] kept = { 0, 2, 3, 4 };
        for (int r = 0; r < 4; r++)
        {
            coords[r, 0] = original.Coordinates[kept[r], 0];
            coords[r, 1] = original.Coordinates[kept[r], 1];
            for (int i = 0; i < 30; i++)
                data[i, r] = original.AtSiteData[i, kept[r]];
        }
        var direct = new SpatialGEV(data, coords, new GeneralLinearFunction("Location"), new GeneralLinearFunction("Scale"), new GeneralLinearFunction("Shape"));
        direct.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        direct.UseCopulaDependence = true;
        direct.SetDefaultParameters();

        Assert.AreEqual(before, after, 0.0, "The held-out column does not enter the reduced likelihood.");
        Assert.AreEqual(direct.DataLogLikelihood(values), before, 1e-10, "Equal to the network built without the site.");
    }

    /// <summary>
    /// Verifies that the reduced model removes the held-out covariate row of a regression trend and keeps
    /// the coefficient settings, so site predictions of the remaining sites are unchanged.
    /// </summary>
    [TestMethod]
    public void CreateReducedModel_WithCovariateTrend_RemovesTheHeldOutRow()
    {
        var data = CreateTestAtSiteData();
        var coords = CreateTestCoordinates();
        var covariates = new double[5, 2];
        for (int j = 0; j < 5; j++)
        {
            covariates[j, 0] = coords[j, 0];
            covariates[j, 1] = coords[j, 1];
        }
        var original = new SpatialGEV(data, coords, new GeneralLinearFunction("Location", covariates), new GeneralLinearFunction("Scale"), new GeneralLinearFunction("Shape"));
        original.Parameters[1].Value = 0.01;
        original.Parameters[2].Value = -0.02;
        original.Parameters[1].PriorDistribution = new Normal(0.0, 0.1);
        original.SetParameterValues(original.Parameters.Select(p => p.Value).ToArray());

        SpatialGEV reduced = original.CreateReducedModel(3);

        Assert.AreEqual(2, reduced.Location.NumberOfCovariates);
        Assert.AreEqual(4, reduced.Location.Covariates!.GetLength(0), "One covariate row per remaining site.");
        Assert.AreEqual(original.NumberOfParameters, reduced.NumberOfParameters);
        Assert.IsInstanceOfType(reduced.Parameters[1].PriorDistribution, typeof(Normal), "The coefficient prior is copied.");
        int[] kept = { 0, 1, 2, 4 };
        for (int r = 0; r < 4; r++)
        {
            Assert.AreEqual(original.Location.Predict(kept[r]), reduced.Location.Predict(r), 1e-12, $"Location trend of remaining site {r + 1}.");
            CollectionAssert.AreEqual(original.GetGEVParameters(kept[r]), reduced.GetGEVParameters(r), $"GEV parameters of remaining site {r + 1}.");
        }
    }

    /// <summary>
    /// Verifies that the reduced model removes the held-out site's latent error from every enabled error
    /// block and keeps the remaining latent errors and hyperparameters.
    /// </summary>
    [TestMethod]
    public void CreateReducedModel_WithLatentErrors_RemovesTheHeldOutLatentError()
    {
        var original = CreateModelWithSpatialErrors();
        var values = original.Parameters.Select(p => p.Value).ToArray();
        // Location block [σ, range, ε₁..ε₅] at 3.., scale block at 10..
        values[3] = 0.3;
        values[4] = 25.0;
        for (int j = 0; j < 5; j++)
        {
            values[5 + j] = 0.01 * (j + 1);
            values[12 + j] = -0.02 * (j + 1);
        }
        values[10] = 0.2;
        values[11] = 15.0;
        original.SetParameterValues(values);

        SpatialGEV reduced = original.CreateReducedModel(2);

        Assert.AreEqual(17 - 2, reduced.NumberOfParameters, "One latent error leaves each of the two error blocks.");
        Assert.AreEqual(4, reduced.LocationErrors.Sites);
        Assert.AreEqual(0.3, reduced.Parameters[3].Value, 0.0);
        Assert.AreEqual(25.0, reduced.Parameters[4].Value, 0.0);
        CollectionAssert.AreEqual(new[] { 0.01, 0.02, 0.04, 0.05 }, reduced.Parameters.Skip(5).Take(4).Select(p => p.Value).ToArray());
        Assert.AreEqual(0.2, reduced.Parameters[9].Value, 0.0);
        Assert.AreEqual(15.0, reduced.Parameters[10].Value, 0.0);
        CollectionAssert.AreEqual(new[] { -0.02, -0.04, -0.08, -0.10 }, reduced.Parameters.Skip(11).Take(4).Select(p => p.Value).ToArray());
        int[] kept = { 0, 1, 3, 4 };
        for (int r = 0; r < 4; r++)
            CollectionAssert.AreEqual(original.GetGEVParameters(kept[r]), reduced.GetGEVParameters(r), $"GEV parameters of remaining site {r + 1}.");
        Assert.IsTrue(double.IsFinite(reduced.LogLikelihood(reduced.Parameters.Select(p => p.Value).ToArray())));
    }

    /// <summary>
    /// Verifies the argument validation of the reduced-model factory.
    /// </summary>
    [TestMethod]
    public void CreateReducedModel_InvalidSite_Throws()
    {
        var model = CreateTestModel();

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.CreateReducedModel(-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.CreateReducedModel(5));
    }

    #endregion
}
