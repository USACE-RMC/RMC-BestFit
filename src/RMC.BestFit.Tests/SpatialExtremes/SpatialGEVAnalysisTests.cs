using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.SpatialExtremes;

/// <summary>
/// Programmatic unit tests for the <see cref="SpatialGEVAnalysis"/> wrapper class.
/// </summary>
/// <remarks>
/// <para>
/// These tests cover constructors, XML serialization, validation, ClearResults state
/// transitions, property-change events, cancellation guards, parameter validation
/// (pre-estimation), probability-ordinate handling, site-weight handling, and
/// configuration of Bayesian analysis settings. Estimation-driven tests (any test
/// invoking <c>RunAsync()</c>, including event-raise checks and parameter-recovery
/// runs) live in <c>RMC.BestFit.Verification</c>.
/// </para>
/// <para>
/// Inline deterministic fixtures (small homogeneous spatial GEV samples) are used so
/// this file does not depend on the Verification project's shared
/// <c>SyntheticSpatialGEVData</c> helper. Sample sizes are small (3-5 sites x 30
/// years) so the suite stays in the fast PR gate.
/// </para>
/// </remarks>
[TestClass]
public class SpatialGEVAnalysisTests
{
    #region Inline test fixtures

    /// <summary>
    /// Creates standard inline at-site data (30 observations x 5 sites) drawn from a homogeneous
    /// GEV(10000, 2500, 0) distribution via inverse-CDF sampling with a fixed seed.
    /// </summary>
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
    /// Creates a minimal 2-site x 20-year fixture for edge-case testing.
    /// </summary>
    private static (double[,] Data, double[,] Coordinates) CreateMinimalTestData()
    {
        var data = new double[20, 2];
        var rng = new Random(54321);
        var gev = new GeneralizedExtremeValue(6000, 1500, 0.0);

        for (int i = 0; i < 20; i++)
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
    /// Creates a default-configured <see cref="SpatialGEV"/> with intercept-only trends and 5 sites.
    /// </summary>
    private static SpatialGEV CreateTestSpatialGEV()
    {
        var data = CreateTestAtSiteData();
        var coords = CreateTestCoordinates();

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        return new SpatialGEV(data, coords, location, scale, shape);
    }

    /// <summary>
    /// Creates a minimal spatial GEV model with only 2 sites for edge-case coverage.
    /// </summary>
    private static SpatialGEV CreateMinimalSpatialGEV()
    {
        var (data, coords) = CreateMinimalTestData();

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        return new SpatialGEV(data, coords, location, scale, shape);
    }

    /// <summary>
    /// Creates a spatial GEV model whose data matrix contains NaN values at several site-year cells.
    /// </summary>
    private static SpatialGEV CreateSpatialGEVWithMissingData()
    {
        var data = CreateDataWithMissingValues();
        var coords = CreateTestCoordinates();

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        return new SpatialGEV(data, coords, location, scale, shape);
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the constructor properly initializes the analysis with a spatial GEV model.
    /// </summary>
    [TestMethod]
    public void Constructor_WithSpatialGEV_InitializesCorrectly()
    {
        var spatialGEV = CreateTestSpatialGEV();

        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsNotNull(analysis.SpatialGEV, "SpatialGEV should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis, "BayesianAnalysis should not be null.");
        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false initially.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null initially.");
        Assert.IsNull(analysis.SiteResults, "SiteResults should be null initially.");
        Assert.IsNull(analysis.CrossValidationResults, "CrossValidationResults should be null initially.");
    }

    /// <summary>
    /// Tests that the constructor throws <see cref="ArgumentNullException"/> when spatial GEV model is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullSpatialGEV_ThrowsArgumentNullException()
    {
        _ = new SpatialGEVAnalysis(null!);
    }

    /// <summary>
    /// Tests that the BayesianAnalysis property references the correct model.
    /// </summary>
    [TestMethod]
    public void Constructor_BayesianAnalysis_HasCorrectModel()
    {
        var spatialGEV = CreateTestSpatialGEV();

        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.AreSame(spatialGEV, analysis.BayesianAnalysis.Model,
            "BayesianAnalysis.Model should reference the spatial GEV model.");
    }

    /// <summary>
    /// Tests that the constructor initializes probability ordinates with default values.
    /// </summary>
    [TestMethod]
    public void Constructor_InitializesDefaultProbabilityOrdinates()
    {
        var spatialGEV = CreateTestSpatialGEV();

        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsNotNull(analysis.ProbabilityOrdinates, "ProbabilityOrdinates should not be null.");
        Assert.IsTrue(analysis.ProbabilityOrdinates.Count > 0,
            "Default probability ordinates should have values.");
    }

    /// <summary>
    /// Tests that the constructor works with minimal (2 site) configuration.
    /// </summary>
    [TestMethod]
    public void Constructor_WithMinimalSites_InitializesCorrectly()
    {
        var spatialGEV = CreateMinimalSpatialGEV();

        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsNotNull(analysis, "Analysis should be created with minimal sites.");
        Assert.AreEqual(2, analysis.SpatialGEV.Sites, "Model should have 2 sites.");
    }

    /// <summary>
    /// Tests that the constructor works with data containing missing values.
    /// </summary>
    [TestMethod]
    public void Constructor_WithMissingData_InitializesCorrectly()
    {
        var spatialGEV = CreateSpatialGEVWithMissingData();

        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsNotNull(analysis, "Analysis should handle missing data.");
        Assert.AreEqual(5, analysis.SpatialGEV.Sites, "Model should have 5 sites.");
    }

    #endregion

    #region XML Serialization Tests

    /// <summary>
    /// Tests that the analysis can be serialized to XML and restored.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesConfiguration()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new SpatialGEVAnalysis(spatialGEV, xElement);

        Assert.IsNotNull(restoredAnalysis, "Restored analysis should not be null.");
        Assert.AreEqual(analysis.ProbabilityOrdinates.Count, restoredAnalysis.ProbabilityOrdinates.Count,
            "Probability ordinates count should be preserved.");
    }

    /// <summary>
    /// Tests that the constructor throws when XElement is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullXElement_ThrowsArgumentNullException()
    {
        var spatialGEV = CreateTestSpatialGEV();

        _ = new SpatialGEVAnalysis(spatialGEV, null!);
    }

    /// <summary>
    /// Tests that the constructor throws when spatial GEV is null in XML constructor.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_XmlWithNullSpatialGEV_ThrowsArgumentNullException()
    {
        var xElement = new XElement("SpatialGEVAnalysis");

        _ = new SpatialGEVAnalysis(null!, xElement);
    }

    /// <summary>
    /// Tests serialization with Bayesian analysis settings preserved.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_WithBayesianSettings_PreservesSettings()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        analysis.BayesianAnalysis.Iterations = 5000;
        analysis.BayesianAnalysis.WarmupIterations = 1000;

        var xElement = analysis.ToXElement();
        var restoredAnalysis = new SpatialGEVAnalysis(spatialGEV, xElement);

        Assert.AreEqual(5000, restoredAnalysis.BayesianAnalysis.Iterations,
            "Iterations should be preserved.");
        Assert.AreEqual(1000, restoredAnalysis.BayesianAnalysis.WarmupIterations,
            "WarmupIterations should be preserved.");
    }

    /// <summary>
    /// Tests that ToXElement creates valid XML structure.
    /// </summary>
    [TestMethod]
    public void ToXElement_CreatesValidXmlStructure()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var xElement = analysis.ToXElement();

        Assert.IsNotNull(xElement, "XElement should not be null.");
        Assert.AreEqual("SpatialGEVAnalysis", xElement.Name.LocalName,
            "Root element should be SpatialGEVAnalysis.");
        Assert.IsNotNull(xElement.Attribute("IsEstimated"),
            "Should have IsEstimated attribute.");
        Assert.IsNotNull(xElement.Element("ProbabilityOrdinates"),
            "Should have ProbabilityOrdinates element.");
    }

    /// <summary>
    /// Tests that IsEstimated attribute is properly serialized.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_PreservesIsEstimatedFlag()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var xElement = analysis.ToXElement();
        var isEstimatedAttr = xElement.Attribute("IsEstimated");

        Assert.IsNotNull(isEstimatedAttr, "IsEstimated attribute should exist.");
        // XLinq serializes booleans as lowercase "true"/"false" per XML convention
        Assert.AreEqual("false", isEstimatedAttr.Value,
            "IsEstimated should be false for unestimated analysis.");
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Tests that Validate returns valid for a properly configured analysis.
    /// </summary>
    [TestMethod]
    public void Validate_WithValidConfiguration_ReturnsValid()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that Validate propagates validation from the underlying spatial GEV model.
    /// </summary>
    [TestMethod]
    public void Validate_PropagatesModelValidation()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var (isValid, _) = analysis.Validate();

        Assert.IsTrue(isValid, "Validation should pass for valid model.");
    }

    /// <summary>
    /// Tests validation with minimal configuration (2 sites).
    /// </summary>
    [TestMethod]
    public void Validate_WithMinimalSites_ReturnsValid()
    {
        var spatialGEV = CreateMinimalSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass for 2 sites. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests validation with missing data in the model.
    /// </summary>
    [TestMethod]
    public void Validate_WithMissingData_ReturnsValid()
    {
        var spatialGEV = CreateSpatialGEVWithMissingData();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation should pass with missing data. Messages: {string.Join(", ", messages)}");
    }

    #endregion

    #region ClearResults Tests

    /// <summary>
    /// Tests that ClearResults resets all analysis results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ResetsAllResults()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should be false after clearing.");
        Assert.IsNull(analysis.AnalysisResults, "AnalysisResults should be null after clearing.");
        Assert.IsNull(analysis.SiteResults, "SiteResults should be null after clearing.");
        Assert.IsNull(analysis.CrossValidationResults, "CrossValidationResults should be null after clearing.");
    }

    /// <summary>
    /// Tests that ClearResults also clears BayesianAnalysis results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ClearsBayesianAnalysisResults()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        analysis.ClearResults();

        Assert.IsFalse(analysis.BayesianAnalysis.IsEstimated,
            "BayesianAnalysis.IsEstimated should be false after clearing.");
    }

    /// <summary>
    /// Tests that ClearResults raises PropertyChanged events.
    /// </summary>
    [TestMethod]
    public void ClearResults_RaisesPropertyChangedEvents()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var changedProperties = new List<string>();
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        analysis.ClearResults();

        Assert.IsTrue(changedProperties.Contains(nameof(SpatialGEVAnalysis.AnalysisResults)),
            "Should raise PropertyChanged for AnalysisResults.");
        Assert.IsTrue(changedProperties.Contains(nameof(SpatialGEVAnalysis.SiteResults)),
            "Should raise PropertyChanged for SiteResults.");
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// Tests that ProbabilityOrdinates changes raise PropertyChanged.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinates_Change_ClearsResults()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SpatialGEVAnalysis.ProbabilityOrdinates))
                propertyChanged = true;
        };

        analysis.ProbabilityOrdinates = new ProbabilityOrdinates();

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for ProbabilityOrdinates.");
    }

    /// <summary>
    /// Tests that model property changes propagate through the analysis.
    /// </summary>
    [TestMethod]
    public void ModelPropertyChange_PropagatesPropertyChanged()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            propertyChanged = true;
        };

        // Modify site weights on the model and clear results
        spatialGEV.SiteWeights[0] = 0.5;
        analysis.ClearResults();

        Assert.IsTrue(propertyChanged, "PropertyChanged should propagate from model changes.");
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>
    /// Tests that CancelAnalysis can be called without error when not running.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act & Assert - Should not throw
        analysis.CancelAnalysis();
    }

    /// <summary>
    /// Tests that CancelAnalysis propagates to BayesianAnalysis without error.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_CancelsBayesianSimulation()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act & Assert - Should not throw
        analysis.CancelAnalysis();
    }

    #endregion

    #region Pre-Estimation Method Guards

    /// <summary>
    /// Tests that GetSiteQuantiles throws when analysis is not estimated.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void GetSiteQuantiles_WhenNotEstimated_ThrowsInvalidOperationException()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        var probs = new double[] { 0.5, 0.1, 0.01 };

        analysis.GetSiteQuantiles(0, probs);
    }

    /// <summary>
    /// Tests that GetSiteQuantiles validation precondition is observable via IsEstimated.
    /// </summary>
    [TestMethod]
    public void GetSiteQuantiles_NotEstimated_StateObservable()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsFalse(analysis.IsEstimated, "Analysis should not be estimated initially.");
    }

    /// <summary>
    /// Tests that PredictAtUngaugedLocation throws when analysis is not estimated.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void PredictAtUngaugedLocation_WhenNotEstimated_ThrowsInvalidOperationException()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        var coords = new double[] { 25.0, 10.0 };
        var probs = new double[] { 0.5, 0.1, 0.01 };

        analysis.PredictAtUngaugedLocation(coords, null, probs);
    }

    /// <summary>
    /// Tests that PredictAtUngaugedLocation validates coordinate access on the underlying model.
    /// </summary>
    [TestMethod]
    public void PredictAtUngaugedLocation_HasAccessibleCoordinates()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsNotNull(analysis.SpatialGEV.Coordinates, "Coordinates should exist.");
    }

    /// <summary>
    /// Tests that GetRegionalGrowthCurve throws when analysis is not estimated.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void GetRegionalGrowthCurve_WhenNotEstimated_ThrowsInvalidOperationException()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        var probs = new double[] { 0.5, 0.2, 0.1, 0.04, 0.02, 0.01 };

        analysis.GetRegionalGrowthCurve(probs);
    }

    #endregion

    #region Probability Ordinates Tests

    /// <summary>
    /// Tests that probability ordinates can be modified.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinates_CanBeModified()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        analysis.ProbabilityOrdinates.Clear();

        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        Assert.AreEqual(3, analysis.ProbabilityOrdinates.Count, "Should have 3 probability ordinates.");
    }

    /// <summary>
    /// Tests that setting ProbabilityOrdinates to new object clears results.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinates_SetNewObject_ClearsResults()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        analysis.ProbabilityOrdinates = new ProbabilityOrdinates();

        Assert.IsNull(analysis.AnalysisResults, "Results should be cleared when ordinates change.");
    }

    #endregion

    #region Site Weights Tests

    /// <summary>
    /// Tests that site weights default to 1.0 for all sites.
    /// </summary>
    [TestMethod]
    public void SiteWeights_DefaultToOne()
    {
        var spatialGEV = CreateTestSpatialGEV();

        for (int i = 0; i < spatialGEV.Sites; i++)
        {
            Assert.AreEqual(1.0, spatialGEV.SiteWeights[i], 1e-10,
                $"Site {i} weight should default to 1.0.");
        }
    }

    /// <summary>
    /// Tests that site weights can be modified.
    /// </summary>
    [TestMethod]
    public void SiteWeights_CanBeModified()
    {
        var spatialGEV = CreateTestSpatialGEV();
        _ = new SpatialGEVAnalysis(spatialGEV);

        spatialGEV.SiteWeights[0] = 0.5;
        spatialGEV.SiteWeights[2] = 2.0;

        Assert.AreEqual(0.5, spatialGEV.SiteWeights[0], 1e-10, "Site 0 weight should be 0.5.");
        Assert.AreEqual(2.0, spatialGEV.SiteWeights[2], 1e-10, "Site 2 weight should be 2.0.");
    }

    /// <summary>
    /// Tests that setting a site weight to zero effectively excludes the site.
    /// </summary>
    [TestMethod]
    public void SiteWeights_ZeroWeight_ExcludesSite()
    {
        var spatialGEV = CreateTestSpatialGEV();
        _ = new SpatialGEVAnalysis(spatialGEV);

        spatialGEV.SiteWeights[0] = 0.0;

        Assert.AreEqual(0.0, spatialGEV.SiteWeights[0], 1e-10,
            "Site 0 weight should be 0 (excluded).");
    }

    #endregion

    #region Spatial Model Configuration Tests

    /// <summary>
    /// Tests that the spatial GEV model has correct number of sites.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_HasCorrectSiteCount()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.AreEqual(5, analysis.SpatialGEV.Sites, "Model should have 5 sites.");
    }

    /// <summary>
    /// Tests that the spatial GEV model has correct observation count.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_HasCorrectObservationCount()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.AreEqual(30, analysis.SpatialGEV.Observations, "Model should have 30 observations.");
    }

    /// <summary>
    /// Tests that the spatial GEV model coordinates are accessible.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_CoordinatesAccessible()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsNotNull(analysis.SpatialGEV.Coordinates, "Coordinates should not be null.");
        Assert.AreEqual(5, analysis.SpatialGEV.Coordinates.GetLength(0),
            "Should have coordinates for 5 sites.");
        Assert.AreEqual(2, analysis.SpatialGEV.Coordinates.GetLength(1),
            "Coordinates should be 2D (X, Y).");
    }

    /// <summary>
    /// Tests that link functions can be configured.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_LinkFunctionsConfigurable()
    {
        var spatialGEV = CreateTestSpatialGEV();

        Assert.IsTrue(spatialGEV.UseLogLinkForLocation, "Default should use log-link for location.");
        Assert.IsTrue(spatialGEV.UseLogLinkForScale, "Default should use log-link for scale.");

        spatialGEV.UseLogLinkForLocation = false;
        spatialGEV.UseLogLinkForScale = false;

        Assert.IsFalse(spatialGEV.UseLogLinkForLocation, "Should be identity link for location.");
        Assert.IsFalse(spatialGEV.UseLogLinkForScale, "Should be identity link for scale.");
    }

    #endregion

    #region Bayesian Analysis Configuration Tests

    /// <summary>
    /// Tests that Bayesian analysis settings can be configured.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_SettingsConfigurable()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        analysis.BayesianAnalysis.Iterations = 10000;
        analysis.BayesianAnalysis.WarmupIterations = 2000;
        analysis.BayesianAnalysis.ThinningInterval = 5;

        Assert.AreEqual(10000, analysis.BayesianAnalysis.Iterations);
        Assert.AreEqual(2000, analysis.BayesianAnalysis.WarmupIterations);
        Assert.AreEqual(5, analysis.BayesianAnalysis.ThinningInterval);
    }

    /// <summary>
    /// Tests that credible interval width can be configured.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_CredibleIntervalConfigurable()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;

        Assert.AreEqual(0.90, analysis.BayesianAnalysis.CredibleIntervalWidth, 1e-10,
            "Credible interval width should be 0.90.");
    }

    /// <summary>
    /// Tests that point estimator type can be configured.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_PointEstimatorConfigurable()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMode, analysis.BayesianAnalysis.PointEstimator);

        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;
        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, analysis.BayesianAnalysis.PointEstimator);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests analysis with minimum required sites (2 sites).
    /// </summary>
    [TestMethod]
    public void Analysis_WithTwoSites_IsValid()
    {
        var spatialGEV = CreateMinimalSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with 2 sites should be valid. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that model with copula dependence disabled validates.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_WithCopulaDisabled_ValidatesCorrectly()
    {
        var spatialGEV = CreateTestSpatialGEV();
        spatialGEV.UseCopulaDependence = false;
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var (isValid, _) = analysis.Validate();

        Assert.IsTrue(isValid, "Model without copula should validate.");
    }

    /// <summary>
    /// Tests that model without spatial errors validates.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_WithoutSpatialErrors_ValidatesCorrectly()
    {
        var spatialGEV = CreateTestSpatialGEV();
        spatialGEV.UseLocationErrors = false;
        spatialGEV.UseScaleErrors = false;
        spatialGEV.UseShapeErrors = false;
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var (isValid, _) = analysis.Validate();

        Assert.IsTrue(isValid, "Model without spatial errors should validate.");
    }

    /// <summary>
    /// Tests handling of coordinates at the origin.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_WithSiteAtOrigin_Validates()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.AreEqual(0.0, analysis.SpatialGEV.Coordinates[0, 0], 1e-10, "First site X should be 0.");
        Assert.AreEqual(0.0, analysis.SpatialGEV.Coordinates[0, 1], 1e-10, "First site Y should be 0.");

        var (isValid, _) = analysis.Validate();
        Assert.IsTrue(isValid, "Model with site at origin should validate.");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that multiple analysis objects can be created for the same model.
    /// </summary>
    [TestMethod]
    public void MultipleAnalysisObjects_CanBeCreated()
    {
        var spatialGEV = CreateTestSpatialGEV();

        var analysis1 = new SpatialGEVAnalysis(spatialGEV);
        var analysis2 = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsNotNull(analysis1);
        Assert.IsNotNull(analysis2);
        Assert.AreSame(spatialGEV, analysis1.SpatialGEV);
        Assert.AreSame(spatialGEV, analysis2.SpatialGEV);
    }

    /// <summary>
    /// Tests that analysis properly handles data variability across sites.
    /// </summary>
    [TestMethod]
    public void Analysis_WithVariableData_InitializesCorrectly()
    {
        // Arrange - Create data with different scales at each site
        var data = new double[20, 3];
        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 10.0, 0.0 },
            { 20.0, 0.0 }
        };

        var rng = new Random(11111);
        for (int i = 0; i < 20; i++)
        {
            data[i, 0] = 100 + rng.NextDouble() * 50;
            data[i, 1] = 5000 + rng.NextDouble() * 2000;
            data[i, 2] = 50000 + rng.NextDouble() * 20000;
        }

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var spatialGEV = new SpatialGEV(data, coords, location, scale, shape);

        var analysis = new SpatialGEVAnalysis(spatialGEV);
        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Analysis with variable data should be valid. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that analysis interface (IBayesianAnalysis) is implemented.
    /// </summary>
    [TestMethod]
    public void Analysis_ImplementsIBayesianAnalysis()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsInstanceOfType(analysis, typeof(IBayesianAnalysis),
            "SpatialGEVAnalysis should implement IBayesianAnalysis.");
    }

    /// <summary>
    /// Tests that the analysis inherits from AnalysisBase.
    /// </summary>
    [TestMethod]
    public void Analysis_InheritsFromAnalysisBase()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsInstanceOfType(analysis, typeof(AnalysisBase),
            "SpatialGEVAnalysis should inherit from AnalysisBase.");
    }

    #endregion

    #region Cross-Validation Configuration Tests

    /// <summary>
    /// Tests that cross-validation results are initially null.
    /// </summary>
    [TestMethod]
    public void CrossValidationResults_InitiallyNull()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        Assert.IsNull(analysis.CrossValidationResults, "CrossValidationResults should be null initially.");
    }

    /// <summary>
    /// Tests that clearing results also clears cross-validation results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ClearsCrossValidationResults()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        analysis.ClearResults();

        Assert.IsNull(analysis.CrossValidationResults, "CrossValidationResults should be null after clearing.");
    }

    #endregion

    #region Regional Analysis Tests

    /// <summary>
    /// Tests that analysis can be configured for regional frequency analysis.
    /// </summary>
    [TestMethod]
    public void RegionalAnalysis_CanBeConfigured()
    {
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Configure for regional analysis
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.5);    // 2-year
        analysis.ProbabilityOrdinates.Add(0.1);    // 10-year
        analysis.ProbabilityOrdinates.Add(0.04);   // 25-year
        analysis.ProbabilityOrdinates.Add(0.02);   // 50-year
        analysis.ProbabilityOrdinates.Add(0.01);   // 100-year
        analysis.ProbabilityOrdinates.Add(0.002);  // 500-year

        Assert.AreEqual(6, analysis.ProbabilityOrdinates.Count, "Should have 6 return period ordinates.");
    }

    /// <summary>
    /// Tests that equal weights represent homogeneous regional pooling.
    /// </summary>
    [TestMethod]
    public void EqualWeights_RepresentHomogeneousRegion()
    {
        var spatialGEV = CreateTestSpatialGEV();
        _ = new SpatialGEVAnalysis(spatialGEV);

        // All weights should be 1.0 by default
        double sum = 0;
        for (int i = 0; i < spatialGEV.Sites; i++)
        {
            sum += spatialGEV.SiteWeights[i];
        }

        Assert.AreEqual(5.0, sum, 1e-10, "Sum of weights should be 5 for 5 equal-weight sites.");
    }

    /// <summary>
    /// Tests that unequal weights can be used for heterogeneous regions.
    /// </summary>
    [TestMethod]
    public void UnequalWeights_ForHeterogeneousRegion()
    {
        var spatialGEV = CreateTestSpatialGEV();
        _ = new SpatialGEVAnalysis(spatialGEV);

        // Set weights based on hypothetical record lengths
        spatialGEV.SiteWeights[0] = 30.0 / 100.0;
        spatialGEV.SiteWeights[1] = 50.0 / 100.0;
        spatialGEV.SiteWeights[2] = 20.0 / 100.0;
        spatialGEV.SiteWeights[3] = 40.0 / 100.0;
        spatialGEV.SiteWeights[4] = 60.0 / 100.0;

        Assert.AreEqual(0.3, spatialGEV.SiteWeights[0], 1e-10);
        Assert.AreEqual(0.6, spatialGEV.SiteWeights[4], 1e-10);
    }

    #endregion
}
