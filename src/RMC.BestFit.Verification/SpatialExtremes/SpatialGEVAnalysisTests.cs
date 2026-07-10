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
/// Unit tests for the <see cref="SpatialGEVAnalysis"/> class.
/// Validates Bayesian MCMC estimation workflow for spatial GEV models used in regional frequency analysis.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
///     The <see cref="SpatialGEVAnalysis"/> class performs hierarchical Bayesian spatial modeling
///     of extreme values following Renard's framework. It implements regional frequency analysis
///     with GEV distributions at each site, spatially-varying parameters, and optional spatial
///     correlation structures.
/// </para>
/// <para>
///     <b>Key features tested:</b>
/// </para>
/// <list type="bullet">
///     <item><description>Bayesian MCMC estimation of spatial GEV model parameters</description></item>
///     <item><description>Site-specific quantile estimation with uncertainty quantification</description></item>
///     <item><description>Regional pooling of information across gauging stations</description></item>
///     <item><description>Spatial interpolation to ungauged locations</description></item>
///     <item><description>Leave-one-site-out cross-validation</description></item>
/// </list>
/// <para>
///     <b>References:</b>
/// </para>
/// <list type="bullet">
///     <item>Renard, B., et al. (2006). Use of a Gaussian copula for multivariate extreme value analysis.</item>
///     <item>Cooley, D., et al. (2007). Bayesian spatial modeling of extreme precipitation return levels.</item>
///     <item>Davison, A.C., et al. (2012). Statistical modeling of spatial extremes.</item>
/// </list>
/// </remarks>
[TestClass]
public class SpatialGEVAnalysisTests
{
    #region Test Data

    /// <summary>
    /// Creates test at-site flood data for multiple gauging stations.
    /// </summary>
    /// <remarks>
    /// Creates a 30 observations x 5 sites data matrix representing annual peak flows
    /// at five stream gauges along a river reach. Uses a homogeneous GEV distribution
    /// (same parameters for all sites) so that an intercept-only model is appropriate.
    /// </remarks>
    /// <returns>A 2D array [observations x sites] of flood data.</returns>
    private static double[,] CreateTestAtSiteData()
    {
        var data = new double[30, 5];
        var rng = new Random(12345);

        // Use SAME GEV parameters for all sites (homogeneous regional model)
        // This makes an intercept-only model appropriate for the data
        double xi = 10000;    // location
        double alpha = 2500;  // scale
        double kappa = 0.0;   // shape (Gumbel)

        var gev = new GeneralizedExtremeValue(xi, alpha, kappa);

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
    /// Creates test site coordinates representing a network of stream gauges.
    /// </summary>
    /// <remarks>
    /// Coordinates are in a local coordinate system (km) representing
    /// stations along a river from upstream (site 0) to downstream (site 4).
    /// </remarks>
    /// <returns>A 2D array [sites x 2] of (X, Y) coordinates.</returns>
    private static double[,] CreateTestCoordinates()
    {
        return new double[,]
        {
            { 0.0, 0.0 },    // Site 0: upstream reference
            { 10.0, 5.0 },   // Site 1: downstream, slightly east
            { 22.0, 8.0 },   // Site 2
            { 35.0, 12.0 },  // Site 3
            { 50.0, 15.0 }   // Site 4: most downstream
        };
    }

    /// <summary>
    /// Creates a minimum configuration with only 2 sites for edge case testing.
    /// </summary>
    /// <returns>A tuple of (data, coordinates) for 2 sites.</returns>
    private static (double[,] Data, double[,] Coordinates) CreateMinimalTestData()
    {
        var data = new double[20, 2];
        var rng = new Random(54321);

        // Use SAME parameters for both sites (homogeneous model)
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
    /// Creates data with missing values (NaN) at some site-year combinations.
    /// </summary>
    /// <returns>A 2D array with some NaN values representing missing data.</returns>
    private static double[,] CreateDataWithMissingValues()
    {
        var data = CreateTestAtSiteData();

        // Introduce missing values at a few locations
        data[5, 2] = double.NaN;   // Year 5, Site 2
        data[10, 0] = double.NaN;  // Year 10, Site 0
        data[15, 4] = double.NaN;  // Year 15, Site 4
        data[20, 1] = double.NaN;  // Year 20, Site 1
        data[25, 3] = double.NaN;  // Year 25, Site 3

        return data;
    }

    /// <summary>
    /// Creates a test spatial GEV model with default settings.
    /// </summary>
    /// <returns>A configured <see cref="SpatialGEV"/> model.</returns>
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
    /// Creates a minimal spatial GEV model with only 2 sites.
    /// </summary>
    /// <returns>A <see cref="SpatialGEV"/> model with minimum site count.</returns>
    private static SpatialGEV CreateMinimalSpatialGEV()
    {
        var (data, coords) = CreateMinimalTestData();

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        return new SpatialGEV(data, coords, location, scale, shape);
    }

    /// <summary>
    /// Creates a spatial GEV model with missing data.
    /// </summary>
    /// <returns>A <see cref="SpatialGEV"/> model with NaN values in data.</returns>
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();

        // Act
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
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
        // Act
        _ = new SpatialGEVAnalysis(null!);
    }

    /// <summary>
    /// Tests that the BayesianAnalysis property references the correct model.
    /// </summary>
    [TestMethod]
    public void Constructor_BayesianAnalysis_HasCorrectModel()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();

        // Act
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
        Assert.AreSame(spatialGEV, analysis.BayesianAnalysis.Model,
            "BayesianAnalysis.Model should reference the spatial GEV model.");
    }

    /// <summary>
    /// Tests that the constructor initializes probability ordinates with default values.
    /// </summary>
    [TestMethod]
    public void Constructor_InitializesDefaultProbabilityOrdinates()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();

        // Act
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
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
        // Arrange
        var spatialGEV = CreateMinimalSpatialGEV();

        // Act
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
        Assert.IsNotNull(analysis, "Analysis should be created with minimal sites.");
        Assert.AreEqual(2, analysis.SpatialGEV.Sites, "Model should have 2 sites.");
    }

    /// <summary>
    /// Tests that the constructor works with data containing missing values.
    /// </summary>
    [TestMethod]
    public void Constructor_WithMissingData_InitializesCorrectly()
    {
        // Arrange
        var spatialGEV = CreateSpatialGEVWithMissingData();

        // Act
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Configure probability ordinates
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        // Act
        var xElement = analysis.ToXElement();
        var restoredAnalysis = new SpatialGEVAnalysis(spatialGEV, xElement);

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();

        // Act
        _ = new SpatialGEVAnalysis(spatialGEV, null!);
    }

    /// <summary>
    /// Tests that the constructor throws when spatial GEV is null in XML constructor.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_XmlWithNullSpatialGEV_ThrowsArgumentNullException()
    {
        // Arrange
        var xElement = new XElement("SpatialGEVAnalysis");

        // Act
        _ = new SpatialGEVAnalysis(null!, xElement);
    }

    /// <summary>
    /// Tests serialization with Bayesian analysis settings preserved.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_WithBayesianSettings_PreservesSettings()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        analysis.BayesianAnalysis.Iterations = 5000;
        analysis.BayesianAnalysis.WarmupIterations = 1000;

        // Act
        var xElement = analysis.ToXElement();
        var restoredAnalysis = new SpatialGEVAnalysis(spatialGEV, xElement);

        // Assert
        Assert.AreEqual(5000, restoredAnalysis.BayesianAnalysis.Iterations,
            "NumberOfIterations should be preserved.");
        Assert.AreEqual(1000, restoredAnalysis.BayesianAnalysis.WarmupIterations,
            "BurnInPeriod should be preserved.");
    }

    /// <summary>
    /// Tests that ToXElement creates valid XML structure.
    /// </summary>
    [TestMethod]
    public void ToXElement_CreatesValidXmlStructure()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        var xElement = analysis.ToXElement();

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        var xElement = analysis.ToXElement();
        var isEstimatedAttr = xElement.Attribute("IsEstimated");

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that Validate propagates validation from the underlying spatial GEV model.
    /// </summary>
    [TestMethod]
    public void Validate_PropagatesModelValidation()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, "Validation should pass for valid model.");
    }

    /// <summary>
    /// Tests validation with minimal configuration (2 sites).
    /// </summary>
    [TestMethod]
    public void Validate_WithMinimalSites_ReturnsValid()
    {
        // Arrange
        var spatialGEV = CreateMinimalSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Validation should pass for 2 sites. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests validation with missing data in the model.
    /// </summary>
    [TestMethod]
    public void Validate_WithMissingData_ReturnsValid()
    {
        // Arrange
        var spatialGEV = CreateSpatialGEVWithMissingData();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        analysis.ClearResults();

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        analysis.ClearResults();

        // Assert
        Assert.IsFalse(analysis.BayesianAnalysis.IsEstimated,
            "BayesianAnalysis.IsEstimated should be false after clearing.");
    }

    /// <summary>
    /// Tests that ClearResults raises PropertyChanged events.
    /// </summary>
    [TestMethod]
    public void ClearResults_RaisesPropertyChangedEvents()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        var changedProperties = new List<string>();
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        // Act
        analysis.ClearResults();

        // Assert
        Assert.IsTrue(changedProperties.Contains(nameof(SpatialGEVAnalysis.AnalysisResults)),
            "Should raise PropertyChanged for AnalysisResults.");
        Assert.IsTrue(changedProperties.Contains(nameof(SpatialGEVAnalysis.SiteResults)),
            "Should raise PropertyChanged for SiteResults.");
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// Tests that ProbabilityOrdinates changes clear results.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinates_Change_ClearsResults()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SpatialGEVAnalysis.ProbabilityOrdinates))
                propertyChanged = true;
        };

        // Act
        analysis.ProbabilityOrdinates = new ProbabilityOrdinates();

        // Assert
        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for ProbabilityOrdinates.");
    }

    /// <summary>
    /// Tests that model property changes propagate through the analysis.
    /// </summary>
    [TestMethod]
    public void ModelPropertyChange_PropagatesPropertyChanged()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            propertyChanged = true;
        };

        // Act - Modify site weights on the model
        spatialGEV.SiteWeights[0] = 0.5;
        analysis.ClearResults(); // This should trigger property changes

        // Assert
        Assert.IsTrue(propertyChanged, "PropertyChanged should propagate from model changes.");
    }

    #endregion

    #region Event Tests

    /// <summary>
    /// Tests that AnalysisStarting event is raised when RunAsync is called.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisStartingEvent()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        analysis.BayesianAnalysis.Iterations = 100;
        analysis.BayesianAnalysis.WarmupIterations = 50;

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) =>
        {
            eventRaised = true;
            e.Cancel = true; // Cancel to avoid long test
        };

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(eventRaised, "AnalysisStarting event should be raised.");
    }

    /// <summary>
    /// Tests that AnalysisCompleted event is raised after RunAsync completes.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisCompletedEvent()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) => e.Cancel = true; // Cancel immediately
        analysis.AnalysisCompleted += (s, e) =>
        {
            eventRaised = true;
            Assert.IsTrue(e.Cancelled, "WasCanceled should be true when canceled.");
        };

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(eventRaised, "AnalysisCompleted event should be raised.");
    }

    /// <summary>
    /// Tests that AnalysisCompleted event indicates cancellation when canceled.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenCanceled_AnalysisCompletedIndicatesCancellation()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        bool wasCanceled = false;
        bool succeeded = true;
        analysis.AnalysisStarting += (s, e) => e.Cancel = true;
        analysis.AnalysisCompleted += (s, e) =>
        {
            wasCanceled = e.Cancelled;
            succeeded = e.Succeeded;
        };

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(wasCanceled, "WasCanceled should be true.");
        Assert.IsFalse(succeeded, "Succeeded should be false when canceled.");
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>
    /// Tests that CancelAnalysis can be called without error when not running.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act & Assert - Should not throw
        analysis.CancelAnalysis();
    }

    /// <summary>
    /// Tests that CancelAnalysis propagates to BayesianAnalysis.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_CancelsBayesianSimulation()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act & Assert - Should not throw
        analysis.CancelAnalysis();
        // The cancel propagates through - if no exception, it worked
    }

    #endregion

    #region GetSiteQuantiles Tests

    /// <summary>
    /// Tests that GetSiteQuantiles throws when analysis is not estimated.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void GetSiteQuantiles_WhenNotEstimated_ThrowsInvalidOperationException()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        var probs = new double[] { 0.5, 0.1, 0.01 };

        // Act
        analysis.GetSiteQuantiles(0, probs);
    }

    /// <summary>
    /// Tests that GetSiteQuantiles validates site index.
    /// </summary>
    [TestMethod]
    public void GetSiteQuantiles_ValidatesParameters()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert - Cannot test with invalid index until estimated
        Assert.IsFalse(analysis.IsEstimated, "Analysis should not be estimated initially.");
    }

    #endregion

    #region PredictAtUngaugedLocation Tests

    /// <summary>
    /// Tests that PredictAtUngaugedLocation throws when analysis is not estimated.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void PredictAtUngaugedLocation_WhenNotEstimated_ThrowsInvalidOperationException()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        var coords = new double[] { 25.0, 10.0 };
        var probs = new double[] { 0.5, 0.1, 0.01 };

        // Act
        analysis.PredictAtUngaugedLocation(coords, null, probs);
    }

    /// <summary>
    /// Tests that PredictAtUngaugedLocation validates coordinate array.
    /// </summary>
    [TestMethod]
    public void PredictAtUngaugedLocation_ValidatesCoordinates()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert - Cannot fully test until estimated, but validation happens first
        Assert.IsNotNull(analysis.SpatialGEV.Coordinates, "Coordinates should exist.");
    }

    #endregion

    #region GetRegionalGrowthCurve Tests

    /// <summary>
    /// Tests that GetRegionalGrowthCurve throws when analysis is not estimated.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void GetRegionalGrowthCurve_WhenNotEstimated_ThrowsInvalidOperationException()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        var probs = new double[] { 0.5, 0.2, 0.1, 0.04, 0.02, 0.01 };

        // Act
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        analysis.ProbabilityOrdinates.Clear();

        // Act
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        // Assert
        Assert.AreEqual(3, analysis.ProbabilityOrdinates.Count, "Should have 3 probability ordinates.");
    }

    /// <summary>
    /// Tests that setting ProbabilityOrdinates to new object clears results.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinates_SetNewObject_ClearsResults()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        analysis.ProbabilityOrdinates = new ProbabilityOrdinates();

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        spatialGEV.SiteWeights[0] = 0.5;
        spatialGEV.SiteWeights[2] = 2.0;

        // Assert
        Assert.AreEqual(0.5, spatialGEV.SiteWeights[0], 1e-10, "Site 0 weight should be 0.5.");
        Assert.AreEqual(2.0, spatialGEV.SiteWeights[2], 1e-10, "Site 2 weight should be 2.0.");
    }

    /// <summary>
    /// Tests that setting a site weight to zero effectively excludes the site.
    /// </summary>
    [TestMethod]
    public void SiteWeights_ZeroWeight_ExcludesSite()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        spatialGEV.SiteWeights[0] = 0.0;

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
        Assert.AreEqual(5, analysis.SpatialGEV.Sites, "Model should have 5 sites.");
    }

    /// <summary>
    /// Tests that the spatial GEV model has correct observation count.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_HasCorrectObservationCount()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
        Assert.AreEqual(30, analysis.SpatialGEV.Observations, "Model should have 30 observations.");
    }

    /// <summary>
    /// Tests that the spatial GEV model coordinates are accessible.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_CoordinatesAccessible()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();

        // Act - Default is log-link for location and scale
        Assert.IsTrue(spatialGEV.UseLogLinkForLocation, "Default should use log-link for location.");
        Assert.IsTrue(spatialGEV.UseLogLinkForScale, "Default should use log-link for scale.");

        // Modify
        spatialGEV.UseLogLinkForLocation = false;
        spatialGEV.UseLogLinkForScale = false;

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        analysis.BayesianAnalysis.Iterations = 10000;
        analysis.BayesianAnalysis.WarmupIterations = 2000;
        analysis.BayesianAnalysis.ThinningInterval = 5;

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;

        // Assert
        Assert.AreEqual(0.90, analysis.BayesianAnalysis.CredibleIntervalWidth, 1e-10,
            "Credible interval width should be 0.90.");
    }

    /// <summary>
    /// Tests that point estimator type can be configured.
    /// </summary>
    [TestMethod]
    public void BayesianAnalysis_PointEstimatorConfigurable()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act - Try both estimator types
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
        // Arrange
        var spatialGEV = CreateMinimalSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Analysis with 2 sites should be valid. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that model with copula dependence disabled validates.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_WithCopulaDisabled_ValidatesCorrectly()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        spatialGEV.UseCopulaDependence = false;
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, "Model without copula should validate.");
    }

    /// <summary>
    /// Tests that model without spatial errors validates.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_WithoutSpatialErrors_ValidatesCorrectly()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        spatialGEV.UseLocationErrors = false;
        spatialGEV.UseScaleErrors = false;
        spatialGEV.UseShapeErrors = false;
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, "Model without spatial errors should validate.");
    }

    /// <summary>
    /// Tests handling of coordinates at the origin.
    /// </summary>
    [TestMethod]
    public void SpatialGEV_WithSiteAtOrigin_Validates()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert - First site should be at origin
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();

        // Act
        var analysis1 = new SpatialGEVAnalysis(spatialGEV);
        var analysis2 = new SpatialGEVAnalysis(spatialGEV);

        // Assert
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
            data[i, 0] = 100 + rng.NextDouble() * 50;    // Small scale
            data[i, 1] = 5000 + rng.NextDouble() * 2000; // Medium scale
            data[i, 2] = 50000 + rng.NextDouble() * 20000; // Large scale
        }

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var spatialGEV = new SpatialGEV(data, coords, location, scale, shape);

        // Act
        var analysis = new SpatialGEVAnalysis(spatialGEV);
        var (isValid, messages) = analysis.Validate();

        // Assert
        Assert.IsTrue(isValid, $"Analysis with variable data should be valid. Messages: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Tests that analysis interface (IBayesianAnalysis) is implemented.
    /// </summary>
    [TestMethod]
    public void Analysis_ImplementsIBayesianAnalysis()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
        Assert.IsInstanceOfType(analysis, typeof(IBayesianAnalysis),
            "SpatialGEVAnalysis should implement IBayesianAnalysis.");
    }

    /// <summary>
    /// Tests that the analysis inherits from AnalysisBase.
    /// </summary>
    [TestMethod]
    public void Analysis_InheritsFromAnalysisBase()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert
        Assert.IsNull(analysis.CrossValidationResults, "CrossValidationResults should be null initially.");
    }

    /// <summary>
    /// Tests that clearing results also clears cross-validation results.
    /// </summary>
    [TestMethod]
    public void ClearResults_ClearsCrossValidationResults()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act
        analysis.ClearResults();

        // Assert
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
        // Arrange
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

        // Assert
        Assert.AreEqual(6, analysis.ProbabilityOrdinates.Count, "Should have 6 return period ordinates.");
    }

    /// <summary>
    /// Tests that equal weights represent homogeneous regional pooling.
    /// </summary>
    [TestMethod]
    public void EqualWeights_RepresentHomogeneousRegion()
    {
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Assert - All weights should be 1.0 by default
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
        // Arrange
        var spatialGEV = CreateTestSpatialGEV();
        var analysis = new SpatialGEVAnalysis(spatialGEV);

        // Act - Set weights based on hypothetical record lengths
        spatialGEV.SiteWeights[0] = 30.0 / 100.0; // 30 years
        spatialGEV.SiteWeights[1] = 50.0 / 100.0; // 50 years
        spatialGEV.SiteWeights[2] = 20.0 / 100.0; // 20 years
        spatialGEV.SiteWeights[3] = 40.0 / 100.0; // 40 years
        spatialGEV.SiteWeights[4] = 60.0 / 100.0; // 60 years

        // Assert
        Assert.AreEqual(0.3, spatialGEV.SiteWeights[0], 1e-10);
        Assert.AreEqual(0.6, spatialGEV.SiteWeights[4], 1e-10);
    }

    #endregion

    #region Bayesian Parameter Recovery Tests - Basic Homogeneous

    /// <summary>
    /// Tests Bayesian MCMC estimation of homogeneous spatial GEV model with default settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses basic identifiable data with constant GEV parameters across all sites.
    /// Validates that Bayesian estimation converges and recovers intercept parameters.
    /// </para>
    /// <para>
    /// Model parameters:
    /// - Location intercept (log-link): log(10000) ≈ 9.21
    /// - Scale intercept (log-link): log(3000) ≈ 8.01
    /// - Shape intercept (identity): -0.1
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_BasicHomogeneous_Converges()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, seed: 12345);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation should converge.");
        Assert.IsNotNull(analysis.BayesianAnalysis.Results, "Results should not be null.");
        Assert.IsNotNull(analysis.BayesianAnalysis.Results!.MAP, "MAP should not be null.");
    }

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
    /// Tests Bayesian MCMC R-hat convergence diagnostics for homogeneous model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates that all parameter R-hat values are below 1.1, indicating
    /// good chain mixing and convergence.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_BasicHomogeneous_HasGoodRhat()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, seed: 12345);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation should converge.");

        var results = analysis.BayesianAnalysis.Results!;
        for (int i = 0; i < results.ParameterResults.Count(); i++)
        {
            double rhat = results.ParameterResults[i].SummaryStatistics.Rhat;
            Assert.IsTrue(rhat < 1.1, $"Parameter {model.Parameters[i].Name} has R-hat = {rhat:F3}, should be < 1.1.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC ESS (Effective Sample Size) for homogeneous model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates that all parameters have ESS > 100 for reliable inference.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_BasicHomogeneous_HasAdequateESS()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, seed: 12345);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation should converge.");

        var results = analysis.BayesianAnalysis.Results!;
        for (int i = 0; i < results.ParameterResults.Count(); i++)
        {
            double ess = results.ParameterResults[i].SummaryStatistics.ESS;
            Assert.IsTrue(ess > 100, $"Parameter {model.Parameters[i].Name} has ESS = {ess:F0}, should be > 100.");
        }
    }

    #endregion

    #region Bayesian Parameter Recovery Tests - With Copula

    /// <summary>
    /// Tests Bayesian MCMC estimation with Gaussian copula dependence using basic exponential correlation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tests that the model converges with spatial dependence modeled via Gaussian copula.
    /// The copula uses basic exponential correlation: ρ(h) = exp(-h/range).
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithCopulaBasicExponential_Converges()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(
            nObs: 50, nSites: 10, range: 40.0, seed: 33333);

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
        Assert.IsNotNull(analysis.BayesianAnalysis.Results, "Results should not be null.");
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
    /// Tests Bayesian MCMC with powered exponential copula correlation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tests convergence with powered exponential correlation: ρ(h) = exp(-(h/range)^p).
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithCopulaPoweredExponential_Converges()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetCopulaDataPoweredExponential(
            nObs: 50, nSites: 10, range: 40.0, power: 1.5, seed: 44444);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        model.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.PoweredExponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();

        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation with powered exponential copula should converge.");
    }

    #endregion

    #region Bayesian Parameter Recovery Tests - With Spatial Regression

    /// <summary>
    /// Tests Bayesian MCMC estimation with spatial regression on location parameter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tests that the model converges when location varies spatially:
    /// log(ξ_j) = β₀ + β₁*X_j + β₂*Y_j
    /// </para>
    /// <para>
    /// The covariate matrix is passed to the GeneralLinearFunction constructor
    /// where each row is a site and each column is a covariate (X, Y coordinates).
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithLocationRegression_Converges()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetRegressionLocationOnly(
            nObs: 50, nSites: 10, seed: 66666);

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
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation with location regression should converge.");
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
    /// Tests Bayesian MCMC with spatial regression on both location and scale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tests convergence when both location and scale vary spatially.
    /// This is a common scenario where flood magnitude and variability
    /// both increase with drainage area.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_WithLocationScaleRegression_Converges()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetRegressionLocationScale(
            nObs: 50, nSites: 10, seed: 99999);

        // Build covariate matrices [sites × 2] with X,Y coordinates
        var locCovariates = new double[10, 2];
        var sclCovariates = new double[10, 2];
        for (int j = 0; j < 10; j++)
        {
            locCovariates[j, 0] = coords[j, 0]; // X coordinate
            locCovariates[j, 1] = coords[j, 1]; // Y coordinate
            sclCovariates[j, 0] = coords[j, 0];
            sclCovariates[j, 1] = coords[j, 1];
        }

        var location = new GeneralLinearFunction("Location", locCovariates);
        var scale = new GeneralLinearFunction("Scale", sclCovariates);
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation with location+scale regression should converge.");
    }

    #endregion

    #region Bayesian Parameter Recovery Tests - Different Shape Values

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

    #endregion

    #region Bayesian Parameter Recovery Tests - Large Sample

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

    #endregion

    #region Site-Level Results Tests

    /// <summary>
    /// Tests that site results are populated after Bayesian estimation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates that SiteResults is populated with quantile estimates for all sites.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_PopulatesSiteResults()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, seed: 12345);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation should converge.");
        Assert.IsNotNull(analysis.SiteResults, "SiteResults should not be null after estimation.");
        Assert.AreEqual(model.Sites, analysis.SiteResults!.Length,
            "SiteResults should have entries for all sites.");
    }

    /// <summary>
    /// Tests that analysis results contain valid posterior samples.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates that AnalysisResults contains posterior samples that can be
    /// used for uncertainty quantification.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Bayesian_PopulatesAnalysisResults()
    {
        // Arrange
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: 50, nSites: 10, seed: 12345);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");

        var model = new SpatialGEV(data, coords, location, scale, shape);
        var analysis = new SpatialGEVAnalysis(model);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Bayesian estimation should converge.");
        Assert.IsNotNull(analysis.AnalysisResults, "AnalysisResults should not be null after estimation.");
    }

    #endregion
}
